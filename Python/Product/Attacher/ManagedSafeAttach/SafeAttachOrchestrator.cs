// Skeleton orchestrator for managed safe attach (Phase 2B/2C implementation + cache + heuristic stop-bit)
// Adds offsets discovery + parsing + basic thread state discovery + memory write sequence (gated by env var).
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using Microsoft.PythonTools.Debugging.Shared;
using Microsoft.PythonTools.Debugging.Shared.SafeAttach;
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools.Debugger.ManagedSafeAttach {
    /// <summary>
    /// Abstraction for a target process used by the managed safe attach orchestrator.
    /// Provides minimal operations needed: module enumeration, memory read and write.
    /// </summary>
    public interface ISafeAttachProcess { // changed to public for test access
        int Pid { get; }
        IntPtr Handle { get; }
        bool EnumerateModules(Func<string, IntPtr, int, bool> onModule);
        bool Read(ulong address, byte[] buffer, int size);
        bool Write(ulong address, byte[] buffer, int size);
    }

    /// <summary>
    /// Real process implementation using Win32 Toolhelp + Read/WriteProcessMemory.
    /// </summary>
    internal sealed class RealSafeAttachProcess : ISafeAttachProcess {
        private readonly int _pid; private readonly IntPtr _handle;
        public RealSafeAttachProcess(int pid, IntPtr handle) { _pid = pid; _handle = handle; }
        public int Pid => _pid; public IntPtr Handle => _handle;
        public bool EnumerateModules(Func<string, IntPtr, int, bool> onModule) => SafeAttachOrchestrator.EnumerateModulesInternal(_pid, onModule);
        public bool Read(ulong address, byte[] buffer, int size) => SafeAttachOrchestrator.ReadFullyInternal(_handle, new IntPtr((long)address), buffer, size);
        public bool Write(ulong address, byte[] buffer, int size) => SafeAttachOrchestrator.WriteFullyInternal(_handle, new IntPtr((long)address), buffer, size);
    }

    /// <summary>
    /// Orchestrates managed safe attach for CPython 3.14+ using PEP 768 _Py_DebugOffsets.
    /// Performs module discovery, offsets slab parsing, optional interpreter walk inference,
    /// thread state resolution, script injection and eval breaker modification.
    /// </summary>
    public static class SafeAttachOrchestrator { // changed from internal to public
        private static readonly Regex _pyDllRegex = new Regex(@"^python3(\d{2,3})(?:_d)?\.dll$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private const int HEADER_READ = 0x800; // larger header read for export directory
        private const string PY_RUNTIME_SECTION = "PyRuntim"; // truncated section name
        private const string EXPORT_TSTATE_CURRENT = "_PyThreadState_Current"; // data symbol pointer to current tstate
        private const string EXPORT_TSTATE_UNCHECKED_GET = "_PyThreadState_UncheckedGet"; // function returning current tstate (fallback)
        private const uint IMAGE_DIRECTORY_ENTRY_EXPORT = 0; // export table index
        private static readonly uint[] STOP_BIT_CANDIDATES = new uint[] { 0x1, 0x2, 0x4, 0x8 }; // heuristic candidate bits (RT1 will refine)
        private const ulong MAX_SCRIPT_PATH_SIZE = 0x100000; // allow up to 1MB (flex fallback may report non-canonical sizes)
        private const ulong MAX_REMOTE_SUPPORT_OFFSET = 0x4000;
        private const int MAX_RUNTIME_SECTION_READ = 0x20000; // 128KB safety cap

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, IntPtr nSize, out IntPtr lpNumberOfBytesRead);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, IntPtr nSize, out IntPtr lpNumberOfBytesWritten);

        private class CacheEntry { public ulong ThreadState; public ulong PyBase; public uint Version; public DateTime Stamp; }
        private static readonly ConcurrentDictionary<int, CacheEntry> _tstateCache = new ConcurrentDictionary<int, CacheEntry>();

        /// <summary>
        /// Entry point for production paths given a real OS process handle.
        /// </summary>
        public static SafeAttachResult TryManagedSafeAttach(IntPtr hProcess, int pid) {
            if (hProcess == IntPtr.Zero) return FailTelemetry(pid, SafeAttachFailureSite.OpenProcess, "null handle", exportBypassed: false);
            return TryManagedSafeAttach(new RealSafeAttachProcess(pid, hProcess));
        }

        /// <summary>
        /// Core orchestrator logic operating on an abstract process implementation.
        /// Returns a success result only after all memory writes (path, pending flag, eval breaker) succeed.
        /// </summary>
        internal static SafeAttachResult TryManagedSafeAttach(ISafeAttachProcess proc) {
            int pid = proc.Pid;
            // Mixed-mode guard: allow opt-out if Concord / native mixed mode signalled
            if (Environment.GetEnvironmentVariable("PTVS_MIXED_MODE") == "1") {
                Debug.WriteLine($"[PTVS][ManagedSafeAttach] Bypassing managed safe attach due to PTVS_MIXED_MODE for pid={pid}");
                return FailTelemetry(pid, SafeAttachFailureSite.VersionGate, "mixed mode bypass", exportBypassed: true);
            }
            var cfg = new SafeAttachConfig(Verbose); // snapshot env vars once
            var mem = new ProcessMemory(proc); // new helper
            try {
                // 1. Locate python3XY.dll module
                IntPtr pyBase = IntPtr.Zero; int pySize = 0; int minor = -1;
                if (!proc.EnumerateModules((name, baseAddr, size) => {
                    var m = _pyDllRegex.Match(name);
                    if (m.Success && int.TryParse(m.Groups[1].Value, out minor) && minor >= 14) { pyBase = baseAddr; pySize = size; return true; }
                    return false;
                })) {
                    return FailTelemetry(pid, SafeAttachFailureSite.VersionGate, "python < 3.14 or not found", exportBypassed: false);
                }

                // 2. Resolve _Py_DebugOffsets start + section size
                ulong offsetsAddr; uint runtimeSize;
                (offsetsAddr, runtimeSize) = LocateDebugOffsetsAndSize(proc, pyBase, pySize);
                if (offsetsAddr == 0) {
                    return FailTelemetry(pid, SafeAttachFailureSite.OffsetsAddressResolution, "_Py_DebugOffsets not found", exportBypassed: false);
                }

                // 3. Read slab
                uint readSize = runtimeSize == 0 ? (uint)4096 : runtimeSize;
                if (readSize > MAX_RUNTIME_SECTION_READ) readSize = MAX_RUNTIME_SECTION_READ;
                byte[] slab = new byte[readSize];
                if (!mem.TryRead(offsetsAddr, slab)) {
                    return FailTelemetry(pid, SafeAttachFailureSite.OffsetsRead, "read fail", exportBypassed: false);
                }
                if (cfg.Verbose) {
                    string first32 = DebugOffsetsParser.Hex(slab, 32);
                    bool cookieOk = true; for (int i = 0; i < DebugOffsetsParser.Cookie.Length && i < slab.Length; i++) if (slab[i] != (byte)DebugOffsetsParser.Cookie[i]) { cookieOk = false; break; }
                    Debug.WriteLine($"[PTVS][ManagedSafeAttach] OffsetsAddr=0x{offsetsAddr:X} size={readSize} first32={first32} cookieOk={cookieOk}");
                }

                // 3a. Parse with automatic flex fallback retry
                ParsedDebugOffsets parsed; string fail;
                bool parsedOk = DebugOffsetsParser.TryParse(slab, (ulong)pyBase.ToInt64(), mem.PointerSize, out parsed, out fail);
                if (!parsedOk && fail == "no valid layout" && Environment.GetEnvironmentVariable("PTVS_SAFE_ATTACH_FLEX_FALLBACK") != "1") {
                    // Force flex fallback for retry
                    Environment.SetEnvironmentVariable("PTVS_SAFE_ATTACH_FLEX_FALLBACK", "1");
                    if (cfg.Verbose) Debug.WriteLine("[PTVS][ManagedSafeAttach] Retrying parse with forced flex fallback");
                    parsedOk = DebugOffsetsParser.TryParse(slab, (ulong)pyBase.ToInt64(), mem.PointerSize, out parsed, out fail);
                }
                if (!parsedOk) {
                    return FailTelemetry(pid, SafeAttachFailureSite.OffsetsParse, fail, exportBypassed: false);
                }
                if (cfg.Verbose) Debug.WriteLine($"[PTVS][ManagedSafeAttach] Parsed offsets ver=0x{parsed.Version:X} evalBreakerOff=0x{parsed.EvalBreaker:X} supportOff=0x{parsed.RemoteSupport:X} pendingOff=0x{parsed.PendingCall:X} scriptPathOff=0x{parsed.ScriptPath:X} size={parsed.ScriptPathSize} flexNonCanonical={(parsed.ScriptPathSize!=512)}");
                if (parsed.RemoteDebugDisabled) {
                    return FailTelemetry(pid, SafeAttachFailureSite.PolicyDisabled, "remote debug disabled", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded, exportBypassed: true);
                }

                bool version314Plus = ((parsed.Version >> 24) & 0xFF) == 3 && ((parsed.Version >> 16) & 0xFF) >= 14;
                bool bypassExport = version314Plus && !cfg.AllowExport;
                bool exportBypassed = bypassExport;
                bool allowHeuristic = !cfg.HeuristicDisabled;

                // Sanity checks (relaxed for non-canonical size)
                if (parsed.RemoteSupport == 0 || parsed.RemoteSupport > MAX_REMOTE_SUPPORT_OFFSET) {
                    return FailTelemetry(pid, SafeAttachFailureSite.OffsetsParse, $"remote_support offset implausible (0x{parsed.RemoteSupport:X})", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded, exportBypassed);
                }
                if (parsed.ScriptPathSize < 512 || parsed.ScriptPathSize > MAX_SCRIPT_PATH_SIZE || parsed.ScriptPath >= parsed.ScriptPathSize) {
                    return FailTelemetry(pid, SafeAttachFailureSite.OffsetsParse, $"script_path tuple invalid (path=0x{parsed.ScriptPath:X} size={parsed.ScriptPathSize})", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded, exportBypassed);
                }
                if (parsed.PendingCall >= parsed.ScriptPathSize || (parsed.PendingCall % 4) != 0 || parsed.ScriptPath <= parsed.PendingCall) {
                    return FailTelemetry(pid, SafeAttachFailureSite.OffsetsParse, $"pending/script ordering invalid (pending=0x{parsed.PendingCall:X} path=0x{parsed.ScriptPath:X})", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded, exportBypassed);
                }

                // 4. Attempt interpreter walk inference if not already provided
                if (!parsed.HasInterpreterWalk) {
                    if (InterpreterWalkLocator.TryInferOffsets(proc, offsetsAddr, slab, mem.PointerSize, ref parsed, out var whyNot, out var _)) {
                        if (cfg.Verbose) Debug.WriteLine("[PTVS][ManagedSafeAttach] Inferred interpreter walk offsets from slab");
                    } else if (cfg.Verbose) Debug.WriteLine("[PTVS][ManagedSafeAttach] Could not infer interpreter walk offsets: " + whyNot);
                }

                // 5. Thread state discovery sequence: forced -> cache -> walk -> heuristic -> export
                ulong tstatePtr = 0;
                if (!string.IsNullOrEmpty(cfg.ForcedTStateHex) && ulong.TryParse(cfg.ForcedTStateHex, System.Globalization.NumberStyles.HexNumber, null, out var forcedVal)) tstatePtr = forcedVal;

                if (tstatePtr == 0 && !cfg.DisableCache) {
                    if (_tstateCache.TryGetValue(pid, out var ce) && ce.PyBase == (ulong)pyBase.ToInt64() && ce.Version == parsed.Version) {
                        if (ThreadStateCacheValidator.Validate(ce.ThreadState, parsed, mem.PointerSize, (addr, buffer) => proc.Read(addr, buffer, buffer.Length))) tstatePtr = ce.ThreadState;
                        else if (cfg.Verbose) Debug.WriteLine("[PTVS][ManagedSafeAttach] Cache invalid; rediscovering");
                    }
                }

                if (tstatePtr == 0 && parsed.HasInterpreterWalk) {
                    tstatePtr = InterpreterWalkLocator.FindThreadState(proc, offsetsAddr, parsed, mem.PointerSize, c => ThreadStateValidator.Validate(c, parsed, proc, cfg.Verbose));
                    if (cfg.Verbose && tstatePtr != 0) Debug.WriteLine($"[PTVS][ManagedSafeAttach] Walk located tstate=0x{tstatePtr:X}");
                }

                if (tstatePtr == 0 && allowHeuristic && cfg.ForceHeuristic) {
                    if (cfg.Verbose) Debug.WriteLine("[PTVS][ManagedSafeAttach] Attempting heuristic scan");
                    tstatePtr = HeuristicThreadStateLocator.TryLocate(0, parsed.RemoteSupport, parsed.ScriptPath, parsed.ScriptPathSize, mem.PointerSize, (addr, hb) => proc.Read(addr, hb, hb.Length));
                }

                if (tstatePtr == 0 && !bypassExport && cfg.AllowExportFallback) {
                    var exportTstate = LocateThreadStateCurrent(proc, pyBase, pySize);
                    if (exportTstate != 0) tstatePtr = exportTstate;
                }

                if (tstatePtr != 0 && !ThreadStateValidator.Validate(tstatePtr, parsed, proc, cfg.Verbose)) tstatePtr = 0;
                if (tstatePtr == 0) {
                    return FailTelemetry(pid, SafeAttachFailureSite.ThreadStateDiscovery, "thread state unresolved", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded, exportBypassed);
                }
                _tstateCache[pid] = new CacheEntry { ThreadState = tstatePtr, PyBase = (ulong)pyBase.ToInt64(), Version = parsed.Version, Stamp = DateTime.UtcNow };

                if (!cfg.WriteEnabled) {
                    return FailTelemetry(pid, SafeAttachFailureSite.ScriptBufferWrite, "write gate disabled", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded, exportBypassed);
                }

                // 6. Write phase
                ulong supportBase = tstatePtr + parsed.RemoteSupport;
                ulong scriptPathBufAddr = supportBase + parsed.ScriptPath;
                ulong pendingFlagAddr = supportBase + parsed.PendingCall;
                ulong evalBreakerAddr = tstatePtr + parsed.EvalBreaker;

                // Ensure PTVS_DEBUG_PORT is explicitly set (even if dynamic 0) so loader doesn't see <null>.
                string chosenPort = Environment.GetEnvironmentVariable("PTVS_DEBUG_PORT");
                if (string.IsNullOrEmpty(chosenPort)) {
                    // If host hasn't provided a port, default to dynamic selection (0).
                    chosenPort = "0";
                    Environment.SetEnvironmentVariable("PTVS_DEBUG_PORT", chosenPort);
                }
                Debug.WriteLine($"[PTVS][ManagedSafeAttach] Using PTVS_DEBUG_PORT={chosenPort}");

                // For dynamic port (0) create / assign a port file so the actual listener port can be discovered.
                try {
                    var portFile = Environment.GetEnvironmentVariable("PTVS_DEBUG_PORT_FILE");
                    if (chosenPort == "0") {
                        if (string.IsNullOrEmpty(portFile)) {
                            portFile = Path.Combine(Path.GetTempPath(), $"ptvs_attach_{pid}.port");
                            Environment.SetEnvironmentVariable("PTVS_DEBUG_PORT_FILE", portFile);
                            Debug.WriteLine($"[PTVS][ManagedSafeAttach] Assigned PTVS_DEBUG_PORT_FILE='{portFile}' for dynamic port");
                        }
                    } else if (!string.IsNullOrEmpty(portFile)) {
                        // Fixed port scenario: remove the port file env to avoid ambiguity.
                        Environment.SetEnvironmentVariable("PTVS_DEBUG_PORT_FILE", null);
                        Debug.WriteLine("[PTVS][ManagedSafeAttach] Removed PTVS_DEBUG_PORT_FILE for fixed port attach");
                    }
                } catch { }

                byte[] probeBuf = new byte[32];
                if (!mem.TryRead(scriptPathBufAddr, probeBuf)) {
                    return FailTelemetry(pid, SafeAttachFailureSite.ScriptBufferWrite, "script buffer unreadable", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded, exportBypassed);
                }

                // Simplified loader resolution: prefer only safe_attach_loader.py.
                // Optional legacy fallback (ptvsd_loader.py) enabled only when PTVS_SAFE_ATTACH_ALLOW_LEGACY=1.
                string loaderPath = ResolveLoaderPath();
                if (Verbose) Debug.WriteLine($"[PTVS][ManagedSafeAttach] Loader resolution final: {(string.IsNullOrEmpty(loaderPath)?"<none>":loaderPath)}");
                if (string.IsNullOrEmpty(loaderPath)) {
                    return FailTelemetry(pid, SafeAttachFailureSite.ScriptBufferWrite, "loader file not found", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded, exportBypassed);
                }

                // If we have a fixed port (non-zero) we cannot set env vars inside the already running target process
                // so generate a small wrapper that sets them before invoking the real loader.
                if (chosenPort != "0") {
                    try {
                        string hostEnv = Environment.GetEnvironmentVariable("PTVS_DEBUG_HOST") ?? "127.0.0.1";
                        bool breakFlag = (Environment.GetEnvironmentVariable("PTVS_DEBUG_BREAK") == "1") || (Environment.GetEnvironmentVariable("PTVS_WAIT_FOR_CLIENT") == "1");
                        string wrapperPath = Path.Combine(Path.GetTempPath(), $"safe_attach_wrapper_{pid}.py");
                        var sbw = new StringBuilder();
                        sbw.AppendLine("# Auto-generated safe attach wrapper");
                        sbw.AppendLine("import os, runpy");
                        sbw.AppendLine($"os.environ['PTVS_DEBUG_HOST'] = '{hostEnv}'");
                        sbw.AppendLine($"os.environ['PTVS_DEBUG_PORT'] = '{chosenPort}'");
                        if (breakFlag) {
                            sbw.AppendLine("os.environ.setdefault('PTVS_WAIT_FOR_CLIENT','1')");
                            sbw.AppendLine("os.environ.setdefault('PTVS_DEBUG_BREAK','1')");
                        }
                        sbw.AppendLine($"runpy.run_path(r'{loaderPath}', run_name='__main__')");
                        File.WriteAllText(wrapperPath, sbw.ToString(), Encoding.UTF8);
                        loaderPath = wrapperPath;
                        Debug.WriteLine($"[PTVS][ManagedSafeAttach] Using wrapper loader {wrapperPath} host={hostEnv} port={chosenPort} break={breakFlag}");
                    } catch (Exception exWrap) {
                        Debug.WriteLine("[PTVS][ManagedSafeAttach] Wrapper generation failed: " + exWrap.Message);
                    }
                }

                if (!SafeAttachUtilities.TryPrepareLoaderBuffer(loaderPath, parsed.ScriptPathSize, out var scriptWrite, out var truncated)) {
                    return FailTelemetry(pid, SafeAttachFailureSite.ScriptBufferWrite, "loader buffer prep failed", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded, exportBypassed);
                }
                if (scriptWrite.Length > (long)parsed.ScriptPathSize) {
                    return FailTelemetry(pid, SafeAttachFailureSite.ScriptBufferWrite, "prepared buffer exceeds script_path_size", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded, exportBypassed);
                }
                if (!proc.Write(scriptPathBufAddr, scriptWrite, scriptWrite.Length)) {
                    return FailTelemetry(pid, SafeAttachFailureSite.ScriptBufferWrite, "script write failed", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded, exportBypassed);
                }
                if (!proc.Write(pendingFlagAddr, new byte[] { 1, 0, 0, 0 }, 4)) {
                    return FailTelemetry(pid, SafeAttachFailureSite.PendingFlagWrite, "pending flag write failed", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded, exportBypassed);
                }

                const uint EVAL_BREAKER_MASK = 0x20;
                bool breakerOk = false;
                if (version314Plus) {
                    var brBuf = new byte[4];
                    if (!mem.TryRead(evalBreakerAddr, brBuf)) {
                        return FailTelemetry(pid, SafeAttachFailureSite.EvalBreakerWrite, "eval breaker read failed", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded, exportBypassed);
                    }
                    uint brVal = BitConverter.ToUInt32(brBuf, 0);
                    if ((brVal & EVAL_BREAKER_MASK) == 0) {
                        brVal |= EVAL_BREAKER_MASK;
                        var newBr = BitConverter.GetBytes(brVal);
                        if (!proc.Write(evalBreakerAddr, newBr, newBr.Length)) {
                            return FailTelemetry(pid, SafeAttachFailureSite.EvalBreakerWrite, "eval breaker write failed", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded, exportBypassed);
                        }
                    }
                    breakerOk = true;
                }
                if (!breakerOk && !version314Plus) {
                    byte[] breakerBuf = new byte[mem.PointerSize];
                    StopBitSelection sel = EvalBreakerHelper.DetermineMask(
                        readBreaker: () => mem.TryRead(evalBreakerAddr, breakerBuf),
                        getValue: () => mem.PointerSize == 8 ? (ulong)BitConverter.ToUInt64(breakerBuf, 0) : BitConverter.ToUInt32(breakerBuf, 0),
                        candidateMasks: STOP_BIT_CANDIDATES,
                        defaultMask: STOP_BIT_CANDIDATES[0]);
                    ulong breakerVal = mem.PointerSize == 8 ? BitConverter.ToUInt64(breakerBuf, 0) : BitConverter.ToUInt32(breakerBuf, 0);
                    if (!sel.AlreadySet) {
                        breakerVal |= sel.Mask;
                        byte[] newBreaker = mem.PointerSize == 8 ? BitConverter.GetBytes(breakerVal) : BitConverter.GetBytes((uint)breakerVal);
                        if (!proc.Write(evalBreakerAddr, newBreaker, newBreaker.Length)) {
                            return FailTelemetry(pid, SafeAttachFailureSite.EvalBreakerWrite, "eval breaker write failed", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded, exportBypassed);
                        }
                    }
                }

                var ok = SafeAttachResult.Ok(parsed.Version, parsed.FreeThreaded, parsed.RemoteDebugDisabled, reused: _tstateCache.ContainsKey(pid), truncated: truncated, exportBypassed: exportBypassed);
                Debug.WriteLine($"[PTVS][ManagedSafeAttach] SUCCESS pid={pid} ver=0x{parsed.Version:X} tstate=0x{tstatePtr:X} exportBypassed={exportBypassed} size={parsed.ScriptPathSize} loader={Path.GetFileName(loaderPath)}");
                return ok;
            } catch (Exception ex) {
                Debug.WriteLine("[PTVS][ManagedSafeAttach] Orchestrator exception: " + ex);
                return FailTelemetry(proc.Pid, SafeAttachFailureSite.Unknown, ex.Message, exportBypassed: false);
            }
        }

        // Helper methods restored after refactor truncation
        private static SafeAttachResult FailTelemetry(int pid, SafeAttachFailureSite site, string msg, uint rawVersion = 0, bool disabled = false, bool freeThreaded = false, bool exportBypassed = false) {
            Debug.WriteLine($"[PTVS][ManagedSafeAttach] FAIL pid={pid} site={site} msg={msg} exportBypassed={exportBypassed}");
            return SafeAttachResult.Fail(site, msg, rawVersion, disabled, freeThreaded, exportBypassed);
        }

#if DEBUG
        private static bool Verbose => true;
#else
        private static bool Verbose => string.Equals(Environment.GetEnvironmentVariable("PTVS_SAFE_ATTACH_VERBOSE"), "1", StringComparison.Ordinal);
#endif

        private static (ulong addr, uint size) LocateDebugOffsetsAndSize(ISafeAttachProcess proc, IntPtr baseAddr, int moduleSize) {
            try {
                if (moduleSize < 0x1000) return (0, 0);
                const int HEADER_READ_LOCAL = 0x800;
                byte[] hdr = new byte[HEADER_READ_LOCAL];
                if (!proc.Read((ulong)baseAddr.ToInt64(), hdr, hdr.Length)) return (0, 0);
                if (!(hdr[0] == 'M' && hdr[1] == 'Z')) return (0, 0);
                int e_lfanew = BitConverter.ToInt32(hdr, 0x3C);
                if (e_lfanew <= 0 || e_lfanew > hdr.Length - 0x200) return (0, 0);
                if (!(hdr[e_lfanew] == 'P' && hdr[e_lfanew + 1] == 'E')) return (0, 0);
                int coff = e_lfanew + 4;
                ushort numberOfSections = BitConverter.ToUInt16(hdr, coff + 2);
                ushort optSize = BitConverter.ToUInt16(hdr, coff + 16);
                int sectionTable = coff + 20 + optSize;
                int sectionSize = 40;
                for (int i = 0; i < numberOfSections; i++) {
                    int off = sectionTable + i * sectionSize;
                    if (off + sectionSize > hdr.Length) break;
                    string name = ExtractAscii(hdr, off, 8);
                    if (name == PY_RUNTIME_SECTION) {
                        uint virtualSize = BitConverter.ToUInt32(hdr, off + 8);
                        uint virtualAddress = BitConverter.ToUInt32(hdr, off + 12);
                        uint rawSize = BitConverter.ToUInt32(hdr, off + 16);
                        uint size = Math.Max(virtualSize, rawSize);
                        if (size == 0) size = 4096;
                        return ((ulong)baseAddr.ToInt64() + virtualAddress, size);
                    }
                }
                // fallback scan for cookie
                int scanSize = Math.Min(moduleSize, 2 * 1024 * 1024);
                byte[] scan = new byte[scanSize];
                if (proc.Read((ulong)baseAddr.ToInt64(), scan, scan.Length)) {
                    for (int i = 0; i <= scan.Length - DebugOffsetsParser.Cookie.Length; i++) {
                        bool match = true;
                        for (int j = 0; j < DebugOffsetsParser.Cookie.Length; j++) {
                            if (scan[i + j] != (byte)DebugOffsetsParser.Cookie[j]) { match = false; break; }
                        }
                        if (match) return ((ulong)baseAddr.ToInt64() + (ulong)i, 16384);
                    }
                }
            } catch (Exception ex) { Debug.WriteLine("[PTVS][ManagedSafeAttach] LocateDebugOffsetsAndSize exception: " + ex.Message); }
            return (0, 0);
        }

        private static ulong LocateThreadStateCurrent(ISafeAttachProcess proc, IntPtr baseAddr, int moduleSize) {
            try {
                ulong moduleBase = (ulong)baseAddr.ToInt64();
                bool Read(ulong addr, byte[] buffer, int size) => proc.Read(addr, buffer, size);
                return ThreadStateExportResolver.TryGetCurrentThreadState(Read, moduleBase, IntPtr.Size);
            } catch (Exception ex) { Debug.WriteLine("[PTVS][ManagedSafeAttach] LocateThreadStateCurrent exception: " + ex.Message); }
            return 0;
        }

        internal static bool ReadFullyInternal(IntPtr hProcess, IntPtr address, byte[] buffer, int size) {
            IntPtr read; if (!ReadProcessMemory(hProcess, address, buffer, (IntPtr)size, out read)) return false; return read.ToInt64() == size;
        }
        internal static bool WriteFullyInternal(IntPtr hProcess, IntPtr address, byte[] buffer, int size) {
            IntPtr written; if (!WriteProcessMemory(hProcess, address, buffer, (IntPtr)size, out written)) return false; return written.ToInt64() == size;
        }

        internal static bool EnumerateModulesInternal(int pid, Func<string, IntPtr, int, bool> onModule) {
            IntPtr snap = NativeMethods.CreateToolhelp32Snapshot(SnapshotFlags.Module, (uint)pid);
            if (snap == NativeMethods.INVALID_HANDLE_VALUE) return false;
            try {
                uint sz = (uint)Marshal.SizeOf(typeof(MODULEENTRY32));
                var me = new MODULEENTRY32 { dwSize = sz };
                if (NativeMethods.Module32First(snap, ref me)) {
                    do {
                        string name = me.szModule ?? string.Empty;
                        if (onModule(name, me.modBaseAddr, (int)me.modBaseSize)) return true;
                        me.dwSize = sz;
                    } while (NativeMethods.Module32Next(snap, ref me));
                }
            } finally { NativeMethods.CloseHandle(snap); }
            return false;
        }

        private static string ExtractAscii(byte[] data, int offset, int length) {
            int end = offset + length; if (end > data.Length) end = data.Length;
            int realEnd = offset; while (realEnd < end && data[realEnd] != 0) realEnd++;
            return Encoding.ASCII.GetString(data, offset, realEnd - offset);
        }

        private static string ResolveLoaderPath() {
            // 1. Explicit override (for tests / custom scenarios)
            try {
                var overridePath = Environment.GetEnvironmentVariable("PTVS_SAFE_ATTACH_LOADER_OVERRIDE");
                if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath)) {
                    return overridePath;
                }
            } catch { }

            // 2. Normal preferred loader
            string path = PythonToolsInstallPath.TryGetFile("safe_attach_loader.py");
            if (!string.IsNullOrEmpty(path)) return path;

            return string.Empty;
        }
    }
}

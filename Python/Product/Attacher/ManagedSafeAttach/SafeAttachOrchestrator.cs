// Skeleton orchestrator for managed safe attach (Phase 2B/2C implementation + cache + heuristic stop-bit)
// Adds offsets discovery + parsing + basic thread state discovery + memory write sequence (gated by env var).
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
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
        private const ulong MAX_SCRIPT_PATH_SIZE = 4096; // spec nominal 512; allow headroom
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
            var cfg = new SafeAttachConfig(Verbose); // snapshot env vars once
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
                if (!proc.Read(offsetsAddr, slab, slab.Length)) {
                    return FailTelemetry(pid, SafeAttachFailureSite.OffsetsRead, "read fail", exportBypassed: false);
                }
                if (cfg.Verbose) {
                    string first32 = DebugOffsetsParser.Hex(slab, 32);
                    bool cookieOk = true; for (int i = 0; i < DebugOffsetsParser.Cookie.Length && i < slab.Length; i++) if (slab[i] != (byte)DebugOffsetsParser.Cookie[i]) { cookieOk = false; break; }
                    Debug.WriteLine($"[PTVS][ManagedSafeAttach] OffsetsAddr=0x{offsetsAddr:X} size={readSize} first32={first32} cookieOk={cookieOk}");
                }

                if (!DebugOffsetsParser.TryParse(slab, (ulong)pyBase.ToInt64(), IntPtr.Size, out var parsed, out var fail)) {
                    return FailTelemetry(pid, SafeAttachFailureSite.OffsetsParse, fail, exportBypassed: false);
                }
                if (cfg.Verbose) Debug.WriteLine($"[PTVS][ManagedSafeAttach] Parsed offsets ver=0x{parsed.Version:X} evalBreakerOff=0x{parsed.EvalBreaker:X} supportOff=0x{parsed.RemoteSupport:X} pendingOff=0x{parsed.PendingCall:X} scriptPathOff=0x{parsed.ScriptPath:X} size={parsed.ScriptPathSize} hasInterpWalk=0x{parsed.HasInterpreterWalk}");
                if (parsed.RemoteDebugDisabled) {
                    return FailTelemetry(pid, SafeAttachFailureSite.PolicyDisabled, "remote debug disabled", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded, exportBypassed: true);
                }

                bool version314Plus = ((parsed.Version >> 24) & 0xFF) == 3 && ((parsed.Version >> 16) & 0xFF) >= 14;
                bool bypassExport = version314Plus && !cfg.AllowExport;
                bool exportBypassed = bypassExport;
                bool allowHeuristic = !cfg.HeuristicDisabled;

                // Sanity checks
                if (parsed.RemoteSupport == 0 || parsed.RemoteSupport > MAX_REMOTE_SUPPORT_OFFSET) {
                    return FailTelemetry(pid, SafeAttachFailureSite.OffsetsParse, $"remote_support offset implausible (0x{parsed.RemoteSupport:X})", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded, exportBypassed);
                }
                if (parsed.ScriptPathSize != 512 || parsed.ScriptPath >= parsed.ScriptPathSize) {
                    return FailTelemetry(pid, SafeAttachFailureSite.OffsetsParse, $"script_path tuple invalid (path=0x{parsed.ScriptPath:X} size={parsed.ScriptPathSize})", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded, exportBypassed);
                }
                if (parsed.PendingCall >= parsed.ScriptPathSize || (parsed.PendingCall % 4) != 0 || parsed.ScriptPath <= parsed.PendingCall) {
                    return FailTelemetry(pid, SafeAttachFailureSite.OffsetsParse, $"pending/script ordering invalid (pending=0x{parsed.PendingCall:X} path=0x{parsed.ScriptPath:X})", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded, exportBypassed);
                }

                // 4. Attempt interpreter walk inference if not already provided
                if (!parsed.HasInterpreterWalk) {
                    if (InterpreterWalkLocator.TryInferOffsets(proc, offsetsAddr, slab, IntPtr.Size, ref parsed, out var whyNot, out var _)) {
                        if (cfg.Verbose) Debug.WriteLine("[PTVS][ManagedSafeAttach] Inferred interpreter walk offsets from slab");
                    } else if (cfg.Verbose) Debug.WriteLine("[PTVS][ManagedSafeAttach] Could not infer interpreter walk offsets: " + whyNot);
                }

                // 5. Thread state discovery sequence: forced -> cache -> walk -> heuristic -> export
                ulong tstatePtr = 0;
                if (!string.IsNullOrEmpty(cfg.ForcedTStateHex) && ulong.TryParse(cfg.ForcedTStateHex, System.Globalization.NumberStyles.HexNumber, null, out var forcedVal)) tstatePtr = forcedVal;

                if (tstatePtr == 0 && !cfg.DisableCache) {
                    if (_tstateCache.TryGetValue(pid, out var ce) && ce.PyBase == (ulong)pyBase.ToInt64() && ce.Version == parsed.Version) {
                        if (ThreadStateCacheValidator.Validate(ce.ThreadState, parsed, IntPtr.Size, (addr, buffer) => proc.Read(addr, buffer, buffer.Length))) tstatePtr = ce.ThreadState;
                        else if (cfg.Verbose) Debug.WriteLine("[PTVS][ManagedSafeAttach] Cache invalid; rediscovering");
                    }
                }

                if (tstatePtr == 0 && parsed.HasInterpreterWalk) {
                    tstatePtr = InterpreterWalkLocator.FindThreadState(proc, offsetsAddr, parsed, IntPtr.Size, c => ThreadStateValidator.Validate(c, parsed, proc, cfg.Verbose));
                    if (cfg.Verbose && tstatePtr != 0) Debug.WriteLine($"[PTVS][ManagedSafeAttach] Walk located tstate=0x{tstatePtr:X}");
                }

                if (tstatePtr == 0 && allowHeuristic && cfg.ForceHeuristic) {
                    if (cfg.Verbose) Debug.WriteLine("[PTVS][ManagedSafeAttach] Attempting heuristic scan");
                    tstatePtr = HeuristicThreadStateLocator.TryLocate(0, parsed.RemoteSupport, parsed.ScriptPath, parsed.ScriptPathSize, IntPtr.Size, (addr, hb) => proc.Read(addr, hb, hb.Length));
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

                byte[] probeBuf = new byte[32];
                if (!proc.Read(scriptPathBufAddr, probeBuf, probeBuf.Length)) {
                    return FailTelemetry(pid, SafeAttachFailureSite.ScriptBufferWrite, "script buffer unreadable", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded, exportBypassed);
                }

                string loaderPath = PythonToolsInstallPath.GetFile("ptvsd_loader.py") ?? PythonToolsInstallPath.GetFile("ptvsd\\ptvsd_loader.py");
                if (string.IsNullOrEmpty(loaderPath)) {
                    try {
                        string temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ptvs_bootstrap.py");
                        if (!System.IO.File.Exists(temp)) System.IO.File.WriteAllText(temp, "import debugpy; debugpy.listen(('127.0.0.1',0));\n");
                        loaderPath = temp;
                    } catch { loaderPath = null; }
                }
                if (string.IsNullOrEmpty(loaderPath)) {
                    return FailTelemetry(pid, SafeAttachFailureSite.ScriptBufferWrite, "no loader file path", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded, exportBypassed);
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
                    if (!proc.Read(evalBreakerAddr, brBuf, brBuf.Length)) {
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
                    byte[] breakerBuf = new byte[IntPtr.Size];
                    StopBitSelection sel = EvalBreakerHelper.DetermineMask(
                        readBreaker: () => proc.Read(evalBreakerAddr, breakerBuf, breakerBuf.Length),
                        getValue: () => IntPtr.Size == 8 ? (ulong)BitConverter.ToUInt64(breakerBuf, 0) : BitConverter.ToUInt32(breakerBuf, 0),
                        candidateMasks: STOP_BIT_CANDIDATES,
                        defaultMask: STOP_BIT_CANDIDATES[0]);
                    ulong breakerVal = IntPtr.Size == 8 ? BitConverter.ToUInt64(breakerBuf, 0) : BitConverter.ToUInt32(breakerBuf, 0);
                    if (!sel.AlreadySet) {
                        breakerVal |= sel.Mask;
                        byte[] newBreaker = IntPtr.Size == 8 ? BitConverter.GetBytes(breakerVal) : BitConverter.GetBytes((uint)breakerVal);
                        if (!proc.Write(evalBreakerAddr, newBreaker, newBreaker.Length)) {
                            return FailTelemetry(pid, SafeAttachFailureSite.EvalBreakerWrite, "eval breaker write failed", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded, exportBypassed);
                        }
                    }
                }

                var ok = SafeAttachResult.Ok(parsed.Version, parsed.FreeThreaded, parsed.RemoteDebugDisabled, reused: _tstateCache.ContainsKey(pid), truncated: truncated, exportBypassed: exportBypassed);
                Debug.WriteLine($"[PTVS][ManagedSafeAttach] SUCCESS pid={pid} ver=0x{parsed.Version:X} tstate=0x{tstatePtr:X} exportBypassed={exportBypassed}");
                return ok;
            } catch (Exception ex) {
                Debug.WriteLine("[PTVS][ManagedSafeAttach] Orchestrator exception: " + ex);
                return FailTelemetry(proc.Pid, SafeAttachFailureSite.Unknown, ex.Message, exportBypassed: false);
            }
        }

        /// <summary>
        /// Reads a native pointer sized value from the target process.
        /// Returns 0 if address is null or read fails.
        /// </summary>
        private static ulong ReadPointer(ISafeAttachProcess proc, ulong address, int pointerSize) {
            if (address == 0) return 0;
            var tmp = new byte[pointerSize];
            if (!proc.Read(address, tmp, pointerSize)) return 0;
            return pointerSize == 8 ? BitConverter.ToUInt64(tmp, 0) : BitConverter.ToUInt32(tmp, 0);
        }

        /// <summary>
        /// Creates a failure result and logs a debug line with site + message.
        /// </summary>
        private static SafeAttachResult FailTelemetry(int pid, SafeAttachFailureSite site, string msg, uint rawVersion = 0, bool disabled = false, bool freeThreaded = false, bool exportBypassed = false) {
            Debug.WriteLine($"[PTVS][ManagedSafeAttach] FAIL pid={pid} site={site} msg={msg} exportBypassed={exportBypassed}");
            return SafeAttachResult.Fail(site, msg, rawVersion, disabled, freeThreaded, exportBypassed);
        }

        private static bool DisableCache() => EnvVarTrue("PTVS_SAFE_ATTACH_MANAGED_NO_CACHE");
        private static bool EnvVarTrue(string name) => string.Equals(Environment.GetEnvironmentVariable(name), "1", StringComparison.Ordinal);
#if DEBUG
        private static bool Verbose => true;
#else
        private static bool Verbose => EnvVarTrue("PTVS_SAFE_ATTACH_VERBOSE");
#endif
        #region ThreadState / Export helpers
        /// <summary>
        /// Attempts to resolve current PyThreadState via exported TLS symbols (legacy path / fallback).
        /// </summary>
        private static ulong LocateThreadStateCurrent(ISafeAttachProcess proc, IntPtr baseAddr, int moduleSize) {
            try {
                ulong moduleBase = (ulong)baseAddr.ToInt64();
                bool Read(ulong addr, byte[] buffer, int size) => proc.Read(addr, buffer, size);
                return ThreadStateExportResolver.TryGetCurrentThreadState(Read, moduleBase, IntPtr.Size);
            } catch (Exception ex) { Debug.WriteLine("[PTVS][ManagedSafeAttach] LocateThreadStateCurrent exception: " + ex.Message); }
            return 0;
        }
        #endregion

        public static SafeAttachResult LegacyProbeOnly(IntPtr hProcess, int pid) => SafeAttachResult.Fail(SafeAttachFailureSite.ThreadStateDiscovery, "legacy stub");

        /// <summary>
        /// Enumerates modules via Toolhelp snapshot. Stops when callback returns true.
        /// </summary>
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

        /// <summary>
        /// Locates the _Py_DebugOffsets slab either via section lookup of the PyRuntim section or cookie scan fallback.
        /// Returns (0,0) on failure.
        /// </summary>
        private static (ulong addr, uint size) LocateDebugOffsetsAndSize(ISafeAttachProcess proc, IntPtr baseAddr, int moduleSize) {
            try {
                if (moduleSize < 0x1000) return (0, 0);
                byte[] hdr = new byte[HEADER_READ];
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
                // Fallback: scan module for cookie
                int scanSize = Math.Min(moduleSize, 2 * 1024 * 1024);
                byte[] scan = new byte[scanSize];
                if (proc.Read((ulong)baseAddr.ToInt64(), scan, scan.Length)) {
                    for (int i = 0; i <= scan.Length - DebugOffsetsParser.Cookie.Length; i++) {
                        bool match = true;
                        for (int j = 0; j < DebugOffsetsParser.Cookie.Length; j++) { if (scan[i + j] != (byte)DebugOffsetsParser.Cookie[j]) { match = false; break; } }
                        if (match) return ((ulong)baseAddr.ToInt64() + (ulong)i, 16384);
                    }
                }
            } catch (Exception ex) { Debug.WriteLine("[PTVS][ManagedSafeAttach] LocateDebugOffsetsAndSize exception: " + ex.Message); }
            return (0, 0);
        }

        /// <summary>
        /// Reads memory fully from target process; returns true only if requested size read.
        /// </summary>
        internal static bool ReadFullyInternal(IntPtr hProcess, IntPtr address, byte[] buffer, int size) {
            IntPtr read; if (!ReadProcessMemory(hProcess, address, buffer, (IntPtr)size, out read)) return false; return read.ToInt64() == size;
        }
        /// <summary>
        /// Writes memory fully to target process; returns true only if requested size written.
        /// </summary>
        internal static bool WriteFullyInternal(IntPtr hProcess, IntPtr address, byte[] buffer, int size) {
            IntPtr written; if (!WriteProcessMemory(hProcess, address, buffer, (IntPtr)size, out written)) return false; return written.ToInt64() == size;
        }

        private static string ExtractAscii(byte[] data, int offset, int length) {
            int end = offset + length; if (end > data.Length) end = data.Length;
            int realEnd = offset; while (realEnd < end && data[realEnd] != 0) realEnd++;
            return Encoding.ASCII.GetString(data, offset, realEnd - offset);
        }
    }
}

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
    internal static class SafeAttachOrchestrator {
        private static readonly Regex _pyDllRegex = new Regex(@"python3(\d{2})(?:_d)?\.dll", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private const int HEADER_READ = 0x800; // larger header read for export directory
        private const string PY_RUNTIME_SECTION = "PyRuntim"; // truncated section name
        private const string EXPORT_TSTATE_CURRENT = "_PyThreadState_Current"; // data symbol pointer to current tstate
        private const uint IMAGE_DIRECTORY_ENTRY_EXPORT = 0; // export table index
        private static readonly uint[] STOP_BIT_CANDIDATES = new uint[] { 0x1, 0x2, 0x4, 0x8 }; // heuristic candidate bits (RT1 will refine)

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, IntPtr nSize, out IntPtr lpNumberOfBytesRead);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, IntPtr nSize, out IntPtr lpNumberOfBytesWritten);

        private class CacheEntry { public ulong ThreadState; public ulong PyBase; public uint Version; public DateTime Stamp; }
        private static readonly ConcurrentDictionary<int, CacheEntry> _tstateCache = new ConcurrentDictionary<int, CacheEntry>();

        public static SafeAttachResult TryManagedSafeAttach(IntPtr hProcess, int pid) {
            var attemptStart = Stopwatch.StartNew();
            try {
                if (hProcess == IntPtr.Zero) return FailTelemetry(pid, SafeAttachFailureSite.OpenProcess, "null handle");

                // 1. Locate python3XY.dll module
                IntPtr pyBase = IntPtr.Zero; int pySize = 0; int minor = -1;
                if (!EnumerateModules(pid, (name, baseAddr, size) => {
                    var m = _pyDllRegex.Match(name);
                    if (m.Success && int.TryParse(m.Groups[1].Value, out minor) && minor >= 14) { pyBase = baseAddr; pySize = size; return true; }
                    return false;
                })) {
                    return FailTelemetry(pid, SafeAttachFailureSite.VersionGate, "python < 3.14 or not found");
                }

                // 2. Resolve _Py_DebugOffsets
                ulong offsetsAddr = LocateDebugOffsets(hProcess, pyBase, pySize);
                if (offsetsAddr == 0) {
                    return FailTelemetry(pid, SafeAttachFailureSite.OffsetsAddressResolution, "_Py_DebugOffsets not found");
                }

                // 3. Read & parse offsets
                byte[] buf = new byte[128];
                if (!ReadFully(hProcess, new IntPtr((long)offsetsAddr), buf, buf.Length)) {
                    return FailTelemetry(pid, SafeAttachFailureSite.OffsetsRead, "read fail");
                }
                if (!DebugOffsetsParser.TryParse(buf, (ulong)pyBase.ToInt64(), IntPtr.Size, out var parsed, out var fail)) {
                    return FailTelemetry(pid, SafeAttachFailureSite.OffsetsParse, fail);
                }
                if (parsed.RemoteDebugDisabled) {
                    return FailTelemetry(pid, SafeAttachFailureSite.PolicyDisabled, "remote debug disabled", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded);
                }

                // 4. Thread state discovery with cache reuse
                ulong tstatePtr = 0;
                if (!DisableCache()) {
                    if (_tstateCache.TryGetValue(pid, out var ce) && ce.PyBase == (ulong)pyBase.ToInt64() && ce.Version == parsed.Version) {
                        tstatePtr = ce.ThreadState;
                        if (!ValidateThreadState(hProcess, tstatePtr, parsed)) {
                            tstatePtr = 0; // force rediscovery
                        } else {
                            Debug.WriteLine($"[PTVS][ManagedSafeAttach] Reusing cached tstate 0x{tstatePtr:X}");
                        }
                    }
                }
                if (tstatePtr == 0) {
                    tstatePtr = LocateThreadStateCurrent(hProcess, pyBase, pySize);
                    if (tstatePtr == 0) {
                        return FailTelemetry(pid, SafeAttachFailureSite.ThreadStateDiscovery, "_PyThreadState_Current unresolved", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded);
                    }
                    _tstateCache[pid] = new CacheEntry { ThreadState = tstatePtr, PyBase = (ulong)pyBase.ToInt64(), Version = parsed.Version, Stamp = DateTime.UtcNow };
                }

                // 5. Optional: if write gate disabled, stop here with failure so legacy fallback happens (unless FORCE success requested)
                bool performWrites = EnvVarTrue("PTVS_SAFE_ATTACH_MANAGED_WRITE");
                bool forceSuccess = EnvVarTrue("PTVS_SAFE_ATTACH_MANAGED_FORCE");
                if (!performWrites && !forceSuccess) {
                    return FailTelemetry(pid, SafeAttachFailureSite.ScriptBufferWrite, "write gate disabled", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded);
                }

                // 6. Compute remote support structure addresses
                ulong remoteSupportAddr = tstatePtr + parsed.RemoteSupport; // offset inside PyThreadState
                ulong scriptPathBufAddr = remoteSupportAddr + parsed.ScriptPath;
                ulong pendingFlagAddr = remoteSupportAddr + parsed.PendingCall;
                ulong evalBreakerAddr = parsed.EvalBreaker; // already normalized in parser

                // 7. Build script path string
                string loaderPath = PythonToolsInstallPath.GetFile("ptvsd_loader.py") ?? PythonToolsInstallPath.GetFile("ptvsd\\ptvsd_loader.py");
                if (string.IsNullOrEmpty(loaderPath)) loaderPath = "import debugpy";
                byte[] scriptBytes = Encoding.UTF8.GetBytes(loaderPath);
                ulong maxScriptBytes = parsed.ScriptPathSize > 0 ? parsed.ScriptPathSize : (ulong)scriptBytes.Length;
                bool truncated = false;
                if ((ulong)scriptBytes.Length >= maxScriptBytes) {
                    truncated = true;
                    int newLen = (int)Math.Max(0, (long)maxScriptBytes - 1);
                    if (newLen < scriptBytes.Length) Array.Resize(ref scriptBytes, newLen);
                }
                byte[] scriptWrite = new byte[scriptBytes.Length + 1]; // null terminate
                Array.Copy(scriptBytes, scriptWrite, scriptBytes.Length);
                if (!WriteFully(hProcess, new IntPtr((long)scriptPathBufAddr), scriptWrite, scriptWrite.Length)) {
                    return FailTelemetry(pid, SafeAttachFailureSite.ScriptBufferWrite, "script write failed", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded);
                }

                // 8. Set pending flag
                if (!WriteFully(hProcess, new IntPtr((long)pendingFlagAddr), new byte[] { 1 }, 1)) {
                    return FailTelemetry(pid, SafeAttachFailureSite.PendingFlagWrite, "pending flag write failed", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded);
                }

                // 9. Set eval breaker stop bit with dynamic mask selection
                uint mask = SelectStopBitMask(hProcess, evalBreakerAddr);
                int ptrSize = IntPtr.Size;
                byte[] breakerBuf = new byte[ptrSize];
                if (!ReadFully(hProcess, new IntPtr((long)evalBreakerAddr), breakerBuf, breakerBuf.Length)) {
                    return FailTelemetry(pid, SafeAttachFailureSite.EvalBreakerWrite, "eval breaker read failed", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded);
                }
                ulong breakerVal = ptrSize == 8 ? BitConverter.ToUInt64(breakerBuf, 0) : BitConverter.ToUInt32(breakerBuf, 0);
                if ((breakerVal & mask) == 0) breakerVal |= mask; // only set if not already set
                byte[] newBreaker = ptrSize == 8 ? BitConverter.GetBytes(breakerVal) : BitConverter.GetBytes((uint)breakerVal);
                if (!WriteFully(hProcess, new IntPtr((long)evalBreakerAddr), newBreaker, newBreaker.Length)) {
                    return FailTelemetry(pid, SafeAttachFailureSite.EvalBreakerWrite, "eval breaker write failed", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded);
                }

                attemptStart.Stop();
                var ok = SafeAttachResult.Ok(parsed.Version, parsed.FreeThreaded, parsed.RemoteDebugDisabled, tstatePtr != 0 && _tstateCache.ContainsKey(pid), truncated);
                Debug.WriteLine($"[PTVS][ManagedSafeAttach] SUCCESS pid={pid} ver=0x{parsed.Version:X} tstate=0x{tstatePtr:X} mask=0x{mask:X} truncated={truncated} elapsedMs={attemptStart.ElapsedMilliseconds}");
                return ok;
            } catch (Exception ex) {
                Debug.WriteLine("[PTVS][ManagedSafeAttach] Orchestrator exception: " + ex);
                return FailTelemetry(pid, SafeAttachFailureSite.Unknown, ex.Message);
            }
        }

        private static SafeAttachResult FailTelemetry(int pid, SafeAttachFailureSite site, string msg, uint rawVersion=0, bool disabled=false, bool freeThreaded=false) {
            Debug.WriteLine($"[PTVS][ManagedSafeAttach] FAIL pid={pid} site={site} msg={msg}");
            return SafeAttachResult.Fail(site, msg, rawVersion, disabled, freeThreaded);
        }

        private static bool DisableCache() => EnvVarTrue("PTVS_SAFE_ATTACH_MANAGED_NO_CACHE");
        private static bool EnvVarTrue(string name) => string.Equals(Environment.GetEnvironmentVariable(name), "1", StringComparison.Ordinal);

        private static uint SelectStopBitMask(IntPtr hProcess, ulong evalBreakerAddr) {
            // Very naive heuristic: try candidate masks and pick first that is not already set.
            // Future (RT1): derive from runtime state / symbol.
            int ps = IntPtr.Size; byte[] buf = new byte[ps];
            if (!ReadFully(hProcess, new IntPtr((long)evalBreakerAddr), buf, ps)) return STOP_BIT_CANDIDATES[0];
            ulong val = ps == 8 ? BitConverter.ToUInt64(buf, 0) : BitConverter.ToUInt32(buf, 0);
            foreach (var m in STOP_BIT_CANDIDATES) { if ((val & m) == 0) return m; }
            return STOP_BIT_CANDIDATES[0];
        }

        #region ThreadState / Export helpers
        private static ulong LocateThreadStateCurrent(IntPtr hProcess, IntPtr baseAddr, int moduleSize) {
            try {
                byte[] hdr = new byte[HEADER_READ];
                if (!ReadFully(hProcess, baseAddr, hdr, hdr.Length)) return 0;
                if (!(hdr[0] == 'M' && hdr[1] == 'Z')) return 0;
                int e_lfanew = BitConverter.ToInt32(hdr, 0x3C);
                if (e_lfanew <= 0 || e_lfanew + 0x90 > hdr.Length) return 0;
                if (!(hdr[e_lfanew] == 'P' && hdr[e_lfanew + 1] == 'E')) return 0;
                int optOffset = e_lfanew + 24;
                bool isPE32Plus = BitConverter.ToUInt16(hdr, optOffset) == 0x20b;
                int dataDirCountOffset = isPE32Plus ? optOffset + 108 : optOffset + 92;
                ushort numberOfRva = BitConverter.ToUInt16(hdr, dataDirCountOffset);
                int dataDirBase = isPE32Plus ? optOffset + 112 : optOffset + 96;
                if (numberOfRva <= IMAGE_DIRECTORY_ENTRY_EXPORT) return 0;
                int exportDirEntry = dataDirBase + (int)IMAGE_DIRECTORY_ENTRY_EXPORT * 8;
                uint exportRva = BitConverter.ToUInt32(hdr, exportDirEntry);
                uint exportSize = BitConverter.ToUInt32(hdr, exportDirEntry + 4);
                if (exportRva == 0 || exportSize == 0) return 0;
                byte[] exportDir = new byte[40];
                if (!ReadFully(hProcess, new IntPtr(baseAddr.ToInt64() + exportRva), exportDir, exportDir.Length)) return 0;
                uint numberOfNames = BitConverter.ToUInt32(exportDir, 24);
                uint addressOfFunctions = BitConverter.ToUInt32(exportDir, 28);
                uint addressOfNames = BitConverter.ToUInt32(exportDir, 32);
                uint addressOfNameOrdinals = BitConverter.ToUInt32(exportDir, 36);
                if (numberOfNames == 0) return 0;
                byte[] nameRvas = new byte[numberOfNames * 4];
                if (!ReadFully(hProcess, new IntPtr(baseAddr.ToInt64() + addressOfNames), nameRvas, nameRvas.Length)) return 0;
                byte[] ordinals = new byte[numberOfNames * 2];
                if (!ReadFully(hProcess, new IntPtr(baseAddr.ToInt64() + addressOfNameOrdinals), ordinals, ordinals.Length)) return 0;
                for (uint i = 0; i < numberOfNames; i++) {
                    uint nameRva = BitConverter.ToUInt32(nameRvas, (int)(i * 4));
                    string name = ReadAsciiZ(hProcess, new IntPtr(baseAddr.ToInt64() + nameRva), 128);
                    if (name == EXPORT_TSTATE_CURRENT) {
                        ushort ordinalIndex = BitConverter.ToUInt16(ordinals, (int)(i * 2));
                        byte[] funcRvaBuf = new byte[4];
                        if (!ReadFully(hProcess, new IntPtr(baseAddr.ToInt64() + addressOfFunctions + ordinalIndex * 4), funcRvaBuf, 4)) return 0;
                        uint symbolRva = BitConverter.ToUInt32(funcRvaBuf, 0);
                        ulong symbolAddr = (ulong)baseAddr.ToInt64() + symbolRva;
                        int ptrSize = IntPtr.Size; byte[] tstateBuf = new byte[ptrSize];
                        if (ReadFully(hProcess, new IntPtr((long)symbolAddr), tstateBuf, ptrSize)) {
                            ulong tstatePtr = ptrSize == 8 ? BitConverter.ToUInt64(tstateBuf, 0) : BitConverter.ToUInt32(tstateBuf, 0);
                            return tstatePtr;
                        }
                        return 0;
                    }
                }
            } catch (Exception ex) { Debug.WriteLine("[PTVS][ManagedSafeAttach] LocateThreadStateCurrent exception: " + ex.Message); }
            return 0;
        }
        #endregion

        private static bool ValidateThreadState(IntPtr hProcess, ulong tstatePtr, ParsedDebugOffsets parsed) {
            if (tstatePtr == 0) return false;
            // Basic sanity: remote support address should be readable (first 8 bytes)
            ulong remoteSupport = tstatePtr + parsed.RemoteSupport;
            byte[] probe = new byte[8];
            return ReadFully(hProcess, new IntPtr((long)remoteSupport), probe, probe.Length);
        }

        public static SafeAttachResult LegacyProbeOnly(IntPtr hProcess, int pid) => SafeAttachResult.Fail(SafeAttachFailureSite.ThreadStateDiscovery, "legacy stub");

        private static bool EnumerateModules(int pid, Func<string, IntPtr, int, bool> onModule) {
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

        private static ulong LocateDebugOffsets(IntPtr hProcess, IntPtr baseAddr, int moduleSize) {
            try {
                if (moduleSize < 0x1000) return 0;
                byte[] hdr = new byte[HEADER_READ];
                if (!ReadFully(hProcess, baseAddr, hdr, hdr.Length)) return 0;
                if (!(hdr[0] == 'M' && hdr[1] == 'Z')) return 0;
                int e_lfanew = BitConverter.ToInt32(hdr, 0x3C);
                if (e_lfanew <= 0 || e_lfanew > hdr.Length - 0x200) return 0;
                if (!(hdr[e_lfanew] == 'P' && hdr[e_lfanew + 1] == 'E')) return 0;
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
                        uint virtualAddress = BitConverter.ToUInt32(hdr, off + 12);
                        return (ulong)baseAddr.ToInt64() + virtualAddress;
                    }
                }
                int scanSize = Math.Min(moduleSize, 2 * 1024 * 1024);
                const string cookie = DebugOffsetsParser.Cookie;
                byte[] scan = new byte[scanSize];
                if (ReadFully(hProcess, baseAddr, scan, scan.Length)) {
                    for (int i = 0; i <= scan.Length - cookie.Length; i++) {
                        bool match = true;
                        for (int j = 0; j < cookie.Length; j++) { if (scan[i + j] != (byte)cookie[j]) { match = false; break; } }
                        if (match) return (ulong)baseAddr.ToInt64() + (ulong)i;
                    }
                }
            } catch (Exception ex) { Debug.WriteLine("[PTVS][ManagedSafeAttach] LocateDebugOffsets exception: " + ex.Message); }
            return 0;
        }

        private static bool ReadFully(IntPtr hProcess, IntPtr address, byte[] buffer, int size) {
            IntPtr read;
            if (!ReadProcessMemory(hProcess, address, buffer, (IntPtr)size, out read)) return false;
            return read.ToInt64() == size;
        }

        private static bool WriteFully(IntPtr hProcess, IntPtr address, byte[] buffer, int size) {
            IntPtr written;
            if (!WriteProcessMemory(hProcess, address, buffer, (IntPtr)size, out written)) return false;
            return written.ToInt64() == size;
        }

        private static string ExtractAscii(byte[] data, int offset, int length) {
            int end = offset + length; if (end > data.Length) end = data.Length;
            int realEnd = offset; while (realEnd < end && data[realEnd] != 0) realEnd++;
            return Encoding.ASCII.GetString(data, offset, realEnd - offset);
        }

        private static string ReadAsciiZ(IntPtr hProcess, IntPtr address, int maxLen) {
            byte[] tmp = new byte[maxLen];
            if (!ReadFully(hProcess, address, tmp, maxLen)) return string.Empty;
            int len = 0; while (len < maxLen && tmp[len] != 0) len++;
            return Encoding.ASCII.GetString(tmp, 0, len);
        }
    }
}

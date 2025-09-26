// Skeleton orchestrator for managed safe attach (Phase 2B partial implementation)
// Adds offsets discovery + parsing. Memory write & thread state discovery remain TODO.
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Debugging.Shared;
using Microsoft.PythonTools.Debugging.Shared.SafeAttach;

namespace Microsoft.PythonTools.Debugger.ManagedSafeAttach {
    internal static class SafeAttachOrchestrator {
        private static readonly Regex _pyDllRegex = new Regex(@"python3(\d{2})(?:_d)?\.dll", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private const int HEADER_READ = 0x200; // read enough for section headers
        private const string PY_RUNTIME_SECTION = "PyRuntim"; // truncated section name

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, IntPtr nSize, out IntPtr lpNumberOfBytesRead);

        public static SafeAttachResult TryManagedSafeAttach(IntPtr hProcess, int pid) {
            try {
                if (hProcess == IntPtr.Zero) return SafeAttachResult.Fail(SafeAttachFailureSite.OpenProcess, "null handle");

                // 1. Locate python3XY.dll module
                IntPtr pyBase = IntPtr.Zero; int pySize = 0; int minor = -1;
                if (!EnumerateModules(pid, (name, baseAddr, size) => {
                    var m = _pyDllRegex.Match(name);
                    if (m.Success && int.TryParse(m.Groups[1].Value, out minor) && minor >= 14) {
                        pyBase = baseAddr; pySize = size; return true; // stop
                    }
                    return false;
                })) {
                    return SafeAttachResult.Fail(SafeAttachFailureSite.VersionGate, "python < 3.14 or not found");
                }

                // 2. Resolve address of _Py_DebugOffsets (PEP 768) by scanning sections
                ulong offsetsAddr = LocateDebugOffsets(hProcess, pyBase, pySize);
                if (offsetsAddr == 0) {
                    return SafeAttachResult.Fail(SafeAttachFailureSite.OffsetsAddressResolution, "_Py_DebugOffsets not found");
                }

                // 3. Read first 128 bytes & parse
                byte[] buf = new byte[128];
                if (!ReadFully(hProcess, new IntPtr((long)offsetsAddr), buf, buf.Length)) {
                    return SafeAttachResult.Fail(SafeAttachFailureSite.OffsetsRead, "read fail");
                }
                if (!DebugOffsetsParser.TryParse(buf, (ulong)pyBase.ToInt64(), IntPtr.Size, out var parsed, out var fail)) {
                    return SafeAttachResult.Fail(SafeAttachFailureSite.OffsetsParse, fail);
                }
                if (parsed.RemoteDebugDisabled) {
                    return SafeAttachResult.Fail(SafeAttachFailureSite.PolicyDisabled, "remote debug disabled", parsed.Version, true, parsed.FreeThreaded);
                }

                // 4. (TODO MA2) Thread state discovery -> currently not implemented
                // 5. (TODO MA3) Memory writes (script path, pending flag, eval breaker)
                // For now we allow a FORCE success path for iterative development & telemetry validation.
                bool force = Environment.GetEnvironmentVariable("PTVS_SAFE_ATTACH_MANAGED_FORCE") == "1";
                if (force) {
                    return SafeAttachResult.Ok(parsed.Version, parsed.FreeThreaded, parsed.RemoteDebugDisabled, false, false);
                }

                return SafeAttachResult.Fail(SafeAttachFailureSite.ThreadStateDiscovery, "thread state locator not implemented", parsed.Version, parsed.RemoteDebugDisabled, parsed.FreeThreaded);
            } catch (Exception ex) {
                Debug.WriteLine("[PTVS][ManagedSafeAttach] Orchestrator exception: " + ex);
                return SafeAttachResult.Fail(SafeAttachFailureSite.Unknown, ex.Message);
            }
        }

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
            // Strategy:
            // 1. Parse PE headers, iterate section table for "PyRuntim"; if found use section RVA as candidate.
            // 2. Fallback: brute scan first 2MB of image for ASCII cookie.
            try {
                if (moduleSize < 0x1000) return 0;
                // Read DOS + minimal PE header region
                byte[] hdr = new byte[HEADER_READ];
                if (!ReadFully(hProcess, baseAddr, hdr, hdr.Length)) return 0;
                if (!(hdr[0] == 'M' && hdr[1] == 'Z')) return 0;
                int e_lfanew = BitConverter.ToInt32(hdr, 0x3C);
                if (e_lfanew <= 0 || e_lfanew > hdr.Length - 0x100) return 0;
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

                // Fallback scan (2MB or module size, whichever smaller)
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
            } catch (Exception ex) {
                Debug.WriteLine("[PTVS][ManagedSafeAttach] LocateDebugOffsets exception: " + ex.Message);
            }
            return 0;
        }

        private static bool ReadFully(IntPtr hProcess, IntPtr address, byte[] buffer, int size) {
            IntPtr read;
            if (!ReadProcessMemory(hProcess, address, buffer, (IntPtr)size, out read)) return false;
            return read.ToInt64() == size;
        }

        private static string ExtractAscii(byte[] data, int offset, int length) {
            int end = offset + length; if (end > data.Length) end = data.Length;
            int realEnd = offset; while (realEnd < end && data[realEnd] != 0) realEnd++;
            return System.Text.Encoding.ASCII.GetString(data, offset, realEnd - offset);
        }
    }
}

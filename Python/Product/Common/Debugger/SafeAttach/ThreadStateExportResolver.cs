// Shared export-based thread state resolver for safe attach paths (managed + concord)
// Provides logic to obtain current PyThreadState* via exported data symbol or accessor function.
using System;
using System.Diagnostics;
using System.Text;

namespace Microsoft.PythonTools.Debugging.Shared.SafeAttach {
    internal static class ThreadStateExportResolver {
        private const string EXPORT_TSTATE_CURRENT = "_PyThreadState_Current";
        private const string EXPORT_TSTATE_UNCHECKED_GET = "_PyThreadState_UncheckedGet"; // legacy accessor
        private const string EXPORT_TSTATE_GETCURRENT = "_PyThreadState_GetCurrent";      // newer accessor name
#if DEBUG
        private static bool Verbose => true; // debug build: always verbose
        private static bool Diag => true;    // debug build: always diagnostic
#else
        private static bool Verbose => Environment.GetEnvironmentVariable("PTVS_SAFE_ATTACH_VERBOSE") == "1";
        private static bool Diag => Environment.GetEnvironmentVariable("PTVS_SAFE_ATTACH_MANAGED_DIAG") == "1";
#endif

        /// <summary>
        /// Attempts to resolve the current PyThreadState* using exports from the python DLL.
        /// read delegate reads 'size' bytes from absolute address into buffer.
        /// Returns 0 on failure.
        /// </summary>
        internal static ulong TryGetCurrentThreadState(Func<ulong, byte[], int, bool> read, ulong moduleBase, int pointerSize) {
            try {
                if (moduleBase == 0 || read == null) { if (Verbose) Debug.WriteLine("[PTVS][ThreadStateExportResolver] Invalid parameters (module/read)"); return 0; }
                // 1. Direct data export
                if (PeExportReader.TryGetExport(read, moduleBase, EXPORT_TSTATE_CURRENT, out var dataSym) && dataSym.Rva != 0) {
                    ulong addr = moduleBase + dataSym.Rva;
                    var buf = new byte[pointerSize];
                    if (read(addr, buf, pointerSize)) {
                        ulong ptr = pointerSize == 8 ? BitConverter.ToUInt64(buf, 0) : BitConverter.ToUInt32(buf, 0);
                        if (Verbose) Debug.WriteLine($"[PTVS][ThreadStateExportResolver] Data export {EXPORT_TSTATE_CURRENT} RVA=0x{dataSym.Rva:X} addr=0x{addr:X} value=0x{ptr:X}");
                        if (ptr != 0) return ptr;
                        if (Verbose) Debug.WriteLine("[PTVS][ThreadStateExportResolver] Data export pointer was NULL – falling back to function");
                    } else if (Verbose) {
                        Debug.WriteLine($"[PTVS][ThreadStateExportResolver] Failed reading data export address=0x{addr:X}");
                    }
                } else if (Verbose) {
                    Debug.WriteLine($"[PTVS][ThreadStateExportResolver] Data export {EXPORT_TSTATE_CURRENT} not found or RVA=0");
                }

                // 2. Function accessor fallbacks (x64 only – pattern: 48 8B 05/0D disp32 ... C3)
                if (pointerSize == 8) {
                    string[] fnExports = new[] { EXPORT_TSTATE_UNCHECKED_GET, EXPORT_TSTATE_GETCURRENT };
                    foreach (var fn in fnExports) {
                        if (!PeExportReader.TryGetExport(read, moduleBase, fn, out var funcSym) || funcSym.Rva == 0) {
                            if (Verbose) Debug.WriteLine($"[PTVS][ThreadStateExportResolver] Function export {fn} not found or RVA=0");
                            continue;
                        }
                        ulong funcAddr = moduleBase + funcSym.Rva;
                        byte[] code = new byte[256];
                        if (!read(funcAddr, code, code.Length)) {
                            if (Verbose) Debug.WriteLine($"[PTVS][ThreadStateExportResolver] Failed to read function export bytes {fn} addr=0x{funcAddr:X}");
                            continue;
                        }
                        if (Verbose) Debug.WriteLine($"[PTVS][ThreadStateExportResolver] Function export {fn} RVA=0x{funcSym.Rva:X} addr=0x{funcAddr:X} scanning={code.Length} bytes");
                        if (Diag) {
                            var sb = new StringBuilder(); sb.Append($"[PTVS][ThreadStateExportResolver] {fn} first 64 bytes:");
                            int dump = Math.Min(64, code.Length); for (int i = 0; i < dump; i++) sb.AppendFormat(" {0:X2}", code[i]);
                            Debug.WriteLine(sb.ToString());
                        }
                        for (int i = 0; i <= code.Length - 8; i++) {
                            byte b0 = code[i]; byte b1 = code[i + 1]; byte b2 = code[i + 2];
                            // look for MOV RAX,[RIP+disp32] (48 8B 05/0D xx xx xx xx) optionally followed by ... RET (C3) within a few bytes
                            bool movRipPattern = b0 == 0x48 && b1 == 0x8B && (b2 == 0x05 || b2 == 0x0D);
                            if (!movRipPattern) continue;
                            int disp = BitConverter.ToInt32(code, i + 3);
                            // We accept presence of RET within next 10 bytes but not strictly required to extract pointer
                            ulong ripAfterMov = funcAddr + (ulong)(i + 7);
                            ulong targetAddr = ripAfterMov + (ulong)disp;
                            var buf = new byte[8];
                            if (!read(targetAddr, buf, 8)) { if (Verbose) Debug.WriteLine($"[PTVS][ThreadStateExportResolver] Pattern {fn} hit @+0x{i:X} read failure target=0x{targetAddr:X}"); continue; }
                            ulong ptr = BitConverter.ToUInt64(buf, 0);
                            if (Verbose) Debug.WriteLine($"[PTVS][ThreadStateExportResolver] Pattern {fn} hit @+0x{i:X} target=0x{targetAddr:X} value=0x{ptr:X}");
                            if (ptr != 0) {
                                Debug.WriteLine($"[PTVS][ThreadStateExportResolver] {fn} pattern@+0x{i:X} tstate=0x{ptr:X}");
                                return ptr;
                            }
                        }
                        if (Verbose) Debug.WriteLine($"[PTVS][ThreadStateExportResolver] Function export pattern scan yielded no non-zero pointer for {fn}");
                    }
                }
            } catch (Exception ex) {
                Debug.WriteLine("[PTVS][ThreadStateExportResolver] Exception: " + ex.Message);
            }
            return 0;
        }
    }
}

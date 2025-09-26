// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Native;
using Microsoft.Dia;
using System.Text;
using System.Collections.Generic;

namespace Microsoft.PythonTools.Debugger.Concord {
    internal sealed class PyDebugOffsetsReader {
        public bool IsAvailable { get; private set; }
        public uint Version { get; private set; }
        public string Cookie { get; private set; } = string.Empty;
        public DebuggerSupportOffsets DebuggerSupport { get; private set; }
        public bool FreeThreaded { get; private set; }
        public bool RemoteDebugDisabled { get; private set; }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct PyDebugOffsetsStructV1A {
            public uint version; public ulong sizeof_struct; public ulong cookie64;
            public ulong r1, r2, r3, r4, r5, r6, r7, r8;
            public ulong dbg_eval_breaker; public ulong dbg_remote_support; public ulong dbg_pending_call; public ulong dbg_script_path; public ulong dbg_script_path_size;
        }

        private const int ALT_COOKIE_SIZE = 8;
        private const string EXPECTED_COOKIE = "xdebugpy";
        private const byte FLAG_FREE_THREADED = 0x01;           // tentative mapping (PEP may finalize)
        private const byte FLAG_REMOTE_DEBUG_DISABLED = 0x02;    // tentative mapping
        private const uint MIN_SUPPORTED_VERSION = 0x030E0000;   // 3.14.0 (PEP 768)

        public struct DebuggerSupportOffsets {
            // For now we expose what we parse. eval_breaker may be absolute or offset; others are offsets relative
            // to per-thread structures per PEP. We store raw values (no base arithmetic) so later code can interpret.
            public ulong EvalBreaker;
            public ulong RemoteSupport;        // offset in PyThreadState
            public ulong PendingCall;          // offset inside _PyRemoteDebuggerSupport
            public ulong ScriptPath;           // offset inside _PyRemoteDebuggerSupport
            public ulong ScriptPathSize;       // size of script_path buffer (bytes)
        }

        private readonly DkmProcess _process;
        private PyDebugOffsetsReader(DkmProcess process) { _process = process; }

        #region Legacy (<3.14) parse retained for unit tests
        public static bool TryParse(byte[] data, out uint version, out string cookie, out DebuggerSupportOffsets offsets) {
            version = 0; cookie = string.Empty; offsets = default;
            if (data == null) return false;
            int szA = Marshal.SizeOf(typeof(PyDebugOffsetsStructV1A));
            if (data.Length < szA) return false;
            try {
                var raw = BytesToStruct<PyDebugOffsetsStructV1A>(data);
                if (raw.sizeof_struct < (ulong)szA) return false;
                version = raw.version;
                cookie = BuildCookie(raw.cookie64);
                if (version < MIN_SUPPORTED_VERSION || !IsExpectedCookie(cookie)) return false;
                if (raw.dbg_script_path_size == 0 || raw.dbg_script_path_size > 1_000_000UL) return false;
                if (raw.dbg_script_path == 0 || raw.dbg_remote_support == 0 || raw.dbg_eval_breaker == 0) return false;
                offsets = new DebuggerSupportOffsets {
                    EvalBreaker = raw.dbg_eval_breaker,
                    RemoteSupport = raw.dbg_remote_support,
                    PendingCall = raw.dbg_pending_call,
                    ScriptPath = raw.dbg_script_path,
                    ScriptPathSize = raw.dbg_script_path_size
                };
                return true;
            } catch { return false; }
        }
        #endregion

        #region Spec-driven parser (PEP 768)
        private struct ParseResult {
            public bool Success; public string Failure; public uint Version; public byte Flags; public ulong SizeOfStruct; public DebuggerSupportOffsets Offsets;
        }

        private static ParseResult ParseDebugOffsetsSpec(byte[] data, byte ptrSize, ulong baseAddress) {
            var pr = new ParseResult { Success = false, Failure = "unknown" };
            if (data == null || data.Length < 64) { pr.Failure = "buffer too small"; return pr; }
            // cookie
            for (int i = 0; i < EXPECTED_COOKIE.Length; i++) {
                if (data[i] != (byte)EXPECTED_COOKIE[i]) { pr.Failure = "cookie mismatch"; return pr; }
            }
            // version (little endian)
            if (data.Length < 12) { pr.Failure = "incomplete header"; return pr; }
            uint version = BitConverter.ToUInt32(data, 8);
            // Accept prerelease variants: only major/minor must match
            uint vMask = version & 0xFFFF0000;
            if (vMask < MIN_SUPPORTED_VERSION) { pr.Failure = $"unsupported version 0x{version:X8}"; return pr; }
            // flags (1 byte) followed by padding up to pointer alignment
            int flagsOffset = 12;
            byte flags = data.Length > flagsOffset ? data[flagsOffset] : (byte)0;
            int cursor = flagsOffset + 1;
            while ((cursor % ptrSize) != 0 && cursor < data.Length) cursor++; // align
            if (cursor + ptrSize > data.Length) { pr.Failure = "missing sizeof_struct"; return pr; }
            ulong sizeofStruct = ptrSize == 8 ? BitConverter.ToUInt64(data, cursor) : BitConverter.ToUInt32(data, cursor);
            cursor += ptrSize;
            // Some prerelease builds may still have sizeof=0; tolerate but record
            ulong minExpected = (ulong)(EXPECTED_COOKIE.Length + 4 + 1 + (ptrSize - ((EXPECTED_COOKIE.Length + 5) % ptrSize)) + ptrSize + (5 * ptrSize));
            if (sizeofStruct != 0 && sizeofStruct < minExpected) { pr.Failure = $"sizeof_struct too small ({sizeofStruct})"; return pr; }
            // debugger_support block: 5 * pointer-size (PEP indicates 5 x 64-bit; treat pointer-size for portability)
            if (cursor + 5 * ptrSize > data.Length) { pr.Failure = "insufficient data for debugger_support"; return pr; }
            ulong ReadPtr(int off) => ptrSize == 8 ? BitConverter.ToUInt64(data, off) : BitConverter.ToUInt32(data, off);
            ulong evalBreakerRaw = ReadPtr(cursor); cursor += (int)ptrSize;
            ulong remoteSupport = ReadPtr(cursor); cursor += (int)ptrSize;
            ulong pendingCall = ReadPtr(cursor); cursor += (int)ptrSize;
            ulong scriptPath = ReadPtr(cursor); cursor += (int)ptrSize;
            ulong scriptPathSize = ReadPtr(cursor); cursor += (int)ptrSize;
            // Basic validation of offsets / sizes
            if (remoteSupport == 0 || pendingCall == 0 || scriptPath == 0) { pr.Failure = "zero offset(s)"; return pr; }
            if (scriptPathSize == 0 || scriptPathSize > 1_000_000UL) { pr.Failure = "invalid script_path_size"; return pr; }
            // remoteSupport, pendingCall, scriptPath expected to be small offsets (but tolerate larger prototypes)
            bool looksOffset(ulong v) => v < 64 * 1024 * 1024; // 64MB upper bound for offset heuristics
            if (!looksOffset(remoteSupport) || !looksOffset(pendingCall) || !looksOffset(scriptPath)) { pr.Failure = "offset(s) out of expected range"; return pr; }
            // eval_breaker: if value is suspiciously small (likely an offset inside PyRuntime) reinterpret relative to base.
            ulong evalBreaker = evalBreakerRaw;
            if (evalBreakerRaw < baseAddress || evalBreakerRaw < 0x0100_0000UL) { // treat as relative offset
                evalBreaker = baseAddress + evalBreakerRaw;
            }
            pr.Success = true; pr.Version = version; pr.Flags = flags; pr.SizeOfStruct = sizeofStruct;
            pr.Offsets = new DebuggerSupportOffsets {
                EvalBreaker = evalBreaker,
                RemoteSupport = remoteSupport,
                PendingCall = pendingCall,
                ScriptPath = scriptPath,
                ScriptPathSize = scriptPathSize
            };
            return pr;
        }
        #endregion

        public static PyDebugOffsetsReader TryCreate(DkmProcess process) {
            Debug.WriteLine("[PTVS][DebugOffsets] TryCreate: starting resolution of _Py_DebugOffsets");
            var reader = new PyDebugOffsetsReader(process);
            try {
                ulong addr = ResolveDebugOffsetsAddress(process);
                if (addr == 0) {
                    Debug.WriteLine("[PTVS][DebugOffsets] TryCreate: address resolution returned 0 (symbol not found / evaluation failed).");
                    return reader;
                }
                Debug.WriteLine($"[PTVS][DebugOffsets] TryCreate: Resolved address 0x{addr:X}");

                // Read minimal header area (128 bytes) – enough for cookie, header, and debugger_support block.
                const int probeSize = 128;
                byte[] data;
                try { data = ReadMemorySafely(process, addr, probeSize); }
                catch (Exception exRead) { Debug.WriteLine($"[PTVS][DebugOffsets] TryCreate: ReadMemory failed @0x{addr:X}: {exRead.Message}"); return reader; }
                Debug.WriteLine("[PTVS][DebugOffsets] First64=" + Hex(data, 0, 64));

                var spec = ParseDebugOffsetsSpec(data, process.GetPointerSize(), addr);
                if (spec.Success) {
                    reader.Version = spec.Version;
                    reader.Cookie = EXPECTED_COOKIE;
                    reader.DebuggerSupport = spec.Offsets;
                    reader.FreeThreaded = (spec.Flags & FLAG_FREE_THREADED) != 0;
                    reader.RemoteDebugDisabled = (spec.Flags & FLAG_REMOTE_DEBUG_DISABLED) != 0;
                    reader.IsAvailable = !reader.RemoteDebugDisabled; // if disabled, we expose detection but not availability
                    Debug.WriteLine($"[PTVS][DebugOffsets] Spec parse OK: Version=0x{spec.Version:X8} Flags=0x{spec.Flags:X2} FreeThreaded={reader.FreeThreaded} Disabled={reader.RemoteDebugDisabled} SizeOfStruct={spec.SizeOfStruct} EvalBreaker=0x{reader.DebuggerSupport.EvalBreaker:X} RemoteSupportOff=0x{reader.DebuggerSupport.RemoteSupport:X} PendingCallOff=0x{reader.DebuggerSupport.PendingCall:X} ScriptPathOff=0x{reader.DebuggerSupport.ScriptPath:X} ScriptPathSize={reader.DebuggerSupport.ScriptPathSize}");
                    return reader;
                }
                Debug.WriteLine($"[PTVS][DebugOffsets] Spec parse failed: {spec.Failure}; attempting legacy fallback");

                // Legacy fallback (older experimental builds) – attempt to marshal a V1 layout (not cookie-first).
                if (TryParse(data, out var ver, out var cookie, out var offs)) {
                    reader.Version = ver; reader.Cookie = cookie; reader.DebuggerSupport = offs; reader.IsAvailable = true;
                    Debug.WriteLine($"[PTVS][DebugOffsets] Legacy parse OK: Version=0x{ver:X8}, Cookie='{cookie}' EvalBreaker=0x{offs.EvalBreaker:X}");
                } else {
                    Debug.WriteLine("[PTVS][DebugOffsets] All parsing attempts failed.");
                }
            } catch (Exception ex) {
                Debug.WriteLine($"[PTVS][DebugOffsets] TryCreate: Exception: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
            return reader;
        }

        private static ulong ResolveDebugOffsetsAddress(DkmProcess process) {
            try {
                var pyInfo = process.GetPythonRuntimeInfo();
                var pyModule = pyInfo?.DLLs?.Python;
                if (pyModule != null) {
                    try {
                        if (pyModule.HasSymbols()) {
                            var symAddr = TryGetStaticVariableAddressNoAssert(pyModule, "_Py_DebugOffsets");
                            if (symAddr != 0 && pyModule.ContainsAddress(symAddr)) {
                                Debug.WriteLine($"[PTVS][DebugOffsets] ResolveDebugOffsetsAddress: Found via symbols @0x{symAddr:X}");
                                return symAddr;
                            }
                        }
                        var sectionAddr = TryLocatePyRuntimeSectionWindows(pyModule, process);
                        if (sectionAddr != 0) {
                            Debug.WriteLine($"[PTVS][DebugOffsets] ResolveDebugOffsetsAddress: Found via PE section 'PyRuntim' @0x{sectionAddr:X}");
                            return sectionAddr;
                        }
                        try {
                            var export = pyModule.FindExportName("_Py_DebugOffsets", false);
                            if (export != null) {
                                var addr = pyModule.BaseAddress + export.RVA;
                                Debug.WriteLine($"[PTVS][DebugOffsets] ResolveDebugOffsetsAddress: Found via export @0x{addr:X}");
                                return addr;
                            }
                        } catch (Exception exExport) { Debug.WriteLine($"[PTVS][DebugOffsets] ResolveDebugOffsetsAddress: export lookup failed: {exExport.Message}"); }
                    } catch (Exception modEx) { Debug.WriteLine($"[PTVS][DebugOffsets] ResolveDebugOffsetsAddress: module exception: {modEx.Message}"); }
                }
                var thread = process.GetThreads().FirstOrDefault();
                if (thread != null) {
                    var frame = thread.GetTopStackFrame();
                    if (frame != null) {
                        try {
                            var cpp = new CppExpressionEvaluator(thread, frame.FrameBase, frame.FrameBase);
                            var value = cpp.EvaluateUInt64("&::_Py_DebugOffsets");
                            Debug.WriteLine($"[PTVS][DebugOffsets] ResolveDebugOffsetsAddress: evaluation succeeded -> 0x{value:X}");
                            return value;
                        } catch { }
                    }
                }
            } catch (Exception ex) { Debug.WriteLine($"[PTVS][DebugOffsets] ResolveDebugOffsetsAddress: outer exception: {ex.Message}"); }
            return 0;
        }

        private static ulong TryLocatePyRuntimeSectionWindows(DkmNativeModuleInstance module, DkmProcess process) {
            try {
                const string targetName = "PyRuntim"; // truncated section name in PE (<=8 chars)
                var dos = new byte[0x40]; process.ReadMemory(module.BaseAddress, DkmReadMemoryFlags.None, dos);
                if (!(dos[0] == 'M' && dos[1] == 'Z')) return 0;
                int e_lfanew = BitConverter.ToInt32(dos, 0x3C); if (e_lfanew <= 0 || e_lfanew > module.Size - 0x200) return 0;
                var ntfh = new byte[24]; process.ReadMemory(module.BaseAddress + (ulong)e_lfanew, DkmReadMemoryFlags.None, ntfh);
                if (!(ntfh[0] == 'P' && ntfh[1] == 'E')) return 0;
                ushort numberOfSections = BitConverter.ToUInt16(ntfh, 6); ushort sizeOfOptionalHeader = BitConverter.ToUInt16(ntfh, 20);
                ulong sectionTable = module.BaseAddress + (ulong)(e_lfanew + 24 + sizeOfOptionalHeader);
                var sectionHeader = new byte[40];
                for (int i = 0; i < numberOfSections; i++) {
                    ulong secAddr = sectionTable + (ulong)(i * 40);
                    process.ReadMemory(secAddr, DkmReadMemoryFlags.None, sectionHeader);
                    string name = Encoding.ASCII.GetString(sectionHeader, 0, 8).TrimEnd('\0');
                    if (name == targetName) { uint virtualAddress = BitConverter.ToUInt32(sectionHeader, 12); return module.BaseAddress + virtualAddress; }
                }
            } catch (Exception ex) { Debug.WriteLine($"[PTVS][DebugOffsets] TryLocatePyRuntimeSectionWindows: {ex.Message}"); }
            return 0;
        }

        private static byte[] ReadMemorySafely(DkmProcess process, ulong address, int size) {
            var buffer = new byte[size]; var threads = process.GetThreads(); var suspended = new List<DkmThread>(); bool attempted = false;
            try { if (threads.Length > 1) { foreach (var t in threads) { try { t.Suspend(true); suspended.Add(t); attempted = true; } catch { } } } process.ReadMemory(address, DkmReadMemoryFlags.None, buffer); }
            finally { if (attempted) foreach (var t in suspended) { try { t.Resume(true); } catch { } } }
            return buffer;
        }

        private static ulong TryGetStaticVariableAddressNoAssert(DkmNativeModuleInstance moduleInstance, string name) {
            try {
                using (var moduleSym = moduleInstance.TryGetSymbols()) {
                    if (moduleSym.Object == null) return 0; IDiaEnumSymbols enumSymbols = null; moduleSym.Object.findChildren(SymTagEnum.SymTagData, name, 1, out enumSymbols);
                    using (ComPtr.Create(enumSymbols)) {
                        if (enumSymbols == null || enumSymbols.count != 1) return 0;
                        using (var varSym = ComPtr.Create(enumSymbols.Item(0))) { return moduleInstance.BaseAddress + varSym.Object.relativeVirtualAddress; }
                    }
                }
            } catch (Exception ex) { Debug.WriteLine($"[PTVS][DebugOffsets] TryGetStaticVariableAddressNoAssert: {ex.Message}"); }
            return 0;
        }

        private static unsafe T BytesToStruct<T>(byte[] data) where T : struct { fixed (byte* p = data) return (T)Marshal.PtrToStructure(new IntPtr(p), typeof(T)); }
        private static string BuildCookie(ulong cookie64) { var bytes = BitConverter.GetBytes(cookie64); int len = 0; while (len < bytes.Length && bytes[len] != 0) len++; return len == 0 ? string.Empty : Encoding.ASCII.GetString(bytes, 0, len); }
        private static bool IsExpectedCookie(string cookie) => cookie == EXPECTED_COOKIE;
        private static string Hex(byte[] data, int start, int len) { int end = Math.Min(data.Length, start + len); var sb = new StringBuilder(); for (int i = start; i < end; i++) { if (i > start) sb.Append(' '); sb.Append(data[i].ToString("X2")); } return sb.ToString(); }
    }
}

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
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.PythonTools.Debugger.Concord.Proxies.Structs;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Native;


namespace Microsoft.PythonTools.Debugger.Concord {

    internal class PythonDLLs {
        private static readonly Regex pythonName = new Regex(@"^python(3\d+)(?:_d)?\.dll$");

        public static readonly string[] DebuggerHelperNames = {
            "Microsoft.PythonTools.Debugger.Helper.x86.dll",
            "Microsoft.PythonTools.Debugger.Helper.x64.dll",
        };

        public static readonly string[] CTypesNames = {
            "_ctypes.pyd", "_ctypes_d.pyd"
        };

        // Cache of successfully created debug offsets readers keyed by process id (UniqueId cast to int) or fallback hash
        private static readonly ConcurrentDictionary<int, PyDebugOffsetsReader> _debugOffsetsCache = new ConcurrentDictionary<int, PyDebugOffsetsReader>();

        private readonly PythonRuntimeInfo _pyrtInfo;
        private DkmNativeModuleInstance _python;

        public PythonDLLs(PythonRuntimeInfo pyrtInfo) { _pyrtInfo = pyrtInfo; }

        public DkmNativeModuleInstance Python {
            get { return _python; }
            set {
                _python = value;
                if (value == null) { return; }

                _pyrtInfo.LanguageVersion = GetPythonLanguageVersion(value);
                Debug.Assert(_pyrtInfo.LanguageVersion != PythonLanguageVersion.None);

                try {
                    // Determine a stable process id for caching. Prefer LivePart.Id (int) when available; fall back to hash.
                    int pid;
                    try {
                        var live = value.Process.LivePart; // LivePart is null for dump debugging
                        pid = live != null ? live.Id : value.Process.GetHashCode();
                    } catch {
                        pid = value.Process.GetHashCode();
                    }

                    if (_debugOffsetsCache.TryGetValue(pid, out var cached) && cached != null) {
                        _pyrtInfo.DebugOffsets = cached;
                        if (cached.IsAvailable && _pyrtInfo.ValidateDebugOffsets()) {
                            Debug.WriteLine($"[PTVS][DebugOffsets] Reused cached reader (Version={cached.Version}, EvalBreaker=0x{cached.DebuggerSupport.EvalBreaker:X})");
                        } else {
                            Debug.WriteLine("[PTVS][DebugOffsets] Cached reader present but not valid/available.");
                        }
                        return;
                    }

                    if (_pyrtInfo.DebugOffsets == null) {
                        var reader = PyDebugOffsetsReader.TryCreate(value.Process);
                        _pyrtInfo.DebugOffsets = reader;
                        if (reader != null) {
                            if (reader.IsAvailable && _pyrtInfo.ValidateDebugOffsets()) {
                                _debugOffsetsCache[pid] = reader;
                                Debug.WriteLine($"[PTVS][DebugOffsets] Loaded (Version={reader.Version}, Cookie='{reader.Cookie}', EvalBreaker=0x{reader.DebuggerSupport.EvalBreaker:X}, RemoteSupport=0x{reader.DebuggerSupport.RemoteSupport:X})");
                            } else {
                                Debug.WriteLine("[PTVS][DebugOffsets] Not available, invalid, or symbol not found for this runtime version.");
                            }
                        } else {
                            Debug.WriteLine("[PTVS][DebugOffsets] Reader creation returned null.");
                        }
                    } else {
                        Debug.WriteLine("[PTVS][DebugOffsets] Skipping re-creation; already initialized in this instance.");
                    }
                } catch (Exception ex) {
                    Debug.WriteLine($"[PTVS][DebugOffsets] Exception while loading: {ex.Message}");
                }
            }
        }

        public DkmNativeModuleInstance DebuggerHelper { get; set; }
        public DkmNativeModuleInstance CTypes { get; set; }

        public static PythonLanguageVersion GetPythonLanguageVersion(DkmNativeModuleInstance moduleInstance) {
            var m = pythonName.Match(moduleInstance.Name);
            if (!m.Success) { return PythonLanguageVersion.None; }
            var ver = m.Groups[1].Value;
            switch (ver) {
                case "27": return PythonLanguageVersion.V27;
                case "33": return PythonLanguageVersion.V33;
                case "34": return PythonLanguageVersion.V34;
                case "35": return PythonLanguageVersion.V35;
                case "36": return PythonLanguageVersion.V36;
                case "37": return PythonLanguageVersion.V37;
                case "38": return PythonLanguageVersion.V38;
                case "39": return PythonLanguageVersion.V39;
                case "310": return PythonLanguageVersion.V310;
                case "311": return PythonLanguageVersion.V311;
                case "312": return PythonLanguageVersion.V312;
                case "313": return PythonLanguageVersion.V313;
                case "314": return PythonLanguageVersion.V314; // Added 3.14 support
                default: return PythonLanguageVersion.None;
            }
        }
    }

    internal class PythonRuntimeInfo : DkmDataItem {
        public PythonLanguageVersion LanguageVersion { get; set; }
        public PythonDLLs DLLs { get; private set; }
        public PyDebugOffsetsReader DebugOffsets { get; internal set; }

        // Cached attach thread state (PyThreadState base address) to avoid repeated discovery.
        public ulong CachedAttachThreadState { get; internal set; }
        // Placeholder for dynamic stop-bit mask (future extraction from ceval state); defaults to 0x1.
        public uint EvalPleaseStopMask { get; internal set; } = 0x1;

        // Indicates offsets were parsed & validated.
        public bool HasValidDebugOffsets => DebugOffsets != null && DebugOffsets.IsAvailable && _validatedDebugOffsets;

        // EvalBreaker is normalized to an absolute address by the reader (if possible).
        public ulong EvalBreakerAddress => HasValidDebugOffsets ? DebugOffsets.DebuggerSupport.EvalBreaker : 0UL;

        // NOTE: This returns the *offset* inside PyThreadState – NOT an absolute address.
        // New code should prefer RemoteSupportOffset.
        public ulong RemoteSupportAddress => HasValidDebugOffsets ? DebugOffsets.DebuggerSupport.RemoteSupport : 0UL;

        // Convenience accessors (PEP 768 terminology) ------------------------------------
        public ulong RemoteSupportOffset => HasValidDebugOffsets ? DebugOffsets.DebuggerSupport.RemoteSupport : 0UL;          // offset of _PyRemoteDebuggerSupport in PyThreadState
        public ulong PendingCallOffset => HasValidDebugOffsets ? DebugOffsets.DebuggerSupport.PendingCall : 0UL;            // offset inside support struct
        public ulong ScriptPathOffset => HasValidDebugOffsets ? DebugOffsets.DebuggerSupport.ScriptPath : 0UL;             // offset inside support struct
        public ulong ScriptPathSize => HasValidDebugOffsets ? DebugOffsets.DebuggerSupport.ScriptPathSize : 0UL;         // buffer size (bytes)
        public bool RemoteDebugDisabled => DebugOffsets?.RemoteDebugDisabled == true;                                         // environment / policy disabled attach
        public bool FreeThreadedRuntime => DebugOffsets?.FreeThreaded == true;                                               // flag surfaced by PEP

        private bool _validatedDebugOffsets;

        public PythonRuntimeInfo() { DLLs = new PythonDLLs(this); }

        /// <summary>
        /// Lightweight validation of parsed _Py_DebugOffsets values (PEP 768).
        /// EvalBreaker is probed (absolute) while the other fields are treated as relative offsets.
        /// We do not dereference RemoteSupport/PendingCall/ScriptPath as absolute addresses to avoid invalid reads
        /// in early interpreter states (values can be small offsets like 0x310).
        /// </summary>
        internal bool ValidateDebugOffsets() {
            if (DebugOffsets == null) { Debug.WriteLine("[PTVS][DebugOffsets] ValidateDebugOffsets: DebugOffsets is null."); return false; }
            if (!DebugOffsets.IsAvailable) { Debug.WriteLine("[PTVS][DebugOffsets] ValidateDebugOffsets: DebugOffsets.IsAvailable == false."); return false; }
            if (_validatedDebugOffsets) { Debug.WriteLine("[PTVS][DebugOffsets] ValidateDebugOffsets: Already validated (cached success)."); return true; }
            try {
                var process = DLLs.Python?.Process; if (process == null) { Debug.WriteLine("[PTVS][DebugOffsets] ValidateDebugOffsets: process is null."); return false; }
                var dbg = DebugOffsets.DebuggerSupport;
                Debug.WriteLine($"[PTVS][DebugOffsets] ValidateDebugOffsets: Version=0x{DebugOffsets.Version:X8}, EvalBreaker=0x{dbg.EvalBreaker:X}, RemoteSupport(off)=0x{dbg.RemoteSupport:X}, PendingCall(off)=0x{dbg.PendingCall:X}, ScriptPath(off)=0x{dbg.ScriptPath:X} size={dbg.ScriptPathSize}");

                // Probe EvalBreaker (must be readable) – assume it's absolute after reader normalization.
                ProbeAddress(process, dbg.EvalBreaker, 1, "EvalBreaker");

                bool offsetSane(ulong v) => v < 64 * 1024 * 1024 && v != 0; // <64MB and non-zero
                if (!offsetSane(dbg.RemoteSupport)) { throw new InvalidOperationException($"RemoteSupport offset looks invalid: 0x{dbg.RemoteSupport:X}"); }
                if (!offsetSane(dbg.PendingCall)) { throw new InvalidOperationException($"PendingCall offset looks invalid: 0x{dbg.PendingCall:X}"); }
                if (!offsetSane(dbg.ScriptPath) || dbg.ScriptPathSize == 0 || dbg.ScriptPathSize > 1_000_000UL) {
                    throw new InvalidOperationException($"ScriptPath offset/size invalid: off=0x{dbg.ScriptPath:X} size={dbg.ScriptPathSize}");
                }

                _validatedDebugOffsets = true;
                Debug.WriteLine("[PTVS][DebugOffsets] Validation succeeded (EvalBreaker probed). Offsets retained as relative values.");
                return true;
            } catch (Exception ex) {
                Debug.WriteLine($"[PTVS][DebugOffsets] Validation failed: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                _validatedDebugOffsets = false;
                return false;
            }
        }

        private static void ProbeAddress(DkmProcess process, ulong address, int size, string name) {
            if (address == 0) { throw new InvalidOperationException($"Address for {name} is 0"); }
            Debug.WriteLine($"[PTVS][DebugOffsets] Probing {name} @0x{address:X} (size={size})");
            var buffer = new byte[size];
            try { process.ReadMemory(address, DkmReadMemoryFlags.None, buffer); } catch (Exception ex) { throw new InvalidOperationException($"Memory read failed for {name} at 0x{address:X}: {ex.Message}", ex); }
        }


        public PyRuntimeState GetRuntimeState() {
            if (LanguageVersion < PythonLanguageVersion.V37) {
                return null;
            }
            return DLLs.Python.GetStaticVariable<PyRuntimeState>("_PyRuntime");
        }
    }

    internal static class PythonRuntimeInfoExtensions {
        public static PythonRuntimeInfo GetPythonRuntimeInfo(this DkmProcess process) {
            return process.GetOrCreateDataItem(() => new PythonRuntimeInfo());
        }
    }
}

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
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Native;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    /// <summary>
    /// Managed reader for CPython's self-describing <c>_Py_DebugOffsets</c> table
    /// (CPython 3.14, <c>Include/internal/pycore_debug_offsets.h</c>). The table is placed
    /// at offset 0 of the exported <c>_PyRuntime</c> global so out-of-process debuggers can
    /// discover the byte offset of interesting fields (frames, code objects, thread state,
    /// builtin object layouts, ...) without relying on the interpreter's PDB.
    ///
    /// The table opens with an 8-byte <c>"xdebugpy"</c> cookie, a <c>PY_VERSION_HEX</c>
    /// version word and a <c>free_threaded</c> flag, followed by a flat run of little-endian
    /// <c>uint64_t</c> offsets grouped by struct. This class only knows the 3.14 layout; the
    /// header explicitly warns the layout is not stable across minor versions, so callers must
    /// gate on <see cref="Is314"/> before trusting anything but the cookie/version prefix.
    /// </summary>
    internal sealed class PyDebugOffsets {
        public const string CookieString = "xdebugpy";

        // Header: char cookie[8]; uint64 version; uint64 free_threaded.
        private const int CookieSize = 8;
        private const int HeaderSize = CookieSize + sizeof(ulong) + sizeof(ulong);

        // The 3.14 layout, mirroring pycore_debug_offsets.h field-for-field (every entry is
        // a uint64 offset). Keeping the groups/fields in exact source order is what makes the
        // byte positions correct by construction; do not reorder without matching the header.
        private static readonly string[] RuntimeStateFields = {
            "size", "finalizing", "interpreters_head",
        };
        private static readonly string[] InterpreterStateFields = {
            "size", "id", "next", "threads_head", "threads_main", "gc",
            "imports_modules", "sysdict", "builtins", "ceval_gil",
            "gil_runtime_state", "gil_runtime_state_enabled",
            "gil_runtime_state_locked", "gil_runtime_state_holder",
            "code_object_generation", "tlbc_generation",
        };
        private static readonly string[] ThreadStateFields = {
            "size", "prev", "next", "interp", "current_frame", "thread_id",
            "native_thread_id", "datastack_chunk", "status",
        };
        private static readonly string[] InterpreterFrameFields = {
            "size", "previous", "executable", "instr_ptr", "localsplus", "owner",
            "stackpointer", "tlbc_index",
        };
        private static readonly string[] CodeObjectFields = {
            "size", "filename", "name", "qualname", "linetable", "firstlineno",
            "argcount", "localsplusnames", "localspluskinds", "co_code_adaptive",
            "co_tlbc",
        };
        private static readonly string[] PyObjectFields = {
            "size", "ob_type",
        };
        private static readonly string[] TypeObjectFields = {
            "size", "tp_name", "tp_repr", "tp_flags",
        };
        private static readonly string[] TupleObjectFields = {
            "size", "ob_item", "ob_size",
        };
        private static readonly string[] ListObjectFields = {
            "size", "ob_item", "ob_size",
        };
        private static readonly string[] SetObjectFields = {
            "size", "used", "table", "mask",
        };
        private static readonly string[] DictObjectFields = {
            "size", "ma_keys", "ma_values",
        };
        private static readonly string[] FloatObjectFields = {
            "size", "ob_fval",
        };
        private static readonly string[] LongObjectFields = {
            "size", "lv_tag", "ob_digit",
        };
        private static readonly string[] BytesObjectFields = {
            "size", "ob_size", "ob_sval",
        };
        private static readonly string[] UnicodeObjectFields = {
            "size", "state", "length", "asciiobject_size",
        };
        private static readonly string[] GcFields = {
            "size", "collecting",
        };
        private static readonly string[] GenObjectFields = {
            "size", "gi_name", "gi_iframe", "gi_frame_state",
        };
        private static readonly string[] LListNodeFields = {
            "next", "prev",
        };
        private static readonly string[] DebuggerSupportFields = {
            "eval_breaker", "remote_debugger_support", "remote_debugging_enabled",
            "debugger_pending_call", "debugger_script_path", "debugger_script_path_size",
        };

        // Groups in the exact order they appear in _Py_DebugOffsets.
        private static readonly Tuple<string, string[]>[] Groups = {
            Tuple.Create("runtime_state", RuntimeStateFields),
            Tuple.Create("interpreter_state", InterpreterStateFields),
            Tuple.Create("thread_state", ThreadStateFields),
            Tuple.Create("interpreter_frame", InterpreterFrameFields),
            Tuple.Create("code_object", CodeObjectFields),
            Tuple.Create("pyobject", PyObjectFields),
            Tuple.Create("type_object", TypeObjectFields),
            Tuple.Create("tuple_object", TupleObjectFields),
            Tuple.Create("list_object", ListObjectFields),
            Tuple.Create("set_object", SetObjectFields),
            Tuple.Create("dict_object", DictObjectFields),
            Tuple.Create("float_object", FloatObjectFields),
            Tuple.Create("long_object", LongObjectFields),
            Tuple.Create("bytes_object", BytesObjectFields),
            Tuple.Create("unicode_object", UnicodeObjectFields),
            Tuple.Create("gc", GcFields),
            Tuple.Create("gen_object", GenObjectFields),
            Tuple.Create("llist_node", LListNodeFields),
            Tuple.Create("debugger_support", DebuggerSupportFields),
        };

        private readonly System.Collections.Generic.Dictionary<string, ulong> _offsets;

        /// <summary>PY_VERSION_HEX recorded by the interpreter that produced this table.</summary>
        public ulong Version { get; }

        /// <summary>True if the interpreter was built free-threaded (PEP 703, Py_GIL_DISABLED).</summary>
        public bool FreeThreaded { get; }

        public int Major => (int)((Version >> 24) & 0xFF);
        public int Minor => (int)((Version >> 16) & 0xFF);
        public int Micro => (int)((Version >> 8) & 0xFF);

        /// <summary>True when this table describes a CPython 3.14 layout (the only layout this reader knows).</summary>
        public bool Is314 => Major == 3 && Minor == 14;

        /// <summary>Total number of bytes consumed by the parsed 3.14 table (header + all offset words).</summary>
        public static int TableSize {
            get {
                int fields = 0;
                foreach (var group in Groups) {
                    fields += group.Item2.Length;
                }
                return HeaderSize + fields * sizeof(ulong);
            }
        }

        private PyDebugOffsets(ulong version, bool freeThreaded, System.Collections.Generic.Dictionary<string, ulong> offsets) {
            Version = version;
            FreeThreaded = freeThreaded;
            _offsets = offsets;
        }

        /// <summary>
        /// Returns the recorded offset for <paramref name="group"/>.<paramref name="field"/>, e.g.
        /// <c>Offset("code_object", "linetable")</c>. Throws if the name is unknown (programming error).
        /// </summary>
        public ulong Offset(string group, string field) {
            return _offsets[group + "." + field];
        }

        /// <summary>
        /// Attempts to parse a <c>_Py_DebugOffsets</c> table from raw bytes read out of the debuggee.
        /// Validates the cookie and that the buffer is large enough for the full 3.14 layout, but does
        /// not require a specific version so callers can inspect <see cref="Is314"/> / <see cref="Version"/>.
        /// </summary>
        public static bool TryParse(byte[] data, out PyDebugOffsets result, out string error) {
            result = null;
            error = null;

            if (data == null || data.Length < HeaderSize) {
                error = "buffer too small for _Py_DebugOffsets header";
                return false;
            }

            for (int i = 0; i < CookieSize; i++) {
                if (data[i] != (byte)CookieString[i]) {
                    error = "missing xdebugpy cookie";
                    return false;
                }
            }

            ulong version = BitConverter.ToUInt64(data, CookieSize);
            ulong freeThreaded = BitConverter.ToUInt64(data, CookieSize + sizeof(ulong));

            int needed = TableSize;
            if (data.Length < needed) {
                error = "buffer too small for _Py_DebugOffsets 3.14 layout (need " + needed + " bytes, have " + data.Length + ")";
                return false;
            }

            var offsets = new System.Collections.Generic.Dictionary<string, ulong>(needed / sizeof(ulong));
            int pos = HeaderSize;
            foreach (var group in Groups) {
                foreach (var field in group.Item2) {
                    offsets[group.Item1 + "." + field] = BitConverter.ToUInt64(data, pos);
                    pos += sizeof(ulong);
                }
            }

            result = new PyDebugOffsets(version, freeThreaded != 0, offsets);
            return true;
        }

        /// <summary>
        /// The name of the exported <c>_PyRuntime</c> global whose first field is the
        /// <c>_Py_DebugOffsets</c> table. It is exported (<c>PyAPI_DATA</c>) so it can be located
        /// from the module's export table without a PDB.
        /// </summary>
        public const string RuntimeSymbol = "_PyRuntime";

        /// <summary>
        /// Attempts to locate and parse the <c>_Py_DebugOffsets</c> table out of a live debuggee.
        /// Resolves the exported <c>_PyRuntime</c> symbol from <paramref name="pythonDll"/> (no PDB
        /// required), reads the table bytes from process memory and parses them. Returns null if the
        /// symbol is absent (pre-3.14 interpreters) or the table does not validate.
        /// </summary>
        public static PyDebugOffsets TryRead(DkmProcess process, DkmNativeModuleInstance pythonDll) {
            if (process == null || pythonDll == null) {
                return null;
            }

            ulong address = pythonDll.TryGetExportedStaticVariableAddress(RuntimeSymbol);
            if (address == 0) {
                return null;
            }

            var buffer = new byte[TableSize];
            try {
                process.ReadMemory(address, DkmReadMemoryFlags.None, buffer);
            } catch (DkmException) {
                return null;
            }

            PyDebugOffsets result;
            string error;
            if (!TryParse(buffer, out result, out error)) {
                return null;
            }
            return result;
        }

        public override string ToString() {
            return string.Format(
                "_Py_DebugOffsets(version=0x{0:x}, {1}.{2}.{3}, free_threaded={4})",
                Version, Major, Minor, Micro, FreeThreaded);
        }
    }
}

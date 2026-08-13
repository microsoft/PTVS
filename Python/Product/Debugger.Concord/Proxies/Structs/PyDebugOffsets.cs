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
    /// (<c>Include/internal/pycore_debug_offsets.h</c>). The table is placed
    /// at offset 0 of the exported <c>_PyRuntime</c> global so out-of-process debuggers can
    /// discover the byte offset of interesting fields (frames, code objects, thread state,
    /// builtin object layouts, ...) without relying on the interpreter's PDB.
    ///
    /// The table opens with an 8-byte <c>"xdebugpy"</c> cookie, a <c>PY_VERSION_HEX</c>
    /// version word and a <c>free_threaded</c> flag, followed by a flat run of little-endian
    /// <c>uint64_t</c> offsets grouped by struct. There is no per-group length in the blob, so the
    /// byte position of every field is implied purely by the ordered list of groups/fields for that
    /// interpreter version. CPython grows this table between minor versions (adding fields and whole
    /// sub-structs), so this class keeps one ordered <see cref="Layout"/> per version it understands
    /// (3.14 and 3.15) and selects the matching one from the recorded version word. Callers should
    /// gate on <see cref="IsSupported"/> before trusting any offset.
    /// </summary>
    internal sealed class PyDebugOffsets {
        public const string CookieString = "xdebugpy";

        // Header: char cookie[8]; uint64 version; uint64 free_threaded.
        private const int CookieSize = 8;
        private const int HeaderSize = CookieSize + sizeof(ulong) + sizeof(ulong);

        // _Py_DebugOffsets is a flat run of grouped uint64 offsets with no per-group length, so the
        // byte position of every field is implied by the ordered list of groups/fields. These lists
        // must therefore mirror pycore_debug_offsets.h field-for-field, in exact source order, for
        // each interpreter version we support. Do not reorder without matching the header.

        // Groups whose field set is identical across every layout we support (3.14 and 3.15).
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

        // Groups whose field set changed between 3.14 and 3.15.
        private static readonly string[] ThreadStateFields314 = {
            "size", "prev", "next", "interp", "current_frame", "thread_id",
            "native_thread_id", "datastack_chunk", "status",
        };
        private static readonly string[] ThreadStateFields315 = {
            "size", "prev", "next", "interp", "current_frame", "base_frame",
            "last_profiled_frame", "last_profiled_frame_seq", "thread_id",
            "native_thread_id", "datastack_chunk", "status", "holds_gil",
            "gil_requested", "current_exception", "exc_state",
        };
        private static readonly string[] TypeObjectFields314 = {
            "size", "tp_name", "tp_repr", "tp_flags",
        };
        private static readonly string[] TypeObjectFields315 = {
            "size", "tp_name", "tp_repr", "tp_flags", "tp_basicsize", "tp_dictoffset",
        };
        private static readonly string[] UnicodeObjectFields314 = {
            "size", "state", "length", "asciiobject_size",
        };
        private static readonly string[] UnicodeObjectFields315 = {
            "size", "state", "length", "asciiobject_size", "compactunicodeobject_size",
        };
        private static readonly string[] GcFields314 = {
            "size", "collecting",
        };
        private static readonly string[] GcFields315 = {
            "size", "collecting", "frame", "generation_stats_size", "generation_stats",
        };
        // Groups introduced in 3.15 (inserted mid-table relative to the 3.14 layout).
        private static readonly string[] ErrStackItemFields315 = {
            "exc_value",
        };
        private static readonly string[] HeapTypeObjectFields315 = {
            "size", "ht_cached_keys",
        };

        /// <summary>
        /// One interpreter version's ordered view of <c>_Py_DebugOffsets</c>: the groups in source
        /// order plus the total number of table bytes they occupy (header + one uint64 per field).
        /// The byte position of each field is implied by its position in this ordered list.
        /// </summary>
        private sealed class Layout {
            public readonly int Major;
            public readonly int Minor;
            public readonly Tuple<string, string[]>[] Groups;
            public readonly int TableSize;

            public Layout(int major, int minor, Tuple<string, string[]>[] groups) {
                Major = major;
                Minor = minor;
                Groups = groups;
                int fields = 0;
                foreach (var group in groups) {
                    fields += group.Item2.Length;
                }
                TableSize = HeaderSize + fields * sizeof(ulong);
            }
        }

        // CPython 3.14 (Include/internal/pycore_debug_offsets.h @ branch 3.14).
        private static readonly Layout Layout314 = new Layout(3, 14, new[] {
            Tuple.Create("runtime_state", RuntimeStateFields),
            Tuple.Create("interpreter_state", InterpreterStateFields),
            Tuple.Create("thread_state", ThreadStateFields314),
            Tuple.Create("interpreter_frame", InterpreterFrameFields),
            Tuple.Create("code_object", CodeObjectFields),
            Tuple.Create("pyobject", PyObjectFields),
            Tuple.Create("type_object", TypeObjectFields314),
            Tuple.Create("tuple_object", TupleObjectFields),
            Tuple.Create("list_object", ListObjectFields),
            Tuple.Create("set_object", SetObjectFields),
            Tuple.Create("dict_object", DictObjectFields),
            Tuple.Create("float_object", FloatObjectFields),
            Tuple.Create("long_object", LongObjectFields),
            Tuple.Create("bytes_object", BytesObjectFields),
            Tuple.Create("unicode_object", UnicodeObjectFields314),
            Tuple.Create("gc", GcFields314),
            Tuple.Create("gen_object", GenObjectFields),
            Tuple.Create("llist_node", LListNodeFields),
            Tuple.Create("debugger_support", DebuggerSupportFields),
        });

        // CPython 3.15 (Include/internal/pycore_debug_offsets.h @ branch 3.15). Relative to 3.14 the
        // thread_state group gained 7 fields (3 inserted after current_frame, 4 appended), a new
        // err_stackitem group was inserted before interpreter_frame, type_object gained 2 fields, a
        // new heap_type_object group follows it, and unicode_object / gc gained fields. Every other
        // group keeps its 3.14 order.
        private static readonly Layout Layout315 = new Layout(3, 15, new[] {
            Tuple.Create("runtime_state", RuntimeStateFields),
            Tuple.Create("interpreter_state", InterpreterStateFields),
            Tuple.Create("thread_state", ThreadStateFields315),
            Tuple.Create("err_stackitem", ErrStackItemFields315),
            Tuple.Create("interpreter_frame", InterpreterFrameFields),
            Tuple.Create("code_object", CodeObjectFields),
            Tuple.Create("pyobject", PyObjectFields),
            Tuple.Create("type_object", TypeObjectFields315),
            Tuple.Create("heap_type_object", HeapTypeObjectFields315),
            Tuple.Create("tuple_object", TupleObjectFields),
            Tuple.Create("list_object", ListObjectFields),
            Tuple.Create("set_object", SetObjectFields),
            Tuple.Create("dict_object", DictObjectFields),
            Tuple.Create("float_object", FloatObjectFields),
            Tuple.Create("long_object", LongObjectFields),
            Tuple.Create("bytes_object", BytesObjectFields),
            Tuple.Create("unicode_object", UnicodeObjectFields315),
            Tuple.Create("gc", GcFields315),
            Tuple.Create("gen_object", GenObjectFields),
            Tuple.Create("llist_node", LListNodeFields),
            Tuple.Create("debugger_support", DebuggerSupportFields),
        });

        private static readonly Layout[] KnownLayouts = { Layout314, Layout315 };

        // Largest table across all known layouts. TryRead reads this many bytes before it knows the
        // version, which is safe because the table is only the first member of the much larger
        // _PyRuntime global; TryParse then consumes only the matching layout's TableSize.
        private static readonly int MaxTableSize = GetMaxTableSize();

        private static int GetMaxTableSize() {
            int maxTableSize = 0;
            foreach (var layout in KnownLayouts) {
                maxTableSize = Math.Max(maxTableSize, layout.TableSize);
            }
            return maxTableSize;
        }

        private static Layout GetLayout(int major, int minor) {
            foreach (var layout in KnownLayouts) {
                if (layout.Major == major && layout.Minor == minor) {
                    return layout;
                }
            }
            return null;
        }

        private readonly System.Collections.Generic.Dictionary<string, ulong> _offsets;

        /// <summary>PY_VERSION_HEX recorded by the interpreter that produced this table.</summary>
        public ulong Version { get; }

        /// <summary>True if the interpreter was built free-threaded (PEP 703, Py_GIL_DISABLED).</summary>
        public bool FreeThreaded { get; }

        public int Major => (int)((Version >> 24) & 0xFF);
        public int Minor => (int)((Version >> 16) & 0xFF);
        public int Micro => (int)((Version >> 8) & 0xFF);

        /// <summary>True when this table describes a CPython 3.14 layout.</summary>
        public bool Is314 => Major == 3 && Minor == 14;

        /// <summary>True when this table describes a CPython 3.15 layout.</summary>
        public bool Is315 => Major == 3 && Minor == 15;

        /// <summary>
        /// True when this reader knows the exact table layout for the recorded version and therefore
        /// parsed every field at an authoritative position (currently CPython 3.14 and 3.15). Callers
        /// that source offsets from the table must gate on this; when false, fall back to the PDB.
        /// </summary>
        public bool IsSupported => GetLayout(Major, Minor) != null;

        /// <summary>
        /// Total number of table bytes (header + all offset words) for a known layout, or 0 if the
        /// version is not one this reader understands. Exposed for tests.
        /// </summary>
        internal static int TableSizeFor(int major, int minor) {
            var layout = GetLayout(major, minor);
            return layout != null ? layout.TableSize : 0;
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
        /// Validates the cookie, reads the version word, selects the matching layout (3.14 or 3.15) and
        /// verifies the buffer is large enough for it. Returns false for versions this reader does not
        /// understand, so callers fall back to the PDB.
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

            int major = (int)((version >> 24) & 0xFF);
            int minor = (int)((version >> 16) & 0xFF);
            var layout = GetLayout(major, minor);
            if (layout == null) {
                error = "unsupported _Py_DebugOffsets version " + major + "." + minor;
                return false;
            }

            if (data.Length < layout.TableSize) {
                error = "buffer too small for _Py_DebugOffsets " + major + "." + minor +
                    " layout (need " + layout.TableSize + " bytes, have " + data.Length + ")";
                return false;
            }

            var offsets = new System.Collections.Generic.Dictionary<string, ulong>(layout.TableSize / sizeof(ulong));
            int pos = HeaderSize;
            foreach (var group in layout.Groups) {
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

            // Read enough bytes for the largest layout we know; the version prefix then tells TryParse
            // which layout to consume. The table is the first member of the much larger _PyRuntime
            // global, so over-reading here is safe.
            var buffer = new byte[MaxTableSize];
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

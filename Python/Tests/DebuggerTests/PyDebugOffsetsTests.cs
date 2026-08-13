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
using Microsoft.PythonTools.Debugger.Concord.Proxies.Structs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DebuggerTests {
    /// <summary>
    /// Locks down the managed reader of CPython 3.14's self-describing <c>_Py_DebugOffsets</c>
    /// table (<see cref="PyDebugOffsets"/>) against bytes recorded from real 3.14.6 interpreters.
    /// The two vectors are the standard (GIL) build and the free-threaded (<c>python3.14t</c>,
    /// PEP 703) build; each byte blob is the exact <c>_Py_DebugOffsets</c> region read out of the
    /// live <c>_PyRuntime</c> global, and every expected offset was cross-checked in-process
    /// against live objects (e.g. <c>code_object.linetable</c> actually points at
    /// <c>co.co_linetable</c>) by the generator that produced these vectors.
    /// </summary>
    [TestClass]
    public class PyDebugOffsetsTests {
        // Recorded _Py_DebugOffsets bytes from CPython 3.14.6 (standard, GIL-enabled build).
        internal const string RawV314 =
            "7864656275677079f0060e03000000000000000000000000d0d0040000000000100300000000000028030000000000008871030000000000701c0000" +
            "00000000681c000000000000a81c000000000000b81c000000000000e81c000000000000001e000000000000f01d000000000000f81d000000000000" +
            "1000000000000000581e0000000000000000000000000000681e000000000000601e000000000000901e000000000000000000000000000038030000" +
            "00000000000000000000000008000000000000001000000000000000480000000000000098000000000000009c00000000000000e000000000000000" +
            "2000000000000000580000000000000008000000000000000000000000000000380000000000000050000000000000004a0000000000000040000000" +
            "000000000000000000000000d80000000000000070000000000000007800000000000000800000000000000088000000000000004400000000000000" +
            "340000000000000060000000000000006800000000000000d000000000000000000000000000000010000000000000000800000000000000a0010000" +
            "0000000018000000000000005800000000000000a8000000000000002800000000000000200000000000000010000000000000002800000000000000" +
            "18000000000000001000000000000000c800000000000000180000000000000028000000000000002000000000000000300000000000000020000000" +
            "000000002800000000000000180000000000000010000000000000002000000000000000100000000000000018000000000000002800000000000000" +
            "1000000000000000200000000000000040000000000000002000000000000000100000000000000028000000000000000801000000000000c0000000" +
            "00000000a000000000000000180000000000000048000000000000004300000000000000000000000000000008000000000000001800000000000000" +
            "2801000000000000e01e000000000000000000000000000004000000000000000002000000000000";

        // Recorded _Py_DebugOffsets bytes from CPython 3.14.6 free-threaded build (python3.14t).
        internal const string RawV314T =
            "7864656275677079f0060e03000000000100000000000000c0830500000000001003000000000000280300000000000000c3030000000000701c0000" +
            "00000000681c000000000000a81c000000000000b81c000000000000e81c000000000000181e000000000000081e000000000000101e000000000000" +
            "1000000000000000701e0000000000000000000000000000801e000000000000781e000000000000a81e0000000000001c4400000000000038030000" +
            "00000000000000000000000008000000000000001000000000000000480000000000000098000000000000009c00000000000000e000000000000000" +
            "2000000000000000580000000000000008000000000000000000000000000000380000000000000050000000000000004e0000000000000040000000" +
            "000000004800000000000000f00000000000000080000000000000008800000000000000900000000000000098000000000000005400000000000000" +
            "440000000000000070000000000000007800000000000000e800000000000000e00000000000000020000000000000001800000000000000b0010000" +
            "0000000028000000000000006800000000000000b8000000000000003800000000000000300000000000000020000000000000003800000000000000" +
            "28000000000000002000000000000000d800000000000000280000000000000038000000000000003000000000000000400000000000000030000000" +
            "000000003800000000000000280000000000000020000000000000003000000000000000200000000000000028000000000000003800000000000000" +
            "2000000000000000300000000000000050000000000000003000000000000000200000000000000038000000000000002001000000000000c0000000" +
            "00000000b000000000000000280000000000000058000000000000005300000000000000000000000000000008000000000000001800000000000000" +
            "2801000000000000f81e000000000000000000000000000004000000000000000002000000000000";

        // Recorded _Py_DebugOffsets bytes from CPython 3.15.0rc1 (standard, GIL-enabled build).
        internal const string RawV315 =
            "7864656275677079c1000f03000000000000000000000000a0440500000000009003000000000000a803000000000000f872030000000000681c0000" +
            "00000000601c000000000000a01c000000000000b01c000000000000e01c000000000000a01d000000000000901d000000000000981d000000000000" +
            "1000000000000000281e0000000000000000000000000000381e000000000000301e000000000000601e000000000000000000000000000070030000" +
            "000000000000000000000000080000000000000010000000000000004800000000000000500000000000000058000000000000006000000000000000" +
            "b000000000000000b400000000000000f800000000000000200000000000000024000000000000002800000000000000880000000000000018010000" +
            "000000000000000000000000580000000000000008000000000000000000000000000000380000000000000050000000000000004a00000000000000" +
            "40000000000000000000000000000000d800000000000000700000000000000078000000000000008000000000000000880000000000000044000000" +
            "00000000340000000000000060000000000000006800000000000000d000000000000000000000000000000010000000000000000800000000000000" +
            "a80100000000000018000000000000005800000000000000a80000000000000020000000000000002001000000000000b00300000000000078030000" +
            "00000000280000000000000020000000000000001000000000000000280000000000000018000000000000001000000000000000c800000000000000" +
            "180000000000000028000000000000002000000000000000300000000000000020000000000000002800000000000000180000000000000010000000" +
            "000000002000000000000000100000000000000018000000000000002800000000000000100000000000000020000000000000004000000000000000" +
            "2000000000000000100000000000000028000000000000003800000000000000b0000000000000007000000000000000780000000000000058040000" +
            "000000006800000000000000a00000000000000018000000000000004800000000000000430000000000000000000000000000000800000000000000" +
            "18000000000000005001000000000000b01e000000000000000000000000000004000000000000000002000000000000";

        // Recorded _Py_DebugOffsets bytes from CPython 3.15.0rc1 free-threaded build (python3.15t).
        internal const string RawV315T =
            "7864656275677079c1000f03000000000100000000000000002d0600000000009003000000000000a80300000000000080c4030000000000681c0000" +
            "00000000601c000000000000a01c000000000000b01c000000000000e01c000000000000a01d000000000000901d000000000000981d000000000000" +
            "1000000000000000301e0000000000000000000000000000401e000000000000381e000000000000681e0000000000001c4400000000000070030000" +
            "000000000000000000000000080000000000000010000000000000004800000000000000500000000000000058000000000000006000000000000000" +
            "b000000000000000b400000000000000f800000000000000200000000000000024000000000000002800000000000000880000000000000018010000" +
            "000000000000000000000000580000000000000008000000000000000000000000000000380000000000000050000000000000004e00000000000000" +
            "40000000000000004800000000000000f000000000000000800000000000000088000000000000009000000000000000980000000000000054000000" +
            "00000000440000000000000070000000000000007800000000000000e800000000000000e00000000000000020000000000000001800000000000000" +
            "b80100000000000028000000000000006800000000000000b80000000000000030000000000000003001000000000000c80300000000000088030000" +
            "00000000380000000000000030000000000000002000000000000000380000000000000028000000000000002000000000000000d800000000000000" +
            "280000000000000038000000000000003000000000000000400000000000000030000000000000003800000000000000280000000000000020000000" +
            "000000003000000000000000200000000000000028000000000000003800000000000000200000000000000030000000000000005000000000000000" +
            "3000000000000000200000000000000038000000000000004800000000000000b00000000000000070000000000000007800000000000000d8000000" +
            "000000006800000000000000b00000000000000028000000000000005800000000000000530000000000000000000000000000000800000000000000" +
            "18000000000000005001000000000000b81e000000000000000000000000000004000000000000000002000000000000";

        // Full expected offsets for the standard 3.14.6 build (every field in the table).
        private static readonly Tuple<string, string, ulong>[] ExpectedV314 = {
            T("runtime_state", "size", 315600), T("runtime_state", "finalizing", 784), T("runtime_state", "interpreters_head", 808),
            T("interpreter_state", "size", 225672), T("interpreter_state", "id", 7280), T("interpreter_state", "next", 7272),
            T("interpreter_state", "threads_head", 7336), T("interpreter_state", "threads_main", 7352), T("interpreter_state", "gc", 7400),
            T("interpreter_state", "imports_modules", 7680), T("interpreter_state", "sysdict", 7664), T("interpreter_state", "builtins", 7672),
            T("interpreter_state", "ceval_gil", 16), T("interpreter_state", "gil_runtime_state", 7768), T("interpreter_state", "gil_runtime_state_enabled", 0),
            T("interpreter_state", "gil_runtime_state_locked", 7784), T("interpreter_state", "gil_runtime_state_holder", 7776),
            T("interpreter_state", "code_object_generation", 7824), T("interpreter_state", "tlbc_generation", 0),
            T("thread_state", "size", 824), T("thread_state", "prev", 0), T("thread_state", "next", 8), T("thread_state", "interp", 16),
            T("thread_state", "current_frame", 72), T("thread_state", "thread_id", 152), T("thread_state", "native_thread_id", 156),
            T("thread_state", "datastack_chunk", 224), T("thread_state", "status", 32),
            T("interpreter_frame", "size", 88), T("interpreter_frame", "previous", 8), T("interpreter_frame", "executable", 0),
            T("interpreter_frame", "instr_ptr", 56), T("interpreter_frame", "localsplus", 80), T("interpreter_frame", "owner", 74),
            T("interpreter_frame", "stackpointer", 64), T("interpreter_frame", "tlbc_index", 0),
            T("code_object", "size", 216), T("code_object", "filename", 112), T("code_object", "name", 120), T("code_object", "qualname", 128),
            T("code_object", "linetable", 136), T("code_object", "firstlineno", 68), T("code_object", "argcount", 52),
            T("code_object", "localsplusnames", 96), T("code_object", "localspluskinds", 104), T("code_object", "co_code_adaptive", 208),
            T("code_object", "co_tlbc", 0),
            T("pyobject", "size", 16), T("pyobject", "ob_type", 8),
            T("type_object", "size", 416), T("type_object", "tp_name", 24), T("type_object", "tp_repr", 88), T("type_object", "tp_flags", 168),
            T("tuple_object", "size", 40), T("tuple_object", "ob_item", 32), T("tuple_object", "ob_size", 16),
            T("list_object", "size", 40), T("list_object", "ob_item", 24), T("list_object", "ob_size", 16),
            T("set_object", "size", 200), T("set_object", "used", 24), T("set_object", "table", 40), T("set_object", "mask", 32),
            T("dict_object", "size", 48), T("dict_object", "ma_keys", 32), T("dict_object", "ma_values", 40),
            T("float_object", "size", 24), T("float_object", "ob_fval", 16),
            T("long_object", "size", 32), T("long_object", "lv_tag", 16), T("long_object", "ob_digit", 24),
            T("bytes_object", "size", 40), T("bytes_object", "ob_size", 16), T("bytes_object", "ob_sval", 32),
            T("unicode_object", "size", 64), T("unicode_object", "state", 32), T("unicode_object", "length", 16), T("unicode_object", "asciiobject_size", 40),
            T("gc", "size", 264), T("gc", "collecting", 192),
            T("gen_object", "size", 160), T("gen_object", "gi_name", 24), T("gen_object", "gi_iframe", 72), T("gen_object", "gi_frame_state", 67),
            T("llist_node", "next", 0), T("llist_node", "prev", 8),
            T("debugger_support", "eval_breaker", 24), T("debugger_support", "remote_debugger_support", 296),
            T("debugger_support", "remote_debugging_enabled", 7904), T("debugger_support", "debugger_pending_call", 0),
            T("debugger_support", "debugger_script_path", 4), T("debugger_support", "debugger_script_path_size", 512),
        };

        // Full expected offsets for the standard 3.15.0rc1 build (every field in the table).
        private static readonly Tuple<string, string, ulong>[] ExpectedV315 = {
            T("runtime_state", "size", 345248), T("runtime_state", "finalizing", 912), T("runtime_state", "interpreters_head", 936),
            T("interpreter_state", "size", 226040), T("interpreter_state", "id", 7272), T("interpreter_state", "next", 7264), T("interpreter_state", "threads_head", 7328), T("interpreter_state", "threads_main", 7344), T("interpreter_state", "gc", 7392), T("interpreter_state", "imports_modules", 7584), T("interpreter_state", "sysdict", 7568), T("interpreter_state", "builtins", 7576), T("interpreter_state", "ceval_gil", 16), T("interpreter_state", "gil_runtime_state", 7720), T("interpreter_state", "gil_runtime_state_enabled", 0), T("interpreter_state", "gil_runtime_state_locked", 7736), T("interpreter_state", "gil_runtime_state_holder", 7728), T("interpreter_state", "code_object_generation", 7776), T("interpreter_state", "tlbc_generation", 0),
            T("thread_state", "size", 880), T("thread_state", "prev", 0), T("thread_state", "next", 8), T("thread_state", "interp", 16), T("thread_state", "current_frame", 72), T("thread_state", "base_frame", 80), T("thread_state", "last_profiled_frame", 88), T("thread_state", "last_profiled_frame_seq", 96), T("thread_state", "thread_id", 176), T("thread_state", "native_thread_id", 180), T("thread_state", "datastack_chunk", 248), T("thread_state", "status", 32), T("thread_state", "holds_gil", 36), T("thread_state", "gil_requested", 40), T("thread_state", "current_exception", 136), T("thread_state", "exc_state", 280),
            T("err_stackitem", "exc_value", 0),
            T("interpreter_frame", "size", 88), T("interpreter_frame", "previous", 8), T("interpreter_frame", "executable", 0), T("interpreter_frame", "instr_ptr", 56), T("interpreter_frame", "localsplus", 80), T("interpreter_frame", "owner", 74), T("interpreter_frame", "stackpointer", 64), T("interpreter_frame", "tlbc_index", 0),
            T("code_object", "size", 216), T("code_object", "filename", 112), T("code_object", "name", 120), T("code_object", "qualname", 128), T("code_object", "linetable", 136), T("code_object", "firstlineno", 68), T("code_object", "argcount", 52), T("code_object", "localsplusnames", 96), T("code_object", "localspluskinds", 104), T("code_object", "co_code_adaptive", 208), T("code_object", "co_tlbc", 0),
            T("pyobject", "size", 16), T("pyobject", "ob_type", 8),
            T("type_object", "size", 424), T("type_object", "tp_name", 24), T("type_object", "tp_repr", 88), T("type_object", "tp_flags", 168), T("type_object", "tp_basicsize", 32), T("type_object", "tp_dictoffset", 288),
            T("heap_type_object", "size", 944), T("heap_type_object", "ht_cached_keys", 888),
            T("tuple_object", "size", 40), T("tuple_object", "ob_item", 32), T("tuple_object", "ob_size", 16),
            T("list_object", "size", 40), T("list_object", "ob_item", 24), T("list_object", "ob_size", 16),
            T("set_object", "size", 200), T("set_object", "used", 24), T("set_object", "table", 40), T("set_object", "mask", 32),
            T("dict_object", "size", 48), T("dict_object", "ma_keys", 32), T("dict_object", "ma_values", 40),
            T("float_object", "size", 24), T("float_object", "ob_fval", 16),
            T("long_object", "size", 32), T("long_object", "lv_tag", 16), T("long_object", "ob_digit", 24),
            T("bytes_object", "size", 40), T("bytes_object", "ob_size", 16), T("bytes_object", "ob_sval", 32),
            T("unicode_object", "size", 64), T("unicode_object", "state", 32), T("unicode_object", "length", 16), T("unicode_object", "asciiobject_size", 40), T("unicode_object", "compactunicodeobject_size", 56),
            T("gc", "size", 176), T("gc", "collecting", 112), T("gc", "frame", 120), T("gc", "generation_stats_size", 1112), T("gc", "generation_stats", 104),
            T("gen_object", "size", 160), T("gen_object", "gi_name", 24), T("gen_object", "gi_iframe", 72), T("gen_object", "gi_frame_state", 67),
            T("llist_node", "next", 0), T("llist_node", "prev", 8),
            T("debugger_support", "eval_breaker", 24), T("debugger_support", "remote_debugger_support", 336), T("debugger_support", "remote_debugging_enabled", 7856), T("debugger_support", "debugger_pending_call", 0), T("debugger_support", "debugger_script_path", 4), T("debugger_support", "debugger_script_path_size", 512),
        };

        private static Tuple<string, string, ulong> T(string group, string field, ulong value) {
            return Tuple.Create(group, field, value);
        }

        private static byte[] FromHex(string hex) {
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++) {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }

        [TestMethod, Priority(0)]
        public void TryParse_ParsesStandard314Header() {
            PyDebugOffsets result;
            string error;
            Assert.IsTrue(PyDebugOffsets.TryParse(FromHex(RawV314), out result, out error), error);
            Assert.AreEqual(3, result.Major);
            Assert.AreEqual(14, result.Minor);
            Assert.AreEqual(6, result.Micro);
            Assert.AreEqual(0x30e06f0UL, result.Version);
            Assert.IsTrue(result.Is314);
            Assert.IsFalse(result.FreeThreaded);
        }

        [TestMethod, Priority(0)]
        public void TryParse_ReadsEveryStandard314Offset() {
            PyDebugOffsets result;
            string error;
            Assert.IsTrue(PyDebugOffsets.TryParse(FromHex(RawV314), out result, out error), error);
            foreach (var expected in ExpectedV314) {
                ulong actual = result.Offset(expected.Item1, expected.Item2);
                Assert.AreEqual(expected.Item3, actual, expected.Item1 + "." + expected.Item2);
            }
        }

        [TestMethod, Priority(0)]
        public void TryParse_ParsesFreeThreaded314() {
            PyDebugOffsets result;
            string error;
            Assert.IsTrue(PyDebugOffsets.TryParse(FromHex(RawV314T), out result, out error), error);
            Assert.IsTrue(result.Is314);
            Assert.IsTrue(result.FreeThreaded);

            // instr_ptr / current_frame happen to land at the same place as the standard build...
            Assert.AreEqual(56UL, result.Offset("interpreter_frame", "instr_ptr"));
            Assert.AreEqual(72UL, result.Offset("thread_state", "current_frame"));

            // ...but the free-threaded build inserts extra fields, so object layouts shift. This is
            // precisely why reading the table beats hard-coding offsets: linetable/firstlineno move
            // within PyCodeObject and ob_type moves within PyObject.
            Assert.AreEqual(152UL, result.Offset("code_object", "linetable"));
            Assert.AreEqual(84UL, result.Offset("code_object", "firstlineno"));
            Assert.AreEqual(24UL, result.Offset("pyobject", "ob_type"));

            // The TLBC (thread-local bytecode) fields are only non-zero in the free-threaded build.
            Assert.AreEqual(72UL, result.Offset("interpreter_frame", "tlbc_index"));
            Assert.AreEqual(224UL, result.Offset("code_object", "co_tlbc"));
            Assert.AreEqual(17436UL, result.Offset("interpreter_state", "tlbc_generation"));
        }

        [TestMethod, Priority(0)]
        public void TryParse_TlbcFieldsZeroInStandardBuild() {
            PyDebugOffsets result;
            string error;
            Assert.IsTrue(PyDebugOffsets.TryParse(FromHex(RawV314), out result, out error), error);
            Assert.AreEqual(0UL, result.Offset("interpreter_frame", "tlbc_index"));
            Assert.AreEqual(0UL, result.Offset("code_object", "co_tlbc"));
            Assert.AreEqual(0UL, result.Offset("interpreter_state", "tlbc_generation"));
        }

        [TestMethod, Priority(0)]
        public void TryParse_RejectsMissingCookie() {
            var data = FromHex(RawV314);
            data[0] = (byte)'X';
            PyDebugOffsets result;
            string error;
            Assert.IsFalse(PyDebugOffsets.TryParse(data, out result, out error));
            Assert.IsNull(result);
            Assert.IsNotNull(error);
        }

        [TestMethod, Priority(0)]
        public void TryParse_RejectsTooShortBuffer() {
            PyDebugOffsets result;
            string error;
            Assert.IsFalse(PyDebugOffsets.TryParse(new byte[8], out result, out error));
            Assert.IsFalse(PyDebugOffsets.TryParse(null, out result, out error));

            // A valid header but a buffer too short for the full 3.14 layout is rejected.
            var truncated = new byte[PyDebugOffsets.TableSizeFor(3, 14) - 1];
            var full = FromHex(RawV314);
            Array.Copy(full, truncated, truncated.Length);
            Assert.IsFalse(PyDebugOffsets.TryParse(truncated, out result, out error));
        }

        [TestMethod, Priority(0)]
        public void TableSize_MatchesRecordedVectorLength() {
            Assert.AreEqual(FromHex(RawV314).Length, PyDebugOffsets.TableSizeFor(3, 14));
            Assert.AreEqual(FromHex(RawV314T).Length, PyDebugOffsets.TableSizeFor(3, 14));
            Assert.AreEqual(FromHex(RawV315).Length, PyDebugOffsets.TableSizeFor(3, 15));
            Assert.AreEqual(FromHex(RawV315T).Length, PyDebugOffsets.TableSizeFor(3, 15));
        }

        // ------------------------------------------------------------------
        // CPython 3.15 layout.
        //
        // RawV315 / RawV315T are real _Py_DebugOffsets tables recorded in-process from CPython
        // 3.15.0rc1 (standard and free-threaded builds). BuildSynthetic315 additionally builds a blob
        // whose field i simply holds the value i (i = 0..N-1) so we can assert the reader places each
        // group/field at the exact ordinal the CPython 3.15 header dictates
        // (Include/internal/pycore_debug_offsets.h @ branch 3.15). The expected ordinals are derived
        // independently from that header's group/field order, so the assertions are not circular with
        // the reader's own field lists:
        //   runtime_state       3   [0..2]
        //   interpreter_state  16   [3..18]
        //   thread_state       16   [19..34]   (+3 after current_frame, +4 appended vs 3.14)
        //   err_stackitem       1   [35]       (new in 3.15)
        //   interpreter_frame   8   [36..43]
        //   code_object        11   [44..54]
        //   pyobject            2   [55..56]
        //   type_object         6   [57..62]   (+tp_basicsize, +tp_dictoffset vs 3.14)
        //   heap_type_object    2   [63..64]   (new in 3.15)
        //   ... remaining groups follow ...    (108 fields total => 888 bytes)
        internal static byte[] BuildSynthetic315() {
            int size = PyDebugOffsets.TableSizeFor(3, 15);
            Assert.AreEqual(888, size, "3.15 layout should be 24-byte header + 108 uint64 fields");
            var data = new byte[size];

            for (int i = 0; i < 8; i++) {
                data[i] = (byte)"xdebugpy"[i];
            }
            // PY_VERSION_HEX for 3.15.1: major=3, minor=15, micro=1.
            Array.Copy(BitConverter.GetBytes(0x030f0100UL), 0, data, 8, 8);
            // free_threaded = 0 (standard build).
            Array.Copy(BitConverter.GetBytes(0UL), 0, data, 16, 8);

            int fieldCount = (size - 24) / 8;
            for (int i = 0; i < fieldCount; i++) {
                Array.Copy(BitConverter.GetBytes((ulong)i), 0, data, 24 + i * 8, 8);
            }
            return data;
        }

        [TestMethod, Priority(0)]
        public void TryParse_ParsesStandard315Header() {
            PyDebugOffsets result;
            string error;
            Assert.IsTrue(PyDebugOffsets.TryParse(FromHex(RawV315), out result, out error), error);
            Assert.AreEqual(3, result.Major);
            Assert.AreEqual(15, result.Minor);
            Assert.AreEqual(0, result.Micro);
            Assert.AreEqual(0x30f00c1UL, result.Version);
            Assert.IsFalse(result.Is314);
            Assert.IsTrue(result.Is315);
            Assert.IsTrue(result.IsSupported);
            Assert.IsFalse(result.FreeThreaded);
        }

        [TestMethod, Priority(0)]
        public void TryParse_ReadsEveryStandard315Offset() {
            PyDebugOffsets result;
            string error;
            Assert.IsTrue(PyDebugOffsets.TryParse(FromHex(RawV315), out result, out error), error);
            foreach (var expected in ExpectedV315) {
                ulong actual = result.Offset(expected.Item1, expected.Item2);
                Assert.AreEqual(expected.Item3, actual, expected.Item1 + "." + expected.Item2);
            }
        }

        [TestMethod, Priority(0)]
        public void TryParse_ParsesFreeThreaded315() {
            PyDebugOffsets result;
            string error;
            Assert.IsTrue(PyDebugOffsets.TryParse(FromHex(RawV315T), out result, out error), error);
            Assert.IsTrue(result.Is315);
            Assert.IsTrue(result.FreeThreaded);

            // instr_ptr / current_frame land at the same place as the standard 3.15 build...
            Assert.AreEqual(56UL, result.Offset("interpreter_frame", "instr_ptr"));
            Assert.AreEqual(72UL, result.Offset("thread_state", "current_frame"));

            // ...but the free-threaded build inserts extra fields, so object layouts shift. This is
            // precisely why reading the table beats hard-coding offsets: linetable/firstlineno move
            // within PyCodeObject and ob_type moves within PyObject (all +16 vs the standard build).
            Assert.AreEqual(152UL, result.Offset("code_object", "linetable"));
            Assert.AreEqual(84UL, result.Offset("code_object", "firstlineno"));
            Assert.AreEqual(24UL, result.Offset("pyobject", "ob_type"));

            // The TLBC (thread-local bytecode) fields are only non-zero in the free-threaded build.
            Assert.AreEqual(72UL, result.Offset("interpreter_frame", "tlbc_index"));
            Assert.AreEqual(224UL, result.Offset("code_object", "co_tlbc"));
            Assert.AreEqual(17436UL, result.Offset("interpreter_state", "tlbc_generation"));
        }

        [TestMethod, Priority(0)]
        public void TryParse_TlbcFieldsZeroInStandard315Build() {
            PyDebugOffsets result;
            string error;
            Assert.IsTrue(PyDebugOffsets.TryParse(FromHex(RawV315), out result, out error), error);
            Assert.AreEqual(0UL, result.Offset("interpreter_frame", "tlbc_index"));
            Assert.AreEqual(0UL, result.Offset("code_object", "co_tlbc"));
            Assert.AreEqual(0UL, result.Offset("interpreter_state", "tlbc_generation"));
        }

        [TestMethod, Priority(0)]
        public void TryParse_Parses315SyntheticHeader() {
            PyDebugOffsets result;
            string error;
            Assert.IsTrue(PyDebugOffsets.TryParse(BuildSynthetic315(), out result, out error), error);
            Assert.AreEqual(3, result.Major);
            Assert.AreEqual(15, result.Minor);
            Assert.AreEqual(1, result.Micro);
            Assert.IsFalse(result.Is314);
            Assert.IsTrue(result.Is315);
            Assert.IsTrue(result.IsSupported);
            Assert.IsFalse(result.FreeThreaded);
        }

        [TestMethod, Priority(0)]
        public void TryParse_Places315GroupsAtHeaderOrdinals() {
            PyDebugOffsets result;
            string error;
            Assert.IsTrue(PyDebugOffsets.TryParse(BuildSynthetic315(), out result, out error), error);

            // thread_state gained fields in 3.15; current_frame stays at ordinal 23 but the appended
            // fields (holds_gil..exc_state) land after the inserted profiling fields.
            Assert.AreEqual(23UL, result.Offset("thread_state", "current_frame"));
            Assert.AreEqual(24UL, result.Offset("thread_state", "base_frame"));
            Assert.AreEqual(27UL, result.Offset("thread_state", "thread_id"));
            Assert.AreEqual(34UL, result.Offset("thread_state", "exc_state"));

            // err_stackitem is a new group inserted between thread_state and interpreter_frame.
            Assert.AreEqual(35UL, result.Offset("err_stackitem", "exc_value"));

            // interpreter_frame shifts by the 3.15 insertions but keeps its internal order.
            Assert.AreEqual(38UL, result.Offset("interpreter_frame", "executable"));
            Assert.AreEqual(39UL, result.Offset("interpreter_frame", "instr_ptr"));
            Assert.AreEqual(40UL, result.Offset("interpreter_frame", "localsplus"));
            Assert.AreEqual(41UL, result.Offset("interpreter_frame", "owner"));

            // code_object: the hot-path fields the in-process line decoder consumes.
            Assert.AreEqual(48UL, result.Offset("code_object", "linetable"));
            Assert.AreEqual(49UL, result.Offset("code_object", "firstlineno"));
            Assert.AreEqual(53UL, result.Offset("code_object", "co_code_adaptive"));

            // type_object gained fields; heap_type_object is a new group right after it.
            Assert.AreEqual(61UL, result.Offset("type_object", "tp_basicsize"));
            Assert.AreEqual(62UL, result.Offset("type_object", "tp_dictoffset"));
            Assert.AreEqual(64UL, result.Offset("heap_type_object", "ht_cached_keys"));
        }

        [TestMethod, Priority(0)]
        public void TryParse_Rejects315BufferTooShortForLayout() {
            var full = BuildSynthetic315();
            var truncated = new byte[full.Length - 1];
            Array.Copy(full, truncated, truncated.Length);
            PyDebugOffsets result;
            string error;
            Assert.IsFalse(PyDebugOffsets.TryParse(truncated, out result, out error));
            Assert.IsNull(result);
            Assert.IsNotNull(error);
        }

        [TestMethod, Priority(0)]
        public void TryParse_RejectsUnknownVersion() {
            // A well-formed header advertising an unknown version (e.g. a future 3.16) is rejected so
            // callers fall back to the PDB rather than mis-parsing an unknown layout.
            var data = BuildSynthetic315();
            Array.Copy(BitConverter.GetBytes(0x03100000UL), 0, data, 8, 8); // 3.16.0
            PyDebugOffsets result;
            string error;
            Assert.IsFalse(PyDebugOffsets.TryParse(data, out result, out error));
            Assert.IsNull(result);
            Assert.IsNotNull(error);
        }

        [TestMethod, Priority(0)]
        public void TableSizeFor_UnknownVersionIsZero() {
            Assert.AreEqual(0, PyDebugOffsets.TableSizeFor(3, 13));
            Assert.AreEqual(0, PyDebugOffsets.TableSizeFor(3, 16));
            Assert.AreEqual(760, PyDebugOffsets.TableSizeFor(3, 14));
            Assert.AreEqual(888, PyDebugOffsets.TableSizeFor(3, 15));
        }
    }
}

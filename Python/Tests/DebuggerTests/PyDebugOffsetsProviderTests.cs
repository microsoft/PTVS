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
    /// Verifies the hot-path offset provider (<see cref="DebugOffsetsFieldProvider"/>) that lets
    /// <c>StructProxy</c> source CPython 3.14 frame / code-object / thread-state field offsets from the
    /// self-describing <c>_Py_DebugOffsets</c> table instead of the PDB. Everything is exercised against
    /// the same real 3.14.6 vectors used by <see cref="PyDebugOffsetsTests"/>, mapping the CPython
    /// struct/field names that <c>StructProxy</c> passes in to the values the table reports.
    /// </summary>
    [TestClass]
    public class DebugOffsetsFieldProviderTests {
        private static byte[] FromHex(string hex) {
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++) {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }

        private static DebugOffsetsFieldProvider Provider(string rawHex) {
            PyDebugOffsets offsets;
            string error;
            Assert.IsTrue(PyDebugOffsets.TryParse(FromHex(rawHex), out offsets, out error), error);
            return new DebugOffsetsFieldProvider(offsets);
        }

        private static long Offset(IStructFieldOffsetProvider provider, string structName, string fieldName) {
            long offset;
            Assert.IsTrue(provider.TryGetFieldOffset(structName, fieldName, out offset),
                "expected a mapped offset for " + structName + "." + fieldName);
            return offset;
        }

        [TestMethod, Priority(0)]
        public void Standard314_MapsInterpreterFrameHotPath() {
            var provider = Provider(PyDebugOffsetsTests.RawV314);
            Assert.AreEqual(8L, Offset(provider, "_PyInterpreterFrame", "previous"));
            Assert.AreEqual(0L, Offset(provider, "_PyInterpreterFrame", "f_executable"));
            Assert.AreEqual(56L, Offset(provider, "_PyInterpreterFrame", "instr_ptr"));
            Assert.AreEqual(80L, Offset(provider, "_PyInterpreterFrame", "localsplus"));
            Assert.AreEqual(74L, Offset(provider, "_PyInterpreterFrame", "owner"));
        }

        [TestMethod, Priority(0)]
        public void Standard314_MapsCodeObjectHotPath() {
            var provider = Provider(PyDebugOffsetsTests.RawV314);
            Assert.AreEqual(112L, Offset(provider, "PyCodeObject", "co_filename"));
            Assert.AreEqual(120L, Offset(provider, "PyCodeObject", "co_name"));
            Assert.AreEqual(68L, Offset(provider, "PyCodeObject", "co_firstlineno"));
            Assert.AreEqual(96L, Offset(provider, "PyCodeObject", "co_localsplusnames"));
            Assert.AreEqual(104L, Offset(provider, "PyCodeObject", "co_localspluskinds"));
            Assert.AreEqual(208L, Offset(provider, "PyCodeObject", "co_code_adaptive"));
            Assert.AreEqual(136L, Offset(provider, "PyCodeObject", "co_linetable"));
        }

        [TestMethod, Priority(0)]
        public void Standard314_MapsThreadStateHotPath() {
            var provider = Provider(PyDebugOffsetsTests.RawV314);
            Assert.AreEqual(8L, Offset(provider, "_ts", "next"));
            Assert.AreEqual(16L, Offset(provider, "_ts", "interp"));
            Assert.AreEqual(152L, Offset(provider, "_ts", "thread_id"));
            Assert.AreEqual(72L, Offset(provider, "_ts", "current_frame"));
        }

        [TestMethod, Priority(0)]
        public void FreeThreaded314_TracksShiftedLayout() {
            var standard = Provider(PyDebugOffsetsTests.RawV314);
            var freeThreaded = Provider(PyDebugOffsetsTests.RawV314T);

            // The whole point of sourcing from the table: the free-threaded build shifts these fields,
            // and the provider surfaces the shifted offsets with no code change (co_linetable 136->152,
            // co_firstlineno 68->84).
            Assert.AreEqual(136L, Offset(standard, "PyCodeObject", "co_linetable"));
            Assert.AreEqual(152L, Offset(freeThreaded, "PyCodeObject", "co_linetable"));
            Assert.AreEqual(68L, Offset(standard, "PyCodeObject", "co_firstlineno"));
            Assert.AreEqual(84L, Offset(freeThreaded, "PyCodeObject", "co_firstlineno"));

            // Fields that don't move stay put across builds.
            Assert.AreEqual(56L, Offset(freeThreaded, "_PyInterpreterFrame", "instr_ptr"));
            Assert.AreEqual(72L, Offset(freeThreaded, "_ts", "current_frame"));
        }

        [TestMethod, Priority(0)]
        public void ReturnsFalse_ForUnmappedFieldsAndStructs() {
            var provider = Provider(PyDebugOffsetsTests.RawV314);
            long offset;

            // Field exists in the struct but is deliberately left on the PDB (not a hot-path field).
            Assert.IsFalse(provider.TryGetFieldOffset("_PyInterpreterFrame", "f_globals", out offset));
            Assert.IsFalse(provider.TryGetFieldOffset("PyCodeObject", "co_names", out offset));

            // Structs with no hot-path mapping at all fall through to the PDB.
            Assert.IsFalse(provider.TryGetFieldOffset("_is", "eval_frame", out offset));
            Assert.IsFalse(provider.TryGetFieldOffset("PyDictObject", "ma_keys", out offset));

            // Unknown names and nulls never match.
            Assert.IsFalse(provider.TryGetFieldOffset("_ts", "not_a_field", out offset));
            Assert.IsFalse(provider.TryGetFieldOffset(null, "next", out offset));
            Assert.IsFalse(provider.TryGetFieldOffset("_ts", null, out offset));
        }
    }
}

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
using Microsoft.PythonTools.Debugger.Concord.Proxies.Structs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DebuggerTests {
    /// <summary>
    /// Locks down the managed port of CPython's location-table (PEP 626) decoder
    /// (<see cref="PyLineTable"/>) against data recorded from real 3.11, 3.12 and 3.13
    /// runtimes. Each case stores the function's <c>co_firstlineno</c>, the raw
    /// <c>co_linetable</c> bytes, and the authoritative <c>(start, end, line)</c> ranges
    /// produced by <c>code.co_lines()</c> (a line of -1 means the range has no source
    /// location, i.e. a NONE marker). The decoder must reproduce those lines for every
    /// bytecode offset.
    /// </summary>
    [TestClass]
    public class PyLineTableTests {
        private sealed class LineTableCase {
            public string Version { get; }
            public string FunctionName { get; }
            public int FirstLineNo { get; }
            public byte[] LineTable { get; }
            // Each entry is { startOffset, endOffset, expectedLine } in bytecode bytes.
            // expectedLine == NoLine (-1) marks a range with no source location.
            public int[][] Ranges { get; }

            public LineTableCase(string version, string functionName, int firstLineNo, string lineTableHex, int[][] ranges) {
                Version = version;
                FunctionName = functionName;
                FirstLineNo = firstLineNo;
                LineTable = FromHex(lineTableHex);
                Ranges = ranges;
            }
        }

        private const int NoLine = -1;

        private static byte[] FromHex(string hex) {
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++) {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }

        // Recorded from CPython 3.11.9, 3.12.7 and 3.13.14. See Addr2Line summary above.
        private static readonly LineTableCase[] Cases = {
            new LineTableCase("3.11.9", "simple", 4, "8000d80809884189058041d80809884189058041d80b0c8048",
                new[] { new[] { 0x0, 0x2, 4 }, new[] { 0x2, 0x4, 5 }, new[] { 0x4, 0x6, 5 }, new[] { 0x6, 0xA, 5 }, new[] { 0xA, 0xC, 5 }, new[] { 0xC, 0xE, 6 }, new[] { 0xE, 0x10, 6 }, new[] { 0x10, 0x14, 6 }, new[] { 0x14, 0x16, 6 }, new[] { 0x16, 0x18, 7 }, new[] { 0x18, 0x1A, 7 } }),
            new LineTableCase("3.11.9", "branchy", 9, "8000d80c0d8045dd0d12903189588c58f000040517f0000405178801d80b0c8871893590418a3a883ad80c119051894a88458845e00c119051894a88458845f002030513d810159801910988058805f8dd0b1cf000010513f000010513f000010513d81012880588058805f003010513f8f8f8e00b10804c",
                new[] { new[] { 0x0, 0x2, 9 }, new[] { 0x2, 0x4, 10 }, new[] { 0x4, 0x6, 10 }, new[] { 0x6, 0x12, 11 }, new[] { 0x12, 0x14, 11 }, new[] { 0x14, 0x18, 11 }, new[] { 0x18, 0x22, 11 }, new[] { 0x22, 0x24, 11 }, new[] { 0x24, 0x26, 11 }, new[] { 0x26, 0x28, 11 }, new[] { 0x28, 0x2A, 12 }, new[] { 0x2A, 0x2C, 12 }, new[] { 0x2C, 0x30, 12 }, new[] { 0x30, 0x32, 12 }, new[] { 0x32, 0x38, 12 }, new[] { 0x38, 0x3A, 12 }, new[] { 0x3A, 0x3C, 13 }, new[] { 0x3C, 0x3E, 13 }, new[] { 0x3E, 0x42, 13 }, new[] { 0x42, 0x44, 13 }, new[] { 0x44, 0x46, 13 }, new[] { 0x46, 0x48, 15 }, new[] { 0x48, 0x4A, 15 }, new[] { 0x4A, 0x4E, 15 }, new[] { 0x4E, 0x50, 15 }, new[] { 0x50, 0x52, 15 }, new[] { 0x52, 0x54, 16 }, new[] { 0x54, 0x56, 17 }, new[] { 0x56, 0x58, 17 }, new[] { 0x58, 0x5C, 17 }, new[] { 0x5C, 0x5E, 17 }, new[] { 0x5E, 0x60, 17 }, new[] { 0x60, 0x62, -1 }, new[] { 0x62, 0x6E, 18 }, new[] { 0x6E, 0x70, 18 }, new[] { 0x70, 0x72, 18 }, new[] { 0x72, 0x74, 18 }, new[] { 0x74, 0x76, 19 }, new[] { 0x76, 0x78, 19 }, new[] { 0x78, 0x7A, 19 }, new[] { 0x7A, 0x7C, 19 }, new[] { 0x7C, 0x7E, 18 }, new[] { 0x7E, 0x80, -1 }, new[] { 0x80, 0x82, -1 }, new[] { 0x82, 0x84, -1 }, new[] { 0x84, 0x86, 20 }, new[] { 0x86, 0x88, 20 } }),
            new LineTableCase("3.11.9", "comprehension", 22, "8000d80e2dd00e2d9865d00e2dd10e2dd40e2d8047d80c27d00c279877d00c27d10c27d40c278045d80b10804c",
                new[] { new[] { 0x0, 0x2, 22 }, new[] { 0x2, 0x4, 23 }, new[] { 0x4, 0x6, 23 }, new[] { 0x6, 0x8, 23 }, new[] { 0x8, 0xA, 23 }, new[] { 0xA, 0xE, 23 }, new[] { 0xE, 0x18, 23 }, new[] { 0x18, 0x1A, 23 }, new[] { 0x1A, 0x1C, 24 }, new[] { 0x1C, 0x1E, 24 }, new[] { 0x1E, 0x20, 24 }, new[] { 0x20, 0x22, 24 }, new[] { 0x22, 0x26, 24 }, new[] { 0x26, 0x30, 24 }, new[] { 0x30, 0x32, 24 }, new[] { 0x32, 0x34, 25 }, new[] { 0x34, 0x36, 25 } }),
            new LineTableCase("3.11.9", "multiline", 27, "8000f006000f10d80e0ff103010f10e00e0ff105020f108046f006000c12804d",
                new[] { new[] { 0x0, 0x2, 27 }, new[] { 0x2, 0x4, 30 }, new[] { 0x4, 0x6, 31 }, new[] { 0x6, 0xA, 30 }, new[] { 0xA, 0xC, 32 }, new[] { 0xC, 0x10, 30 }, new[] { 0x10, 0x12, 30 }, new[] { 0x12, 0x14, 33 }, new[] { 0x14, 0x16, 33 } }),
            new LineTableCase("3.12.7", "simple", 4, "8000d80809884189058041d80809884189058041d80b0c8048",
                new[] { new[] { 0x0, 0x2, 4 }, new[] { 0x2, 0xC, 5 }, new[] { 0xC, 0x16, 6 }, new[] { 0x16, 0x1A, 7 } }),
            new LineTableCase("3.12.7", "branchy", 9, "8000d80c0d8045dc0d1290318e588801d80b0c8871893590418a3ad80c119051894a8945e00c119051894a8945f009000e16f00a030513d81015980191098805f006000c11804cf8f405000c1df200010513d810128905d80b10804cf005010513fa",
                new[] { new[] { 0x0, 0x2, 9 }, new[] { 0x2, 0x6, 10 }, new[] { 0x6, 0x22, 11 }, new[] { 0x22, 0x32, 12 }, new[] { 0x32, 0x3E, 13 }, new[] { 0x3E, 0x4A, 15 }, new[] { 0x4A, 0x4C, 11 }, new[] { 0x4C, 0x4E, 16 }, new[] { 0x4E, 0x58, 17 }, new[] { 0x58, 0x5C, 20 }, new[] { 0x5C, 0x5E, -1 }, new[] { 0x5E, 0x6E, 18 }, new[] { 0x6E, 0x74, 19 }, new[] { 0x74, 0x78, 20 }, new[] { 0x78, 0x7A, 18 }, new[] { 0x7A, 0x80, -1 } }),
            new LineTableCase("3.12.7", "comprehension", 22, "8000d91e23d30e2d99659811a071a831a375887190318b7598658047d00e2dd91f26d30c27997798218851900190419105895898778045d00c27d80b10804cf9f205000f2ef9da0c27",
                new[] { new[] { 0x0, 0x2, 22 }, new[] { 0x2, 0x32, 23 }, new[] { 0x32, 0x58, 24 }, new[] { 0x58, 0x5C, 25 }, new[] { 0x5C, 0x60, -1 }, new[] { 0x60, 0x66, 23 }, new[] { 0x66, 0x6A, -1 }, new[] { 0x6A, 0x70, 24 } }),
            new LineTableCase("3.12.7", "multiline", 27, "8000f006000f10d80e0ff103010f10e00e0ff105020f108046f006000c12804d",
                new[] { new[] { 0x0, 0x2, 27 }, new[] { 0x2, 0x4, 30 }, new[] { 0x4, 0x6, 31 }, new[] { 0x6, 0xA, 30 }, new[] { 0xA, 0xC, 32 }, new[] { 0xC, 0x12, 30 }, new[] { 0x12, 0x16, 33 } }),
            new LineTableCase("3.13.14", "simple", 4, "8000d8080989058041d80809884189058041d80b0c8048",
                new[] { new[] { 0x0, 0x2, 4 }, new[] { 0x2, 0xA, 5 }, new[] { 0xA, 0x14, 6 }, new[] { 0x14, 0x18, 7 } }),
            new LineTableCase("3.13.14", "branchy", 9, "8000d80c0d8045dc0d1290318e588801d80b0c8871893590418b3ad80c11894a8a45e00c11894a8a45f109000e16f00a030513d8101591098805f006000c11804cf8f405000c1df300010513d810128905d80b10804cf005010513fa",
                new[] { new[] { 0x0, 0x2, 9 }, new[] { 0x2, 0x6, 10 }, new[] { 0x6, 0x22, 11 }, new[] { 0x22, 0x34, 12 }, new[] { 0x34, 0x40, 13 }, new[] { 0x40, 0x4C, 15 }, new[] { 0x4C, 0x50, 11 }, new[] { 0x50, 0x52, 16 }, new[] { 0x52, 0x5A, 17 }, new[] { 0x5A, 0x5E, 20 }, new[] { 0x5E, 0x60, -1 }, new[] { 0x60, 0x72, 18 }, new[] { 0x72, 0x78, 19 }, new[] { 0x78, 0x7C, 20 }, new[] { 0x7C, 0x7E, 18 }, new[] { 0x7E, 0x84, -1 } }),
            new LineTableCase("3.13.14", "comprehension", 22, "8000d91e23d30e2d9a659811a831a1758b7588718c7599658047d00e2dd91f26d30c279a7798219001904191058a5899778045d00c27d80b10804cf9f205000f2ef9da0c27",
                new[] { new[] { 0x0, 0x2, 22 }, new[] { 0x2, 0x38, 23 }, new[] { 0x38, 0x62, 24 }, new[] { 0x62, 0x66, 25 }, new[] { 0x66, 0x6A, -1 }, new[] { 0x6A, 0x70, 23 }, new[] { 0x70, 0x74, -1 }, new[] { 0x74, 0x7A, 24 } }),
            new LineTableCase("3.13.14", "multiline", 27, "8000f006000f10d80e0ff103010f10e00e0ff105020f108046f006000c12804d",
                new[] { new[] { 0x0, 0x2, 27 }, new[] { 0x2, 0x4, 30 }, new[] { 0x4, 0x6, 31 }, new[] { 0x6, 0xA, 30 }, new[] { 0xA, 0xC, 32 }, new[] { 0xC, 0x12, 30 }, new[] { 0x12, 0x16, 33 } }),
        };

        [TestMethod, Priority(0)]
        public void Addr2Line_MatchesRecordedRuntimeLineTables() {
            foreach (var c in Cases) {
                foreach (var range in c.Ranges) {
                    int start = range[0], end = range[1], expected = range[2];
                    for (int offset = start; offset < end; offset += 2) {
                        int actual = PyLineTable.Addr2Line(c.LineTable, c.FirstLineNo, offset);
                        Assert.AreEqual(expected, actual,
                            $"{c.Version} {c.FunctionName}: offset 0x{offset:X} expected line {expected} but got {actual}");
                    }
                }
            }
        }

        [TestMethod, Priority(0)]
        public void Addr2Line_NoLineRangesDecodeToNoLine() {
            // The recorded data must actually exercise NONE markers, and each must decode to -1.
            int noLineRanges = 0;
            foreach (var c in Cases) {
                foreach (var range in c.Ranges.Where(r => r[2] == NoLine)) {
                    noLineRanges++;
                    for (int offset = range[0]; offset < range[1]; offset += 2) {
                        Assert.AreEqual(NoLine, PyLineTable.Addr2Line(c.LineTable, c.FirstLineNo, offset),
                            $"{c.Version} {c.FunctionName}: offset 0x{offset:X} should have no source line");
                    }
                }
            }
            Assert.IsTrue(noLineRanges > 0, "Expected the recorded line tables to include NONE markers.");
        }

        [TestMethod, Priority(0)]
        public void Addr2Line_NegativeOffsetReturnsFirstLine() {
            // CPython's PyCode_Addr2Line maps a negative offset to co_firstlineno; this is the
            // entry-frame case (3.11/3.12 prev_instr points just before the first instruction).
            foreach (var c in Cases) {
                Assert.AreEqual(c.FirstLineNo, PyLineTable.Addr2Line(c.LineTable, c.FirstLineNo, -1),
                    $"{c.Version} {c.FunctionName}: negative offset should return the first line");
                Assert.AreEqual(c.FirstLineNo, PyLineTable.Addr2Line(c.LineTable, c.FirstLineNo, -2),
                    $"{c.Version} {c.FunctionName}: negative offset should return the first line");
            }
        }

        [TestMethod, Priority(0)]
        public void Addr2Line_OffsetPastEndReturnsNoLine() {
            foreach (var c in Cases) {
                int lastEnd = c.Ranges[c.Ranges.Length - 1][1];
                Assert.AreEqual(NoLine, PyLineTable.Addr2Line(c.LineTable, c.FirstLineNo, lastEnd + 0x100),
                    $"{c.Version} {c.FunctionName}: offset past the end should have no source line");
            }
        }

        [TestMethod, Priority(0)]
        public void Addr2Line_NullTableReturnsNoLine() {
            Assert.AreEqual(NoLine, PyLineTable.Addr2Line(null, 5, 0));
        }
    }
}

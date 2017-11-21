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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnalysisTests {
    [TestClass]
    public class StringListReaderTests {
        private static void AssertReadLines(IList<string> lines) {
            using (var r = new StringListReader(lines)) {
                int lineno = 0;
                foreach (var expected in lines) {
                    lineno += 1;
                    var actual = r.ReadLine();
                    Assert.AreEqual(expected, actual, $"Line #{lineno}");
                }
                Assert.IsNull(r.ReadLine(), "Did not reach EOF");
            }
        }

        private static void AssertRead(IList<string> lines) {
            using (var r = new StringListReader(lines)) {
                int charno = 0;
                foreach (var expected in lines.SelectMany(s => s.AsEnumerable())) {
                    charno += 1;
                    var actual = r.Read();
                    Assert.AreEqual((int)expected, actual, $"Character #{charno}");
                }
                Assert.IsTrue(r.Read() < 0, "Did not reach EOF");
            }
        }

        [TestMethod]
        public void BasicReadTest() {
            var str = "A;B;C".Split(';');
            AssertReadLines(str);
            AssertRead(str);
        }

        private static string ToString(IEnumerable<char> chars) {
            return chars.Aggregate("", (s, c) => s + c);
        }

        [TestMethod]
        public void PartialReadTest() {
            using (var r = new StringListReader(new[] { "0123456789", "ABCDEFGHIJ" })) {
                var buffer = new char[7];
                Assert.AreEqual(7, r.Read(buffer, 0, 7));
                Assert.AreEqual("0123456", ToString(buffer));
                
                // We do not get a full read here, by design
                Assert.AreEqual(3, r.Read(buffer, 0, 7));
                Assert.AreEqual("789", ToString(buffer.Take(3)));

                Assert.AreEqual(7, r.Read(buffer, 0, 7));
                Assert.AreEqual("ABCDEFG", ToString(buffer));

                // We do not get a full read here, by design
                Assert.AreEqual(3, r.Read(buffer, 0, 7));
                Assert.AreEqual("HIJ", ToString(buffer.Take(3)));
            }
        }

        [TestMethod]
        public void ReadBlockTest() {
            using (var r = new StringListReader(new[] { "0123456789", "ABCDEFGHIJ" })) {
                var buffer = new char[7];
                Assert.AreEqual(7, r.ReadBlock(buffer, 0, 7));
                Assert.AreEqual("0123456", ToString(buffer));
                
                Assert.AreEqual(7, r.ReadBlock(buffer, 0, 7));
                Assert.AreEqual("789ABCD", ToString(buffer));

                Assert.AreEqual(6, r.ReadBlock(buffer, 0, 7));
                Assert.AreEqual("EFGHIJ", ToString(buffer.Take(6)));
            }
        }

        [TestMethod]
        public void PeekTest() {
            var buffer = new char[10];

            using (var r = new StringListReader(new[] { "AB", "CD", "EF" })) {
                Assert.AreEqual('A', r.Peek());
                Assert.AreEqual('A', r.Peek());
                Assert.AreEqual('A', r.Read());
            }

            using (var r = new StringListReader(new[] { "AB", "CD", "EF" })) {
                Assert.AreEqual('A', r.Peek());
                Assert.AreEqual(2, r.Read(buffer, 0, 5));
                Assert.AreEqual('A', buffer[0]);
                Assert.AreEqual('B', buffer[1]);
            }

            using (var r = new StringListReader(new[] { "AB", "CD", "EF" })) {
                Assert.AreEqual('A', r.Read());
                Assert.AreEqual('B', r.Peek());
                // Read after peeking the last char in a line will get us
                // the next line too.
                Assert.AreEqual(3, r.Read(buffer, 0, 5));
                Assert.AreEqual('B', buffer[0]);
                Assert.AreEqual('C', buffer[1]);
                Assert.AreEqual('D', buffer[2]);
            }

            using (var r = new StringListReader(new[] { "AB", "CD", "EF" })) {
                Assert.AreEqual('A', r.Read());
                Assert.AreEqual('B', r.Peek());
                // Read after peeking the last char in a line will get us
                // the next line too.
                Assert.AreEqual("BCD", r.ReadLine());
            }

        }

        [TestMethod]
        public void ReadToEndTest() {
            using (var r = new StringListReader(new[] { "A", "B", "C" })) {
                Assert.AreEqual("ABC", r.ReadToEndWithoutAssert());
            }

            using (var r = new StringListReader(new[] { "A", "B", "C" })) {
                Assert.AreEqual('A', r.Read());
                Assert.AreEqual("BC", r.ReadToEndWithoutAssert());
            }

            using (var r = new StringListReader(new[] { "A", "B", "C" })) {
                Assert.AreEqual('A', r.Peek());
                Assert.AreEqual("ABC", r.ReadToEndWithoutAssert());
            }
        }
    }
}

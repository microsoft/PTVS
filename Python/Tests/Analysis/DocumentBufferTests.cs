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
using System.Text;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using static Microsoft.PythonTools.Intellisense.ProjectEntryExtensions;

namespace AnalysisTests {
    [TestClass]
    public class DocumentBufferTests {
        [TestMethod, Priority(0)]
        public void BasicDocumentBuffer() {
            var doc = new DocumentBuffer();
            doc.Reset(0, @"def f(x):
    return

def g(y):
    return y * 2
");

            doc.Update(new DocumentChangeSet(0, 1, new[] {
                // We *should* batch adjacent insertions, but we should also
                // work fine even if we don't. Note that the insertion point
                // tracks backwards with previous insertions in the same version.
                // If each of these were in its own array, the location would
                // have to change for each.
                DocumentChange.Insert(")", new SourceLocation(2, 11)),
                DocumentChange.Insert("x", new SourceLocation(2, 11)),
                DocumentChange.Insert("(", new SourceLocation(2, 11)),
                DocumentChange.Insert("g", new SourceLocation(2, 11)),
                DocumentChange.Insert(" ", new SourceLocation(2, 11))
            }));

            AssertUtil.Contains(doc.Text.ToString(), "return g(x)");
            Assert.AreEqual(1, doc.Version);

            doc.Update(new[] {
                new DocumentChangeSet(1, 2, new [] {
                    DocumentChange.Delete(new SourceLocation(2, 14), new SourceLocation(2, 15))
                }),
                new DocumentChangeSet(2, 3, new [] {
                    DocumentChange.Insert("x * 2", new SourceLocation(2, 14))
                })
            });

            AssertUtil.Contains(doc.Text.ToString(), "return g(x * 2)");

            doc.Update(new DocumentChangeSet(3, 4, new[] {
                DocumentChange.Replace(new SourceLocation(2, 18), new SourceLocation(2, 19), "300")
            }));

            AssertUtil.Contains(doc.Text.ToString(), "return g(x * 300)");

            doc.Update(new DocumentChangeSet(4, 5, new[] {
                // Changes are out of order, but we should fix that automatically
                DocumentChange.Delete(new SourceLocation(2, 13), new SourceLocation(2, 22)),
                DocumentChange.Insert("#", new SourceLocation(2, 7))
            }));
            AssertUtil.Contains(doc.Text.ToString(), "re#turn g");
        }

        [TestMethod, Priority(0)]
        public void ResetDocumentBuffer() {
            var doc = new DocumentBuffer();

            doc.Reset(0, "");

            Assert.AreEqual("", doc.Text.ToString());

            doc.Update(new[] { new DocumentChangeSet(0, 1, new[] {
                DocumentChange.Insert("text", SourceLocation.MinValue)
            }) });

            Assert.AreEqual("text", doc.Text.ToString());

            try {
                doc.Update(new[] { new DocumentChangeSet(1, 0, new[] {
                DocumentChange.Delete(SourceLocation.MinValue, SourceLocation.MinValue.AddColumns(4))
            }) });
                Assert.Fail("expected InvalidOperationException");
            } catch (InvalidOperationException) {
            }
            Assert.AreEqual("text", doc.Text.ToString());
            Assert.AreEqual(1, doc.Version);

            doc.Update(new[] { new DocumentChangeSet(1, 0, new[] {
                new DocumentChange { WholeBuffer = true }
            }) });

            Assert.AreEqual("", doc.Text.ToString());
            Assert.AreEqual(0, doc.Version);
        }

        [TestMethod, Priority(0)]
        public void EncodeToStream() {
            var sb = new StringBuilder();
            var bytes = new byte[256];
            int read;
            sb.Append('\ufeff', 12);

            for (int chunkSize = 1; chunkSize < 14; ++chunkSize) {
                Console.WriteLine($"Chunk size: {chunkSize}");
                read = ProjectEntry.EncodeToStream(sb, Encoding.UTF8, chunkSize).Read(bytes, 0, bytes.Length);
                Assert.AreEqual(39, read);
                for (int i = 0; i < read; i += 3) {
                    Console.WriteLine($"At {i}: {bytes[i]}, {bytes[i + 1]}, {bytes[i + 2]}");
                    AssertUtil.AreEqual(bytes.Skip(i).Take(3).ToArray(), (byte)239, (byte)187, (byte)191);
                }
            }

            for (int chunkSize = 1; chunkSize < 14; ++chunkSize) {
                Console.WriteLine($"Chunk size: {chunkSize}");
                read = ProjectEntry.EncodeToStream(sb, new UTF8Encoding(false), chunkSize).Read(bytes, 0, bytes.Length);
                Assert.AreEqual(36, read);
                for (int i = 0; i < read; i += 3) {
                    Console.WriteLine($"At {i}: {bytes[i]}, {bytes[i + 1]}, {bytes[i + 2]}");
                    AssertUtil.AreEqual(bytes.Skip(i).Take(3).ToArray(), (byte)239, (byte)187, (byte)191);
                }
            }

            for (int chunkSize = 1; chunkSize < 14; ++chunkSize) {
                Console.WriteLine($"Chunk size: {chunkSize}");
                read = ProjectEntry.EncodeToStream(sb, Encoding.Unicode, chunkSize).Read(bytes, 0, bytes.Length);
                Assert.AreEqual(26, read);
                for (int i = 0; i < read; i += 2) {
                    Console.WriteLine($"At {i}: {bytes[i]}, {bytes[i + 1]}");
                    AssertUtil.AreEqual(bytes.Skip(i).Take(2).ToArray(), (byte)0xFF, (byte)0xFE);
                }
            }

            for (int chunkSize = 1; chunkSize < 14; ++chunkSize) {
                Console.WriteLine($"Chunk size: {chunkSize}");
                read = ProjectEntry.EncodeToStream(sb, new UnicodeEncoding(false, false), chunkSize).Read(bytes, 0, bytes.Length);
                Assert.AreEqual(24, read);
                for (int i = 0; i < read; i += 2) {
                    Console.WriteLine($"At {i}: {bytes[i]}, {bytes[i + 1]}");
                    AssertUtil.AreEqual(bytes.Skip(i).Take(2).ToArray(), (byte)0xFF, (byte)0xFE);
                }
            }
        }
    }
}

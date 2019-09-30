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

extern alias pythontools;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Parsing;
using pythontools::Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using TestUtilities.Mocks;
using Microsoft.PythonTools;
using pythontools::Microsoft.PythonTools.Editor;
using TestUtilities;

namespace PythonToolsTests {
    [TestClass]
    public class LocationTrackerTests {
        List<ITextSnapshot> TestSnapshots {
            get {
                var buffer = new MockTextBuffer("");
                var result = new List<ITextSnapshot> { buffer.CurrentSnapshot };

                // def f(x):
                using (var edit = buffer.CreateEdit()) {
                    edit.Replace(0, 0, "def f(x):\n");
                    edit.Apply();
                }
                result.Add(buffer.CurrentSnapshot);

                // def f(x):
                //     return x
                using (var edit = buffer.CreateEdit()) {
                    edit.Insert(edit.Snapshot.Length, "    return x\n");
                    edit.Apply();
                }
                result.Add(buffer.CurrentSnapshot);

                // def g(y):
                //     return y * 2
                //
                // def f(x):
                //     return x
                using (var edit = buffer.CreateEdit()) {
                    edit.Insert(0, "def g(y):\n    return y * 2\n\n");
                    edit.Apply();
                }
                result.Add(buffer.CurrentSnapshot);

                // def g(y):
                //     return y * 2
                //
                // def f(x):
                //     return
                using (var edit = buffer.CreateEdit()) {
                    edit.Delete(edit.Snapshot.Length - 3, 3);
                    edit.Apply();
                }
                result.Add(buffer.CurrentSnapshot);

                // def g(y):
                //     return y * 2
                //
                // def f(x):
                //     return g(x)
                using (var edit = buffer.CreateEdit()) {
                    edit.Insert(edit.Snapshot.Length, "g(x)\n");
                    edit.Apply();
                }
                result.Add(buffer.CurrentSnapshot);

                return result;
            }
        }

        private void AssertLines(PythonTextBufferInfo buffer, params int[] lengths) {
            AssertLines(
                buffer.LocationTracker.GetLineLocations(buffer.Buffer.CurrentSnapshot.Version.VersionNumber),
                lengths
            );
        }

        private void AssertLines(NewLineLocation[] lines, params int[] lengths) {
            int p = 0;
            for (int i = 0; i < lines.Length || i < lengths.Length; ++i) {
                if (i < lengths.Length) {
                    p += lengths[i];
                }

                Console.WriteLine($"Line {i + 1}: Expected {(i < lengths.Length ? p.ToString() : "(null)")}. " + 
                    $"Actual {(i < lines.Length ? lines[i].EndIndex.ToString() : "(null)")}");
            }
            Console.WriteLine();
            Assert.AreEqual(lengths.Length, lines.Length);

            p = 0;
            for (int i = 0; i < lines.Length; ++i) {
                p += lengths[i];
                Assert.AreEqual(p, lines[i].EndIndex, $"Line {i + 1}");
            }
        }

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
        public void GetLineLocationsTest() {
            var t = new LocationTracker(TestSnapshots[0]);

            var lines = t.GetLineLocations(0);
            AssertLines(lines, 0);
            
            lines = t.GetLineLocations(1);
            AssertLines(lines, 10, 0);

            lines = t.GetLineLocations(2);
            AssertLines(lines, 10, 13, 0);

            lines = t.GetLineLocations(3);
            AssertLines(lines, 10, 17, 1, 10, 13, 0);

            lines = t.GetLineLocations(4);
            AssertLines(lines, 10, 17, 1, 10, 10);

            lines = t.GetLineLocations(5);
            AssertLines(lines, 10, 17, 1, 10, 15, 0);
        }

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
        public void UpdateTrackerSnapshot() {
            var snapshots = TestSnapshots;

            var t = new LocationTracker(snapshots[0]);
            Assert.IsTrue(t.CanTranslateFrom(0));
            Assert.IsTrue(t.CanTranslateFrom(1));
            Assert.IsTrue(t.CanTranslateFrom(5));
            Assert.IsFalse(t.CanTranslateFrom(6));

            Assert.AreEqual(0, t.GetIndex(new SourceLocation(1, 1), 5));
            Assert.IsTrue(t.IsCached(0));
            Assert.IsTrue(t.IsCached(5));

            t.UpdateBaseSnapshot(snapshots[1]);
            Assert.IsFalse(t.CanTranslateFrom(0), "Should not be able to translate from old version 0");
            Assert.IsTrue(t.CanTranslateFrom(1));
            Assert.IsTrue(t.CanTranslateFrom(5));
            Assert.IsFalse(t.CanTranslateFrom(6));
            Assert.IsFalse(t.IsCached(0), "Old snapshot should have been removed from cache");
            Assert.IsTrue(t.IsCached(5), "Current snapshot should not have been removed from cache");

            t.UpdateBaseSnapshot(snapshots[4]);
            Assert.IsFalse(t.CanTranslateFrom(0), "Should not be able to translate from old version 0");
            Assert.IsFalse(t.CanTranslateFrom(1), "Should not be able to translate from old version 1");
            Assert.IsTrue(t.CanTranslateFrom(5));
            Assert.IsFalse(t.CanTranslateFrom(6));
        }

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
        public void BufferSync_Issue3570() {
            // https://github.com/Microsoft/PTVS/issues/3570

            var buffer = new MockTextBuffer("line");
            var bi = PythonTextBufferInfo.ForBuffer(null, buffer);

            bi.AddSentSnapshot(buffer.CurrentSnapshot);

            Assert.AreEqual(new SourceLocation(1, 5), bi.LocationTracker.GetSourceLocation(4, 0));

            using (var e = buffer.CreateEdit()) {
                e.Insert(e.Snapshot.Length, "\r\n");
                e.Apply();
            }

            using (var e = buffer.CreateEdit()) {
                e.Insert(e.Snapshot.Length, "    c");
                e.Apply();
            }

            using (var e = buffer.CreateEdit()) {
                e.Insert(e.Snapshot.Length, "o");
                e.Apply();
            }

            var updates = BufferParser.GetUpdatesForSnapshot(bi, buffer.CurrentSnapshot).ToArray();
            var changeInfo = string.Join(", ", updates
                .Select(u => string.Join(", ", u.changes.Select(c => $"({c.startLine},{c.startColumn},'{c.newText}')")))
                .Select(u => $"[{u}]"));
            Assert.AreEqual("[(1,5,'\r\n')], [(2,1,'    c')], [(2,6,'o')]", changeInfo);
        }

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
        public void BufferSync_Issue3733() {
            // https://github.com/Microsoft/PTVS/issues/3733

            var bigString = string.Join("\n", Enumerable.Repeat("content", 1000));

            var buffer = new MockTextBuffer("");
            var bi = PythonTextBufferInfo.ForBuffer(null, buffer);

            bi.AddSentSnapshot(buffer.CurrentSnapshot);
            bi.LocationTracker.UpdateBaseSnapshot(buffer.CurrentSnapshot);

            AssertLines(bi, 0);

            using (var e = buffer.CreateEdit()) {
                e.Insert(e.Snapshot.Length, bigString);
                e.Apply();
            }
            bi.LocationTracker.UpdateBaseSnapshot(buffer.CurrentSnapshot);

            int beforeVersion = bi.Buffer.CurrentSnapshot.Version.VersionNumber;

            using (var e = buffer.CreateEdit()) {
                e.Replace(0, e.Snapshot.Length, "z");
                e.Apply();
            }
            bi.LocationTracker.UpdateBaseSnapshot(buffer.CurrentSnapshot);

            using (var e = buffer.CreateEdit()) {
                e.Insert(e.Snapshot.Length, bigString);
                e.Apply();
            }
            bi.LocationTracker.UpdateBaseSnapshot(buffer.CurrentSnapshot);

            var before = bi.LocationTracker.GetLineLocations(beforeVersion);
            var after = bi.LocationTracker.GetLineLocations(bi.Buffer.CurrentSnapshot.Version.VersionNumber);
            foreach (var t in before.Zip(after, Tuple.Create)) {
                Assert.AreEqual(t.Item1.EndIndex, t.Item2.EndIndex - 1);
            }
        }

        void CheckTranslate(LocationTracker tracker, int fromLine, int fromCol, int fromVersion, int toLine, int toCol, int toVersion, bool checkReverse = true) {
            var from_ = new SourceLocation(fromLine, fromCol);
            var to_ = new SourceLocation(toLine, toCol);

            var actualTo = tracker.Translate(from_, fromVersion, toVersion);
            Assert.AreEqual(to_, actualTo, $"Translating {from_} from {fromVersion} to {toVersion}");
            if (checkReverse && fromVersion != toVersion) {
                var actualFrom = tracker.Translate(actualTo, toVersion, fromVersion);
                Assert.AreEqual(from_, actualFrom, $"Reverse translating {actualTo} from {toVersion} to {fromVersion}");
            }
        }

        [TestMethod, Priority(TestExtensions.CORE_UNIT_TEST)]
        public void TranslateLocations() {
            var t = new LocationTracker(TestSnapshots[0]);

            // Translate forwards
            CheckTranslate(t, 1, 5, 1, 1, 5, 1);
            CheckTranslate(t, 1, 5, 1, 1, 5, 2);
            CheckTranslate(t, 1, 5, 1, 4, 5, 3);
            CheckTranslate(t, 1, 5, 1, 4, 5, 4);
            CheckTranslate(t, 1, 5, 1, 4, 5, 5);

            // Translate backwards (includes a delete, so doesn't always round-trip)
            CheckTranslate(t, 1, 5, 2, 1, 5, 1);
            CheckTranslate(t, 1, 5, 3, 1, 1, 1, checkReverse: false);
            CheckTranslate(t, 1, 5, 4, 1, 1, 1, checkReverse: false);
            CheckTranslate(t, 1, 5, 5, 1, 1, 1, checkReverse: false);

            // Translate to actual line location in same version
            CheckTranslate(t, 5, 5, 0, 1, 1, 0);
            CheckTranslate(t, 5, 5, 1, 2, 1, 1);
            CheckTranslate(t, 5, 5, 2, 3, 1, 2);
            CheckTranslate(t, 5, 5, 3, 5, 5, 3);
            CheckTranslate(t, 8, 5, 3, 6, 1, 3);
            CheckTranslate(t, 8, 5, 4, 5, 11, 4);
            CheckTranslate(t, 8, 5, 5, 6, 1, 5);
        }
    }
}

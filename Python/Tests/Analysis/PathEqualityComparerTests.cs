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

using System.Linq;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    [TestClass]
    public class PathEqualityComparerTests {
        [TestMethod]
        public void GetPathCompareKey() {
            foreach (var path in new[] {
                "C:/normalized//path/",
                "C:/normalized/.\\path",
                "C:\\NORMalized\\\\path",
                "C:/normalized/path\\",
                "C:/normalized/path/",
                "C:/normalized/here////..////path/"
            }) {
                Assert.AreEqual("C:\\NORMALIZED\\PATH", PathEqualityComparer.GetCompareKeyUncached(path), path);
            }
            foreach (var path in new[] {
                "\\\\computer/normalized//path/",
                "//computer/normalized/.\\path",
                "\\\\computer\\NORMalized\\\\path",
                "\\\\computer/normalized/path\\",
                "\\/computer/normalized/path/",
                "\\\\computer/normalized/here////..////path/"
            }) {
                Assert.AreEqual("\\\\COMPUTER\\NORMALIZED\\PATH", PathEqualityComparer.GetCompareKeyUncached(path), path);
            }

            Assert.AreEqual("C:\\..\\..\\PATH", PathEqualityComparer.GetCompareKeyUncached("C:/.././.././path/"));
            Assert.AreEqual("\\\\COMPUTER\\SHARE\\..\\..\\PATH", PathEqualityComparer.GetCompareKeyUncached("//computer/share/.././.././path/"));
        }

        [TestMethod]
        public void PathEqualityStartsWith() {
            var cmp = new PathEqualityComparer();
            foreach (var path in new[] {
                new { p="C:/root/a/b", isFull=false },
                new { p ="C:\\ROOT\\", isFull=true },
                new { p="C:\\Root", isFull=true },
                new { p="C:\\notroot\\..\\root", isFull=true },
                new { p="C:\\.\\root\\", isFull = true }
            }) {
                Assert.IsTrue(cmp.StartsWith(path.p, "C:\\Root"));
                if (path.isFull) {
                    Assert.IsFalse(cmp.StartsWith(path.p, "C:\\Root", allowFullMatch: false));
                } else {
                    Assert.IsTrue(cmp.StartsWith(path.p, "C:\\Root", allowFullMatch: false));
                }
            }
        }

        [TestMethod]
        public void PathEqualityCache() {
            var cmp = new PathEqualityComparer();

            var path1 = "C:\\normalized\\path\\";
            var path2 = "c:/normalized/here/../path";

            Assert.AreEqual(0, cmp._compareKeyCache.Count);
            cmp.GetHashCode(path1);
            Assert.AreEqual(1, cmp._compareKeyCache[path1].Accessed);

            cmp.GetHashCode(path2);
            Assert.AreEqual(1, cmp._compareKeyCache[path1].Accessed);
            Assert.AreEqual(2, cmp._compareKeyCache[path2].Accessed);

            cmp.GetHashCode(path1);
            Assert.AreEqual(3, cmp._compareKeyCache[path1].Accessed);
            Assert.AreEqual(2, cmp._compareKeyCache[path2].Accessed);

            foreach (var path_i in Enumerable.Range(0, 100).Select(i => $"C:\\path\\{i}\\here")) {
                cmp.GetHashCode(path_i);
                cmp.GetHashCode(path1);
            }

            AssertUtil.CheckCollection(
                cmp._compareKeyCache.Keys,
                new[] { path1 },
                new[] { path2 }
            );
        }
    }
}

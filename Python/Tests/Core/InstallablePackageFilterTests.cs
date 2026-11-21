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
using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.EnvironmentsList;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace PythonToolsTests {
    [TestClass]
    public class InstallablePackageFilterTests {
        private static FuzzyStringMatcher NewMatcher()
            => new FuzzyStringMatcher(FuzzyMatchMode.FuzzyIgnoreCase);

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void SelectTopMatches_BoundsResultCount() {
            // Regression guard: a large available-package index must never
            // materialize more results than the requested maximum, even when
            // every entry matches the query.
            var source = Enumerable.Range(0, 100)
                .Select(i => new PackageSpec("requests" + i.ToString("D3")))
                .ToList();

            var result = InstallablePackageFilter.SelectTopMatches(source, "requests", NewMatcher(), 20);

            Assert.AreEqual(20, result.Count);
            Assert.IsTrue(result.All(s => s.IsValid));
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void SelectTopMatches_ExcludesNonMatches() {
            var source = new List<PackageSpec> {
                new PackageSpec("requests"),
                new PackageSpec("requests-oauthlib"),
                new PackageSpec("flask"),
                new PackageSpec("django"),
            };

            var result = InstallablePackageFilter.SelectTopMatches(source, "requests", NewMatcher(), 20);

            CollectionAssert.AreEquivalent(
                new[] { "requests", "requests-oauthlib" },
                result.Select(s => s.Name).ToArray());
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void SelectTopMatches_OrdersByDescendingRelevance() {
            var matcher = NewMatcher();
            var source = Enumerable.Range(0, 30)
                .Select(i => new PackageSpec("pkg" + i.ToString("D2")))
                .ToList();

            var result = InstallablePackageFilter.SelectTopMatches(source, "pkg", matcher, 10);

            var sortKeys = result.Select(s => matcher.GetSortKey(s.Name, "pkg")).ToList();
            for (int i = 1; i < sortKeys.Count; i++) {
                Assert.IsTrue(sortKeys[i - 1] >= sortKeys[i], "Results must be ordered by descending relevance.");
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void SelectTopMatches_NoMatchesReturnsEmpty() {
            var source = new List<PackageSpec> {
                new PackageSpec("flask"),
                new PackageSpec("django"),
            };

            var result = InstallablePackageFilter.SelectTopMatches(source, "zzzznomatch", NewMatcher(), 20);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void MergeRowKeyMatchesFilterKey() {
            // The available-packages list reuses a displayed row across refreshes only
            // when the key derived from its materialized PackageResultView equals, under
            // the ordinal-ignore-case comparer the Merge uses, the key derived from the
            // raw PackageSpec. If these ever diverge, every refresh disposes and recreates
            // all displayed rows, silently re-introducing the per-refresh view-model churn
            // this fix removes (no crash, no other failing test). This pins that invariant.
            var specs = new[] {
                new PackageSpec("requests"),
                new PackageSpec("requests", "2.31.0"),
                new PackageSpec(""),
            };

            foreach (var spec in specs) {
                var row = new PackageResultView(null, new PipPackageView(null, spec, false));
                try {
                    var leftKey = row.Package.PackageSpec;
                    var rightKey = PipPackageView.GetPackageSpecString(spec);
                    Assert.IsTrue(
                        StringComparer.OrdinalIgnoreCase.Equals(leftKey, rightKey),
                        "Merge row key must match the filter key for spec '" + spec.Name +
                        "': left='" + leftKey + "' right='" + rightKey + "'.");
                } finally {
                    row.Dispose();
                }
            }
        }
    }
}

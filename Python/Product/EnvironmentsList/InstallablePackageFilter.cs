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
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.EnvironmentsList {
    internal static class InstallablePackageFilter {
        /// <summary>
        /// Selects up to <paramref name="max"/> package specs from
        /// <paramref name="source"/> whose spec string matches
        /// <paramref name="query"/>, ordered by descending relevance.
        /// </summary>
        /// <remarks>
        /// This selection is intentionally free of any view-model construction so
        /// that a large available-package index (the full PyPI listing is hundreds
        /// of thousands of entries) never materializes a view-model per entry.
        /// Callers wrap only the bounded set that is returned.
        /// </remarks>
        public static IList<PackageSpec> SelectTopMatches(
            IEnumerable<PackageSpec> source,
            string query,
            FuzzyStringMatcher matcher,
            int max
        ) {
            return source
                .Select(spec => Tuple.Create(
                    matcher.GetSortKey(PipPackageView.GetPackageSpecString(spec), query),
                    spec))
                .Where(candidate => matcher.IsCandidateMatch(
                    PipPackageView.GetPackageSpecString(candidate.Item2),
                    query,
                    candidate.Item1))
                .OrderByDescending(candidate => candidate.Item1)
                .Select(candidate => candidate.Item2)
                .Take(max)
                .ToArray();
        }
    }
}

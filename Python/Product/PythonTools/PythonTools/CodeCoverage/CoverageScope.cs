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

namespace Microsoft.PythonTools.CodeCoverage {
    /// <summary>
    /// Tracks data about coverage for each scope.
    /// </summary>

    sealed class CoverageScope {
        public readonly ScopeStatement Statement;
        public readonly List<CoverageScope> Children = new List<CoverageScope>();
        public readonly SortedDictionary<int, CoverageLineInfo> Lines = new SortedDictionary<int, CoverageLineInfo>();
        public int BlocksCovered, BlocksNotCovered;

        public CoverageScope(ScopeStatement node) {
            Statement = node;
        }

        public int LinesCovered {
            get {
                return Lines.Select(x => x.Value).Where(x => x.Covered).Count();
            }
        }

        public int LinesNotCovered {
            get {
                return Lines.Select(x => x.Value).Where(x => !x.Covered).Count();
            }
        }
    }
}

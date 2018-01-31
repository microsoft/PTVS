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

using System;
using System.Linq;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    [TestClass]
    public class LongestCommonSequenceTests {
        [TestMethod, Priority(0)]
        public void FindDiffs_AGTACGCA_TATGC() {
            FindCharDiffs("AGTACGCA", "TATGC", new LcsDiff(0, 1, 0, -1), new LcsDiff(4, 4, 2, 2), new LcsDiff(7, 7, 5, 4));
        }

        [TestMethod, Priority(0)]
        public void FindDiffs_TATGC_AGTACGCA() {
            FindCharDiffs("TATGC", "AGTACGCA", new LcsDiff(0, -1, 0, 1), new LcsDiff(2, 2, 4, 4), new LcsDiff(5, 4, 7, 7));
        }

        [TestMethod, Priority(0)]
        public void FindDiffs_ASAGTACGCARB_ASTATGCRB() {
            FindCharDiffs("ASAGTACGCARB", "ASTATGCRB", new LcsDiff(2, 3, 2, 1), new LcsDiff(6, 6, 4, 4), new LcsDiff(9, 9, 7, 6));
        }

        [TestMethod, Priority(0)]
        public void FindDiffs_ASTATGCRB_ASAGTACGCARB() {
            FindCharDiffs("ASTATGCRB", "ASAGTACGCARB", new LcsDiff(2, 1, 2, 3), new LcsDiff(4, 4, 6, 6), new LcsDiff(7, 6, 9, 9));
        }

        [TestMethod, Priority(0)]
        public void FindDiffs_ASAGTACGCARB_ASRB() {
            FindCharDiffs("ASAGTACGCARB", "ASRB", new LcsDiff(2, 9, 2, 1));
        }

        [TestMethod, Priority(0)]
        public void FindDiffs_ASRB_ASAGTACGCARB() {
            FindCharDiffs("ASRB", "ASAGTACGCARB", new LcsDiff(2, 1, 2, 9));
        }

        [TestMethod, Priority(0)]
        public void FindDiffs_AGTACGCA_AGTA() {
            FindCharDiffs("AGTACGCA", "AGTA", new LcsDiff(4, 7, 4, 3));
        }

        [TestMethod, Priority(0)]
        public void FindDiffs_AGTA_AGTACGCA() {
            FindCharDiffs("AGTA", "AGTACGCA", new LcsDiff(4, 3, 4, 7));
        }

        [TestMethod, Priority(0)]
        public void FindDiffs_AGTACGCA_CGCA() {
            FindCharDiffs("AGTACGCA", "CGCA", new LcsDiff(0, 3, 0, -1));
        }

        [TestMethod, Priority(0)]
        public void FindDiffs_CGCA_AGTACGCA() {
            FindCharDiffs("CGCA", "AGTACGCA", new LcsDiff(0, -1, 0, 3));
        }

        private void FindCharDiffs(string oldText, string newText, params LcsDiff[] expected) {
            var diffs = LongestCommonSequence<char>.Find(oldText.ToCharArray(), newText.ToCharArray(), (c1, c2) => c1 == c2);
            AssertUtil.ArrayEquals(expected, diffs.ToArray());
        }
    }
}
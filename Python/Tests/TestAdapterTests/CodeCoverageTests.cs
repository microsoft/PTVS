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

extern alias pt;
using Microsoft.PythonTools.TestAdapter.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace TestAdapterTests {
    [TestClass]
    public class CodeCoverageTests {
        [TestMethod, Priority(UnitTestPriority.P0)]
        public void TestBestFile() {
            var file1 = "C:\\Some\\Path\\file1.py";
            var file2 = "C:\\Some\\Path\\file2.py";
            var best = CodeCoverage.UpdateBestFile(null, file1);
            Assert.AreEqual(best, file1);

            best = CodeCoverage.UpdateBestFile(null, file1);
            Assert.AreEqual(best, file1);

            best = CodeCoverage.UpdateBestFile(best, file2);
            Assert.AreEqual("C:\\Some\\Path", best);
        }
    }
}

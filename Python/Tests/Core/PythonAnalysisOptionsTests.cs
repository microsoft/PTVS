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
// See the Apache Version 2.0 License for specific language governing permissions
// and limitations under the License.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;

namespace PythonToolsTests {
    [TestClass]
    public class PythonAnalysisOptionsTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void IndexingDefaultsToTrue() {
            var options = PythonToolsTestUtilities.CreatePythonService().AnalysisOptions;

            Assert.IsTrue(options.Indexing);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void ResetEnablesIndexing() {
            var options = PythonToolsTestUtilities.CreatePythonService().AnalysisOptions;
            options.Indexing = false;

            options.Reset();

            Assert.IsTrue(options.Indexing);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void PersistedIndexingOptOutIsPreserved() {
            var options = PythonToolsTestUtilities.CreatePythonService().AnalysisOptions;
            options.Indexing = false;
            options.Save();
            options.Indexing = true;

            options.Load();

            Assert.IsFalse(options.Indexing);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void ResetPersistsIndexingEnabled() {
            var options = PythonToolsTestUtilities.CreatePythonService().AnalysisOptions;
            options.Indexing = false;
            options.Save();

            options.Reset();
            options.Save();

            options.Indexing = false;
            options.Load();

            Assert.IsTrue(options.Indexing);
        }
    }
}

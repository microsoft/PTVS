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
using Microsoft.PythonTools.TestAdapter.Config;
using Microsoft.PythonTools.TestAdapter.Pytest;
using Microsoft.PythonTools.TestAdapter.UnitTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestAdapterTests {
    [TestClass]
    public class TestDiscovererParseTests {

        static PythonProjectSettings CreateMockSettings(string projectDir) {
            return new PythonProjectSettings(
                projectName: string.Empty,
                projectHome: projectDir,
                workingDir: string.Empty,
                interpreter: string.Empty,
                pathEnv: string.Empty,
                nativeDebugging: false,
                isWorkspace: false,
                useLegacyDebugger: false,
                testFramework: string.Empty,
                unitTestPattern: string.Empty,
                unitTestRootDir: string.Empty,
                discoveryWaitTimeInSeconds: string.Empty
            );
        }


        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void PytestShouldThrowNullResults() {
            var settings = CreateMockSettings(projectDir: "dummypath");
            var discoverer = new TestDiscovererPytest(settings);
            
            var testcases = discoverer.ParseDiscoveryResults(null);
            testcases.Any();
        }

        [TestMethod]
        public void PytestShouldHandleEmptyListResults() {
            var settings = CreateMockSettings(projectDir:"dummypath");
            var discoverer = new TestDiscovererPytest(settings);

            var results = new List<PytestDiscoveryResults>();
            var testcases = discoverer.ParseDiscoveryResults(results);

            Assert.IsFalse(testcases.Any());
        }

        [TestMethod]
        public void PytestShouldHandleEmptyResults() {
            var settings = CreateMockSettings(projectDir: "dummypath");
            var discoverer = new TestDiscovererPytest(settings);

            var results = new List<PytestDiscoveryResults>() { new PytestDiscoveryResults() };
            var testcases = discoverer.ParseDiscoveryResults(results);

            Assert.IsFalse(testcases.Any());
        }


        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void UnnitShouldThrowNullResults() {
            var settings = CreateMockSettings(projectDir: "dummypath");
            var discoverer = new TestDiscovererUnitTest(settings);

            var testcases = discoverer.ParseDiscoveryResults(null);
            testcases.Any();
        }

        [TestMethod]
        public void UnittestShouldHandleEmptyListResults() {
            var settings = CreateMockSettings(projectDir: "dummypath");
            var discoverer = new TestDiscovererUnitTest(settings);

            var results = new List<UnitTestDiscoveryResults>();
            var testcases = discoverer.ParseDiscoveryResults(results);

            Assert.IsFalse(testcases.Any());
        }

        [TestMethod]
        public void UnittestShouldHandleEmptyResults() {
            var settings = CreateMockSettings(projectDir: "dummypath");
            var discoverer = new TestDiscovererUnitTest(settings);

            var results = new List<UnitTestDiscoveryResults>() { new UnitTestDiscoveryResults() };
            var testcases = discoverer.ParseDiscoveryResults(results);

            Assert.IsFalse(testcases.Any());
        }
    }
}

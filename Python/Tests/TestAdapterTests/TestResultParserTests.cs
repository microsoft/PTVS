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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.PythonTools.TestAdapter.Pytest;

namespace TestAdapterTests {

    [TestClass]
    public class TestResultParserTests {

        [TestMethod]
        public void CreatePytestId_FuncInsideClass() {
            Assert.AreEqual(
                ".\\test2.py::Test_test2::test_A", 
                TestResultParser.CreatePytestId("test2.py", "test2.Test_test2", "test_A")
            );
        }

        [TestMethod]
        public void CreatePytestId_GlobalFunc() {
            Assert.AreEqual(
                ".\\test_sample.py::test_answer", 
                TestResultParser.CreatePytestId("test_sample.py", "test_sample", "test_answer")
            );
        }

        [TestMethod]
        public void CreatePytestId_ClassFuncWithRelativeFilename() {
            Assert.AreEqual(
                ".\\package1\\packageA\\test1.py::Test_test1::test_A", 
                TestResultParser.CreatePytestId("package1\\packageA\\test1.py", "package1.packageA.test1.Test_test1", "test_A")
            );
        }

        [TestMethod]
        public void CreatePytestIdMatchesDiscoveryPytestId() {
            var projectRoot = "c:\\home\\";
            var filename = "Package1\\packageA\\Test1.py";
            var pytestId = ".\\package1\\packageA\\test1.py::Test_test1::test_A";

            //Note: ignoring case since vsTestResult lookup ignores
            Assert.AreEqual(
                string.Compare(
                    PyTestExtensions.CreateProperCasedPytestId(projectRoot+filename, projectRoot, pytestId),
                    TestResultParser.CreatePytestId(filename.ToLower(), "package1.packageA.test1.Test_test1", "test_A"),
                    ignoreCase:true), 
                0
            );
        }
    }
}

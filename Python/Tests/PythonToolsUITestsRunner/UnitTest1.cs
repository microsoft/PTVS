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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestRunnerInterop;

namespace PythonToolsUITestsRunner {
    [TestClass]
    public class BasicProjectTests {
        private static readonly VsTestContext _vs = new VsTestContext("Microsoft.PythonTools.Tests.PythonToolsUITests");

        [TestInitialize]
        public void TestInitialize() {
            _vs.TestInitialize();
        }

        [TestCleanup]
        public void TestCleanup() {
            _vs.TestCleanup();
        }

        [ClassCleanup]
        public static void ClassCleanup() {
            _vs.Dispose();
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void TemplateDirectories() {
            _vs.RunTest("PythonToolsUITests.BasicProjectTests:TemplateDirectories");
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void UserProjectFile() {
            _vs.RunTest("PythonToolsUITests.BasicProjectTests:UserProjectFile");
        }
    }
}

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
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace VSInterpretersTests {
    [TestClass]
    public class VSInterpretersTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void InvalidInterpreterVersion() {
            try {
                var lv = new Version(1, 0).ToLanguageVersion();
                Assert.Fail("Expected InvalidOperationException");
            } catch (InvalidOperationException) {
            }

            try {
                InterpreterFactoryCreator.CreateInterpreterFactory(new VisualStudioInterpreterConfiguration(
                    Guid.NewGuid().ToString(),
                    "Test Interpreter",
                    version: new Version(1, 0)
                ));
                Assert.Fail("Expected ArgumentException");
            } catch (ArgumentException ex) {
                // Expect version number in message
                AssertUtil.Contains(ex.Message, "1.0");
            }
        }
    }
}

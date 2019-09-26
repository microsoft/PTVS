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
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.PythonTools.TestAdapter.UnitTest {
    internal static class UnittestExtensions {

        public static TestCase ToVsTestCase(this UnittestTestCase test, string projectHome) {
            var relativeModulePath = PathUtils.CreateFriendlyFilePath(projectHome, test.Source);
            var fullyQualifiedName = MakeFullyQualifiedTestName(relativeModulePath, test.Id);
            var testCase = new TestCase(fullyQualifiedName, PythonConstants.UnitTestExecutorUri, test.Source) {
                DisplayName = test.Name,
                LineNumber = test.Line,
                CodeFilePath = test.Source
            };

            return testCase;
        }

        // Note any changes here need to agree with TestReader.ParseFullyQualifiedTestName
        private static string MakeFullyQualifiedTestName(string relativeModulePath, string unitTestId) {
            // ie. test3.Test_test3.test_A
            int numTrailingElementsAfterModule = 2;
            var idParts = unitTestId.Split('.');

            Debug.Assert(idParts.Length > numTrailingElementsAfterModule);

            //Take last 2 elements
            //unittest id could be folder.modulefilename.class.func
            if (idParts.Length > numTrailingElementsAfterModule) {
                var classAndFuncNames = idParts.Skip(Math.Max(0, idParts.Count() - numTrailingElementsAfterModule)).ToArray();

                // drop module name and append to filepath
                var parts = new string[] { relativeModulePath, classAndFuncNames[0], classAndFuncNames[1] };
                return String.Join("::", parts);
            } else {
                return relativeModulePath + "::" + String.Join("::", idParts);
            }
        }
    }
}

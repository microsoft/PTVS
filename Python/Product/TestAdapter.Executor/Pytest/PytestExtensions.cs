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
using System.IO;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.PythonTools.TestAdapter.Pytest {
    static class PyTestDiscoveryReader {

        public static TestCase ToVsTestCase(this PytestTest test, string projectHome, Dictionary<string, PytestParent> parentMap) {
            if (String.IsNullOrEmpty(projectHome)) {
                throw new ArgumentException(nameof(projectHome));
            }
            if (parentMap == null) {
                throw new ArgumentException(nameof(parentMap));
            }

            var sourceAndLineNum = test.Source.Replace(".\\", "");
            String[] sourceParts = sourceAndLineNum.Split(':');
            int line = 0;

            if (sourceParts.Length != 2 ||
                !Int32.TryParse(sourceParts[1], out line) ||
                String.IsNullOrWhiteSpace(test.Name) ||
                String.IsNullOrWhiteSpace(test.Id)) {
                throw new FormatException("Invalid pytest test discovered: " + test.ToString());
            }

            //bschnurr todo: fix codepath for files outside of project
            var fullSourcePathNormalized = Path.Combine(projectHome, sourceParts[0]).ToLowerInvariant();
            var fullyQualifiedName = CreateFullyQualifiedTestNameFromId(test.Id);

            var tc = new TestCase(fullyQualifiedName, PythonConstants.PytestExecutorUri, fullSourcePathNormalized) {
                DisplayName = test.Name,
                LineNumber = line,
                CodeFilePath = fullSourcePathNormalized
            };

            tc.SetPropertyValue(Constants.PytestIdProperty, test.Id);
            tc.SetPropertyValue(Constants.PyTestXmlClassNameProperty, CreateXmlClassName(test, parentMap));
            tc.SetPropertyValue(Constants.PytestTestExecutionPathPropertery, GetAbsoluteTestExecutionPath(fullSourcePathNormalized, test.Id));
            return tc;
        }


        /// <summary>
        /// Creates a classname that matches the junit testresult generated one so that we can match testresults with testcases
        /// Note if a function doesn't have a class, its classname appears to be the filename without an extension
        /// </summary>
        /// <param name="t"></param>
        /// <param name="parentMap"></param>
        /// <returns></returns>
        public static string CreateXmlClassName(PytestTest t, Dictionary<string, PytestParent> parentMap) {
            var parentList = new List<string>();
            var currId = t.Parentid;
            while (parentMap.TryGetValue(currId, out PytestParent parent)) {
                // class names for functions dont append the direct parent 
                if (String.Compare(parent.Kind, "function", StringComparison.OrdinalIgnoreCase) != 0) {
                    parentList.Add(Path.GetFileNameWithoutExtension(parent.Name));
                }
                currId = parent.Parentid;
            }
            parentList.Reverse();

            var xmlClassName = String.Join(".", parentList);
            return xmlClassName;
        }

        public static string CreateFullyQualifiedTestNameFromId(string pytestId) {
            var fullyQualifiedName = pytestId.Replace(".\\", "");
            String[] parts = fullyQualifiedName.Split(new string[] { "::" }, StringSplitOptions.None);

            // set classname as filename, without extension for test functions outside of classes,
            // so test explorer doesn't use .py as the classname
            if (parts.Length == 2) {
                var className = Path.GetFileNameWithoutExtension(parts[0]);
                return $"{parts[0]}::{className}::{parts[1]}";
            }
            return fullyQualifiedName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="absoluteFilePath"></param>
        /// <param name="pytestId"></param>
        /// <returns></returns>
        public static string GetAbsoluteTestExecutionPath(string absoluteFilePath, string pytestId) {
            var filename = Path.GetFileName(absoluteFilePath);
            var executionTestPath = "";
            var index = pytestId.LastIndexOf(filename);
            if (index != -1) {
                //join full codefilepath and pytestId but remove overlapping directories or filename
                var functionName = pytestId.Substring(index + filename.Length);
                executionTestPath = absoluteFilePath + functionName;
            } else {
                executionTestPath = Path.Combine(Path.GetDirectoryName(absoluteFilePath), pytestId.TrimStart('.'));
            }
            return executionTestPath;
        }
    }
}

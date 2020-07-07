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
using System.Linq;
using Microsoft.PythonTools.TestAdapter.Config;
using Microsoft.PythonTools.TestAdapter.Utils;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace Microsoft.PythonTools.TestAdapter.Pytest {
    internal class PytestConfiguration : ITestConfiguration {
        private readonly IRunContext _runContext;

        public PytestConfiguration(IRunContext runContext) {
            _runContext = runContext ?? throw new ArgumentNullException(nameof(runContext));
            ResultsXmlPath = GetJunitXmlFilePath();
        }

        public string Command => "pytest";

        public string ResultsXmlPath { get; }

        public IList<string> GetExecutionArguments(IEnumerable<TestCase> tests, PythonProjectSettings settings) {
            
            if (tests is null) {
                throw new ArgumentNullException(nameof(tests));
            }

            if (settings is null) {
                throw new ArgumentNullException(nameof(settings));
            }

            var args = new List<string>();

            // For a small set of tests, we'll pass them on the command
            // line. Once we exceed a certain (arbitrary) number, create
            // a test list on disk so that we do not overflow the 
            // 32K argument limit.
            var testIds = tests.Select(t => t.GetPropertyValue<string>(Pytest.Constants.PytestIdProperty, default));
            if (testIds.Count() > 5) {
                var testListFilePath = TestUtils.CreateTestListFile(testIds);
                args.Add(testListFilePath);
            } else {
                args.Add("dummyfilename"); //expected not to exist, but script excepts something
                foreach (var testId in testIds) {
                    args.Add(testId);
                }
            }

            // output results to xml file
            args.Add(String.Format("--junitxml={0}", ResultsXmlPath));
            args.Add(String.Format("--rootdir={0}", settings.ProjectHome));
            args.Add("-o");
            args.Add("junit_logging=all");
            args.Add("-o");
            args.Add("junit_family=xunit1");

            return args;
        }

        private string GetJunitXmlFilePath() {
            string baseName = "junitresults_";
            string path = Path.Combine(_runContext.TestRunDirectory, baseName + Guid.NewGuid().ToString() + ".xml");
            return path;
        }
    }
}

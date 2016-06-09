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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.XPath;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.TestAdapter {
    [FileExtension(".py")]
    [DefaultExecutorUri(TestExecutor.ExecutorUriString)]
    class TestDiscoverer : ITestDiscoverer {
        public void DiscoverTests(IEnumerable<string> sources, IDiscoveryContext discoveryContext, IMessageLogger logger, ITestCaseDiscoverySink discoverySink) {
            ValidateArg.NotNull(sources, "sources");
            ValidateArg.NotNull(discoverySink, "discoverySink");

            var settings = discoveryContext.RunSettings;
            
            DiscoverTests(sources, logger, discoverySink, settings);
        }

        public static void DiscoverTests(IEnumerable<string> sources, IMessageLogger logger, ITestCaseDiscoverySink discoverySink, IRunSettings settings) {
            HashSet<string> sourcesSet = new HashSet<string>(sources);

            // Test list is sent to us via our run settings which we use to smuggle the
            // data we have in our analysis process.
            var doc = new XPathDocument(new StringReader(settings.SettingsXml));
            XPathNodeIterator nodes = doc.CreateNavigator().Select("/RunSettings/Python/TestCases/Project/Test");
            foreach (XPathNavigator test in nodes) {
                var className = test.GetAttribute("className", "");
                var file = test.GetAttribute("file", "");

                if (!sources.Contains(file)) {
                    continue;
                }

                var line = test.GetAttribute("line", "");
                var column = test.GetAttribute("column", "");
                var methodName = test.GetAttribute("method", "");
                string project = null, projectHome = null;
                if (!test.MoveToParent()) {
                    continue;
                }

                project = test.GetAttribute("path", "");
                projectHome = test.GetAttribute("home", "");

                int lineNo, columnNo;
                if (Int32.TryParse(line, out lineNo) &&
                    Int32.TryParse(column, out columnNo) &&
                    !String.IsNullOrWhiteSpace(className) &&
                    !String.IsNullOrWhiteSpace(methodName) &&
                    !String.IsNullOrWhiteSpace(file)) {
                    var moduleName = CommonUtils.CreateFriendlyFilePath(projectHome, file);
                    var fullyQualifiedName = MakeFullyQualifiedTestName(moduleName, className, methodName);

                    // If this is a runTest test we should provide a useful display name
                    var displayName = methodName == "runTest" ? className : methodName;

                    var tc = new TestCase(fullyQualifiedName, new Uri(TestExecutor.ExecutorUriString), file) {
                        DisplayName = displayName,
                        LineNumber = lineNo,
                        CodeFilePath = CommonUtils.GetAbsoluteFilePath(projectHome, file)
                    };

                    discoverySink.SendTestCase(tc);
                } else if (logger != null) {
                    logger.SendMessage(
                        TestMessageLevel.Warning,
                        String.Format(
                            "Bad test case: {0} {1} {2} {3} {4}",
                            className,
                            methodName,
                            file,
                            line,
                            column
                        )
                    );
                }
            }
        }

        internal static string MakeFullyQualifiedTestName(string modulePath, string className, string methodName) {
            return modulePath + "::" + className + "::" + methodName;
        }

        internal static void ParseFullyQualifiedTestName(
            string fullyQualifiedName,
            out string modulePath,
            out string className,
            out string methodName
        ) {
            string[] parts = fullyQualifiedName.Split(new string[] { "::" }, StringSplitOptions.None);
            Debug.Assert(parts.Length == 3);
            modulePath = parts[0];
            className = parts[1];
            methodName = parts[2];
        }
    }
}

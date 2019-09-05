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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.XPath;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.TestAdapter.Config;
using Microsoft.PythonTools.TestAdapter.Pytest;
using Microsoft.PythonTools.TestAdapter.Services;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Microsoft.PythonTools.TestAdapter {

    [ExtensionUri(PythonConstants.PytestExecutorUriString)]

    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
        Justification = "object owned by VS")]
    public class TestExecutorPytest : ITestExecutor {
        private static readonly Guid PythonRemoteDebugPortSupplierUnsecuredId = new Guid("{FEB76325-D127-4E02-B59D-B16D93D46CF5}");
        private static readonly Guid PythonDebugEngineGuid = new Guid("EC1375B7-E2CE-43E8-BF75-DC638DE1F1F9");
        private static readonly Guid NativeDebugEngineGuid = new Guid("3B476D35-A401-11D2-AAD4-00C04F990171");
        private static readonly string TestLauncherPath = PythonToolsInstallPath.GetFile("visualstudio_py_testlauncher.py");
        private readonly ManualResetEvent _cancelRequested = new ManualResetEvent(false);
        private readonly VisualStudioProxy _app;

        public TestExecutorPytest() {
            _app = VisualStudioProxy.FromEnvironmentVariable(PythonConstants.PythonToolsProcessIdEnvironmentVariable);
        }

        public void Cancel() {
            _cancelRequested.Set();
        }

        public void RunTests(IEnumerable<string> sources, IRunContext runContext, IFrameworkHandle frameworkHandle) {
            if (sources == null) {
                throw new ArgumentNullException(nameof(sources));
            }
            if (runContext == null) {
                throw new ArgumentNullException(nameof(runContext));
            }
            if (frameworkHandle == null) {
                throw new ArgumentNullException(nameof(frameworkHandle));
            }

            _cancelRequested.Reset();

            var sourceToProjSettings = RunSettingsUtil.GetSourceToProjSettings(runContext.RunSettings, filterType:TestFrameworkType.Pytest);
            var testCollection = new TestCollection();
            foreach (var testGroup in sources.GroupBy(x => sourceToProjSettings[x])) {
                var settings = testGroup.Key;

                try {
                    var discovery = DiscovererFactory.GetDiscoverer(settings);
                    discovery.DiscoverTests(testGroup, frameworkHandle, testCollection);
                } catch (Exception ex) {
                    frameworkHandle.SendMessage(TestMessageLevel.Error, ex.Message);
                }

                if (_cancelRequested.WaitOne(0)) {
                    return;
                }
            }
            RunPytest(testCollection.Tests, runContext, frameworkHandle);
        }

        public void RunTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle) {
            if (tests == null) {
                throw new ArgumentNullException(nameof(tests));
            }
            if (runContext == null) {
                throw new ArgumentNullException(nameof(runContext));
            }
            if (frameworkHandle == null) {
                throw new ArgumentNullException(nameof(frameworkHandle));
            }

            RunPytest(tests, runContext, frameworkHandle);

            _cancelRequested.Reset();
        }

        private void RunPytest(
            IEnumerable<TestCase> tests,
            IRunContext runContext,
            IFrameworkHandle frameworkHandle
        ) {
            var sourceToProjSettings = RunSettingsUtil.GetSourceToProjSettings(runContext.RunSettings, filterType:TestFrameworkType.Pytest);

            foreach (var testGroup in tests.GroupBy(t => sourceToProjSettings.TryGetValue(t.CodeFilePath ?? String.Empty, out PythonProjectSettings proj) ? proj : null)) {
                if (testGroup.Key == null) {
                    Debug.WriteLine("Missing projectSettings for TestCases:");
                    Debug.WriteLine(String.Join(",\n", testGroup));
                }

                if (_cancelRequested.WaitOne(0)) {
                    break;
                }

                RunTestGroup(testGroup, runContext, frameworkHandle);
            }
        }

        private void RunTestGroup(
            IGrouping<PythonProjectSettings, TestCase> testGroup,
            IRunContext runContext,
            IFrameworkHandle frameworkHandle
        ) {
            PythonProjectSettings settings = testGroup.Key;
            if (settings == null || settings.TestFramework != TestFrameworkType.Pytest) {
                return;
            }

            using (var executor = new ExecutorService(settings, frameworkHandle, runContext)) {
                var idToResultsMap = CreatePytestIdToVsTestResultsMap(testGroup);

                bool codeCoverage = ExecutorService.EnableCodeCoverage(runContext);
                string covPath = null;
                if (codeCoverage) {
                    covPath = ExecutorService.GetCoveragePath(testGroup);
                }

                var resultsXML = executor.Run(testGroup, covPath);

                //Read pytest results from xml
                if (File.Exists(resultsXML)) {
                    var xmlTestResultNodes = TestResultParser.Read(resultsXML).CreateNavigator().SelectDescendants("testcase", "", false);
                    foreach (XPathNavigator pytestResultNode in xmlTestResultNodes) {
                        if (_cancelRequested.WaitOne(0)) {
                            break;
                        }
                        try {
                            var pytestId = TestResultParser.GetPytestId(pytestResultNode);
                            if (pytestId != null && idToResultsMap.TryGetValue(pytestId, out TestResult vsTestResult)) {
                                TestResultParser.UpdateVsTestResult(vsTestResult, pytestResultNode);
                            } else {
                                frameworkHandle.SendMessage(TestMessageLevel.Error, Strings.ErrorTestCaseNotFound.FormatUI(pytestResultNode.OuterXml));
                            }
                        } catch (Exception ex) {
                            frameworkHandle.SendMessage(TestMessageLevel.Error, ex.Message);
                        }
                    }
                } else {
                    frameworkHandle.SendMessage(TestMessageLevel.Error, Strings.PytestResultsXmlNotFound.FormatUI(resultsXML));
                }

                foreach (var result in idToResultsMap.Values) {
                    if (_cancelRequested.WaitOne(0)) {
                        break;
                    }
                    frameworkHandle.RecordResult(result);
                }

                if (codeCoverage) {
                    ExecutorService.AttachCoverageResults(frameworkHandle, covPath);
                }
            }
        }

        private static Dictionary<string, TestResult> CreatePytestIdToVsTestResultsMap(IEnumerable<TestCase> vsTestCases) {
            return vsTestCases.Select(tc => new TestResult(tc) { Outcome = TestOutcome.NotFound })
                .ToDictionary(tr => tr.TestCase.GetPropertyValue<string>(Pytest.Constants.PytestIdProperty, String.Empty), tr => tr);
        }
    }
}
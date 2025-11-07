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
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.TestAdapter.Config;
using Microsoft.PythonTools.TestAdapter.Pytest;
using Microsoft.PythonTools.TestAdapter.Services;
using Microsoft.PythonTools.TestAdapter.Utils;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Microsoft.PythonTools.TestAdapter {

    
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
        Justification = "object owned by VS")]
    [ExtensionUri(PythonConstants.PytestExecutorUriString)]
    public class PytestTestExecutor : ITestExecutor {
        private static readonly Guid PythonRemoteDebugPortSupplierUnsecuredId = new Guid("{FEB76325-D127-4E02-B59D-B16D93D46CF5}");
        private static readonly Guid PythonDebugEngineGuid = new Guid("EC1375B7-E2CE-43E8-BF75-DC638DE1F1F9");
        private static readonly Guid NativeDebugEngineGuid = new Guid("3B476D35-A401-11D2-AAD4-00C04F990171");
        private static readonly string TestLauncherPath = PythonToolsInstallPath.GetFile("visualstudio_py_testlauncher.py");
        private readonly ManualResetEvent _cancelRequested = new ManualResetEvent(false);
        private readonly VisualStudioProxy _app;

        public PytestTestExecutor() {
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
                    var discovery = new PytestTestDiscoverer();
                    discovery.DiscoverTests(testGroup, settings, frameworkHandle, testCollection);
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

            // Group tests by resolved project settings; attempt robust path matching (case-insensitive, normalized)
            var groups = tests.GroupBy(t => {
                var sourcePath = t.Source ?? String.Empty;
                PythonProjectSettings proj;
                if (sourceToProjSettings.TryGetValue(sourcePath, out proj)) {
                    return proj;
                }
                // try normalized path match
                var normalized = Microsoft.PythonTools.Infrastructure.PathUtils.NormalizePath(sourcePath);
                var key = sourceToProjSettings.Keys.FirstOrDefault(k => Microsoft.PythonTools.Infrastructure.PathUtils.IsSamePath(k, normalized));
                if (key != null && sourceToProjSettings.TryGetValue(key, out proj)) {
                    return proj;
                }
                return null; // will trigger error trace and skipped outcome logic
            });

            foreach (var testGroup in groups) {
                if (testGroup.Key == null) {
                    Debug.WriteLine("Missing projectSettings for TestCases:");
                    Debug.WriteLine(String.Join(",\n", testGroup.Select(tc => tc.FullyQualifiedName)));
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

            var testConfig = new PytestConfiguration(runContext);
            using (var executor = new ExecutorService(
                testConfig,
                settings,
                frameworkHandle,
                runContext)
            ) {
                executor.Run(testGroup, _cancelRequested);
            }
            
            var testResults = ParseResults(testConfig.ResultsXmlPath, testGroup, frameworkHandle);

            foreach (var result in testResults) {
                if (_cancelRequested.WaitOne(0)) {
                    break;
                }
                frameworkHandle.RecordResult(result);
            }
        }

        private IEnumerable<TestResult> ParseResults(
            string resultsXMLPath,
            IEnumerable<TestCase> testCases,
            IFrameworkHandle frameworkHandle
        ) {
            // Default TestResults
            var pytestIdToResultsMap = testCases
                .Select(tc => new TestResult(tc) { Outcome = TestOutcome.Skipped })
                .ToDictionary(tr => tr.TestCase.GetPropertyValue<string>(Pytest.Constants.PytestIdProperty, String.Empty), tr => tr);

            if (File.Exists(resultsXMLPath)) {
                try {
                    var doc = JunitXmlTestResultParser.Read(resultsXMLPath);
                    Parse(doc, pytestIdToResultsMap, frameworkHandle);
                } catch (Exception ex) {
                    frameworkHandle.SendMessage(TestMessageLevel.Error, ex.Message);
                }
            } else {
                frameworkHandle.SendMessage(TestMessageLevel.Error, Strings.PytestResultsXmlNotFound.FormatUI(resultsXMLPath));
            }

            return pytestIdToResultsMap.Values;
        }

        private void Parse(
            XPathDocument doc,
            Dictionary<string, TestResult> pytestIdToResultsMap,
            IFrameworkHandle frameworkHandle
        ) {
            var xmlTestResultNodes = doc.CreateNavigator().SelectDescendants("testcase", "", false);
         
            foreach (XPathNavigator pytestResultNode in xmlTestResultNodes) {
                if (_cancelRequested.WaitOne(0)) {
                    break;
                }
                try {
                    var pytestId = JunitXmlTestResultParser.GetPytestId(pytestResultNode);
                    if (pytestId != null && pytestIdToResultsMap.TryGetValue(pytestId, out TestResult vsTestResult)) {
                        JunitXmlTestResultParser.UpdateVsTestResult(vsTestResult, pytestResultNode);
                    } else {
                        frameworkHandle.SendMessage(TestMessageLevel.Error, Strings.ErrorTestCaseNotFound.FormatUI(pytestResultNode.OuterXml));
                    }
                } catch (Exception ex) {
                    frameworkHandle.SendMessage(TestMessageLevel.Error, ex.Message);
                }
            }
        }
    }
}
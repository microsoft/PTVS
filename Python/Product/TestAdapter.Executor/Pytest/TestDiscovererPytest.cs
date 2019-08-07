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
using System.IO;
using System.Linq;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.TestAdapter.Config;
using Microsoft.PythonTools.TestAdapter.Services;
using Microsoft.PythonTools.TestAdapter.Utils;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Newtonsoft.Json;

namespace Microsoft.PythonTools.TestAdapter.Pytest {
    internal class TestDiscovererPytest : IPythonTestDiscoverer {
        private readonly PythonProjectSettings _settings;
        private IMessageLogger _logger;
        private static readonly string DiscoveryAdapterPath = PythonToolsInstallPath.GetFile("PythonFiles\\testing_tools\\run_adapter.py");

        public TestDiscovererPytest(PythonProjectSettings settings) {
            _settings = settings;
        }

        public void DiscoverTests(IEnumerable<string> sources, IMessageLogger logger, ITestCaseDiscoverySink discoverySink) {
            _logger = logger;
            var workspaceText = _settings.IsWorkspace ? Strings.WorkspaceText : Strings.ProjectText;
            LogInfo(Strings.PythonTestDiscovererStartedMessage.FormatUI(PythonConstants.PytestText, _settings.ProjectName, workspaceText, _settings.DiscoveryWaitTimeInSeconds));

            var env = InitializeEnvironment(sources, _settings);
            var outputfilename = Path.GetTempFileName();
            var arguments = GetArguments(sources, _settings, outputfilename);
       
            DebugInfo("cd " + _settings.WorkingDirectory);
            DebugInfo("set " + _settings.PathEnv + "=" + env[_settings.PathEnv]);
            DebugInfo($"{_settings.InterpreterPath} {string.Join(" ", arguments)}");

            try {
                var stdout = ProcessExecute.RunWithTimeout(
                    _settings.InterpreterPath,
                    env,
                    arguments,
                    _settings.WorkingDirectory,
                    _settings.PathEnv,
                    _settings.DiscoveryWaitTimeInSeconds
                );
                if (!String.IsNullOrEmpty(stdout)) {
                    Error(stdout);
                }
            } catch (TimeoutException) {
                Error(Strings.PythonTestDiscovererTimeoutErrorMessage);
                return;
            }

            if (!File.Exists(outputfilename)) {
                Error(Strings.PythonDiscoveryResultsNotFound.FormatUI(outputfilename));
                return;
            }

            string json = File.ReadAllText(outputfilename);
            if (string.IsNullOrEmpty(json)) {
                return;
            }

            List<PytestDiscoveryResults> results = null;
            try {
                results = JsonConvert.DeserializeObject<List<PytestDiscoveryResults>>(json);
                CreateVsTests(results, discoverySink);
            } catch (InvalidOperationException ex) {
                Error("Failed to parse: {0}".FormatInvariant(ex.Message));
                Error(json ?? string.Empty);
            } catch (JsonException ex) {
                Error("Failed to parse: {0}".FormatInvariant(ex.Message));
                Error(json ?? string.Empty);
            }
        }

        private void CreateVsTests(IEnumerable<PytestDiscoveryResults> discoveryResults, ITestCaseDiscoverySink discoverySink) {
            foreach (var result in discoveryResults.MaybeEnumerate()) {
                var parentMap = result.Parents.ToDictionary(p => p.Id, p => p);
                foreach (PytestTest test in result.Tests) {
                    try {
                        TestCase tc = test.ToVsTestCase(_settings.ProjectHome, parentMap);
                        discoverySink?.SendTestCase(tc);
                    } catch (Exception ex) {
                        Error(ex.Message);
                    }
                }
            }
        }

        public string[] GetArguments(IEnumerable<string> sources, PythonProjectSettings projSettings, string outputfilename) {
            var arguments = new List<string>();
            arguments.Add(DiscoveryAdapterPath);
            arguments.Add("discover");
            arguments.Add("pytest");

            arguments.Add("--output-file");
            arguments.Add(outputfilename);

            // For a small set of tests, we'll pass them on the command
            // line. Once we exceed a certain (arbitrary) number, create
            // a test list on disk so that we do not overflow the 
            // 32K argument limit.
            bool useTestList = sources.Count() > 5;
            if (!projSettings.IsWorkspace &&
                useTestList) {
                var testListFilePath = TestUtils.CreateTestListFile(sources);
                arguments.Add("--test-list");
                arguments.Add(testListFilePath);
            }
            //Note pytest specific arguments go after this separator
            arguments.Add("--");
            // Add source files to pytest as arguments
            if (!projSettings.IsWorkspace &&
                !useTestList) {
                foreach (var s in sources) {
                    arguments.Add(s);
                }
            }
            arguments.Add("--cache-clear");
            return arguments.ToArray();
        }

        private Dictionary<string, string> InitializeEnvironment(IEnumerable<string> sources, PythonProjectSettings projSettings) {
            var pythonPathVar = projSettings.PathEnv;
            var pythonPath = GetSearchPaths(sources, projSettings);
            var env = new Dictionary<string, string>();

            if (!string.IsNullOrWhiteSpace(pythonPathVar)) {
                env[pythonPathVar] = pythonPath;
            }

            foreach (var envVar in projSettings.Environment) {
                env[envVar.Key] = envVar.Value;
            }

            env["PYTHONUNBUFFERED"] = "1";
            return env;
        }

        private string GetSearchPaths(IEnumerable<string> sources, PythonProjectSettings settings) {
            var paths = settings.SearchPath;

            paths.Insert(0, settings.WorkingDirectory);

            string searchPaths = string.Join(
                ";",
                paths.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase)
            );
            return searchPaths;
        }

        [Conditional("DEBUG")]
        private void DebugInfo(string message) {
            _logger?.SendMessage(TestMessageLevel.Informational, message ?? String.Empty);
        }

        private void LogInfo(string message) {
            _logger?.SendMessage(TestMessageLevel.Informational, message ?? String.Empty);
        }

        private void Error(string message) {
            _logger?.SendMessage(TestMessageLevel.Error, message ?? String.Empty);
        }
    }
}

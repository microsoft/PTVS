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
using System.Windows.Forms;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.TestAdapter.Config;
using Microsoft.PythonTools.TestAdapter.Services;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Newtonsoft.Json;

namespace Microsoft.PythonTools.TestAdapter.Pytest {

    /// <summary>
    /// Note even though we specify  [DefaultExecutorUri(PythonConstants.PytestExecutorUriString)] we still get all .py source files
    /// from all testcontainers.  
    /// </summary>

    // 1. If an ITestDiscoverer only includes one or more [FileExtension] attributes, then it will only be invoked for sources that are 
    // files with the specified extensions (and that exist on disk at the specified path)
    // 2. If an ITestDiscoverer only includes a [DirectoryBasedTestDiscoverer] attribute, then it will only be invoked for sources that 
    // are directories (and that exist on disk at the specified path)
    // 3. If an ITestDiscoverer includes both [FileExtension] and [DirectoryBasedTestDiscoverer] attributes then it will be invoked for 
    // sources that exist on disk and that are either files matching the specified extensions OR directories.
    // 4. If an ITestDiscoverer includes neither [FileExtension] nor [DirectoryBasedTestDiscoverer] attributes it will be invoked for all 
    // test container sources that exist on disk (regardless of the file extensions of these sources, and regardless of whether the source 
    // is a directory). For example, if your ITestDiscoverer omits both attributes, it will end up being called for .dll files for any C# 
    // test projects that are included in the user's solution.
    [DirectoryBasedTestDiscoverer]
    [DefaultExecutorUri(PythonConstants.PytestExecutorUriString)]
    public class PytestTestDiscoverer : PythonTestDiscoverer {
        private IMessageLogger _logger;
        private static readonly string DiscoveryAdapterPath = PythonToolsInstallPath.GetFile("PythonFiles\\testing_tools\\run_adapter.py");

        public PytestTestDiscoverer() : base(TestFrameworkType.Pytest) {
            
        }

        public override void DiscoverTests(
            IEnumerable<string> sources,
            PythonProjectSettings settings,
            IMessageLogger logger,
            ITestCaseDiscoverySink discoverySink
        ) {
            if (sources is null) {
                throw new ArgumentNullException(nameof(sources));
            }

            if (discoverySink is null) {
                throw new ArgumentNullException(nameof(discoverySink));
            }

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var workspaceText = settings.IsWorkspace ? Strings.WorkspaceText : Strings.ProjectText;
            LogInfo(Strings.PythonTestDiscovererStartedMessage.FormatUI(PythonConstants.PytestText, settings.ProjectName, workspaceText, settings.DiscoveryWaitTimeInSeconds));

            var env = InitializeEnvironment(sources, settings);
            var outputFilePath = Path.GetTempFileName();
            var arguments = GetArguments(sources, settings, outputFilePath);

            LogInfo("cd " + settings.WorkingDirectory);
            LogInfo("set " + settings.PathEnv + "=" + env[settings.PathEnv]);
            LogInfo($"{settings.InterpreterPath} {string.Join(" ", arguments)}");

            try {
                var stdout = ProcessExecute.RunWithTimeout(
                    settings.InterpreterPath,
                    env,
                    arguments,
                    settings.WorkingDirectory,
                    settings.PathEnv,
                    settings.DiscoveryWaitTimeInSeconds
                );
                if (!String.IsNullOrEmpty(stdout)) {
                    Error(stdout);
                }
            } catch (TimeoutException) {
                Error(Strings.PythonTestDiscovererTimeoutErrorMessage);
                return;
            }

            if (!File.Exists(outputFilePath)) {
                Error(Strings.PythonDiscoveryResultsNotFound.FormatUI(outputFilePath));
                return;
            }

            string json = File.ReadAllText(outputFilePath);
            if (string.IsNullOrEmpty(json)) {
                return;
            }

            try {
                var results = JsonConvert.DeserializeObject<List<PytestDiscoveryResults>>(json);
                var testcases = ParseDiscoveryResults(results, settings.ProjectHome);

                foreach (var tc in testcases) {
                    // Note: Test Explorer will show a key not found exception if we use a source path that doesn't match a test container's source.
                    if (settings.TestContainerSources.TryGetValue(tc.CodeFilePath, out _)) {
                        discoverySink.SendTestCase(tc);
                    }
                }
            } catch (InvalidOperationException ex) {
                Error("Failed to parse: {0}".FormatInvariant(ex.Message));
                Error(json);
            } catch (JsonException ex) {
                Error("Failed to parse: {0}".FormatInvariant(ex.Message));
                Error(json);
            }
        }

        internal IEnumerable<TestCase> ParseDiscoveryResults(IList<PytestDiscoveryResults> results, string projectHome) {
            if (results is null) {
                throw new ArgumentNullException(nameof(results));
            }

            var testcases = results
                .Where(r => r.Tests != null)
                .SelectMany(r => r.Tests.Select(test => TryCreateVsTestCase(test, projectHome)))
                .Where(tc => tc != null);

            return testcases;
        }

        private TestCase TryCreateVsTestCase(PytestTest test, string projectHome) {
            try {
                TestCase tc = test.ToVsTestCase(projectHome);
                tc.Source = projectHome;
                return tc;
            } catch (Exception ex) {
                Error(ex.Message);
            }
            return null;
        }

        public string[] GetArguments(IEnumerable<string> sources, PythonProjectSettings projSettings, string outputfilename) =>
            new[] {
            DiscoveryAdapterPath,
            "discover",
            "pytest",
            "--output-file",
            outputfilename,
            //Note pytest specific arguments go after this separator
            "--",
            "--cache-clear",
            String.Format("--rootdir={0}", projSettings.ProjectHome)
            };

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
        private void Warn(string message) {
            _logger?.SendMessage(TestMessageLevel.Warning, message ?? String.Empty);
        }
    }
}

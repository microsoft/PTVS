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
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Xml;
using System.Xml.XPath;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.TestAdapter.Config;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Microsoft.PythonTools.TestAdapter.Services {
    public class ExecutorService : IDisposable {
        private readonly IFrameworkHandle _frameworkHandle;
        private static readonly string TestLauncherPath = PythonToolsInstallPath.GetFile("testlauncher.py");
        private static readonly Guid PythonRemoteDebugPortSupplierUnsecuredId = new Guid("{FEB76325-D127-4E02-B59D-B16D93D46CF5}");
        private static readonly Guid PythonDebugEngineGuid = new Guid("EC1375B7-E2CE-43E8-BF75-DC638DE1F1F9");
        private static readonly Guid NativeDebugEngineGuid = new Guid("3B476D35-A401-11D2-AAD4-00C04F990171");
        private readonly VisualStudioProxy _app;
        private readonly PythonProjectSettings _projectSettings;
        private readonly PythonDebugMode _debugMode;
        private readonly string _debugSecret;
        private readonly int _debugPort;
        private readonly IRunContext _runContext;
        private readonly ITestConfiguration _testConfig;
        /// <summary>
        /// Used to send messages to TestExplorer's Test output pane
        /// </summary>
        sealed class TestRedirector : Redirector {
            private readonly IMessageLogger _logger;

            public TestRedirector(IMessageLogger logger) {
                _logger = logger;
            }

            public override void WriteErrorLine(string line) {
                try {
                    _logger.SendMessage(TestMessageLevel.Error, line);
                } catch (ArgumentException) {
                }
            }

            public override void WriteLine(string line) {
                try {
                    _logger.SendMessage(TestMessageLevel.Informational, line);
                } catch (ArgumentException) {
                }
            }
        }

        public ExecutorService(
            ITestConfiguration config, 
            PythonProjectSettings projectSettings, 
            IFrameworkHandle frameworkHandle, 
            IRunContext runContext
        ) {
            _testConfig = config;
            _projectSettings = projectSettings;
            _frameworkHandle = frameworkHandle;
            _runContext = runContext;
            _app = VisualStudioProxy.FromEnvironmentVariable(PythonConstants.PythonToolsProcessIdEnvironmentVariable);

            GetDebugSettings(_app, _runContext, _projectSettings, out _debugMode, out _debugSecret, out _debugPort);
        }

        public void Dispose() {

        }

        public string[] GetArguments(IEnumerable<TestCase> tests, string coveragePath) {
            var arguments = new List<string> {
                TestLauncherPath,
                _projectSettings.WorkingDirectory,
                _testConfig.Command,
                _debugSecret,
                _debugPort.ToString(),
                GetDebuggerSearchPath(_projectSettings.UseLegacyDebugger),
                _debugMode == PythonDebugMode.PythonAndNative ? "mixed" : string.Empty,
                coveragePath ?? string.Empty
            };

            var testAruguments = _testConfig.GetExecutionArguments(tests, _projectSettings);
            arguments.AddRange(testAruguments);
            return arguments.ToArray();
        }

        /// <summary>
        /// Returns true if this is a dry run. Dry runs require a
        /// &lt;DryRun value="true" /&gt; element under RunSettings/Python.
        /// </summary>
        internal static bool IsDryRun(IRunSettings settings) {
            var doc = Read(settings.SettingsXml);
            try {
                var node = doc.CreateNavigator().SelectSingleNode("/RunSettings/Python/DryRun[@value='true']");
                return node != null;
            } catch (Exception ex) {
                Debug.Fail(ex.ToUnhandledExceptionMessage(typeof(TestExecutorUnitTest)));
                return false;
            }
        }

        /// <summary>
        /// Returns true if the console should be shown. This is the default
        /// unless a &lt;ShowConsole value="false" /&gt; element exists under
        /// RunSettings/Python.
        /// </summary>
        internal static bool ShouldShowConsole(IRunSettings settings) {
            var doc = Read(settings.SettingsXml);
            try {
                var node = doc.CreateNavigator().SelectSingleNode("/RunSettings/Python/ShowConsole[@value='false']");
                return node == null;
            } catch (Exception ex) {
                Debug.Fail(ex.ToUnhandledExceptionMessage(typeof(TestExecutorUnitTest)));
                return true;
            }
        }

        internal static string GetCoveragePath(IEnumerable<TestCase> tests) {
            string bestFile = null, bestClass = null, bestMethod = null;

            // Try and generate a friendly name for the coverage report.  We use
            // the filename, class, and method.  We include each one if we're
            // running from a single filename/class/method.  When we have multiple
            // we drop the identifying names.  If we have multiple files we
            // go to the top level directory...  If all else fails we do "pycov".
            foreach (var test in tests) {
                string testFile, testClass, testMethod;
                TestReader.ParseFullyQualifiedTestName(
                    test.FullyQualifiedName,
                    out testFile,
                    out testClass,
                    out testMethod
                );

                bestFile = UpdateBestFile(bestFile, test.CodeFilePath);
                if (bestFile != test.CodeFilePath) {
                    // Different files, don't include class/methods even
                    // if they happen to be the same.
                    bestClass = bestMethod = "";
                }

                bestClass = UpdateBest(bestClass, testClass);
                bestMethod = UpdateBest(bestMethod, testMethod);
            }

            string filename = "";

            if (!String.IsNullOrWhiteSpace(bestFile)) {
                if (ModulePath.IsPythonSourceFile(bestFile)) {
                    filename = ModulePath.FromFullPath(bestFile).ModuleName;
                } else {
                    filename = Path.GetFileName(bestFile);
                }
            } else {
                filename = "pycov";
            }

            if (!String.IsNullOrWhiteSpace(bestClass)) {
                filename += "_" + bestClass;
            }

            if (!String.IsNullOrWhiteSpace(bestMethod)) {
                filename += "_" + bestMethod;
            }

            filename += "_" + DateTime.Now.ToString("s").Replace(':', '_');

            return Path.Combine(Path.GetTempPath(), filename);
        }

        private static XPathDocument Read(string xml) {
            var settings = new XmlReaderSettings();
            settings.XmlResolver = null;
            return new XPathDocument(XmlReader.Create(new StringReader(xml), settings));
        }

        private static string UpdateBest(string best, string test) {
            if (best == null || best == test) {
                best = test;
            } else if (!string.IsNullOrEmpty(best)) {
                best = "";
            }

            return best;
        }

        internal static string UpdateBestFile(string bestFile, string testFile) {
            if (bestFile == null || bestFile == testFile) {
                bestFile = testFile;
            } else if (!string.IsNullOrEmpty(bestFile)) {
                // Get common directory name, trim to the last \\ where we 
                // have things in common
                int lastSlash = 0;
                for (int i = 0; i < bestFile.Length && i < testFile.Length; i++) {
                    if (bestFile[i] != testFile[i]) {
                        bestFile = bestFile.Substring(0, lastSlash);
                        break;
                    } else if (bestFile[i] == '\\' || bestFile[i] == '/') {
                        lastSlash = i;
                    }
                }
            }

            return bestFile;
        }

        internal static bool EnableCodeCoverage(IRunContext runContext) {
            var doc = Read(runContext.RunSettings.SettingsXml);
            XPathNodeIterator nodes = doc.CreateNavigator().Select("/RunSettings/Python/EnableCoverage");
            bool enableCoverage;
            if (nodes.MoveNext()) {
                if (Boolean.TryParse(nodes.Current.Value, out enableCoverage)) {
                    return enableCoverage;
                }
            }
            return false;
        }

        internal static void AttachCoverageResults(IFrameworkHandle frameworkHandle, string covPath) {
            if (File.Exists(covPath + ".xml")) {
                var set = new AttachmentSet(PythonConstants.PythonCodeCoverageUri, "CodeCoverage");

                set.Attachments.Add(
                    new UriDataAttachment(new Uri(covPath + ".xml"), "Coverage Data")
                );
                frameworkHandle.RecordAttachments(new[] { set });

                File.Delete(covPath);
            } else {
                frameworkHandle.SendMessage(TestMessageLevel.Warning, Strings.Test_NoCoverageProduced);
            }
        }

        internal static void GetDebugSettings(VisualStudioProxy app, IRunContext runContext, PythonProjectSettings projectSettings, out PythonDebugMode debugMode, out string debugSecret, out int debugPort) {
            debugMode = PythonDebugMode.None;
            debugSecret = "";
            debugPort = 0;

            if (runContext.IsBeingDebugged && app != null) {
                debugMode = projectSettings.EnableNativeCodeDebugging ? PythonDebugMode.PythonAndNative : PythonDebugMode.PythonOnly;
            }

            if (debugMode == PythonDebugMode.PythonOnly) {
                if (projectSettings.UseLegacyDebugger) {
                    var secretBuffer = new byte[24];
                    RandomNumberGenerator.Create().GetNonZeroBytes(secretBuffer);
                    debugSecret = Convert.ToBase64String(secretBuffer)
                                        .Replace('+', '-')
                                        .Replace('/', '_')
                                        .TrimEnd('=');
                }

                SocketUtils.GetRandomPortListener(IPAddress.Loopback, out debugPort).Stop();
            }
        }

        private Dictionary<string, string> InitializeEnvironment(IEnumerable<TestCase> tests) {
            var pythonPathVar = _projectSettings.PathEnv;
            var pythonPath = GetSearchPaths(tests, _projectSettings);
            var env = new Dictionary<string, string>();

            if (!string.IsNullOrWhiteSpace(pythonPathVar)) {
                env[pythonPathVar] = pythonPath;
            }

            foreach (var envVar in _projectSettings.Environment) {
                env[envVar.Key] = envVar.Value;
            }

            return env;
        }

        private string GetSearchPaths(IEnumerable<TestCase> tests, PythonProjectSettings settings) {
            var paths = settings.SearchPath;
            paths.Insert(0, settings.WorkingDirectory);

            string searchPaths = string.Join(
                ";",
                paths.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase)
            );
            return searchPaths;
        }

        public void Run(IEnumerable<TestCase> tests, string coveragePath, ManualResetEvent cancelRequested) {
            try {
                DetachFromSillyManagedProcess(_app, _debugMode);

                var env = InitializeEnvironment(tests);
                var arguments = GetArguments(tests, coveragePath);
                var testRedirector = new TestRedirector(_frameworkHandle);

                using (var proc = ProcessOutput.Run(
                    _projectSettings.InterpreterPath,
                    arguments,
                    _projectSettings.WorkingDirectory,
                    env,
                    visible: false,
                    testRedirector,
                    quoteArgs:true,
                    elevate:false,
                    System.Text.Encoding.UTF8,
                    System.Text.Encoding.UTF8
                )) {
                    LogInfo("cd " + _projectSettings.WorkingDirectory);
                    LogInfo("set " + _projectSettings.PathEnv + "=" + env[_projectSettings.PathEnv]);
                    LogInfo(proc.Arguments);

                    if (!proc.ExitCode.HasValue) {
                        try {
                            if (_debugMode != PythonDebugMode.None) {
                                AttachDebugger(_app, proc, _debugMode, _debugSecret, _debugPort);
                            }

                            var handles = new WaitHandle[] { cancelRequested, proc.WaitHandle };
                            if (proc.WaitHandle != null) {
                                switch (WaitHandle.WaitAny(handles)) {
                                    case 0:
                                        // We've been cancelled
                                        try {
                                            proc.Kill();
                                        } catch (InvalidOperationException) {
                                            // Process has already exited
                                        }
                                        break;
                                    case 1:
                                        break;
                                }
                            }

                        } catch (COMException ex) {
                            Error(Strings.Test_ErrorConnecting);
                            DebugError(ex.ToString());
                            try {
                                proc.Kill();
                            } catch (InvalidOperationException) {
                                // Process has already exited
                            }
                        }
                    }
                }
            } catch (Exception e) {
                Error(e.ToString());
            }
        }

        internal static void AttachDebugger(VisualStudioProxy app, ProcessOutput proc, PythonDebugMode debugMode, string debugSecret, int debugPort) {
            if (debugMode == PythonDebugMode.PythonOnly) {
                string qualifierUri = string.Format("tcp://{0}@localhost:{1}", debugSecret, debugPort);
                while (!app.AttachToProcess(proc, PythonRemoteDebugPortSupplierUnsecuredId, qualifierUri)) {
                    if (proc.Wait(TimeSpan.FromMilliseconds(500))) {
                        break;
                    }
                }
            } else if (debugMode == PythonDebugMode.PythonAndNative) {
                var engines = new[] { PythonDebugEngineGuid, NativeDebugEngineGuid };
                while (!app.AttachToProcess(proc, engines)) {
                    if (proc.Wait(TimeSpan.FromMilliseconds(500))) {
                        break;
                    }
                }
            }
        }

        [Conditional("DEBUG")]
        private void DebugInfo(string message) {
            _frameworkHandle.SendMessage(TestMessageLevel.Informational, message);
        }


        [Conditional("DEBUG")]
        private void DebugError(string message) {
            _frameworkHandle.SendMessage(TestMessageLevel.Error, message);
        }

        private void LogInfo(string message) {
            _frameworkHandle.SendMessage(TestMessageLevel.Informational, message);
        }

        private void Error(string message) {
            _frameworkHandle.SendMessage(TestMessageLevel.Error, message);
        }

        internal static string GetDebuggerSearchPath(bool isLegacy) {
            if (isLegacy) {
                return Path.GetDirectoryName(Path.GetDirectoryName(PythonToolsInstallPath.GetFile("ptvsd\\__init__.py")));
            }

            return Path.GetDirectoryName(Path.GetDirectoryName(PythonToolsInstallPath.GetFile("Packages\\ptvsd\\__init__.py")));
        }

        internal static void DetachFromSillyManagedProcess(VisualStudioProxy app, PythonDebugMode debugMode) {
            var dte = app?.GetDTE();
            if (dte != null && debugMode != PythonDebugMode.None) {
                dte.Debugger.DetachAll();
            }
        }
    }
}

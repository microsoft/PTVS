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
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Xml.XPath;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;
using MSBuild = Microsoft.Build.Evaluation;

namespace Microsoft.PythonTools.TestAdapter {
    //using ValidateArg = Microsoft.VisualStudio.TestPlatform.ObjectModel.ValidateArg;

    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
        Justification = "object owned by VS")]
    [ExtensionUri(TestExecutor.ExecutorUriString)]
    class TestExecutor : ITestExecutor {
        public const string ExecutorUriString = "executor://PythonTestExecutor/v1";
        public static readonly Uri ExecutorUri = new Uri(ExecutorUriString);
        private static readonly Guid PythonRemoteDebugPortSupplierUnsecuredId = new Guid("{FEB76325-D127-4E02-B59D-B16D93D46CF5}");
        private static readonly Guid PythonDebugEngineGuid = new Guid("EC1375B7-E2CE-43E8-BF75-DC638DE1F1F9");
        private static readonly string TestLauncherPath = PythonToolsInstallPath.GetFile("visualstudio_py_testlauncher.py");

        private readonly ManualResetEvent _cancelRequested = new ManualResetEvent(false);

        private readonly VisualStudioProxy _app;
        private readonly CompositionContainer _container;
        private readonly IInterpreterOptionsService _interpreterService;

        public TestExecutor() {
            _app = VisualStudioProxy.FromEnvironmentVariable(PythonConstants.PythonToolsProcessIdEnvironmentVariable);
            _container = InterpreterCatalog.CreateContainer(typeof(IInterpreterRegistryService), typeof(IInterpreterOptionsService), typeof(TestExecutorProjectContext));
            _interpreterService = _container.GetExportedValue<IInterpreterOptionsService>();
        }


        public void Cancel() {
            _cancelRequested.Set();
        }

        public void RunTests(IEnumerable<string> sources, IRunContext runContext, IFrameworkHandle frameworkHandle) {
            ValidateArg.NotNull(sources, "sources");
            ValidateArg.NotNull(runContext, "runContext");
            ValidateArg.NotNull(frameworkHandle, "frameworkHandle");
            
            _cancelRequested.Reset();

            var receiver = new TestReceiver();

            var discoverer = new TestDiscoverer(_container);
            discoverer.DiscoverTests(sources, null, null, receiver);

            if (_cancelRequested.WaitOne(0)) {
                return;
            }

            RunTestCases(receiver.Tests, runContext, frameworkHandle);
        }

        public void RunTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle) {
            ValidateArg.NotNull(tests, "tests");
            ValidateArg.NotNull(runContext, "runContext");
            ValidateArg.NotNull(frameworkHandle, "frameworkHandle");

            _cancelRequested.Reset();

            RunTestCases(tests, runContext, frameworkHandle);
        }

        private void RunTestCases(
            IEnumerable<TestCase> tests,
            IRunContext runContext,
            IFrameworkHandle frameworkHandle
        ) {
            // .pyproj file path -> project settings
            var sourceToSettings = new Dictionary<string, PythonProjectSettings>();

            bool codeCoverage = EnableCodeCoverage(runContext);
            string covPath = null;
            if (codeCoverage) {
                covPath = GetCoveragePath(tests);
            }

            foreach (var test in tests) {
                if (_cancelRequested.WaitOne(0)) {
                    break;
                }

                try {
                    RunTestCase(frameworkHandle, runContext, test, sourceToSettings, covPath);
                } catch (Exception ex) {
                    frameworkHandle.SendMessage(TestMessageLevel.Error, ex.ToString());
                }
            }

            if (codeCoverage) {
                if (File.Exists(covPath + ".xml")) {
                    var set = new AttachmentSet(
                        PythonRunSettings.PythonCodeCoverageUri,
                        "CodeCoverage"
                    );

                    set.Attachments.Add(
                        new UriDataAttachment(new Uri(covPath + ".xml"), "Coverage Data")
                    );
                    frameworkHandle.RecordAttachments(new[] { set });

                    File.Delete(covPath);
                } else {
                    frameworkHandle.SendMessage(
                        TestMessageLevel.Warning,
                        Resources.NoCoverageProduced
                    );
                }
            }
        }

        private static string GetCoveragePath(IEnumerable<TestCase> tests) {
            string bestFile = null, bestClass = null, bestMethod = null;

            // Try and generate a friendly name for the coverage report.  We use
            // the filename, class, and method.  We include each one if we're
            // running from a single filename/class/method.  When we have multiple
            // we drop the identifying names.  If we have multiple files we
            // go to the top level directory...  If all else fails we do "pycov".
            foreach (var test in tests) {
                string testFile, testClass, testMethod;
                TestAnalyzer.ParseFullyQualifiedTestName(
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

        private static string UpdateBest(string best, string test) {
            if (best == null || best == test) {
                best = test;
            } else if (best != "") {
                best = "";
            }

            return best;
        }

        internal static string UpdateBestFile(string bestFile, string testFile) {
            if (bestFile == null || bestFile == testFile) {
                bestFile = testFile;
            } else if (bestFile != "") {
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

        private static bool EnableCodeCoverage(IRunContext runContext) {
            var doc = new XPathDocument(new StringReader(runContext.RunSettings.SettingsXml));
            XPathNodeIterator nodes = doc.CreateNavigator().Select("/RunSettings/Python/EnableCoverage");
            bool enableCoverage;
            if (nodes.MoveNext()) {
                if (Boolean.TryParse(nodes.Current.Value, out enableCoverage)) {
                    return enableCoverage;
                }
            }
            return false;
        }

        private void RunTestCase(
            IFrameworkHandle frameworkHandle,
            IRunContext runContext,
            TestCase test,
            Dictionary<string, PythonProjectSettings> sourceToSettings,
            string codeCoverageFile
        ) {
            var testResult = new TestResult(test);
            frameworkHandle.RecordStart(test);
            testResult.StartTime = DateTimeOffset.Now;

            PythonProjectSettings settings;
            if (!sourceToSettings.TryGetValue(test.Source, out settings)) {
                sourceToSettings[test.Source] = settings = LoadProjectSettings(test.Source);
            }
            if (settings == null) {
                frameworkHandle.SendMessage(
                    TestMessageLevel.Error,
                    String.Format(Resources.UnableToDetermineInterpreter, test.Source)
                );
                RecordEnd(
                    frameworkHandle,
                    test,
                    testResult,
                    null,
                    String.Format(Resources.UnableToDetermineInterpreter, test.Source),
                    TestOutcome.Failed);
                return;
            }

            var debugMode = PythonDebugMode.None;
            if (runContext.IsBeingDebugged && _app != null) {
                debugMode = settings.EnableNativeCodeDebugging ? PythonDebugMode.PythonAndNative : PythonDebugMode.PythonOnly;
            }

            var testCase = new PythonTestCase(settings, test, debugMode, codeCoverageFile);

            var dte = _app != null ? _app.GetDTE() : null;
            if (dte != null && debugMode != PythonDebugMode.None) {
                dte.Debugger.DetachAll();
            }

            if (!File.Exists(settings.Factory.Configuration.InterpreterPath)) {
                frameworkHandle.SendMessage(TestMessageLevel.Error,  String.Format(Resources.InterpreterDoesNotExist, settings.Factory.Configuration.InterpreterPath));
                return;
            }

            var env = new Dictionary<string, string>();
            var pythonPathVar = settings.Factory.Configuration.PathEnvironmentVariable;
            var pythonPath = testCase.SearchPaths;
            if (!string.IsNullOrWhiteSpace(pythonPathVar)) {
                if (_app != null) {
                    var settingsManager = SettingsManagerCreator.GetSettingsManager(dte);
                    if (settingsManager != null) {
                        var store = settingsManager.GetReadOnlySettingsStore(SettingsScope.UserSettings);
                        if (store != null && store.CollectionExists(@"PythonTools\Options\General")) {
                            var settingStr = store.GetString(@"PythonTools\Options\General", "ClearGlobalPythonPath", "True");
                            bool settingBool;
                            if (bool.TryParse(settingStr, out settingBool) && !settingBool) {
                                pythonPath += ";" + Environment.GetEnvironmentVariable(pythonPathVar);
                            }
                        }
                    }
                }
                env[pythonPathVar] = pythonPath;
            }

            foreach (var envVar in testCase.Environment) {
                env[envVar.Key] = envVar.Value;
            }

            using (var proc = ProcessOutput.Run(
                !settings.IsWindowsApplication ? 
                    settings.Factory.Configuration.InterpreterPath :
                    settings.Factory.Configuration.WindowsInterpreterPath,
                testCase.Arguments,
                testCase.WorkingDirectory,
                env,
                false,
                null
            )) {
                bool killed = false;

#if DEBUG
                frameworkHandle.SendMessage(TestMessageLevel.Informational, "cd " + testCase.WorkingDirectory);
                frameworkHandle.SendMessage(TestMessageLevel.Informational, "set " + (pythonPathVar ?? "") + "=" + (pythonPath ?? ""));
                frameworkHandle.SendMessage(TestMessageLevel.Informational, proc.Arguments);
#endif

                proc.Wait(TimeSpan.FromMilliseconds(500));
                if (debugMode != PythonDebugMode.None) {
                    if (proc.ExitCode.HasValue) {
                        // Process has already exited
                        frameworkHandle.SendMessage(
                            TestMessageLevel.Error, 
                            Resources.FailedToAttachExited);
                        if (proc.StandardErrorLines.Any()) {
                            frameworkHandle.SendMessage(TestMessageLevel.Error, "Standard error from Python:");
                            foreach (var line in proc.StandardErrorLines) {
                                frameworkHandle.SendMessage(TestMessageLevel.Error, line);
                            }
                        }
                    }

                    try {
                        if (debugMode == PythonDebugMode.PythonOnly) {
                            string qualifierUri = string.Format("tcp://{0}@localhost:{1}", testCase.DebugSecret, testCase.DebugPort);
                            while (!_app.AttachToProcess(proc, PythonRemoteDebugPortSupplierUnsecuredId, qualifierUri)) {
                                if (proc.Wait(TimeSpan.FromMilliseconds(500))) {
                                    break;
                                }
                            }
                        } else {
                            var engines = new[] { PythonDebugEngineGuid, VSConstants.DebugEnginesGuids.NativeOnly_guid };
                            while (!_app.AttachToProcess(proc, engines)) {
                                if (proc.Wait(TimeSpan.FromMilliseconds(500))) {
                                    break;
                                }
                            }
                        }

#if DEBUG
                    } catch (COMException ex) {
                        frameworkHandle.SendMessage(TestMessageLevel.Error, Resources.ErrorConnecting);
                        frameworkHandle.SendMessage(TestMessageLevel.Error, ex.ToString());
                        try {
                            proc.Kill();
                        } catch (InvalidOperationException) {
                            // Process has already exited
                        }
                        killed = true;
                    }
#else
                    } catch (COMException) {
                        frameworkHandle.SendMessage(TestMessageLevel.Error, Resources.ErrorConnecting);
                        try {
                            proc.Kill();
                        } catch (InvalidOperationException) {
                            // Process has already exited
                        }
                        killed = true;
                    }
#endif
                }


                // https://pytools.codeplex.com/workitem/2290
                // Check that proc.WaitHandle was not null to avoid crashing if
                // a test fails to start running. We will report failure and
                // send the error message from stdout/stderr.
                var handles = new WaitHandle[] { _cancelRequested, proc.WaitHandle };
                if (handles[1] == null) {
                    killed = true;
                }

                if (!killed && WaitHandle.WaitAny(handles) == 0) {
                    try {
                        proc.Kill();
                    } catch (InvalidOperationException) {
                        // Process has already exited
                    }
                    killed = true;
                } else {
                    RecordEnd(frameworkHandle, test, testResult,
                        string.Join(Environment.NewLine, proc.StandardOutputLines),
                        string.Join(Environment.NewLine, proc.StandardErrorLines),
                        (proc.ExitCode == 0 && !killed) ? TestOutcome.Passed : TestOutcome.Failed);
                }
            }
        }

        private PythonProjectSettings LoadProjectSettings(
            string projectFile
        ) {
            var registry = _container.GetExportedValue<IInterpreterRegistryService>();
            
            var buildEngine = new MSBuild.ProjectCollection();
            MSBuildProjectInterpreterFactoryProvider provider = null;
            try {
                
                var proj = buildEngine.LoadProject(projectFile);
                var interpreter = proj.GetPropertyValue(MSBuildConstants.InterpreterIdProperty);
                IPythonInterpreterFactory factory = null;
                if (interpreter != null) {
                    factory = registry.FindInterpreter(interpreter);
                }

                if (factory == null) {
                    var options = _container.GetExportedValue<IInterpreterOptionsService>();
                    factory = options.DefaultInterpreter;
                }

                if (factory == null) {
                    return null;
                }

                var projSettings = new PythonProjectSettings();
                projSettings.Factory = factory;

                projSettings.ProjectHome = Path.GetFullPath(Path.Combine(proj.DirectoryPath, proj.GetPropertyValue(PythonConstants.ProjectHomeSetting) ?? "."));

                bool isWindowsApplication;
                if (bool.TryParse(proj.GetPropertyValue(PythonConstants.IsWindowsApplicationSetting), out isWindowsApplication)) {
                    projSettings.IsWindowsApplication = isWindowsApplication;
                } else {
                    projSettings.IsWindowsApplication = false;
                }

                projSettings.WorkingDir = Path.GetFullPath(Path.Combine(projSettings.ProjectHome, proj.GetPropertyValue(PythonConstants.WorkingDirectorySetting) ?? "."));
                projSettings.SearchPath.AddRange(
                    (proj.GetPropertyValue(PythonConstants.SearchPathSetting) ?? "")
                        .Split(';')
                        .Where(path => !string.IsNullOrEmpty(path))
                        .Select(path => Path.GetFullPath(Path.Combine(projSettings.ProjectHome, path)))
                );

                // Add all extension <Reference> items to search path.
                foreach (var item in proj.GetItems(ProjectFileConstants.Reference)) {
                    string path = item.GetMetadataValue(PythonConstants.PythonExtension);
                    if (string.IsNullOrWhiteSpace(path)) {
                        continue;
                    }

                    string absPath;
                    try {
                        absPath = CommonUtils.GetAbsoluteFilePath(projSettings.ProjectHome, path);
                    } catch (InvalidOperationException) {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(absPath)) {
                        string parentPath = CommonUtils.GetParent(absPath);
                        if (!string.IsNullOrEmpty(parentPath)) {
                            projSettings.SearchPath.Add(parentPath);
                        }
                    }
                }

                projSettings.DjangoSettingsModule = proj.GetPropertyValue("DjangoSettingsModule");

                bool enableNativeCodeDebugging;
                if (bool.TryParse(proj.GetPropertyValue(PythonConstants.EnableNativeCodeDebugging), out enableNativeCodeDebugging)) {
                    projSettings.EnableNativeCodeDebugging = enableNativeCodeDebugging;
                } else {
                    projSettings.EnableNativeCodeDebugging = false;
                }

                var userEnv = proj.GetPropertyValue(PythonConstants.EnvironmentSetting);
                if (userEnv != null) {
                    foreach (var envVar in userEnv.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
                        var nameValue = envVar.Split(new[] { '=' }, 2);
                        if (nameValue.Length == 2) {
                            projSettings.Environment[nameValue[0]] = nameValue[1];
                        }
                    }
                }

                return projSettings;
            } finally {
                if (provider != null) {
                    provider.Dispose();
                }

                buildEngine.UnloadAllProjects();
                buildEngine.Dispose();
            }
        }

        private static void RecordEnd(IFrameworkHandle frameworkHandle, TestCase test, TestResult result, string stdout, string stderr, TestOutcome outcome) {
            result.EndTime = DateTimeOffset.Now;
            result.Duration = result.EndTime - result.StartTime;
            result.Outcome = outcome;
            result.Messages.Add(new TestResultMessage(TestResultMessage.StandardOutCategory, stdout));
            result.Messages.Add(new TestResultMessage(TestResultMessage.StandardErrorCategory, stderr));
            result.Messages.Add(new TestResultMessage(TestResultMessage.AdditionalInfoCategory, stderr));

            frameworkHandle.RecordResult(result);
            frameworkHandle.RecordEnd(test, outcome);
        }

        class TestReceiver : ITestCaseDiscoverySink {
            public List<TestCase> Tests { get; private set; }

            public TestReceiver() {
                Tests = new List<TestCase>();
            }

            public void SendTestCase(TestCase discoveredTest) {
                Tests.Add(discoveredTest);
            }
        }

        class PythonProjectSettings {
            public PythonProjectSettings() {
                SearchPath = new List<string>();
                WorkingDir = String.Empty;
                ProjectHome = String.Empty;
                DjangoSettingsModule = String.Empty;
                Environment = new Dictionary<string, string>();
            }

            public IPythonInterpreterFactory Factory { get; set; }
            public bool IsWindowsApplication { get; set; }
            public List<string> SearchPath { get; private set; }
            public string WorkingDir { get; set; }
            public string ProjectHome { get; set; }
            public string DjangoSettingsModule { get; set; }
            public bool EnableNativeCodeDebugging { get; set; }
            public Dictionary<string, string> Environment { get; set; }
        }

        enum PythonDebugMode {
            None,
            PythonOnly,
            PythonAndNative
        }

        class PythonTestCase {
            public readonly PythonProjectSettings Settings;
            public readonly TestCase TestCase;
            public readonly ModulePath ModulePath;
            public readonly string TestFilePath;
            public readonly string TestFile;
            public readonly string TestClass;
            public readonly string TestMethod;

            public readonly string DebugSecret;
            public readonly int DebugPort;

            public readonly string WorkingDirectory;
            public readonly string SearchPaths;
            public readonly IDictionary<string, string> Environment;
            public readonly IEnumerable<string> Arguments;

            public readonly string CodeCoverageFile;

            public PythonTestCase(PythonProjectSettings settings, TestCase testCase, PythonDebugMode debugMode, string codeCoverageFile) {
                Settings = settings;
                TestCase = testCase;
                CodeCoverageFile = codeCoverageFile;

                TestAnalyzer.ParseFullyQualifiedTestName(
                    testCase.FullyQualifiedName,
                    out TestFile,
                    out TestClass,
                    out TestMethod
                );

                TestFilePath = CommonUtils.GetAbsoluteFilePath(Settings.ProjectHome, TestFile);
                ModulePath = ModulePath.FromFullPath(TestFilePath);

                WorkingDirectory = Settings.WorkingDir;

                var paths = settings.SearchPath.ToList();

                paths.Insert(0, ModulePath.LibraryPath);
                paths.Insert(0, WorkingDirectory);
                if (debugMode == PythonDebugMode.PythonOnly) {
                    paths.Insert(0, PtvsdSearchPath);
                }

                SearchPaths = string.Join(
                    ";",
                    paths.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase)
                );

                var arguments = new List<string> {
                    TestLauncherPath,
                    "-m", ModulePath.ModuleName,
                    "-t", string.Format("{0}.{1}", TestClass, TestMethod)
                };

                if (CodeCoverageFile != null) {
                    arguments.Add("--coverage");
                    arguments.Add(CodeCoverageFile);
                }

                if (debugMode == PythonDebugMode.PythonOnly) {
                    var secretBuffer = new byte[24];
                    RandomNumberGenerator.Create().GetNonZeroBytes(secretBuffer);
                    DebugSecret = Convert.ToBase64String(secretBuffer)
                        .Replace('+', '-')
                        .Replace('/', '_')
                        .TrimEnd('=');

                    DebugPort = GetFreePort();

                    arguments.AddRange(new[] {
                        "-s", DebugSecret,
                        "-p", DebugPort.ToString()
                    });
                } else if (debugMode == PythonDebugMode.PythonAndNative) {
                    arguments.Add("-x");
                }

                Arguments = arguments;

                Environment = new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(settings.DjangoSettingsModule)) {
                    Environment["DJANGO_SETTINGS_MODULE"] = settings.DjangoSettingsModule;
                }
                foreach (var envVar in settings.Environment) {
                    Environment[envVar.Key] = envVar.Value;
                }
            }

            private static int GetFreePort() {
                return Enumerable.Range(new Random().Next(49152, 65536), 60000).Except(
                    from connection in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections()
                    select connection.LocalEndPoint.Port
                ).First();
            }

            private static string PtvsdSearchPath {
                get {
                    return Path.GetDirectoryName(Path.GetDirectoryName(PythonToolsInstallPath.GetFile("ptvsd\\__init__.py")));
                }
            }

        }
    }

}

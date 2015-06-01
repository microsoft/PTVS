/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.PythonTools.Analysis;
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

        private readonly VisualStudioApp _app;
        private readonly IInterpreterOptionsService _interpreterService;

        public TestExecutor() {
            _app = VisualStudioApp.FromEnvironmentVariable(PythonConstants.PythonToolsProcessIdEnvironmentVariable);
            _interpreterService = InterpreterOptionsServiceProvider.GetService(_app);
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

            var discoverer = new TestDiscoverer(_app, _interpreterService);
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

            foreach (var test in tests) {
                if (_cancelRequested.WaitOne(0)) {
                    break;
                }

                try {
                    RunTestCase(frameworkHandle, runContext, test, sourceToSettings);
                } catch (Exception ex) {
                    frameworkHandle.SendMessage(TestMessageLevel.Error, ex.ToString());
                }
            }
        }

        private void RunTestCase(
            IFrameworkHandle frameworkHandle,
            IRunContext runContext,
            TestCase test,
            Dictionary<string, PythonProjectSettings> sourceToSettings
        ) {
            var testResult = new TestResult(test);
            frameworkHandle.RecordStart(test);
            testResult.StartTime = DateTimeOffset.Now;

            PythonProjectSettings settings;
            if (!sourceToSettings.TryGetValue(test.Source, out settings)) {
                sourceToSettings[test.Source] = settings = LoadProjectSettings(test.Source, _interpreterService);
            }
            if (settings == null) {
                frameworkHandle.SendMessage(
                    TestMessageLevel.Error,
                    "Unable to determine interpreter to use for " + test.Source);
                RecordEnd(
                    frameworkHandle,
                    test,
                    testResult,
                    null,
                    "Unable to determine interpreter to use for " + test.Source,
                    TestOutcome.Failed);
                return;
            }

            var debugMode = PythonDebugMode.None;
            if (runContext.IsBeingDebugged && _app != null) {
                debugMode = settings.EnableNativeCodeDebugging ? PythonDebugMode.PythonAndNative : PythonDebugMode.PythonOnly;
            }

            var testCase = new PythonTestCase(settings, test, debugMode);

            var dte = _app.GetDTE();
            if (debugMode != PythonDebugMode.None) {
                dte.Debugger.DetachAll();
            }

            if (!File.Exists(settings.Factory.Configuration.InterpreterPath)) {
                frameworkHandle.SendMessage(TestMessageLevel.Error, "Interpreter path does not exist: " + settings.Factory.Configuration.InterpreterPath);
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
                        frameworkHandle.SendMessage(TestMessageLevel.Error, "Failed to attach debugger because the process has already exited.");
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
                        frameworkHandle.SendMessage(TestMessageLevel.Error, "Error occurred connecting to debuggee.");
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
                        frameworkHandle.SendMessage(TestMessageLevel.Error, "Error occurred connecting to debuggee.");
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
            string projectFile,
            IInterpreterOptionsService interpreterService
        ) {
            var buildEngine = new MSBuild.ProjectCollection();
            MSBuildProjectInterpreterFactoryProvider provider = null;
            try {
                var proj = buildEngine.LoadProject(projectFile);
                provider = new MSBuildProjectInterpreterFactoryProvider(interpreterService, proj);
                try {
                    provider.DiscoverInterpreters();
                } catch (InvalidDataException) {
                    // Can safely ignore this exception here.
                }

                if (provider.ActiveInterpreter == interpreterService.NoInterpretersValue) {
                    return null;
                }

                var projSettings = new PythonProjectSettings();
                projSettings.Factory = provider.ActiveInterpreter;

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

            public PythonTestCase(PythonProjectSettings settings, TestCase testCase, PythonDebugMode debugMode) {
                Settings = settings;
                TestCase = testCase;

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

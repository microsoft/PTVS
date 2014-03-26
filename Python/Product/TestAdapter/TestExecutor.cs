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
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;
using MSBuild = Microsoft.Build.Evaluation;

namespace Microsoft.PythonTools.TestAdapter {
    [ExtensionUri(TestExecutor.ExecutorUriString)]
    class TestExecutor : ITestExecutor {
        public const string ExecutorUriString = "executor://PythonTestExecutor/v1";
        public static readonly Uri ExecutorUri = new Uri(ExecutorUriString);
        private static readonly Guid PythonRemoteDebugPortSupplierUnsecuredId = new Guid("{FEB76325-D127-4E02-B59D-B16D93D46CF5}");
        private static readonly string TestLauncherPath = PythonToolsInstallPath.GetFile("visualstudio_py_testlauncher.py");

        private readonly ManualResetEvent _cancelRequested = new ManualResetEvent(false);

        public void Cancel() {
            _cancelRequested.Set();
        }

        public void RunTests(IEnumerable<string> sources, IRunContext runContext, IFrameworkHandle frameworkHandle) {
            ValidateArg.NotNull(sources, "sources");
            ValidateArg.NotNull(runContext, "runContext");
            ValidateArg.NotNull(frameworkHandle, "frameworkHandle");

            _cancelRequested.Reset();

            var receiver = new TestReceiver();

            using (var app = VisualStudioApp.FromCommandLineArgs(Environment.GetCommandLineArgs())) {
                var interpreterService = InterpreterOptionsServiceProvider.GetService(app);
                var discoverer = new TestDiscoverer(interpreterService);
                discoverer.DiscoverTests(sources, null, null, receiver);

                if (_cancelRequested.WaitOne(0)) {
                    return;
                }

                RunTestCases(app, interpreterService, receiver.Tests, runContext, frameworkHandle);
            }
        }

        public void RunTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle) {
            ValidateArg.NotNull(tests, "tests");
            ValidateArg.NotNull(runContext, "runContext");
            ValidateArg.NotNull(frameworkHandle, "frameworkHandle");

            _cancelRequested.Reset();

            using (var app = VisualStudioApp.FromCommandLineArgs(Environment.GetCommandLineArgs())) {
                var interpreterService = InterpreterOptionsServiceProvider.GetService(app);
                RunTestCases(app, interpreterService, tests, runContext, frameworkHandle);
            }
        }

        private void RunTestCases(
            VisualStudioApp app,
            IInterpreterOptionsService interpreterService,
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
                    RunTestCase(app, interpreterService, frameworkHandle, runContext, test, sourceToSettings);
                } catch (Exception ex) {
                    frameworkHandle.SendMessage(TestMessageLevel.Error, ex.ToString());
                }
            }
        }

        private void RunTestCase(
            VisualStudioApp app,
            IInterpreterOptionsService interpreterService,
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
                sourceToSettings[test.Source] = settings = LoadProjectSettings(test.Source, interpreterService);
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

            bool usePtvsd = runContext.IsBeingDebugged && app != null;

            var testCase = new PythonTestCase(settings, test, usePtvsd);

            if (usePtvsd) {
                app.DTE.Debugger.DetachAll();
            }

            if (!File.Exists(settings.Factory.Configuration.InterpreterPath)) {
                frameworkHandle.SendMessage(TestMessageLevel.Error, "Interpreter path does not exist: " + settings.Factory.Configuration.InterpreterPath);
                return;
            }

            var env = new Dictionary<string, string> {
                { settings.Factory.Configuration.PathEnvironmentVariable ?? "PYTHONPATH", testCase.SearchPaths }
            };

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
                frameworkHandle.SendMessage(TestMessageLevel.Informational, "set " + settings.Factory.Configuration.PathEnvironmentVariable + "=" + testCase.SearchPaths);
                frameworkHandle.SendMessage(TestMessageLevel.Informational, proc.Arguments);
#endif

                proc.Wait(TimeSpan.FromMilliseconds(500));
                if (runContext.IsBeingDebugged && app != null) {
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
                        while (!app.AttachToProcess(proc, PythonRemoteDebugPortSupplierUnsecuredId, testCase.DebugSecret, testCase.DebugPort)) {
                            if (proc.Wait(TimeSpan.FromMilliseconds(500))) {
                                break;
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
            try {
                var proj = buildEngine.LoadProject(projectFile);
                var provider = new MSBuildProjectInterpreterFactoryProvider(interpreterService, proj);
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

                return projSettings;
            } finally {
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

        class DataReceiver {
            public readonly StringBuilder Data = new StringBuilder();

            public void DataReceived(object sender, DataReceivedEventArgs e) {
                if (e.Data != null) {
                    Data.AppendLine(e.Data);
                }
            }
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
            }

            public IPythonInterpreterFactory Factory { get; set; }
            public bool IsWindowsApplication { get; set; }
            public List<string> SearchPath { get; private set; }
            public string WorkingDir { get; set; }
            public string ProjectHome { get; set; }
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
            public readonly IEnumerable<string> Arguments;

            public PythonTestCase(PythonProjectSettings settings, TestCase testCase, bool usePtvsd) {
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

                paths.Insert(0, WorkingDirectory);
                if (usePtvsd) {
                    paths.Insert(0, PtvsdSearchPath);
                }

                SearchPaths = string.Join(
                    ";", 
                    paths.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase)
                );

                var arguments = new List<string> {
                    TestLauncherPath,
                    "-m", ModulePath.ModuleName,
                    "--search", ModulePath.LibraryPath,
                    "-t", string.Format("{0}.{1}", TestClass, TestMethod)
                };

                if (usePtvsd) {
                    var secretBuffer = new byte[24];
                    RandomNumberGenerator.Create().GetNonZeroBytes(secretBuffer);
                    DebugSecret = Convert.ToBase64String(secretBuffer);

                    DebugPort = GetFreePort();

                    arguments.AddRange(new[] {
                        "-s", DebugSecret,
                        "-p", DebugPort.ToString()
                    });
                }

                Arguments = arguments;
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

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
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using MSBuild = Microsoft.Build.Evaluation;

namespace Microsoft.PythonTools.TestAdapter {
    [ExtensionUri(TestExecutor.ExecutorUriString)]
    class TestExecutor : ITestExecutor {
        public const string ExecutorUriString = "executor://PythonTestExecutor/v1";
        public static readonly Uri ExecutorUri = new Uri(ExecutorUriString);
        private static readonly Guid PythonRemoteDebugPortSupplierUnsecuredId = new Guid("{FEB76325-D127-4E02-B59D-B16D93D46CF5}");
        private static readonly string TestLauncherPath = Path.Combine(Path.GetDirectoryName(typeof(TestExecutor).Assembly.Location), "visualstudio_py_testlauncher.py");

        private readonly IInterpreterOptionsService _interpService = InterpreterOptionsServiceProvider.GetService();

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
            var discoverer = new TestDiscoverer(_interpService);
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

        private void RunTestCases(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle) {
            // May be null, but this is handled by RunTestCase if it matters.
            // No VS instance just means no debugging, but everything else is
            // okay.
            using (var app = VisualStudioApp.FromCommandLineArgs(Environment.GetCommandLineArgs())) {
                // .pyproj file path -> project settings
                var sourceToSettings = new Dictionary<string, PythonProjectSettings>();

                foreach (var test in tests) {
                    if (_cancelRequested.WaitOne(0)) {
                        break;
                    }

                    try {
                        RunTestCase(app, frameworkHandle, runContext, test, sourceToSettings);
                    } catch (Exception ex) {
                        frameworkHandle.SendMessage(TestMessageLevel.Error, ex.ToString());
                    }
                }
            }
        }

        private static int GetFreePort() {
            return Enumerable.Range(new Random().Next(49152, 65536), 60000).Except(
                from connection in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections()
                select connection.LocalEndPoint.Port
            ).First();
        }

        private static IEnumerable<string> GetInterpreterArgs(TestCase test) {
            string moduleName;
            string testClass;
            string testMethod;
            TestDiscoverer.ParseFullyQualifiedTestName(test.FullyQualifiedName, out moduleName, out testClass, out testMethod);

            return new[] {
                TestLauncherPath,
                "-m", moduleName,
                "-t", string.Format("{0}.{1}", testClass, testMethod)
            };
        }

        private static IEnumerable<string> GetDebugArgs(PythonProjectSettings settings, out string secret, out int port) {
            var secretBuffer = new byte[24];
            RandomNumberGenerator.Create().GetNonZeroBytes(secretBuffer);
            secret = Convert.ToBase64String(secretBuffer);

            port = GetFreePort();

            return new[] {
                "-s", secret,
                "-p", port.ToString()
            };
        }

        private void RunTestCase(VisualStudioApp app, IFrameworkHandle frameworkHandle, IRunContext runContext, TestCase test, Dictionary<string, PythonProjectSettings> sourceToSettings) {
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

            var args = GetInterpreterArgs(test);
            var searchPath = settings.SearchPath;

            string secret = null;
            int port = 0;
            if (runContext.IsBeingDebugged && app != null) {
                app.DTE.Debugger.DetachAll();
                args = args.Concat(GetDebugArgs(settings, out secret, out port));
            }

            var env = new Dictionary<string, string> {
                { settings.Factory.Configuration.PathEnvironmentVariable, searchPath }
            };

            using (var proc = ProcessOutput.Run(
                !settings.IsWindowsApplication ? 
                    settings.Factory.Configuration.InterpreterPath :
                    settings.Factory.Configuration.WindowsInterpreterPath,
                args,
                settings.WorkingDir,
                env,
                false,
                null)) {
                bool killed = false;

#if DEBUG
                frameworkHandle.SendMessage(TestMessageLevel.Informational, proc.Arguments);
#endif

                proc.Wait(TimeSpan.FromMilliseconds(500));
                if (runContext.IsBeingDebugged && app != null) {
                    try {
                        while (!app.AttachToProcess(proc, PythonRemoteDebugPortSupplierUnsecuredId, secret, port)) {
                            if (proc.Wait(TimeSpan.FromMilliseconds(500))) {
                                break;
                            }
                        }
#if DEBUG
                    } catch (COMException ex) {
                        frameworkHandle.SendMessage(TestMessageLevel.Error, "Error occurred connecting to debuggee.");
                        frameworkHandle.SendMessage(TestMessageLevel.Error, ex.ToString());
                        proc.Kill();
                        killed = true;
                    }
#else
                    } catch (COMException) {
                        frameworkHandle.SendMessage(TestMessageLevel.Error, "Error occurred connecting to debuggee.");
                        proc.Kill();
                        killed = true;
                    }
#endif
                }

                if (!killed && WaitHandle.WaitAny(new WaitHandle[] { _cancelRequested, proc.WaitHandle }) == 0) {
                    proc.Kill();
                    killed = true;
                } else {
                    RecordEnd(frameworkHandle, test, testResult,
                        string.Join(Environment.NewLine, proc.StandardOutputLines),
                        string.Join(Environment.NewLine, proc.StandardErrorLines),
                        (proc.ExitCode == 0 && !killed) ? TestOutcome.Passed : TestOutcome.Failed);
                }
            }
        }

        private PythonProjectSettings LoadProjectSettings(string projectFile) {
            var buildEngine = new MSBuild.ProjectCollection();
            var proj = buildEngine.LoadProject(projectFile);
            var projectHome = Path.GetFullPath(Path.Combine(proj.DirectoryPath, proj.GetPropertyValue(PythonConstants.ProjectHomeSetting) ?? "."));

            var projSettings = new PythonProjectSettings();
            var id = proj.GetPropertyValue("InterpreterId");
            var version = proj.GetPropertyValue("InterpreterVersion");
            projSettings.Factory = _interpService.FindInterpreter(id, version) ?? _interpService.DefaultInterpreter;
            if (projSettings.Factory == null) {
                return null;
            }

            bool isWindowsApplication;
            if (bool.TryParse(proj.GetPropertyValue(PythonConstants.IsWindowsApplicationSetting), out isWindowsApplication)) {
                projSettings.IsWindowsApplication = isWindowsApplication;
            } else {
                projSettings.IsWindowsApplication = false;
            }

            projSettings.WorkingDir = Path.GetFullPath(Path.Combine(projectHome, proj.GetPropertyValue(PythonConstants.WorkingDirectorySetting) ?? "."));
            projSettings.SearchPath = string.Join(";", 
                (proj.GetPropertyValue(PythonConstants.SearchPathSetting) ?? "")
                    .Split(';')
                    .Select(path => Path.GetFullPath(Path.Combine(projectHome, path)))
                .Concat(Enumerable.Repeat(projSettings.WorkingDir, 1)));
            
            return projSettings;
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
                SearchPath = String.Empty;
                WorkingDir = String.Empty;
            }

            public IPythonInterpreterFactory Factory { get; set; }
            public bool IsWindowsApplication { get; set; }
            public string SearchPath { get; set; }
            public string WorkingDir { get; set; }
        }
    }
}

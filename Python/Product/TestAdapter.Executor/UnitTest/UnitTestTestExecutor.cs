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
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Ipc.Json;
using Microsoft.PythonTools.TestAdapter.Config;
using Microsoft.PythonTools.TestAdapter.Services;
using Microsoft.PythonTools.TestAdapter.UnitTest;
using Microsoft.PythonTools.TestAdapter.Utils;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using TP = Microsoft.PythonTools.TestAdapter.TestProtocol;

namespace Microsoft.PythonTools.TestAdapter {
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
        Justification = "object owned by VS")]
    [ExtensionUri(PythonConstants.UnitTestExecutorUriString)]
    class UnittestTestExecutor : ITestExecutor {
        private static readonly string TestLauncherPath = PythonToolsInstallPath.GetFile("visualstudio_py_testlauncher.py");

        private readonly ManualResetEvent _cancelRequested = new ManualResetEvent(false);

        private readonly VisualStudioProxy _app;

        public UnittestTestExecutor() {
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

            var sourceToProjSettings = RunSettingsUtil.GetSourceToProjSettings(runContext.RunSettings, filterType:TestFrameworkType.UnitTest);
            var testColletion = new TestCollection();

            foreach (var testGroup in sources.GroupBy(x => sourceToProjSettings[x])) {
                var settings = testGroup.Key;

                try {
                    var discovery = new UnittestTestDiscoverer();
                    discovery.DiscoverTests(testGroup, settings, frameworkHandle, testColletion);
                } catch (Exception ex) {
                    frameworkHandle.SendMessage(TestMessageLevel.Error, ex.Message);
                }

                if (_cancelRequested.WaitOne(0)) {
                    return;
                }
            }

            RunTestCases(testColletion.Tests, runContext, frameworkHandle);
        }

        public void RunTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle) {

            //MessageBox.Show("Hello1: " + Process.GetCurrentProcess().Id);

            if (tests == null) {
                throw new ArgumentNullException(nameof(tests));
            }

            if (runContext == null) {
                throw new ArgumentNullException(nameof(runContext));
            }

            if (frameworkHandle == null) {
                throw new ArgumentNullException(nameof(frameworkHandle));
            }

            _cancelRequested.Reset();

            RunTestCases(tests, runContext, frameworkHandle);
        }

        private void RunTestCases(
            IEnumerable<TestCase> tests,
            IRunContext runContext,
            IFrameworkHandle frameworkHandle
        ) {
            bool codeCoverage = CodeCoverage.EnableCodeCoverage(runContext);
            string covPath = null;
            if (codeCoverage) {
                covPath = CodeCoverage.GetCoveragePath(tests);
            }
            // .py file path -> project settings
            var sourceToSettings = RunSettingsUtil.GetSourceToProjSettings(runContext.RunSettings, filterType:TestFrameworkType.UnitTest);

            foreach (var testGroup in tests.GroupBy(x => sourceToSettings[x.CodeFilePath])) {
                if (_cancelRequested.WaitOne(0)) {
                    break;
                }
                
                if (testGroup.Key.TestFramework != TestFrameworkType.UnitTest) {
                    continue;
                }

                using (var runner = new TestRunner(
                    frameworkHandle,
                    runContext,
                    testGroup,
                    covPath,
                    testGroup.Key,
                    _app,
                    _cancelRequested
                )) {
                    runner.Run();
                }
            }

            if (codeCoverage) {
                CodeCoverage.AttachCoverageResults(frameworkHandle, covPath);
            }
        }

        sealed class TestRunner : IDisposable {
            private readonly IFrameworkHandle _frameworkHandle;
            private readonly IRunContext _context;
            private readonly TestCase[] _tests;
            private readonly string _codeCoverageFile;
            private readonly PythonProjectSettings _settings;
            private readonly PythonDebugMode _debugMode;
            private readonly VisualStudioProxy _app;
            private readonly string _searchPaths;
            private readonly Dictionary<string, string> _env;
            private readonly string _debugSecret;
            private readonly int _debugPort;
            private readonly ManualResetEvent _cancelRequested;
            private readonly ManualResetEvent _connected = new ManualResetEvent(false);
            private readonly AutoResetEvent _done = new AutoResetEvent(false);
            private Connection _connection;
            private readonly Socket _socket;
            private readonly StringBuilder _stdOut = new StringBuilder(), _stdErr = new StringBuilder();
            private TestResult _curTestResult;
            private readonly bool _dryRun, _showConsole;

            public TestRunner(
                IFrameworkHandle frameworkHandle,
                IRunContext runContext,
                IEnumerable<TestCase> tests,
                string codeCoverageFile,
                PythonProjectSettings settings,
                VisualStudioProxy app,
                ManualResetEvent cancelRequested) {

                _frameworkHandle = frameworkHandle;
                _context = runContext;
                _tests = tests.ToArray();
                _codeCoverageFile = codeCoverageFile;
                _settings = settings;
                _app = app;
                _cancelRequested = cancelRequested;
                _dryRun = ExecutorService.IsDryRun(runContext.RunSettings);
                _showConsole = ExecutorService.ShouldShowConsole(runContext.RunSettings);

                _env = new Dictionary<string, string>();

                _searchPaths = GetSearchPaths(tests, settings);

                ExecutorService.GetDebugSettings(_app, _context, _settings, out _debugMode, out _debugSecret, out _debugPort);

                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                _socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                _socket.Listen(0);
                _socket.BeginAccept(AcceptConnection, _socket);
            }

            [SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "_connection")]
            public void Dispose() {
                _socket.Dispose();
                _connected.Dispose();
                _done.Dispose();
                _connection?.Dispose();
            }

            private static Task RequestHandler(RequestArgs arg1, Func<Response, Task> arg2) {
                throw new NotImplementedException();
            }

            private void ConnectionReceivedEvent(object sender, EventReceivedEventArgs e) {
                switch (e.Name) {
                    case TP.ResultEvent.Name:
                        var result = (TP.ResultEvent)e.Event;
                        TestOutcome outcome = TestOutcome.None;
                        switch (result.outcome) {
                            case "passed": outcome = TestOutcome.Passed; break;
                            case "failed": outcome = TestOutcome.Failed; break;
                            case "skipped": outcome = TestOutcome.Skipped; break;
                        }

                        RecordEnd(
                            _frameworkHandle,
                            _curTestResult,
                            _stdOut.ToString(),
                            _stdErr.ToString(),
                            outcome,
                            result
                        );

                        _stdOut.Clear();
                        _stdErr.Clear();
                        break;

                    case TP.StartEvent.Name:
                        var start = (TP.StartEvent)e.Event;

                        // Create the TestResult object right away, so that
                        // StartTime is initialized correctly.
                        _curTestResult = null;
                        foreach (var test in GetTestCases()) {
                            if (test.Key == start.test) {
                                _curTestResult = new TestResult(test.Value);
                                break;
                            }
                        }

                        if (_curTestResult != null) {
                            _frameworkHandle.RecordStart(_curTestResult.TestCase);
                        } else {
                            Warning(Strings.Test_UnexpectedResult.FormatUI(start.classname, start.method));
                        }
                        break;
                    case TP.StdErrEvent.Name:
                        var err = (TP.StdErrEvent)e.Event;
                        _stdErr.Append(err.content);
                        break;
                    case TP.StdOutEvent.Name:
                        var outp = (TP.StdOutEvent)e.Event;
                        _stdOut.Append(outp.content);
                        break;
                    case TP.DoneEvent.Name:
                        _done.Set();
                        break;
                }
            }

            private static string GetSearchPaths(IEnumerable<TestCase> tests, PythonProjectSettings settings) {
                var paths = settings.SearchPath.ToList();

                HashSet<string> knownModulePaths = new HashSet<string>();
                foreach (var test in tests) {
                    string testFilePath = PathUtils.GetAbsoluteFilePath(settings.ProjectHome, test.CodeFilePath);
                    var modulePath = ModulePath.FromFullPath(testFilePath);
                    if (knownModulePaths.Add(modulePath.LibraryPath)) {
                        paths.Insert(0, modulePath.LibraryPath);
                    }
                }

                string searchPaths = string.Join(
                    ";",
                    paths.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase)
                );
                return searchPaths;
            }

            private void AcceptConnection(IAsyncResult iar) {
                Socket socket;
                var socketSource = ((Socket)iar.AsyncState);
                try {
                    socket = socketSource.EndAccept(iar);
                } catch (SocketException ex) {
                    Debug.WriteLine("DebugConnectionListener socket failed");
                    Debug.WriteLine(ex);
                    return;
                } catch (ObjectDisposedException) {
                    Debug.WriteLine("DebugConnectionListener socket closed");
                    return;
                }

                var stream = new NetworkStream(socket, ownsSocket: true);
                _connection = new Connection(
                    new MemoryStream(),
                    true,
                    stream,
                    true,
                    RequestHandler,
                    TP.RegisteredTypes,
                    "TestExecutor"
                );
                _connection.EventReceived += ConnectionReceivedEvent;
                Task.Run(_connection.ProcessMessages).DoNotWait();
                _connected.Set();
            }

            public void Run() {
                if (!File.Exists(_settings.InterpreterPath)) {
                    Error(Strings.Test_InterpreterDoesNotExist.FormatUI(_settings.InterpreterPath));
                    return;
                }
                try {
                    ExecutorService.DetachFromSillyManagedProcess(_app, _debugMode);

                    var pythonPath = InitializeEnvironment();

                    string testList = null;
                    // For a small set of tests, we'll pass them on the command
                    // line. Once we exceed a certain (arbitrary) number, create
                    // a test list on disk so that we do not overflow the 
                    // 32K argument limit.
                    if (_tests.Length > 5) {
                        testList = TestUtils.CreateTestListFile(GetTestCases().Select(pair => pair.Key));
                    }
                    var arguments = GetArguments(testList);

                    ////////////////////////////////////////////////////////////
                    // Do the test run
                    _connected.Reset();
                    using (var proc = ProcessOutput.Run(
                        _settings.InterpreterPath,
                        arguments,
                        _settings.WorkingDirectory,
                        _env,
                        _showConsole,
                        null
                    )) {
                        bool killed = false;

                        Info("cd " + _settings.WorkingDirectory);
                        Info("set " + pythonPath.Key + "=" + pythonPath.Value);
                        Info(proc.Arguments);

                        // If there's an error in the launcher script,
                        // it will terminate without connecting back.
                        WaitHandle.WaitAny(new WaitHandle[] { _connected, proc.WaitHandle });
                        bool processConnected = _connected.WaitOne(1);

                        if (!processConnected && proc.ExitCode.HasValue) {
                            // Process has already exited
                            proc.Wait();
                            Error(Strings.Test_FailedToStartExited);
                            if (proc.StandardErrorLines.Any()) {
                                foreach (var line in proc.StandardErrorLines) {
                                    Error(line);
                                }
                            }

                            foreach (var test in GetTestCases()) {
                                _frameworkHandle.RecordStart(test.Value);
                                _frameworkHandle.RecordResult(new TestResult(test.Value) {
                                    Outcome = TestOutcome.Skipped,
                                    ErrorMessage = Strings.Test_NotRun
                                });
                            }

                            killed = true;
                        }

                        if (!killed && _debugMode != PythonDebugMode.None) {
                            try {
                                ExecutorService.AttachDebugger(_app, proc, _debugMode, _debugSecret, _debugPort);
                            } catch (COMException ex) {
                                Error(Strings.Test_ErrorConnecting);
                                DebugError(ex.ToString());
                                try {
                                    proc.Kill();
                                } catch (InvalidOperationException) {
                                    // Process has already exited
                                }
                                killed = true;
                            }
                        }


                        // https://pytools.codeplex.com/workitem/2290
                        // Check that proc.WaitHandle was not null to avoid crashing if
                        // a test fails to start running. We will report failure and
                        // send the error message from stdout/stderr.
                        var handles = new WaitHandle[] { _cancelRequested, proc.WaitHandle, _done };
                        if (handles[1] == null) {
                            killed = true;
                        }

                        if (!killed) {
                            switch (WaitHandle.WaitAny(handles)) {
                                case 0:
                                    // We've been cancelled
                                    try {
                                        proc.Kill();
                                    } catch (InvalidOperationException) {
                                        // Process has already exited
                                    }
                                    killed = true;
                                    break;
                                case 1:
                                    // The process has exited, give a chance for our comm channel
                                    // to be flushed...
                                    handles = new WaitHandle[] { _cancelRequested, _done };
                                    if (WaitHandle.WaitAny(handles, 10000) != 1) {
                                        Warning(Strings.Test_NoTestFinishedNotification);
                                    }
                                    break;
                                case 2:
                                    // We received the done event
                                    break;
                            }
                        }
                    }
                    if (File.Exists(testList)) {
                        try {
                            File.Delete(testList);
                        } catch (IOException) {
                        }
                    }
                } catch (Exception e) {
                    Error(e.ToString());
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

            private void Info(string message) {
                _frameworkHandle.SendMessage(TestMessageLevel.Informational, message);
            }


            private void Error(string message) {
                _frameworkHandle.SendMessage(TestMessageLevel.Error, message);
            }

            private void Warning(string message) {
                _frameworkHandle.SendMessage(TestMessageLevel.Warning, message);
            }

            private KeyValuePair<string, string> InitializeEnvironment() {
                var pythonPathVar = _settings.PathEnv;
                var pythonPath = _searchPaths;
                if (!string.IsNullOrWhiteSpace(pythonPathVar)) {
                    _env[pythonPathVar] = pythonPath;
                }

                foreach (var envVar in _settings.Environment) {
                    _env[envVar.Key] = envVar.Value;
                }
                return new KeyValuePair<string, string>(pythonPathVar, pythonPath);
            }

            private IEnumerable<KeyValuePair<string, TestCase>> GetTestCases() {
                var moduleCache = new Dictionary<string, ModulePath>();

                foreach (var test in _tests) {
                    string testFile, testClass, testMethod;
                    TestReader.ParseFullyQualifiedTestName(
                        test.FullyQualifiedName,
                        out testFile,
                        out testClass,
                        out testMethod
                    );

                    ModulePath module;
                    if (!moduleCache.TryGetValue(testFile, out module)) {
                        string testFilePath = PathUtils.GetAbsoluteFilePath(_settings.ProjectHome, testFile);
                        moduleCache[testFile] = module = ModulePath.FromFullPath(testFilePath);
                    }

                    yield return new KeyValuePair<string, TestCase>("{0}.{1}.{2}".FormatInvariant(
                        module.ModuleName,
                        testClass,
                        testMethod
                    ), test);
                }
            }

            private string[] GetArguments(string testList = null) {
                var arguments = new List<string>();
                arguments.Add(TestLauncherPath);

                if (string.IsNullOrEmpty(testList)) {
                    foreach (var test in GetTestCases()) {
                        arguments.Add("-t");
                        arguments.Add(test.Key);
                    }
                } else {
                    arguments.Add("--test-list");
                    arguments.Add(testList);
                }

                if (_dryRun) {
                    arguments.Add("--dry-run");
                }

                if (_codeCoverageFile != null) {
                    arguments.Add("--coverage");
                    arguments.Add(_codeCoverageFile);
                }

                if (_debugMode == PythonDebugMode.PythonOnly) {
                    arguments.Add("-p");
                    arguments.Add(_debugPort.ToString());

                    if (_settings.UseLegacyDebugger) {
                        arguments.Add("-s");
                        arguments.Add(_debugSecret);
                    }

                } else if (_debugMode == PythonDebugMode.PythonAndNative) {
                    arguments.Add("-x");
                }

                arguments.Add("-r");
                arguments.Add(((IPEndPoint)_socket.LocalEndPoint).Port.ToString());
                return arguments.ToArray();
            }
        }

        private static void RecordEnd(IFrameworkHandle frameworkHandle, TestResult result, string stdout, string stderr, TestOutcome outcome, TP.ResultEvent resultInfo) {
            result.EndTime = DateTimeOffset.Now;
            result.Duration = TimeSpan.FromSeconds(resultInfo.durationInSecs);
            result.Outcome = outcome;

            // Replace \n with \r\n to be more friendly when copying output...
            stdout = stdout.Replace("\r\n", "\n").Replace("\n", "\r\n");
            stderr = stderr.Replace("\r\n", "\n").Replace("\n", "\r\n");

            result.Messages.Add(new TestResultMessage(TestResultMessage.StandardOutCategory, stdout));
            result.Messages.Add(new TestResultMessage(TestResultMessage.StandardErrorCategory, stderr));
            result.Messages.Add(new TestResultMessage(TestResultMessage.AdditionalInfoCategory, stderr));
            if (resultInfo.traceback != null) {
                result.ErrorStackTrace = resultInfo.traceback;
                result.Messages.Add(new TestResultMessage(TestResultMessage.DebugTraceCategory, resultInfo.traceback));
            }
            if (resultInfo.message != null) {
                result.ErrorMessage = resultInfo.message;
            }

            frameworkHandle.RecordResult(result);
            frameworkHandle.RecordEnd(result.TestCase, outcome);
        }
    }
}

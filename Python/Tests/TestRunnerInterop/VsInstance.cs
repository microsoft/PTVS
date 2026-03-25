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
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.SafeHandles;

namespace TestRunnerInterop {
    public sealed class VsInstance : IDisposable {
        private const int VsOutputTailLimit = 200;
        private static readonly TimeSpan DteStartupTimeout = GetDteStartupTimeout();
        private static readonly TimeSpan DteResponsivenessTimeout = TimeSpan.FromSeconds(30);

        private readonly object _lock = new object();
        private readonly object _processOutputLock = new object();
        private readonly SafeFileHandle _jobObject;
        private readonly Queue<string> _vsOutputTail = new Queue<string>();
        private string _activityLogPath;
        private Process _vs;
        private VisualStudioApp _app;
        private Queue<string> _recentProcessOutput;

        private bool _isDisposed = false;

        private string _currentSettings;

        private void WriteProcessOutput(string data, bool isError) {
            if (data == null) {
                return;
            }

            lock (_processOutputLock) {
                if (_recentProcessOutput == null) {
                    _recentProcessOutput = new Queue<string>();
                }

                _recentProcessOutput.Enqueue((isError ? "[stderr] " : "[stdout] ") + data);
                while (_recentProcessOutput.Count > 50) {
                    _recentProcessOutput.Dequeue();
                }
            }

            if (isError) {
                Console.Error.WriteLine(data);
                Console.Error.Flush();
            } else {
                Console.WriteLine(data);
                Console.Out.Flush();
            }
        }

        private static void FlushConsoleStreams() {
            Console.Out.Flush();
            Console.Error.Flush();
        }

        private string GetRecentProcessOutputTail() {
            lock (_processOutputLock) {
                if (_recentProcessOutput == null || _recentProcessOutput.Count == 0) {
                    return "<no devenv output captured>";
                }

                return string.Join(Environment.NewLine, _recentProcessOutput.ToArray());
            }
        }

        public VsInstance() {
            _jobObject = NativeMethods.CreateJobObject(IntPtr.Zero, null);
            if (_jobObject.IsInvalid) {
                throw new InvalidOperationException("Failed to create job object");
            }

            var objInfo = new NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            objInfo.BasicLimitInformation.LimitFlags = NativeMethods.JOB_OBJECT_LIMIT.KILL_ON_JOB_CLOSE;
            if (!NativeMethods.SetInformationJobObject(
                _jobObject,
                NativeMethods.JOBOBJECTINFOCLASS.JobObjectExtendedLimitInformation,
                ref objInfo,
                Marshal.SizeOf(objInfo)
            )) {
                _jobObject.Dispose();
                throw new InvalidOperationException("Failed to set job info");
            }
        }

        public void Restart() {
            if (string.IsNullOrEmpty(_currentSettings)) {
                throw new InvalidOperationException("No current settings to restart VS with");
            }
            var args = _currentSettings.Split(';');
            if (args.Length != 4) {
                throw new InvalidOperationException($"Current settings are not valid: {_currentSettings}");
            }

            StartOrRestart(args[0], args[1], args[2], args[3]);
        }

        public void StartOrRestart(
            string devenvExe,
            string devenvArguments,
            string testDataRoot,
            string tempRoot
        ) {
            lock (_lock) {
                if (string.IsNullOrWhiteSpace(devenvExe)) {
                    throw new InvalidOperationException(
                        $"Unable to resolve devenv.exe from VisualStudio.InstallationUnderTest.Path='{Environment.GetEnvironmentVariable("VisualStudio.InstallationUnderTest.Path") ?? "<unset>"}'."
                    );
                }

                var settings = $"{devenvExe ?? ""};{devenvArguments ?? ""};{testDataRoot ?? ""};{tempRoot ?? ""}";
                if (_vs != null && _app != null) {
                    if (_currentSettings == settings) {
                        return;
                    }
                    Console.WriteLine("Restarting VS because settings have changed");
                }
                _currentSettings = settings;
                CloseCurrentInstance();
                lock (_processOutputLock) {
                    _recentProcessOutput = new Queue<string>();
                }

                var psi = new ProcessStartInfo {
                    FileName = devenvExe,
                    Arguments = AppendDevenvLogArgument(devenvArguments, tempRoot),
                    ErrorDialog = false,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                psi.Environment["_PTVS_UI_TEST"] = "1";
                if (!string.IsNullOrEmpty(testDataRoot)) {
                    psi.Environment["_TESTDATA_ROOT_PATH"] = testDataRoot;
                }
                if (!string.IsNullOrEmpty(tempRoot)) {
                    psi.Environment["_TESTDATA_TEMP_PATH"] = tempRoot;
                }

                Console.WriteLine($"Starting VS: '{devenvExe}' {devenvArguments}");
                Console.WriteLine($"  testDataRoot: '{testDataRoot}'");
                Console.WriteLine($"  tempRoot: '{tempRoot}'");
                Console.WriteLine($"  env:_TESTDATA_ROOT_PATH='{Environment.GetEnvironmentVariable("_TESTDATA_ROOT_PATH") ?? "<unset>"}'");
                Console.WriteLine($"  env:VisualStudio.InstallationUnderTest.Path='{Environment.GetEnvironmentVariable("VisualStudio.InstallationUnderTest.Path") ?? "<unset>"}'");

                ApplySkipVerification(testDataRoot);

                _vs = Process.Start(psi);
                ClearVsOutputTail();
                if (!NativeMethods.AssignProcessToJobObject(_jobObject, _vs.Handle)) {
                    try {
                        _vs.Kill();
                    } catch (Exception) {
                    }
                    _vs.Dispose();
                    throw new InvalidOperationException("Failed to add VS to our job object");
                }

                // Forward console output to our own output, which will
                // be captured by the test runner.
                _vs.OutputDataReceived += (s, e) => LogVsOutput(e.Data, isError: false);
                _vs.ErrorDataReceived += (s, e) => LogVsOutput(e.Data, isError: true);
                _vs.BeginOutputReadLine();
                _vs.BeginErrorReadLine();

                // Always allow at least five seconds to start
                Thread.Sleep(5000);
                if (_vs.HasExited) {
                    throw CreateVsStartupException("VS exited during startup");
                }
                _app = VisualStudioApp.FromProcessId(_vs.Id);

                var dte = WaitForDte(DteStartupTimeout);
                if (dte == null) {
                    throw CreateVsStartupException($"Failed to obtain DTE within {DteStartupTimeout}");
                }

                AttachIfDebugging(_vs);
            }
        }

        private static bool _skipVerificationApplied;

        private static void ApplySkipVerification(string testDataRoot) {
            if (_skipVerificationApplied || string.IsNullOrWhiteSpace(testDataRoot)) {
                Console.WriteLine($"ApplySkipVerification skipped. alreadyApplied={_skipVerificationApplied}, testDataRoot='{testDataRoot ?? "<null>"}'");
                return;
            }
            _skipVerificationApplied = true;

            Console.WriteLine($"ApplySkipVerification using testDataRoot='{testDataRoot}'");
            var nestedRegFile = Path.Combine(testDataRoot, "TestData", "EnableSkipVerification.reg");
            var flatRegFile = Path.Combine(testDataRoot, "EnableSkipVerification.reg");
            Console.WriteLine($"  nested candidate: '{nestedRegFile}' (exists={File.Exists(nestedRegFile)})");
            Console.WriteLine($"  flat candidate: '{flatRegFile}' (exists={File.Exists(flatRegFile)})");

            var regFile = nestedRegFile;
            if (!File.Exists(regFile)) {
                // Also check directly under testDataRoot (flat layout)
                regFile = flatRegFile;
            }

            if (!File.Exists(regFile)) {
                Console.WriteLine($"EnableSkipVerification.reg not found under '{testDataRoot}', skipping.");
                return;
            }

            Console.WriteLine($"Applying strong-name skip verification: {regFile}");
            try {
                var proc = Process.Start(new ProcessStartInfo {
                    FileName = "reg.exe",
                    Arguments = $"import \"{regFile}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                });
                proc.WaitForExit(15000);
                var stdout = proc.StandardOutput.ReadToEnd();
                var stderr = proc.StandardError.ReadToEnd();
                Console.WriteLine($"reg.exe exit code: {proc.ExitCode}");
                if (!string.IsNullOrWhiteSpace(stdout)) Console.WriteLine($"  stdout: {stdout.Trim()}");
                if (!string.IsNullOrWhiteSpace(stderr)) Console.WriteLine($"  stderr: {stderr.Trim()}");
            } catch (Exception ex) {
                Console.WriteLine($"Failed to apply EnableSkipVerification.reg: {ex.Message}");
            }
        }

        private string AppendDevenvLogArgument(string devenvArguments, string tempRoot) {
            if (string.IsNullOrWhiteSpace(tempRoot)) {
                _activityLogPath = null;
                return devenvArguments;
            }

            Directory.CreateDirectory(tempRoot);
            _activityLogPath = Path.Combine(tempRoot, "ActivityLog.xml");
            try {
                if (File.Exists(_activityLogPath)) {
                    File.Delete(_activityLogPath);
                }
            } catch (Exception ex) {
                Console.WriteLine($"Failed to reset ActivityLog '{_activityLogPath}': {ex.Message}");
            }

            var logArgument = $"/log \"{_activityLogPath}\"";
            if (string.IsNullOrWhiteSpace(devenvArguments)) {
                return logArgument;
            }

            if (devenvArguments.IndexOf("/log", StringComparison.OrdinalIgnoreCase) >= 0) {
                return devenvArguments;
            }

            return $"{devenvArguments} {logArgument}";
        }

        private void CloseCurrentInstance(bool hard = false) {
            lock (_lock) {
                if (_vs != null) {
                    try {
                        if (hard) {
                            _vs.Kill();
                        } else {
                            if (!_vs.CloseMainWindow()) {
                                try {
                                    _vs.Kill();
                                } catch (Exception) {
                                }
                            }
                            if (!_vs.WaitForExit(10000)) {
                                try {
                                    _vs.Kill();
                                } catch (Exception) {
                                }
                            }
                        }
                    } catch (ObjectDisposedException) {
                    } catch (InvalidOperationException) {
                    }
                    _vs.Dispose();
                    _vs = null;
                }
                _app = null;
            }
        }

        public bool IsRunning {
            get {
                if (_isDisposed || _vs == null || _vs.HasExited || _app == null) {
                    return false;
                }

                try {
                    _app.GetDTE();
                } catch (InvalidOperationException) {
                    return false;
                } catch (InvalidComObjectException) {
                    return false;
                } catch (COMException) {
                    return false;
                }

                return true;
            }
        }

        private static void AttachIfDebugging(Process targetVs) {
            if (!Debugger.IsAttached) {
                return;
            }

            // We are debugging tests, so attach the debugger to VS
            var selfId = Process.GetCurrentProcess().Id;

            foreach (var p in Process.GetProcessesByName("devenv")) {
                if (p.Id == targetVs.Id) {
                    continue;
                }

                using (var vs = VisualStudioApp.FromProcessId(p.Id)) {
                    EnvDTE.DTE dte;
                    try {
                        dte = vs.GetDTE();
                    } catch (InvalidOperationException) {
                        // DTE is not available, which means VS has not been running
                        continue;
                    }

                    if (dte.Debugger.CurrentMode == EnvDTE.dbgDebugMode.dbgDesignMode) {
                        // Not the correct VS
                        continue;
                    }

                    foreach (EnvDTE.Process dp in dte.Debugger.DebuggedProcesses) {
                        if (dp.ProcessID == selfId) {
                            // This is the correct VS, so attach and return.

                            vs.AttachToProcess(targetVs, null);
                            return;
                        }
                    }
                }
            }

        }

        public bool RunTest(string container, string name, TimeSpan timeout, object[] arguments, bool allowRetry) {
            if (_isDisposed) {
                throw new ObjectDisposedException(GetType().Name);
            }

            bool timedOut = false;
            CancellationTokenSource cts = null;
            var startTime = DateTime.UtcNow;

            if (!Debugger.IsAttached && timeout < TimeSpan.MaxValue) {
                cts = new CancellationTokenSource();
                Task.Delay(timeout, cts.Token).ContinueWith(t => {
                    timedOut = true;
                    Console.WriteLine($"Terminating {container}.{name}() after {DateTime.UtcNow - startTime}");
                    // Terminate VS to unblock the Execute() call below
                    CloseCurrentInstance(hard: true);
                }, TaskContinuationOptions.OnlyOnRanToCompletion);
            }

            try {
                dynamic r = null;
                for (int attempt = 0; attempt < 2; attempt++) {
                    var dte = WaitForResponsiveDte(DteResponsivenessTimeout);
                    if (dte == null) {
                        throw CreateVsStartupException($"VS did not expose a responsive DTE before running {container}.{name}()");
                    }

                    try {
                        var containerObj = dte.GetObject(container) as dynamic;
                        if (containerObj == null) {
                            throw new InvalidOperationException($"DTE.GetObject('{container}') returned null.");
                        }

                        r = containerObj.Execute(name, arguments);
                        break;
                    } catch (COMException ex) when (attempt == 0 && IsProcessAlive() && IsTransientRpcFailure(ex)) {
                        Console.WriteLine($"Transient COM failure executing {container}.{name}() (0x{ex.ErrorCode:X8}). Retrying once.");
                        Thread.Sleep(1000);
                    }
                }

                if (!r.IsSuccess) {
                    if (r.ExceptionType == "Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException") {
                        throw new AssertInconclusiveException(r.ExceptionMessage);
                    }
                    throw new TestFailedException(
                        r.ExceptionType,
                        r.ExceptionMessage,
                        r.ExceptionTraceback
                    );
                }
                return true;
            } catch (InvalidComObjectException ex) {
                Console.WriteLine(ex);
                LogVsFailureState(container, name);
                CloseCurrentInstance();
                if (!allowRetry) {
                    ExceptionDispatchInfo.Capture(ex).Throw();
                }
            } catch (COMException ex) {
                Console.WriteLine(ex);
                LogVsFailureState(container, name);
                CloseCurrentInstance();
                if (timedOut) {
                    throw new TimeoutException($"Terminating {container}.{name}() after {DateTime.UtcNow - startTime}", ex);
                }
                if (!allowRetry) {
                    ExceptionDispatchInfo.Capture(ex).Throw();
                }
            } catch (ThreadAbortException ex) {
                Console.WriteLine(ex);
                LogVsFailureState(container, name);
                CloseCurrentInstance(hard: true);
                ExceptionDispatchInfo.Capture(ex).Throw();
            } catch (Exception ex) {
                Console.WriteLine(ex);
                LogVsFailureState(container, name);
                CloseCurrentInstance();
                ExceptionDispatchInfo.Capture(ex).Throw();
            } finally {
                if (cts != null) {
                    cts.Cancel();
                    cts.Dispose();
                }
            }
            return false;

        }

        private void ClearVsOutputTail() {
            lock (_vsOutputTail) {
                _vsOutputTail.Clear();
            }
        }

        private void LogVsOutput(string data, bool isError) {
            if (data == null) {
                return;
            }

            lock (_vsOutputTail) {
                _vsOutputTail.Enqueue($"[{(isError ? "stderr" : "stdout")}] {data}");
                while (_vsOutputTail.Count > VsOutputTailLimit) {
                    _vsOutputTail.Dequeue();
                }
            }

            if (isError) {
                Console.Error.WriteLine(data);
            } else {
                Console.WriteLine(data);
            }
        }

        private string GetVsOutputTail() {
            lock (_vsOutputTail) {
                return _vsOutputTail.Count == 0 ? "<no output captured>" : string.Join(Environment.NewLine, _vsOutputTail.ToArray());
            }
        }

        private bool IsProcessAlive() {
            try {
                return _vs != null && !_vs.HasExited;
            } catch (InvalidOperationException) {
                return false;
            }
        }

        private static TimeSpan GetDteStartupTimeout() {
            const int defaultSeconds = 240;
            const string envVar = "PTVS_DTE_STARTUP_TIMEOUT_SECONDS";

            var rawValue = Environment.GetEnvironmentVariable(envVar);
            if (string.IsNullOrWhiteSpace(rawValue)) {
                return TimeSpan.FromSeconds(defaultSeconds);
            }

            if (int.TryParse(rawValue, out var seconds) && seconds > 0) {
                return TimeSpan.FromSeconds(seconds);
            }

            Console.WriteLine($"Ignoring invalid {envVar} value '{rawValue}'. Using default timeout of {defaultSeconds} seconds.");
            return TimeSpan.FromSeconds(defaultSeconds);
        }

        private EnvDTE.DTE WaitForDte(TimeSpan timeout) {
            Console.WriteLine($"Waiting for DTE (timeout={timeout}). Expected ROT moniker: {_app?.ExpectedDteMoniker ?? "<no app>"}");
            var stopAt = DateTime.UtcNow + timeout;
            var lastRotDump = DateTime.MinValue;
            while (DateTime.UtcNow < stopAt) {
                if (!IsProcessAlive() || _app == null) {
                    Console.WriteLine("WaitForDte: process died or app is null.");
                    return null;
                }

                try {
                    var dte = _app.GetDTE();
                    Console.WriteLine("WaitForDte: DTE obtained successfully.");
                    return dte;
                } catch (InvalidOperationException) {
                } catch (InvalidComObjectException) {
                } catch (COMException) {
                }

                // Periodically dump ROT entries to diagnose version mismatches
                if (DateTime.UtcNow - lastRotDump > TimeSpan.FromSeconds(30)) {
                    lastRotDump = DateTime.UtcNow;
                    try {
                        var rotEntries = VisualStudioApp.EnumerateRunningObjectTable();
                        var dteEntries = rotEntries.FindAll(e => e.IndexOf("DTE", StringComparison.OrdinalIgnoreCase) >= 0
                            || e.IndexOf("VisualStudio", StringComparison.OrdinalIgnoreCase) >= 0);
                        Console.WriteLine($"ROT entries containing DTE/VisualStudio ({dteEntries.Count} of {rotEntries.Count} total):");
                        foreach (var entry in dteEntries) {
                            Console.WriteLine($"  {entry}");
                        }
                        if (dteEntries.Count == 0) {
                            Console.WriteLine("  <none>");
                        }
                    } catch (Exception ex) {
                        Console.WriteLine($"ROT enumeration failed: {ex.Message}");
                    }
                }

                Thread.Sleep(1000);
            }

            return null;
        }

        private EnvDTE.DTE WaitForResponsiveDte(TimeSpan timeout) {
            var stopAt = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < stopAt) {
                if (!IsProcessAlive() || _app == null) {
                    return null;
                }

                try {
                    var dte = _app.GetDTE();
                    var mainWindow = dte.MainWindow;
                    var hwnd = mainWindow.HWnd;
                    var name = dte.Name;
                    return dte;
                } catch (InvalidOperationException) {
                } catch (InvalidComObjectException) {
                } catch (COMException) {
                }

                Thread.Sleep(1000);
            }

            return null;
        }

        private InvalidOperationException CreateVsStartupException(string message) {
            string processState;
            try {
                processState = _vs == null
                    ? "<no process>"
                    : _vs.HasExited
                        ? $"exited with code {_vs.ExitCode}"
                        : $"running (pid {_vs.Id})";
            } catch (InvalidOperationException) {
                processState = "<process state unavailable>";
            }

            return new InvalidOperationException(
                $"{message}. Process state: {processState}.{Environment.NewLine}Recent devenv output:{Environment.NewLine}{GetVsOutputTail()}{Environment.NewLine}Activity log path: {GetActivityLogPath()}"
            );
        }

        private void LogVsFailureState(string container, string name) {
            string processState;
            try {
                processState = _vs == null
                    ? "<no process>"
                    : _vs.HasExited
                        ? $"exited with code {_vs.ExitCode}"
                        : $"running (pid {_vs.Id})";
            } catch (InvalidOperationException) {
                processState = "<process state unavailable>";
            }

            Console.WriteLine($"VS failure while running {container}.{name}(). Process state: {processState}");
            Console.WriteLine($"Recent devenv output:{Environment.NewLine}{GetVsOutputTail()}");
        }

        private static bool IsTransientRpcFailure(COMException ex) {
            return ex.ErrorCode == unchecked((int)0x800706BE)
                || ex.ErrorCode == unchecked((int)0x80010001)
                || ex.ErrorCode == unchecked((int)0x8001010A);
        }

        private string GetActivityLogPath() {
            return string.IsNullOrWhiteSpace(_activityLogPath)
                ? "<activity log not configured>"
                : _activityLogPath;
        }

        void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    // TODO: dispose managed state (managed objects).
                }

                _jobObject.Dispose();
                CloseCurrentInstance();

                _isDisposed = true;
            }
        }

        ~VsInstance() {
            Dispose(false);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
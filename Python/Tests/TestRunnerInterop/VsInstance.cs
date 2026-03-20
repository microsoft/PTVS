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
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.SafeHandles;

namespace TestRunnerInterop {
    public sealed class VsInstance : IDisposable {
        private const int DteAvailabilityTimeoutSeconds = 120;
        private const int DteProbeDelayMilliseconds = 250;

        private readonly object _lock = new object();
        private readonly SafeFileHandle _jobObject;
        private Process _vs;
        private VisualStudioApp _app;

        private bool _isDisposed = false;

        private string _currentSettings;

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
                var settings = $"{devenvExe ?? ""};{devenvArguments ?? ""};{testDataRoot ?? ""};{tempRoot ?? ""}";
                if (_vs != null && _app != null) {
                    if (_currentSettings == settings) {
                        return;
                    }
                    Console.WriteLine("Restarting VS because settings have changed");
                }
                _currentSettings = settings;
                CloseCurrentInstance();

                if (string.IsNullOrWhiteSpace(devenvExe)) {
                    throw new InvalidOperationException(
                        "Cannot start Visual Studio because devenv executable path is empty. "
                        + "CurrentDirectory=" + Environment.CurrentDirectory + "; "
                        + "VisualStudio_IDE=" + (Environment.GetEnvironmentVariable("VisualStudio_IDE") ?? "<null>") + "; "
                        + "VSAPPIDDIR=" + (Environment.GetEnvironmentVariable("VSAPPIDDIR") ?? "<null>") + "; "
                        + "DevEnvDir=" + (Environment.GetEnvironmentVariable("DevEnvDir") ?? "<null>") + "; "
                        + "VSINSTALLDIR=" + (Environment.GetEnvironmentVariable("VSINSTALLDIR") ?? "<null>")
                    );
                }

                if (!System.IO.File.Exists(devenvExe)) {
                    throw new InvalidOperationException(
                        "Cannot start Visual Studio because devenv executable path does not exist. "
                        + "devenvExe=" + devenvExe + "; "
                        + "devenvArguments=" + (devenvArguments ?? "<null>") + "; "
                        + "testDataRoot=" + (testDataRoot ?? "<null>") + "; "
                        + "tempRoot=" + (tempRoot ?? "<null>")
                    );
                }

                var outputTail = new Queue<string>();
                void AddOutputLine(string line) {
                    const int maxLines = 200;
                    if (string.IsNullOrWhiteSpace(line)) {
                        return;
                    }

                    outputTail.Enqueue(line);
                    while (outputTail.Count > maxLines) {
                        outputTail.Dequeue();
                    }
                }

                string lastDteLookupFailure = null;

                string BuildDevenvProcessSnapshot() {
                    Process[] processes;
                    try {
                        processes = Process.GetProcessesByName("devenv");
                    } catch (Exception ex) {
                        return "<failed to enumerate devenv processes: " + ex.GetType().Name + ": " + ex.Message + ">";
                    }

                    if (processes.Length == 0) {
                        return "<none>";
                    }

                    var snapshot = new StringBuilder();
                    foreach (var process in processes) {
                        using (process) {
                            if (snapshot.Length > 0) {
                                snapshot.Append(" | ");
                            }

                            snapshot.Append("pid=").Append(process.Id);

                            try {
                                snapshot.Append(", sessionId=").Append(process.SessionId);
                            } catch (Exception ex) {
                                snapshot.Append(", sessionIdError=").Append(ex.GetType().Name);
                            }

                            try {
                                snapshot.Append(", started=").Append(process.StartTime.ToString("o"));
                            } catch (Exception ex) {
                                snapshot.Append(", startedError=").Append(ex.GetType().Name);
                            }

                            try {
                                snapshot.Append(", responding=").Append(process.Responding);
                            } catch (Exception ex) {
                                snapshot.Append(", respondingError=").Append(ex.GetType().Name);
                            }

                            try {
                                snapshot.Append(", mainWindowHandle=0x").Append(process.MainWindowHandle.ToInt64().ToString("X"));
                            } catch (Exception ex) {
                                snapshot.Append(", mainWindowHandleError=").Append(ex.GetType().Name);
                            }

                            try {
                                snapshot.Append(", title=").Append(process.MainWindowTitle ?? string.Empty);
                            } catch (Exception ex) {
                                snapshot.Append(", titleError=").Append(ex.GetType().Name);
                            }

                            if (_vs != null && process.Id == _vs.Id) {
                                snapshot.Append(", launchedByTest=true");
                            }
                        }
                    }

                    return snapshot.ToString();
                }

                string BuildLaunchFailureDetails(string reason) {
                    var details = new StringBuilder();
                    details.Append(reason)
                        .Append("; devenvExe=").Append(devenvExe)
                        .Append("; devenvArguments=").Append(devenvArguments ?? "<null>")
                        .Append("; dteTimeoutSeconds=").Append(DteAvailabilityTimeoutSeconds)
                        .Append("; testDataRoot=").Append(testDataRoot ?? "<null>")
                        .Append("; tempRoot=").Append(tempRoot ?? "<null>")
                        .Append("; VisualStudio.InstallationUnderTest.Path=")
                        .Append(Environment.GetEnvironmentVariable("VisualStudio.InstallationUnderTest.Path") ?? "<null>")
                        .Append("; VisualStudio_IDE=")
                        .Append(Environment.GetEnvironmentVariable("VisualStudio_IDE") ?? "<null>")
                        .Append("; VSAPPIDDIR=")
                        .Append(Environment.GetEnvironmentVariable("VSAPPIDDIR") ?? "<null>")
                        .Append("; DevEnvDir=")
                        .Append(Environment.GetEnvironmentVariable("DevEnvDir") ?? "<null>")
                        .Append("; VSINSTALLDIR=")
                        .Append(Environment.GetEnvironmentVariable("VSINSTALLDIR") ?? "<null>");

                    if (!string.IsNullOrEmpty(lastDteLookupFailure)) {
                        details.Append("; lastDteLookupFailure=")
                            .Append(lastDteLookupFailure);
                    }

                    if (_vs != null) {
                        try {
                            details.Append("; pid=").Append(_vs.Id);
                        } catch (InvalidOperationException) {
                        }

                        try {
                            if (_vs.HasExited) {
                                details.Append("; exitCode=").Append(_vs.ExitCode);
                            }
                        } catch (InvalidOperationException) {
                        }
                    }

                    details.Append("; devenvProcesses=")
                        .Append(BuildDevenvProcessSnapshot());

                    if (outputTail.Count > 0) {
                        details.Append("; outputTail=")
                            .Append(string.Join("\\n", outputTail));
                    }

                    return details.ToString();
                }

                var psi = new ProcessStartInfo {
                    FileName = devenvExe,
                    Arguments = devenvArguments,
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

                try {
                    _vs = Process.Start(psi);
                } catch (Exception ex) {
                    throw new InvalidOperationException(
                        BuildLaunchFailureDetails("Failed to create VS process: " + ex.GetType().Name + ": " + ex.Message),
                        ex
                    );
                }

                if (_vs == null) {
                    throw new InvalidOperationException(BuildLaunchFailureDetails("Failed to create VS process"));
                }

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
                _vs.OutputDataReceived += (s, e) => {
                    if (e.Data != null) {
                        AddOutputLine("OUT: " + e.Data);
                        Console.WriteLine(e.Data);
                    }
                };
                _vs.ErrorDataReceived += (s, e) => {
                    if (e.Data != null) {
                        AddOutputLine("ERR: " + e.Data);
                        Console.Error.WriteLine(e.Data);
                    }
                };
                _vs.BeginOutputReadLine();
                _vs.BeginErrorReadLine();

                _app = VisualStudioApp.FromProcessId(_vs.Id);

                var stopAt = DateTime.Now.AddSeconds(DteAvailabilityTimeoutSeconds);
                EnvDTE.DTE dte = null;
                while (DateTime.Now < stopAt && dte == null) {
                    _vs.Refresh();
                    if (_vs.HasExited) {
                        throw new InvalidOperationException(BuildLaunchFailureDetails("Failed to start VS: process exited before DTE was available"));
                    }

                    try {
                        _vs.WaitForInputIdle(DteProbeDelayMilliseconds);
                    } catch (InvalidOperationException) {
                        // Process may not have created its UI thread yet; keep probing.
                    }

                    try {
                        dte = _app.GetDTE();
                        lastDteLookupFailure = null;
                    } catch (InvalidOperationException ex) {
                        lastDteLookupFailure = ex.Message;
                        Thread.Sleep(DteProbeDelayMilliseconds);
                    } catch (COMException ex) {
                        lastDteLookupFailure = ex.Message;
                        Thread.Sleep(DteProbeDelayMilliseconds);
                    }
                }
                if (dte == null) {
                    throw new InvalidOperationException(BuildLaunchFailureDetails("Failed to start VS: DTE did not become available in time"));
                }

                AttachIfDebugging(_vs);
            }
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

            var dte = _app.GetDTE();
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
                var containerObj = dte.GetObject(container) as dynamic;
                var r = containerObj.Execute(name, arguments);
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
                CloseCurrentInstance();
                if (!allowRetry) {
                    ExceptionDispatchInfo.Capture(ex).Throw();
                }
            } catch (COMException ex) {
                Console.WriteLine(ex);
                CloseCurrentInstance();
                if (timedOut) {
                    throw new TimeoutException($"Terminating {container}.{name}() after {DateTime.UtcNow - startTime}", ex);
                }
                if (!allowRetry) {
                    ExceptionDispatchInfo.Capture(ex).Throw();
                }
            } catch (ThreadAbortException ex) {
                Console.WriteLine(ex);
                CloseCurrentInstance(hard: true);
                ExceptionDispatchInfo.Capture(ex).Throw();
            } catch (Exception ex) {
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

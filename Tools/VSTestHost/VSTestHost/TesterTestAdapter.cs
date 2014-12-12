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

#if SUPPORT_TESTER

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.Common;
using Microsoft.VisualStudio.TestTools.Execution;
using Microsoft.VisualStudio.TestTools.TestAdapter;
using Microsoft.VisualStudioTools.VSTestHost.Internal;

namespace Microsoft.VisualStudioTools.VSTestHost {
    /// <summary>
    /// Executes tests attributed with HostType("VSTestHost").
    /// 
    /// This class is instantiated by the EXECUTION ENGINE and communicates with
    /// the TESTEE via <see cref="TesteeTestAdapter"/> over IPC.
    /// </summary>
    class TesterTestAdapter : ITestAdapter {
        private Internal.VisualStudio _ide;
        private Guid _runId;
        private IRunContext _runContext;
        private TesteeTestAdapter _remote;
        private bool _mockVs;

        public TesterTestAdapter() { }

        private async Task Connect(
            string application,
            string executable,
            Version version,
            string hive,
            CancellationToken cancel
        ) {
            Close();

            Internal.VisualStudio ide = null;
            try {
                ide = await Internal.VisualStudio.LaunchAsync(application, executable, version, hive, cancel);

                var p = Process.GetProcessById(ide.ProcessId);
                string url = string.Format(
                    "ipc://{0}/{1}",
                    Internal.VSTestHostPackage.GetChannelName(Process.GetProcessById(ide.ProcessId)),
                    TesteeTestAdapter.Url
                );

                _remote = (TesteeTestAdapter)RemotingServices.Connect(typeof(TesteeTestAdapter), url);

                _ide = ide;
                ide = null;
            } finally {
                if (ide != null) {
                    ide.Dispose();
                }
            }
        }

        /// <summary>
        /// Closes VS and clears our state. Close may be called multiple times
        /// safely, and may be safely followed by another call to Connect with
        /// the same or different parameters.
        /// </summary>
        private void Close() {
            var ide = Interlocked.Exchange(ref _ide, null);
            var remote = Interlocked.Exchange(ref _remote, null);

            var disposableRemote = remote as IDisposable;
            if (disposableRemote != null) {
                try {
                    disposableRemote.Dispose();
                } catch (RemotingException) {
                }
            }
            if (ide != null) {
                try {
                    // Try to close VS gracefully, otherwise Dispose() will just
                    // go in and kill it
                    ide.DTE.Quit();
                } catch (Exception ex) {
                    Trace.TraceError(ex.ToString());
                }
                ide.Dispose();
            }
        }

        private bool IsClientAlive() {
            var ide = _ide;
            var remote = _remote;

            if (ide == null || remote == null) {
                return false;
            }

            try {
                if (remote.IsInitialized) {
                    return true;
                }
            } catch (RemotingException) {
            }

            return false;
        }

        /// <summary>
        /// Implements initialization. This is called by ITestAdapter.Initialize
        /// (a synchronous function) which will block until this function is
        /// completed.
        /// </summary>
        /// <param name="runContext">
        /// The context for the current test run.
        /// </param>
        private async Task InitializeWorker(IRunContext runContext) {
            string application, executable, versionString, hive;
            Version version;
            string launchTimeoutInSecondsString;
            int launchTimeoutInSeconds;

            var vars = runContext.RunConfig.TestRun.RunConfiguration.TestSettingsProperties;

            // VSApplication is the registry key name like 'VisualStudio'
            vars.TryGetValue("VSApplication", out application);
            // VSExecutableName is the executable name like 'devenv'
            if (vars.TryGetValue("VSExecutable", out executable) &&
                !string.IsNullOrEmpty(executable) &&
                string.IsNullOrEmpty(Path.GetExtension(executable))) {
                executable = Path.ChangeExtension(executable, ".exe");
            }
            // VSVersion is the version like '12.0'
            if (!vars.TryGetValue("VSVersion", out versionString) ||
                !Version.TryParse(versionString, out version)) {
                version = null;
            }
            // VSHive is the optional hive like 'Exp'
            vars.TryGetValue("VSHive", out hive);

            if (!vars.TryGetValue("VSLaunchTimeoutInSeconds", out launchTimeoutInSecondsString) ||
                !int.TryParse(launchTimeoutInSecondsString, out launchTimeoutInSeconds)) {
                launchTimeoutInSeconds = 30;
            }

            if (string.IsNullOrEmpty(application) || string.IsNullOrEmpty(executable) || version == null) {
                throw new ArgumentException(string.Format(
                    Resources.MissingConfigurationValues,
                    application ?? "(null)",
                    executable ?? "(null)",
                    version != null ? version.ToString() : "(null)",
                    hive ?? "(null)"
                ));
            }
            
            if (application == "Mock") {
                _runContext = runContext;
                _remote = new TesteeTestAdapter();
                _remote.Initialize(_runContext);
                // In the mock case tester and testee are the same process, therefore
                // VSTestContext is in our process too.  So we can just set this value
                // directly here.
                VSTestContext.IsMock = true;
                _mockVs = true;
                return;
            }

            // TODO: Detect and perform first run of VS if necessary.
            // The first time a VS hive is run, the user sees a dialog allowing
            // them to select settings such as the theme. We can avoid this by
            // running devenv.exe /resetSettings <path to profile.settings>
            // first, though it is not trivial to detect when this is necessary.
            // Without having done this, all tests will time out. For now, the
            // user is responsible for running VS at least once before
            // attempting to execute tests.

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(launchTimeoutInSeconds));
            try {
                await Connect(application, executable, version, hive, cts.Token);
            } catch (OperationCanceledException ex) {
                throw new TimeoutException(string.Format(Resources.VSLaunchTimeout, launchTimeoutInSeconds), ex);
            }

            _runContext = runContext;
            _remote.Initialize(_runContext);

            if (_runContext.RunConfig.TestRun.RunConfiguration.IsExecutedUnderDebugger) {
                // If we're debugging, tell our host VS to attach to the new VS
                // instance we just started.
                bool mixedMode = false;
                string debugMixedMode;
                if (!vars.TryGetValue("VSDebugMixedMode", out debugMixedMode) ||
                    !bool.TryParse(debugMixedMode, out mixedMode)) {
                    mixedMode = false;
                }
                TesterDebugAttacherShared.AttachDebugger(_ide.ProcessId, mixedMode);
            }
        }

        #region ITestAdapter members

        public void Initialize(IRunContext runContext) {
            if (_ide == null) {
                var runId = _runId = runContext.RunConfig.TestRun.Id;
                TestRunTextResultMessage failure = null;

                try {
                    InitializeWorker(runContext).GetAwaiter().GetResult();
                } catch (ArgumentException ex) {
                    failure = new TestRunTextResultMessage(runId, ex.Message);
                } catch (TimeoutException ex) {
                    failure = new TestRunTextResultMessage(runId, ex.Message);
                } catch (Exception ex) {
                    failure = new TestRunTextResultMessage(
                        runId,
                        string.Format("{0}: {1}{2}{3}", ex.GetType().Name, ex.Message, Environment.NewLine, ex)
                    );
                    failure.SystemException = ex;
                }

                if (failure != null) {
                    runContext.ResultSink.AddResult(failure);
                    runContext.StopTestRun();
                }
            }
        }

        private void SendMessage(IRunContext runContext, string message, ITestElement currentTest = null) {
            if (runContext == null) {
                return;
            }

            var runId = runContext.RunConfig.TestRun.Id;

            TestMessage msg;
            if (currentTest == null) {
                msg = new TestRunTextResultMessage(runId, message);
            } else {
                msg = new TextTestResultMessage(runId, currentTest, message);
            }

            runContext.ResultSink.AddResult(msg);
        }

        private bool RemoteCall(
            Action<TesteeTestAdapter> action,
            int retries = 2,
            bool restartVS = true,
            ITestElement currentTest = null,
            [CallerMemberName] string caller = null
        ) {
            var runContext = _runContext;
            if (runContext == null) {
                throw new InvalidOperationException(Resources.NoRunContext);
            }

            var runId = runContext.RunConfig.TestRun.Id;

            bool firstAttempt = true;

            while (retries-- > 0) {
                if (!_mockVs) {
                    if (!firstAttempt) {
                        // Send a message announcing that we are retrying the call
                        SendMessage(
                            runContext,
                            string.Format(
                                Resources.RetryRemoteCall,
                                currentTest == null ? currentTest.HumanReadableId : caller
                            ),
                            currentTest
                        );
                    }
                    firstAttempt = false;

                    if (!IsClientAlive()) {
                        Close();

                        if (restartVS) {
                            SendMessage(runContext, "Restarting VS", currentTest);
                            Initialize(runContext);
                        } else {
                            SendMessage(runContext, Resources.NoClient, currentTest);
                            return false;
                        }
                    }
                }

                var remote = _remote;
                if (remote == null) {
                    return false;
                }

                try {
                    action(remote);
                    return true;
                } catch (RemotingException ex) {
#if DEBUG
                    var msg = string.Format(Resources.RemotingErrorDebug, caller, ex.Message, ex.ToString());
#else
                    var msg = string.Format(Resources.RemotingError, caller, ex.Message);
#endif
                    SendMessage(runContext, msg, currentTest);

                    // Close _remote and let EnsureClient bring it back if
                    // requested by the caller
                    var disposableRemote = Interlocked.Exchange(ref _remote, null) as IDisposable;
                    if (disposableRemote != null) {
                        try {
                            disposableRemote.Dispose();
                        } catch (RemotingException) {
                        }
                    }
                }
            }

            throw new InvalidOperationException(Resources.NoClient);
        }

        public void Cleanup() {
            RemoteCall(r => r.Cleanup());
            Close();
        }


        public void PreTestRunFinished(IRunContext runContext) {
            RemoteCall(r => r.PreTestRunFinished(runContext));
        }

        public void ReceiveMessage(object message) {
            RemoteCall(r => r.ReceiveMessage(message));
        }


        public void AbortTestRun() {
            RemoteCall(r => r.AbortTestRun(), restartVS: false);
        }

        public void PauseTestRun() {
            RemoteCall(r => r.PauseTestRun(), restartVS: false);
        }

        public void ResumeTestRun() {
            if (!RemoteCall(r => r.ResumeTestRun())) {
                throw new InvalidOperationException(string.Format(Resources.FailedToResume));
            }
        }

        public void Run(ITestElement testElement, ITestContext testContext) {
            if (!RemoteCall(r => r.Run(testElement, testContext), currentTest: testElement)) {
                testContext.ResultSink.AddResult(new TestResult(".", _runId, testElement) {
                    Outcome = TestOutcome.NotRunnable
                });
            }
        }

        public void StopTestRun() {
            RemoteCall(r => r.StopTestRun(), restartVS: false);
        }

        #endregion
    }
}

#endif

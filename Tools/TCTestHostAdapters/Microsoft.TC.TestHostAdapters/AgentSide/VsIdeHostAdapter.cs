/***************************************************************************

Copyright (c) Microsoft Corporation. All rights reserved.
This code is licensed under the Visual Studio SDK license terms.
THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

***************************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Runtime.Serialization.Formatters;
using System.Security;
using System.Security.Permissions;
using System.Security.Principal;
using System.Windows.Forms;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestTools.Common;
using Microsoft.VisualStudio.TestTools.Execution;
using Microsoft.VisualStudio.TestTools.TestAdapter;
using Microsoft.Win32;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.TestTools.Vsip;

namespace Microsoft.TC.TestHostAdapters
{
    /// <summary>
    /// Vs Ide Host Adapter: Agent side.
    /// This wraps ITestAdapter and looks like ITestAdapter for the Agent.
    /// Internally it delegates to original test adapter hosted by Visual Studio IDE.
    /// 
    /// Registry settins that affect execution:
    /// Registry: HKCU\SOFTWARE\Microsoft\VisualStudio\10.0\EnterpriseTools\QualityTools\HostAdapters\TC VS IDE\
    /// - RestartVsCounter (DWORD): 
    ///   If set to value != 0, Host Adapter will restart VS BEFORE next test, 
    ///   then decrement the value in registry by 1, until it becomes 0.
    /// - RegistryHiveOverride (string): 
    ///   Overrides Run Config hive setting, also can be used for running with attribute.
    /// </summary>
    [RegisterHostAdapter(Constants.VsIdeHostAdapterName, typeof(VsIdeHostAdapter), typeof(RunConfigControl))]
    [DefaultRegistryRoot(@"Software\Microsoft\VisualStudio\11.0")]
    // We don't need to define Dispose, as IDisposable children are managed objects and we call Dispose on them when we clean up.
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable")]
    internal class VsIdeHostAdapter : ITestAdapter
    {
        #region Fields
        private IRunContext m_runContext;
        private TestRunConfiguration m_runConfig;
        private string m_workingDir;
        private ITestAdapter m_hostSide;
        private object m_hostSideLock = new object();
        private IVsIdeTestHostAddin m_testHostAddin;
        private IChannel m_clientChannel;
        private IChannel m_serverChannel;
        private VisualStudioIde m_vsIde;
        private string m_vsRegistryHive;    // This is like 10.0 or 10.0Exp.
        private bool m_isHostSideDirty;     // Means: there was at least 1 tests that used the IDE.
        private RetryMessageFilter m_comMessageFilter;
        private static readonly TimeSpan s_ideStartupTimeout = TimeSpan.FromMilliseconds(RegistrySettings.BaseTimeout * 60 * 2);
        private static readonly TimeSpan s_addinWaitTimeout = TimeSpan.FromMilliseconds(RegistrySettings.BaseTimeout * 60); // For how long time to poll VS to load plugins.
        private static readonly TimeSpan s_debuggerTimeout = TimeSpan.FromMilliseconds(RegistrySettings.BaseTimeout * 60 * 3);
        #endregion

        #region Constructor
        /// <summary>
        /// Constructor. Called by Agent via Activator.CreateInstance and should not have any parameters.
        /// </summary>
        public VsIdeHostAdapter()
        {
        }
        #endregion

        #region IBaseAdapter, ITestAdapter
        /// <summary>
        /// ITestAdapter method: called to initialize run context for this adapter.
        /// </summary>
        /// <param name="runContext">The run context to be used for this run</param>
        [SuppressMessage("Microsoft.Naming", "CA1725:ParameterNamesShouldMatchBaseDeclaration", MessageId = "0#")]  // Base param name is incorrect.
        void ITestAdapter.Initialize(IRunContext runContext)
        {
            Debug.Assert(runContext != null);

            m_runContext = runContext;
            m_runConfig = m_runContext.RunConfig.TestRun.RunConfiguration;
            m_workingDir = m_runContext.RunContextVariables.GetStringValue("TestDeploymentDir");

            Debug.Assert(m_runConfig != null);
            Debug.Assert(!string.IsNullOrEmpty(m_workingDir));

            SetupChannels();

            // Install COM message filter to retry COM calls when VS IDE is busy, e.g. when getting the addin from VS IDE.
            // This prevents RPC_E_CALL_REJECTED error when VS IDE is busy.
            m_comMessageFilter = new RetryMessageFilter();

            InitHostSide();
        }

        /// <summary>
        /// IBaseAdapter method: called to execute a test.
        /// </summary>
        /// <param name="testElement">The test object to run</param>
        /// <param name="testContext">The Test conext for this test invocation</param>
        void IBaseAdapter.Run(ITestElement testElement, ITestContext testContext)
        {
            CheckRestartVs();

            ((ITestAdapter)m_hostSide).Run(testElement, testContext);

            m_isHostSideDirty = true;
        }

        /// <summary>
        /// IBaseAdapter method: called when the test run is complete.
        /// </summary>
        void IBaseAdapter.Cleanup()
        {
            try
            {
                CleanupHostSide();

                // Uninstall COM message filter.
                m_comMessageFilter.Dispose();
            }
            finally
            {
                CleanupChannels();
            }
        }

        /// <summary>
        /// IBaseAdapter method: called when the user stops the test run.
        /// </summary>
        void IBaseAdapter.StopTestRun()
        {
            ((ITestAdapter)HostSide).StopTestRun();
        }

        /// <summary>
        /// IBaseAdapter method: called when the test run is aborted.
        /// </summary>
        void IBaseAdapter.AbortTestRun()
        {
            ((ITestAdapter)HostSide).AbortTestRun();
        }

        /// <summary>
        /// IBaseAdapter method: called when the user pauses the test run.
        /// </summary>
        void IBaseAdapter.PauseTestRun()
        {
            ((ITestAdapter)HostSide).PauseTestRun();
        }

        /// <summary>
        /// IBaseAdapter method: called when the user resumes a paused test run.
        /// </summary>
        void IBaseAdapter.ResumeTestRun()
        {
            ((ITestAdapter)HostSide).ResumeTestRun();
        }

        /// <summary>
        /// ITestAdapter method: called when a message is sent from the UI or the controller.
        /// </summary>
        /// <param name="obj">The message object</param>
        void ITestAdapter.ReceiveMessage(object obj)
        {
            ((ITestAdapter)HostSide).ReceiveMessage(obj);
        }

        /// <summary>
        /// ITestAdapter method: called just before the test run finishes and
        /// gives the adapter a chance to do any clean-up.
        /// </summary>
        /// <param name="runContext">The run context for this run</param>
        void ITestAdapter.PreTestRunFinished(IRunContext runContext)
        {
            ((ITestAdapter)HostSide).PreTestRunFinished(runContext);
        }
        #endregion

        #region Private
        /// <summary>
        /// The Host Side of Vs Ide Host Adapter.
        /// </summary>
        private ITestAdapter HostSide
        {
            get
            {
                Debug.Assert(m_hostSide != null, "HA.HostSide: m_hostSide = null!");
                return m_hostSide;
            }
        }

        /// <summary>
        /// Creates VS IDE and HostSide inside it.
        /// This can be called multiple times in the run if specified to restart VS between tests.
        /// </summary>
        private void InitHostSide()
        {
            Debug.Assert(m_runContext != null);
            try
            {
                m_vsRegistryHive = GetRegistryHive();

                lock (m_hostSideLock)
                {
                    // Get the "host side" of the host adapter.
                    CreateHostSide();

                    // Call Initialize for the host side.
                    ((ITestAdapter)HostSide).Initialize(m_runContext);

                    // If test run was started under debugger, attach debugger.
                    CheckAttachDebugger();
                }
            }
            catch (Exception ex)
            {
                Debug.Fail("VsIdeHostAdapter.Initialize" + ex.ToString());

                // Report the error to the agent.
                SendResult(string.Format(CultureInfo.InvariantCulture, Resources.FailedToInitHostSide, ex.ToString()), TestOutcome.Error, true);
                throw;
            }

            m_isHostSideDirty = false;
        }

        /// <summary>
        /// Determine which registry hive to use for Visual Studio:
        ///     If override value is set, use it, don't use anything else.
        ///     Else If using RunConfig, get it from RunConfig
        ///     Else get it from environment.
        /// </summary>
        /// <returns></returns>
        private string GetRegistryHive()
        {
            // We get registry hive each time we initialize host side, i.e. it can be changed in between tests.
            string overrideHiveValue = RegistrySettings.RegistryHiveOverride;
            if (!string.IsNullOrEmpty(overrideHiveValue))
            {
                return overrideHiveValue;
            }

            // Note that Run Config Data can be null, e.g. when executing using HostType attribute.
            TestRunConfiguration runConfig = m_runContext.RunConfig.TestRun.RunConfiguration;
            RunConfigData runConfigHostData = runConfig.HostData[Constants.VsIdeHostAdapterName] as RunConfigData;
            if (runConfigHostData != null)
            {
                return runConfigHostData.RegistryHive;
            }

            return null;    // VsIde will figure out and use default.
        }

        /// <summary>
        /// Starts new Visual Studio process and obtains host side from it.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        private void CreateHostSide()
        {
            // Note: we already have try-catch-Debug.Fail in Initialize that calls this method.
            // Note: registryHive can be null when using HostType attribute. In this case we'll use default hive.
            Debug.Assert(!string.IsNullOrEmpty(m_workingDir));
            // Note: registryHive can be null when using the attribute. That's OK, VsIde will figure out.
            Debug.Assert(!string.IsNullOrEmpty(m_workingDir));
            Debug.Assert(m_hostSide == null, "HA.CreateHostSide: m_hostSide should be null (ignorable).");
            Debug.Assert(m_vsIde == null, "HA.CreateHostSide: m_vsIde should be null (ignorable).");

            // Start devenv.
            m_vsIde = new VisualStudioIde(new VsIdeStartupInfo(m_vsRegistryHive, m_workingDir));
            m_vsIde.ErrorHandler += HostProcessErrorHandler;

            Stopwatch timer = Stopwatch.StartNew();
            do
            {
                try
                {
                    m_vsIde.Dte.MainWindow.Visible = true;    // This could be in TestRunConfig options for this host type.
                    break;
                }
                catch (Exception)
                {
                }
                System.Threading.Thread.Sleep(RegistrySettings.BaseSleepDuration);
            } while (timer.Elapsed < s_ideStartupTimeout);
            
            m_hostSide = GetHostSideFromAddin();
        }

        /// <summary>
        /// Obtain host side from the addin.
        /// </summary>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        private ITestAdapter GetHostSideFromAddin()
        {
            // Find the Addin.
            // Note: After VS starts addin needs some time to load, so we try a few times.
            AddIn addinLookingFor = null;

            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                do
                {
                    try
                    {
                        // There is no index-by-name API, so we have to check all addins.
                        foreach (AddIn addin in m_vsIde.Dte.AddIns)
                        {
                            if (addin.Name == Constants.VsAddinName)
                            {
                                addinLookingFor = addin;
                                break;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Catch all exceptions to prevent intermittent failures such as COMException (0x8001010A)
                        // while VS has not started yet. Just retry again until we timeout.
                    }

                    if (addinLookingFor != null)
                    {
                        break;
                    }

                    System.Threading.Thread.Sleep(RegistrySettings.BaseSleepDuration);

                } while (timer.Elapsed < s_addinWaitTimeout);
            }
            finally
            {
                timer.Stop();
            }

            if (addinLookingFor == null)
            {
                Debug.Fail("HA.GetHostSideFromAddin: timed out but could not get the addin from VS.");
                throw new VsIdeTestHostException(Resources.TimedOutGettingAddin);
            }

            m_testHostAddin = (IVsIdeTestHostAddin)addinLookingFor.Object;
            ITestAdapter hostSide = m_testHostAddin.GetHostSide();
            Debug.Assert(hostSide != null);

            return hostSide;
        }

        /// <summary>
        /// Check if we need to attach debugger and attach it.
        /// </summary>
        private void CheckAttachDebugger()
        {
            Debug.Assert(m_runConfig != null);

            if (m_runConfig.IsExecutedUnderDebugger)
            {
                Debug.Assert(m_vsIde != null);

                DebugTargetInfo debugInfo = new DebugTargetInfo();
                debugInfo.ProcessId = m_vsIde.Process.Id;
                ExecutionUtilities.DebugTarget(m_runContext.ResultSink, m_runContext.RunConfig.TestRun.Id, debugInfo, s_debuggerTimeout);
            }
        }

        /// <summary>
        /// Clean up the host side.
        /// We do the following steps. Each step is important and must be done whenever previous step throws or not.
        /// - Call HostSide.Cleanup.
        /// - Ask Runner IDE to detach debugger (if attached).
        /// - Dispose m_vsIde.
        /// This should not throw.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        private void CleanupHostSide()
        {
            lock (m_hostSideLock)
            {
                Debug.Assert(HostSide != null);

                if (HostSide != null)
                {
                    try
                    {
                        ((ITestAdapter)HostSide).Cleanup();
                    }
                    catch (Exception ex)    // We don't know what this can throw in advance.
                    {
                        SendResult(string.Format(CultureInfo.InvariantCulture, Resources.FailedToCallTACleanup, ex), TestOutcome.Warning);
                    }
                }

                try
                {
                    Debug.Assert(m_vsIde != null);
                    m_vsIde.Dispose();
                }
                catch (Exception ex)
                {
                    SendResult(string.Format(CultureInfo.InvariantCulture, Resources.ErrorShuttingDownVS, ex), TestOutcome.Warning);
                }

                m_vsIde = null;
                m_hostSide = null;  // Note: host side lifetime is controlled by the addin.
            }
        }

        /// <summary>
        /// Check if we need to restart Visual Studio and restart it.
        /// </summary>
        private void CheckRestartVs()
        {
            lock (m_hostSideLock)
            {
                // If needed, restart IDE. We check reg key, and then auto-reset it. So it's 1 time only key.
                uint restartTimes = RegistrySettings.RestartVsCounter;
                if (restartTimes > 0)
                {
                    try
                    {
                        if (m_isHostSideDirty) // This is optimization: if nobody used this instance of VS, do not restart it.
                        {
                            CleanupHostSide();
                            InitHostSide();
                        }
                    }
                    finally
                    {
                        // Decrement restartTimes for next time. If specified to restart, even if 
                        // m_isHostSideDirty = false, decrement the value anyway, as if we restarted.
                        Debug.Assert(restartTimes > 0);
                        --restartTimes;
                        RegistrySettings.RestartVsCounter = restartTimes;
                    }
                }
            }
        }

        /// <summary>
        /// Set up remoting communication channels.
        /// </summary>
        private void SetupChannels()
        {
            string channelPrefix = "EqtVsIdeHostAdapter_" + Guid.NewGuid().ToString();

            // Server channel is required for callbacks from client side.
            // Actually it is not required when running from vstesthost as vstesthost already sets up the channels
            // but since we have /noisolation (~ no vstesthost) mode we need to create this channel. 
            BinaryServerFormatterSinkProvider serverProvider = new BinaryServerFormatterSinkProvider();
            serverProvider.TypeFilterLevel = TypeFilterLevel.Full;  // Enable remoting objects as arguments.
            Hashtable props = new Hashtable();
            string serverChannelName = channelPrefix + "_ServerChannel";
            props["name"] = serverChannelName;
            props["portName"] = serverChannelName;           // Must be different from client's port.
            // Default IpcChannel security is: allow for all users who can authorize on this machine.
            props["authorizedGroup"] = WindowsIdentity.GetCurrent().Name;
            m_serverChannel = new IpcServerChannel(props, serverProvider);
            ChannelServices.RegisterChannel(m_serverChannel, false);

            m_clientChannel = new IpcClientChannel(channelPrefix + "_ClientChannel", new BinaryClientFormatterSinkProvider());
            ChannelServices.RegisterChannel(m_clientChannel, false);
        }

        /// <summary>
        /// Clean up remoting communication channels.
        /// </summary>
        private void CleanupChannels()
        {
            if (m_clientChannel != null)
            {
                ChannelServices.UnregisterChannel(m_clientChannel);
                m_clientChannel = null;
            }
            if (m_serverChannel != null)
            {
                ChannelServices.UnregisterChannel(m_serverChannel);
                m_serverChannel = null;
            }
        }

        /// <summary>
        /// Error handler for the Visual Studio process exited event.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <param name="outcome">The outcome for the test.</param>
        /// <param name="abortTestRun">Whether test run needs to be aborted.</param>
        private void HostProcessErrorHandler(string errorMessage, TestOutcome outcome, bool abortTestRun)
        {
            Debug.Assert(m_runContext != null);
            SendResult(errorMessage, outcome, abortTestRun);
        }

        /// <summary>
        /// Helper method to send message to the Agent.
        /// </summary>
        /// <param name="messageText">The text for the message.</param>
        /// <param name="outcome">The outcome for the test.</param>
        private void SendResult(string messageText, TestOutcome outcome)
        {
            SendResult(messageText, outcome, false);
        }

        /// <summary>
        /// Sends run level message to the result sink.
        /// </summary>
        /// <param name="message">Text for the message.</param>
        /// <param name="outcome">Outcome for the message. Affects test run outcome.</param>
        /// <param name="abortTestRun">If true, we use TMK.Panic, otherwise TMK.TextMessage</param>
        private void SendResult(string messageText, TestOutcome outcome, bool abortTestRun)
        {
            Debug.Assert(!abortTestRun || outcome == TestOutcome.Error, 
                "HA.SendResult: When abortTestRun = true, Outcome should be Error.");

            TestRunTextResultMessage message = new TestRunTextResultMessage(
                Environment.MachineName,
                m_runContext.RunConfig.TestRun.Id,
                messageText,
                TestMessageKind.TextMessage);
            message.Outcome = outcome;

            m_runContext.ResultSink.AddResult(message);

            if (abortTestRun)
            {
                m_runContext.ResultSink.AddResult(new RunStateEvent(m_runContext.RunConfig.TestRun.Id,
                                                                RunState.Aborting,
                                                                Environment.MachineName));
            }
        }
        #endregion
    }
}

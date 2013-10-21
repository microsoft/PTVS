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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Runtime.Serialization.Formatters;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Xml;
using EnvDTE;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.TestTools.Common;
using Microsoft.VisualStudio.TestTools.Common.Xml;
using Microsoft.VisualStudio.TestTools.Execution;
using Microsoft.VisualStudio.TestTools.TestAdapter;
using Microsoft.Win32;
using IBindCtx = Microsoft.VisualStudio.OLE.Interop.IBindCtx;
using IEnumMoniker = Microsoft.VisualStudio.OLE.Interop.IEnumMoniker;
using IMessageFilter = Microsoft.VisualStudio.OLE.Interop.IMessageFilter;
using IMoniker = Microsoft.VisualStudio.OLE.Interop.IMoniker;
using IRunningObjectTable = Microsoft.VisualStudio.OLE.Interop.IRunningObjectTable;
using Process = System.Diagnostics.Process;

namespace TestUtilities
{
    /// <summary>
    /// Vs Ide Host Adapter: Agent side.
    /// This wraps ITestAdapter and looks like ITestAdapter for the Agent.
    /// Internally it delegates to original test adapter hosted by Visual Studio IDE.
    /// </summary>
    public class VsIdeHostAdapter : ITestAdapter
    {
        public const string DynamicHostAdapterName = "TC Dynamic";
        public const string VsAddinName = "TcVsIdeTestHost";

        private IRunContext _runContext;
        private TestRunConfiguration _runConfig;
        private string _workingDir;
        private ITestAdapter _hostSide;
        private object _hostSideLock = new object();
        private IVsIdeTestHostAddin _testHostAddin;
        private IChannel _clientChannel;
        private IChannel _serverChannel;
        private VisualStudioIde _vsIde;
        private string _vsRegistryHive;    // This is like 10.0 or 10.0Exp.
        private RetryMessageFilter _comMessageFilter;
        private static readonly TimeSpan _ideStartupTimeout = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan _addinWaitTimeout = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan _debuggerTimeout = TimeSpan.FromMinutes(3);
        private static readonly TimeSpan _baseSleepDuration = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan _baseSleepDoubleDuration = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Constructor. Called by Agent via Activator.CreateInstance and should not have any parameters.
        /// </summary>
        public VsIdeHostAdapter()
        {
        }

        /// <summary>
        /// The Host Side of Vs Ide Host Adapter.
        /// </summary>
        private ITestAdapter HostSide
        {
            get
            {
                return _hostSide;
            }
        }

        /// <summary>
        /// ITestAdapter method: called to initialize run context for this adapter.
        /// </summary>
        /// <param name="runContext">The run context to be used for this run</param>
        void ITestAdapter.Initialize(IRunContext runContext)
        {
            Contract.Assert(runContext != null);

            _runContext = runContext;
            _runConfig = _runContext.RunConfig.TestRun.RunConfiguration;
            _workingDir = _runContext.RunContextVariables.GetStringValue("TestDeploymentDir");

            Contract.Assert(_runConfig != null);
            Contract.Assert(!string.IsNullOrEmpty(_workingDir));

            SetupChannels();

            // Install COM message filter to retry COM calls when VS IDE is busy, e.g. when getting the addin from VS IDE.
            // This prevents RPC_E_CALL_REJECTED error when VS IDE is busy.
            _comMessageFilter = new RetryMessageFilter();

            InitHostSide();
        }

        /// <summary>
        /// IBaseAdapter method: called to execute a test.
        /// </summary>
        /// <param name="testElement">The test object to run</param>
        /// <param name="testContext">The Test conext for this test invocation</param>
        void IBaseAdapter.Run(ITestElement testElement, ITestContext testContext)
        {
            if (testElement.TestCategories.Contains(new TestCategoryItem("RestartVS"))) {
                CleanupHostSide();
                InitHostSide();
            }

            _hostSide.Run(testElement, testContext);
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
                _comMessageFilter.Dispose();
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
            HostSide.StopTestRun();
        }

        /// <summary>
        /// IBaseAdapter method: called when the test run is aborted.
        /// </summary>
        void IBaseAdapter.AbortTestRun()
        {
            HostSide.AbortTestRun();
        }

        /// <summary>
        /// IBaseAdapter method: called when the user pauses the test run.
        /// </summary>
        void IBaseAdapter.PauseTestRun()
        {
            HostSide.PauseTestRun();
        }

        /// <summary>
        /// IBaseAdapter method: called when the user resumes a paused test run.
        /// </summary>
        void IBaseAdapter.ResumeTestRun()
        {
            HostSide.ResumeTestRun();
        }

        /// <summary>
        /// ITestAdapter method: called when a message is sent from the UI or the controller.
        /// </summary>
        /// <param name="obj">The message object</param>
        void ITestAdapter.ReceiveMessage(object obj)
        {
            HostSide.ReceiveMessage(obj);
        }

        /// <summary>
        /// ITestAdapter method: called just before the test run finishes and
        /// gives the adapter a chance to do any clean-up.
        /// </summary>
        /// <param name="runContext">The run context for this run</param>
        void ITestAdapter.PreTestRunFinished(IRunContext runContext)
        {
            HostSide.PreTestRunFinished(runContext);
        }

        /// <summary>
        /// Creates VS IDE and HostSide inside it.
        /// This can be called multiple times in the run if specified to restart VS between tests.
        /// </summary>
        private void InitHostSide()
        {
            Contract.Assert(_runContext != null);
            try
            {
                _vsRegistryHive = GetRegistryHive();

                lock (_hostSideLock)
                {
                    // Get the "host side" of the host adapter.
                    CreateHostSide();

                    // Call Initialize for the host side.
                    ((ITestAdapter)HostSide).Initialize(_runContext);

                    // If test run was started under debugger, attach debugger.
                    CheckAttachDebugger();
                }
            }
            catch (Exception ex)
            {
                // Report the error to the agent.
                SendResult(string.Format(CultureInfo.InvariantCulture, "VsIdeHostAdapter: Failed to initialize the host side: {0}", ex.ToString()), TestOutcome.Error, true);
                throw;
            }
        }


        /// <summary>
        /// Determine which registry hive to use for Visual Studio:
        ///     If using RunConfig, get it from RunConfig
        ///     Else get it from environment.
        /// </summary>
        /// <returns></returns>
        private string GetRegistryHive()
        {
            // Note that Run Config Data can be null, e.g. when executing using HostType attribute.
            TestRunConfiguration runConfig = _runContext.RunConfig.TestRun.RunConfiguration;
            string configKey;
            if (TryGetRegistryHiveFromConfig(runConfig, out configKey))
            {
                return configKey;
            }

            if (System.Environment.GetEnvironmentVariable("RUN_NO_EXP") != null)
                return VSUtility.Version;

            // Default to the experimental hive for the development evnironment.
            return VSUtility.Version + "Exp";
        }

        /// <summary>
        /// Starts new Visual Studio process and obtains host side from it.
        /// </summary>
        private void CreateHostSide()
        {
            Contract.Assert(!string.IsNullOrEmpty(_workingDir));
            Contract.Assert(_hostSide == null);
            Contract.Assert(_vsIde == null);

            // Start devenv.
            _vsIde = new VisualStudioIde(new VsIdeStartupInfo(_vsRegistryHive, _workingDir));
            _vsIde.ErrorHandler += HostProcessErrorHandler;

            Stopwatch timer = Stopwatch.StartNew();
            do
            {
                try
                {
                    _vsIde.Dte.MainWindow.Visible = true;    // This could be in TestRunConfig options for this host type.
                    break;
                }
                catch (Exception)
                {
                }
                System.Threading.Thread.Sleep(_baseSleepDuration);
            } while (timer.Elapsed < _ideStartupTimeout);

            _hostSide = GetHostSideFromAddin();
        }

        /// <summary>
        /// Obtain host side from the addin.
        /// </summary>
        /// <returns></returns>
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
                        foreach (AddIn addin in _vsIde.Dte.AddIns)
                        {
                            if (addin.Name.StartsWith(VsAddinName))
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

                    System.Threading.Thread.Sleep(_baseSleepDuration);

                } while (timer.Elapsed < _addinWaitTimeout);
            }
            finally
            {
                timer.Stop();
            }

            if (addinLookingFor == null)
            {
                throw new VsIdeTestHostException("Timed out getting Vs Ide Test Host Add-in from Visual Studio. Please make sure that the Add-in is installed and started when VS starts (use Tools->Add-in Manager).");
            }

            _testHostAddin = (IVsIdeTestHostAddin)addinLookingFor.Object;
            ITestAdapter hostSide = _testHostAddin.GetHostSide();
            Contract.Assert(hostSide != null);

            return hostSide;
        }

        /// <summary>
        /// Check if we need to attach debugger and attach it.
        /// </summary>
        private void CheckAttachDebugger()
        {
            Contract.Assert(_runConfig != null);

            if (_runConfig.IsExecutedUnderDebugger)
            {
                Contract.Assert(_vsIde != null);

                DebugTargetInfo debugInfo = new DebugTargetInfo();
                debugInfo.ProcessId = _vsIde.Process.Id;
                ExecutionUtilities.DebugTarget(_runContext.ResultSink, _runContext.RunConfig.TestRun.Id, debugInfo, _debuggerTimeout);
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
        private void CleanupHostSide()
        {
            lock (_hostSideLock)
            {
                Contract.Assert(HostSide != null);

                if (HostSide != null)
                {
                    try
                    {
                        HostSide.Cleanup();
                    }
                    catch (Exception ex)    // We don't know what this can throw in advance.
                    {
                        SendResult(string.Format(CultureInfo.InvariantCulture, "Warning: VsIdeHostAdapter failed to call ITestAdapter.Cleanup: {0}", ex), TestOutcome.Warning);
                    }
                }

                try
                {
                    Contract.Assert(_vsIde != null);
                    _vsIde.Dispose();
                }
                catch (Exception ex)
                {
                    SendResult(string.Format(CultureInfo.InvariantCulture, "Warning: VsIdeHostAdapter: error shutting down VS IDE: {0}", ex), TestOutcome.Warning);
                }

                _vsIde = null;
                _hostSide = null;  // Note: host side lifetime is controlled by the addin.
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
            _serverChannel = new IpcServerChannel(props, serverProvider);
            ChannelServices.RegisterChannel(_serverChannel, false);

            _clientChannel = new IpcClientChannel(channelPrefix + "_ClientChannel", new BinaryClientFormatterSinkProvider());
            ChannelServices.RegisterChannel(_clientChannel, false);
        }

        /// <summary>
        /// Clean up remoting communication channels.
        /// </summary>
        private void CleanupChannels()
        {
            if (_clientChannel != null)
            {
                ChannelServices.UnregisterChannel(_clientChannel);
                _clientChannel = null;
            }
            if (_serverChannel != null)
            {
                ChannelServices.UnregisterChannel(_serverChannel);
                _serverChannel = null;
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
            Contract.Assert(_runContext != null);
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
            Contract.Assert(!abortTestRun || outcome == TestOutcome.Error,
                "SendResult: When abortTestRun = true, Outcome should be Error.");

            TestRunTextResultMessage message = new TestRunTextResultMessage(
                Environment.MachineName,
                _runContext.RunConfig.TestRun.Id,
                messageText,
                TestMessageKind.TextMessage);
            message.Outcome = outcome;

            _runContext.ResultSink.AddResult(message);

            if (abortTestRun)
            {
                _runContext.ResultSink.AddResult(new RunStateEvent(_runContext.RunConfig.TestRun.Id,
                                                                RunState.Aborting,
                                                                Environment.MachineName));
            }
        }

        private bool TryGetRegistryHiveFromConfig(TestRunConfiguration runConfig, out string hive)
        {
            const string VSHiveElement = "VSHive";
            hive = null;

            if (runConfig == null)
            {
                return false;
            }
            string configFileName = runConfig.Storage;
            if (string.IsNullOrEmpty(configFileName))
            {
                return false;
            }
            if (!File.Exists(configFileName))
            {
                // This will happen in the case with no file where a default file is created.
                if (!configFileName.StartsWith("default", StringComparison.OrdinalIgnoreCase))
                {
                    SendResult(string.Format(CultureInfo.InvariantCulture, "VsIdeHostAdapter: Unable to find config file: {0}", configFileName), TestOutcome.Warning, false);
                }
                return false;
            }

            try
            {
                using (var configXml = new XmlTextReader(configFileName))
                {
                    while (configXml.Read())
                    {
                        if (configXml.NodeType == XmlNodeType.Element && configXml.Name == VSHiveElement)
                        {
                            configXml.Read();
                            if (configXml.NodeType == XmlNodeType.Text)
                            {
                                hive = configXml.Value;
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Report the error to the agent.
                SendResult(string.Format(CultureInfo.InvariantCulture, "VsIdeHostAdapter: Error reading config file: {0}", ex.ToString()), TestOutcome.Warning, false);
            }
            return false;
        }

        /// <summary>
        /// The information to start Visual Studio.
        /// </summary>
        internal class VsIdeStartupInfo
        {
            private string _registryHive;
            private string _workingDirectory;

            internal VsIdeStartupInfo(string registryHive, string workingDirectory)
            {
                // Note: registryHive can be null when using the attribute. That's OK, VsIde will figure out.
                Contract.Assert(!string.IsNullOrEmpty(workingDirectory));

                _registryHive = registryHive;
                _workingDirectory = workingDirectory;
            }

            /// <summary>
            /// Hive name under Microsoft.VisualStudio, like 10.0Exp.
            /// </summary>
            internal string RegistryHive
            {
                get { return _registryHive; }
                set { _registryHive = value; }
            }

            /// <summary>
            /// Working directory for devenv.exe process.
            /// </summary>
            internal string WorkingDirectory
            {
                get { return _workingDirectory; }
            }
        }

        /// <summary>
        /// This wraps Visual Studio DTE (automation object).
        /// </summary>
        internal class VisualStudioIde : IDisposable
        {

            /// <summary>
            /// Used for error reporting.
            /// </summary>
            /// <param name="errorMessage">Error message.</param>
            /// <param name="outcome">The outcome for the test due to this error.</param>
            /// <param name="abortTestRun">Whether the error causes test run to abort.</param>
            internal delegate void VsIdeHostErrorHandler(string errorMessage, TestOutcome outcome, bool abortTestRun);

            private const string BaseProgId = "VisualStudio.DTE";
            // HRESULTs for COM errors.
            private const int CallRejectedByCalleeErrorCode = -2147418111;

            /// <summary>
            /// Time to wait for the VS process to exit after resetting it's profile. 
            /// </summary>
            private static readonly TimeSpan _ideFirstRunTimeout = TimeSpan.FromMinutes(2);

            /// <summary>
            /// How long to wait for IDE to appear in ROT. 
            /// </summary>
            private static readonly TimeSpan _ideStartupTimeout = TimeSpan.FromMinutes(2);

            /// <summary>
            /// How long to wait before killing devenv.exe after Dispose() is called. During this time VS can e.g. save buffers to disk.
            /// </summary>
            private static readonly TimeSpan _ideExitTimeout = TimeSpan.FromSeconds(5);

            /// <summary>
            /// Timeout to wait while VS rejects calls.
            /// </summary>
            private static readonly TimeSpan _rejectedCallTimeout = TimeSpan.FromSeconds(30);


            private DTE _dte;
            private Process _process;
            private object _cleanupLock = new object();

            /// <summary>
            /// Constructor. Starts new instance of VS IDE.
            /// </summary>
            public VisualStudioIde(VsIdeStartupInfo info)
            {
                Contract.Assert(info != null);

                if (string.IsNullOrEmpty(info.RegistryHive))
                {
                    info.RegistryHive = VsRegistry.GetDefaultVersion();
                    if (string.IsNullOrEmpty(info.RegistryHive))
                    {
                        throw new VsIdeTestHostException(string.Format(CultureInfo.InvariantCulture, "Cannot find installation of Visual Studio in '{0}' registry hive.", info.RegistryHive));
                    }
                }

                StartNewInstance(info);
            }

            /// <summary>
            /// Finalizer.
            /// </summary>
            ~VisualStudioIde()
            {
                Dispose(false);
            }

            public DTE Dte
            {
                get { return _dte; }
            }

            public Process Process
            {
                get { return _process; }
            }

            public event VsIdeHostErrorHandler ErrorHandler;

            private static void ResetVSSettings(string vsPath, string hiveSuffix)
            {
                Process process = new Process();
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.FileName = vsPath;
                process.StartInfo.Arguments = "/resetsettings \"General.vssettings\" /Command \"File.Exit\"";
                if (!string.IsNullOrEmpty(hiveSuffix))
                {
                    process.StartInfo.Arguments += " /RootSuffix " + hiveSuffix;
                }

                // Launch VS for the "first time"
                if (!process.Start())
                {
                    throw new VsIdeTestHostException("Failed to start Visual Studio process.");
                }

                // Wait for the process to exit
                if (!process.WaitForExit((int)_ideFirstRunTimeout.TotalMilliseconds))
                {
                    // Kill the process and raise an exception 
                    process.Kill();

                    string message = string.Format(
                        CultureInfo.InvariantCulture,
                        "First run Visual Studio process, used for resetting settings did not exit after {0} seconds.",
                        _ideFirstRunTimeout.TotalSeconds);

                    throw new TimeoutException(message);
                }
            }

            /// <summary>
            /// Create a Visual Studio process.
            /// </summary>
            /// <param name="info">Startup information.</param>
            private void StartNewInstance(VsIdeStartupInfo startupInfo)
            {
                Contract.Assert(startupInfo != null);
                Contract.Assert(_process == null, "VisualStudioIde.StartNewInstance: _process should be null!");

                Process process = new Process();
                process.StartInfo.UseShellExecute = false;
                if (startupInfo.WorkingDirectory != null)
                {
                    process.StartInfo.WorkingDirectory = startupInfo.WorkingDirectory;
                }

                // Note that this needs to be partial (not $-terminated) as we partially match/replace.
                Regex versionRegex = new Regex(@"^[0-9]+\.[0-9]+");

                string hiveVersion = versionRegex.Match(startupInfo.RegistryHive).Value;
                string hiveSuffix = versionRegex.Replace(startupInfo.RegistryHive, string.Empty);

                if (!string.IsNullOrEmpty(hiveSuffix)) {
                    process.StartInfo.Arguments = "/RootSuffix " + hiveSuffix + " /Log";
                } else {
                    process.StartInfo.Arguments = "/Log";
                }

                process.StartInfo.FileName = VsRegistry.GetVsLocation(hiveVersion);
                Contract.Assert(!string.IsNullOrEmpty(process.StartInfo.FileName));

                // Prevent the settings popup on startup
                if (!VsRegistry.UserSettingsArchiveExists(startupInfo.RegistryHive))
                {
                    ResetVSSettings(process.StartInfo.FileName, hiveSuffix);
                    if (!VsRegistry.UserSettingsArchiveExists(startupInfo.RegistryHive))
                    {
                        throw new VsIdeTestHostException("Unable to reset VS settings.");
                    }
                }

                process.Exited += new EventHandler(ProcessExited);
                process.EnableRaisingEvents = true;

                if (!process.Start())
                {
                    throw new VsIdeTestHostException("Failed to start Visual Studio process.");
                }

                _process = process;

                string progId = string.Format(CultureInfo.InvariantCulture, "{0}.{1}", VisualStudioIde.BaseProgId, hiveVersion);
                _dte = GetDteFromRot(progId, _process.Id);
                if (_dte == null)
                {
                    throw new VsIdeTestHostException("Failed to get the DTE object from Visual Studio process. Please make sure that the Add-in is registered in Tools->Add-in Manager to load in when Visual Studio starts.");
                }
            }

            /// <summary>
            /// Obtains Visual Studio automation object from Running Object Table.
            /// </summary>
            /// <param name="progId">DTE's prog id.</param>
            /// <param name="processId">Visual Studio process id to obtain the automation object for.</param>
            /// <returns>Visual Studio automation object.</returns>
            public static DTE GetDteFromRot(string progId, int processId)
            {
                Contract.Assert(!string.IsNullOrEmpty(progId));

                EnvDTE.DTE dte;
                string moniker = string.Format(CultureInfo.InvariantCulture, "!{0}:{1}", progId, processId);

                // It takes some time after process started to register in ROT.
                Stopwatch sw = Stopwatch.StartNew();
                do
                {
                    dte = GetDteFromRot(moniker);
                    if (dte != null)
                    {
                        break;
                    }
                    System.Threading.Thread.Sleep(_baseSleepDoubleDuration);
                } while (sw.Elapsed < _ideStartupTimeout);

                if (dte == null)
                {
                    throw new VsIdeTestHostException("Timed out getting VS.DTE from COM Running Object Table.");
                }
                return dte;
            }

            /// <summary>
            /// Obtains Visual Studio automation object from Running Object Table.
            /// </summary>
            /// <param name="monikerName">The moniker to use as a filter when looking in Running Object Table.</param>
            /// <returns></returns>
            private static DTE GetDteFromRot(string monikerName)
            {
                Contract.Assert(!string.IsNullOrEmpty(monikerName));

                IRunningObjectTable rot;
                IEnumMoniker monikerEnumerator;
                object dte = null;
                try
                {
                    NativeMethods.GetRunningObjectTable(0, out rot);
                    rot.EnumRunning(out monikerEnumerator);
                    monikerEnumerator.Reset();

                    uint fetched = 0;
                    IMoniker[] moniker = new IMoniker[1];
                    while (monikerEnumerator.Next(1, moniker, out fetched) == 0)
                    {
                        IBindCtx bindingContext;
                        NativeMethods.CreateBindCtx(0, out bindingContext);

                        string name;
                        moniker[0].GetDisplayName(bindingContext, null, out name);
                        if (name == monikerName)
                        {
                            object returnObject;
                            rot.GetObject(moniker[0], out returnObject);
                            dte = (object)returnObject;
                            break;
                        }
                    }
                }
                catch
                {
                    return null;
                }

                return (DTE)dte;
            }

            /// <summary>
            /// Called when Visual Studio process exits.
            /// </summary>
            /// <param name="sender">The sender of the event.</param>
            /// <param name="args">Event arguments.</param>
            private void ProcessExited(object sender, EventArgs args)
            {
                lock (_cleanupLock)
                {
                    _process.EnableRaisingEvents = false;
                    _process.Exited -= new EventHandler(ProcessExited);

                    if (ErrorHandler != null)
                    {
                        ErrorHandler("Visual Studio process exited unexpectedly.", TestOutcome.Error, true);
                    }
                }
            }

            /// <summary>
            /// Implements Idisposable.Dispose.
            /// </summary>
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            /// <summary>
            /// The other part of the .NET Dispose pattern.
            /// </summary>
            /// <param name="disposingNotFinalizing">Whether this is explicit dispose, not auto-finalization of the object.</param>
            private void Dispose(bool explicitDispose)
            {
                if (!explicitDispose)
                {
                    // When called from finalizer, just clean up. Don't lock. Don't throw.
                    KillProcess();
                }
                else
                {
                    lock (_cleanupLock)
                    {
                        if (_process.EnableRaisingEvents)
                        {
                            _process.EnableRaisingEvents = false;
                            _process.Exited -= new EventHandler(ProcessExited);
                        }

                        try
                        {
                            if (_dte != null)
                            {
                                // Visual Studio sometimes rejects the call to Quit() so we need to retry it.
                                Stopwatch sw = Stopwatch.StartNew();
                                bool timedOut = true;
                                do
                                {
                                    try 
                                    {
                                        _dte.Quit();
                                        timedOut = false;
                                        break;
                                    } 
                                    catch (COMException ex) 
                                    {
                                        if (ex.ErrorCode == CallRejectedByCalleeErrorCode) 
                                        {
                                            System.Threading.Thread.Sleep(_baseSleepDoubleDuration);
                                        } 
                                        else 
                                        {
                                            throw;
                                        }
                                    } 
                                    catch (Exception) 
                                    {
                                        throw;
                                    }
                                } while (sw.Elapsed < _rejectedCallTimeout);

                                if (timedOut)
                                {
                                    throw new VsIdeTestHostException("Warning: timed out calling Dte.Quit: all the calls were rejected. Visual Studio process will be killed.");
                                }
                            }
                        }
                        finally
                        {
                            KillProcess();
                        }
                    }
                }
            }

            /// <summary>
            /// Waits for Visual Studio process to exit and if it does not in s_ideExitTimeout time, kills it.
            /// </summary>
            private void KillProcess()
            {
                if (_process != null)
                {
                    // wait for the specified time for the IDE to exit.  
                    // If it hasn't, kill the process so we can proceed to the next test.
                    Stopwatch sw = Stopwatch.StartNew();
                    while (!_process.HasExited && (sw.Elapsed < _ideExitTimeout))
                    {
                        System.Threading.Thread.Sleep(_baseSleepDuration);
                    }

                    if (!_process.HasExited)
                    {
                        _process.Kill();
                    }

                    _process = null;
                }
            }
        }

        /// <summary>
        /// COM message filter class to prevent RPC_E_CALL_REJECTED error while DTE is busy.
        /// The filter is used by COM to handle incoming/outgoing messages while waiting for response from a synchonous call.
        /// </summary>
        /// <seealso cref="http://msdn.microsoft.com/library/en-us/com/html/e12d48c0-5033-47a8-bdcd-e94c49857248.asp"/>
        [ComVisible(true)]
        internal class RetryMessageFilter : IMessageFilter, IDisposable
        {
            private const uint RetryCall = 99;
            private const uint CancelCall = unchecked((uint)-1);   // For COM this must be -1 but IMessageFilter.RetryRejectedCall returns uint.

            private IMessageFilter _oldFilter;

            /// <summary>
            /// Constructor.
            /// </summary>
            public RetryMessageFilter()
            {
                // Register the filter.
                NativeMethods.CoRegisterMessageFilter(this, out _oldFilter);
            }

            /// <summary>
            /// FInalizer.
            /// </summary>
            ~RetryMessageFilter()
            {
                Dispose();
            }

            /// <summary>
            /// Implements IDisposable.Dispose.
            /// </summary>
            public void Dispose()
            {
                // Unregister the filter.
                IMessageFilter ourFilter;
                NativeMethods.CoRegisterMessageFilter(_oldFilter, out ourFilter);
                GC.SuppressFinalize(this);
            }

            /// <summary>
            /// Provides an ability to filter or reject incoming calls (or callbacks) to an object or a process. 
            /// Called by COM prior to each method invocation originating outside the current process. 
            /// </summary>
            public uint HandleInComingCall(uint dwCallType, IntPtr htaskCaller, uint dwTickCount, INTERFACEINFO[] lpInterfaceInfo)
            {
                // Let current process try process the call.
                return (uint)SERVERCALL.SERVERCALL_ISHANDLED;
            }

            /// <summary>
            /// An ability to choose to retry or cancel the outgoing call or switch to the task specified by threadIdCallee.
            /// Called by COM immediately after receiving SERVERCALL_RETRYLATER or SERVERCALL_REJECTED
            /// from the IMessageFilter::HandleIncomingCall method on the callee's IMessageFilter interface.
            /// </summary>
            /// <returns>
            /// -1: The call should be canceled. COM then returns RPC_E_CALL_REJECTED from the original method call. 
            /// 0..99: The call is to be retried immediately. 
            /// 100 and above: COM will wait for this many milliseconds and then retry the call.
            /// </returns>
            public uint RetryRejectedCall(IntPtr htaskCallee, uint dwTickCount, uint dwRejectType)
            {
                if (dwRejectType == (uint)SERVERCALL.SERVERCALL_RETRYLATER)
                {
                    // The server called by this process is busy. Ask COM to retry the outgoing call.
                    return RetryCall;
                }
                else
                {
                    // Ask COM to cancel the call and return RPC_E_CALL_REJECTED from the original method call. 
                    return CancelCall;
                }
            }

            /// <summary>
            /// Called by COM when a Windows message appears in a COM application's message queue 
            /// while the application is waiting for a reply to an outgoing remote call. 
            /// </summary>
            /// <returns>
            /// Tell COM whether: to process the message without interrupting the call, 
            /// to continue waiting, or to cancel the operation. 
            /// </returns>
            public uint MessagePending(IntPtr htaskCallee, uint dwTickCount, uint dwPendingType)
            {
                // Continue waiting for the reply, and do not dispatch the message unless it is a task-switching or window-activation message. 
                // A subsequent message will trigger another call to IMessageFilter::MessagePending. 
                return (uint)PENDINGMSG.PENDINGMSG_WAITDEFPROCESS;
            }

        }

        /// <summary>
        /// The data for this host type to extend Run Config with.
        /// - Registry Hive, like 10.0Exp.
        /// - Session id for debugging.
        /// </summary>
        [Serializable]
        internal class RunConfigData : IHostSpecificRunConfigurationData, IXmlTestStore, IXmlTestStoreCustom
        {
            private const string RegistryHiveAttributeName = "registryHive";
            private const string XmlNamespaceUri = "http://microsoft.com/schemas/TC/TCTestHostAdapters";
            private const string XmlElementName = "VsIdeTestHostRunConfig";

            /// <summary>
            /// The registry hive of VS to use for the VS instance to start.
            /// This field is persisted in the .TestRunConfig file.
            /// </summary>
            private string _registryHive;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="registryHive">The registry hive to use settings from for new Visual Studio instance.</param>
            internal RunConfigData(string registryHive)
            {
                _registryHive = registryHive;  // null is OK. null means get latest version.
            }

            /// <summary>
            /// The description of this host to use in Run Config dialog.
            /// </summary>
            public string RunConfigurationInformation
            {
                get 
                {
                    return "VsIdeHostAdapter Test Host Configuration Data"; 
                }
            }

            /// <summary>
            /// Implements ICloneable.Clone.
            /// </summary>
            public object Clone()
            {
                return new RunConfigData(_registryHive);
            }

            /// <summary>
            /// The registry hive to use settings from for new Visual Studio instance.
            /// </summary>
            internal string RegistryHive
            {
                get { return _registryHive; }
                set { _registryHive = value; }
            }

            public void Load(XmlElement element, XmlTestStoreParameters parameters)
            {
                this.RegistryHive = element.GetAttribute(RegistryHiveAttributeName);
            }

            public void Save(XmlElement element, XmlTestStoreParameters parameters)
            {
                element.SetAttribute(RegistryHiveAttributeName, this.RegistryHive);
            }


            public string ElementName
            {
                get { return XmlElementName; }
            }

            public string NamespaceUri
            {
                get { return XmlNamespaceUri; }
            }

        }
        /// <summary>
        /// Helper class for VS registry.
        /// Used by both Host Adapter and UI side.
        /// </summary>
        internal static class VsRegistry
        {
            private const string ProcessName = "devenv.exe";
            internal const string VSRegistryRoot = @"SOFTWARE\Microsoft\VisualStudio";

            /// <summary>
            /// Obtains all installed Visual Studio versions.
            /// </summary>
            internal static List<string> GetVersions()
            {
                List<string> versions = new List<string>();
                GetVersionsHelper(versions);
                return versions;
            }

            /// <summary>
            /// Returns max version without suffix.
            /// </summary>
            internal static string GetDefaultVersion()
            {
                return VSUtility.Version;
            }

            internal static bool UserSettingsArchiveExists(string registryHive)
            {
                // This must be a key that does not get set if you start up, hit the no settings prompt and select 
                // "Exit Visual Studio", but does get set if you select a default
#if DEV12_OR_LATER
                const string SettingsMarkerKey = @"General\\StartPage";
#else
                const string SettingsMarkerKey = @"StartPage";
#endif

                string versionKeyName = VSRegistryRoot + @"\" + registryHive;
                using (RegistryKey hive = Registry.CurrentUser.OpenSubKey(versionKeyName))
                {
                    if (hive == null)
                    {
                        return false;
                    }
                    using (RegistryKey settingsMarker = hive.OpenSubKey(SettingsMarkerKey))
                    {
                        return settingsMarker != null;
                    }
                }
            }


            /// <summary>
            /// Returns location of devenv.exe on disk.
            /// </summary>
            /// <param name="registryHive">The registry hive (version) of Visual Studio to get location for.</param>
            internal static string GetVsLocation(string registryHive)
            {
                Contract.Assert(!string.IsNullOrEmpty(registryHive));

                string versionKeyName = VSRegistryRoot + @"\" + registryHive;

                string installDir = null; 
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(versionKeyName))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("InstallDir");
                        installDir = value as String;
                    }
                }
                if (string.IsNullOrEmpty(installDir))
                {
                    throw new VsIdeTestHostException(string.Format(CultureInfo.InvariantCulture, "Cannot find installation of Visual Studio in '{0}' registry hive.", registryHive));
                }

                return Path.Combine(installDir, ProcessName);
            }

            /// <summary>
            /// Obtains installed Visual Studio versions and default version.
            /// </summary>
            /// <param name="versions">If null, this is ignored.</param>
            /// <returns>Returns default version = max version without suffix.</returns>
            private static string GetVersionsHelper(List<string> versions)
            {
                // Default is the latest version without suffix, like 10.0.
                string defaultVersion = null;
                Regex versionNoSuffixRegex = new Regex(@"^[0-9]+\.[0-9]+$");

                // Note that the version does not have to be numeric only: can be 10.0Exp.
                using (RegistryKey vsKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio"))
                {
                    foreach (string versionKeyName in vsKey.GetSubKeyNames())
                    {
                        // If there's no InstallDir subkey we skip this key.
                        using (RegistryKey versionKey = vsKey.OpenSubKey(versionKeyName))
                        {
                            if (versionKey.GetValue("InstallDir") == null)
                            {
                                continue;
                            }
                            if (versions != null)
                            {
                                versions.Add(versionKeyName);
                            }
                        }

                        if (versionNoSuffixRegex.Match(versionKeyName).Success &&
                            string.Compare(versionKeyName, defaultVersion, StringComparison.OrdinalIgnoreCase) > 0) // null has the smallest value.
                        {
                            defaultVersion = versionKeyName;
                        }
                    }
                }

                return defaultVersion;
            }
        }
    }
}

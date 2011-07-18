/***************************************************************************

Copyright (c) Microsoft Corporation. All rights reserved.
This code is licensed under the Visual Studio SDK license terms.
THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

***************************************************************************/
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Lifetime;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using EnvDTE;
using Microsoft.Win32;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.TestTools.Common;
using Microsoft.TC.TestHostAdapters;
using Process = System.Diagnostics.Process;  // Ambiguos with EnvDte.Process.

namespace Microsoft.TC.TestHostAdapters
{
    #region VsIdeStartupInfo
    /// <summary>
    /// The information to start Visual Studio.
    /// </summary>
    internal class VsIdeStartupInfo
    {
        private string m_registryHive;
        private string m_workingDirectory;

        internal VsIdeStartupInfo(string registryHive, string workingDirectory)
        {
            // Note: registryHive can be null when using the attribute. That's OK, VsIde will figure out.
            Debug.Assert(!string.IsNullOrEmpty(workingDirectory));

            m_registryHive = registryHive;
            m_workingDirectory = workingDirectory;
        }

        /// <summary>
        /// Hive name under Microsoft.VisualStudio, like 10.0Exp.
        /// </summary>
        internal string RegistryHive
        {
            get { return m_registryHive; }
            set { m_registryHive = value; } 
        }

        /// <summary>
        /// Working directory for devenv.exe process.
        /// </summary>
        internal string WorkingDirectory
        {
            get { return m_workingDirectory; }
        }
    }
    #endregion

    #region VisualStudioIde
    /// <summary>
    /// This wraps Visual Studio DTE (automation object).
    /// </summary>
    internal class VisualStudioIde : IDisposable
    {
        #region Fields
        private const string BaseProgId = "VisualStudio.DTE";

        /// <summary>
        /// How long to wait for IDE to appear in ROT. 
        /// </summary>
        private static readonly TimeSpan s_ideStartupTimeout = TimeSpan.FromMilliseconds(RegistrySettings.BaseTimeout * 120);

        /// <summary>
        /// How long to wait before killing devenv.exe after Dispose() is called. During this time VS can e.g. save buffers to disk.
        /// </summary>
        private static readonly TimeSpan s_ideExitTimeout = TimeSpan.FromMilliseconds(RegistrySettings.BaseTimeout * 5);

        /// <summary>
        /// Timeout to wait while VS rejects calls.
        /// </summary>
        private static readonly TimeSpan s_rejectedCallTimeout = TimeSpan.FromMilliseconds(RegistrySettings.BaseTimeout * 30);

        // HRESULTs for COM errors.
        private const int CallRejectedByCalleeErrorCode = -2147418111;

        private DTE m_dte;
        private System.Diagnostics.Process m_process;
        private object m_cleanupLock = new object();
        #endregion

        #region Constructor/Finalizer
        /// <summary>
        /// Constructor. Starts new instance of VS IDE.
        /// </summary>
        public VisualStudioIde(VsIdeStartupInfo info)
        {
            Debug.Assert(info != null);

            if (string.IsNullOrEmpty(info.RegistryHive))
            {
                info.RegistryHive = VsRegistry.GetDefaultVersion();
                if (string.IsNullOrEmpty(info.RegistryHive))
                {                
                    // Please no Debug.Assert. This is a valid case.
                    throw new VsIdeTestHostException(string.Format(CultureInfo.InvariantCulture, Resources.CannotFindVSInstallation, info.RegistryHive));
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
        #endregion

        #region Properties
        public DTE Dte
        {
            get { return m_dte; }
        }

        public Process Process
        {
            get { return m_process; }
        }

        public event VsIdeHostErrorHandler ErrorHandler;
        #endregion

        #region Private
        /// <summary>
        /// Create a Visual Studio process.
        /// </summary>
        /// <param name="info">Startup information.</param>
        private void StartNewInstance(VsIdeStartupInfo startupInfo)
        {
            try
            {
                Debug.Assert(startupInfo != null);
                Debug.Assert(m_process == null, "VisualStudioIde.StartNewInstance: m_process should be null!");

                Process process = new Process();
                process.StartInfo.UseShellExecute = false;
                if (startupInfo.WorkingDirectory != null)
                {
                    process.StartInfo.WorkingDirectory = startupInfo.WorkingDirectory;
                }

                process.StartInfo.FileName = VsRegistry.GetVsLocation(startupInfo.RegistryHive);
                Debug.Assert(!string.IsNullOrEmpty(process.StartInfo.FileName));

                // Note that this needs to be partial (not $-terminated) as we partially match/replace.
                Regex versionRegex = new Regex(@"^[0-9]+\.[0-9]+");

                string hiveVersion = versionRegex.Match(startupInfo.RegistryHive).Value;
                string hiveSuffix = versionRegex.Replace(startupInfo.RegistryHive, string.Empty);

                if (!string.IsNullOrEmpty(hiveSuffix))
                {
                    process.StartInfo.Arguments = "/RootSuffix " + hiveSuffix;
                }

                process.Exited += new EventHandler(ProcessExited);
                process.EnableRaisingEvents = true;

                if (!process.Start())
                {
                    throw new VsIdeTestHostException(Resources.FailedToStartVSProcess);
                }

                m_process = process;

                string progId = string.Format(CultureInfo.InvariantCulture, "{0}.{1}", VisualStudioIde.BaseProgId, hiveVersion);
                m_dte = GetDteFromRot(progId, m_process.Id);
                if (m_dte == null)
                {
                    throw new VsIdeTestHostException(Resources.FailedToGetDte);
                }
            }
            catch (Exception ex)
            {
                Debug.Fail("VsIde.StartNewInstance: " + ex.ToString());
                throw;
            }
        }

        /// <summary>
        /// Obtains Visual Studio automation object from Running Object Table.
        /// </summary>
        /// <param name="progId">DTE's prog id.</param>
        /// <param name="processId">Visual Studio process id to obtain the automation object for.</param>
        /// <returns>Visual Studio automation object.</returns>
        private static DTE GetDteFromRot(string progId, int processId)
        {
            Debug.Assert(!string.IsNullOrEmpty(progId));

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
                System.Threading.Thread.Sleep(RegistrySettings.BaseSleepDuration * 2);
            } while (sw.Elapsed < s_ideStartupTimeout);

            if (dte == null)
            {
                throw new VsIdeTestHostException(Resources.TimedOutGettingDteFromRot);
            }
            return dte;
        }

        /// <summary>
        /// Obtains Visual Studio automation object from Running Object Table.
        /// </summary>
        /// <param name="monikerName">The moniker to use as a filter when looking in Running Object Table.</param>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "Microsoft.TC.TestHostAdapters.NativeMethods.GetRunningObjectTable(System.Int32,Microsoft.VisualStudio.OLE.Interop.IRunningObjectTable@)")]
        [SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "Microsoft.VisualStudio.OLE.Interop.IEnumMoniker.Reset")]
        [SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "Microsoft.TC.TestHostAdapters.NativeMethods.CreateBindCtx(System.Int32,Microsoft.VisualStudio.OLE.Interop.IBindCtx@)")]
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        private static DTE GetDteFromRot(string monikerName)
        {
            Debug.Assert(!string.IsNullOrEmpty(monikerName));

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
            lock (m_cleanupLock)
            {
                m_process.EnableRaisingEvents = false;
                m_process.Exited -= new EventHandler(ProcessExited);

                if (ErrorHandler != null)
                {
                    ErrorHandler(Resources.VSExitedUnexpectedly, TestOutcome.Error, true);
                }
            }
        }
        #endregion

        #region IDisposable
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
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        private void Dispose(bool explicitDispose)
        {
            if (!explicitDispose)
            {
                // When called from finalizer, just clean up. Don't lock. Don't throw.
                KillProcess();
            }
            else
            {
                lock (m_cleanupLock)
                {
                    if (m_process.EnableRaisingEvents)
                    {
                        m_process.EnableRaisingEvents = false;
                        m_process.Exited -= new EventHandler(ProcessExited);
                    }

                    try
                    {
                        if (m_dte != null)
                        {
                            // Visual Studio sometimes rejects the call to Quit() so we need to retry it.
                            Stopwatch sw = Stopwatch.StartNew();
                            bool timedOut = true;
                            do
                            {
                                try
                                {
                                    m_dte.Quit();
                                    timedOut = false;
                                    break;
                                }
                                catch (COMException ex)
                                {
                                    if (ex.ErrorCode == CallRejectedByCalleeErrorCode)
                                    {
                                        System.Threading.Thread.Sleep(RegistrySettings.BaseSleepDuration * 2);
                                    }
                                    else
                                    {
                                        Debug.Assert(!RegistrySettings.VerboseAssertionsEnabled, ex.ToString());
                                        throw;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.Assert(!RegistrySettings.VerboseAssertionsEnabled, ex.ToString());
                                    throw;
                                }
                            } while (sw.Elapsed < s_rejectedCallTimeout);

                            if (timedOut)
                            {
                                throw new VsIdeTestHostException(Resources.TimedOutWaitingDteQuit);
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
            if (m_process != null)
            {
                // wait for the specified time for the IDE to exit.  
                // If it hasn't, kill the process so we can proceed to the next test.
                Stopwatch sw = Stopwatch.StartNew();
                while (!m_process.HasExited && (sw.Elapsed < s_ideExitTimeout))
                {
                    System.Threading.Thread.Sleep(RegistrySettings.BaseSleepDuration);
                }

                if (!m_process.HasExited)
                {
                    m_process.Kill();
                }

                m_process = null;
            }
        }
        #endregion
    }
    #endregion

    #region VsIdeHostErrorHandler
    /// <summary>
    /// Used for error reporting.
    /// </summary>
    /// <param name="errorMessage">Error message.</param>
    /// <param name="outcome">The outcome for the test due to this error.</param>
    /// <param name="abortTestRun">Whether the error causes test run to abort.</param>
    internal delegate void VsIdeHostErrorHandler(string errorMessage, TestOutcome outcome, bool abortTestRun);
    #endregion
}

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
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.PythonTools.Debugger.Remote;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Uwp.Project {
    /// <summary>
    /// Merges the PTVS IVsCfg object with the Venus IVsCfg implementation redirecting
    /// things appropriately to either one.
    /// </summary>
    class PythonUwpProjectConfig :
        IVsCfg,
        IVsProjectCfg,
        IVsProjectCfg2,
        IVsProjectFlavorCfg,
        IVsDebuggableProjectCfg,
        ISpecifyPropertyPages,
        IVsSpecifyProjectDesignerPages,
        IVsCfgBrowseObject,
        IVsDeployableProjectCfg,
        IVsProjectCfgDebugTargetSelection,
        IVsQueryDebuggableProjectCfg,
        IVsQueryDebuggableProjectCfg2,
        IVsAppContainerProjectDeployCallback {
        private readonly IVsCfg _pythonCfg;
        private readonly IVsProjectFlavorCfg _uwpCfg;
        private readonly object syncObject = new object();
        private EventSinkCollection deployCallbackCollection = new EventSinkCollection();
        private IVsAppContainerProjectDeployOperation deployOp;
        private IVsTask appContainerBootstrapperOperation;
        private IVsOutputWindowPane outputWindow = null;
        private IVsDebuggerDeployConnection connection = null;
        private string deployPackageMoniker;
        private string deployAppUserModelID;

        private const string RemoteTarget = "Remote Device";
        private const string DefaultRemoteDebugPort = "5678";

        public PythonUwpProjectConfig(IVsCfg pythonCfg, IVsProjectFlavorCfg uwpConfig) {
            _pythonCfg = pythonCfg;
            _uwpCfg = uwpConfig;
        }

        internal IVsHierarchy PythonConfig {
            get {
                IVsHierarchy proj = null;

                var browseObj = _pythonCfg as IVsCfgBrowseObject;

                if (browseObj != null) {
                    uint itemId = 0;

                    browseObj.GetProjectItem(out proj, out itemId);
                }
                return proj;
            }
        }

        internal string LayoutDir {
            get; private set;
        }

        internal string DeployPackageMoniker {
            get { return this.deployPackageMoniker; }
        }

        internal string DeployAppUserModelID {
            get { return this.deployAppUserModelID; }
        }

        private async System.Threading.Tasks.Task RemoteProcessAttachAsync(string remoteMachine, string secret, string port, string sourceDir, string targetDir) {
            bool attached = false;
            bool stoppedDebugging = false;
            const int attachRetryLimit = 10;
            int attachRetryCount = 0;

            var qualifierString = string.Format(
                "tcp://{0}@{1}:{2}?{3}={4}&{5}={6}&{7}={8}",
                secret,
                remoteMachine,
                port,
                AD7Engine.SourceDirectoryKey,
                HttpUtility.UrlEncode(sourceDir),
                AD7Engine.TargetDirectoryKey,
                HttpUtility.UrlEncode(targetDir),
                AD7Engine.TargetHostType,
                HttpUtility.UrlEncode(AD7Engine.TargetUwp));

            var dte = (EnvDTE.DTE)Package.GetGlobalService(typeof(EnvDTE.DTE));
            var debugger = (EnvDTE100.Debugger5)dte.Debugger;

            var transport = default(EnvDTE80.Transport);
            var transports = debugger.Transports;

            for (int i = 1; i <= transports.Count; ++i) {
                var t = transports.Item(i);
                Guid tid;
                if (Guid.TryParse(t.ID, out tid) && tid == PythonRemoteDebugPortSupplier.PortSupplierGuid) {
                    transport = t;
                    break;
                }
            }

            System.Diagnostics.Debug.Assert(transport != null, "Python remote debugging transport is missing.");

            if (transport == null) {
                return;
            }

            while (!attached && attachRetryCount++ < attachRetryLimit) {
                try {
                    if (debugger.DebuggedProcesses == null || debugger.DebuggedProcesses.Count == 0) {
                        // We are no longer debugging, so just bail
                        stoppedDebugging = true;
                        break;
                    } else if (debugger.CurrentMode == EnvDTE.dbgDebugMode.dbgBreakMode) {
                        attachRetryCount--;
                        await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(1));
                        continue;
                    }

                    var processes = debugger.GetProcesses(transport, qualifierString);

                    if (processes.Count > 0) {
                        foreach (EnvDTE90.Process3 process in processes) {
                            process.Attach();
                            attached = true;
                            break;
                        }

                        if (attached) {
                            IVsDebugger vsDebugger = Package.GetGlobalService(typeof(SVsShellDebugger)) as IVsDebugger;

                            if (vsDebugger != null) {
                                vsDebugger.UnadviseDebugEventCallback(Debugger.PythonRemoteDebugEvents.Instance);
                            }

                            break;
                        }
                    }
                } catch (COMException comException) {
                    // In the case where the debug client does not setup the PTVSD server in time for the Attach to work, we will
                    // get this exception.  We retry a few times to ensure client has time to start the Python debug server
                    System.Diagnostics.Debug.WriteLine("Non-fatal failure during attach to remote Python process:\r\n{0}", comException);
                }

                await System.Threading.Tasks.Task.Delay(TimeSpan.FromMilliseconds(100));
            }

            if (!attached && !stoppedDebugging) {
                MessageBox.Show("Could not attach to remote Python debug session.", null, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #region IVsCfg Members

        public int get_DisplayName(out string pbstrDisplayName) {
            int ret = _pythonCfg.get_DisplayName(out pbstrDisplayName);

            return ret;
        }

        public int get_IsDebugOnly(out int pfIsDebugOnly) {
            return _pythonCfg.get_IsDebugOnly(out pfIsDebugOnly);
        }

        public int get_IsReleaseOnly(out int pfIsReleaseOnly) {
            return _pythonCfg.get_IsReleaseOnly(out pfIsReleaseOnly);
        }

        #endregion

        #region IVsProjectCfg Members

        public int EnumOutputs(out IVsEnumOutputs ppIVsEnumOutputs) {
            IVsProjectCfg projCfg = _uwpCfg as IVsProjectCfg;
            if (projCfg != null) {
                return projCfg.EnumOutputs(out ppIVsEnumOutputs);
            }
            ppIVsEnumOutputs = null;
            return VSConstants.E_NOTIMPL;
        }

        public int OpenOutput(string szOutputCanonicalName, out IVsOutput ppIVsOutput) {
            IVsProjectCfg projCfg = _uwpCfg as IVsProjectCfg;
            if (projCfg != null) {
                return projCfg.OpenOutput(szOutputCanonicalName, out ppIVsOutput);
            }
            ppIVsOutput = null;
            return VSConstants.E_NOTIMPL;
        }

        public int get_BuildableProjectCfg(out IVsBuildableProjectCfg ppIVsBuildableProjectCfg) {
            IVsProjectCfg projCfg = _uwpCfg as IVsProjectCfg;
            if (projCfg != null) {
                return projCfg.get_BuildableProjectCfg(out ppIVsBuildableProjectCfg);
            }
            ppIVsBuildableProjectCfg = null;
            return VSConstants.E_NOTIMPL;
        }

        public int get_CanonicalName(out string pbstrCanonicalName) {
            IVsProjectCfg projCfg = _uwpCfg as IVsProjectCfg;
            if (projCfg != null) {
                return projCfg.get_CanonicalName(out pbstrCanonicalName);
            }
            pbstrCanonicalName = null;
            return VSConstants.E_NOTIMPL;
        }

        public int get_IsPackaged(out int pfIsPackaged) {
            IVsProjectCfg projCfg = _uwpCfg as IVsProjectCfg;
            if (projCfg != null) {
                return projCfg.get_IsPackaged(out pfIsPackaged);
            }
            pfIsPackaged = 0;
            return VSConstants.E_NOTIMPL;
        }

        public int get_IsSpecifyingOutputSupported(out int pfIsSpecifyingOutputSupported) {
            IVsProjectCfg projCfg = _uwpCfg as IVsProjectCfg;
            if (projCfg != null) {
                return projCfg.get_IsSpecifyingOutputSupported(out pfIsSpecifyingOutputSupported);
            }
            pfIsSpecifyingOutputSupported = 0;
            return VSConstants.E_NOTIMPL;
        }

        public int get_Platform(out Guid pguidPlatform) {
            IVsProjectCfg projCfg = _uwpCfg as IVsProjectCfg;
            if (projCfg != null) {
                return projCfg.get_Platform(out pguidPlatform);
            }
            pguidPlatform = Guid.Empty;
            return VSConstants.E_NOTIMPL;
        }

        public int get_ProjectCfgProvider(out IVsProjectCfgProvider ppIVsProjectCfgProvider) {
            IVsProjectCfg projCfg = _uwpCfg as IVsProjectCfg;
            if (projCfg != null) {
                return projCfg.get_ProjectCfgProvider(out ppIVsProjectCfgProvider);
            }
            ppIVsProjectCfgProvider = null;
            return VSConstants.E_NOTIMPL;
        }

        public int get_RootURL(out string pbstrRootURL) {
            IVsProjectCfg projCfg = _uwpCfg as IVsProjectCfg;
            if (projCfg != null) {
                return projCfg.get_RootURL(out pbstrRootURL);
            }
            pbstrRootURL = null;
            return VSConstants.E_NOTIMPL;
        }

        public int get_TargetCodePage(out uint puiTargetCodePage) {
            IVsProjectCfg projCfg = _uwpCfg as IVsProjectCfg;
            if (projCfg != null) {
                return projCfg.get_TargetCodePage(out puiTargetCodePage);
            }
            puiTargetCodePage = 0;
            return VSConstants.E_NOTIMPL;
        }

        public int get_UpdateSequenceNumber(ULARGE_INTEGER[] puliUSN) {
            IVsProjectCfg projCfg = _uwpCfg as IVsProjectCfg;
            if (projCfg != null) {
                return projCfg.get_UpdateSequenceNumber(puliUSN);
            }
            return VSConstants.E_NOTIMPL;
        }

        #endregion

        #region IVsProjectCfg2 Members

        public int OpenOutputGroup(string szCanonicalName, out IVsOutputGroup ppIVsOutputGroup) {
            IVsProjectCfg2 projCfg = _pythonCfg as IVsProjectCfg2;
            if (projCfg != null) {
                return projCfg.OpenOutputGroup(szCanonicalName, out ppIVsOutputGroup);
            }
            ppIVsOutputGroup = null;
            return VSConstants.E_NOTIMPL;
        }

        public int OutputsRequireAppRoot(out int pfRequiresAppRoot) {
            IVsProjectCfg2 projCfg = _pythonCfg as IVsProjectCfg2;
            if (projCfg != null) {
                return projCfg.OutputsRequireAppRoot(out pfRequiresAppRoot);
            }
            pfRequiresAppRoot = 1;
            return VSConstants.E_NOTIMPL;
        }

        public int get_CfgType(ref Guid iidCfg, out IntPtr ppCfg) {
            if (iidCfg == typeof(IVsDebuggableProjectCfg).GUID) {
                ppCfg = Marshal.GetComInterfaceForObject(this, typeof(IVsDebuggableProjectCfg));
                return VSConstants.S_OK;
            }

            if (iidCfg == typeof(IVsDeployableProjectCfg).GUID) {
                ppCfg = Marshal.GetComInterfaceForObject(this, typeof(IVsDeployableProjectCfg));
                return VSConstants.S_OK;
            }

            if (iidCfg == typeof(IVsQueryDebuggableProjectCfg).GUID) {
                ppCfg = Marshal.GetComInterfaceForObject(this, typeof(IVsQueryDebuggableProjectCfg));
                return VSConstants.S_OK;
            }

            var projCfg = _uwpCfg as IVsProjectFlavorCfg;
            if (projCfg != null) {
                return projCfg.get_CfgType(ref iidCfg, out ppCfg);
            }
            ppCfg = IntPtr.Zero;
            return VSConstants.E_NOTIMPL;
        }

        public int get_IsPrivate(out int pfPrivate) {
            IVsProjectCfg2 projCfg = _pythonCfg as IVsProjectCfg2;
            if (projCfg != null) {
                return projCfg.get_IsPrivate(out pfPrivate);
            }
            pfPrivate = 0;
            return VSConstants.E_NOTIMPL;
        }

        public int get_OutputGroups(uint celt, IVsOutputGroup[] rgpcfg, uint[] pcActual = null) {
            IVsProjectCfg2 projCfg = _pythonCfg as IVsProjectCfg2;
            if (projCfg != null) {
                return projCfg.get_OutputGroups(celt, rgpcfg, pcActual);
            }
            return VSConstants.E_NOTIMPL;
        }

        public int get_VirtualRoot(out string pbstrVRoot) {
            IVsProjectCfg2 projCfg = _pythonCfg as IVsProjectCfg2;
            if (projCfg != null) {
                return projCfg.get_VirtualRoot(out pbstrVRoot);
            }
            pbstrVRoot = null;
            return VSConstants.E_NOTIMPL;
        }

        #endregion

        #region IVsProjectFlavorCfg Members

        public int Close() {
            IVsProjectFlavorCfg cfg = _uwpCfg as IVsProjectFlavorCfg;
            if (cfg != null) {
                return cfg.Close();
            }
            return VSConstants.S_OK;
        }

        #endregion

        #region IVsDebuggableProjectCfg Members

        public int DebugLaunch(uint grfLaunch) {
            if (PythonConfig.IsAppxPackageableProject()) {
                VsDebugTargetInfo2[] targets;
                int hr = QueryDebugTargets(out targets);
                if (ErrorHandler.Failed(hr))
                    return hr;

                VsDebugTargetInfo4[] appPackageDebugTarget = new VsDebugTargetInfo4[1];
                int targetLength = (int)Marshal.SizeOf(typeof(VsDebugTargetInfo4));

                // Setup the app-specific parameters
                appPackageDebugTarget[0].AppPackageLaunchInfo.AppUserModelID = DeployAppUserModelID;
                appPackageDebugTarget[0].AppPackageLaunchInfo.PackageMoniker = DeployPackageMoniker;
                appPackageDebugTarget[0].AppPackageLaunchInfo.AppPlatform = VsAppPackagePlatform.APPPLAT_WindowsAppx;

#if DEV14_OR_LATER
                // Check if this project contains a startup task and set launch flag appropriately
                IVsBuildPropertyStorage bps = (IVsBuildPropertyStorage)this.PythonConfig;
                string canonicalName;
                string containsStartupTaskValue = null;
                bool containsStartupTask = false;

                get_CanonicalName(out canonicalName);

                bps.GetPropertyValue("ContainsStartupTask", canonicalName, (uint)_PersistStorageType.PST_PROJECT_FILE, out containsStartupTaskValue);

                if (containsStartupTaskValue != null && bool.TryParse(containsStartupTaskValue, out containsStartupTask) && containsStartupTask) {
                    grfLaunch |= (uint)__VSDBGLAUNCHFLAGS140.DBGLAUNCH_ContainsStartupTask;
                }
#endif

                appPackageDebugTarget[0].dlo = (uint)_DEBUG_LAUNCH_OPERATION4.DLO_AppPackageDebug;
                appPackageDebugTarget[0].LaunchFlags = grfLaunch;
                appPackageDebugTarget[0].bstrRemoteMachine = targets[0].bstrRemoteMachine;
                appPackageDebugTarget[0].bstrExe = targets[0].bstrExe;
                appPackageDebugTarget[0].bstrArg = targets[0].bstrArg;
                appPackageDebugTarget[0].bstrCurDir = targets[0].bstrCurDir;
                appPackageDebugTarget[0].bstrEnv = targets[0].bstrEnv;
                appPackageDebugTarget[0].dwProcessId = targets[0].dwProcessId;
                appPackageDebugTarget[0].pStartupInfo = IntPtr.Zero;
                appPackageDebugTarget[0].guidLaunchDebugEngine = targets[0].guidLaunchDebugEngine;
                appPackageDebugTarget[0].dwDebugEngineCount = targets[0].dwDebugEngineCount;
                appPackageDebugTarget[0].pDebugEngines = targets[0].pDebugEngines;
                appPackageDebugTarget[0].guidPortSupplier = targets[0].guidPortSupplier;

                appPackageDebugTarget[0].bstrPortName = targets[0].bstrPortName;
                appPackageDebugTarget[0].bstrOptions = targets[0].bstrOptions;
                appPackageDebugTarget[0].fSendToOutputWindow = targets[0].fSendToOutputWindow;
                appPackageDebugTarget[0].pUnknown = targets[0].pUnknown;
                appPackageDebugTarget[0].guidProcessLanguage = targets[0].guidProcessLanguage;
                appPackageDebugTarget[0].project = PythonConfig;

                // Pass the debug launch targets to the debugger
                IVsDebugger4 debugger4 = (IVsDebugger4)Package.GetGlobalService(typeof(SVsShellDebugger));

                VsDebugTargetProcessInfo[] results = new VsDebugTargetProcessInfo[1];

                IVsDebugger debugger = (IVsDebugger)debugger4;

                // Launch task to monitor to attach to Python remote process
                var sourceDir = Path.GetFullPath(PythonConfig.GetProjectProperty("ProjectDir")).Trim('\\');
                var targetDir = Path.GetFullPath(this.LayoutDir).Trim('\\');
                var debugPort = PythonConfig.GetProjectProperty("RemoteDebugPort") ?? DefaultRemoteDebugPort;
                var debugId = Guid.NewGuid();
                var serializer = new JavaScriptSerializer();
                var debugCmdJson = serializer.Serialize(new string[] { "visualstudio_py_remote_launcher.py", debugPort.ToString(), debugId.ToString(), "--redirect-output" });

                Debugger.PythonRemoteDebugEvents.Instance.RemoteDebugCommandInfo = Encoding.Unicode.GetBytes(debugCmdJson);
                Debugger.PythonRemoteDebugEvents.Instance.AttachRemoteProcessFunction = () => {
                    return RemoteProcessAttachAsync(
                         appPackageDebugTarget[0].bstrRemoteMachine,
                         debugId.ToString(),
                         debugPort,
                         sourceDir,
                         targetDir);
                };

                int result = debugger.AdviseDebugEventCallback(Debugger.PythonRemoteDebugEvents.Instance);

                if (result == VSConstants.S_OK) {
                    debugger4.LaunchDebugTargets4(1, appPackageDebugTarget, results);
                } else {
                    Debug.Fail(string.Format(CultureInfo.CurrentCulture, "Failure {0}", result));
                }

                return result;
            } else {
                IVsDebuggableProjectCfg cfg = _pythonCfg as IVsDebuggableProjectCfg;
                if (cfg != null) {
                    return cfg.DebugLaunch(grfLaunch);
                }
                return VSConstants.E_NOTIMPL;
            }
        }

        public int QueryDebugLaunch(uint grfLaunch, out int pfCanLaunch) {
            pfCanLaunch = this.PythonConfig.IsAppxPackageableProject() ? 1 : 0;
            return VSConstants.S_OK;
        }

        #endregion

        #region ISpecifyPropertyPages Members

        public void GetPages(CAUUID[] pPages) {
            var cfg = _pythonCfg as ISpecifyPropertyPages;
            if (cfg != null) {
                cfg.GetPages(pPages);
            }
        }

        #endregion

        #region IVsSpecifyProjectDesignerPages Members

        public int GetProjectDesignerPages(CAUUID[] pPages) {
            var cfg = _pythonCfg as IVsSpecifyProjectDesignerPages;
            if (cfg != null) {
                return cfg.GetProjectDesignerPages(pPages);
            }
            return VSConstants.E_NOTIMPL;
        }

        #endregion

        #region IVsCfgBrowseObject Members

        public int GetCfg(out IVsCfg ppCfg) {
            ppCfg = this;
            return VSConstants.S_OK;
        }

        public int GetProjectItem(out IVsHierarchy pHier, out uint pItemid) {
            var cfg = _pythonCfg as IVsCfgBrowseObject;
            if (cfg != null) {
                return cfg.GetProjectItem(out pHier, out pItemid);
            }
            pHier = null;
            pItemid = 0;
            return VSConstants.E_NOTIMPL;
        }

        #endregion


        #region IVsDeployableProjectCfg Members

        public int AdviseDeployStatusCallback(IVsDeployStatusCallback pIVsDeployStatusCallback, out uint pdwCookie) {
            if (pIVsDeployStatusCallback == null) {
                pdwCookie = 0;
                return VSConstants.E_UNEXPECTED;
            }

            lock (syncObject) {
                pdwCookie = deployCallbackCollection.Add(pIVsDeployStatusCallback);
            }

            return VSConstants.S_OK;
        }

        public int Commit(uint dwReserved) {
            return VSConstants.S_OK;
        }

        public int QueryStartDeploy(uint dwOptions, int[] pfSupported, int[] pfReady) {
            if (pfSupported.Length > 0) {
                // Only Appx package producing appcontainer projects should support deployment
                pfSupported[0] = 1;
            }

            if (pfReady.Length > 0) {
                lock (syncObject) {
                    pfReady[0] = (deployOp == null && appContainerBootstrapperOperation == null) ? 1 : 0;
                }
            }

            return VSConstants.S_OK;
        }

        public int QueryStatusDeploy(out int pfDeployDone) {
            lock (syncObject) {
                pfDeployDone = (deployOp == null && appContainerBootstrapperOperation == null) ? 1 : 0;
            }

            return VSConstants.S_OK;
        }

        public int Rollback(uint dwReserved) {
            return VSConstants.S_OK;
        }

        public int StartDeploy(IVsOutputWindowPane pIVsOutputWindowPane, uint dwOptions) {
            int continueOn = 1;

            outputWindow = pIVsOutputWindowPane;

            if (connection != null) {
                lock (syncObject) {
                    if (connection != null) {
                        connection.Dispose();
                        connection = null;
                    }
                }
            }

            // Loop through deploy status callbacks
            foreach (IVsDeployStatusCallback callback in deployCallbackCollection) {
                if (ErrorHandler.Failed(callback.OnStartDeploy(ref continueOn))) {
                    continueOn = 0;
                }

                if (continueOn == 0) {
                    outputWindow = null;
                    return VSConstants.E_ABORT;
                }
            }

            try {
                VsDebugTargetInfo2[] targets;
                uint deployFlags = (uint)(_AppContainerDeployOptions.ACDO_NetworkLoopbackEnable | _AppContainerDeployOptions.ACDO_SetNetworkLoopback);
                string recipeFile = null;
                string layoutDir = null;
                var pythonProject = PythonConfig;
                IVsBuildPropertyStorage bps = (IVsBuildPropertyStorage)pythonProject;
                int hr = QueryDebugTargets(out targets);
                if (ErrorHandler.Failed(hr)) {
                    NotifyEndDeploy(0);
                    return hr;
                }

                string canonicalName;

                get_CanonicalName(out canonicalName);

                bps.GetPropertyValue("AppxPackageRecipe", canonicalName, (uint)_PersistStorageType.PST_PROJECT_FILE, out recipeFile);

                string projectUniqueName = null;
                IVsSolution vsSolution = Package.GetGlobalService(typeof(SVsSolution)) as IVsSolution;
                if (vsSolution != null) {
                    hr = vsSolution.GetUniqueNameOfProject(pythonProject, out projectUniqueName);
                }

                IVsAppContainerProjectDeploy deployHelper = (IVsAppContainerProjectDeploy)Package.GetGlobalService(typeof(SVsAppContainerProjectDeploy));
                if (String.IsNullOrEmpty(targets[0].bstrRemoteMachine)) {
                    bps.GetPropertyValue("LayoutDir", canonicalName, (uint)_PersistStorageType.PST_PROJECT_FILE, out layoutDir);

                    deployOp = deployHelper.StartDeployAsync(deployFlags, recipeFile, layoutDir, projectUniqueName, this);
                } else {
                    IVsDebuggerDeploy deploy = (IVsDebuggerDeploy)Package.GetGlobalService(typeof(SVsShellDebugger));
                    IVsDebuggerDeployConnection deployConnection;

                    hr = deploy.ConnectToTargetComputer(targets[0].bstrRemoteMachine, VsDebugRemoteAuthenticationMode.VSAUTHMODE_None, out deployConnection);
                    if (ErrorHandler.Failed(hr)) {
                        NotifyEndDeploy(0);
                        return hr;
                    }

                    connection = deployConnection;

                    deployOp = deployHelper.StartRemoteDeployAsync(deployFlags, connection, recipeFile, projectUniqueName, this);
                }
            } catch (Exception) {
                if (connection != null) {
                    lock (syncObject) {
                        if (connection != null) {
                            connection.Dispose();
                            connection = null;
                        }
                    }
                }
                connection = null;

                NotifyEndDeploy(0);

                // Rethrow exception
                throw;
            }

            return VSConstants.S_OK;
        }

        public int StopDeploy(int fSync) {
            IVsTask bootstrapOp = null;
            IVsAppContainerProjectDeployOperation deployOp = null;
            int result = VSConstants.S_OK;

            lock (syncObject) {
                bootstrapOp = appContainerBootstrapperOperation;
                deployOp = this.deployOp;
                appContainerBootstrapperOperation = null;
                this.deployOp = null;
            }

            if (bootstrapOp != null) {
                bootstrapOp.Cancel();
                if (fSync != 0) {
                    try {
                        bootstrapOp.Wait();
                    } catch (Exception e) {
                        if (outputWindow != null) {
                            outputWindow.OutputString(e.ToString());
                        }
                        result = VSConstants.E_FAIL;
                    }
                }
            }

            if (deployOp != null) {
                deployOp.StopDeploy(fSync != 0);
            }

            return result;
        }

        int IVsDeployableProjectCfg.UnadviseDeployStatusCallback(uint dwCookie) {
            lock (syncObject) {
                deployCallbackCollection.RemoveAt(dwCookie);
            }

            return VSConstants.S_OK;
        }

        int IVsDeployableProjectCfg.WaitDeploy(uint dwMilliseconds, int fTickWhenMessageQNotEmpty) {
            return VSConstants.S_OK;
        }

        #endregion

        #region IVsAppContainerProjectDeployCallback Members

        void IVsAppContainerProjectDeployCallback.OnEndDeploy(bool successful, string deployedPackageMoniker, string deployedAppUserModelID) {
            try {
                if (successful) {
                    deployPackageMoniker = deployedPackageMoniker;
                    deployAppUserModelID = deployedAppUserModelID;
                    NotifyEndDeploy(1);

                    var result = deployOp.GetDeployResult();

                    this.LayoutDir = result.LayoutFolder;
                } else {
                    deployPackageMoniker = null;
                    deployAppUserModelID = null;
                    NotifyEndDeploy(0);
                }
            } finally {
                lock (syncObject) {
                    deployOp = null;

                    if (connection != null) {
                        connection.Dispose();
                        connection = null;
                    }
                }
            }
        }

        void IVsAppContainerProjectDeployCallback.OutputMessage(string message) {
            if (null != outputWindow) {
                outputWindow.OutputString(message);
            }
        }

        #endregion

        #region IVsQueryDebuggableProjectCfg2 Members
        void IVsQueryDebuggableProjectCfg2.QueryDebugTargets(uint grfLaunch, uint cTargets, VsDebugTargetInfo4[] rgDebugTargetInfo, uint[] pcActual) {
            if (cTargets <= 0) {
                if (pcActual == null) {
                    Marshal.ThrowExceptionForHR(VSConstants.E_POINTER);
                }

                pcActual[0] = 1;
                return;
            }

            if (pcActual != null) {
                pcActual[0] = 0;
            }

            VsDebugTargetInfo2[] targets;
            int hr = QueryDebugTargets(out targets);
            if (ErrorHandler.Failed(hr))
                Marshal.ThrowExceptionForHR(hr);

            int targetLength = (int)Marshal.SizeOf(typeof(VsDebugTargetInfo4));

            rgDebugTargetInfo[0].AppPackageLaunchInfo.AppUserModelID = deployAppUserModelID;
            rgDebugTargetInfo[0].AppPackageLaunchInfo.PackageMoniker = deployPackageMoniker;

            bool isSimulator = GetDebugFlag("UseSimulator", false);

            if (isSimulator && String.IsNullOrEmpty(targets[0].bstrRemoteMachine)) {
                grfLaunch |= (uint)__VSDBGLAUNCHFLAGS6.DBGLAUNCH_StartInSimulator;
            }

            grfLaunch |= (uint)__VSDBGLAUNCHFLAGS.DBGLAUNCH_DetachOnStop;

            bool containsStartupTask = GetDebugFlag("ContainsStartupTask", false);

#if DEV14_OR_LATER
            if (containsStartupTask) {
                grfLaunch |= (uint)__VSDBGLAUNCHFLAGS140.DBGLAUNCH_ContainsStartupTask;
            }
#endif

            rgDebugTargetInfo[0].dlo = (uint)_DEBUG_LAUNCH_OPERATION4.DLO_AppPackageDebug;
            rgDebugTargetInfo[0].LaunchFlags = grfLaunch;
            rgDebugTargetInfo[0].bstrRemoteMachine = targets[0].bstrRemoteMachine;
            rgDebugTargetInfo[0].bstrExe = targets[0].bstrExe;
            rgDebugTargetInfo[0].bstrArg = targets[0].bstrArg;
            rgDebugTargetInfo[0].bstrCurDir = targets[0].bstrCurDir;
            rgDebugTargetInfo[0].bstrEnv = targets[0].bstrEnv;
            rgDebugTargetInfo[0].dwProcessId = targets[0].dwProcessId;
            rgDebugTargetInfo[0].pStartupInfo = IntPtr.Zero;
            rgDebugTargetInfo[0].guidLaunchDebugEngine = targets[0].guidLaunchDebugEngine;
            rgDebugTargetInfo[0].dwDebugEngineCount = targets[0].dwDebugEngineCount;
            rgDebugTargetInfo[0].pDebugEngines = targets[0].pDebugEngines;
            rgDebugTargetInfo[0].guidPortSupplier = targets[0].guidPortSupplier;
            rgDebugTargetInfo[0].bstrPortName = targets[0].bstrPortName;
            rgDebugTargetInfo[0].bstrOptions = targets[0].bstrOptions;
            rgDebugTargetInfo[0].fSendToOutputWindow = targets[0].fSendToOutputWindow;
            rgDebugTargetInfo[0].pUnknown = targets[0].pUnknown;
            rgDebugTargetInfo[0].guidProcessLanguage = targets[0].guidProcessLanguage;

            if (pcActual != null) {
                pcActual[0] = 1;
            }
        }
        #endregion

        #region IVsProjectCfgDebugTargetSelection Members
        void IVsProjectCfgDebugTargetSelection.GetCurrentDebugTarget(out Guid pguidDebugTargetType, out uint pDebugTargetTypeId, out string pbstrCurrentDebugTarget) {
            IVsBuildPropertyStorage bps = (IVsBuildPropertyStorage)PythonConfig;

            pguidDebugTargetType = VSConstants.AppPackageDebugTargets.guidAppPackageDebugTargetCmdSet;
            pDebugTargetTypeId = VSConstants.AppPackageDebugTargets.cmdidAppPackage_RemoteMachine;
            pbstrCurrentDebugTarget = RemoteTarget;
        }

        Array IVsProjectCfgDebugTargetSelection.GetDebugTargetListOfType(Guid guidDebugTargetType, uint debugTargetTypeId) {
            string[] result = new string[1];
            if (guidDebugTargetType != VSConstants.AppPackageDebugTargets.guidAppPackageDebugTargetCmdSet) {
                return new string[0];
            }

            switch (debugTargetTypeId) {
            case VSConstants.AppPackageDebugTargets.cmdidAppPackage_RemoteMachine:
                result[0] = RemoteTarget;
                break;
            default:
                return new string[0];
            }

            return result;
        }

        bool IVsProjectCfgDebugTargetSelection.HasDebugTargets(IVsDebugTargetSelectionService pDebugTargetSelectionService, out Array pbstrSupportedTargetCommandIDs) {
            pbstrSupportedTargetCommandIDs = new string[] {
                String.Join(":", VSConstants.AppPackageDebugTargets.guidAppPackageDebugTargetCmdSet, VSConstants.AppPackageDebugTargets.cmdidAppPackage_RemoteMachine)
            };

            return true;
        }

        void IVsProjectCfgDebugTargetSelection.SetCurrentDebugTarget(Guid guidDebugTargetType, uint debugTargetTypeId, string bstrCurrentDebugTarget) {
            if (guidDebugTargetType == VSConstants.AppPackageDebugTargets.guidAppPackageDebugTargetCmdSet) {
            }
        }

        #endregion

        internal int QueryDebugTargets(out VsDebugTargetInfo2[] targets) {
            IntPtr queryDebuggableProjectCfgPtr = IntPtr.Zero;
            targets = null;

            Guid guid = typeof(IVsQueryDebuggableProjectCfg).GUID;
            int hr = get_CfgType(ref guid, out queryDebuggableProjectCfgPtr);
            if (ErrorHandler.Failed(hr))
                return hr;

            object queryDebuggableProjectCfgObject = Marshal.GetObjectForIUnknown(queryDebuggableProjectCfgPtr);
            if (queryDebuggableProjectCfgObject == null)
                return VSConstants.E_UNEXPECTED;

            IVsQueryDebuggableProjectCfg baseQueryDebugbableCfg = queryDebuggableProjectCfgObject as IVsQueryDebuggableProjectCfg;
            if (baseQueryDebugbableCfg == null)
                return VSConstants.E_UNEXPECTED;

            uint[] targetsCountOutput = new uint[1];
            hr = baseQueryDebugbableCfg.QueryDebugTargets(0, 0, null, targetsCountOutput);
            if (ErrorHandler.Failed(hr))
                return hr;
            uint numberOfDebugTargets = targetsCountOutput[0];

            targets = new VsDebugTargetInfo2[numberOfDebugTargets];
            hr = baseQueryDebugbableCfg.QueryDebugTargets(0, numberOfDebugTargets, targets, null);
            if (ErrorHandler.Failed(hr))
                return hr;

            if (string.IsNullOrEmpty(targets[0].bstrRemoteMachine)) {
                MessageBox.Show(
                    "The project cannot be deployed or debugged because there is not a remote machine specified in Debug settings.",
                    "Python Tools for Visual Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return VSConstants.E_ABORT;
            }

            return hr;
        }

        private bool GetDebugFlag(string name, bool defaultValue) {
            string value;
            string canonicalName;
            IVsBuildPropertyStorage bps = (IVsBuildPropertyStorage)PythonConfig;

            get_CanonicalName(out canonicalName);

            int hr = bps.GetPropertyValue(name, canonicalName, (uint)_PersistStorageType.PST_PROJECT_FILE, out value);

            if (Microsoft.VisualStudio.ErrorHandler.Failed(hr))
                return defaultValue;

            return value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private int Deploy() {
            try {
                foreach (IVsDeployStatusCallback callback in deployCallbackCollection) {
                    callback.OnEndDeploy(1);
                }
            } finally {
                lock (syncObject) {
                    appContainerBootstrapperOperation = null;
                    deployOp = null;
                }
            }

            this.outputWindow = null;

            return VSConstants.S_OK;
        }

        private void NotifyEndDeploy(int success) {
            foreach (IVsDeployStatusCallback callback in deployCallbackCollection) {
                callback.OnEndDeploy(success);
            }

            outputWindow = null;
        }

        int IVsQueryDebuggableProjectCfg.QueryDebugTargets(uint grfLaunch, uint cTargets, VsDebugTargetInfo2[] rgDebugTargetInfo, uint[] pcActual) {
            var project = PythonConfig;

            if (pcActual != null && pcActual.Length > 0) {
                pcActual[0] = 1;
            }

            if (rgDebugTargetInfo != null && rgDebugTargetInfo.Length > 0) {
                IList<Guid> debugEngineGuids = new Guid[] { VSConstants.DebugEnginesGuids.NativeOnly_guid };

                rgDebugTargetInfo[0] = new VsDebugTargetInfo2();

                rgDebugTargetInfo[0].bstrExe = project.GetProjectProperty("Name");
                rgDebugTargetInfo[0].bstrRemoteMachine = project.GetProjectProperty("RemoteDebugMachine");
                rgDebugTargetInfo[0].guidPortSupplier = VSConstants.DebugPortSupplierGuids.NoAuth_guid;
                rgDebugTargetInfo[0].guidLaunchDebugEngine = debugEngineGuids[0];
                rgDebugTargetInfo[0].dwDebugEngineCount = (uint)debugEngineGuids.Count;
                rgDebugTargetInfo[0].pDebugEngines = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(Guid)) * debugEngineGuids.Count);

                for (var i = 0; i < debugEngineGuids.Count; i++) {
                    Marshal.StructureToPtr(debugEngineGuids[i],
                        IntPtr.Add(rgDebugTargetInfo[0].pDebugEngines, i * Marshal.SizeOf(typeof(Guid))),
                        false);
                }
            }

            return VSConstants.S_OK;
        }
    }
}

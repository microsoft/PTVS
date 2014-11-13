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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.Win32;
using Process = System.Diagnostics.Process;
using Task = System.Threading.Tasks.Task;
using Thread = System.Threading.Thread;

namespace Microsoft.VisualStudioTools.VSTestHost.Internal {
    class VisualStudio : IDisposable {
        private readonly Version _version;
        private readonly int _processId;
        private readonly bool _killOnDispose;
        private DTE _dte;

        public VisualStudio(int processId, Version version, bool killOnDispose = false) {
            _processId = processId;
            _version = version;
            _killOnDispose = killOnDispose;
        }

        protected virtual void Dispose(bool disposing) {
            MessageFilter.Revoke();
            if (_killOnDispose) {
                try {
                    Process.GetProcessById(_processId).Kill();
                } catch (ArgumentException) {
                } catch (InvalidOperationException) {
                } catch (Win32Exception) {
                }
            }
        }

        ~VisualStudio() {
            Dispose(false);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public int ProcessId {
            get { return _processId; }
        }

        public static async Task<VisualStudio> LaunchAsync(
            string application,
            string executable,
            Version version,
            string hive,
            CancellationToken cancel
        ) {
            var installDir = GetInstallDir(application, version, hive);
            if (!Directory.Exists(installDir)) {
                throw new DirectoryNotFoundException("Cannot find Visual Studio install path");
            }

            var devenv = Path.Combine(installDir, executable);
            if (!File.Exists(devenv)) {
                throw new FileNotFoundException("Cannot find Visual Studio executable");
            }

            var psi = new ProcessStartInfo(devenv);
            if (!string.IsNullOrEmpty(hive)) {
                psi.Arguments = "/rootSuffix " + hive;
            }

            cancel.ThrowIfCancellationRequested();
            var process = Process.Start(psi);

            await Task.Run(() => process.WaitForInputIdle(), cancel);

            var vs = new VisualStudio(process.Id, version, true);
            try {
                while (true) {
                    cancel.ThrowIfCancellationRequested();
                    if (vs.DTE != null) {
                        return Interlocked.Exchange(ref vs, null);
                    }
                    await Task.Delay(1000, cancel);
                }
            } finally {
                if (vs != null) {
                    vs.Dispose();
                }
            }
        }

        private static string GetInstallDir(string application, Version version, string hive) {
            using (var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            using (var key = root.OpenSubKey("Software\\Microsoft\\" + application)) {
                if (key == null) {
                    return null;
                }

                string installDir;

                using (var subkey = key.OpenSubKey(version.ToString())) {
                    if (subkey == null) {
                        return null;
                    }

                    installDir = subkey.GetValue("InstallDir") as string;
                }

                if (!Directory.Exists(installDir)) {
                    return null;
                }

                return installDir;
            }
        }

        public DTE DTE {
            get {
                if (_dte == null) {
                    _dte = GetDTE(_processId, _version);
                }
                return _dte;
            }
        }

        public T GetService<T>(Type type = null) where T : class {
            var sp = ((Microsoft.VisualStudio.OLE.Interop.IServiceProvider)DTE);
            return new ServiceProvider(sp).GetService(type ?? typeof(T)) as T;
        }

        // Source from
        //  http://blogs.msdn.com/b/kirillosenkov/archive/2011/08/10/how-to-get-dte-from-visual-studio-process-id.aspx
        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(uint reserved, out IBindCtx ppbc);

        private static DTE GetDTE(int processId, Version version) {
            MessageFilter.Register();

            var process = Process.GetProcessById(processId);
            string progIdName = "VisualStudio";

            switch (process.MainModule.ModuleName.ToLowerInvariant()) {
                case "wdexpress.exe":
                    progIdName = "WDExpress";
                    break;
                case "vwdexpress.exe":
                    progIdName = "VWDExpress";
                    break;
            }

            string progId = string.Format("!{0}.DTE.{1}:{2}", progIdName, version, processId);
            object runningObject = null;

            IBindCtx bindCtx = null;
            IRunningObjectTable rot = null;
            IEnumMoniker enumMonikers = null;

            Marshal.ThrowExceptionForHR(CreateBindCtx(reserved: 0, ppbc: out bindCtx));
            bindCtx.GetRunningObjectTable(out rot);
            rot.EnumRunning(out enumMonikers);

            IMoniker[] moniker = new IMoniker[1];
            uint numberFetched = 0;
            while (enumMonikers.Next(1, moniker, out numberFetched) == 0) {
                IMoniker runningObjectMoniker = moniker[0];

                string name = null;

                try {
                    if (runningObjectMoniker != null) {
                        runningObjectMoniker.GetDisplayName(bindCtx, null, out name);
                    }
                } catch (UnauthorizedAccessException) {
                    // Do nothing, there is something in the ROT that we do not have access to.
                }

                if (!string.IsNullOrEmpty(name) && string.Equals(name, progId, StringComparison.Ordinal)) {
                    rot.GetObject(runningObjectMoniker, out runningObject);
                    break;
                }
            }

            return (DTE)runningObject;
        }

        public class MessageFilter : IOleMessageFilter {
            // Start the filter.
            public static void Register() {
                IOleMessageFilter newFilter = new MessageFilter();
                IOleMessageFilter oldFilter = null;
                CoRegisterMessageFilter(newFilter, out oldFilter);
            }

            // Done with the filter, close it.
            public static void Revoke() {
                IOleMessageFilter oldFilter = null;
                CoRegisterMessageFilter(null, out oldFilter);
            }

            const int SERVERCALL_ISHANDLED = 0;
            const int SERVERCALL_RETRYLATER = 2;
            const int PENDINGMSG_WAITDEFPROCESS = 2;

            private MessageFilter() { }

            // IOleMessageFilter functions.
            // Handle incoming thread requests.
            int IOleMessageFilter.HandleInComingCall(int dwCallType,
                                                     IntPtr hTaskCaller,
                                                     int dwTickCount,
                                                     IntPtr lpInterfaceInfo) {
                return SERVERCALL_ISHANDLED;
            }

            // Thread call was rejected, so try again.
            int IOleMessageFilter.RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType) {
                if (dwRejectType == SERVERCALL_RETRYLATER && dwTickCount < 10000) {
                    // Retry the thread call after 250ms
                    return 250;
                }
                // Too busy; cancel call.
                return -1;
            }

            int IOleMessageFilter.MessagePending(System.IntPtr hTaskCallee, int dwTickCount, int dwPendingType) {
                return PENDINGMSG_WAITDEFPROCESS;
            }

            // Implement the IOleMessageFilter interface.
            [DllImport("Ole32.dll")]
            private static extern int CoRegisterMessageFilter(IOleMessageFilter newFilter, out IOleMessageFilter oldFilter);
        }

        [ComImport(), Guid("00000016-0000-0000-C000-000000000046"),
        InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
        interface IOleMessageFilter {
            [PreserveSig]
            int HandleInComingCall(int dwCallType,
                                   IntPtr hTaskCaller,
                                   int dwTickCount,
                                   IntPtr lpInterfaceInfo);

            [PreserveSig]
            int RetryRejectedCall(IntPtr hTaskCallee,
                                  int dwTickCount,
                                  int dwRejectType);

            [PreserveSig]
            int MessagePending(IntPtr hTaskCallee,
                               int dwTickCount,
                               int dwPendingType);
        }
    }
}

#endif

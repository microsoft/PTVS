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
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudioTools.Project;
using Process = System.Diagnostics.Process;

namespace Microsoft.VisualStudioTools {
    class VisualStudioApp : IDisposable {
        private static readonly Dictionary<int, VisualStudioApp> _knownInstances = new Dictionary<int, VisualStudioApp>();
        private readonly int _processId;
        private DTE _dte;

        public DTE DTE {
            get {
                if (_dte == null) {
                    _dte = GetDTE(_processId);
                }
                return _dte;
            }
        }

        public static VisualStudioApp FromCommandLineArgs(string[] commandLineArgs) {
            for (int i = 0; i < commandLineArgs.Length - 1; ++i) {
                int processId;
                if (commandLineArgs[i].Equals("/parentProcessId", StringComparison.InvariantCultureIgnoreCase) &&
                    int.TryParse(commandLineArgs[i + 1], out processId)) {
                    VisualStudioApp inst;
                    if (!_knownInstances.TryGetValue(processId, out inst)) {
                        _knownInstances[processId] = inst = new VisualStudioApp(processId);
                    }
                    return inst;
                }
            }
            return null;
        }

        public VisualStudioApp(int processId) {
            _processId = processId;
        }

        public void Dispose() {
            _knownInstances.Remove(_processId);
            if (_dte != null) {
                Marshal.ReleaseComObject(_dte);
                _dte = null;
                MessageFilter.Revoke();
            }
        }

        // Source from
        //  http://blogs.msdn.com/b/kirillosenkov/archive/2011/08/10/how-to-get-dte-from-visual-studio-process-id.aspx
        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(uint reserved, out IBindCtx ppbc);

        private static DTE GetDTE(int processId) {
            MessageFilter.Register();

            var prefix = Process.GetProcessById(processId).ProcessName;
            if ("devenv".Equals(prefix, StringComparison.OrdinalIgnoreCase)) {
                prefix = "VisualStudio";
            }

            string progId = string.Format("!{0}.DTE.{1}:{2}", prefix, AssemblyVersionInfo.VSVersion, processId);
            object runningObject = null;

            IBindCtx bindCtx = null;
            IRunningObjectTable rot = null;
            IEnumMoniker enumMonikers = null;

            try {
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
            } finally {
                if (enumMonikers != null) {
                    Marshal.ReleaseComObject(enumMonikers);
                }

                if (rot != null) {
                    Marshal.ReleaseComObject(rot);
                }

                if (bindCtx != null) {
                    Marshal.ReleaseComObject(bindCtx);
                }
            }

            return (DTE)runningObject;
        }


        public bool AttachToProcess(ProcessOutput proc, Guid portSupplier, string secret, int port) {
            var debugger3 = (EnvDTE90.Debugger3)DTE.Debugger;
            var transports = debugger3.Transports;
            EnvDTE80.Transport transport = null;
            for (int i = 1; i <= transports.Count; ++i) {
                var t = transports.Item(i);
                if (Guid.Parse(t.ID) == portSupplier) {
                    transport = t;
                    break;
                }
            }
            if (transport == null) {
                return false;
            }
            var processes = debugger3.GetProcesses(transport, string.Format("{0}@localhost:{1}", secret, port));
            if (processes.Count < 1) {
                return false;
            }

            // Retry the attach itself 3 times before displaying a Retry/Cancel
            // dialog to the user.
            DTE.SuppressUI = true;
            try {
                try {
                    processes.Item(1).Attach();
                    return true;
                } catch (COMException) {
                    if (proc.Wait(TimeSpan.FromMilliseconds(500))) {
                        // Process exited while we were trying
                        return false;
                    }
                }
            } finally {
                DTE.SuppressUI = false;
            }

            // Another attempt, but display UI.
            processes.Item(1).Attach();
            return true;
        }
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

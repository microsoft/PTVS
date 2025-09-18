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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using EnvDTE;
using Process = System.Diagnostics.Process;

namespace Microsoft.PythonTools.Infrastructure {
    sealed class VisualStudioProxy : IDisposable {
        private static readonly Dictionary<int, VisualStudioProxy> _knownInstances = new Dictionary<int, VisualStudioProxy>();
        private readonly int _processId;

        public static VisualStudioProxy FromProcessId(int processId) {
            VisualStudioProxy inst;
            lock (_knownInstances) {
                if (!_knownInstances.TryGetValue(processId, out inst)) {
                    _knownInstances[processId] = inst = new VisualStudioProxy(processId);
                }
            }
            return inst;
        }

        public static VisualStudioProxy FromEnvironmentVariable(string variable) {
            string pid = Environment.GetEnvironmentVariable(variable);
            if (pid == null) {
                return null;
            }

            int processId;
            if (!int.TryParse(pid, out processId)) {
                return null;
            }

            return FromProcessId(processId);
        }

        public VisualStudioProxy(int processId) {
            _processId = processId;
        }

        public void Dispose() {
            lock (_knownInstances) {
                _knownInstances.Remove(_processId);
            }
        }

        // Source from
        //  http://blogs.msdn.com/b/kirillosenkov/archive/2011/08/10/how-to-get-dte-from-visual-studio-process-id.aspx
        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass")]
        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(uint reserved, out IBindCtx ppbc);

        public DTE GetDTE() {
            var dte = GetDTE(_processId);
            if (dte == null) {
                throw new InvalidOperationException("Could not find VS DTE object for process " + _processId);
            }
            return dte;
        }

        private static DTE GetDTE(int processId) {
            MessageFilter.Register();

            var prefix = Process.GetProcessById(processId).ProcessName;
            if ("devenv".Equals(prefix, StringComparison.OrdinalIgnoreCase)) {
                prefix = "VisualStudio";
            }

            // Temporary fix. we should revert back to AssemblyVersionInfo.VSVersion once pipeline support dev18 toolchain
            string progIdDev17 = string.Format("!{0}.DTE.{1}:{2}", prefix, 17.0, processId);
            string progIdDev18 = string.Format("!{0}.DTE.{1}:{2}", prefix, 18.0, processId);
            object runningObject = null;

            IBindCtx bindCtx = null;
            IRunningObjectTable rot = null;
            IEnumMoniker enumMonikers = null;

            try {
                Marshal.ThrowExceptionForHR(CreateBindCtx(reserved: 0, ppbc: out bindCtx));
                bindCtx.GetRunningObjectTable(out rot);
                rot.EnumRunning(out enumMonikers);

                IMoniker[] moniker = new IMoniker[1];
                while (enumMonikers.Next(1, moniker, IntPtr.Zero) == 0) {
                    IMoniker runningObjectMoniker = moniker[0];

                    string name = null;

                    try {
                        if (runningObjectMoniker != null) {
                            runningObjectMoniker.GetDisplayName(bindCtx, null, out name);
                        }
                    } catch (UnauthorizedAccessException) {
                        // Do nothing, there is something in the ROT that we do not have access to.
                    }

                    if (!string.IsNullOrEmpty(name) && (string.Equals(name, progIdDev17, StringComparison.Ordinal) || string.Equals(name, progIdDev18, StringComparison.Ordinal))) {
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

        public bool AttachToProcess(ProcessOutput processOutput, Guid portSupplier, string transportQualifierUri) {
            var debugger3 = (EnvDTE90.Debugger3)GetDTE().Debugger;
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

            var processes = debugger3.GetProcesses(transport, transportQualifierUri);
            if (processes.Count < 1) {
                return false;
            }

            var process = processes.Item(1);
            return AttachToProcess(processOutput, process);
        }

        public bool AttachToProcess(ProcessOutput processOutput, Guid[] engines) {
            var debugger3 = (EnvDTE90.Debugger3)GetDTE().Debugger;
            var processes = debugger3.LocalProcesses;
            for (int i = 1; i < processes.Count; ++i) {
                var process = processes.Item(i);
                if (process.ProcessID == processOutput.ProcessId) {
                    return AttachToProcess(processOutput, process, engines);
                }
            }

            return false;
        }

        public bool AttachToProcess(ProcessOutput processOutput, EnvDTE.Process process, Guid[] engines = null) {
            // Retry the attach itself 3 times before displaying a Retry/Cancel
            // dialog to the user.
            var dte = GetDTE();
            dte.SuppressUI = true;
            try {
                try {
                    if (engines == null) {
                        process.Attach();
                    } else {
                        var process3 = process as EnvDTE90.Process3;
                        if (process3 == null) {
                            return false;
                        }
                        process3.Attach2(engines.Select(engine => engine.ToString("B")).ToArray());
                    }
                    return true;
                } catch (COMException) {
                    if (processOutput.Wait(TimeSpan.FromMilliseconds(500))) {
                        // Process exited while we were trying
                        return false;
                    }
                }
            } finally {
                dte.SuppressUI = false;
            }

            // Another attempt, but display UI.
            process.Attach();
            return true;
        }
    }

    class MessageFilter : IOleMessageFilter {
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
        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass")]
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

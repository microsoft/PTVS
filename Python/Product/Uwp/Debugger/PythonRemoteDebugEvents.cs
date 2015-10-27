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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.ComponentInterfaces;
using Microsoft.VisualStudio.Debugger.Exceptions;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Uwp.Debugger {
    internal class PythonRemoteDebugEvents : IVsDebuggerEvents, IDebugEventCallback2, IDkmExceptionTriggerHitNotification {
        private static readonly Lazy<PythonRemoteDebugEvents> instance = new Lazy<PythonRemoteDebugEvents>();

        // This exception code is a Win32 exception that is expected to be thrown by the debug client to trigger this code flow
        // i.e. whatever remote process is launching the debug server, should throw and catch this exception code before starting
        // remote Python debug server to have automatic attach work
        internal const uint RemoteDebugStartExceptionCode = 0xEDCBA987;
        internal const uint RemoteDebugAttachExceptionCode = 0xEDCBA988;

        public const string RemoteDebugExceptionId = "E7DD0845-FB1A-4A45-8192-44953C0ACC51";
        public static readonly Guid RemoteDebugExceptionGuid = new Guid(RemoteDebugExceptionId);

        public static PythonRemoteDebugEvents Instance {
            get { return instance.Value; }
        }

        public Func<System.Threading.Tasks.Task> AttachRemoteProcessFunction { get; set; }

        public byte[] RemoteDebugCommandInfo { get; set; }

        public int Event(IDebugEngine2 pEngine, IDebugProcess2 pProcess, IDebugProgram2 pProgram, IDebugThread2 pThread, IDebugEvent2 pEvent, ref Guid riidEvent, uint dwAttrib) {
            if (riidEvent == typeof(IDebugProgramCreateEvent2).GUID) {
                Guid processId;

                // A program was created and attached
                if (pProcess != null) {
                    if (VSConstants.S_OK == pProcess.GetProcessId(out processId)) {
                        DkmProcess dkmProcess = DkmProcess.FindProcess(processId);

                        if (dkmProcess != null) {
                            var debugTrigger = DkmExceptionCodeTrigger.Create(DkmExceptionProcessingStage.Thrown, null, DkmExceptionCategory.Win32, RemoteDebugStartExceptionCode);
                            var attachTrigger = DkmExceptionCodeTrigger.Create(DkmExceptionProcessingStage.Thrown, null, DkmExceptionCategory.Win32, RemoteDebugAttachExceptionCode);

                            // Try to add exception trigger for when a remote debugger server is started for Python
                            dkmProcess.AddExceptionTrigger(RemoteDebugExceptionGuid, debugTrigger);
                            dkmProcess.AddExceptionTrigger(RemoteDebugExceptionGuid, attachTrigger);
                        }
                    }
                }
            }

            return VSConstants.S_OK;
        }

        void IDkmExceptionTriggerHitNotification.OnExceptionTriggerHit(DkmExceptionTriggerHit hit, DkmEventDescriptorS eventDescriptor) {
            ThreadHelper.Generic.Invoke(() => {
                var exceptionInfo = hit.Exception as VisualStudio.Debugger.Native.DkmWin32ExceptionInformation;

                if (exceptionInfo.Code == RemoteDebugStartExceptionCode) {
                    // Parameters expected are the flag to indicate the debugger is present and JSON to write to the target for configuration
                    // of the debugger
                    const int exceptionParameterCount = 3;

                    if (exceptionInfo.ExceptionParameters.Count == exceptionParameterCount) {
                        // If we have a port and debug id, we'll go ahead and tell the client we are present
                        if (Instance.AttachRemoteProcessFunction != null && Instance.RemoteDebugCommandInfo != null) {
                            if (Instance.RemoteDebugCommandInfo.Length <= (int)exceptionInfo.ExceptionParameters[2]) {
                                // Write back that debugger is present
                                hit.Process.WriteMemory(exceptionInfo.ExceptionParameters[0], BitConverter.GetBytes(true));

                                // Write back the details of the debugger arguments
                                hit.Process.WriteMemory(exceptionInfo.ExceptionParameters[1], Instance.RemoteDebugCommandInfo);
                            }
                        }
                    }
                } else if (exceptionInfo.Code == RemoteDebugAttachExceptionCode) {
                    // Parameters expected are the flag to indicate the debugger is present and XML to write to the target for configuration
                    // of the debugger
                    const int exceptionAttachParameterCount = 1;

                    if (exceptionInfo.ExceptionParameters.Count == exceptionAttachParameterCount) {
                        // If we have a port and debug id, we'll go ahead and tell the client we are present
                        if (Instance.AttachRemoteProcessFunction != null) {
                            // Write back that debugger is present
                            hit.Process.WriteMemory(exceptionInfo.ExceptionParameters[0], BitConverter.GetBytes(true));

                            // Start the task to attach to the remote Python debugger session
                            System.Threading.Tasks.Task.Factory.StartNew(Instance.AttachRemoteProcessFunction);
                        }
                    }
                }
            });

            eventDescriptor.Suppress();
        }

        public int OnModeChange(DBGMODE dbgmodeNew) {
            return VSConstants.S_OK;
        }
    }
}

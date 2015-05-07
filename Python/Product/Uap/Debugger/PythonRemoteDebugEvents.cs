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
using System.Text;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.ComponentInterfaces;
using Microsoft.VisualStudio.Debugger.Exceptions;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Uap.Debugger {
    internal class PythonRemoteDebugEvents : IVsDebuggerEvents, IDebugEventCallback2, IDkmExceptionTriggerHitNotification {
        private static readonly Lazy<PythonRemoteDebugEvents> instance = new Lazy<PythonRemoteDebugEvents>();

        // This exception code is a Win32 exception that is expected to be thrown by the debug client to trigger this code flow
        // i.e. whatever remote process is launching the debug server, should throw and catch this exception code before starting
        // remote Python debug server to have automatic attach work
        internal const uint RemoteDebugStartExceptionCode = 0xEDCBA987;

        public const string RemoteDebugExceptionId = "E7DD0845-FB1A-4A45-8192-44953C0ACC51";
        public static readonly Guid RemoteDebugExceptionGuid = new Guid(RemoteDebugExceptionId);

        public static PythonRemoteDebugEvents Instance {
            get { return instance.Value; }
        }

        public Func<System.Threading.Tasks.Task> AttachRemoteProcessFunction { get; set; }

        public string AttachRemoteDebugXml { get; set; }

        public int Event(IDebugEngine2 pEngine, IDebugProcess2 pProcess, IDebugProgram2 pProgram, IDebugThread2 pThread, IDebugEvent2 pEvent, ref Guid riidEvent, uint dwAttrib) {
            if (riidEvent == typeof(IDebugProgramCreateEvent2).GUID) {
                Guid processId;

                // A program was created and attached
                if (pProcess != null) {
                    if (VSConstants.S_OK == pProcess.GetProcessId(out processId)) {
                        DkmProcess dkmProcess = DkmProcess.FindProcess(processId);

                        if (dkmProcess != null) {
                            var debugTrigger = DkmExceptionCodeTrigger.Create(DkmExceptionProcessingStage.Thrown, null, DkmExceptionCategory.Win32, RemoteDebugStartExceptionCode);

                            // Try to add exception trigger for when a remote debugger server is started for Python
                            dkmProcess.AddExceptionTrigger(RemoteDebugExceptionGuid, debugTrigger);
                        }
                    }
                }
            }

            return VSConstants.S_OK;
        }

        void IDkmExceptionTriggerHitNotification.OnExceptionTriggerHit(DkmExceptionTriggerHit hit, DkmEventDescriptorS eventDescriptor) {
            var remoteProcessTask = default(System.Threading.Tasks.Task);

            ThreadHelper.Generic.Invoke(() => {
                var exceptionInfo = hit.Exception as VisualStudio.Debugger.Native.DkmWin32ExceptionInformation;

                // Parameters expected are the flag to indicate the debugger is present and XML to write to the target for configuration
                // of the debugger
                const int exceptionParameterCount = 2;

                if (exceptionInfo.ExceptionParameters.Count == exceptionParameterCount) {
                    // If we have a port and debug id, we'll go ahead and tell the client we are present
                    if (Instance.AttachRemoteProcessFunction != null && Instance.AttachRemoteDebugXml != null) {
                        // Write back that debugger is present
                        hit.Process.WriteMemory(exceptionInfo.ExceptionParameters[0], BitConverter.GetBytes(true));

                        // Write back the details of the debugger arguments
                        hit.Process.WriteMemory(exceptionInfo.ExceptionParameters[1], Encoding.Unicode.GetBytes(Instance.AttachRemoteDebugXml));
                    }
                }
            });

            eventDescriptor.Suppress();

            // Start the task to attach to the remote Python debugger session
            remoteProcessTask = System.Threading.Tasks.Task.Factory.StartNew(Instance.AttachRemoteProcessFunction);
        }

        public int OnModeChange(DBGMODE dbgmodeNew) {
            return VSConstants.S_OK;
        }
    }
}

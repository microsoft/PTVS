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
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;

namespace Microsoft.PythonTools.Debugger.DebugEngine {
    // This implementation of IDebugEngine2 is used solely to initiate mixed-mode debugging (Debugger.Concord).
    // The engine itself is a dummy that doesn't do anything useful, but its GUID is used by the Concord
    // extension to light up the Python runtime.
    //
    // For pure Python debugging, we use the DAP-based engine on top of debugpy; see DebugAdapterLauncher.
    [ComVisible(true)]
    [Guid(Guids.DebugEngineCLSID)]
    public sealed class AD7Engine : IDebugEngine2, IDebugProgram2 {
        private IDebugEventCallback2 _events;
        private Guid _ad7ProgramId;

        public const string DebugEngineId = "{EC1375B7-E2CE-43E8-BF75-DC638DE1F1F9}";
        public const string DebugEngineName = "Python (native)";
        public static Guid DebugEngineGuid = new Guid(DebugEngineId);
        public const string SourceDirectoryKey = "sd";
        public const string TargetDirectoryKey = "td";
        public const string TargetHostType = "host";
        public const string DebugOptionsKey = "opt";
        public const string DebugOptionsMetric = "PythonDebugOptions";

        public const string TargetUwp = "uwp";

        /// <summary>
        /// Specifies the version of the language which is being debugged.  One of
        /// V24, V25, V26, V27, V30, V31 or V32.
        /// </summary>
        public const string VersionSetting = "VERSION";

        /// <summary>
        /// Specifies whether the process should prompt for input before exiting on an abnormal exit.
        /// </summary>
        public const string WaitOnAbnormalExitSetting = "WAIT_ON_ABNORMAL_EXIT";

        /// <summary>
        /// Specifies whether the process should prompt for input before exiting on a normal exit.
        /// </summary>
        public const string WaitOnNormalExitSetting = "WAIT_ON_NORMAL_EXIT";

        /// <summary>
        /// Specifies if the output should be redirected to the visual studio output window.
        /// </summary>
        public const string RedirectOutputSetting = "REDIRECT_OUTPUT";

        /// <summary>
        /// Specifies if the debugger should break on SystemExit exceptions with an exit code of zero.
        /// </summary>
        public const string BreakSystemExitZero = "BREAK_SYSTEMEXIT_ZERO";

        /// <summary>
        /// Specifies if the debugger should step/break into std lib code.
        /// </summary>
        public const string DebugStdLib = "DEBUG_STDLIB";

        /// <summary>
        /// Specifies the debugger should stop on first statement.
        /// </summary>
        public const string StopOnEntry = "STOP_ON_ENTRY";

        /// <summary>
        /// Specifies if the debugger should display the function return values in locals window
        /// </summary>
        public const string ShowReturnValue = "SHOW_RETURN_VALUE";

        /// <summary>
        /// Specifies if the debugger should treat the application as if it doesn't have a console.
        /// </summary>
        /// <remarks>
        /// Currently, the only effect this has is suppressing <see cref="WaitOnAbnormalExitSetting"/>
        /// and <see cref="WaitOnNormalExitSetting"/> if they're set.
        /// </remarks>
        public const string IsWindowsApplication = "IS_WINDOWS_APPLICATION";

        /// <summary>
        /// Specifies options which should be passed to the Python interpreter before the script.  If
        /// the interpreter options should include a semicolon then it should be escaped as a double
        /// semi-colon.
        /// </summary>
        public const string InterpreterOptions = "INTERPRETER_OPTIONS";

        public const string AttachRunning = "ATTACH_RUNNING";

        /// <summary>
        /// Specifies URL to which to open web browser on debug connect.
        /// </summary>
        public const string WebBrowserUrl = "WEB_BROWSER_URL";

        /// <summary>
        /// True if Django debugging is enabled.
        /// </summary>
        public const string EnableDjangoDebugging = "DJANGO_DEBUG";

        /// <summary>
        /// Specifies a directory mapping in the form of:
        /// 
        /// OldDir|NewDir
        /// 
        /// for mapping between the files on the local machine and the files deployed on the
        /// running machine.
        /// </summary>
        public const string DirMappingSetting = "DIR_MAPPING";

        private static bool IsDebuggingPythonOnly(IDebugProgram2 program) {
            IDebugProcess2 process;
            program.GetProcess(out process);

            IEnumDebugPrograms2 enumPrograms;
            process.EnumPrograms(out enumPrograms);

            while (true) {
                IDebugProgram2[] programs = new IDebugProgram2[1];
                uint fetched = 0;
                if (enumPrograms.Next(1, programs, ref fetched) != VSConstants.S_OK || fetched != 1 || programs[0] == null) {
                    break;
                }

                string engineName;
                Guid engineGuid;
                programs[0].GetEngineInfo(out engineName, out engineGuid);
                if (engineGuid != AD7Engine.DebugEngineGuid) {
                    return false;
                }
            }

            return true;
        }

        #region IDebugEngine2 Members

        // Attach the debug engine to a program. 
        int IDebugEngine2.Attach(IDebugProgram2[] rgpPrograms, IDebugProgramNode2[] rgpProgramNodes, uint celtPrograms, IDebugEventCallback2 ad7Callback, enum_ATTACH_REASON dwReason) {
            Debug.WriteLine("PythonEngine Attach Begin " + GetHashCode());
            Debug.Assert(_ad7ProgramId == Guid.Empty);

            if (celtPrograms != 1) {
                Debug.Fail("Python debugging only supports one program in a process");
                throw new ArgumentException();
            }

            var program = rgpPrograms[0];
            int processId = EngineUtils.GetProcessId(program);
            if (processId == 0) {
                // engine only supports system processes
                Debug.WriteLine("PythonEngine failed to get process id during attach");
                return VSConstants.E_NOTIMPL;
            }

            EngineUtils.RequireOk(program.GetProgramId(out _ad7ProgramId));

            _events = ad7Callback;
            Send(new AD7CustomEvent(VsPackageMessage.SetDebugOptions, this), AD7CustomEvent.IID, null);
            if (IsDebuggingPythonOnly(program)) {
                return VSConstants.E_FAIL;
            }

            AD7EngineCreateEvent.Send(this);
            AD7ProgramCreateEvent.Send(this);
            AD7LoadCompleteEvent.Send(this, null);
            Debug.WriteLine("PythonEngine Attach bailing out early - mixed-mode debugging");
            return VSConstants.S_OK;
        }

        // Requests that all programs being debugged by this DE stop execution the next time one of their threads attempts to run.
        // This is normally called in response to the user clicking on the pause button in the debugger.
        // When the break is complete, an AsyncBreakComplete event will be sent back to the debugger.
        int IDebugEngine2.CauseBreak()
            => VSConstants.E_NOTIMPL;

        int IDebugEngine2.ContinueFromSynchronousEvent(IDebugEvent2 eventObject)
            => VSConstants.E_NOTIMPL;

        int IDebugEngine2.CreatePendingBreakpoint(IDebugBreakpointRequest2 pBPRequest, out IDebugPendingBreakpoint2 ppPendingBP) {
            ppPendingBP = null;
            return VSConstants.E_NOTIMPL;
        }

        int IDebugEngine2.DestroyProgram(IDebugProgram2 pProgram)
            => VSConstants.E_NOTIMPL;

        int IDebugEngine2.GetEngineId(out Guid guidEngine) {
            guidEngine = new Guid(DebugEngineId);
            return VSConstants.S_OK;
        }

        int IDebugEngine2.RemoveAllSetExceptions(ref Guid guidType)
            => VSConstants.E_NOTIMPL;

        int IDebugEngine2.RemoveSetException(EXCEPTION_INFO[] pException)
            => VSConstants.E_NOTIMPL;

        int IDebugEngine2.SetException(EXCEPTION_INFO[] pException)
            => VSConstants.E_NOTIMPL;

        int IDebugEngine2.SetLocale(ushort wLangID)
            => VSConstants.S_OK;

        // A metric is a registry value used to change a debug engine's behavior or to advertise supported functionality. 
        // This method can forward the call to the appropriate form of the Debugging SDK Helpers function, SetMetric.
        int IDebugEngine2.SetMetric(string pszMetric, object varValue)
            => VSConstants.S_OK;

        // Sets the registry root currently in use by the DE. Different installations of Visual Studio can change where their registry information is stored
        // This allows the debugger to tell the engine where that location is.
        int IDebugEngine2.SetRegistryRoot(string pszRegistryRoot)
            => VSConstants.S_OK;

        int IDebugEngine2.EnumPrograms(out IEnumDebugPrograms2 programs) {
            programs = null;
            return VSConstants.E_NOTIMPL;
        }

        #endregion


        internal void Send(IDebugEvent2 eventObject, string iidEvent, IDebugProgram2 program, IDebugThread2 thread) {
            uint attributes;
            Guid riidEvent = new Guid(iidEvent);

            EngineUtils.RequireOk(eventObject.GetAttributes(out attributes));

            var events = _events;
            if (events == null) {
                // Probably racing with the end of the process.
                Debug.Fail("_events is null");
                return;
            }

            Debug.WriteLine(String.Format("Sending Event: {0} {1}", eventObject.GetType(), iidEvent));
            try {
                EngineUtils.RequireOk(events.Event(this, null, program, thread, eventObject, ref riidEvent, attributes));
            } catch (InvalidCastException) {
                // COM object has gone away
            }
        }

        internal void Send(IDebugEvent2 eventObject, string iidEvent, IDebugThread2 thread) {
            Send(eventObject, iidEvent, this, thread);
        }

        int IDebugProgram2.EnumThreads(out IEnumDebugThreads2 ppEnum) => throw new NotImplementedException();
        int IDebugProgram2.GetName(out string pbstrName) => throw new NotImplementedException();
        int IDebugProgram2.GetProcess(out IDebugProcess2 ppProcess) => throw new NotImplementedException();
        int IDebugProgram2.Terminate() => throw new NotImplementedException();
        int IDebugProgram2.Attach(IDebugEventCallback2 pCallback) => throw new NotImplementedException();
        int IDebugProgram2.CanDetach() => throw new NotImplementedException();
        int IDebugProgram2.Detach() => throw new NotImplementedException();
        int IDebugProgram2.GetProgramId(out Guid pguidProgramId) => throw new NotImplementedException();
        int IDebugProgram2.GetDebugProperty(out IDebugProperty2 ppProperty) => throw new NotImplementedException();
        int IDebugProgram2.Execute() => throw new NotImplementedException();
        int IDebugProgram2.Continue(IDebugThread2 pThread) => throw new NotImplementedException();
        int IDebugProgram2.Step(IDebugThread2 pThread, enum_STEPKIND sk, enum_STEPUNIT Step) => throw new NotImplementedException();
        int IDebugProgram2.CauseBreak() => throw new NotImplementedException();
        int IDebugProgram2.GetEngineInfo(out string pbstrEngine, out Guid pguidEngine) => throw new NotImplementedException();
        int IDebugProgram2.EnumCodeContexts(IDebugDocumentPosition2 pDocPos, out IEnumDebugCodeContexts2 ppEnum) => throw new NotImplementedException();
        int IDebugProgram2.GetMemoryBytes(out IDebugMemoryBytes2 ppMemoryBytes) => throw new NotImplementedException();
        int IDebugProgram2.GetDisassemblyStream(enum_DISASSEMBLY_STREAM_SCOPE dwScope, IDebugCodeContext2 pCodeContext, out IDebugDisassemblyStream2 ppDisassemblyStream) => throw new NotImplementedException();
        int IDebugProgram2.EnumModules(out IEnumDebugModules2 ppEnum) => throw new NotImplementedException();
        int IDebugProgram2.GetENCUpdate(out object ppUpdate) => throw new NotImplementedException();
        int IDebugProgram2.EnumCodePaths(string pszHint, IDebugCodeContext2 pStart, IDebugStackFrame2 pFrame, int fSource, out IEnumCodePaths2 ppEnum, out IDebugCodeContext2 ppSafety) => throw new NotImplementedException();
        int IDebugProgram2.WriteDump(enum_DUMPTYPE DUMPTYPE, string pszDumpUrl) => throw new NotImplementedException();
    }
}

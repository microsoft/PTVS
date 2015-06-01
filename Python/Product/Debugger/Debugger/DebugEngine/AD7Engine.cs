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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web;
using System.Windows.Forms;
using Microsoft.PythonTools.DkmDebugger;
using Microsoft.PythonTools.Debugger.Remote;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Debugger.DebugEngine {
    // AD7Engine is the primary entrypoint object for the debugging engine. 
    //
    // It implements:
    //
    // IDebugEngine2: This interface represents a debug engine (DE). It is used to manage various aspects of a debugging session, 
    // from creating breakpoints to setting and clearing exceptions.
    //
    // IDebugEngineLaunch2: Used by a debug engine (DE) to launch and terminate programs.
    //
    // IDebugProgram3: This interface represents a program that is running in a process. Since this engine only debugs one process at a time and each 
    // process only contains one program, it is implemented on the engine.

    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
        Justification = "We do not control ownership of this class")]
    [ComVisible(true)]
    [Guid(Guids.DebugEngineCLSID)]
    public sealed class AD7Engine : IDebugEngine2, IDebugEngineLaunch2, IDebugProgram3, IDebugSymbolSettings100, IThreadIdMapper {
        // used to send events to the debugger. Some examples of these events are thread create, exception thrown, module load.
        private IDebugEventCallback2 _events;

        // A flag indicating that we're in mixed-mode debugging. If true, most of the fields below are not valid,
        // since all debugging is handled by Concord.
        private bool _mixedMode;

        // Initial settings at launch or attach. Filled by ParseOptions.
        private PythonLanguageVersion _languageVersion;
        private PythonDebugOptions _debugOptions;
        private string _interpreterOptions;
        private List<string[]> _dirMapping;

        // The core of the engine is implemented by PythonProcess - we wrap and expose that to VS.
        private PythonProcess _process;

        // mapping between PythonProcess threads and AD7Threads
        private Dictionary<PythonThread, AD7Thread> _threads = new Dictionary<PythonThread, AD7Thread>();
        private Dictionary<PythonModule, AD7Module> _modules = new Dictionary<PythonModule, AD7Module>();
        private Dictionary<string, int> _breakOnException = new Dictionary<string, int>();
        private int _defaultBreakOnExceptionMode;
        private bool _justMyCodeEnabled = true;
        private AutoResetEvent _loadComplete = new AutoResetEvent(false);
        private bool _engineCreated, _programCreated;
        private object _syncLock = new object();
        private bool _isProgramCreateDelayed;
        private AD7Thread _processLoadedThread, _startThread;
        private AD7Module _startModule;
        private bool _attached, _pseudoAttach;
        private readonly BreakpointManager _breakpointManager;
        private Guid _ad7ProgramId;             // A unique identifier for the program being debugged.
        private static HashSet<WeakReference> _engines = new HashSet<WeakReference>();

        private string _webBrowserUrl;

        // Python thread IDs can be 64-bit (e.g. when remotely debugging a 64-bit Linux system), but VS debugger APIs only work
        // with 32-bit identifiers, so we need to set up a mapping system. If the thread ID is small enough to fit into 32 bits,
        // it is used as is; otherwise, we generate a new one.
        private readonly Dictionary<uint, long> _threadIdMapping = new Dictionary<uint, long>();
        private uint _lastGeneratedVsTid;

        internal static event EventHandler<AD7EngineEventArgs> EngineAttached;
        internal static event EventHandler<AD7EngineEventArgs> EngineDetaching;

        // These constants are duplicated in HpcLauncher and cannot be changed

        public const string DebugEngineId = "{EC1375B7-E2CE-43E8-BF75-DC638DE1F1F9}";
        public const string DebugEngineName = "Python";
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

        public AD7Engine() {
            _breakpointManager = new BreakpointManager(this);
            _defaultBreakOnExceptionMode = (int)enum_EXCEPTION_STATE.EXCEPTION_STOP_USER_UNCAUGHT;
            Debug.WriteLine("Python Engine Created " + GetHashCode());
            _engines.Add(new WeakReference(this));
        }

        ~AD7Engine() {
            Debug.WriteLine("Python Engine Finalized " + GetHashCode());
            if (!_attached && _process != null) {
                // detach the process exited event, we don't need to send the exited event
                // which could happen when we terminate the process and check if it's still
                // running.
                try {
                    _process.ProcessExited -= OnProcessExited;

                    // we launched the process, go ahead and kill it now that
                    // VS has released us
                    _process.Terminate();
                } catch (InvalidOperationException) {
                }
            }

            foreach (var engine in _engines) {
                if (engine.Target == this) {
                    _engines.Remove(engine);
                    break;
                }
            }
        }

        internal static IList<AD7Engine> GetEngines() {
            List<AD7Engine> engines = new List<AD7Engine>();
            foreach (var engine in AD7Engine._engines) {
                AD7Engine target = (AD7Engine)engine.Target;
                if (target != null) {
                    engines.Add(target);
                }
            }
            return engines;
        }

        internal PythonProcess Process {
            get {
                return _process;
            }
        }

        internal BreakpointManager BreakpointManager {
            get {
                return _breakpointManager;
            }
        }

        /// <summary>
        /// Map a generic 64-bit thread ID to a 32-bit thread ID usable in VS. If necessary (i.e. if the ID does not fit into 32 bits,
        /// or if it is already used), generates a new fake ID, and establishes the mapping between the two.
        /// </summary>
        /// <returns>The original ID if it could be used as is, generated ID otherwise.</returns>
        internal uint RegisterThreadId(long tid) {
            uint vsTid;

            if (tid <= uint.MaxValue && !_threadIdMapping.ContainsKey((uint)tid)) {
                vsTid = (uint)tid;
            } else {
                do {
                    vsTid = ++_lastGeneratedVsTid;
                } while (_threadIdMapping.ContainsKey(vsTid));
            }

            _threadIdMapping[vsTid] = tid;
            return vsTid;
        }

        internal void UnregisterThreadId(uint vsTid) {
            _threadIdMapping.Remove(vsTid);
        }

        long? IThreadIdMapper.GetPythonThreadId(uint vsThreadId) {
            long result;
            return _threadIdMapping.TryGetValue(vsThreadId, out result) ? result : (long?)null;
        }

        private static bool IsDebuggingPythonOnly(IDebugProgram2 program) {
#if DEV11_OR_LATER
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
#endif

            return true;
        }

        #region IDebugEngine2 Members

        // Attach the debug engine to a program. 
        int IDebugEngine2.Attach(IDebugProgram2[] rgpPrograms, IDebugProgramNode2[] rgpProgramNodes, uint celtPrograms, IDebugEventCallback2 ad7Callback, enum_ATTACH_REASON dwReason) {
            Debug.WriteLine("PythonEngine Attach Begin " + GetHashCode());

            AssertMainThread();
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

            // Attach can either be called to attach to a new process, or to complete an attach
            // to a launched process
            if (_process == null) {
                _events = ad7Callback;

                Send(new AD7CustomEvent(VsPackageMessage.SetDebugOptions, this), AD7CustomEvent.IID, null);

                // Check whether we're debugging Python alongside something else. If so, let Concord handle everything.
                if (!IsDebuggingPythonOnly(program)) {
                    _attached = true;
                    _mixedMode = true;
                    AD7EngineCreateEvent.Send(this);
                    AD7ProgramCreateEvent.Send(this);
                    AD7LoadCompleteEvent.Send(this);
                    Debug.WriteLine("PythonEngine Attach bailing out early - mixed-mode debugging");
                    return VSConstants.S_OK;
                }

                // Check if we're attaching remotely using the Python remote debugging transport
                var remoteProgram = program as PythonRemoteDebugProgram;
                try {
                    if (remoteProgram != null) {
                        var remotePort = remoteProgram.DebugProcess.DebugPort;

                        var uriBuilder = new UriBuilder(remotePort.Uri);
                        string query = uriBuilder.Query ?? "";
                        if (query.Length > 0) {
                            // Strip leading "?" - UriBuilder.Query getter returns it as part of the string, but the setter
                            // will automatically prepend it even if it was already there, producing a malformed query.
                            query = query.Substring(1);
                        }
                        query += "&" + DebugOptionsKey + "=" + _debugOptions;
                        uriBuilder.Query = query;

                        _process = PythonRemoteProcess.Attach(uriBuilder.Uri, true);
                    } else {
                        _process = PythonProcess.Attach(processId, _debugOptions);
                    }
                } catch (ConnectionException ex) {
                    MessageBox.Show("Failed to attach debugger:\r\n" + ex.Message, null, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return VSConstants.E_FAIL;
                }

                AttachEvents(_process);
                _attached = true;
            } else {
                if (processId != _process.Id) {
                    Debug.Fail("Asked to attach to a process while we are debugging");
                    return VSConstants.E_FAIL;
                }
            }

            AD7EngineCreateEvent.Send(this);

            lock (_syncLock) {
                _engineCreated = true;
                if (_isProgramCreateDelayed) {
                    _isProgramCreateDelayed = false;
                    SendProgramCreate();
                }
            }

            Debug.WriteLine("PythonEngine Attach returning S_OK");
            return VSConstants.S_OK;
        }

        private void SendProgramCreate() {
            Debug.WriteLine("Sending program create " + GetHashCode());
            AD7ProgramCreateEvent.Send(this);

            _programCreated = true;
            if (_processLoadedThread != null) {
                SendLoadComplete(_processLoadedThread);
            }
        }

        private void SendLoadComplete(AD7Thread thread) {
            Debug.WriteLine("Sending load complete " + GetHashCode());

            if (_startModule != null) {
                SendModuleLoaded(_startModule);
                _startModule = null;
            }
            if (_startThread != null) {
                SendThreadStart(_startThread);
                _startThread = null;
            }

            var attached = EngineAttached;
            if (attached != null) {
                attached(this, new AD7EngineEventArgs(this));
            }

            Send(new AD7LoadCompleteEvent(), AD7LoadCompleteEvent.IID, thread);

            _processLoadedThread = null;
            _loadComplete.Set();

            StartWebBrowser();
        }

        private bool ProcessExited() {
            var process = _process;
            if (process != null) {
                return process.HasExited;
            }
            return true;
        }

        private void StartWebBrowser() {
            Uri uri;
            if (_webBrowserUrl != null && Uri.TryCreate(_webBrowserUrl, UriKind.RelativeOrAbsolute, out uri)) {
                OnPortOpenedHandler.CreateHandler(
                    uri.Port,
                    shortCircuitPredicate: ProcessExited,
                    action: LaunchBrowserDebugger
                );
            }
        }

        private void LaunchBrowserDebugger() {
            var vsDebugger = (IVsDebugger2)ServiceProvider.GlobalProvider.GetService(typeof(SVsShellDebugger)); ;

            VsDebugTargetInfo2 info = new VsDebugTargetInfo2();
            var infoSize = Marshal.SizeOf(info);
            info.cbSize = (uint)infoSize;
            info.bstrExe = _webBrowserUrl;
            info.dlo = (uint)_DEBUG_LAUNCH_OPERATION3.DLO_LaunchBrowser;
            info.LaunchFlags = (uint)__VSDBGLAUNCHFLAGS4.DBGLAUNCH_UseDefaultBrowser | (uint)__VSDBGLAUNCHFLAGS.DBGLAUNCH_NoDebug;
            info.guidLaunchDebugEngine = DebugEngineGuid;
            IntPtr infoPtr = Marshal.AllocCoTaskMem(infoSize);
            Marshal.StructureToPtr(info, infoPtr, false);

            try {
                vsDebugger.LaunchDebugTargets2(1, infoPtr);
            } finally {
                if (infoPtr != IntPtr.Zero) {
                    Marshal.FreeCoTaskMem(infoPtr);
                }
            }
        }
        private void SendThreadStart(AD7Thread ad7Thread) {
            Send(new AD7ThreadCreateEvent(), AD7ThreadCreateEvent.IID, ad7Thread);
        }

        private void SendModuleLoaded(AD7Module ad7Module) {
            AD7ModuleLoadEvent eventObject = new AD7ModuleLoadEvent(ad7Module, true /* this is a module load */);

            // TODO: Bind breakpoints when the module loads

            Send(eventObject, AD7ModuleLoadEvent.IID, null);
        }

        // Requests that all programs being debugged by this DE stop execution the next time one of their threads attempts to run.
        // This is normally called in response to the user clicking on the pause button in the debugger.
        // When the break is complete, an AsyncBreakComplete event will be sent back to the debugger.
        int IDebugEngine2.CauseBreak() {
            if (_mixedMode) {
                return VSConstants.E_NOTIMPL;
            }

            AssertMainThread();

            return ((IDebugProgram2)this).CauseBreak();
        }

        [Conditional("DEBUG")]
        private static void AssertMainThread() {
            //Debug.Assert(Worker.MainThreadId == Worker.CurrentThreadId);
        }

        // Called by the SDM to indicate that a synchronous debug event, previously sent by the DE to the SDM,
        // was received and processed. 
        int IDebugEngine2.ContinueFromSynchronousEvent(IDebugEvent2 eventObject) {
            if (_mixedMode) {
                return VSConstants.E_NOTIMPL;
            }

            AssertMainThread();

            if (eventObject is AD7ProgramDestroyEvent) {
                var debuggedProcess = _process;

                _events = null;
                _process = null;
                _ad7ProgramId = Guid.Empty;
                foreach (var thread in _threads.Values) {
                    thread.Dispose();
                }
                _threads.Clear();
                _modules.Clear();

                debuggedProcess.Dispose();
            } else if (eventObject is AD7CustomEvent) { 
            } else {
                Debug.Fail("Unknown synchronous event");
            }

            return VSConstants.S_OK;
        }

        // Creates a pending breakpoint in the engine. A pending breakpoint is contains all the information needed to bind a breakpoint to 
        // a location in the debuggee.
        int IDebugEngine2.CreatePendingBreakpoint(IDebugBreakpointRequest2 pBPRequest, out IDebugPendingBreakpoint2 ppPendingBP) {
            if (_mixedMode) {
                ppPendingBP = null;
                return VSConstants.E_NOTIMPL;
            }

            Debug.WriteLine("Creating pending break point");
            Debug.Assert(_breakpointManager != null);
            ppPendingBP = null;

            _breakpointManager.CreatePendingBreakpoint(pBPRequest, out ppPendingBP);
            return VSConstants.S_OK;
        }

        // Informs a DE that the program specified has been atypically terminated and that the DE should 
        // clean up all references to the program and send a program destroy event.
        int IDebugEngine2.DestroyProgram(IDebugProgram2 pProgram) {
            if (_mixedMode) {
                return VSConstants.E_NOTIMPL;
            }

            Debug.WriteLine("PythonEngine DestroyProgram");
            // Tell the SDM that the engine knows that the program is exiting, and that the
            // engine will send a program destroy. We do this because the Win32 debug api will always
            // tell us that the process exited, and otherwise we have a race condition.

            return (DebuggerConstants.E_PROGRAM_DESTROY_PENDING);
        }

        // Gets the GUID of the DE.
        int IDebugEngine2.GetEngineId(out Guid guidEngine) {
            guidEngine = new Guid(DebugEngineId);
            return VSConstants.S_OK;
        }

        int IDebugEngine2.RemoveAllSetExceptions(ref Guid guidType) {
            if (_mixedMode) {
                return VSConstants.E_NOTIMPL;
            }

            _breakOnException.Clear();
            _defaultBreakOnExceptionMode = (int)enum_EXCEPTION_STATE.EXCEPTION_STOP_USER_UNCAUGHT;

            SetExceptionInfo(_defaultBreakOnExceptionMode, _breakOnException);
            return VSConstants.S_OK;
        }

        int IDebugEngine2.RemoveSetException(EXCEPTION_INFO[] pException) {
            if (_mixedMode) {
                return VSConstants.E_NOTIMPL;
            }

            bool sendUpdate = false;
            for (int i = 0; i < pException.Length; i++) {
                if (pException[i].guidType == DebugEngineGuid) {
                    sendUpdate = true;
                    if (pException[i].bstrExceptionName == "Python Exceptions") {
                        _defaultBreakOnExceptionMode = (int)enum_EXCEPTION_STATE.EXCEPTION_STOP_USER_UNCAUGHT;
                    } else {
                        _breakOnException.Remove(pException[i].bstrExceptionName);
                    }
                }
            }

            if (sendUpdate) {
                SetExceptionInfo(_defaultBreakOnExceptionMode, _breakOnException);
            }
            return VSConstants.S_OK;
        }

        int IDebugEngine2.SetException(EXCEPTION_INFO[] pException) {
            if (_mixedMode) {
                return VSConstants.E_NOTIMPL;
            }

            bool sendUpdate = false;
            for (int i = 0; i < pException.Length; i++) {
                if (pException[i].guidType == DebugEngineGuid) {
                    sendUpdate = true;
                    if (pException[i].bstrExceptionName == "Python Exceptions") {
                        _defaultBreakOnExceptionMode =
                            (int)(pException[i].dwState & (enum_EXCEPTION_STATE.EXCEPTION_STOP_FIRST_CHANCE | enum_EXCEPTION_STATE.EXCEPTION_STOP_USER_UNCAUGHT));
                    } else {
                        _breakOnException[pException[i].bstrExceptionName] =
                            (int)(pException[i].dwState & (enum_EXCEPTION_STATE.EXCEPTION_STOP_FIRST_CHANCE | enum_EXCEPTION_STATE.EXCEPTION_STOP_USER_UNCAUGHT));
                    }
                }
            }

            if (sendUpdate) {
                SetExceptionInfo(_defaultBreakOnExceptionMode, _breakOnException);
            }
            return VSConstants.S_OK;
        }

        // Sets the locale of the DE.
        // This method is called by the session debug manager (SDM) to propagate the locale settings of the IDE so that
        // strings returned by the DE are properly localized. The engine is not localized so this is not implemented.
        int IDebugEngine2.SetLocale(ushort wLangID) {
            return VSConstants.S_OK;
        }

        // A metric is a registry value used to change a debug engine's behavior or to advertise supported functionality. 
        // This method can forward the call to the appropriate form of the Debugging SDK Helpers function, SetMetric.
        int IDebugEngine2.SetMetric(string pszMetric, object varValue) {
            switch (pszMetric) {
                case "JustMyCodeStepping":
                    {
                        var enabledUint = varValue as uint?;
                        if (enabledUint.HasValue) {
                            var enabled = enabledUint.Value != 0;
                            if (_justMyCodeEnabled != enabled) {
                                _justMyCodeEnabled = enabled;
                                SetExceptionInfo(_defaultBreakOnExceptionMode, _breakOnException);
                            }
                        }
                        return VSConstants.S_OK;
                    }

                case DebugOptionsMetric:
                    {
                        if (_engineCreated) {
                            Debug.Fail(DebugOptionsMetric + " metric can only be sent immediately in response to IDebugEngineCreateEvent2.");
                            return VSConstants.E_FAIL;
                        }

                        var options = varValue as string;
                        if (!string.IsNullOrEmpty(options)) {
                            // ParseOptions only overwrites the flags that are explicitly set to True or False, leaving any
                            // already existing values intact. Thus, any options that were previouly passed to LaunchSuspended
                            // are preserved unless explicitly overwritten here. 
                            ParseOptions(options);
                        }
                        return VSConstants.S_OK;
                    }

                default:
                    return VSConstants.S_OK;
            }
        }

        private void SetExceptionInfo(int defaultBreakOnMode, IEnumerable<KeyValuePair<string, int>> breakOn) {
            if (!_justMyCodeEnabled) {
                // Mask out just my code related flag not masked out by VS SDM
                var mask = ~(int)enum_EXCEPTION_STATE.EXCEPTION_STOP_USER_UNCAUGHT;
                defaultBreakOnMode &= mask;
                breakOn = breakOn.Select(kvp => new KeyValuePair<string, int>(kvp.Key, kvp.Value & mask));
            }
            _process.SetExceptionInfo(defaultBreakOnMode, breakOn);
        }

        // Sets the registry root currently in use by the DE. Different installations of Visual Studio can change where their registry information is stored
        // This allows the debugger to tell the engine where that location is.
        int IDebugEngine2.SetRegistryRoot(string pszRegistryRoot) {
            return VSConstants.S_OK;
        }

        #endregion

        #region IDebugEngineLaunch2 Members

        // Determines if a process can be terminated.
        int IDebugEngineLaunch2.CanTerminateProcess(IDebugProcess2 process) {
            if (_mixedMode) {
                return VSConstants.S_OK;
            }

            Debug.WriteLine("PythonEngine CanTerminateProcess");

            AssertMainThread();
            Debug.Assert(_events != null);
            Debug.Assert(_process != null);

            int processId = EngineUtils.GetProcessId(process);

            if (processId == _process.Id) {
                return VSConstants.S_OK;
            } else {
                return VSConstants.S_FALSE;
            }
        }

        // Launches a process by means of the debug engine.
        // Normally, Visual Studio launches a program using the IDebugPortEx2::LaunchSuspended method and then attaches the debugger 
        // to the suspended program. However, there are circumstances in which the debug engine may need to launch a program 
        // (for example, if the debug engine is part of an interpreter and the program being debugged is an interpreted language), 
        // in which case Visual Studio uses the IDebugEngineLaunch2::LaunchSuspended method
        // The IDebugEngineLaunch2::ResumeProcess method is called to start the process after the process has been successfully launched in a suspended state.
        int IDebugEngineLaunch2.LaunchSuspended(string pszServer, IDebugPort2 port, string exe, string args, string dir, string env, string options, enum_LAUNCH_FLAGS launchFlags, uint hStdInput, uint hStdOutput, uint hStdError, IDebugEventCallback2 ad7Callback, out IDebugProcess2 process) {
            process = null;
            if (_mixedMode) {
                return VSConstants.E_NOTIMPL;
            }

            Debug.WriteLine("--------------------------------------------------------------------------------");
            Debug.WriteLine("PythonEngine LaunchSuspended Begin " + launchFlags + " " + GetHashCode());
            AssertMainThread();
            Debug.Assert(_events == null);
            Debug.Assert(_process == null);
            Debug.Assert(_ad7ProgramId == Guid.Empty);

            _events = ad7Callback;
            _engineCreated = _programCreated = false;
            _loadComplete.Reset();

            if (options != null) {
                ParseOptions(options);
            }

            Send(new AD7CustomEvent(VsPackageMessage.SetDebugOptions, this), AD7CustomEvent.IID, null, null);

            // If this is a windowed application, there's no console to wait on, so disable those flags if they were set.
            if (_debugOptions.HasFlag(PythonDebugOptions.IsWindowsApplication)) {
                _debugOptions &= ~(PythonDebugOptions.WaitOnNormalExit | PythonDebugOptions.WaitOnAbnormalExit);
            }

            Guid processId;
            if (_debugOptions.HasFlag(PythonDebugOptions.AttachRunning)) {
                if (!Guid.TryParse(exe, out processId)) {
                    Debug.Fail("When PythonDebugOptions.AttachRunning is used, the 'exe' parameter must be a debug session GUID.");
                    return VSConstants.E_INVALIDARG;
                }

                _process = DebugConnectionListener.GetProcess(processId);
                _attached = true;
                _pseudoAttach = true;
            } else {
                _process = new PythonProcess(_languageVersion, exe, args, dir, env, _interpreterOptions, _debugOptions, _dirMapping);
            }

            if (!_debugOptions.HasFlag(PythonDebugOptions.AttachRunning)) {
                _process.Start(false);
            }

            AttachEvents(_process);

            AD_PROCESS_ID adProcessId = new AD_PROCESS_ID();
            adProcessId.ProcessIdType = (uint)enum_AD_PROCESS_ID.AD_PROCESS_ID_SYSTEM;
            adProcessId.dwProcessId = (uint)_process.Id;

            EngineUtils.RequireOk(port.GetProcess(adProcessId, out process));
            Debug.WriteLine("PythonEngine LaunchSuspended returning S_OK");
            Debug.Assert(process != null);
            Debug.Assert(!_process.HasExited);

            return VSConstants.S_OK;
        }

        private static string[] SplitOptions(string options) {
            List<string> res = new List<string>();
            int lastStart = 0;
            for (int i = 0; i < options.Length; i++) {
                if (options[i] == ';') {
                    if (i < options.Length - 1 && options[i + 1] != ';') {
                        // valid option boundary
                        res.Add(options.Substring(lastStart, i - lastStart));
                        lastStart = i + 1;
                    } else {
                        i++;
                    }
                }
            }
            if (options.Length - lastStart > 0) {
                res.Add(options.Substring(lastStart, options.Length - lastStart));
            }
            return res.ToArray();
        }

        // TODO: turn PythonDebugOptions into a class that encapsulates all options (not just flags), including the "not set"
        // state for all of them, and that knows how to stringify and parse itself, and how to merge isntances, and refactor
        // this entire codepath, including the bits in DefaultPythonLauncher and in CustomDebuggerEventHandler, to use that.
        private void ParseOptions(string options) {
            foreach (var optionSetting in SplitOptions(options)) {
                var setting = optionSetting.Split(new[] { '=' }, 2);
                if (setting.Length == 2) {
                    switch (setting[0]) {
                        case VersionSetting:
                            _languageVersion = GetLanguageVersion(setting[1]);
                            break;

                        case WaitOnAbnormalExitSetting:
                            bool value;
                            if (Boolean.TryParse(setting[1], out value) && value) {
                                _debugOptions |= PythonDebugOptions.WaitOnAbnormalExit;
                            }
                            break;

                        case WaitOnNormalExitSetting:
                            if (Boolean.TryParse(setting[1], out value) && value) {
                                _debugOptions |= PythonDebugOptions.WaitOnNormalExit;
                            }
                            break;

                        case RedirectOutputSetting:
                            if (Boolean.TryParse(setting[1], out value) && value) {
                                _debugOptions |= PythonDebugOptions.RedirectOutput;
                            }
                            break;

                        case BreakSystemExitZero:
                            if (Boolean.TryParse(setting[1], out value) && value) {
                                _debugOptions |= PythonDebugOptions.BreakOnSystemExitZero;
                            }
                            break;

                        case DebugStdLib:
                            if (Boolean.TryParse(setting[1], out value) && value) {
                                _debugOptions |= PythonDebugOptions.DebugStdLib;
                            }
                            break;

                        case IsWindowsApplication:
                            if (Boolean.TryParse(setting[1], out value) && value) {
                                _debugOptions |= PythonDebugOptions.IsWindowsApplication;
                            }
                            break;

                        case DirMappingSetting:
                            string[] dirs = setting[1].Split('|');
                            if (dirs.Length == 2) {
                                if (_dirMapping == null) {
                                    _dirMapping = new List<string[]>();
                                }
                                Debug.WriteLine(String.Format("Mapping dir {0} to {1}", dirs[0], dirs[1]));
                                _dirMapping.Add(dirs);
                            }
                            break;

                        case InterpreterOptions:
                            _interpreterOptions = setting[1];
                            break;

                        case AttachRunning:
                            if (Boolean.TryParse(setting[1], out value) && value) {
                                _debugOptions |= PythonDebugOptions.AttachRunning;
                            }
                            break;

                        case WebBrowserUrl:
                            _webBrowserUrl = HttpUtility.UrlDecode(setting[1]);
                            break;

                        case EnableDjangoDebugging:
                            if (Boolean.TryParse(setting[1], out value) && value) {
                                _debugOptions |= PythonDebugOptions.DjangoDebugging;
                            }
                            break;
                    }
                }
            }
        }

        // Default version, we never really use this because we always provide the version, but if someone
        // else started our debugger they could choose not to provide the version.
        private const PythonLanguageVersion DefaultVersion = PythonLanguageVersion.V27;

        private static PythonLanguageVersion GetLanguageVersion(string options) {
            PythonLanguageVersion langVersion;
            if (options == null || !Enum.TryParse<PythonLanguageVersion>(options, out langVersion)) {
                langVersion = DefaultVersion;
            }
            return langVersion;
        }

        // Resume a process launched by IDebugEngineLaunch2.LaunchSuspended
        int IDebugEngineLaunch2.ResumeProcess(IDebugProcess2 process) {
            if (_mixedMode) {
                return VSConstants.E_NOTIMPL;
            }

            Debug.WriteLine("Python Debugger ResumeProcess Begin");

            AssertMainThread();
            if (_events == null) {
                // process failed to start
                Debug.WriteLine("ResumeProcess fails, no events");
                return VSConstants.E_FAIL;
            }

            Debug.Assert(_process != null);
            Debug.Assert(_process != null);
            Debug.Assert(_ad7ProgramId == Guid.Empty);

            int processId = EngineUtils.GetProcessId(process);

            if (processId != _process.Id) {
                Debug.WriteLine("ResumeProcess fails, wrong process");
                return VSConstants.S_FALSE;
            }

            // Send a program node to the SDM. This will cause the SDM to turn around and call IDebugEngine2.Attach
            // which will complete the hookup with AD7
            IDebugPort2 port;
            EngineUtils.RequireOk(process.GetPort(out port));

            IDebugDefaultPort2 defaultPort = (IDebugDefaultPort2)port;

            IDebugPortNotify2 portNotify;
            EngineUtils.RequireOk(defaultPort.GetPortNotify(out portNotify));

            EngineUtils.RequireOk(portNotify.AddProgramNode(new AD7ProgramNode(_process.Id)));

            if (_ad7ProgramId == Guid.Empty) {
                Debug.WriteLine("ResumeProcess fails, empty program guid");
                Debug.Fail("Unexpected problem -- IDebugEngine2.Attach wasn't called");
                return VSConstants.E_FAIL;
            }

            Debug.WriteLine("ResumeProcess return S_OK");
            return VSConstants.S_OK;
        }

        // This function is used to terminate a process that the engine launched
        // The debugger will call IDebugEngineLaunch2::CanTerminateProcess before calling this method.
        int IDebugEngineLaunch2.TerminateProcess(IDebugProcess2 process) {
            if (_mixedMode) {
                return VSConstants.E_NOTIMPL;
            }

            Debug.WriteLine("PythonEngine TerminateProcess");

            AssertMainThread();
            Debug.Assert(_events != null);
            Debug.Assert(_process != null);

            int processId = EngineUtils.GetProcessId(process);
            if (processId != _process.Id) {
                return VSConstants.S_FALSE;
            }

            var detaching = EngineDetaching;
            if (detaching != null) {
                detaching(this, new AD7EngineEventArgs(this));
            }

            if (!_pseudoAttach) {
                _process.Terminate();
            } else {
                _process.Detach();
            }

            return VSConstants.S_OK;
        }

        #endregion

        #region IDebugProgram2 Members

        // Determines if a debug engine (DE) can detach from the program.
        public int CanDetach() {
            if (_attached) {
                return VSConstants.S_OK;
            }
            return VSConstants.S_FALSE;
        }

        // The debugger calls CauseBreak when the user clicks on the pause button in VS. The debugger should respond by entering
        // breakmode. 
        public int CauseBreak() {
            if (_mixedMode) {
                return VSConstants.E_NOTIMPL;
            }

            Debug.WriteLine("PythonEngine CauseBreak");
            AssertMainThread();

            _process.Break();

            return VSConstants.S_OK;
        }

        // Continue is called from the SDM when it wants execution to continue in the debugee
        // but have stepping state remain. An example is when a tracepoint is executed, 
        // and the debugger does not want to actually enter break mode.
        // It is also called to continue after load complete and autoresume after
        // entry point hit (when starting debugging with F5).
        public int Continue(IDebugThread2 pThread) {
            if (_mixedMode) {
                return VSConstants.E_NOTIMPL;
            }

            AD7Thread thread = (AD7Thread)pThread;

            Debug.WriteLine("PythonEngine Continue " + thread.GetDebuggedThread().Id);
            AssertMainThread();

            // Resume process, but leave stepping state intact, allowing stepping accross tracepoints
            thread.GetDebuggedThread().AutoResume();
            return VSConstants.S_OK;
        }

        // Detach is called when debugging is stopped and the process was attached to (as opposed to launched)
        // or when one of the Detach commands are executed in the UI.
        public int Detach() {
            Debug.WriteLine("PythonEngine Detach");
            AssertMainThread();

            if (_mixedMode) {
                Send(new AD7ProgramDestroyEvent(0), AD7ProgramDestroyEvent.IID, null);
            } else {
                AssertMainThread();

                _breakpointManager.ClearBoundBreakpoints();

                var detaching = EngineDetaching;
                if (detaching != null) {
                    detaching(this, new AD7EngineEventArgs(this));
                }

                _process.Detach();
            }

            _ad7ProgramId = Guid.Empty;
            return VSConstants.S_OK;
        }

        // Enumerates the code contexts for a given position in a source file.
        public int EnumCodeContexts(IDebugDocumentPosition2 pDocPos, out IEnumDebugCodeContexts2 ppEnum) {
            if (_mixedMode) {
                ppEnum = null;
                return VSConstants.E_NOTIMPL;
            }

            string filename;
            pDocPos.GetFileName(out filename);
            TEXT_POSITION[] beginning = new TEXT_POSITION[1], end = new TEXT_POSITION[1];

            pDocPos.GetRange(beginning, end);

            ppEnum = new AD7CodeContextEnum(new[] { new AD7MemoryAddress(this, filename, (uint)beginning[0].dwLine) });
            return VSConstants.S_OK;
        }

        // EnumCodePaths is used for the step-into specific feature -- right click on the current statment and decide which
        // function to step into. This is not something that we support.
        public int EnumCodePaths(string hint, IDebugCodeContext2 start, IDebugStackFrame2 frame, int fSource, out IEnumCodePaths2 pathEnum, out IDebugCodeContext2 safetyContext) {
            pathEnum = null;
            safetyContext = null;
            return VSConstants.E_NOTIMPL;
        }

        // EnumModules is called by the debugger when it needs to enumerate the modules in the program.
        public int EnumModules(out IEnumDebugModules2 ppEnum) {
            if (_mixedMode) {
                ppEnum = null;
                return VSConstants.E_NOTIMPL;
            }

            AssertMainThread();

            AD7Module[] moduleObjects = new AD7Module[_modules.Count];
            int i = 0;
            foreach (var keyValue in _modules) {
                var module = keyValue.Key;
                var adModule = keyValue.Value;

                moduleObjects[i++] = adModule;
            }

            ppEnum = new AD7ModuleEnum(moduleObjects);

            return VSConstants.S_OK;
        }

        // EnumThreads is called by the debugger when it needs to enumerate the threads in the program.
        public int EnumThreads(out IEnumDebugThreads2 ppEnum) {
            if (_mixedMode) {
                ppEnum = null;
                return VSConstants.E_NOTIMPL;
            }

            AssertMainThread();

            AD7Thread[] threadObjects = new AD7Thread[_threads.Count];
            int i = 0;
            foreach (var keyValue in _threads) {
                var thread = keyValue.Key;
                var adThread = keyValue.Value;

                Debug.Assert(adThread != null);
                threadObjects[i++] = adThread;
            }

            ppEnum = new AD7ThreadEnum(threadObjects);

            return VSConstants.S_OK;
        }

        // The properties returned by this method are specific to the program. If the program needs to return more than one property, 
        // then the IDebugProperty2 object returned by this method is a container of additional properties and calling the 
        // IDebugProperty2::EnumChildren method returns a list of all properties.
        // A program may expose any number and type of additional properties that can be described through the IDebugProperty2 interface. 
        // An IDE might display the additional program properties through a generic property browser user interface.
        public int GetDebugProperty(out IDebugProperty2 ppProperty) {
            throw new Exception("The method or operation is not implemented.");
        }

        // The debugger calls this when it needs to obtain the IDebugDisassemblyStream2 for a particular code-context.
        public int GetDisassemblyStream(enum_DISASSEMBLY_STREAM_SCOPE dwScope, IDebugCodeContext2 codeContext, out IDebugDisassemblyStream2 disassemblyStream) {
            disassemblyStream = null;
            return VSConstants.E_NOTIMPL;
        }

        // This method gets the Edit and Continue (ENC) update for this program. A custom debug engine always returns E_NOTIMPL
        public int GetENCUpdate(out object update) {
            update = null;
            return VSConstants.S_OK;
        }

        // Gets the name and identifier of the debug engine (DE) running this program.
        public int GetEngineInfo(out string engineName, out Guid engineGuid) {
            engineName = "Python";
            engineGuid = new Guid(DebugEngineId);
            return VSConstants.S_OK;
        }

        // The memory bytes as represented by the IDebugMemoryBytes2 object is for the program's image in memory and not any memory 
        // that was allocated when the program was executed.
        public int GetMemoryBytes(out IDebugMemoryBytes2 ppMemoryBytes) {
            throw new Exception("The method or operation is not implemented.");
        }

        // Gets the name of the program.
        // The name returned by this method is always a friendly, user-displayable name that describes the program.
        public int GetName(out string programName) {
            // The engine uses default transport and doesn't need to customize the name of the program,
            // so return NULL.
            programName = null;
            return VSConstants.S_OK;
        }

        // Gets a GUID for this program. A debug engine (DE) must return the program identifier originally passed to the IDebugProgramNodeAttach2::OnAttach
        // or IDebugEngine2::Attach methods. This allows identification of the program across debugger components.
        public int GetProgramId(out Guid guidProgramId) {
            guidProgramId = _ad7ProgramId;
            return guidProgramId == Guid.Empty ? VSConstants.E_FAIL : VSConstants.S_OK;
        }

        // This method is deprecated. Use the IDebugProcess3::Step method instead.

        /// <summary>
        /// Performs a step. 
        /// 
        /// In case there is any thread synchronization or communication between threads, other threads in the program should run when a particular thread is stepping.
        /// </summary>
        public int Step(IDebugThread2 pThread, enum_STEPKIND sk, enum_STEPUNIT Step) {
            if (_mixedMode) {
                return VSConstants.E_NOTIMPL;
            }

            var thread = ((AD7Thread)pThread).GetDebuggedThread();
            switch (sk) {
                case enum_STEPKIND.STEP_INTO: thread.StepInto(); break;
                case enum_STEPKIND.STEP_OUT: thread.StepOut(); break;
                case enum_STEPKIND.STEP_OVER: thread.StepOver(); break;
            }
            return VSConstants.S_OK;
        }

        // Terminates the program.
        public int Terminate() {
            Debug.WriteLine("PythonEngine Terminate");
            // Because we implement IDebugEngineLaunch2 we will terminate
            // the process in IDebugEngineLaunch2.TerminateProcess
            return VSConstants.S_OK;
        }

        // Writes a dump to a file.
        public int WriteDump(enum_DUMPTYPE DUMPTYPE, string pszDumpUrl) {
            return VSConstants.E_NOTIMPL;
        }

        #endregion

        #region IDebugProgram3 Members

        // ExecuteOnThread is called when the SDM wants execution to continue and have 
        // stepping state cleared.  See http://msdn.microsoft.com/en-us/library/bb145596.aspx for a
        // description of different ways we can resume.
        public int ExecuteOnThread(IDebugThread2 pThread) {
            if (_mixedMode) {
                return VSConstants.E_NOTIMPL;
            }

            AssertMainThread();

            // clear stepping state on the thread the user was currently on
            AD7Thread thread = (AD7Thread)pThread;
            thread.GetDebuggedThread().ClearSteppingState();

            ResumeProcess();

            return VSConstants.S_OK;
        }

        private void ResumeProcess() {
            _process.Resume();
        }

        #endregion

        #region IDebugSymbolSettings100 members

        public int SetSymbolLoadState(int bIsManual, int bLoadAdjacent, string strIncludeList, string strExcludeList) {
            // The SDM will call this method on the debug engine when it is created, to notify it of the user's
            // symbol settings in Tools->Options->Debugging->Symbols.
            //
            // Params:
            // bIsManual: true if 'Automatically load symbols: Only for specified modules' is checked
            // bLoadAdjacent: true if 'Specify modules'->'Always load symbols next to the modules' is checked
            // strIncludeList: semicolon-delimited list of modules when automatically loading 'Only specified modules'
            // strExcludeList: semicolon-delimited list of modules when automatically loading 'All modules, unless excluded'

            return VSConstants.S_OK;
        }

        #endregion

        #region Deprecated interface methods
        // These methods are not called by the Visual Studio debugger, so they don't need to be implemented

        int IDebugEngine2.EnumPrograms(out IEnumDebugPrograms2 programs) {
            Debug.Fail("This function is not called by the debugger");

            programs = null;
            return VSConstants.E_NOTIMPL;
        }

        public int Attach(IDebugEventCallback2 pCallback) {
            Debug.Fail("This function is not called by the debugger");

            return VSConstants.E_NOTIMPL;
        }

        public int GetProcess(out IDebugProcess2 process) {
            Debug.Fail("This function is not called by the debugger");

            process = null;
            return VSConstants.E_NOTIMPL;
        }

        public int Execute() {
            Debug.Fail("This function is not called by the debugger.");
            return VSConstants.E_NOTIMPL;
        }

        #endregion

        #region Events

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

        private void AttachEvents(PythonProcess process) {
            process.Connected += OnConnected;
            process.ProcessLoaded += OnProcessLoaded;
            process.ModuleLoaded += OnModuleLoaded;
            process.ThreadCreated += OnThreadCreated;

            process.BreakpointBindFailed += OnBreakpointBindFailed;
            process.BreakpointBindSucceeded += OnBreakpointBindSucceeded;

            process.BreakpointHit += OnBreakpointHit;
            process.EntryPointHit += OnEntryPointHit;
            process.AsyncBreakComplete += OnAsyncBreakComplete;
            process.ExceptionRaised += OnExceptionRaised;
            process.ProcessExited += OnProcessExited;
            process.StepComplete += OnStepComplete;
            process.ThreadExited += OnThreadExited;
            process.DebuggerOutput += OnDebuggerOutput;

            process.StartListening();
        }

        private void OnThreadExited(object sender, ThreadEventArgs e) {
            // TODO: Thread exit code
            var oldThread = _threads[e.Thread];
            _threads.Remove(e.Thread);
            oldThread.Dispose();

            Send(new AD7ThreadDestroyEvent(0), AD7ThreadDestroyEvent.IID, oldThread);
        }

        private void OnThreadCreated(object sender, ThreadEventArgs e) {
            Debug.WriteLine("Thread created:  " + e.Thread.Id);
            var newThread = new AD7Thread(this, e.Thread);
            _threads.Add(e.Thread, newThread);

            lock (_syncLock) {
                if (_programCreated) {
                    SendThreadStart(newThread);
                } else {
                    _startThread = newThread;
                }
            }
        }

        private void OnStepComplete(object sender, ThreadEventArgs e) {
            Send(new AD7SteppingCompleteEvent(), AD7SteppingCompleteEvent.IID, _threads[e.Thread]);
        }

        private void OnConnected(object sender, EventArgs e) {
            lock (_syncLock) {
                if (_engineCreated) {
                    SendProgramCreate();
                } else {
                    Debug.WriteLine("Delaying program create " + GetHashCode());
                    _isProgramCreateDelayed = true;
                }
            }
        }

        private void OnProcessLoaded(object sender, ThreadEventArgs e) {
            lock (_syncLock) {
                if (_pseudoAttach) {
                    _process.Unregister();
                }

                if (_programCreated) {
                    // we've delviered the program created event, deliver the load complete event
                    SendLoadComplete(_threads[e.Thread]);
                } else {
                    Debug.WriteLine("Delaying load complete " + GetHashCode() + " on thread " + _threads[e.Thread].GetDebuggedThread().Id);
                    // we haven't delivered the program created event, wait until we do to deliver the process loaded event.
                    _processLoadedThread = _threads[e.Thread];
                }
            }
        }

        private void OnProcessExited(object sender, ProcessExitedEventArgs e) {
            try {
                Send(new AD7ProgramDestroyEvent((uint)e.ExitCode), AD7ProgramDestroyEvent.IID, null);
            } catch (InvalidOperationException) {
                // we can race at shutdown and deliver the event after the debugger is shutting down.
            }
        }

        private void OnModuleLoaded(object sender, ModuleLoadedEventArgs e) {
            lock (_syncLock) {
                var adModule = _modules[e.Module] = new AD7Module(e.Module);
                if (_programCreated) {
                    SendModuleLoaded(adModule);
                } else {
                    _startModule = adModule;
                }
            }
        }

        private void OnExceptionRaised(object sender, ExceptionRaisedEventArgs e) {
            // Exception events are sent when an exception occurs in the debuggee that the debugger was not expecting.
            AD7Thread thread;
            if (_threads.TryGetValue(e.Thread, out thread)) {
                Send(
                    new AD7DebugExceptionEvent(e.Exception.TypeName, e.Exception.Description, e.IsUnhandled),
                    AD7DebugExceptionEvent.IID,
                    thread
                );
            }
        }

        private void OnBreakpointHit(object sender, BreakpointHitEventArgs e) {
            var boundBreakpoints = new[] { _breakpointManager.GetBreakpoint(e.Breakpoint) };

            // An engine that supports more advanced breakpoint features such as hit counts, conditions and filters
            // should notify each bound breakpoint that it has been hit and evaluate conditions here.

            Send(new AD7BreakpointEvent(new AD7BoundBreakpointsEnum(boundBreakpoints)), AD7BreakpointEvent.IID, _threads[e.Thread]);
        }

        private void OnEntryPointHit(object sender, ThreadEventArgs e) {
            Send(new AD7EntryPointEvent(), AD7EntryPointEvent.IID, _threads[e.Thread]);
        }

        private void OnBreakpointBindSucceeded(object sender, BreakpointEventArgs e) {
            IDebugPendingBreakpoint2 pendingBreakpoint;
            var boundBreakpoint = _breakpointManager.GetBreakpoint(e.Breakpoint);
            ((IDebugBoundBreakpoint2)boundBreakpoint).GetPendingBreakpoint(out pendingBreakpoint);

            Send(
                new AD7BreakpointBoundEvent((AD7PendingBreakpoint)pendingBreakpoint, boundBreakpoint),
                AD7BreakpointBoundEvent.IID,
                null
            );
        }

        private void OnBreakpointBindFailed(object sender, BreakpointEventArgs e) {
        }

        private void OnAsyncBreakComplete(object sender, ThreadEventArgs e) {
            AD7Thread thread;
            if (!_threads.TryGetValue(e.Thread, out thread)) {
                _threads[e.Thread] = thread = new AD7Thread(this, e.Thread);
            }
            Send(new AD7AsyncBreakCompleteEvent(), AD7AsyncBreakCompleteEvent.IID, thread);
        }

        private void OnDebuggerOutput(object sender, OutputEventArgs e) {
            AD7Thread thread;
            if (!_threads.TryGetValue(e.Thread, out thread)) {
                _threads[e.Thread] = thread = new AD7Thread(this, e.Thread);
            }

            Send(new AD7DebugOutputStringEvent2(e.Output), AD7DebugOutputStringEvent2.IID, thread);
        }

        #endregion

        /// <summary>
        /// Returns information about the given stack frame for the given process and thread ID.
        /// 
        /// If the process, thread, or frame are unknown the null is returned.
        /// 
        /// New in 1.5.
        /// </summary>
        public static IDebugDocumentContext2 GetCodeMappingDocument(int processId, int threadId, int frame) {
            if (frame >= 0) {
                foreach (var engineRef in _engines) {
                    var engine = engineRef.Target as AD7Engine;
                    if (engine != null) {
                        if (engine._process.Id == processId) {
                            foreach (var thread in engine._threads.Keys) {
                                if (thread.Id == threadId) {
                                    var frames = thread.Frames;

                                    if (frame < frames.Count) {
                                        var curFrame = thread.Frames[frame];

                                        switch (curFrame.Kind) {
                                            case FrameKind.Django:
                                                var djangoFrame = (DjangoStackFrame)curFrame;

                                                return new AD7DocumentContext(djangoFrame.SourceFile,
                                                    new TEXT_POSITION() { dwLine = (uint)djangoFrame.SourceLine, dwColumn = 0 },
                                                    new TEXT_POSITION() { dwLine = (uint)djangoFrame.SourceLine, dwColumn = 0 },
                                                    new AD7MemoryAddress(engine, djangoFrame.SourceFile, (uint)djangoFrame.SourceLine, curFrame),
                                                    FrameKind.Python
                                                );
                                            default:
                                                return null;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }
    }
}

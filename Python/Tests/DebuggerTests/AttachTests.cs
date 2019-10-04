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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Parsing;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.Debugger.Remote;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace DebuggerTests {
    [TestClass, Ignore]
    public abstract class AttachTests : BaseDebuggerTests {
        [TestInitialize]
        public void CheckVersion() {
            Version.AssertInstalled();
        }

        internal override PythonVersion Version {
            get {
                throw new NotImplementedException("Do not invoke tests on base class");
            }
        }

        string CreateString {
            get {
                if (Version.Version < PythonLanguageVersion.V30) {
                    return "PyString_FromString";
                } else if (Version.Version < PythonLanguageVersion.V33) {
                    return "PyUnicodeUCS2_FromString";
                } else {
                    return "PyUnicode_FromString";
                }
            }
        }

        #region Attach Tests

        /// <summary>
        /// threading module imports thread.start_new_thread, verifies that we patch threading's method
        /// in addition to patching the thread method so that breakpoints on threads created after
        /// attach via the threading module can be hit.
        /// </summary>
        [TestMethod, Priority(3)]
        public virtual async Task AttachThreadingStartNewThread() {
            // http://pytools.codeplex.com/workitem/638
            // http://pytools.codeplex.com/discussions/285741#post724014
            var psi = new ProcessStartInfo(Version.InterpreterPath, "-B \"" + TestData.GetPath(@"TestData\DebuggerProject\ThreadingStartNewThread.py") + "\"");
            psi.WorkingDirectory = TestData.GetPath(@"TestData\DebuggerProject");
            psi.EnvironmentVariables["PYTHONPATH"] = @"..\..";
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            Process p = Process.Start(psi);
            try {
                using (var dumpWriter = new MiniDumpWriter(p)) {
                    if (p.WaitForExit(1000)) {
                        Assert.Fail("Process exited");
                    }

                    var proc = PythonProcess.Attach(p.Id, debugLog: DebugLog);
                    try {
                        var attached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        var readyToContinue = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        var threadBreakpointHit = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                        proc.ProcessLoaded += async (sender, args) => {
                            attached.TrySetResult(true);
                            var bp = proc.AddBreakpoint("ThreadingStartNewThread.py", 9);
                            await bp.AddAsync(TimeoutToken());

                            bp = proc.AddBreakpoint("ThreadingStartNewThread.py", 5);
                            await bp.AddAsync(TimeoutToken());

                            await proc.ResumeAsync(TimeoutToken());
                        };

                        PythonThread mainThread = null;
                        PythonThread bpThread = null;
                        bool wrongLine = false;
                        proc.BreakpointHit += async (sender, args) => {
                            if (args.Breakpoint.LineNo == 9) {
                                // stop running the infinite loop
                                DebugLog?.WriteLine(String.Format("First BP hit {0}", args.Thread.Id));
                                mainThread = args.Thread;
                                await args.Thread.Frames[0].ExecuteTextAsync(
                                    "x = False",
                                    x => readyToContinue.TrySetResult(true),
                                    TimeoutToken());
                            } else if (args.Breakpoint.LineNo == 5) {
                                // we hit the breakpoint on the new thread
                                DebugLog?.WriteLine(String.Format("Second BP hit {0}", args.Thread.Id));
                                bpThread = args.Thread;
                                threadBreakpointHit.TrySetResult(true);
                                await proc.ResumeAsync(TimeoutToken());
                            } else {
                                DebugLog?.WriteLine(String.Format("Hit breakpoint on wrong line number: {0}", args.Breakpoint.LineNo));
                                wrongLine = true;
                                attached.TrySetResult(true);
                                threadBreakpointHit.TrySetResult(true);
                                await proc.ResumeAsync(TimeoutToken());
                            }
                        };

                        await proc.StartListeningAsync();
                        await attached.Task.WithTimeout(30000, "Failed to attach within 30s");

                        await readyToContinue.Task.WithTimeout(30000, "Failed to hit the main thread breakpoint within 30s");
                        await proc.ResumeAsync(TimeoutToken());

                        await threadBreakpointHit.Task.WithTimeout(30000, "Failed to hit the background thread breakpoint within 30s");
                        Assert.IsFalse(wrongLine, "Breakpoint broke on the wrong line");

                        Assert.AreNotEqual(mainThread, bpThread);
                    } finally {
                        await DetachProcessAsync(proc);
                    }
                }
            } finally {
                DisposeProcess(p);
            }
        }

        [TestMethod, Priority(TestExtensions.P2_FAILING_UNIT_TEST)]
        [TestCategory("10s"), TestCategory("60s")]
        public virtual async Task AttachReattach() {
            Process p = Process.Start(Version.InterpreterPath, "-B \"" + TestData.GetPath(@"TestData\DebuggerProject\InfiniteRun.py") + "\"");
            try {
                using (var dumpWriter = new MiniDumpWriter(p)) {
                    Thread.Sleep(1000);

                    for (int i = 0; i < 10; i++) {
                        Console.WriteLine(i);

                        var attached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        var detached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                        var proc = PythonProcess.Attach(p.Id, debugLog: DebugLog);

                        proc.ProcessLoaded += (sender, args) => {
                            attached.SetResult(true);
                        };
                        proc.ProcessExited += (sender, args) => {
                            detached.SetResult(true);
                        };
                        await proc.StartListeningAsync(20000);

                        await attached.Task.WithTimeout(10000, "Failed to attach within 10s");
                        await proc.DetachAsync(TimeoutToken());
                        await detached.Task.WithTimeout(10000, "Failed to detach within 10s");
                    }

                    dumpWriter.Cancel();
                }
            } finally {
                DisposeProcess(p);
            }
        }

        /// <summary>
        /// When we do the attach one thread is blocked in native code.  We attach, resume execution, and that
        /// thread should eventually wake up.  
        /// 
        /// The bug was two issues, when doing a resume all:
        ///		1) we don't clear the stepping if it's STEPPING_ATTACH_BREAK
        ///		2) We don't clear the stepping if we haven't yet blocked the thread
        ///		
        /// Because the thread is blocked in native code, and we don't clear the stepping, when the user
        /// hits resume the thread will eventually return back to Python code, and then we'll block it
        /// because we haven't cleared the stepping bit.
        /// </summary>
        [TestMethod, Priority(3)]
        [TestCategory("10s")]
        public virtual async Task AttachMultithreadedSleeper() {
            // http://pytools.codeplex.com/discussions/285741 1/12/2012 6:20 PM
            Process p = Process.Start(Version.InterpreterPath, "-B \"" + TestData.GetPath(@"TestData\DebuggerProject\AttachMultithreadedSleeper.py") + "\"");
            try {
                using (var dumpWriter = new MiniDumpWriter(p)) {
                    Thread.Sleep(1000);

                    var attached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                    var proc = PythonProcess.Attach(p.Id, debugLog: DebugLog);
                    try {
                        proc.ProcessLoaded += (sender, args) => {
                            attached.SetResult(true);
                        };
                        await proc.StartListeningAsync();

                        await attached.Task.WithTimeout(10000, "Failed to attach within 10s");
                        await proc.ResumeAsync(TimeoutToken());
                        DebugLog?.WriteLine("Waiting for exit");
                    } finally {
                        WaitForExit(proc);
                    }

                    dumpWriter.Cancel();
                }
            } finally {
                DisposeProcess(p);
            }
        }

        /// <summary>
        /// Python 3.2 changes the rules about when we can call Py_InitThreads.
        /// 
        /// http://pytools.codeplex.com/workitem/834
        /// </summary>
        [TestMethod, Priority(3)]
        public virtual async Task AttachSingleThreadedSleeper() {
            // http://pytools.codeplex.com/discussions/285741 1/12/2012 6:20 PM
            Process p = Process.Start(Version.InterpreterPath, "-B \"" + TestData.GetPath(@"TestData\DebuggerProject\AttachSingleThreadedSleeper.py") + "\"");
            try {
                Thread.Sleep(1000);

                var attached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                var proc = PythonProcess.Attach(p.Id, debugLog: DebugLog);
                try {
                    proc.ProcessLoaded += (sender, args) => {
                        attached.SetResult(true);
                    };
                    await proc.StartListeningAsync();

                    using (var dumpWriter = new MiniDumpWriter(p)) {
                        await attached.Task.WithTimeout(10000, "Failed to attach within 10s");
                        await proc.ResumeAsync(TimeoutToken());
                        DebugLog?.WriteLine("Waiting for exit");
                        dumpWriter.Cancel();
                    }
                } finally {
                    TerminateProcess(proc);
                }
            } finally {
                DisposeProcess(p);
            }
        }

        [TestMethod, Priority(2)]
        [TestCategory("10s")]
        public virtual async Task AttachReattachThreadingInited() {
            Process p = Process.Start(Version.InterpreterPath, "-B \"" + TestData.GetPath(@"TestData\DebuggerProject\InfiniteRunThreadingInited.py") + "\"");
            try {
                using (var dumpWriter = new MiniDumpWriter(p)) {
                    Thread.Sleep(1000);

                    for (int i = 0; i < 10; i++) {
                        Console.WriteLine(i);

                        var attached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        var detached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                        var proc = PythonProcess.Attach(p.Id, debugLog: DebugLog);
                        proc.ProcessLoaded += (sender, args) => {
                            attached.SetResult(true);
                        };
                        proc.ProcessExited += (sender, args) => {
                            detached.SetResult(true);
                        };
                        await proc.StartListeningAsync();

                        await attached.Task.WithTimeout(10000, "Failed to attach within 10s");
                        await proc.DetachAsync(TimeoutToken());
                        await detached.Task.WithTimeout(10000, "Failed to detach within 10s");
                    }

                    dumpWriter.Cancel();
                }
            } finally {
                DisposeProcess(p);
            }
        }

        [TestMethod, Priority(3)]
        [TestCategory("10s")]
        public virtual async Task AttachReattachInfiniteThreads() {
            Process p = Process.Start(Version.InterpreterPath, "-B \"" + TestData.GetPath(@"TestData\DebuggerProject\InfiniteThreads.py") + "\"");
            try {
                using (var dumpWriter = new MiniDumpWriter(p)) {
                    Thread.Sleep(1000);

                    for (int i = 0; i < 10; i++) {
                        Console.WriteLine(i);

                        var attached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        var detached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                        var proc = PythonProcess.Attach(p.Id, debugLog: DebugLog);
                        proc.ProcessLoaded += (sender, args) => {
                            attached.SetResult(true);
                        };
                        proc.ProcessExited += (sender, args) => {
                            detached.SetResult(true);
                        };
                        await proc.StartListeningAsync();

                        await attached.Task.WithTimeout(30000, "Failed to attach within 30s");
                        await proc.DetachAsync(TimeoutToken());
                        await detached.Task.WithTimeout(30000, "Failed to detach within 30s");
                    }

                    dumpWriter.Cancel();
                }
            } finally {
                DisposeProcess(p);
            }
        }

        [TestMethod, Priority(TestExtensions.P2_FAILING_UNIT_TEST)]
        [TestCategory("10s")]
        public virtual async Task AttachTimeout() {
            string cast = "(PyCodeObject*)";
            if (Version.Version >= PythonLanguageVersion.V32) {
                // 3.2 changed the API here...
                cast = "";
            }

            var hostCode = @"#include <python.h>
#include <windows.h>
#include <stdio.h>

int main(int argc, char* argv[]) {
    Py_SetPythonHome($PYTHON_HOME);
    Py_Initialize();
    auto event = OpenEventA(EVENT_ALL_ACCESS, FALSE, argv[1]);
    if(!event) {
        printf(""Failed to open event\r\n"");
    }
    printf(""Waiting for event\r\n"");
    if(WaitForSingleObject(event, INFINITE)) {
        printf(""Wait failed\r\n"");
    }

    auto loc = PyDict_New ();
    auto glb = PyDict_New ();

    auto src = " + cast + @"Py_CompileString (""while 1:\n    pass"", ""<stdin>"", Py_file_input);

    if(src == nullptr) {
        printf(""Failed to compile code\r\n"");
    }
    printf(""Executing\r\n"");
    PyEval_EvalCode(src, glb, loc);
}";
            await AttachTestTimeoutAsync(hostCode);
        }

        /// <summary>
        /// Attempts to attach w/ code only running on new threads which are initialized using PyGILState_Ensure
        /// </summary>
        [TestMethod, Priority(TestExtensions.P2_FAILING_UNIT_TEST)]
        [TestCategory("10s")]
        public virtual async Task AttachNewThread_PyGILState_Ensure() {
            var hostCode = @"#include <Python.h>
#include <Windows.h>
#include <process.h>

PyObject *g_pFunc;

void Thread(void*)
{
    printf(""Worker thread started %x\r\n"", GetCurrentThreadId());
    while (true)
    {
        PyGILState_STATE state = PyGILState_Ensure();
        PyObject *pValue;

        pValue = PyObject_CallObject(g_pFunc, 0);
        if (!pValue) {
            PyErr_Print();
            return;
        }
        Py_DECREF(pValue);
        PyGILState_Release(state);

        Sleep(1000);
    }
}

void main()
{
    PyObject *pName, *pModule;

    Py_SetPythonHome($PYTHON_HOME);
    Py_Initialize();
    PyEval_InitThreads();

    pModule = PyImport_ImportModule(""gilstate_attach"");

    if (pModule != NULL) {
        g_pFunc = PyObject_GetAttrString(pModule, ""test"");

        if (g_pFunc && PyCallable_Check(g_pFunc))
        {
            DWORD threadID;
            threadID = _beginthread(&Thread, 1024*1024, 0);
            threadID = _beginthread(&Thread, 1024*1024, 0);

            PyEval_ReleaseLock();
            while (true);
        }
        else
        {
            if (PyErr_Occurred())
                PyErr_Print();
        }
        Py_XDECREF(g_pFunc);
        Py_DECREF(pModule);
    }
    else
    {
        PyErr_Print();
        return;
    }
    Py_Finalize();
    return;
}";
            var exe = CompileCode(hostCode);

            File.WriteAllText(Path.Combine(Path.GetDirectoryName(exe), "gilstate_attach.py"), @"def test():
    import sys
    print(sys.gettrace() or ""(no trace function)"")
    for i in range(1):
        print(i)

    return 0");


            // start the test process w/ our handle
            Process p = RunHost(exe);
            try {
                using (var dumpWriter = new MiniDumpWriter(p)) {
                    Thread.Sleep(1500);

                    var attached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var bpHit = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                    var proc = PythonProcess.Attach(p.Id, PythonDebugOptions.RedirectOutput, debugLog: DebugLog);
                    try {
                        proc.ProcessLoaded += (sender, args) => {
                            Console.WriteLine("Process loaded");
                            attached.SetResult(true);
                        };
                        proc.DebuggerOutput += (sender, args) => {
                            Console.WriteLine(args.Output ?? "");
                        };
                        await proc.StartListeningAsync();

                        await attached.Task.WithTimeout(20000, "Failed to attach within 20s");

                        proc.BreakpointHit += (sender, args) => {
                            Console.WriteLine("Breakpoint hit");
                            bpHit.SetResult(true);
                        };

                        var bp = proc.AddBreakpoint("gilstate_attach.py", 2);
                        await bp.AddAsync(TimeoutToken());

                        await bpHit.Task.WithTimeout(20000, "Failed to hit breakpoint within 20s");
                    } finally {
                        await DetachProcessAsync(proc);
                    }

                    dumpWriter.Cancel();
                }
            } finally {
                DisposeProcess(p);
            }
        }

        /// <summary>
        /// Attempts to attach w/ code only running on new threads which are initialized using PyThreadState_New
        /// </summary>
        [TestMethod, Priority(TestExtensions.P2_FAILING_UNIT_TEST)]
        [TestCategory("10s")]
        public virtual async Task AttachNewThread_PyThreadState_New() {
            var hostCode = @"#include <Windows.h>
#include <process.h>
#include <Python.h>

PyObject *g_pFunc;

void Thread(void*)
{
    printf(""Worker thread started %x\r\n"", GetCurrentThreadId());
    while (true)
    {
        PyEval_AcquireLock();
        PyInterpreterState* pMainInterpreterState = PyInterpreterState_Head();
        auto pThisThreadState = PyThreadState_New(pMainInterpreterState);
        PyThreadState_Swap(pThisThreadState);

        PyObject *pValue;

        pValue = PyObject_CallObject(g_pFunc, 0);
        if (pValue != NULL) {
            //printf(""Result of call: %ld\n"", PyInt_AsLong(pValue));
            Py_DECREF(pValue);
        }
        else {
            PyErr_Print();
            return;
        }

        PyThreadState_Swap(NULL);
        PyThreadState_Clear(pThisThreadState);
        PyThreadState_Delete(pThisThreadState);
        PyEval_ReleaseLock();

        Sleep(1000);
    }
}

void main()
{
    PyObject *pName, *pModule;

    Py_SetPythonHome($PYTHON_HOME);
    Py_Initialize();
    PyEval_InitThreads();
    pName = CREATE_STRING(""gilstate_attach"");

    pModule = PyImport_Import(pName);
    Py_DECREF(pName);

    if (pModule != NULL) {
        g_pFunc = PyObject_GetAttrString(pModule, ""test"");

        if (g_pFunc && PyCallable_Check(g_pFunc))
        {
            DWORD threadID;
            threadID = _beginthread(&Thread, 1024*1024, 0);
            threadID = _beginthread(&Thread, 1024*1024, 0);
            PyEval_ReleaseLock();

            while (true);
        }
        else
        {
            if (PyErr_Occurred())
                PyErr_Print();
        }
        Py_XDECREF(g_pFunc);
        Py_DECREF(pModule);
    }
    else
    {
        PyErr_Print();
        return;
    }
    Py_Finalize();
    return;
}".Replace("CREATE_STRING", CreateString);
            var exe = CompileCode(hostCode);

            File.WriteAllText(Path.Combine(Path.GetDirectoryName(exe), "gilstate_attach.py"), @"def test():
    for i in range(10):
        print(i)

    return 0");


            // start the test process w/ our handle
            Process p = RunHost(exe);
            try {
                using (var dumpWriter = new MiniDumpWriter(p)) {
                    Thread.Sleep(1500);

                    var attached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var bpHit = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                    var proc = PythonProcess.Attach(p.Id, debugLog: DebugLog);
                    try {
                        proc.ProcessLoaded += (sender, args) => {
                            Console.WriteLine("Process loaded");
                            attached.SetResult(true);
                        };
                        await proc.StartListeningAsync();

                        await attached.Task.WithTimeout(20000, "Failed to attach within 20s");

                        proc.BreakpointHit += (sender, args) => {
                            Console.WriteLine("Breakpoint hit");
                            bpHit.SetResult(true);
                        };

                        var bp = proc.AddBreakpoint("gilstate_attach.py", 3);
                        await bp.AddAsync(TimeoutToken());

                        await bpHit.Task.WithTimeout(20000, "Failed to hit breakpoint within 20s");
                    } finally {
                        await DetachProcessAsync(proc);
                    }

                    dumpWriter.Cancel();
                }
            } finally {
                DisposeProcess(p);
            }
        }

        [TestMethod, Priority(TestExtensions.P2_FAILING_UNIT_TEST)]
        [TestCategory("10s")]
        public virtual async Task AttachTimeoutThreadsInitialized() {
            string cast = "(PyCodeObject*)";
            if (Version.Version >= PythonLanguageVersion.V32) {
                // 3.2 changed the API here...
                cast = "";
            }

            var hostCode = @"#include <python.h>
#include <windows.h>

int main(int argc, char* argv[]) {
    Py_SetPythonHome($PYTHON_HOME);
    Py_Initialize();
    PyEval_InitThreads();

    auto event = OpenEventA(EVENT_ALL_ACCESS, FALSE, argv[1]);
    WaitForSingleObject(event, INFINITE);

    auto loc = PyDict_New ();
    auto glb = PyDict_New ();

    auto src = " + cast + @"Py_CompileString (""while 1:\n    pass"", ""<stdin>"", Py_file_input);

    if(src == nullptr) {
        printf(""Failed to compile code\r\n"");
    }
    printf(""Executing\r\n"");
    PyEval_EvalCode(src, glb, loc);
}";
            await AttachTestTimeoutAsync(hostCode);
        }

        private async Task AttachTestTimeoutAsync(string hostCode) {
            var exe = CompileCode(hostCode);

            // start the test process w/ our handle
            var eventName = Guid.NewGuid().ToString();
            EventWaitHandle handle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            ProcessStartInfo psi = new ProcessStartInfo(exe, eventName);
            psi.UseShellExecute = false;
            psi.RedirectStandardError = psi.RedirectStandardOutput = true;
            psi.CreateNoWindow = true;
            // Add Python to PATH so that the host can locate the DLL in case it's not in \Windows\System32 (e.g. for EPD)
            psi.EnvironmentVariables["PATH"] = Environment.GetEnvironmentVariable("PATH") + ";" + Path.GetDirectoryName(Version.InterpreterPath);

            Process p = Process.Start(psi);
            var outRecv = new OutputReceiver();
            p.OutputDataReceived += outRecv.OutputDataReceived;
            p.ErrorDataReceived += outRecv.OutputDataReceived;
            p.BeginErrorReadLine();
            p.BeginOutputReadLine();

            try {
                using (var dumpWriter = new MiniDumpWriter(p)) {
                    bool isAttached = false;

                    // Start the attach with the GIL held.

                    var attachStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var attachDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                    // We run the Attach and StartListeningAsync on a separate thread,
                    // because StartListeningAsync waits until debuggee has connected
                    // back (which it won't do until handle is set).
                    var attachTask = Task.Run(async () => {
                        var proc = PythonProcess.Attach(p.Id, debugLog: DebugLog);
                        try {
                            proc.ProcessLoaded += (sender, args) => {
                                attachDone.SetResult(true);
                                isAttached = true;
                            };

                            attachStarted.SetResult(true);
                            await proc.StartListeningAsync(10000);
                        } finally {
                            await DetachProcessAsync(proc);
                        }
                    });

                    await Task.WhenAny(attachTask, attachStarted.Task).Unwrap().WithTimeout(10000, "Failed to start attaching within 10s");
                    Assert.IsFalse(isAttached, "Should not have attached yet"); // we should be blocked

                    handle.Set();   // let the code start running

                    await Task.WhenAny(attachTask, attachDone.Task).Unwrap().WithTimeout(10000, "Failed to attach within 10s");
                    dumpWriter.Cancel();
                }
            } finally {
                DebugLog?.WriteLine(String.Format("Process output: {0}", outRecv.Output.ToString()));
                DisposeProcess(p);
            }
        }

        [TestMethod, Priority(3)]
        [TestCategory("10s")]
        public virtual async Task AttachAndStepWithBlankSysPrefix() {
            string script = TestData.GetPath(@"TestData\DebuggerProject\InfiniteRunBlankPrefix.py");
            var p = Process.Start(Version.InterpreterPath, "-B \"" + script + "\"");
            try {
                using (var dumpWriter = new MiniDumpWriter(p)) {
                    Thread.Sleep(1000);
                    var proc = PythonProcess.Attach(p.Id, debugLog: DebugLog);
                    try {
                        var attached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        proc.ProcessLoaded += async (sender, args) => {
                            Console.WriteLine("Process loaded");
                            await proc.ResumeAsync(TimeoutToken());
                            attached.SetResult(true);
                        };
                        await proc.StartListeningAsync();
                        await attached.Task.WithTimeout(20000, "Failed to attach within 20s");

                        var bpHit = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        PythonThread thread = null;
                        PythonStackFrame oldFrame = null;
                        proc.BreakpointHit += (sender, args) => {
                            Console.WriteLine("Breakpoint hit");
                            thread = args.Thread;
                            oldFrame = args.Thread.Frames[0];
                            bpHit.SetResult(true);
                        };
                        var bp = proc.AddBreakpoint(script, 6);
                        await bp.AddAsync(TimeoutToken());
                        await bpHit.Task.WithTimeout(20000, "Failed to hit breakpoint within 20s");

                        var stepComplete = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        PythonStackFrame newFrame = null;
                        proc.StepComplete += (sender, args) => {
                            newFrame = args.Thread.Frames[0];
                            stepComplete.SetResult(true);
                        };
                        await thread.StepOverAsync(TimeoutToken());
                        await stepComplete.Task.WithTimeout(20000, "Failed to complete the step within 20s");

                        Assert.AreEqual(oldFrame.FileName, newFrame.FileName);
                        Assert.IsTrue(oldFrame.LineNo + 1 == newFrame.LineNo);
                    } finally {
                        await DetachProcessAsync(proc);
                    }

                    dumpWriter.Cancel();
                }
            } finally {
                DisposeProcess(p);
            }
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        [TestCategory("10s")]
        public virtual async Task AttachWithOutputRedirection() {
            var expectedOutput = new[] { "stdout", "stderr" };

            string script = TestData.GetPath(@"TestData\DebuggerProject\AttachOutput.py");
            var p = Process.Start(Version.InterpreterPath, "-B \"" + script + "\"");
            try {
                Thread.Sleep(1000);
                if (p.HasExited) {
                    Assert.Fail($"Failed to start process: {p.StartInfo.FileName} {p.StartInfo.Arguments}");
                }

                using (var dumpWriter = new MiniDumpWriter(p)) {
                    var proc = PythonProcess.Attach(p.Id, PythonDebugOptions.RedirectOutput, debugLog: DebugLog);
                    try {
                        var attached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        proc.ProcessLoaded += (sender, args) => {
                            Console.WriteLine("Process loaded");
                            attached.SetResult(true);
                        };
                        await proc.StartListeningAsync();

                        await attached.Task.WithTimeout(20000, "Failed to attach within 20s");
                        await proc.ResumeAsync(TimeoutToken());

                        var bpHit = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        PythonThread thread = null;
                        proc.BreakpointHit += (sender, args) => {
                            thread = args.Thread;
                            bpHit.SetResult(true);
                        };
                        var bp = proc.AddBreakpoint(script, 5);
                        await bp.AddAsync(TimeoutToken());

                        await bpHit.Task.WithTimeout(20000, "Failed to hit breakpoint within 20s");
                        Assert.IsNotNull(thread);

                        var actualOutput = new List<string>();
                        proc.DebuggerOutput += (sender, e) => {
                            Console.WriteLine("Debugger output: '{0}'", e.Output);
                            actualOutput.Add(e.Output);
                        };

                        var frame = thread.Frames[0];
                        Assert.AreEqual("False", (await frame.ExecuteTextAsync("attached", ct: CancellationTokens.After15s)).StringRepr);
                        await frame.ExecuteTextAsync("attached = True", ct: TimeoutToken());

                        await proc.ResumeAsync(TimeoutToken());
                        WaitForExit(proc);
                        AssertUtil.ArrayEquals(expectedOutput, actualOutput);
                    } finally {
                        await DetachProcessAsync(proc);
                    }

                    dumpWriter.Cancel();
                }
            } finally {
                DisposeProcess(p);
            }
        }

        protected virtual string PtvsdInterpreterArguments {
            get { return "-B"; }
        }

        private void CaptureOutput(StreamReader reader, StringBuilder builder) {
            try {
                while (true) {
                    int ch = reader.Read();
                    if (ch < 0) {
                        break;
                    }

                    lock (builder) {
                        builder.Append((char)ch);
                    }
                }
            } catch (Exception) {
            }
        }

        private async Task TestPtvsd(string pythonArgs, string script, string scriptArgs, Uri uri, List<string> output, Func<PythonProcess, Task> body, Func<Task> waitBeforeAttach = null) {
            var query = uri.Query;
            if (query.Length > 0) {
                // Remove leading "?"
                query = query.Substring(1);
            }
            query += "&opt=" + PythonDebugOptions.RedirectOutput;
            uri = new UriBuilder(uri) { Query = query }.Uri;

            string args = PtvsdInterpreterArguments + " " + pythonArgs + " \"" + script + "\"  " + scriptArgs;
            var psi = new ProcessStartInfo(Version.InterpreterPath, args) {
                WorkingDirectory = TestData.GetPath(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                Environment = {
                    { "PYTHONPATH", Path.GetDirectoryName(GetType().Assembly.Location) },
                }
            };

            Console.WriteLine("Python command line: " + psi.Arguments);
            Console.WriteLine("Attach URI: " + uri);

            var p = Process.Start(psi);

            var stdout = new StringBuilder();
            Task.Run(() => CaptureOutput(p.StandardOutput, stdout)).DoNotWait();

            var stderr = new StringBuilder();
            Task.Run(() => CaptureOutput(p.StandardError, stderr)).DoNotWait();

            if (waitBeforeAttach != null) {
                await waitBeforeAttach();
            }

            try {
                using (var dumpWriter = new MiniDumpWriter(p)) {
                    PythonProcess proc = null;
                    for (int i = 0; ; ++i) {
                        Thread.Sleep(1000);
                        try {
                            proc = await PythonRemoteProcess.AttachAsync(uri, false, DebugLog, TimeoutToken());
                            break;
                        } catch (SocketException) {
                            // Failed to connect - the process might have not started yet, so keep trying a few more times.
                            if (i >= 5 || p.HasExited) {
                                throw;
                            }
                        }
                    }

                    try {
                        var attached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                        if (output != null) {
                            proc.DebuggerOutput += (sender, e) => {
                                output.Add(e.Output);
                            };
                        }

                        await body(proc);

                        Assert.IsTrue(p.WaitForExit(DefaultWaitForExitTimeout), "Python process did not terminate.");
                    } finally {
                        await DetachProcessAsync(proc);
                    }

                    dumpWriter.Cancel();
                }
            } finally {
                Console.WriteLine("STDOUT:");
                lock (stdout) {
                    Console.WriteLine(stdout.ToString());
                }

                Console.WriteLine("STDERR:");
                lock (stderr) {
                    Console.WriteLine(stderr.ToString());
                }

                DisposeProcess(p);
            }
        }

        protected virtual bool HasPtvsdCommandLine => true;

        private async Task TestPtvsdImport(string enableAttachArgs, Uri uri) {
            if (!HasPtvsdCommandLine) {
                return;
            }

            string script = TestData.GetPath(@"TestData\DebuggerProject\AttachPtvsd.py");
            var expectedOutput = new[] { "stdout", "stderr" };
            var actualOutput = new List<string>();

            await TestPtvsd("", script, '"' + enableAttachArgs + '"', uri, actualOutput, async proc => {
                var attached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                proc.ProcessLoaded += async (sender, e) => {
                    Console.WriteLine("Process loaded");

                    var bp = proc.AddBreakpoint(script, 11);
                    await bp.AddAsync(TimeoutToken());

                    await proc.ResumeAsync(TimeoutToken());
                    attached.SetResult(true);
                };

                var bpHit = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                proc.BreakpointHit += async (sender, e) => {
                    Console.WriteLine("Breakpoint hit");
                    bpHit.SetResult(true);
                    await proc.ResumeAsync(TimeoutToken());
                };

                await proc.StartListeningAsync();
                await attached.Task.WithTimeout(20000, "Failed to attach within 20s");
                await bpHit.Task.WithTimeout(20000, "Failed to hit breakpoint within 20s");
            });

            AssertUtil.ArrayEquals(expectedOutput, actualOutput);
        }

        private async Task TestPtvsdCommandLine(string args, Uri uri) {
            if (!HasPtvsdCommandLine) {
                return;
            }

            string script = TestData.GetPath(@"TestData\DebuggerProject\AttachPtvsd.py");
            var expectedOutput = new[] { "stdout", "stderr" };
            var actualOutput = new List<string>();

            await TestPtvsd("-B -m ptvsd " + args, script, "", uri, actualOutput, async proc => {
                var attached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                proc.ProcessLoaded += async (sender, e) => {
                    Console.WriteLine("Process loaded");

                    var bp = proc.AddBreakpoint(script, 11);
                    await bp.AddAsync(TimeoutToken());

                    await proc.ResumeAsync(TimeoutToken());
                    attached.SetResult(true);
                };

                var bpHit = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                proc.BreakpointHit += async (sender, e) => {
                    Console.WriteLine("Breakpoint hit");
                    bpHit.SetResult(true);
                    await proc.ResumeAsync(TimeoutToken());
                };

                await proc.StartListeningAsync();
                await attached.Task.WithTimeout(20000, "Failed to attach within 20s");
                await bpHit.Task.WithTimeout(20000, "Failed to hit breakpoint within 20s");
            });

            AssertUtil.ArrayEquals(expectedOutput, actualOutput);
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public async Task AttachPtvsdImport() {
            await TestPtvsdImport("secret=None", new Uri("tcp://localhost"));
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public async Task AttachPtvsdCommandLine() {
            await TestPtvsdCommandLine("--wait", new Uri("tcp://localhost"));
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public async Task AttachPtvsdImportSecret() {
            await TestPtvsdImport("secret='secret'", new Uri("tcp://secret@localhost"));
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public async Task AttachPtvsdCommandLineSecret() {
            await TestPtvsdCommandLine("--wait --secret secret", new Uri("tcp://secret@localhost"));
        }

        private static IPAddress GetNetworkInterface() {
            var ip = (from netif in NetworkInterface.GetAllNetworkInterfaces()
                      where netif.OperationalStatus == OperationalStatus.Up && netif.Supports(NetworkInterfaceComponent.IPv4)
                      from uaddr in netif.GetIPProperties().UnicastAddresses
                      where uaddr.Address.AddressFamily == AddressFamily.InterNetwork
                      select uaddr.Address
                     ).FirstOrDefault();

            if (ip == null) {
                Assert.Inconclusive("Couldn't find an IPv4 network interface to test with");
            }

            Console.WriteLine("Using interface " + ip);
            return ip;
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public async Task AttachPtvsdImportAddress() {
            var ip = GetNetworkInterface();
            await TestPtvsdImport("secret=None, address=('" + ip + "', 8765)", new Uri("tcp://" + ip + ":8765"));
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public async Task AttachPtvsdCommandLinePort() {
            await TestPtvsdCommandLine("--wait --port 8765", new Uri("tcp://localhost:8765"));
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public async Task AttachPtvsdCommandLineInterface() {
            var ip = GetNetworkInterface();
            await TestPtvsdCommandLine("--wait --interface " + ip, new Uri("tcp://" + ip));
        }

        [TestMethod, Priority(2)]
        public async Task AttachPtvsdCommandLineWait() {
            if (!HasPtvsdCommandLine) {
                return;
            }

            string script = TestData.GetPath(@"TestData\DebuggerProject\AttachPtvsdLoop.py");

            await TestPtvsd("-m ptvsd --wait", script, "", new Uri("tcp://localhost"), null, async proc => {
                var attached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                proc.ProcessLoaded += async (sender, e) => {
                    Console.WriteLine("Process loaded");
                    await proc.ResumeAsync(TimeoutToken());
                    attached.SetResult(true);
                };

                var threadTcs = new TaskCompletionSource<PythonThread>(TaskCreationOptions.RunContinuationsAsynchronously);
                proc.AsyncBreakComplete += async (sender, e) => {
                    Console.WriteLine("break_into_debugger hit");
                    threadTcs.TrySetResult(e.Thread);
                };

                await proc.StartListeningAsync();
                await attached.Task.WithTimeout(20000, "Failed to attach within 20s");

                var thread = await threadTcs.Task.WithTimeout(20000, "Failed to break into debugger within 20s");
                var frame = thread.Frames[0];

                var result = await frame.ExecuteTextAsync("i", PythonEvaluationResultReprKind.Normal, TimeoutToken());
                Assert.AreEqual(int.Parse(result.StringRepr), 0);

                await frame.ExecuteTextAsync("i = None", PythonEvaluationResultReprKind.Normal, TimeoutToken());
                await proc.ResumeAsync(TimeoutToken());
            });
        }

        [TestMethod, Priority(TestExtensions.P3_FAILING_UNIT_TEST)]
        public async Task AttachPtvsdCommandLineNoWait() {
            if (!HasPtvsdCommandLine) {
                return;
            }

            string script = TestData.GetPath("TestData", "DebuggerProject", "AttachPtvsdLoop.py");
            string runFile = Path.Combine(TestData.GetTempPath(), "AttachPtvsdLoop.running");

            await TestPtvsd("-m ptvsd", script, "- \"" + runFile + "\"", new Uri("tcp://localhost"), null, async proc => {
                proc.ProcessLoaded += async (sender, e) => {
                    Console.WriteLine("Process loaded");
                    await proc.ResumeAsync(TimeoutToken());
                };

                var threadTcs = new TaskCompletionSource<PythonThread>(TaskCreationOptions.RunContinuationsAsynchronously);
                proc.AsyncBreakComplete += async (sender, e) => {
                    Console.WriteLine("break_into_debugger hit");
                    threadTcs.TrySetResult(e.Thread);
                };

                await proc.StartListeningAsync();

                var thread = await threadTcs.Task.WithTimeout(20000, "Failed to break into debugger within 20s");
                var frame = thread.Frames[0];

                var result = await frame.ExecuteTextAsync("i", PythonEvaluationResultReprKind.Normal, TimeoutToken());
                Assert.AreNotEqual(int.Parse(result.StringRepr), 0);

                await frame.ExecuteTextAsync("i = None", PythonEvaluationResultReprKind.Normal, TimeoutToken());
                await proc.ResumeAsync(TimeoutToken());
            }, waitBeforeAttach: async () => {
                for (int i = 0;; ++i) {
                    if (File.Exists(runFile)) {
                        File.Delete(runFile);
                        break;
                    }

                    if (i > 200) {
                        Assert.Fail("Python code didn't start running within 20s");
                    }

                    await Task.Delay(100);
                }

                // Now that it's running, give the loop some time to spin and increment the counter.
                await Task.Delay(500);
            });
        }

        // https://github.com/Microsoft/PTVS/issues/2842
        [TestMethod, Priority(TestExtensions.P2_FAILING_UNIT_TEST)]
        public virtual async Task AttachPtvsdAndStopDebugging() {
            if (!HasPtvsdCommandLine) {
                return;
            }

            string script = TestData.GetPath(@"TestData\TestAdapterTestB\InheritanceBaseTest.py");

            await TestPtvsd("-m ptvsd --wait", script, "", new Uri("tcp://localhost"), null, async proc => {
                var attached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                proc.ProcessLoaded += async (sender, e) => {
                    Console.WriteLine("Process loaded");

                    var bp = proc.AddBreakpoint(script, 8);
                    await bp.AddAsync(TimeoutToken());

                    await proc.ResumeAsync(TimeoutToken());
                    attached.SetResult(true);
                };

                var bpHit = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                proc.BreakpointHit += async (sender, args) => {
                    Console.WriteLine("Breakpoint hit");
                    bpHit.SetResult(true);
                    await proc.ResumeAsync(TimeoutToken());
                };

                await proc.StartListeningAsync();
                await attached.Task.WithTimeout(20000, "Failed to attach within 20s");
                await bpHit.Task.WithTimeout(20000, "Failed to hit breakpoint within 20s");

                await proc.DetachAsync(TimeoutToken());
            });
        }

        class TraceRedirector : Redirector {
            private readonly string _prefix;

            public TraceRedirector(string prefix = "") {
                if (string.IsNullOrEmpty(prefix)) {
                    _prefix = "";
                } else {
                    _prefix = prefix + ": ";
                }
            }

            public override void WriteLine(string line) {
                Trace.WriteLine(_prefix + line);
            }

            public override void WriteErrorLine(string line) {
                Trace.WriteLine(_prefix + "[ERROR] " + line);
            }
        }

        private string CompileCode(string hostCode) {
            var buildDir = TestData.GetTempPath();

            var pythonHome = "\"" + Version.PrefixPath.Replace("\\", "\\\\") + "\"";
            if (Version.Version >= PythonLanguageVersion.V30) {
                pythonHome = "L" + pythonHome;
            }
            hostCode = hostCode.Replace("$PYTHON_HOME", pythonHome);

            File.WriteAllText(Path.Combine(buildDir, "test.cpp"), hostCode);

            VCCompiler vc;

            if (Version.Version <= PythonLanguageVersion.V32) {
                vc = Version.Isx64
                    ? (VCCompiler.VC12_X64 ?? VCCompiler.VC11_X64 ?? VCCompiler.VC10_X64)
                    : (VCCompiler.VC12_X86 ?? VCCompiler.VC11_X86 ?? VCCompiler.VC10_X86);
            } else if (Version.Version <= PythonLanguageVersion.V34) {
                vc = Version.Isx64
                    ? (VCCompiler.VC10_X64 ?? VCCompiler.VC12_X64 ?? VCCompiler.VC11_X64)
                    : (VCCompiler.VC10_X86 ?? VCCompiler.VC12_X86 ?? VCCompiler.VC11_X86);
            } else {
                vc = Version.Isx64
                    ? (VCCompiler.VC15_X64 ?? VCCompiler.VC14_X64)
                    : (VCCompiler.VC15_X86 ?? VCCompiler.VC14_X86);
            }

            if (vc == null) {
                Assert.Inconclusive("VC not installed for " + Version.Version);
            }

            // compile our host code...
            var env = new Dictionary<string, string>();
            env["PATH"] = vc.BinPaths + ";" + Environment.GetEnvironmentVariable("PATH");
            env["INCLUDE"] = vc.IncludePaths + ";" + Path.Combine(Version.PrefixPath, "Include");
            env["LIB"] = vc.LibPaths + ";" + Path.Combine(Version.PrefixPath, "libs");

            foreach (var kv in env) {
                Trace.TraceInformation("SET {0}={1}", kv.Key, kv.Value);
            }

            using (var p = ProcessOutput.Run(
                Path.Combine(vc.BinPath, "cl.exe"),
                new[] { "/nologo", "/Zi", "/MD", "test.cpp" },
                buildDir,
                env,
                false,
                new TraceRedirector("cl.exe")
            )) {
                Trace.TraceInformation(p.Arguments);
                if (!p.Wait(TimeSpan.FromMilliseconds(DefaultWaitForExitTimeout))) {
                    p.Kill();
                    Assert.Fail("Timeout while waiting for compiler");
                }
                Assert.AreEqual(0, p.ExitCode ?? -1, "Incorrect exit code from compiler");
            }

            return Path.Combine(buildDir, "test.exe");
        }

        private Process RunHost(string hostExe) {
            var psi = new ProcessStartInfo(hostExe) { UseShellExecute = false };
            // Add Python to PATH so that the host can locate the DLL in case it's not in \Windows\System32 (e.g. for EPD)
            psi.EnvironmentVariables["PATH"] = Environment.GetEnvironmentVariable("PATH") + ";" + Version.PrefixPath;
            if (psi.EnvironmentVariables.ContainsKey("PYTHONPATH")) {
                psi.EnvironmentVariables.Remove("PYTHONPATH");
            }
            return Process.Start(psi);
        }

        #endregion

    }

    [TestClass]
    public class AttachTests35 : AttachTests {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python35;
            }
        }

        public override async Task AttachNewThread_PyThreadState_New() {
            // PyEval_AcquireLock deprecated in 3.2
        }
    }

    [TestClass]
    public class AttachTests35_x64 : AttachTests {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python35_x64;
            }
        }

        public override async Task AttachNewThread_PyThreadState_New()
        {
            // PyEval_AcquireLock deprecated in 3.2
        }
    }

    [TestClass]
    public class AttachTests36 : AttachTests {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python36;
            }
        }

        public override async Task AttachNewThread_PyThreadState_New() {
            // PyEval_AcquireLock deprecated in 3.2
        }
    }

    [TestClass]
    public class AttachTests36_x64 : AttachTests36 {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python36_x64;
            }
        }
    }

    [TestClass]
    public class AttachTests37 : AttachTests {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python37;
            }
        }

        public override async Task AttachNewThread_PyThreadState_New() {
            // PyEval_AcquireLock deprecated in 3.2
        }
    }

    [TestClass]
    public class AttachTests37_x64 : AttachTests37 {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python37_x64;
            }
        }
    }

    [TestClass]
    public class AttachTests27 : AttachTests {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python27;
            }
        }
    }

    [TestClass]
    public class AttachTests27_x64 : AttachTests {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python27_x64;
            }
        }
    }

    [TestClass]
    public class AttachTestsIpy27 : AttachTests {
        internal override PythonVersion Version {
            get {
                return PythonPaths.IronPython27 ?? PythonPaths.IronPython27_x64;
            }
        }

        // IronPython does not support normal attach.
        public override async Task AttachMultithreadedSleeper() { }
        public override async Task AttachNewThread_PyGILState_Ensure() { }
        public override async Task AttachNewThread_PyThreadState_New() { }
        public override async Task AttachReattach() { }
        public override async Task AttachReattachInfiniteThreads() { }
        public override async Task AttachReattachThreadingInited() { }
        public override async Task AttachSingleThreadedSleeper() { }
        public override async Task AttachThreadingStartNewThread() { }
        public override async Task AttachTimeoutThreadsInitialized() { }
        public override async Task AttachTimeout() { }
        public override async Task AttachWithOutputRedirection() { }
        public override async Task AttachAndStepWithBlankSysPrefix() { }

        [TestMethod, Priority(2)]
        public override async Task AttachPtvsdAndStopDebugging() => await base.AttachPtvsdAndStopDebugging();

        protected override string PtvsdInterpreterArguments {
            get { return "-X:Tracing -X:Frames"; }
        }
    }

}

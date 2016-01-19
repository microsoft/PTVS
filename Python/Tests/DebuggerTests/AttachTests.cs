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
using System.Net.Sockets;
using System.Threading;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.Debugger.Remote;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools.Project;
using TestUtilities;

namespace DebuggerTests {
    [TestClass, Ignore]
    public abstract class AttachTests : BaseDebuggerTests {
        [TestInitialize]
        public void CheckVersion() {
            if (Version == null) {
                Assert.Inconclusive("Required version of Python is not installed");
            }
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
        public virtual void AttachThreadingStartNewThread() {
            // http://pytools.codeplex.com/workitem/638
            // http://pytools.codeplex.com/discussions/285741#post724014
            var psi = new ProcessStartInfo(Version.InterpreterPath, "\"" + TestData.GetPath(@"TestData\DebuggerProject\ThreadingStartNewThread.py") + "\"");
            psi.WorkingDirectory = TestData.GetPath(@"TestData\DebuggerProject");
            psi.EnvironmentVariables["PYTHONPATH"] = @"..\..";
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            Process p = Process.Start(psi);
            try {
                if (p.WaitForExit(1000)) {
                    Assert.Fail("Process exited");
                }

                var proc = PythonProcess.Attach(p.Id);
                try {
                    using (var attached = new AutoResetEvent(false))
                    using (var readyToContinue = new AutoResetEvent(false))
                    using (var threadBreakpointHit = new AutoResetEvent(false)) {
                        proc.ProcessLoaded += (sender, args) => {
                            attached.Set();
                            var bp = proc.AddBreakPoint("ThreadingStartNewThread.py", 9);
                            bp.Add();

                            bp = proc.AddBreakPoint("ThreadingStartNewThread.py", 5);
                            bp.Add();

                            proc.Resume();
                        };
                        PythonThread mainThread = null;
                        PythonThread bpThread = null;
                        bool wrongLine = false;
                        proc.BreakpointHit += (sender, args) => {
                            if (args.Breakpoint.LineNo == 9) {
                                // stop running the infinite loop
                                Debug.WriteLine(String.Format("First BP hit {0}", args.Thread.Id));
                                mainThread = args.Thread;
                                args.Thread.Frames[0].ExecuteText("x = False", (x) => { readyToContinue.Set(); });
                            } else if (args.Breakpoint.LineNo == 5) {
                                // we hit the breakpoint on the new thread
                                Debug.WriteLine(String.Format("Second BP hit {0}", args.Thread.Id));
                                bpThread = args.Thread;
                                threadBreakpointHit.Set();
                                proc.Continue();
                            } else {
                                Debug.WriteLine(String.Format("Hit breakpoint on wrong line number: {0}", args.Breakpoint.LineNo));
                                wrongLine = true;
                                attached.Set();
                                threadBreakpointHit.Set();
                                proc.Continue();
                            }
                        };

                        proc.StartListening();
                        Assert.IsTrue(attached.WaitOne(30000), "Failed to attach within 30s");

                        Assert.IsTrue(readyToContinue.WaitOne(30000), "Failed to hit the main thread breakpoint within 30s");
                        proc.Continue();

                        Assert.IsTrue(threadBreakpointHit.WaitOne(30000), "Failed to hit the background thread breakpoint within 30s");
                        Assert.IsFalse(wrongLine, "Breakpoint broke on the wrong line");

                        Assert.AreNotEqual(mainThread, bpThread);
                    }
                } finally {
                    DetachProcess(proc);
                }
            } finally {
                DisposeProcess(p);
            }
        }

        [TestMethod, Priority(2)]
        [TestCategory("10s"), TestCategory("60s")]
        public virtual void AttachReattach() {
            Process p = Process.Start(Version.InterpreterPath, "\"" + TestData.GetPath(@"TestData\DebuggerProject\InfiniteRun.py") + "\"");
            try {
                Thread.Sleep(1000);

                AutoResetEvent attached = new AutoResetEvent(false);
                AutoResetEvent detached = new AutoResetEvent(false);
                for (int i = 0; i < 10; i++) {
                    Console.WriteLine(i);

                    var proc = PythonProcess.Attach(p.Id);

                    proc.ProcessLoaded += (sender, args) => {
                        attached.Set();
                    };
                    proc.ProcessExited += (sender, args) => {
                        detached.Set();
                    };
                    proc.StartListening();

                    Assert.IsTrue(attached.WaitOne(10000), "Failed to attach within 10s");
                    proc.Detach();
                    Assert.IsTrue(detached.WaitOne(10000), "Failed to detach within 10s");
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
        public virtual void AttachMultithreadedSleeper() {
            // http://pytools.codeplex.com/discussions/285741 1/12/2012 6:20 PM
            Process p = Process.Start(Version.InterpreterPath, "\"" + TestData.GetPath(@"TestData\DebuggerProject\AttachMultithreadedSleeper.py") + "\"");
            try {
                Thread.Sleep(1000);

                AutoResetEvent attached = new AutoResetEvent(false);

                var proc = PythonProcess.Attach(p.Id);

                try {
                    proc.ProcessLoaded += (sender, args) => {
                        attached.Set();
                    };
                    proc.StartListening();

                    Assert.IsTrue(attached.WaitOne(10000), "Failed to attach within 10s");
                    proc.Resume();
                    Debug.WriteLine("Waiting for exit");
                } finally {
                    WaitForExit(proc);
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
        public virtual void AttachSingleThreadedSleeper() {
            // http://pytools.codeplex.com/discussions/285741 1/12/2012 6:20 PM
            Process p = Process.Start(Version.InterpreterPath, "\"" + TestData.GetPath(@"TestData\DebuggerProject\AttachSingleThreadedSleeper.py") + "\"");
            try {
                Thread.Sleep(1000);

                AutoResetEvent attached = new AutoResetEvent(false);

                var proc = PythonProcess.Attach(p.Id);
                try {
                    proc.ProcessLoaded += (sender, args) => {
                        attached.Set();
                    };
                    proc.StartListening();

                    Assert.IsTrue(attached.WaitOne(10000), "Failed to attach within 10s");
                    proc.Resume();
                    Debug.WriteLine("Waiting for exit");
                } finally {
                    TerminateProcess(proc);
                }
            } finally {
                DisposeProcess(p);
            }
        }

        /*
        [TestMethod, Priority(1)]
        public void AttachReattach64() {
            Process p = Process.Start("C:\\Python27_x64\\python.exe", "\"" + TestData.GetPath(@"TestData\DebuggerProject\InfiniteRun.py") + "\"");
            try {
                Thread.Sleep(1000);

                for (int i = 0; i < 10; i++) {
                    Console.WriteLine(i);

                    PythonProcess proc;
                    ConnErrorMessages errReason;
                    if ((errReason = PythonProcess.TryAttach(p.Id, out proc)) != ConnErrorMessages.None) {
                        Assert.Fail("Failed to attach {0}", errReason);
                    }

                    proc.Detach();
                }
            } finally {
                DisposeProcess(p);
            }
        }*/

        [TestMethod, Priority(2)]
        [TestCategory("10s"), TestCategory("60s")]
        public virtual void AttachReattachThreadingInited() {
            Process p = Process.Start(Version.InterpreterPath, "\"" + TestData.GetPath(@"TestData\DebuggerProject\InfiniteRunThreadingInited.py") + "\"");
            try {
                Thread.Sleep(1000);

                AutoResetEvent attached = new AutoResetEvent(false);
                AutoResetEvent detached = new AutoResetEvent(false);
                for (int i = 0; i < 10; i++) {
                    Console.WriteLine(i);

                    var proc = PythonProcess.Attach(p.Id);

                    proc.ProcessLoaded += (sender, args) => {
                        attached.Set();
                    };
                    proc.ProcessExited += (sender, args) => {
                        detached.Set();
                    };
                    proc.StartListening();

                    Assert.IsTrue(attached.WaitOne(10000), "Failed to attach within 10s");
                    proc.Detach();
                    Assert.IsTrue(detached.WaitOne(10000), "Failed to detach within 10s");
                }
            } finally {
                DisposeProcess(p);
            }
        }

        [TestMethod, Priority(3)]
        [TestCategory("10s")]
        public virtual void AttachReattachInfiniteThreads() {
            Process p = Process.Start(Version.InterpreterPath, "\"" + TestData.GetPath(@"TestData\DebuggerProject\InfiniteThreads.py") + "\"");
            try {
                Thread.Sleep(1000);

                AutoResetEvent attached = new AutoResetEvent(false);
                AutoResetEvent detached = new AutoResetEvent(false);
                for (int i = 0; i < 10; i++) {
                    Console.WriteLine(i);

                    var proc = PythonProcess.Attach(p.Id);

                    proc.ProcessLoaded += (sender, args) => {
                        attached.Set();
                    };
                    proc.ProcessExited += (sender, args) => {
                        detached.Set();
                    };
                    proc.StartListening();

                    Assert.IsTrue(attached.WaitOne(30000), "Failed to attach within 30s");
                    proc.Detach();
                    Assert.IsTrue(detached.WaitOne(30000), "Failed to detach within 30s");

                }
            } finally {
                DisposeProcess(p);
            }
        }

        [TestMethod, Priority(2)]
        [TestCategory("10s")]
        public virtual void AttachTimeout() {
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
            AttachTest(hostCode);
        }

        /// <summary>
        /// Attempts to attach w/ code only running on new threads which are initialized using PyGILState_Ensure
        /// </summary>
        [TestMethod, Priority(2)]
        [TestCategory("10s")]
        public virtual void AttachNewThread_PyGILState_Ensure() {
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
        if (pValue != NULL) {
            //printf(""Result of call: %ld\n"", PyInt_AsLong(pValue));
            Py_DECREF(pValue);
        }
        else {
            PyErr_Print();
            return;
        }
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
    import sys
    print('\n'.join(sys.path))
    for i in range(10):
        print(i)

    return 0");


            // start the test process w/ our handle
            Process p = RunHost(exe);
            try {
                Thread.Sleep(1500);

                AutoResetEvent attached = new AutoResetEvent(false);
                AutoResetEvent bpHit = new AutoResetEvent(false);

                var proc = PythonProcess.Attach(p.Id);
                try {
                    proc.ProcessLoaded += (sender, args) => {
                        Console.WriteLine("Process loaded");
                        attached.Set();
                    };
                    proc.StartListening();

                    Assert.IsTrue(attached.WaitOne(20000), "Failed to attach within 20s");

                    proc.BreakpointHit += (sender, args) => {
                        Console.WriteLine("Breakpoint hit");
                        bpHit.Set();
                    };

                    var bp = proc.AddBreakPoint("gilstate_attach.py", 3);
                    bp.Add();

                    Assert.IsTrue(bpHit.WaitOne(20000), "Failed to hit breakpoint within 20s");
                } finally {
                    DetachProcess(proc);
                }
            } finally {
                DisposeProcess(p);
            }
        }

        /// <summary>
        /// Attempts to attach w/ code only running on new threads which are initialized using PyThreadState_New
        /// </summary>
        [TestMethod, Priority(2)]
        [TestCategory("10s")]
        public virtual void AttachNewThread_PyThreadState_New() {
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
                Thread.Sleep(1500);

                AutoResetEvent attached = new AutoResetEvent(false);
                AutoResetEvent bpHit = new AutoResetEvent(false);

                var proc = PythonProcess.Attach(p.Id);
                try {
                    proc.ProcessLoaded += (sender, args) => {
                        Console.WriteLine("Process loaded");
                        attached.Set();
                    };
                    proc.StartListening();

                    Assert.IsTrue(attached.WaitOne(20000), "Failed to attach within 20s");

                    proc.BreakpointHit += (sender, args) => {
                        Console.WriteLine("Breakpoint hit");
                        bpHit.Set();
                    };

                    var bp = proc.AddBreakPoint("gilstate_attach.py", 3);
                    bp.Add();

                    Assert.IsTrue(bpHit.WaitOne(20000), "Failed to hit breakpoint within 20s");
                } finally {
                    DetachProcess(proc);
                }
            } finally {
                DisposeProcess(p);
            }
        }

        [TestMethod, Priority(2)]
        [TestCategory("10s")]
        public virtual void AttachTimeoutThreadsInitialized() {
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
            AttachTest(hostCode);
        }

        private void AttachTest(string hostCode) {
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
                // start the attach with the GIL held
                AutoResetEvent attached = new AutoResetEvent(false);

                var proc = PythonProcess.Attach(p.Id);
                try {
                    bool isAttached = false;
                    proc.ProcessLoaded += (sender, args) => {
                        attached.Set();
                        isAttached = false;
                    };
                    proc.StartListening();

                    Assert.IsFalse(isAttached, "should not have attached yet"); // we should be blocked
                    handle.Set();   // let the code start running

                    Assert.IsTrue(attached.WaitOne(20000), "Failed to attach within 20s");
                } finally {
                    DetachProcess(proc);
                }
            } finally {
                Debug.WriteLine(String.Format("Process output: {0}", outRecv.Output.ToString()));
                DisposeProcess(p);
            }
        }

        [TestMethod, Priority(3)]
        [TestCategory("10s")]
        public virtual void AttachAndStepWithBlankSysPrefix() {
            string script = TestData.GetPath(@"TestData\DebuggerProject\InfiniteRunBlankPrefix.py");
            var p = Process.Start(Version.InterpreterPath, "\"" + script + "\"");
            try {
                Thread.Sleep(1000);
                var proc = PythonProcess.Attach(p.Id);
                try {
                    var attached = new AutoResetEvent(false);
                    proc.ProcessLoaded += (sender, args) => {
                        Console.WriteLine("Process loaded");
                        proc.Resume();
                        attached.Set();
                    };
                    proc.StartListening();
                    Assert.IsTrue(attached.WaitOne(20000), "Failed to attach within 20s");

                    var bpHit = new AutoResetEvent(false);
                    PythonThread thread = null;
                    PythonStackFrame oldFrame = null;
                    proc.BreakpointHit += (sender, args) => {
                        Console.WriteLine("Breakpoint hit");
                        thread = args.Thread;
                        oldFrame = args.Thread.Frames[0];
                        bpHit.Set();
                    };
                    var bp = proc.AddBreakPoint(script, 6);
                    bp.Add();
                    Assert.IsTrue(bpHit.WaitOne(20000), "Failed to hit breakpoint within 20s");

                    var stepComplete = new AutoResetEvent(false);
                    PythonStackFrame newFrame = null;
                    proc.StepComplete += (sender, args) => {
                        newFrame = args.Thread.Frames[0];
                        stepComplete.Set();
                    };
                    thread.StepOver();
                    Assert.IsTrue(stepComplete.WaitOne(20000), "Failed to complete the step within 20s");

                    Assert.AreEqual(oldFrame.FileName, newFrame.FileName);
                    Assert.IsTrue(oldFrame.LineNo + 1 == newFrame.LineNo);
                } finally {
                    DetachProcess(proc);
                }
            } finally {
                DisposeProcess(p);
            }
        }

        [TestMethod, Priority(2)]
        [TestCategory("10s")]
        public virtual void AttachWithOutputRedirection() {
            var expectedOutput = new[] { "stdout", "stderr" };

            string script = TestData.GetPath(@"TestData\DebuggerProject\AttachOutput.py");
            var p = Process.Start(Version.InterpreterPath, "\"" + script + "\"");
            try {
                Thread.Sleep(1000);
                var proc = PythonProcess.Attach(p.Id, PythonDebugOptions.RedirectOutput);
                try {
                    var attached = new AutoResetEvent(false);
                    proc.ProcessLoaded += (sender, args) => {
                        Console.WriteLine("Process loaded");
                        attached.Set();
                    };
                    proc.StartListening();

                    Assert.IsTrue(attached.WaitOne(20000), "Failed to attach within 20s");
                    proc.Resume();

                    var bpHit = new AutoResetEvent(false);
                    PythonThread thread = null;
                    proc.BreakpointHit += (sender, args) => {
                        thread = args.Thread;
                        bpHit.Set();
                    };
                    var bp = proc.AddBreakPoint(script, 5);
                    bp.Add();

                    Assert.IsTrue(bpHit.WaitOne(20000), "Failed to hit breakpoint within 20s");
                    Assert.IsNotNull(thread);

                    var actualOutput = new List<string>();
                    proc.DebuggerOutput += (sender, e) => {
                        Console.WriteLine("Debugger output: '{0}'", e.Output);
                        actualOutput.Add(e.Output);
                    };

                    var frame = thread.Frames[0];
                    Assert.AreEqual("False", frame.ExecuteTextAsync("attached").Result.StringRepr);
                    Assert.IsTrue(frame.ExecuteTextAsync("attached = True").Wait(20000), "Failed to complete evaluation within 20s");

                    proc.Resume();
                    WaitForExit(proc);
                    AssertUtil.ArrayEquals(expectedOutput, actualOutput);
                } finally {
                    DetachProcess(proc);
                }
            } finally {
                DisposeProcess(p);
            }
        }

        protected virtual string PtvsdInterpreterArguments {
            get { return ""; }
        }

        [TestMethod, Priority(1)]
        public void AttachPtvsd() {
            var expectedOutput = new[] { "stdout", "stderr" };

            string script = TestData.GetPath(@"TestData\DebuggerProject\AttachPtvsd.py");
            var psi = new ProcessStartInfo(Version.InterpreterPath, PtvsdInterpreterArguments + " \"" + script + "\"") {
                WorkingDirectory = TestData.GetPath(),
                UseShellExecute = false
            };

            var p = Process.Start(psi);
            try {
                PythonProcess proc = null;
                for (int i = 0; ; ++i) {
                    Thread.Sleep(1000);
                    try {
                        proc = PythonRemoteProcess.Attach(
                            new Uri("tcp://secret@localhost?opt=" + PythonDebugOptions.RedirectOutput),
                            warnAboutAuthenticationErrors: false);
                        break;
                    } catch (SocketException) {
                        // Failed to connect - the process might have not started yet, so keep trying a few more times.
                        if (i >= 5) {
                            throw;
                        }
                    }
                }

                try {
                    var attached = new AutoResetEvent(false);
                    proc.ProcessLoaded += (sender, e) => {
                        Console.WriteLine("Process loaded");

                        var bp = proc.AddBreakPoint(script, 10);
                        bp.Add();

                        proc.Resume();
                        attached.Set();
                    };

                    var actualOutput = new List<string>();
                    proc.DebuggerOutput += (sender, e) => {
                        actualOutput.Add(e.Output);
                    };

                    var bpHit = new AutoResetEvent(false);
                    proc.BreakpointHit += (sender, args) => {
                        Console.WriteLine("Breakpoint hit");
                        bpHit.Set();
                        proc.Continue();
                    };

                    proc.StartListening();
                    Assert.IsTrue(attached.WaitOne(20000), "Failed to attach within 20s");
                    Assert.IsTrue(bpHit.WaitOne(20000), "Failed to hit breakpoint within 20s");

                    p.WaitForExit(DefaultWaitForExitTimeout);
                    AssertUtil.ArrayEquals(expectedOutput, actualOutput);
                } finally {
                    DetachProcess(proc);
                }
            } finally {
                DisposeProcess(p);
            }
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
            var buildDir = TestData.GetTempPath(randomSubPath: true);

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
                vc = Version.Isx64 ? VCCompiler.VC14_X64 : VCCompiler.VC14_X86;
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
                new[] { "/MD", "test.cpp" },
                buildDir,
                env,
                false,
                new TraceRedirector()
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
    public class AttachTests30 : AttachTests {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python30 ?? PythonPaths.Python30_x64;
            }
        }
    }

    [TestClass]
    public class AttachTests31 : AttachTests {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python31 ?? PythonPaths.Python31_x64;
            }
        }
    }

    [TestClass]
    public class AttachTests32 : AttachTests {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python32 ?? PythonPaths.Python32_x64;
            }
        }

        public override void AttachNewThread_PyThreadState_New() {
            // PyEval_AcquireLock deprecated in 3.2
        }
    }

    [TestClass]
    public class AttachTests33 : AttachTests {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python33 ?? PythonPaths.Python33_x64;
            }
        }

        public override void AttachNewThread_PyThreadState_New() {
            // PyEval_AcquireLock deprecated in 3.2
        }
    }

    [TestClass]
    public class AttachTests34 : AttachTests {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python34;
            }
        }

        public override void AttachNewThread_PyThreadState_New() {
            // PyEval_AcquireLock deprecated in 3.2
        }
    }

    [TestClass]
    public class AttachTests34_x64 : AttachTests34 {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python34_x64;
            }
        }
    }

    [TestClass]
    public class AttachTests35 : AttachTests {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python35;
            }
        }

        public override void AttachNewThread_PyThreadState_New() {
            // PyEval_AcquireLock deprecated in 3.2
        }
    }

    [TestClass]
    public class AttachTests35_x64 : AttachTests35 {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python35_x64;
            }
        }
    }

    [TestClass]
    public class AttachTests25 : AttachTests {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python25 ?? PythonPaths.Python25_x64;
            }
        }
    }

    [TestClass]
    public class AttachTests26 : AttachTests {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python26 ?? PythonPaths.Python26_x64;
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
    public class AttachTestsIpy : AttachTests {
        internal override PythonVersion Version {
            get {
                return PythonPaths.IronPython27 ?? PythonPaths.IronPython27_x64;
            }
        }

        // IronPython does not support normal attach.
        public override void AttachMultithreadedSleeper() { }
        public override void AttachNewThread_PyGILState_Ensure() { }
        public override void AttachNewThread_PyThreadState_New() { }
        public override void AttachReattach() { }
        public override void AttachReattachInfiniteThreads() { }
        public override void AttachReattachThreadingInited() { }
        public override void AttachSingleThreadedSleeper() { }
        public override void AttachThreadingStartNewThread() { }
        public override void AttachTimeoutThreadsInitialized() { }
        public override void AttachTimeout() { }
        public override void AttachWithOutputRedirection() { }
        public override void AttachAndStepWithBlankSysPrefix() { }

        protected override string PtvsdInterpreterArguments {
            get { return "-X:Tracing -X:Frames"; }
        }
    }

}

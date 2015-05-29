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
using System.ComponentModel;
using System.Linq;
using System.Threading;
using DebuggerTests;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.Repl;
#if DEV14_OR_LATER
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.InteractiveWindow.Commands;
#else
using Microsoft.VisualStudio.Repl;
#endif
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;
using TestUtilities;
using TestUtilities.Mocks;
using TestUtilities.Python;

namespace PythonToolsTests {
#if DEV14_OR_LATER
    using IReplEvaluator = IInteractiveEvaluator;
    using IReplWindow = IInteractiveWindow;
    using IReplWindowProvider = InteractiveWindowProvider;
    using IReplCommand = Microsoft.VisualStudio.InteractiveWindow.Commands.IInteractiveWindowCommand;
#endif

    [TestClass]
    public class DebugReplEvaluatorTests {
        private PythonDebugReplEvaluator _evaluator;
        private MockReplWindow _window;
        private List<PythonProcess> _processes;

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        internal virtual string DebuggerTestPath {
            get {
                return TestData.GetPath(@"TestData\DebuggerProject\");
            }
        }

        internal virtual PythonVersion Version {
            get {
                return PythonPaths.Python26 ?? PythonPaths.Python26_x64;
            }
        }

        [TestInitialize]
        public void TestInit() {
            Version.AssertInstalled();
            var serviceProvider = PythonToolsTestUtilities.CreateMockServiceProvider();
            _evaluator = new PythonDebugReplEvaluator(serviceProvider);
            _window = new MockReplWindow(_evaluator);
            _evaluator._Initialize(_window);
            _processes = new List<PythonProcess>();
        }

        [TestCleanup]
        public void TestClean() {
            foreach (var proc in _processes) {
                try {
                    proc.Continue();
                } catch (Exception ex) {
                    Console.WriteLine("Failed to continue process");
                    Console.WriteLine(ex);
                }
                if (!proc.WaitForExit(5000)) {
                    try {
                        proc.Terminate();
                    } catch (Exception ex) {
                        Console.WriteLine("Failed to terminate process");
                        Console.WriteLine(ex);
                    }
                }
            }
            if (_window != null) {
                Console.WriteLine("Stdout:");
                Console.Write(_window.Output);
                Console.WriteLine("Stderr:");
                Console.Write(_window.Error);
            }
        }

        [TestMethod, Priority(0)]
        public void DisplayVariables() {
            Attach("DebugReplTest1.py", 3);

            Assert.AreEqual("hello", ExecuteText("print(a)"));
            Assert.AreEqual("'hello'", ExecuteText("a"));
        }

        [TestMethod, Priority(0)]
        public void DisplayFunctionLocalsAndGlobals() {
            Attach("DebugReplTest2.py", 13);

            Assert.AreEqual("51", ExecuteText("print(innermost_val)"));
            Assert.AreEqual("5", ExecuteText("print(global_val)"));
        }

        [TestMethod, Priority(0)]
        public void ErrorInInput() {
            Attach("DebugReplTest2.py", 13);

            Assert.AreEqual("", ExecuteText("print(does_not_exist)", false));
            Assert.AreEqual(@"Traceback (most recent call last):
  File ""<debug input>"", line 1, in <module>
NameError: name 'does_not_exist' is not defined
", _window.Error);
        }

        [TestMethod, Priority(0)]
        public void ChangeVariables() {
            Attach("DebugReplTest2.py", 13);

            Assert.AreEqual("", ExecuteText("innermost_val = 1"));
            Assert.AreEqual("1", ExecuteText("print(innermost_val)"));
        }

        [TestMethod, Priority(0)]
        public void ChangeModule() {
            Attach("DebugReplTest1.py", 3);

            Assert.AreEqual("'hello'", ExecuteText("a"));

            // Change to the dis module
            Assert.AreEqual("Current module changed to dis", ExecuteCommand(new SwitchModuleCommand(), "dis"));
            Assert.AreEqual("", ExecuteText("test = 'world'"));
            Assert.AreEqual("'world'", ExecuteText("test"));

            // Change back to the current frame
            Assert.AreEqual("Current module changed to <CurrentFrame>", ExecuteCommand(new SwitchModuleCommand(), "<CurrentFrame>"));
            Assert.AreEqual("", ExecuteText("test", false));
            Assert.IsTrue(_window.Error.Contains("NameError:"));
            Assert.AreEqual("'hello'", ExecuteText("a"));
        }

        [TestMethod, Priority(0)]
        public void ChangeFrame() {
            Attach("DebugReplTest2.py", 13);

            // We are broken in the innermost function
            string stack;
            stack = ExecuteCommand(new DebugReplFramesCommand(), "");
            Assert.IsTrue(stack.StartsWith(@"=> Frame id=0, function=innermost
   Frame id=1, function=inner
   Frame id=2, function=outer
   Frame id=3, function=<module>"));
            Assert.AreEqual("0", ExecuteCommand(new DebugReplFrameCommand(), ""));
            Assert.AreEqual("51", ExecuteText("print(innermost_val)"));

            // Move up the stack to the inner function
            Assert.AreEqual("Current frame changed to 1", ExecuteCommand(new DebugReplFrameUpCommand(), ""));
            stack = ExecuteCommand(new DebugReplFramesCommand(), "");
            Assert.IsTrue(stack.StartsWith(@"   Frame id=0, function=innermost
=> Frame id=1, function=inner
   Frame id=2, function=outer
   Frame id=3, function=<module>"));
            Assert.AreEqual("1", ExecuteCommand(new DebugReplFrameCommand(), ""));
            Assert.AreEqual("50", ExecuteText("print(inner_val)"));

            // Move to frame 2, the outer function
            Assert.AreEqual("Current frame changed to 2", ExecuteCommand(new DebugReplFrameCommand(), "2"));
            Assert.AreEqual("2", ExecuteCommand(new DebugReplFrameCommand(), ""));
            Assert.AreEqual("10", ExecuteText("print(outer_val)"));

            // Move down the stack, back to the inner function
            Assert.AreEqual("Current frame changed to 1", ExecuteCommand(new DebugReplFrameDownCommand(), ""));
            Assert.AreEqual("1", ExecuteCommand(new DebugReplFrameCommand(), ""));
        }

        [TestMethod, Priority(0)]
        public void ChangeThread() {
            Attach("DebugReplTest3.py", 39);

            var threads = _processes[0].GetThreads();
            PythonThread main = threads.SingleOrDefault(t => t.Frames[0].FunctionName == "threadmain");
            PythonThread worker1 = threads.SingleOrDefault(t => t.Frames[0].FunctionName == "thread1");
            PythonThread worker2 = threads.SingleOrDefault(t => t.Frames[0].FunctionName == "thread2");

            // We are broken in the the main thread
            string text;
            text = ExecuteCommand(new DebugReplThreadsCommand(), "");
            Assert.IsTrue(text.Contains(String.Format("=> Thread id={0}, name=", main.Id)));
            Assert.IsTrue(text.Contains(String.Format("   Thread id={0}, name=", worker1.Id)));
            Assert.IsTrue(text.Contains(String.Format("   Thread id={0}, name=", worker2.Id)));
            Assert.AreEqual(main.Id.ToString(), ExecuteCommand(new DebugReplThreadCommand(), ""));
            Assert.AreEqual("False", ExecuteText("t1_done"));
            Assert.AreEqual("False", ExecuteText("t2_done"));

            // Switch to worker thread 1
            Assert.AreEqual(String.Format("Current thread changed to {0}, frame 0", worker1.Id), ExecuteCommand(new DebugReplThreadCommand(), worker1.Id.ToString()));
            text = ExecuteCommand(new DebugReplThreadsCommand(), "");
            Assert.IsTrue(text.Contains(String.Format("   Thread id={0}, name=", main.Id)));
            Assert.IsTrue(text.Contains(String.Format("=> Thread id={0}, name=", worker1.Id)));
            Assert.IsTrue(text.Contains(String.Format("   Thread id={0}, name=", worker2.Id)));
            Assert.AreEqual(worker1.Id.ToString(), ExecuteCommand(new DebugReplThreadCommand(), ""));
            Assert.AreEqual("'thread1'", ExecuteText("t1_val"));
        }

        [TestMethod, Priority(0)]
        public void ChangeProcess() {
            Attach("DebugReplTest4A.py", 3);
            Attach("DebugReplTest4B.py", 3);

            PythonProcess proc1 = _processes[0];
            PythonProcess proc2 = _processes[1];

            // We are broken in process 2 (the last one attached is the current one)
            string text;
            text = ExecuteCommand(new DebugReplProcessesCommand(), "");
            Assert.AreEqual(String.Format(@"   Process id={0}, Language version={2}
=> Process id={1}, Language version={2}", proc1.Id, proc2.Id, Version.Version), text);

            // Switch to process 1
            Assert.AreEqual(String.Format("Current process changed to {0}", proc1.Id), ExecuteCommand(new DebugReplProcessCommand(), proc1.Id.ToString()));
            Assert.AreEqual(String.Format("{0}", proc1.Id), ExecuteCommand(new DebugReplProcessCommand(), String.Empty));
            Assert.AreEqual("'hello'", ExecuteText("a1"));
            Assert.AreEqual("30", ExecuteText("b1"));

            // Switch to process 2
            Assert.AreEqual(String.Format("Current process changed to {0}", proc2.Id), ExecuteCommand(new DebugReplProcessCommand(), proc2.Id.ToString()));
            Assert.AreEqual(String.Format("{0}", proc2.Id), ExecuteCommand(new DebugReplProcessCommand(), String.Empty));
            Assert.AreEqual("'world'", ExecuteText("a2"));
            Assert.AreEqual("60", ExecuteText("b2"));
        }

        [TestMethod, Priority(0)]
        public void Abort() {
            Attach("DebugReplTest5.py", 3);

            _window.ClearScreen();
            var execute = _evaluator.ExecuteText("for i in range(0,20): time.sleep(0.5)");
            _evaluator.AbortCommand();
            execute.Wait();
            Assert.IsTrue(execute.Result.IsSuccessful);
            Assert.AreEqual("Abort is not supported.", _window.Error.TrimEnd());
        }

        [TestMethod, Priority(0)]
        public void StepInto() {
            // Make sure that we don't step into the internal repl code
            // http://pytools.codeplex.com/workitem/777
            Attach("DebugReplTest6.py", 2);

            var thread = _processes[0].GetThreads()[0];
            thread.StepInto();

            // Result of step into is not immediate
            Thread.Sleep(1000);

            // We should still be in the <module>, not in the internals of print in repl code
            foreach (var frame in thread.Frames) {
                Console.WriteLine("{0}:{1} [{2}]", frame.FunctionName, frame.LineNo, frame.FileName);
            }
            Assert.AreEqual(1, thread.Frames.Count);
            Assert.AreEqual("<module>", thread.Frames[0].FunctionName);
        }

        private string ExecuteCommand(IReplCommand cmd, string args) {
            _window.ClearScreen();
            var execute = cmd.Execute(_window, args);
            execute.Wait();
            Assert.IsTrue(execute.Result.IsSuccessful);
            return _window.Output.TrimEnd();
        }

        private string ExecuteText(string executionText) {
            return ExecuteText(executionText, true);
        }

        private string ExecuteText(string executionText, bool expectSuccess) {
            _window.ClearScreen();
            var execute = _evaluator.ExecuteText(executionText);
            execute.Wait();
            Assert.AreEqual(expectSuccess, execute.Result.IsSuccessful);
            return _window.Output.TrimEnd();
        }

        private void Attach(string filename, int lineNo) {
            var debugger = new PythonDebugger();
            PythonProcess process = debugger.DebugProcess(Version, DebuggerTestPath + filename, (newproc, newthread) => {
                var breakPoint = newproc.AddBreakPointByFileExtension(lineNo, filename);
                breakPoint.Add();
                _evaluator.AttachProcess(newproc, new MockThreadIdMapper());
            },
            debugOptions: PythonDebugOptions.CreateNoWindow);

            _processes.Add(process);

            using (var brkHit = new AutoResetEvent(false))
            using (var procExited = new AutoResetEvent(false)) {
                EventHandler<BreakpointHitEventArgs> breakpointHitHandler = (s, e) => brkHit.Set();
                EventHandler<ProcessExitedEventArgs> processExitedHandler = (s, e) => procExited.Set();
                process.BreakpointHit += breakpointHitHandler;
                process.ProcessExited += processExitedHandler;

                try {
                    process.Start();
                } catch (Win32Exception ex) {
                    _processes.Remove(process);
#if DEV11_OR_LATER
                    if (ex.HResult == -2147467259 /*0x80004005*/) {
                        Assert.Inconclusive("Required Python interpreter is not installed");
                    } else
#endif
                    {
                        Assert.Fail("Process start failed:\r\n" + ex.ToString());
                    }
                }

                var handles = new[] { brkHit, procExited };
                if (WaitHandle.WaitAny(handles, 25000) != 0) {
                    Assert.Fail("Failed to wait on event");
                }

                process.BreakpointHit -= breakpointHitHandler;
                process.ProcessExited -= processExitedHandler;
            }

            _evaluator.AttachProcess(process, new MockThreadIdMapper());
        }

        private class MockThreadIdMapper : IThreadIdMapper {
            public long? GetPythonThreadId(uint vsThreadId) {
                return vsThreadId;
            }
        }
    }

    [TestClass]
    public class DebugReplEvaluatorTests30 : DebugReplEvaluatorTests {
        [ClassInitialize]
        public static new void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        internal override PythonVersion Version {
            get {
                return PythonPaths.Python30 ?? PythonPaths.Python30_x64;
            }
        }
    }

    [TestClass]
    public class DebugReplEvaluatorTests31 : DebugReplEvaluatorTests {
        [ClassInitialize]
        public static new void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        internal override PythonVersion Version {
            get {
                return PythonPaths.Python31 ?? PythonPaths.Python31_x64;
            }
        }
    }

    [TestClass]
    public class DebugReplEvaluatorTests32 : DebugReplEvaluatorTests {
        [ClassInitialize]
        public static new void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        internal override PythonVersion Version {
            get {
                return PythonPaths.Python32 ?? PythonPaths.Python32_x64;
            }
        }
    }

    [TestClass]
    public class DebugReplEvaluatorTests33 : DebugReplEvaluatorTests {
        [ClassInitialize]
        public static new void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        internal override PythonVersion Version {
            get {
                return PythonPaths.Python33 ?? PythonPaths.Python33_x64;
            }
        }
    }

    [TestClass]
    public class DebugReplEvaluatorTests34 : DebugReplEvaluatorTests {
        [ClassInitialize]
        public static new void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        internal override PythonVersion Version {
            get {
                return PythonPaths.Python34 ?? PythonPaths.Python34_x64;
            }
        }
    }

    [TestClass]
    public class DebugReplEvaluatorTests35 : DebugReplEvaluatorTests {
        [ClassInitialize]
        public static new void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        internal override PythonVersion Version {
            get {
                return PythonPaths.Python35 ?? PythonPaths.Python35_x64;
            }
        }
    }

    [TestClass]
    public class DebugReplEvaluatorTests27 : DebugReplEvaluatorTests {
        [ClassInitialize]
        public static new void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        internal override PythonVersion Version {
            get {
                return PythonPaths.Python27 ?? PythonPaths.Python27_x64;
            }
        }
    }

    [TestClass]
    public class DebugReplEvaluatorTests25 : DebugReplEvaluatorTests {
        [ClassInitialize]
        public static new void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        internal override PythonVersion Version {
            get {
                return PythonPaths.Python25 ?? PythonPaths.Python25_x64;
            }
        }
    }

    [TestClass]
    public class DebugReplEvaluatorTestsIPy : DebugReplEvaluatorTests {
        [ClassInitialize]
        public static new void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        internal override PythonVersion Version {
            get {
                return PythonPaths.IronPython27 ?? PythonPaths.IronPython27_x64;
            }
        }
    }
}

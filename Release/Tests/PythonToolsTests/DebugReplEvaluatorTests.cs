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
using System.Linq;
using System.Threading;
using DebuggerTests;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Mocks;

namespace PythonToolsTests {
#if INTERACTIVE_WINDOW
    using IReplEvaluator = IInteractiveEngine;
    using IReplWindow = IInteractiveWindow;
    using IReplWindowProvider = IInteractiveWindowProvider;
#endif

    [TestClass]
    public class DebugReplEvaluatorTests {
        private PythonDebugReplEvaluator _evaluator;
        private MockReplWindow _window;
        private List<PythonProcess> _processes;

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            TestData.Deploy();
        }

        internal virtual string DebuggerTestPath {
            get {
                return TestData.GetPath(@"TestData\DebuggerProject\");
            }
        }

        internal virtual PythonVersion Version {
            get {
                return PythonPaths.Python26;
            }
        }

        [TestInitialize]
        public void TestInit() {
            _evaluator = new PythonDebugReplEvaluator();
            _window = new MockReplWindow(_evaluator);
            _evaluator.Initialize(_window);
            _processes = new List<PythonProcess>();
        }

        [TestCleanup]
        public void TestClean() {
            _processes.ForEach(proc => proc.Continue());
            _processes.ForEach(proc => proc.WaitForExit(5000));
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
        public void StepInto()
        {
            // Make sure that we don't step into the internal repl code
            // http://pytools.codeplex.com/workitem/777
            Attach("DebugReplTest6.py", 1);

            var thread = _processes[0].GetThreads()[0];
            thread.StepInto();
            
            // Result of step into is not immediate
            Thread.Sleep(1000);

            // We should still be in the <module>, not in the internals of print in repl code
            Assert.AreEqual(1, thread.Frames.Count);
            Assert.AreEqual("<module>", thread.Frames[0].FunctionName);
        }

        private string ExecuteCommand(IReplCommand cmd, string args)
        {
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
            PythonProcess process = BaseDebuggerTests.DebugProcess(debugger, Version, DebuggerTestPath + filename, (newproc, newthread) => {
                var breakPoint = BaseDebuggerTests.AddBreakPoint(newproc, lineNo, filename);
                breakPoint.Add();
                _evaluator.AttachProcess(newproc);
            },
            debugOptions: PythonDebugOptions.CreateNoWindow);

            _processes.Add(process);

            AutoResetEvent brkHit = new AutoResetEvent(false);
            process.BreakpointHit += (sender, args) => {
                brkHit.Set();
            };

            process.Start();

            if (!brkHit.WaitOne(25000)) {
                Assert.Fail("Failed to wait on event");
            }

            _evaluator.AttachProcess(process);
        }
    }

    [TestClass]
    public class DebugReplEvaluatorTests30 : DebugReplEvaluatorTests {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python30;
            }
        }
    }

    [TestClass]
    public class DebugReplEvaluatorTests31 : DebugReplEvaluatorTests {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python31;
            }
        }
    }

    [TestClass]
    public class DebugReplEvaluatorTests32 : DebugReplEvaluatorTests {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python32;
            }
        }
    }

    [TestClass]
    public class DebugReplEvaluatorTests27 : DebugReplEvaluatorTests {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python27;
            }
        }
    }

    [TestClass]
    public class DebugReplEvaluatorTests25 : DebugReplEvaluatorTests {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python25;
            }
        }
    }

    [TestClass]
    public class DebugReplEvaluatorTestsIPy : DebugReplEvaluatorTests {
        internal override PythonVersion Version {
            get {
                return PythonPaths.IronPython27;
            }
        }
    }
}

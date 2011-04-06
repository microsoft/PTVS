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
using System.Threading;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnalysisTest {
    [TestClass]
    [DeploymentItem(@"..\\PythonTools\\debugger.py")]
    [DeploymentItem(@"..\\PythonTools\\launcher.py")]
    [DeploymentItem(@"Python.VS.TestData\", "Python.VS.TestData")]
    [DeploymentItem("Binaries\\Win32\\Debug\\PyDebugAttach.dll")]
    [DeploymentItem("Binaries\\Win32\\Debug\\x64\\PyDebugAttach.dll", "x64")]
    public class DebuggerTests {
        private const string _pythonPath = "C:\\Python26\\python.exe";

        [TestMethod]
        public void TestThreads() {
            // TODO: Thread creation tests w/ both thread.start_new_thread and threading module.
        }

        #region Enum Children Tests

        [TestMethod]
        public void EnumChildrenTest() {
            const int lastLine = 27;

            if (Version.Version.Is3x()) {
                ChildTest(EnumChildrenTestName, lastLine, "s", new ChildInfo("[0]", "frozenset({2, 3, 4})"));
            } else {
                ChildTest(EnumChildrenTestName, lastLine, "s", new ChildInfo("[0]", "frozenset([2, 3, 4])"));
            }
            if (GetType() != typeof(DebuggerTestsIpy) && Version.Version.Is2x()) {
                // IronPython unicode repr differs
                // 3.x: http://pytools.codeplex.com/workitem/76
                ChildTest(EnumChildrenTestName, lastLine, "cinst", new ChildInfo("abc", "42", "0x2a"), new ChildInfo("uc", "u\'привет мир\'"));
            }
            ChildTest(EnumChildrenTestName, lastLine, "c2inst", new ChildInfo("abc", "42", "0x2a"), new ChildInfo("bar", "100", "0x64"), new ChildInfo("self", "myrepr", "myhex"));
            ChildTest(EnumChildrenTestName, lastLine, "l", new ChildInfo("[0]", "1"), new ChildInfo("[1]", "2"));
            ChildTest(EnumChildrenTestName, lastLine, "d1", new ChildInfo("[42]", "100", "0x64"));
            ChildTest(EnumChildrenTestName, lastLine, "d2", new ChildInfo("['abc']", "'foo'"));
            ChildTest(EnumChildrenTestName, lastLine, "i", null);
            ChildTest(EnumChildrenTestName, lastLine, "u1", null);
        }

        public virtual string EnumChildrenTestName {
            get {
                return "EnumChildTest.py";
            }
        }

        private void ChildTest(string filename, int lineNo, string text, params ChildInfo[] children) {
            var debugger = new PythonDebugger();
            PythonThread thread = null;
            var process = DebugProcess(debugger, DebuggerTestPath + filename, (newproc, newthread) => {
                var breakPoint = newproc.AddBreakPoint(filename, lineNo);
                breakPoint.Add();
                thread = newthread;
            });

            AutoResetEvent brkHit = new AutoResetEvent(false);
            process.BreakpointHit += (sender, args) => {
                brkHit.Set();
            };
            process.ExceptionRaised += (sender, args) => {
                // some versions of Python raise exceptions during startup
                args.Thread.Resume();
            };

            process.Start();

            AssertWaited(brkHit);

            var frames = thread.Frames;

            AutoResetEvent evalComplete = new AutoResetEvent(false);
            PythonEvaluationResult evalRes = null;
            frames[0].ExecuteText(text, (completion) => {
                evalRes = completion;
                evalComplete.Set();
            });

            AssertWaited(evalComplete);
            Assert.IsTrue(evalRes != null);


            if (children == null) {
                Assert.IsTrue(!evalRes.IsExpandable);
                Assert.IsTrue(evalRes.GetChildren(Int32.MaxValue) == null);
            } else {
                Assert.IsTrue(evalRes.IsExpandable);
                var childrenReceived = new List<PythonEvaluationResult>(evalRes.GetChildren(Int32.MaxValue));

                Assert.IsTrue(children.Length == childrenReceived.Count);
                for (int i = 0; i < children.Length; i++) {
                    var curChild = children[i];
                    bool foundChild = false;
                    for (int j = 0; j < childrenReceived.Count; j++) {
                        var curReceived = childrenReceived[j];
                        if (ChildrenMatch(curChild, curReceived)) {
                            foundChild = true;

                            if (children[i].ChildText.StartsWith("[")) {
                                Assert.AreEqual(childrenReceived[j].Expression, text + children[i].ChildText);
                            } else {
                                Assert.AreEqual(childrenReceived[j].Expression, text + "." + children[i].ChildText);
                            }

                            Assert.AreEqual(childrenReceived[j].Frame, frames[0]);
                            childrenReceived.RemoveAt(j);
                            break;
                        }
                    }
                    Assert.IsTrue(foundChild);
                }
                Assert.IsTrue(childrenReceived.Count == 0);
            }


            process.Continue();

            process.WaitForExit();
        }

        private bool ChildrenMatch(ChildInfo curChild, PythonEvaluationResult curReceived) {
            return curReceived.ChildText == curChild.ChildText && 
                curReceived.StringRepr == curChild.Repr &&
                (Version.Version.Is3x() || (curChild.HexRepr == null || curChild.HexRepr == curReceived.HexRepr));// __hex__ no longer used in 3.x, http://mail.python.org/pipermail/python-list/2009-September/1218287.html
        }

        class ChildInfo {
            public readonly string ChildText;
            public readonly string Repr;
            public readonly string HexRepr;

            public ChildInfo(string key, string value, string hexRepr = null) {
                ChildText = key;
                Repr = value;
                HexRepr = hexRepr;
            }
        }

        #endregion

        #region Set Next Line Tests

        [TestMethod]
        public void SetNextLineTest() {
            if (GetType() == typeof(DebuggerTestsIpy)) {
                //http://ironpython.codeplex.com/workitem/30129
                return;
            }

            var debugger = new PythonDebugger();
            PythonThread thread = null;
            var process = DebugProcess(debugger, DebuggerTestPath + @"SetNextLine.py", (newproc, newthread) => {
                var breakPoint = newproc.AddBreakPoint("SetNextLine.py", 1);
                breakPoint.Add();
                thread = newthread;
            });

            AutoResetEvent brkHit = new AutoResetEvent(false);
            AutoResetEvent stepDone = new AutoResetEvent(false);
            process.BreakpointHit += (sender, args) => {
                brkHit.Set();
            };
            process.StepComplete += (sender, args) => {
                stepDone.Set();
            };
            process.ExceptionRaised += (sender, args) => {
                args.Thread.Resume();
            };

            process.Start();

            AssertWaited(brkHit);

            var moduleFrame = thread.Frames[0];
            Assert.AreEqual(moduleFrame.StartLine, 1);
            if (GetType() != typeof(DebuggerTestsIpy)) {
                Assert.AreEqual(moduleFrame.EndLine, 13);
            }

            // skip over def f()
            Assert.IsTrue(moduleFrame.SetLineNumber(6));

            // set break point in g, run until we hit it.
            var newBp = process.AddBreakPoint("SetNextLine.py", 7);
            newBp.Add();

            process.Resume();
            AssertWaited(brkHit);

            thread.StepOver(); // step over x = 42
            AssertWaited(stepDone);

            // skip y = 100
            Assert.IsTrue(moduleFrame.SetLineNumber(9));

            thread.StepOver(); // step over z = 200
            AssertWaited(stepDone);

            // z shouldn't be defined
            var frames = thread.Frames;
            new HashSet<string>(new[] { "x", "z" }).ContainsExactly(frames[0].Locals.Select(x => x.Expression));

            // set break point in module, run until we hit it.
            newBp = process.AddBreakPoint("SetNextLine.py", 13);
            newBp.Add();
            thread.Resume();
            AssertWaited(brkHit);

            // f shouldn't be defined.
            frames = thread.Frames;
            new HashSet<string>(new[] { "sys", "g" }).ContainsExactly(frames[0].Locals.Select(x => x.Expression));

            process.Continue();

            process.WaitForExit();
        }

        #endregion

        #region BreakAll Tests

        internal const string DebuggerTestPath = @"Python.VS.TestData\DebuggerProject\";
        [TestMethod]
        public void TestBreakAll() {
            var debugger = new PythonDebugger();

            PythonThread thread = null;
            AutoResetEvent loaded = new AutoResetEvent(false);
            var process = DebugProcess(debugger, DebuggerTestPath + "BreakAllTest.py", (newproc, newthread) => {
                loaded.Set();
                thread = newthread;
            });
            process.ExceptionRaised += (sender, args) => {
                args.Thread.Resume();
            };

            process.Start();
            AssertWaited(loaded);

            // let loop run
            Thread.Sleep(500);
            AutoResetEvent breakComplete = new AutoResetEvent(false);
            PythonThread breakThread = null;
            process.AsyncBreakComplete += (sender, args) => {
                breakThread = args.Thread;
                breakComplete.Set();
            };

            process.Break();
            AssertWaited(breakComplete);

            Assert.AreEqual(breakThread, thread);

            process.Resume();

            process.Terminate();
        }

        private static void AssertWaited(EventWaitHandle eventObj) {
            if (!eventObj.WaitOne(10000)) {
                Assert.Fail("Failed to wait on event");
            }
        }

        [TestMethod]
        public void TestBreakAllThreads() {
            var debugger = new PythonDebugger();

            PythonThread thread = null;
            AutoResetEvent loaded = new AutoResetEvent(false);
            var process = DebugProcess(debugger, DebuggerTestPath + "InfiniteThreads.py", (newproc, newthread) => {
                loaded.Set();
                thread = newthread;
            });
            process.ExceptionRaised += (sender, args) => {
                args.Thread.Resume();
            };

            process.Start();
            AssertWaited(loaded);

            AutoResetEvent breakComplete = new AutoResetEvent(false);
            process.AsyncBreakComplete += (sender, args) => {
                breakComplete.Set();
            };

            // let loop run
            for (int i = 0; i < 100; i++) {
                Thread.Sleep(50);

                Debug.WriteLine(String.Format("Breaking {0}", i));
                process.Break();
                if (!breakComplete.WaitOne(10000)) {
                    Console.WriteLine("Failed to break");
                }
                process.Resume();
                Debug.WriteLine(String.Format("Resumed {0}", i));
            }

            process.Terminate();
        }

        #endregion

        #region Eval Tests

        [TestMethod]
        public void EvalTest() {
            EvalTest("LocalsTest4.py", 2, "g", 1, EvalResult.Value("baz", "int", "42"));
            EvalTest("LocalsTest3.py", 2, "f", 0, EvalResult.Value("x", "int", "42"));
            EvalTest("LocalsTest3.py", 2, "f", 0, EvalResult.Exception("not_defined", "name 'not_defined' is not defined"));
            EvalTest("LocalsTest3.py", 2, "f", 0, EvalResult.ErrorExpression("/2", "unexpected token '/'\r\n"));
        }

        class EvalResult {
            private readonly string _typeName, _repr;
            public readonly string ExceptionText, Expression;
            public readonly bool IsError;

            public static EvalResult Exception(string expression, string exceptionText) {
                return new EvalResult(expression, exceptionText, false);
            }

            public static EvalResult Value(string expression, string typeName, string repr) {
                return new EvalResult(expression, typeName, repr);
            }

            public static EvalResult ErrorExpression(string expression, string error) {
                return new EvalResult(expression, error, true);
            }

            EvalResult(string expression, string exceptionText, bool isError) {
                Expression = expression;
                ExceptionText = exceptionText;
                IsError = isError;
            }

            EvalResult(string expression, string typeName, string repr) {
                Expression = expression;
                _typeName = typeName;
                _repr = repr;
            }

            public void Validate(PythonEvaluationResult result) {
                if (ExceptionText != null) {
                    Assert.AreEqual(result.ExceptionText, ExceptionText);
                } else {
                    Assert.AreEqual(result.TypeName, _typeName);
                    Assert.AreEqual(result.StringRepr, _repr);
                }
            }
        }

        private void EvalTest(string filename, int lineNo, string frameName, int frameIndex, EvalResult eval) {
            var debugger = new PythonDebugger();
            PythonThread thread = null;
            var process = DebugProcess(debugger, DebuggerTestPath + filename, (newproc, newthread) => {
                var breakPoint = newproc.AddBreakPoint(filename, lineNo);
                breakPoint.Add();
                thread = newthread;
            });

            AutoResetEvent brkHit = new AutoResetEvent(false);
            process.BreakpointHit += (sender, args) => {
                brkHit.Set();
            };
            process.ExceptionRaised += (sender, args) => {
                args.Thread.Resume();
            };

            process.Start();

            AssertWaited(brkHit);

            var frames = thread.Frames;

            PythonEvaluationResult obj = null;
            string errorMsg;
            if (eval.IsError) {
                Assert.IsTrue(!frames[frameIndex].TryParseText(eval.Expression, out errorMsg));
                Assert.AreEqual(errorMsg, eval.ExceptionText);
            } else {
                Assert.IsTrue(frames[frameIndex].TryParseText(eval.Expression, out errorMsg));
                Assert.AreEqual(errorMsg, null);

                AutoResetEvent textExecuted = new AutoResetEvent(false);
                Assert.AreEqual(frameName, frames[frameIndex].FunctionName);
                frames[frameIndex].ExecuteText(eval.Expression, (completion) => {
                    obj = completion;
                    textExecuted.Set();
                }
                );
                AssertWaited(textExecuted);
                eval.Validate(obj);
            }

            process.Continue();

            process.WaitForExit();
        }


        #endregion

        #region Local Tests

        [TestMethod]
        public void LocalsTest() {
            LocalsTest("LocalsTest.py", 3, new string[] { }, new string[] { "x" });

            LocalsTest("LocalsTest2.py", 2, new string[] { "x" }, new string[] { });

            LocalsTest("LocalsTest3.py", 2, new string[] { "x" }, new string[] { "y" });
        }

        [TestMethod]
        public void GlobalsTest() {
            if (Version.Version >= PythonLanguageVersion.V32) {
                LocalsTest("GlobalsTest.py", 4, new string[] { }, new[] { "x", "y", "__file__", "__name__", "__package__", "__builtins__", "__doc__", "__cached__" });
            } else if (Version.Version >= PythonLanguageVersion.V26 && GetType() != typeof(DebuggerTestsIpy)) { // IronPython doesn't set __package__
                LocalsTest("GlobalsTest.py", 4, new string[] { }, new[] { "x", "y", "__file__", "__name__", "__package__", "__builtins__", "__doc__" });
            } else {
                LocalsTest("GlobalsTest.py", 4, new string[] { }, new[] { "x", "y", "__file__", "__name__", "__builtins__", "__doc__" });
            }
        }

        private void LocalsTest(string filename, int lineNo, string[] paramNames, string[] localsNames) {
            var debugger = new PythonDebugger();
            PythonThread thread = null;
            var process = DebugProcess(debugger, DebuggerTestPath + filename, (newproc, newthread) => {
                var breakPoint = newproc.AddBreakPoint(filename, lineNo);
                breakPoint.Add();
                thread = newthread;
            });

            AutoResetEvent brkHit = new AutoResetEvent(false);
            process.BreakpointHit += (sender, args) => {
                brkHit.Set();
            };
            process.ExceptionRaised += (sender, args) => {
                // some versions of Python raise exceptions
                args.Thread.Resume();
            };

            process.Start();

            AssertWaited(brkHit);

            var frames = thread.Frames;
            var localsExpected = new HashSet<string>(localsNames);
            var paramsExpected = new HashSet<string>(paramNames);
            Assert.IsTrue(localsExpected.ContainsExactly(frames[0].Locals.Select(x => x.Expression)));
            Assert.IsTrue(paramsExpected.ContainsExactly(frames[0].Parameters.Select(x => x.Expression)));
            Assert.AreEqual(frames[0].FileName, Path.GetFullPath(DebuggerTestPath + filename));

            process.Continue();

            process.WaitForExit();
        }

        #endregion

        #region Stepping Tests

        [TestMethod]
        public void StepTest() {
            if (Version.Version < PythonLanguageVersion.V30) {  // step into print on 3.x runs more Python code
                StepTest(DebuggerTestPath + @"SteppingTest7.py",
                    new ExpectedStep(StepKind.Over, 1),     // step over def f():
                    new ExpectedStep(StepKind.Over, 6),     // step over def g():
                    new ExpectedStep(StepKind.Over, 10),     // step over def h()
                    new ExpectedStep(StepKind.Into, 13),     // step into f() call
                    new ExpectedStep(StepKind.Into, 2),     // step into print 'abc'
                    new ExpectedStep(StepKind.Into, 3),     // step into print 'def'
                    new ExpectedStep(StepKind.Into, 4),     // step into print 'baz'
                    new ExpectedStep(StepKind.Into, 14),     // step into g()
                    new ExpectedStep(StepKind.Into, 7),     // step into dict assign
                    new ExpectedStep(StepKind.Into, 8),     // step into print 'hello'
                    new ExpectedStep(StepKind.Into, 15),     // step into h()
                    new ExpectedStep(StepKind.Into, 11)    // step into h() return
                );
            }

            StepTest(DebuggerTestPath + @"SteppingTest6.py",
                new ExpectedStep(StepKind.Over, 1),     // step over print 'hello world'
                new ExpectedStep(StepKind.Over, 2),     // step over a = set([i for i in range(256)])
                new ExpectedStep(StepKind.Over, 3),     // step over print a
                new ExpectedStep(StepKind.Resume, 3)    // let the program exit
            );

            StepTest(DebuggerTestPath + @"SteppingTest5.py",
                new ExpectedStep(StepKind.Over, 1),     // step over def g():...
                new ExpectedStep(StepKind.Over, 4),     // step over def f():...
                new ExpectedStep(StepKind.Into, 8),     // step into f()
                new ExpectedStep(StepKind.Out, 5),      // step out of f() on line "g()"
                new ExpectedStep(StepKind.Resume, 8)    // let the program exit
            );

            StepTest(DebuggerTestPath + @"SteppingTest4.py",
                new ExpectedStep(StepKind.Over, 1),     // step over def f():...
                new ExpectedStep(StepKind.Into, 5),     // step into f()
                new ExpectedStep(StepKind.Over, 2),     // step over for i in xrange(10):
                new ExpectedStep(StepKind.Over, 3),     // step over for print i
                new ExpectedStep(StepKind.Over, 2),     // step over for i in xrange(10):
                new ExpectedStep(StepKind.Over, 3),     // step over for print i
                new ExpectedStep(StepKind.Over, 2),     // step over for i in xrange(10):
                new ExpectedStep(StepKind.Over, 3),     // step over for print i
                new ExpectedStep(StepKind.Over, 2),     // step over for i in xrange(10):
                new ExpectedStep(StepKind.Resume, 5)    // let the program exit
            );

            StepTest(DebuggerTestPath + @"SteppingTest3.py",
                new ExpectedStep(StepKind.Over, 1),     // step over def f():...
                new ExpectedStep(StepKind.Over, 5),     // step over f()
                new ExpectedStep(StepKind.Resume, 5)    // let the program exit
            );

            StepTest(DebuggerTestPath + @"SteppingTest3.py",
                new ExpectedStep(StepKind.Over, 1),     // step over def f():...
                new ExpectedStep(StepKind.Into, 5),     // step into f()
                new ExpectedStep(StepKind.Out, 2),     // step out of f()
                new ExpectedStep(StepKind.Resume, 5)    // let the program exit
            );

            StepTest(DebuggerTestPath + @"SteppingTest2.py",
                new ExpectedStep(StepKind.Over, 1),     // step over def f():...
                new ExpectedStep(StepKind.Into, 4),     // step into f()
                new ExpectedStep(StepKind.Over, 2),     // step over print 'hi'
                new ExpectedStep(StepKind.Resume, 4)    // let the program exit
            );

            StepTest(DebuggerTestPath + @"SteppingTest2.py",
                new ExpectedStep(StepKind.Over, 1),     // step over def f():...
                new ExpectedStep(StepKind.Into, 4),     // step into f()
                new ExpectedStep(StepKind.Over, 2),     // step over print 'hi'
                new ExpectedStep(StepKind.Over, 4)      // let the program exit
            );

            StepTest(DebuggerTestPath + @"SteppingTest.py",
                new ExpectedStep(StepKind.Over, 1),     // step over print "hello"
                new ExpectedStep(StepKind.Over, 2),     // step over print "goodbye"
                new ExpectedStep(StepKind.Resume, 2)   // let the program exit
            );
        }

        enum StepKind {
            Into,
            Out,
            Over,
            Resume
        }

        class ExpectedStep {
            public readonly StepKind Kind;
            public readonly int StartLine;

            public ExpectedStep(StepKind kind, int startLine) {
                Kind = kind;
                StartLine = startLine;
            }
        }

        private void StepTest(string filename, params ExpectedStep[] kinds) {
            var debugger = new PythonDebugger();

            string fullPath = Path.GetFullPath(filename);
            string dir = Path.GetDirectoryName(filename);
            var process = debugger.CreateProcess(Version.Version, Version.Path, "\"" + fullPath + "\"", dir, "");
            PythonThread thread = null;
            process.ThreadCreated += (sender, args) => {
                thread = args.Thread;
            };


            AutoResetEvent processEvent = new AutoResetEvent(false);

            bool processLoad = false, stepComplete = false;
            process.ProcessLoaded += (sender, args) => {
                processLoad = true;
                processEvent.Set();
            };

            process.StepComplete += (sender, args) => {
                stepComplete = true;
                processEvent.Set();
            };

            process.ExceptionRaised += (sender, args) => {
                args.Thread.Resume();
            };

            process.Start();

            for (int curStep = 0; curStep < kinds.Length; curStep++) {
                // process the stepping events as they occur, we cannot callback during the
                // event because the notificaiton happens on the debugger thread and we 
                // need to callback to get the frames.
                AssertWaited(processEvent);

                // first time through we hit process load, each additional time we should hit step complete.
                Debug.Assert((processLoad == true && stepComplete == false && curStep == 0) ||
                            (stepComplete == true && processLoad == false && curStep != 0));

                processLoad = stepComplete = false;

                var frames = thread.Frames;
                var stepInfo = kinds[curStep];
                Assert.AreEqual(stepInfo.StartLine, frames[0].LineNo, String.Format("{0} != {1} on {2} step", stepInfo.StartLine, frames[0].LineNo, curStep));

                switch (stepInfo.Kind) {
                    case StepKind.Into:
                        thread.StepInto();
                        break;
                    case StepKind.Out:
                        thread.StepOut();
                        break;
                    case StepKind.Over:
                        thread.StepOver();
                        break;
                    case StepKind.Resume:
                        process.Resume();
                        break;
                }
            }

            process.WaitForExit();
        }

        #endregion

        #region Breakpoint Tests

        [TestMethod]
        public void TestBreakpoints() {
            BreakpointTest("BreakpointTest.py", new[] { 1 }, new[] { 1 });
        }

        [TestMethod]
        public void TestBreakpoints2() {
            BreakpointTest("BreakpointTest2.py", new[] { 3 }, new[] { 3, 3, 3 });
        }

        [TestMethod]
        public void TestBreakpoints3() {
            BreakpointTest("BreakpointTest3.py", new[] { 1 }, new[] { 1 });
        }

        [TestMethod]
        public void TestBreakpointsConditional() {
            BreakpointTest("BreakpointTest2.py", new[] { 3 }, new[] { 3 }, new[] { "i == 1" });
        }

        [TestMethod]
        public void TestBreakpointsConditionalOnChange() {
            BreakpointTest("BreakpointTest5.py", new[] { 4 }, new[] { 4, 4, 4, 4, 4 }, new[] { "j" }, new[] { true });
        }

        [TestMethod]
        public void TestBreakpointRemove() {
            BreakpointTest("BreakpointTest2.py", new[] { 3 }, new[] { -3 });
        }

        [TestMethod]
        public void TestBreakpointFailed() {
            var debugger = new PythonDebugger();

            PythonThread thread = null;
            PythonBreakpoint breakPoint = null;
            var process = DebugProcess(debugger, DebuggerTestPath + "BreakpointTest.py", (newproc, newthread) => {
                breakPoint = newproc.AddBreakPoint("doesnotexist.py", 1);
                breakPoint.Add();
                thread = newthread;
            });

            bool bindFailed = false;
            process.BreakpointBindFailed += (sender, args) => {
                bindFailed = true;
                Assert.AreEqual(args.Breakpoint, breakPoint);
            };
            process.ExceptionRaised += (sender, args) => {
                args.Thread.Resume();
            };

            process.Start();

            process.WaitForExit();

            Assert.AreEqual(bindFailed, true);
        }

        /// <summary>
        /// Runs the given file name setting break points at linenos.  Expects to hit the lines
        /// in lineHits as break points in the order provided in lineHits.  If lineHits is negative
        /// expects to hit the positive number and then removes the break point.
        /// </summary>
        private void BreakpointTest(string filename, int[] linenos, int[] lineHits, string[] conditions = null, bool[] breakWhenChanged = null) {
            var debugger = new PythonDebugger();
            PythonThread thread = null;
            var process = DebugProcess(debugger, DebuggerTestPath + filename, (newproc, newthread) => {
                for (int i = 0; i < linenos.Length; i++) {
                    var line = linenos[i];

                    int finalLine = line;
                    if (finalLine < 0) {
                        finalLine = -finalLine;
                    }

                    PythonBreakpoint breakPoint;
                    if (conditions != null) {
                        if (breakWhenChanged != null) {
                            breakPoint = newproc.AddBreakPoint(filename, line, conditions[i], breakWhenChanged[i]);
                        } else {
                            breakPoint = newproc.AddBreakPoint(filename, line, conditions[i]);
                        }
                    } else {
                        breakPoint = newproc.AddBreakPoint(filename, line);
                    }

                    breakPoint.Add();
                }
                thread = newthread;
            });

            process.BreakpointBindFailed += (sender, args) => {
                Assert.Fail("unexpected bind failure");
            };

            var lineList = new List<int>(linenos);

            int breakpointBound = 0;
            int breakpointHit = 0;
            process.BreakpointBindSucceeded += (sender, args) => {
                Assert.AreEqual(args.Breakpoint.Filename, filename);
                int index = lineList.IndexOf(args.Breakpoint.LineNo);
                Assert.IsTrue(index != -1);
                lineList[index] = -1;
                breakpointBound++;
            };

            process.BreakpointHit += (sender, args) => {
                if (lineHits[breakpointHit] < 0) {
                    Assert.AreEqual(args.Breakpoint.LineNo, -lineHits[breakpointHit++]);
                    try {
                        args.Breakpoint.Remove();
                    } catch {
                        Debug.Assert(false);
                    }
                } else {
                    Assert.AreEqual(args.Breakpoint.LineNo, lineHits[breakpointHit++]);
                }
                Assert.AreEqual(args.Thread, thread);
                process.Continue();
            };

            process.ExceptionRaised += (sender, args) => {
                args.Thread.Resume();
            };

            process.Start();

            process.WaitForExit();

            Assert.AreEqual(breakpointHit, lineHits.Length);
            Assert.AreEqual(breakpointBound, linenos.Length);
        }

        #endregion

        #region Exception Tests

        class ExceptionInfo {
            public readonly string TypeName;
            public readonly int LineNumber;

            public ExceptionInfo(string typeName, int lineNumber) {
                TypeName = typeName;
                LineNumber = lineNumber;
            }
        }

        public string ExceptionModule {
            get {
                if (Version.Version.Is3x()) {
                    return "builtins";
                }
                return "exceptions";
            }
        }

        public string PickleModule {
            get {
                if (Version.Version.Is3x()) {
                    return "_pickle";
                }
                return "cPickle";
            }
        }

        public virtual string ComplexExceptions {
            get {
                return "ComplexExceptions.py";
            }
        }

        [TestMethod]
        public void TestExceptions() {
            var debugger = new PythonDebugger();
            for (int i = 0; i < 2; i++) {
                TestException(debugger, DebuggerTestPath + @"SimpleException.py", i == 0, new ExceptionInfo(ExceptionModule + ".Exception", 3));

                TestException(debugger, DebuggerTestPath + ComplexExceptions, i == 0,
                    new ExceptionInfo(PickleModule + ".PickleError", 6),
                    new ExceptionInfo(ExceptionModule + ".StopIteration", 13),
                    new ExceptionInfo(ExceptionModule + ".NameError", 15),
                    new ExceptionInfo(ExceptionModule + ".StopIteration", 21),
                    new ExceptionInfo(ExceptionModule + ".NameError", 23),
                    new ExceptionInfo(ExceptionModule + ".Exception", 29),
                    new ExceptionInfo(ExceptionModule + ".Exception", 32)
                );

                if (Version.Version.Is2x()) {
                    TestException(debugger, DebuggerTestPath + @"UnicodeException.py", i == 0, new ExceptionInfo(ExceptionModule + ".Exception", 3));
                }
            }
        }

        private void TestException(PythonDebugger debugger, string filename, bool resumeProcess, params ExceptionInfo[] exceptions) {
            var process = DebugProcess(debugger, filename);
            bool loaded = false;
            process.ProcessLoaded += (sender, args) => {
                loaded = true;
            };
            int curException = 0;
            process.ExceptionRaised += (sender, args) => {
                // V30 raises an exception as the process shuts down.
                if (loaded && ((Version.Version == PythonLanguageVersion.V30 && curException < exceptions.Length) || Version.Version != PythonLanguageVersion.V30)) {
                    Assert.AreEqual(args.Exception.TypeName, exceptions[curException].TypeName);

                    // http://ironpython.codeplex.com/workitem/30130
                    if (GetType() != typeof(DebuggerTestsIpy)) {
                        Assert.IsTrue(args.Exception.Description.IndexOf("line " + exceptions[curException].LineNumber) != -1);
                    }
                    Assert.IsTrue(args.Exception.Description.IndexOf(filename) != -1);

                    curException++;
                    if (resumeProcess) {
                        process.Resume();
                    } else {
                        args.Thread.Resume();
                    }
                } else {
                    args.Thread.Resume();
                }
            };

            process.Start();
            process.WaitForExit();

            Assert.AreEqual(exceptions.Length, curException);
        }

        #endregion

        #region Module Load Tests

        [TestMethod]
        public void TestModuleLoad() {
            var debugger = new PythonDebugger();

            // main file is reported
            TestModuleLoad(debugger, @"Python.VS.TestData\HelloWorld\Program.py", "Program.py");

            // imports are reported
            TestModuleLoad(debugger, DebuggerTestPath + @"imports_other.py", "imports_other.py", "is_imported.py");
        }

        private void TestModuleLoad(PythonDebugger debugger, string filename, params string[] expectedModulesLoaded) {
            var process = DebugProcess(debugger, filename);

            List<string> receivedFilenames = new List<string>();
            process.ModuleLoaded += (sender, args) => {
                receivedFilenames.Add(args.Module.Filename);
            };
            process.ExceptionRaised += (sender, args) => {
                args.Thread.Resume();
            };

            process.Start();
            process.WaitForExit();

            Assert.IsTrue(receivedFilenames.Count >= expectedModulesLoaded.Length);
            var set = new HashSet<string>();
            foreach (var received in receivedFilenames) {
                set.Add(Path.GetFileName(received));
            }

            foreach (var file in expectedModulesLoaded) {
                Assert.IsTrue(set.Contains(file));
            }
        }

        #endregion

        #region Exit Code Tests

        [TestMethod]
        public void TestStartup() {
            var debugger = new PythonDebugger();

            // hello world
            TestExitCode(debugger, @"Python.VS.TestData\HelloWorld\Program.py", 0);

            // test which calls sys.exit(23)
            TestExitCode(debugger, DebuggerTestPath + @"SysExit.py", 23);

            // test which calls raise Exception()
            TestExitCode(debugger, DebuggerTestPath + @"ExceptionalExit.py", 1);
        }

        private void TestExitCode(PythonDebugger debugger, string filename, int expectedExitCode) {
            var process = DebugProcess(debugger, filename);

            bool created = false, exited = false;
            process.ThreadCreated += (sender, args) => {
                created = true;
            };
            process.ThreadExited += (sender, args) => {
                exited = true;
            };
            process.ProcessExited += (sender, args) => {
                Assert.AreEqual(args.ExitCode, expectedExitCode);
            };
            process.ExceptionRaised += (sender, args) => {
                process.Resume();
            };

            process.Start();
            process.WaitForExit();

            Assert.IsTrue(created);
            Assert.IsTrue(exited);
        }

        private PythonProcess DebugProcess(PythonDebugger debugger, string filename, Action<PythonProcess, PythonThread> onLoaded = null) {
            string fullPath = Path.GetFullPath(filename);
            string dir = Path.GetFullPath(Path.GetDirectoryName(filename));
            var process = debugger.CreateProcess(Version.Version, Version.Path, "\"" + fullPath + "\"", dir, "");
            process.ProcessLoaded += (sender, args) => {
                if (onLoaded != null) {
                    onLoaded(process, args.Thread);
                }
                process.Resume();
            };

            return process;
        }

        #endregion

        #region Attach Tests

        [TestMethod]
        public void AttachReattach() {
            if (GetType() != typeof(DebuggerTestsIpy)) {    // IronPython doesn't support attach
                Process p = Process.Start(Version.Path, "\"" + Path.GetFullPath(@"Python.VS.TestData\DebuggerProject\InfiniteRun.py") + "\"");
                System.Threading.Thread.Sleep(1000);

                for (int i = 0; i < 10; i++) {
                    Console.WriteLine(i);

                    PythonProcess proc;
                    ConnErrorMessages errReason;
                    if ((errReason = PythonProcess.TryAttach(p.Id, out proc)) != ConnErrorMessages.None) {
                        Assert.Fail("Failed to attach {0}", errReason);
                    }

                    proc.Detach();
                }

                p.Kill();
            }
        }
        /*
        [TestMethod]
        public void AttachReattach64() {
            Process p = Process.Start("C:\\Python27_x64\\python.exe", "\"" + Path.GetFullPath(@"Python.VS.TestData\DebuggerProject\InfiniteRun.py") + "\"");
            System.Threading.Thread.Sleep(1000);

            for (int i = 0; i < 10; i++) {
                Console.WriteLine(i);

                PythonProcess proc;
                ConnErrorMessages errReason;
                if ((errReason = PythonProcess.TryAttach(p.Id, out proc)) != ConnErrorMessages.None) {
                    Assert.Fail("Failed to attach {0}", errReason);
                }

                proc.Detach();
            }

            p.Kill();
        }*/

        [TestMethod]
        public void AttachReattachThreadingInited() {
            if (GetType() != typeof(DebuggerTestsIpy)) {    // IronPython shouldn't support attach
                Process p = Process.Start(Version.Path, "\"" + Path.GetFullPath(@"Python.VS.TestData\DebuggerProject\InfiniteRunThreadingInited.py") + "\"");
                System.Threading.Thread.Sleep(1000);

                for (int i = 0; i < 10; i++) {
                    Console.WriteLine(i);

                    PythonProcess proc;
                    ConnErrorMessages errReason;
                    if ((errReason = PythonProcess.TryAttach(p.Id, out proc)) != ConnErrorMessages.None) {
                        Assert.Fail("Failed to attach {0}", errReason);
                    }

                    proc.Detach();
                }

                p.Kill();
            }
        }

        [TestMethod]
        public void AttachReattachInfiniteThreads() {
            if (GetType() != typeof(DebuggerTestsIpy)) {    // IronPython shouldn't support attach
                Process p = Process.Start(Version.Path, "\"" + Path.GetFullPath(@"Python.VS.TestData\DebuggerProject\InfiniteThreads.py") + "\"");
                System.Threading.Thread.Sleep(1000);

                for (int i = 0; i < 10; i++) {
                    Console.WriteLine(i);

                    PythonProcess proc;
                    ConnErrorMessages errReason;
                    if ((errReason = PythonProcess.TryAttach(p.Id, out proc)) != ConnErrorMessages.None) {
                        Assert.Fail("Failed to attach {0}", errReason);
                    }

                    proc.Detach();
                }

                p.Kill();
            }
        }

        #endregion

        internal virtual PythonVersion Version {
            get {
                return PythonPaths.Python26;
            }
        }
    }

    public abstract class DebuggerTests3x : DebuggerTests {
        public override string EnumChildrenTestName {
            get {
                return "EnumChildTestV3.py";
            }
        }
        public override string ComplexExceptions {
            get {
                return "ComplexExceptionsV3.py";
            }
        }
    }

    [TestClass]
    public class DebuggerTests30 : DebuggerTests3x {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python30;
            }
        }
    }

    [TestClass]
    public class DebuggerTests31 : DebuggerTests3x {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python31;
            }
        }
    }

    [TestClass]
    public class DebuggerTests32 : DebuggerTests3x {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python32;
            }
        }
    }

    [TestClass]
    public class DebuggerTests27 : DebuggerTests {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python27;
            }
        }
    }

    [TestClass]
    public class DebuggerTests25 : DebuggerTests {
        internal override PythonVersion Version {
            get {
                return PythonPaths.Python25;
            }
        }
    }

    [TestClass]
    public class DebuggerTestsIpy : DebuggerTests {
        internal override PythonVersion Version {
            get {
                return PythonPaths.IronPython27;
            }
        }
    }
}

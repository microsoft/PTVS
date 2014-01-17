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
using System.Text;
using System.Threading;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using TestUtilities;
using TestUtilities.Python;

namespace DebuggerTests {
    [TestClass]
    public class DebuggerTests : BaseDebuggerTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        [TestInitialize]
        public void CheckVersion() {
            if (Version == null) {
                Assert.Inconclusive("Required version of Python is not installed");
            }
        }

        [TestMethod, Priority(0)]
        public void TestThreads() {
            // TODO: Thread creation tests w/ both thread.start_new_thread and threading module.
        }

        private string InfRepr {
            get {
                return Version.Version > PythonLanguageVersion.V25 ? "inf" : "1.#INF";
            }
        }

        #region Enum Children Tests

        [TestMethod, Priority(0)]
        public void EnumChildrenTest() {
            const int lastLine = 41;

            ChildTest(EnumChildrenTestName, lastLine, "s", GetSetChildren(
                new ChildInfo("[0]", "next((v for i, v in enumerate(s) if i == 0))", Version.Version.Is3x() ? "frozenset({2, 3, 4})" : "frozenset([2, 3, 4])")));

            if (GetType() != typeof(DebuggerTestsIpy) && Version.Version.Is2x()) {
                // IronPython unicode repr differs
                // 3.x: http://pytools.codeplex.com/workitem/76
                ChildTest(EnumChildrenTestName, lastLine, "cinst",
                    new ChildInfo("abc", null, "42", "0x2a"),
                    new ChildInfo("uc", null, "u\'привет мир\'"));
            }
            ChildTest(EnumChildrenTestName, lastLine, "c2inst",
                new ChildInfo("abc", null, "42", "0x2a"),
                new ChildInfo("oar", null, "100", "0x64"),
                new ChildInfo("self", null, "myrepr", "myhex"));
            ChildTest(EnumChildrenTestName, lastLine, "c3inst",
                new ChildInfo("_contents", null, "[1, 2]"),
                new ChildInfo("abc", null, "42", "0x2a"),
                new ChildInfo("[0]", null, "1"),
                new ChildInfo("[1]", null, "2"));
            ChildTest(EnumChildrenTestName, lastLine, "l", GetListChildren(
                new ChildInfo("[0]", null, "1"),
                new ChildInfo("[1]", null, "2")));
            ChildTest(EnumChildrenTestName, lastLine, "d1", GetDictChildren(
                new ChildInfo("[42]", null, "100", "0x64")));
            string itemsName = Version.Version == PythonLanguageVersion.V27 ? "viewitems" : "items";
            ChildTest(EnumChildrenTestName, lastLine, "d2", GetDictChildren(
                new ChildInfo("['abc']", null, "'fob'")));
            ChildTest(EnumChildrenTestName, lastLine, "d3", GetDictChildren(
                new ChildInfo("[" + InfRepr + "]", "next((v for i, (k, v) in enumerate(d3." + itemsName + "()) if i == 0))", "{42: 100}")));
            ChildTest(EnumChildrenTestName, lastLine, "i", null);
            ChildTest(EnumChildrenTestName, lastLine, "u1", null);
        }

        private ChildInfo[] GetSetChildren(ChildInfo items) {
            if (this is DebuggerTestsIpy) {
                return new ChildInfo[] { new ChildInfo("Count", null), items };
            }
            return new[] { items };
        }

        private ChildInfo[] GetListChildren(params ChildInfo[] items) {
            if (this is DebuggerTestsIpy) {
                var res = new List<ChildInfo>(items);
                res.Add(new ChildInfo("Count", null));
                res.Add(new ChildInfo("Item", null));
                return res.ToArray();
            }
            return items;
        }

        private ChildInfo[] GetDictChildren(params ChildInfo[] items) {
            var res = new List<ChildInfo>();

            if (Version.Version == PythonLanguageVersion.V27) {
                res.Add(new ChildInfo("viewitems()", null, ""));
            } else {
                res.Add(new ChildInfo("items()", null, ""));
            }

            res.AddRange(items);

            if (this is DebuggerTestsIpy) {
                res.Add(new ChildInfo("Count", null));
                res.Add(new ChildInfo("Item", null));
                res.Add(new ChildInfo("Keys", null));
                res.Add(new ChildInfo("Values", null));
            }

            return res.ToArray();
        }

        [TestMethod, Priority(0)]
        public void EnumChildrenTestPrevFrame() {
            const int breakLine = 2;

            ChildTest("PrevFrame" + EnumChildrenTestName, breakLine, "s", 1, GetSetChildren(
                new ChildInfo("[0]", "next((v for i, v in enumerate(s) if i == 0))", Version.Version.Is3x() ? "frozenset({2, 3, 4})" : "frozenset([2, 3, 4])")));

            if (GetType() != typeof(DebuggerTestsIpy) && Version.Version.Is2x()) {
                // IronPython unicode repr differs
                // 3.x: http://pytools.codeplex.com/workitem/76
                ChildTest("PrevFrame" + EnumChildrenTestName, breakLine, "cinst", 1,
                    new ChildInfo("abc", null, "42", "0x2a"),
                    new ChildInfo("uc", null, "u\'привет мир\'"));
            }
            ChildTest("PrevFrame" + EnumChildrenTestName, breakLine, "c2inst", 1,
                new ChildInfo("abc", null, "42", "0x2a"),
                new ChildInfo("oar", null, "100", "0x64"),
                new ChildInfo("self", null, "myrepr", "myhex"));
            ChildTest("PrevFrame" + EnumChildrenTestName, breakLine, "l", 1, GetListChildren(
                new ChildInfo("[0]", null, "1"),
                new ChildInfo("[1]", null, "2")));
            ChildTest("PrevFrame" + EnumChildrenTestName, breakLine, "d1", 1, GetDictChildren(
                new ChildInfo("[42]", null, "100", "0x64")));
            string itemsName = Version.Version == PythonLanguageVersion.V27 ? "viewitems" : "items";
            ChildTest("PrevFrame" + EnumChildrenTestName, breakLine, "d2", 1, GetDictChildren(
                new ChildInfo("['abc']", null, "'fob'")));
            ChildTest("PrevFrame" + EnumChildrenTestName, breakLine, "d3", 1, GetDictChildren(
                new ChildInfo("[" + InfRepr + "]", "next((v for i, (k, v) in enumerate(d3." + itemsName + "()) if i == 0))", "{42: 100}")));
            ChildTest("PrevFrame" + EnumChildrenTestName, breakLine, "i", 1, null);
            ChildTest("PrevFrame" + EnumChildrenTestName, breakLine, "u1", 1, null);
        }

        [TestMethod, Priority(0)]
        public void GeneratorChildrenTest() {
            var children = new List<ChildInfo> {
                new ChildInfo("gi_frame", null),
                new ChildInfo("gi_running", null),
                new ChildInfo("Results View", "tuple(a)", "Expanding the Results View will run the iterator")
            };

            if (Version.Version >= PythonLanguageVersion.V26) {
                children.Insert(0, new ChildInfo("gi_code", null));
            }

            ChildTest("GeneratorTest.py", 6, "a", 0, children.ToArray());
        }

        public virtual string EnumChildrenTestName {
            get {
                return "EnumChildTest.py";
            }
        }

        private void ChildTest(string filename, int lineNo, string text, params ChildInfo[] children) {
            ChildTest(filename, lineNo, text, 0, children);
        }

        private void ChildTest(string filename, int lineNo, string text, int frame, params ChildInfo[] children) {
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

            try {
                process.Start();

                AssertWaited(brkHit);

                var frames = thread.Frames;

                AutoResetEvent evalComplete = new AutoResetEvent(false);
                PythonEvaluationResult evalRes = null;
                Console.WriteLine("Executing {0}", text);
                frames[frame].ExecuteText(text, (completion) => {
                    evalRes = completion;
                    evalComplete.Set();
                });

                AssertWaited(evalComplete);
                Assert.IsNotNull(evalRes, "didn't get evaluation result");


                if (children == null) {
                    Assert.IsFalse(evalRes.IsExpandable, "result should not be expandable");
                    Assert.IsNull(evalRes.GetChildren(Int32.MaxValue), "result should not have children");
                } else {
                    Assert.IsTrue(evalRes.IsExpandable, "result is not expandable");
                    var childrenReceived = new List<PythonEvaluationResult>(evalRes.GetChildren(Int32.MaxValue));

                    Console.WriteLine("{0} children received:", childrenReceived.Count);
                    foreach (var childReceived in childrenReceived) {
                        Console.WriteLine("\t{0}\t{1}\t{2}\t{3}", childReceived.ChildName, childReceived.Expression, childReceived.StringRepr, childReceived.HexRepr);
                    }

                    Assert.AreEqual(children.Length, childrenReceived.Count, "received incorrect number of children");

                    for (int i = 0; i < children.Length; i++) {
                        var curChild = children[i];
                        Console.WriteLine("Finding: <{0}> (Repr: <{1}>)", curChild.ChildText, curChild.Repr ?? "(null)");

                        bool foundChild = false;
                        for (int j = 0; j < childrenReceived.Count; j++) {
                            var curReceived = childrenReceived[j];
                            Console.WriteLine("Candidate: <{0}> (Repr: <{1}>)", curReceived.ChildName, curReceived.StringRepr ?? "(null)");
                            if (ChildrenMatch(curChild, curReceived)) {
                                foundChild = true;

                                string expr = curChild.Expression;
                                if (expr == null) {
                                    if (curChild.ChildText.StartsWith("[")) {
                                        expr = text + curChild.ChildText;
                                    } else {
                                        expr = text + "." + curChild.ChildText;
                                    }
                                }

                                Assert.AreEqual(expr, curReceived.Expression);
                                Assert.AreEqual(frames[frame], curReceived.Frame);
                                childrenReceived.RemoveAt(j);
                                break;
                            }
                        }

                        Assert.IsTrue(foundChild, "failed to find " + curChild.ChildText + " found " + String.Join(", ", childrenReceived.Select(x => x.ChildName)));
                    }

                    Assert.AreEqual(0, childrenReceived.Count, "there's still some children left over which we didn't find");
                }
            } finally {
                process.Continue();
                WaitForExit(process);
            }
        }

        private bool ChildrenMatch(ChildInfo curChild, PythonEvaluationResult curReceived) {
            return curReceived.ChildName == curChild.ChildText &&
                (curReceived.StringRepr == curChild.Repr || curChild.Repr == null) &&
                (Version.Version.Is3x() || (curChild.HexRepr == null || curChild.HexRepr == curReceived.HexRepr));// __hex__ no longer used in 3.x, http://mail.python.org/pipermail/python-list/2009-September/1218287.html
        }

        class ChildInfo {
            public readonly string ChildText;
            public readonly string Expression; // if null, compute automatically from parent expression and ChildText
            public readonly string Repr;
            public readonly string HexRepr;

            public ChildInfo(string childText, string expression, string value = null, string hexRepr = null) {
                ChildText = childText;
                Expression = expression;
                Repr = value;
                HexRepr = hexRepr;
            }
        }

        #endregion

        #region Set Next Line Tests

        [TestMethod, Priority(0)]
        public void SetNextLineTest() {
            if (GetType() == typeof(DebuggerTestsIpy)) {
                //http://ironpython.codeplex.com/workitem/30129
                return;
            }

            var debugger = new PythonDebugger();
            PythonThread thread = null;
            AutoResetEvent processLoaded = new AutoResetEvent(false);
            var process =
                DebugProcess(
                    debugger,
                    Path.Combine(DebuggerTestPath, "SetNextLine.py"),
                    resumeOnProcessLoaded: false,
                    onLoaded: (newproc, newthread) => {
                        thread = newthread;
                        processLoaded.Set();
                    }
                );

            AutoResetEvent brkHit = new AutoResetEvent(false);
            AutoResetEvent stepDone = new AutoResetEvent(false);
            process.BreakpointHit += (sender, args) => {
                brkHit.Set();
            };
            process.StepComplete += (sender, args) => {
                stepDone.Set();
            };

            try {
                process.Start();

                AssertWaited(processLoaded);

                var moduleFrame = thread.Frames[0];
                Assert.AreEqual(1, moduleFrame.StartLine);
                if (GetType() != typeof(DebuggerTestsIpy)) {
                    Assert.AreEqual(13, moduleFrame.EndLine);
                }

                // skip over def f()
                Assert.IsTrue(moduleFrame.SetLineNumber(6), "failed to set line number to 6");

                // set break point in g, run until we hit it.
                var newBp = process.AddBreakPoint("SetNextLine.py", 7);
                newBp.Add();

                process.Resume();
                AssertWaited(brkHit);

                thread.StepOver(); // step over x = 42
                AssertWaited(stepDone);

                // skip y = 100
                Assert.IsTrue(moduleFrame.SetLineNumber(9), "failed to set line number to 9");

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
            } finally {
                WaitForExit(process);
            }
        }

        #endregion

        #region BreakAll Tests


        [TestMethod, Priority(0)]
        public void TestBreakAll() {
            var debugger = new PythonDebugger();

            PythonThread thread = null;
            AutoResetEvent loaded = new AutoResetEvent(false);
            var process = DebugProcess(debugger, DebuggerTestPath + "BreakAllTest.py", (newproc, newthread) => {
                loaded.Set();
                thread = newthread;
            });

            try {
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

                Assert.AreEqual(thread, breakThread);

                process.Resume();
            } finally {
                TerminateProcess(process);
            }
        }

        [TestMethod, Priority(0)]
        public void TestBreakAllThreads() {
            var debugger = new PythonDebugger();

            PythonThread thread = null;
            AutoResetEvent loaded = new AutoResetEvent(false);
            var process = DebugProcess(debugger, DebuggerTestPath + "InfiniteThreads.py", (newproc, newthread) => {
                loaded.Set();
                thread = newthread;
            });

            try {
                process.Start();
                AssertWaited(loaded);

                AutoResetEvent breakComplete = new AutoResetEvent(false);
                process.AsyncBreakComplete += (sender, args) => {
                    breakComplete.Set();
                };

                // let loop run
                for (int i = 0; i < 20; i++) {
                    Thread.Sleep(50);

                    Debug.WriteLine(String.Format("Breaking {0}", i));
                    process.Break();
                    if (!breakComplete.WaitOne(10000)) {
                        Console.WriteLine("Failed to break");
                    }
                    process.Resume();
                    Debug.WriteLine(String.Format("Resumed {0}", i));
                }
            } finally {
                TerminateProcess(process);
            }
        }

        #endregion

        #region Eval Tests

        [TestMethod, Priority(0)]
        public void EvalTest() {
            EvalTest("LocalsTest4.py", 2, "g", 1, EvalResult.Value("baz", "int", "42"));
            EvalTest("LocalsTest3.py", 2, "f", 0, EvalResult.Value("x", "int", "42"));
            EvalTest("LocalsTest3.py", 2, "f", 0, EvalResult.Exception("not_defined", "name 'not_defined' is not defined"));
            EvalTest("LocalsTest3.py", 2, "f", 0, EvalResult.ErrorExpression("/2", "unexpected token '/'\r\ninvalid syntax\r\n"));
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
                    Assert.AreEqual(ExceptionText, result.ExceptionText);
                } else {
                    Assert.AreEqual(_typeName, result.TypeName);
                    Assert.AreEqual(_repr, result.StringRepr);
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

            try {
                process.Start();
                AssertWaited(brkHit);

                var frames = thread.Frames;

                PythonEvaluationResult obj = null;
                string errorMsg;
                if (eval.IsError) {
                    Assert.IsFalse(frames[frameIndex].TryParseText(eval.Expression, out errorMsg), "should not have been able to parse expression");
                    Assert.AreEqual(eval.ExceptionText, errorMsg);
                } else {
                    Assert.IsTrue(frames[frameIndex].TryParseText(eval.Expression, out errorMsg), "failed to parse expression");
                    Assert.IsNull(errorMsg);

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
            } finally {
                WaitForExit(process);
            }
        }


        #endregion

        #region Local Tests

        /// <summary>
        /// Verify it takes more than just an items() method for us to treat something like a dictionary.
        /// </summary>
        [TestMethod, Priority(0)]
        public void CloseToDictExpansionBug484() {
            PythonThread thread = RunAndBreak("LocalsTestBug484.py", 7);
            var process = thread.Process;
            try {
                var frames = thread.Frames;

                var obj = frames[0].Locals.First(x => x.Expression == "x");
                var children = obj.GetChildren(2000);
                int extraCount = 0;
                if (this is DebuggerTestsIpy) {
                    extraCount += 2;
                }
                Assert.AreEqual(extraCount + 3, children.Length);
                Assert.AreEqual("2", children[0 + extraCount].StringRepr);
                Assert.AreEqual("3", children[1 + extraCount].StringRepr);
                Assert.AreEqual("4", children[2 + extraCount].StringRepr);

                process.Continue();
            } finally {
                WaitForExit(process);
            }
        }

        [TestMethod, Priority(0)]
        public void LocalsTest() {
            LocalsTest("LocalsTest.py", 3, new string[] { }, new string[] { "x" });

            LocalsTest("LocalsTest2.py", 2, new string[] { "x" }, new string[] { });

            LocalsTest("LocalsTest3.py", 2, new string[] { "x" }, new string[] { "y" });
        }

        /// <summary>
        /// https://pytools.codeplex.com/workitem/1347
        /// </summary>
        [TestMethod, Priority(0)]
        public void LocalGlobalsTest() {
            LocalsTest("LocalGlobalsTest.py", 3, new string[] { }, new string[] { });
            LocalsTest("LocalGlobalsTest.py", 4, new string[] { }, new string[] { "x" });

            LocalsTest("LocalGlobalsTest.py", 5, new string[] { "self" }, new string[] { }, "LocalGlobalsTestImported.py");
            LocalsTest("LocalGlobalsTest.py", 6, new string[] { "self" }, new string[] { "x" }, "LocalGlobalsTestImported.py");
        }

        /// <summary>
        /// https://pytools.codeplex.com/workitem/1348
        /// </summary>
        [TestMethod, Priority(0)]
        public void LocalClosureVarsTest() {
            // IronPython doesn't expose closure variables in frame.f_locals
            if (GetType() == typeof(DebuggerTestsIpy)) {
                LocalsTest("LocalClosureVarsTest.py", 4, new string[] { "z" }, new string[] { });
                LocalsTest("LocalClosureVarsTest.py", 6, new string[] { "z" }, new string[] { }, "LocalClosureVarsTestImported.py");
                return;
            }

            LocalsTest("LocalClosureVarsTest.py", 4, new string[] { "z" }, new string[] { "x", "y" });
            LocalsTest("LocalClosureVarsTest.py", 6, new string[] { "z" }, new string[] { "x", "y" }, "LocalClosureVarsTestImported.py");
        }

        /// <summary>
        /// https://pytools.codeplex.com/workitem/1710
        /// </summary>
        [TestMethod, Priority(0)]
        public void LocalBuiltinUsageTest() {
            // IronPython exposes some builtin elements in co_names not in __builtins__
            if (GetType() == typeof(DebuggerTestsIpy)) {
                LocalsTest("LocalBuiltinUsageTest.py", 4, new string[] { "start", "end" }, new string[] { "i", "foreach_enumerator" });
                LocalsTest("LocalBuiltinUsageTest.py", 6, new string[] { "self", "start", "end" }, new string[] { "i", "foreach_enumerator" }, "LocalBuiltinUsageTestImported.py");
                return;
            }
            LocalsTest("LocalBuiltinUsageTest.py", 4, new string[] { "start", "end" }, new string[] { "i" });
            LocalsTest("LocalBuiltinUsageTest.py", 6, new string[] { "self", "start", "end" }, new string[] { "i" }, "LocalBuiltinUsageTestImported.py");
        }

        [TestMethod, Priority(0)]
        public void GlobalsTest() {
            if (Version.Version >= PythonLanguageVersion.V34) {
                LocalsTest("GlobalsTest.py", 4, new string[] { }, new[] { "x", "y", "__file__", "__name__", "__package__", "__builtins__", "__doc__", "__cached__", "__loader__", "__spec__" });
            }  else if (Version.Version >= PythonLanguageVersion.V33) {
                LocalsTest("GlobalsTest.py", 4, new string[] { }, new[] { "x", "y", "__file__", "__name__", "__package__", "__builtins__", "__doc__", "__cached__", "__loader__" });
            } else if (Version.Version >= PythonLanguageVersion.V32) {
                LocalsTest("GlobalsTest.py", 4, new string[] { }, new[] { "x", "y", "__file__", "__name__", "__package__", "__builtins__", "__doc__", "__cached__" });
            } else if (Version.Version >= PythonLanguageVersion.V26) {
                LocalsTest("GlobalsTest.py", 4, new string[] { }, new[] { "x", "y", "__file__", "__name__", "__package__", "__builtins__", "__doc__" });
            } else {
                LocalsTest("GlobalsTest.py", 4, new string[] { }, new[] { "x", "y", "__file__", "__name__", "__builtins__", "__doc__" });
            }
        }

        [TestMethod, Priority(0)]
        public void LocalBooleanTest() {
            // https://pytools.codeplex.com/workitem/1334
            var filename = DebuggerTestPath + "LocalBooleanTest.py";
            var debugger = new PythonDebugger();

            PythonThread thread = null;
            AutoResetEvent loaded = new AutoResetEvent(false);
            var process =
                DebugProcess(
                    debugger,
                    filename,
                    (newproc, newthread) => {
                        var bp = newproc.AddBreakPoint(filename, 2);
                        bp.Add();
                        thread = newthread;
                        loaded.Set();
                    }
                );

            AutoResetEvent breakpointHit = new AutoResetEvent(false);
            process.BreakpointHit += (sender, args) => {
                breakpointHit.Set();
            };

            process.Start();
            try {
                AssertWaited(breakpointHit);

                // Null hex representation flags AD7 to substitute string representation
                var parms = thread.Frames[0].Parameters;
                Assert.IsNull(parms[0].HexRepr);
                Assert.IsNull(parms[1].HexRepr);

                // Handle order inconsitencies accross interpreters
                foreach (var parm in parms) {
                    if (parm.Expression == "x") {
                        Assert.AreEqual("True", parm.StringRepr);
                    } else {
                        Assert.AreEqual("False", parm.StringRepr);
                    }
                }
            } finally {
                TerminateProcess(process);
            }
        }

        [TestMethod, Priority(0)]
        public void LocalReprRestrictionsTest() {
            // https://pytools.codeplex.com/workitem/931
            var filename = DebuggerTestPath + "LocalReprRestrictionsTest.py";
            var debugger = new PythonDebugger();

            PythonThread thread = null;
            AutoResetEvent loaded = new AutoResetEvent(false);
            var process =
                DebugProcess(
                    debugger,
                    filename,
                    (newproc, newthread) => {
                        var bp = newproc.AddBreakPoint(filename, 22);
                        bp.Add();
                        thread = newthread;
                        loaded.Set();
                    }
                );

            AutoResetEvent breakpointHit = new AutoResetEvent(false);
            process.BreakpointHit += (sender, args) => {
                breakpointHit.Set();
            };

            process.Start();
            try {
                AssertWaited(breakpointHit);

                // Handle order inconsitencies accross interpreters
                var parms = thread.Frames[0].Parameters;
                foreach (var local in thread.Frames[0].Locals) {
                    switch (local.Expression) {
                        case "s":
                            Assert.AreEqual("'01234567890123456789012345678901234567890123456789'", local.StringRepr);
                            break;
                        case "sa1":
                            Assert.AreEqual("['0123456789012345678...123456789']", local.StringRepr);
                            break;
                        case "sa2":
                            Assert.AreEqual("[['0123456789012345678...123456789'], ['0123456789012345678...123456789']]", local.StringRepr);
                            break;
                        case "sa3":
                            Assert.AreEqual("[[[...], [...]], [[...], [...]], [[...], [...]]]", local.StringRepr);
                            break;
                        case "sa4":
                            Assert.AreEqual("[[[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], ...]", local.StringRepr);
                            break;
                        case "n":
                            Assert.AreEqual(
                                Version.Version < PythonLanguageVersion.V30 ?
                                "12345678901234567890123456789012345678901234567890L" :
                                "12345678901234567890123456789012345678901234567890",
                                local.StringRepr);
                            break;
                        case "na1":
                            Assert.AreEqual(
                                Version.Version < PythonLanguageVersion.V30 ?
                                "[12345678901234567890...234567890L]" :
                                "[12345678901234567890...1234567890]",
                                local.StringRepr);
                            break;
                        case "na2":
                            Assert.AreEqual(
                                Version.Version < PythonLanguageVersion.V30 ?
                                "[[12345678901234567890...234567890L], [12345678901234567890...234567890L]]" :
                                "[[12345678901234567890...1234567890], [12345678901234567890...1234567890]]",
                                local.StringRepr);
                            break;
                        case "na3":
                            Assert.AreEqual("[[[...], [...]], [[...], [...]], [[...], [...]]]", local.StringRepr);
                            break;
                        case "na4":
                            Assert.AreEqual("[[[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], ...]", local.StringRepr);
                            break;
                        case "c":
                            Assert.AreEqual("my_class: 0123456789012345678901234567890123456789", local.StringRepr);
                            break;
                        case "ca1":
                            Assert.AreEqual("[my_class: 0123456789...0123456789]", local.StringRepr);
                            break;
                        case "ca2":
                            Assert.AreEqual("[[my_class: 0123456789...0123456789], [my_class: 0123456789...0123456789]]", local.StringRepr);
                            break;
                        case "ca3":
                            Assert.AreEqual("[[[...], [...]], [[...], [...]], [[...], [...]]]", local.StringRepr);
                            break;
                        case "ca4":
                            Assert.AreEqual("[[[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], [[...], [...], [...]], ...]", local.StringRepr);
                            break;
                        case "da1":
                            Assert.AreEqual("{'0123456789012345678...123456789': '0123456789012345678...123456789'}", local.StringRepr);
                            break;
                        case "da2":
                            Assert.AreEqual("{'0123456789012345678...123456789': {'0123456789012345678...123456789': '0123456789012345678...123456789'}, '1': {'0123456789012345678...123456789': '0123456789012345678...123456789'}}", local.StringRepr);
                            break;
                        case "da3":
                            Assert.AreEqual("{'0123456789012345678...123456789': {'0123456789012345678...123456789': {...}, '1': {...}}, '1': {'0123456789012345678...123456789': {...}, '1': {...}}, '2': {'0123456789012345678...123456789': {...}, '1': {...}}}", local.StringRepr);
                            break;
                        case "da4":
                            Assert.AreEqual("{'01': {'0123456789012345678...123456789': {...}, '1': {...}, '2': {...}}, '0123456789012345678...123456789': {'0123456789012345678...123456789': {...}, '1': {...}, '2': {...}}, '02': {'0123456789012345678...123456789': {...}, '1': {...}, '2': {...}}, '03': {'0123456789012345678...123456789': {...}, '1': {...}, '2': {...}}, '04': {'0123456789012345678...123456789': {...}, '1': {...}, '2': {...}}, '05': {'0123456789012345678...123456789': {...}, '1': {...}, '2': {...}}, '06': {'0123456789012345678...123456789': {...}, '1': {...}, '2': {...}}, '07': {'0123456789012345678...123456789': {...}, '1': {...}, '2': {...}}, '08': {'0123456789012345678...123456789': {...}, '1': {...}, '2': {...}}, '09': {'0123456789012345678...123456789': {...}, '1': {...}, '2': {...}}, '10': {'0123456789012345678...123456789': {...}, '1': {...}, '2': {...}}, '11': {'0123456789012345678...123456789': {...}, '1': {...}, '2': {...}}, '12': {'0123456789012345678...123456789': {...}, '1': {...}, '2': {...}}, '13': {'0123456789012345678...123456789': {...}, '1': {...}, '2': {...}}, ...}", local.StringRepr);
                            break;
                        default:
                            break;
                    }
                }
            } finally {
                TerminateProcess(process);
            }
        }

        #endregion

        #region Stepping Tests

        [TestMethod, Priority(0)]
        public void StepTest() {
            // Bug 1315: https://pytools.codeplex.com/workitem/1315
            StepTest(Path.Combine(DebuggerTestPath, "StepIntoThroughStdLib.py"),
                    new ExpectedStep(StepKind.Over, 1),     // step over import os
                    new ExpectedStep(StepKind.Over, 2),     // step over code =  ...
                    new ExpectedStep(StepKind.Over, 3),     // step over d = {}
                    new ExpectedStep(StepKind.Over, 4),     // step over exec(code, d, d)
                    new ExpectedStep(StepKind.Over, 6),     // step over def myfunc():
                    new ExpectedStep(StepKind.Into, 9),     // step d['f'](myfunc)
                    new ExpectedStep(StepKind.Over, 7),     // step over print('abc')
                    new ExpectedStep(StepKind.Resume, 10)     // wait for exit
                );
            // 1315 resurrected:
            StepTest(Path.Combine(DebuggerTestPath, "SteppingTestBug1315.py"),
                    new[] { 9 },
                    null,
                    new ExpectedStep(StepKind.Resume, 1),   // continue from import thread
                    new ExpectedStep(StepKind.Over, 9),
                    new ExpectedStep(StepKind.Resume, 10)
                );

            // Bug 507: http://pytools.codeplex.com/workitem/507
            StepTest(Path.Combine(DebuggerTestPath, "SteppingTestBug507.py"),
                    new ExpectedStep(StepKind.Over, 1),     // step over def add_two_numbers(x, y):
                    new ExpectedStep(StepKind.Over, 4),     // step over class Z(object):
                    new ExpectedStep(StepKind.Over, 9),     // step over p = Z()
                    new ExpectedStep(StepKind.Into, 10),     // step into print add_two_numbers(p.fob, 3)
                    new ExpectedStep(StepKind.Out, 7),     // step out return 7
                    new ExpectedStep(StepKind.Into, 10),     // step into add_two_numbers(p.fob, 3)
                    new ExpectedStep(StepKind.Resume, 2)     // wait for exit after return x + y
                );

            // Bug 508: http://pytools.codeplex.com/workitem/508
            StepTest(Path.Combine(DebuggerTestPath, "SteppingTestBug508.py"),
                    new ExpectedStep(StepKind.Into, 1),     // step print (should step over)
                    new ExpectedStep(StepKind.Resume, 2)     // step print (should step over)
                );

            // Bug 509: http://pytools.codeplex.com/workitem/509
            StepTest(Path.Combine(DebuggerTestPath, "SteppingTestBug509.py"),
                    new ExpectedStep(StepKind.Over, 1),     // step over def triangular_number
                    new ExpectedStep(StepKind.Into, 3),     // step into triangular_number
                    new ExpectedStep(StepKind.Into, 1),     // step over triangular_number
                    new ExpectedStep(StepKind.Into, 1),     // step into triangular_number
                    new ExpectedStep(StepKind.Into, 1),     // step into triangular_number
                    new ExpectedStep(StepKind.Resume, 1)    // let program exit
                );

            // Bug 503: http://pytools.codeplex.com/workitem/503
            StepTest(Path.Combine(DebuggerTestPath, "SteppingTestBug503.py"),
                new[] { 6, 12 },
                new Action<PythonProcess>[] { 
                    (x) => {},
                    (x) => {},
                },
                new ExpectedStep(StepKind.Resume, 1),     // continue from def x1(y):
                new ExpectedStep(StepKind.Out, 6),     // step out after hitting breakpoint at return y
                new ExpectedStep(StepKind.Out, 3),     // step out z += 1
                new ExpectedStep(StepKind.Out, 3),     // step out z += 1
                new ExpectedStep(StepKind.Out, 3),     // step out z += 1
                new ExpectedStep(StepKind.Out, 3),     // step out z += 1
                new ExpectedStep(StepKind.Out, 3),     // step out z += 1

                new ExpectedStep(StepKind.Out, 14),     // step out after stepping out to x2(5)
                new ExpectedStep(StepKind.Out, 12),     // step out after hitting breakpoint at return y
                new ExpectedStep(StepKind.Out, 10),     // step out return z + 3
                new ExpectedStep(StepKind.Out, 10),     // step out return z + 3
                new ExpectedStep(StepKind.Out, 10),     // step out return z + 3
                new ExpectedStep(StepKind.Out, 10),     // step out return z + 3
                new ExpectedStep(StepKind.Out, 10),     // step out return z + 3

                new ExpectedStep(StepKind.Resume, 15)     // let the program exit
            );

            if (Version.Version < PythonLanguageVersion.V30) {  // step into print on 3.x runs more Python code
                StepTest(Path.Combine(DebuggerTestPath, "SteppingTest7.py"),
                    new ExpectedStep(StepKind.Over, 1),     // step over def f():
                    new ExpectedStep(StepKind.Over, 6),     // step over def g():
                    new ExpectedStep(StepKind.Over, 10),     // step over def h()
                    new ExpectedStep(StepKind.Into, 13),     // step into f() call
                    new ExpectedStep(StepKind.Into, 2),     // step into print 'abc'
                    new ExpectedStep(StepKind.Into, 3),     // step into print 'def'
                    new ExpectedStep(StepKind.Into, 4),     // step into print 'baz'
                    new ExpectedStep(StepKind.Into, 13),     // step into g()
                    new ExpectedStep(StepKind.Into, 14),     // step into g()
                    new ExpectedStep(StepKind.Into, 7),     // step into dict assign
                    new ExpectedStep(StepKind.Into, 8),     // step into print 'hello'
                    new ExpectedStep(StepKind.Into, 14),     // step into h()
                    new ExpectedStep(StepKind.Into, 15),     // step into h()
                    new ExpectedStep(StepKind.Into, 11),    // step into h() return
                    new ExpectedStep(StepKind.Resume, 15)    // step into h() return
                );
            }

            StepTest(Path.Combine(DebuggerTestPath, "SteppingTest6.py"),
                new ExpectedStep(StepKind.Over, 1),     // step over print 'hello world'
                new ExpectedStep(StepKind.Over, 2),     // step over a = set([i for i in range(256)])
                new ExpectedStep(StepKind.Over, 3),     // step over print a
                new ExpectedStep(StepKind.Resume, 4)    // let the program exit
            );

            StepTest(Path.Combine(DebuggerTestPath, "SteppingTest5.py"),
                new ExpectedStep(StepKind.Over, 1),     // step over def g():...
                new ExpectedStep(StepKind.Over, 4),     // step over def f():...
                new ExpectedStep(StepKind.Into, 8),     // step into f()
                new ExpectedStep(StepKind.Out, 5),      // step out of f() on line "g()"
                new ExpectedStep(StepKind.Resume, 8)    // let the program exit
            );

            StepTest(Path.Combine(DebuggerTestPath, "SteppingTest4.py"),
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

            StepTest(Path.Combine(DebuggerTestPath, "SteppingTest3.py"),
                new ExpectedStep(StepKind.Over, 1),     // step over def f():...
                new ExpectedStep(StepKind.Over, 5),     // step over f()
                new ExpectedStep(StepKind.Resume, 6)    // let the program exit
            );

            StepTest(Path.Combine(DebuggerTestPath, "SteppingTest3.py"),
                new ExpectedStep(StepKind.Over, 1),     // step over def f():...
                new ExpectedStep(StepKind.Into, 5),     // step into f()
                new ExpectedStep(StepKind.Out, 2),     // step out of f()
                new ExpectedStep(StepKind.Resume, 5)    // let the program exit
            );

            StepTest(Path.Combine(DebuggerTestPath, "SteppingTest2.py"),
                new ExpectedStep(StepKind.Over, 1),     // step over def f():...
                new ExpectedStep(StepKind.Into, 4),     // step into f()
                new ExpectedStep(StepKind.Over, 2),     // step over print 'hi'
                new ExpectedStep(StepKind.Resume, 4)    // let the program exit
            );

            StepTest(Path.Combine(DebuggerTestPath, "SteppingTest2.py"),
                new ExpectedStep(StepKind.Over, 1),     // step over def f():...
                new ExpectedStep(StepKind.Into, 4),     // step into f()
                new ExpectedStep(StepKind.Over, 2),     // step over print 'hi'
                new ExpectedStep(StepKind.Resume, 4)      // let the program exit
            );

            StepTest(Path.Combine(DebuggerTestPath, "SteppingTest.py"),
                new ExpectedStep(StepKind.Over, 1),     // step over print "hello"
                new ExpectedStep(StepKind.Over, 2),     // step over print "goodbye"
                new ExpectedStep(StepKind.Resume, 3)   // let the program exit
            );

        }

        [TestMethod, Priority(0)]
        public void StepStdLib() {
            // http://pytools.codeplex.com/workitem/504 - test option for stepping into std lib.
            var debugger = new PythonDebugger();

            string fullPath = Path.Combine(DebuggerTestPath, "StepStdLib.py");
            foreach (var steppingStdLib in new[] { false, true }) {
                var process = debugger.CreateProcess(
                    Version.Version,
                    Version.Path,
                    "\"" + fullPath + "\"",
                    DebuggerTestPath,
                    "",
                    debugOptions: steppingStdLib ? (PythonDebugOptions.DebugStdLib | PythonDebugOptions.RedirectOutput) : PythonDebugOptions.RedirectOutput);

                PythonThread thread = null;
                process.ThreadCreated += (sender, args) => {
                    thread = args.Thread;
                };

                AutoResetEvent processEvent = new AutoResetEvent(false);

                bool processLoad = false, stepComplete = false;
                PythonBreakpoint bp = null;
                process.ProcessLoaded += (sender, args) => {
                    bp = process.AddBreakPoint(fullPath, 2);
                    bp.Add();

                    processLoad = true;
                    processEvent.Set();
                };

                process.StepComplete += (sender, args) => {
                    stepComplete = true;
                    processEvent.Set();
                };

                bool breakHit = false;
                process.BreakpointHit += (sender, args) => {
                    breakHit = true;
                    bp.Disable();
                    processEvent.Set();
                };

                process.Start();
                try {
                    AssertWaited(processEvent);
                    Assert.IsTrue(processLoad, "process did not load");
                    Assert.IsFalse(stepComplete, "step should not have completed");
                    process.Resume();

                    AssertWaited(processEvent);
                    Assert.IsTrue(breakHit, "breakpoint was not hit");

                    thread.StepInto();
                    AssertWaited(processEvent);
                    Assert.IsTrue(stepComplete, "step was not completed");

                    Debug.WriteLine(thread.Frames[thread.Frames.Count - 1].FileName);

                    if (steppingStdLib) {
                        Assert.IsTrue(thread.Frames[0].FileName.EndsWith("\\os.py"), "did not break in os.py; instead, " + thread.Frames[0].FileName);
                    } else {
                        Assert.IsTrue(thread.Frames[0].FileName.EndsWith("\\StepStdLib.py"), "did not break in StepStdLib.py; instead, " + thread.Frames[0].FileName);
                    }

                    process.Resume();
                } finally {
                    WaitForExit(process);
                }
            }
        }

        [TestMethod, Priority(0)]
        public void StepToEntryPoint() {
            // https://pytools.codeplex.com/workitem/1344
            var debugger = new PythonDebugger();

            PythonThread thread = null;
            AutoResetEvent loaded = new AutoResetEvent(false);
            var process = DebugProcess(debugger, DebuggerTestPath + "SteppingTest.py", (newproc, newthread) => {
                thread = newthread;
                loaded.Set();
            });

            process.Start();
            try {
                AssertWaited(loaded);
                Assert.IsTrue(thread.Frames[0].FileName.EndsWith("SteppingTest.py"), "did not break in SteppingTest.py; instead, " + thread.Frames[0].FileName);
                Assert.AreEqual(1, thread.Frames[0].StartLine);
            } finally {
                TerminateProcess(process);
            }
        }

        #endregion

        #region Breakpoint Tests

        /// <summary>
        /// Sets 2 breakpoints on one line after another, hits the 1st one, then steps onto
        /// the next one.  Makes sure we only break in once.
        /// </summary>
        [TestMethod, Priority(0)]
        public void BreakStepStep() {
            // http://pytools.codeplex.com/workitem/815

            var debugger = new PythonDebugger();
            string fn = Path.Combine(DebuggerTestPath, "StepBreakBreak.py");
            var process = DebugProcess(debugger, fn, (newproc, newthread) => {
                PythonBreakpoint breakPoint = newproc.AddBreakPointByFileExtension(2, fn);
                breakPoint.Add();

                breakPoint = newproc.AddBreakPointByFileExtension(3, fn);
                breakPoint.Add();
            }, cwd: DebuggerTestPath);

            int hitBp = 0;
            process.BreakpointHit += (sender, args) => {
                ++hitBp;
                args.Thread.StepOver();
            };
            bool sentStep = false;
            process.StepComplete += (sender, args) => {
                if (sentStep) {
                    process.Continue();
                } else {
                    args.Thread.StepOver();
                    sentStep = true;
                }
            };
            StartAndWaitForExit(process);
        }

        [TestMethod, Priority(0)]
        public void BreakpointNonMainFileRemoved() {
            // http://pytools.codeplex.com/workitem/638

            BreakpointTest(
                Path.Combine(DebuggerTestPath, "BreakpointNonMainFileRemoved.py"),
                new[] { 2 },
                new[] { -2 },
                cwd: DebuggerTestPath,
                breakFilename: Path.Combine(DebuggerTestPath, "BreakpointNonMainFileRemovedImported.py"),
                checkBound: false,
                checkThread: false);
        }


        [TestMethod, Priority(0)]
        public void BreakpointNonMainThreadMainThreadExited() {
            // http://pytools.codeplex.com/workitem/638

            BreakpointTest(
                Path.Combine(DebuggerTestPath, "BreakpointMainThreadExited.py"),
                new[] { 8 },
                new[] { 8, 8, 8, 8, 8 },
                cwd: DebuggerTestPath,
                breakFilename: Path.Combine(DebuggerTestPath, "BreakpointMainThreadExited.py"),
                checkBound: false,
                checkThread: false);
        }

        [TestMethod, Priority(0)]
        public void TestBreakpointsCollidingFilenames() {
            // http://pytools.codeplex.com/workitem/565

            BreakpointTest(
                Path.Combine(DebuggerTestPath, "BreakpointFilenames.py"),
                new[] { 4 },
                new int[0],
                cwd: DebuggerTestPath,
                breakFilename: Path.Combine(DebuggerTestPath, "B", "module1.py"),
                checkBound: false);
        }

        [TestMethod, Priority(0)]
        public void TestBreakpointsRelativePathTopLevel() {
            // http://pytools.codeplex.com/workitem/522

            BreakpointTest(
                Path.Combine(DebuggerTestPath, "SimpleFilenameBreakpoint.py"),
                new[] { 4, 10 },
                new[] { 4, 10 },
                cwd: Path.GetDirectoryName(DebuggerTestPath),
                breakFilename: Path.Combine(DebuggerTestPath, "CompiledCodeFile.py"),
                checkBound: false);
        }

        [TestMethod, Priority(0)]
        public void TestBreakpointsRelativePathInPackage() {
            // http://pytools.codeplex.com/workitem/522

            var xFrames = (Version == PythonPaths.IronPython27 || Version == PythonPaths.IronPython27_x64) ?
                "-X:Frames" :
                "";

            BreakpointTest(
                Path.Combine(DebuggerTestPath, "BreakpointRelativePathInPackage.py"),
                new[] { 6 },
                new[] { 6, 6 },
                cwd: Path.GetDirectoryName(DebuggerTestPath),
                breakFilename: Path.Combine(DebuggerTestPath, "A", "relpath.py"),
                checkBound: false,
                interpreterOptions: xFrames
            );
        }

        [TestMethod, Priority(0)]
        public void TestBreakpointHitOtherThreadStackTrace() {
            // http://pytools.codeplex.com/workitem/483

            var debugger = new PythonDebugger();
            string filename = Path.Combine(DebuggerTestPath, "ThreadJoin.py");
            PythonThread thread = null;
            var process = DebugProcess(debugger, filename, (newproc, newthread) => {
                thread = newthread;
                var bp = newproc.AddBreakPoint(filename, 5);
                bp.Add();
            },
                debugOptions: PythonDebugOptions.WaitOnAbnormalExit | PythonDebugOptions.WaitOnNormalExit
            );

            AutoResetEvent bpHit = new AutoResetEvent(false);

            process.BreakpointHit += (sender, args) => {
                Assert.AreNotEqual(args.Thread, thread, "breakpoint shouldn't be on main thread");

                foreach (var frame in thread.Frames) {
                    Console.WriteLine(frame.FileName);
                    Console.WriteLine(frame.LineNo);
                }
                Assert.IsTrue(thread.Frames.Count > 1, "expected more than one frame");
                process.Continue();
                bpHit.Set();
            };

            process.Start();

            try {
                if (!bpHit.WaitOne(10000)) {
                    Assert.Fail("Failed to hit breakpoint");
                }
            } finally {
                TerminateProcess(process);
            }
        }

        [TestMethod, Priority(0)]
        public void TestBreakpoints() {
            BreakpointTest("BreakpointTest.py", new[] { 1 }, new[] { 1 });
        }

        [TestMethod, Priority(0)]
        public void TestBreakpoints2() {
            BreakpointTest("BreakpointTest2.py", new[] { 3 }, new[] { 3, 3, 3 });
        }

        [TestMethod, Priority(0)]
        public void TestBreakpoints3() {
            BreakpointTest("BreakpointTest3.py", new[] { 1 }, new[] { 1 });
        }

        [TestMethod, Priority(0)]
        public void TestBreakpointsConditional() {
            BreakpointTest("BreakpointTest2.py", new[] { 3 }, new[] { 3 }, new[] { "i == 1" });
        }

        [TestMethod, Priority(0)]
        public void TestBreakpointsConditionalOnChange() {
            BreakpointTest("BreakpointTest5.py", new[] { 4 }, new[] { 4, 4, 4, 4, 4 }, new[] { "j" }, new[] { true });
        }

        [TestMethod, Priority(0)]
        public void TestBreakpointRemove() {
            BreakpointTest("BreakpointTest2.py", new[] { 3 }, new[] { -3 });
        }

        [TestMethod, Priority(0)]
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
                Assert.AreEqual(breakPoint, args.Breakpoint);
            };

            StartAndWaitForExit(process);

            Assert.IsTrue(bindFailed, "Should not have bound the breakpoint");
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

        class ExceptionHandlerInfo {
            public readonly int FirstLine;
            public readonly int LastLine;
            public readonly HashSet<string> Expressions;

            public ExceptionHandlerInfo(int firstLine, int lastLine, params string[] expressions) {
                FirstLine = firstLine;
                LastLine = lastLine;
                Expressions = new HashSet<string>(expressions);
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

        [TestMethod, Priority(0)]
        public void TestExceptions() {
            var debugger = new PythonDebugger();
            for (int i = 0; i < 2; i++) {
                TestException(debugger, Path.Combine(DebuggerTestPath, "SimpleException.py"), i == 0, ExceptionMode.Always, null,
                    new ExceptionInfo(ExceptionModule + ".Exception", 3)
                );

                TestException(debugger, DebuggerTestPath + ComplexExceptions, i == 0, ExceptionMode.Always, null,
                    new ExceptionInfo(PickleModule + ".PickleError", 6),
                    new ExceptionInfo(ExceptionModule + ".StopIteration", 13),
                    new ExceptionInfo(ExceptionModule + ".NameError", 15),
                    new ExceptionInfo(ExceptionModule + ".StopIteration", 21),
                    new ExceptionInfo(ExceptionModule + ".NameError", 23),
                    new ExceptionInfo(ExceptionModule + ".Exception", 29),
                    new ExceptionInfo(ExceptionModule + ".Exception", 32)
                );

                TestException(debugger, DebuggerTestPath + ComplexExceptions, i == 0, ExceptionMode.Unhandled, new[] {
                    new KeyValuePair<string, ExceptionMode>(PickleModule + ".PickleError", ExceptionMode.Never)
                });
                TestException(debugger, DebuggerTestPath + ComplexExceptions, i == 0, ExceptionMode.Never, new[] {
                    new KeyValuePair<string, ExceptionMode>(PickleModule + ".PickleError", ExceptionMode.Always),
                    new KeyValuePair<string, ExceptionMode>(ExceptionModule + ".StopIteration", ExceptionMode.Unhandled),
                    new KeyValuePair<string, ExceptionMode>(ExceptionModule + ".NameError", ExceptionMode.Never),
                    new KeyValuePair<string, ExceptionMode>(ExceptionModule + ".Exception", ExceptionMode.Always | ExceptionMode.Unhandled),
                },
                    new ExceptionInfo(PickleModule + ".PickleError", 6),
                    new ExceptionInfo(ExceptionModule + ".Exception", 29),
                    new ExceptionInfo(ExceptionModule + ".Exception", 32)
                );

                TestException(debugger, DebuggerTestPath + "FinallyExceptions.py", i == 0, ExceptionMode.Unhandled, null,
                    new ExceptionInfo(ExceptionModule + ".Exception", 3)
                );

                if (Version.Version.Is2x()) {
                    TestException(debugger, Path.Combine(DebuggerTestPath, "UnicodeException.py"), i == 0, ExceptionMode.Always, null,
                        new ExceptionInfo(ExceptionModule + ".Exception", 3)
                    );
                }

                // Only the last exception in each file should be noticed.
                if (Version.Version <= PythonLanguageVersion.V25) {
                    TestException(debugger, Path.Combine(DebuggerTestPath, "UnhandledException1_v25.py"), i == 0, ExceptionMode.Unhandled, null,
                        new ExceptionInfo(ExceptionModule + ".Exception", 57)
                    );
                } else if (Version.Version.Is3x()) {
                    TestException(debugger, Path.Combine(DebuggerTestPath, "UnhandledException1_v3x.py"), i == 0, ExceptionMode.Unhandled, null,
                        new ExceptionInfo(ExceptionModule + ".Exception", 56)
                    );
                } else {
                    TestException(debugger, Path.Combine(DebuggerTestPath, "UnhandledException1.py"), i == 0, ExceptionMode.Unhandled, null,
                        new ExceptionInfo(ExceptionModule + ".Exception", 81)
                    );
                }

                TestException(debugger, Path.Combine(DebuggerTestPath, "UnhandledException2.py"), i == 0, ExceptionMode.Unhandled, null,
                    new ExceptionInfo(ExceptionModule + ".Exception", 16)
                );
                TestException(debugger, Path.Combine(DebuggerTestPath, "UnhandledException3.py"), i == 0, ExceptionMode.Unhandled, null,
                    new ExceptionInfo(ExceptionModule + ".ValueError", 12)
                );
                TestException(debugger, Path.Combine(DebuggerTestPath, "UnhandledException4.py"), i == 0, ExceptionMode.Unhandled, null,
                    new ExceptionInfo(ExceptionModule + ".OSError", 17)
                );
                TestException(debugger, Path.Combine(DebuggerTestPath, "UnhandledException5.py"), i == 0, ExceptionMode.Unhandled, null,
                    new ExceptionInfo(ExceptionModule + ".ValueError", 4)
                );
                TestException(debugger, Path.Combine(DebuggerTestPath, "UnhandledException6.py"), i == 0, ExceptionMode.Unhandled, null,
                    new ExceptionInfo(ExceptionModule + ".OSError", 12)
                );
            }
        }

        [Flags]
        private enum ExceptionMode {
            Never = 0,
            Always = 1,
            Unhandled = 32
        }

        private void TestException(
            PythonDebugger debugger,
            string filename,
            bool resumeProcess,
            ExceptionMode defaultExceptionMode,
            ICollection<KeyValuePair<string, ExceptionMode>> exceptionModes,
            params ExceptionInfo[] exceptions
        ) {
            TestException(debugger, filename, resumeProcess, defaultExceptionMode, exceptionModes, PythonDebugOptions.RedirectOutput, exceptions);
        }

        private void TestException(
            PythonDebugger debugger,
            string filename,
            bool resumeProcess,
            ExceptionMode defaultExceptionMode,
            ICollection<KeyValuePair<string, ExceptionMode>> exceptionModes,
            PythonDebugOptions debugOptions,
            params ExceptionInfo[] exceptions
        ) {
            bool loaded = false;
            var process = DebugProcess(debugger, filename, (processObj, threadObj) => {
                loaded = true;
                processObj.SetExceptionInfo(
                    (int)defaultExceptionMode,
                    exceptionModes == null ?
                        Enumerable.Empty<KeyValuePair<string, int>>() :
                        exceptionModes.Select(m => new KeyValuePair<string, int>(m.Key, (int)m.Value))
                );
            }, debugOptions: debugOptions);

            int curException = 0;
            process.ExceptionRaised += (sender, args) => {
                // V30 raises an exception as the process shuts down.
                if (loaded && ((Version.Version == PythonLanguageVersion.V30 && curException < exceptions.Length) || Version.Version != PythonLanguageVersion.V30)) {
                    if (GetType() != typeof(DebuggerTestsIpy) || curException < exceptions.Length) {    // Ipy over reports
                        Assert.AreEqual(exceptions[curException].TypeName, args.Exception.TypeName);
                    }

                    if (GetType() != typeof(DebuggerTestsIpy) || curException < exceptions.Length) {    // Ipy over reports
                        curException++;
                    }
                    if (resumeProcess) {
                        process.Resume();
                    } else {
                        args.Thread.Resume();
                    }
                } else {
                    args.Thread.Resume();
                }
            };

            StartAndWaitForExit(process);

            Assert.AreEqual(exceptions.Length, curException);
        }

        /// <summary>
        /// Test cases for http://pytools.codeplex.com/workitem/367
        /// </summary>
        [TestMethod, Priority(0)]
        public void TestExceptionsSysExitZero() {
            var debugger = new PythonDebugger();

            TestException(
                debugger,
                Path.Combine(DebuggerTestPath, "SysExitZeroRaise.py"),
                true, ExceptionMode.Unhandled,
                null,
                PythonDebugOptions.BreakOnSystemExitZero,
                new ExceptionInfo(ExceptionModule + ".SystemExit", 1)
            );

            TestException(
                debugger,
                Path.Combine(DebuggerTestPath, "SysExitZero.py"),
                true, ExceptionMode.Unhandled,
                null,
                PythonDebugOptions.BreakOnSystemExitZero,
                new ExceptionInfo(ExceptionModule + ".SystemExit", 2)
            );

            TestException(
                debugger,
                Path.Combine(DebuggerTestPath, "SysExitZeroRaise.py"),
                true, ExceptionMode.Unhandled,
                null,
                PythonDebugOptions.RedirectOutput
            );

            TestException(
                debugger,
                Path.Combine(DebuggerTestPath, "SysExitZero.py"),
                true, ExceptionMode.Unhandled,
                null,
                PythonDebugOptions.RedirectOutput
            );
        }

        [TestMethod, Priority(0)]
        public void TestExceptionHandlers() {
            var debugger = new PythonDebugger();

            TestGetHandledExceptionRanges(debugger, Path.Combine(DebuggerTestPath, "ExceptionHandlers.py"),
                new ExceptionHandlerInfo(1, 3, "*"),
                new ExceptionHandlerInfo(6, 7, "*"),
                new ExceptionHandlerInfo(9, 13, "*"),

                new ExceptionHandlerInfo(18, 19, "ArithmeticError", "AssertionError", "AttributeError", "BaseException", "BufferError", "BytesWarning", "DeprecationWarning", "EOFError", "EnvironmentError", "Exception", "FloatingPointError", "FutureWarning", "GeneratorExit", "IOError", "ImportError", "ImportWarning", "IndentationError", "IndexError", "KeyError", "KeyboardInterrupt", "LookupError", "MemoryError", "NameError", "NotImplementedError", "OSError", "OverflowError", "PendingDeprecationWarning", "ReferenceError", "RuntimeError", "RuntimeWarning", "StandardError", "StopIteration", "SyntaxError", "SyntaxWarning", "SystemError", "SystemExit", "TabError", "TypeError", "UnboundLocalError", "UnicodeDecodeError", "UnicodeEncodeError", "UnicodeError", "UnicodeTranslateError", "UnicodeWarning", "UserWarning", "ValueError", "Warning", "WindowsError", "ZeroDivisionError"),
                new ExceptionHandlerInfo(69, 70, "ArithmeticError", "AssertionError", "AttributeError", "BaseException", "BufferError", "BytesWarning", "DeprecationWarning", "EOFError", "EnvironmentError", "Exception", "FloatingPointError", "FutureWarning", "GeneratorExit", "IOError", "ImportError", "ImportWarning", "IndentationError", "IndexError", "KeyError", "KeyboardInterrupt", "LookupError", "MemoryError", "NameError", "NotImplementedError", "OSError", "OverflowError", "PendingDeprecationWarning", "ReferenceError", "RuntimeError", "RuntimeWarning", "StandardError", "StopIteration", "SyntaxError", "SyntaxWarning", "SystemError", "SystemExit", "TabError", "TypeError", "UnboundLocalError", "UnicodeDecodeError", "UnicodeEncodeError", "UnicodeError", "UnicodeTranslateError", "UnicodeWarning", "UserWarning", "ValueError", "Warning", "WindowsError", "ZeroDivisionError"),
                new ExceptionHandlerInfo(72, 73, "*"),

                new ExceptionHandlerInfo(125, 126, "struct.error", "socket.error", "os.error"),
                new ExceptionHandlerInfo(130, 131, "struct.error", "socket.error", "os.error"),

                new ExceptionHandlerInfo(133, 143, "ValueError"),
                new ExceptionHandlerInfo(135, 141, "TypeError"),
                new ExceptionHandlerInfo(137, 139, "ValueError"),

                new ExceptionHandlerInfo(146, 148, "ValueError"),
                new ExceptionHandlerInfo(150, 156, "TypeError"),
                new ExceptionHandlerInfo(152, 154, "ValueError"),

                new ExceptionHandlerInfo(159, 160, "Exception"),
                new ExceptionHandlerInfo(162, 163, "Exception"),
                new ExceptionHandlerInfo(165, 166, "ValueError", "TypeError"),
                new ExceptionHandlerInfo(168, 169, "ValueError", "TypeError"),

                new ExceptionHandlerInfo(171, 172, "is_included", "also.included", "this.one.too.despite.having.lots.of.dots")
            );
        }

        private void TestGetHandledExceptionRanges(PythonDebugger debugger, string filename, params ExceptionHandlerInfo[] expected) {
            var process = DebugProcess(debugger, filename, (processObj, threadObj) => { });

            var actual = process.GetHandledExceptionRanges(filename);
            Assert.AreEqual(expected.Length, actual.Count);

            Assert.IsTrue(actual.All(a =>
                expected.SingleOrDefault(e => e.FirstLine == a.Item1 && e.LastLine == a.Item2 && e.Expressions.ContainsExactly(a.Item3)) != null
            ));
        }

        #endregion

        #region Module Load Tests

        [TestMethod, Priority(0)]
        public void TestModuleLoad() {
            var debugger = new PythonDebugger();

            // main file is reported
            TestModuleLoad(debugger, TestData.GetPath(@"TestData\HelloWorld\Program.py"), "Program.py");

            // imports are reported
            TestModuleLoad(debugger, Path.Combine(DebuggerTestPath, "imports_other.py"), "imports_other.py", "is_imported.py");
        }

        private void TestModuleLoad(PythonDebugger debugger, string filename, params string[] expectedModulesLoaded) {
            var process = DebugProcess(debugger, filename);

            List<string> receivedFilenames = new List<string>();
            process.ModuleLoaded += (sender, args) => {
                receivedFilenames.Add(args.Module.Filename);
            };

            StartAndWaitForExit(process);

            Assert.IsTrue(receivedFilenames.Count >= expectedModulesLoaded.Length, "did not receive enough module names");
            var set = new HashSet<string>();
            foreach (var received in receivedFilenames) {
                set.Add(Path.GetFileName(received));
            }

            AssertUtil.ContainsAtLeast(set, expectedModulesLoaded);
        }

        #endregion

        #region Exit Code Tests

        [TestMethod, Priority(0)]
        public void TestStartup() {
            var debugger = new PythonDebugger();

            // hello world
            TestExitCode(debugger, TestData.GetPath(@"TestData\HelloWorld\Program.py"), 0);

            // test which calls sys.exit(23)
            TestExitCode(debugger, Path.Combine(DebuggerTestPath, "SysExit.py"), 23);

            // test which calls raise Exception()
            TestExitCode(debugger, Path.Combine(DebuggerTestPath, "ExceptionalExit.py"), 1);
        }

        [TestMethod, Priority(0)]
        public void TestWindowsStartup() {
            var debugger = new PythonDebugger();

            string pythonwExe = Path.Combine(Path.GetDirectoryName(Version.Path), "pythonw.exe");
            if (!File.Exists(pythonwExe)) {
                pythonwExe = Path.Combine(Path.GetDirectoryName(Version.Path), "ipyw.exe");
            }

            if (File.Exists(pythonwExe)) {
                // hello world
                TestExitCode(debugger, TestData.GetPath(@"TestData\HelloWorld\Program.py"), 0, pythonExe: pythonwExe);

                // test which calls sys.exit(23)
                TestExitCode(debugger, Path.Combine(DebuggerTestPath, "SysExit.py"), 23, pythonExe: pythonwExe);

                // test which calls raise Exception()
                TestExitCode(debugger, Path.Combine(DebuggerTestPath, "ExceptionalExit.py"), 1, pythonExe: pythonwExe);
            }
        }

        private void TestExitCode(PythonDebugger debugger, string filename, int expectedExitCode, string interpreterOptions = null, string pythonExe = null) {
            var process = DebugProcess(debugger, filename, interpreterOptions: interpreterOptions, pythonExe: pythonExe);

            // Collect these values and assert on them on the main thread
            bool threadCreated = false, threadExited = false;
            bool processExited = false;
            int exitCode = -1000;
            var output = new StringBuilder();
            AutoResetEvent hasExited = new AutoResetEvent(false);
            process.ThreadCreated += (sender, args) => {
                threadCreated = true;
            };
            process.ThreadExited += (sender, args) => {
                threadExited = true;
            };
            process.ProcessExited += (sender, args) => {
                exitCode = args.ExitCode;
                processExited = true;
                hasExited.Set();
            };
            process.ExceptionRaised += (sender, args) => {
                process.Resume();
            };
            process.DebuggerOutput += (sender, args) => {
                if (args.Output != null) {
                    lock (output) {
                        output.AppendLine(args.Output);
                    }
                }
            };

            StartAndWaitForExit(process);
            // Only wait a little while - the process should have already exited
            // by the time we get here, but we may not have received the event
            // yet.
            Assert.IsTrue(hasExited.WaitOne(1000), "ProcessExited event was not raised");

            Console.WriteLine("Output from process:");
            Console.Write(output.ToString());
            Console.WriteLine("=== End of output ===");
            Assert.IsTrue(threadCreated, "Never got notification of thread creation");
            Assert.IsTrue(threadExited, "Process failed to exit");
            Assert.IsTrue(processExited, "Process failed to exit");
            Assert.AreEqual(expectedExitCode, exitCode, String.Format("Unexpected Python process exit code for '{0}'", filename));
        }

        private new PythonProcess DebugProcess(PythonDebugger debugger, string filename, Action<PythonProcess, PythonThread> onLoaded = null, bool resumeOnProcessLoaded = true, string interpreterOptions = null, PythonDebugOptions debugOptions = PythonDebugOptions.RedirectOutput, string cwd = null, string pythonExe = null) {
            string fullPath = Path.GetFullPath(filename);
            string dir = cwd ?? Path.GetFullPath(Path.GetDirectoryName(filename));
            var process = debugger.CreateProcess(Version.Version, pythonExe ?? Version.Path, "\"" + fullPath + "\"", dir, "", interpreterOptions, debugOptions);
            process.ProcessLoaded += (sender, args) => {
                if (onLoaded != null) {
                    onLoaded(process, args.Thread);
                }
                if (resumeOnProcessLoaded) {
                    process.Resume();
                }
            };

            return process;
        }

        #endregion

        #region Argument Tests

        [TestMethod, Priority(0)]
        public void TestInterpreterArguments() {
            Version.AssertInstalled();
            var debugger = new PythonDebugger();

            // test which verifies we have no doc string when running w/ -OO
            TestExitCode(debugger, Path.Combine(DebuggerTestPath, "DocString.py"), 0, interpreterOptions: "-OO");
        }

        #endregion

        #region Attach Tests

        /// <summary>
        /// threading module imports thread.start_new_thread, verifies that we patch threading's method
        /// in addition to patching the thread method so that breakpoints on threads created after
        /// attach via the threading module can be hit.
        /// </summary>
        [TestMethod, Priority(0)]
        public void AttachThreadingStartNewThread() {
            if (GetType() != typeof(DebuggerTestsIpy)) {    // IronPython doesn't support attach
                // http://pytools.codeplex.com/workitem/638
                // http://pytools.codeplex.com/discussions/285741#post724014
                var psi = new ProcessStartInfo(Version.Path, "\"" + TestData.GetPath(@"TestData\DebuggerProject\ThreadingStartNewThread.py") + "\"");
                psi.WorkingDirectory = TestData.GetPath(@"TestData\DebuggerProject");
                psi.EnvironmentVariables["PYTHONPATH"] = @"..\..";
                psi.UseShellExecute = false;
                Process p = Process.Start(psi);
                try {
                    System.Threading.Thread.Sleep(1000);

                    AutoResetEvent attached = new AutoResetEvent(false);
                    AutoResetEvent breakpointHit = new AutoResetEvent(false);

                    PythonProcess proc;
                    ConnErrorMessages errReason;
                    if ((errReason = PythonProcess.TryAttach(p.Id, out proc)) != ConnErrorMessages.None) {
                        Assert.Fail("Failed to attach {0}", errReason);
                    }

                    try {
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
                                args.Thread.Frames[0].ExecuteText("x = False", (x) => { });
                                mainThread = args.Thread;
                            } else if (args.Breakpoint.LineNo == 5) {
                                // we hit the breakpoint on the new thread
                                Debug.WriteLine(String.Format("Second BP hit {0}", args.Thread.Id));
                                breakpointHit.Set();
                                bpThread = args.Thread;
                            } else {
                                Debug.WriteLine(String.Format("Hit breakpoint on wrong line number: {0}", args.Breakpoint.LineNo));
                                wrongLine = true;
                                attached.Set();
                                breakpointHit.Set();
                            }
                            proc.Continue();
                        };
                        proc.StartListening();

                        Assert.IsTrue(attached.WaitOne(10000), "Failed to attach within 10s");
                        Assert.IsTrue(breakpointHit.WaitOne(20000), "Failed to hit breakpoint within 20s of attaching");
                        Assert.IsFalse(wrongLine, "Breakpoint broke on the wrong line");

                        Assert.AreNotEqual(mainThread, bpThread);
                    } finally {
                        DetachProcess(proc);
                    }
                } finally {
                    DisposeProcess(p);
                }
            }
        }


        [TestMethod, Priority(0)]
        public void AttachReattach() {
            if (GetType() != typeof(DebuggerTestsIpy)) {    // IronPython doesn't support attach
                Process p = Process.Start(Version.Path, "\"" + TestData.GetPath(@"TestData\DebuggerProject\InfiniteRun.py") + "\"");
                try {
                    System.Threading.Thread.Sleep(1000);

                    AutoResetEvent attached = new AutoResetEvent(false);
                    AutoResetEvent detached = new AutoResetEvent(false);
                    for (int i = 0; i < 10; i++) {
                        Console.WriteLine(i);

                        PythonProcess proc;
                        ConnErrorMessages errReason;
                        if ((errReason = PythonProcess.TryAttach(p.Id, out proc)) != ConnErrorMessages.None) {
                            Assert.Fail("Failed to attach {0}", errReason);
                        }

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
        [TestMethod, Priority(0)]
        public void AttachMultithreadedSleeper() {
            if (GetType() != typeof(DebuggerTestsIpy)) {    // IronPython doesn't support attach
                // http://pytools.codeplex.com/discussions/285741 1/12/2012 6:20 PM
                Process p = Process.Start(Version.Path, "\"" + TestData.GetPath(@"TestData\DebuggerProject\AttachMultithreadedSleeper.py") + "\"");
                try {
                    System.Threading.Thread.Sleep(1000);

                    AutoResetEvent attached = new AutoResetEvent(false);

                    PythonProcess proc;
                    ConnErrorMessages errReason;
                    if ((errReason = PythonProcess.TryAttach(p.Id, out proc)) != ConnErrorMessages.None) {
                        Assert.Fail("Failed to attach {0}", errReason);
                    }

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
        }

        /// <summary>
        /// Python 3.2 changes the rules about when we can call Py_InitThreads.
        /// 
        /// http://pytools.codeplex.com/workitem/834
        /// </summary>
        [TestMethod, Priority(0)]
        public void AttachSingleThreadedSleeper() {
            if (GetType() != typeof(DebuggerTestsIpy)) {    // IronPython doesn't support attach
                // http://pytools.codeplex.com/discussions/285741 1/12/2012 6:20 PM
                Process p = Process.Start(Version.Path, "\"" + TestData.GetPath(@"TestData\DebuggerProject\AttachSingleThreadedSleeper.py") + "\"");
                try {
                    System.Threading.Thread.Sleep(1000);

                    AutoResetEvent attached = new AutoResetEvent(false);

                    PythonProcess proc;
                    ConnErrorMessages errReason;
                    if ((errReason = PythonProcess.TryAttach(p.Id, out proc)) != ConnErrorMessages.None) {
                        Assert.Fail("Failed to attach {0}", errReason);
                    }
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
        }

        /*
        [TestMethod, Priority(0)]
        public void AttachReattach64() {
            Process p = Process.Start("C:\\Python27_x64\\python.exe", "\"" + TestData.GetPath(@"TestData\DebuggerProject\InfiniteRun.py") + "\"");
            try {
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
            } finally {
                DisposeProcess(p);
            }
        }*/

        [TestMethod, Priority(0)]
        public void AttachReattachThreadingInited() {
            if (GetType() != typeof(DebuggerTestsIpy)) {    // IronPython shouldn't support attach
                Process p = Process.Start(Version.Path, "\"" + TestData.GetPath(@"TestData\DebuggerProject\InfiniteRunThreadingInited.py") + "\"");
                try {
                    System.Threading.Thread.Sleep(1000);

                    AutoResetEvent attached = new AutoResetEvent(false);
                    AutoResetEvent detached = new AutoResetEvent(false);
                    for (int i = 0; i < 10; i++) {
                        Console.WriteLine(i);

                        PythonProcess proc;
                        ConnErrorMessages errReason;
                        if ((errReason = PythonProcess.TryAttach(p.Id, out proc)) != ConnErrorMessages.None) {
                            Assert.Fail("Failed to attach {0}", errReason);
                        }

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
        }

        [TestMethod, Priority(0)]
        public void AttachReattachInfiniteThreads() {
            if (GetType() != typeof(DebuggerTestsIpy)) {    // IronPython shouldn't support attach
                Process p = Process.Start(Version.Path, "\"" + TestData.GetPath(@"TestData\DebuggerProject\InfiniteThreads.py") + "\"");
                try {
                    System.Threading.Thread.Sleep(1000);

                    AutoResetEvent attached = new AutoResetEvent(false);
                    AutoResetEvent detached = new AutoResetEvent(false);
                    for (int i = 0; i < 10; i++) {
                        Console.WriteLine(i);

                        PythonProcess proc;
                        ConnErrorMessages errReason;
                        if ((errReason = PythonProcess.TryAttach(p.Id, out proc)) != ConnErrorMessages.None) {
                            Assert.Fail("Failed to attach {0}", errReason);
                        }

                        proc.ProcessLoaded += (sender, args) => {
                            attached.Set();
                        };
                        proc.ProcessExited += (sender, args) => {
                            detached.Set();
                        };
                        proc.StartListening();

                        Assert.IsTrue(attached.WaitOne(20000), "Failed to attach within 20s");
                        proc.Detach();
                        Assert.IsTrue(detached.WaitOne(20000), "Failed to detach within 20s");

                    }
                } finally {
                    DisposeProcess(p);
                }
            }
        }

        [TestMethod, Priority(0)]
        public void AttachTimeout() {
            if (GetType() != typeof(DebuggerTestsIpy)) {    // IronPython doesn't support attach

                string cast = "(PyCodeObject*)";
                if (Version.Version >= PythonLanguageVersion.V32) {
                    // 3.2 changed the API here...
                    cast = "";
                }

                var hostCode = @"#include <python.h>
#include <windows.h>
#include <stdio.h>

int main(int argc, char* argv[]) {
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
        }

        /// <summary>
        /// Attempts to attach w/ code only running on new threads which are initialized using PyGILState_Ensure
        /// </summary>
        [TestMethod, Priority(0)]
        public void AttachNewThread_PyGILState_Ensure() {
            if (GetType() == typeof(DebuggerTestsIpy)) {    // IronPython doesn't support attach
                Assert.Inconclusive("Test not supported");
            }

            File.WriteAllText("gilstate_attach.py", @"def test():
    for i in range(10):
        print(i)

    return 0");

            var hostCode = @"#include <Windows.h>
#include <process.h>
#undef _DEBUG
#include <Python.h>

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
            CompileCode(hostCode);

            // start the test process w/ our handle
            Process p = RunHost("test.exe");
            try {
                System.Threading.Thread.Sleep(1500);

                AutoResetEvent attached = new AutoResetEvent(false);
                AutoResetEvent bpHit = new AutoResetEvent(false);
                PythonProcess proc;
                ConnErrorMessages errReason;
                if ((errReason = PythonProcess.TryAttach(p.Id, out proc)) != ConnErrorMessages.None) {
                    Assert.Fail("Failed to attach {0}", errReason);
                } else {
                    Console.WriteLine("Attached");
                }

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
        [TestMethod, Priority(0)]
        public void AttachNewThread_PyThreadState_New() {
            if (GetType() == typeof(DebuggerTestsIpy) ||    // IronPython doesn't support attach
                Version.Version >= PythonLanguageVersion.V32) {    // PyEval_AcquireLock deprecated in 3.2
                Assert.Inconclusive("Test not supported");
            }

            File.WriteAllText("gilstate_attach.py", @"def test():
    for i in range(10):
        print(i)

    return 0");

            var hostCode = @"#include <Windows.h>
#include <process.h>
#undef _DEBUG
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
            CompileCode(hostCode);

            // start the test process w/ our handle
            Process p = RunHost("test.exe");
            try {
                System.Threading.Thread.Sleep(1500);

                AutoResetEvent attached = new AutoResetEvent(false);
                AutoResetEvent bpHit = new AutoResetEvent(false);
                PythonProcess proc;
                ConnErrorMessages errReason;
                if ((errReason = PythonProcess.TryAttach(p.Id, out proc)) != ConnErrorMessages.None) {
                    Assert.Fail("Failed to attach {0}", errReason);
                } else {
                    Console.WriteLine("Attached");
                }

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

        public virtual string CreateString {
            get {
                return "PyString_FromString";
            }
        }

        [TestMethod, Priority(0)]
        public void AttachTimeoutThreadsInitialized() {
            if (GetType() != typeof(DebuggerTestsIpy)) {    // IronPython doesn't support attach

                string cast = "(PyCodeObject*)";
                if (Version.Version >= PythonLanguageVersion.V32) {
                    // 3.2 changed the API here...
                    cast = "";
                }


                var hostCode = @"#include <python.h>
#include <windows.h>

int main(int argc, char* argv[]) {
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
        }

        private void AttachTest(string hostCode) {
            CompileCode(hostCode);

            // start the test process w/ our handle
            var eventName = Guid.NewGuid().ToString();
            EventWaitHandle handle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            ProcessStartInfo psi = new ProcessStartInfo("test.exe", eventName);
            psi.UseShellExecute = false;
            psi.RedirectStandardError = psi.RedirectStandardOutput = true;
            psi.CreateNoWindow = true;
            // Add Python to PATH so that the host can locate the DLL in case it's not in \Windows\System32 (e.g. for EPD)
            psi.EnvironmentVariables["PATH"] = Environment.GetEnvironmentVariable("PATH") + ";" + Path.GetDirectoryName(Version.Path);

            Process p = Process.Start(psi);
            var outRecv = new OutputReceiver();
            p.OutputDataReceived += outRecv.OutputDataReceived;
            p.ErrorDataReceived += outRecv.OutputDataReceived;
            p.BeginErrorReadLine();
            p.BeginOutputReadLine();

            try {
                // start the attach with the GIL held
                AutoResetEvent attached = new AutoResetEvent(false);
                PythonProcess proc;
                ConnErrorMessages errReason;
                if ((errReason = PythonProcess.TryAttach(p.Id, out proc)) != ConnErrorMessages.None) {
                    Assert.Fail("Failed to attach {0}", errReason);
                }

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

        private void CompileCode(string hostCode) {
            File.WriteAllText("test.cpp", hostCode);

            // compile our host code...
            var startInfo = new ProcessStartInfo(
                Path.Combine(GetVCBinDir(), "cl.exe"),
                String.Format("/I{0}\\Include test.cpp /link /libpath:{0}\\libs", Path.GetDirectoryName(Version.Path))
            );

            startInfo.EnvironmentVariables["PATH"] = Environment.GetEnvironmentVariable("PATH") + ";" + GetVSIDEInstallDir();

            startInfo.EnvironmentVariables["INCLUDE"] = GetVCIncludeDir() + ";" +
                string.Join(";", WindowsSdk.Latest.IncludePaths);
            startInfo.EnvironmentVariables["LIB"] = GetVCLibDir() + ";" +
                (Version.Isx64 ? WindowsSdk.Latest.X64LibPath : WindowsSdk.Latest.X86LibPath);

            Console.WriteLine("\n\nPATH:\n" + startInfo.EnvironmentVariables["PATH"]);
            Console.WriteLine("\n\nINCLUDE:\n" + startInfo.EnvironmentVariables["INCLUDE"]);
            Console.WriteLine("\n\nLIB:\n" + startInfo.EnvironmentVariables["LIB"]);

            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.CreateNoWindow = true;
            var compileProcess = Process.Start(startInfo);
            try {
                var outputReceiver = new OutputReceiver();
                compileProcess.OutputDataReceived += outputReceiver.OutputDataReceived; // for debugging if you change the code...
                compileProcess.ErrorDataReceived += outputReceiver.OutputDataReceived;
                compileProcess.BeginErrorReadLine();
                compileProcess.BeginOutputReadLine();
                Assert.IsTrue(compileProcess.WaitForExit(DefaultWaitForExitTimeout), "Timeout while waiting for compiler process to exit.");

                Assert.AreEqual(0, compileProcess.ExitCode,
                    "Incorrect exit code: " + compileProcess.ExitCode + Environment.NewLine +
                    outputReceiver.Output.ToString()
                );
            } finally {
                if (!compileProcess.HasExited) {
                    compileProcess.Kill();
                }
                compileProcess.Dispose();
            }
        }

        private Process RunHost(string hostExe) {
            var psi = new ProcessStartInfo(hostExe) { UseShellExecute = false };
            // Add Python to PATH so that the host can locate the DLL in case it's not in \Windows\System32 (e.g. for EPD)
            psi.EnvironmentVariables["PATH"] = Environment.GetEnvironmentVariable("PATH") + ";" + Path.GetDirectoryName(Version.Path);
            return Process.Start(psi);
        }

        private string GetVCBinDir() {
            var installDir = GetVCInstallDir();
            return Version.Isx64 ?
                Path.Combine(installDir, "bin", "x86_amd64") :
                Path.Combine(installDir, "bin");
        }

        private string GetVCIncludeDir() {
            return Path.Combine(GetVCInstallDir(), "include");
        }

        private string GetVCLibDir() {
            var installDir = GetVCInstallDir();
            return Version.Isx64 ?
                Path.Combine(installDir, "lib", "amd64") :
                Path.Combine(installDir, "lib");
        }

        private static string GetVCInstallDir() {
            using (var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey("SOFTWARE\\Microsoft\\VisualStudio\\" + VSUtility.Version + "\\Setup\\VC")) {
                return key.GetValue("ProductDir").ToString();
            }
        }

        class WindowsSdk {
            public string X86LibPath { get; private set; }
            public string X64LibPath { get; private set; }
            public string[] IncludePaths { get; private set; }

            public static WindowsSdk Sdk70 = FindWindowsSdk("v7.0");
            public static WindowsSdk Sdk70a = FindWindowsSdk("v7.0A");
            public static WindowsSdk Sdk80a = FindWindowsSdk("v8.0A");
            public static WindowsSdk Kits80 = FindWindowsKits("KitsRoot", "win8");
            public static WindowsSdk Kits81 = FindWindowsKits("KitsRoot81", "winv6.3");

            public static WindowsSdk Latest {
                get {
                    if (Kits81 != null) return Kits81;
                    if (Kits80 != null) return Kits80;
                    if (Sdk80a != null) return Sdk80a;
                    if (Sdk70a != null) return Sdk70a;
                    if (Sdk70 != null) return Sdk70;
                    Assert.Fail("Windows SDK is not installed");
                    return null;
                }
            }

            private WindowsSdk(string x86libPath, string x64LibPath, params string[] includePaths) {
                X86LibPath = x86libPath;
                X64LibPath = x64LibPath;
                IncludePaths = includePaths;
            }

            private static WindowsSdk FindWindowsSdk(string version) {
                var regValue = Registry.GetValue(
                    "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Microsoft SDKs\\Windows\\" + version,
                    "InstallationFolder",
                    null);

                if (regValue != null) {
                    var rootPath = regValue.ToString();
                    if (Directory.Exists(Path.Combine(rootPath, "Include"))) {
                        return new WindowsSdk(
                            Path.Combine(rootPath, "Lib"),
                            Path.Combine(rootPath, "Include"));
                    }
                }

                string[] wellKnownLocations = new[] {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft SDKs", "Windows", version),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft SDKs", "Windows", version)
                };

                foreach (var rootPath in wellKnownLocations) {
                    if (Directory.Exists(Path.Combine(rootPath, "Include")))
                        return new WindowsSdk(
                            Path.Combine(rootPath, "Lib"),
                            Path.Combine(rootPath, "Lib", "x64"),
                            Path.Combine(rootPath, "Include"));
                }

                return null;
            }

            private static WindowsSdk FindWindowsKits(string version, string libFolderName) {
                var regValue = Registry.GetValue(
                    "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows Kits\\Installed Roots",
                    version,
                    null);

                if (regValue != null) {
                    var rootPath = regValue.ToString();
                    if (Directory.Exists(Path.Combine(rootPath, "Include"))) {
                        return new WindowsSdk(
                            Path.Combine(rootPath, "Lib", libFolderName, "um", "x86"),
                            Path.Combine(rootPath, "Lib", libFolderName, "um", "x64"),
                            Path.Combine(rootPath, "Include", "shared"),
                            Path.Combine(rootPath, "Include", "um"));
                    }
                }

                return null;
            }
        }

        private static string GetVSIDEInstallDir() {
            using (var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey("SOFTWARE\\Microsoft\\VisualStudio\\" + VSUtility.Version + "\\Setup\\VS")) {
                return key.GetValue("EnvironmentDirectory").ToString();
            }
        }

        #endregion

        #region Output Tests

        [TestMethod, Priority(0)]
        public void Test3xStdoutBuffer() {
            if (Version.Version.Is3x()) {
                var debugger = new PythonDebugger();

                bool gotOutput = false;
                var process = DebugProcess(debugger, Path.Combine(DebuggerTestPath, "StdoutBuffer3x.py"), (processObj, threadObj) => {
                    processObj.DebuggerOutput += (sender, args) => {
                        Assert.IsFalse(gotOutput, "got output more than once");
                        gotOutput = true;
                        Assert.AreEqual("fob", args.Output);
                    };
                }, debugOptions: PythonDebugOptions.RedirectOutput);

                StartAndWaitForExit(process);

                Assert.IsTrue(gotOutput, "failed to get output");
            }
        }

        [TestMethod, Priority(0)]
        public void TestInputFunction() {
            // 845 Python 3.3 Bad argument type for the debugger output wrappers
            // A change to the Python 3.3 implementation of input() now requires
            // that `errors` be set to a valid value on stdout. This test
            // ensures that calls to `input` continue to work.

            var debugger = new PythonDebugger();
            var expectedOutput = "Provide A: fob\n";
            string actualOutput = string.Empty;

            var process = DebugProcess(debugger, Path.Combine(DebuggerTestPath, "InputFunction.py"), (processObj, threadObj) => {
                processObj.DebuggerOutput += (sender, args) => {
                    actualOutput += args.Output;
                };
            }, debugOptions: PythonDebugOptions.RedirectOutput | PythonDebugOptions.RedirectInput);

            try {
                process.Start();
                Thread.Sleep(1000);
                process.SendStringToStdInput("fob\n");
            } finally {
                WaitForExit(process);
            }

            Assert.AreEqual(expectedOutput, actualOutput);
        }

        #endregion

        internal override PythonVersion Version {
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

        public override string CreateString {
            get {
                return "PyUnicodeUCS2_FromString";
            }
        }
    }

    [TestClass]
    public class DebuggerTests30 : DebuggerTests3x {
        [ClassInitialize]
        public static new void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        internal override PythonVersion Version {
            get {
                return PythonPaths.Python30;
            }
        }
    }

    [TestClass]
    public class DebuggerTests31 : DebuggerTests3x {
        [ClassInitialize]
        public static new void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        internal override PythonVersion Version {
            get {
                return PythonPaths.Python31;
            }
        }
    }

    [TestClass]
    public class DebuggerTests32 : DebuggerTests3x {
        [ClassInitialize]
        public static new void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        internal override PythonVersion Version {
            get {
                return PythonPaths.Python32;
            }
        }
    }

    [TestClass]
    public class DebuggerTests33 : DebuggerTests3x {
        [ClassInitialize]
        public static new void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        internal override PythonVersion Version {
            get {
                return PythonPaths.Python33;
            }
        }

        public override string CreateString {
            get {
                return "PyUnicode_FromString";
            }
        }
    }

    [TestClass]
    public class DebuggerTests34 : DebuggerTests3x {
        [ClassInitialize]
        public static new void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        internal override PythonVersion Version {
            get {
                return PythonPaths.Python34;
            }
        }

        public override string CreateString {
            get {
                return "PyUnicode_FromString";
            }
        }
    }

    [TestClass]
    public class DebuggerTests27 : DebuggerTests {
        [ClassInitialize]
        public static new void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        internal override PythonVersion Version {
            get {
                return PythonPaths.Python27;
            }
        }
    }

    [TestClass]
    public class DebuggerTests25 : DebuggerTests {
        [ClassInitialize]
        public static new void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        internal override PythonVersion Version {
            get {
                return PythonPaths.Python25;
            }
        }
    }

    [TestClass]
    public class DebuggerTestsIpy : DebuggerTests {
        [ClassInitialize]
        public static new void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        internal override PythonVersion Version {
            get {
                return PythonPaths.IronPython27;
            }
        }
    }
}

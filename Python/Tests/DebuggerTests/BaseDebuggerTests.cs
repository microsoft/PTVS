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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using EnvDTE90;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using TestUtilities;

namespace DebuggerTests {
    public class BaseDebuggerTests {
        protected const int DefaultWaitForExitTimeout = 20000;

        internal virtual string DebuggerTestPath {
            get {
                return TestData.GetPath(@"TestData\DebuggerProject\");
            }
        }

        internal class EvalResult {
            private readonly string _typeName, _repr;
            private readonly long? _length;
            private readonly PythonEvaluationResultFlags? _flags;
            private readonly bool _allowOtherFlags;

            public readonly string ExceptionText, Expression;
            public readonly bool IsError;

            public string HexRepr;
            public bool ValidateRepr = true;
            public bool ValidateHexRepr = false;

            public static EvalResult Exception(string expression, string exceptionText) {
                return new EvalResult(expression, exceptionText, false);
            }

            public static EvalResult Value(string expression, string typeName, string repr, long? length = null, PythonEvaluationResultFlags? flags = null, bool allowOtherFlags = false) {
                return new EvalResult(expression, typeName, repr, length, flags, allowOtherFlags);
            }

            public static EvalResult ErrorExpression(string expression, string error) {
                return new EvalResult(expression, error, true);
            }

            EvalResult(string expression, string exceptionText, bool isError) {
                Expression = expression;
                ExceptionText = exceptionText;
                IsError = isError;
            }

            EvalResult(string expression, string typeName, string repr, long? length, PythonEvaluationResultFlags? flags, bool allowOtherFlags) {
                Expression = expression;
                _typeName = typeName;
                _repr = repr;
                _length = length;
                _flags = flags;
                _allowOtherFlags = allowOtherFlags;
            }

            public void Validate(PythonEvaluationResult result) {
                if (ExceptionText != null) {
                    Assert.AreEqual(ExceptionText, result.ExceptionText);
                } else {
                    if (_typeName != null) {
                        Assert.AreEqual(_typeName, result.TypeName);
                    }

                    if (ValidateRepr) {
                        Assert.AreEqual(_repr, result.StringRepr);
                    }

                    if (ValidateHexRepr) {
                        Assert.AreEqual(HexRepr, result.HexRepr);
                    }

                    if (_length != null) {
                        Assert.AreEqual(_length.Value, result.Length);
                    }

                    if (_flags != null) {
                        if (_allowOtherFlags) {
                            Assert.AreEqual(_flags.Value, _flags.Value & result.Flags);
                        } else {
                            Assert.AreEqual(_flags.Value, result.Flags);
                        }
                    }
                }
            }
        }

        internal class VariableCollection : List<EvalResult> {
            public void Add(string name, string typeName = null, string repr = null) {
                var er = EvalResult.Value(name, typeName, repr);
                if (repr == null) {
                    er.ValidateRepr = false;
                }
                Add(er);
            }

            public void AddRange(params string[] names) {
                foreach (var name in names) {
                    Add(name);
                }
            }
        }

        /// <summary>
        /// Runs the given file name setting break points at linenos.  Expects to hit the lines
        /// in lineHits as break points in the order provided in lineHits.  If lineHits is negative
        /// expects to hit the positive number and then removes the break point.
        /// </summary>
        internal void BreakpointTest(string filename, int[] linenos, int[] lineHits, string[] conditions = null, bool[] breakWhenChanged = null,
                                     string cwd = null, string breakFilename = null, bool checkBound = true, bool checkThread = true, string arguments = "",
                                     Action onProcessLoaded = null, PythonDebugOptions debugOptions = PythonDebugOptions.RedirectOutput,
                                     bool waitForExit = true, string interpreterOptions = null) {
            var debugger = new PythonDebugger();
            PythonThread thread = null;
            string rootedFilename = filename;
            if (!Path.IsPathRooted(filename)) {
                rootedFilename = DebuggerTestPath + filename;
            }

            var lineList = new List<int>(linenos);
            var breakpointsToBeBound = lineList.Count;

            AutoResetEvent processLoaded = new AutoResetEvent(false);
            var process = DebugProcess(
                debugger,
                rootedFilename,
                cwd: cwd,
                arguments: arguments,
                resumeOnProcessLoaded: false,
                onLoaded: (newproc, newthread) => {
                    for (int i = 0; i < linenos.Length; i++) {
                        var line = linenos[i];

                        int finalLine = line;
                        if (finalLine < 0) {
                            finalLine = -finalLine;
                        }

                        PythonBreakpoint breakPoint;
                        var finalBreakFilename = breakFilename ?? rootedFilename;

                        if (conditions != null) {
                            if (breakWhenChanged != null) {
                                breakPoint = newproc.AddBreakPoint(finalBreakFilename, line, conditions[i], breakWhenChanged[i]);
                            } else {
                                breakPoint = newproc.AddBreakPoint(finalBreakFilename, line, conditions[i]);
                            }
                        } else {
                            breakPoint = newproc.AddBreakPointByFileExtension(line, finalBreakFilename);
                        }

                        breakPoint.Add();

                        // Django breakpoints are never bound
                        if (breakPoint.IsDjangoBreakpoint) {
                            --breakpointsToBeBound;
                        }
                    }
                    thread = newthread;

                    if (onProcessLoaded != null) {
                        onProcessLoaded();
                    }
                    processLoaded.Set();
                },
                interpreterOptions: interpreterOptions
            );

            int breakpointsBound = 0;
            int breakpointsNotBound = 0;
            AutoResetEvent allBreakpointBindResults = new AutoResetEvent(breakpointsToBeBound == 0);
            int breakpointHit = 0;
            AutoResetEvent allBreakpointsHit = new AutoResetEvent(false);

            try {
                process.BreakpointBindFailed += (sender, args) => {
                    if (checkBound) {
                        Assert.Fail("unexpected bind failure");
                    }
                    ++breakpointsNotBound;
                    if (breakpointsBound + breakpointsNotBound == breakpointsToBeBound) {
                        allBreakpointBindResults.Set();
                    }
                };

                process.BreakpointBindSucceeded += (sender, args) => {
                    Assert.AreEqual(args.Breakpoint.Filename, breakFilename ?? rootedFilename);
                    int index = lineList.IndexOf(args.Breakpoint.LineNo);
                    Assert.IsTrue(index != -1);
                    lineList[index] = -1;
                    breakpointsBound++;
                    if (breakpointsBound + breakpointsNotBound == breakpointsToBeBound) {
                        allBreakpointBindResults.Set();
                    }
                };

                process.BreakpointHit += (sender, args) => {
                    if (breakpointHit < lineHits.Length) {
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
                        if (checkThread) {
                            Assert.AreEqual(args.Thread, thread);
                        }
                        if (breakpointHit == lineHits.Length) {
                            allBreakpointsHit.Set();
                        }
                    }
                    process.Continue();
                };

                process.Start();
                AssertWaited(processLoaded);
                process.AutoResumeThread(thread.Id);
                if (breakpointsToBeBound > 0) {
                    AssertWaited(allBreakpointBindResults);
                }
            } finally {
                if (waitForExit) {
                    WaitForExit(process);
                } else {
                    allBreakpointsHit.WaitOne(20000);
                    process.Terminate();
                }
            }
            Assert.AreEqual(lineHits.Length, breakpointHit);
            if (checkBound) {
                Assert.AreEqual(linenos.Length, breakpointsBound);
            }
        }

        internal PythonProcess DebugProcess(PythonDebugger debugger, string filename, Action<PythonProcess, PythonThread> onLoaded = null, bool resumeOnProcessLoaded = true, string interpreterOptions = null, PythonDebugOptions debugOptions = PythonDebugOptions.RedirectOutput, string cwd = null, string arguments = "") {
            return debugger.DebugProcess(Version, filename, onLoaded, resumeOnProcessLoaded, interpreterOptions, debugOptions, cwd, arguments);
        }

        internal class LocalsTest {
            private readonly BaseDebuggerTests _tests;

            public readonly VariableCollection Locals = new VariableCollection();
            public readonly VariableCollection Params = new VariableCollection();

            public string FileName;
            public int LineNo;
            public string BreakFileName;
            public string Arguments;
            public Action ProcessLoaded;
            public PythonDebugOptions DebugOptions = PythonDebugOptions.RedirectOutput;
            public bool WaitForExit = true;
            public bool IgnoreExtra = false;

            public LocalsTest(BaseDebuggerTests tests, string fileName, int lineNo) {
                _tests = tests;
                FileName = fileName;
                LineNo = lineNo;
            }

            public void Run() {
                PythonThread thread = _tests.RunAndBreak(FileName, LineNo, breakFilename: BreakFileName, arguments: Arguments, processLoaded: ProcessLoaded, debugOptions: DebugOptions);
                PythonProcess process = thread.Process;
                try {
                    var frames = thread.Frames;
                    var localNamesExpected = Locals.Select(v => v.Expression).ToSet();
                    var paramNamesExpected = Params.Select(v => v.Expression).ToSet();

                    string fileNameExpected;
                    if (BreakFileName == null) {
                        fileNameExpected = Path.GetFullPath(_tests.DebuggerTestPath + FileName);
                    } else if (Path.IsPathRooted(BreakFileName)) {
                        fileNameExpected = BreakFileName;
                    } else {
                        fileNameExpected = Path.GetFullPath(_tests.DebuggerTestPath + BreakFileName);
                    }

                    Assert.AreEqual(frames[0].FileName, fileNameExpected, true);

                    if (!IgnoreExtra) {
                        AssertUtil.ContainsExactly(frames[0].Locals.Select(x => x.Expression), localNamesExpected);
                        AssertUtil.ContainsExactly(frames[0].Parameters.Select(x => x.Expression), paramNamesExpected);
                    }

                    foreach (var expectedLocal in Locals) {
                        var actualLocal = frames[0].Locals.First(v => v.Expression == expectedLocal.Expression);
                        expectedLocal.Validate(actualLocal);
                    }

                    foreach (var expectedParam in Params) {
                        var actualParam = frames[0].Parameters.First(v => v.Expression == expectedParam.Expression);
                        expectedParam.Validate(actualParam);
                    }

                    process.Continue();

                    if (WaitForExit) {
                        _tests.WaitForExit(process);
                    }
                } finally {
                    if (!process.HasExited) {
                        process.Terminate();
                    }
                }
            }
        }

        internal PythonThread RunAndBreak(string filename, int lineNo, string breakFilename = null, string arguments = "", Action processLoaded = null, PythonDebugOptions debugOptions = PythonDebugOptions.RedirectOutput) {
            PythonThread thread;

            var debugger = new PythonDebugger();
            thread = null;
            PythonProcess process = DebugProcess(debugger, DebuggerTestPath + filename, (newproc, newthread) => {
                var breakPoint = newproc.AddBreakPointByFileExtension(lineNo, breakFilename ?? filename);
                breakPoint.Add();
                thread = newthread;
                if (processLoaded != null) {
                    processLoaded();
                }
            },
            arguments: arguments,
            debugOptions: debugOptions);

            AutoResetEvent brkHit = new AutoResetEvent(false);
            process.BreakpointHit += (sender, args) => {
                thread = args.Thread;
                brkHit.Set();
            };

            bool ready = false;
            try {
                process.Start();

                AssertWaited(brkHit);
                ready = true;
            } finally {
                if (!ready) {
                    process.Terminate();
                }
            }

            return thread;
        }

        internal static void AssertWaited(EventWaitHandle eventObj) {
            if (!eventObj.WaitOne(10000)) {
                Assert.Fail("Failed to wait on event");
            }
        }

        internal virtual PythonVersion Version {
            get {
                return PythonPaths.Python26;
            }
        }

        internal enum StepKind {
            Into,
            Out,
            Over,
            Resume
        }

        internal class ExpectedStep {
            public readonly StepKind Kind;
            public readonly int StartLine;

            public ExpectedStep(StepKind kind, int startLine) {
                Kind = kind;
                StartLine = startLine;
            }
        }

        internal void StepTest(string filename, params ExpectedStep[] kinds) {
            StepTest(filename, new int[0], new Action<PythonProcess>[0], kinds);
        }

        internal void StepTest(string filename, int[] breakLines, Action<PythonProcess>[] breakAction, params ExpectedStep[] kinds) {
            StepTest(filename, null, null, breakLines, breakAction, null, PythonDebugOptions.RedirectOutput, true, kinds);
        }

        internal void StepTest(string filename, string breakFile, string arguments, int[] breakLines, Action<PythonProcess>[] breakAction, Action processLoaded, PythonDebugOptions options = PythonDebugOptions.RedirectOutput, bool waitForExit = true, params ExpectedStep[] kinds) {
            Console.WriteLine("--- Begin Step Test ---");
            var debugger = new PythonDebugger();
            if (breakFile == null) {
                breakFile = filename;
            }

            string fullPath = Path.GetFullPath(filename);
            string dir = Path.GetDirectoryName(filename);
            var process = debugger.CreateProcess(Version.Version, Version.InterpreterPath, "\"" + fullPath + "\" " + (arguments ?? ""), dir, "", null, options);
            try {
                PythonThread thread = null;
                process.ThreadCreated += (sender, args) => {
                    thread = args.Thread;
                };


                AutoResetEvent processEvent = new AutoResetEvent(false);

                bool processLoad = false, stepComplete = false;
                process.ProcessLoaded += (sender, args) => {
                    foreach (var breakLine in breakLines) {
                        var bp = process.AddBreakPointByFileExtension(breakLine, breakFile);
                        bp.Add();
                    }

                    processLoad = true;
                    processEvent.Set();

                    if (processLoaded != null) {
                        processLoaded();
                    }
                };

                process.StepComplete += (sender, args) => {
                    stepComplete = true;
                    processEvent.Set();
                };

                int breakHits = 0;
                process.BreakpointHit += (sender, args) => {
                    Console.WriteLine("Breakpoint hit");
                    if (breakAction != null) {
                        breakAction[breakHits++](process);
                    }
                    stepComplete = true;
                    processEvent.Set();
                };

                process.Start();
                for (int curStep = 0; curStep < kinds.Length; curStep++) {
                    Console.WriteLine("Step {0} {1}", curStep, kinds[curStep].Kind);
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
                if (waitForExit) {
                    WaitForExit(process);
                }
            } finally {
                process.Terminate();
            }
        }

        internal void WaitForExit(PythonProcess process, bool assert = true) {
            bool exited = process.WaitForExit(DefaultWaitForExitTimeout);
            if (!exited) {
                process.Terminate();
                if (assert) {
                    Assert.Fail("Timeout while waiting for Python process to exit.");
                }
            }
        }

        internal void StartAndWaitForExit(PythonProcess process) {
            bool exited = false;
            try {
                process.Start();
                exited = process.WaitForExit(DefaultWaitForExitTimeout);
            } finally {
                if (!exited && !process.HasExited) {
                    process.Terminate();
                    Assert.Fail("Timeout while waiting for Python process to exit.");
                }
            }
        }

        internal void DetachProcess(PythonProcess p) {
            try {
                p.Detach();
            } catch (Exception ex) {
                Console.WriteLine("Failed to detach process");
                Console.WriteLine(ex);
            }
        }

        internal void TerminateProcess(PythonProcess p) {
            try {
                p.Terminate();
            } catch (Exception ex) {
                Console.WriteLine("Failed to detach process");
                Console.WriteLine(ex);
            }
        }

        internal void DisposeProcess(Process p) {
            try {
                if (!p.HasExited) {
                    p.Kill();
                }
            } catch (Exception ex) {
                Console.WriteLine("Failed to kill process");
                Console.WriteLine(ex);
            }
            p.Dispose();
        }
    }
}

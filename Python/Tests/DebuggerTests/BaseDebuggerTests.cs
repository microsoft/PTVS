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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;

namespace DebuggerTests {
    public class BaseDebuggerTests {
        static BaseDebuggerTests() {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        protected const int DefaultWaitForExitTimeout = 20000;

        internal virtual string DebuggerTestPath {
            get {
                return TestData.GetPath(@"TestData\DebuggerProject\");
            }
        }

        internal static void ForEachLine(TextReader reader, Action<string> action) {
            for (var line = reader.ReadLine(); line != null; line = reader.ReadLine()) {
                action(line);
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

        internal class BreakpointBase {
            public string FileName; // if null, BreakpointTest.BreakFileName is used instead
            public int LineNumber;
            public bool? ExpectHitOnMainThread;
            public bool RemoveWhenHit;
            public Action<BreakpointHitEventArgs> OnHit;
        }

        internal class Breakpoint : BreakpointBase {
            public PythonBreakpointConditionKind ConditionKind;
            public string Condition;
            public PythonBreakpointPassCountKind PassCountKind;
            public int PassCount;
            public bool? IsBindFailureExpected;

            public Breakpoint(int lineNumber) {
                LineNumber = lineNumber;
            }

            public Breakpoint(string fileName, int lineNumber) {
                Assert.IsTrue(fileName.EndsWith(".py"));
                FileName = fileName;
                LineNumber = lineNumber;
            }
        }

        internal class DjangoBreakpoint : BreakpointBase {
            public DjangoBreakpoint(int lineNumber) {
                LineNumber = lineNumber;
            }
        }

        internal class BreakpointCollection : List<BreakpointBase> {
            public void Add(int lineNumber) {
                Add(new Breakpoint(lineNumber));
            }

            public void AddRange(params int[] lineNumbers) {
                foreach (var lineNumber in lineNumbers) {
                    Add(lineNumber);
                }
            }
        }

        internal PythonProcess DebugProcess(PythonDebugger debugger, string filename, Func<PythonProcess, PythonThread, Task> onLoaded = null, bool resumeOnProcessLoaded = true, string interpreterOptions = null, PythonDebugOptions debugOptions = PythonDebugOptions.RedirectOutput, string cwd = null, string arguments = "") {
            return debugger.DebugProcess(Version, filename, onLoaded, resumeOnProcessLoaded, interpreterOptions, debugOptions, cwd, arguments);
        }

        internal class BreakpointTest {
            private readonly BaseDebuggerTests _tests;

            public readonly BreakpointCollection Breakpoints = new BreakpointCollection();
            public readonly List<int> ExpectedHits = new List<int>(); // indices into Breakpoints

            public string WorkingDirectory;
            public string RunFileName;
            public string BreakFileName; // if null, RunFileName is used instead
            public PythonDebugOptions DebugOptions = PythonDebugOptions.RedirectOutput;
            public bool WaitForExit = true;
            public bool ExpectHitOnMainThread = true;
            public bool IsBindFailureExpected = false;
            public string Arguments = "";
            public string InterpreterOptions = null;
            public Action<PythonProcess> OnProcessLoaded;

            public BreakpointTest(BaseDebuggerTests tests, string runFileName) {
                _tests = tests;
                RunFileName = runFileName;

            }

            public async Task RunAsync() {
                string runFileName = RunFileName;
                if (!Path.IsPathRooted(runFileName)) {
                    runFileName = _tests.DebuggerTestPath + runFileName;
                }

                string breakFileName = BreakFileName;
                if (breakFileName != null && !Path.IsPathRooted(breakFileName)) {
                    breakFileName = _tests.DebuggerTestPath + breakFileName;
                }

                foreach (var bp in Breakpoints) {
                    var fileName = bp.FileName ?? breakFileName ?? runFileName;
                    if (fileName.EndsWith(".py")) {
                        Assert.IsTrue(bp is Breakpoint);
                    } else {
                        Assert.IsTrue(bp is DjangoBreakpoint);
                    }
                }

                var bps = new Dictionary<PythonBreakpoint, BreakpointBase>();
                var unboundBps = new HashSet<Breakpoint>();
                var breakpointsToBeBound = Breakpoints.Count;

                var debugger = new PythonDebugger();
                PythonThread thread = null;

                // Used to signal exceptions from debugger event handlers that run on a background thread.
                var backgroundException = new TaskCompletionSource<bool>();

                var processLoaded = new TaskCompletionSource<bool>();
                var process = _tests.DebugProcess(
                    debugger,
                    runFileName,
                    cwd: WorkingDirectory,
                    arguments: Arguments,
                    resumeOnProcessLoaded: false,
                    onLoaded: async (newproc, newthread) => {
                        try {
                            foreach (var bp in Breakpoints) {
                                var fileName = bp.FileName ?? breakFileName ?? runFileName;

                                PythonBreakpoint breakpoint;
                                var pyBP = bp as Breakpoint;
                                if (pyBP != null) {
                                    breakpoint = newproc.AddBreakpoint(fileName, pyBP.LineNumber, pyBP.ConditionKind, pyBP.Condition, pyBP.PassCountKind, pyBP.PassCount);
                                    unboundBps.Add(pyBP);
                                } else {
                                    var djangoBP = bp as DjangoBreakpoint;
                                    if (djangoBP != null) {
                                        breakpoint = newproc.AddDjangoBreakpoint(fileName, djangoBP.LineNumber);
                                        // Django breakpoints are never bound.
                                        --breakpointsToBeBound;
                                    } else {
                                        Assert.Fail("Unknown breakpoint type.");
                                        return;
                                    }
                                }

                                // Bind failed and succeeded events expect to find the breakpoint
                                // in the dictionary, so update it before sending the add request.
                                bps.Add(breakpoint, bp);
                                await breakpoint.AddAsync(TimeoutToken());
                            }

                            OnProcessLoaded?.Invoke(newproc);

                            thread = newthread;
                            processLoaded.SetResult(true);
                        } catch (Exception ex) {
                            backgroundException.TrySetException(ex);
                        }
                    },
                    interpreterOptions: InterpreterOptions
                );

                int breakpointsBound = 0;
                int breakpointsNotBound = 0;
                int nextExpectedHit = 0;

                var allBreakpointsHit = new TaskCompletionSource<bool>();
                var allBreakpointBindResults = new TaskCompletionSource<bool>();
                if (breakpointsToBeBound == 0) {
                    allBreakpointBindResults.SetResult(true);
                }

                try {
                    process.BreakpointBindFailed += (sender, args) => {
                        try {
                            var bp = (Breakpoint)bps[args.Breakpoint];
                            if (bp != null && !(bp.IsBindFailureExpected ?? IsBindFailureExpected)) {
                                Assert.Fail("Breakpoint at {0}:{1} failed to bind.", bp.FileName ?? breakFileName ?? runFileName, bp.LineNumber);
                            }
                            ++breakpointsNotBound;
                            if (breakpointsBound + breakpointsNotBound == breakpointsToBeBound) {
                                allBreakpointBindResults.SetResult(true);
                            }
                        } catch (Exception ex) {
                            backgroundException.TrySetException(ex);
                        }
                    };

                    process.BreakpointBindSucceeded += (sender, args) => {
                        try {
                            var bp = (Breakpoint)bps[args.Breakpoint];
                            Assert.AreEqual(bp.FileName ?? breakFileName ?? runFileName, args.Breakpoint.Filename);
                            Assert.IsTrue(unboundBps.Remove(bp));
                            ++breakpointsBound;
                            if (breakpointsBound + breakpointsNotBound == breakpointsToBeBound) {
                                allBreakpointBindResults.SetResult(true);
                            }
                        } catch (Exception ex) {
                            backgroundException.TrySetException(ex);
                        }
                    };

                    process.BreakpointHit += async (sender, args) => {
                        try {
                            if (nextExpectedHit < ExpectedHits.Count) {
                                var bp = Breakpoints[ExpectedHits[nextExpectedHit]];
                                Trace.TraceInformation("Hit {0}:{1}", args.Breakpoint.Filename, args.Breakpoint.LineNo);
                                Assert.AreSame(bp, bps[args.Breakpoint]);

                                if (bp.RemoveWhenHit) {
                                    await args.Breakpoint.RemoveAsync(TimeoutToken());
                                }

                                if (bp.ExpectHitOnMainThread ?? ExpectHitOnMainThread) {
                                    Assert.AreSame(thread, args.Thread);
                                }

                                bp.OnHit?.Invoke(args);

                                if (++nextExpectedHit == ExpectedHits.Count) {
                                    allBreakpointsHit.SetResult(true);
                                }
                            }

                            try {
                                await process.ResumeAsync(TimeoutToken());
                            } catch (TaskCanceledException) {
                                // If we don't wait for exit, the Terminate() call
                                // will cause ResumeAsync to be canceled.
                                if (WaitForExit) {
                                    throw;
                                }
                            }
                        } catch (Exception ex) {
                            backgroundException.TrySetException(ex);
                        }
                    };

                    await process.StartAsync();
                    Assert.IsTrue(WaitForAny(10000, processLoaded.Task, backgroundException.Task), "Timed out waiting for process load");

                    await process.AutoResumeThread(thread.Id, TimeoutToken());
                    if (breakpointsToBeBound > 0) {
                        Assert.IsTrue(WaitForAny(10000, allBreakpointBindResults.Task, backgroundException.Task), "Timed out waiting for breakpoints to bind");
                    }
                } finally {
                    if (WaitForExit) {
                        _tests.WaitForExit(process);
                    } else {
                        Assert.IsTrue(WaitForAny(20000, allBreakpointsHit.Task, backgroundException.Task), "Timed out waiting for breakpoints to hit");
                        process.Terminate();
                    }
                }

                if (backgroundException.Task.IsFaulted) {
                    backgroundException.Task.GetAwaiter().GetResult();
                }

                Assert.AreEqual(ExpectedHits.Count, nextExpectedHit);
                Assert.IsTrue(unboundBps.All(bp => bp.IsBindFailureExpected ?? IsBindFailureExpected));
            }

            private static bool WaitForAny(int timeout, params Task[] tasks) {
                try {
                    Task.WhenAny(tasks.Concat(new[] { Task.Delay(Timeout.Infinite, new CancellationTokenSource(timeout).Token) }))
                        .GetAwaiter().GetResult()
                        // At this point we have the task that ran to completion first. Now we need to
                        // get its result to get an exception if that task failed or got canceled.
                        .GetAwaiter().GetResult();
                    return true;
                } catch (OperationCanceledException) {
                    return false;
                }
            }
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

            public async Task RunAsync() {
                PythonThread thread = await _tests.RunAndBreakAsync(FileName, LineNo, breakFilename: BreakFileName, arguments: Arguments, processLoaded: ProcessLoaded, debugOptions: DebugOptions);
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

                    await process.ResumeAsync(TimeoutToken());

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

        internal async Task<PythonThread> RunAndBreakAsync(string filename, int lineNo, string breakFilename = null, string arguments = "", Action processLoaded = null, PythonDebugOptions debugOptions = PythonDebugOptions.RedirectOutput) {
            PythonThread thread;

            var debugger = new PythonDebugger();
            thread = null;
            PythonProcess process = DebugProcess(debugger, DebuggerTestPath + filename, async (newproc, newthread) => {
                var breakPoint = newproc.AddBreakpointByFileExtension(lineNo, breakFilename ?? filename);
                await breakPoint.AddAsync(TimeoutToken());
                thread = newthread;
                processLoaded?.Invoke();
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
                await process.StartAsync();

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
            if (!eventObj.WaitOne(20000)) {
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

        internal async Task StepTestAsync(string filename, params ExpectedStep[] kinds) {
            await StepTestAsync(filename, new int[0], new Action<PythonProcess>[0], kinds);
        }

        internal async Task StepTestAsync(string filename, int[] breakLines, Action<PythonProcess>[] breakAction, params ExpectedStep[] kinds) {
            await StepTestAsync(filename, null, null, breakLines, breakAction, null, PythonDebugOptions.RedirectOutput, true, kinds);
        }

        internal async Task StepTestAsync(string filename, string breakFile, string arguments, int[] breakLines, Action<PythonProcess>[] breakAction, Action processLoaded, PythonDebugOptions options = PythonDebugOptions.RedirectOutput, bool waitForExit = true, params ExpectedStep[] kinds) {
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
                process.ProcessLoaded += async (sender, args) => {
                    foreach (var breakLine in breakLines) {
                        var bp = process.AddBreakpointByFileExtension(breakLine, breakFile);
                        await bp.AddAsync(TimeoutToken());
                    }

                    processLoad = true;
                    processEvent.Set();
                    processLoaded?.Invoke();
                };

                process.StepComplete += (sender, args) => {
                    stepComplete = true;
                    processEvent.Set();
                };

                int breakHits = 0;
                ExceptionDispatchInfo edi = null;
                process.BreakpointHit += (sender, args) => {
                    try {
                        Console.WriteLine("Breakpoint hit");
                        if (breakAction != null) {
                            if (breakHits >= breakAction.Length) {
                                Assert.Fail("Unexpected breakpoint hit at {0}:{1}", args.Breakpoint.Filename, args.Breakpoint.LineNo);
                            }
                            breakAction[breakHits++](process);
                        }
                        stepComplete = true;
                        processEvent.Set();
                    } catch (Exception ex) {
                        edi = ExceptionDispatchInfo.Capture(ex);
                        try {
                            processEvent.Set();
                        } catch { }
                    }
                };

                await process.StartAsync();
                for (int curStep = 0; curStep < kinds.Length; curStep++) {
                    Console.WriteLine("Step {0} {1}", curStep, kinds[curStep].Kind);
                    // process the stepping events as they occur, we cannot callback during the
                    // event because the notificaiton happens on the debugger thread and we 
                    // need to callback to get the frames.
                    AssertWaited(processEvent);
                    edi?.Throw();

                    // first time through we hit process load, each additional time we should hit step complete.
                    Debug.Assert((processLoad == true && stepComplete == false && curStep == 0) ||
                                (stepComplete == true && processLoad == false && curStep != 0));

                    processLoad = stepComplete = false;

                    var frames = thread.Frames;
                    var stepInfo = kinds[curStep];
                    Assert.AreEqual(stepInfo.StartLine, frames[0].LineNo, String.Format("{0} != {1} on {2} step", stepInfo.StartLine, frames[0].LineNo, curStep));

                    switch (stepInfo.Kind) {
                        case StepKind.Into:
                            await thread.StepIntoAsync(TimeoutToken());
                            break;
                        case StepKind.Out:
                            await thread.StepOutAsync(TimeoutToken());
                            break;
                        case StepKind.Over:
                            await thread.StepOverAsync(TimeoutToken());
                            break;
                        case StepKind.Resume:
                            await process.ResumeAsync(TimeoutToken());
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

        internal async Task StartAndWaitForExitAsync(PythonProcessRunInfo processRunInfo) {
            bool exited = false;
            try {
                await processRunInfo.Process.StartAsync();

                AssertWaited(processRunInfo.ProcessLoaded);
                processRunInfo.ProcessLoadedException?.Throw();

                exited = processRunInfo.Process.WaitForExit(DefaultWaitForExitTimeout);
            } finally {
                if (!exited && !processRunInfo.Process.HasExited) {
                    processRunInfo.Process.Terminate();
                    Assert.Fail("Timeout while waiting for Python process to exit.");
                }
            }
        }

        internal async Task DetachProcessAsync(PythonProcess p) {
            try {
                await p.DetachAsync(TimeoutToken());
            } catch (Exception ex) {
                Console.WriteLine("Failed to detach process");
                Console.WriteLine(ex);
            }
        }

        internal void TerminateProcess(PythonProcess p) {
            // Killing the process will cause multiple ObjectDisposedException
            // which are normal, since the communication stream is forcibly closed.
            // Disable their logging to reduce the noise.
            AssertListener.LogObjectDisposedExceptions = false;
            try {
                p.Terminate();
            } catch (Exception ex) {
                Console.WriteLine("Failed to detach process");
                Console.WriteLine(ex);
            } finally {
                AssertListener.LogObjectDisposedExceptions = true;
            }
        }

        internal void DisposeProcess(Process p) {
            try {
                if (!p.HasExited) {
                    p.Kill();
                }

                // Process.StandardOutput/Error can only be used if BeginOutput/ErrorReadLine was
                // not called on that Process object; otherwise it throws InvalidOperationException.
                // If that happens, presumably there's some other redirector that already traced
                // the output, so there's nothing for us to do here.

                if (p.StartInfo.RedirectStandardOutput) {
                    StreamReader stdout;
                    try {
                        stdout = p.StandardOutput;
                    } catch (InvalidOperationException) {
                        stdout = null;
                    }

                    if (stdout != null) {
                        ForEachLine(stdout, s => Trace.TraceInformation("STDOUT: {0}", s));
                    }
                }

                if (p.StartInfo.RedirectStandardError) {
                    StreamReader stderr;
                    try {
                        stderr = p.StandardError;
                    } catch (InvalidOperationException) {
                        stderr = null;
                    }

                    if (stderr != null) {
                        ForEachLine(stderr, s => Trace.TraceWarning("STDERR: {0}", s));
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine("Failed to kill process");
                Console.WriteLine(ex);
            }
            p.Dispose();
        }

        protected static CancellationToken TimeoutToken() {
            return CancellationTokens.After5s;
        }
    }

    class PythonProcessRunInfo {
        public PythonProcess Process;
        public ExceptionDispatchInfo ProcessLoadedException;
        public AutoResetEvent ProcessLoaded = new AutoResetEvent(false);
    }
}

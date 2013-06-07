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
using EnvDTE90;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.Parsing;
using Microsoft.TC.TestHostAdapters;
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

        /// <summary>
        /// Runs the given file name setting break points at linenos.  Expects to hit the lines
        /// in lineHits as break points in the order provided in lineHits.  If lineHits is negative
        /// expects to hit the positive number and then removes the break point.
        /// </summary>
        internal void BreakpointTest(string filename, int[] linenos, int[] lineHits, string[] conditions = null, bool[] breakWhenChanged = null, 
                                     string cwd = null, string breakFilename = null, bool checkBound = true, bool checkThread = true, string arguments = "", 
                                     Action processLoaded = null, PythonDebugOptions debugOptions = PythonDebugOptions.RedirectOutput,
                                    bool waitForExit = true) {
            var debugger = new PythonDebugger();
            PythonThread thread = null;
            string rootedFilename = filename;
            if (!Path.IsPathRooted(filename)) {
                rootedFilename = DebuggerTestPath + filename;
            }

            var process = DebugProcess(debugger, rootedFilename, (newproc, newthread) => {
                for (int i = 0; i < linenos.Length; i++) {
                    var line = linenos[i];

                    int finalLine = line;
                    if (finalLine < 0) {
                        finalLine = -finalLine;
                    }

                    PythonBreakpoint breakPoint;
                    var finalBreakFilename = breakFilename ?? filename;

                    if (conditions != null) {
                        if (breakWhenChanged != null) {
                            breakPoint = newproc.AddBreakPoint(finalBreakFilename, line, conditions[i], breakWhenChanged[i]);
                        } else {
                            breakPoint = newproc.AddBreakPoint(finalBreakFilename, line, conditions[i]);
                        }
                    } else {
                        breakPoint = AddBreakPoint(newproc, line, finalBreakFilename);
                    }

                    breakPoint.Add();
                }
                thread = newthread;

                if (processLoaded != null) {
                    processLoaded();
                }
            }, cwd: cwd, arguments: arguments);

            process.BreakpointBindFailed += (sender, args) => {
                if (checkBound) {
                    Assert.Fail("unexpected bind failure");
                }
            };

            var lineList = new List<int>(linenos);

            int breakpointBound = 0;
            int breakpointHit = 0;
            process.BreakpointBindSucceeded += (sender, args) => {
                Assert.AreEqual(args.Breakpoint.Filename, breakFilename ?? filename);
                int index = lineList.IndexOf(args.Breakpoint.LineNo);
                Assert.IsTrue(index != -1);
                lineList[index] = -1;
                breakpointBound++;
            };

            AutoResetEvent allBreakpointsHit = new AutoResetEvent(false);
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
                if (checkThread) {
                    Assert.AreEqual(args.Thread, thread);
                }
                if (breakpointHit == lineHits.Length) {
                    allBreakpointsHit.Set();
                }
                process.Continue();
            };

            process.Start();

            if (waitForExit) {
                WaitForExit(process);
            } else {
                allBreakpointsHit.WaitOne(20000);
                process.Terminate();
            }

            Assert.AreEqual(breakpointHit, lineHits.Length);
            if (checkBound) {
                Assert.AreEqual(breakpointBound, linenos.Length);
            }
        }

        internal static PythonBreakpoint AddBreakPoint(PythonProcess newproc, int line, string finalBreakFilename) {
            PythonBreakpoint breakPoint;
            var ext = Path.GetExtension(finalBreakFilename);

            if (String.Equals(ext, ".html", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(ext, ".htm", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(ext, ".djt", StringComparison.OrdinalIgnoreCase)) {
                breakPoint = newproc.AddDjangoBreakPoint(finalBreakFilename, line);
            } else {
                breakPoint = newproc.AddBreakPoint(finalBreakFilename, line);
            }
            return breakPoint;
        }

        internal PythonProcess DebugProcess(PythonDebugger debugger, string filename, Action<PythonProcess, PythonThread> onLoaded = null, string interpreterOptions = null, PythonDebugOptions debugOptions = PythonDebugOptions.RedirectOutput, string cwd = null, string arguments = "") {
            return DebugProcess(debugger, Version, filename, onLoaded, interpreterOptions, debugOptions, cwd, arguments);
        }

        internal static PythonProcess DebugProcess(PythonDebugger debugger, PythonVersion version, string filename, Action<PythonProcess, PythonThread> onLoaded = null, string interpreterOptions = null, PythonDebugOptions debugOptions = PythonDebugOptions.RedirectOutput, string cwd = null, string arguments = "") {
            string fullPath = Path.GetFullPath(filename);
            string dir = cwd ?? Path.GetFullPath(Path.GetDirectoryName(filename));
            if (!String.IsNullOrEmpty(arguments)) {
                arguments = "\"" + fullPath + "\" " + arguments;
            } else {
                arguments = "\"" + fullPath + "\"";
            }
            var process = debugger.CreateProcess(version.Version, version.Path, arguments, dir, "", interpreterOptions, debugOptions);
            process.DebuggerOutput += (sender, args) => {
                Console.WriteLine("{0}: {1}", args.Thread.Id, args.Output);
            };
            process.ProcessLoaded += (sender, args) => {
                if (onLoaded != null) {
                    onLoaded(process, args.Thread);
                }
                process.Resume();
            };

            return process;
        }

        internal void LocalsTest(string filename, int lineNo, string[] paramNames, string[] localsNames, string breakFilename = null, string arguments = "", Action processLoaded = null, PythonDebugOptions debugOptions = PythonDebugOptions.RedirectOutput, bool waitForExit = true) {
            PythonThread thread = RunAndBreak(filename, lineNo, breakFilename: breakFilename, arguments: arguments, processLoaded: processLoaded, debugOptions: debugOptions);
            PythonProcess process = thread.Process;
            try {
                var frames = thread.Frames;
                var localsExpected = new HashSet<string>(localsNames);
                var paramsExpected = new HashSet<string>(paramNames);

                AssertUtil.ContainsExactly(frames[0].Locals.Select(x => x.Expression), localsExpected);
                AssertUtil.ContainsExactly(frames[0].Parameters.Select(x => x.Expression), paramsExpected);
                Assert.AreEqual(frames[0].FileName, breakFilename ?? Path.GetFullPath(DebuggerTestPath + filename), true);

                process.Continue();

                if (waitForExit) {
                    WaitForExit(process);
                } else {
                    process.Terminate();
                }
            } finally {
                if (!process.HasExited) {
                    process.Terminate();
                }
            }
        }

        internal PythonThread RunAndBreak(string filename, int lineNo, string breakFilename = null, string arguments = "", Action processLoaded = null, PythonDebugOptions debugOptions = PythonDebugOptions.RedirectOutput) {
            PythonThread thread;

            var debugger = new PythonDebugger();
            thread = null;
            PythonProcess process = DebugProcess(debugger, DebuggerTestPath + filename, (newproc, newthread) => {
                var breakPoint = AddBreakPoint(newproc, lineNo, breakFilename ?? filename);
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

            process.Start();

            AssertWaited(brkHit);
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
            var debugger = new PythonDebugger();
            if (breakFile == null) {
                breakFile = filename;
            }

            string fullPath = Path.GetFullPath(filename);
            string dir = Path.GetDirectoryName(filename);
            var process = debugger.CreateProcess(Version.Version, Version.Path, "\"" + fullPath + "\" " + (arguments ?? ""), dir, "", null, options);
            PythonThread thread = null;
            process.ThreadCreated += (sender, args) => {
                thread = args.Thread;
            };


            AutoResetEvent processEvent = new AutoResetEvent(false);

            bool processLoad = false, stepComplete = false;
            process.ProcessLoaded += (sender, args) => {
                foreach (var breakLine in breakLines) {
                    var bp = AddBreakPoint(process, breakLine, breakFile);
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
                breakAction[breakHits++](process);
                stepComplete = true;
                processEvent.Set();
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

            if (waitForExit) {
                WaitForExit(process);
            } else {
                process.Terminate();
            }
        }

        internal void WaitForExit(PythonProcess process) {
            bool exited = process.WaitForExit(DefaultWaitForExitTimeout);
            if (!exited) {
                process.Terminate();
                Assert.Fail("Timeout while waiting for Python process to exit.");
            }
        }
    }
}

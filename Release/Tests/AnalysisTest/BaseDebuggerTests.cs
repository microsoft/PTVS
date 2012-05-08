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

namespace AnalysisTest {
    public class BaseDebuggerTests {
        internal virtual string DebuggerTestPath {
            get {
                return @"Python.VS.TestData\DebuggerProject\";
            }
        }

        /// <summary>
        /// Runs the given file name setting break points at linenos.  Expects to hit the lines
        /// in lineHits as break points in the order provided in lineHits.  If lineHits is negative
        /// expects to hit the positive number and then removes the break point.
        /// </summary>
        internal void BreakpointTest(string filename, int[] linenos, int[] lineHits, string[] conditions = null, bool[] breakWhenChanged = null, 
                                     string cwd = null, string breakFilename = null, bool checkBound = true, bool checkThread = true, string arguments = "", 
                                     Action processLoaded = null, PythonDebugOptions deubgOptions = PythonDebugOptions.None,
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
                        
                        var ext = Path.GetExtension(finalBreakFilename);

                        if (String.Equals(ext, ".html", StringComparison.OrdinalIgnoreCase) ||
                            String.Equals(ext, ".htm", StringComparison.OrdinalIgnoreCase) ||
                            String.Equals(ext, ".djt", StringComparison.OrdinalIgnoreCase)) {
                            breakPoint = newproc.AddDjangoBreakPoint(finalBreakFilename, line);
                        } else {
                            breakPoint = newproc.AddBreakPoint(finalBreakFilename, line);
                        }
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
                Assert.AreEqual(args.Breakpoint.Filename, filename);
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
                process.WaitForExit(20000);
            } else {
                allBreakpointsHit.WaitOne(20000);
                process.Terminate();
            }

            Assert.AreEqual(breakpointHit, lineHits.Length);
            if (checkBound) {
                Assert.AreEqual(breakpointBound, linenos.Length);
            }
        }

        internal PythonProcess DebugProcess(PythonDebugger debugger, string filename, Action<PythonProcess, PythonThread> onLoaded = null, string interpreterOptions = null, PythonDebugOptions debugOptions = PythonDebugOptions.None, string cwd = null, string arguments = "") {
            string fullPath = Path.GetFullPath(filename);
            string dir = cwd ?? Path.GetFullPath(Path.GetDirectoryName(filename));
            if (!String.IsNullOrEmpty(arguments)) {
                arguments = "\"" + fullPath + "\" " + arguments;
            } else {
                arguments = "\"" + fullPath + "\"";
            }
            var process = debugger.CreateProcess(Version.Version, Version.Path, arguments, dir, "", interpreterOptions, debugOptions);
            process.ProcessLoaded += (sender, args) => {
                if (onLoaded != null) {
                    onLoaded(process, args.Thread);
                }
                process.Resume();
            };

            return process;
        }

        internal virtual PythonVersion Version {
            get {
                return PythonPaths.Python26;
            }
        }
    }
}

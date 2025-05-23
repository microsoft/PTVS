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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using EnvDTE;
using EnvDTE90;
using EnvDTE90a;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudioTools;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;
using TestUtilities.UI.Python;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using Thread = System.Threading.Thread;

namespace DebuggerUITests {
    public class DebugProjectUITests {
        #region Test Cases

        /// <summary>
        /// Loads the simple project and then unloads it, ensuring that the solution is created with a single project.
        /// </summary>
        public void DebugPythonProject(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                StartHelloWorldAndBreak(app);

                app.Dte.Debugger.Go(WaitForBreakOrEnd: true);
                Assert.AreEqual(dbgDebugMode.dbgDesignMode, app.Dte.Debugger.CurrentMode);
            }
        }

        /// <summary>
        /// Loads a project with the startup file in a subdirectory, ensuring that syspath is correct when debugging.
        /// </summary>
        public void DebugPythonProjectSubFolderStartupFileSysPath(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                var sln = app.CopyProjectForTest(@"TestData\SysPath.sln");
                var project = app.OpenProject(sln);

                ClearOutputWindowDebugPaneText(app);
                app.Dte.ExecuteCommand("Debug.Start");
                WaitForMode(app, dbgDebugMode.dbgDesignMode);

                // sys.path should point to the startup file directory, not the project directory.
                // this matches the behavior of start without debugging.
                // Note: backslashes are escaped in the output
                string testDataPath = Path.Combine(Path.GetDirectoryName(project.FullName), "Sub").Replace("\\", "\\\\");
                WaitForDebugOutput(app, text => text.Contains(testDataPath));
            }
        }

        /// <summary>
        /// Debugs a project when clearing process-wide PYTHONPATH value.
        /// If <see cref="DebugPythonProjectSubFolderStartupFileSysPath"/> fails
        /// this test may also fail.
        /// </summary>
        public void DebugPythonProjectWithClearingPythonPath(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                var sysPathSln = app.CopyProjectForTest(@"TestData\SysPath.sln");
                var helloWorldSln = app.CopyProjectForTest(@"TestData\HelloWorld.sln");
                var testDataPath = Path.Combine(PathUtils.GetParent(helloWorldSln), "HelloWorld").Replace("\\", "\\\\");

                using (new EnvironmentVariableSetter("PYTHONPATH", testDataPath)) {
                    app.OpenProject(sysPathSln);

                    using (new PythonServiceGeneralOptionsSetter(pyService, clearGlobalPythonPath: true)) {
                        ClearOutputWindowDebugPaneText(app);
                        app.Dte.ExecuteCommand("Debug.Start");
                        WaitForMode(app, dbgDebugMode.dbgDesignMode);

                        var outputWindowText = WaitForDebugOutput(app, text => text.Contains("DONE"));
                        Assert.IsFalse(outputWindowText.Contains(testDataPath), outputWindowText);
                    }
                }
            }
        }

        /// <summary>
        /// Debugs a project when not clearing a process-wide PYTHONPATH value.
        /// If <see cref="DebugPythonProjectSubFolderStartupFileSysPath"/> fails
        /// this test may also fail.
        /// </summary>
        public void DebugPythonProjectWithoutClearingPythonPath(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                var sysPathSln = app.CopyProjectForTest(@"TestData\SysPath.sln");
                var helloWorldSln = app.CopyProjectForTest(@"TestData\HelloWorld.sln");
                var testDataPath = Path.Combine(PathUtils.GetParent(helloWorldSln), "HelloWorld").Replace("\\", "\\\\");

                using (new EnvironmentVariableSetter("PYTHONPATH", testDataPath)) {
                    app.OpenProject(sysPathSln);

                    using (new PythonServiceGeneralOptionsSetter(pyService, clearGlobalPythonPath: false)) {
                        ClearOutputWindowDebugPaneText(app);
                        app.Dte.ExecuteCommand("Debug.Start");
                        WaitForMode(app, dbgDebugMode.dbgDesignMode);

                        WaitForDebugOutput(app, text => text.Contains(testDataPath));
                    }
                }
            }
        }

        /// <summary>
        /// Tests using a custom interpreter path that is relative
        /// </summary>
        public void DebugPythonCustomInterpreter(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                var sln = app.CopyProjectForTest(@"TestData\RelativeInterpreterPath.sln");
                var project = app.OpenProject(sln, "Program.py");
                var interpreterFolder = PathUtils.GetParent(sln);
                var interpreterPath = Path.Combine(interpreterFolder, "Interpreter.exe");

                var defaultInterpreter = app.OptionsService.DefaultInterpreter;
                File.Copy(defaultInterpreter.Configuration.InterpreterPath, interpreterPath, true);
                if (defaultInterpreter.Configuration.Version >= new Version(3, 0)) {
                    foreach (var sourceDll in FileUtils.EnumerateFiles(defaultInterpreter.Configuration.GetPrefixPath(), "python*.dll", recurse: false)) {
                        var targetDll = Path.Combine(interpreterFolder, Path.GetFileName(sourceDll));
                        File.Copy(sourceDll, targetDll, true);
                    }
                }

                app.Dte.Debugger.Breakpoints.Add(File: "Program.py", Line: 1);
                app.Dte.ExecuteCommand("Debug.Start");

                WaitForMode(app, dbgDebugMode.dbgBreakMode);

                Assert.IsNotNull(app.Dte.Debugger.BreakpointLastHit);
                Assert.AreEqual(1, app.Dte.Debugger.BreakpointLastHit.FileLine);

                app.Dte.Debugger.Go(WaitForBreakOrEnd: true);
                Assert.AreEqual(dbgDebugMode.dbgDesignMode, app.Dte.Debugger.CurrentMode);
            }
        }

        /// <summary>
        /// Tests using a custom interpreter path that doesn't exist
        /// </summary>
        public void DebugPythonCustomInterpreterMissing(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                var sln = app.CopyProjectForTest(@"TestData\RelativeInterpreterPath.sln");
                var project = app.OpenProject(sln, "Program.py");
                var interpreterPath = Path.Combine(PathUtils.GetParent(sln), "Interpreter.exe");

                app.Dte.ExecuteCommand("Debug.Start");

                string expectedMissingInterpreterText = string.Format(
                    "The project cannot be launched because no Python interpreter is available at \"{0}\". Please check the " +
                    "Python Environments window and ensure the version of Python is installed and has all settings specified.",
                    interpreterPath);
                var dialog = app.WaitForDialog();
                app.CheckMessageBox(expectedMissingInterpreterText);
            }
        }

        public void PendingBreakPointLocation(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                var sln = app.CopyProjectForTest(@"TestData\DebuggerProject.sln");
                var project = app.OpenProject(sln, "BreakpointInfo.py");
                var bpInfo = project.ProjectItems.Item("BreakpointInfo.py");

                // LSC
                //project.GetPythonProject().GetAnalyzer().WaitForCompleteAnalysis(x => true);

                var bp = app.Dte.Debugger.Breakpoints.Add(File: "BreakpointInfo.py", Line: 2);
                Assert.AreEqual("Python", bp.Item(1).Language);
                // FunctionName doesn't get queried for when adding the BP via EnvDTE, so we can't assert here :(
                //Assert.AreEqual("BreakpointInfo.C", bp.Item(1).FunctionName);
                bp = app.Dte.Debugger.Breakpoints.Add(File: "BreakpointInfo.py", Line: 3);
                Assert.AreEqual("Python", bp.Item(1).Language);
                //Assert.AreEqual("BreakpointInfo.C.f", bp.Item(1).FunctionName);
                bp = app.Dte.Debugger.Breakpoints.Add(File: "BreakpointInfo.py", Line: 6);
                Assert.AreEqual("Python", bp.Item(1).Language);
                //Assert.AreEqual("BreakpointInfo", bp.Item(1).FunctionName);
                bp = app.Dte.Debugger.Breakpoints.Add(File: "BreakpointInfo.py", Line: 7);
                Assert.AreEqual("Python", bp.Item(1).Language);
                //Assert.AreEqual("BreakpointInfo.f", bp.Item(1).FunctionName);

                // https://github.com/Microsoft/PTVS/pull/630
                // Make sure 
            }
        }

        public void BoundBreakpoint(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                var project = OpenDebuggerProjectAndBreak(app, "BreakpointInfo.py", 2);

                var pendingBp = (Breakpoint3)app.Dte.Debugger.Breakpoints.Item(1);
                Assert.AreEqual(1, pendingBp.Children.Count);

                var bp = (Breakpoint3)pendingBp.Children.Item(1);
                Assert.AreEqual("Python", bp.Language);
                Assert.AreEqual(Path.Combine(Path.GetDirectoryName(project.FullName), "BreakpointInfo.py"), bp.File);
                Assert.AreEqual(2, bp.FileLine);
                Assert.AreEqual(1, bp.FileColumn);
                Assert.AreEqual(true, bp.Enabled);
                Assert.AreEqual(true, bp.BreakWhenHit);

                if (!useVsCodeDebugger) {
                    // Retreiving hit condition info for a breakpoint is not supported by VSCode protocol
                    // Note: this is NOT hit condition feature
                    Assert.AreEqual(1, bp.CurrentHits);
                    Assert.AreEqual(1, bp.HitCountTarget);
                    Assert.AreEqual(dbgHitCountType.dbgHitCountTypeNone, bp.HitCountType);
                }

                // Resetting BreakWhenHit without a message set throws a ComException, see
                // https://stackoverflow.com/questions/27753513/visual-studio-sdk-breakpoint2-breakwhenhit-true-throws-exception-0x8971101a
                pendingBp.Message = "foo";
                pendingBp.BreakWhenHit = false; // causes rebind
                Assert.AreEqual(1, pendingBp.Children.Count);
                bp = (Breakpoint3)pendingBp.Children.Item(1);
                Assert.AreEqual(false, bp.BreakWhenHit);
            }
        }

        public void Step(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                var project = OpenDebuggerProjectAndBreak(app, "SteppingTest.py", 1);
                app.Dte.Debugger.StepOver(true);
                WaitForMode(app, dbgDebugMode.dbgBreakMode);

                Assert.AreEqual((uint)2, ((StackFrame2)app.Dte.Debugger.CurrentStackFrame).LineNumber);

                app.Dte.Debugger.TerminateAll();

                WaitForMode(app, dbgDebugMode.dbgDesignMode);
            }
        }

        public void Step3(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                var project = OpenDebuggerProjectAndBreak(app, "SteppingTest3.py", 2);
                app.Dte.Debugger.StepOut(true);
                WaitForMode(app, dbgDebugMode.dbgBreakMode);

                Assert.AreEqual((uint)5, ((StackFrame2)app.Dte.Debugger.CurrentStackFrame).LineNumber);

                app.Dte.Debugger.TerminateAll();

                WaitForMode(app, dbgDebugMode.dbgDesignMode);
            }
        }

        public void Step5(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                var project = OpenDebuggerProjectAndBreak(app, "SteppingTest5.py", 5);
                app.Dte.Debugger.StepInto(true);
                WaitForMode(app, dbgDebugMode.dbgBreakMode);

                Assert.AreEqual((uint)2, ((StackFrame2)app.Dte.Debugger.CurrentStackFrame).LineNumber);

                app.Dte.Debugger.TerminateAll();

                WaitForMode(app, dbgDebugMode.dbgDesignMode);
            }
        }

        public void StepMultiProc(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                var project = OpenDebuggerProjectAndBreak(app, "SteppingTest8.py", 14);
                app.Dte.Debugger.StepOver(true);
                WaitForMode(app, dbgDebugMode.dbgBreakMode);

                Assert.AreEqual((uint)16, ((StackFrame2)app.Dte.Debugger.CurrentStackFrame).LineNumber);

                app.Dte.Debugger.TerminateAll();

                WaitForMode(app, dbgDebugMode.dbgDesignMode);
            }
        }

        public void SetNextLine(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                var project = OpenDebuggerProjectAndBreak(app, "SetNextLine.py", 7);

                var doc = app.Dte.Documents.Item("SetNextLine.py");
                ((TextSelection)doc.Selection).GotoLine(8);
                ((TextSelection)doc.Selection).EndOfLine(false);
                var curLine = ((TextSelection)doc.Selection).CurrentLine;

                app.Dte.Debugger.SetNextStatement();
                app.Dte.Debugger.StepOver(true);
                WaitForMode(app, dbgDebugMode.dbgBreakMode);
                Assert.AreEqual((uint)9, ((StackFrame2)app.Dte.Debugger.CurrentStackFrame).LineNumber);

                var curFrame = app.Dte.Debugger.CurrentStackFrame;
                if (useVsCodeDebugger) {
                    var locals = new List<Expression>();
                    foreach (Expression e in curFrame.Locals) {
                        locals.Add(e);
                    }

                    var local = locals.Single(e => e.Name == "y");
                    Assert.AreEqual("100", local.Value);
                    try {
                        locals.Single(e => e.Name == "x");
                        Assert.Fail("Expected exception, x should not be defined");
                    } catch {
                    }

                } else {
                    var local = curFrame.Locals.Item("y");
                    Assert.AreEqual("100", local.Value);
                    try {
                        curFrame.Locals.Item("x");
                        Assert.Fail("Expected exception, x should not be defined");
                    } catch {
                    }
                }

                app.Dte.Debugger.TerminateAll();

                WaitForMode(app, dbgDebugMode.dbgDesignMode);
            }
        }

        /*
        //[TestMethod, Priority(UITestPriority.P0)]
        //[TestCategory("Installed")]
        public void TestBreakAll() {
            var project = OpenDebuggerProjectAndBreak("BreakAllTest.py", 1);

            app.Dte.Debugger.Go(false);


            WaitForMode(app, dbgDebugMode.dbgRunMode);

            Thread.Sleep(2000);

            app.Dte.Debugger.Break();

            WaitForMode(app, dbgDebugMode.dbgBreakMode);

            var lineNo = ((StackFrame2)app.Dte.Debugger.CurrentStackFrame).LineNumber;
            Assert.IsTrue(lineNo == 1 || lineNo == 2);

            app.Dte.Debugger.Go(false);

            WaitForMode(app, dbgDebugMode.dbgRunMode);

            app.Dte.Debugger.TerminateAll();

            WaitForMode(app, dbgDebugMode.dbgDesignMode);
        }*/

        /// <summary>
        /// Loads the simple project and then terminates the process while we're at a breakpoint.
        /// </summary>
        public void TerminateProcess(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                StartHelloWorldAndBreak(app);

                Assert.AreEqual(dbgDebugMode.dbgBreakMode, app.Dte.Debugger.CurrentMode);
                Assert.AreEqual(1, app.Dte.Debugger.BreakpointLastHit.FileLine);

                app.Dte.Debugger.TerminateAll();

                WaitForMode(app, dbgDebugMode.dbgDesignMode);
            }
        }

        /// <summary>
        /// Loads the simple project and makes sure we get the correct module.
        /// </summary>
        public void EnumModules(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                StartHelloWorldAndBreak(app);

                var modules = ((Process3)app.Dte.Debugger.CurrentProcess).Modules;
                Assert.IsTrue(modules.Count >= 1);

                var module = modules.Item("__main__");
                Assert.IsNotNull(module);

                Assert.IsTrue(module.Path.EndsWith("Program.py", StringComparison.OrdinalIgnoreCase));
                Assert.AreEqual("__main__", module.Name);
                Assert.AreNotEqual((uint)0, module.Order);

                app.Dte.Debugger.TerminateAll();
                WaitForMode(app, dbgDebugMode.dbgDesignMode);
            }
        }

        public void MainThread(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                StartHelloWorldAndBreak(app);

                var thread = ((Thread2)app.Dte.Debugger.CurrentThread);
                Assert.AreEqual("MainThread", thread.Name);
                Assert.AreEqual(0, thread.SuspendCount);
                Assert.AreEqual("Normal", thread.Priority);
                Assert.AreEqual("MainThread", thread.DisplayName);
                thread.DisplayName = "Hi";
                Assert.AreEqual("Hi", thread.DisplayName);

                app.Dte.Debugger.TerminateAll();
                WaitForMode(app, dbgDebugMode.dbgDesignMode);
            }
        }

        public void ExpressionEvaluation(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                OpenDebuggerProject(app, "Program.py");

                app.Dte.Debugger.Breakpoints.Add(File: "Program.py", Line: 14);
                app.Dte.ExecuteCommand("Debug.Start");

                WaitForMode(app, dbgDebugMode.dbgBreakMode);

                Assert.AreEqual(14, app.Dte.Debugger.BreakpointLastHit.FileLine);

                Assert.AreEqual("i", app.Dte.Debugger.GetExpression("i").Name);
                Assert.AreEqual("42", app.Dte.Debugger.GetExpression("i").Value);
                Assert.AreEqual("int", app.Dte.Debugger.GetExpression("i").Type);
                Assert.IsTrue(app.Dte.Debugger.GetExpression("i").IsValidValue);
                Assert.AreEqual(0, app.Dte.Debugger.GetExpression("i").DataMembers.Count);

                var curFrame = app.Dte.Debugger.CurrentStackFrame;

                if (useVsCodeDebugger) {
                    var locals = new List<Expression>();
                    foreach (Expression e in curFrame.Locals) {
                        locals.Add(e);
                    }

                    var local = locals.Single(e => e.Name == "i");
                    Assert.AreEqual("42", local.Value);

                    local = locals.Single(e => e.Name == "l");
                    // Experimental debugger includes methods + values now, and that's different on Python 2 and 3
                    Assert.AreEqual(interpreter.Contains("Python27") ? 49 : 50, local.DataMembers.Count);

                    // TODO: re-enable this when the sorting of list members is corrected
                    // (right now it's methods followed by values)
                    //Assert.AreEqual("0", local.DataMembers.Item(1).Name);

                    // TODO: Uncomment line after this is done
                    // https://github.com/Microsoft/ptvsd/issues/316
                    // Assert.AreEqual("Program", ((StackFrame2)curFrame).Module);

                    // TODO: Experimental debugger does not support separating locals and arguments
                    // Assert.AreEqual(3, ((StackFrame2)curFrame).Arguments.Count);
                    // Assert.AreEqual("a", ((StackFrame2)curFrame).Arguments.Item(1).Name);
                    // Assert.AreEqual("2", ((StackFrame2)curFrame).Arguments.Item(1).Value);

                    // The result of invalid expressions is a error message in the experimental debugger
                    var invalidExpr = ((Debugger3)app.Dte.Debugger).GetExpression("invalid expression");
                    Assert.IsTrue(invalidExpr.Value.StartsWith("SyntaxError"));
                    // Experimental debugger treats the request for evalautions as succeeded. Any errors
                    // such as syntax errors are reported as valid results. A failure indicates that the debugger
                    // failed to handle the request.
                    Assert.IsTrue(invalidExpr.IsValidValue);

                } else {
                    var local = curFrame.Locals.Item("i");
                    Assert.AreEqual("42", local.Value);
                    Assert.AreEqual(3, curFrame.Locals.Item("l").DataMembers.Count);
                    Assert.AreEqual("[0]", curFrame.Locals.Item("l").DataMembers.Item(1).Name);
                    Assert.AreEqual("Program", ((StackFrame2)curFrame).Module);

                    Assert.AreEqual(3, ((StackFrame2)curFrame).Arguments.Count);
                    Assert.AreEqual("a", ((StackFrame2)curFrame).Arguments.Item(1).Name);
                    Assert.AreEqual("2", ((StackFrame2)curFrame).Arguments.Item(1).Value);

                    var invalidExpr = ((Debugger3)app.Dte.Debugger).GetExpression("invalid expression");
                    var str = invalidExpr.Value;
                    Assert.IsFalse(invalidExpr.IsValidValue);
                }

                Assert.AreEqual("f", curFrame.FunctionName);
                Assert.IsTrue(((StackFrame2)curFrame).FileName.EndsWith("Program.py"));
                Assert.AreEqual((uint)14, ((StackFrame2)curFrame).LineNumber);

                var expr = ((Debugger3)app.Dte.Debugger).GetExpression("l[0] + l[1]");
                Assert.AreEqual("l[0] + l[1]", expr.Name);
                Assert.AreEqual("5", expr.Value);

                app.Dte.Debugger.ExecuteStatement("x = 2");

                app.Dte.Debugger.TerminateAll();
                WaitForMode(app, dbgDebugMode.dbgDesignMode);
            }
        }

        public void SimpleException(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                string exceptionDescription = useVsCodeDebugger ? "exception: no description" : "Exception";
                ExceptionTest(app, "SimpleException.py", exceptionDescription, "Exception", 3);
            }
        }

        public void SimpleException2(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                string exceptionDescription = useVsCodeDebugger ? "bad value" : "ValueError: bad value";
                ExceptionTest(app, "SimpleException2.py", exceptionDescription, "ValueError", 3);
            }
        }

        public void SimpleExceptionUnhandled(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, waitOnAbnormalExit: false, useLegacyDebugger: !useVsCodeDebugger)) {
                string exceptionDescription = useVsCodeDebugger ? "bad value" : "ValueError: bad value";
                ExceptionTest(app, "SimpleExceptionUnhandled.py", exceptionDescription, "ValueError", 2, true);
            }
        }

        // https://github.com/Microsoft/PTVS/issues/275
        public void ExceptionInImportLibNotReported(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger))
            using (new DebuggingGeneralOptionsSetter(app.Dte, enableJustMyCode: true)) {
                OpenDebuggerProjectAndBreak(app, "ImportLibException.py", 2);
                app.Dte.Debugger.Go(WaitForBreakOrEnd: true);
                Assert.AreEqual(dbgDebugMode.dbgDesignMode, app.Dte.Debugger.CurrentMode);
            }
        }

        public void Breakpoints(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                OpenDebuggerProjectAndBreak(app, "BreakpointTest2.py", 3);
                var debug3 = (Debugger3)app.Dte.Debugger;
                Assert.AreEqual((uint)3, ((StackFrame2)debug3.CurrentThread.StackFrames.Item(1)).LineNumber);
                debug3.Go(true);
                Assert.AreEqual((uint)3, ((StackFrame2)debug3.CurrentThread.StackFrames.Item(1)).LineNumber);
                Assert.IsTrue(debug3.Breakpoints.Item(1).Enabled);
                debug3.Breakpoints.Item(1).Delete();
                debug3.Go(true);

                WaitForMode(app, dbgDebugMode.dbgDesignMode);
            }
        }

        public void BreakpointsDisable(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                OpenDebuggerProjectAndBreak(app, "BreakpointTest4.py", 2);
                var debug3 = (Debugger3)app.Dte.Debugger;
                Assert.AreEqual((uint)2, ((StackFrame2)debug3.CurrentThread.StackFrames.Item(1)).LineNumber);
                if (useVsCodeDebugger) {
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
                debug3.Go(true);
                Assert.AreEqual((uint)2, ((StackFrame2)debug3.CurrentThread.StackFrames.Item(1)).LineNumber);
                Assert.IsTrue(debug3.Breakpoints.Item(1).Enabled);
                debug3.Breakpoints.Item(1).Enabled = false;
                if (useVsCodeDebugger) {
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
                debug3.Go(true);

                WaitForMode(app, dbgDebugMode.dbgDesignMode);
            }
        }

        public void BreakpointsDisableReenable(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                var debug3 = (Debugger3)app.Dte.Debugger;
                OpenDebuggerProjectAndBreak(app, "BreakpointTest4.py", 2);
                Assert.AreEqual((uint)2, ((StackFrame2)debug3.CurrentThread.StackFrames.Item(1)).LineNumber);
                debug3.Go(true);
                Assert.AreEqual((uint)2, ((StackFrame2)debug3.CurrentThread.StackFrames.Item(1)).LineNumber);
                int bpCount = debug3.Breakpoints.Count;

                Assert.AreEqual(1, bpCount);
                Assert.IsTrue(debug3.Breakpoints.Item(1).Enabled);
                Assert.AreEqual(2, debug3.Breakpoints.Item(1).FileLine);
                debug3.Breakpoints.Item(1).Enabled = false;

                debug3.Breakpoints.Add(File: "BreakpointTest4.py", Line: 4);
                debug3.Breakpoints.Add(File: "BreakpointTest4.py", Line: 5);
                Assert.AreEqual(4, debug3.Breakpoints.Item(2).FileLine);
                Assert.AreEqual(5, debug3.Breakpoints.Item(3).FileLine);

                // line 4
                debug3.Go(true);
                WaitForMode(app, dbgDebugMode.dbgBreakMode);
                Assert.AreEqual((uint)4, ((StackFrame2)debug3.CurrentThread.StackFrames.Item(1)).LineNumber);

                // line 5
                debug3.Go(true);
                WaitForMode(app, dbgDebugMode.dbgBreakMode);
                Assert.AreEqual((uint)5, ((StackFrame2)debug3.CurrentThread.StackFrames.Item(1)).LineNumber);
                debug3.Breakpoints.Item(3).Enabled = false;

                // back to line 4
                debug3.Go(true);
                WaitForMode(app, dbgDebugMode.dbgBreakMode);
                Assert.AreEqual((uint)4, ((StackFrame2)debug3.CurrentThread.StackFrames.Item(1)).LineNumber);

                debug3.Go(true);
                WaitForMode(app, dbgDebugMode.dbgBreakMode);
                Assert.AreEqual((uint)4, ((StackFrame2)debug3.CurrentThread.StackFrames.Item(1)).LineNumber);

                debug3.Breakpoints.Item(2).Enabled = false;
                debug3.Breakpoints.Item(3).Enabled = true;

                // back to line 5
                debug3.Go(true);
                WaitForMode(app, dbgDebugMode.dbgBreakMode);
                Assert.AreEqual((uint)5, ((StackFrame2)debug3.CurrentThread.StackFrames.Item(1)).LineNumber);
                debug3.Breakpoints.Item(3).Enabled = false;

                // all disabled, run to completion
                debug3.Go(true);
                WaitForMode(app, dbgDebugMode.dbgDesignMode);
            }
        }

        /// <summary>
        /// Make sure the presence of errors causes F5 to prevent running w/o a confirmation.
        /// </summary>
        public void LaunchWithErrorsDontRun(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger, promptBeforeRunningWithBuildErrorSetting: true)) {
                var sln = app.CopyProjectForTest(@"TestData\ErrorProject.sln");
                var project = app.OpenProject(sln);
                var projectDir = PathUtils.GetParent(project.FullName);

                // Open a file with errors
                string scriptFilePath = Path.Combine(projectDir, "Program.py");
                app.Dte.ItemOperations.OpenFile(scriptFilePath);
                app.Dte.ExecuteCommand("View.ErrorList");
                var items = app.WaitForErrorListItems(7);

                var debug3 = (Debugger3)app.Dte.Debugger;
                debug3.Go(true);

                var dialog = new PythonLaunchWithErrorsDialog(app.WaitForDialog());
                dialog.No();

                // make sure we don't go into debug mode
                for (int i = 0; i < 10; i++) {
                    Assert.AreEqual(dbgDebugMode.dbgDesignMode, app.Dte.Debugger.CurrentMode);
                    System.Threading.Thread.Sleep(100);
                }

                WaitForMode(app, dbgDebugMode.dbgDesignMode);
            }
        }

        /// <summary>
        /// Start with debugging, with script but no project.
        /// </summary>
        public void StartWithDebuggingNoProject(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                string scriptFilePath = TestData.GetPath(@"TestData\HelloWorld\Program.py");

                app.DeleteAllBreakPoints();

                app.Dte.ItemOperations.OpenFile(scriptFilePath);
                app.Dte.Debugger.Breakpoints.Add(File: scriptFilePath, Line: 1);
                app.Dte.ExecuteCommand("Python.StartWithDebugging");
                WaitForMode(app, dbgDebugMode.dbgBreakMode);
                Assert.AreEqual(dbgDebugMode.dbgBreakMode, app.Dte.Debugger.CurrentMode);
                Assert.IsNotNull(app.Dte.Debugger.BreakpointLastHit);
                Assert.AreEqual("Program.py, line 1 character 1", app.Dte.Debugger.BreakpointLastHit.Name);
                app.Dte.Debugger.Go(WaitForBreakOrEnd: true);
                Assert.AreEqual(dbgDebugMode.dbgDesignMode, app.Dte.Debugger.CurrentMode);
            }
        }

        /// <summary>
        /// Start without debugging, with script but no project.
        /// </summary>
        public void StartWithoutDebuggingNoProject(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                var tempFolder = TestData.GetTempPath();
                string scriptFilePath = Path.Combine(tempFolder, "CreateFile1.py");
                string resultFilePath = Path.Combine(tempFolder, "File1.txt");
                File.Copy(TestData.GetPath(@"TestData\CreateFile1.py"), scriptFilePath, true);

                app.DeleteAllBreakPoints();

                app.Dte.ItemOperations.OpenFile(scriptFilePath);
                app.Dte.Debugger.Breakpoints.Add(File: scriptFilePath, Line: 1);
                app.Dte.ExecuteCommand("Python.StartWithoutDebugging");
                Assert.AreEqual(dbgDebugMode.dbgDesignMode, app.Dte.Debugger.CurrentMode);
                WaitForFileCreatedByScript(resultFilePath);
            }
        }

        /// <summary>
        /// Start with debugging, with script not in project.
        /// </summary>
        public void StartWithDebuggingNotInProject(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                string scriptFilePath = TestData.GetPath(@"TestData\HelloWorld\Program.py");

                OpenDebuggerProject(app);

                app.Dte.ItemOperations.OpenFile(scriptFilePath);
                app.Dte.Debugger.Breakpoints.Add(File: scriptFilePath, Line: 1);                
                app.Dte.ExecuteCommand("Python.StartWithDebugging");
                WaitForMode(app, dbgDebugMode.dbgBreakMode);
                Assert.AreEqual(dbgDebugMode.dbgBreakMode, app.Dte.Debugger.CurrentMode);
                Assert.AreEqual("Program.py, line 1 character 1", app.Dte.Debugger.BreakpointLastHit.Name);
                app.Dte.Debugger.Go(WaitForBreakOrEnd: true);
                Assert.AreEqual(dbgDebugMode.dbgDesignMode, app.Dte.Debugger.CurrentMode);
            }
        }

        /// <summary>
        /// Start without debugging, with script not in project.
        /// </summary>
        public void StartWithoutDebuggingNotInProject(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                var tempFolder = TestData.GetTempPath();
                string scriptFilePath = Path.Combine(tempFolder, "CreateFile2.py");
                string resultFilePath = Path.Combine(tempFolder, "File2.txt");
                File.Copy(TestData.GetPath(@"TestData\CreateFile2.py"), scriptFilePath, true);

                OpenDebuggerProject(app);

                app.Dte.ItemOperations.OpenFile(scriptFilePath);
                app.Dte.Debugger.Breakpoints.Add(File: scriptFilePath, Line: 1);
                app.Dte.ExecuteCommand("Python.StartWithoutDebugging");
                Assert.AreEqual(dbgDebugMode.dbgDesignMode, app.Dte.Debugger.CurrentMode);
                WaitForFileCreatedByScript(resultFilePath);
            }
        }

        /// <summary>
        /// Start with debuggging, with script in project.
        /// </summary>
        public void StartWithDebuggingInProject(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                var proj = OpenDebuggerProject(app);
                var projDir = PathUtils.GetParent(proj.FullName);
                var scriptFilePath = Path.Combine(projDir, "Program.py");

                app.Dte.ItemOperations.OpenFile(scriptFilePath);
                app.Dte.Debugger.Breakpoints.Add(File: scriptFilePath, Line: 1);
                app.Dte.ExecuteCommand("Python.StartWithDebugging");
                WaitForMode(app, dbgDebugMode.dbgBreakMode);
                Assert.AreEqual(dbgDebugMode.dbgBreakMode, app.Dte.Debugger.CurrentMode);
                Assert.AreEqual("Program.py, line 1 character 1", app.Dte.Debugger.BreakpointLastHit.Name);
                app.Dte.Debugger.Go(WaitForBreakOrEnd: true);
                Assert.AreEqual(dbgDebugMode.dbgDesignMode, app.Dte.Debugger.CurrentMode);
            }
        }

        /// <summary>
        /// Start with debuggging, with script in subfolder project.
        /// </summary>
        public void StartWithDebuggingSubfolderInProject(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                var proj = OpenDebuggerProject(app);
                var projDir = PathUtils.GetParent(proj.FullName);
                var scriptFilePath = Path.Combine(projDir, "Sub", "paths.py");
                var expectedProjDir = "'" + PathUtils.TrimEndSeparator(projDir).Replace("\\", "\\\\") + "'";

                app.Dte.ItemOperations.OpenFile(scriptFilePath);
                app.Dte.Debugger.Breakpoints.Add(File: scriptFilePath, Line: 3);
                app.Dte.ExecuteCommand("Python.StartWithDebugging");
                WaitForMode(app, dbgDebugMode.dbgBreakMode);
                Assert.AreEqual(dbgDebugMode.dbgBreakMode, app.Dte.Debugger.CurrentMode);
                AssertUtil.ContainsAtLeast(
                    app.Dte.Debugger.GetExpression("sys.path").DataMembers.Cast<Expression>().Select(e => e.Value),
                    expectedProjDir
                );
                Assert.AreEqual(
                    expectedProjDir,
                    app.Dte.Debugger.GetExpression("os.path.abspath(os.curdir)").Value
                );
                app.Dte.Debugger.Go(WaitForBreakOrEnd: true);
                Assert.AreEqual(dbgDebugMode.dbgDesignMode, app.Dte.Debugger.CurrentMode);
            }
        }

        /// <summary>
        /// Start without debuggging, with script in project.
        /// </summary>
        public void StartWithoutDebuggingInProject(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                var proj = OpenDebuggerProject(app);
                var projDir = PathUtils.GetParent(proj.FullName);
                var scriptFilePath = Path.Combine(projDir, "CreateFile3.py");
                var resultFilePath = Path.Combine(projDir, "File3.txt");

                app.Dte.ItemOperations.OpenFile(scriptFilePath);
                app.Dte.Debugger.Breakpoints.Add(File: scriptFilePath, Line: 1);
                app.Dte.ExecuteCommand("Python.StartWithoutDebugging");
                Assert.AreEqual(dbgDebugMode.dbgDesignMode, app.Dte.Debugger.CurrentMode);
                WaitForFileCreatedByScript(resultFilePath);
            }
        }

        /// <summary>
        /// Start with debugging, no script.
        /// </summary>
        public void StartWithDebuggingNoScript(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                try {
                    app.ExecuteCommand("Python.StartWithDebugging");
                } catch (COMException e) {
                    // Requires an opened python file with focus
                    Assert.IsTrue(e.ToString().Contains("is not available"));
                }
            }
        }

        /// <summary>
        /// Start without debugging, no script.
        /// </summary>
        public void StartWithoutDebuggingNoScript(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                try {
                    app.ExecuteCommand("Python.StartWithoutDebugging");
                } catch (COMException e) {
                    // Requires an opened python file with focus
                    Assert.IsTrue(e.ToString().Contains("is not available"));
                }
            }
        }

        public void WebProjectLauncherNoStartupFile(PythonVisualStudioApp app, bool useVsCodeDebugger, string interpreter, DotNotWaitOnNormalExit optionSetter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (SelectDefaultInterpreter(app, interpreter))
            using (new PythonOptionsSetter(app.Dte, useLegacyDebugger: !useVsCodeDebugger)) {
                var project = app.CreateProject(
                    PythonVisualStudioApp.TemplateLanguageName,
                    PythonVisualStudioApp.EmptyWebProjectTemplate,
                    TestData.GetTempPath(),
                    "NewWebProject"
                );

                foreach (var cmd in new[] { "Debug.Start", "Debug.StartWithoutDebugging" }) {
                    app.Dte.ExecuteCommand(cmd);
                    app.CheckMessageBox("The project cannot be launched because the startup file is not specified.");
                }
            }
        }

        #endregion

        #region Helpers

        public class DotNotWaitOnNormalExit : PythonOptionsSetter {
            public DotNotWaitOnNormalExit(DTE dte) :
                base(dte, waitOnNormalExit: false) {
            }
        }

        private static void WaitForFileCreatedByScript(string createdFilePath) {
            bool exists = false;
            for (int i = 0; i < 10; i++) {
                exists = File.Exists(createdFilePath);
                if (exists) {
                    break;
                }
                System.Threading.Thread.Sleep(250);
            }

            Assert.IsTrue(exists, "Python script was expected to create file '{0}'.", createdFilePath);
        }

        private static void ExceptionTest(PythonVisualStudioApp app, string filename, string expectedDescription, string exceptionType, int expectedLine, bool isUnhandled=false) {
            var debug3 = (Debugger3)app.Dte.Debugger;
            using (new DebuggingGeneralOptionsSetter(app.Dte, enableJustMyCode: true)) {
                OpenDebuggerProject(app, filename);

                var exceptionSettings = debug3.ExceptionGroups.Item("Python Exceptions");

                if (!isUnhandled) {
                    exceptionSettings.SetBreakWhenThrown(true, exceptionSettings.Item(exceptionType));
                }

                app.Dte.ExecuteCommand("Debug.Start");
                WaitForMode(app, dbgDebugMode.dbgBreakMode);

                exceptionSettings.SetBreakWhenThrown(false, exceptionSettings.Item(exceptionType));
                exceptionSettings.SetBreakWhenThrown(true, exceptionSettings.Item(exceptionType));
                debug3.ExceptionGroups.ResetAll();

                var excepAdorner = app.WaitForExceptionAdornment();
                AutomationWrapper.DumpElement(excepAdorner.Element);

                Assert.AreEqual(expectedDescription, excepAdorner.Description.TrimEnd());

                Assert.AreEqual((uint)expectedLine, ((StackFrame2)debug3.CurrentThread.StackFrames.Item(1)).LineNumber);

                debug3.Go(WaitForBreakOrEnd: true);

                WaitForMode(app, dbgDebugMode.dbgDesignMode);
            }
        }

        internal static Project OpenDebuggerProject(VisualStudioApp app, string startItem = null) {
            var solutionPath = app.CopyProjectForTest(@"TestData\DebuggerProject.sln");
            return app.OpenProject(solutionPath, startItem);
        }

        private static Project OpenDebuggerProjectAndBreak(VisualStudioApp app, string startItem, int lineNo, bool setStartupItem = true) {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PTVS_TEST_DEBUGADAPTER_LOGGING_ENABLED"))) {
                app.ExecuteCommand("DebugAdapterHost.Logging /On");
            }
            return OpenProjectAndBreak(app, @"TestData\DebuggerProject.sln", startItem, lineNo);
        }

        private static void ClearOutputWindowDebugPaneText(VisualStudioApp app) {
            OutputWindow window = ((EnvDTE80.DTE2)app.Dte).ToolWindows.OutputWindow;
            OutputWindowPane debugPane = window.OutputWindowPanes.Item("Debug");
            debugPane.Clear();
        }

        private static string WaitForDebugOutput(VisualStudioApp app, Predicate<string> condition) {
            var uiThread = app.ServiceProvider.GetUIThread();
            var text = uiThread.Invoke(() => app.GetOutputWindowText("Debug"));
            for (int i = 0; i < 50 && !condition(text); i++) {
                Thread.Sleep(100);
                text = uiThread.Invoke(() => app.GetOutputWindowText("Debug"));
            }

            Assert.IsTrue(condition(text));
            return text;
        }

        private static void StartHelloWorldAndBreak(VisualStudioApp app) {
            OpenProjectAndBreak(app, @"TestData\HelloWorld.sln", "Program.py", 1);
        }

        internal static Project OpenProjectAndBreak(VisualStudioApp app, string projName, string filename, int lineNo, bool setStartupItem = true) {
            var projectPath = app.CopyProjectForTest(projName);
            var project = app.OpenProject(projectPath, filename, setStartupItem: setStartupItem);

            app.Dte.Debugger.Breakpoints.Add(File: filename, Line: lineNo);
            app.Dte.ExecuteCommand("Debug.Start");

            WaitForMode(app, dbgDebugMode.dbgBreakMode);

            Assert.IsNotNull(app.Dte.Debugger.BreakpointLastHit);
            Assert.AreEqual(lineNo, app.Dte.Debugger.BreakpointLastHit.FileLine);
            return project;
        }

        internal static void WaitForMode(VisualStudioApp app, dbgDebugMode mode) {
            for (int i = 0; i < 30 && app.Dte.Debugger.CurrentMode != mode; i++) {
                Thread.Sleep(1000);
            }

            Assert.AreEqual(mode, app.Dte.Debugger.CurrentMode);
        }

        class EmptyDisposable : IDisposable {
            public void Dispose() {
            }
        }

        private static IDisposable SelectDefaultInterpreter(PythonVisualStudioApp app, string pythonVersion) {
            if (string.IsNullOrEmpty(pythonVersion)) {
                // Test wants to use the existing global default
                return new EmptyDisposable();
            }

            return app.SelectDefaultInterpreter(FindInterpreter(pythonVersion));
        }

        private static PythonVersion FindInterpreter(string pythonVersion) {
            var interpreter = PythonPaths.GetVersionsByName(pythonVersion).FirstOrDefault();
            if (interpreter == null) {
                Assert.Inconclusive($"Interpreter '{pythonVersion}' not installed.");
            }

            return interpreter;
        }
        #endregion

    }
}
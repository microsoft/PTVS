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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using EnvDTE;
using EnvDTE90;
using EnvDTE90a;
using Microsoft.PythonTools;
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
        public void DebugPythonProject(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
            StartHelloWorldAndBreak(app);

            app.Dte.Debugger.Go(WaitForBreakOrEnd: true);
            Assert.AreEqual(dbgDebugMode.dbgDesignMode, app.Dte.Debugger.CurrentMode);
        }

        /// <summary>
        /// Loads a project with the startup file in a subdirectory, ensuring that syspath is correct when debugging.
        /// </summary>
        public void DebugPythonProjectSubFolderStartupFileSysPath(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
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

        /// <summary>
        /// Debugs a project with and without a process-wide PYTHONPATH value.
        /// If <see cref="DebugPythonProjectSubFolderStartupFileSysPath"/> fails
        /// this test may also fail.
        /// </summary>
        public void DebugPythonProjectWithAndWithoutClearingPythonPath(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
            var sysPathSln = app.CopyProjectForTest(@"TestData\SysPath.sln");
            var helloWorldSln = app.CopyProjectForTest(@"TestData\HelloWorld.sln");
            var testDataPath = Path.Combine(PathUtils.GetParent(helloWorldSln), "HelloWorld").Replace("\\", "\\\\");
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());

            using (new EnvironmentVariableSetter("PYTHONPATH", testDataPath)) {
                app.OpenProject(sysPathSln);

                using (new PythonServiceGeneralOptionsSetter(pyService, clearGlobalPythonPath: false)) {
                    ClearOutputWindowDebugPaneText(app);
                    app.Dte.ExecuteCommand("Debug.Start");
                    WaitForMode(app, dbgDebugMode.dbgDesignMode);

                    WaitForDebugOutput(app, text => text.Contains(testDataPath));
                }

                ClearOutputWindowDebugPaneText(app);
                app.Dte.ExecuteCommand("Debug.Start");
                WaitForMode(app, dbgDebugMode.dbgDesignMode);

                var outputWindowText = WaitForDebugOutput(app, text => text.Contains("DONE"));
                Assert.IsFalse(outputWindowText.Contains(testDataPath), outputWindowText);
            }
        }

        /// <summary>
        /// Tests using a custom interpreter path that is relative
        /// </summary>
        public void DebugPythonCustomInterpreter(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
            var sln = app.CopyProjectForTest(@"TestData\RelativeInterpreterPath.sln");
            var project = app.OpenProject(sln, "Program.py");
            var interpreterPath = Path.Combine(PathUtils.GetParent(sln), "Interpreter.exe");
            File.Copy(PythonPaths.Python27.InterpreterPath, interpreterPath, true);

            app.Dte.Debugger.Breakpoints.Add(File: "Program.py", Line: 1);
            app.Dte.ExecuteCommand("Debug.Start");

            WaitForMode(app, dbgDebugMode.dbgBreakMode);

            Assert.IsNotNull(app.Dte.Debugger.BreakpointLastHit);
            Assert.AreEqual(1, app.Dte.Debugger.BreakpointLastHit.FileLine);

            app.Dte.Debugger.Go(WaitForBreakOrEnd: true);
            Assert.AreEqual(dbgDebugMode.dbgDesignMode, app.Dte.Debugger.CurrentMode);
        }

        /// <summary>
        /// Tests using a custom interpreter path that doesn't exist
        /// </summary>
        public void DebugPythonCustomInterpreterMissing(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
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

        public void PendingBreakPointLocation(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
            var sln = app.CopyProjectForTest(@"TestData\DebuggerProject.sln");
            var project = app.OpenProject(sln, "BreakpointInfo.py");
            var bpInfo = project.ProjectItems.Item("BreakpointInfo.py");

            project.GetPythonProject().GetAnalyzer().WaitForCompleteAnalysis(x => true);

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

        public void BoundBreakpoint(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
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
            Assert.AreEqual(1, bp.CurrentHits);
            Assert.AreEqual(1, bp.HitCountTarget);
            Assert.AreEqual(dbgHitCountType.dbgHitCountTypeNone, bp.HitCountType);

            // https://github.com/Microsoft/PTVS/pull/630
            pendingBp.BreakWhenHit = false; // causes rebind
            Assert.AreEqual(1, pendingBp.Children.Count);
            bp = (Breakpoint3)pendingBp.Children.Item(1);
            Assert.AreEqual(false, bp.BreakWhenHit);
        }

        public void Step(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
            var project = OpenDebuggerProjectAndBreak(app, "SteppingTest.py", 1);
            app.Dte.Debugger.StepOver(true);
            WaitForMode(app, dbgDebugMode.dbgBreakMode);

            Assert.AreEqual((uint)2, ((StackFrame2)app.Dte.Debugger.CurrentStackFrame).LineNumber);

            app.Dte.Debugger.TerminateAll();

            WaitForMode(app, dbgDebugMode.dbgDesignMode);
        }

        public void ShowCallStackOnCodeMap(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
            var project = OpenDebuggerProjectAndBreak(app, "SteppingTest3.py", 2);

            app.Dte.ExecuteCommand("Debug.ShowCallStackonCodeMap");

            // Got the CodeMap Graph displaying, but it may not have finished processing
            app.WaitForInputIdle();

            var dgmlKind = "{295A0962-5A59-4F4F-9E12-6BC670C15C3B}";

            Document dgmlDoc = null;
            for (int i = 1; i <= app.Dte.Documents.Count; i++) {
                var doc = app.Dte.Documents.Item(i);
                if (doc.Kind == dgmlKind) {
                    dgmlDoc = doc;
                    break;
                }
            }

            Assert.IsNotNull(dgmlDoc, "Could not find dgml document");

            var dgmlFile = Path.GetTempFileName();
            try {
                // Save to a temp file. If the code map is not ready, it 
                // may have template xml but no data in it, so give it
                // some more time and try again.
                string fileText = string.Empty;
                for (int i = 0; i < 10; i++) {
                    dgmlDoc.Save(dgmlFile);

                    fileText = File.ReadAllText(dgmlFile);
                    if (fileText.Contains("SteppingTest3")) {
                        break;
                    }

                    Thread.Sleep(250);
                }

                // These are the lines of interest in the DGML File.  If these match, the correct content should be displayed in the code map.
                List<string> LinesToMatch = new List<string>() {
                        @"<Node Id=""\(Name=f @1 IsUnresolved=True\)"" Category=""CodeSchema_CallStackUnresolvedMethod"" Label=""f"">",
                        @"<Node Id=""@2"" Category=""CodeSchema_CallStackUnresolvedMethod"" Label=""SteppingTest3 module"">",
                        @"<Node Id=""ExternalCodeRootNode"" Category=""ExternalCallStackEntry"" Label=""External Code"">",
                        @"<Link Source=""@2"" Target=""\(Name=f @1 IsUnresolved=True\)"" Category=""CallStackDirectCall"">",
                        @"<Alias n=""1"" Uri=""Assembly=SteppingTest3"" />",
                        @"<Alias n=""2"" Id=""\(Name=&quot;SteppingTest3 module&quot; @1 IsUnresolved=True\)"" />"
                    };

                foreach (var line in LinesToMatch) {
                    Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(fileText, line), "Expected:\r\n{0}\r\nsActual:\r\n{1}", line, fileText);
                }
            } finally {
                File.Delete(dgmlFile);
            }
        }

        public void Step3(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
            var project = OpenDebuggerProjectAndBreak(app, "SteppingTest3.py", 2);
            app.Dte.Debugger.StepOut(true);
            WaitForMode(app, dbgDebugMode.dbgBreakMode);

            Assert.AreEqual((uint)5, ((StackFrame2)app.Dte.Debugger.CurrentStackFrame).LineNumber);

            app.Dte.Debugger.TerminateAll();

            WaitForMode(app, dbgDebugMode.dbgDesignMode);
        }

        public void Step5(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
            var project = OpenDebuggerProjectAndBreak(app, "SteppingTest5.py", 5);
            app.Dte.Debugger.StepInto(true);
            WaitForMode(app, dbgDebugMode.dbgBreakMode);

            Assert.AreEqual((uint)2, ((StackFrame2)app.Dte.Debugger.CurrentStackFrame).LineNumber);

            app.Dte.Debugger.TerminateAll();

            WaitForMode(app, dbgDebugMode.dbgDesignMode);
        }

        public void SetNextLine(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
            var project = OpenDebuggerProjectAndBreak(app, "SetNextLine.py", 7);

            var doc = app.Dte.Documents.Item("SetNextLine.py");
            ((TextSelection)doc.Selection).GotoLine(8);
            ((TextSelection)doc.Selection).EndOfLine(false);
            var curLine = ((TextSelection)doc.Selection).CurrentLine;

            app.Dte.Debugger.SetNextStatement();
            app.Dte.Debugger.StepOver(true);
            WaitForMode(app, dbgDebugMode.dbgBreakMode);

            var curFrame = app.Dte.Debugger.CurrentStackFrame;
            var local = curFrame.Locals.Item("y");
            Assert.AreEqual("100", local.Value);

            try {
                curFrame.Locals.Item("x");
                Assert.Fail("Expected exception, x should not be defined");
            } catch {
            }

            app.Dte.Debugger.TerminateAll();

            WaitForMode(app, dbgDebugMode.dbgDesignMode);
        }

        /*
        //[TestMethod, Priority(0)]
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
        public void TerminateProcess(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
            StartHelloWorldAndBreak(app);

            Assert.AreEqual(dbgDebugMode.dbgBreakMode, app.Dte.Debugger.CurrentMode);
            Assert.AreEqual(1, app.Dte.Debugger.BreakpointLastHit.FileLine);

            app.Dte.Debugger.TerminateAll();

            WaitForMode(app, dbgDebugMode.dbgDesignMode);
        }

        /// <summary>
        /// Loads the simple project and makes sure we get the correct module.
        /// </summary>
        public void EnumModules(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
            StartHelloWorldAndBreak(app);

            var modules = ((Process3)app.Dte.Debugger.CurrentProcess).Modules;
            Assert.IsTrue(modules.Count >= 1);

            var module = modules.Item("Program");
            Assert.IsNotNull(module);

            Assert.IsTrue(module.Path.EndsWith("Program.py"));
            Assert.AreEqual("Program", module.Name);
            Assert.AreNotEqual((uint)0, module.Order);

            app.Dte.Debugger.TerminateAll();
            WaitForMode(app, dbgDebugMode.dbgDesignMode);
        }

        public void MainThread(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
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

        public void ExpressionEvaluation(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
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

            var local = curFrame.Locals.Item("i");
            Assert.AreEqual("42", local.Value);
            Assert.AreEqual("f", curFrame.FunctionName);
            Assert.IsTrue(((StackFrame2)curFrame).FileName.EndsWith("Program.py"));
            Assert.AreEqual((uint)14, ((StackFrame2)curFrame).LineNumber);
            Assert.AreEqual("Program", ((StackFrame2)curFrame).Module);

            Assert.AreEqual(3, curFrame.Locals.Item("l").DataMembers.Count);
            Assert.AreEqual("[0]", curFrame.Locals.Item("l").DataMembers.Item(1).Name);

            Assert.AreEqual(3, ((StackFrame2)curFrame).Arguments.Count);
            Assert.AreEqual("a", ((StackFrame2)curFrame).Arguments.Item(1).Name);
            Assert.AreEqual("2", ((StackFrame2)curFrame).Arguments.Item(1).Value);

            var expr = ((Debugger3)app.Dte.Debugger).GetExpression("l[0] + l[1]");
            Assert.AreEqual("l[0] + l[1]", expr.Name);
            Assert.AreEqual("5", expr.Value);

            expr = ((Debugger3)app.Dte.Debugger).GetExpression("invalid expression");
            Assert.IsFalse(expr.IsValidValue);

            app.Dte.Debugger.ExecuteStatement("x = 2");

            app.Dte.Debugger.TerminateAll();
            WaitForMode(app, dbgDebugMode.dbgDesignMode);
        }

        public void SimpleException(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
            ExceptionTest(app, "SimpleException.py", "Exception Thrown", "Exception", "Exception", 3);
        }

        public void SimpleException2(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
            ExceptionTest(app, "SimpleException2.py", "Exception Thrown", "ValueError: bad value", "ValueError", 3);
        }

        public void SimpleExceptionUnhandled(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
            using (new PythonDebuggingGeneralOptionsSetter(app.Dte, waitOnAbnormalExit: false)) {
                ExceptionTest(app, "SimpleExceptionUnhandled.py", "Exception User-Unhandled", "ValueError: bad value", "ValueError", 2);
            }
        }

        // https://github.com/Microsoft/PTVS/issues/275
        public void ExceptionInImportLibNotReported(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
            using (new DebuggingGeneralOptionsSetter(app.Dte, enableJustMyCode: true)) {
                OpenDebuggerProjectAndBreak(app, "ImportLibException.py", 2);
                app.Dte.Debugger.Go(WaitForBreakOrEnd: true);
                Assert.AreEqual(dbgDebugMode.dbgDesignMode, app.Dte.Debugger.CurrentMode);
            }
        }

        public void Breakpoints(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
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

        public void BreakpointsDisable(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
            OpenDebuggerProjectAndBreak(app, "BreakpointTest4.py", 2);
            var debug3 = (Debugger3)app.Dte.Debugger;
            Assert.AreEqual((uint)2, ((StackFrame2)debug3.CurrentThread.StackFrames.Item(1)).LineNumber);
            debug3.Go(true);
            Assert.AreEqual((uint)2, ((StackFrame2)debug3.CurrentThread.StackFrames.Item(1)).LineNumber);
            Assert.IsTrue(debug3.Breakpoints.Item(1).Enabled);
            debug3.Breakpoints.Item(1).Enabled = false;
            debug3.Go(true);

            WaitForMode(app, dbgDebugMode.dbgDesignMode);
        }

        public void BreakpointsDisableReenable(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
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

        /// <summary>
        /// Make sure the presence of errors causes F5 to prevent running w/o a confirmation.
        /// </summary>
        public void LaunchWithErrorsDontRun(PythonVisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
            using (new PythonDebuggingGeneralOptionsSetter(app.Dte, promptBeforeRunningWithBuildErrorSetting: true)) {
                var sln = app.CopyProjectForTest(@"TestData\ErrorProject.sln");
                var project = app.OpenProject(sln);
                var projectDir = PathUtils.GetParent(project.FullName);

                // Open a file with errors
                string scriptFilePath = Path.Combine(projectDir, "Program.py");
                app.Dte.ItemOperations.OpenFile(scriptFilePath);
                app.Dte.ExecuteCommand("View.ErrorList");
                var items = app.WaitForErrorListItems(7);

                var debug3 = (Debugger3)app.Dte.Debugger;
                ThreadPool.QueueUserWorkItem(x => debug3.Go(true));

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
        public void StartWithDebuggingNoProject(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
            string scriptFilePath = TestData.GetPath(@"TestData\HelloWorld\Program.py");

            app.DeleteAllBreakPoints();

            app.Dte.ItemOperations.OpenFile(scriptFilePath);
            app.Dte.Debugger.Breakpoints.Add(File: scriptFilePath, Line: 1);
            app.Dte.ExecuteCommand("Python.StartWithDebugging");
            WaitForMode(app, dbgDebugMode.dbgBreakMode);
            Assert.AreEqual(dbgDebugMode.dbgBreakMode, app.Dte.Debugger.CurrentMode);
            Assert.IsNotNull(app.Dte.Debugger.BreakpointLastHit);
            Assert.AreEqual("Program.py, line 1", app.Dte.Debugger.BreakpointLastHit.Name);
            app.Dte.Debugger.Go(WaitForBreakOrEnd: true);
            Assert.AreEqual(dbgDebugMode.dbgDesignMode, app.Dte.Debugger.CurrentMode);
        }

        /// <summary>
        /// Start without debugging, with script but no project.
        /// </summary>
        public void StartWithoutDebuggingNoProject(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
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

        /// <summary>
        /// Start with debugging, with script not in project.
        /// </summary>
        public void StartWithDebuggingNotInProject(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
            string scriptFilePath = TestData.GetPath(@"TestData\HelloWorld\Program.py");

            OpenDebuggerProject(app);

            app.Dte.ItemOperations.OpenFile(scriptFilePath);
            app.Dte.Debugger.Breakpoints.Add(File: scriptFilePath, Line: 1);
            app.Dte.ExecuteCommand("Python.StartWithDebugging");
            WaitForMode(app, dbgDebugMode.dbgBreakMode);
            Assert.AreEqual(dbgDebugMode.dbgBreakMode, app.Dte.Debugger.CurrentMode);
            Assert.AreEqual("Program.py, line 1", app.Dte.Debugger.BreakpointLastHit.Name);
            app.Dte.Debugger.Go(WaitForBreakOrEnd: true);
            Assert.AreEqual(dbgDebugMode.dbgDesignMode, app.Dte.Debugger.CurrentMode);
        }

        /// <summary>
        /// Start without debugging, with script not in project.
        /// </summary>
        public void StartWithoutDebuggingNotInProject(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
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

        /// <summary>
        /// Start with debuggging, with script in project.
        /// </summary>
        public void StartWithDebuggingInProject(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
            var proj = OpenDebuggerProject(app);
            var projDir = PathUtils.GetParent(proj.FullName);
            var scriptFilePath = Path.Combine(projDir, "Program.py");

            app.Dte.ItemOperations.OpenFile(scriptFilePath);
            app.Dte.Debugger.Breakpoints.Add(File: scriptFilePath, Line: 1);
            app.Dte.ExecuteCommand("Python.StartWithDebugging");
            WaitForMode(app, dbgDebugMode.dbgBreakMode);
            Assert.AreEqual(dbgDebugMode.dbgBreakMode, app.Dte.Debugger.CurrentMode);
            Assert.AreEqual("Program.py, line 1", app.Dte.Debugger.BreakpointLastHit.Name);
            app.Dte.Debugger.Go(WaitForBreakOrEnd: true);
            Assert.AreEqual(dbgDebugMode.dbgDesignMode, app.Dte.Debugger.CurrentMode);
        }

        /// <summary>
        /// Start with debuggging, with script in subfolder project.
        /// </summary>
        public void StartWithDebuggingSubfolderInProject(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
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

        /// <summary>
        /// Start without debuggging, with script in project.
        /// </summary>
        public void StartWithoutDebuggingInProject(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
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

        /// <summary>
        /// Start with debugging, no script.
        /// </summary>
        public void StartWithDebuggingNoScript(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
            try {
                app.ExecuteCommand("Python.StartWithDebugging");
            } catch (COMException e) {
                // Requires an opened python file with focus
                Assert.IsTrue(e.ToString().Contains("is not available"));
            }
        }

        /// <summary>
        /// Start without debugging, no script.
        /// </summary>
        public void StartWithoutDebuggingNoScript(VisualStudioApp app, DotNotWaitOnNormalExit optionSetter) {
            try {
                app.ExecuteCommand("Python.StartWithoutDebugging");
            } catch (COMException e) {
                // Requires an opened python file with focus
                Assert.IsTrue(e.ToString().Contains("is not available"));
            }
        }

        #endregion

        #region Helpers

        public class DotNotWaitOnNormalExit : PythonDebuggingGeneralOptionsSetter {
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

        private static void ExceptionTest(VisualStudioApp app, string filename, string expectedTitle, string expectedDescription, string exceptionType, int expectedLine) {
            var debug3 = (Debugger3)app.Dte.Debugger;
            using (new DebuggingGeneralOptionsSetter(app.Dte, enableJustMyCode: true)) {
                OpenDebuggerProject(app, filename);

                var exceptionSettings = debug3.ExceptionGroups.Item("Python Exceptions");

                exceptionSettings.SetBreakWhenThrown(true, exceptionSettings.Item(exceptionType));

                app.Dte.ExecuteCommand("Debug.Start");
                WaitForMode(app, dbgDebugMode.dbgBreakMode);

                exceptionSettings.SetBreakWhenThrown(false, exceptionSettings.Item(exceptionType));
                exceptionSettings.SetBreakWhenThrown(true, exceptionSettings.Item(exceptionType));
                debug3.ExceptionGroups.ResetAll();

                var excepAdorner = app.WaitForExceptionAdornment();
                AutomationWrapper.DumpElement(excepAdorner.Element);

                Assert.AreEqual(expectedDescription, excepAdorner.Description.TrimEnd());
                Assert.AreEqual(expectedTitle, excepAdorner.Title.TrimEnd());

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

        #endregion
    }
}
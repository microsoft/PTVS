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
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using EnvDTE;
using EnvDTE90;
using EnvDTE90a;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.UI;
using TestUtilities.UI.Python;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using Thread = System.Threading.Thread;

namespace DebuggerUITests {
    [TestClass]
    public class DebugProject {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            TestData.Deploy();
        }

        [TestCleanup]
        public void MyTestCleanup() {
            VsIdeTestHostContext.Dte.Solution.Close(false);
        }

        #region Test Cases

        /// <summary>
        /// Loads the simple project and then unloads it, ensuring that the solution is created with a single project.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DebugPythonProject() {
            StartHelloWorldAndBreak();

            VsIdeTestHostContext.Dte.Debugger.Go(WaitForBreakOrEnd: true);
            Assert.AreEqual(VsIdeTestHostContext.Dte.Debugger.CurrentMode, dbgDebugMode.dbgDesignMode);
        }

        /// <summary>
        /// Loads a project with the startup file in a subdirectory, ensuring that syspath is correct when debugging.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DebugPythonProjectSubFolderStartupFileSysPath() {
            OpenProject(TestData.GetPath(@"TestData\SysPath.sln"));
            
            VsIdeTestHostContext.Dte.ExecuteCommand("Debug.Start");
            WaitForMode(dbgDebugMode.dbgDesignMode);
            
            // sys.path should point to the startup file directory, not the project directory.
            // this matches the behavior of start without debugging.
            // Note: backslashes are escaped in the output
            string testDataPath = TestData.GetPath("TestData\\SysPath\\Sub'").Replace("\\", "\\\\");
            WaitForDebugOutput(text => text.Contains(testDataPath));
        }

        /// <summary>
        /// Tests using a custom interpreter path that is relative
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DebugPythonCustomInterpreter() {
            // try once when the interpreter doesn't exist...
            var project = OpenProject(TestData.GetPath(@"TestData\RelativeInterpreterPath.sln"), "Program.py");

            VsIdeTestHostContext.Dte.ExecuteCommand("Debug.Start");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var dialog = app.WaitForDialog();
            VisualStudioApp.CheckMessageBox(TestUtilities.UI.MessageBoxButton.Ok, "Interpreter specified in the project does not exist:",  "Interpreter.exe'");

            VsIdeTestHostContext.Dte.Solution.Close(false);

            // copy an interpreter over and try again
            File.Copy(PythonPaths.Python27.Path, TestData.GetPath(@"TestData\Interpreter.exe"));
            try {
                OpenProjectAndBreak(TestData.GetPath(@"TestData\RelativeInterpreterPath.sln"), "Program.py", 1);
                VsIdeTestHostContext.Dte.Debugger.Go(WaitForBreakOrEnd: true);
                Assert.AreEqual(VsIdeTestHostContext.Dte.Debugger.CurrentMode, dbgDebugMode.dbgDesignMode);
            } finally {
                File.Delete(TestData.GetPath(@"TestData\Interpreter.exe"));
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestPendingBreakPointLocation() {

            var project = OpenProject(@"TestData\DebuggerProject.sln", "BreakpointInfo.py");
            var bpInfo = project.ProjectItems.Item("BreakpointInfo.py");

            project.GetPythonProject().GetAnalyzer().WaitForCompleteAnalysis(x => true);

            var bp = VsIdeTestHostContext.Dte.Debugger.Breakpoints.Add(File: "BreakpointInfo.py", Line: 2);
            Assert.AreEqual("Python", bp.Item(1).Language);
            // FunctionName doesn't get queried for when adding the BP via EnvDTE, so we can't assert here :(
            //Assert.AreEqual("BreakpointInfo.C", bp.Item(1).FunctionName);
            bp = VsIdeTestHostContext.Dte.Debugger.Breakpoints.Add(File: "BreakpointInfo.py", Line: 3);
            Assert.AreEqual("Python", bp.Item(1).Language);
            //Assert.AreEqual("BreakpointInfo.C.f", bp.Item(1).FunctionName);
            bp = VsIdeTestHostContext.Dte.Debugger.Breakpoints.Add(File: "BreakpointInfo.py", Line: 6);
            Assert.AreEqual("Python", bp.Item(1).Language);
            //Assert.AreEqual("BreakpointInfo", bp.Item(1).FunctionName);
            bp = VsIdeTestHostContext.Dte.Debugger.Breakpoints.Add(File: "BreakpointInfo.py", Line: 7);
            Assert.AreEqual("Python", bp.Item(1).Language);
            //Assert.AreEqual("BreakpointInfo.f", bp.Item(1).FunctionName);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestStep() {
            var project = OpenDebuggerProjectAndBreak("SteppingTest.py", 1);
            VsIdeTestHostContext.Dte.Debugger.StepOver(true);
            WaitForMode(dbgDebugMode.dbgBreakMode);

            Assert.AreEqual((uint)2, ((StackFrame2)VsIdeTestHostContext.Dte.Debugger.CurrentStackFrame).LineNumber);

            VsIdeTestHostContext.Dte.Debugger.TerminateAll();

            WaitForMode(dbgDebugMode.dbgDesignMode);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestStep3() {
            var project = OpenDebuggerProjectAndBreak("SteppingTest3.py", 2);
            VsIdeTestHostContext.Dte.Debugger.StepOut(true);
            WaitForMode(dbgDebugMode.dbgBreakMode);

            Assert.AreEqual((uint)5, ((StackFrame2)VsIdeTestHostContext.Dte.Debugger.CurrentStackFrame).LineNumber);

            VsIdeTestHostContext.Dte.Debugger.TerminateAll();

            WaitForMode(dbgDebugMode.dbgDesignMode);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestStep5() {
            var project = OpenDebuggerProjectAndBreak("SteppingTest5.py", 5);
            VsIdeTestHostContext.Dte.Debugger.StepInto(true);
            WaitForMode(dbgDebugMode.dbgBreakMode);

            Assert.AreEqual((uint)2, ((StackFrame2)VsIdeTestHostContext.Dte.Debugger.CurrentStackFrame).LineNumber);

            VsIdeTestHostContext.Dte.Debugger.TerminateAll();

            WaitForMode(dbgDebugMode.dbgDesignMode);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestSetNextLine() {
            var project = OpenDebuggerProjectAndBreak("SetNextLine.py", 7);

            var doc = VsIdeTestHostContext.Dte.Documents.Item("SetNextLine.py");
            ((TextSelection)doc.Selection).GotoLine(8);
            ((TextSelection)doc.Selection).EndOfLine(false);
            //((TextSelection)doc.Selection).CharRight(false, 5);
            //((TextSelection)doc.Selection).CharRight(true, 1);
            var curLine = ((TextSelection)doc.Selection).CurrentLine;

            VsIdeTestHostContext.Dte.Debugger.SetNextStatement();
            VsIdeTestHostContext.Dte.Debugger.StepOver(true);
            WaitForMode(dbgDebugMode.dbgBreakMode);

            var curFrame = VsIdeTestHostContext.Dte.Debugger.CurrentStackFrame;
            var local = curFrame.Locals.Item("y");
            Assert.AreEqual("100", local.Value);

            try {
                curFrame.Locals.Item("x");
                Assert.Fail("Expected exception, x should not be defined");
            } catch {
            }

            VsIdeTestHostContext.Dte.Debugger.TerminateAll();

            WaitForMode(dbgDebugMode.dbgDesignMode);
        }

        /*
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestBreakAll() {
            var project = OpenDebuggerProjectAndBreak("BreakAllTest.py", 1);
            
            VsIdeTestHostContext.Dte.Debugger.Go(false);

            
            WaitForMode(dbgDebugMode.dbgRunMode);

            Thread.Sleep(2000);

            VsIdeTestHostContext.Dte.Debugger.Break();

            WaitForMode(dbgDebugMode.dbgBreakMode);

            var lineNo = ((StackFrame2)VsIdeTestHostContext.Dte.Debugger.CurrentStackFrame).LineNumber;
            Assert.IsTrue(lineNo == 1 || lineNo == 2);

            VsIdeTestHostContext.Dte.Debugger.Go(false);

            WaitForMode(dbgDebugMode.dbgRunMode);

            VsIdeTestHostContext.Dte.Debugger.TerminateAll();

            WaitForMode(dbgDebugMode.dbgDesignMode);
        }*/
        
        /// <summary>
        /// Loads the simple project and then terminates the process while we're at a breakpoint.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestTerminateProcess() {
            StartHelloWorldAndBreak();

            Assert.AreEqual(VsIdeTestHostContext.Dte.Debugger.CurrentMode, dbgDebugMode.dbgBreakMode);
            Assert.AreEqual(VsIdeTestHostContext.Dte.Debugger.BreakpointLastHit.FileLine, 1);

            VsIdeTestHostContext.Dte.Debugger.TerminateAll();

            WaitForMode(dbgDebugMode.dbgDesignMode);
        }

        /// <summary>
        /// Loads the simple project and makes sure we get the correct module.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestEnumModules() {
            StartHelloWorldAndBreak();

            var modules = ((Process3)VsIdeTestHostContext.Dte.Debugger.CurrentProcess).Modules;
            Assert.AreEqual(modules.Count, 1);
            var module = modules.Item(1);
            var modulePath = module.Path;
            Assert.IsTrue(modulePath.EndsWith("Program.py"));
            Assert.AreEqual("Program", module.Name);
            Assert.AreNotEqual(module.Order, (uint)0);

            VsIdeTestHostContext.Dte.Debugger.TerminateAll();
            WaitForMode(dbgDebugMode.dbgDesignMode);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestThread() {
            StartHelloWorldAndBreak();

            var thread = ((Thread2)VsIdeTestHostContext.Dte.Debugger.CurrentThread);
            Assert.AreEqual("MainThread", thread.Name);
            Assert.AreEqual(0, thread.SuspendCount);
            Assert.AreEqual("Normal", thread.Priority);
            Assert.AreEqual("MainThread", thread.DisplayName);
            thread.DisplayName = "Hi";
            Assert.AreEqual("Hi", thread.DisplayName);

            VsIdeTestHostContext.Dte.Debugger.TerminateAll();
            WaitForMode(dbgDebugMode.dbgDesignMode);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ExpressionEvaluation() {
            OpenDebuggerProject("Program.py");

            VsIdeTestHostContext.Dte.Debugger.Breakpoints.Add(File: "Program.py", Line: 14);
            VsIdeTestHostContext.Dte.ExecuteCommand("Debug.Start");

            WaitForMode(dbgDebugMode.dbgBreakMode);

            Assert.AreEqual(VsIdeTestHostContext.Dte.Debugger.BreakpointLastHit.FileLine, 14);

            Assert.AreEqual(VsIdeTestHostContext.Dte.Debugger.GetExpression("i").Name, "i");
            Assert.AreEqual(VsIdeTestHostContext.Dte.Debugger.GetExpression("i").Value, "42");
            Assert.AreEqual(VsIdeTestHostContext.Dte.Debugger.GetExpression("i").Type, "int");
            Assert.AreEqual(VsIdeTestHostContext.Dte.Debugger.GetExpression("i").IsValidValue, true);
            Assert.AreEqual(VsIdeTestHostContext.Dte.Debugger.GetExpression("i").DataMembers.Count, 0);

            var curFrame = VsIdeTestHostContext.Dte.Debugger.CurrentStackFrame;
                        
            var local = curFrame.Locals.Item("i");
            Assert.AreEqual("42", local.Value);
            Assert.AreEqual("f in Program line 14", curFrame.FunctionName);
            Assert.IsTrue(((StackFrame2)curFrame).FileName.EndsWith("Program.py"));
            Assert.AreEqual((uint)14, ((StackFrame2)curFrame).LineNumber);
            Assert.AreEqual("Program", ((StackFrame2)curFrame).Module);

            Assert.AreEqual(curFrame.Locals.Item("l").DataMembers.Count, 3);
            Assert.AreEqual(curFrame.Locals.Item("l").DataMembers.Item(1).Name, "[0]");

            Assert.AreEqual(3, ((StackFrame2)curFrame).Arguments.Count);
            Assert.AreEqual("a", ((StackFrame2)curFrame).Arguments.Item(1).Name);
            Assert.AreEqual("2", ((StackFrame2)curFrame).Arguments.Item(1).Value);

            var expr = ((Debugger3)VsIdeTestHostContext.Dte.Debugger).GetExpression("l[0] + l[1]");
            Assert.AreEqual("l[0] + l[1]", expr.Name);
            Assert.AreEqual("5", expr.Value);

            expr = ((Debugger3)VsIdeTestHostContext.Dte.Debugger).GetExpression("invalid expression");
            Assert.AreEqual(expr.IsValidValue, false);

            VsIdeTestHostContext.Dte.Debugger.ExecuteStatement("x = 2");

            VsIdeTestHostContext.Dte.Debugger.Go(WaitForBreakOrEnd: true);
            Assert.AreEqual(VsIdeTestHostContext.Dte.Debugger.CurrentMode, dbgDebugMode.dbgDesignMode);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestException() {
            ExceptionTest("SimpleException.py", "Exception occurred", "", "exceptions.Exception", 3);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestException2() {
            ExceptionTest("SimpleException2.py", "ValueError occurred", "bad value", "exceptions.ValueError", 3);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestExceptionUnhandled() {
            ExceptionTest("SimpleExceptionUnhandled.py", "ValueError was unhandled by user code", "bad value", "exceptions.ValueError", 2);
        }

        private static void ExceptionTest(string filename, string expectedTitle, string expectedDescription, string exceptionType, int expectedLine) {
            OpenDebuggerProject(filename);
            var debug3 = (Debugger3)VsIdeTestHostContext.Dte.Debugger;
            var exceptionSettings = debug3.ExceptionGroups.Item("Python Exceptions");

            exceptionSettings.SetBreakWhenThrown(true, exceptionSettings.Item(exceptionType));

            VsIdeTestHostContext.Dte.ExecuteCommand("Debug.Start");
            WaitForMode(dbgDebugMode.dbgBreakMode);

            exceptionSettings.SetBreakWhenThrown(false, exceptionSettings.Item(exceptionType));
            exceptionSettings.SetBreakWhenThrown(true, exceptionSettings.Item(exceptionType));
            debug3.ExceptionGroups.ResetAll();

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var excepDialog = new ExceptionHelperDialog(AutomationElement.FromHandle(app.WaitForDialog()));
            AutomationWrapper.DumpElement(excepDialog.Element);

            Assert.AreEqual(excepDialog.Description, expectedDescription);
            Assert.AreEqual(excepDialog.Title, expectedTitle);

            excepDialog.Ok();

            Assert.AreEqual(((StackFrame2)debug3.CurrentThread.StackFrames.Item(1)).LineNumber, (uint)expectedLine);

            VsIdeTestHostContext.Dte.Debugger.Go(WaitForBreakOrEnd: true);

            WaitForMode(dbgDebugMode.dbgDesignMode);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestBreakpoints() {
            OpenDebuggerProjectAndBreak("BreakpointTest2.py", 3);
            var debug3 = (Debugger3)VsIdeTestHostContext.Dte.Debugger;
            Assert.AreEqual(((StackFrame2)debug3.CurrentThread.StackFrames.Item(1)).LineNumber, (uint)3);
            debug3.Go(true);
            Assert.AreEqual(((StackFrame2)debug3.CurrentThread.StackFrames.Item(1)).LineNumber, (uint)3);
            Assert.AreEqual(true, debug3.Breakpoints.Item(1).Enabled);            
            debug3.Breakpoints.Item(1).Delete();
            debug3.Go(true);

            WaitForMode(dbgDebugMode.dbgDesignMode);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestBreakpointsDisable() {
            OpenDebuggerProjectAndBreak("BreakpointTest4.py", 2);
            var debug3 = (Debugger3)VsIdeTestHostContext.Dte.Debugger;
            Assert.AreEqual(((StackFrame2)debug3.CurrentThread.StackFrames.Item(1)).LineNumber, (uint)2);
            debug3.Go(true);
            Assert.AreEqual(((StackFrame2)debug3.CurrentThread.StackFrames.Item(1)).LineNumber, (uint)2);
            Assert.AreEqual(true, debug3.Breakpoints.Item(1).Enabled);
            debug3.Breakpoints.Item(1).Enabled = false;            
            debug3.Go(true);

            WaitForMode(dbgDebugMode.dbgDesignMode);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestBreakpointsDisableReenable() {
            var debug3 = (Debugger3)VsIdeTestHostContext.Dte.Debugger;

            OpenDebuggerProjectAndBreak("BreakpointTest4.py", 2);
            Assert.AreEqual(((StackFrame2)debug3.CurrentThread.StackFrames.Item(1)).LineNumber, (uint)2);
            debug3.Go(true);
            Assert.AreEqual(((StackFrame2)debug3.CurrentThread.StackFrames.Item(1)).LineNumber, (uint)2);
            int bpCount = debug3.Breakpoints.Count;
            
            Assert.AreEqual(1, bpCount);
            Assert.AreEqual(true, debug3.Breakpoints.Item(1).Enabled);
            Assert.AreEqual(2, debug3.Breakpoints.Item(1).FileLine);
            debug3.Breakpoints.Item(1).Enabled = false;

            debug3.Breakpoints.Add(File: "BreakpointTest4.py", Line: 4);
            debug3.Breakpoints.Add(File: "BreakpointTest4.py", Line: 5);
            Assert.AreEqual(4, debug3.Breakpoints.Item(2).FileLine);
            Assert.AreEqual(5, debug3.Breakpoints.Item(3).FileLine);
            
            // line 4
            debug3.Go(true);
            WaitForMode(dbgDebugMode.dbgBreakMode);
            Assert.AreEqual(((StackFrame2)debug3.CurrentThread.StackFrames.Item(1)).LineNumber, (uint)4);

            // line 5
            debug3.Go(true);
            WaitForMode(dbgDebugMode.dbgBreakMode);
            Assert.AreEqual(((StackFrame2)debug3.CurrentThread.StackFrames.Item(1)).LineNumber, (uint)5);
            debug3.Breakpoints.Item(3).Enabled = false;

            // back to line 4
            debug3.Go(true);
            WaitForMode(dbgDebugMode.dbgBreakMode);
            Assert.AreEqual(((StackFrame2)debug3.CurrentThread.StackFrames.Item(1)).LineNumber, (uint)4);

            debug3.Go(true);
            WaitForMode(dbgDebugMode.dbgBreakMode);
            Assert.AreEqual(((StackFrame2)debug3.CurrentThread.StackFrames.Item(1)).LineNumber, (uint)4);
            
            debug3.Breakpoints.Item(2).Enabled = false;
            debug3.Breakpoints.Item(3).Enabled = true;

            // back to line 5
            debug3.Go(true);
            WaitForMode(dbgDebugMode.dbgBreakMode);
            Assert.AreEqual(((StackFrame2)debug3.CurrentThread.StackFrames.Item(1)).LineNumber, (uint)5);
            debug3.Breakpoints.Item(3).Enabled = false;

            // all disabled, run to completion
            debug3.Go(true);
            WaitForMode(dbgDebugMode.dbgDesignMode);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestLaunchWithErrorsDontRun() {
            var project = DebugProject.OpenProject(@"TestData\ErrorProject.sln");
            var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte);

            GetOptions().PromptBeforeRunningWithBuildErrorSetting = true;

            var debug3 = (Debugger3)VsIdeTestHostContext.Dte.Debugger;
            ThreadPool.QueueUserWorkItem(x => debug3.Go(true));

            var dialog = new PythonLaunchWithErrorsDialog(app.WaitForDialog());
            dialog.No();

            // make sure we don't go into debug mode
            for (int i = 0; i < 10; i++) {
                Assert.AreEqual(VsIdeTestHostContext.Dte.Debugger.CurrentMode, dbgDebugMode.dbgDesignMode);
                System.Threading.Thread.Sleep(100);
            }

            WaitForMode(dbgDebugMode.dbgDesignMode);
        }

        /// <summary>
        /// Make sure the presence of errors causes F5 to prevent running w/o a confirmation.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestLaunchWithErrorsRun() {
            var project = DebugProject.OpenProject(@"TestData\ErrorProject.sln");
            var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte);

            GetOptions().PromptBeforeRunningWithBuildErrorSetting = true;

            var debug3 = (Debugger3)VsIdeTestHostContext.Dte.Debugger;
            ThreadPool.QueueUserWorkItem(x => debug3.Go(true));

            var dialog = new PythonLaunchWithErrorsDialog(app.WaitForDialog());
            dialog.No();

            // make sure we don't go into debug mode
            for (int i = 0; i < 10; i++) {
                Assert.AreEqual(VsIdeTestHostContext.Dte.Debugger.CurrentMode, dbgDebugMode.dbgDesignMode);
                System.Threading.Thread.Sleep(100);
            }

            WaitForMode(dbgDebugMode.dbgDesignMode);
        }

        /// <summary>
        /// Make sure errors in a file show up in the error list window
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestProjectWithErrors_ErrorList() {
            var project = DebugProject.OpenProject(@"TestData\ErrorProject.sln");
            var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte);
            
            var errorList = (IVsErrorList)VsIdeTestHostContext.ServiceProvider.GetService(typeof(SVsErrorList));

            const int expectedItems = 5;
            List<IVsTaskItem> allItems = GetErrorListItems(errorList, expectedItems);
            Assert.AreEqual(expectedItems, allItems.Count);
        }

        /// <summary>
        /// Make sure deleting a project clears the error list
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestProjectWithErrorsDeleteProject() {
            var project = DebugProject.OpenProject(@"TestData\ErrorProjectDelete.sln");
            var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte);

            var errorList = (IVsErrorList)VsIdeTestHostContext.ServiceProvider.GetService(typeof(SVsErrorList));

            const int expectedItems = 5;
            List<IVsTaskItem> allItems = GetErrorListItems(errorList, expectedItems);
            Assert.AreEqual(expectedItems, allItems.Count);

            project.Delete();

            allItems = GetErrorListItems(errorList, 0);
            Assert.AreEqual(0, allItems.Count);
        }

        /// <summary>
        /// Make sure deleting a project clears the error list
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestProjectWithErrorsUnloadProject() {
            var project = DebugProject.OpenProject(@"TestData\ErrorProjectDelete.sln");
            var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte);

            var errorList = (IVsErrorList)VsIdeTestHostContext.ServiceProvider.GetService(typeof(SVsErrorList));

            const int expectedItems = 5;
            List<IVsTaskItem> allItems = GetErrorListItems(errorList, expectedItems);
            Assert.AreEqual(expectedItems, allItems.Count);

            IVsSolution solutionService = VsIdeTestHostContext.ServiceProvider.GetService(typeof(SVsSolution)) as IVsSolution;
            Debug.Assert(solutionService != null);

            IVsHierarchy selectedHierarchy;
            ErrorHandler.ThrowOnFailure(solutionService.GetProjectOfUniqueName(project.UniqueName, out selectedHierarchy));
            Debug.Assert(selectedHierarchy != null);

            ErrorHandler.ThrowOnFailure(solutionService.CloseSolutionElement((uint)__VSSLNCLOSEOPTIONS.SLNCLOSEOPT_UnloadProject, selectedHierarchy, 0));

            allItems = GetErrorListItems(errorList, 0);
            Assert.AreEqual(0, allItems.Count);
        }

        /// <summary>
        /// Make sure deleting a file w/ errors clears the error list
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestProjectWithErrorsDeleteFile() {
            var project = DebugProject.OpenProject(@"TestData\ErrorProjectDeleteFile.sln");
            var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte);

            var errorList = (IVsErrorList)VsIdeTestHostContext.ServiceProvider.GetService(typeof(SVsErrorList));

            const int expectedItems = 5;
            List<IVsTaskItem> allItems = GetErrorListItems(errorList, expectedItems);
            Assert.AreEqual(expectedItems, allItems.Count);

            project.ProjectItems.Item("Program.py").Delete();

            allItems = GetErrorListItems(errorList, 0);
            Assert.AreEqual(0, allItems.Count);
        }

        /// <summary>
        /// Make sure deleting a file w/ errors clears the error list
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void IndentationInconsistencyWarning() {
            GetOptions().IndentationInconsistencySeverity = Severity.Warning;
            var project = DebugProject.OpenProject(@"TestData\InconsistentIndentation.sln");
            var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte);

            System.Threading.Thread.Sleep(5000);
            var errorList = (IVsErrorList)VsIdeTestHostContext.ServiceProvider.GetService(typeof(SVsErrorList));

            const int expectedItems = 1;
            List<IVsTaskItem> allItems = GetErrorListItems(errorList, expectedItems);
            Assert.AreEqual(expectedItems, allItems.Count);

            VSTASKPRIORITY[] pri = new VSTASKPRIORITY[1];
            ErrorHandler.ThrowOnFailure(allItems[0].get_Priority(pri));
            Assert.AreEqual(VSTASKPRIORITY.TP_NORMAL, pri[0]);
        }

        /// <summary>
        /// Make sure deleting a file w/ errors clears the error list
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void IndentationInconsistencyError() {
            GetOptions().IndentationInconsistencySeverity = Severity.Error;
            var project = DebugProject.OpenProject(@"TestData\InconsistentIndentation.sln");

            var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte);

            var errorList = (IVsErrorList)VsIdeTestHostContext.ServiceProvider.GetService(typeof(SVsErrorList));

            const int expectedItems = 1;
            List<IVsTaskItem> allItems = GetErrorListItems(errorList, expectedItems);
            Assert.AreEqual(expectedItems, allItems.Count);

            VSTASKPRIORITY[] pri = new VSTASKPRIORITY[1];
            ErrorHandler.ThrowOnFailure(allItems[0].get_Priority(pri));
            Assert.AreEqual(VSTASKPRIORITY.TP_HIGH, pri[0]);
        }

        /// <summary>
        /// Make sure deleting a file w/ errors clears the error list
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void IndentationInconsistencyIgnore() {
            GetOptions().IndentationInconsistencySeverity = Severity.Ignore;

            var project = DebugProject.OpenProject(@"TestData\InconsistentIndentation.sln");
            var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte);

            var errorList = (IVsErrorList)VsIdeTestHostContext.ServiceProvider.GetService(typeof(SVsErrorList));

            const int expectedItems = 0;
            List<IVsTaskItem> allItems = GetErrorListItems(errorList, expectedItems);
            Assert.AreEqual(expectedItems, allItems.Count);
        }

        /// <summary>
        /// Debug as script when no project
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DebugAsScriptNoProject() {
            string scriptFilePath = TestData.GetPath(@"TestData\HelloWorld\Program.py");

            DeleteAllBreakPoints();

            VsIdeTestHostContext.Dte.ItemOperations.OpenFile(scriptFilePath);
            VsIdeTestHostContext.Dte.Debugger.Breakpoints.Add(File: scriptFilePath, Line: 1);
            VsIdeTestHostContext.Dte.ExecuteCommand("EditorContextMenus.CodeWindow.DebugasScript");
            WaitForMode(dbgDebugMode.dbgBreakMode);
            Assert.AreEqual(VsIdeTestHostContext.Dte.Debugger.CurrentMode, dbgDebugMode.dbgBreakMode);
            Assert.AreEqual(VsIdeTestHostContext.Dte.Debugger.BreakpointLastHit.Name, "Program.py, line 1");
            VsIdeTestHostContext.Dte.Debugger.Go(WaitForBreakOrEnd: true);
            Assert.AreEqual(VsIdeTestHostContext.Dte.Debugger.CurrentMode, dbgDebugMode.dbgDesignMode);
        }

        /// <summary>
        /// Debug as script not in project
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DebugAsScriptNotInProject() {
            string scriptFilePath = TestData.GetPath(@"TestData\HelloWorld\Program.py");

            OpenDebuggerProject();

            VsIdeTestHostContext.Dte.ItemOperations.OpenFile(scriptFilePath);
            VsIdeTestHostContext.Dte.Debugger.Breakpoints.Add(File: scriptFilePath, Line: 1);
            VsIdeTestHostContext.Dte.ExecuteCommand("EditorContextMenus.CodeWindow.DebugasScript");
            WaitForMode(dbgDebugMode.dbgBreakMode);
            Assert.AreEqual(VsIdeTestHostContext.Dte.Debugger.CurrentMode, dbgDebugMode.dbgBreakMode);
            Assert.AreEqual(VsIdeTestHostContext.Dte.Debugger.BreakpointLastHit.Name, "Program.py, line 1");
            VsIdeTestHostContext.Dte.Debugger.Go(WaitForBreakOrEnd: true);
            Assert.AreEqual(VsIdeTestHostContext.Dte.Debugger.CurrentMode, dbgDebugMode.dbgDesignMode);
        }

        /// <summary>
        /// Debug as script in project
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DebugAsScriptInProject() {
            string scriptFilePath = TestData.GetPath(@"TestData\DebuggerProject\Program.py");

            OpenDebuggerProject();

            VsIdeTestHostContext.Dte.ItemOperations.OpenFile(scriptFilePath);
            VsIdeTestHostContext.Dte.Debugger.Breakpoints.Add(File: scriptFilePath, Line: 1);
            VsIdeTestHostContext.Dte.ExecuteCommand("EditorContextMenus.CodeWindow.DebugasScript");
            WaitForMode(dbgDebugMode.dbgBreakMode);
            Assert.AreEqual(VsIdeTestHostContext.Dte.Debugger.CurrentMode, dbgDebugMode.dbgBreakMode);
            Assert.AreEqual(VsIdeTestHostContext.Dte.Debugger.BreakpointLastHit.Name, "Program.py, line 1");
            VsIdeTestHostContext.Dte.Debugger.Go(WaitForBreakOrEnd: true);
            Assert.AreEqual(VsIdeTestHostContext.Dte.Debugger.CurrentMode, dbgDebugMode.dbgDesignMode);
        }

        /// <summary>
        /// Debug as script no script
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DebugAsScriptNoScript() {
            try {
                VsIdeTestHostContext.Dte.ExecuteCommand("EditorContextMenus.CodeWindow.DebugasScript");
            }
            catch (COMException e) {
                // Requires an opened python file with focus
                Assert.IsTrue(e.ToString().Contains("is not available"));
            }
        }

        private static List<IVsTaskItem> GetErrorListItems(IVsErrorList errorList, int expectedItems) {
            List<IVsTaskItem> allItems = new List<IVsTaskItem>();
            for (int i = 0; i < 10; i++) {
                allItems.Clear();
                IVsEnumTaskItems items;
                ErrorHandler.ThrowOnFailure(((IVsTaskList)errorList).EnumTaskItems(out items));

                IVsTaskItem[] taskItems = new IVsTaskItem[1];

                uint[] itemCnt = new uint[1];

                while (ErrorHandler.Succeeded(items.Next(1, taskItems, itemCnt)) && itemCnt[0] == 1) {                    
                    allItems.Add(taskItems[0]);
                }
                if (allItems.Count == expectedItems) {
                    break;
                }
                // give time for errors to process...
                System.Threading.Thread.Sleep(1000);
            }
            return allItems;
        }

        private static void DeleteAllBreakPoints() {
            var debug3 = (Debugger3)VsIdeTestHostContext.Dte.Debugger;
            if (debug3.Breakpoints != null) {                               
                foreach (var bp in debug3.Breakpoints) {                    
                    ((Breakpoint3)bp).Delete();
                }
            }
        }

        protected static IPythonOptions GetOptions() {
            return (IPythonOptions)VsIdeTestHostContext.Dte.GetObject("VsPython");
        }


        
        #endregion

        #region Helpers

       
        internal static Project OpenDebuggerProject(string startItem = null) {
            return OpenProject(@"TestData\DebuggerProject.sln", startItem);
        }

        private static Project OpenDebuggerProjectAndBreak(string startItem, int lineNo, bool setStartupItem = true) {
            return OpenProjectAndBreak(@"TestData\DebuggerProject.sln", startItem, lineNo);
        }

        internal static Project OpenProject(string projName, string startItem = null, int expectedProjects = 1, string projectName = null, bool setStartupItem = true) {
            string fullPath = TestData.GetPath(projName);
            Assert.IsTrue(File.Exists(fullPath), "Cannot find " + fullPath);
            VsIdeTestHostContext.Dte.Solution.Open(fullPath);

            Assert.IsTrue(VsIdeTestHostContext.Dte.Solution.IsOpen, "The solution is not open");

            int count = VsIdeTestHostContext.Dte.Solution.Projects.Count;
            if (expectedProjects != count) {
                // if we have other files open we can end up with a bonus project...
                int i = 0;
                foreach (EnvDTE.Project proj in VsIdeTestHostContext.Dte.Solution.Projects) {
                    if (proj.Name != "Miscellaneous Files") {
                        i++;
                    }
                }

                Assert.IsTrue(i == expectedProjects, String.Format("Loading project resulted in wrong number of loaded projects, expected 1, received {0}", VsIdeTestHostContext.Dte.Solution.Projects.Count));
            }
            
            var iter = VsIdeTestHostContext.Dte.Solution.Projects.GetEnumerator();
            iter.MoveNext();
            
            Project project = (Project)iter.Current;
            if (projectName != null) {
                while (project.Name != projectName) {
                    if (!iter.MoveNext()) {
                        Assert.Fail("Failed to find project named " + projectName);
                    }
                    project = (Project)iter.Current;
                }
            }

            if (startItem != null && setStartupItem) {
                project.SetStartupFile(startItem);
            }

            DeleteAllBreakPoints();

            return project;
        }

        private static string GetOutputWindowDebugPaneText() {
            OutputWindow window = ((EnvDTE80.DTE2)VsIdeTestHostContext.Dte).ToolWindows.OutputWindow;
            OutputWindowPane debugPane = window.OutputWindowPanes.Item("Debug");
            debugPane.Activate();
            var debugDoc = debugPane.TextDocument;
            string debugText = debugDoc.StartPoint.CreateEditPoint().GetText(debugDoc.EndPoint);
            return debugText;
        }

        private static void WaitForDebugOutput(Predicate<string> condition) {
            for (int i = 0; i < 50 && !condition(GetOutputWindowDebugPaneText()); i++) {
                Thread.Sleep(100);
            }

            Assert.IsTrue(condition(GetOutputWindowDebugPaneText()));
        }

        private static void StartHelloWorldAndBreak() {
            OpenProjectAndBreak(@"TestData\HelloWorld.sln", "Program.py", 1);
        }

        internal static Project OpenProjectAndBreak(string projName, string filename, int lineNo, bool setStartupItem = true) {
            var project = OpenProject(projName, filename, setStartupItem: setStartupItem);

            VsIdeTestHostContext.Dte.Debugger.Breakpoints.Add(File: filename, Line: lineNo);
            VsIdeTestHostContext.Dte.ExecuteCommand("Debug.Start");

            WaitForMode(dbgDebugMode.dbgBreakMode);

            Assert.AreEqual(VsIdeTestHostContext.Dte.Debugger.BreakpointLastHit.FileLine, lineNo);
            return project;
        }


        internal static void WaitForMode(dbgDebugMode mode) {
            for (int i = 0; i < 300 && VsIdeTestHostContext.Dte.Debugger.CurrentMode != mode; i++) {
                Thread.Sleep(100);
            }

            Assert.AreEqual(VsIdeTestHostContext.Dte.Debugger.CurrentMode, mode);
        }
        
        #endregion
    }
}
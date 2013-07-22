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
using System.Linq;
using EnvDTE;
using EnvDTE80;
using EnvDTE90;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.PythonTools.Interpreter;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.UI;
using TestUtilities.UI.Python;
using Path = System.IO.Path;
using SD = System.Diagnostics;


namespace DebuggerUITests {
    /// <summary>
    /// Summary description for AttachTest
    /// </summary>
    [TestClass]
    public class AttachTest {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            TestData.Deploy();
        }

        public AttachTest() {
            //
            // TODO: Add constructor logic here
            //
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext {
            get {
                return testContextInstance;
            }
            set {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }

        [TestCleanup()]
        public void MyTestCleanup() {
            VsIdeTestHostContext.Dte.Solution.Close(false);
        }

        #endregion
        #region Tests
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestAttachBasic() {

            string debugSolution = TestData.GetPath(@"TestData\DebugAttach\DebugAttach.sln");
            string startFile = "Simple.py";

            Debugger2 dbg2 = (Debugger2)VsIdeTestHostContext.Dte.Debugger;

            SD.Process processToAttach = OpenSolutionAndLaunchFile(debugSolution, startFile, "", "");

            try {
                AttachAndWaitForMode(processToAttach, AD7Engine.DebugEngineName, dbgDebugMode.dbgRunMode);
            } finally {
                dbg2.DetachAll();
                DebugProject.WaitForMode(dbgDebugMode.dbgDesignMode);
                if (!processToAttach.HasExited) processToAttach.Kill();
            }
            return;

        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestAttachBreakImmediately() {

            string debugSolution = TestData.GetPath(@"TestData\DebugAttach\DebugAttach.sln");
            string startFile = "Simple.py";
            int breakLine = 22;

            Debugger2 dbg2 = (Debugger2)VsIdeTestHostContext.Dte.Debugger;

            SD.Process processToAttach = OpenSolutionAndLaunchFile(debugSolution, startFile, "", "");

            VsIdeTestHostContext.Dte.Debugger.Breakpoints.Add(File: startFile, Line: breakLine);

            try {
                AttachAndWaitForMode(processToAttach, AD7Engine.DebugEngineName, dbgDebugMode.dbgBreakMode);
            } finally {
                dbg2.DetachAll();
                DebugProject.WaitForMode(dbgDebugMode.dbgDesignMode);
                if (!processToAttach.HasExited) processToAttach.Kill();
            }
            return;

        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestAttachUserSetsBreakpoint() {

            string debugSolution = TestData.GetPath(@"TestData\DebugAttach\DebugAttach.sln");
            string startFile = "Simple.py";
            int breakLine = 22;

            Debugger2 dbg2 = (Debugger2)VsIdeTestHostContext.Dte.Debugger;

            SD.Process processToAttach = OpenSolutionAndLaunchFile(debugSolution, startFile, "", "");

            try {
                AttachAndWaitForMode(processToAttach, AD7Engine.DebugEngineName, dbgDebugMode.dbgRunMode);
                dbg2.Breakpoints.Add(File: startFile, Line: breakLine);
                DebugProject.WaitForMode(dbgDebugMode.dbgBreakMode);

            } finally {
                dbg2.DetachAll();
                DebugProject.WaitForMode(dbgDebugMode.dbgDesignMode);
                if (!processToAttach.HasExited) processToAttach.Kill();
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestAttachThreadsBreakAllAndSetExitFlag() {
            string debugSolution = TestData.GetPath(@"TestData\DebugAttach\DebugAttach.sln");
            string startFile = "fg.py";

            Debugger2 dbg2 = (Debugger2)VsIdeTestHostContext.Dte.Debugger;

            SD.Process processToAttach = OpenSolutionAndLaunchFile(debugSolution, startFile, "", "");

            try {
                Process2 proc = AttachAndWaitForMode(processToAttach, AD7Engine.DebugEngineName, dbgDebugMode.dbgRunMode);
                dbg2.Break(WaitForBreakMode: false);
                DebugProject.WaitForMode(dbgDebugMode.dbgBreakMode);

                var x = proc.Threads.Cast<Thread2>()
                    .SelectMany<Thread2, StackFrame>(t => t.StackFrames.Cast<StackFrame>())
                    .SelectMany<StackFrame, Expression>(f => f.Locals.Cast<Expression>())
                    .Where(e => e.Name == "exit_flag")
                    .First();

                Assert.IsNotNull(x, "Couldn't find a frame with 'exit_flag' defined!");

                x.Value = "True";

                dbg2.Go(WaitForBreakOrEnd: false);
                DebugProject.WaitForMode(dbgDebugMode.dbgDesignMode);

            } finally {
                if (!processToAttach.HasExited) processToAttach.Kill();
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestAttachThreadsBreakOneAndSetExitFlag() {
            string debugSolution = TestData.GetPath(@"TestData\DebugAttach\DebugAttach.sln");
            string startFile = "fg.py";
            int breakLine = 8;

            Debugger2 dbg2 = (Debugger2)VsIdeTestHostContext.Dte.Debugger;

            SD.Process processToAttach = OpenSolutionAndLaunchFile(debugSolution, startFile, "", "");

            try {
                Process2 proc = AttachAndWaitForMode(processToAttach, AD7Engine.DebugEngineName, dbgDebugMode.dbgRunMode);
                dbg2.Breakpoints.Add(File: startFile, Line: breakLine);
                DebugProject.WaitForMode(dbgDebugMode.dbgBreakMode);
                dbg2.BreakpointLastHit.Delete();

                var x = proc.Threads.Cast<Thread2>()
                    .SelectMany<Thread2, StackFrame>(t => t.StackFrames.Cast<StackFrame>())
                    .SelectMany<StackFrame, Expression>(f => f.Locals.Cast<Expression>())
                    .Where(e => e.Name == "exit_flag")
                    .First();

                Assert.IsNotNull(x, "Couldn't find a frame with 'exit_flag' defined!");

                x.Value = "True";

                dbg2.Go(WaitForBreakOrEnd: false);
                DebugProject.WaitForMode(dbgDebugMode.dbgDesignMode);

            } finally {
                if (!processToAttach.HasExited) processToAttach.Kill();
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestAttachLotsOfThreads() {
            string debugSolution = TestData.GetPath(@"TestData\DebugAttach\DebugAttach.sln");
            string startFile = "LotsOfThreads.py";

            Debugger2 dbg2 = (Debugger2)VsIdeTestHostContext.Dte.Debugger;

            SD.Process processToAttach = OpenSolutionAndLaunchFile(debugSolution, startFile, "", "");
            System.Threading.Thread.Sleep(2000);

            try {
                Process2 proc = AttachAndWaitForMode(processToAttach, AD7Engine.DebugEngineName, dbgDebugMode.dbgRunMode);

            } finally {
                if (!processToAttach.HasExited) processToAttach.Kill();
            }
        }

        ///TODO: TestAttachThreadsMakingProgress
        /// <summary>
        /// See workitem http://pytools.codeplex.com/workitem/456 
        /// </summary>
        /// <param name="debugSolution"></param>
        /// <param name="startFile"></param>
        /// <param name="interpreterArgs"></param>
        /// <param name="programArgs"></param>
        /// <returns></returns>

        #endregion

        #region Helper methods
        private static SD.Process OpenSolutionAndLaunchFile(string debugSolution, string startFile, string interpreterArgs, string programArgs) {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var project = app.OpenAndFindProject(debugSolution, startFile);
            return LaunchFileFromProject(project, startFile, interpreterArgs, programArgs);
        }

        private static Process2 AttachAndWaitForMode(SD.Process processToAttach, object debugEngines, dbgDebugMode expectedMode) {
            Debugger2 dbg2 = (Debugger2)VsIdeTestHostContext.Dte.Debugger;
            System.Threading.Thread.Sleep(1000);
            Process2 result = null;
            Transport t = dbg2.Transports.Item("Default");
            bool foundit = false;
            foreach (Process2 p in dbg2.LocalProcesses) {
                if (p.ProcessID == processToAttach.Id) {
                    foundit = true;
                    p.Attach2(debugEngines);
                    result = p;
                    break;
                }
            }
            Assert.IsTrue(foundit, "The process to attach [{0}] could not be found in LocalProcesses (did it exit immediately?)", processToAttach.Id);
            DebugProject.WaitForMode(expectedMode);
            return result;
        }

        public static SD.Process LaunchFileFromProject(EnvDTE.Project project, string filename, string interpreterArgs, string programArgs) {

            var item = project.ProjectItems.Item(filename);
            var window = item.Open();
            window.Activate();
            var doc = item.Document;
            var docFN = doc.FullName;
            string fullFilename = Path.GetFullPath(docFN);

            string cmdlineArgs = String.Format("{0} \"{1}\" {2}", interpreterArgs, fullFilename, programArgs);

            string projectInterpreter = GetProjectInterpreterOrDefault(project);

            var psi = new SD.ProcessStartInfo(projectInterpreter, cmdlineArgs);
            psi.RedirectStandardError = true;
            psi.RedirectStandardOutput = true;
            psi.UseShellExecute = false;
            var p = SD.Process.Start(psi);
            p.EnableRaisingEvents = true;
            string output = "";
            p.OutputDataReceived += (sender, args) => {
                output += args.Data;
            };
            p.ErrorDataReceived += (sender, args) => {
                output += args.Data;
            };
            p.BeginErrorReadLine();
            p.BeginOutputReadLine();
            p.Exited += (sender, args) => {
                SD.Debug.WriteLine("Process Id ({0}) exited with ExitCode: {1}", p.Id, p.ExitCode);
                SD.Debug.WriteLine(String.Format("Output: {0}", output));
            };

            Assert.IsNotNull(p, "Failure to start process, {0} {1} ", projectInterpreter, cmdlineArgs);
            return p;
        }

        public static string GetProjectInterpreterOrDefault(EnvDTE.Project project) {
            var interpreterId = (string)project.Properties.Item("InterpreterId").Value;
            var interpreterVersion = (string)project.Properties.Item("InterpreterVersion").Value;
            var interpreterPath = (string)project.Properties.Item("InterpreterPath").Value;
            
            // use the project's custom interpreter path if defined
            if (!String.IsNullOrWhiteSpace(interpreterPath)) {
                interpreterPath = Path.GetFullPath(interpreterPath);
                SD.Debug.WriteLine("Using project specified interpreter path: {0}", interpreterPath, null);
                return interpreterPath;
            }

            var interpService = PythonToolsPackage.ComponentModel.GetService<IInterpreterOptionsService>();
            var interpreter = interpService.FindInterpreter(interpreterId, interpreterVersion);

            // use the project's interpreter if we can find it
            if (interpreter != null) {
                interpreterPath = Path.GetFullPath(interpreter.Configuration.InterpreterPath);
                SD.Debug.WriteLine("Using project specified interpreter: {0}", interpreter, null);
                return interpreterPath;
            }
            // use the VS instance's default interpreter if there is one
            SD.Debug.WriteLine("Project specified interpreter not found: {0} - {1}", interpreterId, interpreterVersion);
            interpreter = interpService.DefaultInterpreter;
            if (interpreter != null) {
                interpreterPath = Path.GetFullPath(interpreter.Configuration.InterpreterPath);
                SD.Debug.WriteLine("Using VS default interpreter: {0}", interpreter, null);
                return interpreterPath;
            }
            // fail
            Assert.Fail("There were no available interpreters. Could not launch project.");
            return null;

        }

        #endregion
    }
}

////EnvDTE80.Debugger2

//var atp = app.OpenDebugAttach();

//var sctpd = atp.SelectCodeTypeForDebugging();
//sctpd.SetDebugSpecificCodeTypes();

//foreach (var codeType in sctpd.AvailableCodeTypes.Items) {
//    if (codeType.Name == AD7Engine.DebugEngineName) codeType.SetSelected();
//    else codeType.SetUnselected();
//}

//sctpd.ClickOk();

//atp.SelectProcessForDebuggingByName("python.exe");
//atp.ClickAttach();
//DebugProject.WaitForMode(dbgDebugMode.dbgRunMode);

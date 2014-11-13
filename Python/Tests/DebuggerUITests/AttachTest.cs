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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;
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
            PythonTestData.Deploy();
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void TestAttachBasic() {

            string debugSolution = TestData.GetPath(@"TestData\DebugAttach\DebugAttach.sln");
            string startFile = "Simple.py";


            using (var app = new VisualStudioApp()) {
                var dbg2 = (Debugger2)app.Dte.Debugger;
                SD.Process processToAttach = OpenSolutionAndLaunchFile(app, debugSolution, startFile, "", "");

                try {
                    AttachAndWaitForMode(app, processToAttach, AD7Engine.DebugEngineName, dbgDebugMode.dbgRunMode);
                } finally {
                    dbg2.DetachAll();
                    DebugProject.WaitForMode(app, dbgDebugMode.dbgDesignMode);
                    if (!processToAttach.HasExited) processToAttach.Kill();
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void TestAttachBreakImmediately() {

            string debugSolution = TestData.GetPath(@"TestData\DebugAttach\DebugAttach.sln");
            string startFile = "Simple.py";
            int breakLine = 22;

            using (var app = new VisualStudioApp()) {
                var dbg2 = (Debugger2)app.Dte.Debugger;
                SD.Process processToAttach = OpenSolutionAndLaunchFile(app, debugSolution, startFile, "", "");

                app.Dte.Debugger.Breakpoints.Add(File: startFile, Line: breakLine);

                try {
                    AttachAndWaitForMode(app, processToAttach, AD7Engine.DebugEngineName, dbgDebugMode.dbgBreakMode);
                } finally {
                    dbg2.DetachAll();
                    DebugProject.WaitForMode(app, dbgDebugMode.dbgDesignMode);
                    if (!processToAttach.HasExited) processToAttach.Kill();
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void TestAttachUserSetsBreakpoint() {

            string debugSolution = TestData.GetPath(@"TestData\DebugAttach\DebugAttach.sln");
            string startFile = "Simple.py";
            int breakLine = 22;

            using (var app = new VisualStudioApp()) {
                var dbg2 = (Debugger2)app.Dte.Debugger;
                SD.Process processToAttach = OpenSolutionAndLaunchFile(app, debugSolution, startFile, "", "");

                try {
                    AttachAndWaitForMode(app, processToAttach, AD7Engine.DebugEngineName, dbgDebugMode.dbgRunMode);
                    dbg2.Breakpoints.Add(File: startFile, Line: breakLine);
                    DebugProject.WaitForMode(app, dbgDebugMode.dbgBreakMode);

                } finally {
                    dbg2.DetachAll();
                    DebugProject.WaitForMode(app, dbgDebugMode.dbgDesignMode);
                    if (!processToAttach.HasExited) processToAttach.Kill();
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void TestAttachThreadsBreakAllAndSetExitFlag() {
            string debugSolution = TestData.GetPath(@"TestData\DebugAttach\DebugAttach.sln");
            string startFile = "fg.py";

            using (var app = new VisualStudioApp()) {
                var dbg2 = (Debugger2)app.Dte.Debugger;
                SD.Process processToAttach = OpenSolutionAndLaunchFile(app, debugSolution, startFile, "", "");

                try {
                    Process2 proc = AttachAndWaitForMode(app, processToAttach, AD7Engine.DebugEngineName, dbgDebugMode.dbgRunMode);
                    dbg2.Break(WaitForBreakMode: false);
                    DebugProject.WaitForMode(app, dbgDebugMode.dbgBreakMode);

                    var x = proc.Threads.Cast<Thread2>()
                        .SelectMany<Thread2, StackFrame>(t => t.StackFrames.Cast<StackFrame>())
                        .SelectMany<StackFrame, Expression>(f => f.Locals.Cast<Expression>())
                        .Where(e => e.Name == "exit_flag")
                        .First();

                    Assert.IsNotNull(x, "Couldn't find a frame with 'exit_flag' defined!");

                    x.Value = "True";

                    dbg2.Go(WaitForBreakOrEnd: false);
                    DebugProject.WaitForMode(app, dbgDebugMode.dbgDesignMode);

                } finally {
                    if (!processToAttach.HasExited) processToAttach.Kill();
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void TestAttachThreadsBreakOneAndSetExitFlag() {
            string debugSolution = TestData.GetPath(@"TestData\DebugAttach\DebugAttach.sln");
            string startFile = "fg.py";
            int breakLine = 8;

            using (var app = new VisualStudioApp()) {
                var dbg2 = (Debugger2)app.Dte.Debugger;
                SD.Process processToAttach = OpenSolutionAndLaunchFile(app, debugSolution, startFile, "", "");

                try {
                    Process2 proc = AttachAndWaitForMode(app, processToAttach, AD7Engine.DebugEngineName, dbgDebugMode.dbgRunMode);
                    dbg2.Breakpoints.Add(File: startFile, Line: breakLine);
                    DebugProject.WaitForMode(app, dbgDebugMode.dbgBreakMode);
                    dbg2.BreakpointLastHit.Delete();

                    var x = proc.Threads.Cast<Thread2>()
                        .SelectMany<Thread2, StackFrame>(t => t.StackFrames.Cast<StackFrame>())
                        .SelectMany<StackFrame, Expression>(f => f.Locals.Cast<Expression>())
                        .Where(e => e.Name == "exit_flag")
                        .First();

                    Assert.IsNotNull(x, "Couldn't find a frame with 'exit_flag' defined!");

                    x.Value = "True";

                    dbg2.Go(WaitForBreakOrEnd: false);
                    DebugProject.WaitForMode(app, dbgDebugMode.dbgDesignMode);

                } finally {
                    if (!processToAttach.HasExited) processToAttach.Kill();
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void TestAttachLotsOfThreads() {
            string debugSolution = TestData.GetPath(@"TestData\DebugAttach\DebugAttach.sln");
            string startFile = "LotsOfThreads.py";

            using (var app = new VisualStudioApp()) {
                var dbg2 = (Debugger2)app.Dte.Debugger;
                SD.Process processToAttach = OpenSolutionAndLaunchFile(app, debugSolution, startFile, "", "");
                System.Threading.Thread.Sleep(2000);

                try {
                    Process2 proc = AttachAndWaitForMode(app, processToAttach, AD7Engine.DebugEngineName, dbgDebugMode.dbgRunMode);

                } finally {
                    if (!processToAttach.HasExited) processToAttach.Kill();
                }
            }
        }

        //TODO: TestAttachThreadsMakingProgress
        // See workitem http://pytools.codeplex.com/workitem/456 

        #region Helper methods

        private static SD.Process OpenSolutionAndLaunchFile(VisualStudioApp app, string debugSolution, string startFile, string interpreterArgs, string programArgs) {
            var project = app.OpenProject(debugSolution, startFile);
            return LaunchFileFromProject(project, startFile, interpreterArgs, programArgs);
        }

        private static Process2 AttachAndWaitForMode(VisualStudioApp app, SD.Process processToAttach, object debugEngines, dbgDebugMode expectedMode) {
            Debugger2 dbg2 = (Debugger2)app.Dte.Debugger;
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
            DebugProject.WaitForMode(app, expectedMode);
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

            var projectInterpreter = project.GetPythonProject().GetInterpreterFactory().Configuration.InterpreterPath;

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
//DebugProject.WaitForMode(app, dbgDebugMode.dbgRunMode);

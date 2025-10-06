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
using System.IO;
using System.Linq;
using EnvDTE;
using EnvDTE80;
using EnvDTE90;
using Microsoft.PythonTools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools;
using TestUtilities.UI;
using SD = System.Diagnostics;

namespace DebuggerUITests {
    public class AttachUITests {
        private readonly Guid PythonDebugEngineGuid = new Guid("86432F39-ADFD-4C56-AA8F-AF8FCDC66039");
        
        #region Test Cases
        public void AttachBasic(VisualStudioApp app) {
            string debugSolution = app.CopyProjectForTest(@"TestData\DebugAttach\DebugAttach.sln");
            string startFile = "Simple.py";

            var dbg2 = (Debugger2)app.Dte.Debugger;
            SD.Process processToAttach = OpenSolutionAndLaunchFileWithoutDebugging(app, debugSolution, startFile, "", "");

            try {
                AttachAndWaitForMode(app, processToAttach, PythonDebugEngineGuid.ToString("B"), dbgDebugMode.dbgRunMode);
            } finally {
                dbg2.DetachAll();
                DebugProjectUITests.WaitForMode(app, dbgDebugMode.dbgDesignMode);
                if (!processToAttach.HasExited) processToAttach.Kill();
            }
        }

        public void AttachBreakImmediately(VisualStudioApp app) {
            string debugSolution = app.CopyProjectForTest(@"TestData\DebugAttach\DebugAttach.sln");
            string startFile = "Simple.py";
            int breakLine = 22;

            var dbg2 = (Debugger2)app.Dte.Debugger;
            SD.Process processToAttach = OpenSolutionAndLaunchFileWithoutDebugging(app, debugSolution, startFile, "", "");

            app.Dte.Debugger.Breakpoints.Add(File: startFile, Line: breakLine);

            try {
                AttachAndWaitForMode(app, processToAttach, PythonDebugEngineGuid.ToString("B"), dbgDebugMode.dbgBreakMode);
            } finally {
                dbg2.DetachAll();
                DebugProjectUITests.WaitForMode(app, dbgDebugMode.dbgDesignMode);
                if (!processToAttach.HasExited) processToAttach.Kill();
            }
        }

        public void AttachUserSetsBreakpoint(VisualStudioApp app) {
            string debugSolution = app.CopyProjectForTest(@"TestData\DebugAttach\DebugAttach.sln");
            string startFile = "Simple.py";
            int breakLine = 13;

            var dbg2 = (Debugger2)app.Dte.Debugger;
            SD.Process processToAttach = OpenSolutionAndLaunchFileWithoutDebugging(app, debugSolution, startFile, "", "");

            try {
                

                
                AttachAndWaitForMode(app, processToAttach, PythonDebugEngineGuid.ToString("B"), dbgDebugMode.dbgRunMode);
                dbg2.Breakpoints.Add(File: startFile, Line: breakLine);
                
                DebugProjectUITests.WaitForMode(app, dbgDebugMode.dbgBreakMode);
            } finally {
                dbg2.DetachAll();
                DebugProjectUITests.WaitForMode(app, dbgDebugMode.dbgDesignMode);
                if (!processToAttach.HasExited) processToAttach.Kill();
            }
        }

        public void AttachThreadsBreakAllAndSetExitFlag(VisualStudioApp app) {
            string debugSolution = app.CopyProjectForTest(@"TestData\DebugAttach\DebugAttach.sln");
            string startFile = "fg.py";

            var dbg2 = (Debugger2)app.Dte.Debugger;
            SD.Process processToAttach = OpenSolutionAndLaunchFileWithoutDebugging(app, debugSolution, startFile, "", "");

            try {
                Process2 proc = AttachAndWaitForMode(app, processToAttach, PythonDebugEngineGuid.ToString("B"), dbgDebugMode.dbgRunMode);
                dbg2.Break(WaitForBreakMode: false);
                DebugProjectUITests.WaitForMode(app, dbgDebugMode.dbgBreakMode);

                var x = proc.Threads.Cast<Thread2>()
                    .SelectMany<Thread2, StackFrame>(t => t.StackFrames.Cast<StackFrame>())
                    .SelectMany<StackFrame, Expression>(f => f.Locals.Cast<Expression>())
                    .Where(e => e.Name == "exit_flag")
                    .First();

                Assert.IsNotNull(x, "Couldn't find a frame with 'exit_flag' defined!");

                x.Value = "True";

                dbg2.Go(WaitForBreakOrEnd: false);
                DebugProjectUITests.WaitForMode(app, dbgDebugMode.dbgDesignMode);
            } finally {
                if (!processToAttach.HasExited) processToAttach.Kill();
            }
        }

        public void AttachThreadsBreakOneAndSetExitFlag(VisualStudioApp app) {
            string debugSolution = app.CopyProjectForTest(@"TestData\DebugAttach\DebugAttach.sln");
            string startFile = "fg.py";
            int breakLine = 9;

            var dbg2 = (Debugger2)app.Dte.Debugger;
            SD.Process processToAttach = OpenSolutionAndLaunchFileWithoutDebugging(app, debugSolution, startFile, "", "");

            try {
                Process2 proc = AttachAndWaitForMode(app, processToAttach, PythonDebugEngineGuid.ToString("B"), dbgDebugMode.dbgRunMode);
                dbg2.Breakpoints.Add(File: startFile, Line: breakLine);
                DebugProjectUITests.WaitForMode(app, dbgDebugMode.dbgBreakMode);
                dbg2.BreakpointLastHit.Delete();

                var x = proc.Threads.Cast<Thread2>()
                    .SelectMany<Thread2, StackFrame>(t => t.StackFrames.Cast<StackFrame>())
                    .SelectMany<StackFrame, Expression>(f => f.Locals.Cast<Expression>())
                    .Where(e => e.Name == "exit_flag")
                    .First();

                Assert.IsNotNull(x, "Couldn't find a frame with 'exit_flag' defined!");

                x.Value = "True";

                dbg2.Go(WaitForBreakOrEnd: false);
                DebugProjectUITests.WaitForMode(app, dbgDebugMode.dbgDesignMode);
            } finally {
                if (!processToAttach.HasExited) processToAttach.Kill();
            }
        }

        public void AttachLotsOfThreads(VisualStudioApp app) {
            string debugSolution = app.CopyProjectForTest(@"TestData\DebugAttach\DebugAttach.sln");
            string startFile = "LotsOfThreads.py";

            var dbg2 = (Debugger2)app.Dte.Debugger;
            SD.Process processToAttach = OpenSolutionAndLaunchFileWithoutDebugging(app, debugSolution, startFile, "", "");
            System.Threading.Thread.Sleep(2000);

            try {
                Process2 proc = AttachAndWaitForMode(app, processToAttach, PythonDebugEngineGuid.ToString("B"), dbgDebugMode.dbgRunMode);

            } finally {
                if (!processToAttach.HasExited) processToAttach.Kill();
            }
        }

        #endregion

        #region Helper methods

        private static SD.Process OpenSolutionAndLaunchFileWithoutDebugging(VisualStudioApp app, string debugSolution, string startFile, string interpreterArgs, string programArgs) {
            var project = app.OpenProject(debugSolution, startFile);
            return LaunchFileFromProjectWithoutDebugging(app, project, startFile, interpreterArgs, programArgs);
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
            DebugProjectUITests.WaitForMode(app, expectedMode);
            return result;
        }

        private static SD.Process LaunchFileFromProjectWithoutDebugging(VisualStudioApp app, EnvDTE.Project project, string filename, string interpreterArgs, string programArgs) {
            var item = project.ProjectItems.Item(filename);
            var window = item.Open();
            window.Activate();
            var doc = item.Document;
            var docFN = doc.FullName;
            string fullFilename = Path.GetFullPath(docFN);
            // Use VS "Start Without Debugging" so launch closely matches user scenario.
            // (interpreterArgs/programArgs currently unused in existing tests – passed as empty strings.)
            var uiThread = app.GetService<UIThreadBase>();
            var projectInterpreter = uiThread.Invoke(() => project.GetPythonProject().GetLaunchConfigurationOrThrow().GetInterpreterPath());
            var exeName = Path.GetFileName(projectInterpreter);
            var before = SD.Process.GetProcesses()
                .Where(pr => {
                    try { return string.Equals(pr.MainModule.FileName, projectInterpreter, StringComparison.OrdinalIgnoreCase); } catch { return false; }
                })
                .Select(pr => pr.Id)
                .ToHashSet();

            // Ensure document is active (some VS versions require focus for StartWithoutDebugging)
            try { doc.Activate(); } catch { }

            app.Dte.ExecuteCommand("Debug.StartWithoutDebugging");

            SD.Process launched = null;
            const int timeoutMs = 15000;
            var sw = SD.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs && launched == null) {
                foreach (var proc in SD.Process.GetProcesses()) {
                    if (before.Contains(proc.Id)) continue;
                    try {
                        if (string.Equals(proc.MainModule.FileName, projectInterpreter, StringComparison.OrdinalIgnoreCase)) {
                            launched = proc;
                            break;
                        }
                    } catch { /* access denied / race */ }
                }
                if (launched == null) System.Threading.Thread.Sleep(200);
            }

            Assert.IsNotNull(launched, $"Failed to detect launched process for interpreter '{projectInterpreter}'");
            launched.EnableRaisingEvents = true;
            launched.Exited += (sender, args) => {
                SD.Debug.WriteLine("Process Id ({0}) exited with ExitCode: {1}", launched.Id, launched.ExitCode);
            };
            return launched;
        }

        #endregion
    }
}

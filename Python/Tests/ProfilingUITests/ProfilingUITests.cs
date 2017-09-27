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
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Profiling;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;
using TestUtilities.UI.Python;

namespace ProfilingUITests {
    //[TestClass]
    public class ProfilingUITests {
        bool _waitOnNormalExit, _waitOnAbnormalExit;

        [TestInitialize]
        public void TestInitialize(VisualStudioApp app) {
            IVsShell shell = (IVsShell)app.ServiceProvider.GetService(typeof(IVsShell));
            Guid perfGuid = new Guid("{F4A63B2A-49AB-4b2d-AA59-A10F01026C89}");
            int installed;
            ErrorHandler.ThrowOnFailure(
                shell.IsPackageInstalled(ref perfGuid, out installed)
            );
            if (installed == 0) {
                Assert.Fail("Profiling is not installed");
                return;
            }

            PythonToolsService pyService;
            try {
                pyService = app.ServiceProvider.GetPythonToolsService_NotThreadSafe();
            } catch (InvalidOperationException) {
                // Nothing to initialize
                return;
            }
            _waitOnNormalExit = pyService.DebuggerOptions.WaitOnNormalExit;
            _waitOnAbnormalExit = pyService.DebuggerOptions.WaitOnAbnormalExit;
            pyService.DebuggerOptions.WaitOnNormalExit = false;
            pyService.DebuggerOptions.WaitOnAbnormalExit = false;
        }

        [TestCleanup]
        public void DeleteVspFiles(VisualStudioApp app) {
            try {
                foreach (var file in Directory.EnumerateFiles(Path.GetTempPath(), "*.vsp", SearchOption.TopDirectoryOnly)) {
                    try {
                        File.Delete(file);
                    } catch {
                        // Weak attempt only
                    }
                }
            } catch {
            }

            PythonToolsService pyService;
            try {
                pyService = app.ServiceProvider.GetPythonToolsService_NotThreadSafe();
            } catch (InvalidOperationException) {
                // Nothing to uninitialize
                return;
            }
            pyService.DebuggerOptions.WaitOnNormalExit = _waitOnNormalExit;
            pyService.DebuggerOptions.WaitOnAbnormalExit = _waitOnAbnormalExit;
        }

        private string SaveDirectory {
            get {
                var p = TestData.GetTempPath();
                Console.WriteLine($"Saving to {p}");
                return p;
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void DefaultInterpreterSelected(PythonVisualStudioApp app) {
            var service = app.InterpreterService;
            var options = app.OptionsService;
            var originalDefault = options.DefaultInterpreter;

            try {
                foreach (var interpreter in service.Interpreters) {
                    options.DefaultInterpreter = interpreter;
                    using (var dialog = app.LaunchPythonProfiling()) {
                        Assert.AreEqual(interpreter.Configuration.Description, dialog.SelectedInterpreter);
                    }
                    app.WaitForDialogDismissed();
                }
            } finally {
                options.DefaultInterpreter = originalDefault;
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void StartupProjectSelected(PythonVisualStudioApp app) {
            app.OpenProject(TestData.GetPath(@"TestData\MultiProjectAnalysis\MultiProjectAnalysis.sln"));

            foreach (var project in app.Dte.Solution.Projects.Cast<EnvDTE.Project>()) {
                var tree = app.OpenSolutionExplorer();
                var item = tree.FindByName(project.Name);
                item.Select();
                app.Dte.ExecuteCommand("Project.SetasStartupProject");

                using (var dialog = app.LaunchPythonProfiling()) {
                    Assert.AreEqual(project.Name, dialog.SelectedProject);
                }
                app.WaitForDialogDismissed();
            }
        }

        private IPythonProfileSession LaunchSession(
            PythonVisualStudioApp app,
            Func<IPythonProfileSession> creator
        ) {
            // Ensure the performance window has been opened, which will make
            // the app clean up all sessions when it is disposed.
            app.OpenPythonPerformance();

            IPythonProfileSession session = null;
            ExceptionDispatchInfo edi = null;
            var task = Task.Factory.StartNew(() => {
                try {
                    session = creator();
                } catch (Exception ex) {
                    edi = ExceptionDispatchInfo.Capture(ex);
                }
                // Must fault the task to abort the wait
                throw new Exception();
            });
            var dialog = app.WaitForDialog(task);
            if (dialog != IntPtr.Zero) {
                using (var saveDialog = new SaveDialog(app, AutomationElement.FromHandle(dialog))) {

                    var originalDestName = Path.Combine(SaveDirectory, Path.GetFileName(saveDialog.FileName));
                    var destName = originalDestName;

                    while (File.Exists(destName)) {
                        destName = string.Format("{0} {1}{2}",
                            Path.GetFileNameWithoutExtension(originalDestName),
                            Guid.NewGuid(),
                            Path.GetExtension(originalDestName)
                        );
                    }

                    saveDialog.FileName = destName;
                    saveDialog.Save();
                    try {
                        task.Wait(TimeSpan.FromSeconds(5.0));
                        Assert.Fail("Task did not fault");
                    } catch (AggregateException) {
                    }
                }
            } else {
                // Ensure the exception is observed
                var ex = task.Exception;
            }
            edi?.Throw();
            Assert.IsNotNull(session, "Session was not correctly initialized");
            return session;
        }

        private IPythonProfileSession LaunchProcess(
            PythonVisualStudioApp app,
            IPythonProfiling profiling,
            string interpreterPath,
            string filename,
            string directory,
            string arguments,
            bool openReport
        ) {
            return LaunchSession(app,
                () => profiling.LaunchProcess(
                    interpreterPath,
                    filename,
                    directory,
                    "",
                    openReport
                )
            );
        }

        private IPythonProfileSession LaunchProject(
            PythonVisualStudioApp app,
            IPythonProfiling profiling,
            EnvDTE.Project project,
            string directory,
            bool openReport
        ) {
            return LaunchSession(app, () => profiling.LaunchProject(project, openReport));
        }

        private void OpenProfileTestProject(
            PythonVisualStudioApp app,
            out EnvDTE.Project project,
            out IPythonProfiling profiling,
            string projectFile = @"TestData\ProfileTest.sln"
        ) {
            profiling = (IPythonProfiling)app.Dte.GetObject("PythonProfiling");

            // no sessions yet
            Assert.IsNull(profiling.GetSession(1));

            if (string.IsNullOrEmpty(projectFile)) {
                project = null;
            } else {
                project = app.OpenProject(projectFile);
            }
        }


        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void NewProfilingSession(PythonVisualStudioApp app) {
            PythonPaths.Python27.AssertInstalled();

            var testFile = TestData.GetPath(@"TestData\ProfileTest\Program.py");
            Assert.IsTrue(File.Exists(testFile), "ProfileTest\\Program.py does not exist");

            app.OpenPythonPerformance();
            app.PythonPerformanceExplorerToolBar.NewPerfSession();

            var profiling = (IPythonProfiling)app.Dte.GetObject("PythonProfiling");

            app.OpenPythonPerformance();
            var perf = app.PythonPerformanceExplorerTreeView.WaitForItem("Performance *");
            Assert.IsNotNull(perf);
            var session = profiling.GetSession(1);
            Assert.IsNotNull(session);

            Mouse.MoveTo(perf.GetClickablePoint());
            Mouse.DoubleClick(System.Windows.Input.MouseButton.Left);

            // wait for the dialog, set some settings, save them.
            using (var perfTarget = new PythonPerfTarget(app.WaitForDialog())) {
                perfTarget.SelectProfileScript();

                perfTarget.InterpreterComboBox.SelectItem("Python 2.7 (32-bit)");
                perfTarget.ScriptName = testFile;
                perfTarget.WorkingDir = Path.GetDirectoryName(testFile);

                try {
                    perfTarget.Ok();
                } catch (ElementNotEnabledException) {
                    Assert.Fail("Settings were invalid:\n  ScriptName = {0}\n  Interpreter = {1}",
                        perfTarget.ScriptName, perfTarget.SelectedInterpreter);
                }
            }
            app.WaitForDialogDismissed();

            Mouse.MoveTo(perf.GetClickablePoint());
            Mouse.DoubleClick(System.Windows.Input.MouseButton.Left);

            // re-open the dialog, verify the settings
            using (var perfTarget = new PythonPerfTarget(app.WaitForDialog())) {
                Assert.AreEqual("Python 2.7 (32-bit)", perfTarget.SelectedInterpreter);
                Assert.AreEqual(TestData.GetPath(@"TestData\ProfileTest\Program.py"), perfTarget.ScriptName);
            }
        }

        /// <summary>
        /// https://pytools.codeplex.com/workitem/1179
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void DeleteMultipleSessions(PythonVisualStudioApp app) {
            app.Dte.Solution.Close(false);

            app.OpenPythonPerformance();
            app.PythonPerformanceExplorerToolBar.NewPerfSession();
            app.PythonPerformanceExplorerToolBar.NewPerfSession();

            var profiling = (IPythonProfiling)app.Dte.GetObject("PythonProfiling");

            app.OpenPythonPerformance();
            var perf = app.PythonPerformanceExplorerTreeView.WaitForItem("Performance *");
            Assert.IsNotNull(perf);

            var perf2 = app.PythonPerformanceExplorerTreeView.WaitForItem("Performance1 *");

            AutomationWrapper.Select(perf);
            // Cannot use AddToSelection because the tree view declares that
            // it does not support multi-select, even though it does.
            // AutomationWrapper.AddToSelection(perf2);
            Mouse.MoveTo(perf2.GetClickablePoint());
            try {
                Keyboard.Press(System.Windows.Input.Key.LeftCtrl);
                Mouse.Click(System.Windows.Input.MouseButton.Left);
            } finally {
                Keyboard.Release(System.Windows.Input.Key.LeftCtrl);
            }

            var dialog = AutomationElement.FromHandle(app.OpenDialogWithDteExecuteCommand("Edit.Delete")).AsWrapper();
            dialog.ClickButtonByName("Delete");

            Assert.IsNull(app.PythonPerformanceExplorerTreeView.WaitForItemRemoved("Performance *"));
            Assert.IsNull(app.PythonPerformanceExplorerTreeView.WaitForItemRemoved("Performance1 *"));
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void NewProfilingSessionOpenSolution(PythonVisualStudioApp app) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            OpenProfileTestProject(app, out project, out profiling);
            app.OpenPythonPerformance();
            app.PythonPerformanceExplorerToolBar.NewPerfSession();

            var perf = app.PythonPerformanceExplorerTreeView.WaitForItem("Performance");

            var session = profiling.GetSession(1);
            Assert.IsNotNull(session);

            Mouse.MoveTo(perf.GetClickablePoint());
            Mouse.DoubleClick(System.Windows.Input.MouseButton.Left);

            // wait for the dialog, set some settings, save them.
            using (var perfTarget = new PythonPerfTarget(app.WaitForDialog())) {
                perfTarget.SelectProfileProject();

                perfTarget.SelectedProjectComboBox.SelectItem("HelloWorld");

                try {
                    perfTarget.Ok();
                } catch (ElementNotEnabledException) {
                    Assert.Fail("Settings were invalid:\n  SelectedProject = {0}",
                        perfTarget.SelectedProjectComboBox.GetSelectedItemName());
                }
            }

            Mouse.MoveTo(perf.GetClickablePoint());
            Mouse.DoubleClick(System.Windows.Input.MouseButton.Left);

            // re-open the dialog, verify the settings
            using (var perfTarget = new PythonPerfTarget(app.WaitForDialog())) {
                Assert.AreEqual("HelloWorld", perfTarget.SelectedProject);
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void LaunchPythonProfilingWizard(PythonVisualStudioApp app) {
            var project = app.OpenProject(@"TestData\ProfileTest.sln");

            using (var perfTarget = app.LaunchPythonProfiling()) {
                perfTarget.SelectProfileProject();

                perfTarget.SelectedProjectComboBox.SelectItem("HelloWorld");

                try {
                    perfTarget.Ok();
                } catch (ElementNotEnabledException) {
                    Assert.Fail("Settings were invalid:\n  SelectedProject = {0}",
                        perfTarget.SelectedProjectComboBox.GetSelectedItemName());
                }
            }
            app.WaitForDialogDismissed();

            var profiling = (IPythonProfiling)app.Dte.GetObject("PythonProfiling");
            var session = profiling.GetSession(1);

            Assert.IsNotNull(app.PythonPerformanceExplorerTreeView.WaitForItem("HelloWorld *"));

            while (profiling.IsProfiling) {
                // wait for profiling to finish...
                Thread.Sleep(100);
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void LaunchProject(PythonVisualStudioApp app) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            OpenProfileTestProject(app, out project, out profiling);
            var session = LaunchProject(app, profiling, project, TestData.GetPath(@"TestData\ProfileTest"), false);
            try {
                while (profiling.IsProfiling) {
                    Thread.Sleep(100);
                }

                var report = session.GetReport(1);
                var filename = report.Filename;
                Assert.IsTrue(filename.Contains("HelloWorld"));

                Assert.IsNull(session.GetReport(2));

                Assert.IsNotNull(session.GetReport(report.Filename));

                VerifyReport(report, true, "Program.f", "time.sleep");
            } finally {
                profiling.RemoveSession(session, true);
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void LaunchProjectWithSpaceInFilename(PythonVisualStudioApp app) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            OpenProfileTestProject(app, out project, out profiling, @"TestData\Profile Test.sln");
            var session = LaunchProject(app, profiling, project, TestData.GetPath(@"TestData\Profile Test"), false);
            try {
                while (profiling.IsProfiling) {
                    Thread.Sleep(100);
                }

                var report = session.GetReport(1);
                var filename = report.Filename;
                Assert.IsTrue(filename.Contains("Profile Test"));

                Assert.IsNull(session.GetReport(2));

                Assert.IsNotNull(session.GetReport(report.Filename));

                VerifyReport(report, true, "Program.f", "time.sleep");
            } finally {
                profiling.RemoveSession(session, true);
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void LaunchProjectWithSearchPath(PythonVisualStudioApp app) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            OpenProfileTestProject(app, out project, out profiling, @"TestData\ProfileTestSysPath.sln");
            var session = LaunchProject(app, profiling, project, TestData.GetPath(@"TestData\ProfileTestSysPath"), false);
            try {
                while (profiling.IsProfiling) {
                    Thread.Sleep(100);
                }

                var report = session.GetReport(1);
                var filename = report.Filename;
                Assert.IsTrue(filename.Contains("HelloWorld"));

                Assert.IsNull(session.GetReport(2));

                Assert.IsNotNull(session.GetReport(report.Filename));

                VerifyReport(report, true, "A.mod.func");
            } finally {
                profiling.RemoveSession(session, true);
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void LaunchProjectWithPythonPathSet(PythonVisualStudioApp app) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            OpenProfileTestProject(app, out project, out profiling, @"TestData\ProfileTestSysPath.sln");
            IPythonProfileSession session = null;
            var oldPythonPath = Environment.GetEnvironmentVariable("PYTHONPATH");
            var oldClearPythonPath = app.PythonToolsService.GeneralOptions.ClearGlobalPythonPath;
            try {
                Environment.SetEnvironmentVariable("PYTHONPATH", TestData.GetPath(@"TestData\ProfileTestSysPath\B"));
                app.PythonToolsService.GeneralOptions.ClearGlobalPythonPath = false;
                session = LaunchProject(app, profiling, project, TestData.GetPath(@"TestData\ProfileTestSysPath"), false);

                while (profiling.IsProfiling) {
                    Thread.Sleep(100);
                }

                var report = session.GetReport(1);
                var filename = report.Filename;
                Assert.IsTrue(filename.Contains("HelloWorld"));

                Assert.IsNull(session.GetReport(2));

                Assert.IsNotNull(session.GetReport(report.Filename));

                VerifyReport(report, true, "B.mod2.func");
            } finally {
                app.PythonToolsService.GeneralOptions.ClearGlobalPythonPath = oldClearPythonPath;
                Environment.SetEnvironmentVariable("PYTHONPATH", oldPythonPath);
                if (session != null) {
                    profiling.RemoveSession(session, true);
                }
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void LaunchProjectWithPythonPathClear(PythonVisualStudioApp app) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            OpenProfileTestProject(app, out project, out profiling, @"TestData\ProfileTestSysPath.sln");
            IPythonProfileSession session = null;
            var oldPythonPath = Environment.GetEnvironmentVariable("PYTHONPATH");
            var oldClearPythonPath = app.PythonToolsService.GeneralOptions.ClearGlobalPythonPath;
            try {
                Environment.SetEnvironmentVariable("PYTHONPATH", TestData.GetPath(@"TestData\ProfileTestSysPath\B"));
                app.PythonToolsService.GeneralOptions.ClearGlobalPythonPath = true;
                session = LaunchProject(app, profiling, project, TestData.GetPath(@"TestData\ProfileTestSysPath"), false);

                while (profiling.IsProfiling) {
                    Thread.Sleep(100);
                }

                var report = session.GetReport(1);
                var filename = report.Filename;
                Assert.IsTrue(filename.Contains("HelloWorld"));

                Assert.IsNull(session.GetReport(2));

                Assert.IsNotNull(session.GetReport(report.Filename));

                VerifyReport(report, true, "A.mod.func");
            } finally {
                app.PythonToolsService.GeneralOptions.ClearGlobalPythonPath = oldClearPythonPath;
                Environment.SetEnvironmentVariable("PYTHONPATH", oldPythonPath);
                if (session != null) {
                    profiling.RemoveSession(session, true);
                }
            }
        }


        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void LaunchProjectWithEnvironment(PythonVisualStudioApp app) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            OpenProfileTestProject(app, out project, out profiling, @"TestData\ProfileTestEnvironment.sln");
            var session = LaunchProject(app, profiling, project, TestData.GetPath(@"TestData\ProfileTestEnvironment"), false);
            try {
                while (profiling.IsProfiling) {
                    Thread.Sleep(100);
                }

                var report = session.GetReport(1);
                var filename = report.Filename;
                Assert.IsTrue(filename.Contains("HelloWorld"));
                Assert.IsNull(session.GetReport(2));
                Assert.IsNotNull(session.GetReport(report.Filename));

                VerifyReport(report, true, "Program.user_env_var_valid");
            } finally {
                profiling.RemoveSession(session, true);
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void TestSaveDirtySession(PythonVisualStudioApp app) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            OpenProfileTestProject(app, out project, out profiling);
            var session = LaunchProject(app, profiling, project, TestData.GetPath(@"TestData\ProfileTest"), false);
            try {
                while (profiling.IsProfiling) {
                    Thread.Sleep(100);
                }

                var report = session.GetReport(1);
                var filename = report.Filename;
                Assert.IsTrue(filename.Contains("HelloWorld"));

                app.OpenPythonPerformance();
                var pyPerf = app.PythonPerformanceExplorerTreeView;
                Assert.IsNotNull(pyPerf);

                var item = pyPerf.FindItem("HelloWorld *", "Reports");
                var child = item.FindFirst(System.Windows.Automation.TreeScope.Descendants, Condition.TrueCondition);
                var childName = child.GetCurrentPropertyValue(AutomationElement.NameProperty) as string;

                Assert.IsTrue(childName.StartsWith("HelloWorld"));

                // select the dirty session node and save it
                var perfSessionItem = pyPerf.FindItem("HelloWorld *");
                perfSessionItem.SetFocus();
                app.SaveSelection();

                // now it should no longer be dirty
                perfSessionItem = pyPerf.WaitForItem("HelloWorld");
            } finally {
                profiling.RemoveSession(session, true);
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void TestDeleteReport(PythonVisualStudioApp app) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            OpenProfileTestProject(app, out project, out profiling);
            var session = LaunchProject(app, profiling, project, TestData.GetPath(@"TestData\ProfileTest"), false);
            try {
                string reportFilename;
                WaitForReport(profiling, session, app, out reportFilename);

                new RemoveItemDialog(app.WaitForDialog()).Delete();

                app.WaitForDialogDismissed();

                Assert.IsTrue(!File.Exists(reportFilename));
            } finally {
                profiling.RemoveSession(session, true);
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void TestCompareReports(PythonVisualStudioApp app) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            OpenProfileTestProject(app, out project, out profiling);
            var session = LaunchProject(app, profiling, project, TestData.GetPath(@"TestData\ProfileTest"), false);
            try {
                for (int i = 0; i < 100 && profiling.IsProfiling; i++) {
                    Thread.Sleep(100);
                }

                session.Launch(false);
                for (int i = 0; i < 100 && profiling.IsProfiling; i++) {
                    Thread.Sleep(100);
                }

                var pyPerf = app.PythonPerformanceExplorerTreeView;

                var baselineFile = session.GetReport(1).Filename;
                var comparisonFile = session.GetReport(2).Filename;

                var child = pyPerf.FindItem("HelloWorld *", "Reports", Path.GetFileNameWithoutExtension(baselineFile));
                AutomationWrapper.EnsureExpanded(child);
                child.SetFocus();
                child.Select();

                Mouse.MoveTo(child.GetClickablePoint());
                Mouse.Click(System.Windows.Input.MouseButton.Right);
                Keyboard.PressAndRelease(System.Windows.Input.Key.C);

                using (var cmpReports = new ComparePerfReports(app.WaitForDialog())) {
                    try {
                        cmpReports.BaselineFile = baselineFile;
                        cmpReports.ComparisonFile = comparisonFile;
                        cmpReports.Ok();
                        app.WaitForDialogDismissed();
                    } catch (ElementNotEnabledException) {
                        Assert.Fail("Settings were invalid:\n  BaselineFile = {0}\n  ComparisonFile = {1}",
                            cmpReports.BaselineFile, cmpReports.ComparisonFile);
                    }
                }

                app.WaitForDialogDismissed();

                // verify the difference file opens....
                bool foundDiff = false;
                for (int j = 0; j < 10 && !foundDiff; j++) {
                    for (int i = 0; i < app.Dte.Documents.Count; i++) {
                        var doc = app.Dte.Documents.Item(i + 1);
                        string name = doc.FullName;

                        if (name.StartsWith("vsp://diff/?baseline=")) {
                            foundDiff = true;
                            Thread.Sleep(5000);
                            Task.Run(() => doc.Close(EnvDTE.vsSaveChanges.vsSaveChangesNo)).Wait();
                            break;
                        }
                    }
                    if (!foundDiff) {
                        Thread.Sleep(300);
                    }
                }
                Assert.IsTrue(foundDiff);
            } finally {
                profiling.RemoveSession(session, true);
            }
        }


        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void TestRemoveReport(PythonVisualStudioApp app) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            OpenProfileTestProject(app, out project, out profiling);
            var session = LaunchProject(app, profiling, project, TestData.GetPath(@"TestData\ProfileTest"), false);
            string reportFilename;
            WaitForReport(profiling, session, app, out reportFilename);

            new RemoveItemDialog(app.WaitForDialog()).Remove();

            app.WaitForDialogDismissed();

            Assert.IsTrue(File.Exists(reportFilename));
        }

        // P2 because the report viewer may crash VS depending on prior state.
        // We will restart VS before running this test to ensure it is clean.
        //[TestMethod, Priority(2), TestCategory("RestartVS")]
        //[TestCategory("Installed")]
        public void TestOpenReport(PythonVisualStudioApp app) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            OpenProfileTestProject(app, out project, out profiling);
            var session = LaunchProject(app, profiling, project, TestData.GetPath(@"TestData\ProfileTest"), false);
            try {
                IPythonPerformanceReport report;
                AutomationElement child;
                WaitForReport(profiling, session, out report, app, out child);

                var clickPoint = child.GetClickablePoint();
                Mouse.MoveTo(clickPoint);
                Mouse.DoubleClick(System.Windows.Input.MouseButton.Left);

                Assert.IsNotNull(app.WaitForDocument(report.Filename));

                app.Dte.Documents.CloseAll(EnvDTE.vsSaveChanges.vsSaveChangesNo);
            } finally {
                profiling.RemoveSession(session, true);
            }
        }

        private static void WaitForReport(IPythonProfiling profiling, IPythonProfileSession session, out IPythonPerformanceReport report, PythonVisualStudioApp app, out AutomationElement child) {
            while (profiling.IsProfiling) {
                Thread.Sleep(100);
            }

            report = session.GetReport(1);
            var filename = report.Filename;
            Assert.IsTrue(filename.Contains("HelloWorld"));

            app.OpenPythonPerformance();
            var pyPerf = app.PythonPerformanceExplorerTreeView;
            Assert.IsNotNull(pyPerf);

            var item = pyPerf.WaitForItem("HelloWorld *", "Reports");
            child = item.FindFirst(TreeScope.Descendants, Condition.TrueCondition);
            var childName = (string)child.GetCurrentPropertyValue(AutomationElement.NameProperty);

            Assert.IsTrue(childName.StartsWith("HelloWorld"));

            AutomationWrapper.EnsureExpanded(child);
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void TestOpenReportCtxMenu(PythonVisualStudioApp app) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            OpenProfileTestProject(app, out project, out profiling);
            var session = LaunchProject(app, profiling, project, TestData.GetPath(@"TestData\ProfileTest"), false);
            try {
                IPythonPerformanceReport report;
                AutomationElement child;
                WaitForReport(profiling, session, out report, app, out child);

                var clickPoint = child.GetClickablePoint();
                Mouse.MoveTo(clickPoint);
                Mouse.Click(System.Windows.Input.MouseButton.Right);
                Keyboard.Press(System.Windows.Input.Key.O);

                Assert.IsNotNull(app.WaitForDocument(report.Filename));
            } finally {
                profiling.RemoveSession(session, true);
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void TestTargetPropertiesForProject(PythonVisualStudioApp app) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            OpenProfileTestProject(app, out project, out profiling);
            var session = LaunchProject(app, profiling, project, TestData.GetPath(@"TestData\ProfileTest"), false);
            while (profiling.IsProfiling) {
                Thread.Sleep(100);
            }

            app.OpenPythonPerformance();
            var pyPerf = app.PythonPerformanceExplorerTreeView;

            var item = pyPerf.FindItem("HelloWorld *");

            Mouse.MoveTo(item.GetClickablePoint());
            Mouse.DoubleClick(System.Windows.Input.MouseButton.Left);

            using (var perfTarget = new PythonPerfTarget(app.WaitForDialog())) {
                Assert.AreEqual("HelloWorld", perfTarget.SelectedProject);
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void TestTargetPropertiesForInterpreter(PythonVisualStudioApp app) {
            PythonPaths.Python27.AssertInstalled();

            EnvDTE.Project project;
            IPythonProfiling profiling;
            OpenProfileTestProject(app, out project, out profiling, null);
            var session = LaunchProcess(app,
                profiling,
                "Global|PythonCore|2.7-32",
                TestData.GetPath(@"TestData\ProfileTest\Program.py"),
                TestData.GetPath(@"TestData\ProfileTest"),
                "",
                false
            );

            try {
                while (profiling.IsProfiling) {
                    Thread.Sleep(100);
                }

                app.OpenPythonPerformance();
                var pyPerf = app.PythonPerformanceExplorerTreeView;

                var item = pyPerf.FindItem("Program *");

                Mouse.MoveTo(item.GetClickablePoint());
                Mouse.DoubleClick(System.Windows.Input.MouseButton.Left);

                using (var perfTarget = new PythonPerfTarget(app.WaitForDialog())) {
                    Assert.AreEqual("Python 2.7 (32-bit)", perfTarget.SelectedInterpreter);
                    Assert.AreEqual("", perfTarget.Arguments);
                    Assert.IsTrue(perfTarget.ScriptName.EndsWith("Program.py"));
                    Assert.IsTrue(perfTarget.ScriptName.StartsWith(perfTarget.WorkingDir));
                }

                app.WaitForDialogDismissed();
            } finally {
                profiling.RemoveSession(session, true);
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void TestTargetPropertiesForExecutable(PythonVisualStudioApp app) {
            var interp = PythonPaths.Python27;
            interp.AssertInstalled();

            EnvDTE.Project project;
            IPythonProfiling profiling;
            OpenProfileTestProject(app, out project, out profiling, null);
            var session = LaunchProcess(app,
                profiling,
                interp.InterpreterPath,
                TestData.GetPath(@"TestData\ProfileTest\Program.py"),
                TestData.GetPath(@"TestData\ProfileTest"),
                "",
                false
            );

            while (profiling.IsProfiling) {
                Thread.Sleep(100);
            }

            app.OpenPythonPerformance();
            var pyPerf = app.PythonPerformanceExplorerTreeView;

            var item = pyPerf.FindItem("Program *");

            Mouse.MoveTo(item.GetClickablePoint());
            Mouse.DoubleClick(System.Windows.Input.MouseButton.Left);

            using (var perfTarget = new PythonPerfTarget(app.WaitForDialog())) {
                Assert.AreEqual(interp.InterpreterPath, perfTarget.InterpreterPath);
                Assert.AreEqual("", perfTarget.Arguments);
                Assert.IsTrue(perfTarget.ScriptName.EndsWith("Program.py"));
                Assert.IsTrue(perfTarget.ScriptName.StartsWith(perfTarget.WorkingDir));
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void TestStopProfiling(PythonVisualStudioApp app) {
            var interp = PythonPaths.Python27;
            interp.AssertInstalled();

            EnvDTE.Project project;
            IPythonProfiling profiling;
            OpenProfileTestProject(app, out project, out profiling, null);
            var session = LaunchProcess(app,
                profiling,
                interp.InterpreterPath,
                TestData.GetPath(@"TestData\ProfileTest\InfiniteProfile.py"),
                TestData.GetPath(@"TestData\ProfileTest"),
                "",
                false
            );

            try {
                Thread.Sleep(1000);
                Assert.IsTrue(profiling.IsProfiling);
                app.OpenPythonPerformance();
                app.PythonPerformanceExplorerToolBar.Stop();

                while (profiling.IsProfiling) {
                    Thread.Sleep(100);
                }

                var report = session.GetReport(1);

                Assert.IsNotNull(report);
            } finally {
                profiling.RemoveSession(session, true);
            }
        }

        private static void WaitForReport(IPythonProfiling profiling, IPythonProfileSession session, PythonVisualStudioApp app, out string reportFilename) {
            while (profiling.IsProfiling) {
                Thread.Sleep(100);
            }

            var report = session.GetReport(1);
            var filename = report.Filename;
            Assert.IsTrue(filename.Contains("HelloWorld"));

            app.OpenPythonPerformance();
            var pyPerf = app.PythonPerformanceExplorerTreeView;
            Assert.IsNotNull(pyPerf);

            var item = pyPerf.FindItem("HelloWorld *", "Reports");
            var child = item.FindFirst(System.Windows.Automation.TreeScope.Descendants, Condition.TrueCondition);
            var childName = child.GetCurrentPropertyValue(AutomationElement.NameProperty) as string;

            reportFilename = report.Filename;
            Assert.IsTrue(childName.StartsWith("HelloWorld"));

            child.SetFocus();
            Keyboard.PressAndRelease(System.Windows.Input.Key.Delete);
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MultipleTargets(PythonVisualStudioApp app) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            OpenProfileTestProject(app, out project, out profiling);
            var session = LaunchProject(app, profiling, project, TestData.GetPath(@"TestData\ProfileTest"), false);
            IPythonProfileSession session2 = null;
            try {
                {
                    while (profiling.IsProfiling) {
                        Thread.Sleep(100);
                    }

                    var report = session.GetReport(1);
                    var filename = report.Filename;
                    Assert.IsTrue(filename.Contains("HelloWorld"));

                    Assert.IsNull(session.GetReport(2));

                    Assert.IsNotNull(session.GetReport(report.Filename));

                    VerifyReport(report, true, "Program.f", "time.sleep");
                }

                {
                    var interp = PythonPaths.Python27;
                    interp.AssertInstalled();

                    session2 = LaunchProcess(app, profiling, interp.InterpreterPath,
                        TestData.GetPath(@"TestData\ProfileTest\Program.py"),
                        TestData.GetPath(@"TestData\ProfileTest"),
                        "",
                        false
                    );

                    while (profiling.IsProfiling) {
                        Thread.Sleep(100);
                    }

                    var report = session2.GetReport(1);
                    var filename = report.Filename;
                    Assert.IsTrue(filename.Contains("Program"));

                    Assert.IsNull(session2.GetReport(2));

                    Assert.IsNotNull(session2.GetReport(report.Filename));

                    VerifyReport(report, true, "Program.f", "time.sleep");
                }

            } finally {
                profiling.RemoveSession(session, true);
                if (session2 != null) {
                    profiling.RemoveSession(session2, true);
                }
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MultipleTargetsWithProjectHome(PythonVisualStudioApp app) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            OpenProfileTestProject(app, out project, out profiling, @"TestData\ProfileTest2.sln");
            var session = LaunchProject(app, profiling, project, TestData.GetPath(@"TestData\ProfileTest2"), false);
            IPythonProfileSession session2 = null;
            try {
                {
                    while (profiling.IsProfiling) {
                        Thread.Sleep(100);
                    }

                    var report = session.GetReport(1);
                    var filename = report.Filename;
                    Assert.IsTrue(filename.Contains("HelloWorld"));

                    Assert.IsNull(session.GetReport(2));

                    Assert.IsNotNull(session.GetReport(report.Filename));

                    VerifyReport(report, true, "Program.f", "time.sleep");
                }

                {
                    var interp = PythonPaths.Python27;
                    interp.AssertInstalled();

                    session2 = LaunchProcess(app, profiling, interp.InterpreterPath,
                        TestData.GetPath(@"TestData\ProfileTest\Program.py"),
                        TestData.GetPath(@"TestData\ProfileTest"),
                        "",
                        false
                    );

                    while (profiling.IsProfiling) {
                        Thread.Sleep(100);
                    }

                    var report = session2.GetReport(1);
                    var filename = report.Filename;
                    Assert.IsTrue(filename.Contains("Program"));

                    Assert.IsNull(session2.GetReport(2));

                    Assert.IsNotNull(session2.GetReport(report.Filename));

                    VerifyReport(report, true, "Program.f", "time.sleep");
                }

            } finally {
                profiling.RemoveSession(session, true);
                if (session2 != null) {
                    profiling.RemoveSession(session2, true);
                }
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MultipleReports(PythonVisualStudioApp app) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            OpenProfileTestProject(app, out project, out profiling);
            var session = LaunchProject(app, profiling, project, TestData.GetPath(@"TestData\ProfileTest"), false);
            try {

                while (profiling.IsProfiling) {
                    Thread.Sleep(100);
                }

                var report = session.GetReport(1);
                var filename = report.Filename;
                Assert.IsTrue(filename.Contains("HelloWorld"));

                Assert.IsNull(session.GetReport(2));

                Assert.IsNotNull(session.GetReport(report.Filename));

                VerifyReport(report, true, "Program.f", "time.sleep");

                session.Launch();

                while (profiling.IsProfiling) {
                    Thread.Sleep(100);
                }

                report = session.GetReport(2);
                VerifyReport(report, true, "Program.f", "time.sleep");
            } finally {
                profiling.RemoveSession(session, true);
            }
        }


        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void LaunchExecutable(PythonVisualStudioApp app) {
            var interp = PythonPaths.Python27;
            interp.AssertInstalled();

            EnvDTE.Project project;
            IPythonProfiling profiling;
            OpenProfileTestProject(app, out project, out profiling, null);
            var session = LaunchProcess(app, profiling, interp.InterpreterPath,
                TestData.GetPath(@"TestData\ProfileTest\Program.py"),
                TestData.GetPath(@"TestData\ProfileTest"),
                "",
                false
            );
            try {
                while (profiling.IsProfiling) {
                    Thread.Sleep(100);
                }

                var report = session.GetReport(1);
                var filename = report.Filename;
                Assert.IsTrue(filename.Contains("Program"));

                Assert.IsNull(session.GetReport(2));

                Assert.IsNotNull(session.GetReport(report.Filename));

                VerifyReport(report, true, "Program.f", "time.sleep");
            } finally {
                profiling.RemoveSession(session, true);
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void ClassProfile(PythonVisualStudioApp app) {
            var interp = PythonPaths.Python27;
            interp.AssertInstalled();

            EnvDTE.Project project;
            IPythonProfiling profiling;
            OpenProfileTestProject(app, out project, out profiling, null);
            var session = LaunchProcess(app, profiling, interp.InterpreterPath,
                TestData.GetPath(@"TestData\ProfileTest\ClassProfile.py"),
                TestData.GetPath(@"TestData\ProfileTest"),
                "",
                false
            );
            try {
                while (profiling.IsProfiling) {
                    Thread.Sleep(100);
                }

                var report = session.GetReport(1);
                var filename = report.Filename;
                Assert.IsTrue(filename.Contains("ClassProfile"));

                Assert.IsNull(session.GetReport(2));

                Assert.IsNotNull(session.GetReport(report.Filename));
                Assert.IsTrue(File.Exists(filename));

                VerifyReport(report, true, "ClassProfile.C.f", "time.sleep");
            } finally {
                profiling.RemoveSession(session, false);
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void OldClassProfile(PythonVisualStudioApp app) {
            bool anyMissing = false;

            foreach (var version in new[] { PythonPaths.Python26, PythonPaths.Python27 }) {
                if (version == null) {
                    anyMissing = true;
                    continue;
                }

                EnvDTE.Project project;
                IPythonProfiling profiling;
                OpenProfileTestProject(app, out project, out profiling, null);
                var session = LaunchProcess(app, profiling, version.InterpreterPath,
                    TestData.GetPath(@"TestData\ProfileTest\OldStyleClassProfile.py"),
                    TestData.GetPath(@"TestData\ProfileTest"),
                    "",
                    false
                );
                try {
                    while (profiling.IsProfiling) {
                        Thread.Sleep(100);
                    }

                    var report = session.GetReport(1);
                    Assert.IsNotNull(report);

                    var filename = report.Filename;
                    Assert.IsTrue(filename.Contains("OldStyleClassProfile"));

                    Assert.IsNull(session.GetReport(2));

                    Assert.IsNotNull(session.GetReport(report.Filename));
                    Assert.IsTrue(File.Exists(filename));

                    VerifyReport(report, true, "OldStyleClassProfile.C.f", "time.sleep");
                } finally {
                    profiling.RemoveSession(session, false);
                }
            }

            if (anyMissing) {
                Assert.Inconclusive("Not all interpreters were present");
            }
        }


        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void DerivedProfile(PythonVisualStudioApp app) {
            var interp = PythonPaths.Python27;
            interp.AssertInstalled();

            EnvDTE.Project project;
            IPythonProfiling profiling;
            OpenProfileTestProject(app, out project, out profiling, null);
            var session = LaunchProcess(app, profiling, interp.InterpreterPath,
                TestData.GetPath(@"TestData\ProfileTest\DerivedProfile.py"),
                TestData.GetPath(@"TestData\ProfileTest"),
                "",
                false
            );
            try {
                while (profiling.IsProfiling) {
                    Thread.Sleep(100);
                }

                var report = session.GetReport(1);
                var filename = report.Filename;
                Assert.IsTrue(filename.Contains("DerivedProfile"));

                Assert.IsNull(session.GetReport(2));

                Assert.IsNotNull(session.GetReport(report.Filename));

                VerifyReport(report, true, "DerivedProfile.C.f", "time.sleep");
            } finally {
                profiling.RemoveSession(session, true);
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void Pystone(PythonVisualStudioApp app) {
            var interp = PythonPaths.Python27;
            interp.AssertInstalled();

            EnvDTE.Project project;
            IPythonProfiling profiling;
            OpenProfileTestProject(app, out project, out profiling, null);
            var session = LaunchProcess(app, profiling, interp.InterpreterPath,
                Path.Combine(interp.PrefixPath, "Lib", "test", "pystone.py"),
                Path.Combine(interp.PrefixPath, "Lib", "test"),
                "",
                false
            );
            while (profiling.IsProfiling) {
                Thread.Sleep(100);
            }

            var report = session.GetReport(1);
            var filename = report.Filename;
            Assert.IsTrue(filename.Contains("pystone"));

            Assert.IsNull(session.GetReport(2));

            Assert.IsNotNull(session.GetReport(report.Filename));
            Assert.IsTrue(File.Exists(filename));

            VerifyReport(report, true, "test.pystone.Proc1");
        }

        private void BuiltinsProfile(PythonVisualStudioApp app, PythonVersion interp, string[] expectedFunctions, string[] expectedNonFunctions) {
            interp.AssertInstalled();

            EnvDTE.Project project;
            IPythonProfiling profiling;
            OpenProfileTestProject(app, out project, out profiling, null);
            var session = LaunchProcess(app, profiling, interp.Id,
                TestData.GetPath(@"TestData\ProfileTest\BuiltinsProfile.py"),
                TestData.GetPath(@"TestData\ProfileTest"),
                "",
                false
            );
            try {
                while (profiling.IsProfiling) {
                    Thread.Sleep(100);
                }

                var report = session.GetReport(1);
                var filename = report.Filename;
                Assert.IsTrue(filename.Contains("BuiltinsProfile"));

                Assert.IsNull(session.GetReport(2));

                Assert.IsNotNull(session.GetReport(report.Filename));
                Assert.IsTrue(File.Exists(filename));

                if (expectedFunctions != null && expectedFunctions.Length > 0) {
                    VerifyReport(report, true, expectedFunctions);
                }
                if (expectedNonFunctions != null && expectedNonFunctions.Length > 0) {
                    VerifyReport(report, false, expectedNonFunctions);
                }
            } finally {
                profiling.RemoveSession(session, false);
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void BuiltinsProfilePython26(PythonVisualStudioApp app) {
            TestInitialize(app);

            BuiltinsProfile(
                app,
                PythonPaths.Python26,
                new[] { "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring" },
                null
            );
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void BuiltinsProfilePython27(PythonVisualStudioApp app) {
            BuiltinsProfile(
                app,
                PythonPaths.Python27,
                new[] { "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring" },
                null
            );
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void BuiltinsProfilePython27x64(PythonVisualStudioApp app) {
            BuiltinsProfile(
                app,
                PythonPaths.Python27_x64,
                new[] { "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring" },
                null
            );
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void BuiltinsProfilePython31(PythonVisualStudioApp app) {
            BuiltinsProfile(
                app,
                PythonPaths.Python31,
                new[] { "BuiltinsProfile.f", "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring" },
                null
            );
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void BuiltinsProfilePython32(PythonVisualStudioApp app) {
            BuiltinsProfile(
                app,
                PythonPaths.Python32,
                new[] { "BuiltinsProfile.f", "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring" },
                new[] { "compile", "exec", "execfile", "_io.TextIOWrapper.read" }
            );
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void BuiltinsProfilePython32x64(PythonVisualStudioApp app) {
            BuiltinsProfile(
                app,
                PythonPaths.Python32_x64,
                new[] { "BuiltinsProfile.f", "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring" },
                new[] { "compile", "exec", "execfile", "_io.TextIOWrapper.read" }
            );
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void BuiltinsProfilePython33(PythonVisualStudioApp app) {
            BuiltinsProfile(
                app,
                PythonPaths.Python33,
                new[] { "BuiltinsProfile.f", "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring" },
                new[] { "compile", "exec", "execfile", "_io.TextIOWrapper.read" }
            );
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void BuiltinsProfilePython33x64(PythonVisualStudioApp app) {
            BuiltinsProfile(
                app,
                PythonPaths.Python33_x64,
                new[] { "BuiltinsProfile.f", "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring" },
                new[] { "compile", "exec", "execfile", "_io.TextIOWrapper.read" }
            );
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void BuiltinsProfilePython34(PythonVisualStudioApp app) {
            BuiltinsProfile(
                app,
                PythonPaths.Python34,
                new[] { "BuiltinsProfile.f", "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring" },
                new[] { "compile", "exec", "execfile", "_io.TextIOWrapper.read" }
            );
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void BuiltinsProfilePython34x64(PythonVisualStudioApp app) {
            BuiltinsProfile(
                app,
                PythonPaths.Python34_x64,
                new[] { "BuiltinsProfile.f", "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring" },
                new[] { "compile", "exec", "execfile", "_io.TextIOWrapper.read" }
            );
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void BuiltinsProfilePython35(PythonVisualStudioApp app) {
            BuiltinsProfile(
                app,
                PythonPaths.Python35,
                new[] { "BuiltinsProfile.f", "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring" },
                new[] { "compile", "exec", "execfile", "_io.TextIOWrapper.read" }
            );
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void BuiltinsProfilePython35x64(PythonVisualStudioApp app) {
            BuiltinsProfile(
                app,
                PythonPaths.Python35_x64,
                new[] { "BuiltinsProfile.f", "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring" },
                new[] { "compile", "exec", "execfile", "_io.TextIOWrapper.read" }
            );
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void BuiltinsProfilePython36(PythonVisualStudioApp app) {
            BuiltinsProfile(
                app,
                PythonPaths.Python36,
                new[] { "BuiltinsProfile.f", "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring" },
                new[] { "compile", "exec", "execfile", "_io.TextIOWrapper.read" }
            );
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void BuiltinsProfilePython36x64(PythonVisualStudioApp app) {
            BuiltinsProfile(
                app,
                PythonPaths.Python36_x64,
                new[] { "BuiltinsProfile.f", "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring" },
                new[] { "compile", "exec", "execfile", "_io.TextIOWrapper.read" }
            );
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void BuiltinsProfilePython37(PythonVisualStudioApp app) {
            BuiltinsProfile(
                app,
                PythonPaths.Python37,
                new[] { "BuiltinsProfile.f", "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring" },
                new[] { "compile", "exec", "execfile", "_io.TextIOWrapper.read" }
            );
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void BuiltinsProfilePython37x64(PythonVisualStudioApp app) {
            BuiltinsProfile(
                app,
                PythonPaths.Python37_x64,
                new[] { "BuiltinsProfile.f", "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring" },
                new[] { "compile", "exec", "execfile", "_io.TextIOWrapper.read" }
            );
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void LaunchExecutableUsingInterpreterGuid(PythonVisualStudioApp app) {
            PythonPaths.Python27.AssertInstalled();

            EnvDTE.Project project;
            IPythonProfiling profiling;
            OpenProfileTestProject(app, out project, out profiling, null);
            var session = LaunchProcess(app, profiling,
                PythonPaths.Python27.Id,
                TestData.GetPath(@"TestData\ProfileTest\Program.py"),
                TestData.GetPath(@"TestData\ProfileTest"),
                "",
                false
            );
            while (profiling.IsProfiling) {
                Thread.Sleep(100);
            }

            var report = session.GetReport(1);
            Assert.IsNotNull(report);

            var filename = report.Filename;
            Assert.IsTrue(filename.Contains("Program"));

            Assert.IsNull(session.GetReport(2));

            Assert.IsNotNull(session.GetReport(report.Filename));

            VerifyReport(report, true, "Program.f", "time.sleep");
        }

        private void VerifyReport(IPythonPerformanceReport report, bool includesFunctions, params string[] expectedFunctions) {
            var expected = expectedFunctions.ToSet(StringComparer.Ordinal);

            var actual = OpenPerformanceReportAsCsv(report)
                .Select(line => Regex.Match(line, @"^""(?<name>.+?)["" ]", RegexOptions.IgnoreCase))
                .Where(m => m.Success)
                .Select(m => m.Groups["name"].Value)
                .ToSet(StringComparer.Ordinal);

            if (includesFunctions) {
                Console.WriteLine(
                    "expected: {0}\r\nactual:   {1}\r\nextra:    {2}\r\n\r\nmissing:  {3}",
                    string.Join(", ", expected.OrderBy(k => k)),
                    string.Join(", ", actual.OrderBy(k => k)),
                    string.Join(", ", actual.Except(expected).OrderBy(k => k)),
                    string.Join(", ", expected.Except(actual).OrderBy(k => k))
                );

                Assert.IsTrue(actual.IsSupersetOf(expected), "Some functions were missing. See test output for details.");
            } else {
                var intersect = new HashSet<string>(expected);
                intersect.IntersectWith(actual);

                Console.WriteLine(
                    "expected:  {0}\r\nactual:    {1}\r\n\r\nintersect: {2}",
                    string.Join(", ", expected.OrderBy(k => k)),
                    string.Join(", ", actual.OrderBy(k => k)),
                    string.Join(", ", intersect.OrderBy(k => k))
                );

                Assert.IsTrue(intersect.Count == 0, "Some functions appeared. See test output for details.");
            }
        }

        private string[] OpenPerformanceReportAsCsv(IPythonPerformanceReport report) {
            var perfReportPath = Path.Combine(GetPerfToolsPath(false), "vsperfreport.exe");
            Console.WriteLine("Opening {0} as CSV", report.Filename);

            for (int i = 0; i < 100; i++) {
                var csvFilename = Path.Combine(SaveDirectory, Path.GetFileNameWithoutExtension(report.Filename));
                var originalName = csvFilename;
                for (int counter = 1; File.Exists(csvFilename + "_FunctionSummary.csv"); ++counter) {
                    csvFilename = originalName + counter;
                }
                Console.WriteLine("Writing to {0}", csvFilename);

                using (var process = ProcessOutput.RunHiddenAndCapture(
                    perfReportPath,
                    report.Filename,
                    "/output:" + csvFilename,
                    "/summary:function"
                )) {
                    process.Wait();
                    if (process.ExitCode != 0) {
                        if (i == 99) {
                            Assert.Fail(string.Join(Environment.NewLine,
                                Enumerable.Repeat("Output: ", 1)
                                    .Concat(process.StandardOutputLines)
                                    .Concat(Enumerable.Repeat("Error:", 1))
                                    .Concat(process.StandardErrorLines)
                                ));
                        } else {
                            Thread.Sleep(100);
                            continue;
                        }
                    }

                }

                string[] res = null;
                for (int j = 0; j < 100; j++) {
                    try {
                        res = File.ReadAllLines(csvFilename + "_FunctionSummary.csv");
                        break;
                    } catch {
                        Thread.Sleep(100);
                    }
                }
                return res ?? new string[0];
            }
            Assert.Fail("Unable to convert to CSV");
            return null;
        }

        private static string GetPerfToolsPath(bool x64) {
            RegistryKey key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\VisualStudio\" + AssemblyVersionInfo.VSVersion);
            var shFolder = key.GetValue("ShellFolder") as string;
            if (shFolder == null) {
                throw new InvalidOperationException("Cannot find shell folder for Visual Studio");
            }

            string perfToolsPath;
            if (x64) {
                perfToolsPath = @"Team Tools\Performance Tools\x64";
            } else {
                perfToolsPath = @"Team Tools\Performance Tools\";
            }
            perfToolsPath = Path.Combine(shFolder, perfToolsPath);
            return perfToolsPath;
        }
    }
}

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
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Automation;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Profiling;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;
using TestUtilities.UI.Python;
using Task = System.Threading.Tasks.Task;

namespace ProfilingUITests {
    public class ProfilingUITests {
        #region Test Cases

        public void DefaultInterpreterSelected(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
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

        public void StartupProjectSelected(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            var sln = app.CopyProjectForTest(@"TestData\MultiProjectAnalysis\MultiProjectAnalysis.sln");
            app.OpenProject(sln);

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

        public void NewProfilingSession(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
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

            try {
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
            } finally {
                app.InvokeOnMainThread(() => profiling.RemoveSession(session, true));
            }
        }

        public void DeleteMultipleSessions(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
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

        public void NewProfilingSessionOpenSolution(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            CopyAndOpenProject(app, out project, out profiling);
            app.OpenPythonPerformance();
            app.PythonPerformanceExplorerToolBar.NewPerfSession();

            var perf = app.PythonPerformanceExplorerTreeView.WaitForItem("Performance");

            var session = profiling.GetSession(1);
            Assert.IsNotNull(session);

            try {
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
            } finally {
                app.InvokeOnMainThread(() => profiling.RemoveSession(session, true));
            }
        }

        public void LaunchPythonProfilingWizard(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
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

            try {
                Assert.IsNotNull(app.PythonPerformanceExplorerTreeView.WaitForItem("HelloWorld *"));

                while (profiling.IsProfiling) {
                    // wait for profiling to finish...
                    Thread.Sleep(100);
                }
            } finally {
                app.InvokeOnMainThread(() => profiling.RemoveSession(session, true));
            }
        }

        public void LaunchProjectPython27(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            var version = PythonPaths.Python27_x64 ?? PythonPaths.Python27;
            version.AssertInstalled();

            LaunchProject(app, version);
        }

        public void LaunchProjectPython35(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            var version = PythonPaths.Python35_x64 ?? PythonPaths.Python35;
            version.AssertInstalled();

            LaunchProject(app, version);
        }

        public void LaunchProjectPython36(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            var version = PythonPaths.Python36_x64 ?? PythonPaths.Python36;
            version.AssertInstalled();

            LaunchProject(app, version);
        }

        public void LaunchProjectPython37(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            var version = PythonPaths.Python37_x64 ?? PythonPaths.Python37;
            version.AssertInstalled();

            LaunchProject(app, version);
        }

        public void LaunchProjectPython38(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            var version = PythonPaths.Python38_x64 ?? PythonPaths.Python38;
            version.AssertInstalled();

            LaunchProject(app, version);
        }

        public void LaunchProjectWithSpaceInFilename(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            LaunchProjectAndVerifyReport(
                app,
                @"TestData\Profile Test.sln",
                "Profile Test",
                new[] { "Program.f", "time.sleep" }
            );
        }

        public void LaunchProjectWithSolutionFolder(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            LaunchProjectAndVerifyReport(
                app,
                @"TestData\ProfileTestSolutionFolder.sln",
                "ProfileTestSolutionFolder",
                new[] { "Program.f", "time.sleep" }
            );
        }

        public void LaunchProjectWithSearchPath(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            CopyAndOpenProject(app, out project, out profiling, @"TestData\ProfileTestSysPath.sln");
            var projDir = PathUtils.GetParent(project.FullName);
            var session = LaunchProject(app, profiling, project, projDir, false);
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
                app.InvokeOnMainThread(() => profiling.RemoveSession(session, true));
            }
        }

        public void LaunchProjectWithPythonPathSet(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            CopyAndOpenProject(app, out project, out profiling, @"TestData\ProfileTestSysPath.sln");
            var projDir = PathUtils.GetParent(project.FullName);
            IPythonProfileSession session = null;
            try {
                using (new PythonServiceGeneralOptionsSetter(app.PythonToolsService, clearGlobalPythonPath: false))
                using (new EnvironmentVariableSetter("PYTHONPATH", Path.Combine(projDir, "B"))) {
                    session = LaunchProject(app, profiling, project, projDir, false);

                    while (profiling.IsProfiling) {
                        Thread.Sleep(100);
                    }

                    var report = session.GetReport(1);
                    var filename = report.Filename;
                    Assert.IsTrue(filename.Contains("HelloWorld"));

                    Assert.IsNull(session.GetReport(2));

                    Assert.IsNotNull(session.GetReport(report.Filename));

                    VerifyReport(report, true, "B.mod2.func");
                }
            } finally {
                if (session != null) {
                    app.InvokeOnMainThread(() => profiling.RemoveSession(session, true));
                }
            }
        }

        public void LaunchProjectWithPythonPathClear(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            CopyAndOpenProject(app, out project, out profiling, @"TestData\ProfileTestSysPath.sln");
            var projDir = PathUtils.GetParent(project.FullName);
            IPythonProfileSession session = null;
            try {
                using (new PythonServiceGeneralOptionsSetter(app.PythonToolsService, clearGlobalPythonPath: true))
                using (new EnvironmentVariableSetter("PYTHONPATH", Path.Combine(projDir, "B"))) {
                    session = LaunchProject(app, profiling, project, projDir, false);

                    while (profiling.IsProfiling) {
                        Thread.Sleep(100);
                    }

                    var report = session.GetReport(1);
                    var filename = report.Filename;
                    Assert.IsTrue(filename.Contains("HelloWorld"));

                    Assert.IsNull(session.GetReport(2));

                    Assert.IsNotNull(session.GetReport(report.Filename));

                    VerifyReport(report, true, "A.mod.func");
                }
            } finally {
                if (session != null) {
                    app.InvokeOnMainThread(() => profiling.RemoveSession(session, true));
                }
            }
        }

        public void LaunchProjectWithEnvironment(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            LaunchProjectAndVerifyReport(
                app,
                @"TestData\ProfileTestEnvironment.sln",
                "HelloWorld",
                new[] { "Program.user_env_var_valid" }
            );
        }

        public void SaveDirtySession(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            CopyAndOpenProject(app, out project, out profiling);
            var projDir = PathUtils.GetParent(project.FullName);
            var session = LaunchProject(app, profiling, project, projDir, false);
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
                app.InvokeOnMainThread(() => profiling.RemoveSession(session, true));
            }
        }

        public void DeleteReport(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            CopyAndOpenProject(app, out project, out profiling);
            var projDir = PathUtils.GetParent(project.FullName);
            var session = LaunchProject(app, profiling, project, projDir, false);
            try {
                string reportFilename;
                WaitForReport(profiling, session, app, out reportFilename);

                new RemoveItemDialog(app.WaitForDialog()).Delete();

                app.WaitForDialogDismissed();

                Assert.IsTrue(!File.Exists(reportFilename));
            } finally {
                app.InvokeOnMainThread(() => profiling.RemoveSession(session, true));
            }
        }

        public void CompareReports(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            CopyAndOpenProject(app, out project, out profiling);
            var projDir = PathUtils.GetParent(project.FullName);
            var session = LaunchProject(app, profiling, project, projDir, false);
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
                app.InvokeOnMainThread(() => profiling.RemoveSession(session, true));
            }
        }

        public void RemoveReport(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            CopyAndOpenProject(app, out project, out profiling);
            var projDir = PathUtils.GetParent(project.FullName);
            var session = LaunchProject(app, profiling, project, projDir, false);

            try {
                string reportFilename;
                WaitForReport(profiling, session, app, out reportFilename);

                new RemoveItemDialog(app.WaitForDialog()).Remove();

                app.WaitForDialogDismissed();

                Assert.IsTrue(File.Exists(reportFilename));
            } finally {
                app.InvokeOnMainThread(() => profiling.RemoveSession(session, true));
            }
        }

        public void OpenReport(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            CopyAndOpenProject(app, out project, out profiling);
            var projDir = PathUtils.GetParent(project.FullName);
            var session = LaunchProject(app, profiling, project, projDir, false);
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
                app.InvokeOnMainThread(() => profiling.RemoveSession(session, true));
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

        public void OpenReportCtxMenu(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            CopyAndOpenProject(app, out project, out profiling);
            var projDir = PathUtils.GetParent(project.FullName);
            var session = LaunchProject(app, profiling, project, projDir, false);
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
                app.InvokeOnMainThread(() => profiling.RemoveSession(session, true));
            }
        }

        public void TargetPropertiesForProject(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            CopyAndOpenProject(app, out project, out profiling);
            var projDir = PathUtils.GetParent(project.FullName);
            var session = LaunchProject(app, profiling, project, projDir, false);
            try {
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
            } finally {
                app.InvokeOnMainThread(() => profiling.RemoveSession(session, true));
            }
        }

        public void TargetPropertiesForInterpreter(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            PythonPaths.Python27.AssertInstalled();

            IPythonProfiling profiling = GetProfiling(app);
            var sln = app.CopyProjectForTest(@"TestData\ProfileTest.sln");
            var projDir = Path.Combine(PathUtils.GetParent(sln), "ProfileTest");
            var session = LaunchProcess(app,
                profiling,
                "Global|PythonCore|2.7-32",
                Path.Combine(projDir, "Program.py"),
                projDir,
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
                app.InvokeOnMainThread(() => profiling.RemoveSession(session, true));
            }
        }

        public void TargetPropertiesForExecutable(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            var interp = PythonPaths.Python27;
            interp.AssertInstalled();

            IPythonProfiling profiling = GetProfiling(app);
            var sln = app.CopyProjectForTest(@"TestData\ProfileTest.sln");
            var projDir = Path.Combine(PathUtils.GetParent(sln), "ProfileTest");
            var session = LaunchProcess(app,
                profiling,
                interp.InterpreterPath,
                Path.Combine(projDir, "Program.py"),
                projDir,
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
                    Assert.AreEqual(interp.InterpreterPath, perfTarget.InterpreterPath);
                    Assert.AreEqual("", perfTarget.Arguments);
                    Assert.IsTrue(perfTarget.ScriptName.EndsWith("Program.py"));
                    Assert.IsTrue(perfTarget.ScriptName.StartsWith(perfTarget.WorkingDir));
                }
            } finally {
                app.InvokeOnMainThread(() => profiling.RemoveSession(session, true));
            }
        }

        public void StopProfiling(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            var interp = PythonPaths.Python27;
            interp.AssertInstalled();

            IPythonProfiling profiling = GetProfiling(app);
            var sln = app.CopyProjectForTest(@"TestData\ProfileTest.sln");
            var projDir = Path.Combine(PathUtils.GetParent(sln), "ProfileTest");
            var session = LaunchProcess(app,
                profiling,
                interp.InterpreterPath,
                Path.Combine(projDir, "InfiniteProfile.py"),
                projDir,
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
                app.InvokeOnMainThread(() => profiling.RemoveSession(session, true));
            }
        }

        public void MultipleTargets(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            CopyAndOpenProject(app, out project, out profiling);
            var projDir = PathUtils.GetParent(project.FullName);
            var session = LaunchProject(app, profiling, project, projDir, false);
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
                        Path.Combine(projDir, "Program.py"),
                        projDir,
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
                app.InvokeOnMainThread(() => {
                    profiling.RemoveSession(session, true);
                    if (session2 != null) {
                        profiling.RemoveSession(session2, true);
                    }
                });
            }
        }

        public void MultipleTargetsWithProjectHome(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            var profiling = GetProfiling(app);
            var sln = app.CopyProjectForTest(@"TestData\ProfileTest2.sln");
            var slnDir = PathUtils.GetParent(sln);
            var profileTestDir = Path.Combine(slnDir, "ProfileTest");
            var profileTest2Dir = Path.Combine(slnDir, "ProfileTest2");
            FileUtils.CopyDirectory(TestData.GetPath(@"TestData\ProfileTest"), profileTestDir);
            var project = app.OpenProject(sln);
            var session = LaunchProject(app, profiling, project, profileTest2Dir, false);
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
                        Path.Combine(profileTestDir, "Program.py"),
                        profileTestDir,
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
                app.InvokeOnMainThread(() => {
                    profiling.RemoveSession(session, true);
                    if (session2 != null) {
                        profiling.RemoveSession(session2, true);
                    }
                });
            }
        }

        public void MultipleReports(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            CopyAndOpenProject(app, out project, out profiling);
            var projDir = PathUtils.GetParent(project.FullName);
            var session = LaunchProject(app, profiling, project, projDir, false);
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
                app.InvokeOnMainThread(() => profiling.RemoveSession(session, true));
            }
        }

        public void LaunchExecutable(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            var interp = PythonPaths.Python27;
            interp.AssertInstalled();

            IPythonProfiling profiling = GetProfiling(app);
            var sln = app.CopyProjectForTest(@"TestData\ProfileTest.sln");
            var projDir = Path.Combine(PathUtils.GetParent(sln), "ProfileTest");
            var session = LaunchProcess(app, profiling, interp.InterpreterPath,
                Path.Combine(projDir, "Program.py"),
                projDir,
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
                app.InvokeOnMainThread(() => profiling.RemoveSession(session, true));
            }
        }

        public void ClassProfile(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            var interp = PythonPaths.Python27;
            interp.AssertInstalled();

            IPythonProfiling profiling = GetProfiling(app);
            var sln = app.CopyProjectForTest(@"TestData\ProfileTest.sln");
            var projDir = Path.Combine(PathUtils.GetParent(sln), "ProfileTest");
            var session = LaunchProcess(app, profiling, interp.InterpreterPath,
                Path.Combine(projDir, "ClassProfile.py"),
                projDir,
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
                app.InvokeOnMainThread(() => profiling.RemoveSession(session, false));
            }
        }

        public void OldClassProfile(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            var version = PythonPaths.Python27 ?? PythonPaths.Python27_x64;
            version.AssertInstalled("Unable to run test because Python 2.7 is not installed");

            IPythonProfiling profiling = GetProfiling(app);
            var sln = app.CopyProjectForTest(@"TestData\ProfileTest.sln");
            var projDir = Path.Combine(PathUtils.GetParent(sln), "ProfileTest");
            var session = LaunchProcess(app, profiling, version.InterpreterPath,
                Path.Combine(projDir, "OldStyleClassProfile.py"),
                projDir,
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
                app.InvokeOnMainThread(() => profiling.RemoveSession(session, false));
            }
        }

        public void DerivedProfile(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            var interp = PythonPaths.Python27;
            interp.AssertInstalled();

            IPythonProfiling profiling = GetProfiling(app);
            var sln = app.CopyProjectForTest(@"TestData\ProfileTest.sln");
            var projDir = Path.Combine(PathUtils.GetParent(sln), "ProfileTest");
            var session = LaunchProcess(app, profiling, interp.InterpreterPath,
                Path.Combine(projDir, "DerivedProfile.py"),
                projDir,
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
                app.InvokeOnMainThread(() => profiling.RemoveSession(session, true));
            }
        }

        public void Pystone(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            var interp = PythonPaths.Python27;
            interp.AssertInstalled();

            IPythonProfiling profiling = GetProfiling(app);
            var session = LaunchProcess(app, profiling, interp.InterpreterPath,
                Path.Combine(interp.PrefixPath, "Lib", "test", "pystone.py"),
                Path.Combine(interp.PrefixPath, "Lib", "test"),
                "",
                false
            );

            try {
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
            } finally {
                app.InvokeOnMainThread(() => profiling.RemoveSession(session, false));
            }
        }

        public void BuiltinsProfilePython27(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            BuiltinsProfile(
                app,
                PythonPaths.Python27,
                new[] { "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring" },
                null
            );
        }

        public void BuiltinsProfilePython27x64(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            BuiltinsProfile(
                app,
                PythonPaths.Python27_x64,
                new[] { "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring" },
                null
            );
        }

        public void BuiltinsProfilePython35(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            BuiltinsProfile(
                app,
                PythonPaths.Python35,
                new[] { "BuiltinsProfile.f", "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring" },
                new[] { "compile", "exec", "execfile", "_io.TextIOWrapper.read" }
            );
        }

        public void BuiltinsProfilePython35x64(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            BuiltinsProfile(
                app,
                PythonPaths.Python35_x64,
                new[] { "BuiltinsProfile.f", "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring" },
                new[] { "compile", "exec", "execfile", "_io.TextIOWrapper.read" }
            );
        }

        public void BuiltinsProfilePython36(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            BuiltinsProfile(
                app,
                PythonPaths.Python36,
                new[] { "BuiltinsProfile.f", "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring" },
                new[] { "compile", "exec", "execfile", "_io.TextIOWrapper.read" }
            );
        }

        public void BuiltinsProfilePython36x64(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            BuiltinsProfile(
                app,
                PythonPaths.Python36_x64,
                new[] { "BuiltinsProfile.f", "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring" },
                new[] { "compile", "exec", "execfile", "_io.TextIOWrapper.read" }
            );
        }

        public void BuiltinsProfilePython37(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            BuiltinsProfile(
                app,
                PythonPaths.Python37,
                new[] { "BuiltinsProfile.f", "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring" },
                new[] { "compile", "exec", "execfile", "_io.TextIOWrapper.read" }
            );
        }

        public void BuiltinsProfilePython37x64(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            BuiltinsProfile(
                app,
                PythonPaths.Python37_x64,
                new[] { "BuiltinsProfile.f", "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring" },
                new[] { "compile", "exec", "execfile", "_io.TextIOWrapper.read" }
            );
        }

        public void BuiltinsProfilePython38(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            BuiltinsProfile(
                app,
                PythonPaths.Python38,
                new[] { "BuiltinsProfile.f", "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring" },
                new[] { "compile", "exec", "execfile", "_io.TextIOWrapper.read" }
            );
        }

        public void BuiltinsProfilePython38x64(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            BuiltinsProfile(
                app,
                PythonPaths.Python38_x64,
                new[] { "BuiltinsProfile.f", "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring" },
                new[] { "compile", "exec", "execfile", "_io.TextIOWrapper.read" }
            );
        }

        public void LaunchExecutableUsingInterpreterGuid(PythonVisualStudioApp app, ProfileCleanup cleanup, DotNotWaitOnExit optionSetter) {
            PythonPaths.Python27.AssertInstalled();

            IPythonProfiling profiling = GetProfiling(app);
            var sln = app.CopyProjectForTest(@"TestData\ProfileTest.sln");
            var projDir = Path.Combine(PathUtils.GetParent(sln), "ProfileTest");
            var session = LaunchProcess(app, profiling,
                PythonPaths.Python27.Id,
                Path.Combine(projDir, "Program.py"),
                projDir,
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
                Assert.IsTrue(filename.Contains("Program"));

                Assert.IsNull(session.GetReport(2));

                Assert.IsNotNull(session.GetReport(report.Filename));

                VerifyReport(report, true, "Program.f", "time.sleep");
            } finally {
                app.InvokeOnMainThread(() => profiling.RemoveSession(session, true));
            }
        }

        #endregion

        #region Helpers

        public class DotNotWaitOnExit : PythonOptionsSetter {
            public DotNotWaitOnExit(EnvDTE.DTE dte) :
                base(dte, waitOnNormalExit: false, waitOnAbnormalExit: false) {
            }
        }

        public class ProfileCleanup : IDisposable {
            public ProfileCleanup() {
            }

            public void Dispose() {
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
            }
        }

        private string SaveDirectory {
            get {
                var p = TestData.GetTempPath();
                Console.WriteLine($"Saving to {p}");
                return p;
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

        private void CopyAndOpenProject(
            PythonVisualStudioApp app,
            out EnvDTE.Project project,
            out IPythonProfiling profiling,
            string projectFile = @"TestData\ProfileTest.sln"
        ) {
            profiling = GetProfiling(app);

            Assert.IsNotNull(projectFile);

            var sln = app.CopyProjectForTest(projectFile);
            project = app.OpenProject(sln);
        }

        private IPythonProfiling GetProfiling(PythonVisualStudioApp app) {
            var profiling = (IPythonProfiling)app.Dte.GetObject("PythonProfiling");

            // no sessions yet
            Assert.IsNull(profiling.GetSession(1));

            return profiling;
        }

        private void LaunchProjectAndVerifyReport(PythonVisualStudioApp app, string solutionPath, string expectedFileNameContains, string[] expectedFunctions) {
            EnvDTE.Project project;
            IPythonProfiling profiling;
            CopyAndOpenProject(app, out project, out profiling, solutionPath);
            var projDir = PathUtils.GetParent(project.FullName);
            var session = LaunchProject(app, profiling, project, projDir, false);
            try {
                while (profiling.IsProfiling) {
                    Thread.Sleep(100);
                }

                var report = session.GetReport(1);
                var filename = report.Filename;
                Assert.IsTrue(filename.Contains(expectedFileNameContains));
                Assert.IsNull(session.GetReport(2));
                Assert.IsNotNull(session.GetReport(report.Filename));

                VerifyReport(report, true, expectedFunctions);
            } finally {
                app.InvokeOnMainThread(() => profiling.RemoveSession(session, true));
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

        private void BuiltinsProfile(PythonVisualStudioApp app, PythonVersion interp, string[] expectedFunctions, string[] expectedNonFunctions) {
            interp.AssertInstalled();

            IPythonProfiling profiling = GetProfiling(app);
            var sln = app.CopyProjectForTest(@"TestData\ProfileTest.sln");
            var projDir = Path.Combine(PathUtils.GetParent(sln), "ProfileTest");
            var session = LaunchProcess(app, profiling, interp.Id,
                Path.Combine(projDir, "BuiltinsProfile.py"),
                projDir,
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
                app.InvokeOnMainThread(() => profiling.RemoveSession(session, false));
            }
        }

        private void LaunchProject(PythonVisualStudioApp app, PythonVersion version) {
            using (app.SelectDefaultInterpreter(version)) {
                LaunchProjectAndVerifyReport(
                    app,
                    @"TestData\ProfileTest.sln",
                    "HelloWorld",
                    new[] { "Program.f", "time.sleep" }
                );
            }
        }

        #endregion
    }
}

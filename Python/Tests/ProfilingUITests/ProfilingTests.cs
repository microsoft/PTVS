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
using System.Diagnostics;
using System.IO;
using System.Windows.Automation;
using Microsoft.PythonTools.Profiling;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using TestUtilities;
using TestUtilities.UI;
using TestUtilities.UI.Python;

namespace ProfilingUITests {
    [TestClass]
    public class ProfilingTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            TestData.Deploy();
        }

        [TestCleanup]
        public void MyTestCleanup() {
            for (int i = 0; i < 100; i++) {
                try {
                    VsIdeTestHostContext.Dte.Solution.Close(false);
                    break;
                } catch {
                    VsIdeTestHostContext.Dte.Documents.CloseAll(EnvDTE.vsSaveChanges.vsSaveChangesNo);
                    System.Threading.Thread.Sleep(200);
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void NewProfilingSession() {
            VsIdeTestHostContext.Dte.Solution.Close(false);

            var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte);

            app.PythonPerformanceExplorerToolBar.NewPerfSession();

            var profiling = (IPythonProfiling)VsIdeTestHostContext.Dte.GetObject("PythonProfiling");

            app.OpenPythonPerformance();
            var perf = app.PythonPerformanceExplorerTreeView.WaitForItem("Performance *");
            Debug.Assert(perf != null);
            var session = profiling.GetSession(1);
            Assert.AreNotEqual(session, null);

            PythonPerfTarget perfTarget = null;
            try {
                Mouse.MoveTo(perf.GetClickablePoint());
                Mouse.DoubleClick(System.Windows.Input.MouseButton.Left);

                // wait for the dialog, set some settings, save them.
                perfTarget = new PythonPerfTarget(app.WaitForDialog());

                perfTarget.SelectProfileScript();

                perfTarget.InterpreterComboBox.SelectItem("Python 2.6");
                perfTarget.ScriptName = TestData.GetPath(@"TestData\ProfileTest\Program.py");

                try {
                    perfTarget.Ok();
                } catch (ElementNotEnabledException) {
                    Assert.Fail("Settings were invalid:\n  ScriptName = {0}\n  Interpreter = {1}",
                        perfTarget.ScriptName, perfTarget.SelectedInterpreter);
                }
                app.WaitForDialogDismissed();

                Mouse.MoveTo(perf.GetClickablePoint());
                Mouse.DoubleClick(System.Windows.Input.MouseButton.Left);

                // re-open the dialog, verify the settings
                perfTarget = new PythonPerfTarget(app.WaitForDialog());

                Assert.AreEqual("Python 2.6", perfTarget.SelectedInterpreter);
                Assert.AreEqual(TestData.GetPath(@"TestData\ProfileTest\Program.py"), perfTarget.ScriptName);

            } finally {
                if (perfTarget != null) {
                    perfTarget.Cancel();
                }
                profiling.RemoveSession(session, true);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void NewProfilingSessionOpenSolution() {
            var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte);
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\ProfileTest.sln");

            app.PythonPerformanceExplorerToolBar.NewPerfSession();

            var profiling = (IPythonProfiling)VsIdeTestHostContext.Dte.GetObject("PythonProfiling");

            var perf = app.PythonPerformanceExplorerTreeView.WaitForItem("Performance");

            var session = profiling.GetSession(1);
            Assert.AreNotEqual(session, null);

            PythonPerfTarget perfTarget = null;
            try {
                Mouse.MoveTo(perf.GetClickablePoint());
                Mouse.DoubleClick(System.Windows.Input.MouseButton.Left);

                // wait for the dialog, set some settings, save them.
                perfTarget = new PythonPerfTarget(app.WaitForDialog());

                perfTarget.SelectProfileProject();

                perfTarget.SelectedProjectComboBox.SelectItem("HelloWorld");

                try {
                    perfTarget.Ok();
                } catch (ElementNotEnabledException) {
                    Assert.Fail("Settings were invalid:\n  SelectedProject = {0}",
                        perfTarget.SelectedProjectComboBox.GetSelectedItemName());
                }
                app.WaitForDialogDismissed();

                Mouse.MoveTo(perf.GetClickablePoint());
                Mouse.DoubleClick(System.Windows.Input.MouseButton.Left);

                // re-open the dialog, verify the settings
                perfTarget = new PythonPerfTarget(app.WaitForDialog());

                Assert.AreEqual("HelloWorld", perfTarget.SelectedProject);
            } finally {
                if (perfTarget != null) {
                    perfTarget.Cancel();
                }
                profiling.RemoveSession(session, true);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void LaunchPythonProfilingWizard() {
            var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte);
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\ProfileTest.sln");

            app.LaunchPythonProfiling();

            // wait for the dialog, set some settings, save them.
            var perfTarget = new PythonPerfTarget(app.WaitForDialog());
            try {
                perfTarget.SelectProfileProject();

                perfTarget.SelectedProjectComboBox.SelectItem("HelloWorld");

                try {
                    perfTarget.Ok();
                    perfTarget = null;
                } catch (ElementNotEnabledException) {
                    Assert.Fail("Settings were invalid:\n  SelectedProject = {0}",
                        perfTarget.SelectedProjectComboBox.GetSelectedItemName());
                }
            } finally {
                if (perfTarget != null) {
                    perfTarget.Cancel();
                    perfTarget = null;
                }
            }
            app.WaitForDialogDismissed();

            var profiling = (IPythonProfiling)VsIdeTestHostContext.Dte.GetObject("PythonProfiling");
            var session = profiling.GetSession(1);

            try {
                Assert.AreNotEqual(null, app.PythonPerformanceExplorerTreeView.WaitForItem("HelloWorld *"));

                while (profiling.IsProfiling) {
                    // wait for profiling to finish...
                    System.Threading.Thread.Sleep(100);
                }
            } finally {
                profiling.RemoveSession(session, true);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void LaunchProject() {
            var profiling = (IPythonProfiling)VsIdeTestHostContext.Dte.GetObject("PythonProfiling");

            // no sessions yet
            Assert.AreEqual(profiling.GetSession(1), null);

            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\ProfileTest.sln");

            var session = profiling.LaunchProject(project, false);
            try {
                while (profiling.IsProfiling) {
                    System.Threading.Thread.Sleep(100);
                }

                var report = session.GetReport(1);
                var filename = report.Filename;
                Assert.IsTrue(filename.Contains("HelloWorld"));

                Assert.AreEqual(session.GetReport(2), null);

                Assert.AreNotEqual(session.GetReport(report.Filename), null);

                VerifyReport(report, "Program.f", "time.sleep");
            } finally {
                profiling.RemoveSession(session, true);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestSaveDirtySession() {
            var profiling = (IPythonProfiling)VsIdeTestHostContext.Dte.GetObject("PythonProfiling");

            // no sessions yet
            Assert.AreEqual(profiling.GetSession(1), null);

            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\ProfileTest.sln");

            var session = profiling.LaunchProject(project, false);
            try {
                while (profiling.IsProfiling) {
                    System.Threading.Thread.Sleep(100);
                }

                var report = session.GetReport(1);
                var filename = report.Filename;
                Assert.IsTrue(filename.Contains("HelloWorld"));

                var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte);
                app.OpenPythonPerformance();
                var pyPerf = app.PythonPerformanceExplorerTreeView;
                Assert.AreNotEqual(null, pyPerf);

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

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestDeleteReport() {
            var profiling = (IPythonProfiling)VsIdeTestHostContext.Dte.GetObject("PythonProfiling");

            // no sessions yet
            Assert.AreEqual(profiling.GetSession(1), null);

            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\ProfileTest.sln");

            var session = profiling.LaunchProject(project, false);
            try {
                PythonVisualStudioApp app;
                string reportFilename;
                WaitForReport(profiling, session, out app, out reportFilename);

                new RemoveItemDialog(app.WaitForDialog()).Delete();

                app.WaitForDialogDismissed();

                Assert.IsTrue(!File.Exists(reportFilename));
            } finally {
                profiling.RemoveSession(session, true);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestCompareReports() {
            var profiling = (IPythonProfiling)VsIdeTestHostContext.Dte.GetObject("PythonProfiling");

            // no sessions yet
            Assert.AreEqual(profiling.GetSession(1), null);

            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\ProfileTest.sln");

            var session = profiling.LaunchProject(project, false);
            try {
                for (int i = 0; i < 100 && profiling.IsProfiling; i++) {
                    System.Threading.Thread.Sleep(100);
                }

                session.Launch(false);
                for (int i = 0; i < 100 && profiling.IsProfiling; i++) {
                    System.Threading.Thread.Sleep(100);
                }

                var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte);
                var pyPerf = app.PythonPerformanceExplorerTreeView;
                var item = pyPerf.FindItem("HelloWorld *", "Reports");
                var child = item.FindFirst(System.Windows.Automation.TreeScope.Descendants, Condition.TrueCondition);

                AutomationWrapper.EnsureExpanded(child);
                child.SetFocus();

                Mouse.MoveTo(child.GetClickablePoint());
                Mouse.Click(System.Windows.Input.MouseButton.Right);
                Keyboard.PressAndRelease(System.Windows.Input.Key.C);

                var cmpReports = new ComparePerfReports(app.WaitForDialog());
                cmpReports.ComparisonFile = session.GetReport(2).Filename;
                try {
                    cmpReports.Ok();
                    cmpReports = null;
                } catch (ElementNotEnabledException) {
                    Assert.Fail("Settings were invalid:\n  BaselineFile = {0}\n  ComparisonFile = {1}", 
                        cmpReports.BaselineFile, cmpReports.ComparisonFile);
                } finally {
                    if (cmpReports != null) {
                        cmpReports.Cancel();
                    }
                }

                app.WaitForDialogDismissed();

                // verify the difference file opens....
                bool foundDiff = false;
                for (int j = 0; j < 100 && !foundDiff; j++) {
                    for (int i = 0; i < app.Dte.Documents.Count; i++) {
                        var doc = app.Dte.Documents.Item(i + 1);
                        string name = doc.FullName;

                        if (name.StartsWith("vsp://diff/?baseline=")) {
                            foundDiff = true;
                            doc.Close(EnvDTE.vsSaveChanges.vsSaveChangesNo);
                            break;
                        }
                    }
                    if (!foundDiff) {
                        System.Threading.Thread.Sleep(300);
                    }
                }
                Assert.IsTrue(foundDiff);
            } finally {
                profiling.RemoveSession(session, true);
            }
        }


        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestRemoveReport() {
            var profiling = (IPythonProfiling)VsIdeTestHostContext.Dte.GetObject("PythonProfiling");

            // no sessions yet
            Assert.AreEqual(profiling.GetSession(1), null);

            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\ProfileTest.sln");

            var session = profiling.LaunchProject(project, false);
            try {
                PythonVisualStudioApp app;
                string reportFilename;
                WaitForReport(profiling, session, out app, out reportFilename);

                new RemoveItemDialog(app.WaitForDialog()).Remove();

                app.WaitForDialogDismissed();

                Assert.IsTrue(File.Exists(reportFilename));
            } finally {
                profiling.RemoveSession(session, true);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestOpenReport() {
            var profiling = (IPythonProfiling)VsIdeTestHostContext.Dte.GetObject("PythonProfiling");

            // no sessions yet
            Assert.AreEqual(profiling.GetSession(1), null);

            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\ProfileTest.sln");

            var session = profiling.LaunchProject(project, false);
            try {
                IPythonPerformanceReport report;
                PythonVisualStudioApp app;
                AutomationElement child;
                WaitForReport(profiling, session, out report, out app, out child);

                var clickPoint = child.GetClickablePoint();
                Mouse.MoveTo(clickPoint);
                Mouse.DoubleClick(System.Windows.Input.MouseButton.Left);

                Assert.AreNotEqual(null, app.WaitForDocument(report.Filename));

                app.Dte.Documents.CloseAll(EnvDTE.vsSaveChanges.vsSaveChangesNo);
            } finally {
                profiling.RemoveSession(session, true);
            }
        }

        private static void WaitForReport(IPythonProfiling profiling, IPythonProfileSession session, out IPythonPerformanceReport report, out PythonVisualStudioApp app, out AutomationElement child) {
            while (profiling.IsProfiling) {
                System.Threading.Thread.Sleep(100);
            }

            report = session.GetReport(1);
            var filename = report.Filename;
            Assert.IsTrue(filename.Contains("HelloWorld"));

            app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenPythonPerformance();
            var pyPerf = app.PythonPerformanceExplorerTreeView;
            Assert.AreNotEqual(null, pyPerf);

            var item = pyPerf.FindItem("HelloWorld *", "Reports");
            child = item.FindFirst(System.Windows.Automation.TreeScope.Descendants, Condition.TrueCondition);
            var childName = child.GetCurrentPropertyValue(AutomationElement.NameProperty) as string;

            Assert.IsTrue(childName.StartsWith("HelloWorld"));

            AutomationWrapper.EnsureExpanded(child);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestOpenReportCtxMenu() {
            var profiling = (IPythonProfiling)VsIdeTestHostContext.Dte.GetObject("PythonProfiling");

            // no sessions yet
            Assert.AreEqual(profiling.GetSession(1), null);

            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\ProfileTest.sln");

            var session = profiling.LaunchProject(project, false);
            try {
                IPythonPerformanceReport report;
                PythonVisualStudioApp app;
                AutomationElement child;
                WaitForReport(profiling, session, out report, out app, out child);

                var clickPoint = child.GetClickablePoint();
                Mouse.MoveTo(clickPoint);
                Mouse.Click(System.Windows.Input.MouseButton.Right);
                Keyboard.Press(System.Windows.Input.Key.O);

                Assert.AreNotEqual(null, app.WaitForDocument(report.Filename));
            } finally {
                profiling.RemoveSession(session, true);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestTargetPropertiesForProject() {
            var profiling = (IPythonProfiling)VsIdeTestHostContext.Dte.GetObject("PythonProfiling");

            // no sessions yet
            Assert.AreEqual(profiling.GetSession(1), null);

            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\ProfileTest.sln");

            var session = profiling.LaunchProject(project, false);
            try {
                while (profiling.IsProfiling) {
                    System.Threading.Thread.Sleep(100);
                }

                var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte);
                app.OpenPythonPerformance();
                var pyPerf = app.PythonPerformanceExplorerTreeView;

                var item = pyPerf.FindItem("HelloWorld *");

                Mouse.MoveTo(item.GetClickablePoint());
                Mouse.DoubleClick(System.Windows.Input.MouseButton.Left);

                var perfTarget = new PythonPerfTarget(app.WaitForDialog());
                Assert.AreEqual("HelloWorld", perfTarget.SelectedProject);

                perfTarget.Cancel();

                app.WaitForDialogDismissed();
            } finally {
                profiling.RemoveSession(session, true);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestTargetPropertiesForInterpreter() {
            var profiling = (IPythonProfiling)VsIdeTestHostContext.Dte.GetObject("PythonProfiling");

            // no sessions yet
            Assert.AreEqual(profiling.GetSession(1), null);

            var session = profiling.LaunchProcess("{2AF0F10D-7135-4994-9156-5D01C9C11B7E};2.6",
                TestData.GetPath(@"TestData\ProfileTest\Program.py"),
                TestData.GetPath(@"TestData\ProfileTest"),
                "",
                false
            );

            try {
                while (profiling.IsProfiling) {
                    System.Threading.Thread.Sleep(100);
                }

                var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte);
                app.OpenPythonPerformance();
                var pyPerf = app.PythonPerformanceExplorerTreeView;

                var item = pyPerf.FindItem("Program *");

                Mouse.MoveTo(item.GetClickablePoint());
                Mouse.DoubleClick(System.Windows.Input.MouseButton.Left);

                var perfTarget = new PythonPerfTarget(app.WaitForDialog());
                Assert.AreEqual("Python 2.6", perfTarget.SelectedInterpreter);
                Assert.AreEqual("", perfTarget.Arguments);
                Assert.IsTrue(perfTarget.ScriptName.EndsWith("Program.py"));
                Assert.IsTrue(perfTarget.ScriptName.StartsWith(perfTarget.WorkingDir));

                perfTarget.Cancel();

                app.WaitForDialogDismissed();
            } finally {
                profiling.RemoveSession(session, true);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestTargetPropertiesForExecutable() {
            var profiling = (IPythonProfiling)VsIdeTestHostContext.Dte.GetObject("PythonProfiling");

            // no sessions yet
            Assert.AreEqual(profiling.GetSession(1), null);

            var session = profiling.LaunchProcess("C:\\Python26\\python.exe",
                TestData.GetPath(@"TestData\ProfileTest\Program.py"),
                TestData.GetPath(@"TestData\ProfileTest"),
                "",
                false
            );

            PythonVisualStudioApp app = null;
            PythonPerfTarget perfTarget = null;
            try {
                while (profiling.IsProfiling) {
                    System.Threading.Thread.Sleep(100);
                }

                app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte);
                app.OpenPythonPerformance();
                var pyPerf = app.PythonPerformanceExplorerTreeView;

                var item = pyPerf.FindItem("Program *");

                Mouse.MoveTo(item.GetClickablePoint());
                Mouse.DoubleClick(System.Windows.Input.MouseButton.Left);

                perfTarget = new PythonPerfTarget(app.WaitForDialog());
                Assert.AreEqual("C:\\Python26\\python.exe", perfTarget.InterpreterPath);
                Assert.AreEqual("", perfTarget.Arguments);
                Assert.IsTrue(perfTarget.ScriptName.EndsWith("Program.py"));
                Assert.IsTrue(perfTarget.ScriptName.StartsWith(perfTarget.WorkingDir));

            } finally {
                if (perfTarget != null) {
                    perfTarget.Cancel();
                    if (app != null) {
                        app.WaitForDialogDismissed();
                    }
                }
                profiling.RemoveSession(session, true);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestStopProfiling() {
            var profiling = (IPythonProfiling)VsIdeTestHostContext.Dte.GetObject("PythonProfiling");

            // no sessions yet
            Assert.AreEqual(profiling.GetSession(1), null);

            var session = profiling.LaunchProcess("C:\\Python26\\python.exe",
                TestData.GetPath(@"TestData\ProfileTest\InfiniteProfile.py"),
                TestData.GetPath(@"TestData\ProfileTest"),
                "",
                false
            );

            try {
                System.Threading.Thread.Sleep(1000);
                Assert.IsTrue(profiling.IsProfiling);
                var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte);
                app.PythonPerformanceExplorerToolBar.Stop();

                while (profiling.IsProfiling) {
                    System.Threading.Thread.Sleep(100);
                }

                var report = session.GetReport(1);

                Assert.AreNotEqual(null, report);
            } finally {
                profiling.RemoveSession(session, true);
            }
        }

        private static void WaitForReport(IPythonProfiling profiling, IPythonProfileSession session, out PythonVisualStudioApp app, out string reportFilename) {
            while (profiling.IsProfiling) {
                System.Threading.Thread.Sleep(100);
            }

            var report = session.GetReport(1);
            var filename = report.Filename;
            Assert.IsTrue(filename.Contains("HelloWorld"));

            app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenPythonPerformance();
            var pyPerf = app.PythonPerformanceExplorerTreeView;
            Assert.AreNotEqual(null, pyPerf);

            var item = pyPerf.FindItem("HelloWorld *", "Reports");
            var child = item.FindFirst(System.Windows.Automation.TreeScope.Descendants, Condition.TrueCondition);
            var childName = child.GetCurrentPropertyValue(AutomationElement.NameProperty) as string;

            reportFilename = report.Filename;
            Assert.IsTrue(childName.StartsWith("HelloWorld"));

            child.SetFocus();
            Keyboard.PressAndRelease(System.Windows.Input.Key.Delete);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MultipleTargets() {
            var profiling = (IPythonProfiling)VsIdeTestHostContext.Dte.GetObject("PythonProfiling");

            // no sessions yet
            Assert.AreEqual(profiling.GetSession(1), null);

            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\ProfileTest.sln");

            var session = profiling.LaunchProject(project, false);
            IPythonProfileSession session2 = null;
            try {
                {
                    while (profiling.IsProfiling) {
                        System.Threading.Thread.Sleep(100);
                    }

                    var report = session.GetReport(1);
                    var filename = report.Filename;
                    Assert.IsTrue(filename.Contains("HelloWorld"));

                    Assert.AreEqual(session.GetReport(2), null);

                    Assert.AreNotEqual(session.GetReport(report.Filename), null);

                    VerifyReport(report, "Program.f", "time.sleep");
                }

                {
                    session2 = profiling.LaunchProcess("C:\\Python26\\python.exe",
                        TestData.GetPath(@"TestData\ProfileTest\Program.py"),
                        TestData.GetPath(@"TestData\ProfileTest"),
                        "",
                        false
                    );

                    while (profiling.IsProfiling) {
                        System.Threading.Thread.Sleep(100);
                    }

                    var report = session2.GetReport(1);
                    var filename = report.Filename;
                    Assert.IsTrue(filename.Contains("Program"));

                    Assert.AreEqual(session2.GetReport(2), null);

                    Assert.AreNotEqual(session2.GetReport(report.Filename), null);

                    VerifyReport(report, "Program.f", "time.sleep");
                }

            } finally {
                profiling.RemoveSession(session, true);
                if (session2 != null) {
                    profiling.RemoveSession(session2, true);
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MultipleTargetsWithProjectHome() {
            var profiling = (IPythonProfiling)VsIdeTestHostContext.Dte.GetObject("PythonProfiling");

            // no sessions yet
            Assert.AreEqual(profiling.GetSession(1), null);

            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\ProfileTest2.sln");

            var session = profiling.LaunchProject(project, false);
            IPythonProfileSession session2 = null;
            try {
                {
                    while (profiling.IsProfiling) {
                        System.Threading.Thread.Sleep(100);
                    }

                    var report = session.GetReport(1);
                    var filename = report.Filename;
                    Assert.IsTrue(filename.Contains("HelloWorld"));

                    Assert.AreEqual(session.GetReport(2), null);

                    Assert.AreNotEqual(session.GetReport(report.Filename), null);

                    VerifyReport(report, "Program.f", "time.sleep");
                }

                {
                    session2 = profiling.LaunchProcess("C:\\Python26\\python.exe",
                        TestData.GetPath(@"TestData\ProfileTest\Program.py"),
                        TestData.GetPath(@"TestData\ProfileTest"),
                        "",
                        false
                    );

                    while (profiling.IsProfiling) {
                        System.Threading.Thread.Sleep(100);
                    }

                    var report = session2.GetReport(1);
                    var filename = report.Filename;
                    Assert.IsTrue(filename.Contains("Program"));

                    Assert.AreEqual(session2.GetReport(2), null);

                    Assert.AreNotEqual(session2.GetReport(report.Filename), null);

                    VerifyReport(report, "Program.f", "time.sleep");
                }

            } finally {
                profiling.RemoveSession(session, true);
                if (session2 != null) {
                    profiling.RemoveSession(session2, true);
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MultipleReports() {
            var profiling = (IPythonProfiling)VsIdeTestHostContext.Dte.GetObject("PythonProfiling");

            // no sessions yet
            Assert.AreEqual(profiling.GetSession(1), null);

            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\ProfileTest.sln");

            var session = profiling.LaunchProject(project, false);
            try {

                while (profiling.IsProfiling) {
                    System.Threading.Thread.Sleep(100);
                }

                var report = session.GetReport(1);
                var filename = report.Filename;
                Assert.IsTrue(filename.Contains("HelloWorld"));

                Assert.AreEqual(session.GetReport(2), null);

                Assert.AreNotEqual(session.GetReport(report.Filename), null);

                VerifyReport(report, "Program.f", "time.sleep");

                session.Launch();

                while (profiling.IsProfiling) {
                    System.Threading.Thread.Sleep(100);
                }

                report = session.GetReport(2);
                VerifyReport(report, "Program.f", "time.sleep");
            } finally {
                profiling.RemoveSession(session, true);
            }
        }


        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void LaunchExecutable() {
            var profiling = (IPythonProfiling)VsIdeTestHostContext.Dte.GetObject("PythonProfiling");

            // no sessions yet
            Assert.AreEqual(profiling.GetSession(1), null);

            var session = profiling.LaunchProcess("C:\\Python26\\python.exe",
                TestData.GetPath(@"TestData\ProfileTest\Program.py"),
                TestData.GetPath(@"TestData\ProfileTest"),
                "",
                false
            );
            try {
                while (profiling.IsProfiling) {
                    System.Threading.Thread.Sleep(100);
                }

                var report = session.GetReport(1);
                var filename = report.Filename;
                Assert.IsTrue(filename.Contains("Program"));

                Assert.AreEqual(session.GetReport(2), null);

                Assert.AreNotEqual(session.GetReport(report.Filename), null);

                VerifyReport(report, "Program.f", "time.sleep");
            } finally {
                profiling.RemoveSession(session, true);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ClassProfile() {
            var profiling = (IPythonProfiling)VsIdeTestHostContext.Dte.GetObject("PythonProfiling");

            // no sessions yet
            Assert.AreEqual(profiling.GetSession(1), null);

            var session = profiling.LaunchProcess("C:\\Python26\\python.exe",
                TestData.GetPath(@"TestData\ProfileTest\ClassProfile.py"),
                TestData.GetPath(@"TestData\ProfileTest"),
                "",
                false
            );
            try {
                while (profiling.IsProfiling) {
                    System.Threading.Thread.Sleep(100);
                }

                var report = session.GetReport(1);
                var filename = report.Filename;
                Assert.IsTrue(filename.Contains("ClassProfile"));

                Assert.AreEqual(session.GetReport(2), null);

                Assert.AreNotEqual(session.GetReport(report.Filename), null);
                Assert.IsTrue(File.Exists(filename));

                VerifyReport(report, "ClassProfile.C.f", "time.sleep");
            } finally {
                profiling.RemoveSession(session, false);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void OldClassProfile() {
            var profiling = (IPythonProfiling)VsIdeTestHostContext.Dte.GetObject("PythonProfiling");

            foreach (var version in new[] { "C:\\Python25\\python.exe", "C:\\Python26\\python.exe", "C:\\Python27\\python.exe" }) {
                // no sessions yet
                Assert.AreEqual(profiling.GetSession(1), null);

                var session = profiling.LaunchProcess(version,
                    TestData.GetPath(@"TestData\ProfileTest\OldStyleClassProfile.py"),
                    TestData.GetPath(@"TestData\ProfileTest"),
                    "",
                    false
                );
                try {
                    while (profiling.IsProfiling) {
                        System.Threading.Thread.Sleep(100);
                    }

                    var report = session.GetReport(1);
                    var filename = report.Filename;
                    Assert.IsTrue(filename.Contains("OldStyleClassProfile"));

                    Assert.AreEqual(session.GetReport(2), null);

                    Assert.AreNotEqual(session.GetReport(report.Filename), null);
                    Assert.IsTrue(File.Exists(filename));

                    VerifyReport(report, "OldStyleClassProfile.C.f", "time.sleep");
                } finally {
                    profiling.RemoveSession(session, false);
                }
            }
        }


        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DerivedProfile() {
            var profiling = (IPythonProfiling)VsIdeTestHostContext.Dte.GetObject("PythonProfiling");

            // no sessions yet
            Assert.AreEqual(profiling.GetSession(1), null);

            var session = profiling.LaunchProcess("C:\\Python26\\python.exe",
                TestData.GetPath(@"TestData\ProfileTest\DerivedProfile.py"),
                TestData.GetPath(@"TestData\ProfileTest"),
                "",
                false
            );
            try {
                while (profiling.IsProfiling) {
                    System.Threading.Thread.Sleep(100);
                }

                var report = session.GetReport(1);
                var filename = report.Filename;
                Assert.IsTrue(filename.Contains("DerivedProfile"));

                Assert.AreEqual(session.GetReport(2), null);

                Assert.AreNotEqual(session.GetReport(report.Filename), null);

                VerifyReport(report, "DerivedProfile.C.f", "time.sleep");
            } finally {
                profiling.RemoveSession(session, true);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void BuiltinsProfile() {
            var profiling = (IPythonProfiling)VsIdeTestHostContext.Dte.GetObject("PythonProfiling");

            // no sessions yet
            Assert.AreEqual(profiling.GetSession(1), null);

            var session = profiling.LaunchProcess("C:\\Python26\\python.exe",
                TestData.GetPath(@"TestData\ProfileTest\BuiltinsProfile.py"),
                TestData.GetPath(@"TestData\ProfileTest"),
                "",
                false
            );
            try {
                while (profiling.IsProfiling) {
                    System.Threading.Thread.Sleep(100);
                }

                var report = session.GetReport(1);
                var filename = report.Filename;
                Assert.IsTrue(filename.Contains("BuiltinsProfile"));

                Assert.AreEqual(session.GetReport(2), null);

                Assert.AreNotEqual(session.GetReport(report.Filename), null);
                Assert.IsTrue(File.Exists(filename));

                VerifyReport(report, "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring");
            } finally {
                profiling.RemoveSession(session, false);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Pystone() {
            var profiling = (IPythonProfiling)VsIdeTestHostContext.Dte.GetObject("PythonProfiling");

            // no sessions yet
            Assert.AreEqual(profiling.GetSession(1), null);

            var session = profiling.LaunchProcess("C:\\Python26\\python.exe",
                @"C:\Python26\Lib\test\pystone.py",
                @"C:\Python26\Lib\test",
                "",
                false
            );
            try {
                while (profiling.IsProfiling) {
                    System.Threading.Thread.Sleep(100);
                }

                var report = session.GetReport(1);
                var filename = report.Filename;
                Assert.IsTrue(filename.Contains("pystone"));

                Assert.AreEqual(session.GetReport(2), null);

                Assert.AreNotEqual(session.GetReport(report.Filename), null);
                Assert.IsTrue(File.Exists(filename));

                VerifyReport(report, "test.pystone.Proc1");
            } finally {
                profiling.RemoveSession(session, false);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Python3k() {
            var profiling = (IPythonProfiling)VsIdeTestHostContext.Dte.GetObject("PythonProfiling");

            // no sessions yet
            Assert.AreEqual(profiling.GetSession(1), null);

            var session = profiling.LaunchProcess("C:\\Python31\\python.exe",
                TestData.GetPath(@"TestData\ProfileTest\BuiltinsProfile.py"),
                TestData.GetPath(@"TestData\ProfileTest"),
                "",
                false
            );
            try {
                while (profiling.IsProfiling) {
                    System.Threading.Thread.Sleep(100);
                }

                var report = session.GetReport(1);
                var filename = report.Filename;
                Assert.IsTrue(filename.Contains("BuiltinsProfile"));

                Assert.AreEqual(session.GetReport(2), null);

                Assert.AreNotEqual(session.GetReport(report.Filename), null);
                Assert.IsTrue(File.Exists(filename));

                VerifyReport(report, "BuiltinsProfile.f", "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring");
            } finally {
                profiling.RemoveSession(session, false);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Python32() {
            var profiling = (IPythonProfiling)VsIdeTestHostContext.Dte.GetObject("PythonProfiling");

            // no sessions yet
            Assert.AreEqual(profiling.GetSession(1), null);

            var session = profiling.LaunchProcess("C:\\Python32\\python.exe",
                TestData.GetPath(@"TestData\ProfileTest\BuiltinsProfile.py"),
                TestData.GetPath(@"TestData\ProfileTest"),
                "",
                false
            );
            try {
                while (profiling.IsProfiling) {
                    System.Threading.Thread.Sleep(100);
                }

                var report = session.GetReport(1);
                var filename = report.Filename;
                Assert.IsTrue(filename.Contains("BuiltinsProfile"));

                Assert.AreEqual(session.GetReport(2), null);

                Assert.AreNotEqual(session.GetReport(report.Filename), null);
                Assert.IsTrue(File.Exists(filename));

                VerifyReport(report, "BuiltinsProfile.f", "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring");
            } finally {
                profiling.RemoveSession(session, false);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Python64Bit() {
            var profiling = (IPythonProfiling)VsIdeTestHostContext.Dte.GetObject("PythonProfiling");

            // no sessions yet
            Assert.AreEqual(profiling.GetSession(1), null);

            var session = profiling.LaunchProcess("{9A7A9026-48C1-4688-9D5D-E5699D47D074};2.7",
                TestData.GetPath(@"TestData\ProfileTest\BuiltinsProfile.py"),
                TestData.GetPath(@"TestData\ProfileTest"),
                "",
                false
            );
            try {
                while (profiling.IsProfiling) {
                    System.Threading.Thread.Sleep(100);
                }

                var report = session.GetReport(1);
                var filename = report.Filename;
                Assert.IsTrue(filename.Contains("BuiltinsProfile"));

                Assert.AreEqual(session.GetReport(2), null);

                Assert.AreNotEqual(session.GetReport(report.Filename), null);
                Assert.IsTrue(File.Exists(filename));

                VerifyReport(report, "BuiltinsProfile.f", "str.startswith", "isinstance", "marshal.dumps", "array.array.tostring");
            } finally {
                profiling.RemoveSession(session, false);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void LaunchExecutableUsingInterpreterGuid() {
            var profiling = (IPythonProfiling)VsIdeTestHostContext.Dte.GetObject("PythonProfiling");

            // no sessions yet
            Assert.AreEqual(profiling.GetSession(1), null);

            var session = profiling.LaunchProcess("{2AF0F10D-7135-4994-9156-5D01C9C11B7E};2.6",
                TestData.GetPath(@"TestData\ProfileTest\Program.py"),
                TestData.GetPath(@"TestData\ProfileTest"),
                "",
                false
            );
            try {
                while (profiling.IsProfiling) {
                    System.Threading.Thread.Sleep(100);
                }

                var report = session.GetReport(1);
                var filename = report.Filename;
                Assert.IsTrue(filename.Contains("Program"));

                Assert.AreEqual(session.GetReport(2), null);

                Assert.AreNotEqual(session.GetReport(report.Filename), null);

                VerifyReport(report, "Program.f", "time.sleep");
            } finally {
                profiling.RemoveSession(session, true);
            }
        }

        private static void VerifyReport(IPythonPerformanceReport report, params string[] expectedFunctions) {
            // run vsperf
            string[] lines = OpenPerformanceReportAsCsv(report);
            bool[] expected = new bool[expectedFunctions.Length];

            // quote the function names so they match the CSV
            for (int i = 0; i < expectedFunctions.Length; i++) {
                expectedFunctions[i] = "\"" + expectedFunctions[i] + "\"";
            }
            
            foreach (var line in lines) {
                for (int i = 0; i < expectedFunctions.Length; i++) {
                    if (line.StartsWith(expectedFunctions[i])) {
                        expected[i] = true;
                    }
                }
            }

            foreach (var found in expected) {
                Assert.IsTrue(found);
            }            
        }

        private static int _counter;

        private static string[] OpenPerformanceReportAsCsv(IPythonPerformanceReport report) {
            var perfReportPath = Path.Combine(GetPerfToolsPath(false), "vsperfreport.exe");

            for (int i = 0; i < 100; i++) {
                string csvFilename;
                do {
                    csvFilename = Path.Combine(Path.GetTempPath(), "test") + DateTime.Now.Ticks + "_" + _counter++;
                } while (File.Exists(csvFilename + "_FunctionSummary.csv"));

                var psi = new ProcessStartInfo(perfReportPath, report.Filename + " /output:" + csvFilename + " /summary:function");
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                var process = Process.Start(psi);
                process.Start();
                process.WaitForExit();
                if (process.ExitCode != 0) {
                    if (i == 99) {
                        string msg = "Output: " + process.StandardOutput.ReadToEnd() + Environment.NewLine +
                            "Error: " + process.StandardError.ReadToEnd() + Environment.NewLine;
                        Assert.Fail(msg);
                    } else {
                        System.Threading.Thread.Sleep(100);
                        continue;
                    }
                }

                string[] res = null;
                for (int j = 0; j < 100; j++) {
                    try {
                        res = File.ReadAllLines(csvFilename + "_FunctionSummary.csv");
                        break;
                    } catch {
                        System.Threading.Thread.Sleep(100);
                    }
                }
                File.Delete(csvFilename + "_FunctionSummary.csv");
                return res ?? new string[0];
            }
            Assert.Fail("Unable to convert to CSV");
            return null;
        }

        private static string GetPerfToolsPath(bool x64) {
            RegistryKey key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\VisualStudio\" + VSUtility.Version);
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

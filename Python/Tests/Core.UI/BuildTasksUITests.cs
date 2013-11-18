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
using System.Linq;
using System.Threading;
using System.Windows.Automation;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Project;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;
using TestUtilities.UI.Python;

namespace PythonToolsUITests {
    [TestClass]
    public class BuildTasksUI27Tests {
        static BuildTasksUI27Tests() {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        internal virtual PythonVersion PythonVersion {
            get {
                return PythonPaths.Python27 ?? PythonPaths.Python27_x64;
            }
        }

        internal void Execute(PythonProjectNode projectNode, string commandName) {
            projectNode._uiSync.Invoke((Action)(() => {
                projectNode._customCommands.First(cc => cc.DisplayLabel == commandName).Execute(projectNode);
            }));
        }

        internal void OpenProject(VisualStudioApp app, string slnName, out PythonProjectNode projectNode, out EnvDTE.Project dteProject) {
            PythonVersion.AssertInstalled();

            dteProject = app.OpenProject("TestData\\Targets\\" + slnName);
            projectNode = dteProject.GetPythonProject();
            var fact = projectNode.Interpreters.FindInterpreter(PythonVersion.Interpreter, PythonVersion.Configuration.Version);
            Assert.IsNotNull(fact, "Project does not contain expected interpreter");
            projectNode.Interpreters.ActiveInterpreter = fact;
            dteProject.Save();
        }

        [TestMethod, Priority(0)]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CustomCommandsAdded() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                PythonProjectNode node;
                EnvDTE.Project proj;
                OpenProject(app, "Commands1.sln", out node, out proj);

                AssertUtil.ContainsExactly(
                    node._customCommands.Select(cc => cc.DisplayLabel),
                    "Test Command 1",
                    "Test Command 2"
                );

                app.OpenSolutionExplorer().FindItem("Solution 'Commands1' (1 project)", "Commands1").Select();

                AutomationWrapper projectMenu = null;
                for (int retries = 10; retries > 0 && projectMenu == null; --retries) {
                    Thread.Sleep(100);
                    projectMenu = app.FindByAutomationId("MenuBar").AsWrapper().FindByName("Project").AsWrapper();
                }
                Assert.IsNotNull(projectMenu, "Unable to find Project menu");
                projectMenu.Element.EnsureExpanded();

                try {
                    foreach (var name in node._customCommands.Select(cc => cc.DisplayLabelWithoutAccessKeys)) {
                        Assert.IsNotNull(projectMenu.FindByName(name), name + " not found");
                    }
                } finally {
                    try {
                        // Try really really hard to collapse and deselect the
                        // Project menu, since VS will keep it selected and it
                        // may not come back for some reason...
                        projectMenu.Element.Collapse();
                        Keyboard.PressAndRelease(System.Windows.Input.Key.Escape);
                        Keyboard.PressAndRelease(System.Windows.Input.Key.Escape);
                    } catch {
                        // ...but don't try so hard that we fail if we can't 
                        // simulate keypresses.
                    }
                }
            }
        }

        [TestMethod, Priority(0)]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CustomCommandsWithResourceLabel() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                PythonProjectNode node;
                EnvDTE.Project proj;
                OpenProject(app, "Commands2.sln", out node, out proj);

                AssertUtil.ContainsExactly(
                    node._customCommands.Select(cc => cc._label),
                    "resource:PythonToolsUITests;PythonToolsUITests.Resources;CommandName"
                );

                AssertUtil.ContainsExactly(
                    node._customCommands.Select(cc => cc.DisplayLabel),
                    "Command from Resource"
                );
            }
        }

        [TestMethod, Priority(0)]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CustomCommandsReplWithResourceLabel() {
            using (var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte)) {
                PythonProjectNode node;
                EnvDTE.Project proj;
                OpenProject(app, "Commands2.sln", out node, out proj);

                Execute(node, "Command from Resource");

                var repl = app.GetInteractiveWindow("Repl from Resource");
                Assert.IsNotNull(repl, "Could not find repl window");
                repl.WaitForTextEnd(
                    "Program.py completed",
                    (string)repl.ReplWindow.GetOptionValue(ReplOptions.CurrentPrimaryPrompt)
                );

                Assert.IsNull(app.GetInteractiveWindow("resource:PythonToolsUITests;PythonToolsUITests.Resources;ReplName"));
            }
        }

        [TestMethod, Priority(0)]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CustomCommandsRunInRepl() {
            using (var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte)) {
                PythonProjectNode node;
                EnvDTE.Project proj;
                OpenProject(app, "Commands1.sln", out node, out proj);

                Execute(node, "Test Command 2");

                var repl = app.GetInteractiveWindow("Test Repl");
                Assert.IsNotNull(repl, "Could not find repl window");
                repl.WaitForTextEnd(
                    "Program.py completed",
                    (string)repl.ReplWindow.GetOptionValue(ReplOptions.CurrentPrimaryPrompt)
                );
            }
        }

        [TestMethod, Priority(0)]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CustomCommandsRunProcessInRepl() {
            using (var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte)) {
                PythonProjectNode node;
                EnvDTE.Project proj;
                OpenProject(app, "Commands3.sln", out node, out proj);

                Execute(node, "Write to Repl");

                var repl = app.GetInteractiveWindow("Test Repl");
                Assert.IsNotNull(repl, "Could not find repl window");
                repl.WaitForTextEnd(
                    string.Format("({0}, {1})", PythonVersion.Configuration.Version.Major, PythonVersion.Configuration.Version.Minor),
                    (string)repl.ReplWindow.GetOptionValue(ReplOptions.CurrentPrimaryPrompt)
                );
            }
        }

        [TestMethod, Priority(0)]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CustomCommandsRunProcessInOutput() {
            using (var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte)) {
                PythonProjectNode node;
                EnvDTE.Project proj;
                OpenProject(app, "Commands3.sln", out node, out proj);

                Execute(node, "Write to Output");

                var outputWindow = app.Element.FindFirst(TreeScope.Descendants,
                    new AndCondition(
                        new PropertyCondition(AutomationElement.ClassNameProperty, "GenericPane"),
                        new PropertyCondition(AutomationElement.NameProperty, "Output")
                    )
                );
                Assert.IsNotNull(outputWindow, "Output Window was not opened");

                var expected = string.Format("({0}, {1})", PythonVersion.Configuration.Version.Major, PythonVersion.Configuration.Version.Minor);
                var outputText = "";

                for (int retries = 100; !outputText.Contains(expected) && retries > 0; --retries) {
                    Thread.Sleep(100);
                    outputText = outputWindow.FindFirst(TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.ClassNameProperty, "WpfTextView")
                    ).AsWrapper().GetValue();
                }

                Console.WriteLine("Output Window: " + outputText);
                Assert.IsTrue(outputText.Contains(expected), outputText);
            }
        }

        [TestMethod, Priority(0)]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CustomCommandsRunProcessInConsole() {
            using (var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte)) {
                PythonProjectNode node;
                EnvDTE.Project proj;
                OpenProject(app, "Commands3.sln", out node, out proj);

                var existingProcesses = Process.GetProcessesByName("cmd");

                Execute(node, "Write to Console");

                Process newProcess = null;
                for (int retries = 100; retries > 0 && newProcess == null; --retries) {
                    Thread.Sleep(100);
                    newProcess = Process.GetProcessesByName("cmd").Except(existingProcesses).FirstOrDefault();
                }
                Assert.IsNotNull(newProcess, "Process did not start");
                try {
                    Keyboard.PressAndRelease(System.Windows.Input.Key.Space);
                    newProcess.WaitForExit(1000);
                    if (newProcess.HasExited) {
                        newProcess = null;
                    }
                } finally {
                    if (newProcess != null) {
                        newProcess.Kill();
                    }
                }
            }
        }
    }

    [TestClass]
    public class BuildTasksUI25Tests : BuildTasksUI27Tests {
        internal override PythonVersion PythonVersion {
            get {
                return PythonPaths.Python25 ?? PythonPaths.Python25_x64;
            }
        }
    }

    [TestClass]
    public class BuildTasksUI33Tests : BuildTasksUI27Tests {
        internal override PythonVersion PythonVersion {
            get {
                return PythonPaths.Python33 ?? PythonPaths.Python33_x64;
            }
        }
    }
}

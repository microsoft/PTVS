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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Project;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Shell.Interop;
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
            Console.WriteLine("Executing command {0}", commandName);
            projectNode._uiSync.Invoke((Action)(() => {
                projectNode._customCommands.First(cc => cc.DisplayLabel == commandName).Execute(projectNode);
            }));
        }

        internal Task ExecuteAsync(PythonProjectNode projectNode, string commandName) {
            Task task = null;
            Console.WriteLine("Executing command {0} asynchronously", commandName);
            projectNode._uiSync.Invoke((Action)(() => {
                task = projectNode._customCommands.First(cc => cc.DisplayLabel == commandName).ExecuteAsync(projectNode);
            }));
            return task;
        }

        internal void OpenProject(VisualStudioApp app, string slnName, out PythonProjectNode projectNode, out EnvDTE.Project dteProject) {
            PythonVersion.AssertInstalled();

            dteProject = app.OpenProject("TestData\\Targets\\" + slnName);
            projectNode = dteProject.GetPythonProject();
            var fact = projectNode.Interpreters.FindInterpreter(PythonVersion.Id, PythonVersion.Configuration.Version);
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
                    node._customCommands.Select(cc => cc.Label),
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

                app.Dte.Solution.Close();

                Assert.IsNull(app.GetInteractiveWindow("Test Repl"), "Repl window was not closed");
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

        private static void ExpectOutputWindowText(VisualStudioApp app, string expected) {
            var outputWindow = app.Element.FindFirst(TreeScope.Descendants,
                new AndCondition(
                    new PropertyCondition(AutomationElement.ClassNameProperty, "GenericPane"),
                    new PropertyCondition(AutomationElement.NameProperty, "Output")
                )
            );
            Assert.IsNotNull(outputWindow, "Output Window was not opened");

            var outputText = "";
            for (int retries = 100; !outputText.Contains(expected) && retries > 0; --retries) {
                Thread.Sleep(100);
                outputText = outputWindow.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ClassNameProperty, "WpfTextView")
                ).AsWrapper().GetValue();
            }

            Console.WriteLine("Output Window: " + outputText);
            Assert.IsTrue(outputText.Contains(expected), string.Format("Expected to see:\r\n\r\n{0}\r\n\r\nActual content:\r\n\r\n{1}", expected, outputText));
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

                ExpectOutputWindowText(app, string.Format("({0}, {1})", PythonVersion.Configuration.Version.Major, PythonVersion.Configuration.Version.Minor));
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

        [TestMethod, Priority(0)]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CustomCommandsErrorList() {
            using (var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte)) {
                PythonProjectNode node;
                EnvDTE.Project proj;
                OpenProject(app, "ErrorCommand.sln", out node, out proj);

                var expectedItems = new[] {
                    new { Document = "Program.py", Line = 0, Column = 1, Category = __VSERRORCATEGORY.EC_ERROR, Message = "This is an error with a relative path." },
                    new { Document = Path.Combine(node.ProjectHome, "Program.py"), Line = 2, Column = 3, Category = __VSERRORCATEGORY.EC_WARNING, Message = "This is a warning with an absolute path." },
                    new { Document = ">>>", Line = 4, Column = -1, Category = __VSERRORCATEGORY.EC_ERROR, Message = "This is an error with an invalid path." },
                };

                Execute(node, "Produce Errors");
                var items = app.WaitForErrorListItems(3);
                Assert.AreEqual(expectedItems.Length, items.Count);

                // Second invoke should replace the error items in the list, not add new ones to those already existing.
                Execute(node, "Produce Errors");
                items = app.WaitForErrorListItems(3);
                Assert.AreEqual(expectedItems.Length, items.Count);

                for (int i = 0; i < expectedItems.Length; ++i) {
                    var item = items[i];
                    var expectedItem = expectedItems[i];

                    string document, message;
                    item.Document(out document);
                    item.get_Text(out message);

                    int line, column;
                    item.Line(out line);
                    item.Column(out column);

                    uint category;
                    ((IVsErrorItem)item).GetCategory(out category);

                    Assert.AreEqual(expectedItem.Document, document);
                    Assert.AreEqual(expectedItem.Line, line);
                    Assert.AreEqual(expectedItem.Column, column);
                    Assert.AreEqual(expectedItem.Message, message);
                    Assert.AreEqual(expectedItem.Category, (__VSERRORCATEGORY)category);
                }

                node._uiSync.Invoke((Action)delegate { items[0].NavigateTo(); });

                var doc = app.Dte.ActiveDocument;
                Assert.IsNotNull(doc);
                Assert.AreEqual("Program.py", doc.Name);

                var textDoc = (EnvDTE.TextDocument)doc.Object("TextDocument");
                Assert.AreEqual(1, textDoc.Selection.ActivePoint.Line);
                Assert.AreEqual(2, textDoc.Selection.ActivePoint.DisplayColumn);
            }
        }

        [TestMethod, Priority(0)]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CustomCommandsRequiredPackages() {
            using (var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte))
            using (var dis = app.SelectDefaultInterpreter(PythonVersion, "virtualenv")) {
                PythonProjectNode node;
                EnvDTE.Project proj;
                OpenProject(app, "CommandRequirePackages.sln", out node, out proj);

                string envName;
                var env = app.CreateVirtualEnvironment(proj, out envName);

                env.Select();
                app.Dte.ExecuteCommand("Project.ActivateEnvironment");
                // Ensure that no error dialog appears
                app.WaitForNoDialog(TimeSpan.FromSeconds(5.0));

                // First, execute the command and cancel it.
                using (var task = ExecuteAsync(node, "Require Packages")) {
                    var dialogHandle = app.WaitForDialog(task);
                    if (dialogHandle == IntPtr.Zero) {
                        if (task.IsFaulted && task.Exception != null) {
                            Assert.Fail("Unexpected exception in package install confirmation dialog:\n{0}", task.Exception);
                        } else {
                            Assert.AreNotEqual(IntPtr.Zero, dialogHandle);
                        }
                    }

                    using (var dialog = new AutomationDialog(app, AutomationElement.FromHandle(dialogHandle))) {
                        var label = dialog.FindByAutomationId("65535");
                        Assert.IsNotNull(label);

                        string expectedLabel =
                            "The following packages will be installed from PyPI in order to run this command:\r\n" +
                            "\r\n" +
                            "ptvsd\r\n" +
                            "azure==0.1\r\n" +
                            "\r\n" +
                            "Do you want to continue?";
                        Assert.AreEqual(expectedLabel, label.Current.Name);

                        dialog.Cancel();
                        try {
                            task.Wait(1000);
                            Assert.Fail("Command was not canceled after dismissing the package install confirmation dialog");
                        } catch (AggregateException ex) {
                            if (!(ex.InnerException is TaskCanceledException)) {
                                throw;
                            }
                        }
                    }
                }

                // Then, execute command and allow it to proceed.
                using (var task = ExecuteAsync(node, "Require Packages")) {
                    var dialogHandle = app.WaitForDialog(task);
                    if (dialogHandle == IntPtr.Zero) {
                        if (task.IsFaulted && task.Exception != null) {
                            Assert.Fail("Unexpected exception in package install confirmation dialog:\n{0}", task.Exception);
                        } else {
                            Assert.AreNotEqual(IntPtr.Zero, dialogHandle);
                        }
                    }

                    using (var dialog = new AutomationDialog(app, AutomationElement.FromHandle(dialogHandle))) {
                        dialog.OK();
                    }
                    task.Wait();

                    var ver = PythonVersion.Version.ToVersion();
                    ExpectOutputWindowText(app, string.Format("pass {0}.{1}", ver.Major, ver.Minor));
                }
            }
        }


        [TestMethod, Priority(0)]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CustomCommandsSearchPath() {
            var expectedSearchPath = string.Format("['{0}', '{1}']",
                TestData.GetPath(@"TestData\Targets").Replace("\\", "\\\\"),
                TestData.GetPath(@"TestData\Targets\Package").Replace("\\", "\\\\")
            );
           
            using (var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte)) {
                PythonProjectNode node;
                EnvDTE.Project proj;
                OpenProject(app, "CommandSearchPath.sln", out node, out proj);

                Execute(node, "Import From Search Path");

                var outputWindow = app.Element.FindFirst(TreeScope.Descendants,
                    new AndCondition(
                        new PropertyCondition(AutomationElement.ClassNameProperty, "GenericPane"),
                        new PropertyCondition(AutomationElement.NameProperty, "Output")
                    )
                );
                Assert.IsNotNull(outputWindow, "Output Window was not opened");

                ExpectOutputWindowText(app, expectedSearchPath);
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

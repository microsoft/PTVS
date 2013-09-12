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
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.InterpreterList;
using Microsoft.PythonTools.Parsing;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;

namespace PythonToolsUITests {
    [TestClass]
    public class InterpreterListTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            TestData.Deploy();
        }

        #region Tests requiring VS

        [TestMethod, Priority(0), TestCategory("InterpreterList")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void GetInstalledInterpreters() {
            var interps = InterpreterView.GetInterpreters().ToList();
            foreach (var ver in PythonPaths.Versions) {
                var expected = AnalyzerStatusUpdater.GetIdentifier(ver.Interpreter, ver.Version.ToVersion());
                Assert.AreEqual(1, interps.Count(iv => iv.Identifier.Equals(expected, StringComparison.Ordinal)), expected);
            }
        }

        [TestMethod, Priority(0), TestCategory("InterpreterList")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void InterpreterListInVS() {
            var dte = VsIdeTestHostContext.Dte;
            var model = (IComponentModel)VsIdeTestHostContext.ServiceProvider.GetService(typeof(SComponentModel));
            var service = model.GetService<IInterpreterOptionsService>();
            Assert.IsNotNull(service);

            dte.ExecuteCommand("View.PythonEnvironments");
            var list = new VisualStudioApp(dte).FindByAutomationId("PythonTools.InterpreterList");
            Assert.IsNotNull(list);

            var allNames = new HashSet<string>(service.Interpreters.Select(i => i.Description));
            var names = list.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "InterpreterName"));
            foreach (var obj in names.Cast<AutomationElement>()) {
                var name = (string)obj.GetCurrentPropertyValue(AutomationElement.NameProperty);
                Assert.IsTrue(allNames.Remove(name));
            }
            Assert.AreEqual(0, allNames.Count);
        }

        [TestMethod, Priority(0), TestCategory("InterpreterList")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CreateRemoveVirtualEnvInInterpreterListInVS() {
            var dte = VsIdeTestHostContext.Dte;
            var app = new VisualStudioApp(dte);
            var newProjDialog = app.FileNewProject();
            newProjDialog.Location = Path.GetTempPath();

            newProjDialog.FocusLanguageNode();

            var consoleApp = newProjDialog.ProjectTypes.FindItem("Python Application");
            consoleApp.Select();

            newProjDialog.ClickOK();

            try {
                // wait for new solution to load...
                for (int i = 0; i < 100 && app.Dte.Solution.Projects.Count == 0; i++) {
                    System.Threading.Thread.Sleep(1000);
                }

                Assert.AreEqual(1, app.Dte.Solution.Projects.Count);

                Assert.AreNotEqual(null, app.Dte.Solution.Projects.Item(1).ProjectItems.Item(Path.GetFileNameWithoutExtension(app.Dte.Solution.FullName) + ".py"));

                // Check that only global environments are in the list
                var model = (IComponentModel)VsIdeTestHostContext.ServiceProvider.GetService(typeof(SComponentModel));
                var service = model.GetService<IInterpreterOptionsService>();
                Assert.IsNotNull(service);

                dte.ExecuteCommand("View.PythonEnvironments");
                var list = app.FindByAutomationId("PythonTools.InterpreterList");
                Assert.IsNotNull(list);

                var allNames = new HashSet<string>(service.Interpreters.Select(i => i.Description));

                var names = list.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "InterpreterName"));
                foreach (var obj in names.Cast<AutomationElement>()) {
                    var name = (string)obj.GetCurrentPropertyValue(AutomationElement.NameProperty);
                    Assert.IsTrue(allNames.Remove(name), name + " should not have been in UI");
                }
                Assert.AreEqual(0, allNames.Count);


                // Create a virtual environment
                string envName;
                var env = VirtualEnvTests.CreateVirtualEnvironment(app, out envName);
                env.Select();

                dte.ExecuteCommand("View.PythonEnvironments");
                list = app.FindByAutomationId("PythonTools.InterpreterList");
                Assert.IsNotNull(list);

                // Check that it has been added to the list
                allNames = new HashSet<string>(service.Interpreters.Select(i => i.Description));
                allNames.Add(envName);

                names = list.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "InterpreterName"));
                foreach (var obj in names.Cast<AutomationElement>()) {
                    var name = (string)obj.GetCurrentPropertyValue(AutomationElement.NameProperty);
                    Assert.IsTrue(allNames.Remove(name), name + " should not have been in UI");
                }
                Assert.AreEqual(0, allNames.Count);

                // Remove the virtual environment
                env.SetFocus();

                var removeDeleteDlg = new AutomationWrapper(AutomationElement.FromHandle(
                    app.OpenDialogWithDteExecuteCommand("Edit.Delete")));
                removeDeleteDlg.ClickButtonByName("Remove");
                app.WaitForDialogDismissed();

                app.SolutionExplorerTreeView.WaitForItemRemoved(
                    "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                    app.Dte.Solution.Projects.Item(1).Name,
                    "Python Environments",
                    envName);

                // Check that only global environments are in the list
                allNames = new HashSet<string>(service.Interpreters.Select(i => i.Description));

                dte.ExecuteCommand("View.PythonEnvironments");
                list = app.FindByAutomationId("PythonTools.InterpreterList");
                Assert.IsNotNull(list);
                names = list.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "InterpreterName"));
                foreach (var obj in names.Cast<AutomationElement>()) {
                    var name = (string)obj.GetCurrentPropertyValue(AutomationElement.NameProperty);
                    Assert.IsTrue(allNames.Remove(name), name + " should not have been in UI");
                }
                Assert.AreEqual(0, allNames.Count);

                dte.Solution.Close(SaveFirst: false);
            } finally {
                dte.Solution.Close();
            }
        }

        [TestMethod, Priority(0), TestCategory("InterpreterList")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void LoadUnloadVirtualEnvInInterpreterListInVS() {
            var dte = VsIdeTestHostContext.Dte;
            var app = new VisualStudioApp(dte);
            var proj = app.OpenAndFindProject(@"TestData\VirtualEnv.sln");

            try {
                var model = (IComponentModel)VsIdeTestHostContext.ServiceProvider.GetService(typeof(SComponentModel));
                var service = model.GetService<IInterpreterOptionsService>();
                Assert.IsNotNull(service);

                dte.ExecuteCommand("View.PythonEnvironments");
                var list = new VisualStudioApp(dte).FindByAutomationId("PythonTools.InterpreterList");
                Assert.IsNotNull(list);

                var allNames = new HashSet<string>(service.Interpreters.Select(i => i.Description));
                allNames.Add("env (Python 2.7)");

                var names = list.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "InterpreterName"));
                foreach (var obj in names.Cast<AutomationElement>()) {
                    var name = (string)obj.GetCurrentPropertyValue(AutomationElement.NameProperty);
                    Assert.IsTrue(allNames.Remove(name), name + " should not have been in UI");
                }
                Assert.AreEqual(0, allNames.Count);

                proj.Delete();

                allNames = new HashSet<string>(service.Interpreters.Select(i => i.Description));

                names = list.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "InterpreterName"));
                foreach (var obj in names.Cast<AutomationElement>()) {
                    var name = (string)obj.GetCurrentPropertyValue(AutomationElement.NameProperty);
                    Assert.IsTrue(allNames.Remove(name), name + " should not have been in UI");
                }
                Assert.AreEqual(0, allNames.Count);
            } finally {
                dte.Solution.Close(SaveFirst: false);
            }
        }

        [TestMethod, Priority(0), TestCategory("InterpreterList")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ActivateVirtualEnvInInterpreterListInVS() {
            var dte = VsIdeTestHostContext.Dte;
            var app = new VisualStudioApp(dte);
            var proj = app.OpenAndFindProject(@"TestData\VirtualEnv.sln");

            try {
                dte.ExecuteCommand("View.PythonEnvironments");
                var list = new VisualStudioApp(dte).FindByAutomationId("PythonTools.InterpreterList");
                Assert.IsNotNull(list, "interpreter list is null");

                // Check that the current environment is the virtual environment
                Guid venvId = Guid.Parse((string)proj.Properties.Item("InterpreterId").Value);

                // Get the activate button and check that it's disabled because the project should have the virtual environment
                // activated already
                var activateButton = list.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "Activate"));
                Assert.IsNotNull(activateButton);
                Assert.IsFalse((bool)activateButton.GetCurrentPropertyValue(AutomationElement.IsEnabledProperty), "Activate button is not disabled");

                // Enable another interpreter so the virtual environment is deactivated
                var python27Env = new AutomationWrapper(app.SolutionExplorerTreeView.FindItem(
                    "Solution '" + app.Dte.Solution.Projects.Item(1).Name + "' (1 project)",
                    app.Dte.Solution.Projects.Item(1).Name,
                    "Python Environments",
                    "Python 2.7"));
                python27Env.Select();
                app.Dte.ExecuteCommand("Project.ActivateEnvironment");

                // Check that the activate button for the virtual environment is now enabled and the interpreter
                // id has been changed to something else
                Guid interpreterId = Guid.Parse((string)proj.Properties.Item("InterpreterId").Value);
                activateButton = list.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "Activate"));
                Assert.IsTrue((bool)activateButton.GetCurrentPropertyValue(AutomationElement.IsEnabledProperty), "Activate button is not enabled");
                Assert.IsFalse(interpreterId == venvId, "The active interpreter hasn't been set to Python 2.7");

                // Activate the virtual environment by clicking on it
                ((InvokePattern)activateButton.GetCurrentPattern(InvokePattern.Pattern)).Invoke();

                // Check that the activate button is now disabled
                activateButton = list.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "Activate"));
                Assert.IsFalse((bool)activateButton.GetCurrentPropertyValue(AutomationElement.IsEnabledProperty), "Activate button is not redisabled");

                // Check that the virtual environment is now selected
                interpreterId = Guid.Parse((string)proj.Properties.Item("InterpreterId").Value);
                Assert.IsTrue(interpreterId == venvId, "The active interpreter hasn't been set back to the virtual environment");
            } finally {
                dte.Solution.Close(SaveFirst: false);
            }
        }

        [TestMethod, Priority(0), TestCategory("InterpreterList")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ChangeDefaultInVS() {
            var dte = VsIdeTestHostContext.Dte;
            var model = (IComponentModel)VsIdeTestHostContext.ServiceProvider.GetService(typeof(SComponentModel));
            var service = model.GetService<IInterpreterOptionsService>();
            Assert.IsNotNull(service);

            var originalDefault = service.DefaultInterpreter;

            dte.ExecuteCommand("View.PythonEnvironments");
            var list = new VisualStudioApp(dte).FindByAutomationId("PythonTools.InterpreterList");
            Assert.IsNotNull(list);

            var buttons = list.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "MakeDefault")).Cast<AutomationElement>().ToList();
            Assert.AreEqual(service.Interpreters.Count(), buttons.Count);
            Assert.AreEqual(1, buttons.Count(b => !(bool)b.GetCurrentPropertyValue(AutomationElement.IsEnabledProperty)));

            service.DefaultInterpreter = null;
            // Now the last button is disabled. Assume we have more than one
            // button available
            try {
                foreach (var button in buttons) {
                    var prev = service.DefaultInterpreter;

                    Assert.IsTrue((bool)button.GetCurrentPropertyValue(AutomationElement.IsEnabledProperty));
                    ((InvokePattern)button.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
                    // 'Clicking' the button does not immediately disable it,
                    // and the VS machinery may take some time to catch up.
                    // After the first change, things should be much quicker.
                    for (int retries = 10; retries > 0; --retries) {
                        Thread.Sleep(100);
                        if (!(bool)button.GetCurrentPropertyValue(AutomationElement.IsEnabledProperty)) {
                            break;
                        }
                    }
                    Assert.IsFalse((bool)button.GetCurrentPropertyValue(AutomationElement.IsEnabledProperty));

                    Assert.AreNotEqual(prev, service.DefaultInterpreter);
                }
            } finally {
                service.DefaultInterpreter = originalDefault;
            }
        }

        [TestMethod, Priority(0), TestCategory("InterpreterList")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void InvalidCustomInterpreterInVS() {
            var model = (IComponentModel)VsIdeTestHostContext.ServiceProvider.GetService(typeof(SComponentModel));
            var service = model.GetService<IInterpreterOptionsService>();
            Assert.IsNotNull(service);

            var countBefore = service.Interpreters.Count();

            var configurable = service.KnownProviders.OfType<ConfigurablePythonInterpreterFactoryProvider>().Single();
            var fake = configurable.SetOptions(new InterpreterFactoryCreationOptions {
                Id = Guid.NewGuid(),
                InterpreterPath = @"C:\Path\That\Probably\Does\Not\Exist",
                WindowInterpreterPath = "",
                Architecture = ProcessorArchitecture.None,
                LanguageVersion = new Version(2, 7),
                PathEnvironmentVariableName = "PYTHONPATH",
                Description = "Invalid"
            });

            var dte = VsIdeTestHostContext.Dte;
            dte.ExecuteCommand("View.PythonEnvironments");
            var list = new VisualStudioApp(dte).FindByAutomationId("PythonTools.InterpreterList");

            bool testFailed = true;
            try {
                Assert.AreEqual(countBefore + 1, service.Interpreters.Count());
                Assert.IsNotNull(list);

                var rowCount = (int)list.GetCurrentPropertyValue(GridPattern.RowCountProperty);
                Assert.AreEqual(service.Interpreters.Count(), rowCount);
                // Until WPF ListView is fixed, we can't run the rest of the test.
                // The problem is that newly added list items are not visible to
                // automation. This is probably due to the virtualization issues
                // mentioned at http://social.msdn.microsoft.com/Forums/en-US/windowsaccessibilityandautomation/thread/e33e0de5-61e1-46e7-85a0-586d3f7c244c

                //var grid = (GridPattern)list.GetCurrentPattern(GridPattern.Pattern);
                //bool foundIt = false;
                //for (int row = 0; row < rowCount; ++row) {
                //    var label = grid.GetItem(row, 0).FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "InterpreterName"));
                //    var name = (string)label.GetCurrentPropertyValue(AutomationElement.NameProperty);
                //    Console.WriteLine(name);
                //    if (!name.StartsWith("Invalid")) {
                //        continue;
                //    }
                //    foundIt = true;

                //    var button = grid.GetItem(row, 1).FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "OpenInteractive"));
                //    Assert.IsFalse((bool)button.GetCurrentPropertyValue(AutomationElement.IsEnabledProperty));

                //    var message = grid.GetItem(row, 4).FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "NoDatabase"));
                //    Assert.IsFalse((bool)message.GetCurrentPropertyValue(AutomationElement.IsOffscreenProperty));
                //}
                //Assert.IsTrue(foundIt, "Fake interpreter was not found");

                testFailed = false;
            } finally {
                configurable.RemoveInterpreter(fake.Id);
                if (!testFailed) {
                    // Don't bother doing more checks if we've already failed
                    var rowCount = (int)list.GetCurrentPropertyValue(GridPattern.RowCountProperty);
                    Assert.AreEqual(service.Interpreters.Count(), rowCount);
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("InterpreterList")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void RefreshDBStatesInVS() {
            var dte = VsIdeTestHostContext.Dte;
            var model = (IComponentModel)VsIdeTestHostContext.ServiceProvider.GetService(typeof(SComponentModel));
            var service = model.GetService<IInterpreterOptionsService>();
            Assert.IsNotNull(service);

            var fact = new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 1", PythonPaths.Versions.First().Configuration);
            var identifier = AnalyzerStatusUpdater.GetIdentifier(fact);

            var oldProviders = ((InterpreterOptionsService)service).SetProviders(new[] { new MockPythonInterpreterFactoryProvider("Test Provider 1", fact) });
            try {
                Assert.AreEqual(1, service.Interpreters.Count());

                dte.ExecuteCommand("View.PythonEnvironments");
                var list = new AutomationWrapper(new VisualStudioApp(dte).FindByAutomationId("PythonTools.InterpreterList"));
                Assert.IsNotNull(list);

                var rowCount = (int)list.Element.GetCurrentPropertyValue(GridPattern.RowCountProperty);
                Assert.AreEqual(service.Interpreters.Count(), rowCount);

                var window = PythonToolsPackage.Instance.FindWindowPane(typeof(InterpreterListToolWindow), 0, true) as WindowPane;
                var interpreterList = window.Content as InterpreterList;

                var label = list.FindByAutomationId("InterpreterName");
                var name = (string)label.GetCurrentPropertyValue(AutomationElement.NameProperty);
                Assert.AreEqual(fact.Description, name);

                var button = list.FindButton("Regenerate");
                Assert.IsNotNull(button);
                var buttonClick = (InvokePattern)button.GetCurrentPattern(InvokePattern.Pattern);
                Assert.IsNotNull(buttonClick);
                var notRequired = list.FindByAutomationId("RegenerateNotRequired");
                Assert.IsNotNull(notRequired);
                var required = list.FindByAutomationId("RegenerateRequired");
                Assert.IsNotNull(required);
                var progress = list.FindByAutomationId("Progress");
                Assert.IsNotNull(progress);

                Assert.IsFalse((bool)required.GetCurrentPropertyValue(AutomationElement.IsOffscreenProperty));
                Assert.IsTrue((bool)button.GetCurrentPropertyValue(AutomationElement.IsEnabledProperty));
                Assert.IsTrue((bool)progress.GetCurrentPropertyValue(AutomationElement.IsOffscreenProperty));

                buttonClick.Invoke();
                Thread.Sleep(1000);

                Assert.IsTrue((bool)required.GetCurrentPropertyValue(AutomationElement.IsOffscreenProperty));
                Assert.IsTrue((bool)button.GetCurrentPropertyValue(AutomationElement.IsOffscreenProperty));
                Assert.IsFalse((bool)progress.GetCurrentPropertyValue(AutomationElement.IsOffscreenProperty));

                fact.EndGenerateCompletionDatabase(identifier, false);
                interpreterList.Dispatcher.Invoke((Action)(() => { CommandManager.InvalidateRequerySuggested(); }));
                Thread.Sleep(1000);

                Assert.IsFalse((bool)required.GetCurrentPropertyValue(AutomationElement.IsOffscreenProperty));
                Assert.IsTrue((bool)button.GetCurrentPropertyValue(AutomationElement.IsEnabledProperty));
                Assert.IsTrue((bool)progress.GetCurrentPropertyValue(AutomationElement.IsOffscreenProperty));

                buttonClick.Invoke();
                Thread.Sleep(500);

                Assert.IsTrue((bool)required.GetCurrentPropertyValue(AutomationElement.IsOffscreenProperty));
                Assert.IsTrue((bool)button.GetCurrentPropertyValue(AutomationElement.IsOffscreenProperty));
                Assert.IsFalse((bool)progress.GetCurrentPropertyValue(AutomationElement.IsOffscreenProperty));

                fact.EndGenerateCompletionDatabase(identifier, true);
                interpreterList.Dispatcher.Invoke((Action)(() => { CommandManager.InvalidateRequerySuggested(); }));
                Thread.Sleep(1000);

                Assert.IsTrue((bool)required.GetCurrentPropertyValue(AutomationElement.IsOffscreenProperty));
                Assert.IsFalse((bool)notRequired.GetCurrentPropertyValue(AutomationElement.IsOffscreenProperty));
                Assert.IsTrue((bool)button.GetCurrentPropertyValue(AutomationElement.IsEnabledProperty));
                Assert.IsTrue((bool)progress.GetCurrentPropertyValue(AutomationElement.IsOffscreenProperty));
            } finally {
                ((InterpreterOptionsService)service).SetProviders(oldProviders);
            }
        }

        [TestMethod, Priority(0), TestCategory("InterpreterList")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void InvalidCustomInterpreterDoesNotCrashInVS() {
            var model = (IComponentModel)VsIdeTestHostContext.ServiceProvider.GetService(typeof(SComponentModel));
            var service = model.GetService<IInterpreterOptionsService>();
            Assert.IsNotNull(service);

            var configurable = service.KnownProviders.OfType<ConfigurablePythonInterpreterFactoryProvider>().Single();
            var fake = configurable.SetOptions(new InterpreterFactoryCreationOptions {
                Id = Guid.NewGuid(),
                InterpreterPath = @"C:\Path\That\Probably\Does\Not\Exist",
                WindowInterpreterPath = "",
                Architecture = ProcessorArchitecture.None,
                LanguageVersion = new Version(2, 7),
                PathEnvironmentVariableName = "PYTHONPATH",
                Description = "Invalid"
            });

            try {
                // Not crashing is sufficient to ensure that
                // https://pytools.codeplex.com/workitem/1199 is fixed.
                var withDb = (IPythonInterpreterFactoryWithDatabase)fake;
                withDb.GenerateDatabase(GenerateDatabaseOptions.None);
            } finally {
                configurable.RemoveInterpreter(fake.Id);
            }
        }

        #endregion

        #region Tests not requiring VS

        private static InterpreterConfiguration MockInterpreterConfiguration(Version version) {
            return new InterpreterConfiguration(version);
        }

        private static InterpreterConfiguration MockInterpreterConfiguration(string path) {
            return new InterpreterConfiguration(Path.GetDirectoryName(path), path, "", "", "", ProcessorArchitecture.None, new Version(2, 7));
        }

        [TestMethod, Priority(0), TestCategory("InterpreterListNonUI")]
        public void HasInterpreters() {
            var mockService = new MockInterpreterOptionsService();
            mockService.AddProvider(new MockPythonInterpreterFactoryProvider("Test Provider 1",
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 1", MockInterpreterConfiguration(new Version(2, 7))),
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 2", MockInterpreterConfiguration(new Version(3, 0))),
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 3", MockInterpreterConfiguration(new Version(3, 3)))
            ));
            mockService.AddProvider(new MockPythonInterpreterFactoryProvider("Test Provider 2",
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 4", MockInterpreterConfiguration(new Version(2, 7))),
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 5", MockInterpreterConfiguration(new Version(3, 0))),
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 6", MockInterpreterConfiguration(new Version(3, 3)))
            ));

            var list = new InterpreterList(mockService);

            Assert.AreEqual(6, list.Interpreters.Count);
        }

        [TestMethod, Priority(0), TestCategory("InterpreterListNonUI")]
        public void AddProviders() {
            var mockService = new MockInterpreterOptionsService();
            var list = new InterpreterList(mockService);

            Assert.AreEqual(0, list.Interpreters.Count);

            mockService.AddProvider(new MockPythonInterpreterFactoryProvider("Test Provider 1",
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 1", MockInterpreterConfiguration(new Version(2, 7))),
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 2", MockInterpreterConfiguration(new Version(3, 0))),
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 3", MockInterpreterConfiguration(new Version(3, 3)))
            ));

            Assert.AreEqual(3, list.Interpreters.Count);

            mockService.AddProvider(new MockPythonInterpreterFactoryProvider("Test Provider 2",
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 4", MockInterpreterConfiguration(new Version(2, 7))),
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 5", MockInterpreterConfiguration(new Version(3, 0))),
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 6", MockInterpreterConfiguration(new Version(3, 3)))
            ));

            Assert.AreEqual(6, list.Interpreters.Count);
        }

        [TestMethod, Priority(0), TestCategory("InterpreterListNonUI")]
        public void AddFactories() {
            var mockService = new MockInterpreterOptionsService();
            var list = new InterpreterList(mockService);

            Assert.AreEqual(0, list.Interpreters.Count);

            var provider = new MockPythonInterpreterFactoryProvider("Test Provider 1",
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 1", MockInterpreterConfiguration(new Version(2, 7))),
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 2", MockInterpreterConfiguration(new Version(3, 0))),
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 3", MockInterpreterConfiguration(new Version(3, 3)))
            );

            mockService.AddProvider(provider);

            Assert.AreEqual(3, list.Interpreters.Count);
            provider.AddFactory(new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 4", MockInterpreterConfiguration(new Version(2, 7))));
            Assert.AreEqual(4, list.Interpreters.Count);
            provider.AddFactory(new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 5", MockInterpreterConfiguration(new Version(3, 0))));
            Assert.AreEqual(5, list.Interpreters.Count);
            provider.AddFactory(new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 6", MockInterpreterConfiguration(new Version(3, 3))));
            Assert.AreEqual(6, list.Interpreters.Count);
        }

        [TestMethod, Priority(0), TestCategory("InterpreterListNonUI")]
        public void FactoryWithInvalidPath() {
            foreach (string invalidPath in new string[] { 
                null, 
                "", 
                "NOT A REAL PATH", 
                string.Join("\\", System.IO.Path.GetInvalidPathChars().Select(c => c.ToString()))
            }) {
                var fact = new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory", new InterpreterConfiguration(invalidPath, invalidPath, "", "", "", ProcessorArchitecture.None, new Version(2, 7)));
                var view = new InterpreterView(fact, fact.Description, false);
                Assert.IsFalse(view.CanRefresh);
            }
        }

        [TestMethod, Priority(0), TestCategory("InterpreterListNonUI")]
        public void FactoryWithValidPath() {
            foreach (string validPath in PythonPaths.Versions.Select(pv => pv.Path)) {
                var fact = new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory", MockInterpreterConfiguration(validPath));
                var view = new InterpreterView(fact, fact.Description, false);
                Assert.IsTrue(view.CanRefresh);
            }
        }

        [TestMethod, Priority(0), TestCategory("InterpreterListNonUI")]
        public void RefreshDBButton() {
            var mockService = new MockInterpreterOptionsService();
            var fact = new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 1", MockInterpreterConfiguration(new Version(2, 7)));
            var provider = new MockPythonInterpreterFactoryProvider("Test Provider 1", fact);
            mockService.AddProvider(provider);

            var list = new InterpreterList(mockService);
            var listView = (System.Windows.Controls.ListView)list.FindName("interpreterList");
            var wnd = new Window() { Content = list };
            wnd.ShowInTaskbar = false;
            wnd.WindowState = WindowState.Minimized;
            wnd.Show();

            // Ensure the command is bound
            Assert.IsTrue(list.CommandBindings.OfType<CommandBinding>().Any(cb => cb.Command == InterpreterList.RegenerateCommand));

            Assert.IsFalse(list.Interpreters[0].CanRefresh);
            Assert.IsFalse(InterpreterList.RegenerateCommand.CanExecute(list.Interpreters[0], list));

            Assert.IsTrue(provider.RemoveFactory(fact));
            Assert.AreEqual(0, listView.Items.Count);
            fact = new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 2", MockInterpreterConfiguration(PythonPaths.Versions.First().Path));
            provider.AddFactory(fact);
            Assert.AreEqual("Test Factory 2", ((InterpreterView)listView.Items[0]).Interpreter.Description);

            Assert.IsTrue(list.Interpreters[0].CanRefresh);
            Assert.IsTrue(InterpreterList.RegenerateCommand.CanExecute(list.Interpreters[0], list));

            wnd.Close();
        }

        [TestMethod, Priority(0), TestCategory("InterpreterListNonUI")]
        public void RefreshDBStates() {
            var mockService = new MockInterpreterOptionsService();
            var fact = new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 1", MockInterpreterConfiguration(PythonPaths.Versions.First().Path));
            mockService.AddProvider(new MockPythonInterpreterFactoryProvider("Test Provider 1", fact));

            var e = new AutoResetEvent(false);
            InterpreterList list = null;
            InterpreterView view = null;
            Window wnd = null;
            var t = new Thread(() => {
                list = new InterpreterList(mockService);
                view = list.Interpreters[0];

                wnd = new Window() { Content = list };
                wnd.ShowInTaskbar = false;
                wnd.WindowState = WindowState.Minimized;
                wnd.Show();
                e.Set();
                Dispatcher.Run();
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            Assert.IsTrue(e.WaitOne(5000));


            Assert.IsTrue((bool)view.Dispatcher.Invoke((Func<bool>)(() => view.CanRefresh)));
            Assert.IsTrue((bool)view.Dispatcher.Invoke((Func<bool>)(() => InterpreterList.RegenerateCommand.CanExecute(view, list))));
            Assert.IsFalse(fact.IsCurrent);
            Assert.AreEqual(MockPythonInterpreterFactory.NoDatabaseReason, fact.GetIsCurrentReason(null));

            view.Dispatcher.Invoke((Action)(() => InterpreterList.RegenerateCommand.Execute(view, list)));

            Assert.IsTrue((bool)view.Dispatcher.Invoke((Func<bool>)(() => view.IsRunning)));
            Assert.IsFalse((bool)view.Dispatcher.Invoke((Func<bool>)(() => InterpreterList.RegenerateCommand.CanExecute(view, list))));
            Assert.IsFalse(fact.IsCurrent);
            Assert.AreEqual(MockPythonInterpreterFactory.GeneratingReason, fact.GetIsCurrentReason(null));

            fact.EndGenerateCompletionDatabase(list, view.Identifier, false, true);
            while ((bool)view.Dispatcher.Invoke((Func<bool>)(() => view.IsRunning))) {
                view.Dispatcher.BeginInvoke((Action)(() => { e.Set(); }), DispatcherPriority.ApplicationIdle);
                Assert.IsTrue(e.WaitOne(5000));
            }

            Assert.IsFalse((bool)view.Dispatcher.Invoke((Func<bool>)(() => view.IsRunning)));
            Assert.IsTrue((bool)view.Dispatcher.Invoke((Func<bool>)(() => InterpreterList.RegenerateCommand.CanExecute(view, list))));
            Assert.IsFalse(fact.IsCurrent);
            Assert.AreEqual(MockPythonInterpreterFactory.MissingModulesReason, fact.GetIsCurrentReason(null));

            view.Dispatcher.Invoke((Action)(() => InterpreterList.RegenerateCommand.Execute(view, list)));

            Assert.IsTrue((bool)view.Dispatcher.Invoke((Func<bool>)(() => view.IsRunning)));
            Assert.IsFalse((bool)view.Dispatcher.Invoke((Func<bool>)(() => InterpreterList.RegenerateCommand.CanExecute(view, list))));
            Assert.IsFalse(fact.IsCurrent);
            Assert.AreEqual(MockPythonInterpreterFactory.GeneratingReason, fact.GetIsCurrentReason(null));

            fact.EndGenerateCompletionDatabase(list, view.Identifier, true, true);
            while ((bool)view.Dispatcher.Invoke((Func<bool>)(() => view.IsRunning))) {
                view.Dispatcher.BeginInvoke((Action)(() => { e.Set(); }), DispatcherPriority.ApplicationIdle);
                Assert.IsTrue(e.WaitOne(5000));
            }

            Assert.IsFalse((bool)view.Dispatcher.Invoke((Func<bool>)(() => view.IsRunning)));
            Assert.IsTrue((bool)view.Dispatcher.Invoke((Func<bool>)(() => InterpreterList.RegenerateCommand.CanExecute(view, list))));
            Assert.IsTrue(fact.IsCurrent);
            Assert.AreEqual(MockPythonInterpreterFactory.UpToDateReason, fact.GetIsCurrentReason(null));

            wnd.Dispatcher.Invoke((Action)(() => wnd.Close()));
        }

        #endregion
    }
}

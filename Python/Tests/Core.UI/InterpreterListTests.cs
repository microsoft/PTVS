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
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.InterpreterList;
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
        #region Tests requiring VS

        [TestMethod, Priority(0), TestCategory("InterpreterList")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void GetInstalledInterpreters() {
            var interps = InterpreterView.GetInterpreters().ToList();
            foreach (var ver in PythonPaths.Versions) {
                var expected = string.Format(CultureInfo.InvariantCulture, "{0};{1}", ver.Interpreter, ver.Version);
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

            dte.ExecuteCommand("View.PythonInterpreters");
            var list = new VisualStudioApp(dte).FindByAutomationId("PythonTools.InterpreterList");
            Assert.IsNotNull(list);

            var allNames = new HashSet<string>(service.Interpreters.Select(i => i.GetInterpreterDisplay()));
            var names = list.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "InterpreterName"));
            foreach (var obj in names.Cast<AutomationElement>()) {
                var name = (string)obj.GetCurrentPropertyValue(AutomationElement.NameProperty);
                Assert.IsTrue(allNames.Remove(name));
            }
            Assert.AreEqual(0, allNames.Count);
        }

        [TestMethod, Priority(0), TestCategory("InterpreterList")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ChangeDefaultInVS() {
            var dte = VsIdeTestHostContext.Dte;
            var model = (IComponentModel)VsIdeTestHostContext.ServiceProvider.GetService(typeof(SComponentModel));
            var service = model.GetService<IInterpreterOptionsService>();
            Assert.IsNotNull(service);

            var originalDefault = service.DefaultInterpreter;

            dte.ExecuteCommand("View.PythonInterpreters");
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
            var fake = configurable.SetOptions(Guid.NewGuid(),
                new Dictionary<string, object> {
                    { "InterpreterPath", @"C:\Path\That\Probably\Does\Not\Exist" },
                    { "WindowsInterpreterPath", "" },
                    { "Architecture", ProcessorArchitecture.None },
                    { "Version", new Version(2, 7) },
                    { "PathEnvironmentVariable", "PYTHONPATH" },
                    { "Description", "Invalid" }
                }
            );

            var dte = VsIdeTestHostContext.Dte;
            dte.ExecuteCommand("View.PythonInterpreters");
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

            var fact = new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 1", new MockInterpreterConfiguration(PythonPaths.Versions.First().Path));
            var identifier = string.Format(
                CultureInfo.InvariantCulture,
                "{0};{1}",
                fact.Id,
                fact.GetLanguageVersion());

            var oldProviders = ((InterpreterOptionsService)service).SetProviders(new[] { new MockPythonInterpreterFactoryProvider("Test Provider 1", fact) });
            try {
                Assert.AreEqual(1, service.Interpreters.Count());

                dte.ExecuteCommand("View.PythonInterpreters");
                var list = new TestUtilities.UI.AutomationWrapper(new VisualStudioApp(dte).FindByAutomationId("PythonTools.InterpreterList"));
                Assert.IsNotNull(list);

                var rowCount = (int)list.Element.GetCurrentPropertyValue(GridPattern.RowCountProperty);
                Assert.AreEqual(service.Interpreters.Count(), rowCount);

                var window = PythonToolsPackage.Instance.FindWindowPane(typeof(InterpreterListToolWindow), 0, true) as WindowPane;
                var interpreterList = window.Content as InterpreterList;

                var label = list.FindByAutomationId("InterpreterName");
                var name = (string)label.GetCurrentPropertyValue(AutomationElement.NameProperty);
                Assert.AreEqual(fact.GetInterpreterDisplay(), name);

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
                Thread.Sleep(500);

                Assert.IsTrue((bool)required.GetCurrentPropertyValue(AutomationElement.IsOffscreenProperty));
                Assert.IsTrue((bool)button.GetCurrentPropertyValue(AutomationElement.IsOffscreenProperty));
                Assert.IsFalse((bool)progress.GetCurrentPropertyValue(AutomationElement.IsOffscreenProperty));

                fact.EndGenerateCompletionDatabase(identifier, false);
                interpreterList.Dispatcher.Invoke(CommandManager.InvalidateRequerySuggested);
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
                interpreterList.Dispatcher.Invoke(CommandManager.InvalidateRequerySuggested);
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
            var fake = configurable.SetOptions(Guid.NewGuid(),
                new Dictionary<string, object> {
                    { "InterpreterPath", @"C:\Path\That\Probably\Does\Not\Exist" },
                    { "WindowsInterpreterPath", "" },
                    { "Architecture", ProcessorArchitecture.None },
                    { "Version", new Version(2, 7) },
                    { "PathEnvironmentVariable", "PYTHONPATH" },
                    { "Description", "Invalid" }
                }
            );

            try {
                // Not crashing is sufficient to ensure that
                // https://pytools.codeplex.com/workitem/1199 is fixed.
                var withDb = (IInterpreterWithCompletionDatabase)fake;
                withDb.GenerateCompletionDatabase(GenerateDatabaseOptions.StdLibDatabase | GenerateDatabaseOptions.BuiltinDatabase, () => { });
            } finally {
                configurable.RemoveInterpreter(fake.Id);
            }
        }

        #endregion

        #region Tests not requiring VS

        [TestMethod, Priority(0), TestCategory("InterpreterListNonUI")]
        public void HasInterpreters() {
            var mockService = new MockInterpreterOptionsService();
            mockService.AddProvider(new MockPythonInterpreterFactoryProvider("Test Provider 1",
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 1", new MockInterpreterConfiguration(new Version(2, 7))),
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 2", new MockInterpreterConfiguration(new Version(3, 0))),
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 3", new MockInterpreterConfiguration(new Version(3, 3)))
            ));
            mockService.AddProvider(new MockPythonInterpreterFactoryProvider("Test Provider 2",
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 4", new MockInterpreterConfiguration(new Version(2, 7))),
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 5", new MockInterpreterConfiguration(new Version(3, 0))),
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 6", new MockInterpreterConfiguration(new Version(3, 3)))
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
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 1", new MockInterpreterConfiguration(new Version(2, 7))),
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 2", new MockInterpreterConfiguration(new Version(3, 0))),
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 3", new MockInterpreterConfiguration(new Version(3, 3)))
            ));

            Assert.AreEqual(3, list.Interpreters.Count);

            mockService.AddProvider(new MockPythonInterpreterFactoryProvider("Test Provider 2",
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 4", new MockInterpreterConfiguration(new Version(2, 7))),
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 5", new MockInterpreterConfiguration(new Version(3, 0))),
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 6", new MockInterpreterConfiguration(new Version(3, 3)))
            ));

            Assert.AreEqual(6, list.Interpreters.Count);
        }

        [TestMethod, Priority(0), TestCategory("InterpreterListNonUI")]
        public void AddFactories() {
            var mockService = new MockInterpreterOptionsService();
            var list = new InterpreterList(mockService);

            Assert.AreEqual(0, list.Interpreters.Count);

            var provider = new MockPythonInterpreterFactoryProvider("Test Provider 1",
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 1", new MockInterpreterConfiguration(new Version(2, 7))),
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 2", new MockInterpreterConfiguration(new Version(3, 0))),
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 3", new MockInterpreterConfiguration(new Version(3, 3)))
            );

            mockService.AddProvider(provider);

            Assert.AreEqual(3, list.Interpreters.Count);
            provider.AddFactory(new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 4", new MockInterpreterConfiguration(new Version(2, 7))));
            Assert.AreEqual(4, list.Interpreters.Count);
            provider.AddFactory(new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 5", new MockInterpreterConfiguration(new Version(3, 0))));
            Assert.AreEqual(5, list.Interpreters.Count);
            provider.AddFactory(new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 6", new MockInterpreterConfiguration(new Version(3, 3))));
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
                var fact = new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory", new MockInterpreterConfiguration(invalidPath));
                var view = new InterpreterView(fact, fact.Description, false);
                Assert.IsFalse(view.CanRefresh);
            }
        }

        [TestMethod, Priority(0), TestCategory("InterpreterListNonUI")]
        public void FactoryWithValidPath() {
            foreach (string validPath in PythonPaths.Versions.Select(pv => pv.Path)) {
                var fact = new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory", new MockInterpreterConfiguration(validPath));
                var view = new InterpreterView(fact, fact.Description, false);
                Assert.IsTrue(view.CanRefresh);
            }
        }

        [TestMethod, Priority(0), TestCategory("InterpreterListNonUI")]
        public void RefreshDBButton() {
            var mockService = new MockInterpreterOptionsService();
            var fact = new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 1", new MockInterpreterConfiguration(new Version(2, 7)));
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
            fact = new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 2", new MockInterpreterConfiguration(PythonPaths.Versions.First().Path));
            provider.AddFactory(fact);
            Assert.AreEqual("Test Factory 2", ((InterpreterView)listView.Items[0]).Interpreter.Description);

            Assert.IsTrue(list.Interpreters[0].CanRefresh);
            Assert.IsTrue(InterpreterList.RegenerateCommand.CanExecute(list.Interpreters[0], list));

            wnd.Close();
        }

        [TestMethod, Priority(0), TestCategory("InterpreterListNonUI")]
        public void RefreshDBStates() {
            var mockService = new MockInterpreterOptionsService();
            var fact = new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 1", new MockInterpreterConfiguration(PythonPaths.Versions.First().Path));
            mockService.AddProvider(new MockPythonInterpreterFactoryProvider("Test Provider 1", fact));

            var list = new InterpreterList(mockService);
            var view = list.Interpreters[0];

            var wnd = new Window() { Content = list };
            wnd.ShowInTaskbar = false;
            wnd.WindowState = WindowState.Minimized;
            wnd.Show();

            Assert.IsTrue(list.Interpreters[0].CanRefresh);
            Assert.IsTrue(InterpreterList.RegenerateCommand.CanExecute(list.Interpreters[0], list));
            Assert.IsFalse(fact.IsCurrent);
            Assert.AreEqual(MockPythonInterpreterFactory.NoDatabaseReason, fact.GetIsCurrentReason(null));

            InterpreterList.RegenerateCommand.Execute(list.Interpreters[0], list);

            Assert.IsFalse(InterpreterList.RegenerateCommand.CanExecute(list.Interpreters[0], list));
            Assert.IsFalse(fact.IsCurrent);
            Assert.AreEqual(MockPythonInterpreterFactory.GeneratingReason, fact.GetIsCurrentReason(null));

            fact.EndGenerateCompletionDatabase(list, view.Identifier, false);

            Assert.IsTrue(InterpreterList.RegenerateCommand.CanExecute(list.Interpreters[0], list));
            Assert.IsFalse(fact.IsCurrent);
            Assert.AreEqual(MockPythonInterpreterFactory.MissingModulesReason, fact.GetIsCurrentReason(null));

            InterpreterList.RegenerateCommand.Execute(list.Interpreters[0], list);

            Assert.IsFalse(InterpreterList.RegenerateCommand.CanExecute(list.Interpreters[0], list));
            Assert.IsFalse(fact.IsCurrent);
            Assert.AreEqual(MockPythonInterpreterFactory.GeneratingReason, fact.GetIsCurrentReason(null));

            fact.EndGenerateCompletionDatabase(list, view.Identifier, true);

            Assert.IsTrue(InterpreterList.RegenerateCommand.CanExecute(list.Interpreters[0], list));
            Assert.IsTrue(fact.IsCurrent);
            Assert.AreEqual(MockPythonInterpreterFactory.UpToDateReason, fact.GetIsCurrentReason(null));

            wnd.Close();
        }

        #endregion
    }
}

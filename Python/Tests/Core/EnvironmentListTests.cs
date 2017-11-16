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
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.EnvironmentsList;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using TestUtilities;
using TestUtilities.Mocks;
using TestUtilities.Python;

namespace PythonToolsUITests {
    [TestClass]
    public class EnvironmentListTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        private static readonly List<string> DeleteFolder = new List<string>();

        [ClassCleanup]
        public static void DoCleanup() {
            foreach (var folder in DeleteFolder) {
                FileUtils.DeleteDirectory(folder);
            }
        }


        private static InterpreterConfiguration MockInterpreterConfiguration(string description, Version version, InterpreterUIMode uiMode) {
            return new InterpreterConfiguration(
                $"Mock|{Guid.NewGuid()}",
                description,
                // Path doesn't matter, as long as it exists
                PythonPaths.Versions.FirstOrDefault()?.PrefixPath,
                PythonPaths.Versions.FirstOrDefault()?.InterpreterPath,
                null,
                null,
                InterpreterArchitecture.Unknown,
                version,
                uiMode
            );
        }

        private static InterpreterConfiguration MockInterpreterConfiguration(string description, Version version) {
            return MockInterpreterConfiguration(description, version, InterpreterUIMode.Normal);
        }

        private static InterpreterConfiguration MockInterpreterConfiguration(string path) {
            return new InterpreterConfiguration($"Mock|{path}", path, Path.GetDirectoryName(path), path, "", "", InterpreterArchitecture.Unknown, new Version(2, 7));
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public void HasInterpreters() {
            var sp = new MockServiceProvider();
            var mockService = new MockInterpreterOptionsService();
            mockService.AddProvider(new MockPythonInterpreterFactoryProvider("Test Provider 1",
                new MockPythonInterpreterFactory(MockInterpreterConfiguration("Test Factory 1", new Version(2, 7))),
                new MockPythonInterpreterFactory(MockInterpreterConfiguration("Test Factory 2", new Version(3, 0))),
                new MockPythonInterpreterFactory(MockInterpreterConfiguration("Test Factory 3", new Version(3, 3)))
            ));
            mockService.AddProvider(new MockPythonInterpreterFactoryProvider("Test Provider 2",
                new MockPythonInterpreterFactory(MockInterpreterConfiguration("Test Factory 4", new Version(2, 7))),
                new MockPythonInterpreterFactory(MockInterpreterConfiguration("Test Factory 5", new Version(3, 0))),
                new MockPythonInterpreterFactory(MockInterpreterConfiguration("Test Factory 6", new Version(3, 3))),
                new MockPythonInterpreterFactory(MockInterpreterConfiguration("Test Factory 7", new Version(3, 3), InterpreterUIMode.Hidden))
            ));

            using (var wpf = new WpfProxy())
            using (var list = new EnvironmentListProxy(wpf)) {
                list.Service = mockService;
                list.Interpreters = mockService;
                var environments = list.Environments;

                Assert.AreEqual(6, environments.Count);
                AssertUtil.ContainsExactly(
                    wpf.Invoke(() => environments.Select(ev => ev.Description).ToList()),
                    "Test Factory 1",
                    "Test Factory 2",
                    "Test Factory 3",
                    "Test Factory 4",
                    "Test Factory 5",
                    "Test Factory 6"
                );
            }
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public void InterpretersWithSameNames() {
            var sp = new MockServiceProvider();
            var mockService = new MockInterpreterOptionsService();

            var fact1 = new MockPythonInterpreterFactory(MockInterpreterConfiguration("Test Factory 1", new Version(2, 7)));
            fact1.Properties[PythonRegistrySearch.CompanyPropertyKey] = "Company 1";
            var fact2 = new MockPythonInterpreterFactory(MockInterpreterConfiguration("Test Factory 1", new Version(2, 7)));
            fact2.Properties[PythonRegistrySearch.CompanyPropertyKey] = "Company 2";

            // Deliberately add fact2 twice, as we should only show that once.
            mockService.AddProvider(new MockPythonInterpreterFactoryProvider("Test Provider 1", fact1, fact2, fact2));

            using (var wpf = new WpfProxy())
            using (var list = new EnvironmentListProxy(wpf)) {
                list.Service = mockService;
                list.Interpreters = mockService;
                var environments = list.Environments;

                Assert.AreEqual(2, environments.Count);
                AssertUtil.ArrayEquals(
                    wpf.Invoke(() => environments.Select(ev => ev.Description).ToList()),
                    new[] { "Test Factory 1", "Test Factory 1" }
                );
                AssertUtil.ContainsExactly(
                    wpf.Invoke(() => environments.Select(ev => ev.Company).ToList()),
                    "Company 1",
                    "Company 2"
                );
            }
        }

        [TestMethod, Priority(2)]
        [TestCategory("10s")]
        public async Task InterpretersRaceCondition() {
            var container = CreateCompositionContainer();
            var service = container.GetExportedValue<IInterpreterOptionsService>();
            var interpreters = container.GetExportedValue<IInterpreterRegistryService>();
            var factories = Enumerable.Repeat(0, 5).Select(
                i => new MockPythonInterpreterFactory(
                    MockInterpreterConfiguration(string.Format("Test Factory {0}", i), new Version(2, 7))
                )
            ).ToList();
            var provider = new MockPythonInterpreterFactoryProvider("Test Provider", factories.ToArray());
            ((InterpreterRegistryService)interpreters).SetProviders(new[] {
                new Lazy<IPythonInterpreterFactoryProvider, Dictionary<string, object>>(
                    () => provider,
                    new Dictionary<string, object>() {
                        { "InterpreterFactoryId", "Mock" }
                    }
                )
            });

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var ct = cts.Token;
            ExceptionDispatchInfo edi = null;

            EventHandler interpretersChanged = (s, e) => {
                Task.Run(() => {
                    try {
                        foreach (var f in factories) {
                            Thread.Sleep(1);
                            ct.ThrowIfCancellationRequested();
                            provider.AddFactory(f);
                        }

                        ct.ThrowIfCancellationRequested();
                        var interpretersList = interpreters.Interpreters.ToList();
                        Trace.TraceInformation("Got {0} interpreters", interpretersList.Count);
                    } catch (OperationCanceledException) {
                    } catch (Exception ex) {
                        edi = ExceptionDispatchInfo.Capture(ex);
                    }
                });
            };
            interpreters.InterpretersChanged += interpretersChanged;

            var t1 = Task.Run(() => {
                while (!ct.IsCancellationRequested) {
                    provider.AddFactory(factories.First());
                    Thread.Sleep(50);
                    if (edi != null) {
                        edi.Throw();
                    }
                    provider.RemoveAllFactories();
                }
            }, ct);
            var t2 = Task.Run(() => {
                try {
                    while (!ct.IsCancellationRequested) {
                        var interpretersList = interpreters.InterpretersOrDefault.ToList();
                        Trace.TraceInformation("Got {0} interpreters or default", interpretersList.Count);
                        Thread.Sleep(10);
                    }
                } finally {
                    cts.Cancel();
                }
            }, ct);

            try {
                await t1;
            } catch (OperationCanceledException) {
            } finally {
                cts.Cancel();
            }
            try {
                await t2;
            } catch (OperationCanceledException) {
            } finally {
                interpreters.InterpretersChanged -= interpretersChanged;
            }
        }

        [TestMethod, Priority(0)]
        public void NonDefaultInterpreter() {
            var mockProvider = new MockPythonInterpreterFactoryProvider("Test Provider 1",
                new MockPythonInterpreterFactory(MockInterpreterConfiguration("Test Factory 1", new Version(2, 7))),
                new MockPythonInterpreterFactory(MockInterpreterConfiguration("Test Factory 2", new Version(3, 0), InterpreterUIMode.CannotBeDefault)),
                new MockPythonInterpreterFactory(MockInterpreterConfiguration("Test Factory 3", new Version(3, 3), InterpreterUIMode.CannotBeAutoDefault))
            );

            using (var wpf = new WpfProxy())
            using (var list = new EnvironmentListProxy(wpf)) {
                var container = CreateCompositionContainer();
                var service = container.GetExportedValue<IInterpreterOptionsService>();
                var interpreters = container.GetExportedValue<IInterpreterRegistryService>();
                var oldDefault = service.DefaultInterpreter;
                var oldProviders = ((InterpreterRegistryService)interpreters).SetProviders(new[] {
                    new Lazy<IPythonInterpreterFactoryProvider, Dictionary<string, object>>(
                        () => mockProvider,
                        new Dictionary<string, object>() {
                            { "InterpreterFactoryId", "Mock" }
                        }
                    )
                });
                try {
                    list.Service = service;
                    list.Interpreters = interpreters;
                    var environments = list.Environments;

                    AssertUtil.AreEqual(
                        wpf.Invoke(() => environments.Select(ev => ev.Description).ToList()),
                        "Test Factory 1",
                        "Test Factory 2",
                        "Test Factory 3"
                    );
                    // TF 1 and 3 can be set as default
                    AssertUtil.AreEqual(
                        wpf.Invoke(() => environments.Select(ev => ev.CanBeDefault).ToList()),
                        true, false, true
                    );
                } finally {
                    ((InterpreterRegistryService)interpreters).SetProviders(oldProviders);
                    service.DefaultInterpreter = oldDefault;
                }
            }
        }

        [TestMethod, Priority(0)]
        public void AddFactories() {
            var mockService = new MockInterpreterOptionsService();
            using (var wpf = new WpfProxy())
            using (var list = wpf.Invoke(() => new EnvironmentListProxy(wpf))) {
                var provider = new MockPythonInterpreterFactoryProvider("Test Provider 1",
                    new MockPythonInterpreterFactory(MockInterpreterConfiguration("Test Factory 1", new Version(2, 7))),
                    new MockPythonInterpreterFactory(MockInterpreterConfiguration("Test Factory 2", new Version(3, 0))),
                    new MockPythonInterpreterFactory(MockInterpreterConfiguration("Test Factory 3", new Version(3, 3)))
                );

                list.Service = mockService;
                list.Interpreters = mockService;

                Assert.AreEqual(0, list.Environments.Count);

                mockService.AddProvider(provider);
                Assert.AreEqual(3, list.Environments.Count);
                provider.AddFactory(new MockPythonInterpreterFactory(MockInterpreterConfiguration("Test Factory 4", new Version(2, 7))));
                Assert.AreEqual(4, list.Environments.Count);
                provider.AddFactory(new MockPythonInterpreterFactory(MockInterpreterConfiguration("Test Factory 5", new Version(3, 0))));
                Assert.AreEqual(5, list.Environments.Count);
                provider.AddFactory(new MockPythonInterpreterFactory(MockInterpreterConfiguration("Test Factory 6", new Version(3, 3))));
                Assert.AreEqual(6, list.Environments.Count);
            }
        }

        [TestMethod, Priority(0)]
        public void FactoryWithInvalidPath() {
            using (var wpf = new WpfProxy())
            using (var list = new EnvironmentListProxy(wpf)) {
                var service = new MockInterpreterOptionsService();
                var provider = new MockPythonInterpreterFactoryProvider("Test Provider");
                service.AddProvider(provider);
                list.Service = service;
                list.Interpreters = service;

                foreach (string invalidPath in new string[] {
                    null,
                    "",
                    "NOT A REAL PATH",
                    "*\\?\\\"\\^"
                }) {
                    Console.WriteLine("Path: <{0}>", invalidPath ?? "(null)");
                    provider.RemoveAllFactories();
                    provider.AddFactory(new MockPythonInterpreterFactory(
                        new InterpreterConfiguration(
                            "Mock;" + Guid.NewGuid().ToString(),
                            "Test Factory",
                            invalidPath,
                            invalidPath,
                            "",
                            "",
                            InterpreterArchitecture.Unknown,
                            new Version(2, 7)
                        )
                    ));
                    var view = list.Environments.Single();
                    Assert.IsFalse(
                        list.CanExecute(DBExtension.StartRefreshDB, view),
                        string.Format("Should not be able to refresh DB for {0}", invalidPath)
                    );
                }
            }
        }

        [TestMethod, Priority(0)]
        public void FactoryWithValidPath() {
            using (var wpf = new WpfProxy())
            using (var list = new EnvironmentListProxy(wpf)) {
                var service = new MockInterpreterOptionsService();
                var provider = new MockPythonInterpreterFactoryProvider("Test Provider");
                service.AddProvider(provider);
                list.Service = service;
                list.Interpreters = service;

                foreach (var version in PythonPaths.Versions) {
                    Console.WriteLine("Path: <{0}>", version.InterpreterPath);
                    provider.RemoveAllFactories();
                    provider.AddFactory(new MockPythonInterpreterFactory(
                        version.Configuration
                    ));
                    var view = list.Environments.Single();
                    Assert.IsTrue(
                        list.CanExecute(DBExtension.StartRefreshDB, view),
                        string.Format("Cannot refresh DB for {0}", version.InterpreterPath)
                    );
                }
            }
        }

        [TestMethod, Priority(0)]
        public void RefreshDBStates() {
            using (var fact = new MockPythonInterpreterFactory(
                MockInterpreterConfiguration(
                    PythonPaths.Versions.First().InterpreterPath
                ),
                true
            ))
            using (var wpf = new WpfProxy())
            using (var list = new EnvironmentListProxy(wpf)) {
                list.CreateDBExtension = true;

                var mockService = new MockInterpreterOptionsService();
                mockService.AddProvider(new MockPythonInterpreterFactoryProvider("Test Provider 1", fact));
                list.Service = mockService;
                list.Interpreters = mockService;
                var view = list.Environments.Single();

                Assert.IsFalse(wpf.Invoke(() => view.IsRefreshingDB));
                Assert.IsTrue(list.CanExecute(DBExtension.StartRefreshDB, view));
                Assert.IsFalse(fact.IsCurrent);
                Assert.AreEqual(MockPythonInterpreterFactory.NoDatabaseReason, fact.GetIsCurrentReason(null));

                list.Execute(DBExtension.StartRefreshDB, view, CancellationTokens.After15s).GetAwaiter().GetResult();
                for (int retries = 10; retries > 0 && !wpf.Invoke(() => view.IsRefreshingDB); --retries) {
                    Thread.Sleep(200);
                }

                Assert.IsTrue(wpf.Invoke(() => view.IsRefreshingDB));
                Assert.IsFalse(list.CanExecute(DBExtension.StartRefreshDB, view));
                Assert.IsFalse(fact.IsCurrent);
                Assert.AreEqual(MockPythonInterpreterFactory.GeneratingReason, fact.GetIsCurrentReason(null));

                fact.EndGenerateCompletionDatabase(AnalyzerStatusUpdater.GetIdentifier(fact), false);
                for (int retries = 10; retries > 0 && wpf.Invoke(() => view.IsRefreshingDB); --retries) {
                    Thread.Sleep(1000);
                }

                Assert.IsFalse(wpf.Invoke(() => view.IsRefreshingDB));
                Assert.IsTrue(list.CanExecute(DBExtension.StartRefreshDB, view));
                Assert.IsFalse(fact.IsCurrent);
                Assert.AreEqual(MockPythonInterpreterFactory.MissingModulesReason, fact.GetIsCurrentReason(null));

                list.Execute(DBExtension.StartRefreshDB, view, CancellationTokens.After15s).GetAwaiter().GetResult();

                Assert.IsTrue(wpf.Invoke(() => view.IsRefreshingDB));
                Assert.IsFalse(list.CanExecute(DBExtension.StartRefreshDB, view));
                Assert.IsFalse(fact.IsCurrent);
                Assert.AreEqual(MockPythonInterpreterFactory.GeneratingReason, fact.GetIsCurrentReason(null));

                fact.EndGenerateCompletionDatabase(AnalyzerStatusUpdater.GetIdentifier(fact), true);
                for (int retries = 10; retries > 0 && wpf.Invoke(() => view.IsRefreshingDB); --retries) {
                    Thread.Sleep(1000);
                }

                Assert.IsFalse(wpf.Invoke(() => view.IsRefreshingDB));
                Assert.IsTrue(list.CanExecute(DBExtension.StartRefreshDB, view));
                Assert.IsTrue(fact.IsCurrent);
                Assert.AreEqual(MockPythonInterpreterFactory.UpToDateReason, fact.GetIsCurrentReason(null));
                Assert.AreEqual(MockPythonInterpreterFactory.UpToDateReason, fact.GetIsCurrentReason(null));
            }
        }


        [TestMethod, Priority(0)]
        public void InstalledFactories() {
            using (var wpf = new WpfProxy())
            using (var list = new EnvironmentListProxy(wpf)) {
                var container = CreateCompositionContainer();
                var service = container.GetExportedValue<IInterpreterOptionsService>();
                var interpreters = container.GetExportedValue<IInterpreterRegistryService>();
                list.Service = service;
                list.Interpreters = interpreters;

                var expected = new HashSet<string>(
                    PythonPaths.Versions
                        .Where(v => !v.IsIronPython)
                        .Select(v => v.InterpreterPath),
                    StringComparer.OrdinalIgnoreCase
                );
                var actual = wpf.Invoke(() => new HashSet<string>(
                    list.Environments
                        .Where(ev => ev.Factory.Configuration.Id.StartsWith("Global|PythonCore|"))
                        .Select(ev => ev.InterpreterPath),
                    StringComparer.OrdinalIgnoreCase
                ));

                Console.WriteLine("Expected - Actual: " + string.Join(", ", expected.Except(actual).OrderBy(s => s)));
                Console.WriteLine("Actual - Expected: " + string.Join(", ", actual.Except(expected).OrderBy(s => s)));

                AssertUtil.ContainsExactly(
                    actual,
                    expected
                );
            }
        }

        [TestMethod, Priority(0)]
        public void AddUpdateRemoveConfigurableFactory() {
            using (var wpf = new WpfProxy())
            using (var list = new EnvironmentListProxy(wpf)) {
                var container = CreateCompositionContainer();
                var service = container.GetExportedValue<IInterpreterOptionsService>();
                var interpreters = container.GetExportedValue<IInterpreterRegistryService>();
                list.Service = service;
                list.Interpreters = interpreters;

                var before = wpf.Invoke(() => new HashSet<string>(
                    list.Environments.Select(ev => (string)ev.InterpreterPath),
                    StringComparer.OrdinalIgnoreCase
                ));

                var id = Guid.NewGuid().ToString();
                string fact;

                using (new AssertInterpretersChanged(interpreters, TimeSpan.FromSeconds(5))) {
                    try {
                        fact = list.Service.AddConfigurableInterpreter(
                            id,
                            new InterpreterConfiguration(
                                "",
                                "Blah",
                                "",
                                TestData.GetPath("HelloWorld\\HelloWorld.pyproj")
                            )
                        );
                    } catch (Exception ex) when (!ex.IsCriticalException()) {
                        Registry.CurrentUser.DeleteSubKeyTree("Software\\Python\\VisualStudio\\" + id);
                        throw;
                    }
                }


                try {
                    var afterAdd = wpf.Invoke(() => new HashSet<string>(
                        list.Environments.Select(ev => (string)ev.InterpreterPath),
                        StringComparer.OrdinalIgnoreCase
                    ));

                    Assert.AreNotEqual(before.Count, afterAdd.Count, "Did not add a new environment");
                    AssertUtil.ContainsExactly(
                        afterAdd.Except(before),
                        TestData.GetPath("HelloWorld\\HelloWorld.pyproj")
                    );

                    using (new AssertInterpretersChanged(interpreters, TimeSpan.FromSeconds(5))) {
                        list.Service.AddConfigurableInterpreter(
                            id,
                            new InterpreterConfiguration(
                                "",
                                "test",
                                "",
                                TestData.GetPath("HelloWorld2\\HelloWorld.pyproj")
                            )
                        );
                    }

                    var afterUpdate = wpf.Invoke(() => new HashSet<string>(
                        list.Environments.Select(ev => (string)ev.InterpreterPath),
                        StringComparer.OrdinalIgnoreCase
                    ));

                    Assert.AreEqual(afterAdd.Count, afterUpdate.Count, "Should not add/remove an environment");
                    AssertUtil.ContainsExactly(
                        afterUpdate.Except(before),
                        TestData.GetPath("HelloWorld2\\HelloWorld.pyproj")
                    );
                } finally {
                    list.Service.RemoveConfigurableInterpreter(fact);
                }

                var afterRemove = wpf.Invoke(() => new HashSet<string>(
                    list.Environments.Select(ev => (string)ev.InterpreterPath),
                    StringComparer.OrdinalIgnoreCase
                ));
                AssertUtil.ContainsExactly(
                    afterRemove,
                    before
                );
            }
        }

        private static async Task AddCustomEnvironment(EnvironmentListProxy list) {

            var view = list.AddNewEnvironmentView;
            var extView = view.Extensions.OfType<ConfigurationExtensionProvider>().First().WpfObject;

            // Extension is not sited in the tool window, so we need
            // to set the DataContext in order to get the Subcontext
            extView.DataContext = view;
            var confView = (ConfigurationEnvironmentView)((System.Windows.Controls.Grid)extView.FindName("Subcontext")).DataContext;

            confView.Description = "Test Environment";
            confView.PrefixPath = @"C:\Test";
            confView.InterpreterPath = @"C:\Test\python.exe";
            confView.WindowsInterpreterPath = @"C:\Test\pythonw.exe";
            confView.VersionName = "3.5";
            confView.ArchitectureName = "32-bit";

            var origCount = list.Environments.Count;

            ((RoutedCommand)ConfigurationExtension.Apply).Execute(confView, extView);

            while (list.Environments.Count == origCount) {
                await Task.Delay(10);
            }
        }

        [TestMethod, Priority(0)]
        [TestCategory("60s")]
        public async Task AddUpdateRemoveConfigurableFactoryThroughUI() {
            using (var wpf = new WpfProxy())
            using (var list = new EnvironmentListProxy(wpf)) {
                string newId = null;
                var container = CreateCompositionContainer();
                var service = container.GetExportedValue<IInterpreterOptionsService>();
                var interpreters = container.GetExportedValue<IInterpreterRegistryService>();
                list.Service = service;
                list.Interpreters = interpreters;

                var before = wpf.Invoke(() => new HashSet<string>(
                    list.Environments.Where(ev => ev.Factory != null).Select(ev => ev.Factory.Configuration.Id)));

                try {
                    wpf.Invoke(() => AddCustomEnvironment(list)).Wait(10000);

                    var afterAdd = wpf.Invoke(() => new HashSet<string>(list.Environments.Where(ev => ev.Factory != null).Select(ev => ev.Factory.Configuration.Id)));
                    var difference = new HashSet<string>(afterAdd);
                    difference.ExceptWith(before);

                    Console.WriteLine("Added {0}", AssertUtil.MakeText(difference));
                    Assert.AreEqual(1, difference.Count, "Did not add a new environment");
                    var newEnv = interpreters.Interpreters.Single(f => difference.Contains(f.Configuration.Id));
                    newId = newEnv.Configuration.Id;

                    Assert.IsTrue(list.Service.IsConfigurable(newEnv.Configuration.Id), "Did not add a configurable environment");

                    // To remove the environment, we need to trigger the Remove
                    // command on the ConfigurationExtensionProvider's control
                    var view = wpf.Invoke(() => list.Environments.First(ev => ev.Factory.Configuration.Id == newId));
                    var extView = wpf.Invoke(() => view.Extensions.OfType<ConfigurationExtensionProvider>().First().WpfObject);
                    var confView = wpf.Invoke(() => {
                        // Extension is not sited in the tool window, so we need
                        // to set the DataContext in order to get the Subcontext
                        extView.DataContext = view;
                        return (ConfigurationEnvironmentView)((System.Windows.Controls.Grid)extView.FindName("Subcontext")).DataContext;
                    });
                    await wpf.Execute((RoutedCommand)ConfigurationExtension.Remove, extView, confView, CancellationTokens.After15s);
                    await Task.Delay(500);

                    var afterRemove = wpf.Invoke(() => new HashSet<string>(list.Environments.Where(ev => ev.Factory != null).Select(ev => ev.Factory.Configuration.Id)));
                    AssertUtil.ContainsExactly(afterRemove, before);
                } finally {
                    // Just in case, we want to clean up the registration
                    if (newId != null) {
                        string company, tag;
                        if (CPythonInterpreterFactoryConstants.TryParseInterpreterId(newId, out company, out tag) &&
                            company == "VisualStudio") {
                            Registry.CurrentUser.DeleteSubKeyTree("Software\\Python\\VisualStudio\\" + tag, false);
                        }
                    }
                }
            }
        }

        [TestMethod, Priority(0)]
        public void ChangeDefault() {
            var container = CreateCompositionContainer();
            var service = container.GetExportedValue<IInterpreterOptionsService>();
            var interpreters = container.GetExportedValue<IInterpreterRegistryService>();
            using (var defaultChanged = new AutoResetEvent(false))
            using (var wpf = new WpfProxy())
            using (var list = new EnvironmentListProxy(wpf)) {
                service.DefaultInterpreterChanged += (s, e) => { defaultChanged.SetIfNotDisposed(); };
                list.Service = service;
                list.Interpreters = interpreters;
                var originalDefault = service.DefaultInterpreter;
                try {
                    foreach (var interpreter in interpreters.Interpreters) {
                        var environment = list.Environments.FirstOrDefault(ev =>
                            ev.Factory == interpreter
                        );
                        Assert.IsNotNull(environment, string.Format("Did not find {0}", interpreter.Configuration.Description));

                        if (!list.CanExecute(EnvironmentView.MakeGlobalDefault, environment)) {
                            Console.WriteLine("Skipping {0} because it cannot be made the default", interpreter.Configuration.Id);
                            continue;
                        }
                        var before = service.DefaultInterpreter;
                        Console.WriteLine("Changing default from {0} to {1}", before.Configuration.Id, interpreter.Configuration.Id);
                        list.Execute(EnvironmentView.MakeGlobalDefault, environment, CancellationTokens.After15s);
                        Assert.IsTrue(defaultChanged.WaitOne(TimeSpan.FromSeconds(10.0)), "Setting default took too long");

                        Assert.AreEqual(interpreter, service.DefaultInterpreter,
                            string.Format(
                                "Failed to change default from {0} to {1}",
                                service.DefaultInterpreter.Configuration.Id,
                                interpreter.Configuration.Id
                        ));
                    }
                } finally {
                    service.DefaultInterpreter = originalDefault;
                }
            }
        }

        private static Task WaitForEvent(CancellationToken cancellationToken, Action<EventHandler> add, Action<EventHandler> remove) {
            var tcs = new TaskCompletionSource<object>();
            if (cancellationToken.CanBeCanceled) {
                cancellationToken.Register(() => tcs.TrySetCanceled());
            }
            EventHandler evt = (s, e) => { tcs.SetResult(null); };
            var mre = new ManualResetEventSlim();
            add(evt);
            tcs.Task.ContinueWith(t => {
                remove(evt);
            });
            return tcs.Task;
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public async Task PipExtension() {
            var service = MakeEmptyVEnv(usePipPackageManager: true);

            using (var wpf = new WpfProxy())
            using (var list = new EnvironmentListProxy(wpf)) {
                list.CreatePipExtension = true;
                list.Service = service;
                list.Interpreters = service;

                var environment = list.Environments.Single();
                var pip = list.GetExtensionOrAssert<PipExtensionProvider>(environment);

                // Allow the initial scan to complete
                var ppm = (PipPackageManager)pip._packageManager;
                await Task.Delay(500);
                await ppm._working.WaitAsync(1500);
                ppm._working.Release();

                Assert.AreEqual(false, pip.IsPipInstalled, "venv should not install pip");
                var task = wpf.Invoke(() => pip.InstallPip().ContinueWith(LogException));
                Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(120.0)), "pip install timed out");
                Assert.IsTrue(task.Result, "pip install failed");
                Assert.AreEqual(true, pip.IsPipInstalled, "pip was not installed");

                var packages = await pip.GetInstalledPackagesAsync();
                AssertUtil.ContainsAtLeast(packages.Select(pv => pv.Name), "pip", "setuptools");

                task = wpf.Invoke(() => pip.InstallPackage(new PackageSpec("ptvsd")).ContinueWith(LogException));
                Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(60.0)), "pip install ptvsd timed out");
                Assert.IsTrue(task.Result, "pip install ptvsd failed");
                packages = await pip.GetInstalledPackagesAsync();
                AssertUtil.ContainsAtLeast(packages.Select(pv => pv.Name), "ptvsd");

                task = wpf.Invoke(() => pip.UninstallPackage(new PackageSpec("ptvsd")).ContinueWith(LogException));
                Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(60.0)), "pip uninstall ptvsd timed out");
                Assert.IsTrue(task.Result, "pip uninstall ptvsd failed");
                packages = await pip.GetInstalledPackagesAsync();
                AssertUtil.DoesntContain(packages.Select(pv => pv.Name), "ptvsd");
            }
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public async Task CondaExtension() {
            var service = MakeEmptyCondaEnv(PythonLanguageVersion.V36);

            using (var wpf = new WpfProxy())
            using (var list = new EnvironmentListProxy(wpf)) {
                list.CreatePipExtension = true;
                list.Service = service;
                list.Interpreters = service;

                var environment = list.Environments.Single();
                var pip = list.GetExtensionOrAssert<PipExtensionProvider>(environment);

                // Allow the initial scan to complete
                var cpm = (CondaPackageManager)pip._packageManager;
                await Task.Delay(500);
                await cpm._working.WaitAsync(12500);
                cpm._working.Release();

                var packages = await pip.GetInstalledPackagesAsync();
                AssertUtil.ContainsAtLeast(packages.Select(pv => pv.Name), "pip", "python", "setuptools", "wheel");

                var task = wpf.Invoke(() => pip.InstallPackage(new PackageSpec("requests")).ContinueWith(LogException));
                Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(60.0)), "conda install resquests timed out");
                Assert.IsTrue(task.Result, "conda install requests failed");
                packages = await pip.GetInstalledPackagesAsync();
                AssertUtil.ContainsAtLeast(packages.Select(pv => pv.Name), "requests");

                task = wpf.Invoke(() => pip.UninstallPackage(new PackageSpec("requests")).ContinueWith(LogException));
                Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(60.0)), "conda uninstall requests timed out");
                Assert.IsTrue(task.Result, "conda uninstall requests failed");
                packages = await pip.GetInstalledPackagesAsync();
                AssertUtil.DoesntContain(packages.Select(pv => pv.Name), "requests");
            }
        }

        [TestMethod, Priority(0)]
        public async Task SaveLoadCache() {
            var cachePath = Path.Combine(TestData.GetTempPath(), "pip.cache");
            using (var cache = new TestPipPackageCache(cachePath)) {
                AssertUtil.ContainsExactly(await cache.TestGetAllPackageNamesAsync());

                await cache.TestInjectPackageAsync("azure==0.9", "azure description");
                await cache.TestInjectPackageAsync("ptvsd==1.0", "ptvsd description");

                AssertUtil.ContainsExactly(await cache.TestGetAllPackageSpecsAsync(),
                    "azure==0.9 #azure description",
                    "ptvsd==1.0 #ptvsd description"
                );

                await cache.TestWriteCacheToDiskAsync();
            }

            using (var cache = new TestPipPackageCache(cachePath)) {
                AssertUtil.ContainsExactly(await cache.TestGetAllPackageNamesAsync());

                await cache.TestReadCacheFromDiskAsync();

                AssertUtil.ContainsExactly(await cache.TestGetAllPackageSpecsAsync(),
                    "azure==0.9 #azure description",
                    "ptvsd==1.0 #ptvsd description"
                );

            }
        }

        [TestMethod, Priority(0)]
        public async Task UpdatePackageInfo() {
            var pm = new MockPackageManager();
            
            var pv = new PipPackageView(pm, PackageSpec.FromRequirement("ptvsd==0.9"), true);

            var changes = new List<string>();
            pv.PropertyChanged += (s, e) => { changes.Add(e.PropertyName); };
            var desc = pv.Description;
            var ver = pv.UpgradeVersion;

            AssertUtil.ContainsExactly(changes);
            Assert.AreNotEqual("ptvsd description", desc);
            Assert.IsTrue(ver.IsEmpty, "Expected empty version, not {0}".FormatInvariant(ver));

            pm.AddInstallable(new PackageSpec("ptvsd", "1.0") { Description = "ptvsd description" });
            var desc2 = pv.Description;

            await Task.Delay(100);

            AssertUtil.ContainsExactly(changes, "Description", "UpgradeVersion");
            Assert.AreNotEqual(desc, pv.Description);
            Assert.IsTrue(pv.UpgradeVersion.CompareTo(pv.Version) > 0,
                string.Format("Expected {0} > {1}", pv.UpgradeVersion, pv.Version)
            );
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public void VendorInfo() {
            var sp = new MockServiceProvider();
            var mockService = new MockInterpreterOptionsService();

            var v = new Version(3, 5);
            var noInfo = new MockPythonInterpreterFactory(MockInterpreterConfiguration("1 No Info", v));
            var vendor = new MockPythonInterpreterFactory(MockInterpreterConfiguration("2 Vendor", v));
            var supportUrl = new MockPythonInterpreterFactory(MockInterpreterConfiguration("3 SupportUrl", v));
            var bothInfo = new MockPythonInterpreterFactory(MockInterpreterConfiguration("4 Both Info", v));

            bothInfo.Properties[EnvironmentView.CompanyKey] = vendor.Properties[EnvironmentView.CompanyKey] = "Vendor Name";
            bothInfo.Properties[EnvironmentView.SupportUrlKey] = supportUrl.Properties[EnvironmentView.SupportUrlKey] = "http://example.com";

            mockService.AddProvider(new MockPythonInterpreterFactoryProvider("Test Provider", noInfo, vendor, supportUrl, bothInfo));

            using (var wpf = new WpfProxy())
            using (var list = new EnvironmentListProxy(wpf)) {
                list.Service = mockService;
                list.Interpreters = mockService;
                var environments = list.Environments;

                Assert.AreEqual(4, environments.Count);
                AssertUtil.AreEqual(
                    wpf.Invoke(() => environments.Select(ev => ev.Company).ToList()),
                    "",
                    "Vendor Name",
                    "",
                    "Vendor Name"
                );
                AssertUtil.AreEqual(
                    wpf.Invoke(() => environments.Select(ev => ev.SupportUrl).ToList()),
                    "",
                    "",
                    "http://example.com",
                    "http://example.com"
                );
            }
        }

        [TestMethod, Priority(0)]
        public void FileNameEllipsis() {
            TestFileNameEllipsis("C:\\Python\\python.exe", "C:\\", "Python", "\\python.exe");
            TestFileNameEllipsis("C:\\Python\\lib\\", "C:\\", "Python", "\\lib\\");
            TestFileNameEllipsis("C:\\python.exe", "C:", "", "\\python.exe");
            TestFileNameEllipsis("\\python.exe", "", "", "\\python.exe");
            TestFileNameEllipsis("python.exe", "", "", "python.exe");
            TestFileNameEllipsis("\\lib\\", "", "", "\\lib\\");
            TestFileNameEllipsis("lib\\", "", "", "lib\\");
            TestFileNameEllipsis("", "", "", "");
        }

        private static void TestFileNameEllipsis(string path, string head, string body, string tail) {
            var h = (string)new FileNameEllipsisConverter { IncludeHead = true }.Convert(path, typeof(string), null, null);
            var b = (string)new FileNameEllipsisConverter { IncludeBody = true }.Convert(path, typeof(string), null, null);
            var t = (string)new FileNameEllipsisConverter { IncludeTail = true }.Convert(path, typeof(string), null, null);

            Assert.AreEqual(head + "|" + body + "|" + tail, h + "|" + body + "|" + tail);
        }

        #region Test Helpers

        private static bool LogException(Task task) {
            var ex = task.Exception;
            if (ex != null) {
                Console.WriteLine(ex.InnerException ?? ex);
                return false;
            }
            return true;
        }

        private MockInterpreterOptionsService MakeEmptyVEnv(bool usePipPackageManager = false) {
            var python = PythonPaths.Versions.FirstOrDefault(p =>
                p.IsCPython && Directory.Exists(Path.Combine(p.PrefixPath, "Lib", "venv"))
            );
            if (python == null) {
                Assert.Inconclusive("Requires Python with venv");
            }

            var env = TestData.GetTempPath();
            if (env.Length > 140) {
                env = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                DeleteFolder.Add(env);
            }
            using (var proc = ProcessOutput.RunHiddenAndCapture(
                python.InterpreterPath, "-m", "venv", env, "--clear"
            )) {
                Console.WriteLine(proc.Arguments);
                proc.Wait();
                foreach (var line in proc.StandardOutputLines.Concat(proc.StandardErrorLines)) {
                    Console.WriteLine(line);
                }
                Assert.AreEqual(0, proc.ExitCode ?? -1, "Failed to create venv");
            }

            // Forcibly remove pip so we can reinstall it
            foreach (var dir in Directory.EnumerateDirectories(Path.Combine(env, "lib", "site-packages"))) {
                Directory.Delete(dir, true);
            }

            var service = new MockInterpreterOptionsService();
            var provider = new MockPythonInterpreterFactoryProvider("VEnv Provider");
            var factory = new MockPythonInterpreterFactory(
                new InterpreterConfiguration(
                    "Mock;" + Guid.NewGuid().ToString(),
                    Path.GetFileName(PathUtils.TrimEndSeparator(env)),
                    env,
                    PathUtils.FindFile(env, "python.exe"),
                    PathUtils.FindFile(env, "python.exe"),
                    "PYTHONPATH",
                    python.Architecture,
                    python.Version.ToVersion()
                )
            );
            if (usePipPackageManager) {
                factory.PackageManager = new PipPackageManager(false);
                factory.PackageManager.SetInterpreterFactory(factory);
            }
            provider.AddFactory(factory);
            service.AddProvider(provider);
            return service;
        }

        private MockInterpreterOptionsService MakeEmptyCondaEnv(PythonLanguageVersion version) {
            var python = PythonPaths.AnacondaVersions.FirstOrDefault(p =>
                p.IsCPython && File.Exists(Path.Combine(p.PrefixPath, "scripts", "conda.exe"))
            );
            if (python == null) {
                Assert.Inconclusive("Requires Anaconda or Miniconda");
            }

            var env = TestData.GetTempPath();
            if (env.Length > 140) {
                env = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                DeleteFolder.Add(env);
            }
            using (var proc = ProcessOutput.RunHiddenAndCapture(
                Path.Combine(python.PrefixPath, "scripts", "conda.exe"),
                "create",
                "-p",
                env,
                "python=={0}".FormatInvariant(version.ToVersion().ToString()),
                "-y"
            )) {
                Console.WriteLine(proc.Arguments);
                proc.Wait();
                foreach (var line in proc.StandardOutputLines.Concat(proc.StandardErrorLines)) {
                    Console.WriteLine(line);
                }
                Assert.AreEqual(0, proc.ExitCode ?? -1, "Failed to create conda environment");
            }

            var service = new MockInterpreterOptionsService();
            var provider = new MockPythonInterpreterFactoryProvider("Conda Env Provider");
            var factory = new MockPythonInterpreterFactory(
                new InterpreterConfiguration(
                    "Mock;" + Guid.NewGuid().ToString(),
                    Path.GetFileName(PathUtils.TrimEndSeparator(env)),
                    env,
                    PathUtils.FindFile(env, "python.exe"),
                    PathUtils.FindFile(env, "python.exe"),
                    "PYTHONPATH",
                    python.Architecture,
                    python.Version.ToVersion()
                )
            );
            factory.PackageManager = new CondaPackageManager(false);
            factory.PackageManager.SetInterpreterFactory(factory);
            provider.AddFactory(factory);
            service.AddProvider(provider);
            return service;
        }

        sealed class EnvironmentListProxy : IDisposable {
            private readonly WpfProxy _proxy;
            private readonly ToolWindow _window;

            public EnvironmentListProxy(WpfProxy proxy) {
                _proxy = proxy;
                _window = proxy.InvokeWithRetry(() => new ToolWindow());
                _window.ViewCreated += Window_ViewCreated;
            }

            public void Dispose() {
                if (_window != null) {
                    _proxy.Invoke(() => _window.Dispose());
                }
            }

            public ToolWindow Window {
                get { return _window; }
            }

            private void Window_ViewCreated(object sender, EnvironmentViewEventArgs e) {
                if (CreateDBExtension) {
                    var withDb = e.View.Factory as PythonInterpreterFactoryWithDatabase;
                    if (withDb != null && !string.IsNullOrEmpty(withDb.DatabasePath)) {
                        e.View.Extensions.Add(new DBExtensionProvider(withDb));
                    }
                }
                if (CreatePipExtension && e.View.Factory.PackageManager != null) {
                    var pip = new PipExtensionProvider(e.View.Factory);
                    pip.OutputTextReceived += (s, e2) => Console.Write("OUT: "+ e2.Data);
                    pip.ErrorTextReceived += (s, e2) => Console.Write("ERR: " + e2.Data);
                    e.View.Extensions.Add(pip);
                }
            }

            public bool CreateDBExtension { get; set; }
            public bool CreatePipExtension { get; set; }

            public IInterpreterOptionsService Service {
                get {
                    return _proxy.Invoke(() => Window.Service);
                }
                set {
                    _proxy.Invoke(() => { Window.Service = value; });
                }
            }

            public IInterpreterRegistryService Interpreters {
                get {
                    return _proxy.Invoke(() => Window.Interpreters);
                }
                set {
                    _proxy.Invoke(() => { Window.Interpreters = value; });
                }
            }

            public List<EnvironmentView> Environments {
                get {
                    return _proxy.Invoke(() =>
                        Window._environments
                            .Where(ev => !EnvironmentView.IsAddNewEnvironmentView(ev) && !EnvironmentView.IsOnlineHelpView(ev))
                            .ToList()
                    );
                }
            }

            public EnvironmentView AddNewEnvironmentView {
                get {
                    return _proxy.Invoke(() => Window._environments.Single(ev => EnvironmentView.IsAddNewEnvironmentView(ev)));
                }
            }

            public bool CanExecute(RoutedCommand command, object parameter) {
                return _proxy.CanExecute(command, _window, parameter);
            }

            public Task Execute(RoutedCommand command, object parameter, CancellationToken token) {
                return _proxy.Execute(command, _window, parameter, token);
            }

            public T GetExtensionOrDefault<T>(EnvironmentView view) where T : IEnvironmentViewExtension {
                var ext = _proxy.Invoke(() => view.Extensions.OfType<T>().FirstOrDefault());
                if (ext != null) {
                    // Get the WpfObject to ensure it is constructed on the
                    // UI thread.
                    var fe = _proxy.Invoke(() => ext.WpfObject);
                }
                return ext;
            }

            public T GetExtensionOrAssert<T>(EnvironmentView view) where T : IEnvironmentViewExtension {
                var ext = GetExtensionOrDefault<T>(view);
                Assert.IsNotNull(ext, "Unable to get " + typeof(T).Name);
                return ext;
            }
        }

        static CompositionContainer CreateCompositionContainer(bool defaultProviders = true) {
            var sp = new MockServiceProvider();
            sp.Services[typeof(SVsActivityLog).GUID] = new MockActivityLog();
            var settings = new MockSettingsManager();
            sp.Services[typeof(SVsSettingsManager).GUID] = settings;

            return InterpreterCatalog.CreateContainer(typeof(IInterpreterRegistryService), typeof(IInterpreterOptionsService));
        }

        sealed class AssertInterpretersChanged : IDisposable {
            private readonly ManualResetEvent _evt;
            private readonly IInterpreterRegistryService _service;
            private readonly TimeSpan _timeout;

            public AssertInterpretersChanged(IInterpreterRegistryService service, TimeSpan timeout) {
                _service = service;
                _evt = new ManualResetEvent(false);
                _timeout = timeout;
                _service.InterpretersChanged += Service_InterpretersChanged;
            }

            private void Service_InterpretersChanged(object sender, EventArgs e) {
                _evt.Set();
            }

            public void Dispose() {
                bool changed = _evt.WaitOne((int)_timeout.TotalMilliseconds);
                _service.InterpretersChanged -= Service_InterpretersChanged;
                _evt.Dispose();
                Assert.IsTrue(changed, "No change observed");
            }
        }

        #endregion
    }

    class TestPipPackageCache : PipPackageCache {
        public TestPipPackageCache(string cachePath = null) : base(
            null, null,
            cachePath ?? Path.Combine(TestData.GetTempPath(), "test.cache")
        ) {
            _userCount = 1;
        }

        internal async Task<PackageSpec> TestInjectPackageAsync(string packageSpec, string description) {
            await _cacheLock.WaitAsync();
            try {
                var p = PackageSpec.FromRequirement(packageSpec);
                p.Description = description;
                _cache[p.Name] = p;
                _cacheAge = DateTime.Now;
                return p;
            } finally {
                _cacheLock.Release();
            }
        }

        internal async Task TestClearPackagesAsync() {
            await _cacheLock.WaitAsync();
            try {
                _cache.Clear();
                _cacheAge = DateTime.MinValue;
            } finally {
                _cacheLock.Release();
            }
        }

        internal async Task<List<string>> TestGetAllPackageNamesAsync() {
            await _cacheLock.WaitAsync();
            try {
                return _cache.Keys.ToList();
            } finally {
                _cacheLock.Release();
            }
        }

        internal async Task<List<string>> TestGetAllPackageSpecsAsync() {
            await _cacheLock.WaitAsync();
            try {
                return _cache.Values.Select(p => "{0} #{1}".FormatInvariant(p.FullSpec, p.Description)).ToList();
            } finally {
                _cacheLock.Release();
            }
        }

        internal async Task TestWriteCacheToDiskAsync() {
            await _cacheLock.WaitAsync();
            try {
                using (var cts = new CancellationTokenSource(5000)) {
                    await WriteCacheToDiskAsync(cts.Token);
                }
            } finally {
                _cacheLock.Release();
            }
        }

        internal async Task TestReadCacheFromDiskAsync() {
            await _cacheLock.WaitAsync();
            try {
                using (var cts = new CancellationTokenSource(5000)) {
                    await ReadCacheFromDiskAsync(cts.Token);
                }
            } finally {
                _cacheLock.Release();
            }
        }
    }
}

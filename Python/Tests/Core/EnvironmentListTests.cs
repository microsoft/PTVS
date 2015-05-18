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

extern alias analysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.EnvironmentsList;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Mocks;
using TestUtilities.Python;
using CommonUtils = analysis::Microsoft.VisualStudioTools.CommonUtils;
using ProcessOutput = analysis::Microsoft.VisualStudioTools.Project.ProcessOutput;

namespace PythonToolsUITests {
    [TestClass]
    public class EnvironmentListTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy(includeTestData: false);
        }


        private static InterpreterConfiguration MockInterpreterConfiguration(Version version, InterpreterUIMode uiMode) {
            return new InterpreterConfiguration(null, null, null, null, null, ProcessorArchitecture.None, version, uiMode);
        }

        private static InterpreterConfiguration MockInterpreterConfiguration(Version version) {
            return new InterpreterConfiguration(version);
        }

        private static InterpreterConfiguration MockInterpreterConfiguration(string path) {
            return new InterpreterConfiguration(Path.GetDirectoryName(path), path, "", "", "", ProcessorArchitecture.None, new Version(2, 7));
        }

        [TestMethod, Priority(0)]
        public void HasInterpreters() {
            var sp = new MockServiceProvider();
            var service = new InterpreterOptionsService(sp);
            var mockService = new MockInterpreterOptionsService();
            mockService.AddProvider(new MockPythonInterpreterFactoryProvider("Test Provider 1",
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 1", MockInterpreterConfiguration(new Version(2, 7))),
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 2", MockInterpreterConfiguration(new Version(3, 0))),
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 3", MockInterpreterConfiguration(new Version(3, 3)))
            ));
            mockService.AddProvider(new MockPythonInterpreterFactoryProvider("Test Provider 2",
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 4", MockInterpreterConfiguration(new Version(2, 7))),
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 5", MockInterpreterConfiguration(new Version(3, 0))),
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 6", MockInterpreterConfiguration(new Version(3, 3))),
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Hidden Factory 7", MockInterpreterConfiguration(new Version(3, 3), InterpreterUIMode.Hidden))
            ));

            using (var wpf = new WpfProxy())
            using (var list = new EnvironmentListProxy(wpf)) {
                list.Service = mockService;
                var environments = list.Environments;

                Assert.AreEqual(6, environments.Count);
                AssertUtil.ContainsExactly(
                    wpf.Invoke(() => environments.Select(ev => ev.Description).ToList()),
                    Enumerable.Range(1, 6).Select(i => string.Format("Test Factory {0}", i))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void NonDefaultInterpreter() {
            var mockProvider = new MockPythonInterpreterFactoryProvider("Test Provider 1",
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 1", MockInterpreterConfiguration(new Version(2, 7))),
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 2", MockInterpreterConfiguration(new Version(3, 0), InterpreterUIMode.CannotBeDefault)),
                new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 3", MockInterpreterConfiguration(new Version(3, 3), InterpreterUIMode.CannotBeAutoDefault))
            );

            using (var wpf = new WpfProxy())
            using (var list = new EnvironmentListProxy(wpf)) {
                var service = GetInterpreterOptionsService();
                var oldDefault = service.DefaultInterpreter;
                var oldProviders = ((InterpreterOptionsService)service).SetProviders(new[] { mockProvider });
                try {
                    list.Service = service;
                    var environments = list.Environments;

                    AssertUtil.AreEqual(
                        wpf.Invoke(() => environments.Select(ev => ev.Description).ToList()),
                        "Test Factory 1", "Test Factory 2", "Test Factory 3"
                    );
                    // TF 1 and 3 can be set as default
                    AssertUtil.AreEqual(
                        wpf.Invoke(() => environments.Select(ev => ev.CanBeDefault).ToList()),
                        true, false, true
                    );

                    // TF 1 should have been selected as the default
                    Assert.AreEqual(
                        "Test Factory 1",
                        wpf.Invoke(() => environments.First(ev => ev.IsDefault).Description)
                    );
                } finally {
                    ((InterpreterOptionsService)service).SetProviders(oldProviders);
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
                    new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 1", MockInterpreterConfiguration(new Version(2, 7))),
                    new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 2", MockInterpreterConfiguration(new Version(3, 0))),
                    new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 3", MockInterpreterConfiguration(new Version(3, 3)))
                );

                list.Service = mockService;

                Assert.AreEqual(0, list.Environments.Count);

                mockService.AddProvider(provider);
                Assert.AreEqual(3, list.Environments.Count);
                provider.AddFactory(new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 4", MockInterpreterConfiguration(new Version(2, 7))));
                Assert.AreEqual(4, list.Environments.Count);
                provider.AddFactory(new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 5", MockInterpreterConfiguration(new Version(3, 0))));
                Assert.AreEqual(5, list.Environments.Count);
                provider.AddFactory(new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Factory 6", MockInterpreterConfiguration(new Version(3, 3))));
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

                foreach (string invalidPath in new string[] { 
                    null, 
                    "", 
                    "NOT A REAL PATH", 
                    string.Join("\\", Path.GetInvalidPathChars().Select(c => c.ToString()))
                }) {
                    Console.WriteLine("Path: <{0}>", invalidPath ?? "(null)");
                    provider.RemoveAllFactories();
                    provider.AddFactory(new MockPythonInterpreterFactory(
                        Guid.NewGuid(),
                        "Test Factory",
                        new InterpreterConfiguration(
                            invalidPath,
                            invalidPath,
                            "",
                            "",
                            "",
                            ProcessorArchitecture.None,
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

                foreach (var version in PythonPaths.Versions) {
                    Console.WriteLine("Path: <{0}>", version.InterpreterPath);
                    provider.RemoveAllFactories();
                    provider.AddFactory(new MockPythonInterpreterFactory(
                        Guid.NewGuid(),
                        "Test Factory",
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
                Guid.NewGuid(),
                "Test Factory 1",
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
                var view = list.Environments.Single();

                Assert.IsFalse(wpf.Invoke(() => view.IsRefreshingDB));
                Assert.IsTrue(list.CanExecute(DBExtension.StartRefreshDB, view));
                Assert.IsFalse(fact.IsCurrent);
                Assert.AreEqual(MockPythonInterpreterFactory.NoDatabaseReason, fact.GetIsCurrentReason(null));

                list.Execute(DBExtension.StartRefreshDB, view).GetAwaiter().GetResult();
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

                list.Execute(DBExtension.StartRefreshDB, view).GetAwaiter().GetResult();

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
                list.Service = GetInterpreterOptionsService();

                var expected = new HashSet<string>(
                    PythonPaths.Versions
                        .Where(v => !v.IsIronPython)
                        .Select(v => v.InterpreterPath),
                    StringComparer.OrdinalIgnoreCase
                );
                var actual = wpf.Invoke(() => new HashSet<string>(
                    list.Environments.Select(ev => (string)ev.InterpreterPath),
                    StringComparer.OrdinalIgnoreCase
                ));

                Console.WriteLine("Expected - Actual: " + string.Join(", ", expected.Except(actual).OrderBy(s => s)));
                Console.WriteLine("Actual - Expected: " + string.Join(", ", actual.Except(expected).OrderBy(s => s)));

                AssertUtil.ContainsExactly(
                    expected,
                    actual
                );
            }
        }

        [TestMethod, Priority(0)]
        public void AddUpdateRemoveConfigurableFactory() {
            using (var wpf = new WpfProxy())
            using (var list = new EnvironmentListProxy(wpf)) {
                list.Service = GetInterpreterOptionsService();

                var before = wpf.Invoke(() => new HashSet<string>(
                    list.Environments.Select(ev => (string)ev.InterpreterPath),
                    StringComparer.OrdinalIgnoreCase
                ));

                var configurable = list.Service.KnownProviders.OfType<ConfigurablePythonInterpreterFactoryProvider>().FirstOrDefault();
                Assert.IsNotNull(configurable, "No configurable provider available");
                var fact = configurable.SetOptions(new InterpreterFactoryCreationOptions {
                    Id = Guid.NewGuid(),
                    LanguageVersionString = "2.7",
                    // The actual file doesn't matter, except to test that it
                    // is added
                    InterpreterPath = TestData.GetPath("HelloWorld\\HelloWorld.pyproj")
                });

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

                    configurable.SetOptions(new InterpreterFactoryCreationOptions {
                        Id = fact.Id,
                        LanguageVersion = fact.Configuration.Version,
                        InterpreterPath = TestData.GetPath("HelloWorld2\\HelloWorld.pyproj")
                    });

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
                    configurable.RemoveInterpreter(fact.Id);
                }

                var afterRemove = wpf.Invoke(() => new HashSet<string>(
                    list.Environments.Select(ev => (string)ev.InterpreterPath),
                    StringComparer.OrdinalIgnoreCase
                ));
                AssertUtil.ContainsExactly(afterRemove, before);
            }
        }

        [TestMethod, Priority(0)]
        public async Task AddUpdateRemoveConfigurableFactoryThroughUI() {
            using (var wpf = new WpfProxy())
            using (var list = new EnvironmentListProxy(wpf)) {
                list.Service = GetInterpreterOptionsService();

                var before = wpf.Invoke(() => new HashSet<Guid>(list.Environments.Where(ev => ev.Factory != null).Select(ev => ev.Factory.Id)));

                var configurable = list.Service.KnownProviders.OfType<ConfigurablePythonInterpreterFactoryProvider>().FirstOrDefault();
                Assert.IsNotNull(configurable, "No configurable provider available");

                await list.Execute(ApplicationCommands.New, null);
                var afterAdd = wpf.Invoke(() => new HashSet<Guid>(list.Environments.Where(ev => ev.Factory != null).Select(ev => ev.Factory.Id)));

                var difference = new HashSet<Guid>(afterAdd);
                difference.ExceptWith(before);

                Console.WriteLine("Added {0}", AssertUtil.MakeText(difference));
                Assert.AreEqual(1, difference.Count, "Did not add a new environment");
                var newEnv = list.Service.Interpreters.Single(f => difference.Contains(f.Id));

                Assert.IsTrue(configurable.IsConfigurable(newEnv), "Did not add a configurable environment");

                // To remove the environment, we need to trigger the Remove
                // command on the ConfigurationExtensionProvider's control
                var view = wpf.Invoke(() => list.Environments.First(ev => ev.Factory == newEnv));
                var extView = wpf.Invoke(() => view.Extensions.OfType<ConfigurationExtensionProvider>().First().WpfObject);
                var confView = wpf.Invoke(() => (ConfigurationEnvironmentView)((System.Windows.Controls.Grid)extView.FindName("Subcontext")).DataContext);
                await wpf.Execute((RoutedCommand)ConfigurationExtension.Remove, extView, confView);

                var afterRemove = wpf.Invoke(() => new HashSet<Guid>(list.Environments.Where(ev => ev.Factory != null).Select(ev => ev.Factory.Id)));
                AssertUtil.ContainsExactly(afterRemove, before);
            }
        }

        [TestMethod, Priority(0)]
        public void LoadUnloadProjectFactories() {
            var service = new MockInterpreterOptionsService();
            var mockProvider = new MockPythonInterpreterFactoryProvider("Test Provider");
            mockProvider.AddFactory(new MockPythonInterpreterFactory(Guid.NewGuid(), "Test Environment", MockInterpreterConfiguration(new Version(2, 7))));
            service.AddProvider(mockProvider);

            var loaded = new LoadedProjectInterpreterFactoryProvider();
            service.AddProvider(loaded);
            using (var wpf = new WpfProxy())
            using (var list = new EnvironmentListProxy(wpf)) {
                list.Service = service;

                // List only contains one entry
                AssertUtil.ContainsExactly(
                    wpf.Invoke(() => list.Environments.Select(ev => ev.Description).ToList()),
                    "Test Environment"
                );

                var project = new MockPythonInterpreterFactoryProvider("Fake Project");
                project.AddFactory(new MockPythonInterpreterFactory(Guid.NewGuid(), "Fake Environment", MockInterpreterConfiguration(new Version(2, 7))));

                loaded.ProjectLoaded(project, null);

                // List now contains two entries
                AssertUtil.ContainsExactly(
                    wpf.Invoke(() => list.Environments.Select(ev => ev.Description).ToList()),
                    "Test Environment",
                    "Fake Environment"
                );

                loaded.ProjectUnloaded(project);

                // List only has one entry again
                AssertUtil.ContainsExactly(
                    wpf.Invoke(() => list.Environments.Select(ev => ev.Description).ToList()),
                    "Test Environment"
                );
            }
        }


        [TestMethod, Priority(0)]
        public void AddRemoveProjectFactories() {
            var service = new MockInterpreterOptionsService();
            var loaded = new LoadedProjectInterpreterFactoryProvider();
            service.AddProvider(loaded);
            using (var wpf = new WpfProxy())
            using (var list = new EnvironmentListProxy(wpf)) {
                list.Service = service;

                // List should be empty
                AssertUtil.ContainsExactly(list.Environments);

                var project = new MockPythonInterpreterFactoryProvider("Fake Project");

                loaded.ProjectLoaded(project, null);

                // List is still empty
                AssertUtil.ContainsExactly(list.Environments);

                project.AddFactory(new MockPythonInterpreterFactory(Guid.NewGuid(), "Fake Environment", MockInterpreterConfiguration(new Version(2, 7))));

                // List now contains one project
                AssertUtil.ContainsExactly(
                    wpf.Invoke(() => list.Environments.Select(ev => ev.Description).ToList()),
                    "Fake Environment"
                );

                project.RemoveAllFactories();

                // List is empty again
                AssertUtil.ContainsExactly(list.Environments);

                loaded.ProjectUnloaded(project);
            }
        }

        [TestMethod, Priority(0)]
        public void ChangeDefault() {
            var service = GetInterpreterOptionsService();
            using (var defaultChanged = new AutoResetEvent(false))
            using (var wpf = new WpfProxy())
            using (var list = new EnvironmentListProxy(wpf)) {
                service.DefaultInterpreterChanged += (s, e) => { defaultChanged.Set(); };
                list.Service = service;
                var originalDefault = service.DefaultInterpreter;
                try {
                    foreach (var interpreter in service.Interpreters) {
                        var environment = list.Environments.FirstOrDefault(ev =>
                            ev.Factory == interpreter
                        );
                        Assert.IsNotNull(environment, string.Format("Did not find {0}", interpreter.Description));

                        list.Execute(EnvironmentView.MakeGlobalDefault, environment);
                        Assert.IsTrue(defaultChanged.WaitOne(TimeSpan.FromSeconds(10.0)), "Setting default took too long");

                        Assert.AreEqual(interpreter, service.DefaultInterpreter,
                            string.Format(
                                "Failed to change default from {0} to {1}",
                                service.DefaultInterpreter.Description,
                                interpreter.Description
                        ));
                    }
                } finally {
                    service.DefaultInterpreter = originalDefault;
                }
            }
        }

        [TestMethod, Priority(0)]
        public void PipExtension() {
            var service = MakeEmptyVEnv();

            using (var wpf = new WpfProxy())
            using (var list = new EnvironmentListProxy(wpf)) {
                list.CreatePipExtension = true;
                list.Service = service;

                var environment = list.Environments.Single();
                var pip = (PipExtensionProvider)list.GetExtensionOrAssert<PipExtensionProvider>(environment);

                pip.CheckPipInstalledAsync().GetAwaiter().GetResult();
                Assert.AreEqual(false, pip.IsPipInstalled, "venv should not install pip");
                var task = wpf.Invoke(() => pip.InstallPip().ContinueWith<bool>(LogException));
                Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(120.0)), "pip install timed out");
                Assert.IsTrue(task.Result, "pip install failed");
                Assert.AreEqual(true, pip.IsPipInstalled, "pip was not installed");

                var packages = pip.GetInstalledPackagesAsync().GetAwaiter().GetResult();
                AssertUtil.ContainsExactly(packages.Select(pv => pv.Name), "pip", "setuptools");

                task = wpf.Invoke(() => pip.InstallPackage("ptvsd", true).ContinueWith<bool>(LogException));
                Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(60.0)), "pip install ptvsd timed out");
                Assert.IsTrue(task.Result, "pip install ptvsd failed");
                packages = pip.GetInstalledPackagesAsync().GetAwaiter().GetResult();
                AssertUtil.ContainsAtLeast(packages.Select(pv => pv.Name), "ptvsd");

                task = wpf.Invoke(() => pip.UninstallPackage("ptvsd").ContinueWith<bool>(LogException));
                Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(60.0)), "pip uninstall ptvsd timed out");
                Assert.IsTrue(task.Result, "pip uninstall ptvsd failed");
                packages = pip.GetInstalledPackagesAsync().GetAwaiter().GetResult();
                AssertUtil.DoesntContain(packages.Select(pv => pv.Name), "ptvsd");
            }
        }

        [TestMethod]
        public async Task SaveLoadCache() {
            var cachePath = Path.Combine(TestData.GetTempPath(randomSubPath: true), "pip.cache");
            using (var cache = new TestPipPackageCache(cachePath)) {
                AssertUtil.ContainsExactly(await cache.TestGetAllPackageNamesAsync());

                var p = await cache.TestInjectPackageAsync("azure==0.9");
                p.Description = "azure description";

                p = await cache.TestInjectPackageAsync("ptvsd==1.0");
                p.Description = "ptvsd description";
                p.UpgradeVersion = p.Version;

                // Descriptions are URL encoded
                // Only UpgradeVersion is stored
                AssertUtil.ContainsExactly(await cache.TestGetAllPackageSpecsAsync(),
                    "azure:azure%20description",
                    "ptvsd==1.0:ptvsd%20description"
                );

                await cache.TestWriteCacheToDiskAsync();
            }

            using (var cache = new TestPipPackageCache(cachePath)) {
                AssertUtil.ContainsExactly(await cache.TestGetAllPackageNamesAsync());

                await cache.TestReadCacheFromDiskAsync();

                // Descriptions are not cached
                AssertUtil.ContainsExactly(await cache.TestGetAllPackageSpecsAsync(),
                    "azure",
                    "ptvsd==1.0"
                );

            }
        }

        [TestMethod]
        public async Task UpdatePackageInfo() {
            using (var cache = new TestPipPackageCache()) {
                AssertUtil.ContainsExactly(await cache.TestGetAllPackageNamesAsync());

                var p = await cache.TestInjectPackageAsync("ptvsd==1.0");

                AssertUtil.ContainsExactly(await cache.TestGetAllPackageNamesAsync(), "ptvsd");

                var changes = new List<string>();
                p.PropertyChanged += (s, e) => { changes.Add(e.PropertyName); };

                await cache.UpdatePackageInfoAsync(p, CancellationToken.None);

                AssertUtil.ContainsExactly(changes, "Description", "UpgradeVersion");
                Assert.IsTrue(p.UpgradeVersion.CompareTo(p.Version) > 0,
                    string.Format("Expected {0} > {1}", p.UpgradeVersion, p.Version)
                );
            }
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

        private static IInterpreterOptionsService MakeEmptyVEnv() {
            var python = PythonPaths.Versions.FirstOrDefault(p =>
                p.IsCPython && Directory.Exists(Path.Combine(p.LibPath, "venv"))
            );
            if (python == null) {
                Assert.Inconclusive("Requires Python with venv");
            }

            var env = TestData.GetTempPath(randomSubPath: true);
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
            provider.AddFactory(new MockPythonInterpreterFactory(
                Guid.NewGuid(),
                Path.GetFileName(CommonUtils.TrimEndSeparator(env)),
                new InterpreterConfiguration(
                    env,
                    CommonUtils.FindFile(env, "python.exe"),
                    CommonUtils.FindFile(env, "python.exe"),
                    Path.GetDirectoryName(CommonUtils.FindFile(env, "site.py", 3)),
                    "PYTHONPATH",
                    python.Isx64 ? ProcessorArchitecture.Amd64 : ProcessorArchitecture.X86,
                    python.Version.ToVersion()
                )
            ));
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
                    if (withDb != null) {
                        e.View.Extensions.Add(new DBExtensionProvider(withDb));
                    }
                }
                if (CreatePipExtension) {
                    var pip = new PipExtensionProvider(e.View.Factory);
                    pip.OutputTextReceived += (s, e2) => Console.WriteLine(e2.Value);
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

            public List<EnvironmentView> Environments {
                get {
                    return _proxy.Invoke(() => 
                        Window._environments
                            .Except(EnvironmentView.AddNewEnvironmentViewOnce.Value)
                            .Except(EnvironmentView.OnlineHelpViewOnce.Value)
                            .ToList()
                    );
                }
            }

            public bool CanExecute(RoutedCommand command, object parameter) {
                return _proxy.CanExecute(command, _window, parameter);
            }

            public Task Execute(RoutedCommand command, object parameter) {
                return _proxy.Execute(command, _window, parameter);
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

        static IInterpreterOptionsService GetInterpreterOptionsService() {
            var sp = new MockServiceProvider();
            sp.Services[typeof(SVsActivityLog).GUID] = new MockActivityLog();
            var settings = new MockSettingsManager();
            sp.Services[typeof(SVsSettingsManager).GUID] = settings;
            settings.Store.AddSetting(
                InterpreterOptionsService.FactoryProvidersCollection + "\\CPythonAndConfigurable",
                InterpreterOptionsService.FactoryProviderCodeBaseSetting,
                typeof(CPythonInterpreterFactoryConstants).Assembly.Location
            );
            settings.Store.AddSetting(
                InterpreterOptionsService.FactoryProvidersCollection + "\\LoadedProjects",
                InterpreterOptionsService.FactoryProviderCodeBaseSetting,
                typeof(LoadedProjectInterpreterFactoryProvider).Assembly.Location
            );
            return new InterpreterOptionsService(sp);
        }

        #endregion
    }

    class TestPipPackageCache : PipPackageCache {
        public TestPipPackageCache(string cachePath = null) : base(
            null, null,
            cachePath ?? Path.Combine(TestData.GetTempPath(randomSubPath: true), "test.cache")
        ) {
            _userCount = 1;
        }

        internal async Task<PipPackageView> TestInjectPackageAsync(string packageSpec) {
            await _cacheLock.WaitAsync();
            try {
                var p = new PipPackageView(this, packageSpec);
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
                return _cache.Values.Select(p => p.GetPackageSpec(true, true)).ToList();
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

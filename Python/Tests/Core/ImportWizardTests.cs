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
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Xml.Linq;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Project.ImportWizard;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;
using CommonUtils = analysis::Microsoft.VisualStudioTools.CommonUtils;
using ProcessOutput = analysis::Microsoft.VisualStudioTools.Project.ProcessOutput;

namespace PythonToolsTests {
    [TestClass]
    public class ImportWizardTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        #region ImportSettingsProxy class

        sealed class ImportSettingsProxy : IDisposable {
            IInterpreterOptionsService _service;
            ImportSettings _settings;
            readonly Thread _controller;
            readonly AutoResetEvent _ready;

            public ImportSettingsProxy(IInterpreterOptionsService service = null) {
                _service = service;
                _ready = new AutoResetEvent(false);
                _controller = new Thread(ControllerThread);
                _controller.Start();
                _ready.WaitOne();
            }

            public void Dispose() {
                GC.SuppressFinalize(this);
                _settings.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
                _controller.Join();
                _ready.Dispose();
            }

            ~ImportSettingsProxy() {
                Dispose();
            }

            private void ControllerThread(object obj) {
                _settings = new ImportSettings(_service);
                _ready.Set();
                Dispatcher.Run();
            }

            public string CreateRequestedProject() {
                ExceptionDispatchInfo exInfo = null;
                string result = null;
                _ready.Reset();
                _settings.Dispatcher.BeginInvoke((Action)(async () => {
                    try {
                        result = await _settings.CreateRequestedProjectAsync();
                    } catch (Exception ex) {
                        if (ErrorHandler.IsCriticalException(ex)) {
                            throw;
                        }
                        exInfo = ExceptionDispatchInfo.Capture(ex);
                    }
                    _ready.Set();
                }));
                _ready.WaitOne();
                if (exInfo != null) {
                    exInfo.Throw();
                }
                return result;
            }

            private T GetValue<T>(DependencyProperty prop) {
                return _settings.Dispatcher.Invoke((Func<T>)(() => (T)_settings.GetValue(prop)));
            }

            private void SetValue<T>(DependencyProperty prop, T value) {
                _settings.Dispatcher.Invoke((Action)(() => _settings.SetCurrentValue(prop, (object)value)));
            }

            public string SourcePath {
                get { return GetValue<string>(ImportSettings.SourcePathProperty); }
                set { SetValue(ImportSettings.SourcePathProperty, value); }
            }

            public string Filters {
                get { return GetValue<string>(ImportSettings.FiltersProperty); }
                set { SetValue(ImportSettings.FiltersProperty, value); }
            }

            public string SearchPaths {
                get { return GetValue<string>(ImportSettings.SearchPathsProperty); }
                set { SetValue(ImportSettings.SearchPathsProperty, value); }
            }

            public string ProjectPath {
                get { return GetValue<string>(ImportSettings.ProjectPathProperty); }
                set { SetValue(ImportSettings.ProjectPathProperty, value); }
            }

            public string StartupFile {
                get { return GetValue<string>(ImportSettings.StartupFileProperty); }
                set { SetValue(ImportSettings.StartupFileProperty, value); }
            }

            public PythonInterpreterView SelectedInterpreter {
                get { return GetValue<PythonInterpreterView>(ImportSettings.SelectedInterpreterProperty); }
                set { SetValue(ImportSettings.SelectedInterpreterProperty, value); }
            }

            public void AddAvailableInterpreter(PythonInterpreterView interpreter) {
                _settings.Dispatcher.Invoke((Action)(() => _settings.AvailableInterpreters.Add(interpreter)));
            }

            public bool UseCustomization {
                get { return GetValue<bool>(ImportSettings.UseCustomizationProperty); }
                set { SetValue(ImportSettings.UseCustomizationProperty, value); }
            }
            
            public ProjectCustomization Customization {
                get { return GetValue<ProjectCustomization>(ImportSettings.CustomizationProperty); }
                set { SetValue(ImportSettings.CustomizationProperty, value); }
            }
        }

        #endregion

        [TestMethod, Priority(0)]
        public void ImportWizardSimple() {
            using (var settings = new ImportSettingsProxy()) {
                settings.SourcePath = TestData.GetPath("TestData\\HelloWorld\\");
                settings.Filters = "*.py;*.pyproj";
                settings.SearchPaths = TestData.GetPath("TestData\\SearchPath1\\") + Environment.NewLine + TestData.GetPath("TestData\\SearchPath2\\");
                settings.ProjectPath = TestData.GetPath("TestData\\TestDestination\\Subdirectory\\ProjectName.pyproj");

                var path = settings.CreateRequestedProject();

                Assert.AreEqual(settings.ProjectPath, path);
                var proj = XDocument.Load(path);

                Assert.AreEqual("4.0", proj.Descendant("Project").Attribute("ToolsVersion").Value);
                Assert.AreEqual("..\\..\\HelloWorld\\", proj.Descendant("ProjectHome").Value);
                Assert.AreEqual("..\\SearchPath1\\;..\\SearchPath2\\", proj.Descendant("SearchPath").Value);
                AssertUtil.ContainsExactly(proj.Descendants(proj.GetName("Compile")).Select(x => x.Attribute("Include").Value),
                    "Program.py");
                AssertUtil.ContainsExactly(proj.Descendants(proj.GetName("Content")).Select(x => x.Attribute("Include").Value),
                    "HelloWorld.pyproj");
            }
        }

        [TestMethod, Priority(0)]
        public void ImportWizardFiltered() {
            using (var settings = new ImportSettingsProxy()) {
                settings.SourcePath = TestData.GetPath("TestData\\HelloWorld\\");
                settings.Filters = "*.py";
                settings.SearchPaths = TestData.GetPath("TestData\\SearchPath1\\") + Environment.NewLine + TestData.GetPath("TestData\\SearchPath2\\");
                settings.ProjectPath = TestData.GetPath("TestData\\TestDestination\\Subdirectory\\ProjectName.pyproj");

                var path = settings.CreateRequestedProject();

                Assert.AreEqual(settings.ProjectPath, path);
                var proj = XDocument.Load(path);

                Assert.AreEqual("..\\..\\HelloWorld\\", proj.Descendant("ProjectHome").Value);
                Assert.AreEqual("..\\SearchPath1\\;..\\SearchPath2\\", proj.Descendant("SearchPath").Value);
                AssertUtil.ContainsExactly(proj.Descendants(proj.GetName("Compile")).Select(x => x.Attribute("Include").Value),
                    "Program.py");
                Assert.AreEqual(0, proj.Descendants(proj.GetName("Content")).Count());
            }
        }

        [TestMethod, Priority(0)]
        public void ImportWizardFolders() {
            using (var settings = new ImportSettingsProxy()) {
                settings.SourcePath = TestData.GetPath("TestData\\HelloWorld2\\");
                settings.Filters = "*";
                settings.ProjectPath = TestData.GetPath("TestData\\TestDestination\\Subdirectory\\ProjectName.pyproj");

                var path = settings.CreateRequestedProject();

                Assert.AreEqual(settings.ProjectPath, path);
                var proj = XDocument.Load(path);

                Assert.AreEqual("..\\..\\HelloWorld2\\", proj.Descendant("ProjectHome").Value);
                Assert.AreEqual("", proj.Descendant("SearchPath").Value);
                AssertUtil.ContainsExactly(proj.Descendants(proj.GetName("Compile")).Select(x => x.Attribute("Include").Value),
                    "Program.py",
                    "TestFolder\\SubItem.py",
                    "TestFolder2\\SubItem.py",
                    "TestFolder3\\SubItem.py");

                AssertUtil.ContainsExactly(proj.Descendants(proj.GetName("Folder")).Select(x => x.Attribute("Include").Value),
                    "TestFolder",
                    "TestFolder2",
                    "TestFolder3");
            }
        }

        [TestMethod, Priority(0)]
        public void ImportWizardInterpreter() {
            using (var settings = new ImportSettingsProxy()) {
                settings.SourcePath = TestData.GetPath("TestData\\HelloWorld\\");
                settings.Filters = "*.py;*.pyproj";

                var interpreter = new PythonInterpreterView("Test", Guid.NewGuid(), new Version(2, 7), null);
                settings.AddAvailableInterpreter(interpreter);
                settings.SelectedInterpreter = interpreter;
                settings.ProjectPath = TestData.GetPath("TestData\\TestDestination\\Subdirectory\\ProjectName.pyproj");

                var path = settings.CreateRequestedProject();

                Assert.AreEqual(settings.ProjectPath, path);
                var proj = XDocument.Load(path);

                Assert.AreEqual(interpreter.Id, Guid.Parse(proj.Descendant("InterpreterId").Value));
                Assert.AreEqual(interpreter.Version, Version.Parse(proj.Descendant("InterpreterVersion").Value));

                var interp = proj.Descendant("InterpreterReference");
                Assert.AreEqual(string.Format("{0:B}\\{1}", interpreter.Id, interpreter.Version),
                    interp.Attribute("Include").Value);
            }
        }

        [TestMethod, Priority(0)]
        public void ImportWizardStartupFile() {
            using (var settings = new ImportSettingsProxy()) {
                settings.SourcePath = TestData.GetPath("TestData\\HelloWorld\\");
                settings.Filters = "*.py;*.pyproj";
                settings.StartupFile = "Program.py";
                settings.ProjectPath = TestData.GetPath("TestData\\TestDestination\\Subdirectory\\ProjectName.pyproj");

                var path = settings.CreateRequestedProject();

                Assert.AreEqual(settings.ProjectPath, path);
                var proj = XDocument.Load(path);

                Assert.AreEqual("Program.py", proj.Descendant("StartupFile").Value);
            }
        }

        [TestMethod, Priority(0)]
        public void ImportWizardSemicolons() {
            // https://pytools.codeplex.com/workitem/2022
            using (var settings = new ImportSettingsProxy()) {
                var sourcePath = TestData.GetTempPath(randomSubPath: true);
                // Create a fake set of files to import
                Directory.CreateDirectory(Path.Combine(sourcePath, "ABC"));
                File.WriteAllText(Path.Combine(sourcePath, "ABC", "a;b;c.py"), "");
                Directory.CreateDirectory(Path.Combine(sourcePath, "A;B;C"));
                File.WriteAllText(Path.Combine(sourcePath, "A;B;C", "abc.py"), "");

                settings.SourcePath = sourcePath;

                var path = settings.CreateRequestedProject();

                Assert.AreEqual(settings.ProjectPath, path);
                var proj = XDocument.Load(path);

                AssertUtil.ContainsExactly(proj.Descendants(proj.GetName("Compile")).Select(x => x.Attribute("Include").Value),
                    "ABC\\a%3bb%3bc.py",
                    "A%3bB%3bC\\abc.py"
                );
                AssertUtil.ContainsExactly(proj.Descendants(proj.GetName("Folder")).Select(x => x.Attribute("Include").Value),
                    "ABC",
                    "A%3bB%3bC"
                );
            }
        }

        private void ImportWizardVirtualEnvWorker(
            PythonVersion python,
            string venvModuleName,
            string expectedFile,
            bool brokenBaseInterpreter
        ) {
            var mockService = new MockInterpreterOptionsService();
            mockService.AddProvider(new MockPythonInterpreterFactoryProvider("Test Provider",
                new MockPythonInterpreterFactory(python.Id, "Test Python", python.Configuration)
            ));

            using (var settings = new ImportSettingsProxy(mockService)) {
                var sourcePath = TestData.GetTempPath(randomSubPath: true);
                // Create a fake set of files to import
                File.WriteAllText(Path.Combine(sourcePath, "main.py"), "");
                Directory.CreateDirectory(Path.Combine(sourcePath, "A"));
                File.WriteAllText(Path.Combine(sourcePath, "A", "__init__.py"), "");
                // Create a real virtualenv environment to import
                using (var p = ProcessOutput.RunHiddenAndCapture(python.InterpreterPath, "-m", venvModuleName, Path.Combine(sourcePath, "env"))) {
                    Console.WriteLine(p.Arguments);
                    p.Wait();
                    Console.WriteLine(string.Join(Environment.NewLine, p.StandardOutputLines.Concat(p.StandardErrorLines)));
                    Assert.AreEqual(0, p.ExitCode);
                }

                if (brokenBaseInterpreter) {
                    var cfgPath = Path.Combine(sourcePath, "env", "Lib", "orig-prefix.txt");
                    if (File.Exists(cfgPath)) {
                        File.WriteAllText(cfgPath, string.Format("C:\\{0:N}", Guid.NewGuid()));
                    } else if (File.Exists((cfgPath = Path.Combine(sourcePath, "env", "pyvenv.cfg")))) {
                        File.WriteAllLines(cfgPath, File.ReadAllLines(cfgPath)
                            .Select(line => {
                                if (line.StartsWith("home = ")) {
                                    return string.Format("home = C:\\{0:N}", Guid.NewGuid());
                                }
                                return line;
                            })
                        );
                    }
                }

                Console.WriteLine("All files:");
                foreach (var f in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories)) {
                    Console.WriteLine(CommonUtils.GetRelativeFilePath(sourcePath, f));
                }

                Assert.IsTrue(
                    File.Exists(Path.Combine(sourcePath, "env", expectedFile)),
                    "Virtualenv was not created correctly"
                );

                settings.SourcePath = sourcePath;

                var path = settings.CreateRequestedProject();

                Assert.AreEqual(settings.ProjectPath, path);
                var proj = XDocument.Load(path);

                // Does not include any .py files from the virtualenv
                AssertUtil.ContainsExactly(proj.Descendants(proj.GetName("Compile")).Select(x => x.Attribute("Include").Value),
                    "main.py",
                    "A\\__init__.py"
                );
                // Does not contain 'env'
                AssertUtil.ContainsExactly(proj.Descendants(proj.GetName("Folder")).Select(x => x.Attribute("Include").Value),
                    "A"
                );

                var env = proj.Descendant("Interpreter");
                Assert.AreEqual("env\\", env.Attribute("Include").Value);
                Assert.AreEqual("lib\\", env.Descendant("LibraryPath").Value, true);
                if (brokenBaseInterpreter) {
                    Assert.AreEqual("env", env.Descendant("Description").Value);
                    Assert.AreEqual("", env.Descendant("InterpreterPath").Value);
                    Assert.AreEqual("", env.Descendant("WindowsInterpreterPath").Value);
                    Assert.AreEqual(Guid.Empty.ToString("B"), env.Descendant("BaseInterpreter").Value);
                    Assert.AreEqual("", env.Descendant("PathEnvironmentVariable").Value);
                } else {
                    Assert.AreEqual("env (Test Python)", env.Descendant("Description").Value);
                    Assert.AreEqual("scripts\\python.exe", env.Descendant("InterpreterPath").Value, true);
                    // The mock configuration uses python.exe for both paths.
                    Assert.AreEqual("scripts\\python.exe", env.Descendant("WindowsInterpreterPath").Value, true);
                    Assert.AreEqual(python.Id.ToString("B"), env.Descendant("BaseInterpreter").Value, true);
                    Assert.AreEqual("PYTHONPATH", env.Descendant("PathEnvironmentVariable").Value, true);
                }
            }
        }

        [TestMethod, Priority(0)]
        public void ImportWizardVirtualEnv() {
            var python = PythonPaths.Versions.LastOrDefault(pv =>
                pv.IsCPython &&
                File.Exists(Path.Combine(pv.LibPath, "site-packages", "virtualenv.py")) &&
                // CPython 3.3.4 does not work correctly with virtualenv, so
                // skip testing on 3.3 to avoid false failures
                pv.Version != PythonLanguageVersion.V33
            );

            ImportWizardVirtualEnvWorker(python, "virtualenv", "lib\\orig-prefix.txt", false);
        }

        [TestMethod, Priority(0)]
        public void ImportWizardVEnv() {
            var python = PythonPaths.Versions.LastOrDefault(pv =>
                pv.IsCPython && File.Exists(Path.Combine(pv.LibPath, "venv", "__main__.py"))
            );

            ImportWizardVirtualEnvWorker(python, "venv", "pyvenv.cfg", false);
        }

        [TestMethod, Priority(0)]
        public void ImportWizardBrokenVirtualEnv() {
            var python = PythonPaths.Versions.LastOrDefault(pv =>
                pv.IsCPython &&
                File.Exists(Path.Combine(pv.LibPath, "site-packages", "virtualenv.py")) &&
                    // CPython 3.3.4 does not work correctly with virtualenv, so
                    // skip testing on 3.3 to avoid false failures
                pv.Version != PythonLanguageVersion.V33
            );

            ImportWizardVirtualEnvWorker(python, "virtualenv", "lib\\orig-prefix.txt", true);
        }

        [TestMethod, Priority(0)]
        public void ImportWizardBrokenVEnv() {
            var python = PythonPaths.Versions.LastOrDefault(pv =>
                pv.IsCPython && File.Exists(Path.Combine(pv.LibPath, "venv", "__main__.py"))
            );

            ImportWizardVirtualEnvWorker(python, "venv", "pyvenv.cfg", true);
        }

        private static void ImportWizardCustomizationsWorker(ProjectCustomization customization, Action<XDocument> verify) {
            using (var settings = new ImportSettingsProxy()) {
                settings.SourcePath = TestData.GetPath("TestData\\HelloWorld\\");
                settings.Filters = "*.py;*.pyproj";
                settings.StartupFile = "Program.py";
                settings.UseCustomization = true;
                settings.Customization = customization;
                settings.ProjectPath = Path.Combine(TestData.GetTempPath("ImportWizardCustomizations_" + customization.GetType().Name), "Project.pyproj");
                Directory.CreateDirectory(Path.GetDirectoryName(settings.ProjectPath));

                var path = settings.CreateRequestedProject();

                Assert.AreEqual(settings.ProjectPath, path);
                Console.WriteLine(File.ReadAllText(path));
                var proj = XDocument.Load(path);

                verify(proj);
            }
        }

        [TestMethod, Priority(0)]
        public void ImportWizardCustomizations() {
            ImportWizardCustomizationsWorker(DefaultProjectCustomization.Instance, proj => {
                Assert.AreEqual("Program.py", proj.Descendant("StartupFile").Value);
                Assert.IsTrue(proj.Descendants(proj.GetName("Import")).Any(d => d.Attribute("Project").Value == "$(PtvsTargetsFile)"));
            });
            ImportWizardCustomizationsWorker(BottleProjectCustomization.Instance, proj => {
                Assert.AreNotEqual(-1, proj.Descendant("ProjectTypeGuids").Value.IndexOf("e614c764-6d9e-4607-9337-b7073809a0bd", StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(proj.Descendants(proj.GetName("Import")).Any(d => d.Attribute("Project").Value.Contains("Web.targets")));
                Assert.AreEqual("Web launcher", proj.Descendant("LaunchProvider").Value);
            });
            ImportWizardCustomizationsWorker(DjangoProjectCustomization.Instance, proj => {
                Assert.AreNotEqual(-1, proj.Descendant("ProjectTypeGuids").Value.IndexOf("5F0BE9CA-D677-4A4D-8806-6076C0FAAD37", StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(proj.Descendants(proj.GetName("Import")).Any(d => d.Attribute("Project").Value.Contains("Django.targets")));
                Assert.AreEqual("Django launcher", proj.Descendant("LaunchProvider").Value);
            });
            ImportWizardCustomizationsWorker(FlaskProjectCustomization.Instance, proj => {
                Assert.AreNotEqual(-1, proj.Descendant("ProjectTypeGuids").Value.IndexOf("789894c7-04a9-4a11-a6b5-3f4435165112", StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(proj.Descendants(proj.GetName("Import")).Any(d => d.Attribute("Project").Value.Contains("Web.targets")));
                Assert.AreEqual("Web launcher", proj.Descendant("LaunchProvider").Value);
            });
        }


        static T Wait<T>(System.Threading.Tasks.Task<T> task) {
            task.Wait();
            return task.Result;
        }

        [TestMethod, Priority(0)]
        public void ImportWizardCandidateStartupFiles() {
            var sourcePath = TestData.GetTempPath(randomSubPath: true);
            // Create a fake set of files to import
            File.WriteAllText(Path.Combine(sourcePath, "a.py"), "");
            File.WriteAllText(Path.Combine(sourcePath, "b.py"), "");
            File.WriteAllText(Path.Combine(sourcePath, "c.py"), "");
            File.WriteAllText(Path.Combine(sourcePath, "a.pyw"), "");
            File.WriteAllText(Path.Combine(sourcePath, "b.pyw"), "");
            File.WriteAllText(Path.Combine(sourcePath, "c.pyw"), "");
            File.WriteAllText(Path.Combine(sourcePath, "a.txt"), "");
            File.WriteAllText(Path.Combine(sourcePath, "b.txt"), "");
            File.WriteAllText(Path.Combine(sourcePath, "c.txt"), "");


            AssertUtil.ContainsExactly(Wait(ImportSettings.GetCandidateStartupFiles(sourcePath, "")),
                "a.py",
                "b.py",
                "c.py"
            );
            AssertUtil.ContainsExactly(Wait(ImportSettings.GetCandidateStartupFiles(sourcePath, "*.pyw")),
                "a.py",
                "b.py",
                "c.py",
                "a.pyw",
                "b.pyw",
                "c.pyw"
            );
            AssertUtil.ContainsExactly(Wait(ImportSettings.GetCandidateStartupFiles(sourcePath, "b.pyw")),
                "a.py",
                "b.py",
                "c.py",
                "b.pyw"
            );
            AssertUtil.ContainsExactly(Wait(ImportSettings.GetCandidateStartupFiles(sourcePath, "*.txt")),
                "a.py",
                "b.py",
                "c.py"
            );
        }

        [TestMethod, Priority(0)]
        public void ImportWizardDefaultStartupFile() {
            var files = new[] { "a.py", "b.py", "c.py" };
            var expectedDefault = files[0];

            Assert.AreEqual(expectedDefault, ImportSettings.SelectDefaultStartupFile(files, null));
            Assert.AreEqual(expectedDefault, ImportSettings.SelectDefaultStartupFile(files, "not in list"));
            Assert.AreEqual("b.py", ImportSettings.SelectDefaultStartupFile(files, "b.py"));
        }
    }
}

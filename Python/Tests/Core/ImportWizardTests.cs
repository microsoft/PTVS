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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Project.ImportWizard;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;

namespace PythonToolsTests {
    [TestClass]
    public class ImportWizardTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        private static string CreateRequestedProject(dynamic settings) {
            return Task.Run(async () => {
                return await await WpfProxy.FromObject((object)settings).InvokeAsync(
                    async () => await (Task<string>)settings.CreateRequestedProjectAsync()
                );
            })
                .GetAwaiter()
                .GetResult();
        }

        [TestMethod, Priority(TestExtensions.CORE_UNIT_TEST)]
        public void ImportWizardSimple() {
            using (var wpf = new WpfProxy()) {
                var root = TestData.GetTempPath();
                FileUtils.CopyDirectory(TestData.GetPath(@"TestData\HelloWorld"), Path.Combine(root, "HelloWorld"));
                FileUtils.CopyDirectory(TestData.GetPath(@"TestData\SearchPath1"), Path.Combine(root, "SearchPath1"));
                FileUtils.CopyDirectory(TestData.GetPath(@"TestData\SearchPath2"), Path.Combine(root, "SearchPath2"));

                var settings = wpf.Create(() => new ImportSettings(null, null));
                settings.SourcePath = PathUtils.GetAbsoluteDirectoryPath(root, "HelloWorld");
                settings.Filters = "*.py;*.pyproj";
                settings.SearchPaths = PathUtils.GetAbsoluteDirectoryPath(root, "SearchPath1") + Environment.NewLine + PathUtils.GetAbsoluteDirectoryPath(root, "SearchPath2");
                settings.ProjectPath = PathUtils.GetAbsoluteFilePath(root, @"TestDestination\Subdirectory\ProjectName.pyproj");

                string path = CreateRequestedProject(settings);

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

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
        public void ImportWizardFiltered() {
            using (var wpf = new WpfProxy()) {
                var root = TestData.GetTempPath();
                FileUtils.CopyDirectory(TestData.GetPath(@"TestData\HelloWorld"), Path.Combine(root, "HelloWorld"));
                FileUtils.CopyDirectory(TestData.GetPath(@"TestData\SearchPath1"), Path.Combine(root, "SearchPath1"));
                FileUtils.CopyDirectory(TestData.GetPath(@"TestData\SearchPath2"), Path.Combine(root, "SearchPath2"));

                var settings = wpf.Create(() => new ImportSettings(null, null));
                settings.SourcePath = PathUtils.GetAbsoluteDirectoryPath(root, "HelloWorld");
                settings.Filters = "*.py";
                settings.SearchPaths = PathUtils.GetAbsoluteDirectoryPath(root, "SearchPath1") + Environment.NewLine + PathUtils.GetAbsoluteDirectoryPath(root, "SearchPath2");
                settings.ProjectPath = PathUtils.GetAbsoluteFilePath(root, @"TestDestination\Subdirectory\ProjectName.pyproj");

                string path = CreateRequestedProject(settings);

                Assert.AreEqual(settings.ProjectPath, path);
                var proj = XDocument.Load(path);

                Assert.AreEqual("..\\..\\HelloWorld\\", proj.Descendant("ProjectHome").Value);
                Assert.AreEqual("..\\SearchPath1\\;..\\SearchPath2\\", proj.Descendant("SearchPath").Value);
                AssertUtil.ContainsExactly(proj.Descendants(proj.GetName("Compile")).Select(x => x.Attribute("Include").Value),
                    "Program.py");
                Assert.AreEqual(0, proj.Descendants(proj.GetName("Content")).Count());
            }
        }

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
        public void ImportWizardFolders() {
            using (var wpf = new WpfProxy()) {
                var root = TestData.GetTempPath();
                FileUtils.CopyDirectory(TestData.GetPath(@"TestData\HelloWorld2"), Path.Combine(root, "HelloWorld2"));

                var settings = wpf.Create(() => new ImportSettings(null, null));
                settings.SourcePath = PathUtils.GetAbsoluteDirectoryPath(root, "HelloWorld2");
                settings.Filters = "*";
                settings.ProjectPath = PathUtils.GetAbsoluteFilePath(root, @"TestDestination\Subdirectory\ProjectName.pyproj");

                string path = CreateRequestedProject(settings);

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

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
        public void ImportWizardInterpreter() {
            using (var wpf = new WpfProxy()) {
                var root = TestData.GetTempPath();
                FileUtils.CopyDirectory(TestData.GetPath(@"TestData\HelloWorld"), Path.Combine(root, "HelloWorld"));

                var settings = wpf.Create(() => new ImportSettings(null, null));
                settings.SourcePath = PathUtils.GetAbsoluteDirectoryPath(root, "HelloWorld");
                settings.Filters = "*.py;*.pyproj";
                settings.ProjectPath = PathUtils.GetAbsoluteFilePath(root, @"TestDestination\Subdirectory\ProjectName.pyproj");

                var interpreter = new PythonInterpreterView("Test", "Test|Blah", null);
                settings.Dispatcher.Invoke((Action)(() => settings.AvailableInterpreters.Add(interpreter)));
                //settings.AddAvailableInterpreter(interpreter);
                settings.SelectedInterpreter = interpreter;

                string path = CreateRequestedProject(settings);

                Assert.AreEqual(settings.ProjectPath, path);
                var proj = XDocument.Load(path);

                Assert.AreEqual(interpreter.Id, proj.Descendant("InterpreterId").Value);

                var interp = proj.Descendant("InterpreterReference");
                Assert.AreEqual(string.Format("{0}", interpreter.Id),
                    interp.Attribute("Include").Value);
            }
        }

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
        public void ImportWizardStartupFile() {
            using (var wpf = new WpfProxy()) {
                var root = TestData.GetTempPath();
                FileUtils.CopyDirectory(TestData.GetPath(@"TestData\HelloWorld"), Path.Combine(root, "HelloWorld"));

                var settings = wpf.Create(() => new ImportSettings(null, null));
                settings.SourcePath = PathUtils.GetAbsoluteDirectoryPath(root, "HelloWorld");
                settings.Filters = "*.py;*.pyproj";
                settings.StartupFile = "Program.py";
                settings.ProjectPath = PathUtils.GetAbsoluteFilePath(root, @"TestDestination\Subdirectory\ProjectName.pyproj");

                string path = CreateRequestedProject(settings);

                Assert.AreEqual(settings.ProjectPath, path);
                var proj = XDocument.Load(path);

                Assert.AreEqual("Program.py", proj.Descendant("StartupFile").Value);
            }
        }

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
        public void ImportWizardSemicolons() {
            // https://pytools.codeplex.com/workitem/2022
            using (var wpf = new WpfProxy()) {
                var settings = wpf.Create(() => new ImportSettings(null, null));
                var sourcePath = TestData.GetTempPath();
                // Create a fake set of files to import
                Directory.CreateDirectory(Path.Combine(sourcePath, "ABC"));
                File.WriteAllText(Path.Combine(sourcePath, "ABC", "a;b;c.py"), "");
                Directory.CreateDirectory(Path.Combine(sourcePath, "A;B;C"));
                File.WriteAllText(Path.Combine(sourcePath, "A;B;C", "abc.py"), "");

                settings.SourcePath = sourcePath;

                string path = CreateRequestedProject(settings);

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
                new MockPythonInterpreterFactory(python.Configuration)
            ));

            using (var wpf = new WpfProxy()) {
                var settings = wpf.Create(() => new ImportSettings(null, mockService));
                var sourcePath = TestData.GetTempPath();
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
                    Console.WriteLine(PathUtils.GetRelativeFilePath(sourcePath, f));
                }

                Assert.IsTrue(
                    File.Exists(Path.Combine(sourcePath, "env", expectedFile)),
                    "Virtualenv was not created correctly"
                );

                settings.SourcePath = sourcePath;

                string path = CreateRequestedProject(settings);

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

                var env = proj.Descendants(proj.GetName("Interpreter")).SingleOrDefault();
                if (brokenBaseInterpreter) {
                    Assert.IsNull(env);
                } else {
                    Assert.AreEqual("env\\", env.Attribute("Include").Value);
                    Assert.AreNotEqual("", env.Descendant("Id").Value);
                    Assert.AreEqual(string.Format("env ({0})", python.Configuration.Description), env.Descendant("Description").Value);
                    Assert.AreEqual("scripts\\python.exe", env.Descendant("InterpreterPath").Value, true);
                    Assert.AreEqual("scripts\\pythonw.exe", env.Descendant("WindowsInterpreterPath").Value, true);
                    Assert.AreEqual("PYTHONPATH", env.Descendant("PathEnvironmentVariable").Value, true);
                    Assert.AreEqual(python.Configuration.Version.ToString(), env.Descendant("Version").Value, true);
                    Assert.AreEqual(python.Configuration.Architecture.ToString("X"), env.Descendant("Architecture").Value, true);
                }
            }
        }

        [TestMethod, Priority(TestExtensions.P2_UNIT_TEST)]
        public void ImportWizardVirtualEnv() {
            var python = PythonPaths.Versions.LastOrDefault(pv =>
                pv.IsCPython &&
                File.Exists(Path.Combine(pv.PrefixPath, "Lib", "site-packages", "virtualenv.py")) &&
                // CPython 3.3.4 does not work correctly with virtualenv, so
                // skip testing on 3.3 to avoid false failures
                pv.Version != PythonLanguageVersion.V33
            );

            ImportWizardVirtualEnvWorker(python, "virtualenv", "lib\\orig-prefix.txt", false);
        }

        [TestMethod, Priority(TestExtensions.P2_UNIT_TEST)]
        public void ImportWizardVEnv() {
            var python = PythonPaths.Versions.LastOrDefault(pv =>
                pv.IsCPython && File.Exists(Path.Combine(pv.PrefixPath, "Lib", "venv", "__main__.py"))
            );

            ImportWizardVirtualEnvWorker(python, "venv", "pyvenv.cfg", false);
        }

        [TestMethod, Priority(TestExtensions.P2_UNIT_TEST)]
        [TestCategory("10s")]
        public void ImportWizardBrokenVirtualEnv() {
            var python = PythonPaths.Versions.LastOrDefault(pv =>
                pv.IsCPython &&
                File.Exists(Path.Combine(pv.PrefixPath, "Lib", "site-packages", "virtualenv.py")) &&
                // CPython 3.3.4 does not work correctly with virtualenv, so
                // skip testing on 3.3 to avoid false failures
                pv.Version != PythonLanguageVersion.V33
            );

            ImportWizardVirtualEnvWorker(python, "virtualenv", "lib\\orig-prefix.txt", true);
        }

        [TestMethod, Priority(TestExtensions.P2_UNIT_TEST)]
        [TestCategory("10s")]
        public void ImportWizardBrokenVEnv() {
            var python = PythonPaths.Versions.LastOrDefault(pv =>
                pv.IsCPython && File.Exists(Path.Combine(pv.PrefixPath, "Lib", "venv", "__main__.py"))
            );

            ImportWizardVirtualEnvWorker(python, "venv", "pyvenv.cfg", true);
        }

        private static void ImportWizardCustomizationsWorker(ProjectCustomization customization, Action<XDocument> verify) {
            using (var wpf = new WpfProxy()) {
                var settings = wpf.Create(() => new ImportSettings(null, null));
                settings.SourcePath = TestData.GetPath("TestData\\HelloWorld\\");
                settings.Filters = "*.py;*.pyproj";
                settings.StartupFile = "Program.py";
                settings.UseCustomization = true;
                settings.Customization = customization;
                settings.ProjectPath = Path.Combine(TestData.GetTempPath("ImportWizardCustomizations_" + customization.GetType().Name), "Project.pyproj");
                Directory.CreateDirectory(Path.GetDirectoryName(settings.ProjectPath));

                string path = CreateRequestedProject(settings);

                Assert.AreEqual(settings.ProjectPath, path);
                Console.WriteLine(File.ReadAllText(path));
                var proj = XDocument.Load(path);

                verify(proj);
            }
        }

        [TestMethod, Priority(TestExtensions.CORE_UNIT_TEST)]
        public void ImportWizardCustomizations() {
            ImportWizardCustomizationsWorker(DefaultProjectCustomization.Instance, proj => {
                Assert.AreEqual("Program.py", proj.Descendant("StartupFile").Value);
                Assert.IsTrue(proj.Descendants(proj.GetName("Import")).Any(d => d.Attribute("Project").Value == @"$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\Python Tools\Microsoft.PythonTools.targets"));
                Assert.AreEqual(0, proj.Descendants("UseCustomServer").Count());
            });
            ImportWizardCustomizationsWorker(BottleProjectCustomization.Instance, proj => {
                Assert.AreNotEqual(-1, proj.Descendant("ProjectTypeGuids").Value.IndexOf("e614c764-6d9e-4607-9337-b7073809a0bd", StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(proj.Descendants(proj.GetName("Import")).Any(d => d.Attribute("Project").Value == @"$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\Python Tools\Microsoft.PythonTools.Web.targets"));
                Assert.AreEqual("Web launcher", proj.Descendant("LaunchProvider").Value);
                Assert.AreEqual("True", proj.Descendant("UseCustomServer").Value);
            });
            ImportWizardCustomizationsWorker(DjangoProjectCustomization.Instance, proj => {
                Assert.AreNotEqual(-1, proj.Descendant("ProjectTypeGuids").Value.IndexOf("5F0BE9CA-D677-4A4D-8806-6076C0FAAD37", StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(proj.Descendants(proj.GetName("Import")).Any(d => d.Attribute("Project").Value == @"$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\Python Tools\Microsoft.PythonTools.Django.targets"));
                Assert.AreEqual("Django launcher", proj.Descendant("LaunchProvider").Value);
                Assert.AreEqual("True", proj.Descendant("UseCustomServer").Value);
            });
            ImportWizardCustomizationsWorker(FlaskProjectCustomization.Instance, proj => {
                Assert.AreNotEqual(-1, proj.Descendant("ProjectTypeGuids").Value.IndexOf("789894c7-04a9-4a11-a6b5-3f4435165112", StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(proj.Descendants(proj.GetName("Import")).Any(d => d.Attribute("Project").Value == @"$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\Python Tools\Microsoft.PythonTools.Web.targets"));
                Assert.AreEqual("Web launcher", proj.Descendant("LaunchProvider").Value);
                Assert.AreEqual("True", proj.Descendant("UseCustomServer").Value);
            });
        }


        static T Wait<T>(Task<T> task) {
            task.Wait();
            return task.Result;
        }

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
        public void ImportWizardCandidateStartupFiles() {
            var sourcePath = TestData.GetTempPath();
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

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
        public void ImportWizardDefaultStartupFile() {
            var files = new[] { "a.py", "b.py", "c.py" };
            var expectedDefault = files[0];

            Assert.AreEqual(expectedDefault, ImportSettings.SelectDefaultStartupFile(files, null));
            Assert.AreEqual(expectedDefault, ImportSettings.SelectDefaultStartupFile(files, "not in list"));
            Assert.AreEqual("b.py", ImportSettings.SelectDefaultStartupFile(files, "b.py"));
        }
    }
}

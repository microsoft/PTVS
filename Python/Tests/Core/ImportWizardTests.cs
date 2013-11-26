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
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Xml.Linq;
using Microsoft.PythonTools;
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
            PythonTestData.Deploy();
        }

        #region ImportSettingsProxy class

        sealed class ImportSettingsProxy : IDisposable {
            ImportSettings settings;
            readonly Thread controller;
            readonly AutoResetEvent ready;

            public ImportSettingsProxy() {
                ready = new AutoResetEvent(false);
                controller = new Thread(ControllerThread);
                controller.Start();
                ready.WaitOne();
            }

            public void Dispose() {
                GC.SuppressFinalize(this);
                settings.Dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Normal);
                controller.Join();
                ready.Dispose();
            }

            ~ImportSettingsProxy() {
                Dispose();
            }

            private void ControllerThread(object obj) {
                settings = new ImportSettings();
                ready.Set();
                Dispatcher.Run();
            }

            public string CreateRequestedProject() {
                string result = null;
                ready.Reset();
                settings.Dispatcher.BeginInvoke((Action)(async () => {
                    result = await settings.CreateRequestedProjectAsync();
                    ready.Set();
                }));
                ready.WaitOne();
                return result;
            }

            private T GetValue<T>(DependencyProperty prop) {
                return settings.Dispatcher.Invoke((Func<T>)(() => (T)settings.GetValue(prop)));
            }

            private void SetValue<T>(DependencyProperty prop, T value) {
                settings.Dispatcher.Invoke((Action)(() => settings.SetCurrentValue(prop, (object)value)));
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
                settings.Dispatcher.Invoke((Action)(() => settings.AvailableInterpreters.Add(interpreter)));
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

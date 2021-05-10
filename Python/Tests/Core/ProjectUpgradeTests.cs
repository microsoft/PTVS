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
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Mocks;
using TestUtilities.Python;

namespace PythonToolsTests {
    [TestClass]
    public class ProjectUpgradeTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void UpgradeCheckToolsVersion() {
            var factory = new PythonProjectFactory(null);
            var sp = new MockServiceProvider();
            sp.Services[typeof(SVsQueryEditQuerySave).GUID] = null;
            sp.Services[typeof(SVsActivityLog).GUID] = new MockActivityLog();
            factory.Site = sp;

            var upgrade = (IVsProjectUpgradeViaFactory)factory;
            foreach (var testCase in new[] {
                new { Name = "NoToolsVersion.pyproj", Expected = 1 },
                new { Name = "OldToolsVersion.pyproj", Expected = 1 },
                new { Name = "CorrectToolsVersion.pyproj", Expected = 0 },
                new { Name = "NewerToolsVersion.pyproj", Expected = 0 }
            }) {
                int actual;
                Guid factoryGuid;
                uint flags;
                var hr = upgrade.UpgradeProject_CheckOnly(
                    TestData.GetPath(Path.Combine("TestData", "ProjectUpgrade", testCase.Name)),
                    null,
                    out actual,
                    out factoryGuid,
                    out flags
                );

                Assert.AreEqual(0, hr, string.Format("Wrong HR for {0}", testCase.Name));
                Assert.AreEqual(testCase.Expected, actual, string.Format("Wrong result for {0}", testCase.Name));
                Assert.AreEqual(Guid.Empty, factoryGuid);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void UpgradeToolsVersion() {
            var factory = new PythonProjectFactory(null);
            var sp = new MockServiceProvider();
            sp.Services[typeof(SVsQueryEditQuerySave).GUID] = null;
            sp.Services[typeof(SVsActivityLog).GUID] = new MockActivityLog();
            factory.Site = sp;

            var upgrade = (IVsProjectUpgradeViaFactory)factory;
            foreach (var testCase in new[] {
                new { Name = "NoToolsVersion.pyproj", Expected = 1 },
                new { Name = "OldToolsVersion.pyproj", Expected = 1 },
                new { Name = "CorrectToolsVersion.pyproj", Expected = 0 },
                new { Name = "NewerToolsVersion.pyproj", Expected = 0 }
            }) {
                int actual;
                Guid factoryGuid;
                string newLocation;

                // Use a copy of the project so we don't interfere with other
                // tests using them.
                var origProject = TestData.GetPath("TestData", "ProjectUpgrade", testCase.Name);
                var tempProject = Path.Combine(TestData.GetTempPath(), testCase.Name);
                File.Copy(origProject, tempProject);

                var hr = upgrade.UpgradeProject(
                    tempProject,
                    0u,  // no backups
                    null,
                    out newLocation,
                    null,
                    out actual,
                    out factoryGuid
                );

                Assert.AreEqual(0, hr, string.Format("Wrong HR for {0}", testCase.Name));
                Assert.AreEqual(testCase.Expected, actual, string.Format("Wrong result for {0}", testCase.Name));
                Assert.AreEqual(tempProject, newLocation, string.Format("Wrong location for {0}", testCase.Name));
                if (testCase.Expected != 0) {
                    Assert.IsTrue(
                        File.ReadAllText(tempProject).Contains("ToolsVersion=\"" + PythonProjectFactory.ToolsVersion + "\""),
                        string.Format("Upgraded {0} did not contain ToolsVersion=\"" + PythonProjectFactory.ToolsVersion + "\"", testCase.Name)
                    );
                } else {
                    Assert.IsTrue(
                        File.ReadAllText(tempProject) == File.ReadAllText(origProject),
                        string.Format("Non-upgraded {0} has different content to original", testCase.Name)
                    );
                }
                Assert.AreEqual(Guid.Empty, factoryGuid);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void UpgradeCheckUserToolsVersion() {
            var factory = new PythonProjectFactory(null);
            var sp = new MockServiceProvider();
            sp.Services[typeof(SVsQueryEditQuerySave).GUID] = null;
            sp.Services[typeof(SVsActivityLog).GUID] = new MockActivityLog();
            factory.Site = sp;

            var projectFile = TestData.GetPath(Path.Combine("TestData", "ProjectUpgrade", "CorrectToolsVersion.pyproj"));

            var upgrade = (IVsProjectUpgradeViaFactory)factory;

            foreach (var testCase in new[] {
                new { Name = "12.0", Expected = 0 },
                new { Name = "4.0", Expected = 0 }
            }) {
                int actual;
                int hr;
                Guid factoryGuid;
                uint flags;

                var xml = Microsoft.Build.Construction.ProjectRootElement.Create();
                xml.ToolsVersion = testCase.Name;
                xml.Save(projectFile + ".user");

                try {
                    hr = upgrade.UpgradeProject_CheckOnly(
                        projectFile,
                        null,
                        out actual,
                        out factoryGuid,
                        out flags
                    );
                } finally {
                    File.Delete(projectFile + ".user");
                }

                Assert.AreEqual(0, hr, string.Format("Wrong HR for ToolsVersion={0}", testCase.Name));
                Assert.AreEqual(testCase.Expected, actual, string.Format("Wrong result for ToolsVersion={0}", testCase.Name));
                Assert.AreEqual(Guid.Empty, factoryGuid);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void OldWebProjectUpgrade() {
            // PTVS 2.1 Beta 1 shipped with separate .targets files for Bottle
            // and Flask. In PTVS 2.1 Beta 2 these were removed. This test
            // ensures that we upgrade projects created in 2.1 Beta 1.
            var factory = new PythonProjectFactory(null);
            var sp = new MockServiceProvider();
            sp.Services[typeof(SVsQueryEditQuerySave).GUID] = null;
            sp.Services[typeof(SVsActivityLog).GUID] = new MockActivityLog();
            factory.Site = sp;

            var upgrade = (IVsProjectUpgradeViaFactory)factory;
            foreach (var testCase in new[] {
                new { Name = "OldBottleProject.pyproj", Expected = 1 },
                new { Name = "OldFlaskProject.pyproj", Expected = 1 }
            }) {
                int actual;
                Guid factoryGuid;
                string newLocation;

                // Use a copy of the project so we don't interfere with other
                // tests using them.
                var project = TestData.GetPath("TestData", "ProjectUpgrade", testCase.Name);
                using (FileUtils.Backup(project)) {
                    var origText = File.ReadAllText(project);
                    var hr = upgrade.UpgradeProject(
                        project,
                        0u,  // no backups
                        null,
                        out newLocation,
                        null,
                        out actual,
                        out factoryGuid
                    );

                    Assert.AreEqual(0, hr, string.Format("Wrong HR for {0}", testCase.Name));
                    Assert.AreEqual(testCase.Expected, actual, string.Format("Wrong result for {0}", testCase.Name));
                    Assert.AreEqual(project, newLocation, string.Format("Wrong location for {0}", testCase.Name));
                    var text = File.ReadAllText(project);
                    if (testCase.Expected != 0) {
                        Assert.IsFalse(
                            text.Contains("<Import Project=\"$(VSToolsPath)"),
                            string.Format("Upgraded {0} should not import from $(VSToolsPath)", testCase.Name)
                        );
                        Assert.IsTrue(
                            text.Contains("Microsoft.PythonTools.Web.targets"),
                            string.Format("Upgraded {0} should import Web.targets", testCase.Name)
                        );
                        Assert.IsTrue(
                            text.Contains("<PythonWsgiHandler>"),
                            string.Format("Upgraded {0} should contain <PythonWsgiHandler>", testCase.Name)
                        );
                    } else {
                        Assert.IsTrue(
                            text == origText,
                            string.Format("Non-upgraded {0} has different content to original", testCase.Name)
                        );
                    }
                    Assert.AreEqual(Guid.Empty, factoryGuid);
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void CommonPropsProjectUpgrade() {
            var factory = new PythonProjectFactory(null);
            var sp = new MockServiceProvider();
            sp.Services[typeof(SVsQueryEditQuerySave).GUID] = null;
            sp.Services[typeof(SVsActivityLog).GUID] = new MockActivityLog();
            factory.Site = sp;

            var upgrade = (IVsProjectUpgradeViaFactory)factory;
            var project = TestData.GetPath("TestData\\ProjectUpgrade\\OldCommonProps.pyproj");
            using (FileUtils.Backup(project)) {
                int actual;
                Guid factoryGuid;
                string newLocation;

                var hr = upgrade.UpgradeProject(
                    project,
                    0u,  // no backups
                    null,
                    out newLocation,
                    null,
                    out actual,
                    out factoryGuid
                );

                Assert.AreEqual(0, hr, string.Format("Wrong HR for OldCommonProps.pyproj"));
                Assert.AreEqual(1, actual, string.Format("Wrong result for OldCommonProps.pyproj"));
                Assert.AreEqual(project, newLocation, string.Format("Wrong location for OldCommonProps.pyproj"));

                Assert.IsFalse(
                    File.ReadAllText(project).Contains("<Import Project=\"" + PythonProjectFactory.CommonProps),
                    string.Format("Upgraded OldCommonProps.pyproj should not import from $(VSToolsPath)")
                );
                Assert.AreEqual(Guid.Empty, factoryGuid);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void CommonTargetsProjectUpgrade() {
            var factory = new PythonProjectFactory(null);
            var sp = new MockServiceProvider();
            sp.Services[typeof(SVsQueryEditQuerySave).GUID] = null;
            sp.Services[typeof(SVsActivityLog).GUID] = new MockActivityLog();
            factory.Site = sp;

            var upgrade = (IVsProjectUpgradeViaFactory)factory;
            var project = TestData.GetPath("TestData\\ProjectUpgrade\\OldCommonTargets.pyproj");
            using (FileUtils.Backup(project)) {
                int actual;
                Guid factoryGuid;
                string newLocation;

                var hr = upgrade.UpgradeProject(
                    project,
                    0u,  // no backups
                    null,
                    out newLocation,
                    null,
                    out actual,
                    out factoryGuid
                );

                Assert.AreEqual(0, hr, string.Format("Wrong HR for OldCommonTargets.pyproj"));
                Assert.AreEqual(1, actual, string.Format("Wrong result for OldCommonTargets.pyproj"));
                Assert.AreEqual(project, newLocation, string.Format("Wrong location for OldCommonTargets.pyproj"));

                var text = File.ReadAllText(project);
                Assert.IsFalse(
                    text.Contains("<PtvsTargetsFile>" + PythonProjectFactory.PtvsTargets),
                    "Upgraded OldCommonTargets.pyproj should not define $(PtvsTargetsFile)"
                );
                Assert.IsTrue(
                    text.Contains("<Import Project=\"" + PythonProjectFactory.PtvsTargets + "\""),
                    "Upgraded OldCommonTargets.pyproj should import the Python targets directly"
                );
                Assert.AreEqual(
                    1, text.FindIndexesOf(PythonProjectFactory.PtvsTargets).Count(),
                    "Expected only one import of Python targets file"
                );
                Assert.AreEqual(Guid.Empty, factoryGuid);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void PythonTargetsProjectUpgrade() {
            var factory = new PythonProjectFactory(null);
            var sp = new MockServiceProvider();
            sp.Services[typeof(SVsQueryEditQuerySave).GUID] = null;
            sp.Services[typeof(SVsActivityLog).GUID] = new MockActivityLog();
            factory.Site = sp;

            var upgrade = (IVsProjectUpgradeViaFactory)factory;
            var project = TestData.GetPath("TestData\\ProjectUpgrade\\OldPythonTargets.pyproj");
            using (FileUtils.Backup(project)) {
                int actual;
                Guid factoryGuid;
                string newLocation;

                var hr = upgrade.UpgradeProject(
                    project,
                    0u,  // no backups
                    null,
                    out newLocation,
                    null,
                    out actual,
                    out factoryGuid
                );

                Assert.AreEqual(0, hr, string.Format("Wrong HR for OldPythonTargets.pyproj"));
                Assert.AreEqual(1, actual, string.Format("Wrong result for OldPythonTargets.pyproj"));
                Assert.AreEqual(project, newLocation, string.Format("Wrong location for OldPythonTargets.pyproj"));

                var text = File.ReadAllText(project);
                Assert.IsFalse(
                    text.Contains("<PtvsTargetsFile>"),
                    string.Format("Upgraded OldPythonTargets.pyproj should not define $(PtvsTargetsFile)")
                );
                Assert.IsTrue(
                    text.Contains("<Import Project=\"" + PythonProjectFactory.PtvsTargets + "\""),
                    string.Format("Upgraded OldPythonTargets.pyproj should import the Python targets directly")
                );
                Assert.AreEqual(
                    1, text.FindIndexesOf(PythonProjectFactory.PtvsTargets).Count(),
                    "Expected only one import of Python targets file"
                );
                Assert.AreEqual(Guid.Empty, factoryGuid);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void InterpreterIdUpgrade() {
            // PTVS 3.0 changed interpreter ID format.
            var factory = new PythonProjectFactory(null);
            var sp = new MockServiceProvider();
            sp.Services[typeof(SVsQueryEditQuerySave).GUID] = null;
            sp.Services[typeof(SVsActivityLog).GUID] = new MockActivityLog();
            factory.Site = sp;

            var upgrade = (IVsProjectUpgradeViaFactory)factory;
            foreach (var testCase in new[] {
                new { Name = "CPythonInterpreterId.pyproj", Expected = 1, Id = "Global|PythonCore|2.7-32" },
                new { Name = "CPython35InterpreterId.pyproj", Expected = 1, Id = "Global|PythonCore|3.5-32" },
                new { Name = "CPythonx64InterpreterId.pyproj", Expected = 1, Id = "Global|PythonCore|3.5" },
                new { Name = "MSBuildInterpreterId.pyproj", Expected = 1, Id = "MSBuild|env|$(MSBuildProjectFullPath)" },
                new { Name = "UnknownInterpreterId.pyproj", Expected = 1, Id = (string)null },
            }) {
                int actual;
                Guid factoryGuid;
                string newLocation;

                var project = TestData.GetPath("TestData\\ProjectUpgrade\\" + testCase.Name);
                using (FileUtils.Backup(project)) {

                    var hr = upgrade.UpgradeProject(
                        project,
                        0u,  // no backups
                        null,
                        out newLocation,
                        null,
                        out actual,
                        out factoryGuid
                    );

                    Assert.AreEqual(0, hr, string.Format("Wrong HR for {0}", testCase.Name));
                    Assert.AreEqual(testCase.Expected, actual, string.Format("Wrong result for {0}", testCase.Name));
                    Assert.AreEqual(project, newLocation, string.Format("Wrong location for {0}", testCase.Name));

                    var content = File.ReadAllText(project);
                    if (testCase.Id == null) {
                        Assert.IsFalse(content.Contains("<InterpreterId>"), "Found <InterpreterId> in " + content);
                    } else {
                        AssertUtil.Contains(content, "<InterpreterId>{0}</InterpreterId>".FormatInvariant(testCase.Id));
                    }
                    Assert.AreEqual(
                        1, content.FindIndexesOf(PythonProjectFactory.PtvsTargets).Count(),
                        "Expected only one import of Python targets file"
                    );
                    Assert.AreEqual(Guid.Empty, factoryGuid);
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void InterpreterReferenceUpgrade() {
            // PTVS 3.0 changed interpreter ID format.
            var factory = new PythonProjectFactory(null);
            var sp = new MockServiceProvider();
            sp.Services[typeof(SVsQueryEditQuerySave).GUID] = null;
            sp.Services[typeof(SVsActivityLog).GUID] = new MockActivityLog();
            factory.Site = sp;

            var upgrade = (IVsProjectUpgradeViaFactory)factory;
            foreach (var testCase in new[] {
                new { Name = "CPythonInterpreterReference.pyproj", Expected = 1, Id = "Global|PythonCore|3.5-32" },
                new { Name = "UnknownInterpreterReference.pyproj", Expected = 1, Id = (string)null },
            }) {
                int actual;
                Guid factoryGuid;
                string newLocation;

                var project = TestData.GetPath("TestData\\ProjectUpgrade\\" + testCase.Name);
                using (FileUtils.Backup(project)) {

                    var hr = upgrade.UpgradeProject(
                        project,
                        0u,  // no backups
                        null,
                        out newLocation,
                        null,
                        out actual,
                        out factoryGuid
                    );

                    Assert.AreEqual(0, hr, string.Format("Wrong HR for {0}", testCase.Name));
                    Assert.AreEqual(testCase.Expected, actual, string.Format("Wrong result for {0}", testCase.Name));
                    Assert.AreEqual(project, newLocation, string.Format("Wrong location for {0}", testCase.Name));

                    var content = File.ReadAllText(project);
                    if (testCase.Id == null) {
                        Assert.IsFalse(content.Contains("<InterpreterReference "), "Found <InterpreterReference> in " + content);
                    } else {
                        AssertUtil.Contains(content, "<InterpreterReference Include=\"{0}\" />".FormatInvariant(testCase.Id));
                    }
                    Assert.AreEqual(Guid.Empty, factoryGuid);
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void BaseInterpreterUpgrade() {
            // PTVS 3.0 changed interpreter ID format.
            var factory = new PythonProjectFactory(null);
            var sp = new MockServiceProvider();
            sp.Services[typeof(SVsQueryEditQuerySave).GUID] = null;
            sp.Services[typeof(SVsActivityLog).GUID] = new MockActivityLog();
            factory.Site = sp;

            var upgrade = (IVsProjectUpgradeViaFactory)factory;
            foreach (var testCase in new[] {
                new { Name = "CPythonBaseInterpreter.pyproj", Expected = 1, Id = "Global|PythonCore|3.4-32" },
            }) {
                int actual;
                Guid factoryGuid;
                string newLocation;

                var project = TestData.GetPath("TestData\\ProjectUpgrade\\" + testCase.Name);
                using (FileUtils.Backup(project)) {

                    var hr = upgrade.UpgradeProject(
                        project,
                        0u,  // no backups
                        null,
                        out newLocation,
                        null,
                        out actual,
                        out factoryGuid
                    );

                    Assert.AreEqual(0, hr, string.Format("Wrong HR for {0}", testCase.Name));
                    Assert.AreEqual(testCase.Expected, actual, string.Format("Wrong result for {0}", testCase.Name));
                    Assert.AreEqual(project, newLocation, string.Format("Wrong location for {0}", testCase.Name));

                    Assert.IsFalse(
                        File.ReadAllText(project).Contains("<BaseInterpreter>"),
                        "Project should not contain <BaseInterpreter> element"
                    );
                    Assert.AreEqual(Guid.Empty, factoryGuid);
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void WebBrowserUrlUpgrade() {
            // PTVS 3.0 changed interpreter ID format.
            var factory = new PythonProjectFactory(null);
            var sp = new MockServiceProvider();
            sp.Services[typeof(SVsQueryEditQuerySave).GUID] = null;
            sp.Services[typeof(SVsActivityLog).GUID] = new MockActivityLog();
            factory.Site = sp;

            var upgrade = (IVsProjectUpgradeViaFactory)factory;
            foreach (var testCase in new[] {
                new { Name = "NoWebBrowserUrl.pyproj", Expected = 1 },
                new { Name = "HasWebBrowserUrl.pyproj", Expected = 0 },
            }) {
                int actual;
                Guid factoryGuid;
                string newLocation;

                var project = TestData.GetPath("TestData\\ProjectUpgrade\\" + testCase.Name);
                using (FileUtils.Backup(project)) {

                    var hr = upgrade.UpgradeProject(
                        project,
                        0u,  // no backups
                        null,
                        out newLocation,
                        null,
                        out actual,
                        out factoryGuid
                    );

                    Assert.AreEqual(0, hr, string.Format("Wrong HR for {0}", testCase.Name));
                    Assert.AreEqual(testCase.Expected, actual, string.Format("Wrong result for {0}", testCase.Name));
                    Assert.AreEqual(project, newLocation, string.Format("Wrong location for {0}", testCase.Name));
                    Console.WriteLine(File.ReadAllText(project));

                    if (testCase.Expected != 0) {
                        AssertUtil.Contains(
                            File.ReadAllText(project),
                            "<WebBrowserUrl>http://localhost</WebBrowserUrl>"
                        );
                    }
                    Assert.AreEqual(Guid.Empty, factoryGuid);
                }
            }
        }
    }
}
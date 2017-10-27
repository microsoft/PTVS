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
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;

namespace PythonToolsTests {
    [TestClass]
    public class PythonProjectTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        public TestContext TestContext { get; set; }

        [TestMethod, Priority(0)]
        public void MergeRequirements() {
            // Comments should be preserved, only package specs should change.
            AssertUtil.AreEqual(
                PythonProjectNode.MergeRequirements(new[] {
                    "a # with a comment",
                    "B==0.2",
                    "# just a comment B==01234",
                    "",
                    "x < 1",
                    "d==1.0 e==2.0 f==3.0"
                }, new[] {
                    "b==0.1",
                    "a==0.2",
                    "c==0.3",
                    "e==4.0",
                    "x==0.8"
                }.Select(p => PackageSpec.FromRequirement(p)), false),
                "a==0.2 # with a comment",
                "b==0.1",
                "# just a comment B==01234",
                "",
                "x==0.8",
                "d==1.0 e==4.0 f==3.0"
            );

            // addNew is true, so the c==0.3 should be added.
            AssertUtil.AreEqual(
                PythonProjectNode.MergeRequirements(new[] {
                    "a # with a comment",
                    "b==0.2",
                    "# just a comment B==01234"
                }, new[] {
                    "B==0.1",   // case is updated
                    "a==0.2",
                    "c==0.3"
                }.Select(p => PackageSpec.FromRequirement(p)), true),
                "a==0.2 # with a comment",
                "B==0.1",
                "# just a comment B==01234",
                "c==0.3"
            );

            // No existing entries, so the new ones are sorted and returned.
            AssertUtil.AreEqual(
                PythonProjectNode.MergeRequirements(null, new[] {
                    "b==0.2",
                    "a==0.1",
                    "c==0.3"
                }.Select(p => PackageSpec.FromRequirement(p)), false),
                "a==0.1",
                "b==0.2",
                "c==0.3"
            );

            // Check all the inequalities
            const string inequalities = "<=|>=|<|>|!=|==";
            AssertUtil.AreEqual(
                PythonProjectNode.MergeRequirements(
                    inequalities.Split('|').Select(s => "a " + s + " 1.2.3"),
                    new[] { "a==0" }.Select(p => PackageSpec.FromRequirement(p)),
                    false
                ),
                inequalities.Split('|').Select(_ => "a==0").ToArray()
            );
        }

        [TestMethod, Priority(0)]
        public void MergeRequirementsMismatchedCase() {
            AssertUtil.AreEqual(
                PythonProjectNode.MergeRequirements(new[] {
                    "aaaaaa==0.0",
                    "BbBbBb==0.1",
                    "CCCCCC==0.2"
                }, new[] {
                    "aaaAAA==0.1",
                    "bbbBBB==0.2",
                    "cccCCC==0.3"
                }.Select(p => PackageSpec.FromRequirement(p)), false),
                "aaaAAA==0.1",
                "bbbBBB==0.2",
                "cccCCC==0.3"
            );

            // https://pytools.codeplex.com/workitem/2465
            AssertUtil.AreEqual(
                PythonProjectNode.MergeRequirements(new[] {
                    "Flask==0.10.1",
                    "itsdangerous==0.24",
                    "Jinja2==2.7.3",
                    "MarkupSafe==0.23",
                    "Werkzeug==0.9.6"
                }, new[] {
                    "flask==0.10.1",
                    "itsdangerous==0.24",
                    "jinja2==2.7.3",
                    "markupsafe==0.23",
                    "werkzeug==0.9.6"
                }.Select(p => PackageSpec.FromRequirement(p)), false),
                "flask==0.10.1",
                "itsdangerous==0.24",
                "jinja2==2.7.3",
                "markupsafe==0.23",
                "werkzeug==0.9.6"
            );
        }

        [TestMethod, Priority(0)]
        public void FindRequirementsRegexTest() {
            var r = PythonProjectNode.FindRequirementRegex;
            AssertUtil.AreEqual(r.Matches("aaaa bbbb cccc").Cast<Match>().Select(m => m.Value),
                "aaaa",
                "bbbb",
                "cccc"
            );
            AssertUtil.AreEqual(r.Matches("aaaa#a\r\nbbbb#b\r\ncccc#c\r\n").Cast<Match>().Select(m => m.Value),
                "aaaa",
                "bbbb",
                "cccc"
            );

            AssertUtil.AreEqual(r.Matches("a==1 b!=2 c<=3").Cast<Match>().Select(m => m.Value),
                "a==1",
                "b!=2",
                "c<=3"
            );

            AssertUtil.AreEqual(r.Matches("a==1 b!=2 c<=3").Cast<Match>().Select(m => m.Groups["name"].Value),
                "a",
                "b",
                "c"
            );

            AssertUtil.AreEqual(r.Matches("a==1#a\r\nb!=2#b\r\nc<=3#c\r\n").Cast<Match>().Select(m => m.Value),
                "a==1",
                "b!=2",
                "c<=3"
            );

            AssertUtil.AreEqual(r.Matches("a == 1 b != 2 c <= 3").Cast<Match>().Select(m => m.Value),
                "a == 1",
                "b != 2",
                "c <= 3"
            );

            AssertUtil.AreEqual(r.Matches("a == 1 b != 2 c <= 3").Cast<Match>().Select(m => m.Groups["name"].Value),
                "a",
                "b",
                "c"
            );

            AssertUtil.AreEqual(r.Matches("a -u b -f:x c").Cast<Match>().Select(m => m.Groups["name"].Value),
                "a",
                "b",
                "c"
            );
        }

        [TestMethod, Priority(0)]
        public void UpdateWorkerRoleServiceDefinitionTest() {
            var doc = new XmlDocument();
            doc.LoadXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<ServiceDefinition name=""Azure1"" xmlns=""http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition"" schemaVersion=""2014-01.2.3"">
  <WorkerRole name=""PythonApplication1"" vmsize=""Small"" />
  <WebRole name=""PythonApplication2"" />
</ServiceDefinition>");

            PythonProjectNode.UpdateServiceDefinition(doc, "Worker", "PythonApplication1");

            AssertUtil.AreEqual(@"<?xml version=""1.0"" encoding=""utf-8""?>
<ServiceDefinition name=""Azure1"" xmlns=""http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition"" schemaVersion=""2014-01.2.3"">
  <WorkerRole name=""PythonApplication1"" vmsize=""Small"">
    <Startup>
      <Task commandLine=""bin\ps.cmd ConfigureCloudService.ps1"" executionContext=""elevated"" taskType=""simple"">
        <Environment>
          <Variable name=""EMULATED"">
            <RoleInstanceValue xpath=""/RoleEnvironment/Deployment/@emulated"" />
          </Variable>
        </Environment>
      </Task>
    </Startup>
    <Runtime>
      <Environment>
        <Variable name=""EMULATED"">
          <RoleInstanceValue xpath=""/RoleEnvironment/Deployment/@emulated"" />
        </Variable>
      </Environment>
      <EntryPoint>
        <ProgramEntryPoint commandLine=""bin\ps.cmd LaunchWorker.ps1 worker.py"" setReadyOnProcessStart=""true"" />
      </EntryPoint>
    </Runtime>
  </WorkerRole>
  <WebRole name=""PythonApplication2"" />
</ServiceDefinition>", doc);
        }

        [TestMethod, Priority(0)]
        public void UpdateWebRoleServiceDefinitionTest() {
            var doc = new XmlDocument();
            doc.LoadXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<ServiceDefinition name=""Azure1"" xmlns=""http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition"" schemaVersion=""2014-01.2.3"">
  <WorkerRole name=""PythonApplication1"" vmsize=""Small"" />
  <WebRole name=""PythonApplication2"" />
</ServiceDefinition>");

            PythonProjectNode.UpdateServiceDefinition(doc, "Web", "PythonApplication2");

            AssertUtil.AreEqual(@"<?xml version=""1.0"" encoding=""utf-8""?>
<ServiceDefinition name=""Azure1"" xmlns=""http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition"" schemaVersion=""2014-01.2.3"">
  <WorkerRole name=""PythonApplication1"" vmsize=""Small"" />
  <WebRole name=""PythonApplication2"">
    <Startup>
      <Task commandLine=""ps.cmd ConfigureCloudService.ps1"" executionContext=""elevated"" taskType=""simple"">
        <Environment>
          <Variable name=""EMULATED"">
            <RoleInstanceValue xpath=""/RoleEnvironment/Deployment/@emulated"" />
          </Variable>
        </Environment>
      </Task>
    </Startup>
  </WebRole>
</ServiceDefinition>", doc);
        }

        [TestMethod, Priority(0)]
        public async Task LoadAndUnloadModule() {
            var factories = new[] { InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(new Version(3, 3)) };
            var services = PythonToolsTestUtilities.CreateMockServiceProvider().GetEditorServices();
            using (var analyzer = await VsProjectAnalyzer.CreateForTestsAsync(services, factories[0])) {
                var m1Path = TestData.GetPath("TestData\\SimpleImport\\module1.py");
                var m2Path = TestData.GetPath("TestData\\SimpleImport\\module2.py");

                var entry1 = await analyzer.AnalyzeFileAsync(m1Path);
                var entry2 = await analyzer.AnalyzeFileAsync(m2Path);

                var cancel = CancellationTokens.After60s;
                analyzer.WaitForCompleteAnalysis(_ => !cancel.IsCancellationRequested);
                cancel.ThrowIfCancellationRequested();

                var loc = new Microsoft.PythonTools.Parsing.SourceLocation(1, 1);
                AssertUtil.ContainsExactly(
                    analyzer.GetEntriesThatImportModuleAsync("module1", true).Result.Select(m => m.moduleName),
                    "module2"
                );

                AssertUtil.ContainsExactly(
                    analyzer.GetValueDescriptions(entry2, "x", loc),
                    "int"
                );

                await analyzer.UnloadFileAsync(entry1);
                analyzer.WaitForCompleteAnalysis(_ => true);

                // Even though module1 has been unloaded, we still know that
                // module2 imports it.
                AssertUtil.ContainsExactly(
                    analyzer.GetEntriesThatImportModuleAsync("module1", true).Result.Select(m => m.moduleName),
                    "module2"
                );

                AssertUtil.ContainsExactly(
                    analyzer.GetValueDescriptions(entry2, "x", loc)
                );

                await analyzer.AnalyzeFileAsync(m1Path);
                analyzer.WaitForCompleteAnalysis(_ => true);

                AssertUtil.ContainsExactly(
                    analyzer.GetEntriesThatImportModuleAsync("module1", true).Result.Select(m => m.moduleName),
                    "module2"
                );

                AssertUtil.ContainsExactly(
                    analyzer.GetValueDescriptions(entry2, "x", loc),
                    "int"
                );
            }
        }


        [TestMethod, Priority(2)]
        public async Task AnalyzeBadEgg() {
            var factories = new[] { InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(new Version(3, 4)) };
            var services = PythonToolsTestUtilities.CreateMockServiceProvider().GetEditorServices();
            using (var analyzer = await VsProjectAnalyzer.CreateForTestsAsync(services, factories[0])) {
                await analyzer.SetSearchPathsAsync(new[] { TestData.GetPath(@"TestData\BadEgg.egg") });
                analyzer.WaitForCompleteAnalysis(_ => true);

                // Analysis result must contain the module for the filename inside the egg that is a valid identifier,
                // and no entries for the other filename which is not. 
                var moduleNames = (await analyzer.GetModulesAsync(null, null)).Select(x => x.Name);
                AssertUtil.Contains(moduleNames, "module");
                AssertUtil.DoesntContain(moduleNames, "42");
            }
        }
    }
}

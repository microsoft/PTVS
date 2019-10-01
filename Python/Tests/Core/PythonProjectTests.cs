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

extern alias pythontools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pythontools::Microsoft.PythonTools;
using pythontools::Microsoft.PythonTools.Intellisense;
using pythontools::Microsoft.PythonTools.Project;
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

        [TestMethod, Priority(UnitTestPriority.SUPPLEMENTARY_UNIT_TEST)]
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

        [TestMethod, Priority(UnitTestPriority.CORE_UNIT_TEST)]
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

        private static void WaitForEmptySet(WaitHandle activity, HashSet<string> set, CancellationToken token) {
            while (true) {
                lock (set) {
                    if (!set.Any()) {
                        return;
                    }
                }
                token.ThrowIfCancellationRequested();
                activity.WaitOne(1000);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0_FAILING_UNIT_TEST)]
        public async Task LoadAndUnloadModule() {
            var services = PythonToolsTestUtilities.CreateMockServiceProvider().GetEditorServices();
            using (var are = new AutoResetEvent(false))
            using (var analyzer = await VsProjectAnalyzer.CreateForTestsAsync(services, InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(new Version(3, 6)))) {
                var m1Path = TestData.GetPath("TestData\\SimpleImport\\module1.py");
                var m2Path = TestData.GetPath("TestData\\SimpleImport\\module2.py");

                var toAnalyze = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { m1Path, m2Path };
                analyzer.AnalysisComplete += (s, e) => {
                    lock (toAnalyze) {
                        toAnalyze.Remove(e.Path);
                    }
                    are.Set();
                };
                var entry1 = await analyzer.AnalyzeFileAsync(m1Path);
                var entry2 = await analyzer.AnalyzeFileAsync(m2Path);
                WaitForEmptySet(are, toAnalyze, CancellationTokens.After60s);

                var loc = new Microsoft.PythonTools.SourceLocation(1, 1);
                AssertUtil.ContainsExactly(
                    analyzer.GetEntriesThatImportModuleAsync("module1", true).Result.Select(m => m.moduleName),
                    "module2"
                );

                AssertUtil.ContainsExactly(
                    analyzer.GetValueDescriptions(entry2, "x", loc),
                    "int"
                );

                toAnalyze.Add(m2Path);
                await analyzer.UnloadFileAsync(entry1);
                WaitForEmptySet(are, toAnalyze, CancellationTokens.After15s);

                // Even though module1 has been unloaded, we still know that
                // module2 imports it.
                AssertUtil.ContainsExactly(
                    analyzer.GetEntriesThatImportModuleAsync("module1", true).Result.Select(m => m.moduleName),
                    "module2"
                );

                AssertUtil.ContainsExactly(
                    analyzer.GetValueDescriptions(entry2, "x", loc)
                );

                toAnalyze.Add(m1Path);
                toAnalyze.Add(m2Path);
                await analyzer.AnalyzeFileAsync(m1Path);
                WaitForEmptySet(are, toAnalyze, CancellationTokens.After5s);

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


        [TestMethod, Priority(UnitTestPriority.P2_FAILING_UNIT_TEST)]
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

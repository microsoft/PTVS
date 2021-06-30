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

#if DJANGO_HTML_EDITOR
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Django.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace DjangoTests {
    [TestClass]
    public class DjangoAnalyzerTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        private void TestSingleRenderVariable(string template, string value = "data") {
            var proj = AnalyzerTest(TestData.GetPath("TestData\\DjangoAnalysisTestApp"), out _);

            var vars = proj.GetVariablesForTemplateFile(TestData.GetPath("TestData\\DjangoAnalysisTestApp\\test_render\\templates\\" + template));
            Assert.IsNotNull(vars, "No variables found for " + template);

            HashSet<AnalysisValue> values;
            Assert.IsTrue(vars.TryGetValue("content", out values), "content was missing");
            Assert.AreEqual(1, values.Count, "expected single value");
            Assert.AreEqual(value, values.Single().GetConstantValueAsString());
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void TestRender() {
            TestSingleRenderVariable("test_render.html");
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void TestRenderToResponse() {
            TestSingleRenderVariable("test_render_to_response.html");
        }

        [TestMethod, Priority(UnitTestPriority.P2_FAILING)] // https://github.com/Microsoft/PTVS/issues/4144
        public void TestCustomFilter() {
            var proj = AnalyzerTest(TestData.GetPath("TestData\\DjangoAnalysisTestApp"), out var langVersion);

            AssertUtil.ContainsExactly(
                proj._filters.Keys.Except(DjangoAnalyzer._knownFilters.Keys),
                "test_filter",
                "test_filter_2"
            );

            var entry = proj._filters["test_filter_2"].Entry;
            var parser = Parser.CreateParser(
                new StringReader(File.ReadAllText(entry.FilePath).Replace("test_filter_2", "test_filter_3")),
                langVersion
            );
            using (var p = entry.BeginParse()) {
                p.Tree = parser.ParseFile();
                p.Complete();
            }
            entry.Analyze(CancellationToken.None);

            AssertUtil.ContainsExactly(
                proj._filters.Keys.Except(DjangoAnalyzer._knownFilters.Keys),
                "test_filter",
                "test_filter_3"
            );
        }

        [TestMethod, Priority(UnitTestPriority.P2_FAILING)] // https://github.com/Microsoft/PTVS/issues/4144
        public void TestCustomTag() {
            var proj = AnalyzerTest(TestData.GetPath("TestData\\DjangoAnalysisTestApp"), out var langVersion);

            AssertUtil.ContainsExactly(
                proj._tags.Keys.Except(DjangoAnalyzer._knownTags.Keys),
                "test_tag",
                "test_tag_2",
                "test_assignment_tag",
                "test_simple_tag"
            );

            var entry = proj._tags["test_tag_2"].Entry;
            var parser = Parser.CreateParser(
                new StringReader(File.ReadAllText(entry.FilePath).Replace("test_tag_2", "test_tag_3")),
                langVersion
            );
            using (var p = entry.BeginParse()) {
                p.Tree = parser.ParseFile();
                p.Complete();
            }
            entry.Analyze(CancellationToken.None);

            AssertUtil.ContainsExactly(
                proj._tags.Keys.Except(DjangoAnalyzer._knownTags.Keys),
                "test_tag",
                "test_tag_3",
                "test_assignment_tag",
                "test_simple_tag"
            );
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void TestListView() {
            var proj = AnalyzerTest(TestData.GetPath("TestData\\DjangoAnalysisTestApp"), out _);
            var templates = TestData.GetPath("TestData\\DjangoAnalysisTestApp\\myapp\\templates\\myapp\\");

            var detailsVars = proj.GetVariablesForTemplateFile(templates + "index.html");
            Assert.IsNotNull(detailsVars, "No vars found for index.html");
            AssertUtil.ContainsExactly(detailsVars.Keys, "latest_poll_list");
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void TestDetailsView() {
            var proj = AnalyzerTest(TestData.GetPath("TestData\\DjangoAnalysisTestApp"), out _);
            var templates = TestData.GetPath("TestData\\DjangoAnalysisTestApp\\myapp\\templates\\myapp\\");

            var detailsVars = proj.GetVariablesForTemplateFile(templates + "details.html");
            Assert.IsNotNull(detailsVars, "No vars found for details.html");
            AssertUtil.ContainsExactly(detailsVars.Keys, "mymodel");

            var mymodel2_detailsVars = proj.GetVariablesForTemplateFile(templates + "mymodel2_details.html");
            Assert.IsNotNull(detailsVars, "No vars found for mymodel2_details.html");
            AssertUtil.ContainsExactly(mymodel2_detailsVars.Keys, "mymodel2");
        }

        private DjangoAnalyzer AnalyzerTest(string path, out PythonLanguageVersion languageVersion) {
            var version = PythonPaths.Versions.LastOrDefault(v => Directory.Exists(Path.Combine(v.PrefixPath, "Lib", "site-packages", "django")));
            version.AssertInstalled();

            var testFact = InterpreterFactoryCreator.CreateInterpreterFactory(version.Configuration, new InterpreterFactoryCreationOptions {
                DatabasePath = TestData.GetTempPath(),
                UseExistingCache = false,
                TraceLevel = TraceLevel.Verbose,
                WatchFileSystem = false
            });
            Debug.WriteLine("Testing with {0}".FormatInvariant(version.InterpreterPath));
            languageVersion = testFact.GetLanguageVersion();

            var analyzer = PythonAnalyzer.CreateAsync(testFact).WaitAndUnwrapExceptions();
            var djangoAnalyzer = new DjangoAnalyzer();
            djangoAnalyzer.Register(analyzer);

            var entries = new List<IPythonProjectEntry>();
            foreach (string file in Directory.EnumerateFiles(path, "*.py", SearchOption.AllDirectories)) {
                if (!ModulePath.FromBasePathAndFile_NoThrow(path, file, out var mp)) {
                    Debug.WriteLine("Not parsing {0}".FormatInvariant(file));
                    continue;
                }
                Debug.WriteLine("Parsing {0} ({1})".FormatInvariant(mp.FullName, file));
                var entry = analyzer.AddModule(mp.ModuleName, file);
                var parser = Parser.CreateParser(
                    new FileStream(file, FileMode.Open, FileAccess.Read),
                    testFact.GetLanguageVersion()
                );
                using (var p = entry.BeginParse()) {
                    p.Tree = parser.ParseFile();
                    p.Complete();
                }
                entries.Add(entry);
            }

            foreach (var entry in entries) {
                entry.Analyze(CancellationToken.None);
            }

            Debug.WriteLine((testFact as IPythonInterpreterFactoryWithLog)?.GetAnalysisLogContent(CultureInfo.CurrentUICulture) ?? "(no logs)");

            return djangoAnalyzer;
        }
    }
}
#endif

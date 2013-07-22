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
using System.IO;
using System.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Django.Project;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace DjangoTests {
    [TestClass]
    public class DjangoAnalyzerTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            TestData.Deploy();
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestDetailsView() {
            var proj = AnalyzerTest(TestData.GetPath("TestData\\DjangoAnalysisTestApp"));

            var detailsVars = proj.GetVariablesForTemplateFile(TestData.GetPath("TestData\\DjangoAnalysisTestApp\\myapp\\templates\\myapp\\details.html"));

            Assert.IsTrue(detailsVars.ContainsKey("mymodel"));
            var mymodel2_detailsVars = proj.GetVariablesForTemplateFile(TestData.GetPath("TestData\\DjangoAnalysisTestApp\\myapp\\templates\\myapp\\mymodel2_details.html"));

            Assert.IsTrue(mymodel2_detailsVars.ContainsKey("mymodel2"));
        }

        private DjangoAnalyzer AnalyzerTest(string path) {
            var testFact = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(
                new Version(2, 7),
                "Django Test Interpreter",
                TestData.GetPath("CompletionDB"),
                TestData.GetPath("TestData\\DjangoDB")
            );

            PythonAnalyzer analyzer = new PythonAnalyzer(testFact);
            DjangoAnalyzer djangoAnalyzer = new DjangoAnalyzer();
            djangoAnalyzer.OnNewAnalyzer(analyzer);

            analyzer.AddAnalysisDirectory(path);

            List<IPythonProjectEntry> entries = new List<IPythonProjectEntry>();
            foreach (string file in Directory.EnumerateFiles(path, "*.py", SearchOption.AllDirectories)) {
                var entry = analyzer.AddModule(PythonAnalyzer.PathToModuleName(file), file);
                var parser = Parser.CreateParser(
                    new FileStream(file, FileMode.Open, FileAccess.Read),
                    PythonLanguageVersion.V27
                );
                entry.UpdateTree(parser.ParseFile(), null);
                entries.Add(entry);
            }

            foreach (var entry in entries) {
                entry.Analyze(CancellationToken.None, false);
            }

            return djangoAnalyzer;
        }
    }
}

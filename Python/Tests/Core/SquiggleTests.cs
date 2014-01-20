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
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using IronPython.Hosting;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.Scripting.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudioTools.Project;
using TestUtilities;
using TestUtilities.Mocks;
using TestUtilities.Python;

namespace PythonToolsTests {
    [TestClass]
    public class SquiggleTests {
        public static IContentType PythonContentType = new MockContentType("Python", new IContentType[0]);
        public static ScriptEngine PythonEngine = Python.CreateEngine();

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy(includeTestData: false);
            UIThread.InitUnitTestingMode();
        }

        private static VsProjectAnalyzer AnalyzeTextBuffer(
            MockTextBuffer buffer,
            PythonLanguageVersion version = PythonLanguageVersion.V27
        ) {
            var fact = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(version.ToVersion());
            var analyzer = new VsProjectAnalyzer(fact, new[] { fact }, new MockErrorProviderFactory());
            buffer.AddProperty(typeof(VsProjectAnalyzer), analyzer);
            var classifierProvider = new PythonClassifierProvider(new MockContentTypeRegistryService());
            classifierProvider._classificationRegistry = new MockClassificationTypeRegistryService();
            classifierProvider.GetClassifier(buffer);
            var monitoredBuffer = analyzer.MonitorTextBuffer(new MockTextView(buffer), buffer);
            analyzer.WaitForCompleteAnalysis(x => true);
            while (((IPythonProjectEntry)buffer.GetAnalysis()).Analysis == null) {
                Thread.Sleep(500);
            }
            analyzer.StopMonitoringTextBuffer(monitoredBuffer.BufferParser);
            return analyzer;
        }

        private static string FormatErrorTag(TrackingTagSpan<ErrorTag> tag) {
            return string.Format("{0}: {1} ({2})",
                tag.Tag.ErrorType,
                tag.Tag.ToolTipContent,
                FormatErrorTagSpan(tag)
            );
        }

        private static string FormatErrorTagSpan(TrackingTagSpan<ErrorTag> tag) {
            return string.Format("{0}-{1}",
                tag.Span.GetStartPoint(tag.Span.TextBuffer.CurrentSnapshot).Position,
                tag.Span.GetEndPoint(tag.Span.TextBuffer.CurrentSnapshot).Position
            );
        }


        private static IEnumerable<TrackingTagSpan<ErrorTag>> GetErrorSquiggles(ITextBuffer buffer) {
            var analyzer = buffer.Properties.GetProperty<VsProjectAnalyzer>(typeof(VsProjectAnalyzer));
            Assert.IsNotNull(analyzer, "Analyzer property was not set on text buffer");
            Assert.IsNotNull(analyzer._errorProvider, "No error provider was set");
            var squiggles = analyzer._errorProvider.GetErrorTagger(buffer);
            var snapshot = buffer.CurrentSnapshot;

            return squiggles.GetTaggedSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));
        }

        [TestMethod, Priority(0)]
        public void UnresolvedImportSquiggle() {
            var buffer = new MockTextBuffer("import fob, oar\r\nfrom baz import *\r\nfrom .spam import eggs", "C:\\name.py");
            var analyzer = AnalyzeTextBuffer(buffer);

            var squiggles = GetErrorSquiggles(buffer).Select(FormatErrorTag).ToArray();

            int i = 0;
            foreach (var expected in new[] {
                // Ensure that the warning includes the module name
                @".*warning:.*fob.*\(7-10\)",
                @".*warning:.*oar.*\(12-15\)",
                @".*warning:.*baz.*\(22-25\)",
                @".*warning:.*\.spam.*\(41-46\)"
            }) {
                Assert.IsTrue(i < squiggles.Length, "Not enough squiggles");
                AssertUtil.AreEqual(new Regex(expected, RegexOptions.IgnoreCase), squiggles[i]);
                i += 1;
            }
        }
    }
}

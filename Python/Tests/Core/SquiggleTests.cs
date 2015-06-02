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
using System.Threading.Tasks;
using IronPython.Hosting;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.Scripting.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
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
        }

        [TestInitialize]
        public void TestInitialize() {
            UnresolvedImportSquiggleProvider._alwaysCreateSquiggle = true;
        }

        [TestCleanup]
        public void TestCleanup() {
            UnresolvedImportSquiggleProvider._alwaysCreateSquiggle = false;
        }

        private static IEnumerable<TrackingTagSpan<ErrorTag>> AnalyzeTextBuffer(
            MockTextBuffer buffer,
            PythonLanguageVersion version = PythonLanguageVersion.V27
        ) {
            return AnalyzeTextBufferAsync(buffer, version).GetAwaiter().GetResult();
        }

        private static async Task<IEnumerable<TrackingTagSpan<ErrorTag>>> AnalyzeTextBufferAsync(
            MockTextBuffer buffer,
            PythonLanguageVersion version = PythonLanguageVersion.V27
        ) {
            var fact = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(version.ToVersion());
            
            try {
                var serviceProvider = PythonToolsTestUtilities.CreateMockServiceProvider();
                var errorProvider = serviceProvider.ComponentModel.GetService<IErrorProviderFactory>();
                Assert.IsNotNull(errorProvider, "Error provider factory is not available");
                var analyzer = new VsProjectAnalyzer(serviceProvider, fact, new[] { fact });
                buffer.AddProperty(typeof(VsProjectAnalyzer), analyzer);
                var classifierProvider = new PythonClassifierProvider(new MockContentTypeRegistryService(PythonCoreConstants.ContentType), serviceProvider);
                classifierProvider._classificationRegistry = new MockClassificationTypeRegistryService();
                classifierProvider.GetClassifier(buffer);
                var squiggles = errorProvider.GetErrorTagger(buffer);
                var textView = new MockTextView(buffer);
                var monitoredBuffer = analyzer.MonitorTextBuffer(textView, buffer);

                var tcs = new TaskCompletionSource<object>();
                buffer.GetPythonProjectEntry().OnNewAnalysis += (s, e) => tcs.SetResult(null);
                await tcs.Task;

                var snapshot = buffer.CurrentSnapshot;
                
                // Ensure all tasks have been updated
                var taskProvider = (ErrorTaskProvider)serviceProvider.GetService(typeof(ErrorTaskProvider));
                var time = await taskProvider.FlushAsync();
                Console.WriteLine("TaskProvider.FlushAsync took {0}ms", time.TotalMilliseconds);

                var spans = squiggles.GetTaggedSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));

                analyzer.StopMonitoringTextBuffer(monitoredBuffer.BufferParser, textView);

                return spans;
            } finally {
            }
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


        [TestMethod, Priority(0)]
        public void UnresolvedImportSquiggle() {
            var buffer = new MockTextBuffer("import fob, oar\r\nfrom baz import *\r\nfrom .spam import eggs", PythonCoreConstants.ContentType, filename: "C:\\name.py");
            var squiggles = AnalyzeTextBuffer(buffer).Select(FormatErrorTag).ToArray();

            Console.WriteLine(" Squiggles found:");
            foreach (var actual in squiggles) {
                Console.WriteLine(actual);
            }
            Console.WriteLine(" Found {0} squiggle(s)", squiggles.Length);

            int i = 0;
            foreach (var expected in new[] {
                // Ensure that the warning includes the module name
                @".*warning:.*fob.*\(7-10\)",
                @".*warning:.*oar.*\(12-15\)",
                @".*warning:.*baz.*\(22-25\)",
                @".*warning:.*\.spam.*\(41-46\)"
            }) {
                Assert.IsTrue(i < squiggles.Length, "Not enough squiggles");
                AssertUtil.AreEqual(new Regex(expected, RegexOptions.IgnoreCase | RegexOptions.Singleline), squiggles[i]);
                i += 1;
            }
        }

        [TestMethod, Priority(0)]
        public void HandledImportSquiggle() {
            var testCases = new List<Tuple<string, string[]>>();
            testCases.AddRange(
                new[] { "", " BaseException", " Exception", " ImportError", " (ValueError, ImportError)" }
                .Select(ex => Tuple.Create(
                    string.Format("try:\r\n    import spam\r\nexcept{0}:\r\n    pass\r\n", ex),
                    new string[0]
                ))
            );

            testCases.Add(Tuple.Create(
                "try:\r\n    import spam\r\nexcept ValueError:\r\n    pass\r\n",
                new[] { @".*warning:.*spam.*\(17-21\)" }
            ));

            foreach (var testCase in testCases) {
                var buffer = new MockTextBuffer(testCase.Item1, PythonCoreConstants.ContentType);
                var squiggles = AnalyzeTextBuffer(buffer).Select(FormatErrorTag).ToArray();

                Console.WriteLine(testCase.Item1);
                Console.WriteLine(" Squiggles found:");
                foreach (var actual in squiggles) {
                    Console.WriteLine(actual);
                }
                Console.WriteLine(" Found {0} squiggle(s)", squiggles.Length);
                Console.WriteLine();

                int i = 0;
                foreach (var expected in testCase.Item2) {
                    Assert.IsTrue(i < squiggles.Length, "Not enough squiggles");
                    AssertUtil.AreEqual(new Regex(expected, RegexOptions.IgnoreCase | RegexOptions.Singleline), squiggles[i]);
                    i += 1;
                }
            }
        }
    }
}

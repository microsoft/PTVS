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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using TestUtilities;
using TestUtilities.Mocks;
using TestUtilities.Python;

namespace PythonToolsTests {
    [TestClass]
    public class ClassifierTests {
        public static IContentType PythonContentType = new MockContentType("Python", new IContentType[0]);

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy(includeTestData: false);
        }

        [TestMethod, Priority(0)]
        public void KeywordClassification27() {
            var code = string.Join(Environment.NewLine, PythonKeywords.All(PythonLanguageVersion.V27));
            code += "\r\nTrue\r\nFalse";
            var helper = new ClassifierHelper(new MockTextBuffer(code), PythonLanguageVersion.V27);

            foreach (var span in helper.AstClassifierSpans) {
                var text = span.Span.GetText();
                if (string.IsNullOrWhiteSpace(text)) {
                    continue;
                }
                
                // None, True and False are special
                if (text == "None" || text == "True" || text == "False") {
                    Assert.AreEqual("Python builtin", span.ClassificationType.Classification, text);
                    continue;
                }

                Assert.AreEqual("keyword", span.ClassificationType.Classification, text);
            }
        }

        [TestMethod, Priority(0)]
        public void KeywordClassification33() {
            var code = string.Join(Environment.NewLine, PythonKeywords.All(PythonLanguageVersion.V33));
            var helper = new ClassifierHelper(new MockTextBuffer(code), PythonLanguageVersion.V33);

            foreach (var span in helper.AstClassifierSpans) {
                var text = span.Span.GetText();
                if (string.IsNullOrWhiteSpace(text)) {
                    continue;
                }
                
                // None is special
                if (text == "None") {
                    Assert.AreEqual("Python builtin", span.ClassificationType.Classification, text);
                    continue;
                }

                Assert.AreEqual("keyword", span.ClassificationType.Classification, text);
            }
        }

        [TestMethod, Priority(0)]
        public void ModuleClassification() {
            var code = @"import abc
import os
import ntpath

os.path = ntpath
abc = 123
abc = True
";
            var helper = new ClassifierHelper(new MockTextBuffer(code), PythonLanguageVersion.V27);
            helper.CheckAstClassifierSpans("ki ki ki i.i=i i=n i=b");

            helper.Analyze();

            helper.CheckAnalysisClassifierSpans("m<abc>m<os>m<ntpath>m<os>m<ntpath>m<abc>m<abc>");
        }

        [TestMethod, Priority(0)]
        public void TypeClassification() {
            var code = @"class MyClass(object):
    pass

mc = MyClass()
MyClassAlias = MyClass
mca = MyClassAlias()
MyClassType = type(mc)
";
            var helper = new ClassifierHelper(new MockTextBuffer(code), PythonLanguageVersion.V27);
            helper.CheckAstClassifierSpans("ki(i): k i=i() i=i i=i() i=i(i)");
            helper.AnalysisClassifierSpans.ToArray();

            helper.Analyze();

            helper.CheckAnalysisClassifierSpans("c<MyClass>c<object>cc<MyClassAlias>ccc<type>");
        }

        [TestMethod, Priority(0)]
        public void ParameterClassification() {
            var code = @"def f(a, b, c):
    a = b
    b = c
    return a

f(a, b, c)
a = b
b = c
";
            var helper = new ClassifierHelper(new MockTextBuffer(code), PythonLanguageVersion.V27);
            helper.CheckAstClassifierSpans("ki(i,i,i): i=i i=i ki i(i,i,i) i=i i=i");
            
            helper.Analyze();

            helper.CheckAnalysisClassifierSpans("f<f>ppppppppf<f>");
        }

        [TestMethod, Priority(0)]
        public void TrueFalseClassification() {
            var code = "True False";

            var helper = new ClassifierHelper(new MockTextBuffer(code), PythonLanguageVersion.V27);
            helper.CheckAstClassifierSpans("b<True> b<False>");

            helper = new ClassifierHelper(new MockTextBuffer(code), PythonLanguageVersion.V33);
            helper.CheckAstClassifierSpans("k<True> k<False>");
        }

        #region ClassifierHelper class

        private class ClassifierHelper {
            private static readonly MockContentTypeRegistryService _contentRegistry = new MockContentTypeRegistryService();
            private static readonly MockClassificationTypeRegistryService _classificationRegistry = new MockClassificationTypeRegistryService();
            private static readonly PythonClassifierProvider _provider1 =
                new PythonClassifierProvider(_contentRegistry) { _classificationRegistry = _classificationRegistry };
            private static readonly PythonAnalysisClassifierProvider _provider2 =
                new PythonAnalysisClassifierProvider(_contentRegistry) { _classificationRegistry = _classificationRegistry };

            private readonly MockTextBuffer _buffer;
            private readonly MockTextView _view;
            private readonly IPythonInterpreterFactory _factory;
            private MonitoredBufferResult _parser;

            public ClassifierHelper(MockTextBuffer buffer, PythonLanguageVersion version) {
                _buffer = buffer;
                _factory = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(version.ToVersion());

                var analyzer = new VsProjectAnalyzer(_factory, new[] { _factory });
                _buffer.AddProperty(typeof(VsProjectAnalyzer), analyzer);

                _view = new MockTextView(_buffer);
            }

            public ITextView TextView {
                get {
                    return _view;
                }
            }

            public ITextBuffer TextBuffer {
                get {
                    return _buffer;
                }
            }

            public IClassifier AstClassifier {
                get {
                    return _provider1.GetClassifier(_buffer);
                }
            }

            public IEnumerable<ClassificationSpan> AstClassifierSpans {
                get {
                    return AstClassifier.GetClassificationSpans(
                        new SnapshotSpan(_buffer.CurrentSnapshot, 0, _buffer.CurrentSnapshot.Length)
                    ).OrderBy(s => s.Span.Start.Position);
                }
            }

            public IClassifier AnalysisClassifier {
                get {
                    return _provider2.GetClassifier(_buffer);
                }
            }

            public IEnumerable<ClassificationSpan> AnalysisClassifierSpans {
                get {
                    return AnalysisClassifier.GetClassificationSpans(
                        new SnapshotSpan(_buffer.CurrentSnapshot, 0, _buffer.CurrentSnapshot.Length)
                    ).OrderBy(s => s.Span.Start.Position);
                }
            }

            private static readonly Dictionary<char, string> ClassificationAbbreviations = new Dictionary<char, string> {
                { 'm', PythonPredefinedClassificationTypeNames.Module },
                { 'c', PythonPredefinedClassificationTypeNames.Class },
                { 'f', PythonPredefinedClassificationTypeNames.Function },
                { 'p', PythonPredefinedClassificationTypeNames.Parameter },
                { 'b', PythonPredefinedClassificationTypeNames.Builtin },
                { 'i', PredefinedClassificationTypeNames.Identifier },
                { 'l', PredefinedClassificationTypeNames.Literal },
                { 'n', PredefinedClassificationTypeNames.Number },
                { 'k', PredefinedClassificationTypeNames.Keyword },
                { '(', PythonPredefinedClassificationTypeNames.Grouping },
                { ')', PythonPredefinedClassificationTypeNames.Grouping },
                { ':', PythonPredefinedClassificationTypeNames.Operator },
                { '=', PythonPredefinedClassificationTypeNames.Operator },
                { ',', PythonPredefinedClassificationTypeNames.Comma },
                { '.', PythonPredefinedClassificationTypeNames.Dot },
            };

            private static IEnumerable<string> ExpandNames(string classifications) {
                return classifications.Select(c => {
                    string name;
                    return ClassificationAbbreviations.TryGetValue(c, out name) ? name : null;
                }).Where(n => !string.IsNullOrEmpty(n));
            }

            public void CheckAstClassifierSpans(string expectedSpans) {
                CheckClassifierSpans(AstClassifierSpans, expectedSpans);
            }

            public void CheckAnalysisClassifierSpans(string expectedSpans) {
                CheckClassifierSpans(AnalysisClassifierSpans, expectedSpans);
            }

            private void CheckClassifierSpans(IEnumerable<ClassificationSpan> spans, string expectedSpans) {
                spans = spans
                    .Where(s => !s.ClassificationType.IsOfType(PredefinedClassificationTypeNames.WhiteSpace))
                    .ToArray();

                var sb = new StringBuilder("Actual: ");
                foreach (var span in spans) {
                    var code = ClassificationAbbreviations.FirstOrDefault(kv => kv.Value == span.ClassificationType.Classification).Key;
                    if (code == '\0') {
                        Console.WriteLine("No code for {0}", span.ClassificationType.Classification);
                        sb.AppendFormat("{{{0}}}", span.ClassificationType.Classification);
                    } else {
                        sb.AppendFormat("{0}", code);
                    }
                    var text = span.Span.GetText();
                    if (!string.IsNullOrEmpty(text)) {
                        sb.AppendFormat("<{0}>", text);
                    }
                }
                Console.WriteLine(sb.ToString());

                foreach (var span in spans) {
                    if (string.IsNullOrEmpty(expectedSpans)) {
                        Assert.Fail("Not enough spans expected");
                    }
                    string expected;
                    var match = Regex.Match(expectedSpans, @"\s*(?<code>.)(\<(?<token>.+?)\>)?(?<rest>.*)$");
                    if (!match.Success) {
                        break;
                    }

                    Assert.IsTrue(ClassificationAbbreviations.TryGetValue(match.Groups["code"].Value[0], out expected), "Did not understand character '" + match.Groups["code"].Value + "'");

                    string expectedToken = "(none)";
                    if (match.Groups["token"].Success) {
                        expectedToken = match.Groups["token"].Value;
                    }

                    Assert.IsTrue(
                        span.ClassificationType.IsOfType(expected),
                        string.Format("Expected <{0}>. Actual <{1}>. Expected token <{2}> Actual token <{3}>.", expected, span.ClassificationType.Classification, expectedToken, span.Span.GetText())
                    );

                    if (match.Groups["token"].Success) {
                        Assert.AreEqual(expectedToken, span.Span.GetText());
                    }

                    expectedSpans = match.Groups["rest"].Value;
                }

                Assert.IsTrue(string.IsNullOrEmpty(expectedSpans), "Remaining: " + expectedSpans);
            }

            public VsProjectAnalyzer Analyzer {
                get {
                    return _buffer.Properties.GetProperty<VsProjectAnalyzer>(typeof(VsProjectAnalyzer));
                }
            }

            public bool MonitorTextBuffer {
                get {
                    return _parser.BufferParser != null;
                }
                set {
                    if (value == MonitorTextBuffer) {
                        return;
                    }
                    var analyzer = Analyzer;
                    if (value) {
                        _parser = analyzer.MonitorTextBuffer(_view, _buffer);
                    } else {
                        analyzer.StopMonitoringTextBuffer(_parser.BufferParser, _view);
                        _parser = default(MonitoredBufferResult);
                    }
                }
            }

            public void Analyze() {
                var analyzer = Analyzer;

                var wasMonitoring = MonitorTextBuffer;
                MonitorTextBuffer = true;
                var classifier = AnalysisClassifier;
                using (var evt = new ManualResetEventSlim()) {
                    classifier.ClassificationChanged += (o, e) => evt.Set();
                    var ensureClassifier = AnalysisClassifierSpans.ToArray();
                    _buffer.GetPythonProjectEntry().Analyze(CancellationToken.None, true);
                    analyzer.WaitForCompleteAnalysis(_ => true);
                    while (_buffer.GetPythonProjectEntry().Analysis == null) {
                        Thread.Sleep(500);
                    }
                    Assert.IsTrue(evt.Wait(10000));
                    MonitorTextBuffer = wasMonitoring;
                }
            }
        }

        #endregion
    }
}

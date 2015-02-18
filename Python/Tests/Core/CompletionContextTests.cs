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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IronPython.Hosting;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.PythonTools.Parsing;
using Microsoft.Scripting.Hosting;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudioTools;
using TestUtilities;
using TestUtilities.Mocks;
using TestUtilities.Python;

namespace PythonToolsTests {
    [TestClass]
    public class CompletionContextTests {
        public static IContentType PythonContentType = new MockContentType("Python", new IContentType[0]);
        public static ScriptEngine PythonEngine = Python.CreateEngine();

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        [TestMethod, Priority(0)]
        public void GetApplicableSpanTest() {
            var text = "if fob.oar(eggs, spam<=ham) :";
            var buffer = MockTextBuffer(text);
            var analyzer = AnalyzeTextBuffer(buffer);
            var snapshot = (MockTextSnapshot)buffer.CurrentSnapshot;

            // We check the applicable span at every index in the string.
            var expected = new[] {
                "if", "if", "if",
                "fob", "fob", "fob", "fob",
                "oar", "oar", "oar", "oar",
                "eggs", "eggs", "eggs", "eggs", "eggs",
                "", // between ',' and ' '
                "spam", "spam", "spam", "spam", "spam",
                "", // between '<' and '='
                "ham", "ham", "ham", "ham",
                "", // between ')' and ' '
                "", // between ' ' and ':'
                "", // between ':' and EOL
            };

            for (int i = 0; i < text.Length; ++i) {
                var span = snapshot.GetApplicableSpan(i);
                if (span == null) {
                    Assert.AreEqual(expected[i], "", text.Substring(0, i) + "|" + text.Substring(i));
                } else {
                    Assert.AreEqual(expected[i], span.GetText(snapshot), text.Substring(0, i) + "|" + text.Substring(i));
                }
            }
        }

        private static VsProjectAnalyzer AnalyzeTextBuffer(MockTextBuffer buffer, PythonLanguageVersion version = PythonLanguageVersion.V27) {
            var fact = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(version.ToVersion());
            var serviceProvider = PythonToolsTestUtilities.CreateMockServiceProvider();
            var analyzer = new VsProjectAnalyzer(serviceProvider, fact, new[] { fact });
            buffer.AddProperty(typeof(VsProjectAnalyzer), analyzer);
            var classifierProvider = new PythonClassifierProvider(new MockContentTypeRegistryService(PythonCoreConstants.ContentType), serviceProvider);
            classifierProvider._classificationRegistry = new MockClassificationTypeRegistryService();
            classifierProvider.GetClassifier(buffer);
            var textView = new MockTextView(buffer);
            var monitoredBuffer = analyzer.MonitorTextBuffer(textView, buffer);
            analyzer.WaitForCompleteAnalysis(x => true);
            while (((IPythonProjectEntry)buffer.GetProjectEntry()).Analysis == null) {
                System.Threading.Thread.Sleep(500);
            }
            analyzer.StopMonitoringTextBuffer(monitoredBuffer.BufferParser, textView);
            return analyzer;
        }

        [TestMethod, Priority(0)]
        public void CtrlSpaceCompletions() {
            string code = @"def f(param1, param2):
    g()";

            var completionList = GetCompletionSetCtrlSpace(code.IndexOf("g(") + 2, code).Completions.Select(x => x.DisplayText).ToArray();
            AssertUtil.Contains(completionList, "param1");
            AssertUtil.Contains(completionList, "param2");

            code = @"def f(param1, param2):
    g(param1, )";

            completionList = GetCompletionSetCtrlSpace(code.IndexOf("g(param1, ") + "g(param1, ".Length, code).Completions.Select(x => x.DisplayText).ToArray();
            AssertUtil.Contains(completionList, "param1");
            AssertUtil.Contains(completionList, "param2");

            // verify Ctrl-Space inside of a function gives proper completions
            foreach (var codeSnippet in new[] { @"def f():
    
    pass", @"def f():
    x = (2 + 3)
    
    pass
", @"def f():
    yield (2 + 3)
    
    pass" }) {

                Debug.WriteLine(String.Format("Testing {0}", codeSnippet));

                completionList = GetCompletionSetCtrlSpace(codeSnippet.IndexOf("pass") - 6, codeSnippet).Completions.Select(x => x.DisplayText).ToArray();
                AssertUtil.Contains(completionList, "min");
                AssertUtil.Contains(completionList, "assert");
            }
        }

        [TestMethod, Priority(0)]
        public void KeywordCompletions() {
            string code = @"";

            var completionList = GetCompletionSetCtrlSpace(0, code).Completions.Select(x => x.DisplayText).ToArray();

            // not in a function
            AssertUtil.DoesntContain(completionList, "yield");
            AssertUtil.DoesntContain(completionList, "return");

            AssertUtil.ContainsAtLeast(completionList, "assert", "and");

            code = @"def f():
    |
    pass";

            completionList = GetCompletionSetCtrlSpace(code.IndexOf("|"), code.Replace("|", "")).Completions.Select(x => x.DisplayText).ToArray();

            AssertUtil.ContainsAtLeast(completionList, "yield", "return");


            code = @"x = (abc, oar, )";

            completionList = GetCompletionSetCtrlSpace(code.IndexOf("oar,") + 5, code).Completions.Select(x => x.DisplayText).ToArray();

            AssertUtil.ContainsAtLeast(completionList, "and");
            AssertUtil.DoesntContain(completionList, "def");
        }

        [TestMethod, Priority(0)]
        public void KeywordOrIdentifierCompletions() {
            // http://pytools.codeplex.com/workitem/560
            string code = @"
def h():
    yiel

def f():
    yield

def g():
    yield_

yield_expression = 42
";

            var completionList = GetCompletionSetCtrlSpace(
                code.IndexOf("yield_") + 6,
                code).Completions.Select(x => x.DisplayText).ToArray();

            AssertUtil.DoesntContain(completionList, "yield");
            AssertUtil.Contains(completionList, "yield_expression");

            completionList = GetCompletionSetCtrlSpace(
                code.IndexOf("yield") + 5,
                code).Completions.Select(x => x.DisplayText).ToArray();

            AssertUtil.Contains(completionList, "yield");
            AssertUtil.Contains(completionList, "yield_expression");

            completionList = GetCompletionSetCtrlSpace(
                code.IndexOf("yiel") + 4,
                code).Completions.Select(x => x.DisplayText).ToArray();

            AssertUtil.Contains(completionList, "yield");
            AssertUtil.Contains(completionList, "yield_expression");
        }

        [TestMethod, Priority(0)]
        public void TrueFalseNoneCompletions() {
            // http://pytools.codeplex.com/workitem/1905
            var factory = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(PythonLanguageVersion.V27.ToVersion());
            var completionList = GetCompletionSetCtrlSpace(0, "", factory: factory)
                .Completions
                .ToArray();

            var trueItems = completionList.Where(t => t.DisplayText == "True").ToArray();
            var falseItems = completionList.Where(t => t.DisplayText == "False").ToArray();
            var noneItems = completionList.Where(t => t.DisplayText == "None").ToArray();
            Assert.AreEqual(1, trueItems.Count());
            Assert.AreEqual(1, falseItems.Count());
            Assert.AreEqual(1, noneItems.Count());
            Assert.AreEqual("Constant", trueItems.Single().IconAutomationText);
            Assert.AreEqual("Constant", falseItems.Single().IconAutomationText);
            Assert.AreEqual("Keyword", noneItems.Single().IconAutomationText);

            factory = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(PythonLanguageVersion.V33.ToVersion());

            completionList = GetCompletionSetCtrlSpace(0, "", factory: factory)
                .Completions
                .ToArray();

            trueItems = completionList.Where(t => t.DisplayText == "True").ToArray();
            falseItems = completionList.Where(t => t.DisplayText == "False").ToArray();
            noneItems = completionList.Where(t => t.DisplayText == "None").ToArray();
            Assert.AreEqual(1, trueItems.Count());
            Assert.AreEqual(1, falseItems.Count());
            Assert.AreEqual(1, noneItems.Count());
            Assert.AreEqual("Keyword", trueItems.Single().IconAutomationText);
            Assert.AreEqual("Keyword", falseItems.Single().IconAutomationText);
            Assert.AreEqual("Keyword", noneItems.Single().IconAutomationText);
        }

        [TestMethod, Priority(0)]
        public void CtrlSpaceAfterKeyword() {
            // http://pytools.codeplex.com/workitem/560
            string code = @"
def h():
    return 

print 

";

            var completionList = GetCompletionSetCtrlSpace(
                code.IndexOf("return") + 7,
                code).Completions.Select(x => x.DisplayText).ToArray();

            AssertUtil.Contains(completionList, "any");

            completionList = GetCompletionSetCtrlSpace(
                code.IndexOf("print") + 6,
                code).Completions.Select(x => x.DisplayText).ToArray();

            AssertUtil.Contains(completionList, "any");
        }

        [TestMethod, Priority(0)]
        public void CtrlSpaceAfterNumber() {
            // http://pytools.codeplex.com/workitem/2323
            string code = @"
2
2.
2..
2.0.
";

            Assert.IsNull(GetCompletionSetCtrlSpace(
                code.IndexOf("2") + 1,
                code
            ));

            Assert.IsNull(GetCompletionSetCtrlSpace(
                code.IndexOf("2.") + 2,
                code
            ));

            var completionList = GetCompletionSetCtrlSpace(
                code.IndexOf("2..") + 3,
                code
            ).Completions.Select(x => x.DisplayText).ToArray();

            AssertUtil.ContainsAtLeast(completionList, "real", "imag");

            completionList = GetCompletionSetCtrlSpace(
                code.IndexOf("2.0.") + 4,
                code
            ).Completions.Select(x => x.DisplayText).ToArray();

            AssertUtil.ContainsAtLeast(completionList, "real", "imag");
        }

        [TestMethod, Priority(0)]
        public void ExceptionCompletions() {
            foreach (string code in new[] { 
                @"import sys
raise None", 
                @"import sys
raise (None", 
                @"import sys
try:
    pass
except None",
                @"import sys
try:
    pass
except (None",
            @"import sys
try:
    pass
except (ValueError, None"}) {
                var completionList = GetCompletionSetCtrlSpace(code.IndexOf("None"), code).Completions.Select(x => x.DisplayText).ToArray();

                AssertUtil.ContainsAtLeast(completionList,
                    "Exception",
                    "KeyboardInterrupt",
                    "GeneratorExit",
                    "StopIteration",
                    "SystemExit",
                    "sys"
                );

                AssertUtil.DoesntContain(completionList, "Warning");
                AssertUtil.DoesntContain(completionList, "str");
                AssertUtil.DoesntContain(completionList, "int");
            }

            foreach (string code in new[] { 
                @"import sys
raise (sys.None", 
                @"import sys
try:
    pass
except (sys.None"}) {
                var completionList = GetCompletionSetCtrlSpace(code.IndexOf("None"), code).Completions.Select(x => x.DisplayText).ToArray();

                AssertUtil.DoesntContain(completionList, "Exception");
                AssertUtil.DoesntContain(completionList, "KeyboardInterrupt");
                AssertUtil.DoesntContain(completionList, "GeneratorExit");
                AssertUtil.DoesntContain(completionList, "StopIteration");
                AssertUtil.DoesntContain(completionList, "SystemExit");

                AssertUtil.ContainsAtLeast(completionList,
                    "modules",
                    "path",
                    "version"
                );
            }
        }

        [TestMethod, Priority(0)]
        public void MemberCompletions() {
            // TODO: Negative tests
            //       Import / from import tests
            MemberCompletionTest(-1, "x = 2\r\nx.", "x");

            // combining various partial expressions with previous expressions
            var prefixes = new[] { "", "(", "a = ", "f(", "l[", "{", "if " };
            var exprs = new[] {
                "f(a=x).",
                "x[0].",
                "x(0).",
                "x.",
                "x.y.",
                "f(x[2]).",
                "f(x, y).",
                "f({2:3}).",
                "f(a + b).",
                "f(a or b).",
                "{2:3}.",
                "f(x if False else y).",
                "(\r\nx\r\n).",
            };
            foreach (var prefix in prefixes) {
                foreach (var expr in exprs) {
                    string test = prefix + expr;
                    Console.WriteLine("   -- {0}", test);
                    MemberCompletionTest(-1, test, expr.TrimEnd('.'));
                }
            }
        }

        [TestMethod, Priority(0)]
        public void SignatureHelp() {
            var prefixes = new[] { "", "(", "a = ", "f(", "l[", "{", "if " };
            var sigs = new[] { 
                new { Expr = "f(", Param = 0, Function="f" } ,
                new { Expr = "f(1,", Param = 1, Function="f" },
                new { Expr = "f(1, 2,", Param = 2, Function="f" }, 
                new { Expr = "f(1, (1, 2),", Param = 2, Function="f" }, 
                new { Expr = "f(1, a + b,", Param = 2, Function="f" }, 
                new { Expr = "f(1, a or b,", Param = 2, Function="f" }, 
                new { Expr = "f(1, a if True else b,", Param = 2, Function="f" }, 
                new { Expr = "a.f(1, a if True else b,", Param = 2, Function="a.f" }, 
                new { Expr = "a().f(1, a if True else b,", Param = 2, Function="a().f" }, 
                new { Expr = "a(2, 3, 4).f(1, a if True else b,", Param = 2, Function="a(2, 3, 4).f" }, 
                new { Expr = "a(2, (3, 4), 4).f(1, a if True else b,", Param = 2, Function="a(2, (3, 4), 4).f" }, 
                new { Expr = "f(lambda a, b, c: 42", Param = 0, Function="f" } ,
                new { Expr = "f(lambda a, b, c", Param = 0, Function="f" } ,
                new { Expr = "f(lambda a: lambda b, c: 42", Param = 0, Function="f" } ,
                new { Expr = "f(z, lambda a, b, c: 42", Param = 1, Function="f" } ,
                new { Expr = "f(z, lambda a, b, c", Param = 1, Function="f" } ,
                new { Expr = "f(z, lambda a: lambda b, c: 42", Param = 1, Function="f" } ,
                new { Expr = "f([a for b in c", Param = 0, Function="f" },
                // No function for f(( because ReverseExpressionParser will stop at the unmatched (
                new { Expr = "f((a for b in c", Param = 0, Function="" },
                new { Expr = "f({a for b in c", Param = 0, Function="f" },
                new { Expr = "f([a for b in c],", Param = 1, Function="f" },
                new { Expr = "f((a for b in c),", Param = 1, Function="f" },
                new { Expr = "f({a for b in c},", Param = 1, Function="f" },
                new { Expr = "f(0, [a for b in c],", Param = 2, Function="f" },
                new { Expr = "f(0, (a for b in c),", Param = 2, Function="f" },
                new { Expr = "f(0, {a for b in c},", Param = 2, Function="f" },
                new { Expr = "f([1,2", Param = 0, Function="f" },
                new { Expr = "f([1,2,", Param = 0, Function="f" },
                new { Expr = "f({1:2,", Param = 0, Function="f" },
                new { Expr = "f({1,", Param = 0, Function="f" },
                new { Expr = "f({1:2", Param = 0, Function="f" },
            };

            foreach (var prefix in prefixes) {
                foreach (var sig in sigs) {
                    var test = prefix + sig.Expr;
                    Console.WriteLine("   -- {0}", test);
                    SignatureTest(-1, test, sig.Function, sig.Param);
                }
            }
        }

        [TestMethod, Priority(0)]
        public void SignatureHelpStarArgs() {
            SignatureAnalysis sigResult;
            SignatureTest(-1, @"def f(a, *b, c=None): pass
f(1, 2, 3, 4,", "f", 4, PythonLanguageVersion.V27, true, out sigResult);
            Assert.IsTrue(sigResult.Signatures.Count >= 1, "No signature analysis results");
            Assert.AreEqual("*b", sigResult.Signatures[0].CurrentParameter.Name);
        }

        [TestMethod, Priority(0)]
        public void ImportCompletions() {
            var code = "import ";
            var completions = GetCompletionSetCtrlSpace(-1, code);
            AssertUtil.Contains(GetCompletionNames(completions), "sys");

            code = "import sys";
            completions = GetCompletionSetCtrlSpace(-1, code);
            AssertUtil.Contains(GetCompletionNames(completions), "sys");

            code = "import sys ";
            completions = GetCompletionSetCtrlSpace(-1, code);
            AssertUtil.ContainsExactly(GetCompletionNames(completions), "as");

            code = "import sys as";
            completions = GetCompletionSetCtrlSpace(-1, code);
            AssertUtil.ContainsExactly(GetCompletionNames(completions), "as");

            code = "import sys as s, ";
            completions = GetCompletionSetCtrlSpace(-1, code);
            AssertUtil.Contains(GetCompletionNames(completions), "sys");

            code = "import sys, ";
            completions = GetCompletionSetCtrlSpace(-1, code);
            AssertUtil.Contains(GetCompletionNames(completions), "exceptions");

            code = "import sys, ex";
            completions = GetCompletionSetCtrlSpace(-1, code);
            AssertUtil.Contains(GetCompletionNames(completions), "exceptions");
        }

        [TestMethod, Priority(0)]
        public void FromImportCompletions() {
            var code = "from ";
            var completions = GetCompletionSetCtrlSpace(-1, code);
            AssertUtil.ContainsAtLeast(GetCompletionNames(completions), "nt", "sys");

            code = "from s";
            completions = GetCompletionSetCtrlSpace(-1, code);
            AssertUtil.ContainsAtLeast(GetCompletionNames(completions), "sys");
            AssertUtil.DoesntContain(GetCompletionNames(completions), "nt");

            code = "from sys ";
            completions = GetCompletionSetCtrlSpace(-1, code);
            AssertUtil.ContainsExactly(GetCompletionNames(completions), "import");

            code = "from sys import";
            completions = GetCompletionSetCtrlSpace(-1, code);
            AssertUtil.ContainsExactly(GetCompletionNames(completions), "import");

            code = "from sys import ";
            completions = GetCompletionSetCtrlSpace(-1, code);
            AssertUtil.ContainsAtLeast(GetCompletionNames(completions),
                "*",                    // Contains *
                "settrace",             // Contains functions
                "api_version"           // Contains data members
            );

            code = "from sys.";
            completions = GetCompletionSetCtrlSpace(-1, code);
            // There will be one completion saying that there are no completions
            Assert.AreEqual(1, completions.Completions.Count);

            // Error case - no completions
            code = "from sys. import ";
            completions = GetCompletionSetCtrlSpace(-1, code);
            Assert.IsNull(completions);

            code = "from sys import settrace ";
            completions = GetCompletionSetCtrlSpace(-1, code);
            AssertUtil.ContainsExactly(GetCompletionNames(completions), "as");

            code = "from sys import settrace as";
            completions = GetCompletionSetCtrlSpace(-1, code);
            AssertUtil.ContainsExactly(GetCompletionNames(completions), "as");

            code = "from sys import settrace,";
            completions = GetCompletionSetCtrlSpace(-1, code);
            AssertUtil.ContainsAtLeast(GetCompletionNames(completions), "api_version", "settrace");
            AssertUtil.DoesntContain(GetCompletionNames(completions), "*");

            // No more completions after a *
            code = "from sys import *, ";
            completions = GetCompletionSetCtrlSpace(-1, code);
            Assert.IsNull(completions);

            code = "from sys import settrace as ";
            completions = GetCompletionSetCtrlSpace(-1, code);
            Assert.IsNull(completions);

            code = "from sys import settrace as st ";
            completions = GetCompletionSetCtrlSpace(-1, code);
            Assert.IsNull(completions);

            code = "from sys import settrace as st, ";
            completions = GetCompletionSetCtrlSpace(-1, code);
            AssertUtil.ContainsAtLeast(GetCompletionNames(completions), "api_version", "settrace");
            AssertUtil.DoesntContain(GetCompletionNames(completions), "*");

        }


        [TestMethod, Priority(0)]
        public void FromOSPathImportCompletions2x() {
            using (var db = MockCompletionDB.Create(PythonLanguageVersion.V27, "os", "ntpath", "posixpath", "os2emxpath")) {
                OSPathImportTest(db);
            }
        }

        [TestMethod, Priority(0)]
        public void FromOSPathImportCompletions3x() {
            using (var db = MockCompletionDB.Create(PythonLanguageVersion.V33, "os", "ntpath", "posixpath", "os2emxpath")) {
                OSPathImportTest(db);
            }
        }

        private static void OSPathImportTest(MockCompletionDB db) {
            var code = "from ";
            var completions = GetCompletionSetCtrlSpace(-1, code, factory: db.Factory);
            AssertUtil.ContainsAtLeast(GetCompletionNames(completions), "os", "sys");

            code = "from o";
            completions = GetCompletionSetCtrlSpace(-1, code, factory: db.Factory);
            AssertUtil.ContainsAtLeast(GetCompletionNames(completions), "os");
            AssertUtil.DoesntContain(GetCompletionNames(completions), "sys");

            code = "from os ";
            completions = GetCompletionSetCtrlSpace(-1, code, factory: db.Factory);
            AssertUtil.ContainsExactly(GetCompletionNames(completions), "import");

            code = "from os import";
            completions = GetCompletionSetCtrlSpace(-1, code, factory: db.Factory);
            AssertUtil.ContainsExactly(GetCompletionNames(completions), "import");

            code = "from os import ";
            completions = GetCompletionSetCtrlSpace(-1, code, factory: db.Factory);
            AssertUtil.ContainsAtLeast(GetCompletionNames(completions), "path");

            code = "from os.";
            completions = GetCompletionSetCtrlSpace(-1, code, factory: db.Factory);
            AssertUtil.ContainsExactly(GetCompletionNames(completions), "path");

            code = "from os.path import ";
            completions = GetCompletionSetCtrlSpace(-1, code, factory: db.Factory);
            AssertUtil.ContainsAtLeast(GetCompletionNames(completions), "abspath", "relpath");

            var allNames = new HashSet<string>();
            code = "from ntpath import ";
            completions = GetCompletionSetCtrlSpace(-1, code, factory: db.Factory);
            allNames.UnionWith(GetCompletionNames(completions));
            code = "from posixpath import ";
            completions = GetCompletionSetCtrlSpace(-1, code, factory: db.Factory);
            allNames.UnionWith(GetCompletionNames(completions));

            code = "from os.path import ";
            completions = GetCompletionSetCtrlSpace(-1, code, factory: db.Factory);
            var osNames = new HashSet<string>(GetCompletionNames(completions));
            AssertUtil.ContainsAtLeast(osNames, allNames);
        }

        [TestMethod, Priority(0)]
        public void FromImportMultilineCompletions() {
            var code = "from sys import (";
            var completions = GetCompletionSetCtrlSpace(-1, code);
            AssertUtil.ContainsAtLeast(GetCompletionNames(completions), "settrace", "api_version");
            AssertUtil.DoesntContain(GetCompletionNames(completions), "*");

            code = "from nt import (\r\n    ";
            completions = GetCompletionSetCtrlSpace(-1, code);
            AssertUtil.ContainsAtLeast(GetCompletionNames(completions), "abort", "W_OK");
            AssertUtil.DoesntContain(GetCompletionNames(completions), "*");

            code = "from nt import (getfilesystemencoding,\r\n    ";
            completions = GetCompletionSetCtrlSpace(-1, code);
            AssertUtil.ContainsAtLeast(GetCompletionNames(completions), "abort", "W_OK");
            AssertUtil.DoesntContain(GetCompletionNames(completions), "*");

            // Need a comma for more completions
            code = "from sys import (settrace\r\n    ";
            completions = GetCompletionSetCtrlSpace(-1, code);
            AssertUtil.ContainsExactly(GetCompletionNames(completions), "as");
        }

        private static IEnumerable<string> GetCompletionNames(CompletionSet completions) {
            foreach (var comp in completions.Completions) {
                yield return comp.InsertionText;
            }
        }

        private static IEnumerable<string> GetCompletionNames(CompletionAnalysis analysis) {
            return GetCompletionNames(analysis.GetCompletions(new MockGlyphService()));
        }

        [TestMethod, Priority(0)]
        public void Scenario_CompletionInTripleQuotedString() {
            string code = @"
'''
fob
oar

baz
'''
";

            for (int i = code.IndexOf("'''") + 4; i < code.LastIndexOf("'''"); i++) {
                Console.WriteLine(i);
                var analysis = AnalyzeExpression(i, code, forCompletion: false);

                var fact = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(PythonLanguageVersion.V27.ToVersion());
                var serviceProvider = PythonToolsTestUtilities.CreateMockServiceProvider();
                using (var analyzer = new VsProjectAnalyzer(serviceProvider, fact, new[] { fact })) {
                    var buffer = MockTextBuffer(code);
                    buffer.AddProperty(typeof(VsProjectAnalyzer), analyzer);
                    var classifierProvider = new PythonClassifierProvider(new MockContentTypeRegistryService(PythonCoreConstants.ContentType), serviceProvider);
                    classifierProvider._classificationRegistry = new MockClassificationTypeRegistryService();
                    classifierProvider.GetClassifier(buffer);
                    var snapshot = (MockTextSnapshot)buffer.CurrentSnapshot;
                    var context = snapshot.GetCompletions(
                        serviceProvider,
                        new MockTrackingSpan(snapshot, i, 0),
                        new MockTrackingPoint(snapshot, i),
                        new CompletionOptions { HideAdvancedMembers = false });
                    Assert.AreEqual(NormalCompletionAnalysis.EmptyCompletionContext, context);
                }
            }
        }

        [TestMethod, Priority(0)]
        public void GotoDefinition() {
            string code = @"
class C:
    def fff(self): pass

C().fff";

            //var emptyAnalysis = AnalyzeExpression(0, code);
            //AreEqual(emptyAnalysis.Expression, "");

            for (int i = -1; i >= -3; i--) {
                var analysis = AnalyzeExpression(i, code, forCompletion: false);
                Assert.AreEqual(analysis.Expression, "C().fff");
            }

            var classAnalysis = AnalyzeExpression(-6, code, forCompletion: false);
            Assert.AreEqual(classAnalysis.Expression, "C()");

            var defAnalysis = AnalyzeExpression(code.IndexOf("def fff") + 4, code, forCompletion: false);
            Assert.AreEqual(defAnalysis.Expression, "fff");
        }

        [TestMethod, Priority(0)]
        public void QuickInfo() {
            string code = @"
x = ""ABCDEFGHIJKLMNOPQRSTUVWYXZ""
cls._parse_block(ast.expr)


f(a,
(b, c, d),
e)


def f():
    """"""helpful information
    
    
""""""

while True:
    pass

lambda larg1, larg2: None";


            // we get the appropriate subexpression
            TestQuickInfo(code, code.IndexOf("cls."), code.IndexOf("cls.") + 4, "cls: <unknown type>");
            TestQuickInfo(code, code.IndexOf("cls.") + 4 + 1, code.IndexOf("cls.") + 4 + 1 + 11, "cls._parse_block: <unknown type>");
            TestQuickInfo(code, code.IndexOf("cls.") + 4 + 1 + 11 + 1, code.IndexOf("cls.") + 4 + 1 + 11 + 1 + 4, "ast: <unknown type>");
            TestQuickInfo(code, code.IndexOf("cls.") + 4 + 1 + 11 + 1 + 4 + 1, code.IndexOf("cls.") + 4 + 1 + 11 + 1 + 4 + 1 + 3, "ast.expr: <unknown type>");
            TestQuickInfo(code, code.IndexOf("cls.") + 4 + 1 + 11 + 1 + 4 + 1 + 3 + 1, code.IndexOf("cls.") + 4 + 1 + 11 + 1 + 5 + 3 + 1 + 1, "cls._parse_block(ast.expr): <unknown type>");

            // the whole strieng shows up in quick info
            TestQuickInfo(code, code.IndexOf("x = ") + 4, code.IndexOf("x = ") + 4 + 28, "\"ABCDEFGHIJKLMNOPQRSTUVWYXZ\": str");

            // trailing new lines don't show up in quick info
            TestQuickInfo(code, code.IndexOf("def f") + 4, code.IndexOf("def f") + 5, "f: def f()\r\nhelpful information");

            // keywords don't show up in quick info
            TestQuickInfo(code, code.IndexOf("while True:"), code.IndexOf("while True:") + 5);

            // 'lambda' keyword doesn't show up in quick info
            TestQuickInfo(code, code.IndexOf("lambda"), code.IndexOf("lambda") + 6);
            // but its arguments do
            TestQuickInfo(code, code.IndexOf("larg1"), code.IndexOf("larg1") + 5, "larg1: <unknown type>");
            TestQuickInfo(code, code.IndexOf("larg2"), code.IndexOf("larg2") + 5, "larg2: <unknown type>");

            // multiline function, hover at the close paren
            TestQuickInfo(code, code.IndexOf("e)") + 1, code.IndexOf("e)") + 2, @"f(a,
(b, c, d),
e): <unknown type>");
        }

        [TestMethod, Priority(0)]
        public void NormalOverrideCompletions() {
            foreach (var code in new[] {
@"class Fob(object):
    def func_a(self, a=100): pass
    def func_b(self, b, *p, **kw): pass

class Baz(Fob):
    def None
",
@"class Fob(object):
    def func_a(self, a=100): pass

class Oar(Fob):
    def func_b(self, b, *p, **kw): pass

class Baz(Oar):
    def None
",
@"class Fob(object):
    def func_a(self, a=100): pass

class Oar(object):
    def func_b(self, b, *p, **kw): pass

class Baz(Fob, Oar):
    def None
",
@"class Fob(object):
    def func_a(self, a=100): pass
    def func_b(self, b, *p, **kw): pass
    def func_c(self): pass

class Baz(Fob):
    def func_c(self): pass
    def None
",
@"class Fob(object):
    def func_a(self, a=100): pass
    def func_c(self): pass

class Oar(Fob):
    def func_b(self, b, *p, **kw): pass

class Baz(Oar):
    def func_c(self): pass
    def None
",
@"class Fob(object):
    def func_a(self, a=100): pass

class Oar(object):
    def func_b(self, b, *p, **kw): pass
    def func_c(self): pass

class Baz(Fob, Oar):
    def func_c(self): pass
    def None
"}) {
                var completionList = GetCompletionSetCtrlSpace(code.IndexOf("None"), code).Completions.Select(x => x.InsertionText).ToArray();

                Console.WriteLine(code);
                AssertUtil.Contains(completionList, @"func_a(self, a = 100):
        return super(Baz, self).func_a(a)");
                AssertUtil.Contains(completionList, @"func_b(self, b, *p, **kw):
        return super(Baz, self).func_b(b, *p, **kw)");
            }
        }

        [TestMethod, Priority(0)]
        public void BuiltinOverrideCompletions() {
            var code = @"class Fob(str):
    def None
";
            var completionList = GetCompletionSetCtrlSpace(code.IndexOf("None"), code).Completions.Select(x => x.InsertionText).ToArray();

            AssertUtil.Contains(completionList, @"capitalize(self):
        return super(Fob, self).capitalize()");
            AssertUtil.Contains(completionList, @"index(self, sub, start, end):
        return super(Fob, self).index(sub, start, end)");

            code = @"class Fob(str, list):
    def None
";
            completionList = GetCompletionSetCtrlSpace(code.IndexOf("None"), code).Completions.Select(x => x.InsertionText).ToArray();
            AssertUtil.Contains(completionList, @"index(self, sub, start, end):
        return super(Fob, self).index(sub, start, end)");

            code = @"class Fob(list, str):
    def None
";
            completionList = GetCompletionSetCtrlSpace(code.IndexOf("None"), code).Completions.Select(x => x.InsertionText).ToArray();
            AssertUtil.Contains(completionList, @"index(self, item, start, stop):
        return super(Fob, self).index(item, start, stop)");
        }

        [TestMethod, Priority(0)]
        public void OverridesWithMismatchedAnalysis() {
            // Here we create a buffer and analyze. We then add some newlines
            // and a def, expecting completions from A (int). Because the def
            // has moved down into class B, GetCompletions() rolls it back
            // to find out where in the analysis to look - without this, we
            // would get completions for dict rather than int.
            var code = @"
class A(int):
    pass

class B(dict):
    pass

";

            var editText = "\r\n\r\n\r\n    def ";
            var editInsert = code.IndexOf("pass") + 4;
            var completions = EditAndGetCompletions(code, editText, editInsert, "def ");

            AssertUtil.ContainsAtLeast(completions, "bit_length", "conjugate");
            AssertUtil.DoesntContain(completions, "keys");

            editText = "\r\n\r\n\r\n\r\n\r\n    def ";
            editInsert = code.IndexOf("pass", editInsert) + 4;
            completions = EditAndGetCompletions(code, editText, editInsert, "def ");

            AssertUtil.ContainsAtLeast(completions, "keys", "__contains__");
            AssertUtil.DoesntContain(completions, "bit_length");
        }

        [TestMethod, Priority(0)]
        public void HideAdvancedMembers() {
            // No text - expect all non-advanced members
            AssertUtil.ContainsExactly(HideAdvancedMembersHelper("", "a", "b", "__c__", "__d_e__", "_f_"),
                "a", "b", "_f_"
            );

            // Matches one item, so we should only see that
            AssertUtil.ContainsExactly(HideAdvancedMembersHelper("a", "a", "b", "__c__", "__d_e__", "_f_"),
                "a"
            );

            // Matches one hidden item - expect all non-advanced members
            AssertUtil.ContainsExactly(HideAdvancedMembersHelper("c", "a", "b", "__c__", "__d_e__", "_f_"),
                "a", "b", "_f_"
            );

            // Matches one item and advanced members
            AssertUtil.ContainsExactly(HideAdvancedMembersHelper("__", "a", "b", "__c__", "__d_e__", "_f_"),
                "_f_",
                "__c__",
                "__d_e__"
            );
        }

        private IEnumerable<string> HideAdvancedMembersHelper(string text, params string[] completions) {
            var buffer = MockTextBuffer(text);
            var snapshot = buffer.CurrentSnapshot;
            var set = new FuzzyCompletionSet(
                "Test Completions",
                "Test Completions",
                snapshot.CreateTrackingSpan(0, snapshot.Length, SpanTrackingMode.EdgeInclusive),
                completions.Select(c => new DynamicallyVisibleCompletion(c)),
                new CompletionOptions { HideAdvancedMembers = true, IntersectMembers = false },
                CompletionComparer.UnderscoresLast
            );
            set.Filter();
            return set.Completions.Select(c => c.DisplayText);
        }

        private static IEnumerable<string> GenerateText(int lines, int width, string prefix = "") {
            var rand = new Random();
            const string VALID_CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.(),     '\"";
            for (int i = 0; i < lines; ++i) {
                yield return prefix + new String(Enumerable.Repeat(0, width - prefix.Length)
                    .Select(_ => rand.Next(VALID_CHARS.Length))
                    .Select(j => VALID_CHARS[j])
                    .ToArray()
                );
            }
        }

        [TestMethod, Priority(0)]
        public void CompletionWithLongDocString() {
            var docString = GenerateText(100, 72, "    ").ToArray();
            string code = @"
def func(a):
    '''" + string.Join(Environment.NewLine, docString) + @"'''
    pass

";

            // Because there is an extra line added for the Quick Info we only
            // want the first 29 lines of the docstring. For signature docs,
            // we'll cut to 15 lines.
            var expected1 = string.Join(Environment.NewLine, docString.Take(29)) + Environment.NewLine + "...";
            var expected2 = string.Join(Environment.NewLine, docString.Take(15)).TrimStart() + Environment.NewLine + "...";

            TestQuickInfo(code, code.IndexOf("func"), code.IndexOf("func") + 4, "func: def func(a)\r\n" + expected1);

            SignatureAnalysis sigs;
            SignatureTest(-1, code + "func(", "func", 0, PythonLanguageVersion.V27, true, out sigs);
            Assert.AreEqual(1, sigs.Signatures.Count);
            Assert.AreEqual(1, sigs.Signatures[0].Parameters.Count);
            Assert.AreEqual(expected2, sigs.Signatures[0].Documentation);

            docString = GenerateText(100, 250, "    ").ToArray();
            code = @"
def func(a):
    '''" + string.Join(Environment.NewLine, docString) + @"'''
    pass

";

            // The long lines cause us to truncate sooner.
            expected1 = string.Join(Environment.NewLine, docString.Take(15)) + Environment.NewLine + "...";
            expected2 = string.Join(Environment.NewLine, docString.Take(8)).TrimStart() + Environment.NewLine + "...";

            TestQuickInfo(code, code.IndexOf("func"), code.IndexOf("func") + 4, "func: def func(a)\r\n" + expected1);

            SignatureTest(-1, code + "func(", "func", 0, PythonLanguageVersion.V27, true, out sigs);
            Assert.AreEqual(1, sigs.Signatures.Count);
            Assert.AreEqual(1, sigs.Signatures[0].Parameters.Count);
            Assert.AreEqual(expected2, sigs.Signatures[0].Documentation);
        }

        [TestMethod, Priority(0)]
        public void ClassCompletionOutsideFunction() {
            // Note that "eggs_and_spam" is longer than the indentation of each
            // scope.
            string code = @"
eggs_and_spam = 'abc'

class Spam(object):
    eggs_and_spam = 123

    def f(self, eggs_and_spam = 3.14):
        #1
        pass

    #2

#3
";

            var testCode = code.Replace("#1", "eggs_and_spam.");
            var completionList = GetCompletionSetCtrlSpace(testCode.IndexOf("eggs_and_spam.") + 14, testCode)
                .Completions.Select(x => x.DisplayText).ToArray();

            AssertUtil.ContainsAtLeast(completionList, "real", "imag");
            AssertUtil.DoesntContain(completionList, "lower");

            testCode = code.Replace("#2", "eggs_and_spam.");
            completionList = GetCompletionSetCtrlSpace(testCode.IndexOf("eggs_and_spam.") + 14, testCode)
                .Completions.Select(x => x.DisplayText).ToArray();

            AssertUtil.ContainsAtLeast(completionList, "bit_length");

            testCode = code.Replace("#3", "eggs_and_spam.");
            completionList = GetCompletionSetCtrlSpace(testCode.IndexOf("eggs_and_spam.") + 14, testCode)
                .Completions.Select(x => x.DisplayText).ToArray();

            AssertUtil.ContainsAtLeast(completionList, "lower", "center");
        }


        [TestMethod, Priority(0)]
        public void LoadAndUnloadModule() {
            var factories = new[] { InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(new Version(3, 3)) };
            using (var analyzer = new VsProjectAnalyzer(PythonToolsTestUtilities.CreateMockServiceProvider(), factories[0], factories)) {
                var m1Path = TestData.GetPath("TestData\\SimpleImport\\module1.py");
                var m2Path = TestData.GetPath("TestData\\SimpleImport\\module2.py");

                var entry1 = analyzer.AnalyzeFile(m1Path) as IPythonProjectEntry;
                var entry2 = analyzer.AnalyzeFile(m2Path) as IPythonProjectEntry;
                analyzer.WaitForCompleteAnalysis(_ => true);

                AssertUtil.ContainsExactly(
                    analyzer.Project.GetEntriesThatImportModule("module1", true).Select(m => m.ModuleName),
                    "module2"
                );

                AssertUtil.ContainsExactly(
                    entry2.Analysis.GetValuesByIndex("x", 0).Select(v => v.TypeId),
                    BuiltinTypeId.Int
                );

                analyzer.UnloadFile(entry1);
                analyzer.WaitForCompleteAnalysis(_ => true);

                // Even though module1 has been unloaded, we still know that
                // module2 imports it.
                AssertUtil.ContainsExactly(
                    analyzer.Project.GetEntriesThatImportModule("module1", true).Select(m => m.ModuleName),
                    "module2"
                );

                AssertUtil.ContainsExactly(
                    entry2.Analysis.GetValuesByIndex("x", 0).Select(v => v.TypeId)
                );

                analyzer.AnalyzeFile(m1Path);
                analyzer.WaitForCompleteAnalysis(_ => true);

                AssertUtil.ContainsExactly(
                    analyzer.Project.GetEntriesThatImportModule("module1", true).Select(m => m.ModuleName),
                    "module2"
                );

                AssertUtil.ContainsExactly(
                    entry2.Analysis.GetValuesByIndex("x", 0).Select(v => v.TypeId),
                    BuiltinTypeId.Int
                );
            }
        }

        private static MockTextBuffer MockTextBuffer(string code) {
            return new MockTextBuffer(code, PythonCoreConstants.ContentType, "C:\\fob.py");
        }

        private static HashSet<string> EditAndGetCompletions(
            string code,
            string editText,
            int editInsert,
            string completeAfter,
            PythonLanguageVersion version = PythonLanguageVersion.V27
        ) {
            CompletionAnalysis context;

            var fact = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(version.ToVersion());

            var serviceProvider = PythonToolsTestUtilities.CreateMockServiceProvider();
            var analyzer = new VsProjectAnalyzer(serviceProvider, fact, new[] { fact });
            try {
                var buffer = MockTextBuffer(code);
                buffer.AddProperty(typeof(VsProjectAnalyzer), analyzer);
                var classifierProvider = new PythonClassifierProvider(new MockContentTypeRegistryService(PythonCoreConstants.ContentType), serviceProvider);
                classifierProvider._classificationRegistry = new MockClassificationTypeRegistryService();
                classifierProvider.GetClassifier(buffer);
                var snapshot = (MockTextSnapshot)buffer.CurrentSnapshot;

                var textView = new MockTextView(buffer);
                var monitoredBuffer = analyzer.MonitorTextBuffer(textView, buffer);

                var tcs = new TaskCompletionSource<object>();
                ((IPythonProjectEntry)monitoredBuffer.ProjectEntry).OnNewAnalysis += (s, e) => tcs.SetResult(null);

                analyzer.WaitForCompleteAnalysis(x => true);

                tcs.Task.GetAwaiter().GetResult();
                analyzer.StopMonitoringTextBuffer(monitoredBuffer.BufferParser, textView);

                var edit = buffer.CreateEdit();
                edit.Insert(editInsert, editText);
                edit.Apply();

                var newSnapshot = (MockTextSnapshot)snapshot.Version.Next.TextBuffer.CurrentSnapshot;
                Assert.AreNotSame(snapshot, newSnapshot);
                int location = newSnapshot.GetText().IndexOf(completeAfter) + completeAfter.Length;

                context = newSnapshot.GetCompletions(
                    serviceProvider, 
                    new MockTrackingSpan(newSnapshot, location, 0),
                    new MockTrackingPoint(newSnapshot, location),
                    new CompletionOptions {
                        HideAdvancedMembers = false,
                        ConvertTabsToSpaces = true,
                        IndentSize = 4
                    }
                );

                var completions = context.GetCompletions(new MockGlyphService()).Completions
                    .Select(c => c.DisplayText).ToSet();
                return completions;
            } finally {
                analyzer.Dispose();
            }
        }

        private static void TestQuickInfo(string code, int start, int end, params string[] expected) {

            for (int i = start; i < end; i++) {
                var quickInfo = AnalyzeQuickInfo(i, code, forCompletion: false);

                Assert.AreEqual(expected.Length, quickInfo.Count);
                for (int j = 0; j < expected.Length; j++) {
                    Assert.AreEqual(expected[j], quickInfo[j]);
                }
            }
        }

        private static ExpressionAnalysis AnalyzeExpression(int location, string sourceCode, bool forCompletion = true) {
            if (location < 0) {
                location = sourceCode.Length + location + 1;
            }
            var fact = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(PythonLanguageVersion.V27.ToVersion());
            var serviceProvider = PythonToolsTestUtilities.CreateMockServiceProvider();
            using (var analyzer = new VsProjectAnalyzer(serviceProvider, fact, new[] { fact })) {
                return AnalyzeExpressionWorker(serviceProvider, location, sourceCode, forCompletion, analyzer);
            }
        }

        private static List<object> AnalyzeQuickInfo(int location, string sourceCode, bool forCompletion = true) {
            if (location < 0) {
                location = sourceCode.Length + location + 1;
            }
            var fact = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(PythonLanguageVersion.V27.ToVersion());
            var serviceProvider = PythonToolsTestUtilities.CreateMockServiceProvider();
            using (var analyzer = new VsProjectAnalyzer(serviceProvider, fact, new[] { fact })) {
                var analysis = AnalyzeExpressionWorker(serviceProvider, location, sourceCode, forCompletion, analyzer);

                List<object> quickInfo = new List<object>();
                ITrackingSpan span;
                QuickInfoSource.AugmentQuickInfoWorker(
                    analysis,
                    quickInfo,
                    out span);

                return quickInfo;
            }
        }

        private static ExpressionAnalysis AnalyzeExpressionWorker(IServiceProvider serviceProvider, int location, string sourceCode, bool forCompletion, VsProjectAnalyzer analyzer) {
            try {
                var buffer = MockTextBuffer(sourceCode);
                buffer.AddProperty(typeof(VsProjectAnalyzer), analyzer);
                var classifierProvider = new PythonClassifierProvider(new MockContentTypeRegistryService(PythonCoreConstants.ContentType), serviceProvider);
                classifierProvider._classificationRegistry = new MockClassificationTypeRegistryService();
                classifierProvider.GetClassifier(buffer);
                var textView = new MockTextView(buffer);
                var item = analyzer.MonitorTextBuffer(textView, textView.TextBuffer); // We leak here because we never un-monitor, but it's a test.
                while (!item.ProjectEntry.IsAnalyzed) {
                    Thread.Sleep(10);
                }

                var snapshot = (MockTextSnapshot)buffer.CurrentSnapshot;
                analyzer.StopMonitoringTextBuffer(item.BufferParser, textView);
                return snapshot.AnalyzeExpression(serviceProvider, new MockTrackingSpan(snapshot, location, location == snapshot.Length ? 0 : 1), forCompletion);
            } finally {
            }
        }

        private static void MemberCompletionTest(int location, string sourceCode, string expectedExpression) {
            var context = GetCompletions(location, sourceCode);
            Assert.IsInstanceOfType(context, typeof(NormalCompletionAnalysis));
            var normalContext = (NormalCompletionAnalysis)context;
            
            string text;
            SnapshotSpan statementExtent;
            normalContext.GetPrecedingExpression(out text, out statementExtent);
            Assert.AreEqual(expectedExpression, text);
        }

        private static CompletionAnalysis GetCompletions(int location, string sourceCode, bool intersectMembers = true) {
            if (location < 0) {
                location = sourceCode.Length + location + 1;
            }
            var fact = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(PythonLanguageVersion.V27.ToVersion());
            using (var analyzer = new VsProjectAnalyzer(PythonToolsTestUtilities.CreateMockServiceProvider(), fact, new[] { fact })) {
                return GetCompletionsWorker(location, sourceCode, intersectMembers, analyzer);
            }
        }

        private static CompletionSet GetCompletionSet(int location, string sourceCode, bool intersectMembers = true, IPythonInterpreterFactory factory = null) {
            if (location < 0) {
                location = sourceCode.Length + location + 1;
            }

            if (factory == null) {
                factory = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(PythonLanguageVersion.V27.ToVersion());
            }
            using (var analyzer = new VsProjectAnalyzer(PythonToolsTestUtilities.CreateMockServiceProvider(), factory, new[] { factory })) {
                return GetCompletionsWorker(location, sourceCode, intersectMembers, analyzer).GetCompletions(new MockGlyphService());
            }
        }

        /// <summary>
        /// Simulates the user hitting Ctrl-Space to get completions.
        /// </summary>
        private static CompletionSet GetCompletionSetCtrlSpace(int location, string sourceCode, bool intersectMembers = true, IPythonInterpreterFactory factory = null) {
            IntellisenseController.ForceCompletions = true;
            try {
                var completionSet = GetCompletionSet(location, sourceCode, intersectMembers, factory);
                if (completionSet != null) {
                    completionSet.Filter();
                }
                return completionSet;
            } finally {
                IntellisenseController.ForceCompletions = false;
            }
        }

        private static CompletionAnalysis GetCompletionsWorker(int location, string sourceCode, bool intersectMembers, VsProjectAnalyzer analyzer) {
            var buffer = MockTextBuffer(sourceCode);
            buffer.AddProperty(typeof(VsProjectAnalyzer), analyzer);
            var serviceProvider = PythonToolsTestUtilities.CreateMockServiceProvider();
            var classifierProvider = new PythonClassifierProvider(new MockContentTypeRegistryService(PythonCoreConstants.ContentType), serviceProvider);
            classifierProvider._classificationRegistry = new MockClassificationTypeRegistryService();
            classifierProvider.GetClassifier(buffer);
            var snapshot = (MockTextSnapshot)buffer.CurrentSnapshot;

            var textView = new MockTextView(buffer);
            var monitoredBuffer = analyzer.MonitorTextBuffer(textView, buffer);
            analyzer.WaitForCompleteAnalysis(x => true);
            while (buffer.GetPythonProjectEntry().Analysis == null) {
                System.Threading.Thread.Sleep(500);
            }
            analyzer.StopMonitoringTextBuffer(monitoredBuffer.BufferParser, textView);
            var span = snapshot.GetApplicableSpan(new SnapshotPoint(snapshot, location)) ??
                snapshot.CreateTrackingSpan(location, 0, SpanTrackingMode.EdgeInclusive);

            var context = snapshot.GetCompletions(
                serviceProvider,
                span,
                new MockTrackingPoint(snapshot, location),
                new CompletionOptions {
                    HideAdvancedMembers = false,
                    IntersectMembers = intersectMembers,
                    ConvertTabsToSpaces = true,
                    IndentSize = 4
                }
            );
            return context;
        }

        private static void SignatureTest(int location, string sourceCode, string expectedExpression, int paramIndex, PythonLanguageVersion version = PythonLanguageVersion.V27) {
            SignatureAnalysis dummy;
            SignatureTest(location, sourceCode, expectedExpression, paramIndex, version, false, out dummy);
        }

        private static void SignatureTest(int location, string sourceCode, string expectedExpression, int paramIndex, PythonLanguageVersion version, bool analyze, out SignatureAnalysis sigs) {
            if (location < 0) {
                location = sourceCode.Length + location;
            }
            var fact = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(version.ToVersion());

            var serviceProvider = PythonToolsTestUtilities.CreateMockServiceProvider();
            var analyzer = new VsProjectAnalyzer(serviceProvider, fact, new[] { fact });
            try {
                var buffer = MockTextBuffer(sourceCode);
                buffer.AddProperty(typeof(VsProjectAnalyzer), analyzer);
                var classifierProvider = new PythonClassifierProvider(new MockContentTypeRegistryService(PythonCoreConstants.ContentType), serviceProvider);
                classifierProvider._classificationRegistry = new MockClassificationTypeRegistryService();
                classifierProvider.GetClassifier(buffer);

                var snapshot = (MockTextSnapshot)buffer.CurrentSnapshot;

                if (analyze) {
                    var textView = new MockTextView(buffer);
                    var monitoredBuffer = analyzer.MonitorTextBuffer(textView, buffer);
                    analyzer.WaitForCompleteAnalysis(x => true);
                    while (((IPythonProjectEntry)buffer.GetProjectEntry()).Analysis == null) {
                        System.Threading.Thread.Sleep(500);
                    }
                    analyzer.StopMonitoringTextBuffer(monitoredBuffer.BufferParser, textView);
                }

                sigs = snapshot.GetSignatures(serviceProvider, new MockTrackingSpan(snapshot, location, 1));
                Assert.AreEqual(expectedExpression, sigs.Text, sourceCode);
                Assert.AreEqual(paramIndex, sigs.ParameterIndex, sourceCode);
            } finally {
                analyzer.Dispose();
            }
        }

#if FALSE
        [TestMethod, Priority(0)]
        public void Scenario_RemoteScriptFactory() {
            using (var factory = RemotePythonEvaluator.CreateFactory()) {
                var runtime = (ScriptRuntime)factory.CreateRuntime(Python.CreateRuntimeSetup(new Dictionary<string, object>()));
                StringWriter writer = new StringWriter();
                StringWriter errWriter = new StringWriter();

                runtime.IO.SetOutput(Stream.Null, writer);
                factory.SetConsoleOut(writer);
                factory.SetConsoleError(errWriter);

                // verify print goes to the correct output
                var engine = runtime.GetEngine("Python");
                engine.Execute("print 'hello'");
                var builder = writer.GetStringBuilder();

                Assert.AreEqual(builder.ToString(), "hello\r\n");
                builder.Clear();

                // verify Console.WriteLine is redirected
                engine.Execute("import System\nSystem.Console.WriteLine('hello')\n");
                Assert.AreEqual(builder.ToString(), "hello\r\n");
                builder.Clear();

                // verify Console.Error.WriteLine is redirected to stderr
                var errBuilder = errWriter.GetStringBuilder();
                engine.Execute("import System\nSystem.Console.Error.WriteLine('hello')\n");
                Assert.AreEqual(errBuilder.ToString(), "hello\r\n");
                errBuilder.Clear();

                // raise an exception, should be propagated back
                try {
                    engine.Execute("import System\nraise System.ArgumentException()\n");
                    Assert.AreEqual(true, false);
                } catch (ArgumentException) {
                }

                /*
                 // verify that all code runs on the same thread
                var scope = engine.CreateScope();
                engine.Execute("import System");

            
                List<object> res = new List<object>();
                for (int i = 0; i < 100; i++) {
                    ThreadPool.QueueUserWorkItem(
                        (x) => {
                            object value = engine.Execute("System.Threading.Thread.CurrentThread.ManagedThreadId", scope);
                            lock (res) {
                                res.Add(value);
                            }
                    });
                }

                while (res.Count != 100) {
                    Thread.Sleep(100);
                }

                for (int i = 1; i < res.Count; i++) {
                    if (!res[i - 1].Equals(res[i])) {
                        throw new Exception("running on multiple threads");
                    }
                }*/

                // create a long running work item, execute it, and then make sure we can continue to execute work items.
                ThreadPool.QueueUserWorkItem(x => {
                    engine.Execute("while True: pass");
                });
                Thread.Sleep(1000);
                factory.Abort();

                Assert.AreEqual((object)engine.Execute("42"), 42);
            }

            // check starting on an MTA thread
            using (var factory = new RemoteScriptFactory(ApartmentState.MTA)) {
                var runtime = (ScriptRuntime)factory.CreateRuntime(Python.CreateRuntimeSetup(new Dictionary<string, object>()));
                var engine = runtime.GetEngine("Python");
                Assert.AreEqual((object)engine.Execute("import System\nSystem.Threading.Thread.CurrentThread.ApartmentState == System.Threading.ApartmentState.MTA"), true);
            }

            // check starting on an STA thread
            using (var factory = new RemoteScriptFactory(ApartmentState.STA)) {
                var runtime = (ScriptRuntime)factory.CreateRuntime(Python.CreateRuntimeSetup(new Dictionary<string, object>()));
                var engine = runtime.GetEngine("Python");
                Assert.AreEqual((object)engine.Execute("import System\nSystem.Threading.Thread.CurrentThread.ApartmentState == System.Threading.ApartmentState.STA"), true);
            }
        }
#endif
    }
}

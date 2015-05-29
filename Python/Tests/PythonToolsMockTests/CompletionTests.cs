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
using Microsoft.VisualStudioTools.MockVsTests;
using TestUtilities;
using TestUtilities.Mocks;
using TestUtilities.Python;

namespace PythonToolsMockTests {
    [TestClass]
    public class CompletionTests {

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        [TestMethod, Priority(0), TestCategory("Mock")]
        public void GetApplicableSpanTest() {
            var text = "if fob.oar(eggs, spam<=ham) :";

            using (var view = new PythonEditor(text)) {
                var snapshot = view.CurrentSnapshot;

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
        }

        [TestMethod, Priority(0), TestCategory("Mock")]
        public void CtrlSpaceCompletions() {
            using (var view = new PythonEditor()) {
                view.Text = @"def f(param1, param2):
    g()";

                AssertUtil.ContainsAtLeast(view.GetCompletionsAfter("g("), "param1", "param2");

                view.Text = @"def f(param1, param2):
    g(param1, )";

                AssertUtil.ContainsAtLeast(view.GetCompletionsAfter("g(param1, "), "param1", "param2");

                // verify Ctrl-Space inside of a function gives proper completions
                foreach (var codeSnippet in new[] { @"def f():
    
    pass", @"def f():
    x = (2 + 3)
    
    pass
", @"def f():
    yield (2 + 3)
    
    pass" }) {

                    Debug.WriteLine(String.Format("Testing {0}", codeSnippet));

                    view.Text = codeSnippet;
                    AssertUtil.ContainsAtLeast(view.GetCompletions(codeSnippet.IndexOf("\r\n    pass")), "min", "assert");
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Mock")]
        public void KeywordCompletions() {
            using (var view = new PythonEditor(version: PythonLanguageVersion.V35)) {
                var completionList = new HashSet<string>(view.GetCompletions(0));

                // not in a function
                AssertUtil.DoesntContain(completionList, "yield");
                AssertUtil.DoesntContain(completionList, "return");
                AssertUtil.DoesntContain(completionList, "await");

                AssertUtil.ContainsAtLeast(completionList, "assert", "and", "async");

                var code = @"def f():
    |
    pass";

                view.Text = code.Replace("|", "");
                AssertUtil.ContainsAtLeast(view.GetCompletions(code.IndexOf("|")), "yield", "return", "async", "await");


                view.Text = "x = (abc, oar, )";
                completionList = new HashSet<string>(view.GetCompletionsAfter("oar, "));

                AssertUtil.ContainsAtLeast(completionList, "and");
                AssertUtil.DoesntContain(completionList, "def");
            }
        }

        [TestMethod, Priority(0), TestCategory("Mock")]
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
            using (var view = new PythonEditor(code)) {
                var completionList = view.GetCompletionsAfter("yield_");

                AssertUtil.DoesntContain(completionList, "yield");
                AssertUtil.Contains(completionList, "yield_expression");

                completionList = view.GetCompletionsAfter("yield");

                AssertUtil.Contains(completionList, "yield");
                AssertUtil.Contains(completionList, "yield_expression");

                completionList = view.GetCompletionsAfter("yiel");

                AssertUtil.Contains(completionList, "yield");
                AssertUtil.Contains(completionList, "yield_expression");
            }
        }

        [TestMethod, Priority(0), TestCategory("Mock")]
        public void TrueFalseNoneCompletions() {
            // http://pytools.codeplex.com/workitem/1905
            foreach (var version in new[] { PythonLanguageVersion.V27, PythonLanguageVersion.V33 }) {
                using (var view = new PythonEditor(version: version)) {
                    var completionList = view.GetCompletionList(0);

                    var trueItems = completionList.Where(t => t.DisplayText == "True").ToArray();
                    var falseItems = completionList.Where(t => t.DisplayText == "False").ToArray();
                    var noneItems = completionList.Where(t => t.DisplayText == "None").ToArray();
                    Assert.AreEqual(1, trueItems.Count());
                    Assert.AreEqual(1, falseItems.Count());
                    Assert.AreEqual(1, noneItems.Count());
                    if (version.Is3x()) {
                        Assert.AreEqual("Keyword", trueItems[0].IconAutomationText);
                        Assert.AreEqual("Keyword", falseItems[0].IconAutomationText);
                    } else {
                        Assert.AreEqual("Constant", trueItems[0].IconAutomationText);
                        Assert.AreEqual("Constant", falseItems[0].IconAutomationText);
                    }
                    Assert.AreEqual("Keyword", noneItems[0].IconAutomationText);
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Mock")]
        public void CtrlSpaceAfterKeyword() {
            // http://pytools.codeplex.com/workitem/560
            string code = @"
def h():
    return 

print 

";

            using (var vs = new MockVs()) {
                AssertUtil.ContainsAtLeast(GetCompletions(vs, code.IndexOfEnd("return "), code), "any");
                AssertUtil.ContainsAtLeast(GetCompletions(vs, code.IndexOfEnd("print "), code), "any");
            }
        }

        [TestMethod, Priority(0), TestCategory("Mock")]
        public void CtrlSpaceAfterNumber() {
            // http://pytools.codeplex.com/workitem/2323
            string code = @"
2
2.
2..
2.0.
";

            using (var vs = new MockVs()) {
                AssertUtil.ContainsExactly(GetCompletions(vs, code.IndexOfEnd("2"), code));
                AssertUtil.ContainsExactly(GetCompletions(vs, code.IndexOfEnd("2."), code));
                AssertUtil.ContainsAtLeast(GetCompletions(vs, code.IndexOfEnd("2.."), code), "real", "imag");
                AssertUtil.ContainsAtLeast(GetCompletions(vs, code.IndexOfEnd("2.0."), code), "real", "imag");
            }
        }

        [TestMethod, Priority(0), TestCategory("Mock")]
        public void ExceptionCompletions() {
            using (var vs = new MockVs()) {
                foreach (string code in new[] { 
                @"import sys
raise |", 
                @"import sys
raise (|", 
                @"import sys
try:
    pass
except |",
                @"import sys
try:
    pass
except (|",
            @"import sys
try:
    pass
except (ValueError, |"}) {
                    var completionList = GetCompletions(vs, code.IndexOf("|"), code.Replace("|", "")).ToArray();

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
raise (sys.", 
                @"import sys
try:
    pass
except (sys."}) {
                    var completionList = GetCompletions(vs, code.IndexOfEnd("sys."), code).ToArray();

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
        }

        [TestMethod, Priority(0), TestCategory("Mock")]
        public void MemberCompletions() {
            using (var view = new PythonEditor("x = 2\r\nx.")) {
                // TODO: Negative tests
                TestMemberCompletion(view, -1, "x");

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
                        view.Text = test;
                        TestMemberCompletion(view, -1, expr.TrimEnd('.'));
                    }
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Mock")]
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

            using (var view = new PythonEditor()) {
                foreach (var prefix in prefixes) {
                    foreach (var sig in sigs) {
                        var test = prefix + sig.Expr;
                        Console.WriteLine("   -- {0}", test);
                        view.Text = test;
                        var snapshot = view.CurrentSnapshot;

                        var res = snapshot.GetSignatures(
                            view.VS.ServiceProvider,
                            snapshot.CreateTrackingSpan(snapshot.Length, 0, SpanTrackingMode.EdgeInclusive)
                        );
                        Assert.AreEqual(sig.Function, res.Text, test);
                        Assert.AreEqual(sig.Param, res.ParameterIndex, test);
                    }
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Mock")]
        public void SignatureHelpStarArgs() {
            SignatureAnalysis sigResult;
            using (var vs = new MockVs()) {
                TestSignature(vs, -1, @"def f(a, *b, c=None): pass
f(1, 2, 3, 4,", "f", 4, PythonLanguageVersion.V27, true, out sigResult);
                Assert.IsTrue(sigResult.Signatures.Count >= 1, "No signature analysis results");
                Assert.AreEqual("*b", sigResult.Signatures[0].CurrentParameter.Name);
            }
        }

        [TestMethod, Priority(0), TestCategory("Mock")]
        public void ImportCompletions() {
            using (var view = new PythonEditor()) {
                view.Text ="import ";
                AssertUtil.ContainsAtLeast(view.GetCompletions(-1), "sys");

                view.Text ="import sys";
                AssertUtil.ContainsAtLeast(view.GetCompletions(-1), "sys");

                view.Text ="import sys ";
                AssertUtil.ContainsExactly(view.GetCompletions(-1), "as");

                view.Text ="import sys as";
                AssertUtil.ContainsExactly(view.GetCompletions(-1), "as");

                view.Text ="import sys as s, ";
                AssertUtil.ContainsAtLeast(view.GetCompletions(-1), "sys");

                view.Text ="import sys, ";
                AssertUtil.ContainsAtLeast(view.GetCompletions(-1), "datetime");

                view.Text ="import sys, da";
                AssertUtil.ContainsAtLeast(view.GetCompletions(-1), "datetime");
            }
        }

        [TestMethod, Priority(0), TestCategory("Mock")]
        public void FromImportCompletions() {
            using (var view = new PythonEditor()) {
                view.Text = "from ";
                AssertUtil.ContainsAtLeast(view.GetCompletions(-1), "nt", "sys");

                view.Text = "from s";
                var completions = view.GetCompletions(-1);
                AssertUtil.ContainsAtLeast(completions, "sys");
                AssertUtil.DoesntContain(completions, "nt");

                view.Text = "from sys ";
                AssertUtil.ContainsExactly(view.GetCompletions(-1), "import");

                view.Text = "from sys import";
                AssertUtil.ContainsExactly(view.GetCompletions(-1), "import");

                view.Text = "from sys import ";
                AssertUtil.ContainsAtLeast(view.GetCompletions(-1),
                    "*",                    // Contains *
                    "settrace",             // Contains functions
                    "api_version"           // Contains data members
                );

                view.Text = "from sys.";
                // There will be one completion saying that there are no completions
                Assert.AreEqual(1, view.GetCompletions(-1).Count());

                // Error case - no completions
                view.Text = "from sys. import ";
                AssertUtil.ContainsExactly(view.GetCompletions(-1));

                view.Text = "from sys import settrace ";
                AssertUtil.ContainsExactly(view.GetCompletions(-1), "as");

                view.Text = "from sys import settrace as";
                AssertUtil.ContainsExactly(view.GetCompletions(-1), "as");

                view.Text = "from sys import settrace,";
                completions = view.GetCompletions(-1);
                AssertUtil.ContainsAtLeast(completions, "api_version", "settrace");
                AssertUtil.DoesntContain(completions, "*");

                // No more completions after a *
                view.Text = "from sys import *, ";
                AssertUtil.ContainsExactly(view.GetCompletions(-1));

                view.Text = "from sys import settrace as ";
                AssertUtil.ContainsExactly(view.GetCompletions(-1));

                view.Text = "from sys import settrace as st ";
                AssertUtil.ContainsExactly(view.GetCompletions(-1));

                view.Text = "from sys import settrace as st, ";
                completions = view.GetCompletions(-1);
                AssertUtil.ContainsAtLeast(completions, "api_version", "settrace");
                AssertUtil.DoesntContain(completions, "*");
            }
        }


        [TestMethod, Priority(0), TestCategory("Mock")]
        public void FromOSPathImportCompletions2x() {
            using (var vs = new MockVs())
            using (var db = MockCompletionDB.Create(PythonLanguageVersion.V27, "os", "ntpath", "posixpath", "os2emxpath")) {
                OSPathImportTest(vs, db);
            }
        }

        [TestMethod, Priority(0), TestCategory("Mock")]
        public void FromOSPathImportCompletions3x() {
            using (var vs = new MockVs())
            using (var db = MockCompletionDB.Create(PythonLanguageVersion.V33, "os", "ntpath", "posixpath", "os2emxpath")) {
                OSPathImportTest(vs, db);
            }
        }

        private static void OSPathImportTest(MockVs vs, MockCompletionDB db) {
            var code = "from ";
            AssertUtil.ContainsAtLeast(GetCompletions(vs, -1, code, db.Factory), "os", "sys");

            code = "from o";
            var completions = GetCompletions(vs, -1, code, db.Factory);
            AssertUtil.ContainsAtLeast(completions, "os");
            AssertUtil.DoesntContain(completions, "sys");

            code = "from os ";
            AssertUtil.ContainsExactly(GetCompletions(vs, -1, code, db.Factory), "import");

            code = "from os import";
            AssertUtil.ContainsExactly(GetCompletions(vs, -1, code, db.Factory), "import");

            code = "from os import ";
            AssertUtil.ContainsAtLeast(GetCompletions(vs, -1, code, db.Factory), "path");

            code = "from os.";
            AssertUtil.ContainsExactly(GetCompletions(vs, -1, code, db.Factory), "path");

            code = "from os.path import ";
            AssertUtil.ContainsAtLeast(GetCompletions(vs, -1, code, db.Factory), "abspath", "relpath");

            var allNames = new HashSet<string>();
            allNames.UnionWith(GetCompletions(vs, -1, "from ntpath import ", db.Factory));
            allNames.UnionWith(GetCompletions(vs, -1, "from posixpath import ", db.Factory));

            code = "from os.path import ";
            AssertUtil.ContainsAtLeast(GetCompletions(vs, -1, code, db.Factory), allNames);
        }

        [TestMethod, Priority(0), TestCategory("Mock")]
        public void FromImportMultilineCompletions() {
            using (var vs = new MockVs()) {
                var code = "from sys import (";
                var completions = GetCompletions(vs, -1, code);
                AssertUtil.ContainsAtLeast(completions, "settrace", "api_version");
                AssertUtil.DoesntContain(completions, "*");

                code = "from nt import (\r\n    ";
                completions = GetCompletions(vs, -1, code);
                AssertUtil.ContainsAtLeast(completions, "abort", "W_OK");
                AssertUtil.DoesntContain(completions, "*");

                code = "from nt import (getfilesystemencoding,\r\n    ";
                completions = GetCompletions(vs, -1, code);
                AssertUtil.ContainsAtLeast(completions, "abort", "W_OK");
                AssertUtil.DoesntContain(completions, "*");

                // Need a comma for more completions
                code = "from sys import (settrace\r\n    ";
                AssertUtil.ContainsExactly(GetCompletions(vs, -1, code), "as");
            }
        }

        private static IEnumerable<string> GetCompletionNames(CompletionSet completions) {
            foreach (var comp in completions.Completions) {
                yield return comp.InsertionText;
            }
        }

        private static IEnumerable<string> GetCompletionNames(CompletionAnalysis analysis) {
            return GetCompletionNames(analysis.GetCompletions(new MockGlyphService()));
        }

        [TestMethod, Priority(0), TestCategory("Mock")]
        public void Scenario_CompletionInTripleQuotedString() {
            string code = @"
'''
import 
from 
except 
@
sys.
'''
";

            using (var view = new PythonEditor(code)) {
                for (int i = code.IndexOfEnd("'''"); i < code.LastIndexOf("'''"); ++i) {
                    AssertUtil.ContainsExactly(view.GetCompletions(i));
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Mock")]
        public void GotoDefinition() {
            using (var vs = new MockVs()) {
                string code = @"
class C:
    def fff(self): pass

C().fff";

                //var emptyAnalysis = AnalyzeExpression(0, code);
                //AreEqual(emptyAnalysis.Expression, "");

                for (int i = -1; i >= -3; i--) {
                    var analysis = AnalyzeExpression(vs, i, code);
                    Assert.AreEqual("C().fff", analysis.Expression);
                }

                var classAnalysis = AnalyzeExpression(vs, -6, code);
                Assert.AreEqual("C()", classAnalysis.Expression);

                var defAnalysis = AnalyzeExpression(vs, code.IndexOf("def fff") + 4, code);
                Assert.AreEqual("fff", defAnalysis.Expression);
            }
        }

        [TestMethod, Priority(0), TestCategory("Mock")]
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


            using (var vs = new MockVs()) {
                // we get the appropriate subexpression
                TestQuickInfo(vs, code, code.IndexOf("cls."), code.IndexOf("cls.") + 4, "cls: <unknown type>");
                TestQuickInfo(vs, code, code.IndexOf("cls.") + 4 + 1, code.IndexOf("cls.") + 4 + 1 + 11, "cls._parse_block: <unknown type>");
                TestQuickInfo(vs, code, code.IndexOf("cls.") + 4 + 1 + 11 + 1, code.IndexOf("cls.") + 4 + 1 + 11 + 1 + 4, "ast: <unknown type>");
                TestQuickInfo(vs, code, code.IndexOf("cls.") + 4 + 1 + 11 + 1 + 4 + 1, code.IndexOf("cls.") + 4 + 1 + 11 + 1 + 4 + 1 + 3, "ast.expr: <unknown type>");
                TestQuickInfo(vs, code, code.IndexOf("cls.") + 4 + 1 + 11 + 1 + 4 + 1 + 3 + 1, code.IndexOf("cls.") + 4 + 1 + 11 + 1 + 5 + 3 + 1 + 1, "cls._parse_block(ast.expr): <unknown type>");

                // the whole string shows up in quick info
                TestQuickInfo(vs, code, code.IndexOf("x = ") + 4, code.IndexOf("x = ") + 4 + 28, "\"ABCDEFGHIJKLMNOPQRSTUVWYXZ\": str");

                // trailing new lines don't show up in quick info
                TestQuickInfo(vs, code, code.IndexOf("def f") + 4, code.IndexOf("def f") + 5, "f: def f()\r\nhelpful information");

                // keywords don't show up in quick info
                TestQuickInfo(vs, code, code.IndexOf("while True:"), code.IndexOf("while True:") + 5);

                // 'lambda' keyword doesn't show up in quick info
                TestQuickInfo(vs, code, code.IndexOf("lambda"), code.IndexOf("lambda") + 6);
                // but its arguments do
                TestQuickInfo(vs, code, code.IndexOf("larg1"), code.IndexOf("larg1") + 5, "larg1: <unknown type>");
                TestQuickInfo(vs, code, code.IndexOf("larg2"), code.IndexOf("larg2") + 5, "larg2: <unknown type>");

                // multiline function, hover at the close paren
                TestQuickInfo(vs, code, code.IndexOf("e)") + 1, code.IndexOf("e)") + 2, @"f(a,
(b, c, d),
e): <unknown type>");
            }
        }

        [TestMethod, Priority(0), TestCategory("Mock")]
        public void NormalOverrideCompletions() {
            using (var view2 = new PythonEditor(version: PythonLanguageVersion.V27))
            using (var view3 = new PythonEditor(version: PythonLanguageVersion.V33)) {
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
                    view2.Text = code;

                    Console.WriteLine(code);
                    AssertUtil.ContainsAtLeast(
                        view2.GetCompletionList(code.IndexOf("None")).Select(c => c.InsertionText),
                        @"func_a(self, a = 100):
        return super(Baz, self).func_a(a)",
                        @"func_b(self, b, *p, **kw):
        return super(Baz, self).func_b(b, *p, **kw)"
                    );

                    view3.Text = code;

                    AssertUtil.ContainsAtLeast(
                        view3.GetCompletionList(code.IndexOf("None")).Select(c => c.InsertionText),
                        @"func_a(self, a = 100):
        return super().func_a(a)",
                        @"func_b(self, b, *p, **kw):
        return super().func_b(b, *p, **kw)"
                    );
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Mock")]
        public void BuiltinOverrideCompletions() {
            using (var view2 = new PythonEditor(version: PythonLanguageVersion.V27))
            using (var view3 = new PythonEditor(version: PythonLanguageVersion.V33)) {
                view2.Text = view3.Text = @"class Fob(str):
    def 
";
                AssertUtil.ContainsAtLeast(
                    view2.GetCompletionListAfter("def ").Select(c => c.InsertionText),
                @"capitalize(self):
        return super(Fob, self).capitalize()",
                @"index(self, sub, start, end):
        return super(Fob, self).index(sub, start, end)"
                );
                AssertUtil.ContainsAtLeast(
                    view3.GetCompletionListAfter("def ").Select(x => x.InsertionText),
                @"capitalize(self):
        return super().capitalize()",
                @"index(self, sub, start, end):
        return super().index(sub, start, end)"
                );

                view2.Text = view3.Text = @"class Fob(str, list):
    def 
";

                AssertUtil.Contains(
                    view2.GetCompletionListAfter("def ").Select(c => c.InsertionText),
                    @"index(self, sub, start, end):
        return super(Fob, self).index(sub, start, end)"
                );
                AssertUtil.Contains(
                    view3.GetCompletionListAfter("def ").Select(c => c.InsertionText),
                    @"index(self, sub, start, end):
        return super().index(sub, start, end)"
                );

                view2.Text = view3.Text = @"class Fob(list, str):
    def 
";
                AssertUtil.Contains(
                    view2.GetCompletionListAfter("def ").Select(c => c.InsertionText),
                    @"index(self, item, start, stop):
        return super(Fob, self).index(item, start, stop)"
                );
                AssertUtil.Contains(
                    view3.GetCompletionListAfter("def ").Select(c => c.InsertionText),
                    @"index(self, item, start, stop):
        return super().index(item, start, stop)"
                );
            }
        }

        [TestMethod, Priority(0), TestCategory("Mock")]
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

            using (var vs = new MockVs()) {
                var editText = "\r\n\r\n\r\n    def ";
                var editInsert = code.IndexOf("pass") + 4;
                var completions = EditAndGetCompletions(vs, code, editText, editInsert, "def ");

                AssertUtil.ContainsAtLeast(completions, "bit_length", "conjugate");
                AssertUtil.DoesntContain(completions, "keys");

                editText = "\r\n\r\n\r\n\r\n\r\n    def ";
                editInsert = code.IndexOf("pass", editInsert) + 4;
                completions = EditAndGetCompletions(vs, code, editText, editInsert, "def ");

                AssertUtil.ContainsAtLeast(completions, "keys", "__contains__");
                AssertUtil.DoesntContain(completions, "bit_length");
            }
        }

        [TestMethod, Priority(0), TestCategory("Mock")]
        public void HideAdvancedMembers() {
            using (var view = new PythonEditor()) {
                // No text - expect all non-advanced members
                AssertUtil.ContainsExactly(HideAdvancedMembersHelper(view, "", "a", "b", "__c__", "__d_e__", "_f_"),
                    "a", "b", "_f_"
                );

                // Matches one item, so we should only see that
                AssertUtil.ContainsExactly(HideAdvancedMembersHelper(view, "a", "a", "b", "__c__", "__d_e__", "_f_"),
                    "a"
                );

                // Matches one hidden item - expect all non-advanced members
                AssertUtil.ContainsExactly(HideAdvancedMembersHelper(view, "c", "a", "b", "__c__", "__d_e__", "_f_"),
                    "a", "b", "_f_"
                );

                // Matches one item and advanced members
                AssertUtil.ContainsExactly(HideAdvancedMembersHelper(view, "__", "a", "b", "__c__", "__d_e__", "_f_"),
                    "_f_",
                    "__c__",
                    "__d_e__"
                );
            }
        }

        private IEnumerable<string> HideAdvancedMembersHelper(PythonEditor view, string text, params string[] completions) {
            view.Text = text;
            var snapshot = view.CurrentSnapshot;
            var set = new FuzzyCompletionSet(
                "Test Completions",
                "Test Completions",
                snapshot.CreateTrackingSpan(0, snapshot.Length, SpanTrackingMode.EdgeInclusive),
                completions.Select(c => new DynamicallyVisibleCompletion(c)),
                new CompletionOptions { HideAdvancedMembers = true, IntersectMembers = false },
                CompletionComparer.UnderscoresLast
            );
            set.Filter();
            return set.Completions.Select(c => c.DisplayText).ToList();
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

        [TestMethod, Priority(0), TestCategory("Mock")]
        public void CompletionWithLongDocString() {
            using (var vs = new MockVs()) {
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

                TestQuickInfo(vs, code, code.IndexOf("func"), code.IndexOf("func") + 4, "func: def func(a)\r\n" + expected1);

                SignatureAnalysis sigs;
                TestSignature(vs, -1, code + "func(", "func", 0, PythonLanguageVersion.V27, true, out sigs);
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

                TestQuickInfo(vs, code, code.IndexOf("func"), code.IndexOf("func") + 4, "func: def func(a)\r\n" + expected1);

                TestSignature(vs, -1, code + "func(", "func", 0, PythonLanguageVersion.V27, true, out sigs);
                Assert.AreEqual(1, sigs.Signatures.Count);
                Assert.AreEqual(1, sigs.Signatures[0].Parameters.Count);
                Assert.AreEqual(expected2, sigs.Signatures[0].Documentation);
            }
        }

        [TestMethod, Priority(0), TestCategory("Mock")]
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
            using (var view = new PythonEditor()) {
                view.Text = code.Replace("#1", "eggs_and_spam.");
                var completionList = view.GetCompletionsAfter("eggs_and_spam.");

                AssertUtil.ContainsAtLeast(completionList, "real", "imag");
                AssertUtil.DoesntContain(completionList, "lower");

                view.Text = code.Replace("#2", "eggs_and_spam.");
                AssertUtil.ContainsAtLeast(view.GetCompletionsAfter("eggs_and_spam."), "bit_length");

                view.Text = code.Replace("#3", "eggs_and_spam.");
                AssertUtil.ContainsAtLeast(view.GetCompletionsAfter("eggs_and_spam."), "lower", "center");
            }
        }

        [TestMethod, Priority(0), TestCategory("Mock")]
        public void ArgumentNameCompletion() {
            const string code = @"
def f(param1 = 123, param2 : int = 234):
    pass

x = f(";

            using (var view = new PythonEditor(code)) {
                AssertUtil.ContainsAtLeast(view.GetCompletions(-1), "param1", "param2");
                AssertUtil.DoesntContain(view.GetCompletions(0), "param1");
            }
        }

        [TestMethod, Priority(0), TestCategory("Mock")]
        public void MethodArgumentNameCompletion() {
            const string code = @"
class MyClass:
    def f(self, param1 = 123, param2 : int = 234):
        pass

m = MyClass()
x = m.f(";

            using (var view = new PythonEditor(code)) {
                AssertUtil.ContainsAtLeast(view.GetCompletions(-1), "param1", "param2");
                AssertUtil.DoesntContain(view.GetCompletions(0), "param1");
            }
        }

        private static IEnumerable<string> EditAndGetCompletions(
            MockVs vs,
            string code,
            string editText,
            int editInsert,
            string completeAfter,
            PythonLanguageVersion version = PythonLanguageVersion.V27
        ) {
            using (var view = new PythonEditor(code, version, vs)) {
                view.AdvancedOptions.HideAdvancedMembers = false;

                var snapshot = view.CurrentSnapshot;
                view.View.MoveCaret(new SnapshotPoint(snapshot, editInsert));
                view.Type(editText);

                var newSnapshot = view.CurrentSnapshot;
                Assert.AreNotSame(snapshot, newSnapshot);

                return view.GetCompletionsAfter(completeAfter);
            }
        }

        private static void TestQuickInfo(MockVs vs, string code, int start, int end, params string[] expected) {
            using (var view = new PythonEditor(code, vs: vs)) {
                var snapshot = view.CurrentSnapshot;

                for (int i = start; i < end; i++) {
                    var analysis = snapshot.AnalyzeExpression(
                        vs.ServiceProvider,
                        snapshot.CreateTrackingSpan(i, i == snapshot.Length ? 0 : 1, SpanTrackingMode.EdgeInclusive),
                        false
                    );

                    List<object> quickInfo = new List<object>();
                    ITrackingSpan span;
                    QuickInfoSource.AugmentQuickInfoWorker(
                        analysis,
                        quickInfo,
                        out span
                    );

                    Assert.AreEqual(expected.Length, quickInfo.Count);
                    for (int j = 0; j < expected.Length; j++) {
                        Assert.AreEqual(expected[j], quickInfo[j]);
                    }
                }
            }
        }

        private static ExpressionAnalysis AnalyzeExpression(MockVs vs, int location, string code, PythonLanguageVersion version = PythonLanguageVersion.V27) {
            if (location < 0) {
                location += code.Length + 1;
            }

            using (var view = new PythonEditor(code, version, vs)) {
                var snapshot = view.CurrentSnapshot;
                return snapshot.AnalyzeExpression(
                    vs.ServiceProvider,
                    snapshot.CreateTrackingSpan(location, location < snapshot.Length ? 1 : 0, SpanTrackingMode.EdgeInclusive),
                    false
                );
            }
        }

        private static void TestMemberCompletion(PythonEditor view, int index, string expectedExpression) {
            var snapshot = view.CurrentSnapshot;
            if (index < 0) {
                index += snapshot.Length + 1;
            }

            var context = snapshot.GetCompletions(
                view.VS.ServiceProvider,
                snapshot.GetApplicableSpan(index) ?? snapshot.CreateTrackingSpan(index, 0, SpanTrackingMode.EdgeInclusive),
                snapshot.CreateTrackingPoint(index, PointTrackingMode.Negative),
                new CompletionOptions()
            );

            Assert.IsInstanceOfType(context, typeof(NormalCompletionAnalysis));
            var normalContext = (NormalCompletionAnalysis)context;

            string text;
            SnapshotSpan statementExtent;
            Assert.IsTrue(normalContext.GetPrecedingExpression(out text, out statementExtent));
            Assert.AreEqual(expectedExpression, text);
        }

        private static SignatureAnalysis GetSignatureAnalysis(MockVs vs, int index, string code, PythonLanguageVersion version = PythonLanguageVersion.V27) {
            using (var view = new PythonEditor(code, version, vs)) {
                var snapshot = view.CurrentSnapshot;

                return snapshot.GetSignatures(
                    vs.ServiceProvider,
                    snapshot.CreateTrackingSpan(index, 1, SpanTrackingMode.EdgeInclusive)
                );
            }
        }


        private static void TestSignature(MockVs vs, int location, string sourceCode, string expectedExpression, int paramIndex, PythonLanguageVersion version, bool analyze, out SignatureAnalysis sigs) {
            if (location < 0) {
                location = sourceCode.Length + location;
            }
            
            sigs = GetSignatureAnalysis(vs, location, sourceCode, version);
            Assert.AreEqual(expectedExpression, sigs.Text, sourceCode);
            Assert.AreEqual(paramIndex, sigs.ParameterIndex, sourceCode);
        }

        private static List<Completion> GetCompletionList(MockVs vs, int index, string code, PythonLanguageVersion version = PythonLanguageVersion.V27) {
            using (var view = new PythonEditor(code, version, vs)) {
                return view.GetCompletionList(index);
            }
        }

        private static IEnumerable<string> GetCompletions(MockVs vs, int index, string code, IPythonInterpreterFactory factory) {
            using (var view = new PythonEditor(code, vs: vs, factory: factory)) {
                return view.GetCompletions(index);
            }
        }

        private static IEnumerable<string> GetCompletions(MockVs vs, int index, string code, PythonLanguageVersion version = PythonLanguageVersion.V27) {
            using (var view = new PythonEditor(code, version, vs)) {
                return view.GetCompletions(index);
            }
        }
    }
}

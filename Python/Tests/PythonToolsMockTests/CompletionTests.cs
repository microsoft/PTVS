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

extern alias analysis;
extern alias pythontools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using analysis::Microsoft.PythonTools.Interpreter;
using analysis::Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudioTools.MockVsTests;
using pythontools::Microsoft.PythonTools;
using pythontools::Microsoft.PythonTools.Intellisense;
using TestUtilities;
using TestUtilities.Mocks;

namespace PythonToolsMockTests {
    [TestClass]
    public class CompletionTests {

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            VsProjectAnalyzer.DefaultTimeout = 10000;
            VsProjectAnalyzer.AssertOnRequestFailure = true;
        }

        [TestInitialize]
        public void OnTestInitialized() {
            MockPythonToolsPackage.SuppressTaskProvider = true;
        }

        [TestCleanup]
        public void OnTestCleanup() {
            MockPythonToolsPackage.SuppressTaskProvider = false;
        }

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
        public void GetApplicableSpanCompleteWordTest() {
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
                    var span = snapshot.GetApplicableSpan(i, completeWord: true);
                    if (span == null) {
                        Assert.AreEqual(expected[i], "", text.Substring(0, i) + "|" + text.Substring(i));
                    } else {
                        Assert.AreEqual(expected[i], span.GetText(snapshot), text.Substring(0, i) + "|" + text.Substring(i));
                    }
                }
            }
        }

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
        public void GetApplicableSpanAutoTest() {
            using (var view = new PythonEditor("x = id")) {
                var snapshot = view.CurrentSnapshot;
                var span = snapshot.GetApplicableSpan(5, completeWord: false);
                Assert.AreEqual(4, span.GetStartPoint(snapshot).Position);
                Assert.AreEqual(5, span.GetEndPoint(snapshot).Position);
            }
        }

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
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

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
        public void KeywordCompletions() {
            var code = "def f():\r\n     \r\n    x = abc, oar, \r\n    pass\r\n#2\r\n";
            using (var view = new PythonEditor(code, version: PythonLanguageVersion.V35)) {
                IEnumerable<string> completionList;
                completionList = view.GetCompletions(code.IndexOfEnd("#2\r\n"));

                // not in a function
                AssertUtil.CheckCollection(completionList,
                    new[] { "assert", "and", "async" },
                    new[] { "await", "return", "yield" }
                );

                completionList = view.GetCompletions(code.IndexOf("    \r\n") - 2);
                AssertUtil.CheckCollection(completionList,
                    new[] { "assert", "and", "async", "yield", "return" },
                    Array.Empty<string>()
                );

                completionList = view.GetCompletions(code.IndexOfEnd("oar,"));
                AssertUtil.CheckCollection(completionList,
                    new[] { "and" },
                    new[] { "def" }
                );
            }
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
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

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
        public void LambdaCompletions() {
            // https://github.com/Microsoft/PTVS/issues/1000
            string code = @"
l = (lambda b:b)
l(42)

";
            using (var view = new PythonEditor(code)) {
                var completionList = view.GetCompletionsAfter(":");

                AssertUtil.Contains(completionList, "b");
            }
        }

        [TestMethod, Priority(TestExtensions.P2_FAILING_UNIT_TEST)]
        public void TrueFalseNoneCompletions() {
            // http://pytools.codeplex.com/workitem/1905
            foreach (var version in new[] { PythonLanguageVersion.V27, PythonLanguageVersion.V33 }) {
                using (var view = new PythonEditor(version: version)) {
                    var completionList = view.GetCompletionList(0);
                    foreach (var c in completionList) {
                        Console.WriteLine(c.DisplayText);
                    }

                    var trueItems = completionList.Where(t => t.DisplayText == "True").ToArray();
                    var falseItems = completionList.Where(t => t.DisplayText == "False").ToArray();
                    var noneItems = completionList.Where(t => t.DisplayText == "None").ToArray();
                    Assert.AreEqual(1, trueItems.Count());
                    Assert.AreEqual(1, falseItems.Count());
                    Assert.AreEqual(1, noneItems.Count());
                    Assert.AreEqual("Keyword", trueItems[0].IconAutomationText);
                    Assert.AreEqual("Keyword", falseItems[0].IconAutomationText);
                    Assert.AreEqual("Keyword", noneItems[0].IconAutomationText);
                }
            }
        }

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
        public void CtrlSpaceAfterKeyword() {
            // http://pytools.codeplex.com/workitem/560
            string code = @"
def h():
    return 

print 

";

            using (var editor = new PythonEditor(code)) {
                AssertUtil.ContainsAtLeast(editor.GetCompletions(code.IndexOfEnd("return ")), "any");
                AssertUtil.ContainsAtLeast(editor.GetCompletions(code.IndexOfEnd("print ")), "any");
            }
        }

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
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

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void ExceptionCompletions() {
            using (var vs = new MockVs()) {
                foreach (var ver in new[] { PythonLanguageVersion.V36, PythonLanguageVersion.V27 }) {
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
                        Console.WriteLine($"{ver}:: {code}");

                        var completionList = GetCompletions(vs, code.IndexOf("|"), code.Replace("|", ""), ver);

                        AssertUtil.CheckCollection(completionList,
                            new[] { "Exception", "KeyboardInterrupt", "GeneratorExit", "StopIteration", "SystemExit", "sys", "Warning" },
                            new[] { "str", "int" }
                        );
                    }

                    foreach (string code in new[] {
@"import sys
raise (sys.",
@"import sys
try:
    pass
except (sys."}) {
                        var completionList = GetCompletions(vs, code.IndexOfEnd("sys."), code, ver);

                        AssertUtil.CheckCollection(
                            completionList,
                            new[] { "modules", "path", "version" },
                            new[] { "Exception", "KeyboardInterrupt", "GeneratorExit", "StopIteration", "SystemExit" }
                        );
                    }
                }
            }
        }

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
        public async Task MemberCompletions() {
            using (var view = new PythonEditor("x = 2\r\nx.")) {
                // See tests in ExpressionFinder

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
                        await Task.Delay(100);
                        var foundExpr = await view.Analyzer.GetExpressionAtPointAsync(
                            new SnapshotPoint(view.CurrentSnapshot, view.CurrentSnapshot.Length),
                            ExpressionAtPointPurpose.EvaluateMembers,
                            TimeSpan.FromSeconds(10.0)
                        );
                        if (foundExpr == null && Debugger.IsAttached) {
                            Debugger.Break();
                            foundExpr = await view.Analyzer.GetExpressionAtPointAsync(
                                new SnapshotPoint(view.CurrentSnapshot, view.CurrentSnapshot.Length),
                                ExpressionAtPointPurpose.EvaluateMembers,
                                TimeSpan.FromDays(1)
                            );
                        }
                        Assert.IsNotNull(foundExpr, $"Did not find any expression in {test}");
                        Assert.AreEqual(expr.TrimEnd('.'), foundExpr.Text);
                    }
                }
            }
        }

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
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

                        SignatureAnalysis res = null;
                        view.VS.InvokeSync(() => {
                            res = view.VS.GetPyService().GetSignatures(
                                view.View.TextView,
                                snapshot,
                                snapshot.CreateTrackingSpan(snapshot.Length, 0, SpanTrackingMode.EdgeInclusive)
                            );
                        });

                        Assert.IsNotNull(res);
                        Assert.AreEqual(sig.Function, res.Text, test);
                        Assert.AreEqual(sig.Param, res.ParameterIndex, test);
                    }
                }
            }
        }

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
        public void SignatureHelpStarArgs() {
            SignatureAnalysis sigResult = null;
            using (var view = new PythonEditor(@"def f(a, *b, c=None): pass
f(1, 2, 3, 4,")) {
                for (int retries = 3; retries >= 0; --retries) {
                    TestSignature(view, -1, "f", 4, out sigResult);
                    if (retries == 0) {
                        Assert.IsTrue(sigResult.Signatures.Count >= 1, "No signature analysis results");
                    } else if (sigResult.Signatures.Count >= 1) {
                        break;
                    }
                    Console.WriteLine("Retry {0}", retries);
                    view.Text = view.Text;
                }
                Assert.AreEqual("*b", sigResult.Signatures[0].CurrentParameter.Name);
            }
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void ImportCompletions() {
            using (var view = new PythonEditor()) {
                view.Text = "import  ";
                AssertUtil.ContainsAtLeast(view.GetCompletions(-2), "sys");

                view.Text = "import sys";
                AssertUtil.ContainsAtLeast(view.GetCompletions(-1), "sys");

                view.Text = "import sys ";
                AssertUtil.ContainsExactly(view.GetCompletions(-1), "as");

                view.Text = "import sys as";
                AssertUtil.ContainsExactly(view.GetCompletions(-1), "as");

                view.Text = "import sys as s, ";
                AssertUtil.ContainsAtLeast(view.GetCompletions(-1), "sys");

                view.Text = "import sys, ";
                AssertUtil.ContainsAtLeast(view.GetCompletions(-1), "datetime");

                view.Text = "import sys, da";
                AssertUtil.ContainsAtLeast(view.GetCompletions(-1), "datetime");

                view.Text = "import unittest.";
                AssertUtil.ContainsAtLeast(view.GetCompletions(-1), "case");

                view.Text = "import unittest.case.";
                AssertUtil.DoesntContain(view.GetCompletions(-1), "sys");
            }
        }

        [TestMethod, Priority(TestExtensions.P2_FAILING_UNIT_TEST)]
        public void FromImportCompletions() {
            using (var view = new PythonEditor()) {
                IEnumerable<string> completions = null;
                view.Text = "from ";
                AssertUtil.ContainsAtLeast(view.GetCompletions(-1), "nt", "sys");

                view.Text = "from s";
                completions = view.GetCompletions(-1);
                AssertUtil.ContainsAtLeast(completions, "sys");
                AssertUtil.DoesntContain(completions, "nt");

                view.Text = "from sys ";
                AssertUtil.ContainsExactly(view.GetCompletions(-1), "import");

                view.Text = "from sys import";
                AssertUtil.ContainsExactly(view.GetCompletions(-1), "import");


                // This is the first time we have imported sys, so it will
                // run Python in the background to scrape it. We can wait.
                for (int retries = 100; retries > 0; --retries) {
                    view.Text = "from sys import ";
                    Thread.Sleep(100);
                    completions = view.GetCompletions(-1);
                    if (completions.Count() > 1) {
                        break;
                    }
                }
                AssertUtil.ContainsAtLeast(completions,
                    "*",                    // Contains *
                    "settrace",             // Contains functions
                    "api_version"           // Contains data members
                );

                // Error case - no completions
                view.Text = "from sys.";
                AssertUtil.ContainsExactly(view.GetCompletions(-1));

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


        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void FromOSPathImportCompletions2x() {
            using (var vs = new MockVs()) {
                var factory = vs.Invoke(() => vs.GetPyService().InterpreterRegistryService.Interpreters.LastOrDefault(p => p.Configuration.Version.Major == 2));
                OSPathImportTest(vs, factory);
            }
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void FromOSPathImportCompletions3x() {
            using (var vs = new MockVs()) {
                var factory = vs.Invoke(() => vs.GetPyService().InterpreterRegistryService.Interpreters.LastOrDefault(p => p.Configuration.Version.Major == 3));
                OSPathImportTest(vs, factory);
            }
        }

        private static void OSPathImportTest(MockVs vs, Microsoft.PythonTools.Interpreter.IPythonInterpreterFactory factory) {
            using (var editor = new PythonEditor(vs: vs, factory: factory)) {
                editor.Text = "from ";
                AssertUtil.ContainsAtLeast(editor.GetCompletions(-1), "os", "sys");

                editor.Text = "from o";
                var completions = editor.GetCompletions(-1);
                AssertUtil.ContainsAtLeast(completions, "os");
                AssertUtil.DoesntContain(completions, "sys");

                editor.Text = "from os ";
                AssertUtil.ContainsExactly(editor.GetCompletions(-1), "import");

                editor.Text = "from os import";
                AssertUtil.ContainsExactly(editor.GetCompletions(-1), "import");

                editor.Text = "from os import ";
                AssertUtil.ContainsAtLeast(editor.GetCompletions(-1), "path");

                editor.Text = "from os.";
                AssertUtil.ContainsExactly(editor.GetCompletions(-1), "path");

                editor.Text = "from os.path import ";
                AssertUtil.ContainsAtLeast(editor.GetCompletions(-1), "abspath", "relpath");

                var allNames = new HashSet<string>();
                editor.Text = "from ntpath import ";
                allNames.UnionWith(editor.GetCompletions(-1));
                editor.Text = "from posixpath import ";
                allNames.UnionWith(editor.GetCompletions(-1));

                editor.Text = "from os.path import ";
                AssertUtil.ContainsAtLeast(editor.GetCompletions(-1), allNames);
            }
        }

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
        public void FromImportMultilineCompletions() {
            using (var editor = new PythonEditor()) {
                editor.Text = "from sys import (";
                var completions = editor.GetCompletions(-1);
                AssertUtil.ContainsAtLeast(completions, "settrace", "api_version");

                editor.Text = "from nt import (\r\n    ";
                completions = editor.GetCompletions(-1);
                AssertUtil.ContainsAtLeast(completions, "abort", "W_OK");

                editor.Text = "from nt import (getfilesystemencoding,\r\n    ";
                completions = editor.GetCompletions(-1);
                AssertUtil.ContainsAtLeast(completions, "abort", "W_OK");

                // Need a comma for more completions
                editor.Text = "from sys import (settrace\r\n    ";
                AssertUtil.ContainsExactly(editor.GetCompletions(-1), "as");
            }
        }
        
        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
        [TestCategory("10s")]
        public void CompletionInTripleQuotedString() {
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
                    Console.WriteLine(code.Substring(0, i).Replace("\r", "\\r").Replace("\n", "\\n"));
                    AssertUtil.ContainsExactly(view.GetCompletions(i));
                }
            }
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void GotoDefinition() {
            using (var vs = new MockVs()) {
                string code = @"
class C:
    def fff(self): pass
i=1+2
C().fff";

                AnalyzeAndValidateExpression(vs, code.IndexOf("fff("), 3, code, "fff");
                AnalyzeAndValidateExpression(vs, code.IndexOf("self)"), 4, code, "self");
                AnalyzeAndValidateExpression(vs, code.IndexOf("C:"), 1, code, "C");
                AnalyzeAndValidateExpression(vs, code.IndexOf("1"), 1, code, "1");
                AnalyzeAndValidateExpression(vs, code.IndexOf("2"), 1, code, "2");
                AnalyzeAndValidateExpression(vs, code.IndexOf("C()."), 1, code, "C");
                AnalyzeAndValidateExpression(vs, code.IndexOf(".fff") + 2, 2, code, "C().fff");
            }
        }

        [TestMethod, Priority(TestExtensions.P2_FAILING_UNIT_TEST)]
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


            using (var view = new PythonEditor(code, filename:"file.py")) {
                // we get the appropriate subexpression
                TestQuickInfo(view, code.IndexOf("cls."), code.IndexOf("cls.") + 4, "cls: <unknown type>");
                TestQuickInfo(view, code.IndexOf("cls.") + 4 + 1, code.IndexOf("cls.") + 4 + 1 + 11, "cls._parse_block: <unknown type>");
                TestQuickInfo(view, code.IndexOf("cls.") + 4 + 1 + 11 + 1, code.IndexOf("cls.") + 4 + 1 + 11 + 1 + 4, "ast: <unknown type>");
                TestQuickInfo(view, code.IndexOf("cls.") + 4 + 1 + 11 + 1 + 4 + 1, code.IndexOf("cls.") + 4 + 1 + 11 + 1 + 4 + 1 + 3, "ast.expr: <unknown type>");
                TestQuickInfo(view, code.IndexOf("cls.") + 4 + 1 + 11 + 1 + 4 + 1 + 3 + 1, code.IndexOf("cls.") + 4 + 1 + 11 + 1 + 5 + 3 + 1 + 1, "cls._parse_block(ast.expr): <unknown type>");

                // the whole string shows up in quick info
                TestQuickInfo(view, code.IndexOf("x = ") + 4, code.IndexOf("x = ") + 4 + 28, "str");

                // trailing new lines don't show up in quick info
                TestQuickInfo(view, code.IndexOf("def f") + 4, code.IndexOf("def f") + 5, "f:\r\nfile.f()\r\nhelpful information");

                // keywords don't show up in quick info
                TestQuickInfo(view, code.IndexOf("while True:"), code.IndexOf("while True:") + 5);

                // 'lambda' keyword doesn't show up in quick info
                TestQuickInfo(view, code.IndexOf("lambda"), code.IndexOf("lambda") + 6);
                // but its arguments do
                TestQuickInfo(view, code.IndexOf("larg1"), code.IndexOf("larg1") + 5, "larg1: <unknown type>");
                TestQuickInfo(view, code.IndexOf("larg2"), code.IndexOf("larg2") + 5, "larg2: <unknown type>");

                // multiline function, hover *before* the close paren
                TestQuickInfo(view, code.IndexOf("e)") + 1, code.IndexOf("e)") + 2, "e: <unknown type>");
                // multiline function, hover *after* the close paren
                TestQuickInfo(view, code.IndexOf("e)") + 2, code.IndexOf("e)") + 3, @"f(a,
(b, c, d),
e): <unknown type>");
            }
        }

        [TestMethod, Priority(TestExtensions.P2_FAILING_UNIT_TEST)]
        public void NormalOverrideCompletions() {
            using (var view2 = new PythonEditor(version: PythonLanguageVersion.V27))
            using (var view3 = new PythonEditor(version: PythonLanguageVersion.V33)) {
                foreach (var code in new[] {
@"class Fob(object):
    def func_a(self, a=100): pass
    def func_b(self, b, *p, **kw): pass

class Baz(Fob):
    def 
",
@"class Fob(object):
    def func_a(self, a=100): pass

class Oar(Fob):
    def func_b(self, b, *p, **kw): pass

class Baz(Oar):
    def 
",
@"class Fob(object):
    def func_a(self, a=100): pass

class Oar(object):
    def func_b(self, b, *p, **kw): pass

class Baz(Fob, Oar):
    def 
",
@"class Fob(object):
    def func_a(self, a=100): pass
    def func_b(self, b, *p, **kw): pass
    def func_c(self): pass

class Baz(Fob):
    def func_c(self): pass
    def 
",
@"class Fob(object):
    def func_a(self, a=100): pass
    def func_c(self): pass

class Oar(Fob):
    def func_b(self, b, *p, **kw): pass

class Baz(Oar):
    def func_c(self): pass
    def 
",
@"class Fob(object):
    def func_a(self, a=100): pass

class Oar(object):
    def func_b(self, b, *p, **kw): pass
    def func_c(self): pass

class Baz(Fob, Oar):
    def func_c(self): pass
    def 
"}) {
                    view2.Text = code;

                    Console.WriteLine(code);
                    AssertUtil.ContainsAtLeast(
                        view2.GetCompletionList(code.LastIndexOf("def ") + 4).Select(c => c.InsertionText),
                        @"func_a(self, a = 100):
        return super(Baz, self).func_a(a)",
                        @"func_b(self, b, *p, **kw):
        return super(Baz, self).func_b(b, *p, **kw)"
                    );

                    view3.Text = code;

                    AssertUtil.ContainsAtLeast(
                        view3.GetCompletionList(code.LastIndexOf("def ") + 4).Select(c => c.InsertionText),
                        @"func_a(self, a = 100):
        return super().func_a(a)",
                        @"func_b(self, b, *p, **kw):
        return super().func_b(b, *p, **kw)"
                    );
                }
            }
        }

        static void AssertOverrideInsertionContains(PythonLanguageVersion version, string bases, string name, params string[] text) {
            using (var view = new PythonEditor($"class Fob({bases}):\r\n    def \r\n", version: version)) {
                var comp = view.GetCompletionListAfter("def ").FirstOrDefault(c => c.DisplayText == name);
                Assert.IsNotNull(comp, $"failed to find {name} in overrides list");
                AssertUtil.Contains(comp.InsertionText, text);
            }
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void BuiltinOverrideCompletions() {
            AssertOverrideInsertionContains(PythonLanguageVersion.V27, "str", "capitalize", "capitalize(self):\r\n        return super(Fob, self).capitalize()");
            AssertOverrideInsertionContains(PythonLanguageVersion.V33, "str", "capitalize", "capitalize(self):\r\n        return super().capitalize()");

            AssertOverrideInsertionContains(PythonLanguageVersion.V27, "str", "index", "index(self", "):\r\n        return super(Fob, self).index(");
            AssertOverrideInsertionContains(PythonLanguageVersion.V33, "str", "index", "index(self", "):\r\n        return super().index(");
        }

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
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

        [TestMethod, Priority(TestExtensions.P2_UNIT_TEST)]
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

        [TestMethod, Priority(TestExtensions.P2_FAILING_UNIT_TEST)]
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

                using (var view = new PythonEditor(code, filename: "file.py")) {
                    TestQuickInfo(view, code.IndexOf("func"), code.IndexOf("func") + 4, "func:\r\nfile.func(a)\r\n" + expected1);

                    SignatureAnalysis sigs;
                    view.Text += "func(";
                    TestSignature(view, -1, "func", 0, out sigs);
                    Assert.AreEqual(1, sigs.Signatures.Count);
                    Assert.AreEqual(1, sigs.Signatures[0].Parameters.Count);
                    Assert.AreEqual(expected2, sigs.Signatures[0].Documentation);
                }

                docString = GenerateText(100, 250, "    ").ToArray();
                code = @"
def func(a):
    '''" + string.Join(Environment.NewLine, docString) + @"'''
    pass

";

                using (var view = new PythonEditor(code, filename: "file.py")) {
                    // The long lines cause us to truncate sooner.
                    expected1 = string.Join(Environment.NewLine, docString).Substring(0, 4096) + Environment.NewLine + "...";
                    expected2 = string.Join(Environment.NewLine, docString.Take(8)).TrimStart() + Environment.NewLine + "...";

                    TestQuickInfo(view, code.IndexOf("func"), code.IndexOf("func") + 4, "func:\r\nfile.func(a)\r\n" + expected1);

                    SignatureAnalysis sigs;
                    view.Text += "func(";
                    TestSignature(view, -1, "func", 0, out sigs);
                    Assert.AreEqual(1, sigs.Signatures.Count);
                    Assert.AreEqual(1, sigs.Signatures[0].Parameters.Count);
                    Assert.AreEqual(expected2, sigs.Signatures[0].Documentation);
                }
            }
        }

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
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

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
        public void ArgumentNameCompletion() {
            const string code = @"
def f(param1 = 123, param2 : int = 234):
    pass

x = f(";

            using (var view = new PythonEditor(code)) {
                view.Text = view.Text;
                AssertUtil.ContainsAtLeast(view.GetCompletions(-1), "param1=", "param2=");
                AssertUtil.DoesntContain(view.GetCompletions(0), "param1");
            }
        }

        [TestMethod, Priority(TestExtensions.IMPORTANT_UNIT_TEST)]
        public void MethodArgumentNameCompletion() {
            const string code = @"
class MyClass:
    def f(self, param1 = 123, param2 : int = 234):
        pass

m = MyClass()
x = m.f(";

            using (var view = new PythonEditor(code)) {
                for (int retries = 3; retries >= 0; --retries) {
                    var sigs = GetSignatureAnalysis(view, code.Length - 1);
                    if (sigs.Signatures.Count > 0) {
                        break;
                    }
                    Console.WriteLine("Retry {0}", retries);
                    view.Text = view.Text;
                }
                AssertUtil.ContainsAtLeast(view.GetCompletions(-1), "param1=", "param2=");
                AssertUtil.DoesntContain(view.GetCompletions(0), "param1=");
            }
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void YieldFromExpressionCompletion() {
            const string code = @"
def f():
    yield 1
    return 1

def g():
    f().
    yield from f().
    (yield from f()).
";

            using (var view = new PythonEditor(code, PythonLanguageVersion.V35)) {
                AssertUtil.CheckCollection(view.GetCompletionsAfter("f()."),
                    new[] { "send", "throw" },
                    new[] { "real", "imag" }
                );
                AssertUtil.CheckCollection(view.GetCompletionsAfter("yield from f()."),
                    new[] { "send", "throw" },
                    new[] { "real", "imag" }
                );
                AssertUtil.CheckCollection(view.GetCompletionsAfter("(yield from f())."),
                    new[] { "real", "imag" },
                    new[] { "send", "throw" }
                );
            }
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void AwaitExpressionCompletion() {
            const string code = @"
async def f():
    return 1

async def g():
    f().
    await f().
    (await f()).";

            using (var view = new PythonEditor(code, PythonLanguageVersion.V35)) {
                AssertUtil.CheckCollection(view.GetCompletionsAfter("f()."),
                    new[] { "send", "throw" },
                    new[] { "real", "imag" }
                );
                AssertUtil.CheckCollection(view.GetCompletionsAfter("await f()."),
                    new[] { "send", "throw" },
                    new[] { "real", "imag" }
                );
                AssertUtil.CheckCollection(view.GetCompletionsAfter("(await f())."),
                    new[] { "real", "imag" },
                    new[] { "send", "throw" }
                );
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
                using (var evt = view.AnalysisCompleteEvent) {
                    view.View.MoveCaret(new SnapshotPoint(snapshot, editInsert));
                    view.Type(editText);

                    if (!evt.WaitOne(10000)) {
                        Assert.Fail("Failed to wait for new analysis");
                    }
                }

                var newSnapshot = view.CurrentSnapshot;
                Assert.AreNotSame(snapshot, newSnapshot);

                return view.GetCompletionsAfter(completeAfter);
            }
        }

        private static void TestQuickInfo(PythonEditor view, int start, int end, string expected = null) {
            var snapshot = view.CurrentSnapshot;

            for (var i = start; i < end; i++) {
                var quickInfo = view.Analyzer.GetQuickInfoAsync(
                    (AnalysisEntry) view.GetAnalysisEntry(),
                    view.View.TextView,
                    new SnapshotPoint(snapshot, start)
                ).Result;

                if (expected != null) {
                    Assert.IsNotNull(quickInfo);
                    Assert.AreEqual(expected, quickInfo.Text);
                } else {
                    Assert.IsNull(quickInfo);
                }
            }
        }

        private static void AnalyzeAndValidateExpression(MockVs vs, int start, int charCount, string code, string expectedExpr) {
            // We check charCount + 1 positions to ensure that go to definition
            // works when caret is on the left AND right of identifier (and in between)
            for (int i = 0; i <= charCount; i++) {
                var defAnalysis = AnalyzeExpression(vs, start + i, code);
                Assert.AreEqual(expectedExpr, defAnalysis?.Expression);
            }
        }

        private static ExpressionAnalysis AnalyzeExpression(MockVs vs, int location, string code, PythonLanguageVersion version = PythonLanguageVersion.V27) {
            if (location < 0) {
                location += code.Length + 1;
            }

            using (var view = new PythonEditor(code, version, vs)) {
                var snapshot = view.CurrentSnapshot;
                return vs.InvokeTask(() => view.Analyzer.AnalyzeExpressionAsync(
                    (AnalysisEntry)view.GetAnalysisEntry(),
                    new SnapshotPoint(snapshot, location)
                ));
            }
        }

        private static SignatureAnalysis GetSignatureAnalysis(PythonEditor view, int index) {
            var snapshot = view.CurrentSnapshot;

            Task<SignatureAnalysis> task = null;
            view.VS.InvokeSync(() => {
                task = view.Analyzer.GetSignaturesAsync(
                    (AnalysisEntry)view.GetAnalysisEntry(),
                    view.View.TextView,
                    snapshot,
                    snapshot.CreateTrackingSpan(index, 1, SpanTrackingMode.EdgeInclusive)
                );
            });
            return task.Wait(10000) ? task.Result : null;
        }


        private static void TestSignature(PythonEditor view, int location, string expectedExpression, int paramIndex, out SignatureAnalysis sigs) {
            if (location < 0) {
                location = view.CurrentSnapshot.Length + location;
            }
            
            sigs = GetSignatureAnalysis(view, location);
            Assert.AreEqual(expectedExpression, sigs.Text, view.Text);
            Assert.AreEqual(paramIndex, sigs.ParameterIndex, view.Text);
        }

        private static IEnumerable<string> GetCompletions(MockVs vs, int index, string code, PythonLanguageVersion version = PythonLanguageVersion.V27) {
            using (var view = new PythonEditor(code, version, vs)) {
                return view.GetCompletions(index);
            }
        }
    }
}

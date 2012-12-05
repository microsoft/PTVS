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
using System.Linq;
using System.Threading;
using IronPython.Hosting;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.Scripting.Hosting;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using TestUtilities;
using TestUtilities.Mocks;

namespace PythonToolsTests {
    [TestClass]
    public class CompletionContextTests {
        public static IContentType PythonContentType = new MockContentType("Python", new IContentType[0]);
        public static ScriptEngine PythonEngine = Python.CreateEngine();

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            TestData.Deploy();
        }

        [TestMethod, Priority(0)]
        public void Scenario_CtrlSpace() {
            string code = @"def f(param1, param2):
    g()";

            var completionList = GetCompletionSetCtrlSpace(code.IndexOf("g(") + 2, code).Completions.Select(x => x.DisplayText).ToArray();
            Assert.IsTrue(completionList.Contains("param1"));
            Assert.IsTrue(completionList.Contains("param2"));

            code = @"def f(param1, param2):
    g(param1, )";

            completionList = GetCompletionSetCtrlSpace(code.IndexOf("g(param1, ") + "g(param1, ".Length, code).Completions.Select(x => x.DisplayText).ToArray();
            Assert.IsTrue(completionList.Contains("param1"));
            Assert.IsTrue(completionList.Contains("param2"));

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
                Assert.IsTrue(completionList.Contains("min"));
                Assert.IsTrue(completionList.Contains("assert"));
            }
        }

        [TestMethod, Priority(0)]
        public void Scenario_Keywords() {
            string code = @"";

            var completionList = GetCompletionSetCtrlSpace(0, code).Completions.Select(x => x.DisplayText).ToArray();

            // not in a function
            Assert.IsFalse(completionList.Contains("yield"));
            Assert.IsFalse(completionList.Contains("return"));

            Assert.IsTrue(completionList.Contains("assert"));
            Assert.IsTrue(completionList.Contains("and"));

            code = @"def f():
    
    pass";

            completionList = GetCompletionSetCtrlSpace(code.IndexOf("pass"), code).Completions.Select(x => x.DisplayText).ToArray();

            Assert.IsTrue(completionList.Contains("yield"));
            Assert.IsTrue(completionList.Contains("return"));


            code = @"x = (abc, bar, baz)";

            completionList = GetCompletionSetCtrlSpace(code.IndexOf("bar"), code).Completions.Select(x => x.DisplayText).ToArray();

            Assert.IsTrue(completionList.Contains("and"));
            Assert.IsFalse(completionList.Contains("def"));
        }

        [TestMethod, Priority(0)]
        public void Scenario_KeywordOrIdentifier() {
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

            Assert.IsFalse(completionList.Contains("yield"));
            Assert.IsTrue(completionList.Contains("yield_expression"));

            completionList  = GetCompletionSetCtrlSpace(
                code.IndexOf("yield") + 5,
                code).Completions.Select(x => x.DisplayText).ToArray();

            Assert.IsTrue(completionList.Contains("yield"));
            Assert.IsTrue(completionList.Contains("yield_expression"));

            completionList = GetCompletionSetCtrlSpace(
                code.IndexOf("yiel") + 4,
                code).Completions.Select(x => x.DisplayText).ToArray();

            Assert.IsTrue(completionList.Contains("yield"));
            Assert.IsTrue(completionList.Contains("yield_expression"));
        }

        [TestMethod, Priority(0)]
        public void Scenario_CtrlSpaceAfterKeyword() {
            // http://pytools.codeplex.com/workitem/560
            string code = @"
def h():
    return 

print 

";

            var completionList = GetCompletionSetCtrlSpace(
                code.IndexOf("return") + 7,
                code).Completions.Select(x => x.DisplayText).ToArray();

            Assert.IsTrue(completionList.Contains("any"));

            completionList = GetCompletionSetCtrlSpace(
                code.IndexOf("print") + 6,
                code).Completions.Select(x => x.DisplayText).ToArray();

            Assert.IsTrue(completionList.Contains("any"));
        }


        [TestMethod, Priority(0)]
        public void Scenario_Exceptions() {
            foreach (string code in new[] { 
                @"raise None", 
                @"try:
    pass
except None"}) {
                var completionList = GetCompletionSetCtrlSpace(code.IndexOf("None"), code).Completions.Select(x => x.DisplayText).ToArray();

                Assert.IsTrue(completionList.Contains("Exception"));
                Assert.IsTrue(completionList.Contains("KeyboardInterrupt"));
                Assert.IsTrue(completionList.Contains("GeneratorExit"));
                Assert.IsTrue(completionList.Contains("StopIteration"));
                Assert.IsTrue(completionList.Contains("SystemExit"));

                Assert.IsFalse(completionList.Contains("Warning"));
                Assert.IsFalse(completionList.Contains("str"));
                Assert.IsFalse(completionList.Contains("int"));
            }

            foreach (string code in new[] { 
                @"raise (None)", 
                @"try:
    pass
except (None)"}) {
                var completionList = GetCompletionSetCtrlSpace(code.IndexOf("None"), code).Completions.Select(x => x.DisplayText).ToArray();

                Assert.IsTrue(completionList.Contains("Exception"));
                Assert.IsTrue(completionList.Contains("KeyboardInterrupt"));
                Assert.IsTrue(completionList.Contains("GeneratorExit"));
                Assert.IsTrue(completionList.Contains("StopIteration"));
                Assert.IsTrue(completionList.Contains("SystemExit"));

                Assert.IsTrue(completionList.Contains("Warning"));
                Assert.IsTrue(completionList.Contains("str"));
                Assert.IsTrue(completionList.Contains("int"));
            }
        }

        [TestMethod, Priority(0)]
        public void Scenario_MemberCompletion() {
            // TODO: Negative tests
            //       Import / from import tests
            MemberCompletionTest(-1, "x = 2\r\nx.", "x.");

            // combining various partial expressions with previous expressions
            var prefixes = new[] { "", "(", "a = ", "f(", "l[", "{", "if " };
            var exprs = new[] { "x[0].", "x(0).", "x", "x.y.", "f(x[2]).", "f(x, y).", "f({2:3}).", "f(a + b).", "f(a or b).", "{2:3}.", "f(x if False else y).", /*"(\r\nx\r\n)."*/ };
            foreach (var prefix in prefixes) {
                foreach (var expr in exprs) {
                    string test = prefix + expr;
                    //Console.WriteLine("   -- {0}", test);
                    MemberCompletionTest(-1, test, expr);
                }
            }

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
                new { Expr = "f([1,2", Param = 0, Function="f" } ,
                new { Expr = "f([1,2,", Param = 0, Function="f" } ,
                new { Expr = "f({1:2,", Param = 0, Function="f" } ,
                new { Expr = "f({1,", Param = 0, Function="f" } ,
                new { Expr = "f({1:2", Param = 0, Function="f" } ,
                new { Expr = "f({1", Param = 0, Function="f" } ,
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
        public void Scenario_MemberCompletion_Imports() {
            var code = "import sys, ";
            var completions = GetCompletions(code.Length - 1, code);
            AssertUtil.Contains(GetCompletionNames(completions), "exceptions");

            var code2 = "import sys, ex";
            var completionList = GetCompletionSetCtrlSpace(code2.Length - 1, code2);
            AssertUtil.Contains(GetCompletionNames(completionList), "ceptions");
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
foo
bar

baz
'''
";

            for (int i = code.IndexOf("'''") + 4; i < code.LastIndexOf("'''"); i++) {
                Console.WriteLine(i);
                var analysis = AnalyzeExpression(i, code, forCompletion: false);

                var fact = new CPythonInterpreterFactory();
                using (var analyzer = new VsProjectAnalyzer(fact, new[] { fact }, new MockErrorProviderFactory())) {
                    var buffer = new MockTextBuffer(code);
                    buffer.AddProperty(typeof(VsProjectAnalyzer), analyzer);
                    var snapshot = (MockTextSnapshot)buffer.CurrentSnapshot;
#pragma warning disable 618
                    var context = snapshot.GetCompletions(new MockTrackingSpan(snapshot, i, 0));
#pragma warning restore 618
                    Assert.AreEqual(context, NormalCompletionAnalysis.EmptyCompletionContext);
                }
            }
        }

        [TestMethod, Priority(0)]
        public void Scenario_GotoDefinition() {
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
        public void Scenario_QuickInfo() {
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
    pass";


            // we get the appropriate subexpression
            TestQuickInfo(code, code.IndexOf("cls."), code.IndexOf("cls.") + 4, "cls: <no type information available>");
            TestQuickInfo(code, code.IndexOf("cls.") + 4 + 1, code.IndexOf("cls.") + 4 + 1 + 11, "cls._parse_block: <no type information available>");
            TestQuickInfo(code, code.IndexOf("cls.") + 4 + 1 + 11 + 1, code.IndexOf("cls.") + 4 + 1 + 11 + 1 + 4, "ast: <no type information available>");
            TestQuickInfo(code, code.IndexOf("cls.") + 4 + 1 + 11 + 1 + 4 + 1, code.IndexOf("cls.") + 4 + 1 + 11 + 1 + 4 + 1 + 3, "ast.expr: <no type information available>");
            TestQuickInfo(code, code.IndexOf("cls.") + 4 + 1 + 11 + 1 + 4 + 1 + 3 + 1, code.IndexOf("cls.") + 4 + 1 + 11 + 1 + 5 + 3 + 1 + 1, "cls._parse_block(ast.expr): <no type information available>");

            // the whole strieng shows up in quick info
            TestQuickInfo(code, code.IndexOf("x = ") + 4, code.IndexOf("x = ") + 4 + 28, "\"ABCDEFGHIJKLMNOPQRSTUVWYXZ\": str");

            // trailing new lines don't show up in quick info
            TestQuickInfo(code, code.IndexOf("def f") + 4, code.IndexOf("def f") + 5, "f: def f(...)\r\nhelpful information");

            // keywords don't show up in quick info
            TestQuickInfo(code, code.IndexOf("while True:"), code.IndexOf("while True:") + 5);

            // multiline function, hover at the close paren
            TestQuickInfo(code, code.IndexOf("e)") + 1, code.IndexOf("e)") + 2, @"f(a,
(b, c, d),
e): <no type information available>");
        }

        [TestMethod, Priority(0)]
        public void Scenario_NormalOverrides() {
            foreach (var code in new[] {
@"class Foo(object):
    def func_a(self, a=100): pass
    def func_b(self, b, *p, **kw): pass

class Baz(Foo):
    def None
",
@"class Foo(object):
    def func_a(self, a=100): pass

class Bar(Foo):
    def func_b(self, b, *p, **kw): pass

class Baz(Bar):
    def None
",
@"class Foo(object):
    def func_a(self, a=100): pass

class Bar(object):
    def func_b(self, b, *p, **kw): pass

class Baz(Foo, Bar):
    def None
",
@"class Foo(object):
    def func_a(self, a=100): pass
    def func_b(self, b, *p, **kw): pass
    def func_c(self): pass

class Baz(Foo):
    def func_c(self): pass
    def None
",
@"class Foo(object):
    def func_a(self, a=100): pass
    def func_c(self): pass

class Bar(Foo):
    def func_b(self, b, *p, **kw): pass

class Baz(Bar):
    def func_c(self): pass
    def None
",
@"class Foo(object):
    def func_a(self, a=100): pass

class Bar(object):
    def func_b(self, b, *p, **kw): pass
    def func_c(self): pass

class Baz(Foo, Bar):
    def func_c(self): pass
    def None
"}) {
                var completionList = GetCompletionSetCtrlSpace(code.IndexOf("None"), code).Completions.Select(x => x.InsertionText).ToArray();

                Assert.IsTrue(completionList.Contains(@"func_a(self, a = 100):
        return super(Baz, self).func_a(a)"), code);
                Assert.IsTrue(completionList.Contains(@"func_b(self, b, *p, **kw):
        return super(Baz, self).func_b(b, *p, **kw)"), code);
            }
        }

        [TestMethod, Priority(0)]
        public void Scenario_BuiltinOverrides() {
            var code = @"class Foo(str):
    def None
";
            var completionList = GetCompletionSetCtrlSpace(code.IndexOf("None"), code).Completions.Select(x => x.InsertionText).ToArray();

            Assert.IsTrue(completionList.Contains(@"capitalize(self):
        return super(Foo, self).capitalize()"));
            Assert.IsTrue(completionList.Contains(@"index(self, sub, start, end):
        return super(Foo, self).index(sub, start, end)"));

            code = @"class Foo(str, list):
    def None
";
            completionList = GetCompletionSetCtrlSpace(code.IndexOf("None"), code).Completions.Select(x => x.InsertionText).ToArray();
            Assert.IsTrue(completionList.Contains(@"index(self, sub, start, end):
        return super(Foo, self).index(sub, start, end)"));

            code = @"class Foo(list, str):
    def None
";
            completionList = GetCompletionSetCtrlSpace(code.IndexOf("None"), code).Completions.Select(x => x.InsertionText).ToArray();
            Assert.IsTrue(completionList.Contains(@"index(self, item, start, stop):
        return super(Foo, self).index(item, start, stop)"));
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

            AssertUtil.Contains(completions, "bit_length");
            AssertUtil.Contains(completions, "conjugate");
            AssertUtil.DoesntContain(completions, "keys");

            editText = "\r\n\r\n\r\n\r\n\r\n    def ";
            editInsert = code.IndexOf("pass", editInsert) + 4;
            completions = EditAndGetCompletions(code, editText, editInsert, "def ");

            AssertUtil.Contains(completions, "keys");
            AssertUtil.Contains(completions, "__contains__");
            AssertUtil.DoesntContain(completions, "bit_length");
        }

        private static HashSet<string> EditAndGetCompletions(string code, string editText, int editInsert, string completeAfter) {
            CompletionAnalysis context;

            var fact = new CPythonInterpreterFactory();
            using (var analyzer = new VsProjectAnalyzer(fact, new[] { fact }, new MockErrorProviderFactory())) {
                var buffer = new MockTextBuffer(code);
                buffer.AddProperty(typeof(VsProjectAnalyzer), analyzer);
                var snapshot = (MockTextSnapshot)buffer.CurrentSnapshot;

                var monitoredBuffer = analyzer.MonitorTextBuffer(new MockTextView(buffer), buffer);
                analyzer.WaitForCompleteAnalysis(x => true);
                while (((IPythonProjectEntry)buffer.GetAnalysis()).Analysis == null) {
                    System.Threading.Thread.Sleep(500);
                }
                analyzer.StopMonitoringTextBuffer(monitoredBuffer.BufferParser);

                var edit = buffer.CreateEdit();
                edit.Insert(editInsert, editText);
                edit.Apply();

                var newSnapshot = (MockTextSnapshot)snapshot.Version.Next.TextBuffer.CurrentSnapshot;
                Assert.AreNotSame(snapshot, newSnapshot);
                int location = newSnapshot.GetText().IndexOf(completeAfter) + completeAfter.Length;

                context = newSnapshot.GetCompletions(new MockTrackingSpan(newSnapshot, location, 0),
                    new CompletionOptions {
                        ConvertTabsToSpaces = true,
                        IndentSize = 4
                    }
                );
            }

            var completions = context.GetCompletions(new MockGlyphService()).Completions
                .Select(c => c.DisplayText).ToSet();
            return completions;
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
            var fact = new CPythonInterpreterFactory();
            using (var analyzer = new VsProjectAnalyzer(fact, new[] { fact }, new MockErrorProviderFactory())) {
                return AnalyzeExpressionWorker(location, sourceCode, forCompletion, analyzer);
            }
        }

        private static List<object> AnalyzeQuickInfo(int location, string sourceCode, bool forCompletion = true) {
            if (location < 0) {
                location = sourceCode.Length + location + 1;
            }
            var fact = new CPythonInterpreterFactory();
            using (var analyzer = new VsProjectAnalyzer(fact, new[] { fact }, new MockErrorProviderFactory())) {
                var analysis = AnalyzeExpressionWorker(location, sourceCode, forCompletion, analyzer);

                List<object> quickInfo = new List<object>();
                ITrackingSpan span;
                QuickInfoSource.AugmentQuickInfoWorker(
                    analysis,
                    quickInfo,
                    out span);

                return quickInfo;
            }
        }

        private static ExpressionAnalysis AnalyzeExpressionWorker(int location, string sourceCode, bool forCompletion, VsProjectAnalyzer analyzer) {
            var buffer = new MockTextBuffer(sourceCode);
            buffer.AddProperty(typeof(VsProjectAnalyzer), analyzer);
            var textView = new MockTextView(buffer);
            var item = analyzer.MonitorTextBuffer(textView, textView.TextBuffer); // We leak here because we never un-monitor, but it's a test.
            while (!item.ProjectEntry.IsAnalyzed) {
                Thread.Sleep(10);
            }

            var snapshot = (MockTextSnapshot)buffer.CurrentSnapshot;
            analyzer.StopMonitoringTextBuffer(item.BufferParser);
            return snapshot.AnalyzeExpression(new MockTrackingSpan(snapshot, location, location == snapshot.Length ? 0 : 1), forCompletion);
        }

        private static void MemberCompletionTest(int location, string sourceCode, string expectedExpression) {
            var context = GetCompletions(location, sourceCode);
            Assert.AreEqual(expectedExpression, context.Text);
        }

        private static CompletionAnalysis GetCompletions(int location, string sourceCode, bool intersectMembers = true) {
            if (location < 0) {
                location = sourceCode.Length + location + 1;
            }
            var fact = new CPythonInterpreterFactory();
            using (var analyzer = new VsProjectAnalyzer(fact, new[] { fact }, new MockErrorProviderFactory())) {
                return GetCompletionsWorker(location, sourceCode, intersectMembers, analyzer);
            }
        }

        private static CompletionSet GetCompletionSet(int location, string sourceCode, bool intersectMembers = true) {
            if (location < 0) {
                location = sourceCode.Length + location + 1;
            }
            var fact = new CPythonInterpreterFactory();
            using (var analyzer = new VsProjectAnalyzer(fact, new[] { fact }, new MockErrorProviderFactory())) {
                return GetCompletionsWorker(location, sourceCode, intersectMembers, analyzer).GetCompletions(new MockGlyphService());
            }
        }

        /// <summary>
        /// Simulates the user hitting Ctrl-Space to get completions.
        /// </summary>
        private static CompletionSet GetCompletionSetCtrlSpace(int location, string sourceCode, bool intersectMembers = true) {
            IntellisenseController.ForceCompletions = true;
            try {
                return GetCompletionSet(location, sourceCode, intersectMembers);
            } finally {
                IntellisenseController.ForceCompletions = false;
            }
        }

        private static CompletionAnalysis GetCompletionsWorker(int location, string sourceCode, bool intersectMembers, VsProjectAnalyzer analyzer) {
            var buffer = new MockTextBuffer(sourceCode);
            buffer.AddProperty(typeof(VsProjectAnalyzer), analyzer);
            var snapshot = (MockTextSnapshot)buffer.CurrentSnapshot;

            var monitoredBuffer = analyzer.MonitorTextBuffer(new MockTextView(buffer), buffer);
            analyzer.WaitForCompleteAnalysis(x => true);
            while (((IPythonProjectEntry)buffer.GetAnalysis()).Analysis == null) {
                System.Threading.Thread.Sleep(500);
            }
            analyzer.StopMonitoringTextBuffer(monitoredBuffer.BufferParser);
#pragma warning disable 618
            var context = snapshot.GetCompletions(new MockTrackingSpan(snapshot, location, 0), 
                new CompletionOptions {
                    IntersectMembers = intersectMembers,
                    ConvertTabsToSpaces = true,
                    IndentSize = 4
                }
            );
#pragma warning restore 618
            return context;
        }

        private static void SignatureTest(int location, string sourceCode, string expectedExpression, int paramIndex) {
            if (location < 0) {
                location = sourceCode.Length + location;
            }
            var fact = new CPythonInterpreterFactory();
            using (var analyzer = new VsProjectAnalyzer(fact, new[] { fact }, new MockErrorProviderFactory())) {
                var buffer = new MockTextBuffer(sourceCode);
                buffer.AddProperty(typeof(VsProjectAnalyzer), analyzer);
                var snapshot = (MockTextSnapshot)buffer.CurrentSnapshot;
                var context = snapshot.GetSignatures(new MockTrackingSpan(snapshot, location, 1));
                Assert.AreEqual(context.Text, expectedExpression);
                Assert.AreEqual(context.ParameterIndex, paramIndex);
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

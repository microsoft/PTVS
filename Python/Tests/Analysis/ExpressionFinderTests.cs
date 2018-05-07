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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.IO;
using System.Linq;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    [TestClass]
    public class ExpressionFinderTests {
        [TestMethod, Priority(0)]
        public void FindExpressionsForTooltip() {
            var code = Parse(@"class C(object):
    def f(a):
        return a

b = C().f(1)
", GetExpressionOptions.Hover);

            AssertNoExpr(code, 1, 1);
            AssertNoExpr(code, 1, 6);
            AssertExpr(code, 1, 7, "C");
            AssertExpr(code, 1, 8, "C");
            AssertExpr(code, 1, 9, "object");
            AssertExpr(code, 1, 15, "object");
            AssertNoExpr(code, 1, 16);

            AssertNoExpr(code, 2, 5);
            AssertNoExpr(code, 2, 8);
            AssertExpr(code, 2, 9, "f");
            AssertExpr(code, 2, 10, "f");
            AssertExpr(code, 2, 11, "a");
            AssertExpr(code, 2, 12, "a");

            AssertNoExpr(code, 3, 15);
            AssertExpr(code, 3, 16, "a");
            AssertExpr(code, 3, 17, "a");

            AssertExpr(code, 5, 5, "C");
            AssertExpr(code, 5, 6, "C");
            AssertExpr(code, 5, 7, "C()");
            AssertExpr(code, 5, 9, "C().f");
            AssertExpr(code, 5, 10, "C().f");
            AssertExpr(code, 5, 11, "1");

            AssertExpr(code, 5, 10, 5, 12, "C().f(1)");
        }

        [TestMethod, Priority(0)]
        public void FindExpressionsForEvaluate() {
            var code = Parse(@"class C(object):
    def f(a):
        return a

b = C().f(1)
", GetExpressionOptions.Evaluate);
            var clsCode = code.Source.Substring(0, code.Source.IndexOfEnd("return a"));
            var funcCode = clsCode.Substring(clsCode.IndexOf("def"));

            AssertExpr(code, 1, 1, clsCode);
            AssertExpr(code, 1, 7, 1, 8, "C");
            AssertExpr(code, 1, 7, 1, 9, clsCode);
            AssertExpr(code, 1, 9, "object");
            AssertExpr(code, 1, 15, "object");
            AssertExpr(code, 1, 16, clsCode);
            AssertExpr(code, 1, 15, 1, 16, clsCode);

            AssertNoExpr(code, 2, 1);
            AssertExpr(code, 2, 5, funcCode);
            AssertExpr(code, 2, 9, 2, 10, "f");

            AssertNoExpr(code, 3, 15);
            AssertExpr(code, 3, 16, "a");
            AssertExpr(code, 3, 17, "a");

            AssertExpr(code, 5, 5, "C");
            AssertExpr(code, 5, 6, "C");
            AssertExpr(code, 5, 7, "C()");
            AssertExpr(code, 5, 9, "C().f");
            AssertExpr(code, 5, 10, "C().f");
            AssertExpr(code, 5, 9, 5, 10, "C().f");
            AssertExpr(code, 5, 11, "1");

            // Same code as in the GotoDefinition test
            code = Parse(@"class C:
    def fff(self): pass
i=1+2
C().fff", GetExpressionOptions.Evaluate);

            AssertExpr(code, 2, 9, 2, 12, "fff");
            AssertExpr(code, 2, 13, 2, 17, "self");
            AssertExpr(code, 1, 7, 1, 8, "C");
            AssertExpr(code, 3, 3, 3, 4, "1");
            AssertExpr(code, 3, 5, 3, 6, "2");
            AssertExpr(code, 4, 1, 4, 2, "C");
            AssertExpr(code, 4, 6, 4, 8, "C().fff");
        }

        [TestMethod, Priority(0)]
        public void FindExpressionsForComplete() {
            PythonAstAndSource code;

            code = Parse(@"class 

pass", GetExpressionOptions.Complete);
            AssertExpr(code, 1, 6, "class");
            AssertNoExpr(code, 1, 7);

            code = Parse(@"def 

pass", GetExpressionOptions.Complete);
            AssertExpr(code, 1, 4, "def");
            AssertNoExpr(code, 1, 5);

            code = Parse(@"class C(object):
    def f(a):
        return a

b = C().f(1)
", GetExpressionOptions.Complete);

            AssertExpr(code, 1, 1, "class");
            AssertNoExpr(code, 1, 7);
            AssertNoExpr(code, 1, 8);
            AssertNoExpr(code, 1, 7, 1, 9);
            AssertExpr(code, 1, 9, "object");
            AssertExpr(code, 1, 15, "object");
            AssertNoExpr(code, 1, 16);
            AssertNoExpr(code, 1, 15, 1, 16);

            AssertNoExpr(code, 2, 1);
            AssertExpr(code, 2, 5, "def");
            AssertNoExpr(code, 2, 9);
            AssertNoExpr(code, 2, 10);

            AssertExpr(code, 3, 15, "return");
            AssertExpr(code, 3, 16, "a");
            AssertExpr(code, 3, 17, "a");

            AssertExpr(code, 5, 5, "C");
            AssertExpr(code, 5, 6, "C");
            AssertNoExpr(code, 5, 7);
            AssertExpr(code, 5, 9, "f");
            AssertExpr(code, 5, 10, "f");
            AssertExpr(code, 5, 9, 5, 10, "f");
            AssertNoExpr(code, 5, 11);

            // Same code as in the GotoDefinition test
            code = Parse(@"class C:
    def fff(self, x=a): pass
i=1+2
C().fff", GetExpressionOptions.Complete);

            AssertNoExpr(code, 1, 8);
            AssertNoExpr(code, 2, 9, 2, 12);
            AssertNoExpr(code, 2, 13, 2, 17);
            AssertExpr(code, 2, 22, "a");
            AssertExpr(code, 2, 29, "pass");
            AssertNoExpr(code, 3, 4);
            AssertNoExpr(code, 3, 6);
            AssertExpr(code, 4, 1, 4, 2, "C");
            AssertExpr(code, 4, 6, 4, 8, "fff");
        }

        [TestMethod, Priority(0)]
        public void FindExpressionsForDefinition() {
            var code = Parse(@"class C(object):
    def f(a):
        return a

b = C().f(1)
", GetExpressionOptions.FindDefinition);
            var clsCode = code.Source.Substring(0, code.Source.IndexOfEnd("return a"));
            var funcCode = clsCode.Substring(clsCode.IndexOf("def"));

            AssertNoExpr(code, 1, 1);
            AssertExpr(code, 1, 7, "C");
            AssertExpr(code, 1, 8, "C");
            AssertNoExpr(code, 1, 7, 1, 9);
            AssertExpr(code, 1, 9, "object");
            AssertExpr(code, 1, 15, "object");
            AssertNoExpr(code, 1, 16);
            AssertNoExpr(code, 1, 15, 1, 16);

            AssertNoExpr(code, 2, 1);
            AssertNoExpr(code, 2, 5);
            AssertExpr(code, 2, 9, "f");
            AssertExpr(code, 2, 10, "f");
            AssertExpr(code, 2, 11, "a");

            AssertNoExpr(code, 3, 15);
            AssertExpr(code, 3, 16, "a");
            AssertExpr(code, 3, 17, "a");

            AssertExpr(code, 5, 1, "b");
            AssertExpr(code, 5, 5, "C");
            AssertExpr(code, 5, 6, "C");
            AssertNoExpr(code, 5, 7);
            AssertExpr(code, 5, 9, "C().f");
            AssertExpr(code, 5, 10, "C().f");
            AssertExpr(code, 5, 9, 5, 10, "C().f");
            AssertNoExpr(code, 5, 11);

            // Same code as in the GotoDefinition test
            code = Parse(@"class C:
    def fff(self, x=a): pass
i=1+2
C().fff", GetExpressionOptions.FindDefinition);

            AssertExpr(code, 1, 8, "C");
            AssertExpr(code, 2, 9, 2, 12, "fff");
            AssertExpr(code, 2, 13, 2, 17, "self");
            AssertExpr(code, 2, 22, "a");
            AssertNoExpr(code, 2, 29);
            AssertNoExpr(code, 3, 4);
            AssertNoExpr(code, 3, 6);
            AssertExpr(code, 4, 1, 4, 2, "C");
            AssertExpr(code, 4, 6, 4, 8, "C().fff");
        }

        [TestMethod, Priority(0)]
        public void FindKeywords() {
            var code = Parse(@"a and b
assert a
async def f():
    global a
    nonlocal b
    await a
    async for x in y:
        return
while True:
    break
    continue
class A():
    def f(self):
        (yield x) + (yield from y)
[a for x in a]
del a
import b
from a import b
x = lambda: a
a or b
raise a
with a as b:
    pass
a in b
a not in b
a is b
a is not b
a if a else b
if a: pass
not a
try: pass
except: pass
while a: pass
", new GetExpressionOptions { Keywords = true });
            AssertExpr(code, 1, 3, "and");
            AssertExpr(code, 2, 4, "assert");
            AssertExpr(code, 3, 2, "async");
            AssertExpr(code, 3, 8, "def");
            AssertExpr(code, 4, 6, "global");
            AssertExpr(code, 5, 6, "nonlocal");
            AssertExpr(code, 6, 6, "await");
            AssertExpr(code, 7, 6, "async");
            AssertExpr(code, 7, 12, "for");
            AssertExpr(code, 7, 17, "in");
            AssertExpr(code, 8, 10, "return");
            AssertExpr(code, 9, 2, "while");
            AssertExpr(code, 10, 6, "break");
            AssertExpr(code, 11, 6, "continue");
            AssertExpr(code, 12, 2, "class");
            AssertExpr(code, 13, 6, "def");
            AssertExpr(code, 14, 13, "yield");
            AssertExpr(code, 14, 26, "yield");
            AssertExpr(code, 14, 28, "from");
            AssertExpr(code, 15, 6, "for");
            AssertExpr(code, 15, 12, "in");
            AssertExpr(code, 16, 2, "del");
            AssertExpr(code, 17, 2, "import");
            AssertExpr(code, 18, 2, "from");
            AssertExpr(code, 18, 12, "import");
            AssertExpr(code, 19, 8, "lambda");
            AssertExpr(code, 20, 4, "or");
            AssertExpr(code, 21, 1, "raise");
            AssertExpr(code, 22, 3, "with");
            AssertExpr(code, 22, 9, "as");
            AssertExpr(code, 23, 8, "pass");
            AssertExpr(code, 24, 5, "in");
            AssertExpr(code, 25, 5, "not");
            AssertExpr(code, 25, 7, "in");
            AssertExpr(code, 26, 5, "is");
            AssertExpr(code, 27, 5, "is");
            AssertExpr(code, 27, 9, "not");
            AssertExpr(code, 28, 3, "if");
            AssertExpr(code, 28, 10, "else");
            AssertExpr(code, 29, 1, "if");
            AssertExpr(code, 30, 1, "not");
            AssertExpr(code, 31, 1, "try");
            //AssertExpr(code, 32, 1, "except");
            AssertExpr(code, 33, 1, "while");
        }

        [TestMethod, Priority(0)]
        public void FindExpressionsForEvaluateMembers() {
            var code = Parse(@"a.b.c.d.", GetExpressionOptions.EvaluateMembers);
            AssertNoExpr(code, 1, 2);
            AssertExpr(code, 1, 3, "a");
            AssertExpr(code, 1, 4, "a");
            AssertExpr(code, 1, 5, "a.b");
            AssertExpr(code, 1, 6, "a.b");
            AssertExpr(code, 1, 7, "a.b.c");
            AssertExpr(code, 1, 8, "a.b.c");
            AssertExpr(code, 1, 9, "a.b.c.d");

            code = Parse(@"a[1].b(2).", GetExpressionOptions.EvaluateMembers);
            AssertNoExpr(code, 1, 2);
            AssertNoExpr(code, 1, 4);
            AssertNoExpr(code, 1, 5);
            AssertExpr(code, 1, 6, "a[1]");
            AssertExpr(code, 1, 7, "a[1]");
            AssertNoExpr(code, 1, 8);
            AssertNoExpr(code, 1, 10);
            AssertExpr(code, 1, 11, "a[1].b(2)");

            code = Parse(@"x={y:f(a=2).", GetExpressionOptions.EvaluateMembers);
            AssertExpr(code, 1, 13, "f(a=2)");

            code = Parse(@"f(a.", GetExpressionOptions.EvaluateMembers);
            AssertNoExpr(code, 1, 4);
            AssertExpr(code, 1, 5, "a");
        }

        [TestMethod, Priority(0)]
        public void FindExpressionsForRename() {
            var code = Parse(@"class C(object):
    def f(a, *b, **c = True):
        global a
        nonlocal a
        return a

b = C().f(1)
", GetExpressionOptions.Rename);

            AssertNoExpr(code, 1, 1);
            AssertExpr(code, 1, 7, 1, 8, "C");
            AssertNoExpr(code, 1, 7, 1, 9);
            AssertExpr(code, 1, 9, "object");
            AssertExpr(code, 1, 15, "object");
            AssertNoExpr(code, 1, 16);
            AssertNoExpr(code, 1, 15, 1, 16);

            AssertNoExpr(code, 2, 1);
            AssertNoExpr(code, 2, 5);
            AssertExpr(code, 2, 9, 2, 10, "f");
            AssertExpr(code, 2, 11, "a");
            AssertNoExpr(code, 2, 14);
            AssertExpr(code, 2, 15, "b");
            AssertNoExpr(code, 2, 19);
            AssertExpr(code, 2, 20, "c");
            AssertNoExpr(code, 2, 22);

            AssertNoExpr(code, 3, 15);
            AssertExpr(code, 3, 16, "a");
            AssertExpr(code, 3, 17, "a");

            AssertNoExpr(code, 4, 17);
            AssertExpr(code, 4, 18, "a");
            AssertExpr(code, 4, 19, "a");

            AssertNoExpr(code, 5, 15);
            AssertExpr(code, 5, 16, "a");
            AssertExpr(code, 5, 17, "a");

            AssertExpr(code, 7, 5, "C");
            AssertExpr(code, 7, 6, "C");
            AssertNoExpr(code, 7, 7);
            AssertExpr(code, 7, 9, "f");
            AssertExpr(code, 7, 10, "f");
            AssertNoExpr(code, 7, 11);
        }



        private class PythonAstAndSource {
            public PythonAst Ast;
            public string Source;
            public GetExpressionOptions Options;
        }

        private static PythonAstAndSource Parse(string code, GetExpressionOptions options) {
            code += "\n";
            var parser = Parser.CreateParser(new StringReader(code), PythonLanguageVersion.V35, new ParserOptions { Verbatim = true });
            return new PythonAstAndSource { Ast = parser.ParseFile(), Source = code, Options = options };
        }

        private static void AssertNoExpr(PythonAstAndSource astAndSource, int line, int column) {
            AssertNoExpr(astAndSource, line, column, line, column);
        }

        private static void AssertNoExpr(PythonAstAndSource astAndSource, int startLine, int startColumn, int endLine, int endColumn) {
            var ast = astAndSource.Ast;
            var code = astAndSource.Source;
            var options = astAndSource.Options;

            int start = ast.LocationToIndex(new SourceLocation(startLine, 1));
            int end = ast.LocationToIndex(new SourceLocation(endLine + 1, 1));
            var fullLine = code.Substring(start);
            int lastNewline = "\r\n".Select(c => fullLine.LastIndexOf(c, end - start - 1)).Where(i => i > 0).DefaultIfEmpty(-1).Min();
            if (lastNewline > 0) {
                fullLine = fullLine.Remove(lastNewline);
            }

            var finder = new ExpressionFinder(ast, options);
            var range = new SourceSpan(new SourceLocation(startLine, startColumn), new SourceLocation(endLine, endColumn));
            var span = finder.GetExpressionSpan(range);
            if (span == null || span.Value.Start == span.Value.End) {
                return;
            }

            start = ast.LocationToIndex(span.Value.Start);
            end = ast.LocationToIndex(span.Value.End);
            var actual = code.Substring(start, end - start);
            Assert.Fail($"Found unexpected expression <{actual}> at from {range} at {span.Value} in <{fullLine}>");
        }

        private static void AssertExpr(PythonAstAndSource astAndSource, int line, int column, string expected) {
            AssertExpr(astAndSource, line, column, line, column, expected);
        }

        private static void AssertExpr(PythonAstAndSource astAndSource, int startLine, int startColumn, int endLine, int endColumn, string expected) {
            var ast = astAndSource.Ast;
            var code = astAndSource.Source;
            var options = astAndSource.Options;

            int start = ast.LocationToIndex(new SourceLocation(startLine, 1));
            int end = ast.LocationToIndex(new SourceLocation(endLine + 1, 1));
            var fullLine = code.Substring(start);
            int lastNewline = "\r\n".Select(c => fullLine.LastIndexOf(c, end - start - 1)).Where(i => i > 0).DefaultIfEmpty(-1).Min();
            if (lastNewline > 0) {
                fullLine = fullLine.Remove(lastNewline);
            }

            var finder = new ExpressionFinder(ast, options);
            var range = new SourceSpan(new SourceLocation(startLine, startColumn), new SourceLocation(endLine, endColumn));
            var span = finder.GetExpressionSpan(range);
            Assert.IsNotNull(span, $"Did not find any expression at {range} in <{fullLine}>");

            start = ast.LocationToIndex(span.Value.Start);
            end = ast.LocationToIndex(span.Value.End);
            var actual = code.Substring(start, end - start);
            Assert.AreEqual(expected, actual, $"Mismatched expression from {range} at {span.Value} in <{fullLine}>");
        }
    }
}

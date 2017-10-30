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
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    [TestClass]
    public class ExpressionFinderTests {
        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
        public void FindExpressionsForRename() {
            var code = Parse(@"class C(object):
    def f(a):
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

            AssertNoExpr(code, 3, 15);
            AssertExpr(code, 3, 16, "a");
            AssertExpr(code, 3, 17, "a");

            AssertExpr(code, 5, 5, "C");
            AssertExpr(code, 5, 6, "C");
            AssertNoExpr(code, 5, 7);
            AssertExpr(code, 5, 9, "f");
            AssertExpr(code, 5, 10, "f");
            AssertNoExpr(code, 5, 11);
        }



        private class PythonAstAndSource {
            public PythonAst Ast;
            public string Source;
            public GetExpressionOptions Options;
        }

        private static PythonAstAndSource Parse(string code, GetExpressionOptions options) {
            code += "\n";
            using (var parser = Parser.CreateParser(new StringReader(code), PythonLanguageVersion.V35, new ParserOptions { Verbatim = true })) {
                return new PythonAstAndSource { Ast = parser.ParseFile(), Source = code, Options = options };
            }
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
            fullLine = fullLine.Remove("\r\n".Select(c => fullLine.LastIndexOf(c, end - start - 1)).Where(i => i > 0).Min());

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
            fullLine = fullLine.Remove("\r\n".Select(c => fullLine.LastIndexOf(c, end - start - 1)).Where(i => i > 0).Min());

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

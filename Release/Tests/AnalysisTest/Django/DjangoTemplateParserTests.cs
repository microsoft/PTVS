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
using Microsoft.PythonTools.Django.TemplateParsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnalysisTest {
    [TestClass]
    public class DjangoTemplateParserTests {
        #region Filter parser tests

        [TestMethod]
        public void FilterRegexTests() {
            var testCases = new[] { 
                new { Got = ("100"), Expected = DjangoVariable.Number("100", 0) },
                new { Got = ("100.0"), Expected = DjangoVariable.Number("100.0", 0) },
                new { Got = ("+100"), Expected = DjangoVariable.Number("+100", 0) },
                new { Got = ("-100"), Expected = DjangoVariable.Number("-100", 0) },
                new { Got = ("'foo'"), Expected = DjangoVariable.Constant("'foo'", 0) },
                new { Got = ("\"foo\""), Expected = DjangoVariable.Constant("\"foo\"", 0) },
                new { Got = ("foo"), Expected = DjangoVariable.Variable("foo", 0) },
                new { Got = ("foo.bar"), Expected = DjangoVariable.Variable("foo.bar", 0) },
                new { Got = ("foo|bar"), Expected = DjangoVariable.Variable("foo", 0, new DjangoFilter("bar", 4)) },
                new { Got = ("foo|bar|baz"), Expected = DjangoVariable.Variable("foo", 0, new DjangoFilter("bar", 4), new DjangoFilter("baz", 8)) },
                new { Got = ("foo|bar:'foo'"), Expected = DjangoVariable.Variable("foo", 0, DjangoFilter.Constant("bar", 4, "'foo'", 8)) },
                new { Got = ("foo|bar:42"), Expected = DjangoVariable.Variable("foo", 0, DjangoFilter.Number("bar", 4, "42", 8)) },
                new { Got = ("foo|bar:\"foo\""), Expected = DjangoVariable.Variable("foo", 0, DjangoFilter.Constant("bar", 4, "\"foo\"", 8)) },
                new { Got = ("foo|bar:100"), Expected = DjangoVariable.Variable("foo", 0, DjangoFilter.Number("bar", 4, "100", 8)) },
                new { Got = ("foo|bar:100.0"), Expected = DjangoVariable.Variable("foo", 0, DjangoFilter.Number("bar", 4, "100.0", 8)) },
                new { Got = ("foo|bar:+100.0"), Expected = DjangoVariable.Variable("foo", 0, DjangoFilter.Number("bar", 4, "+100.0", 8)) },
                new { Got = ("foo|bar:-100.0"), Expected =  DjangoVariable.Variable("foo", 0, DjangoFilter.Number("bar", 4, "-100.0", 8)) },
                new { Got = ("foo|bar:baz.quox"), Expected = DjangoVariable.Variable("foo", 0, DjangoFilter.Variable("bar", 4, "baz.quox", 8)) },
                new { Got = ("foo|bar:baz"), Expected = DjangoVariable.Variable("foo", 0, DjangoFilter.Variable("bar", 4, "baz", 8)) },

                new { Got = ("{{ 100 }}"), Expected = DjangoVariable.Number("100", 3) },
                new { Got = ("{{ 100.0 }}"), Expected = DjangoVariable.Number("100.0", 3) },
                new { Got = ("{{ +100 }}"), Expected = DjangoVariable.Number("+100", 3) },
                new { Got = ("{{ -100 }}"), Expected = DjangoVariable.Number("-100", 3) },
                new { Got = ("{{ 'foo' }}"), Expected = DjangoVariable.Constant("'foo'", 3) },
                new { Got = ("{{ \"foo\" }}"), Expected = DjangoVariable.Constant("\"foo\"", 3) },
                new { Got = ("{{ foo }}"), Expected = DjangoVariable.Variable("foo", 3) },
                new { Got = ("{{ foo.bar }}"), Expected = DjangoVariable.Variable("foo.bar", 3) },
                new { Got = ("{{ foo|bar }}"), Expected = DjangoVariable.Variable("foo", 3, new DjangoFilter("bar", 7)) },                
                new { Got = ("{{ foo|bar|baz }}"), Expected = DjangoVariable.Variable("foo", 3, new DjangoFilter("bar", 7), new DjangoFilter("baz", 11)) },
                new { Got = ("{{ foo|bar:'foo' }}"), Expected = DjangoVariable.Variable("foo", 3, DjangoFilter.Constant("bar", 7, "'foo'", 11)) },
                new { Got = ("{{ foo|bar:42 }}"), Expected = DjangoVariable.Variable("foo", 3, DjangoFilter.Number("bar", 7, "42", 11)) },
                new { Got = ("{{ foo|bar:\"foo\" }}"), Expected = DjangoVariable.Variable("foo", 3, DjangoFilter.Constant("bar", 7, "\"foo\"", 11)) },
                new { Got = ("{{ foo|bar:100 }}"), Expected = DjangoVariable.Variable("foo", 3, DjangoFilter.Number("bar", 7, "100", 11)) },
                new { Got = ("{{ foo|bar:100.0 }}"), Expected = DjangoVariable.Variable("foo", 3, DjangoFilter.Number("bar", 7, "100.0", 11)) },
                new { Got = ("{{ foo|bar:+100.0 }}"), Expected = DjangoVariable.Variable("foo", 3, DjangoFilter.Number("bar", 7, "+100.0", 11)) },
                new { Got = ("{{ foo|bar:-100.0 }}"), Expected =  DjangoVariable.Variable("foo", 3, DjangoFilter.Number("bar", 7, "-100.0", 11)) },
                new { Got = ("{{ foo|bar:baz.quox }}"), Expected = DjangoVariable.Variable("foo", 3, DjangoFilter.Variable("bar", 7, "baz.quox", 11)) },
                new { Got = ("{{ foo|bar:baz }}"), Expected = DjangoVariable.Variable("foo", 3, DjangoFilter.Variable("bar", 7, "baz", 11)) },
};

            foreach (var testCase in testCases) {
                Console.WriteLine(testCase.Got);

                var got = DjangoVariable.Parse(testCase.Got);

                ValidateFilter(testCase.Expected, got);
            }
        }

        internal void ValidateFilter(DjangoVariable got, DjangoVariable expected) {
            Assert.AreEqual(expected.Expression.Value, got.Expression.Value);
            Assert.AreEqual(expected.Expression.Kind, got.Expression.Kind);
            Assert.AreEqual(expected.ExpressionStart, got.ExpressionStart);
            Assert.AreEqual(expected.Filters.Length, got.Filters.Length);
            for (int i = 0; i < expected.Filters.Length; i++) {
                if (expected.Filters[i].Arg == null) {
                    Assert.AreEqual(null, got.Filters[i].Arg);
                } else {
                    Assert.AreEqual(expected.Filters[i].Arg.Value, got.Filters[i].Arg.Value);
                    Assert.AreEqual(expected.Filters[i].Arg.Kind, got.Filters[i].Arg.Kind);
                    Assert.AreEqual(expected.Filters[i].ArgStart, got.Filters[i].ArgStart);
                }
                Assert.AreEqual(expected.Filters[i].Filter, got.Filters[i].Filter);
            }
        }

        #endregion

        #region Block parser tests

        [TestMethod]
        public void BlockParserTests() {
            var testCases = new[] { 
                new { Got = ("for x in bar"), Expected = new DjangoForBlock(0, 6) },
            };

            foreach (var testCase in testCases) {
                Console.WriteLine(testCase.Got);

                var got = DjangoBlock.Parse(testCase.Got);

                ValidateBlock(testCase.Expected, got);
            }
        }

        private static Dictionary<Type, Action<DjangoBlock, DjangoBlock>> _blockValidators = MakeBlockValidators();

        private static Dictionary<Type, Action<DjangoBlock, DjangoBlock>> MakeBlockValidators() {
            return new Dictionary<Type, Action<DjangoBlock, DjangoBlock>>() {
                { typeof(DjangoForBlock), ValidateForBlock }
            };
        }

        private static void ValidateForBlock(DjangoBlock expected, DjangoBlock got) {
            DjangoForBlock forExpected = (DjangoForBlock)expected;
            DjangoForBlock forGot = (DjangoForBlock)got;

            Assert.AreEqual(forExpected.Start, forGot.Start);
            Assert.AreEqual(forExpected.InStart, forGot.InStart);
        }

        private void ValidateBlock(DjangoBlock expected, DjangoBlock got) {
            Assert.AreEqual(expected.GetType(), got.GetType());

            _blockValidators[expected.GetType()](expected, got);
        }

        #endregion

        #region Template tokenizer tests

        [TestMethod]
        public void TestSimpleVariable() {
            var code = @"<html>
<head><title></title></head>

<body>

{{ content }}

</body>
</html>";

            TokenizerTest(code,
                new TemplateToken(TemplateTokenKind.Text, 0, 49),
                new TemplateToken(TemplateTokenKind.Variable, 50, 62),
                new TemplateToken(TemplateTokenKind.Text, 63, 82)
            );

        }

        [TestMethod]
        public void TestEmbeddedWrongClose() {
            var code = @"<html>
<head><title></title></head>

<body>

{{ content %} }}

</body>
</html>";

            TokenizerTest(code,
                new TemplateToken(TemplateTokenKind.Text, 0, 49),
                new TemplateToken(TemplateTokenKind.Variable, 50, 65),
                new TemplateToken(TemplateTokenKind.Text, 66, 85)
            );
        }

        [TestMethod]
        public void SingleTrailingChar() {
            foreach (var code in new[] { "{{foo}}\n", "{{foo}}a" }) {
                TokenizerTest(code,
                    new TemplateToken(TemplateTokenKind.Variable, 0, 6),
                    new TemplateToken(TemplateTokenKind.Text, 7, 7)
                );
            }
        }

        // 
        struct TemplateTokenResult {
            public readonly TemplateToken Token;
            public readonly char? Start, End;
            public TemplateTokenResult(TemplateToken token, char? start = null, char? end = null) {
                Token = token;
                Start = start;
                End = end;
            }

            public static implicit operator TemplateTokenResult(TemplateToken token) {
                return new TemplateTokenResult(token);
            }
        }

        [TestMethod]
        public void TestSimpleBlock() {
            var code = @"<html>
<head><title></title></head>

<body>

{% block %}

</body>
</html>";

            TokenizerTest(code,
                new TemplateToken(TemplateTokenKind.Text, 0, 49),
                new TemplateToken(TemplateTokenKind.Block, 50, 60),
                new TemplateToken(TemplateTokenKind.Text, 61, code.Length - 1));

        }


        [TestMethod]
        public void TestSimpleComment() {
            var code = @"<html>
<head><title></title></head>

<body>

{# comment #}

</body>
</html>";

            TokenizerTest(code,
                new TemplateToken(TemplateTokenKind.Text, 0, 49),
                new TemplateToken(TemplateTokenKind.Comment, 50, 62),
                new TemplateToken(TemplateTokenKind.Text, 63, code.Length - 1));

        }

        [TestMethod]
        public void TestUnclosedVariable() {
            var code = @"<html>
<head><title></title></head>

<body>

{{ content 

</body>
</html>";

            TokenizerTest(code, new TemplateToken(TemplateTokenKind.Text, 0, code.Length - 1));
        }

        [TestMethod]
        public void TestTextStartAndEnd() {
            var code = @"<html>
<head><title></title></head>

<body>

<p>{{ content }}</p>

</body>
</html>";

            TokenizerTest(code,
                new TemplateTokenResult(
                    new TemplateToken(TemplateTokenKind.Text, 0, code.IndexOf("<p>") + 2),
                    '<',
                    '>'
                ),
                new TemplateToken(TemplateTokenKind.Variable, code.IndexOf("<p>") + 3, code.IndexOf("</p>") - 1),
                new TemplateTokenResult(
                    new TemplateToken(TemplateTokenKind.Text, code.IndexOf("</p>"), code.Length - 1),
                    '<',
                    '>'
                )
            );
        }

        [TestMethod]
        public void TestUnclosedComment() {
            var code = @"<html>
<head><title></title></head>

<body>

{# content 

</body>
</html>";

            TokenizerTest(code, new TemplateToken(TemplateTokenKind.Text, 0, code.Length - 1));
        }

        [TestMethod]
        public void TestUnclosedBlock() {
            var code = @"<html>
<head><title></title></head>

<body>

{% content 

</body>
</html>";

            TokenizerTest(code, new TemplateToken(TemplateTokenKind.Text, 0, code.Length - 1));
        }


        private void TokenizerTest(string text, params TemplateTokenResult[] expected) {
            var tokenizer = new TemplateTokenizer(new StringReader(text));
            var tokens = tokenizer.GetTokens().ToArray();

            bool passed = false;
            try {
                Assert.AreEqual(expected.Length, tokens.Length);
                Assert.AreEqual(0, tokens[0].Start);
                Assert.AreEqual(text.Length - 1, tokens[tokens.Length - 1].End);

                for (int i = 0; i < expected.Length; i++) {
                    var expectedToken = expected[i].Token;

                    Assert.AreEqual(expectedToken.Kind, tokens[i].Kind);
                    Assert.AreEqual(expectedToken.Start, tokens[i].Start);
                    Assert.AreEqual(expectedToken.End, tokens[i].End);
                    switch (expectedToken.Kind) {
                        case TemplateTokenKind.Block:
                        case TemplateTokenKind.Comment:
                        case TemplateTokenKind.Variable:
                            Assert.AreEqual('{', text[expectedToken.Start]);
                            Assert.AreEqual('}', text[expectedToken.End]);
                            break;
                    }
                    if (expected[i].Start != null) {
                        Assert.AreEqual(expected[i].Start, text[expectedToken.Start]);
                    }
                    if (expected[i].End != null) {
                        Assert.AreEqual(expected[i].End, text[expectedToken.End]);
                    }
                }
                passed = true;
            } finally {
                if (!passed) {
                    List<string> res = new List<string>();
                    for (int i = 0; i < tokens.Length; i++) {
                        res.Add(
                            String.Format("new TemplateToken(TemplateTokenKind.{0}, {1}, {2})",
                                tokens[i].Kind,
                                tokens[i].Start,
                                tokens[i].End
                            )
                        );
                    }
                    Console.WriteLine(String.Join(",\r\n", res));
                }
            }
        }

        #endregion
    }
}

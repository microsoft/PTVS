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
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Django.Project;
using Microsoft.PythonTools.Django.TemplateParsing;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;

namespace DjangoTests {
    [TestClass]
    public class DjangoTemplateParserTests {
        #region Filter parser tests

        [TestMethod, Priority(0)]
        public void FilterRegexTests() {
            var testCases = new[] { 
                new { Got = ("100"), Expected = DjangoVariable.Number("100", 0) },
                new { Got = ("100.0"), Expected = DjangoVariable.Number("100.0", 0) },
                new { Got = ("+100"), Expected = DjangoVariable.Number("+100", 0) },
                new { Got = ("-100"), Expected = DjangoVariable.Number("-100", 0) },
                new { Got = ("'fob'"), Expected = DjangoVariable.Constant("'fob'", 0) },
                new { Got = ("\"fob\""), Expected = DjangoVariable.Constant("\"fob\"", 0) },
                new { Got = ("fob"), Expected = DjangoVariable.Variable("fob", 0) },
                new { Got = ("fob.oar"), Expected = DjangoVariable.Variable("fob.oar", 0) },
                new { Got = ("fob|oar"), Expected = DjangoVariable.Variable("fob", 0, new DjangoFilter("oar", 4)) },
                new { Got = ("fob|oar|baz"), Expected = DjangoVariable.Variable("fob", 0, new DjangoFilter("oar", 4), new DjangoFilter("baz", 8)) },
                new { Got = ("fob|oar:'fob'"), Expected = DjangoVariable.Variable("fob", 0, DjangoFilter.Constant("oar", 4, "'fob'", 8)) },
                new { Got = ("fob|oar:42"), Expected = DjangoVariable.Variable("fob", 0, DjangoFilter.Number("oar", 4, "42", 8)) },
                new { Got = ("fob|oar:\"fob\""), Expected = DjangoVariable.Variable("fob", 0, DjangoFilter.Constant("oar", 4, "\"fob\"", 8)) },
                new { Got = ("fob|oar:100"), Expected = DjangoVariable.Variable("fob", 0, DjangoFilter.Number("oar", 4, "100", 8)) },
                new { Got = ("fob|oar:100.0"), Expected = DjangoVariable.Variable("fob", 0, DjangoFilter.Number("oar", 4, "100.0", 8)) },
                new { Got = ("fob|oar:+100.0"), Expected = DjangoVariable.Variable("fob", 0, DjangoFilter.Number("oar", 4, "+100.0", 8)) },
                new { Got = ("fob|oar:-100.0"), Expected =  DjangoVariable.Variable("fob", 0, DjangoFilter.Number("oar", 4, "-100.0", 8)) },
                new { Got = ("fob|oar:baz.quox"), Expected = DjangoVariable.Variable("fob", 0, DjangoFilter.Variable("oar", 4, "baz.quox", 8)) },
                new { Got = ("fob|oar:baz"), Expected = DjangoVariable.Variable("fob", 0, DjangoFilter.Variable("oar", 4, "baz", 8)) },

                new { Got = ("{{ 100 }}"), Expected = DjangoVariable.Number("100", 3) },
                new { Got = ("{{ 100.0 }}"), Expected = DjangoVariable.Number("100.0", 3) },
                new { Got = ("{{ +100 }}"), Expected = DjangoVariable.Number("+100", 3) },
                new { Got = ("{{ -100 }}"), Expected = DjangoVariable.Number("-100", 3) },
                new { Got = ("{{ 'fob' }}"), Expected = DjangoVariable.Constant("'fob'", 3) },
                new { Got = ("{{ \"fob\" }}"), Expected = DjangoVariable.Constant("\"fob\"", 3) },
                new { Got = ("{{ fob }}"), Expected = DjangoVariable.Variable("fob", 3) },
                new { Got = ("{{ fob.oar }}"), Expected = DjangoVariable.Variable("fob.oar", 3) },
                new { Got = ("{{ fob|oar }}"), Expected = DjangoVariable.Variable("fob", 3, new DjangoFilter("oar", 7)) },                
                new { Got = ("{{ fob|oar|baz }}"), Expected = DjangoVariable.Variable("fob", 3, new DjangoFilter("oar", 7), new DjangoFilter("baz", 11)) },
                new { Got = ("{{ fob|oar:'fob' }}"), Expected = DjangoVariable.Variable("fob", 3, DjangoFilter.Constant("oar", 7, "'fob'", 11)) },
                new { Got = ("{{ fob|oar:42 }}"), Expected = DjangoVariable.Variable("fob", 3, DjangoFilter.Number("oar", 7, "42", 11)) },
                new { Got = ("{{ fob|oar:\"fob\" }}"), Expected = DjangoVariable.Variable("fob", 3, DjangoFilter.Constant("oar", 7, "\"fob\"", 11)) },
                new { Got = ("{{ fob|oar:100 }}"), Expected = DjangoVariable.Variable("fob", 3, DjangoFilter.Number("oar", 7, "100", 11)) },
                new { Got = ("{{ fob|oar:100.0 }}"), Expected = DjangoVariable.Variable("fob", 3, DjangoFilter.Number("oar", 7, "100.0", 11)) },
                new { Got = ("{{ fob|oar:+100.0 }}"), Expected = DjangoVariable.Variable("fob", 3, DjangoFilter.Number("oar", 7, "+100.0", 11)) },
                new { Got = ("{{ fob|oar:-100.0 }}"), Expected =  DjangoVariable.Variable("fob", 3, DjangoFilter.Number("oar", 7, "-100.0", 11)) },
                new { Got = ("{{ fob|oar:baz.quox }}"), Expected = DjangoVariable.Variable("fob", 3, DjangoFilter.Variable("oar", 7, "baz.quox", 11)) },
                new { Got = ("{{ fob|oar:baz }}"), Expected = DjangoVariable.Variable("fob", 3, DjangoFilter.Variable("oar", 7, "baz", 11)) },
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

        [TestMethod, Priority(0)]
        public void BlockParserTests() {
            var testCases = new[] { 
                new { 
                    Got = ("for x in "), 
                    Expected = (DjangoBlock)new DjangoForBlock(new BlockParseInfo("for", "x in ", 0), 6, null, 9, -1, new[] { new Tuple<string, int>("x",  4) }),
                    Context = TestCompletionContext.Simple,
                    Completions = new[] {
                        new { 
                            Position = 9,
                            Expected = new[] { "fob", "oar" }
                        },
                        new { 
                            Position = 4,
                            Expected = new string[0]
                        }
                    }
                },
                new { 
                    Got = ("for x in oar"), 
                    Expected = (DjangoBlock)new DjangoForBlock(new BlockParseInfo("for", "x in oar", 0), 6, DjangoVariable.Variable("oar", 9), 12, -1, new[] { new Tuple<string, int>("x",  4) }),
                    Context = TestCompletionContext.Simple,
                    Completions = new[] {
                        new { 
                            Position = 9,
                            Expected = new[] { "fob", "oar" }
                        },
                        new { 
                            Position = 4,
                            Expected = new string[0]
                        }
                    }
                },
                new { 
                    Got = ("for x in b"), 
                    Expected = (DjangoBlock)new DjangoForBlock(new BlockParseInfo("for", "x in b", 0), 6, DjangoVariable.Variable("b", 9), 10, -1, new[] { new Tuple<string, int>("x",  4) }),
                    Context = TestCompletionContext.Simple,
                    Completions = new[] {
                        new { 
                            Position = 10,
                            Expected = new [] { "fob", "oar" }
                        },
                        new { 
                            Position = 4,
                            Expected = new string[0]
                        }
                    }
                },

                new { 
                    Got = ("autoescape"), 
                    Expected = (DjangoBlock)new DjangoAutoEscapeBlock(new BlockParseInfo("autoescape", "", 0), -1, -1),
                    Context = TestCompletionContext.Simple,
                    Completions = new[] {
                        new { 
                            Position = 10,
                            Expected = new[] { "on", "off" }
                        }
                    }
                },
                new { 
                    Got = ("autoescape on"), 
                    Expected = (DjangoBlock)new DjangoAutoEscapeBlock(new BlockParseInfo("autoescape", " on", 0), 11, 2),
                    Context = TestCompletionContext.Simple,
                    Completions = new[] {
                        new { 
                            Position = 10,
                            Expected = new string[0]
                        }
                    }
                },
                new { 
                    Got = ("comment"), 
                    Expected = (DjangoBlock)new DjangoArgumentlessBlock(new BlockParseInfo("comment", "", 0)),
                    Context = TestCompletionContext.Simple,
                    Completions = new[] {
                        new { 
                            Position = 0,
                            Expected = new string[0]
                        }
                    }
                },
                new { 
                    Got = ("spaceless"), 
                    Expected = (DjangoBlock)new DjangoSpacelessBlock(new BlockParseInfo("spaceless", "", 0)),
                    Context = TestCompletionContext.Simple,
                    Completions = new[] {
                        new { 
                            Position = 0,
                            Expected = new string[0]
                        }
                    }
                },
                new { 
                    Got = ("filter "), 
                    Expected = (DjangoBlock)new DjangoFilterBlock(new BlockParseInfo("filter", " ", 0), DjangoVariable.Variable("", 7)),
                    Context = TestCompletionContext.Simple,
                    Completions = new[] {
                        new { 
                            Position = 7,
                            Expected = new [] { "cut", "lower" }
                        }
                    }
                },
                new { 
                    Got = ("ifequal "), 
                    Expected = (DjangoBlock)new DjangoIfOrIfNotEqualBlock(new BlockParseInfo("ifequal", " ", 0), DjangoVariable.Variable("", 8)),
                    Context = TestCompletionContext.Simple,
                    Completions = new[] {
                        new { 
                            Position = 8,
                            Expected = new [] { "fob", "oar" }
                        }
                    }
                },
                new { 
                    Got = ("ifequal fob "), 
                    Expected = (DjangoBlock)new DjangoIfOrIfNotEqualBlock(new BlockParseInfo("ifequal", " fob ", 0), DjangoVariable.Variable("fob", 8)),
                    Context = TestCompletionContext.Simple,
                    Completions = new[] {
                        new { 
                            Position = 12,
                            Expected = new [] { "fob", "oar" }
                        }
                    }
                },
                new { 
                    Got = ("if "), 
                    Expected = (DjangoBlock)new DjangoIfBlock(new BlockParseInfo("if", " ", 0)),
                    Context = TestCompletionContext.Simple,
                    Completions = new[] {
                        new { 
                            Position = 3,
                            Expected = new [] { "fob", "oar", "not" }
                        }
                    }
                },
                new { 
                    Got = ("if fob "), 
                    Expected = (DjangoBlock)new DjangoIfBlock(new BlockParseInfo("if", " fob ", 0), new BlockClassification(new Span(3, 3), Classification.Identifier)),
                    Context = TestCompletionContext.Simple,
                    Completions = new[] {
                        new { 
                            Position = 7,
                            Expected = new [] { "and", "or" }
                        }
                    }
                },
                new { 
                    Got = ("if fob and "), 
                    Expected = (DjangoBlock)new DjangoIfBlock(new BlockParseInfo("if", " fob and ", 0), new BlockClassification(new Span(3, 3), Classification.Identifier), new BlockClassification(new Span(7, 3), Classification.Keyword)),
                    Context = TestCompletionContext.Simple,
                    Completions = new[] {
                        new { 
                            Position = 11,
                            Expected = new [] { "fob", "oar", "not" }
                        }
                    }
                },
                new { 
                    Got = ("firstof "), 
                    Expected = (DjangoBlock)new DjangoMultiVariableArgumentBlock(new BlockParseInfo("firstof", " ", 0)),
                    Context = TestCompletionContext.Simple,
                    Completions = new[] {
                        new { 
                            Position = 8,
                            Expected = new [] { "fob", "oar" }
                        }
                    }
                },
                new { 
                    Got = ("firstof fob|"), 
                    Expected = (DjangoBlock)new DjangoMultiVariableArgumentBlock(new BlockParseInfo("firstof", " fob|", 0), DjangoVariable.Variable("fob", 8)),
                    Context = TestCompletionContext.Simple,
                    Completions = new[] {
                        new { 
                            Position = 12,
                            Expected = new [] { "cut", "lower" }
                        }
                    }
                },
                new { 
                    Got = ("spaceless "), 
                    Expected = (DjangoBlock)new DjangoSpacelessBlock(new BlockParseInfo("spaceless", " ", 0)),
                    Context = TestCompletionContext.Simple,
                    Completions = new[] {
                        new { 
                            Position = 10,
                            Expected = new string[0]
                        }
                    }
                },
                new { 
                    Got = ("widthratio "), 
                    Expected = (DjangoBlock)new DjangoWidthRatioBlock(new BlockParseInfo("widthratio", " ", 0)),
                    Context = TestCompletionContext.Simple,
                    Completions = new[] {
                        new { 
                            Position = 11,
                            Expected = new [] { "fob", "oar" }
                        }
                    }
                },
                new { 
                    Got = ("templatetag "), 
                    Expected = (DjangoBlock)new DjangoTemplateTagBlock(new BlockParseInfo("templatetag", " ", 0), 11, null),
                    Context = TestCompletionContext.Simple,
                    Completions = new[] {
                        new { 
                            Position = 11,
                            Expected = new [] { "openblock", "closeblock", "openvariable", "closevariable", "openbrace", "closebrace", "opencomment", "closecomment" }
                        }
                    }
                },
                new { 
                    Got = ("templatetag open"), 
                    Expected = (DjangoBlock)new DjangoTemplateTagBlock(new BlockParseInfo("templatetag", " open", 0), 11, null),
                    Context = TestCompletionContext.Simple,
                    Completions = new[] {
                        new { 
                            Position = 15,
                            Expected = new [] { "openblock", "openvariable", "openbrace", "opencomment" }
                        }
                    }
                },
                new { 
                    Got = ("templatetag openblock "), 
                    Expected = (DjangoBlock)new DjangoTemplateTagBlock(new BlockParseInfo("templatetag", " openblock ", 0), 11, "openblock"),
                    Context = TestCompletionContext.Simple,
                    Completions = new[] {
                        new { 
                            Position = 22,
                            Expected = new string[0]
                        }
                    }
                },
            };

            foreach (var testCase in testCases) {
                Console.WriteLine(testCase.Got);

                var got = DjangoBlock.Parse(testCase.Got);

                ValidateBlock(testCase.Expected, got);

                foreach (var completionCase in testCase.Completions) {
                    var completions = new HashSet<string>(got.GetCompletions(testCase.Context, completionCase.Position).Select(x => x.DisplayText));

                    Assert.AreEqual(completionCase.Expected.Length, completions.Count);
                    var expected = new HashSet<string>(completionCase.Expected);
                    foreach (var value in completions) {
                        Assert.IsTrue(expected.Contains(value));
                    }
                }
            }
        }

        private static Dictionary<Type, Action<DjangoBlock, DjangoBlock>> _blockValidators = MakeBlockValidators();

        private static Dictionary<Type, Action<DjangoBlock, DjangoBlock>> MakeBlockValidators() {
            return new Dictionary<Type, Action<DjangoBlock, DjangoBlock>>() {
                { typeof(DjangoForBlock), ValidateForBlock },
                { typeof(DjangoAutoEscapeBlock), ValidateAutoEscape },
                { typeof(DjangoArgumentlessBlock), ValidateArgumentless },
                { typeof(DjangoFilterBlock), ValidateFilter },
                { typeof(DjangoIfOrIfNotEqualBlock), ValidateIfOrIfNotEqualBlock},
                { typeof(DjangoIfBlock), ValidateIfBlock},
                { typeof(DjangoMultiVariableArgumentBlock), ValidateMultiArgumentBlock},
                { typeof(DjangoSpacelessBlock), ValidateSpacelessBlock},
                { typeof(DjangoTemplateTagBlock), ValidateTemplateTagBlock },
                { typeof(DjangoWidthRatioBlock), ValidateWidthRatioBlock }
            };
        }

        private static void ValidateWidthRatioBlock(DjangoBlock expected, DjangoBlock got) {
            var withExpected = (DjangoWidthRatioBlock)expected;
            var withGot = (DjangoWidthRatioBlock)got;

            Assert.AreEqual(withExpected.ParseInfo.Start, withGot.ParseInfo.Start);
            Assert.AreEqual(withExpected.ParseInfo.Command, withGot.ParseInfo.Command);
            Assert.AreEqual(withExpected.ParseInfo.Args, withGot.ParseInfo.Args);
        }

        private static void ValidateTemplateTagBlock(DjangoBlock expected, DjangoBlock got) {
            var tempTagExpected = (DjangoTemplateTagBlock)expected;
            var tempTagGot = (DjangoTemplateTagBlock)got;

            Assert.AreEqual(tempTagExpected.ParseInfo.Start, tempTagGot.ParseInfo.Start);
            Assert.AreEqual(tempTagExpected.ParseInfo.Command, tempTagGot.ParseInfo.Command);
            Assert.AreEqual(tempTagExpected.ParseInfo.Args, tempTagGot.ParseInfo.Args);
        }

        private static void ValidateSpacelessBlock(DjangoBlock expected, DjangoBlock got) {
            var spacelessExpected = (DjangoSpacelessBlock)expected;
            var spacelessGot = (DjangoSpacelessBlock)got;

            Assert.AreEqual(spacelessExpected.ParseInfo.Start, spacelessGot.ParseInfo.Start);
            Assert.AreEqual(spacelessExpected.ParseInfo.Command, spacelessGot.ParseInfo.Command);
            Assert.AreEqual(spacelessExpected.ParseInfo.Args, spacelessGot.ParseInfo.Args);
        }

        private static void ValidateMultiArgumentBlock(DjangoBlock expected, DjangoBlock got) {
            var maExpected = (DjangoMultiVariableArgumentBlock)expected;
            var maGot = (DjangoMultiVariableArgumentBlock)got;

            Assert.AreEqual(maExpected.ParseInfo.Start, maGot.ParseInfo.Start);
            Assert.AreEqual(maExpected.ParseInfo.Command, maGot.ParseInfo.Command);
            Assert.AreEqual(maExpected.ParseInfo.Args, maGot.ParseInfo.Args);
        }

        private static void ValidateIfBlock(DjangoBlock expected, DjangoBlock got) {
            var ifExpected = (DjangoIfBlock)expected;
            var ifGot = (DjangoIfBlock)got;

            Assert.AreEqual(ifExpected.ParseInfo.Start, ifGot.ParseInfo.Start);
            Assert.AreEqual(ifExpected.ParseInfo.Command, ifGot.ParseInfo.Command);
            Assert.AreEqual(ifExpected.ParseInfo.Args, ifGot.ParseInfo.Args);
            Assert.AreEqual(ifExpected.Args.Length, ifGot.Args.Length);
            for (int i = 0; i < ifExpected.Args.Length; i++) {
                Assert.AreEqual(ifExpected.Args[i], ifGot.Args[i]);
            }
        }

        private static void ValidateIfOrIfNotEqualBlock(DjangoBlock expected, DjangoBlock got) {
            var ifExpected = (DjangoIfOrIfNotEqualBlock)expected;
            var ifGot = (DjangoIfOrIfNotEqualBlock)got;

            Assert.AreEqual(ifExpected.ParseInfo.Start, ifGot.ParseInfo.Start);
            Assert.AreEqual(ifExpected.ParseInfo.Command, ifGot.ParseInfo.Command);
            Assert.AreEqual(ifExpected.ParseInfo.Args, ifGot.ParseInfo.Args);
        }

        private static void ValidateFilter(DjangoBlock expected, DjangoBlock got) {
            var filterExpected = (DjangoFilterBlock)expected;
            var filterGot = (DjangoFilterBlock)got;

            Assert.AreEqual(filterExpected.ParseInfo.Start, filterGot.ParseInfo.Start);
            Assert.AreEqual(filterExpected.ParseInfo.Command, filterGot.ParseInfo.Command);
            Assert.AreEqual(filterExpected.ParseInfo.Args, filterGot.ParseInfo.Args);
        }

        private static void ValidateForBlock(DjangoBlock expected, DjangoBlock got) {
            var forExpected = (DjangoForBlock)expected;
            var forGot = (DjangoForBlock)got;

            Assert.AreEqual(forExpected.ParseInfo.Start, forGot.ParseInfo.Start);
            Assert.AreEqual(forExpected.InStart, forGot.InStart);
        }

        private static void ValidateAutoEscape(DjangoBlock expected, DjangoBlock got) {
            var aeExpected = (DjangoAutoEscapeBlock)expected;
            var aeGot = (DjangoAutoEscapeBlock)got;

            Assert.AreEqual(aeExpected.ParseInfo.Start, aeGot.ParseInfo.Start);
            Assert.AreEqual(aeExpected.ParseInfo.Command, aeGot.ParseInfo.Command);
            Assert.AreEqual(aeExpected.ParseInfo.Args, aeGot.ParseInfo.Args);
        }

        private static void ValidateArgumentless(DjangoBlock expected, DjangoBlock got) {
            var aeExpected = (DjangoArgumentlessBlock)expected;
            var aeGot = (DjangoArgumentlessBlock)got;

            Assert.AreEqual(aeExpected.ParseInfo.Start, aeGot.ParseInfo.Start);
            Assert.AreEqual(aeExpected.ParseInfo.Command, aeGot.ParseInfo.Command);
            Assert.AreEqual(aeExpected.ParseInfo.Args, aeGot.ParseInfo.Args);
        }

        private void ValidateBlock(DjangoBlock expected, DjangoBlock got) {
            Assert.AreEqual(expected.GetType(), got.GetType());

            _blockValidators[expected.GetType()](expected, got);
        }

        #endregion

        #region Template tokenizer tests

        [TestMethod, Priority(0)]
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

        [TestMethod, Priority(0)]
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

        [TestMethod, Priority(0)]
        public void SingleTrailingChar() {
            foreach (var code in new[] { "{{fob}}\n", "{{fob}}a" }) {
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

        [TestMethod, Priority(0)]
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


        [TestMethod, Priority(0)]
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

        [TestMethod, Priority(0)]
        public void TestUnclosedVariable() {
            var code = @"<html>
<head><title></title></head>

<body>

{{ content 

</body>
</html>";

            TokenizerTest(code,
                /*unclosed*/true,
                new TemplateToken(TemplateTokenKind.Text, 0, 49),
                new TemplateToken(TemplateTokenKind.Variable, 50, 80, isClosed: false)
            );
        }

        [TestMethod, Priority(0)]
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

        [TestMethod, Priority(0)]
        public void TestUnclosedComment() {
            var code = @"<html>
<head><title></title></head>

<body>

{# content 

</body>
</html>";

            TokenizerTest(code,
                /*unclosed*/true,
                new TemplateToken(TemplateTokenKind.Text, 0, 49),
                new TemplateToken(TemplateTokenKind.Comment, 50, 80, isClosed: false)
            );
        }

        [TestMethod, Priority(0)]
        public void TestUnclosedBlock() {
            var code = @"<html>
<head><title></title></head>

<body>

{% content 

</body>
</html>";

            TokenizerTest(code, 
                /*unclosed*/true, 
                new TemplateToken(TemplateTokenKind.Text, 0, 49),
                new TemplateToken(TemplateTokenKind.Block, 50, 80, isClosed: false)
            );
        }


        private void TokenizerTest(string text, params TemplateTokenResult[] expected) {
            TokenizerTest(text, false, expected);
        }

        private void TokenizerTest(string text, bool unclosed, params TemplateTokenResult[] expected) {
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
                            if (!unclosed) {
                                Assert.AreEqual('}', text[expectedToken.End]);
                            }
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

    class TestCompletionContext : IDjangoCompletionContext {
        private readonly Dictionary<string, HashSet<AnalysisValue>> _variables;
        private readonly Dictionary<string, TagInfo> _filters;
        internal static TestCompletionContext Simple = new TestCompletionContext(new[] { "fob", "oar" }, new[] { "cut", "lower" });

        public TestCompletionContext(string[] variables, string[] filters) {
            _variables = new Dictionary<string, HashSet<AnalysisValue>>();
            _filters = new Dictionary<string, TagInfo>();
            foreach (var variable in variables) {
                _variables[variable] = new HashSet<AnalysisValue>();
            }
            foreach (var filter in filters) {
                _filters[filter] = new TagInfo("", null);
            }
        }

        #region IDjangoCompletionContext Members

        public Dictionary<string, HashSet<AnalysisValue>> Variables {
            get { return _variables; }
        }

        public Dictionary<string, TagInfo> Filters {
            get { return _filters; }
        }

        public IModuleContext ModuleContext {
            get { return null; }
        }

        #endregion
    }
}

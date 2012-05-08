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
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.PythonTools.Django.TemplateParsing;
using System.IO;

namespace AnalysisTest {
    [TestClass]
    public class DjangoTemplateParserTests {
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
            foreach(var code in new[] { "{{foo}}\n", "{{foo}}a" }) {
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
    }
}

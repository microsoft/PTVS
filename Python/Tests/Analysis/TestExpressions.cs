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

namespace AnalysisTests {
    public static class TestExpressions {
        public static IEnumerable<string> Snippets2x {
            get {
                return Expressions.Concat(Statements2x);
            }
        }

        public static readonly string[] Expressions = new[] { 
                // expressions
                "a",
                "a()",
                "a[42]",
                "a + b",
                "+a",
                "-a",
                "a and b",
                "a or b",
                "`foo`",
                "42",
                "'abc'",
                "42 if True else False",
                "{}",
                "[]",
                "[x for x in abc]",
                "(x for x in abc)",
                "lambda x: 2",
                "a.b",
                "(a)",
                "()",
                "(1, 2, 3)",
                "1, 2, 3",
                "yield 42"
        };
        public static readonly string[] Statements2x = new[] { 
                // statements
                "assert True",
                "x = 42",
                "x += 42",
                "break",
                "continue",
                "def f(): pass",
                "class C: pass",
                "del x",
                "pass",
                "exec 'hello'",
                "for i in xrange(42): pass",
                "import foo",
                "from foo import bar",
                "global x",
                "if True: pass",
                "print abc",
                "raise Exception()",
                "return abc",
                "try:\r\n    pass\r\nexcept:\r\n    pass",
                "while True:\r\n    pass",
                "with abc: pass",
                "@property\r\ndef f(): pass",
            };

        public static readonly string[] Statements3x = new[] { "nonlocal foo" };

        public static string IndentCode(string code, string indentation) {
            StringBuilder res = new StringBuilder();
            var lines = code.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            for(int i = 0; i<lines.Length; i++) {
                res.Append(indentation);
                res.Append(lines[i]);
                if (i != lines.Length - 1) {
                    res.Append("\r\n");
                }
            }
            return res.ToString();
        }
    }
}

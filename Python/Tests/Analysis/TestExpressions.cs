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
                "`fob`",
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
                "import fob",
                "from fob import oar",
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

        public static readonly string[] Statements3x = new[] { "nonlocal fob" };

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

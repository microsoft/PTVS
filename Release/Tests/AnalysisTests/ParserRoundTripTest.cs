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
using System.IO;
using System.Text;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnalysisTests {
    /// <summary>
    /// Test cases to verify that the parser successfully preserves all information for round tripping source code.
    /// </summary>
    [TestClass]
    public class ParserRoundTripTest {
        [TestMethod]
        public void TestExpressions() {            
            // TODO: Trailing white space tests
            // Unary Expressions
            TestOneString(PythonLanguageVersion.V27, "x=~42");
            TestOneString(PythonLanguageVersion.V27, "x=-42");
            TestOneString(PythonLanguageVersion.V27, "x=+42");
            TestOneString(PythonLanguageVersion.V27, "x=not 42");
            TestOneString(PythonLanguageVersion.V27, "x  =   ~    42");
            TestOneString(PythonLanguageVersion.V27, "x  =   -    42");
            TestOneString(PythonLanguageVersion.V27, "x  =   +    42");
            TestOneString(PythonLanguageVersion.V27, "x  =   not    42");

            // Constant Expressions
            TestOneString(PythonLanguageVersion.V27, "\r\n42");
            TestOneString(PythonLanguageVersion.V27, "42");
            TestOneString(PythonLanguageVersion.V27, "'abc'");
            TestOneString(PythonLanguageVersion.V27, "\"abc\"");
            TestOneString(PythonLanguageVersion.V27, "'''abc'''");
            TestOneString(PythonLanguageVersion.V27, "\"\"\"abc\"\"\"");
            TestOneString(PythonLanguageVersion.V27, "x = - 1");
            TestOneString(PythonLanguageVersion.V27, "x = -1");
            TestOneString(PythonLanguageVersion.V27, "x = - 2147483648");
            TestOneString(PythonLanguageVersion.V27, "x = -2147483648");

            // Conditional Expressions
            TestOneString(PythonLanguageVersion.V27, "1 if True else 2");
            TestOneString(PythonLanguageVersion.V27, "1  if   True    else     2");

            // Generator expressions
            TestOneString(PythonLanguageVersion.V27, "(x for x in abc)");
            TestOneString(PythonLanguageVersion.V27, "(x for x in abc if abc >= 42)");
            TestOneString(PythonLanguageVersion.V27, " (  x   for    x     in      abc       )");
            TestOneString(PythonLanguageVersion.V27, " (  x   for    x     in      abc       if        abc        >=          42          )");
            TestOneString(PythonLanguageVersion.V27, "f(x for x in abc)");
            TestOneString(PythonLanguageVersion.V27, "f(x for x in abc if abc >= 42)");
            TestOneString(PythonLanguageVersion.V27, "f (  x   for    x     in      abc       )");
            TestOneString(PythonLanguageVersion.V27, "f (  x   for    x     in      abc       if        abc        >=          42          )");
            TestOneString(PythonLanguageVersion.V27, "x(a for a,b in x)");
            TestOneString(PythonLanguageVersion.V27, "x  (   a    for     a      ,       b        in        x          )");

            // Lambda Expressions
            TestOneString(PythonLanguageVersion.V27, "lambda x: x");
            TestOneString(PythonLanguageVersion.V27, "lambda x, y: x, y");
            TestOneString(PythonLanguageVersion.V27, "lambda x = 42: x");
            TestOneString(PythonLanguageVersion.V27, "lambda *x: x");
            TestOneString(PythonLanguageVersion.V27, "lambda **x: x");
            TestOneString(PythonLanguageVersion.V27, "lambda *x, **y: x");
            TestOneString(PythonLanguageVersion.V27, "lambda : 42");
            TestOneString(PythonLanguageVersion.V30, "lambda *, x: x");
            TestOneString(PythonLanguageVersion.V27, "lambda  x   :    x");
            TestOneString(PythonLanguageVersion.V27, "lambda  x   ,    y     :      x       ,        y");
            TestOneString(PythonLanguageVersion.V27, "lambda  x   =    42     :      x");
            TestOneString(PythonLanguageVersion.V27, "lambda  *   x    :     x");
            TestOneString(PythonLanguageVersion.V27, "lambda  **   x    :     x");
            TestOneString(PythonLanguageVersion.V27, "lambda  *   x    ,     **      y       :        x");
            TestOneString(PythonLanguageVersion.V27, "lambda  :   42");
            TestOneString(PythonLanguageVersion.V30, "lambda  *   ,    x     :      x");

            // List Comprehensions
            TestOneString(PythonLanguageVersion.V27, "[x for x in abc]");
            TestOneString(PythonLanguageVersion.V27, "[x for x in abc, baz]");
            TestOneString(PythonLanguageVersion.V27, "[x for x in (abc, baz)]");
            TestOneString(PythonLanguageVersion.V27, "[x for x in abc if abc >= 42]");
            TestOneString(PythonLanguageVersion.V27, " [  x   for    x     in      abc       ]");
            TestOneString(PythonLanguageVersion.V27, " [  x   for    x     in      abc       if        abc        >=          42          ]");
            TestOneString(PythonLanguageVersion.V27, "[v for k,v in x]");
            TestOneString(PythonLanguageVersion.V27, "  [v   for    k     ,      v       in        x         ]");
            TestOneString(PythonLanguageVersion.V27, "[v for (k,v) in x]");
            TestOneString(PythonLanguageVersion.V27, "  [   v    for     (      k       ,        v          )          in           x             ]");

            // Set comprehensions
            TestOneString(PythonLanguageVersion.V27, "{x for x in abc}");
            TestOneString(PythonLanguageVersion.V27, "{x for x in abc if abc >= 42}");
            TestOneString(PythonLanguageVersion.V27, " {  x   for    x     in      abc       }");
            TestOneString(PythonLanguageVersion.V27, " {  x   for    x     in      abc       if        abc        >=          42          }");

            // Dict Comprehensions
            TestOneString(PythonLanguageVersion.V27, "{x:x for x in abc}");
            TestOneString(PythonLanguageVersion.V27, "{x:x for x in abc if abc >= 42}");
            TestOneString(PythonLanguageVersion.V27, " {  x        :         x   for    x     in      abc       }");
            TestOneString(PythonLanguageVersion.V27, " {  x           :            x   for    x     in      abc       if        abc        >=          42          }");

            // Backquote Expression
            TestOneString(PythonLanguageVersion.V27, "`42`");
            TestOneString(PythonLanguageVersion.V27, " `42`");
            TestOneString(PythonLanguageVersion.V27, " `42  `");

            // Call Expression
            TestOneString(PythonLanguageVersion.V27, "x(abc)");
            TestOneString(PythonLanguageVersion.V27, "x(abc = 42)");
            TestOneString(PythonLanguageVersion.V27, "x(*abc)");
            TestOneString(PythonLanguageVersion.V27, "x(**abc)");
            TestOneString(PythonLanguageVersion.V27, "x(*foo, **bar)");
            TestOneString(PythonLanguageVersion.V27, "x(a, b, c)");
            TestOneString(PythonLanguageVersion.V27, "x(a, b, c, d = 42)");
            TestOneString(PythonLanguageVersion.V27, "x (  abc   )");
            TestOneString(PythonLanguageVersion.V27, "x (  abc   =    42     )");
            TestOneString(PythonLanguageVersion.V27, "x (  *   abc    )");
            TestOneString(PythonLanguageVersion.V27, "x (  **   abc     )");
            TestOneString(PythonLanguageVersion.V27, "x (  *   foo    ,     **      bar       )");
            TestOneString(PythonLanguageVersion.V27, "x (  a,   b,    c     )");
            TestOneString(PythonLanguageVersion.V27, "x (  a   ,    b     ,      c       ,        d         =           42           )");
            TestOneString(PythonLanguageVersion.V27, "x(abc,)");
            TestOneString(PythonLanguageVersion.V27, "x  (   abc    ,     )");
            TestOneString(PythonLanguageVersion.V27, "x(abc=42,)");
            TestOneString(PythonLanguageVersion.V27, "x  (   abc    =     42      ,       )");

            // Member Expression
            TestOneString(PythonLanguageVersion.V27, "foo.bar");
            TestOneString(PythonLanguageVersion.V27, "foo .bar");
            TestOneString(PythonLanguageVersion.V27, "foo. bar");
            TestOneString(PythonLanguageVersion.V27, "foo .  bar");
            TestOneString(PythonLanguageVersion.V27, "class C:\r\n    x = foo.__bar");

            // Parenthesis expression
            TestOneString(PythonLanguageVersion.V27, "(42)");
            TestOneString(PythonLanguageVersion.V27, "( 42  )");
            TestOneString(PythonLanguageVersion.V27, " (  42   )");

            // Starred expression
            TestOneString(PythonLanguageVersion.V30, "*a, b = c, d");
            TestOneString(PythonLanguageVersion.V30, "*a, b, c = d, e, f");
            TestOneString(PythonLanguageVersion.V30, "*               a ,  b   ,    c     =      d       ,        e         ,          f");
            TestOneString(PythonLanguageVersion.V30, "(            *               a ,  b   ,    c     )             =      (              d       ,        e         ,          f              )");
            TestOneString(PythonLanguageVersion.V30, "[            *               a ,  b   ,    c     ]             =      [              d       ,        e         ,          f              ]");
            
            // Index expression
            TestOneString(PythonLanguageVersion.V27, "x[42]");
            TestOneString(PythonLanguageVersion.V27, "x[ 42]");
            
            TestOneString(PythonLanguageVersion.V27, "x [42]");
            TestOneString(PythonLanguageVersion.V27, "x [42 ]");
            TestOneString(PythonLanguageVersion.V27, "x [42,23]");
            TestOneString(PythonLanguageVersion.V27, "x[ 42 ]");
            TestOneString(PythonLanguageVersion.V27, "x [  42   ,    23     ]");
            TestOneString(PythonLanguageVersion.V27, "x[42:23]");
            TestOneString(PythonLanguageVersion.V27, "x [  42   :    23     ]");
            TestOneString(PythonLanguageVersion.V27, "x[42:23:100]");
            TestOneString(PythonLanguageVersion.V27, "x [  42   :    23     :      100       ]");
            TestOneString(PythonLanguageVersion.V27, "x[42:]");
            TestOneString(PythonLanguageVersion.V27, "x[42::]");
            TestOneString(PythonLanguageVersion.V27, "x [  42   :    ]");
            TestOneString(PythonLanguageVersion.V27, "x [  42   :    :     ]");
            TestOneString(PythonLanguageVersion.V27, "x[::]");
            TestOneString(PythonLanguageVersion.V27, "x  [   :    :     ]");

            // or expression
            TestOneString(PythonLanguageVersion.V27, "1 or 2");
            TestOneString(PythonLanguageVersion.V27, "1  or   2");

            // and expression
            TestOneString(PythonLanguageVersion.V27, "1 and 2");
            TestOneString(PythonLanguageVersion.V27, "1  and   2");

            // binary expression
            foreach (var op in new[] { "+", "-", "*", "/", "//", "%", "&", "|", "^", "<<", ">>", "**", "<", ">", "<=", ">=", "==", "!=", "<>" }) {
                TestOneString(PythonLanguageVersion.V27, "1 " + op + "2");
                TestOneString(PythonLanguageVersion.V27, "1"+ op + "2");
                TestOneString(PythonLanguageVersion.V27, "1" + op + " 2");
                TestOneString(PythonLanguageVersion.V27, "1 " + op + " 2");
                TestOneString(PythonLanguageVersion.V27, "1  " + op + "   2");
            }

            foreach (var op in new[] { "is", "is not", "in", "not in" }) {
                // TODO: All of these should pass in the binary expression case once we have error handling working
                TestOneString(PythonLanguageVersion.V27, "1 " + op + " 2");
                TestOneString(PythonLanguageVersion.V27, "1  " + op + "   2");
            }

            // yield expression
            TestOneString(PythonLanguageVersion.V27, "yield 1");
            TestOneString(PythonLanguageVersion.V27, "yield 1, 2");
            TestOneString(PythonLanguageVersion.V27, "yield 1  , 2");
            TestOneString(PythonLanguageVersion.V27, "yield 1  , 2,");
            TestOneString(PythonLanguageVersion.V27, "yield 1 ,  2   ,");
            TestOneString(PythonLanguageVersion.V27, "yield");
            TestOneString(PythonLanguageVersion.V27, "yield None");
            TestOneString(PythonLanguageVersion.V27, "yield 1 == 2");
            TestOneString(PythonLanguageVersion.V27, "yield lambda: 42");
            TestOneString(PythonLanguageVersion.V27, "yield 42, ");


            // tuples
            TestOneString(PythonLanguageVersion.V27, "(1, 2, 3)");
            TestOneString(PythonLanguageVersion.V27, "(1, 2,  3)");
            TestOneString(PythonLanguageVersion.V27, "( 1  ,   2    ,     3      )");
            TestOneString(PythonLanguageVersion.V27, "( 1  ,   2    ,     3      ,       )");
            
            // list expressions
            TestOneString(PythonLanguageVersion.V27, "[1, 2, 3]");
            TestOneString(PythonLanguageVersion.V27, "[1, 2,  3]");
            TestOneString(PythonLanguageVersion.V27, "[ 1  ,   2    ,     3      ]");
            TestOneString(PythonLanguageVersion.V27, "[ 1  ,   2    ,     3      ,       ]");
            TestOneString(PythonLanguageVersion.V27, "[abc, foo and bar]");
            TestOneString(PythonLanguageVersion.V27, "[foo if True else bar]");

            // set expressions
            TestOneString(PythonLanguageVersion.V27, "{1, 2, 3}");
            TestOneString(PythonLanguageVersion.V27, "{1, 2,  3}");
            TestOneString(PythonLanguageVersion.V27, "{ 1  ,   2    ,     3      }");
            TestOneString(PythonLanguageVersion.V27, "{ 1  ,   2    ,     3      ,       }");

            // dict expressions
            TestOneString(PythonLanguageVersion.V27, "{1:2, 2 :3, 3: 4}");
            TestOneString(PythonLanguageVersion.V27, "{1 :2, 2  :3,  3:  4}");
            TestOneString(PythonLanguageVersion.V27, "{ 1  :   2    ,     2      :       3,        3         :          4           }");
            TestOneString(PythonLanguageVersion.V27, "{ 1  :   2    ,     2      :       3,        3         :          4           ,            }");

            // Error cases:
            //TestOneString(PythonLanguageVersion.V27, "{1:2, 2 :3, 3: 4]");
        }

        [TestMethod]
        public void TestMangledPrivateName() {
            TestOneString(PythonLanguageVersion.V27, @"class C:
    def f(__a):
        pass
"); 
            TestOneString(PythonLanguageVersion.V27, @"class C:
    class __D:
        pass
");


            TestOneString(PythonLanguageVersion.V27, @"class C:
    import __abc
    import __foo, __bar
");

            TestOneString(PythonLanguageVersion.V27, @"class C:
    from sys import __abc
    from sys import __foo, __bar
    from __sys import __abc
");

            TestOneString(PythonLanguageVersion.V27, @"class C:
    global __X
");

            TestOneString(PythonLanguageVersion.V30, @"class C:
    nonlocal __X
");
        }

        [TestMethod]
        public void TestComments() {

            TestOneString(PythonLanguageVersion.V27, @"x = foo(
        r'abc'                                # comments
        r'def'                                # are spanning across
                                              # a string plus
                                              # which might make life
                                              # difficult if we don't
        r'ghi'                                # handle it properly
        )");

            TestOneString(PythonLanguageVersion.V27, "#foo\r\npass");
            TestOneString(PythonLanguageVersion.V27, "#foo\r\n\r\npass"); 
            TestOneString(PythonLanguageVersion.V27, "#foo");

        }

        [TestMethod]
        public void TestWhiteSpaceAfterDocString() {
            TestOneString(PythonLanguageVersion.V27, @"'''hello

this is some documentation
'''

import foo");
        }

        [TestMethod]
        public void TestMutateStdLib() {
            var versions = new[] { 
                new { Path = "C:\\Python25\\Lib", Version = PythonLanguageVersion.V25 },
                new { Path = "C:\\Python26\\Lib", Version = PythonLanguageVersion.V26 },
                new { Path = "C:\\Python27\\Lib", Version = PythonLanguageVersion.V27 },
                
                new { Path = "C:\\Python30\\Lib", Version = PythonLanguageVersion.V30 },
                new { Path = "C:\\Python31\\Lib", Version = PythonLanguageVersion.V31 },
                new { Path = "C:\\Python32\\Lib", Version = PythonLanguageVersion.V32 } 
            };

            for (int i = 0; i < 100; i++) {
                int seed = (int)DateTime.Now.Ticks;
                var random = new Random(seed);
                Console.WriteLine("Seed == " + seed);

                foreach (var version in versions) {
                    Console.WriteLine("Testing version {0} {1}", version.Version, version.Path);
                    int ran = 0, succeeded = 0;
                    string[] files;
                    try {
                        files = Directory.GetFiles(version.Path);
                    } catch (DirectoryNotFoundException) {
                        continue;
                    }

                    foreach (var file in files) {
                        try {
                            if (file.EndsWith(".py")) {
                                ran++;
                                TestOneFileMutated(file, version.Version, random);
                                succeeded++;
                            }
                        } catch (Exception e) {
                            Console.WriteLine(e);
                            Console.WriteLine("Failed: {0}", file);
                            break;
                        }
                    }

                    Assert.AreEqual(ran, succeeded);
                }
            }
        }

        private static void TestOneFileMutated(string filename, PythonLanguageVersion version, Random random) {
            var originalText = File.ReadAllText(filename);
            int start = random.Next(originalText.Length);
            int end = random.Next(originalText.Length);

            int realStart = Math.Min(start, end);
            int length = Math.Max(start, end) - Math.Min(start, end);
            //Console.WriteLine("Removing {1} chars at {0}", realStart, length);
            originalText = originalText.Substring(realStart, length);

            TestOneString(version, originalText);
        }

        [TestMethod]
        public void TestBinaryFiles() {
            var filename = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.System), "kernel32.dll");
            TestOneString(PythonLanguageVersion.V27, filename);
        }

        [TestMethod]
        public void TestErrors() {
            TestOneString(PythonLanguageVersion.V30, ":   ...");

            // Index Expression
            TestOneString(PythonLanguageVersion.V27, "x[[val, val, ...], [val, val, ...], .");
            TestOneString(PythonLanguageVersion.V27, "x[[val, val, ...], [val, val, ...], ..");

            // Suite Statement
            TestOneString(PythonLanguageVersion.V27, "while X !=2 :\r\n");

            // Lambda Expression

            TestOneString(PythonLanguageVersion.V27, "lambda");
            TestOneString(PythonLanguageVersion.V27, "lambda :");
            TestOneString(PythonLanguageVersion.V27, "lambda pass");
            TestOneString(PythonLanguageVersion.V27, "lambda : pass"); 
            TestOneString(PythonLanguageVersion.V27, "lambda a, b, quote");
            TestOneString(PythonLanguageVersion.V30, "[x for x in abc if lambda a, b, quote");
            TestOneString(PythonLanguageVersion.V27, "lambda, X+Y Z");
            TestOneString(PythonLanguageVersion.V30, "[x for x in abc if lambda, X+Y Z");

            // print statement
            TestOneString(PythonLanguageVersion.V27, "print >>sys.stderr, \\\r\n");
            TestOneString(PythonLanguageVersion.V27, "print pass");
            TestOneString(PythonLanguageVersion.V27, "print >>pass");
            TestOneString(PythonLanguageVersion.V27, "print >>pass, ");
            TestOneString(PythonLanguageVersion.V27, "print >>pass, pass");
            TestOneString(PythonLanguageVersion.V27, "print >>pass pass");

            // Import statement
            TestOneString(PythonLanguageVersion.V27, "import X as");

            // From Import statement
            TestOneString(PythonLanguageVersion.V27, "from _struct import");
            TestOneString(PythonLanguageVersion.V27, "from _io import (DEFAULT_BUFFER_SIZE");
            TestOneString(PythonLanguageVersion.V27, "from x import y as");
            TestOneString(PythonLanguageVersion.V27, "from ... import ...");

            // Parenthesis Expression
            TestOneString(PythonLanguageVersion.V27, "(\r\n(x");
            TestOneString(PythonLanguageVersion.V27, "(\r\n(");            

            TestOneString(PythonLanguageVersion.V27, "m .b'");
            TestOneString(PythonLanguageVersion.V27, "m . b'");
            TestOneString(PythonLanguageVersion.V27, "x y import");
            TestOneString(PythonLanguageVersion.V27, "x y global");

            TestOneString(PythonLanguageVersion.V27, "x[..., ]");

            TestOneString(PythonLanguageVersion.V27, "(a for x y");
            TestOneString(PythonLanguageVersion.V27, "x(a for x y");
            TestOneString(PythonLanguageVersion.V27, "[a for x y");
            TestOneString(PythonLanguageVersion.V27, "{a for x y");
            TestOneString(PythonLanguageVersion.V27, "{a:v for x y");

            TestOneString(PythonLanguageVersion.V27, ":   ");
            TestOneString(PythonLanguageVersion.V27, "from the");
            TestOneString(PythonLanguageVersion.V27, "when not None");
            TestOneString(PythonLanguageVersion.V27, "for x and y");

            // conditional expression
            TestOneString(PythonLanguageVersion.V27, "e if x y z");
            TestOneString(PythonLanguageVersion.V27, "e if x y");
            TestOneString(PythonLanguageVersion.V27, "e if x");
            TestOneString(PythonLanguageVersion.V27, "e if x pass");

            TestOneString(PythonLanguageVersion.V27, ", 'hello'\r\n        self");
            TestOneString(PythonLanguageVersion.V27, "http://xkcd.com/353/\")");
            TestOneString(PythonLanguageVersion.V27, "�g�\r��\r���\r��\r���\r���\r��\rt4�\r*V�\roA�\r\t�\r�$�\r\t.�\r�t�\r�q�\r�H�\r�|");
            TestOneString(PythonLanguageVersion.V27, "\r\t.�\r�t�\r�q�\r");
            TestOneString(PythonLanguageVersion.V27, "\r\t�\r�$�\r\t.�\r");
            TestOneString(PythonLanguageVersion.V27, "�\r�$�\r\t.�\r�t");
            TestOneString(PythonLanguageVersion.V27, "\r\n.\r\n");
            
            TestOneString(PythonLanguageVersion.V27, "abc\r\n.\r\n");

            // Dictionary Expressions
            TestOneString(PythonLanguageVersion.V27, "{");
            TestOneString(PythonLanguageVersion.V27, @"X = { 42 : 100,
");
            TestOneString(PythonLanguageVersion.V27, @"s.
    X = { 23   : 42,
");
            TestOneString(PythonLanguageVersion.V27, "{x:y");
            TestOneString(PythonLanguageVersion.V27, "{x:y, z:x");
            TestOneString(PythonLanguageVersion.V27, "{x");
            TestOneString(PythonLanguageVersion.V27, "{x, y");
            TestOneString(PythonLanguageVersion.V27, "{x:y for x in abc");
            TestOneString(PythonLanguageVersion.V27, "{x for x in abc");
            TestOneString(PythonLanguageVersion.V27, @")
    X = { 42 : 100,
          100 : 200,
");
            TestOneString(PythonLanguageVersion.V27, @"]
    X = { 42 : 100,
          100 : 200,
");
            TestOneString(PythonLanguageVersion.V27, @"}
    X = { 42 : 100,
          100 : 200,
");
            TestOneString(PythonLanguageVersion.V27, @"{ 42: 100, 100 ");
            TestOneString(PythonLanguageVersion.V27, @"{ 42: 100, 100, 200:30 } ");
            TestOneString(PythonLanguageVersion.V27, @"{ 100, 100:30, 200 } ");


            // generator comprehensions and calls
            TestOneString(PythonLanguageVersion.V27, "x(");
            TestOneString(PythonLanguageVersion.V27, "x(for x in abc");
            TestOneString(PythonLanguageVersion.V27, "x(abc");
            TestOneString(PythonLanguageVersion.V27, "x(abc, ");
            TestOneString(PythonLanguageVersion.V27, "x(pass");

            // lists and list comprehensions
            TestOneString(PythonLanguageVersion.V27, "[");
            TestOneString(PythonLanguageVersion.V27, "[abc");
            TestOneString(PythonLanguageVersion.V27, "[abc,");
            TestOneString(PythonLanguageVersion.V27, "[for x in abc");
            TestOneString(PythonLanguageVersion.V27, "[b for b in");

            TestOneString(PythonLanguageVersion.V27, "x[");
            TestOneString(PythonLanguageVersion.V27, "x[abc");
            TestOneString(PythonLanguageVersion.V27, "x[abc,");
            TestOneString(PythonLanguageVersion.V27, "x[abc:");

            // backquote expression
            TestOneString(PythonLanguageVersion.V27, "`foo");

            // constant expressions
            TestOneString(PythonLanguageVersion.V27, "'\r");
            TestOneString(PythonLanguageVersion.V27, @"'abc' 24 : q");
            TestOneString(PythonLanguageVersion.V27, @"u'abc' 24 : q");

            // bad tokens
            TestOneString(PythonLanguageVersion.V27, "!x");
            TestOneString(PythonLanguageVersion.V27, "$aü");
            TestOneString(PythonLanguageVersion.V27, "0399");
            TestOneString(PythonLanguageVersion.V27, "0o399");
            TestOneString(PythonLanguageVersion.V27, "0399L");
            TestOneString(PythonLanguageVersion.V27, "0399j");
            
            // calls
            TestOneString(PythonLanguageVersion.V27, "x(42 = 42)");

            // for statement
            TestOneString(PythonLanguageVersion.V27, "for pass\r\nin abc: pass");
            TestOneString(PythonLanguageVersion.V27, "for pass in abc: pass");
            TestOneString(PythonLanguageVersion.V27, "def f():\r\nabc");
            TestOneString(PythonLanguageVersion.V27, "for pass in");

            // class defs
            TestOneString(PythonLanguageVersion.V30, "class(object: pass");
            TestOneString(PythonLanguageVersion.V30, "class X(object: pass");
            TestOneString(PythonLanguageVersion.V30, "class X(object, int: pass");
            TestOneString(PythonLanguageVersion.V30, "class X(object, pass");
            TestOneString(PythonLanguageVersion.V30, "class X(=");
            TestOneString(PythonLanguageVersion.V30, "class X(pass");

            TestOneString(PythonLanguageVersion.V27, "class(object: pass");
            TestOneString(PythonLanguageVersion.V27, "class X(object: pass");
            TestOneString(PythonLanguageVersion.V27, "class X(object, int: pass");
            TestOneString(PythonLanguageVersion.V27, "class X(object, pass");
            TestOneString(PythonLanguageVersion.V27, "class X(=");
            TestOneString(PythonLanguageVersion.V27, "class X(pass");

            TestOneString(PythonLanguageVersion.V27, "class C:\r\n    x = foo.42");
            TestOneString(PythonLanguageVersion.V27, "class C:\r\n    @foo.42\r\n    def f(self): pass");
            TestOneString(PythonLanguageVersion.V27, "class C:\r\n    @foo.[]\r\n    def f(self): pass");
            TestOneString(PythonLanguageVersion.V27, "class 42");
            TestOneString(PythonLanguageVersion.V30, "class");
            TestOneString(PythonLanguageVersion.V27, "@foo\r\nclass 42");

            // func defs
            TestOneString(PythonLanguageVersion.V30, "def f(A, *, *x");
            TestOneString(PythonLanguageVersion.V30, "def f(A, *, **x");
            TestOneString(PythonLanguageVersion.V30, "def f(A, *, x = 2");
            TestOneString(PythonLanguageVersion.V30, "def f(A, *");
            TestOneString(PythonLanguageVersion.V30, "def f(A, *, (a, b)");
            TestOneString(PythonLanguageVersion.V30, "def f(A, *,");
            TestOneString(PythonLanguageVersion.V30, "def f(A, *)");

            TestOneString(PythonLanguageVersion.V27, "def f(x, *, ): pass");
            TestOneString(PythonLanguageVersion.V27, "def f((42 + 2: pass");
            TestOneString(PythonLanguageVersion.V27, "def f((42: pass");
            TestOneString(PythonLanguageVersion.V27, "def f((42)): pass");
            TestOneString(PythonLanguageVersion.V27, "def f((42, )): pass");
            TestOneString(PythonLanguageVersion.V27, "def f((42): pass");
            TestOneString(PythonLanguageVersion.V27, "def f((42 pass");
            TestOneString(PythonLanguageVersion.V27, "def f((a, 42)): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(42 = 42): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(42 = pass): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(pass = pass): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(= = =): pass");
            TestOneString(PythonLanguageVersion.V27, "def f");
            TestOneString(PythonLanguageVersion.V27, "def");
            TestOneString(PythonLanguageVersion.V27, " @@");
            TestOneString(PythonLanguageVersion.V27, "def X(abc, **");
            TestOneString(PythonLanguageVersion.V27, "def X(abc, *");
            TestOneString(PythonLanguageVersion.V27, @"@foo(
def f(): pass");


            // misc malformed expressions
            TestOneString(PythonLanguageVersion.V27, "1 + :");
            TestOneString(PythonLanguageVersion.V27, "abc.2");
            TestOneString(PythonLanguageVersion.V27, "abc 1L");
            TestOneString(PythonLanguageVersion.V27, "abc 0j");
            TestOneString(PythonLanguageVersion.V27, "abc.2.3");
            TestOneString(PythonLanguageVersion.V27, "abc 1L 2L");
            TestOneString(PythonLanguageVersion.V27, "abc 0j 1j");

            // global / nonlocal statements
            TestOneString(PythonLanguageVersion.V27, "global abc, baz,"); // trailing comma not allowed
            TestOneString(PythonLanguageVersion.V27, "nonlocal abc");           // nonlocal not supported before 3.0
            TestOneString(PythonLanguageVersion.V30, "nonlocal abc, baz,"); // trailing comma not allowed

            // assert statements
            TestOneString(PythonLanguageVersion.V27, "assert");

            // while statements
            TestOneString(PythonLanguageVersion.V27, "while True:\r\n    break\r\nelse:\r\npass");

            // if statements
            TestOneString(PythonLanguageVersion.V27, "if True:\r\n    pass\r\nelif False:\r\n    pass\r\n    else:\r\n    pass");

            // try/except
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept: pass\r\nelse: pass\r\nexcept Exception: pass");
            TestOneString(PythonLanguageVersion.V27, "try  :   pass\r\finally    :     pass");
        }

        [TestMethod]
        public void TestExplicitLineJoin() {
            TestOneString(PythonLanguageVersion.V27, @"foo(4 + \
                    5)");
        }

        [TestMethod]
        public void TestStatements() {
            // TODO: Vary all of these tests by putting the test case in a function def
            // TODO: Vary all of these tests by adding trailing comments                        
            TestOneString(PythonLanguageVersion.V27, "def _process_result(self, (i");

            // Empty Statement
            TestOneString(PythonLanguageVersion.V27, "pass");
            
            // Break Statement
            TestOneString(PythonLanguageVersion.V27, "break");
            
            // Continue Statement
            TestOneString(PythonLanguageVersion.V27, "continue");

            // Non local statement
            TestOneString(PythonLanguageVersion.V30, "nonlocal abc");
            TestOneString(PythonLanguageVersion.V30, "nonlocal abc, baz");
            TestOneString(PythonLanguageVersion.V30, "nonlocal  abc   ,    baz");

            // Global Statement
            TestOneString(PythonLanguageVersion.V27, "global abc");
            TestOneString(PythonLanguageVersion.V27, "global abc, baz");
            TestOneString(PythonLanguageVersion.V27, "global  abc   ,    baz");

            // Return Statement
            TestOneString(PythonLanguageVersion.V27, "return");
            TestOneString(PythonLanguageVersion.V27, "return 42");
            TestOneString(PythonLanguageVersion.V27, "return 42,");
            TestOneString(PythonLanguageVersion.V27, "return 42,43");
            TestOneString(PythonLanguageVersion.V27, "return  42   ,    43");

            // Del Statement
            TestOneString(PythonLanguageVersion.V27, "del");
            TestOneString(PythonLanguageVersion.V27, "del abc");
            TestOneString(PythonLanguageVersion.V27, "del abc,");
            TestOneString(PythonLanguageVersion.V27, "del abc,baz");
            TestOneString(PythonLanguageVersion.V27, "del  abc   ,    baz     ,");

            // Raise Statement
            TestOneString(PythonLanguageVersion.V27, "raise");
            TestOneString(PythonLanguageVersion.V27, "raise foo");
            TestOneString(PythonLanguageVersion.V27, "raise foo, bar");
            TestOneString(PythonLanguageVersion.V27, "raise foo, bar, baz");
            TestOneString(PythonLanguageVersion.V30, "raise foo from bar");
            TestOneString(PythonLanguageVersion.V27, "raise  foo");
            TestOneString(PythonLanguageVersion.V27, "raise  foo   ,    bar");
            TestOneString(PythonLanguageVersion.V27, "raise  foo   ,    bar     ,      baz");
            TestOneString(PythonLanguageVersion.V30, "raise  foo   from    bar");

            // Assert Statement
            TestOneString(PythonLanguageVersion.V27, "assert foo");
            TestOneString(PythonLanguageVersion.V27, "assert foo, bar");
            TestOneString(PythonLanguageVersion.V27, "assert  foo");
            TestOneString(PythonLanguageVersion.V27, "assert  foo   ,    bar");

            // Import Statement
            TestOneString(PythonLanguageVersion.V27, "import sys");
            TestOneString(PythonLanguageVersion.V27, "import sys as foo");
            TestOneString(PythonLanguageVersion.V27, "import sys as foo, itertools");
            TestOneString(PythonLanguageVersion.V27, "import sys as foo, itertools as i");
            TestOneString(PythonLanguageVersion.V27, "import  sys");
            TestOneString(PythonLanguageVersion.V27, "import  sys   as    foo");
            TestOneString(PythonLanguageVersion.V27, "import  sys   as    foo     ,       itertools");
            TestOneString(PythonLanguageVersion.V27, "import  sys   as    foo     ,       itertools       as        i");
            TestOneString(PythonLanguageVersion.V27, "import X, Y, Z, A as B");

            // From Import Statement
            TestOneString(PythonLanguageVersion.V27, "from sys import *");
            TestOneString(PythonLanguageVersion.V27, "from sys import winver");
            TestOneString(PythonLanguageVersion.V27, "from sys import winver as wv");
            TestOneString(PythonLanguageVersion.V27, "from sys import winver as wv, stdin as si");
            TestOneString(PythonLanguageVersion.V27, "from sys import (winver)");
            TestOneString(PythonLanguageVersion.V27, "from sys import (winver,)");
            TestOneString(PythonLanguageVersion.V27, "from  sys   import    *");
            TestOneString(PythonLanguageVersion.V27, "from  sys   import    winver");
            TestOneString(PythonLanguageVersion.V27, "from  sys   import    winver     as      wv");
            TestOneString(PythonLanguageVersion.V27, "from  sys   import    winver     as      wv       ,        stdin         as           si");
            TestOneString(PythonLanguageVersion.V27, "from  sys   import    (     winver      )");
            TestOneString(PythonLanguageVersion.V27, "from  sys   import    (     winver       as       wv        )");
            TestOneString(PythonLanguageVersion.V27, "from  sys   import    (     winver       as       wv        ,         stdin          as          si           )");
            TestOneString(PythonLanguageVersion.V27, "from  sys   import    (     winver       ,        )");
            TestOneString(PythonLanguageVersion.V27, "from xyz import A, B, C, D, E");


            // Assignment statement
            TestOneString(PythonLanguageVersion.V27, "x = 42");
            TestOneString(PythonLanguageVersion.V27, "x  =   42");
            TestOneString(PythonLanguageVersion.V27, "x = abc = 42");
            TestOneString(PythonLanguageVersion.V27, "x  =   abc    =     42");
            TestOneString(PythonLanguageVersion.V30, "def f():\r\n     a = True");

            // Augmented Assignment Statement
            foreach (var op in new[] { "+", "-", "*", "/", "//", "%", "&", "|", "^", "<<", ">>", "**"}) {
                TestOneString(PythonLanguageVersion.V27, "x " + op + "= 42");
                TestOneString(PythonLanguageVersion.V27, "x  " + op + "   42");
            }

            // Exec Statement
            TestOneString(PythonLanguageVersion.V27, "exec 'abc'");
            TestOneString(PythonLanguageVersion.V27, "exec 'abc' in l");
            TestOneString(PythonLanguageVersion.V27, "exec 'abc' in l, g");
            TestOneString(PythonLanguageVersion.V27, "exec  'abc'");
            TestOneString(PythonLanguageVersion.V27, "exec  'abc'   in    l");
            TestOneString(PythonLanguageVersion.V27, "exec  'abc'   in    l     ,      g");

            // Print Statement
            TestOneString(PythonLanguageVersion.V27, "print foo");
            TestOneString(PythonLanguageVersion.V27, "print foo, bar");
            TestOneString(PythonLanguageVersion.V27, "print foo,");
            TestOneString(PythonLanguageVersion.V27, "print foo, bar,"); 
            TestOneString(PythonLanguageVersion.V27, "print >> dest");
            TestOneString(PythonLanguageVersion.V27, "print >> dest, foo");
            TestOneString(PythonLanguageVersion.V27, "print >> dest, foo, bar");
            TestOneString(PythonLanguageVersion.V27, "print >> dest, foo,");
            TestOneString(PythonLanguageVersion.V27, "print >> dest, foo, bar,");
            TestOneString(PythonLanguageVersion.V27, "print  foo");
            TestOneString(PythonLanguageVersion.V27, "print  foo   ,    bar");
            TestOneString(PythonLanguageVersion.V27, "print  foo   ,");
            TestOneString(PythonLanguageVersion.V27, "print  foo   ,    bar     ,");
            TestOneString(PythonLanguageVersion.V27, "print  >>   dest");
            TestOneString(PythonLanguageVersion.V27, "print  >>   dest    ,     foo");
            TestOneString(PythonLanguageVersion.V27, "print  >>   dest    ,     foo      ,       bar");
            TestOneString(PythonLanguageVersion.V27, "print  >>   dest    ,     foo      ,");
            TestOneString(PythonLanguageVersion.V27, "print  >>   dest    ,     foo      ,       bar        ,");
            TestOneString(PythonLanguageVersion.V27, "print l1==l");


            // For Statement
            TestOneString(PythonLanguageVersion.V27, "for i in xrange(10): pass");
            TestOneString(PythonLanguageVersion.V27, "for i in xrange(10):\r\n    pass\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "for i in xrange(10):\r\n\r\n    pass\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "for i in xrange(10):\r\n    break\r\nelse:\r\n    pass");
            
            TestOneString(PythonLanguageVersion.V27, "for (i), (j) in x.items(): print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for (i, j) in x.items(): print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for i,j in xrange(10): pass");
            TestOneString(PythonLanguageVersion.V27, "for ((i, j)) in x.items(): print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for (((i), (j))) in x.items(): print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for [i, j] in x.items(): print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for [[i], [j]] in x.items(): print(i, j)");

            TestOneString(PythonLanguageVersion.V27, "for  i   in    xrange(10)    :      pass");
            TestOneString(PythonLanguageVersion.V27, "for  i   in    xrange(10)    :\r\n    pass \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "for  i   in    xrange(10)    :\r\n\r\n    pass \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "for  i   in    xrange(10)    :\r\n    break\r\nelse     :      \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "for  (i), (j)   in    x.items()     :      print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for  (i, j)   in    x.items()     :      print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for  i,j    in    xrange(10)     :      pass");
            TestOneString(PythonLanguageVersion.V27, "for  ((i, j))   in    x.items()     :       print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for  (((i), (j)))   in    x.items()     :      print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for  [i, j]   in    x.items()     :      print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for  [[i], [j]]   in    x.items()     :      print(i, j)");
            
            // While Statement
            TestOneString(PythonLanguageVersion.V27, "while True: break");
            TestOneString(PythonLanguageVersion.V27, "while True: break\r\nelse: pass");
            TestOneString(PythonLanguageVersion.V27, "while True:\r\n    break\r\nelse:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "while  True   :    break");
            TestOneString(PythonLanguageVersion.V27, "while  True   :    break\r\nelse     : pass");
            TestOneString(PythonLanguageVersion.V27, "while  True:\r\n    break   \r\nelse    :     \r\n    pass");

            // If Statement
            TestOneString(PythonLanguageVersion.V27, "if True: pass");
            TestOneString(PythonLanguageVersion.V27, "if True:\r\n    pass\r\nelse:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "if True:\r\n    pass\r\nelif False:\r\n    pass\r\nelse:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "if  True   :    pass");
            TestOneString(PythonLanguageVersion.V27, "if  True   :\r\n    pass\r\nelse    :     \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "if  True   :\r\n    pass\r\nelif     False     :\r\n    pass      \r\nelse       :        \r\n    pass");

            // Suite Statement
            TestOneString(PythonLanguageVersion.V27, "abc;foo;bar");
            TestOneString(PythonLanguageVersion.V27, "abc  ;   foo    ;     bar");
            TestOneString(PythonLanguageVersion.V27, "abc;foo\r\n\r\nbar;baz");
            TestOneString(PythonLanguageVersion.V27, "abc  ;   foo    \r\n\r\nbar     ;      baz");
            TestOneString(PythonLanguageVersion.V27, "foo;");
            TestOneString(PythonLanguageVersion.V27, "def f():\r\n    if True:\r\n        foo;\r\n     bar");
            TestOneString(PythonLanguageVersion.V27, @"def f(x):
    length = x
    if length == 0:
        pass
");
            TestOneString(PythonLanguageVersion.V27, @"def f():
    try:
        return 42
    except Exception:
        pass");

            // With Statement
            TestOneString(PythonLanguageVersion.V27, "with abc: pass");
            TestOneString(PythonLanguageVersion.V27, "with abc as bar: pass");
            TestOneString(PythonLanguageVersion.V27, "with foo, bar: pass");
            TestOneString(PythonLanguageVersion.V27, "with foo as f, bar as b: pass");
            TestOneString(PythonLanguageVersion.V27, "with  abc   : pass");
            TestOneString(PythonLanguageVersion.V27, "with  abc   as    bar     :      pass");
            TestOneString(PythonLanguageVersion.V27, "with  foo   ,    bar     :      pass");
            TestOneString(PythonLanguageVersion.V27, "with  foo   as    f     ,       bar       as       b        :          pass");
            TestOneString(PythonLanguageVersion.V27, "with abc: pass");
            TestOneString(PythonLanguageVersion.V27, "with abc as bar:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "with foo, bar:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "with foo as f, bar as b:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "with  abc   :\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "with  abc   as    bar     :  \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "with  foo   ,    bar     :  \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "with  foo   as    f     ,       bar       as       b        :  \r\n    pass");
            
            // Try Statement
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept Exception: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept Exception, e: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept Exception as e: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept TypeError: pass\r\nexcept Exception: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept TypeError, e: pass\r\nexcept Exception: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept TypeError as e: pass\r\nexcept Exception: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept: pass\r\nelse: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept Exception: pass\r\nelse: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept Exception, e: pass\r\nelse: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept Exception as e: pass\r\nelse: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept: pass\r\nfinally: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept Exception: pass\r\nfinally: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept Exception, e: pass\r\nfinally: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept Exception as e: pass\r\nfinally: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept: pass\r\nelse: pass\r\nfinally: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept Exception: pass\r\nelse: pass\r\nfinally: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept Exception, e: pass\r\nelse: pass\r\nfinally: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept Exception as e: pass\r\nelse: pass\r\nfinally: pass");

            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept Exception:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept Exception, e:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept Exception as e:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept TypeError:\r\n    pass\r\nexcept Exception:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept TypeError, e:\r\n    pass\r\nexcept Exception:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept TypeError as e:\r\n    pass\r\nexcept Exception:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept:\r\n    pass\r\nelse:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept Exception:\r\n    pass\r\nelse:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept Exception, e:\r\n    pass\r\nelse:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept Exception as e:\r\n    pass\r\nelse:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept: pass\r\nfinally:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept Exception:\r\n    pass\r\nfinally:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept Exception, e:\r\n    pass\r\nfinally:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept Exception as e:\r\n    pass\r\nfinally:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept: pass\r\nelse:\r\n    pass\r\nfinally:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept Exception:\r\n    pass\r\nelse: pass\r\nfinally:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept Exception, e:\r\n    pass\r\nelse:\r\n    pass\r\nfinally:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept Exception as e:\r\n    pass\r\nelse:\r\n    pass\r\nfinally:\r\n    pass");

            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   :    pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   Exception    :     pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   Exception    ,     e      :        pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   Exception    as    e      :        pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   TypeError    :     pass      \r\nexcept        Exception        :          pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   TypeError    ,     e        :          pass\r\nexcept           Exception            :            pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   TypeError    as    e        :          pass\r\nexcept           Exception             :              pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   :    pass     \r\nelse      :         pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   Exception    :      pass     \r\nelse       :        pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   Exception    ,     e      :       pass        \r\nelse         :              pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   Exception    as     e      :       pass        \r\nelse         :              pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   :    pass     \r\nfinally      :       pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   Exception    :      pass       \r\nfinally       :        pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   Exception    ,      e       :       pass\r\nfinally         :          pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   Exception    as      e       :       pass\r\nfinally         :          pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   :    pass\r\nelse     :      pass       \r\nfinally        :        pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   Exception    :     pass      \r\nelse       :        pass         \r\nfinally          :           pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   Exception    ,    e:     pass      \r\nelse       :       pass         \r\nfinally          :          pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   Exception    as    e:     pass      \r\nelse       :       pass         \r\nfinally          :          pass");

            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept      :\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       Exception       :\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       Exception       ,        e         :          \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       Exception       as        e          :\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       TypeError       :        \r\n    pass\r\nexcept Exception:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       TypeError       ,        e         :\r\n    pass\r\nexcept Exception:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       TypeError       as        e         :\r\n    pass\r\nexcept Exception:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept      :\r\n    pass    \r\nelse        :\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       Exception       :        \r\n    pass\r\nelse        :         \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       Exception       ,         e         :\r\n    pass\r\nelse          :\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       Exception       as       e        :\r\n    pass\r\nelse          :          \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept      : pass      \r\nfinally       :         \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       Exception      :       \r\n    pass\r\nfinally          :\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       Exception       ,        e          :\r\n    pass\r\nfinally:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       Exception       as        e         :\r\n    pass\r\nfinally           :            \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept      :        pass        \r\nelse          :          \r\n    pass\r\nfinally           :           \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       Exception       :\r\n    pass\r\nelse        :         pass\r\nfinally          :           \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       Exception       ,       e         :\r\n    pass\r\nelse           :             \r\n    pass\r\nfinally             :               \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       Exception       as       e        :\r\n    pass\r\nelse           :\r\n    pass\r\nfinally          :              \r\n    pass");

            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nfinally: pass");
            TestOneString(PythonLanguageVersion.V27, "try  :   pass\r\nfinally    :     pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nfinally:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try  :   \r\n    pass\r\nfinally    :     \r\n    pass");

            // Class Definition
            TestOneString(PythonLanguageVersion.V27, "class C: pass");
            TestOneString(PythonLanguageVersion.V27, "class C(): pass");
            TestOneString(PythonLanguageVersion.V27, "class C(object): pass");
            TestOneString(PythonLanguageVersion.V27, "class C(object, ): pass");
            TestOneString(PythonLanguageVersion.V30, "class C(object, metaclass=42): pass");
            TestOneString(PythonLanguageVersion.V30, "class C(*foo): pass");
            TestOneString(PythonLanguageVersion.V30, "class C(*foo, ): pass");
            TestOneString(PythonLanguageVersion.V30, "class C(*foo, **bar): pass");
            TestOneString(PythonLanguageVersion.V30, "class C(*foo, **bar, ): pass");
            TestOneString(PythonLanguageVersion.V30, "class C(**foo): pass");
            TestOneString(PythonLanguageVersion.V30, "class C(**foo, ): pass"); 
            TestOneString(PythonLanguageVersion.V30, "class C(foo = bar): pass");
            TestOneString(PythonLanguageVersion.V30, "class C(foo = bar, baz = 42): pass");

            TestOneString(PythonLanguageVersion.V27, "class  C   :    pass");
            TestOneString(PythonLanguageVersion.V27, "class  C   (    )     :      pass");
            TestOneString(PythonLanguageVersion.V27, "class  C   (    object): pass");
            TestOneString(PythonLanguageVersion.V27, "class  C   (    object      ,       )      : pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    object      ,       metaclass        =         42          )           : pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    *     foo      )       :         pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    *     foo      ,       )        :        pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    *     foo      ,       **        bar         )          :           pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    *     foo      ,      **        bar         ,          )           :            pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    **     foo      )       :         pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    **     foo      ,       )        :         pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    foo     =      bar       )        :         pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    foo     =      bar       ,        baz         =          42           )           :             pass");

            TestOneString(PythonLanguageVersion.V27, "class C: \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "class C(): \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "class C(object): \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "class C(object, ): \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class C(object, metaclass=42): \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class C(*foo): \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class C(*foo, ): \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class C(*foo, **bar): \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class C(*foo, **bar, ): \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class C(**foo): \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class C(**foo, ): \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class C(foo = bar): \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class C(foo = bar, baz = 42): \r\n    pass");

            TestOneString(PythonLanguageVersion.V27, "class  C   :    \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "class  C   (    )     :      \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "class  C   (    object): \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "class  C   (    object      ,       )      : \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    object      ,       metaclass        =         42          )           : \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    *     foo      )       :         \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    *     foo      ,       )        :        \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    *     foo      ,       **        bar         )          :           \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    *     foo      ,      **        bar         ,          )           :            \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    **     foo      )       :         \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    **     foo      ,       )        :         \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    foo     =      bar       )        :         \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    foo     =      bar       ,        baz         =          42           )           :             \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "class Foo(int if y else object):\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "class  Foo   (    int     if      y      else       object         )         :\r\n    pass");

            TestOneString(PythonLanguageVersion.V27, "@foo\r\nclass C: pass");
            TestOneString(PythonLanguageVersion.V27, "@  foo   \r\nclass    C     :       pass");

            // Function Definition
            TestOneString(PythonLanguageVersion.V27, "def f(): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a = 42): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a, b): pass");
            TestOneString(PythonLanguageVersion.V30, "def f(a, b) -> foo: pass");
            TestOneString(PythonLanguageVersion.V27, "def f(*a, **b): pass");
            TestOneString(PythonLanguageVersion.V27, "def  f   (    )     :       pass");
            TestOneString(PythonLanguageVersion.V27, "def  f   (    a     )      :        pass");
            TestOneString(PythonLanguageVersion.V27, "def  f   (    a     =       42        )          :           pass");
            TestOneString(PythonLanguageVersion.V27, "def  f   (    a     ,       b          )          :           pass");
            TestOneString(PythonLanguageVersion.V30, "def  f   (    a     ,       b        )         ->          foo           :            pass");
            TestOneString(PythonLanguageVersion.V27, "def  f   (    *     a      ,        **        b         )          :           pass");
            TestOneString(PythonLanguageVersion.V27, "@foo\r\ndef f(): pass");
            TestOneString(PythonLanguageVersion.V27, "@foo.bar\r\ndef f(): pass");
            TestOneString(PythonLanguageVersion.V27, "@foo(2)\r\ndef f(): pass");
            TestOneString(PythonLanguageVersion.V27, "@foo.bar(2)\r\ndef f(): pass");
            TestOneString(PythonLanguageVersion.V27, "@  foo   \r\ndef f(): pass");
            TestOneString(PythonLanguageVersion.V27, "@  foo   .    bar\r\ndef f(): pass");
            TestOneString(PythonLanguageVersion.V27, "@  foo   (    2     )\r\ndef f(): pass");
            TestOneString(PythonLanguageVersion.V27, "@  foo   .    bar     (      2       )\r\ndef f(): pass");
            TestOneString(PythonLanguageVersion.V27, "def f((a)): pass");
            TestOneString(PythonLanguageVersion.V27, "def  f   (    (     a         )      )       :        pass");
            TestOneString(PythonLanguageVersion.V27, "def f((a, b)): pass");
            TestOneString(PythonLanguageVersion.V27, "def  f   (    (     a         ,      b)       )         :           pass");
            TestOneString(PythonLanguageVersion.V27, "def f((a, (b, c))): pass");
            TestOneString(PythonLanguageVersion.V27, "def  f   (    (     a      ,       (         b          ,          c            )             )              )              :                pass");

            TestOneString(PythonLanguageVersion.V27, "@foo\r\n\r\ndef f(): pass");

            TestOneString(PythonLanguageVersion.V27, "class C:\r\n    @foo.__bar\r\n    def f(self): pass");

            TestOneString(PythonLanguageVersion.V27, "class C:\r\n    def __f(self): pass");

            TestOneString(PythonLanguageVersion.V27, "def f(a,): pass");
            TestOneString(PythonLanguageVersion.V27, "def  f(   a    ,     )      :       pass");

            TestOneString(PythonLanguageVersion.V27, "class C:\r\n    @property\r\n    def foo(self): return 42");

        }

        [TestMethod]
        public void StdLibTest() {
            var versions = new[] { 
                new { Path = "C:\\Python25\\Lib", Version = PythonLanguageVersion.V25 },
                new { Path = "C:\\Python26\\Lib", Version = PythonLanguageVersion.V26 },
                new { Path = "C:\\Python27\\Lib", Version = PythonLanguageVersion.V27 },
                
                new { Path = "C:\\Python30\\Lib", Version = PythonLanguageVersion.V30 },
                new { Path = "C:\\Python31\\Lib", Version = PythonLanguageVersion.V31 },
                new { Path = "C:\\Python32\\Lib", Version = PythonLanguageVersion.V32 } 
            };

            foreach (var version in versions) {
                Console.WriteLine("Testing version {0} {1}", version.Version, version.Path);
                int ran = 0, succeeded = 0;
                string[] files;
                try {
                    files = Directory.GetFiles(version.Path);
                } catch (DirectoryNotFoundException) {
                    continue;
                }

                foreach (var file in files) {
                    try {
                        if (file.EndsWith(".py")) {
                            ran++;
                            TestOneFile(file, version.Version);
                            succeeded++;
                        }
                    } catch (Exception e) {
                        Console.WriteLine(e);
                        Console.WriteLine("Failed: {0}", file);
                        break;
                    }
                }

                Assert.AreEqual(ran, succeeded);
            }
        }

        private static void TestOneFile(string filename, PythonLanguageVersion version) {
            var originalText = File.ReadAllText(filename);

            TestOneString(version, originalText);
        }

        private static void TestOneString(PythonLanguageVersion version, string originalText, bool recurse = true) {
            var parser = Parser.CreateParser(new StringReader(originalText), version, new ParserOptions() { Verbatim = true });
            var ast = parser.ParseFile();

            string output;
            try {
                output = ast.ToCodeString(ast);
            } catch {
                Console.WriteLine("Failed to convert to code: {0}", originalText);
                Assert.Fail();
                return;
            }

            const int contextSize = 50;
            for (int i = 0; i < originalText.Length && i < output.Length; i++) {
                if (originalText[i] != output[i]) {
                    // output some context
                    StringBuilder x = new StringBuilder();
                    StringBuilder y = new StringBuilder();
                    StringBuilder z = new StringBuilder();
                    for (int j = Math.Max(0, i - contextSize); j < Math.Min(Math.Max(originalText.Length, output.Length), i + contextSize); j++) {
                        if (j < originalText.Length) {
                            x.AppendRepr(originalText[j]);
                        }
                        if (j < output.Length) {
                            y.AppendRepr(output[j]);
                        }
                        if (j == i) {
                            z.Append("^");
                        } else {
                            z.Append(" ");
                        }
                    }

                    Console.WriteLine("Mismatch context at {0}:", i);
                    Console.WriteLine("Original: {0}", x.ToString());
                    Console.WriteLine("New     : {0}", y.ToString());
                    Console.WriteLine("Differs : {0}", z.ToString());

                    if (recurse) {
                        // Try and automatically get a minimal repro...
                        try {
                            for (int j = i; j >= 0; j--) {
                                TestOneString(version, originalText.Substring(j), false);
                            }
                        } catch {
                        }
                    } else {
                        Console.WriteLine("-----");
                        Console.WriteLine(originalText);
                        Console.WriteLine("-----");
                    }

                    Assert.AreEqual(originalText[i], output[i], String.Format("Characters differ at {0}, got {1}, expected {2}", i, output[i], originalText[i]));
                }
            }

            if (originalText.Length != output.Length) {
                Console.WriteLine("Original: {0}", originalText.ToString());
                Console.WriteLine("New     : {0}", output.ToString());
            }
            Assert.AreEqual(originalText.Length, output.Length);
        }        
    }
}

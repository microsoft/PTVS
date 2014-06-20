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
using System.Reflection;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Refactoring;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudioTools;
using TestUtilities;
using TestUtilities.Mocks;
using TestUtilities.Python;

namespace PythonToolsTests {
    [TestClass]
    public class RefactorRenameTests {
        private const string ErrorModuleName = "Cannot rename a module name";

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        [TestMethod, Priority(0)]
        public void PrivateMemberMangling() {
            RefactorTest("fob", "__f",
                new[] { 
                    new FileInput(
@"class C:
    def __f(self):
        pass

    def g(self):
        self.__f()

    def x(self):
        self._C__f()
    ",
@"class C:
    def fob(self):
        pass

    def g(self):
        self.fob()

    def x(self):
        self.fob()
    "

                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("def __f(self):"),
                    new ExpectedPreviewItem("self.__f()"),
                    new ExpectedPreviewItem("self._C__f()")
                )
            );

            RefactorTest("fob", "_C__f",
                new[] { 
                    new FileInput(
@"class C:
    def __f(self):
        pass

    def g(self):
        self.__f()

    def x(self):
        self._C__f()
    ",
@"class C:
    def fob(self):
        pass

    def g(self):
        self.fob()

    def x(self):
        self.fob()
    "

                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("def __f(self):"),
                    new ExpectedPreviewItem("self.__f()"),
                    new ExpectedPreviewItem("self._C__f()")
                )
            );


            RefactorTest("__y", "__f",
                new[] { 
                    new FileInput(
@"class C:
    def __f(self):
        pass

    def g(self):
        self.__f()

    def x(self):
        self._C__f()
    ",
@"class C:
    def __y(self):
        pass

    def g(self):
        self.__y()

    def x(self):
        self._C__y()
    "

                    )
                },
                false,
                null,
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("def __f(self):"),
                    new ExpectedPreviewItem("self.__f()"),
                    new ExpectedPreviewItem("self._C__f()")
                )
            );

            RefactorTest("__longname", "__f",
                new[] { 
                    new FileInput(
@"class C:
    def __f(self):
        pass

    def g(self):
        self.__f()

    def x(self):
        self._C__f()
    ",
@"class C:
    def __longname(self):
        pass

    def g(self):
        self.__longname()

    def x(self):
        self._C__longname()
    "

                    )
                },
                false,
                null,
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("def __f(self):"),
                    new ExpectedPreviewItem("self.__f()"),
                    new ExpectedPreviewItem("self._C__f()")
                )
            );

            RefactorTest("__f", "__longname",
                new[] { 
                    new FileInput(
@"class C:
    def __longname(self):
        pass

    def g(self):
        self.__longname()

    def x(self):
        self._C__longname()
    ",
@"class C:
    def __f(self):
        pass

    def g(self):
        self.__f()

    def x(self):
        self._C__f()
    "

                    )
                },
                false,
                null,
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("def __longname(self):"),
                    new ExpectedPreviewItem("self.__longname()"),
                    new ExpectedPreviewItem("self._C__longname()")
                )
            );
        }

        [TestMethod, Priority(0)]
        public void SanityClassField() {
            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
@"class C:
    abc = 100
    def __getitem__(self, key): pass
def d(*p): pass

a = C()
a.abc
a .abc
a. abc
a.abc; a.abc
b = a
b.abc
a[a.abc]
a[ a.abc]
a[a.abc,]
a[a.abc ,]
a[a.abc,a.abc]
a[a.abc, a.abc]
a[a.abc ,a.abc]
a[a.abc , a.abc]
a[a.abc,a.abc,]
a[a.abc, a.abc, ]
a[a.abc ,a.abc ,]
a[a.abc , a.abc,  ]
a[(a.abc)]
a[(a.abc), a.abc]
a[(a.abc, a.abc)]
a[a.abc, (a.abc)]
a[(a.abc,)]
a[(a.abc,), a.abc]
a[(a.abc, a.abc,)]
a[a.abc, (a.abc,)]
a[ a . abc ]
d(a.abc)
d((a.abc))
d((a.abc,))
d((a.abc),)
d(a.abc,a.abc)
d (a.abc)
d(a . abc, a. abc, a. \
abc, a\
.abc)",
@"class C:
    fob = 100
    def __getitem__(self, key): pass
def d(*p): pass

a = C()
a.fob
a .fob
a. fob
a.fob; a.fob
b = a
b.fob
a[a.fob]
a[ a.fob]
a[a.fob,]
a[a.fob ,]
a[a.fob,a.fob]
a[a.fob, a.fob]
a[a.fob ,a.fob]
a[a.fob , a.fob]
a[a.fob,a.fob,]
a[a.fob, a.fob, ]
a[a.fob ,a.fob ,]
a[a.fob , a.fob,  ]
a[(a.fob)]
a[(a.fob), a.fob]
a[(a.fob, a.fob)]
a[a.fob, (a.fob)]
a[(a.fob,)]
a[(a.fob,), a.fob]
a[(a.fob, a.fob,)]
a[a.fob, (a.fob,)]
a[ a . fob ]
d(a.fob)
d((a.fob))
d((a.fob,))
d((a.fob),)
d(a.fob,a.fob)
d (a.fob)
d(a . fob, a. fob, a. \
fob, a\
.fob)"
                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("abc = 100"),
                    new ExpectedPreviewItem("a.abc"),
                    new ExpectedPreviewItem("a .abc"),
                    new ExpectedPreviewItem("a. abc"),
                    new ExpectedPreviewItem("a.abc; a.abc"),
                    new ExpectedPreviewItem("a.abc; a.abc"),
                    new ExpectedPreviewItem("b.abc"),
                    new ExpectedPreviewItem("a[a.abc]"),
                    new ExpectedPreviewItem("a[ a.abc]"),
                    new ExpectedPreviewItem("a[a.abc,]"),
                    new ExpectedPreviewItem("a[a.abc ,]"),
                    new ExpectedPreviewItem("a[a.abc,a.abc]"),
                    new ExpectedPreviewItem("a[a.abc,a.abc]"),
                    new ExpectedPreviewItem("a[a.abc, a.abc]"),
                    new ExpectedPreviewItem("a[a.abc, a.abc]"),
                    new ExpectedPreviewItem("a[a.abc ,a.abc]"),
                    new ExpectedPreviewItem("a[a.abc ,a.abc]"),
                    new ExpectedPreviewItem("a[a.abc , a.abc]"),
                    new ExpectedPreviewItem("a[a.abc , a.abc]"),
                    new ExpectedPreviewItem("a[a.abc,a.abc,]"),
                    new ExpectedPreviewItem("a[a.abc,a.abc,]"),
                    new ExpectedPreviewItem("a[a.abc, a.abc, ]"),
                    new ExpectedPreviewItem("a[a.abc, a.abc, ]"),
                    new ExpectedPreviewItem("a[a.abc ,a.abc ,]"),
                    new ExpectedPreviewItem("a[a.abc ,a.abc ,]"),
                    new ExpectedPreviewItem("a[a.abc , a.abc,  ]"),
                    new ExpectedPreviewItem("a[a.abc , a.abc,  ]"),
                    new ExpectedPreviewItem("a[(a.abc)]"),
                    new ExpectedPreviewItem("a[(a.abc), a.abc]"),
                    new ExpectedPreviewItem("a[(a.abc), a.abc]"),
                    new ExpectedPreviewItem("a[(a.abc, a.abc)]"),
                    new ExpectedPreviewItem("a[(a.abc, a.abc)]"),
                    new ExpectedPreviewItem("a[a.abc, (a.abc)]"),
                    new ExpectedPreviewItem("a[a.abc, (a.abc)]"),
                    new ExpectedPreviewItem("a[(a.abc,)]"),
                    new ExpectedPreviewItem("a[(a.abc,), a.abc]"),
                    new ExpectedPreviewItem("a[(a.abc,), a.abc]"),
                    new ExpectedPreviewItem("a[(a.abc, a.abc,)]"),
                    new ExpectedPreviewItem("a[(a.abc, a.abc,)]"),
                    new ExpectedPreviewItem("a[a.abc, (a.abc,)]"),
                    new ExpectedPreviewItem("a[a.abc, (a.abc,)]"),
                    new ExpectedPreviewItem("a[ a . abc ]"),
                    new ExpectedPreviewItem("d(a.abc)"),
                    new ExpectedPreviewItem("d((a.abc))"),
                    new ExpectedPreviewItem("d((a.abc,))"),
                    new ExpectedPreviewItem("d((a.abc),)"),
                    new ExpectedPreviewItem("d(a.abc,a.abc)"),
                    new ExpectedPreviewItem("d(a.abc,a.abc)"),
                    new ExpectedPreviewItem("d (a.abc)"),
                    new ExpectedPreviewItem("d(a . abc, a. abc, a. \\"),
                    new ExpectedPreviewItem("d(a . abc, a. abc, a. \\"),
                    new ExpectedPreviewItem("abc, a\\"),
                    new ExpectedPreviewItem(".abc)"))
            );

            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
@"class C1:
    abc = 100
class C2:
    abc = 150

A1 = C1
A2 = C2
a1 = C1()
a2 = C2()
A1.abc
A2.abc
a1.abc
a2.abc
abc = 200
",
@"class C1:
    fob = 100
class C2:
    abc = 150

A1 = C1
A2 = C2
a1 = C1()
a2 = C2()
A1.fob
A2.abc
a1.fob
a2.abc
abc = 200
"
                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("abc = 100"),
                    new ExpectedPreviewItem("A1.abc"),
                    new ExpectedPreviewItem("a1.abc")
                )
            );
        }
        
        [TestMethod, Priority(0)]
        public void InheritedClassField() {

            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
@"class C1:
    abc = 100
class C2(C1):
    pass

A1 = C1
A2 = C2
a1 = C1()
a2 = C2()
A1.abc
A2.abc
a1.abc
a2.abc
abc = 200
",
@"class C1:
    fob = 100
class C2(C1):
    pass

A1 = C1
A2 = C2
a1 = C1()
a2 = C2()
A1.fob
A2.fob
a1.fob
a2.fob
abc = 200
"
                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("abc = 100"),
                    new ExpectedPreviewItem("A1.abc"),
                    new ExpectedPreviewItem("A2.abc"),
                    new ExpectedPreviewItem("a1.abc"),
                    new ExpectedPreviewItem("a2.abc")
                )
            );
        }

        [TestMethod, Priority(0)]
        public void RenameGeneratorVariable() {
            // http://pytools.codeplex.com/workitem/454
            RefactorTest("fob", "abc",
                new[] { new FileInput("abc = 100; x = (abc for abc in range(abc))", "fob = 100; x = (abc for abc in range(fob))") }
            );
            RefactorTest("fob", "abc for",
                new[] { new FileInput("abc = 100; x = (abc for abc in range(abc))", "abc = 100; x = (fob for fob in range(abc))") }
            );
            RefactorTest("fob", "abc for",
                new[] { new FileInput("abc = 100; x = (abc for abc in range(abc) for xyz in abc)", "abc = 100; x = (fob for fob in range(abc) for xyz in fob)") }
            );
        }

        [TestMethod, Priority(0)]
        public void TypelessForVariable() {
            RefactorTest("baz", "abc",
                new[] { 
                    new FileInput(
@"def f():
    l = fob
    for abc in l:
        l.append(abc)",
@"def f():
    l = fob
    for baz in l:
        l.append(baz)"
                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void TupleForVariable() {
            RefactorTest("baz", "abc",
                new[] { 
                    new FileInput(
@"def f():
    l = fob
    for abc, de_f in l:
        l.append(abc)",
@"def f():
    l = fob
    for baz, de_f in l:
        l.append(baz)"
                    )
                }
            );

            RefactorTest("baz", "abc",
                new[] { 
                    new FileInput(
@"def f():
    l = fob
    for (abc, de_f) in l:
        l.append(abc)",
@"def f():
    l = fob
    for (baz, de_f) in l:
        l.append(baz)"
                    )
                }
            );

            RefactorTest("baz", "abc",
                new[] { 
                    new FileInput(
@"def f():
    l = fob
    for (abc,), de_f in l:
        l.append(abc)",
@"def f():
    l = fob
    for (baz,), de_f in l:
        l.append(baz)"
                    )
                }
            );

            RefactorTest("baz", "de_f",
                new[] { 
                    new FileInput(
@"def f():
    l = fob
    for abc, de_f in l:
        l.append(de_f)",
@"def f():
    l = fob
    for abc, baz in l:
        l.append(baz)"
                    )
                }
            );

            RefactorTest("baz", "de_f",
                new[] { 
                    new FileInput(
@"def f():
    l = fob
    for (abc, de_f) in l:
        l.append(de_f)",
@"def f():
    l = fob
    for (abc, baz) in l:
        l.append(baz)"
                    )
                }
            );

            RefactorTest("baz", "de_f",
                new[] { 
                    new FileInput(
@"def f():
    l = fob
    for abc, (de_f,) in l:
        l.append(de_f)",
@"def f():
    l = fob
    for abc, (baz,) in l:
        l.append(baz)"
                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanityInstanceField() {
            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
@"class C:
    def __init__(self):
        self.abc = 100
    abc = 200

a = C()
a.abc
",
@"class C:
    def __init__(self):
        self.fob = 100
    fob = 200

a = C()
a.fob
"
                    )
                }
            );
            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
@"class C:
    def make(self):
        self.abc = 100

a = C()
a.abc
",
@"class C:
    def make(self):
        self.fob = 100

a = C()
a.fob
"
                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanityParameter() {
            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
@"def f(abc):
    print abc, abc(), abc[i], x[abc], y(abc)
    abc, _ = 100, abc
",
@"def f(fob):
    print fob, fob(), fob[i], x[fob], y(fob)
    fob, _ = 100, fob
"
                    )
                }
            );

            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
@"def f(abc):
    print abc
    abc = 100

abc = 200
",
@"def f(fob):
    print fob
    fob = 100

abc = 200
"
                    )
                }
            );

            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
@"def f(*abc):
    print abc

abc = 200
",
@"def f(*fob):
    print fob

abc = 200
"
                    )
                }
            );

            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
@"def f(**abc):
    print abc

abc = 200
",
@"def f(**fob):
    print fob

abc = 200
"
                    )
                }
            );

            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
@"def f(*p, **abc):
    print abc

abc = 200
",
@"def f(*p, **fob):
    print fob

abc = 200
"
                    )
                }
            );

            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
@"def f(abc=None):
    print abc

abc = 200
",
@"def f(fob=None):
    print fob

abc = 200
"
                    )
                }
            );

            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
@"def f(i=abc):
    print abc

abc = 200
",
@"def f(i=fob):
    print fob

fob = 200
"
                    )
                }
            );

            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
@"def f(abc=abc):
    print abc

abc = 200
",
@"def f(fob=abc):
    print fob

abc = 200
"
                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanityLocal() {
            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
@"def f():
    abc = 100
    print abc
",
@"def f():
    fob = 100
    print fob
"
                    )
                }
            );

            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
@"def f():
    abc = 100
    print abc

abc = 200
",
@"def f():
    fob = 100
    print fob

abc = 200
"
                    )
                }
            );
            
            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
@"def f():
    abc = 100
    print abc
def g():
    abc = 100
def h(abc):
    pass
",
@"def f():
    fob = 100
    print fob
def g():
    abc = 100
def h(abc):
    pass
"
                    )
                }
            );

        }

        [TestMethod, Priority(0)]
        public void SanityClosure() {
            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
@"def f():
    abc = 100
    def g():
        print abc
",
@"def f():
    fob = 100
    def g():
        print fob
"
                    )
                }
            );

            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
@"def f():
    abc = 100
    def g():
        print abc

abc = 200
",
@"def f():
    fob = 100
    def g():
        print fob

abc = 200
"
                    )
                }
            );

            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
@"def f(abc):
    def g(abc):
        print abc
    abc = 100
    g(abc)
",
@"def f(fob):
    def g(abc):
        print abc
    fob = 100
    g(fob)
"
                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanityLambda() {
            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
@"def f():
    abc = 100
    g = lambda: abc
",
@"def f():
    fob = 100
    g = lambda: fob
"
                    )
                }
            );

            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
@"def f():
    abc = 100
    g = lambda: abc

abc = 200
",
@"def f():
    fob = 100
    g = lambda: fob

abc = 200
"
                    )
                }
            );

            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
@"def f(abc):
    g = lambda abc: abc
    abc = 100
    g(abc)
",
@"def f(fob):
    g = lambda abc: abc
    fob = 100
    g(fob)
"
                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanityInlineIf() {
            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
                        "abc = 0; x = True if abc else False",
                        "fob = 0; x = True if fob else False"
                    )
                }
            );

            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
                        "abc = 0; x = abc if abc else False",
                        "fob = 0; x = fob if fob else False"
                    )
                }
            );

            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
                        "abc = 0; x = True if abc else abc",
                        "fob = 0; x = True if fob else fob"
                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanityGenerator() {
            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
                        "x = (abc for abc in range(100))",
                        "x = (fob for fob in range(100))"
                    )
                }
            );

            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
                        "abc = 100; x = (abc for abc in range(abc))",
                        "fob = 100; x = (abc for abc in range(fob))"
                    )
                }
            );

            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
                        "x = (i[abc] for abc in range(100))",
                        "x = (i[fob] for fob in range(100))"
                    )
                }
            );

            RefactorTest("fob", "abc", version: new Version(3, 2),
                inputs: new[] { 
                    new FileInput(
                        "x = {abc : i[abc] for abc in range(100)}",
                        "x = {fob : i[fob] for fob in range(100)}"
                    )
                }
            );

            RefactorTest("fob", "abc", version: new Version(3, 2),
                inputs: new[] { 
                    new FileInput(
                        "x = {i : abc for i, abc in enumerate(range(100))}",
                        "x = {i : fob for i, fob in enumerate(range(100))}"
                    )
                }
            );

            RefactorTest("fob", "abc", version: new Version(3, 2),
                inputs: new[] { 
                    new FileInput(
                        "x = {abc for abc in range(100)}",
                        "x = {fob for fob in range(100)}"
                    )
                }
            );

            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
                        "abc = [1, 2, 3]; x = (abc for i in abc)",
                        "fob = [1, 2, 3]; x = (fob for i in fob)"
                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanityGeneratorFilter() {
            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
                        "abc = 10; x = (i for i in range(100) if abc > i)",
                        "fob = 10; x = (i for i in range(100) if fob > i)"
                    )
                }
            );

            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
                        "abc = 10; x = (i for i in range(100) if i or abc)",
                        "fob = 10; x = (i for i in range(100) if i or fob)"
                    )
                }
            );

            RefactorTest("fob", "abc", version: new Version(3, 2),
                inputs: new[] { 
                    new FileInput(
                        "x = {abc : i[abc] for abc in range(100)}",
                        "x = {fob : i[fob] for fob in range(100)}"
                    )
                }
            );

            RefactorTest("fob", "abc", version: new Version(3, 2),
                inputs: new[] { 
                    new FileInput(
                        "x = {i : abc for i, abc in enumerate(range(100))}",
                        "x = {i : fob for i, fob in enumerate(range(100))}"
                    )
                }
            );

            RefactorTest("fob", "abc", version: new Version(3, 2),
                inputs: new[] { 
                    new FileInput(
                        "x = {abc for abc in range(100)}",
                        "x = {fob for fob in range(100)}"
                    )
                }
            );

            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
                        "abc = [1, 2, 3]; x = (abc for i in abc)",
                        "fob = [1, 2, 3]; x = (fob for i in fob)"
                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanitySlices() {
            RefactorTest("fob", "abc", version: new Version(3, 2),
            inputs: new[] { 
                    new FileInput(
@"
abc = 100
x = y[:abc]
x = y[abc:]
x = y[abc:abc+1]
x = y[abc-1:abc]
x = y[abc-1:abc:abc+1]
",
@"
fob = 100
x = y[:fob]
x = y[fob:]
x = y[fob:fob+1]
x = y[fob-1:fob]
x = y[fob-1:fob:fob+1]
"                    )
                }
            );


        }

        [TestMethod, Priority(0)]
        public void SanityGlobal() {
            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
@"def f():
    global abc
    assert abc
    print(abc)

abc = 100
",
@"def f():
    global fob
    assert fob
    print(fob)

fob = 100
"
                    )
                }
            );

            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
@"def f():
    global abc; assert abc
    print(abc)

abc = 100
",
@"def f():
    global fob; assert fob
    print(fob)

fob = 100
"
                    )
                }
            );

            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
@"abc = { }
def f():
    print abc[0]
",
@"fob = { }
def f():
    print fob[0]
"
                    )
                }
            );

            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
@"def f():
    global abc
    print abc

def g():
    abc = 200

abc = 100
",
@"def f():
    global fob
    print fob

def g():
    abc = 200

fob = 100
"
                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanityNonLocal() {
            RefactorTest("fob", "abc", version: new Version(3, 2),
            inputs: new[] { 
                    new FileInput(
@"
def g():
    abc = 100
    def f():
        nonlocal abc
        assert abc
        print(abc)

abc = 100
",
@"
def g():
    fob = 100
    def f():
        nonlocal fob
        assert fob
        print(fob)

abc = 100
"                    )
                }
            );

            RefactorTest("fob", "abc", version: new Version(3, 2),
            inputs: new[] { 
                    new FileInput(
@"
def g():
    abc = 100
    def f():
        nonlocal abc;assert abc
        print(abc)

abc = 100
",
@"
def g():
    fob = 100
    def f():
        nonlocal fob;assert fob
        print(fob)

abc = 100
"                    )
                }
            );

            RefactorTest("fob", "abc", version: new Version(3, 2),
            inputs: new[] { 
                    new FileInput(
@"
def g():
    abc = 100
    def f():
        nonlocal  abc
        print(abc)

abc = 100
",
@"
def g():
    fob = 100
    def f():
        nonlocal  fob
        print(fob)

abc = 100
"                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanityRenameClass() {
            RefactorTest("fob", "abc",
            new[] { 
                    new FileInput(
@"
4

class abc:
    pass

x = abc()
y = { abc: 'abc', 'abc': abc }",
@"
4

class fob:
    pass

x = fob()
y = { fob: 'abc', 'abc': fob }"
                    )

                }
            );

            RefactorTest("fob", "abc", 
            new[] { 
                    new FileInput(
@"
4

class abc(object):
    A = 10

x = abc()
abc.A
y = { abc: 'abc', 'abc': abc, abc.A: 'A', 'A': abc.A }",
@"
4

class fob(object):
    A = 10

x = fob()
fob.A
y = { fob: 'abc', 'abc': fob, fob.A: 'A', 'A': fob.A }"
                    )

                }
            );

            RefactorTest("fob", "abc",
            new[] { 
                    new FileInput(
@"
4

class abc(type):
    A = 10

x = abc()
abc.A",
@"
4

class fob(type):
    A = 10

x = fob()
fob.A"
                    )
                }
            );

            RefactorTest("fob", "abc",
            new[] { 
                    new FileInput(
@"
4

class abc(object):
    A = 10

x = abc()
abc.A",
@"
4

class fob(object):
    A = 10

x = fob()
fob.A"
                    )

                }
            );

            RefactorTest("fob", "abc",
            new[] { 
                    new FileInput(
@"class abc(object):
    pass
class x(abc):
    pass
",
@"class fob(object):
    pass
class x(fob):
    pass
"
                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void RenameMetaclass() {
            RefactorTest("fob", "abc", version: new Version(2, 7),
            inputs: new[] { 
                    new FileInput(
@"class abc(type):
    pass
class x(object):
    __metaclass__ = abc
",
@"class fob(type):
    pass
class x(object):
    __metaclass__ = fob
"
                    )
                }
            );

            RefactorTest("fob", "abc", version: new Version(3, 2),
            inputs: new[] { 
                    new FileInput(
@"class abc(type):
    pass
class x(metaclass=abc):
    pass
",
@"class fob(type):
    pass
class x(metaclass=fob):
    pass
"
                    )
                }
            );

        }

        [TestMethod, Priority(0)]
        public void SanityRenameFunction() {
            RefactorTest("fob", "abc",
            new[] { 
                    new FileInput(
@"
4

def abc(x):
    pass

x = abc()
doc = abc.__doc__
fdoc = abc.func_doc
y = { abc: 'abc', 'abc': abc }",
@"
4

def fob(x):
    pass

x = fob()
doc = fob.__doc__
fdoc = fob.func_doc
y = { fob: 'abc', 'abc': fob }"
                    )

                }
            );

            RefactorTest("fob", "abc",
            new[] { 
                    new FileInput(
@"
4

def  abc(x):
    pass

x = abc()
doc = abc.__doc__
fdoc = abc.func_doc",
@"
4

def  fob(x):
    pass

x = fob()
doc = fob.__doc__
fdoc = fob.func_doc"
                    )

                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanityDelLocal() {
            RefactorTest("fob", "abc",
            new[] { 
                    new FileInput(
@"
def f():
    abc = 100
    del abc",
@"
def f():
    fob = 100
    del fob"                    
                    )

                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanityDelInstanceMember() {
            RefactorTest("fob", "abc",
            new[] { 
                    new FileInput(
@"
class C(object):
    def __init__(self):
        self.abc = 42

def f():    
    del C().abc
    c = C()
    del c.abc
    c.abc = 100",
@"
class C(object):
    def __init__(self):
        self.fob = 42

def f():    
    del C().fob
    c = C()
    del c.fob
    c.fob = 100"
                    )

                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanityDelClassMember() {
            RefactorTest("fob", "abc",
            new[] { 
                    new FileInput(
@"
class C(object):
    abc = 42
    del abc

def f():    
    del C().abc
    del C.abc
",
@"
class C(object):
    fob = 42
    del fob

def f():    
    del C().fob
    del C.fob
"
                    )

                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanityDelGlobal() {
            RefactorTest("fob", "abc",
            new[] { 
                    new FileInput(
@"
def f():
    global abc
    del abc

abc = 100
",
@"
def f():
    global fob
    del fob

fob = 100
"
                    )

                }
            );

            RefactorTest("fob", "abc",
            new[] { 
                    new FileInput(
@"
def f():
    global abc;pass
    del f, abc

abc = 100
",
@"
def f():
    global fob;pass
    del f, fob

fob = 100
"
                    )

                }
            );
        }

        [TestMethod, Priority(0)]
        public void DelNonLocal() {
            RefactorTest("fob", "abc", version: new Version(3, 2),
            inputs: new[] { 
                    new FileInput(
@"
def g():
    abc = 100
    def f():
        nonlocal abc
        del abc

abc = 100
",
@"
def g():
    fob = 100
    def f():
        nonlocal fob
        del fob

abc = 100
"                    )
                }
            );

        }

        [TestMethod, Priority(0)]
        public void Decorators() {
            RefactorTest("abc", "fob",
            new[] { 
                    new FileInput(
@"
def fob():
    pass

@fob
def f():
    pass
",
@"
def abc():
    pass

@abc
def f():
    pass
"                    )
                }
            );

            RefactorTest("abc", "fob",
            new[] { 
                    new FileInput(
@"
def fob():
    pass

@fob
@property
@fob
@fob
def f():
    pass
",
@"
def abc():
    pass

@abc
@property
@abc
@abc
def f():
    pass
"                    )
                }
            );

            RefactorTest("abc", "fob",
            new[] { 
                    new FileInput(
@"
def fob(name):
    pass

@fob('name')
def f():
    pass
",
@"
def abc(name):
    pass

@abc('name')
def f():
    pass
"                    )
                }
            );

            RefactorTest("abc", "fob",
            new[] { 
                    new FileInput(
@"
fob = 'fob'

def dec(name):
    pass

@dec(fob)
def f():
    pass
",
@"
abc = 'fob'

def dec(name):
    pass

@dec(abc)
def f():
    pass
"                    )
                }
            );

            RefactorTest("abc", "fob",
            new[] { 
                    new FileInput(
@"
class C:
    @staticmethod
    def fob():
        pass

@C.fob
def f():
    pass
",
@"
class C:
    @staticmethod
    def abc():
        pass

@C.abc
def f():
    pass
"                    )
                }
            );

        }

        [TestMethod, Priority(0)]
        public void TryStatement() {
            RefactorTest("abc", "fob", 
            new[] { 
                    new FileInput(
@"
try:
    pass
except Exception, fob:
    print(fob)
",
@"
try:
    pass
except Exception, abc:
    print(abc)
"                    )
                }
            );

            RefactorTest("abc", "fob", version: new Version(3, 2),
            inputs: new[] { 
                    new FileInput(
@"
try:
    pass
except Exception as fob:
    print(fob)
",
@"
try:
    pass
except Exception as abc:
    print(abc)
"                    )
                }
            );

        }

        [TestMethod, Priority(0)]
        public void FinallyStatement() {
            RefactorTest("fob", "abc",
            new[] { 
                    new FileInput(
@"
try:
    abc = 123
except:
    pass
finally:
    del abc
",
@"
try:
    fob = 123
except:
    pass
finally:
    del fob
"                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void RaiseStatement() {
            RefactorTest("fob", "abc", version: new Version(2, 7),
            inputs: new[] { 
                    new FileInput(
@"
abc = 'a message: abc'
raise Exception(abc)
raise Exception, abc, abc
",
@"
fob = 'a message: abc'
raise Exception(fob)
raise Exception, fob, fob
"                    )
                }
            );

            RefactorTest("fob", "abc", version: new Version(3, 2),
            inputs: new[] { 
                    new FileInput(
@"
abc = 'a message: abc'
raise Exception(abc)
raise Exception() from abc
",
@"
fob = 'a message: abc'
raise Exception(fob)
raise Exception() from fob
"                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void ExecStatement() {
            RefactorTest("fob", "abc", version: new Version(2, 7),
            inputs: new[] { 
                    new FileInput(
@"
abc = 'some code: abc'
exec abc
exec abc in { }
exec abc in globals()
exec compile(abc)
",
@"
fob = 'some code: abc'
exec fob
exec fob in { }
exec fob in globals()
exec compile(fob)
"                    )
                }
            );

            RefactorTest("fob", "abc", version: new Version(3, 2),
            inputs: new[] { 
                    new FileInput(
@"
abc = 'some code: abc'
exec(abc)
exec(abc, { })
exec(abc, globals())
exec(compile(abc))
",
@"
fob = 'some code: abc'
exec(fob)
exec(fob, { })
exec(fob, globals())
exec(compile(fob))
"                    )
                }
            );

            RefactorTest("fob", "abc", version: new Version(2, 7),
            inputs: new[] { 
                    new FileInput(
@"
abc = { }
exec 'abc = 1'
exec 'abc = 1' in abc
exec 'abc = 1' in abc, abc
",
@"
fob = { }
exec 'abc = 1'
exec 'abc = 1' in fob
exec 'abc = 1' in fob, fob
"                    )
                }
            );

            RefactorTest("fob", "abc", version: new Version(3, 2),
            inputs: new[] { 
                    new FileInput(
@"
abc = { }
exec('abc = 1')
exec('abc = 1', abc)
exec('abc = 1', abc, abc)
",
@"
fob = { }
exec('abc = 1')
exec('abc = 1', fob)
exec('abc = 1', fob, fob)
"                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void IsInstanceScope() {
            RefactorTest("abc", "fob", version: new Version(3, 2),
            inputs: new[] { 
                    new FileInput(
@"
fob = 1
assert isinstance(fob, str)
print(fob.upper())
",
@"
abc = 1
assert isinstance(abc, str)
print(abc.upper())
"                    )
                }
            );

        }


        [TestMethod, Priority(0)]
        public void WithStatement() {
            RefactorTest("abc", "fob", version: new Version(3, 2),
            inputs: new[] { 
                    new FileInput(
@"
with fob as fob:
    print(fob)
",
@"
with abc as abc:
    print(abc)
"                    )
                }
            );

        }

        [TestMethod, Priority(0)]
        public void YieldStatement() {
            RefactorTest("fob", "abc", 
            inputs: new[] { 
                    new FileInput(
@"
def a():
    for abc in range(100):
        yield abc
",
@"
def a():
    for fob in range(100):
        yield fob
"                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void KeywordParameter() {
            RefactorTest("fob", "abc", 
            new[] { 
                    new FileInput(
@"
def f(abc):
    pass

f(abc = 10)
",
@"
def f(fob):
    pass

f(fob = 10)
"                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("def f(abc):"),
                    new ExpectedPreviewItem("f(abc = 10)")
                )   
            );

            RefactorTest("fob", "abc",
            new[] { 
                    new FileInput(
@"
def g():
    f(abc = 10)

def f(abc):
    pass
",
@"
def g():
    f(fob = 10)

def f(fob):
    pass
"                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("f(abc = 10)"),
                    new ExpectedPreviewItem("def f(abc):")
                )
            );

            RefactorTest("fob", "abc",
            new[] { 
                    new FileInput(
@"
def f(abc):
    pass

abc = 10
f(abc = abc)
",
@"
def f(fob):
    pass

abc = 10
f(fob = abc)
"                    )
                }
            );

        }

        [TestMethod, Priority(0)]
        public void ImportAsStatement() {
            RefactorTest("fob", "abc",
            new[] { 
                    new FileInput(
@"
import sys as abc
x = abc
",
@"
import sys as fob
x = fob
"                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("import sys as abc"),
                    new ExpectedPreviewItem("x = abc")
                )
            );

            RefactorTest("fob", "abc",
            new[] { 
                    new FileInput(
@"
import os, sys as abc
x = abc
",
@"
import os, sys as fob
x = fob
"                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("import os, sys as abc"),
                    new ExpectedPreviewItem("x = abc")
                )
            );

            RefactorTest("fob", "abc",
            new[] { 
                    new FileInput(
@"
import sys as abc, os
x = abc
",
@"
import sys as fob, os
x = fob
"                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("import sys as abc, os"),
                    new ExpectedPreviewItem("x = abc")
                )
            );
        }

        [TestMethod, Priority(0)]
        public void FromImportAsStatement() {
            RefactorTest("fob", "abc",
            new[] { 
                    new FileInput(
@"
from sys import oar as abc
x = abc
",
@"
from sys import oar as fob
x = fob
"                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("from sys import oar as abc"),
                    new ExpectedPreviewItem("x = abc")
                )
            );

            RefactorTest("fob", "abc",
            new[] { 
                    new FileInput(
@"
from sys import (oar as abc, baz)
x = abc
",
@"
from sys import (oar as fob, baz)
x = fob
"                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("from sys import (oar as abc, baz)"),
                    new ExpectedPreviewItem("x = abc")
                )
            );

        }

        [TestMethod, Priority(0)]
        public void Annotations() {
            RefactorTest("fob", "abc", version: new Version(3, 2),
                inputs: new[] { 
                    new FileInput(
@"def f(x : abc):
    print(abc)

abc = 200
",
@"def f(x : fob):
    print(fob)

fob = 200
"
                    )
                }
            );

            RefactorTest("fob", "abc", version: new Version(3, 2),
                inputs: new[] { 
                    new FileInput(
@"def f(x : f(abc)):
    print(abc)

abc = 200
",
@"def f(x : f(fob)):
    print(fob)

fob = 200
"
                    )
                }
            );

            RefactorTest("fob", "abc", version: new Version(3, 2),
                inputs: new[] { 
                    new FileInput(
@"def f(abc : None):
    print(abc)

abc = 200
",
@"def f(fob : None):
    print(fob)

abc = 200
"
                    )
                }
            );

        }

        [TestMethod, Priority(0)]
        public void NestedFunctions() {
            RefactorTest("h", "g",
                new[] { 
                    new FileInput(
@"def f(abc):
    def g(abc):
        print abc
    abc = 100
    g(abc)

def g(a, b, c):
    pass
",
@"def f(abc):
    def h(abc):
        print abc
    abc = 100
    h(abc)

def g(a, b, c):
    pass
"
                    )
                }
            );

        }

        [TestMethod, Priority(0)]
        public void CrossModuleRename() {
            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
                        "def abc(): pass",
                        "def fob(): pass"
                    ),
                    new FileInput(
                        "import test; test.abc()",
                        "import test; test.fob()",
                        "C:\\a.py"
                    )
                }
            );

            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
                        "abc = None",
                        "fob = None"
                    ),
                    new FileInput(
                        "import test; test.abc",
                        "import test; test.fob",
                        "C:\\a.py"
                    )
                }
            );

            RefactorTest("fob", "abc",
                new[] { 
                    new FileInput(
                        "def abc(): pass",
                        "def fob(): pass"
                    ),
                    new FileInput(
                        "from test import abc",
                        "from test import fob",
                        "C:\\a.py"
                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void CannotRename() {
            CannotRename("abc", "import abc", ErrorModuleName);
            CannotRename("abc", "from abc import oar", ErrorModuleName);
            CannotRename("abc", "import abc as oar", ErrorModuleName);
        }

        class FileInput {
            public readonly string Input, Output, Filename;

            public FileInput(string input, string output, string filename = "C:\\test.py") {
                Input = input;
                Output = output;
                Filename = filename;
            }
        }

        private void RefactorTest(string newName, string caretText, FileInput[] inputs, params ExpectedPreviewItem[] items) {
            RefactorTest(newName, caretText, inputs, true, null, items);
        }

        private void RefactorTest(string newName, string caretText, FileInput[] inputs, bool mutateTest = true, Version version = null, params ExpectedPreviewItem[] items) {
            for (int i = 0; i < inputs.Length; ++i) {
                Console.WriteLine("Test code {0} {1}:\r\n{2}\r\n", i, inputs[i].Filename, inputs[i].Input);
            }
            
            foreach(bool preview in new[] { true, false } ) {
                OneRefactorTest(newName, caretText, inputs, version, preview, null, items);

                if (mutateTest) {
                    // try again w/ a longer name
                    MutateTest(newName, caretText, inputs, version, newName + newName, preview);

                    // and a shorter name
                    MutateTest(newName, caretText, inputs, version, new string(newName[0], 1), preview);
                }
            }
        }

        private void MutateTest(string newName, string caretText, FileInput[] inputs, Version version, string altNewName, bool preview) {
            FileInput[] moreInputs = new FileInput[inputs.Length];
            for (int i = 0; i < moreInputs.Length; i++) {
                moreInputs[i] = new FileInput(
                    inputs[i].Input,
                    inputs[i].Output.Replace(newName, altNewName),
                    inputs[i].Filename
                );
            }

            OneRefactorTest(altNewName, caretText, moreInputs, version, preview, null);
        }

        class ExpectedPreviewItem {
            public readonly string Name;
            public readonly ExpectedPreviewItem[] Children;

            public ExpectedPreviewItem(string name, params ExpectedPreviewItem[] children) {
                Name = name;
                Children = children;
            }
        }

        private static void CannotRename(string caretText, string text, string error) {
            OneRefactorTest("fob", caretText, new[] { new FileInput(text, null), new FileInput("def oar(): pass", "", "C:\\abc.py") }, null, false, error, null);
        }

        private static void OneRefactorTest(string newName, string caretText, FileInput[] inputs, Version version, bool preview, string error, ExpectedPreviewItem[] expected = null) {
            Console.WriteLine("Replacing {0} with {1}", caretText, newName);

            var fact = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(version ?? new Version(2, 6));
            var classifierProvider = new PythonClassifierProvider(new MockContentTypeRegistryService());
            classifierProvider._classificationRegistry = new MockClassificationTypeRegistryService();
            var analyzer = new VsProjectAnalyzer(fact, new[] { fact });

            var taskProvider = new TaskProvider(null, new MockErrorProviderFactory());
            var originalTaskProvider = VsProjectAnalyzer.ReplaceTaskProviderForTests(new Lazy<TaskProvider>(() => {
                return taskProvider;
            }));

            try {
                for (int loops = 0; loops < 2; loops++) {
                    MockTextBuffer[] buffers = new MockTextBuffer[inputs.Length];
                    MockTextView[] views = new MockTextView[inputs.Length];
                    Dictionary<string, ITextBuffer> bufferTable = new Dictionary<string, ITextBuffer>();
                    List<MonitoredBufferResult> analysis = new List<MonitoredBufferResult>();
                    for (int i = 0; i < inputs.Length; i++) {
                        var filename = inputs[i].Filename;
                        buffers[i] = new MockTextBuffer(inputs[i].Input, filename);
                        views[i] = new MockTextView(buffers[i]);
                        buffers[i].AddProperty(typeof(VsProjectAnalyzer), analyzer);
                        classifierProvider.GetClassifier(buffers[i]);

                        bufferTable[filename] = buffers[i];
                        analysis.Add(analyzer.MonitorTextBuffer(views[i], buffers[i]));
                    }

                    // test runs twice, one w/ original buffer, once w/ re-analyzed buffers.
                    if (loops == 1) {
                        using (new DebugTimer("Waiting for previous analysis before raising change events")) {
                            analyzer.WaitForCompleteAnalysis(x => true);
                        }
                        Console.WriteLine("Running w/ re-anlyzed buffers");
                        // do it again w/ a changed buffer
                        foreach (var buffer in buffers) {
                            buffer.RaiseChangedLowPriority();
                        }
                    }

                    using (new DebugTimer("Waiting for analysis")) {
                        analyzer.WaitForCompleteAnalysis(x => true);
                    }

                    var caretPos = inputs[0].Input.IndexOf(caretText);
                    views[0].Caret.MoveTo(new SnapshotPoint(buffers[0].CurrentSnapshot, caretPos));

                    var extractInput = new RenameVariableTestInput(newName, bufferTable, preview);
                    var previewChangesService = new TestPreviewChanges(expected);

                    new VariableRenamer(views[0]).RenameVariable(extractInput, previewChangesService);
                    if (error != null) {
                        Assert.AreEqual(error, extractInput.Failure);
                        return;
                    }
                    Assert.IsNull(extractInput.Failure, "Unexpected error message: " + (extractInput.Failure ?? ""));
                    Assert.AreEqual(preview, previewChangesService.Previewed, preview ? "Changes were not previewed" : "Changes were previewed");
                    for (int i = 0; i < buffers.Length; i++) {
                        Assert.AreEqual(inputs[i].Output, buffers[i].CurrentSnapshot.GetText());
                    }

                    foreach (var monitored in analysis) {
                        analyzer.StopMonitoringTextBuffer(monitored.BufferParser, monitored.TextView);
                    }
                }
            } finally {
                analyzer.Dispose();
                VsProjectAnalyzer.ReplaceTaskProviderForTests(originalTaskProvider);
            }
        }

        class RenameVariableTestInput : IRenameVariableInput {
            private readonly string _name;
            private readonly bool _preview, _searchInStrings, _searchInComments;
            internal readonly List<string> Log = new List<string>();
            private readonly Dictionary<string, ITextBuffer> _buffers;
            internal string Failure;

            public RenameVariableTestInput(string name, Dictionary<string, ITextBuffer> buffers, bool preview = true, bool searchInStrings = false, bool searchInComments = false) {
                _name = name;
                _preview = preview;
                _searchInComments = searchInComments;
                _searchInStrings = searchInStrings;
                _buffers = buffers;
            }

            public RenameVariableRequest GetRenameInfo(string originalName, PythonLanguageVersion languageVersion) {
                var requestView = new RenameVariableRequestView(originalName, languageVersion);
                requestView.Name = _name;
                requestView.PreviewChanges = _preview;
                requestView.SearchInComments = _searchInComments;
                requestView.SearchInStrings = _searchInStrings;
                Assert.IsTrue(requestView.IsValid);
                var request = requestView.GetRequest();
                Assert.IsNotNull(request);
                return request;
            }

            public void CannotRename(string message) {
                Failure = message;
            }

            public void OutputLog(string message) {
                Log.Add(message);
            }

            public ITextBuffer GetBufferForDocument(string filename) {
                return _buffers[filename];
            }

            IVsLinkedUndoTransactionManager IRenameVariableInput.BeginGlobalUndo() {
                return null;
            }

            public void EndGlobalUndo(IVsLinkedUndoTransactionManager undo) {
            }

            public void ClearRefactorPane() {
            }
        }

        class TestPreviewChanges : IVsPreviewChangesService {
            public bool Previewed = false;
            private readonly ExpectedPreviewItem[] _expected;

            public TestPreviewChanges(ExpectedPreviewItem[] expected) {
                _expected = expected;
            }

            public int PreviewChanges(IVsPreviewChangesEngine pIVsPreviewChangesEngine) {
                object rootList;
                pIVsPreviewChangesEngine.GetRootChangesList(out rootList);
                IVsLiteTreeList list = rootList as IVsLiteTreeList;
                IVsPreviewChangesList preview = rootList as IVsPreviewChangesList;

                Assert.AreNotEqual(null, list);
                Assert.AreNotEqual(null, preview);

                if (_expected != null && _expected.Length > 0) {
                    try {
                        VerifyList(list, _expected);
                    } catch {
                        PrintList(list);
                        throw;
                    }
                } else {
                    PrintList(list);
                }
                
                Previewed = true;
                return pIVsPreviewChangesEngine.ApplyChanges();
            }

            private static void VerifyList(IVsLiteTreeList list, ExpectedPreviewItem[] expected) {
                uint count;
                list.GetItemCount(out count);

                Assert.AreEqual(expected.Length, (int)count);
                for (int i = 0; i < expected.Length; i++) {
                    string text;
                    list.GetText((uint)i, VSTREETEXTOPTIONS.TTO_DEFAULT, out text);
                    Assert.AreEqual(expected[i].Name, text);

                    int expandable;
                    list.GetExpandable((uint)i, out expandable);
                    if (expected[i].Children.Length != 0) {
                        Assert.AreEqual(1, expandable);
                        int canRecurse;
                        IVsLiteTreeList subList;
                        list.GetExpandedList((uint)i, out canRecurse, out subList);

                        VerifyList(subList, expected[i].Children);
                    } else {
                        Assert.AreEqual(0, expandable);
                    }
                }
            }

            private static void PrintList(IVsLiteTreeList list, int indent = 0) {
                uint count;
                list.GetItemCount(out count);

                
                for (int i = 0; i < count; i++) {
                    string text;
                    list.GetText((uint)i, VSTREETEXTOPTIONS.TTO_DEFAULT, out text);
                    Console.Write("{1}new ExpectedPreviewItem(\"{0}\"", text, new string(' ', indent * 4));

                    int expandable;
                    list.GetExpandable((uint)i, out expandable);
                    if (expandable != 0) {
                        Console.WriteLine(", ");
                        int canRecurse;
                        IVsLiteTreeList subList;
                        list.GetExpandedList((uint)i, out canRecurse, out subList);

                        PrintList(subList, indent + 1);
                    }

                    VSTREEDISPLAYDATA[] data = new VSTREEDISPLAYDATA[1];
                    list.GetDisplayData((uint)i, data);

                    // TODO: Validate display data

                    uint changeCnt = 0;
                    list.GetListChanges(ref changeCnt, null);

                    VSTREELISTITEMCHANGE[] changes = new VSTREELISTITEMCHANGE[changeCnt];
                    list.GetListChanges(ref changeCnt, changes);

                    // TODO: Valiate changes

                    if (i != count - 1) {
                        Console.WriteLine("),");
                    } else {
                        Console.WriteLine(")");
                    }
                }
            }
        }
    }
}

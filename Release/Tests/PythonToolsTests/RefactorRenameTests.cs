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
using System.Reflection;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.PythonTools.Refactoring;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using TestUtilities;
using TestUtilities.Mocks;

namespace PythonToolsTests {
    [TestClass]
    public class RefactorRenameTests {
        private const string ErrorModuleName = "Cannot rename a module name";

        [TestMethod, Priority(0)]
        public void PrivateMemberMangling() {
            RefactorTest("foo", "__f",
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
    def foo(self):
        pass

    def g(self):
        self.foo()

    def x(self):
        self.foo()
    "

                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("def __f(self):"),
                    new ExpectedPreviewItem("self.__f()"),
                    new ExpectedPreviewItem("self._C__f()")
                )
            );

            RefactorTest("foo", "_C__f",
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
    def foo(self):
        pass

    def g(self):
        self.foo()

    def x(self):
        self.foo()
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
            RefactorTest("foo", "abc",
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
    foo = 100
    def __getitem__(self, key): pass
def d(*p): pass

a = C()
a.foo
a .foo
a. foo
a.foo; a.foo
b = a
b.foo
a[a.foo]
a[ a.foo]
a[a.foo,]
a[a.foo ,]
a[a.foo,a.foo]
a[a.foo, a.foo]
a[a.foo ,a.foo]
a[a.foo , a.foo]
a[a.foo,a.foo,]
a[a.foo, a.foo, ]
a[a.foo ,a.foo ,]
a[a.foo , a.foo,  ]
a[(a.foo)]
a[(a.foo), a.foo]
a[(a.foo, a.foo)]
a[a.foo, (a.foo)]
a[(a.foo,)]
a[(a.foo,), a.foo]
a[(a.foo, a.foo,)]
a[a.foo, (a.foo,)]
a[ a . foo ]
d(a.foo)
d((a.foo))
d((a.foo,))
d((a.foo),)
d(a.foo,a.foo)
d (a.foo)
d(a . foo, a. foo, a. \
foo, a\
.foo)"
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

            RefactorTest("foo", "abc",
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
    foo = 100
class C2:
    abc = 150

A1 = C1
A2 = C2
a1 = C1()
a2 = C2()
A1.foo
A2.abc
a1.foo
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

            RefactorTest("foo", "abc",
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
    foo = 100
class C2(C1):
    pass

A1 = C1
A2 = C2
a1 = C1()
a2 = C2()
A1.foo
A2.foo
a1.foo
a2.foo
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
            RefactorTest("foo", "abc",
                new[] { new FileInput("abc = 100; x = (abc for abc in range(abc))", "foo = 100; x = (abc for abc in range(foo))") }
            );
            RefactorTest("foo", "abc for",
                new[] { new FileInput("abc = 100; x = (abc for abc in range(abc))", "abc = 100; x = (foo for foo in range(abc))") }
            );
            RefactorTest("foo", "abc for",
                new[] { new FileInput("abc = 100; x = (abc for abc in range(abc) for xyz in abc)", "abc = 100; x = (foo for foo in range(abc) for xyz in foo)") }
            );
        }

        [TestMethod, Priority(0)]
        public void TypelessForVariable() {
            RefactorTest("baz", "abc",
                new[] { 
                    new FileInput(
@"def f():
    l = foo
    for abc in l:
        l.append(abc)",
@"def f():
    l = foo
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
    l = foo
    for abc, de_f in l:
        l.append(abc)",
@"def f():
    l = foo
    for baz, de_f in l:
        l.append(baz)"
                    )
                }
            );

            RefactorTest("baz", "abc",
                new[] { 
                    new FileInput(
@"def f():
    l = foo
    for (abc, de_f) in l:
        l.append(abc)",
@"def f():
    l = foo
    for (baz, de_f) in l:
        l.append(baz)"
                    )
                }
            );

            RefactorTest("baz", "abc",
                new[] { 
                    new FileInput(
@"def f():
    l = foo
    for (abc,), de_f in l:
        l.append(abc)",
@"def f():
    l = foo
    for (baz,), de_f in l:
        l.append(baz)"
                    )
                }
            );

            RefactorTest("baz", "de_f",
                new[] { 
                    new FileInput(
@"def f():
    l = foo
    for abc, de_f in l:
        l.append(de_f)",
@"def f():
    l = foo
    for abc, baz in l:
        l.append(baz)"
                    )
                }
            );

            RefactorTest("baz", "de_f",
                new[] { 
                    new FileInput(
@"def f():
    l = foo
    for (abc, de_f) in l:
        l.append(de_f)",
@"def f():
    l = foo
    for (abc, baz) in l:
        l.append(baz)"
                    )
                }
            );

            RefactorTest("baz", "de_f",
                new[] { 
                    new FileInput(
@"def f():
    l = foo
    for abc, (de_f,) in l:
        l.append(de_f)",
@"def f():
    l = foo
    for abc, (baz,) in l:
        l.append(baz)"
                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanityInstanceField() {
            RefactorTest("foo", "abc",
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
        self.foo = 100
    foo = 200

a = C()
a.foo
"
                    )
                }
            );
            RefactorTest("foo", "abc",
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
        self.foo = 100

a = C()
a.foo
"
                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanityParameter() {
            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"def f(abc):
    print abc, abc(), abc[i], x[abc], y(abc)
    abc, _ = 100, abc
",
@"def f(foo):
    print foo, foo(), foo[i], x[foo], y(foo)
    foo, _ = 100, foo
"
                    )
                }
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"def f(abc):
    print abc
    abc = 100

abc = 200
",
@"def f(foo):
    print foo
    foo = 100

abc = 200
"
                    )
                }
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"def f(*abc):
    print abc

abc = 200
",
@"def f(*foo):
    print foo

abc = 200
"
                    )
                }
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"def f(**abc):
    print abc

abc = 200
",
@"def f(**foo):
    print foo

abc = 200
"
                    )
                }
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"def f(*p, **abc):
    print abc

abc = 200
",
@"def f(*p, **foo):
    print foo

abc = 200
"
                    )
                }
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"def f(abc=None):
    print abc

abc = 200
",
@"def f(foo=None):
    print foo

abc = 200
"
                    )
                }
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"def f(i=abc):
    print abc

abc = 200
",
@"def f(i=foo):
    print foo

foo = 200
"
                    )
                }
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"def f(abc=abc):
    print abc

abc = 200
",
@"def f(foo=abc):
    print foo

abc = 200
"
                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanityLocal() {
            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"def f():
    abc = 100
    print abc
",
@"def f():
    foo = 100
    print foo
"
                    )
                }
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"def f():
    abc = 100
    print abc

abc = 200
",
@"def f():
    foo = 100
    print foo

abc = 200
"
                    )
                }
            );
            
            RefactorTest("foo", "abc",
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
    foo = 100
    print foo
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
            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"def f():
    abc = 100
    def g():
        print abc
",
@"def f():
    foo = 100
    def g():
        print foo
"
                    )
                }
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"def f():
    abc = 100
    def g():
        print abc

abc = 200
",
@"def f():
    foo = 100
    def g():
        print foo

abc = 200
"
                    )
                }
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"def f(abc):
    def g(abc):
        print abc
    abc = 100
    g(abc)
",
@"def f(foo):
    def g(abc):
        print abc
    foo = 100
    g(foo)
"
                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanityLambda() {
            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"def f():
    abc = 100
    g = lambda: abc
",
@"def f():
    foo = 100
    g = lambda: foo
"
                    )
                }
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"def f():
    abc = 100
    g = lambda: abc

abc = 200
",
@"def f():
    foo = 100
    g = lambda: foo

abc = 200
"
                    )
                }
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"def f(abc):
    g = lambda abc: abc
    abc = 100
    g(abc)
",
@"def f(foo):
    g = lambda abc: abc
    foo = 100
    g(foo)
"
                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanityInlineIf() {
            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
                        "abc = 0; x = True if abc else False",
                        "foo = 0; x = True if foo else False"
                    )
                }
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
                        "abc = 0; x = abc if abc else False",
                        "foo = 0; x = foo if foo else False"
                    )
                }
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
                        "abc = 0; x = True if abc else abc",
                        "foo = 0; x = True if foo else foo"
                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanityGenerator() {
            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
                        "x = (abc for abc in range(100))",
                        "x = (foo for foo in range(100))"
                    )
                }
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
                        "abc = 100; x = (abc for abc in range(abc))",
                        "foo = 100; x = (abc for abc in range(foo))"
                    )
                }
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
                        "x = (i[abc] for abc in range(100))",
                        "x = (i[foo] for foo in range(100))"
                    )
                }
            );

            RefactorTest("foo", "abc", version: new Version(3, 2),
                inputs: new[] { 
                    new FileInput(
                        "x = {abc : i[abc] for abc in range(100)}",
                        "x = {foo : i[foo] for foo in range(100)}"
                    )
                }
            );

            RefactorTest("foo", "abc", version: new Version(3, 2),
                inputs: new[] { 
                    new FileInput(
                        "x = {i : abc for i, abc in enumerate(range(100))}",
                        "x = {i : foo for i, foo in enumerate(range(100))}"
                    )
                }
            );

            RefactorTest("foo", "abc", version: new Version(3, 2),
                inputs: new[] { 
                    new FileInput(
                        "x = {abc for abc in range(100)}",
                        "x = {foo for foo in range(100)}"
                    )
                }
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
                        "abc = [1, 2, 3]; x = (abc for i in abc)",
                        "foo = [1, 2, 3]; x = (foo for i in foo)"
                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanityGeneratorFilter() {
            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
                        "abc = 10; x = (i for i in range(100) if abc > i)",
                        "foo = 10; x = (i for i in range(100) if foo > i)"
                    )
                }
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
                        "abc = 10; x = (i for i in range(100) if i or abc)",
                        "foo = 10; x = (i for i in range(100) if i or foo)"
                    )
                }
            );

            RefactorTest("foo", "abc", version: new Version(3, 2),
                inputs: new[] { 
                    new FileInput(
                        "x = {abc : i[abc] for abc in range(100)}",
                        "x = {foo : i[foo] for foo in range(100)}"
                    )
                }
            );

            RefactorTest("foo", "abc", version: new Version(3, 2),
                inputs: new[] { 
                    new FileInput(
                        "x = {i : abc for i, abc in enumerate(range(100))}",
                        "x = {i : foo for i, foo in enumerate(range(100))}"
                    )
                }
            );

            RefactorTest("foo", "abc", version: new Version(3, 2),
                inputs: new[] { 
                    new FileInput(
                        "x = {abc for abc in range(100)}",
                        "x = {foo for foo in range(100)}"
                    )
                }
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
                        "abc = [1, 2, 3]; x = (abc for i in abc)",
                        "foo = [1, 2, 3]; x = (foo for i in foo)"
                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanitySlices() {
            RefactorTest("foo", "abc", version: new Version(3, 2),
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
foo = 100
x = y[:foo]
x = y[foo:]
x = y[foo:foo+1]
x = y[foo-1:foo]
x = y[foo-1:foo:foo+1]
"                    )
                }
            );


        }

        [TestMethod, Priority(0)]
        public void SanityGlobal() {
            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"def f():
    global abc
    assert abc
    print(abc)

abc = 100
",
@"def f():
    global foo
    assert foo
    print(foo)

foo = 100
"
                    )
                }
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"def f():
    global abc; assert abc
    print(abc)

abc = 100
",
@"def f():
    global foo; assert foo
    print(foo)

foo = 100
"
                    )
                }
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"abc = { }
def f():
    print abc[0]
",
@"foo = { }
def f():
    print foo[0]
"
                    )
                }
            );

            RefactorTest("foo", "abc",
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
    global foo
    print foo

def g():
    abc = 200

foo = 100
"
                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanityNonLocal() {
            RefactorTest("foo", "abc", version: new Version(3, 2),
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
    foo = 100
    def f():
        nonlocal foo
        assert foo
        print(foo)

abc = 100
"                    )
                }
            );

            RefactorTest("foo", "abc", version: new Version(3, 2),
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
    foo = 100
    def f():
        nonlocal foo;assert foo
        print(foo)

abc = 100
"                    )
                }
            );

            RefactorTest("foo", "abc", version: new Version(3, 2),
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
    foo = 100
    def f():
        nonlocal  foo
        print(foo)

abc = 100
"                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanityRenameClass() {
            RefactorTest("foo", "abc",
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

class foo:
    pass

x = foo()
y = { foo: 'abc', 'abc': foo }"
                    )

                }
            );

            RefactorTest("foo", "abc", 
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

class foo(object):
    A = 10

x = foo()
foo.A
y = { foo: 'abc', 'abc': foo, foo.A: 'A', 'A': foo.A }"
                    )

                }
            );

            RefactorTest("foo", "abc",
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

class foo(type):
    A = 10

x = foo()
foo.A"
                    )
                }
            );

            RefactorTest("foo", "abc",
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

class foo(object):
    A = 10

x = foo()
foo.A"
                    )

                }
            );

            RefactorTest("foo", "abc",
            new[] { 
                    new FileInput(
@"class abc(object):
    pass
class x(abc):
    pass
",
@"class foo(object):
    pass
class x(foo):
    pass
"
                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void RenameMetaclass() {
            RefactorTest("foo", "abc", version: new Version(2, 7),
            inputs: new[] { 
                    new FileInput(
@"class abc(type):
    pass
class x(object):
    __metaclass__ = abc
",
@"class foo(type):
    pass
class x(object):
    __metaclass__ = foo
"
                    )
                }
            );

            RefactorTest("foo", "abc", version: new Version(3, 2),
            inputs: new[] { 
                    new FileInput(
@"class abc(type):
    pass
class x(metaclass=abc):
    pass
",
@"class foo(type):
    pass
class x(metaclass=foo):
    pass
"
                    )
                }
            );

        }

        [TestMethod, Priority(0)]
        public void SanityRenameFunction() {
            RefactorTest("foo", "abc",
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

def foo(x):
    pass

x = foo()
doc = foo.__doc__
fdoc = foo.func_doc
y = { foo: 'abc', 'abc': foo }"
                    )

                }
            );

            RefactorTest("foo", "abc",
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

def  foo(x):
    pass

x = foo()
doc = foo.__doc__
fdoc = foo.func_doc"
                    )

                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanityDelLocal() {
            RefactorTest("foo", "abc",
            new[] { 
                    new FileInput(
@"
def f():
    abc = 100
    del abc",
@"
def f():
    foo = 100
    del foo"                    
                    )

                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanityDelInstanceMember() {
            RefactorTest("foo", "abc",
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
        self.foo = 42

def f():    
    del C().foo
    c = C()
    del c.foo
    c.foo = 100"
                    )

                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanityDelClassMember() {
            RefactorTest("foo", "abc",
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
    foo = 42
    del foo

def f():    
    del C().foo
    del C.foo
"
                    )

                }
            );
        }

        [TestMethod, Priority(0)]
        public void SanityDelGlobal() {
            RefactorTest("foo", "abc",
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
    global foo
    del foo

foo = 100
"
                    )

                }
            );

            RefactorTest("foo", "abc",
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
    global foo;pass
    del f, foo

foo = 100
"
                    )

                }
            );
        }

        [TestMethod, Priority(0)]
        public void DelNonLocal() {
            RefactorTest("foo", "abc", version: new Version(3, 2),
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
    foo = 100
    def f():
        nonlocal foo
        del foo

abc = 100
"                    )
                }
            );

        }

        [TestMethod, Priority(0)]
        public void Decorators() {
            RefactorTest("abc", "foo",
            new[] { 
                    new FileInput(
@"
def foo():
    pass

@foo
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

            RefactorTest("abc", "foo",
            new[] { 
                    new FileInput(
@"
def foo():
    pass

@foo
@property
@foo
@foo
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

            RefactorTest("abc", "foo",
            new[] { 
                    new FileInput(
@"
def foo(name):
    pass

@foo('name')
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

            RefactorTest("abc", "foo",
            new[] { 
                    new FileInput(
@"
foo = 'foo'

def dec(name):
    pass

@dec(foo)
def f():
    pass
",
@"
abc = 'foo'

def dec(name):
    pass

@dec(abc)
def f():
    pass
"                    )
                }
            );

            RefactorTest("abc", "foo",
            new[] { 
                    new FileInput(
@"
class C:
    @staticmethod
    def foo():
        pass

@C.foo
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
            RefactorTest("abc", "foo", 
            new[] { 
                    new FileInput(
@"
try:
    pass
except Exception, foo:
    print(foo)
",
@"
try:
    pass
except Exception, abc:
    print(abc)
"                    )
                }
            );

            RefactorTest("abc", "foo", version: new Version(3, 2),
            inputs: new[] { 
                    new FileInput(
@"
try:
    pass
except Exception as foo:
    print(foo)
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
            RefactorTest("foo", "abc",
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
    foo = 123
except:
    pass
finally:
    del foo
"                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void RaiseStatement() {
            RefactorTest("foo", "abc", version: new Version(2, 7),
            inputs: new[] { 
                    new FileInput(
@"
abc = 'a message: abc'
raise Exception(abc)
raise Exception, abc, abc
",
@"
foo = 'a message: abc'
raise Exception(foo)
raise Exception, foo, foo
"                    )
                }
            );

            RefactorTest("foo", "abc", version: new Version(3, 2),
            inputs: new[] { 
                    new FileInput(
@"
abc = 'a message: abc'
raise Exception(abc)
raise Exception() from abc
",
@"
foo = 'a message: abc'
raise Exception(foo)
raise Exception() from foo
"                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void ExecStatement() {
            RefactorTest("foo", "abc", version: new Version(2, 7),
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
foo = 'some code: abc'
exec foo
exec foo in { }
exec foo in globals()
exec compile(foo)
"                    )
                }
            );

            RefactorTest("foo", "abc", version: new Version(3, 2),
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
foo = 'some code: abc'
exec(foo)
exec(foo, { })
exec(foo, globals())
exec(compile(foo))
"                    )
                }
            );

            RefactorTest("foo", "abc", version: new Version(2, 7),
            inputs: new[] { 
                    new FileInput(
@"
abc = { }
exec 'abc = 1'
exec 'abc = 1' in abc
exec 'abc = 1' in abc, abc
",
@"
foo = { }
exec 'abc = 1'
exec 'abc = 1' in foo
exec 'abc = 1' in foo, foo
"                    )
                }
            );

            RefactorTest("foo", "abc", version: new Version(3, 2),
            inputs: new[] { 
                    new FileInput(
@"
abc = { }
exec('abc = 1')
exec('abc = 1', abc)
exec('abc = 1', abc, abc)
",
@"
foo = { }
exec('abc = 1')
exec('abc = 1', foo)
exec('abc = 1', foo, foo)
"                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void WithStatement() {
            RefactorTest("abc", "foo", version: new Version(3, 2),
            inputs: new[] { 
                    new FileInput(
@"
with foo as foo:
    print(foo)
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
            RefactorTest("foo", "abc", 
            inputs: new[] { 
                    new FileInput(
@"
def a():
    for abc in range(100):
        yield abc
",
@"
def a():
    for foo in range(100):
        yield foo
"                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void KeywordParameter() {
            RefactorTest("foo", "abc", 
            new[] { 
                    new FileInput(
@"
def f(abc):
    pass

f(abc = 10)
",
@"
def f(foo):
    pass

f(foo = 10)
"                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("def f(abc):"),
                    new ExpectedPreviewItem("f(abc = 10)")
                )   
            );

            RefactorTest("foo", "abc",
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
    f(foo = 10)

def f(foo):
    pass
"                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("f(abc = 10)"),
                    new ExpectedPreviewItem("def f(abc):")
                )
            );

            RefactorTest("foo", "abc",
            new[] { 
                    new FileInput(
@"
def f(abc):
    pass

abc = 10
f(abc = abc)
",
@"
def f(foo):
    pass

abc = 10
f(foo = abc)
"                    )
                }
            );

        }

        [TestMethod, Priority(0)]
        public void ImportAsStatement() {
            RefactorTest("foo", "abc",
            new[] { 
                    new FileInput(
@"
import sys as abc
x = abc
",
@"
import sys as foo
x = foo
"                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("import sys as abc"),
                    new ExpectedPreviewItem("x = abc")
                )
            );

            RefactorTest("foo", "abc",
            new[] { 
                    new FileInput(
@"
import os, sys as abc
x = abc
",
@"
import os, sys as foo
x = foo
"                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("import os, sys as abc"),
                    new ExpectedPreviewItem("x = abc")
                )
            );

            RefactorTest("foo", "abc",
            new[] { 
                    new FileInput(
@"
import sys as abc, os
x = abc
",
@"
import sys as foo, os
x = foo
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
            RefactorTest("foo", "abc",
            new[] { 
                    new FileInput(
@"
from sys import bar as abc
x = abc
",
@"
from sys import bar as foo
x = foo
"                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("from sys import bar as abc"),
                    new ExpectedPreviewItem("x = abc")
                )
            );

            RefactorTest("foo", "abc",
            new[] { 
                    new FileInput(
@"
from sys import (bar as abc, baz)
x = abc
",
@"
from sys import (bar as foo, baz)
x = foo
"                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("from sys import (bar as abc, baz)"),
                    new ExpectedPreviewItem("x = abc")
                )
            );

        }

        [TestMethod, Priority(0)]
        public void Annotations() {
            RefactorTest("foo", "abc", version: new Version(3, 2),
                inputs: new[] { 
                    new FileInput(
@"def f(x : abc):
    print(abc)

abc = 200
",
@"def f(x : foo):
    print(foo)

foo = 200
"
                    )
                }
            );

            RefactorTest("foo", "abc", version: new Version(3, 2),
                inputs: new[] { 
                    new FileInput(
@"def f(x : f(abc)):
    print(abc)

abc = 200
",
@"def f(x : f(foo)):
    print(foo)

foo = 200
"
                    )
                }
            );

            RefactorTest("foo", "abc", version: new Version(3, 2),
                inputs: new[] { 
                    new FileInput(
@"def f(abc : None):
    print(abc)

abc = 200
",
@"def f(foo : None):
    print(foo)

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
            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
                        "def abc(): pass",
                        "def foo(): pass"
                    ),
                    new FileInput(
                        "import test; test.abc()",
                        "import test; test.foo()",
                        "a.py"
                    )
                }
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
                        "abc = None",
                        "foo = None"
                    ),
                    new FileInput(
                        "import test; test.abc",
                        "import test; test.foo",
                        "a.py"
                    )
                }
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
                        "def abc(): pass",
                        "def foo(): pass"
                    ),
                    new FileInput(
                        "from test import abc",
                        "from test import foo",
                        "a.py"
                    )
                }
            );
        }

        [TestMethod, Priority(0)]
        public void CannotRename() {
            CannotRename("abc", "import abc", ErrorModuleName);
            CannotRename("abc", "from abc import bar", ErrorModuleName);
            CannotRename("abc", "import abc as bar", ErrorModuleName);
        }

        class FileInput {
            public readonly string Input, Output, Filename;

            public FileInput(string input, string output, string filename = "test.py") {
                Input = input;
                Output = output;
                Filename = filename;
            }
        }

        private void RefactorTest(string newName, string caretText, FileInput[] inputs, params ExpectedPreviewItem[] items) {
            RefactorTest(newName, caretText, inputs, true, null, items);
        }

        private void RefactorTest(string newName, string caretText, FileInput[] inputs, bool mutateTest = true, Version version = null, params ExpectedPreviewItem[] items) {
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
            OneRefactorTest("foo", caretText, new[] { new FileInput(text, null), new FileInput("def bar(): pass", "", "abc.py") }, null, false, error, null);
        }

        private static void OneRefactorTest(string newName, string caretText, FileInput[] inputs, Version version, bool preview, string error, ExpectedPreviewItem[] expected = null) {
            var fact = new CPythonInterpreterFactory(version ?? new Version(2, 6), new Guid(), "test interpreter", "C:\\foo\\python.exe", "C:\\foo\\pythonw.exe", "PYTHONPATH", ProcessorArchitecture.X86);
            using (var analyzer = new VsProjectAnalyzer(fact, new[] { fact }, new MockErrorProviderFactory())) {
                for (int loops = 0; loops < 2; loops++) {
                    MockTextBuffer[] buffers = new MockTextBuffer[inputs.Length];
                    MockTextView[] views = new MockTextView[inputs.Length];
                    Dictionary<string, ITextBuffer> bufferTable = new Dictionary<string, ITextBuffer>();
                    List<MonitoredBufferResult> analysis = new List<MonitoredBufferResult>();
                    for (int i = 0; i < inputs.Length; i++) {
                        var filename = TestData.GetPath(inputs[i].Filename);
                        buffers[i] = new MockTextBuffer(inputs[i].Input, filename);
                        views[i] = new MockTextView(buffers[i]);
                        buffers[i].AddProperty(typeof(VsProjectAnalyzer), analyzer);
                        bufferTable[filename] = buffers[i];
                        analysis.Add(analyzer.MonitorTextBuffer(views[i], buffers[i]));
                    }

                    // test runs twice, one w/ original buffer, once w/ re-analyzed buffers.
                    if (loops == 1) {
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
                    Assert.AreEqual(previewChangesService.Previewed, preview);
                    for (int i = 0; i < buffers.Length; i++) {
                        Assert.AreEqual(inputs[i].Output, buffers[i].CurrentSnapshot.GetText());
                    }
                    
                    foreach (var monitored in analysis) {
                        analyzer.StopMonitoringTextBuffer(monitored.BufferParser);
                    }
                }

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

            public RenameVariableRequest GetRenameInfo(string originalName) {
                var requestView = new RenameVariableRequestView(originalName);
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

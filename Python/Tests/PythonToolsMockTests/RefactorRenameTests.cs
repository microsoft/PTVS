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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

extern alias analysis;
extern alias pythontools;
using analysis::Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.MockVsTests;
using pythontools::Microsoft.PythonTools.Editor;
using pythontools::Microsoft.PythonTools.Intellisense;
using pythontools::Microsoft.PythonTools.Refactoring;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities;

namespace PythonToolsMockTests {
    [TestClass]
    public class RefactorRenameTests {
        private static readonly string ErrorModuleName = Microsoft.PythonTools.Strings.RenameVariable_CannotRenameModuleName;

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        private MockVs _vs;

        [TestInitialize]
        public void TestInit() {
            MockPythonToolsPackage.SuppressTaskProvider = true;
            VsProjectAnalyzer.SuppressTaskProvider = true;
            _vs = new MockVs();
        }

        [TestCleanup]
        public void TestCleanup() {
            MockPythonToolsPackage.SuppressTaskProvider = false;
            VsProjectAnalyzer.SuppressTaskProvider = false;
            _vs.Dispose();
        }


        [TestMethod, Priority(UnitTestPriority.P2)]
        public void PrivateMemberMangling() {
            RefactorTest("xyz", "__f",
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
    def xyz(self):
        pass

    def g(self):
        self.xyz()

    def x(self):
        self.xyz()
    "

                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("def __f(self):"),
                    new ExpectedPreviewItem("self.__f()"),
                    new ExpectedPreviewItem("self._C__f()")
                )
            );

            RefactorTest("xyz", "_C__f",
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
    def xyz(self):
        pass

    def g(self):
        self.xyz()

    def x(self):
        self.xyz()
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
                null,
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("def __longname(self):"),
                    new ExpectedPreviewItem("self.__longname()"),
                    new ExpectedPreviewItem("self._C__longname()")
                )
            );

            // "__" is not a private name
            RefactorTest("__", "__f",
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
    def __(self):
        pass

    def g(self):
        self.__()

    def x(self):
        self.__()
    "

                    )
                },
                false,
                null,
                null,
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("def __f(self):"),
                    new ExpectedPreviewItem("self.__f()"),
                    new ExpectedPreviewItem("self._C__f()")
                )
            );
        }

        [TestMethod, Priority(UnitTestPriority.P2_FAILING)]
        public void SanityClassField() {
            RefactorTest("xyz", "abc",
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
    xyz = 100
    def __getitem__(self, key): pass
def d(*p): pass

a = C()
a.xyz
a .xyz
a. xyz
a.xyz; a.xyz
b = a
b.xyz
a[a.xyz]
a[ a.xyz]
a[a.xyz,]
a[a.xyz ,]
a[a.xyz,a.xyz]
a[a.xyz, a.xyz]
a[a.xyz ,a.xyz]
a[a.xyz , a.xyz]
a[a.xyz,a.xyz,]
a[a.xyz, a.xyz, ]
a[a.xyz ,a.xyz ,]
a[a.xyz , a.xyz,  ]
a[(a.xyz)]
a[(a.xyz), a.xyz]
a[(a.xyz, a.xyz)]
a[a.xyz, (a.xyz)]
a[(a.xyz,)]
a[(a.xyz,), a.xyz]
a[(a.xyz, a.xyz,)]
a[a.xyz, (a.xyz,)]
a[ a . xyz ]
d(a.xyz)
d((a.xyz))
d((a.xyz,))
d((a.xyz),)
d(a.xyz,a.xyz)
d (a.xyz)
d(a . xyz, a. xyz, a. \
xyz, a\
.xyz)"
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

            RefactorTest("xyz", "abc",
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
    xyz = 100
class C2:
    abc = 150

A1 = C1
A2 = C2
a1 = C1()
a2 = C2()
A1.xyz
A2.abc
a1.xyz
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

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void InheritedClassField() {

            RefactorTest("xyz", "abc",
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
    xyz = 100
class C2(C1):
    pass

A1 = C1
A2 = C2
a1 = C1()
a2 = C2()
A1.xyz
A2.xyz
a1.xyz
a2.xyz
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

        [TestMethod, Priority(UnitTestPriority.P3_FAILING)]
        public void RenameGeneratorVariable() {
            // http://pytools.codeplex.com/workitem/454
            RefactorTest("xyz", "abc",
                new[] { new FileInput("abc = 100; x = (abc for abc in range(abc))", "xyz = 100; x = (abc for abc in range(xyz))") }
            );
            RefactorTest("xyz", "abc for",
                new[] { new FileInput("abc = 100; x = (abc for abc in range(abc))", "abc = 100; x = (xyz for xyz in range(abc))") }
            );
            RefactorTest("xyz", "abc for",
                new[] { new FileInput("abc = 100; x = (abc for abc in range(abc) for xyz in abc)", "abc = 100; x = (xyz for xyz in range(abc) for xyz in xyz)") }
            );
        }

        [TestMethod, Priority(UnitTestPriority.P2)]
        public void TypelessForVariable() {
            RefactorTest("baz", "abc",
                new[] {
                    new FileInput(
@"def f():
    l = xyz
    for abc in l:
        l.append(abc)",
@"def f():
    l = xyz
    for baz in l:
        l.append(baz)"
                    )
                }
            );
        }

        [TestMethod, Priority(UnitTestPriority.P2)]
        [TestCategory("10s")]
        public void TupleForVariable() {
            RefactorTest("baz", "abc",
                new[] {
                    new FileInput(
@"def f():
    l = xyz
    for abc, de_f in l:
        l.append(abc)",
@"def f():
    l = xyz
    for baz, de_f in l:
        l.append(baz)"
                    )
                }
            );

            RefactorTest("baz", "abc",
                new[] {
                    new FileInput(
@"def f():
    l = xyz
    for (abc, de_f) in l:
        l.append(abc)",
@"def f():
    l = xyz
    for (baz, de_f) in l:
        l.append(baz)"
                    )
                }
            );

            RefactorTest("baz", "abc",
                new[] {
                    new FileInput(
@"def f():
    l = xyz
    for (abc,), de_f in l:
        l.append(abc)",
@"def f():
    l = xyz
    for (baz,), de_f in l:
        l.append(baz)"
                    )
                }
            );

            RefactorTest("baz", "de_f",
                new[] {
                    new FileInput(
@"def f():
    l = xyz
    for abc, de_f in l:
        l.append(de_f)",
@"def f():
    l = xyz
    for abc, baz in l:
        l.append(baz)"
                    )
                }
            );

            RefactorTest("baz", "de_f",
                new[] {
                    new FileInput(
@"def f():
    l = xyz
    for (abc, de_f) in l:
        l.append(de_f)",
@"def f():
    l = xyz
    for (abc, baz) in l:
        l.append(baz)"
                    )
                }
            );

            RefactorTest("baz", "de_f",
                new[] {
                    new FileInput(
@"def f():
    l = xyz
    for abc, (de_f,) in l:
        l.append(de_f)",
@"def f():
    l = xyz
    for abc, (baz,) in l:
        l.append(baz)"
                    )
                }
            );
        }

        [TestMethod, Priority(UnitTestPriority.P2)]
        public void SanityInstanceField() {
            RefactorTest("xyz", "abc",
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
        self.xyz = 100
    xyz = 200

a = C()
a.xyz
"
                    )
                }
            );
            RefactorTest("xyz", "abc",
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
        self.xyz = 100

a = C()
a.xyz
"
                    )
                }
            );
        }

        [TestMethod, Priority(UnitTestPriority.P3_FAILING)]
        [TestCategory("10s")]
        public void SanityParameter() {
            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
@"def f(abc):
    print abc, abc(), abc[i], x[abc], y(abc)
    abc, _ = 100, abc
",
@"def f(xyz):
    print xyz, xyz(), xyz[i], x[xyz], y(xyz)
    xyz, _ = 100, xyz
"
                    )
                }
            );

            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
@"def f(abc):
    print abc
    abc = 100

abc = 200
",
@"def f(xyz):
    print xyz
    xyz = 100

abc = 200
"
                    )
                }
            );

            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
@"def f(*abc):
    print abc

abc = 200
",
@"def f(*xyz):
    print xyz

abc = 200
"
                    )
                }
            );

            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
@"def f(**abc):
    print abc

abc = 200
",
@"def f(**xyz):
    print xyz

abc = 200
"
                    )
                }
            );

            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
@"def f(*p, **abc):
    print abc

abc = 200
",
@"def f(*p, **xyz):
    print xyz

abc = 200
"
                    )
                }
            );

            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
@"def f(abc=None):
    print abc

abc = 200
",
@"def f(xyz=None):
    print xyz

abc = 200
"
                    )
                }
            );

            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
@"def f(i=abc):
    print abc

abc = 200
",
@"def f(i=xyz):
    print xyz

xyz = 200
"
                    )
                }
            );

            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
@"def f(abc=abc):
    print abc

abc = 200
",
@"def f(xyz=abc):
    print xyz

abc = 200
"
                    )
                }
            );
        }

        [TestMethod, Priority(UnitTestPriority.P2_FAILING)]
        public void SanityLocal() {
            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
@"def f():
    abc = 100
    print abc
",
@"def f():
    xyz = 100
    print xyz
"
                    )
                }
            );

            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
@"def f():
    abc = 100
    print abc

abc = 200
",
@"def f():
    xyz = 100
    print xyz

abc = 200
"
                    )
                }
            );

            RefactorTest("xyz", "abc",
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
    xyz = 100
    print xyz
def g():
    abc = 100
def h(abc):
    pass
"
                    )
                }
            );

        }

        [TestMethod, Priority(UnitTestPriority.P2_FAILING)]
        public void SanityClosure() {
            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
@"def f():
    abc = 100
    def g():
        print abc
",
@"def f():
    xyz = 100
    def g():
        print xyz
"
                    )
                }
            );

            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
@"def f():
    abc = 100
    def g():
        print abc

abc = 200
",
@"def f():
    xyz = 100
    def g():
        print xyz

abc = 200
"
                    )
                }
            );

            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
@"def f(abc):
    def g(abc):
        print abc
    abc = 100
    g(abc)
",
@"def f(xyz):
    def g(abc):
        print abc
    xyz = 100
    g(xyz)
"
                    )
                }
            );
        }

        [TestMethod, Priority(UnitTestPriority.P2_FAILING)]
        public void SanityLambda() {
            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
@"def f():
    abc = 100
    g = lambda: abc
",
@"def f():
    xyz = 100
    g = lambda: xyz
"
                    )
                }
            );

            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
@"def f():
    abc = 100
    g = lambda: abc

abc = 200
",
@"def f():
    xyz = 100
    g = lambda: xyz

abc = 200
"
                    )
                }
            );

            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
@"def f(abc):
    g = lambda abc: abc
    abc = 100
    g(abc)
",
@"def f(xyz):
    g = lambda abc: abc
    xyz = 100
    g(xyz)
"
                    )
                }
            );
        }

        [TestMethod, Priority(UnitTestPriority.P2)]
        public void SanityInlineIf() {
            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
                        "abc = 0; x = True if abc else False",
                        "xyz = 0; x = True if xyz else False"
                    )
                }
            );

            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
                        "abc = 0; x = abc if abc else False",
                        "xyz = 0; x = xyz if xyz else False"
                    )
                }
            );

            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
                        "abc = 0; x = True if abc else abc",
                        "xyz = 0; x = True if xyz else xyz"
                    )
                }
            );
        }

        [TestMethod, Priority(UnitTestPriority.P3)]
        public void SanityGenerator() {
            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
                        "x = (abc for abc in range(100))",
                        "x = (xyz for xyz in range(100))"
                    )
                }
            );

            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
                        "abc = 100; x = (abc for abc in range(abc))",
                        "xyz = 100; x = (abc for abc in range(xyz))"
                    )
                }
            );

            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
                        "x = (i[abc] for abc in range(100))",
                        "x = (i[xyz] for xyz in range(100))"
                    )
                }
            );

            RefactorTest("xyz", "abc", version: new Version(3, 2),
                inputs: new[] {
                    new FileInput(
                        "x = {abc : i[abc] for abc in range(100)}",
                        "x = {xyz : i[xyz] for xyz in range(100)}"
                    )
                }
            );

            RefactorTest("xyz", "abc", version: new Version(3, 2),
                inputs: new[] {
                    new FileInput(
                        "x = {i : abc for i, abc in enumerate(range(100))}",
                        "x = {i : xyz for i, xyz in enumerate(range(100))}"
                    )
                }
            );

            RefactorTest("xyz", "abc", version: new Version(3, 2),
                inputs: new[] {
                    new FileInput(
                        "x = {abc for abc in range(100)}",
                        "x = {xyz for xyz in range(100)}"
                    )
                }
            );

            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
                        "abc = [1, 2, 3]; x = (abc for i in abc)",
                        "xyz = [1, 2, 3]; x = (xyz for i in xyz)"
                    )
                }
            );
        }

        [TestMethod, Priority(UnitTestPriority.P3)]
        public void SanityGeneratorFilter() {
            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
                        "abc = 10; x = (i for i in range(100) if abc > i)",
                        "xyz = 10; x = (i for i in range(100) if xyz > i)"
                    )
                }
            );

            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
                        "abc = 10; x = (i for i in range(100) if i or abc)",
                        "xyz = 10; x = (i for i in range(100) if i or xyz)"
                    )
                }
            );

            RefactorTest("xyz", "abc", version: new Version(3, 2),
                inputs: new[] {
                    new FileInput(
                        "x = {abc : i[abc] for abc in range(100)}",
                        "x = {xyz : i[xyz] for xyz in range(100)}"
                    )
                }
            );

            RefactorTest("xyz", "abc", version: new Version(3, 2),
                inputs: new[] {
                    new FileInput(
                        "x = {i : abc for i, abc in enumerate(range(100))}",
                        "x = {i : xyz for i, xyz in enumerate(range(100))}"
                    )
                }
            );

            RefactorTest("xyz", "abc", version: new Version(3, 2),
                inputs: new[] {
                    new FileInput(
                        "x = {abc for abc in range(100)}",
                        "x = {xyz for xyz in range(100)}"
                    )
                }
            );

            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
                        "abc = [1, 2, 3]; x = (abc for i in abc)",
                        "xyz = [1, 2, 3]; x = (xyz for i in xyz)"
                    )
                }
            );
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void SanitySlices() {
            RefactorTest("xyz", "abc", version: new Version(3, 2),
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
xyz = 100
x = y[:xyz]
x = y[xyz:]
x = y[xyz:xyz+1]
x = y[xyz-1:xyz]
x = y[xyz-1:xyz:xyz+1]
"                    )
                }
            );


        }

        [TestMethod, Priority(UnitTestPriority.P2)]
        public void SanityGlobal() {
            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
@"def f():
    global abc
    assert abc
    print(abc)

abc = 100
",
@"def f():
    global xyz
    assert xyz
    print(xyz)

xyz = 100
"
                    )
                }
            );

            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
@"def f():
    global abc; assert abc
    print(abc)

abc = 100
",
@"def f():
    global xyz; assert xyz
    print(xyz)

xyz = 100
"
                    )
                }
            );

            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
@"abc = { }
def f():
    print abc[0]
",
@"xyz = { }
def f():
    print xyz[0]
"
                    )
                }
            );

            RefactorTest("xyz", "abc",
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
    global xyz
    print xyz

def g():
    abc = 200

xyz = 100
"
                    )
                }
            );
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void SanityNonLocal() {
            RefactorTest("xyz", "abc", version: new Version(3, 2),
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
    xyz = 100
    def f():
        nonlocal xyz
        assert xyz
        print(xyz)

abc = 100
"                    )
                }
            );

            RefactorTest("xyz", "abc", version: new Version(3, 2),
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
    xyz = 100
    def f():
        nonlocal xyz;assert xyz
        print(xyz)

abc = 100
"                    )
                }
            );

            RefactorTest("xyz", "abc", version: new Version(3, 2),
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
    xyz = 100
    def f():
        nonlocal  xyz
        print(xyz)

abc = 100
"                    )
                }
            );
        }

        [TestMethod, Priority(UnitTestPriority.P2)]
        [TestCategory("10s")]
        public void SanityRenameClass() {
            RefactorTest("xyz", "abc",
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

class xyz:
    pass

x = xyz()
y = { xyz: 'abc', 'abc': xyz }"
                    )

                }
            );

            RefactorTest("xyz", "abc",
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

class xyz(object):
    A = 10

x = xyz()
xyz.A
y = { xyz: 'abc', 'abc': xyz, xyz.A: 'A', 'A': xyz.A }"
                    )

                }
            );

            RefactorTest("xyz", "abc",
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

class xyz(type):
    A = 10

x = xyz()
xyz.A"
                    )
                }
            );

            RefactorTest("xyz", "abc",
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

class xyz(object):
    A = 10

x = xyz()
xyz.A"
                    )

                }
            );

            RefactorTest("xyz", "abc",
            new[] {
                    new FileInput(
@"class abc(object):
    pass
class x(abc):
    pass
",
@"class xyz(object):
    pass
class x(xyz):
    pass
"
                    )
                }
            );
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void RenameMetaclass() {
            RefactorTest("xyz", "abc", version: new Version(2, 7),
            inputs: new[] {
                    new FileInput(
@"class abc(type):
    pass
class x(object):
    __metaclass__ = abc
",
@"class xyz(type):
    pass
class x(object):
    __metaclass__ = xyz
"
                    )
                }
            );

            RefactorTest("xyz", "abc", version: new Version(3, 2),
            inputs: new[] {
                    new FileInput(
@"class abc(type):
    pass
class x(metaclass=abc):
    pass
",
@"class xyz(type):
    pass
class x(metaclass=xyz):
    pass
"
                    )
                }
            );

        }

        [TestMethod, Priority(UnitTestPriority.P2)]
        public void SanityRenameFunction() {
            RefactorTest("xyz", "abc",
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

def xyz(x):
    pass

x = xyz()
doc = xyz.__doc__
fdoc = xyz.func_doc
y = { xyz: 'abc', 'abc': xyz }"
                    )

                }
            );

            RefactorTest("xyz", "abc",
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

def  xyz(x):
    pass

x = xyz()
doc = xyz.__doc__
fdoc = xyz.func_doc"
                    )

                }
            );
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void SanityDelLocal() {
            RefactorTest("xyz", "abc",
            new[] {
                    new FileInput(
@"
def f():
    abc = 100
    del abc",
@"
def f():
    xyz = 100
    del xyz"
                    )

                }
            );
        }

        [TestMethod, Priority(UnitTestPriority.P2)]
        public void SanityDelInstanceMember() {
            RefactorTest("xyz", "abc",
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
        self.xyz = 42

def f():    
    del C().xyz
    c = C()
    del c.xyz
    c.xyz = 100"
                    )

                }
            );
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void SanityDelClassMember() {
            RefactorTest("xyz", "abc",
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
    xyz = 42
    del xyz

def f():    
    del C().xyz
    del C.xyz
"
                    )

                }
            );
        }

        [TestMethod, Priority(UnitTestPriority.P2)]
        public void SanityDelGlobal() {
            RefactorTest("xyz", "abc",
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
    global xyz
    del xyz

xyz = 100
"
                    )

                }
            );

            RefactorTest("xyz", "abc",
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
    global xyz;pass
    del f, xyz

xyz = 100
"
                    )

                }
            );
        }

        [TestMethod, Priority(UnitTestPriority.P2_FAILING)]
        public void DelNonLocal() {
            RefactorTest("xyz", "abc", version: new Version(3, 2),
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
    xyz = 100
    def f():
        nonlocal xyz
        del xyz

abc = 100
"                    )
                }
            );

        }

        [TestMethod, Priority(UnitTestPriority.P3)]
        [TestCategory("10s")]
        public void Decorators() {
            RefactorTest("abc", "xyz",
            new[] {
                    new FileInput(
@"
def xyz():
    pass

@xyz
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

            RefactorTest("abc", "xyz",
            new[] {
                    new FileInput(
@"
def xyz():
    pass

@xyz
@property
@xyz
@xyz
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

            RefactorTest("abc", "xyz",
            new[] {
                    new FileInput(
@"
def xyz(name):
    pass

@xyz('name')
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

            RefactorTest("abc", "xyz",
            new[] {
                    new FileInput(
@"
xyz = 'xyz'

def dec(name):
    pass

@dec(xyz)
def f():
    pass
",
@"
abc = 'xyz'

def dec(name):
    pass

@dec(abc)
def f():
    pass
"                    )
                }
            );

            RefactorTest("abc", "xyz",
            new[] {
                    new FileInput(
@"
class C:
    @staticmethod
    def xyz():
        pass

@C.xyz
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

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void TryStatement() {
            RefactorTest("abc", "xyz",
            new[] {
                    new FileInput(
@"
try:
    pass
except Exception, xyz:
    print(xyz)
",
@"
try:
    pass
except Exception, abc:
    print(abc)
"                    )
                }
            );

            RefactorTest("abc", "xyz", version: new Version(3, 2),
            inputs: new[] {
                    new FileInput(
@"
try:
    pass
except Exception as xyz:
    print(xyz)
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

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void FinallyStatement() {
            RefactorTest("xyz", "abc",
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
    xyz = 123
except:
    pass
finally:
    del xyz
"                    )
                }
            );
        }

        [TestMethod, Priority(UnitTestPriority.P2)]
        public void RaiseStatement() {
            RefactorTest("xyz", "abc", version: new Version(2, 7),
            inputs: new[] {
                    new FileInput(
@"
abc = 'a message: abc'
raise Exception(abc)
raise Exception, abc, abc
",
@"
xyz = 'a message: abc'
raise Exception(xyz)
raise Exception, xyz, xyz
"                    )
                }
            );

            RefactorTest("xyz", "abc", version: new Version(3, 2),
            inputs: new[] {
                    new FileInput(
@"
abc = 'a message: abc'
raise Exception(abc)
raise Exception() from abc
",
@"
xyz = 'a message: abc'
raise Exception(xyz)
raise Exception() from xyz
"                    )
                }
            );
        }

        [TestMethod, Priority(UnitTestPriority.P2)]
        public void ExecStatement() {
            RefactorTest("xyz", "abc", version: new Version(2, 7),
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
xyz = 'some code: abc'
exec xyz
exec xyz in { }
exec xyz in globals()
exec compile(xyz)
"                    )
                }
            );

            RefactorTest("xyz", "abc", version: new Version(3, 2),
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
xyz = 'some code: abc'
exec(xyz)
exec(xyz, { })
exec(xyz, globals())
exec(compile(xyz))
"                    )
                }
            );

            RefactorTest("xyz", "abc", version: new Version(2, 7),
            inputs: new[] {
                    new FileInput(
@"
abc = { }
exec 'abc = 1'
exec 'abc = 1' in abc
exec 'abc = 1' in abc, abc
",
@"
xyz = { }
exec 'abc = 1'
exec 'abc = 1' in xyz
exec 'abc = 1' in xyz, xyz
"                    )
                }
            );

            RefactorTest("xyz", "abc", version: new Version(3, 2),
            inputs: new[] {
                    new FileInput(
@"
abc = { }
exec('abc = 1')
exec('abc = 1', abc)
exec('abc = 1', abc, abc)
",
@"
xyz = { }
exec('abc = 1')
exec('abc = 1', xyz)
exec('abc = 1', xyz, xyz)
"                    )
                }
            );
        }

        [TestMethod, Priority(UnitTestPriority.P2)]
        public void IsInstanceScope() {
            RefactorTest("abc", "xyz", version: new Version(3, 2),
            inputs: new[] {
                    new FileInput(
@"
xyz = 1
assert isinstance(xyz, str)
print(xyz.upper())
",
@"
abc = 1
assert isinstance(abc, str)
print(abc.upper())
"                    )
                }
            );

        }


        [TestMethod, Priority(UnitTestPriority.P1)]
        public void WithStatement() {
            RefactorTest("abc", "xyz", version: new Version(3, 2),
            inputs: new[] {
                    new FileInput(
@"
with xyz as xyz:
    print(xyz)
",
@"
with abc as abc:
    print(abc)
"                    )
                }
            );

        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void YieldStatement() {
            RefactorTest("xyz", "abc",
            inputs: new[] {
                    new FileInput(
@"
def a():
    for abc in range(100):
        yield abc
",
@"
def a():
    for xyz in range(100):
        yield xyz
"                    )
                }
            );
        }

        [TestMethod, Priority(UnitTestPriority.P2)]
        public void KeywordParameter() {
            RefactorTest("xyz", "abc",
            new[] {
                    new FileInput(
@"
def f(abc):
    pass

f(abc = 10)
",
@"
def f(xyz):
    pass

f(xyz = 10)
"                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("def f(abc):"),
                    new ExpectedPreviewItem("f(abc = 10)")
                )
            );

            RefactorTest("xyz", "abc",
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
    f(xyz = 10)

def f(xyz):
    pass
"                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("f(abc = 10)"),
                    new ExpectedPreviewItem("def f(abc):")
                )
            );

            RefactorTest("xyz", "abc",
            new[] {
                    new FileInput(
@"
def f(abc):
    pass

abc = 10
f(abc = abc)
",
@"
def f(xyz):
    pass

abc = 10
f(xyz = abc)
"                    )
                }
            );

            RefactorTest("xyz", "abc",
            new[] {
                    new FileInput(
@"
def f(abc=10):
    pass

abc = 10
f(abc)
",
@"
def f(xyz=10):
    pass

abc = 10
f(abc)
"                    )
                }
            );
        }

        [TestMethod, Priority(UnitTestPriority.P2_FAILING)]
        public void ImportAsStatement() {
            RefactorTest("xyz", "abc",
            new[] {
                    new FileInput(
@"
import sys as abc
x = abc
",
@"
import sys as xyz
x = xyz
"                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("import sys as abc"),
                    new ExpectedPreviewItem("x = abc")
                )
            );

            RefactorTest("xyz", "abc",
            new[] {
                    new FileInput(
@"
import os, sys as abc
x = abc
",
@"
import os, sys as xyz
x = xyz
"                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("import os, sys as abc"),
                    new ExpectedPreviewItem("x = abc")
                )
            );

            RefactorTest("xyz", "abc",
            new[] {
                    new FileInput(
@"
import sys as abc, os
x = abc
",
@"
import sys as xyz, os
x = xyz
"                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("import sys as abc, os"),
                    new ExpectedPreviewItem("x = abc")
                )
            );
        }

        [TestMethod, Priority(UnitTestPriority.P2)]
        public void FromImportAsStatement() {
            RefactorTest("xyz", "abc",
            new[] {
                    new FileInput(
@"
from sys import oar as abc
x = abc
",
@"
from sys import oar as xyz
x = xyz
"                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("from sys import oar as abc"),
                    new ExpectedPreviewItem("x = abc")
                )
            );

            RefactorTest("xyz", "abc",
            new[] {
                    new FileInput(
@"
from sys import (oar as abc, baz)
x = abc
",
@"
from sys import (oar as xyz, baz)
x = xyz
"                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("from sys import (oar as abc, baz)"),
                    new ExpectedPreviewItem("x = abc")
                )
            );

        }

        [TestMethod, Priority(UnitTestPriority.P2)]
        public void Annotations() {
            RefactorTest("xyz", "abc", version: new Version(3, 2),
                inputs: new[] {
                    new FileInput(
@"def f(x : abc):
    print(abc)

abc = 200
",
@"def f(x : xyz):
    print(xyz)

xyz = 200
"
                    )
                }
            );

            RefactorTest("xyz", "abc", version: new Version(3, 2),
                inputs: new[] {
                    new FileInput(
@"def f(x : f(abc)):
    print(abc)

abc = 200
",
@"def f(x : f(xyz)):
    print(xyz)

xyz = 200
"
                    )
                }
            );

            RefactorTest("xyz", "abc", version: new Version(3, 2),
                inputs: new[] {
                    new FileInput(
@"def f(abc : None):
    print(abc)

abc = 200
",
@"def f(xyz : None):
    print(xyz)

abc = 200
"
                    )
                }
            );

        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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

        [TestMethod, Priority(UnitTestPriority.P2)]
        public void CrossModuleRename() {
            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
                        "def abc(): pass",
                        "def xyz(): pass"
                    ),
                    new FileInput(
                        "import test; test.abc()",
                        "import test; test.xyz()",
                        "C:\\a.py"
                    )
                }
            );

            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
                        "abc = None",
                        "xyz = None"
                    ),
                    new FileInput(
                        "import test; test.abc",
                        "import test; test.xyz",
                        "C:\\a.py"
                    )
                }
            );

            RefactorTest("xyz", "abc",
                new[] {
                    new FileInput(
                        "def abc(): pass",
                        "def xyz(): pass"
                    ),
                    new FileInput(
                        "from test import abc",
                        "from test import xyz",
                        "C:\\a.py"
                    )
                }
            );
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void CannotRename() {
            CannotRename("abc", "import abc", ErrorModuleName);
            CannotRename("abc", "from abc import oar", ErrorModuleName);
            CannotRename("abc", "import abc as oar", ErrorModuleName);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void ValidPythonIdentifier() {
            string validUnicodeCharacters = "䶵䶴㐁";
            char invalidIdentifier = '!';

            //Valid python 2 and 3 identifiers
            CheckPythonIdentifierValidity("a1b2c3");
            CheckPythonIdentifierValidity("_abc123");
            CheckPythonIdentifierValidity("a1_b2_c3_");
            CheckPythonIdentifierValidity("__");
            CheckPythonIdentifierValidity("_abc_");

            //Valid in python 3 but not 2
            CheckPythonIdentifierValidity(validUnicodeCharacters, false);
            CheckPythonIdentifierValidity(string.Concat(validUnicodeCharacters, "a1"), false);
            CheckPythonIdentifierValidity(string.Concat("a1", validUnicodeCharacters), false);
            CheckPythonIdentifierValidity(string.Concat("a1", validUnicodeCharacters, "b2", validUnicodeCharacters), false);

            //Valid in neither python 3 or 2
            CheckPythonIdentifierValidity(invalidIdentifier.ToString(), false, false);
            CheckPythonIdentifierValidity("", false, false);
            CheckPythonIdentifierValidity(" ", false, false);
            CheckPythonIdentifierValidity("1a2b3c", false, false);
            CheckPythonIdentifierValidity(" 1a 2b 3c ", false, false);
            CheckPythonIdentifierValidity(string.Concat("abc", invalidIdentifier), false, false);
            CheckPythonIdentifierValidity(string.Concat(invalidIdentifier, "abc"), false, false);
            CheckPythonIdentifierValidity(string.Concat("abc", invalidIdentifier, "123", validUnicodeCharacters), false, false);
            CheckPythonIdentifierValidity(string.Concat(validUnicodeCharacters, "1a2b3c", invalidIdentifier), false, false);
            CheckPythonIdentifierValidity(string.Concat(invalidIdentifier, "a1b3c3", validUnicodeCharacters), false, false);
        }

        private static void CheckPythonIdentifierValidity(string identifier, bool isValidPython2Identifier = true, bool isValidPython3Identifier = true) {
            Assert.AreEqual(isValidPython2Identifier, ExtractMethodRequestView.IsValidPythonIdentifier(identifier, PythonLanguageVersion.V27));
            Assert.AreEqual(isValidPython3Identifier, ExtractMethodRequestView.IsValidPythonIdentifier(identifier, PythonLanguageVersion.V30));
        }

        class FileInput {
            public readonly string Input, Output, Filename;

            public FileInput(string input, string output, string filename = null) {
                Input = input;
                Output = output;
                Filename = filename ?? "test.py";
            }
        }

        private void RefactorTest(string newName, string caretText, FileInput[] inputs, params ExpectedPreviewItem[] items) {
            RefactorTest(newName, caretText, inputs, true, null, null, items);
        }

        private void RefactorTest(string newName, string caretText, FileInput[] inputs, bool mutateTest = true, Version version = null, string expectedSelectedText = null, params ExpectedPreviewItem[] items) {
            for (int i = 0; i < inputs.Length; ++i) {
                Console.WriteLine("Test code {0} {1}:\r\n{2}\r\n", i, inputs[i].Filename, inputs[i].Input);
            }

            foreach(bool preview in new[] { true, false } ) {
                OneRefactorTest(newName, caretText, inputs, version, preview, null, items, expectedSelectedText);

                if (mutateTest) {
                    // try again w/ a longer name
                    MutateTest(newName, caretText, inputs, version, newName + newName, preview, expectedSelectedText);

                    // and a shorter name
                    MutateTest(newName, caretText, inputs, version, new string(newName[0], 1), preview, expectedSelectedText);
                }
            }
        }

        private void MutateTest(string newName, string caretText, FileInput[] inputs, Version version, string altNewName, bool preview, string expectedSelectedText) {
            FileInput[] moreInputs = new FileInput[inputs.Length];
            for (int i = 0; i < moreInputs.Length; i++) {
                moreInputs[i] = new FileInput(
                    inputs[i].Input,
                    inputs[i].Output.Replace(newName, altNewName),
                    inputs[i].Filename
                );
            }

            OneRefactorTest(altNewName, caretText, moreInputs, version, preview, null, expectedSelectedText: expectedSelectedText);
        }

        class ExpectedPreviewItem {
            public readonly string Name;
            public readonly ExpectedPreviewItem[] Children;

            public ExpectedPreviewItem(string name, params ExpectedPreviewItem[] children) {
                Name = name;
                Children = children;
            }
        }

        private void CannotRename(string caretText, string text, string error) {
            OneRefactorTest("xyz", caretText, new[] { new FileInput(text, null), new FileInput("def oar(): pass", "", null) }, null, false, error, null);
        }

        private void OneRefactorTest(string newName, string caretText, FileInput[] inputs, Version version, bool preview, string error, ExpectedPreviewItem[] expected = null, string expectedSelectedText = null) {
            Console.WriteLine("Replacing {0} with {1}", caretText, newName);
            version = version ?? new Version(2, 7);

            for (int loops = 0; loops < 2; loops++) {
                var views = new List<PythonEditor>();
                try {
                    var mainView = new PythonEditor(inputs[0].Input, version.ToLanguageVersion(), _vs, filename: inputs[0].Filename, inProcAnalyzer: true);
                    var analyzer = mainView.Analyzer;

                    views.Add(mainView);
                    var bufferTable = new Dictionary<string, ITextBuffer> {
                        { mainView.BufferInfo.Filename, mainView.CurrentSnapshot.TextBuffer }
                    };
                    foreach (var i in inputs.Skip(1)) {
                        var editor = new PythonEditor(i.Input, version.ToLanguageVersion(), _vs, mainView.Factory, analyzer, i.Filename);
                        views.Add(editor);
                        bufferTable[editor.BufferInfo.Filename] = editor.CurrentSnapshot.TextBuffer;
                    }


                    // test runs twice, one w/ original buffer, once w/ re-analyzed buffers.
                    if (loops == 1) {
                        // do it again w/ a changed buffer
                        mainView.Text = mainView.Text;
                    }


                    var extractInput = new RenameVariableTestInput(expectedSelectedText ?? caretText, newName, bufferTable, preview);
                    var previewChangesService = new TestPreviewChanges(expected);

                    var uiThread = _vs.ServiceProvider.GetUIThread();
                    uiThread.InvokeTask(async () => {
                        var snap = mainView.CurrentSnapshot;
                        var caretPos = snap.GetText().IndexOf(caretText);
                        mainView.View.MoveCaret(new SnapshotPoint(snap, caretPos));
                        Assert.AreEqual(caretText, snap.GetText(caretPos, caretText.Length));

                        var vr = new VariableRenamer(mainView.View.View, _vs.ServiceProvider);
                        await vr.RenameVariable(extractInput, previewChangesService);
                    }).Wait();
                    if (error != null) {
                        Assert.AreEqual(error, extractInput.Failure);
                        return;
                    }
                    Assert.IsNull(extractInput.Failure, "Unexpected error message: " + (extractInput.Failure ?? ""));
                    Assert.AreEqual(preview, previewChangesService.Previewed, preview ? "Changes were not previewed" : "Changes were previewed");
                    AssertUtil.ArrayEquals(inputs.Select(i => i.Output).ToList(), views.Select(v => v.Text).ToList());
                } finally {
                    views.Reverse();
                    foreach (var v in views) {
                        v.Dispose();
                    }
                }
            }
        }

        class RenameVariableTestInput : IRenameVariableInput {
            private readonly string _originalName, _name;
            private readonly bool _preview, _searchInStrings, _searchInComments;
            internal readonly List<string> Log = new List<string>();
            private readonly Dictionary<string, ITextBuffer> _buffers;
            internal string Failure;

            public RenameVariableTestInput(
                string expectedOriginalName,
                string name,
                Dictionary<string, ITextBuffer> buffers,
                bool preview = true,
                bool searchInStrings = false,
                bool searchInComments = false
            ) {
                _originalName = expectedOriginalName;
                _name = name;
                _preview = preview;
                _searchInComments = searchInComments;
                _searchInStrings = searchInStrings;
                _buffers = buffers;
            }

            public RenameVariableRequest GetRenameInfo(string originalName, PythonLanguageVersion languageVersion) {
                Assert.IsTrue(_originalName.StartsWith(originalName) || originalName.StartsWith("__") && _originalName.EndsWith(originalName), $"Selected text {originalName} did not match {_originalName}");

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
                try {
                    return _buffers[filename];
                } catch (KeyNotFoundException ex) {
                    throw new KeyNotFoundException($"Failed to find {filename} in {string.Join(", ", _buffers.Keys)}", ex);
                }
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
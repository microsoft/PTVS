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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AnalysisTests;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.PythonTools.Refactoring;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using TestUtilities;
using TestUtilities.Mocks;
using TestUtilities.Python;

namespace PythonToolsTests {
    using AP = AnalysisProtocol;

    [TestClass]
    public class ExtractMethodTests {
        private static readonly string ErrorReturn = Strings.ExtractMethodSelectionContainsReturn;
        private static readonly string ErrorYield = Strings.ExtractMethodContainsYieldExpression;
        private static readonly string ErrorContinue = Strings.ExtractMethodSelectionContainsContinueButNotEnclosingLoop;
        private static readonly string ErrorBreak = Strings.ExtractMethodSelectionContainsBreakButNotEnclosingLoop;
        private static readonly string ErrorReturnWithOutputs = Strings.ExtractMethodAssignsVariablesAndReturns;
        private static readonly string ErrorImportStar = Strings.ExtractMethodContainsFromImportStar;
        private static readonly string ErrorExtractFromClass = Strings.ExtractMethodStatementsFromClassDefinition;

        [TestInitialize]
        public void TestInitialize() => TestEnvironmentImpl.TestInitialize(60);

        [TestCleanup]
        public void TestCleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public async Task TestGlobalNonLocalVars() {
            await SuccessTest("ABC = 42",
@"def f():
    ABC = 42
    def f():
        nonlocal ABC
        ABC = 200
        print(ABC)
    return f",
@"def g():
    ABC = 42
    return ABC

def f():
    ABC = g()
    def f():
        nonlocal ABC
        ABC = 200
        print(ABC)
    return f");

            await SuccessTest("ABC = 42",
@"def f():
    ABC = 42
    def f():
        print(ABC)
    return f",
@"def g():
    ABC = 42
    return ABC

def f():
    ABC = g()
    def f():
        print(ABC)
    return f");

            await SuccessTest("ABC = 42",
@"def f():
    global ABC
    ABC = 42",
@"def g():
    ABC = 42
    return ABC

def f():
    global ABC
    ABC = g()");

        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public async Task TestDefinitions() {
            await SuccessTest("x = .. = h()",
@"def f():
    def g():
        return 42
    def h():
        return 23

    x = g() 
    z = h()
",
@"def g(g, h):
    x = g() 
    z = h()

def f():
    def g():
        return 42
    def h():
        return 23

    g(g, h)
");

            await SuccessTest("x = .. = h()",
@"def f():
    class g():
        pass
    class h():
        pass

    x = g() 
    z = h()
",
@"def g(g, h):
    x = g() 
    z = h()

def f():
    class g():
        pass
    class h():
        pass

    g(g, h)
");

            await SuccessTest("@ .. pass",
@"@property
def f(): pass",
@"def g():
    @property
    def f(): pass
    return f

f = g()");
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public async Task TestLeadingComment() {
            await SuccessTest("x = 41",
@"# fob
x = 41",
@"# fob
def g():
    x = 41
    return x

x = g()");
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public async Task AssignInIfStatementReadAfter() {
            await ExtractMethodTest(@"class C:
    def fob(self):
        if False: # fob
            oar = player = Player()
        else:                
            player.update()
", "oar = player = Player()", TestResult.Success(
 @"class C:
    def g(self):
        oar = player = Player()

    def fob(self):
        if False: # fob
            self.g()
        else:                
            player.update()
"
 ), scopeName: "C");


            await ExtractMethodTest(@"class C:
    def fob(self):
        if False: 
            oar = player = Player()
        else:                
            player.update()
", "oar = player = Player()", TestResult.Success(
 @"class C:
    def g(self):
        oar = player = Player()

    def fob(self):
        if False: 
            self.g()
        else:                
            player.update()
"
 ), scopeName: "C");

            await ExtractMethodTest(@"class C:
    def fob(self):
        if False: 
            oar = player = Player()
                
        player.update()
", "oar = player = Player()", TestResult.Success(
@"class C:
    def g(self):
        oar = player = Player()
        return player

    def fob(self):
        if False: 
            player = self.g()
                
        player.update()
"
), scopeName: "C");


        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public async Task ExtractMethodIndexExpr() {
            await ExtractMethodTest(@"class C:
    def process_kinect_event(self, e):
        for skeleton in e.skeletons:
            fob[skeleton.dwTrackingID] = Player()
", "fob[skeleton.dwTrackingID] = Player()", TestResult.Success(
 @"class C:
    def g(self, skeleton):
        fob[skeleton.dwTrackingID] = Player()

    def process_kinect_event(self, e):
        for skeleton in e.skeletons:
            self.g(skeleton)
"
 ), scopeName: "C");
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public async Task TestExtractLambda() {
            // lambda is present in the code
            await ExtractMethodTest(
@"def f():
    pass

def x():
    abc = lambda x: 42", "pass", TestResult.Success(
@"def g():
    pass

def f():
    g()

def x():
    abc = lambda x: 42"));

            // lambda is being extracted
            await ExtractMethodTest(
@"def f():
    abc = lambda x: 42", "lambda x: 42", TestResult.Success(
@"def g():
    return lambda x: 42

def f():
    abc = g()"));
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public async Task TestExtractGenerator() {
            var code = @"def f(imp = imp):
    yield 42";

            await ExtractMethodTest(
code, () => new Span(code.IndexOf("= imp") + 2, 3), TestResult.Success(
@"def g():
    return imp

def f(imp = g()):
    yield 42"));
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public async Task TestExtractDefaultValue() {
            var code = @"def f(imp = imp):
    pass";

            await ExtractMethodTest(
code, () => new Span(code.IndexOf("= imp") + 2, 3), TestResult.Success(
@"def g():
    return imp

def f(imp = g()):
    pass"));
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public async Task TestFromImportStar() {
            await ExtractMethodTest(
@"def f():
    from sys import *", "from sys import *", TestResult.Error(ErrorImportStar));
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public async Task TestExtractDefiniteAssignmentAfter() {
            await SuccessTest("x = 42",
@"def f():
    x = 42

    for x, y in []:
        print x, y",
@"def g():
    x = 42

def f():
    g()

    for x, y in []:
        print x, y");
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public async Task TestExtractDefiniteAssignmentAfterStmtList() {
            await SuccessTest("x = 42",
@"def f():
    x = 42; x = 100

    for x, y in []:
        print x, y",
@"def g():
    x = 42

def f():
    g(); x = 100

    for x, y in []:
        print x, y");
        }




        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public async Task TestExtractDefiniteAssignmentAfterStmtListRead() {
            await SuccessTest("x = 100",
@"def f():
    x = 100; x

    for x, y in []:
        print (x, y)",
@"def g():
    x = 100
    return x

def f():
    x = g(); x

    for x, y in []:
        print (x, y)");
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        [TestCategory("10s")]
        public async Task TestAllNodes() {
            var prefixes = new string[] { " # fob\r\n", "" };
            var suffixes = new string[] { " # oar", "" };
            foreach (var suffix in suffixes) {
                foreach (var prefix in prefixes) {
                    foreach (var testCase in TestExpressions.Expressions) {
                        if (testCase.StartsWith("yield")) {
                            // not currently supported
                            continue;
                        }

                        var text = prefix + testCase + suffix;
                        string expected = String.Format("{1}def g():\r\n    return {0}\r\n\r\ng(){2}", testCase, prefix, suffix);
                        await SuccessTest(new Span(prefix.Length, testCase.Length), text, expected);
                    }
                }
            }

            var bannedStmts = new[] { "break", "continue", "return abc" };
            var allStmts = TestExpressions.Statements2x
                    .Except(bannedStmts)
                    .Select(text => new { Text = text, Version = PythonLanguageVersion.V27 })
                    .Concat(
                        TestExpressions.Statements3x
                            .Select(text => new { Text = text, Version = PythonLanguageVersion.V33 }));

            foreach (var suffix in suffixes) {
                foreach (var prefix in prefixes) {
                    foreach (var stmtTest in allStmts) {
                        var text = prefix + stmtTest.Text + suffix;
                        var assignments = GetAssignments(text, stmtTest.Version);
                        string expected;
                        if (assignments.Length > 0 && stmtTest.Text != "del x") {
                            expected = String.Format(
                                "{1}def g():\r\n{0}\r\n    return {3}\r\n\r\n{3} = g(){2}",
                                TestExpressions.IndentCode(stmtTest.Text, "    "),
                                prefix,
                                suffix,
                                String.Join(", ", assignments)
                            );
                        } else {
                            expected = String.Format(
                                "{1}def g():\r\n{0}\r\n\r\ng(){2}",
                                TestExpressions.IndentCode(stmtTest.Text, "    "),
                                prefix,
                                suffix
                            );
                        }

                        await SuccessTest(new Span(prefix.Length, stmtTest.Text.Length), text, expected, null, stmtTest.Version.ToVersion());
                    }
                }
            }
        }

        private string[] GetAssignments(string testCase, PythonLanguageVersion version) {
            var ast = Parser.CreateParser(new StringReader(testCase), version).ParseFile();
            var walker = new TestAssignmentWalker();
            ast.Walk(walker);
            return walker._names.ToArray();
        }

        class TestAssignmentWalker : AssignmentWalker {
            private readonly NameWalker _walker;
            internal readonly List<string> _names = new List<string>();

            public TestAssignmentWalker() {
                _walker = new NameWalker(this);
            }

            public override AssignedNameWalker Define {
                get { return _walker; }
            }

            class NameWalker : AssignedNameWalker {
                private readonly TestAssignmentWalker _outer;
                public NameWalker(TestAssignmentWalker outer) {
                    _outer = outer;
                }

                public override bool Walk(NameExpression node) {
                    _outer._names.Add(node.Name);
                    return true;
                }
            }

            public override bool Walk(FunctionDefinition node) {
                _names.Add(node.Name);
                return base.Walk(node);
            }

            public override bool Walk(ClassDefinition node) {
                _names.Add(node.Name);
                return base.Walk(node);
            }

            public override bool Walk(ImportStatement node) {
                var vars = node.Variables;
                for (int i = 0; i < vars.Length; i++) {
                    if (vars[i] != null) {
                        _names.Add(vars[i].Name);
                    }
                }
                return base.Walk(node);
            }

            public override bool Walk(FromImportStatement node) {
                var vars = node.Variables;
                for (int i = 0; i < vars.Length; i++) {
                    if (vars[i] != null) {
                        _names.Add(vars[i].Name);
                    }
                }

                return base.Walk(node);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public async Task TestExtractDefiniteAssignmentAfterStmtListMultipleAssign() {
            await SuccessTest("x = 100; x = 200",
@"def f():
    x = 100; x = 200; x
    
    for x, y in []:
        print (x, y)",
@"def g():
    x = 100; x = 200
    return x

def f():
    x = g(); x
    
    for x, y in []:
        print (x, y)");
        }



        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public async Task TestExtractFromClass() {
            await ExtractMethodTest(
@"class C:
    abc = 42
    oar = 100", "abc .. 100", TestResult.Error(ErrorExtractFromClass));
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public async Task TestExtractSuiteWhiteSpace() {
            await SuccessTest("x .. 200",
@"def f():


    x = 100 
    y = 200",
@"def g():
    x = 100 
    y = 200

def f():


    g()");

            await SuccessTest("x .. 200",
@"def f():
    a = 300

    x = 100 
    y = 200",
@"def g():
    x = 100 
    y = 200

def f():
    a = 300

    g()");
        }

        /// <summary>
        /// Test cases that verify we correctly identify when not all paths contain return statements.
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public async Task TestNotAllCodePathsReturn() {
            await TestMissingReturn("for i .. 23", @"def f(x):
    for i in xrange(100):
        break
        return 42
    else:
        return 23
");


            await TestMissingReturn("if x .. Exception()", @"def f(x):
    if x:
        return 42
    elif x:
        raise Exception()
");

            await TestMissingReturn("if x .. 200", @"def f(x):
    if x:
        def abc():
             return 42
    elif x:
        return 100
    else:
        return 200
");

            await TestMissingReturn("if x .. 100", @"def f(x):
    if x:
        return 42
    elif x:
        return 100
");

            await TestMissingReturn("if x .. pass", @"def f(x):
    if x:
        return 42
    elif x:
        return 100
    else:
        pass
");

            await TestMissingReturn("if True .. pass", @"def f():
    abc = 100
    if True:
        return 100
    else:
        pass
    print('hello')");

            await TestMissingReturn("if x .. aaa",
@"class C:
    def f(self):
        if x == 0:
            return aaa");
        }


        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public async Task TestReturnWithOutputVars() {
            await TestReturnWithOutputs("if x .. 100", @"def f(x):
    if x:
        x = 200
        return 42
    else:
        return 100
    print(x)
");
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public async Task TestCannotRefactorYield() {
            await TestBadYield("yield 42", @"def f(x):
    yield 42
");

            await TestBadYield("yield 42", @"def f(x):
    for i in xrange(100):
        yield 42
");
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public async Task TestContinueWithoutLoop() {
            await TestBadContinue("continue", @"def f(x):
    for i in xrange(100):
        continue
");
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public async Task TestBreakWithoutLoop() {
            await TestBadBreak("break", @"def f(x):
    for i in xrange(100):
        break
");
        }

        /// <summary>
        /// Test cases which make sure we have the right ranges for each statement when doing extract method
        /// and that we don't mess up the code before/after the statement.
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P2_FAILING)] // https://github.com/Microsoft/PTVS/issues/4088
        public async Task StatementTests() {
            await SuccessTest("b",
@"def f():
    return (a or
            b or 
            c)",
@"def g():
    return b

def f():
    return (a or
            g() or 
            c)");

            await SuccessTest("assert False",
@"x = 1

assert False

x = 2",
@"x = 1

def g():
    assert False

g()

x = 2");


            await SuccessTest("x += 2",
@"x = 1

x += 2

x = 2",
@"x = 1

def g():
    x += 2

g()

x = 2");

            await SuccessTest("x = 100",
@"x = 1

x = 100

x = 2",
@"x = 1

def g():
    x = 100

g()

x = 2");

            await SuccessTest("class C: pass",
@"x = 1

class C: pass

x = 2",
@"x = 1

def g():
    class C: pass
    return C

C = g()

x = 2");

            await SuccessTest("del fob",
@"x = 1

del fob

x = 2",
@"x = 1

def g():
    del fob

g()

x = 2");

            await SuccessTest("pass",
@"x = 1

pass

x = 2",
@"x = 1

def g():
    pass

g()

x = 2");

            await SuccessTest("def f(): pass",
@"x = 1

def f(): pass

x = 2",
@"x = 1

def g():
    def f(): pass
    return f

f = g()

x = 2");


            await SuccessTest("for .. pass",
@"x = 1

for i in xrange(100):
    pass

x = 2",
@"x = 1

def g():
    for i in xrange(100):
        pass
    return i

i = g()

x = 2");

            await SuccessTest("if True: .. pass",
@"x = 1

if True:
    pass

x = 2",
@"x = 1

def g():
    if True:
        pass

g()

x = 2");

            await SuccessTest("if True: .. pass",
@"x = 1

if True:
    42
else:
    pass

x = 2",
@"x = 1

def g():
    if True:
        42
    else:
        pass

g()

x = 2");

            await SuccessTest("if True: .. pass",
@"x = 1

if True:
    42
elif False:
    pass

x = 2",
@"x = 1

def g():
    if True:
        42
    elif False:
        pass

g()

x = 2");

            await SuccessTest("import sys",
@"x = 1

import sys

x = 2",
@"x = 1

def g():
    import sys
    return sys

sys = g()

x = 2");

            await SuccessTest("print 42",
@"x = 1

print 42

x = 2",
@"x = 1

def g():
    print 42

g()

x = 2");


            await SuccessTest("raise Exception()",
@"x = 1

raise Exception()

x = 2",
@"x = 1

def g():
    raise Exception()

g()

x = 2");

            await SuccessTest("return 100",
@"x = 1

return 100

x = 2",
@"x = 1

def g():
    return 100

return g()

x = 2");

            await SuccessTest("try: .. pass",
@"x = 1

try:
    42
except:
    pass

x = 2",
@"x = 1

def g():
    try:
        42
    except:
        pass

g()

x = 2");

            await SuccessTest("try: .. pass",
@"x = 1

try:
    42
finally:
    pass

x = 2",
@"x = 1

def g():
    try:
        42
    finally:
        pass

g()

x = 2");

            await SuccessTest("try: .. pass",
@"x = 1

try:
    42
except:
    100
else:
    pass

x = 2",
@"x = 1

def g():
    try:
        42
    except:
        100
    else:
        pass

g()

x = 2");

            await SuccessTest("while .. pass",
@"x = 1

while True:
    pass

x = 2",
@"x = 1

def g():
    while True:
        pass

g()

x = 2");

            await SuccessTest("while .. pass",
@"x = 1

while True:
    42
else:
    pass

x = 2",
@"x = 1

def g():
    while True:
        42
    else:
        pass

g()

x = 2");

            await SuccessTest("with .. pass",
@"x = 1

with abc:
    pass

x = 2",
@"x = 1

def g():
    with abc:
        pass

g()

x = 2");

            await SuccessTest("with .. pass",
@"x = 1

with abc as fob:
    pass

x = 2",
@"x = 1

def g():
    with abc as fob:
        pass

g()

x = 2");

            await SuccessTest("with .. (name)",
@"def f():
    name = 'hello'
    with open('Fob', 'rb') as f:
        print(name)
",
@"def g(name):
    with open('Fob', 'rb') as f:
        print(name)

def f():
    name = 'hello'
    g(name)
");

            await SuccessTest("x .. Oar()",
@"class C:
    def f():
        if True:
            pass
        else:
            pass
        x = Fob()
        y = Oar()",
@"def g():
    x = Fob()
    y = Oar()

class C:
    def f():
        if True:
            pass
        else:
            pass
        g()");
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public async Task ClassTests() {
            await SuccessTest("x = fob",
@"class C(object):
    '''Doc string'''

    def abc(self, fob):
        x = fob
        print(x)",
@"class C(object):
    '''Doc string'''

    def g(self, fob):
        x = fob
        return x

    def abc(self, fob):
        x = self.g(fob)
        print(x)", scopeName: "C");


            await SuccessTest("print(self.abc)",
@"class C:
    def f(self):
        print(self.abc)",
@"class C:
    def g(self):
        print(self.abc)

    def f(self):
        self.g()", scopeName: "C");

            await SuccessTest("print(self.abc, aaa)",
@"class C:
    def f(self):
        aaa = 42
        print(self.abc, aaa)",
@"class C:
    def g(self, aaa):
        print(self.abc, aaa)

    def f(self):
        aaa = 42
        self.g(aaa)", scopeName: "C");

            await SuccessTest("aaa = 42",
@"class C:
    def f(self):
        aaa = 42",
@"class C:
    def g(self):
        aaa = 42

    def f(self):
        self.g()", scopeName: "C");

            await SuccessTest("aaa = 42",
@"class C:
    @staticmethod
    def f():
        aaa = 42",
@"class C:
    @staticmethod
    def g():
        aaa = 42

    @staticmethod
    def f():
        C.g()", scopeName: "C");

            await SuccessTest("aaa = 42",
@"class C:
    @classmethod
    def f(cls):
        aaa = 42",
@"class C:
    @classmethod
    def g(cls):
        aaa = 42

    @classmethod
    def f(cls):
        cls.g()", scopeName: "C");

            await SuccessTest("aaa = 42",
@"class C:
    def f(weird):
        aaa = 42",
@"class C:
    def g(weird):
        aaa = 42

    def f(weird):
        weird.g()", scopeName: "C");

            await SuccessTest("print('hello')",
@"class C:
    class D:
        def f(self):
            print('hello')",
@"class C:
    class D:
        def g(self):
            print('hello')

        def f(self):
            self.g()", scopeName: "D");

        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public async Task TestComprehensions() {
            await SuccessTest("i % 2 == 0", @"def f():
    x = [i for i in range(100) if i % 2 == 0]", @"def g(i):
    return i % 2 == 0

def f():
    x = [i for i in range(100) if g(i)]");

            await SuccessTest("i % 2 == 0", @"def f():
    x = (i for i in range(100) if i % 2 == 0)", @"def g(i):
    return i % 2 == 0

def f():
    x = (i for i in range(100) if g(i))");

            await SuccessTest("i % 2 == 0", @"def f():
    x = {i for i in range(100) if i % 2 == 0}", @"def g(i):
    return i % 2 == 0

def f():
    x = {i for i in range(100) if g(i)}", version: new Version(3, 2));

            await SuccessTest("(k+v) % 2 == 0", @"def f():
    x = {k:v for k,v in range(100) if (k+v) % 2 == 0}", @"def g(k, v):
    return (k+v) % 2 == 0

def f():
    x = {k:v for k,v in range(100) if g(k, v)}", version: new Version(3, 2));
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public async Task SuccessfulTests() {
            await SuccessTest("x .. 100",
@"def f():
    z = 200
    x = z
    z = 42
    y = 100
    print(x, y)",
@"def g(z):
    x = z
    z = 42
    y = 100
    return x, y

def f():
    z = 200
    x, y = g(z)
    print(x, y)");

            await SuccessTest("x .. 100",
@"def f():
    x = 42
    y = 100
    print(x, y)",
@"def g():
    x = 42
    y = 100
    return x, y

def f():
    x, y = g()
    print(x, y)");

            await SuccessTest("42",
@"def f():
    x = 42",
@"def g():
    return 42

def f():
    x = g()");

            await SuccessTest("oar;baz",
@"def f():
    fob;oar;baz;quox",
@"def g():
    oar;baz

def f():
    fob;g();quox");

            await SuccessTest("x() .. = 100",
@"x = 42
while True:
    x()
    x = 100",
@"x = 42
def g(x):
    x()
    x = 100
    return x

while True:
    x = g(x)", parameters: new[] { "x" });


            await SuccessTest("x = 2 .. x)",
@"def f():
    x = 1
    x = 2
    print(x)",
@"def g():
    x = 2
    print(x)

def f():
    x = 1
    g()");

            await SuccessTest("for i in .. return 42",
@"def f():
    for i in xrange(100):
        break
        return 42",
@"def g():
    for i in xrange(100):
        break
        return 42

def f():
    g()");

            await SuccessTest("if x .. 100",
@"def f(x):
    if x:
        return 42
    return 100",
@"def g(x):
    if x:
        return 42
    return 100

def f(x):
    return g(x)");

            await SuccessTest("if x .. 200",
@"def f(x):
    if x:
        return 42
    elif x:
        return 100
    else:
        return 200",
@"def g(x):
    if x:
        return 42
    elif x:
        return 100
    else:
        return 200

def f(x):
    return g(x)");

            await SuccessTest("if x .. 200",
@"def f(x):
    if x:
        return 42
    elif x:
        raise Exception()
    else:
        return 200",
@"def g(x):
    if x:
        return 42
    elif x:
        raise Exception()
    else:
        return 200

def f(x):
    return g(x)");

            await SuccessTest("if x .. Exception()",
@"def f(x):
    if x:
        return 42
    else:
        raise Exception()",
@"def g(x):
    if x:
        return 42
    else:
        raise Exception()

def f(x):
    return g(x)");

            await SuccessTest("print(x)",
@"def f():
    x = 1
    print(x)",
@"def g(x):
    print(x)

def f():
    x = 1
    g(x)");

            await SuccessTest("x = 2 .. x)",
@"def f():
    x = 1
    x = 2
    print(x)",
@"def g():
    x = 2
    print(x)

def f():
    x = 1
    g()");

            await SuccessTest("class C: pass",
@"def f():
    class C: pass
    print C",
@"def g():
    class C: pass
    return C

def f():
    C = g()
    print C");

            await SuccessTest("def x(): pass",
@"def f():
    def x(): pass
    print x",
@"def g():
    def x(): pass
    return x

def f():
    x = g()
    print x");

            await SuccessTest("import sys",
@"def f():
    import sys
    print sys",
@"def g():
    import sys
    return sys

def f():
    sys = g()
    print sys");

            await SuccessTest("import sys as oar",
@"def f():
    import sys as oar
    print oar",
@"def g():
    import sys as oar
    return oar

def f():
    oar = g()
    print oar");

            await SuccessTest("from sys import oar",
@"def f():
    from sys import oar
    print oar",
@"def g():
    from sys import oar
    return oar

def f():
    oar = g()
    print oar");

            await SuccessTest("from sys import oar as baz",
@"def f():
    from sys import oar as baz
    print baz",
@"def g():
    from sys import oar as baz
    return baz

def f():
    baz = g()
    print baz");


            await SuccessTest("return 42",
@"def f():
    return 42",
@"def g():
    return 42

def f():
    return g()");

            await SuccessTest("return x",
@"def f():
    x = 42
    return x",
@"def g(x):
    return x

def f():
    x = 42
    return g(x)");

            await SuccessTest("x = .. = 100",
@"def f():
    x = 42
    y = 100
    return x, y",
@"def g():
    x = 42
    y = 100
    return x, y

def f():
    x, y = g()
    return x, y");

            await SuccessTest("x()",
@"x = 42
while True:
    x()
    x = 100",
@"x = 42
def g(x):
    return x()

while True:
    g(x)
    x = 100",
            parameters: new[] { "x" });

            await SuccessTest("x()",
@"x = 42
while True:
    x()
    x = 100",
@"x = 42
def g():
    return x()

while True:
    g()
    x = 100");

            await SuccessTest("x = 42",
@"x = 42
print(x)",
@"def g():
    x = 42
    return x

x = g()
print(x)");

            await SuccessTest("l = .. return r",
@"def f():
    r = None
    l = fob()
    if l:
        r = l[0]
    return r",
@"def g(r):
    l = fob()
    if l:
        r = l[0]
    return r

def f():
    r = None
    return g(r)");

            await SuccessTest("42",
@"def f(x):
    return (42)",
@"def g():
    return 42

def f(x):
    return (g())");
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public async Task ExtractAsyncFunction() {
            // Ensure extracted bodies that use await generate async functions

            var V35 = new Version(3, 5);
            await SuccessTest("x",
@"async def f():
    return await x",
@"def g():
    return x

async def f():
    return await g()", version: V35);

            await SuccessTest("await x",
@"async def f():
    return await x",
@"async def g():
    return await x

async def f():
    return await g()", version: V35);
        }

        private Task SuccessTest(Span extract, string input, string result, string scopeName = null, Version version = null, string[] parameters = null) {
            return ExtractMethodTest(input, extract, TestResult.Success(result), scopeName: scopeName, version: version, parameters: parameters);
        }

        private Task SuccessTest(string extract, string input, string result, string scopeName = null, Version version = null, string[] parameters = null) {
            return ExtractMethodTest(input, extract, TestResult.Success(result), scopeName: scopeName, version: version, parameters: parameters);
        }


        class TestResult {
            public readonly bool IsError;
            public readonly string Text;

            public static TestResult Error(string message) {
                return new TestResult(message, true);
            }

            public static TestResult Success(string code) {
                return new TestResult(code, false);
            }

            public TestResult(string text, bool isError) {
                Text = text;
                IsError = isError;
            }
        }

        private Task TestMissingReturn(string extract, string input) {
            return ExtractMethodTest(input, extract, TestResult.Error(ErrorReturn));
        }

        private Task TestReturnWithOutputs(string extract, string input) {
            return ExtractMethodTest(input, extract, TestResult.Error(ErrorReturnWithOutputs));
        }

        private Task TestBadYield(string extract, string input) {
            return ExtractMethodTest(input, extract, TestResult.Error(ErrorYield));
        }

        private Task TestBadContinue(string extract, string input) {
            return ExtractMethodTest(input, extract, TestResult.Error(ErrorContinue));
        }

        private Task TestBadBreak(string extract, string input) {
            return ExtractMethodTest(input, extract, TestResult.Error(ErrorBreak));
        }

        private Task ExtractMethodTest(string input, object extract, TestResult expected, string scopeName = null, string targetName = "g", Version version = null, params string[] parameters) {
            Func<Span> textRange = () => GetSelectionSpan(input, extract);
            return ExtractMethodTest(input, textRange, expected, scopeName, targetName, version, parameters);
        }

        internal static Span GetSelectionSpan(string input, object extract) {
            string exStr = extract as string;
            if (exStr != null) {
                if (exStr.IndexOf(" .. ") != -1) {
                    var pieces = exStr.Split(new[] { " .. " }, 2, StringSplitOptions.None);
                    int start = input.IndexOf(pieces[0]);
                    int end = input.IndexOf(pieces[1], start) + pieces[1].Length;
                    return Span.FromBounds(start, end);
                } else {
                    int start = input.IndexOf(exStr);
                    int length = exStr.Length;
                    return new Span(start, length);
                }
            }
            return (Span)extract;
        }

        private async Task ExtractMethodTest(string input, Func<Span> extract, TestResult expected, string scopeName = null, string targetName = "g", Version version = null, params string[] parameters) {
            var fact = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(version ?? new Version(2, 7));

            var editorTestToolset = new EditorTestToolset().WithPythonToolsService();
            var services = editorTestToolset.GetPythonEditorServices();
            using (var analyzer = await VsProjectAnalyzer.CreateForTestsAsync(services, fact)) {
                var analysisStartedTask = EventTaskSources.VsProjectAnalyzer.AnalysisStarted.Create(analyzer);
                var buffer = editorTestToolset.CreatePythonTextBuffer(input, Path.Combine(TestData.GetTempPath(), "fob.py"), analyzer);
                var view = editorTestToolset.CreateTextView(buffer);
                await analysisStartedTask;

                var bi = services.GetBufferInfo(buffer);
                bi.ParseImmediately = true;
                var entry = await analyzer.AnalyzeFileAsync(bi.DocumentUri, bi.Filename);
                Assert.AreEqual(entry, bi.TrySetAnalysisEntry(entry, null));
                var bp = entry.GetOrCreateBufferParser(services);
                bp.AddBuffer(buffer);
                await bp.EnsureCodeSyncedAsync(bi.Buffer, true);

                var extractInput = new ExtractMethodTestInput(true, scopeName, targetName, parameters ?? new string[0]);
                await editorTestToolset.UIThread.InvokeTask(() => {
                    view.Selection.Select(new SnapshotSpan(view.TextBuffer.CurrentSnapshot, extract()), false);
                    return new Microsoft.PythonTools.Refactoring.MethodExtractor(services, view).ExtractMethod(extractInput);
                });

                if (expected.IsError) {
                    Assert.AreEqual(expected.Text, extractInput.FailureReason);
                    Assert.AreEqual(input, view.TextBuffer.CurrentSnapshot.GetText());
                } else {
                    Assert.AreEqual(null, extractInput.FailureReason);
                    Assert.AreEqual(expected.Text, view.TextBuffer.CurrentSnapshot.GetText());
                }
            }
        }

        class ExtractMethodTestInput : IExtractMethodInput {
            private readonly bool _shouldExpand;
            private readonly string _scopeName, _targetName;
            private readonly string[] _parameters;
            private string _failureReason;

            public ExtractMethodTestInput(bool shouldExpand, string scopeName, string targetName, string[] parameters) {
                _shouldExpand = shouldExpand;
                _scopeName = scopeName;
                _parameters = parameters;
                _targetName = targetName;
            }

            public bool ShouldExpandSelection() {
                return _shouldExpand;
            }

            public ExtractMethodRequest GetExtractionInfo(ExtractedMethodCreator previewer) {
                AP.ScopeInfo scope = null;
                if (_scopeName == null) {
                    scope = previewer.LastExtraction.scopes[0];
                } else {
                    foreach (var foundScope in previewer.LastExtraction.scopes) {
                        if (foundScope.name == _scopeName) {
                            scope = foundScope;
                            break;
                        }
                    }
                }

                Assert.AreNotEqual(null, scope);
                var requestView = new ExtractMethodRequestView(PythonToolsTestUtilities.CreateMockServiceProvider(), previewer);
                requestView.TargetScope = requestView.TargetScopes.Single(s => s.Scope == scope);
                requestView.Name = _targetName;
                foreach (var cv in requestView.ClosureVariables) {
                    cv.IsClosure = !_parameters.Contains(cv.Name);
                }
                Assert.IsTrue(requestView.IsValid);
                var request = requestView.GetRequest();
                Assert.IsNotNull(request);
                return request;
            }

            public void CannotExtract(string reason) {
                _failureReason = reason;
            }

            public string FailureReason {
                get {
                    return _failureReason;
                }
            }
        }
    }
}

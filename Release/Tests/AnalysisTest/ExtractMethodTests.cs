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
using AnalysisTest.Mocks;
using Microsoft.IronPythonTools.Interpreter;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.PythonTools.Refactoring;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;

namespace AnalysisTest {
    [TestClass]
    public class ExtractMethodTests {
        private const string ErrorReturn = "When the selection contains a return statement, all code paths must be terminated by a return statement too.";
        private const string ErrorYield = "Cannot extract code containing \"yield\" expression";
        private const string ErrorContinue = "The selection contains a \"continue\" statement, but not the enclosing loop";
        private const string ErrorBreak = "The selection contains a \"break\" statement, but not the enclosing loop";
        private const string ErrorReturnWithOutputs = "Cannot extract method that assigns to variables and returns";
        private const string ErrorImportStar = "Cannot extract method containing from ... import * statement";
        private const string ErrorExtractFromClass = "Cannot extract statements from a class definition";
        private const string ErrorExtractParameter = "Cannot extract parameter name";

        [TestMethod]
        public void TestFromImportStar() {
            ExtractMethodTest(
@"def f():
    from sys import *", "from sys import *", TestResult.Error(ErrorImportStar));
        }

        [TestMethod]
        public void TestExtractParameter() {
            ExtractMethodTest(
@"def f(x):
    pass", "x", TestResult.Error(ErrorExtractParameter));
        }
        

        [TestMethod]
        public void TestExtractFromClass() {
            ExtractMethodTest(
@"class C:
    abc = 42
    bar = 100", "abc .. 100", TestResult.Error(ErrorExtractFromClass));
        }

        /// <summary>
        /// Test cases that verify we correctly identify when not all paths contain return statements.
        /// </summary>
        [TestMethod]
        public void TestNotAllCodePathsReturn() {            
            TestMissingReturn("for i .. 23", @"def f(x):
    for i in xrange(100):
        break
        return 42
    else:
        return 23
");

            
            TestMissingReturn("if x .. Exception()", @"def f(x):
    if x:
        return 42
    elif x:
        raise Exception()
");

            TestMissingReturn("if x .. 200", @"def f(x):
    if x:
        def abc():
             return 42
    elif x:
        return 100
    else:
        return 200
");

            TestMissingReturn("if x .. 100", @"def f(x):
    if x:
        return 42
    elif x:
        return 100
");

            TestMissingReturn("if x .. pass", @"def f(x):
    if x:
        return 42
    elif x:
        return 100
    else:
        pass
");

            TestMissingReturn("if True .. pass", @"def f():
    abc = 100
    if True:
        return 100
    else:
        pass
    print('hello')");

            TestMissingReturn("if x .. aaa",
@"class C:
    def f(self):
        if x == 0:
            return aaa");
        }


        [TestMethod]
        public void TestReturnWithOutputVars() {
            TestReturnWithOutputs("if x .. 100", @"def f(x):
    if x:
        x = 200
        return 42
    else:
        return 100
    print(x)
");
        }

        [TestMethod]
        public void TestCannotRefactorYield() {
            TestBadYield("yield 42", @"def f(x):
    yield 42
");

            TestBadYield("yield 42", @"def f(x):
    for i in xrange(100):
        yield 42
");
        }

        [TestMethod]
        public void TestContinueWithoutLoop() {
            TestBadContinue("continue", @"def f(x):
    for i in xrange(100):
        continue
");
        }

        [TestMethod]
        public void TestBreakWithoutLoop() {
            TestBadBreak("break", @"def f(x):
    for i in xrange(100):
        break
");
        }

        /// <summary>
        /// Test cases which make sure we have the right ranges for each statement when doing extract method
        /// and that we don't mess up the code before/after the statement.
        /// </summary>
        [TestMethod]
        public void StatementTests() {

            SuccessTest("assert False",
@"x = 1

assert False

x = 2",
@"x = 1

def g():
    assert False

g()

x = 2");


            SuccessTest("x += 2",
@"x = 1

x += 2

x = 2",
@"x = 1

def g():
    x += 2

g()

x = 2");

            SuccessTest("x = 100",
@"x = 1

x = 100

x = 2",
@"x = 1

def g():
    x = 100

g()

x = 2");

            SuccessTest("class C: pass",
@"x = 1

class C: pass

x = 2",
@"x = 1

def g():
    class C: pass

g()

x = 2");

            SuccessTest("del foo",
@"x = 1

del foo

x = 2",
@"x = 1

def g():
    del foo

g()

x = 2");

            SuccessTest("pass",
@"x = 1

pass

x = 2",
@"x = 1

def g():
    pass

g()

x = 2");

            SuccessTest("def f(): pass",
@"x = 1

def f(): pass

x = 2",
@"x = 1

def g():
    def f(): pass

g()

x = 2");


            SuccessTest("for .. pass",
@"x = 1

for i in xrange(100):
    pass

x = 2",
@"x = 1

def g():
    for i in xrange(100):
        pass

g()

x = 2");

            SuccessTest("if True: .. pass",
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

            SuccessTest("if True: .. pass",
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

            SuccessTest("if True: .. pass",
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

            SuccessTest("import sys",
@"x = 1

import sys

x = 2",
@"x = 1

def g():
    import sys

g()

x = 2");

            SuccessTest("print 42",
@"x = 1

print 42

x = 2",
@"x = 1

def g():
    print 42

g()

x = 2");


            SuccessTest("raise Exception()",
@"x = 1

raise Exception()

x = 2",
@"x = 1

def g():
    raise Exception()

g()

x = 2");

            SuccessTest("return 100",
@"x = 1

return 100

x = 2",
@"x = 1

def g():
    return 100

return g()

x = 2");

            SuccessTest("try: .. pass",
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

            SuccessTest("try: .. pass",
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

            SuccessTest("try: .. pass",
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

            SuccessTest("while .. pass",
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

            SuccessTest("while .. pass",
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

            SuccessTest("with .. pass",
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

            SuccessTest("with .. pass",
@"x = 1

with abc as foo:
    pass

x = 2",
@"x = 1

def g():
    with abc as foo:
        pass

g()

x = 2");

            SuccessTest("x .. Bar()",
@"class C:
    def f():
        if True:
            pass
        else:
            pass
        x = Foo()
        y = Bar()",
@"def g():
    x = Foo()
    y = Bar()

class C:
    def f():
        if True:
            pass
        else:
            pass
        g()");
        }

        [TestMethod]
        public void ClassTests() {
            SuccessTest("print(self.abc)",
@"class C:
    def f(self):
        print(self.abc)",
@"class C:
    def g(self):
        print(self.abc)

    def f(self):
        self.g()", scopeName:"C");

            SuccessTest("print(self.abc, aaa)",
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

            SuccessTest("aaa = 42",
@"class C:
    def f(self):
        aaa = 42",
@"class C:
    def g(self):
        aaa = 42

    def f(self):
        self.g()", scopeName: "C");

            SuccessTest("aaa = 42",
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

            SuccessTest("aaa = 42",
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

            SuccessTest("aaa = 42",
@"class C:
    def f(weird):
        aaa = 42",
@"class C:
    def g(weird):
        aaa = 42

    def f(weird):
        weird.g()", scopeName: "C");

            SuccessTest("print('hello')",
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

        [TestMethod]
        public void SuccessfulTests() {
            SuccessTest("x .. 100",
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

            SuccessTest("x .. 100",
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

            SuccessTest("42",
@"def f():
    x = 42",
@"def g():
    return 42

def f():
    x = g()");

            SuccessTest("bar;baz",
@"def f():
    foo;bar;baz;quox",
@"def g():
    bar;baz

def f():
    foo;g();quox");

            SuccessTest("x() .. = 100",
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


            SuccessTest("x = 2 .. x)", 
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

            SuccessTest("for i in .. return 42",
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

            SuccessTest("if x .. 100",
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

            SuccessTest("if x .. 200",
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

            SuccessTest("if x .. 200",
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

            SuccessTest("if x .. Exception()",
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

            SuccessTest("print(x)",
@"def f():
    x = 1
    print(x)",
@"def g(x):
    print(x)

def f():
    x = 1
    g(x)");

            SuccessTest("x = 2 .. x)",
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

            SuccessTest("class C: pass",
@"def f():
    class C: pass
    print C",
@"def g():
    class C: pass
    return C

def f():
    C = g()
    print C");

            SuccessTest("def x(): pass",
@"def f():
    def x(): pass
    print x",
@"def g():
    def x(): pass
    return x

def f():
    x = g()
    print x");

            SuccessTest("import sys",
@"def f():
    import sys
    print sys",
@"def g():
    import sys
    return sys

def f():
    sys = g()
    print sys");

            SuccessTest("import sys as bar",
@"def f():
    import sys as bar
    print bar",
@"def g():
    import sys as bar
    return bar

def f():
    bar = g()
    print bar");

            SuccessTest("from sys import bar",
@"def f():
    from sys import bar
    print bar",
@"def g():
    from sys import bar
    return bar

def f():
    bar = g()
    print bar");

            SuccessTest("from sys import bar as baz",
@"def f():
    from sys import bar as baz
    print baz",
@"def g():
    from sys import bar as baz
    return baz

def f():
    baz = g()
    print baz");


            SuccessTest("return 42",
@"def f():
    return 42",
@"def g():
    return 42

def f():
    return g()");

            SuccessTest("return x",
@"def f():
    x = 42
    return x",
@"def g(x):
    return x

def f():
    x = 42
    return g(x)");

            SuccessTest("x = .. = 100",
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

            SuccessTest("x()",
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
            parameters: new[] { "x"});

            SuccessTest("x()",
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

            SuccessTest("x = 42",
@"x = 42
print(x)",
@"def g():
    x = 42
    return x

x = g()
print(x)");

            SuccessTest("l = .. return r",
@"def f():
    r = None
    l = foo()
    if l:
        r = l[0]
    return r",
@"def g(r):
    l = foo()
    if l:
        r = l[0]
    return r

def f():
    r = None
    return g(r)");

            SuccessTest("42",
@"def f(x):
    return (42)",
@"def g():
    return 42

def f(x):
    return (g())");
        }

        private void SuccessTest(string extract, string input, string result, string scopeName = null, string[] parameters = null) {
            ExtractMethodTest(input, extract, TestResult.Success(result), scopeName: scopeName, parameters: parameters);
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

        private void TestMissingReturn(string extract, string input) {
            ExtractMethodTest(input, extract, TestResult.Error(ErrorReturn));
        }

        private void TestReturnWithOutputs(string extract, string input) {
            ExtractMethodTest(input, extract, TestResult.Error(ErrorReturnWithOutputs));
        }

        private void TestBadYield(string extract, string input) {
            ExtractMethodTest(input, extract, TestResult.Error(ErrorYield));
        }

        private void TestBadContinue(string extract, string input) {
            ExtractMethodTest(input, extract, TestResult.Error(ErrorContinue));
        }

        private void TestBadBreak(string extract, string input) {
            ExtractMethodTest(input, extract, TestResult.Error(ErrorBreak));
        }

        private void ExtractMethodTest(string input, string extract, TestResult expected, string scopeName = null, string targetName = "g", params string[] parameters) {
            var analyzer = new ProjectAnalyzer(new IronPythonInterpreterFactory(), new MockErrorProviderFactory());
            var buffer = new MockTextBuffer(input);
            var view = new MockTextView(buffer);
            buffer.AddProperty(typeof(ProjectAnalyzer), analyzer);
            var extractInput = new ExtractMethodTestInput(true, scopeName, targetName, parameters ?? new string[0]);
            
            if (extract.IndexOf(" .. ") != -1) {
                var pieces = extract.Split(new[] { " .. " }, 2, StringSplitOptions.None);
                int start = input.IndexOf(pieces[0]);
                int end = input.IndexOf(pieces[1]) + pieces[1].Length - 1;
                view.Selection.Select(
                    new SnapshotSpan(view.TextBuffer.CurrentSnapshot, Span.FromBounds(start, end)),
                    false
                );
            } else {
                int start = input.IndexOf(extract);
                int length = extract.Length;
                view.Selection.Select(
                    new SnapshotSpan(view.TextBuffer.CurrentSnapshot, new Span(start, length)),
                    false
                );
            }

            new MethodExtractor(view).ExtractMethod(extractInput);

            if (expected.IsError) {
                Assert.AreEqual(expected.Text, extractInput.FailureReason);
                Assert.AreEqual(input, view.TextBuffer.CurrentSnapshot.GetText());
            } else {
                Assert.AreEqual(null, extractInput.FailureReason);
                Assert.AreEqual(expected.Text, view.TextBuffer.CurrentSnapshot.GetText());
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
                ScopeStatement scope = null;
                if (_scopeName == null) {
                    scope = previewer.Scopes[0];
                } else {
                    foreach (var foundScope in previewer.Scopes) {
                        if (foundScope.Name == _scopeName) {
                            scope = foundScope;
                            break;
                        }
                    }
                }

                Assert.AreNotEqual(null, scope);
                return new ExtractMethodRequest(scope, _targetName, _parameters);
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

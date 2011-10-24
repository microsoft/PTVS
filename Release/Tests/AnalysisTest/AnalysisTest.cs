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
using System.Reflection;
using System.Text;
using IronPython.Runtime;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.PythonTools.Parsing;
using Microsoft.Scripting.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace AnalysisTest {
    [TestClass]
    [DeploymentItem(@"..\\PythonTools\\CompletionDB\\", "CompletionDB")]
    [DeploymentItem("PyDebugAttach.dll")]
    public partial class AnalysisTest : BaseAnalysisTest {
        public AnalysisTest() {
        }

        public AnalysisTest(IPythonInterpreter interpreter)
            : base(interpreter) {
        }

        public static int Main(string[] args) {
            int res = 0;
            Type attr = typeof(TestMethodAttribute);
            if (args.Length > 0 && args[0] == "PERF") {
                args = ArrayUtils.ShiftLeft(args, 1);
                attr = typeof(PerfMethodAttribute);
            }
            foreach (var type in Assembly.GetExecutingAssembly().GetExportedTypes()) {
                if (type.IsDefined(typeof(TestClassAttribute), false)) {
                    Console.WriteLine("Running tests against: {0}", type.FullName);

                    res += RunTests(type, args, attr);
                }
            }
            return res;
        }

        private static int RunTests(Type instType, string[] args, Type testAttr) {            
            var fg = Console.ForegroundColor;
            int failures = 0;
            object inst = null;

            foreach (var mi in instType.GetMethods()) {                
                if ((args.Length == 0 || (args.Length > 0 && args.Contains(mi.Name))) &&
                    mi.IsDefined(testAttr, false)) {

                    if (inst == null) {
                        inst = Activator.CreateInstance(instType);
                    }
                    try {
                        mi.Invoke(inst, new object[0]);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Test passed: {0}", mi.Name);
                    } catch (Exception e) {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Test failed: {0}", mi.Name);
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.WriteLine(e);
                        failures++;
                    }
                }
            }

            Console.WriteLine();
            if (failures == 0) {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("No failures");
            } else {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("{0} failures", failures);
            }
            Console.ForegroundColor = fg;
            return failures;
        }

        #region Test Cases

        [TestMethod]
        public void TestInterpreter() {
            Assert.AreEqual(Interpreter.GetBuiltinType((BuiltinTypeId)(-1)), null);
            Assert.IsTrue(Interpreter.GetBuiltinType(BuiltinTypeId.Int).ToString() != "");
        }

        [TestMethod]
        public void TestSpecialArgTypes() {
            var code = @"def f(*foo, **bar):
    pass
";
            var entry = ProcessText(code);

            AssertContainsExactly(entry.GetTypesFromName("foo", GetLineNumber(code, "pass")), Interpreter.GetBuiltinType(BuiltinTypeId.Tuple));
            AssertContainsExactly(entry.GetTypesFromName("bar", GetLineNumber(code, "pass")), Interpreter.GetBuiltinType(BuiltinTypeId.Dict));

            code = @"def f(*foo):
    pass

f(42)
";
            entry = ProcessText(code);

            AssertContainsExactly(entry.GetTypesFromName("foo", GetLineNumber(code, "pass")), Interpreter.GetBuiltinType(BuiltinTypeId.Tuple));
            AssertContainsExactly(entry.GetValues("foo[0]", GetLineNumber(code, "pass")).Select(x => x.PythonType), Interpreter.GetBuiltinType(BuiltinTypeId.Int));

            code = @"def f(*foo):
    pass

f(42, 'abc')
";
            entry = ProcessText(code);

            AssertContainsExactly(entry.GetTypesFromName("foo", GetLineNumber(code, "pass")), Interpreter.GetBuiltinType(BuiltinTypeId.Tuple));
            AssertContainsExactly(entry.GetValues("foo[0]", GetLineNumber(code, "pass")).Select(x => x.PythonType), Interpreter.GetBuiltinType(BuiltinTypeId.Int), Interpreter.GetBuiltinType(BuiltinTypeId.Bytes));

            code = @"def f(**bar):
    pass

f(x=42)
";
            entry = ProcessText(code);

            AssertContainsExactly(entry.GetTypesFromName("bar", GetLineNumber(code, "pass")), Interpreter.GetBuiltinType(BuiltinTypeId.Dict));
            AssertContainsExactly(entry.GetValues("bar['foo']", GetLineNumber(code, "pass")).Select(x => x.PythonType), Interpreter.GetBuiltinType(BuiltinTypeId.Int));


            code = @"def f(**bar):
    pass

f(x=42, y = 'abc')
";
            entry = ProcessText(code);

            AssertContainsExactly(entry.GetTypesFromName("bar", GetLineNumber(code, "pass")), Interpreter.GetBuiltinType(BuiltinTypeId.Dict));
            AssertContainsExactly(entry.GetValues("bar['foo']", GetLineNumber(code, "pass")).Select(x => x.PythonType), Interpreter.GetBuiltinType(BuiltinTypeId.Int), Interpreter.GetBuiltinType(BuiltinTypeId.Bytes));
        }

        [TestMethod]
        public void TestImportAs() {
            var entry = ProcessText(@"import sys as s, array as a");

            AssertContains(entry.GetMembers("s", 1).Select(x => x.Name), "winver");
            AssertContains(entry.GetMembers("a", 1).Select(x => x.Name), "ArrayType");

            entry = ProcessText(@"import sys as s");
            AssertContains(entry.GetMembers("s", 1).Select(x => x.Name), "winver");
        }

        [TestMethod]
        public void TestImportStar() {
            var entry = ProcessText(@"
from nt import *
            ");

            var members = entry.GetMembers("", 1).Select(x => x.Name);

            AssertContains(members, "abort");

            entry = ProcessText(@"");

            // make sure abort hasn't become a builtin, if so this test needs to be updated
            // with a new name
            members = entry.GetMembers("", 1).Select(x => x.Name);
            foreach (var member in members) {
                if(member == "abort") {
                    Assert.Fail("abort has become a builtin, or a clean module includes it for some reason");
                }
            }
        }

        [TestMethod]
        public void ImportStarCorrectRefs() {
            var state = new PythonAnalyzer(Interpreter, PythonLanguageVersion.V27);

            var text1 = @"
from mod2 import *

a = D()
";

            var text2 = @"
class D(object):
    pass
";

            var mod1 = state.AddModule("mod1", "mod1", null);
            Prepare(mod1, GetSourceUnit(text1, "mod1"));
            var mod2 = state.AddModule("mod2", "mod2", null);
            Prepare(mod2, GetSourceUnit(text2, "mod2"));

            mod1.Analyze(true);
            mod2.Analyze(true);
            mod1.AnalysisGroup.AnalyzeQueuedEntries();

            VerifyReferences(
                UniqifyVariables(mod2.Analysis.GetVariables("D", GetLineNumber(text2, "class D"))),
                new VariableLocation(2, 7, VariableType.Definition),
                new VariableLocation(4, 5, VariableType.Reference)
            );
        }


        [TestMethod]
        public void TestMutatingReferences() {
            var state = new PythonAnalyzer(Interpreter, PythonLanguageVersion.V27);

            var text1 = @"
import mod2

class C(object):
    def SomeMethod(self):
        pass

mod2.D(C())
";

            var text2 = @"
class D(object):
    def __init__(self, value):
        self.value = value
        self.value.SomeMethod()
";

            var mod1 = state.AddModule("mod1", "mod1", null);
            Prepare(mod1, GetSourceUnit(text1, "mod1"));
            var mod2 = state.AddModule("mod2", "mod2", null);
            Prepare(mod2, GetSourceUnit(text2, "mod2"));

            mod1.Analyze();
            mod2.Analyze();


            VerifyReferences(UniqifyVariables(mod1.Analysis.GetVariables("SomeMethod", GetLineNumber(text1, "SomeMethod"))), 
                new VariableLocation(5, 9, VariableType.Definition), new VariableLocation(5, 20, VariableType.Reference));

            // mutate 1st file
            text1 = text1.Substring(0, text1.IndexOf("    def")) + Environment.NewLine + text1.Substring(text1.IndexOf("    def"));
            Prepare(mod1, GetSourceUnit(text1, "mod1"));
            mod1.Analyze();

            VerifyReferences(UniqifyVariables(mod1.Analysis.GetVariables("SomeMethod", GetLineNumber(text1, "SomeMethod"))),
                new VariableLocation(6, 9, VariableType.Definition), new VariableLocation(5, 20, VariableType.Reference));

            // mutate 2nd file
            text2 = Environment.NewLine + text2;
            Prepare(mod2, GetSourceUnit(text2, "mod1"));
            mod2.Analyze();

            VerifyReferences(UniqifyVariables(mod1.Analysis.GetVariables("SomeMethod", GetLineNumber(text1, "SomeMethod"))),
                new VariableLocation(6, 9, VariableType.Definition), new VariableLocation(6, 20, VariableType.Reference));

        }

        [TestMethod]
        public void TestPrivateMembers() {
            string code = @"
class C:
    def __init__(self):
        self._C__X = 'abc'	# Completions here should only ever show __X
        self.__X = 42

class D(C):
    def __init__(self):        
        print(self)		# self. here shouldn't have __X or _C__X (could be controlled by Text Editor->Python->general->Hide advanced members to show _C__X)
";
            var entry = ProcessText(code);

            AssertContainsExactly(entry.GetMembersFromName("self", GetLineNumber(code, "_C__X")), "__X", "__init__", "__doc__", "__class__");
            AssertContainsExactly(entry.GetMembersFromName("self", GetLineNumber(code, "print")), "_C__X", "__init__", "__doc__", "__class__");

            code = @"
class C(object):
    def __init__(self):
        self.f(_C__A = 42)		# sig help should be _C__A
    
    def f(self, __A):
        pass


class D(C):
    def __init__(self):
        marker
        self.f(_C__A=42)		# sig help should be _C__A
";
            entry = ProcessText(code);

            AssertEquals(
                entry.GetSignatures("self.f", GetLineNumber(code, "self.f")).First().Parameters.Select(x => x.Name),
                "_C__A"
            );
            AssertEquals(
                entry.GetSignatures("self.f", GetLineNumber(code, "marker")).First().Parameters.Select(x => x.Name),
                "_C__A"
            );

            code = @"
class C(object):
    def __init__(self):
        self.__f(_C__A = 42)		# member should be __f

    def __f(self, __A):
        pass


class D(C):
    def __init__(self):
        marker
        self._C__f(_C__A=42)		# member should be _C__f

";

            entry = ProcessText(code);
            AssertContainsExactly(entry.GetMembersFromName("self", GetLineNumber(code, "self.__f")), GetUnion(_objectMembers, "__f", "__init__"));
            AssertContainsExactly(entry.GetMembersFromName("self", GetLineNumber(code, "marker")), GetUnion(_objectMembers, "_C__f", "__init__"));

            code = @"
class C(object):
    __FOO = 42

    def f(self):
        abc = C.__FOO  # Completion should work here


xyz = C._C__FOO  # Advanced members completion should work here
";
            entry = ProcessText(code);
            AssertContainsExactly(entry.GetMembersFromName("C", GetLineNumber(code, "abc = ")), GetUnion(_objectMembers, "__FOO", "f"));
            AssertContainsExactly(entry.GetMembersFromName("C", GetLineNumber(code, "xyz = ")), GetUnion(_objectMembers, "_C__FOO", "f"));

        }

        [TestMethod]
        public void TestBaseInstanceVariable() {
            var code = @"
class C:
    def __init__(self):
        self.abc = 42


class D(C):
    def __init__(self):        
        self.foo = self.abc
";

            var entry = ProcessText(code);
            AssertContainsExactly(entry.GetMembers("self.foo", GetLineNumber(code, "self.foo")).Select(x => x.Name), _intMembers);
            AssertContains(entry.GetMembers("self", GetLineNumber(code, "self.foo")).Select(x => x.Name), "abc");
        }

        [TestMethod]
        public void TestGenerator() {
            var entry = ProcessText(@"
def f():
    yield 1
    yield 2
    yield 3

a = f()
b = a.next()

for c in f():
    print c
            ");

            AssertContainsExactly(entry.GetTypesFromName("a", 1), GeneratorType);
            AssertContainsExactly(entry.GetTypesFromName("b", 1), IntType);
            AssertContainsExactly(entry.GetTypesFromName("c", 1), IntType);

            var text = @"
def f():
    yield 1
    x = yield 2

a = f()
b = a.next()
c = a.send('abc')";
            entry = ProcessText(text);

            AssertContainsExactly(entry.GetTypesFromName("a", 1), GeneratorType);
            AssertContainsExactly(entry.GetTypesFromName("b", 1), IntType);
            AssertContainsExactly(entry.GetTypesFromName("c", 1), IntType);
            AssertContainsExactly(entry.GetTypesFromName("x", GetLineNumber(text, "yield 2")), StringType);
        }

        
        [TestMethod]
        public void TestListComprehensions() {/*
            var entry = ProcessText(@"
x = [2,3,4]
y = [a for a in x]
z = y[0]
            ");

            AssertContainsExactly(entry.GetTypesFromName("z", 0), IntType);*/

            string text = @"
def f(abc):
    print abc

[f(x) for x in [2,3,4]]
";

            var entry = ProcessText(text);


            var vars = entry.GetVariables("f", GetLineNumber(text, "[f(x"));
            VerifyReferences(UniqifyVariables(vars), new VariableLocation(2, 5, VariableType.Definition), new VariableLocation(5, 2, VariableType.Reference));
        }


        [TestMethod]
        public void TestExecReferences() {
            string text = @"
a = {}
b = ""
exec b in a
";

            var entry = ProcessText(text);

            var vars = entry.GetVariables("a", GetLineNumber(text, "a = "));
            VerifyReferences(UniqifyVariables(vars), new VariableLocation(2, 1, VariableType.Definition), new VariableLocation(4, 11, VariableType.Reference));
            
            vars = entry.GetVariables("b", GetLineNumber(text, "b = "));
            VerifyReferences(UniqifyVariables(vars), new VariableLocation(3, 1, VariableType.Definition), new VariableLocation(4, 6, VariableType.Reference));
        }

        [TestMethod]
        public void TestPrivateMemberReferences() {
            string text = @"
class C:
    def __x(self):
        pass

    def y(self):
        self.__x()
";

            var entry = ProcessText(text);

            var vars = entry.GetVariables("self.__x", GetLineNumber(text, "self.__"));
            VerifyReferences(UniqifyVariables(vars), new VariableLocation(3, 9, VariableType.Definition), new VariableLocation(7, 14, VariableType.Reference));
        }

        [TestMethod]
        public void TestGeneratorComprehensions() {/*
            var entry = ProcessText(@"
x = [2,3,4]
y = [a for a in x]
z = y[0]
            ");

            AssertContainsExactly(entry.GetTypesFromName("z", 0), IntType);*/

            string text = @"
def f(abc):
    print abc

(f(x) for x in [2,3,4])
";

            var entry = ProcessText(text);


            var vars = entry.GetVariables("f", GetLineNumber(text, "(f(x"));
            VerifyReferences(UniqifyVariables(vars), new VariableLocation(2, 5, VariableType.Definition), new VariableLocation(5, 2, VariableType.Reference));
        }


        [TestMethod]
        public void TestForSequence() {
            var entry = ProcessText(@"
x = [('abc', 42, True), ('abc', 23, False),]
for some_str, some_int, some_bool in x:
	print some_str		
	print some_int		
    print some_bool	    
");
            AssertContainsExactly(entry.GetTypesFromName("some_str", 1), StringType);
            AssertContainsExactly(entry.GetTypesFromName("some_int", 1), IntType);
            AssertContainsExactly(entry.GetTypesFromName("some_bool", 1), BoolType);
        }

        [TestMethod]
        public void TestDynamicAttributes() {
            var entry = ProcessText(@"
class x(object):
    def __getattr__(self, name):
        return 42
    def f(self): 
        return 'abc'
        
a = x().abc
b = x().f()

class y(object):
    def __getattribute__(self, x):
        return 'abc'
        
c = y().abc
");

            AssertContainsExactly(entry.GetTypesFromName("a", 1), IntType);
            AssertContainsExactly(entry.GetTypesFromName("b", 1), StringType);
            AssertContainsExactly(entry.GetTypesFromName("c", 1), StringType);
        }
        
        [TestMethod]
        public void TestListAppend() {
            var entry = ProcessText(@"
x = []
x.append('abc')
y = x[0]
");

            AssertContainsExactly(entry.GetTypesFromName("y", 1), StringType);

            entry = ProcessText(@"
x = []
x.extend(('abc', ))
y = x[0]
");
            AssertContainsExactly(entry.GetTypesFromName("y", 1), StringType);

            entry = ProcessText(@"
x = []
x.insert(0, 'abc')
y = x[0]
");
            AssertContainsExactly(entry.GetTypesFromName("y", 1), StringType);

            entry = ProcessText(@"
x = []
x.append('abc')
y = x.pop()
");

            AssertContainsExactly(entry.GetTypesFromName("y", 1), StringType);

            entry = ProcessText(@"
class ListTest(object):
    def reset(self):
        self.items = []
        self.pushItem(self)
    def pushItem(self, item):
        self.items.append(item)

a = ListTest()
b = a.items[0]");

            AssertContains(entry.GetMembersFromName("b", 1), "pushItem");
        }

        [TestMethod]
        public void TestSlicing() {
            var entry = ProcessText(@"
x = [2]
y = x[:-1]
z = y[0]
");

            AssertContainsExactly(entry.GetTypesFromName("z", 1), IntType);

            entry = ProcessText(@"
x = (2, 3, 4)
y = x[:-1]
z = y[0]
");

            AssertContainsExactly(entry.GetTypesFromName("z", 1), IntType);
        }

        [TestMethod]
        public void TestConstantIndex() {
            var entry = ProcessText(@"
ZERO = 0
ONE = 1
TWO = 2
x = ['abc', 42, True)]


some_str = x[ZERO]
some_int = x[ONE]
some_bool = x[TWO]
");
            AssertContainsExactly(entry.GetTypesFromName("some_str", 1), StringType);
            AssertContainsExactly(entry.GetTypesFromName("some_int", 1), IntType);
            AssertContainsExactly(entry.GetTypesFromName("some_bool", 1), BoolType);
        }
        [TestMethod]
        public void TestCtorSignatures() {
            var entry = ProcessText(@"
class C: pass

class D(object): pass

class E(object):
    def __init__(self): pass

class F(object):
    def __init__(self, one): pass

class G(object):
    def __new__(cls): pass

class H(object):
    def __new__(cls, one): pass

            ");

            var result = entry.GetSignatures("C", 1).ToArray();
            Assert.AreEqual(result.Length, 1);
            Assert.AreEqual(result[0].Parameters.Length, 0);

            result = entry.GetSignatures("D", 1).ToArray();
            Assert.AreEqual(result.Length, 1);
            Assert.AreEqual(result[0].Parameters.Length, 0);

            result = entry.GetSignatures("E", 1).ToArray();
            Assert.AreEqual(result.Length, 1);
            Assert.AreEqual(result[0].Parameters.Length, 0);

            result = entry.GetSignatures("F", 1).ToArray();
            Assert.AreEqual(result.Length, 1);
            Assert.AreEqual(result[0].Parameters.Length, 1);

            result = entry.GetSignatures("G", 1).ToArray();
            Assert.AreEqual(result.Length, 1);
            Assert.AreEqual(result[0].Parameters.Length, 0);

            result = entry.GetSignatures("H", 1).ToArray();
            Assert.AreEqual(result.Length, 1);
            Assert.AreEqual(result[0].Parameters.Length, 1);
        }

        [TestMethod]
        public void TestDocStrings() {
            var entry = ProcessText(@"
def f():
    '''func doc'''


def funicode():
    u'''unicode func doc'''

class C:
    '''class doc'''

class Cunicode:
    u'''unicode class doc'''

            ");

            var result = entry.GetSignatures("f", 1).ToArray();
            Assert.AreEqual(result.Length, 1);
            Assert.AreEqual(result[0].Documentation, "func doc");

            result = entry.GetSignatures("C", 1).ToArray();
            Assert.AreEqual(result.Length, 1);
            Assert.AreEqual(result[0].Documentation, "class doc");

            result = entry.GetSignatures("funicode", 1).ToArray();
            Assert.AreEqual(result.Length, 1);
            Assert.AreEqual(result[0].Documentation, "unicode func doc");

            result = entry.GetSignatures("Cunicode", 1).ToArray();
            Assert.AreEqual(result.Length, 1);
            Assert.AreEqual(result[0].Documentation, "unicode class doc");
        }

        [TestMethod]
        public void TestEllipsis() {
            var entry = ProcessText(@"
x = ...
            ", PythonLanguageVersion.V31);

            var result = new List<IPythonType>(entry.GetTypesFromName("x", 1));
            Assert.AreEqual(result.Count, 1);
            Assert.AreEqual(result[0].Name, "ellipsis");
        }

        [TestMethod]
        public void TestBackquote() {
            var entry = ProcessText(@"x = `42`");

            var result = new List<IPythonType>(entry.GetTypesFromName("x", 1));
            Assert.AreEqual(result.Count, 1);
            Assert.AreEqual(result[0].Name, "str");
        }

        [TestMethod]
        public void TestBuiltinMethodSignatures() {
            var entry = ProcessText(@"
const = """".capitalize
constructed = str().capitalize
");

            string[] testCapitalize = new[] { "const", "constructed" };
            foreach (var test in testCapitalize) {
                var result = entry.GetSignatures(test, 1).ToArray();
                Assert.AreEqual(result.Length, 1);                
                Assert.AreEqual(result[0].Parameters.Length, 0);
            }

            entry = ProcessText(@"
const = [].append
constructed = list().append
");

            testCapitalize = new[] { "const", "constructed" };
            foreach (var test in testCapitalize) {
                var result = entry.GetSignatures(test, 1).ToArray();
                Assert.AreEqual(result.Length, 1);
                Assert.AreEqual(result[0].Parameters.Length, 1);
            }
        }

        [TestMethod]
        public void TestDel() {
            string text = @"
del foo
del foo[2]
del foo.bar
del (foo)
del foo, bar
";
            var entry = ProcessText(text);

            // We do no analysis on del statements, nothing to test
        }

        [TestMethod]
        public void TryExcept() {
            string text = @"
class MyException(Exception): pass

def f():
    try:
    except TypeError, e1:
        pass

def g():
    try:
    except MyException, e2:
        pass
";
            var entry = ProcessText(text);


            AssertContainsExactly(GetTypes(entry.GetValues("e1", GetLineNumber(text, ", e1"))), Interpreter.ImportModule("__builtin__").GetMember(Interpreter.CreateModuleContext(), "TypeError"));

            AssertContainsExactly(GetTypeNames(entry.GetValues("e2", GetLineNumber(text, ", e2"))), "MyException instance");
        }

        private IEnumerable<IPythonType> GetTypes(IEnumerable<IAnalysisValue> analysisValues) {
            foreach (var value in analysisValues) {
                yield return value.PythonType;
            }
        }

        private IEnumerable<string> GetTypeNames(IEnumerable<IAnalysisValue> analysisValues) {
            foreach (var value in analysisValues) {
                yield return value.ShortDescription;
            }
        }

        public class VariableLocation {
            public readonly int StartLine;
            public readonly int StartCol;
            public readonly VariableType Type;

            public VariableLocation(int startLine, int startCol, VariableType type) {
                StartLine = startLine;
                StartCol = startCol;
                Type = type;
            }
        }

        [TestMethod]
        public void TestStringFormatting() {
            var text = @"
x = u'abc %d'
y = x % (42, )


x1 = 'abc %d'
y1 = x1 % (42, )

foo = 'abc %d'.lower()
bar = foo % (42, )

foo2 = u'abc' + u'%d'
bar2 = foo2 % (42, )";

            var entry = ProcessText(text);
            AssertContainsExactly(GetTypeNames(entry.GetValues("y", GetLineNumber(text, "y ="))), "type unicode");
            AssertContainsExactly(GetTypeNames(entry.GetValues("y1", GetLineNumber(text, "y1 ="))), "type str");
            AssertContainsExactly(GetTypeNames(entry.GetValues("bar", GetLineNumber(text, "bar ="))), "type str");
            AssertContainsExactly(GetTypeNames(entry.GetValues("bar2", GetLineNumber(text, "bar2 ="))), "type unicode");
        }

        [TestMethod]
        public void TestDescriptorNoDescriptor() {
            var text = @"
class NoDescriptor:   
       def nodesc_method(self): pass

 class SomeClass:
    foo = NoDescriptor()

    def f(self):
        self.foo
        pass
";

            var entry = ProcessText(text);
            AssertContains(entry.GetMembers("self.foo", GetLineNumber(text, "self.foo")).Select(x => x.Name), "nodesc_method");
        }

        /// <summary>
        /// Verifies that a line in triple quoted string which ends with a \ (eating the newline) doesn't throw
        /// off our newline tracking.
        /// </summary>
        [TestMethod]
        public void TestReferencesTripleQuotedStringWithBackslash() {
            // instance variables
            var text = @"
'''this is a triple quoted string\
that ends with a backslash on a line\
and our line info should remain correct'''

# add ref w/o type info
class C(object):
    def __init__(self, foo):
        self.abc = foo
        del self.abc
        print self.abc

";
            var entry = ProcessText(text);
            VerifyReferences(entry.GetVariables("self.abc", GetLineNumber(text, "self.abc")), new VariableLocation(9, 14, VariableType.Definition), new VariableLocation(10, 18, VariableType.Reference), new VariableLocation(11, 20, VariableType.Reference));
            VerifyReferences(entry.GetVariables("foo", GetLineNumber(text, "= foo")), new VariableLocation(8, 24, VariableType.Definition), new VariableLocation(9, 20, VariableType.Reference));
        }

        [TestMethod]
        public void TestReferences() {
            // instance variables
            var text = @"
# add ref w/o type info
class C(object):
    def __init__(self, foo):
        self.abc = foo
        del self.abc
        print self.abc

";
            var entry = ProcessText(text);
            VerifyReferences(entry.GetVariables("self.abc", GetLineNumber(text, "self.abc")), new VariableLocation(5, 14, VariableType.Definition), new VariableLocation(6, 18, VariableType.Reference), new VariableLocation(7, 20, VariableType.Reference));
            VerifyReferences(entry.GetVariables("foo", GetLineNumber(text, "= foo")), new VariableLocation(4, 24, VariableType.Definition), new VariableLocation(5, 20, VariableType.Reference));

            text = @"
# add ref w/ type info
class D(object):
    def __init__(self, foo):
        self.abc = foo
        del self.abc
        print self.abc

D(42)";
            entry = ProcessText(text);

            VerifyReferences(entry.GetVariables("self.abc", GetLineNumber(text, "self.abc")), new VariableLocation(5, 14, VariableType.Definition), new VariableLocation(6, 18, VariableType.Reference), new VariableLocation(7, 20, VariableType.Reference));
            VerifyReferences(entry.GetVariables("foo", GetLineNumber(text, "= foo")), new VariableLocation(4, 24, VariableType.Definition), new VariableLocation(5, 20, VariableType.Reference));
            VerifyReferences(UniqifyVariables(entry.GetVariables("D", GetLineNumber(text, "D(42)"))), new VariableLocation(9, 1, VariableType.Reference), new VariableLocation(3, 7, VariableType.Definition));

            // function definitions
            text = @"
def f(): pass

x = f()";
            entry = ProcessText(text);
            VerifyReferences(UniqifyVariables(entry.GetVariables("f", GetLineNumber(text, "x ="))), new VariableLocation(4, 5, VariableType.Reference), new VariableLocation(2, 5, VariableType.Definition));

            

            text = @"
def f(): pass

x = f";
            entry = ProcessText(text);
            VerifyReferences(UniqifyVariables(entry.GetVariables("f", GetLineNumber(text, "x ="))), new VariableLocation(4, 5, VariableType.Reference), new VariableLocation(2, 5, VariableType.Definition));

            // class variables
            text = @"

class D(object):
    abc = 42
    print abc
    del abc
";
            entry = ProcessText(text);

            VerifyReferences(entry.GetVariables("abc", GetLineNumber(text, "abc =")), new VariableLocation(4, 5, VariableType.Definition), new VariableLocation(5, 11, VariableType.Reference), new VariableLocation(6, 9, VariableType.Reference));

            // class definition
            text = @"
class D(object): pass

a = D
";
            entry = ProcessText(text);
            VerifyReferences(UniqifyVariables(entry.GetVariables("D", GetLineNumber(text, "a ="))), new VariableLocation(4, 5, VariableType.Reference), new VariableLocation(2, 7, VariableType.Definition));

            // method definition
            text = @"
class D(object): 
    def f(self): pass

a = D().f()
";
            entry = ProcessText(text);
            VerifyReferences(UniqifyVariables(entry.GetVariables("D().f", GetLineNumber(text, "a ="))), 
                new VariableLocation(5, 9, VariableType.Reference), new VariableLocation(3, 9, VariableType.Definition));

            // globals
            text = @"
abc = 42
print abc
del abc
";
            entry = ProcessText(text);
            VerifyReferences(entry.GetVariables("abc", GetLineNumber(text, "abc =")), new VariableLocation(4, 5, VariableType.Reference), new VariableLocation(2, 1, VariableType.Definition), new VariableLocation(3, 7, VariableType.Reference));

            // parameters
            text = @"
def f(abc):
    print abc
    abc = 42
    del abc
";
            entry = ProcessText(text);
            VerifyReferences(entry.GetVariables("abc", GetLineNumber(text, "abc =")), new VariableLocation(2, 7, VariableType.Definition), new VariableLocation(4, 5, VariableType.Definition), new VariableLocation(3, 11, VariableType.Reference), new VariableLocation(5, 9, VariableType.Reference));
        
        
            // grammer test - statements
            text = @"
def f(abc):
    try: pass
    except abc: pass

    try: pass
    except TypeError, abc: pass    

    abc, bar = 42, 23
    abc[23] = 42
    abc.foo = 42
    abc += 2

    class D(abc): pass

    for x in abc: print x

    import abc
    from xyz import abc
    from xyz import bar as abc

    if abc: print 'hi'
    elif abc: print 'bye'
    else: abc

    with abc:
        return abc

    print abc
    assert abc, abc

    raise abc
    raise abc, abc, abc

    while abc:
        abc
    else:
        abc

    for x in foo: 
        print x
    else:
        print abc

    try: pass
    except TypeError:
    else:
        abc
";
            entry = ProcessText(text);
            VerifyReferences(entry.GetVariables("abc", GetLineNumber(text, "try:")), 
                new VariableLocation(2, 7, VariableType.Definition), 
                new VariableLocation(4, 12, VariableType.Reference), 
                new VariableLocation(7, 23, VariableType.Definition),
                
                new VariableLocation(9, 5, VariableType.Definition),
                new VariableLocation(10, 5, VariableType.Reference),
                new VariableLocation(11, 5, VariableType.Reference),
                new VariableLocation(12, 5, VariableType.Reference),
                
                new VariableLocation(14, 13, VariableType.Reference),
                
                new VariableLocation(16, 14, VariableType.Reference),

                new VariableLocation(18, 12, VariableType.Reference),
                new VariableLocation(19, 21, VariableType.Definition),
                new VariableLocation(20, 28, VariableType.Definition),

                new VariableLocation(19, 21, VariableType.Reference), 
                new VariableLocation(20, 28, VariableType.Reference),

                new VariableLocation(22, 8, VariableType.Reference),
                new VariableLocation(23, 10, VariableType.Reference),
                new VariableLocation(24, 11, VariableType.Reference),

                new VariableLocation(26, 10, VariableType.Reference),
                new VariableLocation(27, 16, VariableType.Reference),

                new VariableLocation(29, 11, VariableType.Reference),
                new VariableLocation(30, 12, VariableType.Reference),
                new VariableLocation(30, 17, VariableType.Reference),

                new VariableLocation(32, 11, VariableType.Reference),
                new VariableLocation(33, 11, VariableType.Reference),
                new VariableLocation(33, 16, VariableType.Reference),
                new VariableLocation(33, 21, VariableType.Reference),

                new VariableLocation(35, 11, VariableType.Reference),
                new VariableLocation(36, 9, VariableType.Reference),
                new VariableLocation(38, 9, VariableType.Reference),

                new VariableLocation(43, 15, VariableType.Reference),

                new VariableLocation(48, 9, VariableType.Reference) 
            );


            // grammer test - expressions
            text = @"
def f(abc):
    x = abc + 2
    x = 2 + abc
    x = l[abc]
    x = abc[l]
    x = abc.foo
    
    g(abc)

    abc if abc else abc

    {abc:abc},
    [abc, abc]
    (abc, abc)
    {abc}

    yield abc
    [x for x in abc]
    (x for x in abc)

    abc or abc
    abc and abc

    +abc
    x[abc:abc:abc]

    abc == abc
    not abc

    lambda : abc
";
            entry = ProcessText(text);
            VerifyReferences(entry.GetVariables("abc", GetLineNumber(text, "x =")),
                new VariableLocation(2, 7, VariableType.Definition),

                new VariableLocation(3, 9, VariableType.Reference),
                new VariableLocation(4, 13, VariableType.Reference),

                new VariableLocation(5, 11, VariableType.Reference),
                new VariableLocation(6, 9, VariableType.Reference),
                new VariableLocation(7, 9, VariableType.Reference),
                new VariableLocation(9, 7, VariableType.Reference),

                new VariableLocation(11, 5, VariableType.Reference),
                new VariableLocation(11, 12, VariableType.Reference),
                new VariableLocation(11, 21, VariableType.Reference),

                new VariableLocation(13, 6, VariableType.Reference),
                new VariableLocation(13, 10, VariableType.Reference),
                new VariableLocation(14, 6, VariableType.Reference),
                new VariableLocation(14, 11, VariableType.Reference),
                new VariableLocation(15, 6, VariableType.Reference),
                new VariableLocation(15, 11, VariableType.Reference),
                new VariableLocation(16, 6, VariableType.Reference),

                new VariableLocation(18, 11, VariableType.Reference),
                new VariableLocation(19, 17, VariableType.Reference),
                new VariableLocation(20, 17, VariableType.Reference),

                new VariableLocation(22, 5, VariableType.Reference),
                new VariableLocation(22, 12, VariableType.Reference),
                new VariableLocation(23, 5, VariableType.Reference),
                new VariableLocation(23, 13, VariableType.Reference),

                new VariableLocation(25, 6, VariableType.Reference),
                new VariableLocation(26, 7, VariableType.Reference),
                new VariableLocation(26, 11, VariableType.Reference),
                new VariableLocation(26, 15, VariableType.Reference),

                new VariableLocation(28, 5, VariableType.Reference),
                new VariableLocation(28, 12, VariableType.Reference),
                new VariableLocation(29, 9, VariableType.Reference),

                new VariableLocation(31, 14, VariableType.Reference)
            );

            // parameters
            text = @"
def f(a):
    def g():
        print(a)
        assert isinstance(a, int)
        a = 200
";
            entry = ProcessText(text);
            VerifyReferences(
                entry.GetVariables("a", GetLineNumber(text, "print(a)")),
                new VariableLocation(6, 9, VariableType.Definition),
                new VariableLocation(4, 15, VariableType.Reference),
                new VariableLocation(5, 27, VariableType.Reference)
            );


            entry = ProcessText(text);
            VerifyReferences(
                entry.GetVariables("a", GetLineNumber(text, "def g")),
                new VariableLocation(2, 7, VariableType.Definition)
            );

        }

        public void VerifyReferences(IEnumerable<IAnalysisVariable> variables, params VariableLocation[] variableType) {
            var vars = new List<IAnalysisVariable>(variables);
            if (vars.Count == 0) {
                Assert.Fail("Got no references");
            }

            int removed = 0;
            bool removedOne = false;
            do {
                for (int j = 0; j < variableType.Length; j++) {
                    var expected = variableType[j];

                    bool found = false;
                    for (int i = 0; i < vars.Count; i++) {
                        var have = vars[i];

                        if (have.Location.Line == expected.StartLine &&
                            have.Location.Column == expected.StartCol &&
                            have.Type == expected.Type) {
                            vars.RemoveAt(i);
                            removed++;
                            removedOne = found = true;
                            break;
                        }
                    }

                    if (!found) {
                        StringBuilder error = new StringBuilder(String.Format("Failed to find location: new VariableLocation({0}, {1} VariableType.{2})," + Environment.NewLine, expected.StartLine, expected.StartCol, expected.Type));
                        LocationNames(vars, error);

                        Assert.Fail(error.ToString());
                    }
                }
            } while (vars.Count != 0 && removedOne);

            if (vars.Count != 0) {
                StringBuilder error = new StringBuilder("Didn't use all locations - had " + variables.Count() + Environment.NewLine);
                LocationNames(vars, error);
                Assert.Fail(error.ToString());
            }
        }


        [TestMethod]
        public void TestReferencesCrossModule() {
            var state = new PythonAnalyzer(Interpreter, PythonLanguageVersion.V27);

            var fooText = @"
from bar import abc

abc()
";
            var barText = "class abc(object): pass";

            var fooMod = state.AddModule("foo", "foo", null);
            Prepare(fooMod, GetSourceUnit(fooText, "mod1"));
            var barMod = state.AddModule("bar", "bar", null);
            Prepare(barMod, GetSourceUnit(barText, "mod2"));

            fooMod.Analyze();
            barMod.Analyze();

            VerifyReferences(UniqifyVariables(barMod.Analysis.GetVariables("abc", GetLineNumber(barText, "abc"))),
                new VariableLocation(1, 7, VariableType.Definition),     // definition 
                new VariableLocation(2, 17, VariableType.Reference),     // import
                new VariableLocation(4, 1, VariableType.Reference)       // call
            );
        }

        private static void LocationNames(List<IAnalysisVariable> vars, StringBuilder error) {
            foreach (var var in vars) {
                error.AppendFormat("   new VariableLocation({0}, {1}, VariableType.{2}),", var.Location.Line, var.Location.Column, var.Type);
                error.AppendLine();
            }
        }

        [TestMethod]
        public void TestSignatureDefaults() {
            var entry = ProcessText(@"
def f(x = None): pass

def g(x = {}): pass

def h(x = {2:3}): pass

def i(x = []): pass

def j(x = [None]): pass

def k(x = ()): pass

def l(x = (2, )): pass
");

            var tests = new[] {
                new { FuncName = "f", ParamName="x = None" },
                new { FuncName = "g", ParamName="x = {}" },
                new { FuncName = "h", ParamName="x = {...}" },
                new { FuncName = "i", ParamName="x = []" },
                new { FuncName = "j", ParamName="x = [...]" },
                new { FuncName = "k", ParamName="x = ()" },
                new { FuncName = "l", ParamName="x = (...)" },
            };

            foreach (var test in tests) {
                var result = entry.GetSignatures(test.FuncName, 1).ToArray();
                Assert.AreEqual(result.Length, 1);
                Assert.AreEqual(result[0].Parameters.Length, 1);
                Assert.AreEqual(result[0].Parameters[0].Name, test.ParamName);
            }
        }

        [TestMethod]
        public void TestGetVariablesDictionaryGet() {
            var entry = ProcessText(@"
x = {42:'abc'}
            ");

            foreach (var varRef in entry.GetValues("x.get", 1)) {
                Assert.AreEqual("bound built-in method get", varRef.Description);
            }
        }

        [TestMethod]
        public void TestDictMethods() {
            var entry = ProcessText(@"
x = {42:'abc'}
            ");

            Assert.AreEqual(entry.GetValues("x.items()[0][0]", 1).Select(x => x.PythonType).First(), IntType);
            Assert.AreEqual(entry.GetValues("x.items()[0][1]", 1).Select(x => x.PythonType).First(), StringType);
            Assert.AreEqual(entry.GetValues("x.keys()[0]", 1).Select(x => x.PythonType).First(), IntType);
            Assert.AreEqual(entry.GetValues("x.values()[0]", 1).Select(x => x.PythonType).First(), StringType);
            Assert.AreEqual(entry.GetValues("x.pop(1)", 1).Select(x => x.PythonType).First(), StringType);
            Assert.AreEqual(entry.GetValues("x.popitem()[0]", 1).Select(x => x.PythonType).First(), IntType);
            Assert.AreEqual(entry.GetValues("x.popitem()[1]", 1).Select(x => x.PythonType).First(), StringType);
            Assert.AreEqual(entry.GetValues("x.iterkeys().next()", 1).Select(x => x.PythonType).First(), IntType);
            Assert.AreEqual(entry.GetValues("x.itervalues().next()", 1).Select(x => x.PythonType).First(), StringType);
            Assert.AreEqual(entry.GetValues("x.iteritems().next()[0]", 1).Select(x => x.PythonType).First(), IntType);
            Assert.AreEqual(entry.GetValues("x.iteritems().next()[1]", 1).Select(x => x.PythonType).First(), StringType);
        }

        [TestMethod]
        public void TestFutureDivision() {
            var entry = ProcessText(@"
from __future__ import division
x = 1/2
            ");

            Assert.AreEqual(entry.GetValues("x", 1).Select(x => x.PythonType).First(), FloatType);
        }

        [TestMethod]
        public void TestBoundMethodDescription() {
            var entry = ProcessText(@"
class C:
    def f(self):
        'doc string'

a = C()
b = a.f
            ");

            foreach (var varRef in entry.GetValues("b", 1)) {
                Assert.AreEqual("method f of C objects \r\ndoc string", varRef.Description);
            }
        }

        [TestMethod]
        public void TestLambdaExpression() {
            var entry = ProcessText(@"
x = lambda a: a
y = x(42)
");

            AssertContainsExactly(entry.GetTypesFromName("y", 1), IntType);

            entry = ProcessText(@"
def f(a):
    return a

x = lambda b: f(b)
y = x(42)
");

            AssertContainsExactly(entry.GetTypesFromName("y", 1), IntType);
        }

        [TestMethod]
        public void TestRecursiveClass() {
            var entry = ProcessText(@"
cls = object

class cls(cls): 
    abc = 42
");

            entry.GetMembersFromName("cls", 1);
            AssertContainsExactly(entry.GetMembers("cls().abc", 1).Select(member => member.Name), _intMembers);
            AssertContainsExactly(entry.GetMembers("cls.abc", 1).Select(member => member.Name), _intMembers);
        }

        [TestMethod]
        public void TestBadMethod() {
            var entry = ProcessText(@"
class cls(object): 
    def f(): 
        'help'
        return 42

abc = cls()
foo = abc.f()
");
            
            AssertContainsExactly(entry.GetMembers("foo", 1).Select(member => member.Name), _intMembers);
            var sigs = entry.GetSignatures("cls().f", 0).ToArray();
            Assert.AreEqual(1, sigs.Length);
            Assert.AreEqual("help", sigs[0].Documentation);
        }

        [TestMethod]
        public void TestKeywordArguments() {
            var funcDef = @"def f(a, b, c): 
    pass";
            var classWithInit  = @"class f(object):
    def __init__(self, a, b, c):
        pass";
            var classWithNew = @"class f(object):
    def __new__(cls, a, b, c):
        pass";
            var method = @"class x(object):
    def g(self, a, b, c):
        pass

f = x().g";
            var decls = new []  { funcDef, classWithInit, classWithNew, method };

            foreach (var decl in decls) {
                string[] testCalls = new[] { 
                    "f(c = 'abc', b = 42, a = 3j)", "f(3j, c = 'abc', b = 42)", "f(3j, 42, c = 'abc')",
                    "f(c = 'abc', b = 42, a = 3j, d = 42)",  // extra argument
                    "f(3j, 42, 'abc', d = 42)",
                };

                foreach (var testCall in testCalls) {
                    var text = decl + Environment.NewLine + testCall;
                    var entry = ProcessText(text);

                    AssertContainsExactly(entry.GetTypesFromName("a", GetLineNumber(text, "pass")), ComplexType);
                    AssertContainsExactly(entry.GetTypesFromName("b", GetLineNumber(text, "pass")), IntType);
                    AssertContainsExactly(entry.GetTypesFromName("c", GetLineNumber(text, "pass")), StringType);
                }
            }
        }

        [TestMethod]
        public void TestPositionalSplat() {
            var funcDef = @"def f(a, b, c): 
    pass";            
            var classWithInit = @"class f(object):
    def __init__(self, a, b, c):
        pass";
            var classWithNew = @"class f(object):
    def __new__(cls, a, b, c):
        pass";
            var method = @"class x(object):
    def g(self, a, b, c):
        pass

f = x().g";
            var decls = new[] { funcDef, classWithInit, classWithNew, method };

            foreach (var decl in decls) {
                string[] testCalls = new[] { 
                    "f(*(3j, 42, 'abc'))", 
                    "f(*[3j, 42, 'abc'])", 
                    "f(*(3j, 42, 'abc', 4L))",  // extra argument
                    "f(*[3j, 42, 'abc', 4L])",  // extra argument
                };

                foreach (var testCall in testCalls) {
                    var text = decl + Environment.NewLine + testCall;
                    var entry = ProcessText(text);

                    AssertContainsExactly(entry.GetTypesFromName("a", GetLineNumber(text, "pass")), ComplexType);
                    AssertContainsExactly(entry.GetTypesFromName("b", GetLineNumber(text, "pass")), IntType);
                    AssertContainsExactly(entry.GetTypesFromName("c", GetLineNumber(text, "pass")), StringType);
                }
            }
        }
        [TestMethod]
        public void TestForwardRef() {
            var text = @"

class D(object):
    def bar(self, x):
        abc = C()
        abc.foo(2)
        a = abc.foo(2.0)
        a.bar(('a', 'b', 'c', 'd'))

class C(object):
    def foo(self, x):
        D().bar('abc')
        D().bar(['a', 'b', 'c'])
        return D()
    def baz(self): pass
";
            var entry = ProcessText(text);

            var fifty = entry.GetVariablesNoBuiltins(GetLineNumber(text, "abc.foo")).ToSet();
            AssertContainsExactly(fifty, "C", "D", "a", "abc", "self", "x");

            var three = entry.GetVariablesNoBuiltins(GetLineNumber(text, "lass D") + 1).ToSet();
            AssertContainsExactly(three, "C", "D", "bar");

            var allFifty = entry.GetMembersFromName("abc", GetLineNumber(text, "abc.foo")).ToSet();
            AssertContainsExactly(allFifty, GetUnion(_objectMembers, "baz", "foo"));

            var xTypes = entry.GetTypesFromName("x", GetLineNumber(text, "abc.foo")).ToSet();
            AssertContainsExactly(xTypes, ListType, StringType, TupleType);

            var xMembers = entry.GetMembersFromName("x", GetLineNumber(text, "abc.foo")).ToSet();
            AssertContainsExactly(xMembers, GetIntersection(_strMembers, _listMembers));
        }

        public static int GetLineNumber(string text, string substring) {
            string[] splitLines = text.Split('\n');
            for (int i = 0; i < splitLines.Length; i++) {
                if (splitLines[i].IndexOf(substring) != -1) {
                    return i + 1;
                }
            }

            throw new InvalidOperationException();
        }

        [TestMethod]
        public void TestBuiltins() {
            var text = @"
booltypetrue = True
booltypefalse = False
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetTypesFromName("booltypetrue", 1), BoolType);
            AssertContainsExactly(entry.GetTypesFromName("booltypefalse", 1), BoolType);
        }

        [TestMethod]
        public void TestDictionaryFunctionTable() {
            var text = @"
def f(a, b):
    print a, b
    
def g(a, b):
    x, y = a, b

x = {'foo': f, 'bar' : g}
x['foo'](42, [])
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetTypesFromName("a", GetLineNumber(text, "print")), IntType);
            AssertContainsExactly(entry.GetTypesFromName("b", GetLineNumber(text, "print")), ListType);
            AssertContainsExactly(entry.GetTypesFromName("a", GetLineNumber(text, "x, y")), IntType);
            AssertContainsExactly(entry.GetTypesFromName("b", GetLineNumber(text, "x, y")), ListType);
        }

        [TestMethod]
        public void TestDictionaryAssign() {
            var text = @"
x = {'abc': 42}
y = x['foo']
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetTypesFromName("y", 1), IntType);
        }

        [TestMethod]
        public void TestDictionaryFunctionTableGet2() {
            var text = @"
def f(a, b):
    print a, b
    
def g(a, b):
    x, y = a, b

x = {'foo': f, 'bar' : g}
x.get('foo')(42, [])
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetTypesFromName("a", GetLineNumber(text, "print")), IntType);
            AssertContainsExactly(entry.GetTypesFromName("b", GetLineNumber(text, "print")), ListType);
            AssertContainsExactly(entry.GetTypesFromName("a", GetLineNumber(text, "x, y")), IntType);
            AssertContainsExactly(entry.GetTypesFromName("b", GetLineNumber(text, "x, y")), ListType);
        }

        [TestMethod]
        public void TestDictionaryFunctionTableGet() {
            var text = @"
def f(a, b):
    print a, b
    
def g(a, b):
    x, y = a, b

x = {'foo': f, 'bar' : g}
y = x.get('foo', None)
if y is not None:
    y(42, [])
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetTypesFromName("a", GetLineNumber(text, "print")), IntType);
            AssertContainsExactly(entry.GetTypesFromName("b", GetLineNumber(text, "print")), ListType);
            AssertContainsExactly(entry.GetTypesFromName("a", GetLineNumber(text, "x, y")), IntType);
            AssertContainsExactly(entry.GetTypesFromName("b", GetLineNumber(text, "x, y")), ListType);
        }

        [TestMethod]
        public void TestSimpleGlobals() {
            var text = @"
class x(object):
    def abc(self):
        pass
        
a = x()
x.abc()
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetVariablesNoBuiltins(1), "a", "x");
            AssertContainsExactly(entry.GetMembersFromName("x", 1), GetUnion(_objectMembers, "abc"));
        }

        [TestMethod]
        public void TestFuncCallInIf() {
            var text = @"
def Method(a, b, c):
    print a, b, c
    
if not Method(42, 'abc', []):
    pass
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetTypesFromName("a", GetLineNumber(text, "print")), IntType);
            AssertContainsExactly(entry.GetTypesFromName("b", GetLineNumber(text, "print")), StringType);
            AssertContainsExactly(entry.GetTypesFromName("c", GetLineNumber(text, "print")), ListType);
        }

        [TestMethod]
        public void TestWithStatement() {
            var text = @"
class x(object):
    def x_method(self):
        pass
        
with x() as foo:
    print foo
    
with x():
    pass
";
            var entry = ProcessText(text);
            var foo = entry.GetMembersFromName("foo", GetLineNumber(text, "print foo"));
            AssertContainsExactly(foo, GetUnion(_objectMembers, "x_method"));
        }

        [TestMethod]
        public void TestOverrideFunction() {
            var text = @"
class bar(object):
    def Call(self, xvar, yvar):
        pass

class baz(bar):
    def Call(self, xvar, yvar):
        pass

class Cxxxx(object):
    def __init__(self):
        self.foo = baz()
        
    def Cmeth(self, avar, bvar):
        self.foo.Call(avar, bvar)
        


abc = Cxxxx()
abc.Cmeth(['foo'], 'bar')
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetTypesFromName("xvar", GetLineNumber(text, "pass")), ListType);
        }

        [TestMethod]
        public void TestSimpleMethodCall() {
            var text = @"
class x(object):
    def abc(self, foo):
        pass
        
a = x()
a.abc('abc')
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetTypesFromName("foo", GetLineNumber(text, "pass")), StringType);
            AssertContainsExactly(entry.GetMembersFromName("self", GetLineNumber(text, "pass")), GetUnion(_objectMembers, "abc"));
        }

        [TestMethod]
        public void TestBuiltinRetval() {
            var text = @"
x = [2,3,4]
a = x.index(2)
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetTypesFromName("x", GetLineNumber(text, "x =")).ToSet(), ListType);
            AssertContainsExactly(entry.GetTypesFromName("a", GetLineNumber(text, "a =")).ToSet(), IntType);
        }

        [TestMethod]
        public void TestBuiltinFuncRetval() {
            var text = @"
x = ord('a')
y = range(5)
";

            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetTypesFromName("x", GetLineNumber(text, "x = ")).ToSet(), IntType);
            AssertContainsExactly(entry.GetTypesFromName("y", GetLineNumber(text, "y = ")).ToSet(), ListType);
        }

        [TestMethod]
        public void TestFunctionMembers() {
            var text = @"
def f(x): pass
f.abc = 32
";
            var entry = ProcessText(text);
            AssertContains(entry.GetMembersFromName("f", 1), "abc");

            text = @"
def f(x): pass

";
            entry = ProcessText(text);
            AssertDoesntContain(entry.GetMembersFromName("f", 1), "x");
            AssertContainsExactly(entry.GetMembersFromName("f", 1), _functionMembers);

            AssertContainsExactly(entry.GetMembersFromName("f.func_name", 1), _strMembers);
        }


        [TestMethod]
        public void TestRangeIteration() {
            var text = @"
for i in range(5):
    pass
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetTypesFromName("i", GetLineNumber(text, "for i")).ToSet(), IntType);
        }

        [TestMethod]
        public void TestBuiltinImport() {
            var text = @"
import sys
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetVariablesNoBuiltins(1), "sys");
            Assert.IsTrue(entry.GetMembersFromName("sys", 1).Any((s) => s == "winver"));
        }

        [TestMethod]
        public void TestBuiltinImportInFunc() {
            var text = @"
def f():
    import sys
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetVariablesNoBuiltins(GetLineNumber(text, "sys")), "f", "sys");
            AssertContains(entry.GetMembersFromName("sys", GetLineNumber(text, "sys")), "winver");
        }

        [TestMethod]
        public void TestBuiltinImportInClass() {
            var text = @"
class C:
    import sys
";
            var entry = ProcessText(text);

            AssertContainsExactly(entry.GetVariablesNoBuiltins(GetLineNumber(text, "sys")), "C", "sys");
            Assert.IsTrue(entry.GetMembersFromName("sys", GetLineNumber(text, "sys")).Any((s) => s == "winver"));
        }

        [TestMethod]
        public void TestNoImportClr() {
            var text = @"
x = 'abc'
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetTypesFromName("x", 1), StringType);
            AssertContainsExactly(entry.GetMembersFromName("x", 1), _strMembers);
        }

        [TestMethod]
        public void TestMutualRecursion() {
            var text = @"
class C:
    def f(self, other, depth):
        if depth == 0:
            return 'abc'
        return other.g(self, depth - 1)

class D:
    def g(self, other, depth):
        if depth == 0:
            return ['d', 'e', 'f']
        
        return other.f(self, depth - 1)

x = D().g(C(), 42)

";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetMembersFromName("other", GetLineNumber(text, "other.g")), "g", "__doc__", "__class__");
            AssertContainsExactly(entry.GetTypesFromName("x", GetLineNumber(text, "x =")), ListType, StringType);
            AssertContainsExactly(entry.GetMembersFromName("x", GetLineNumber(text, "x =")),
                GetIntersection(_listMembers, _strMembers));
        }

        [TestMethod]
        public void TestForwardRefVars() {
            var text = @"
class x(object):
    def __init__(self, val):
        self.abc = []
    
x(42)
x('abc')
x([])
";
            var entry = ProcessText(text);
            Assert.AreEqual(1, entry.GetValues("self.abc", GetLineNumber(text, "self.abc")).ToList().Count);
        }

        [TestMethod]
        public void TestReturnFunc() {
            var text = @"
def g():
    return []

def f():
    return g
    
x = f()()
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetTypesFromName("x", 1), ListType);
        }

        [TestMethod]
        public void TestReturnArg() {
            var text = @"
def g(a):
    return a

x = g(1)
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetTypesFromName("x", 1), IntType);
        }

        [TestMethod]
        public void TestReturnArg2() {
            var text = @"

def f(a):
    def g():
        return a
    return g

x = f(2)()
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetTypesFromName("x", 1), IntType);
        }

        [TestMethod]
        public void TestMemberAssign() {
            var text = @"
class C:
    def func(self):
        self.abc = 42

a = C()
a.func()
foo = a.abc
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetTypesFromName("foo", 1), IntType);
            AssertContainsExactly(entry.GetMembersFromName("foo", 1), _intMembers);
            AssertContainsExactly(entry.GetMembersFromName("a", 1), "abc", "func", "__doc__", "__class__");
        }

        [TestMethod]
        public void TestMemberAssign2() {
            var text = @"
class D:
    def func2(self):
        a = C()
        a.func()
        return a.abc

class C:
    def func(self):
        self.abc = [2,3,4]

foo = D().func2()
";
            var entry = ProcessText(text);
            // TODO: AssertContainsExactly(entry.GetTypesFromName("foo", 0), ListType);
        }

        [TestMethod]
        public void TestUnfinishedDot() {
            // the partial dot should be ignored and we shouldn't see g as
            // a member of D
            var text = @"
class D(object):
    def func(self):
        self.
        
def g(a, b, c): pass
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetMembersFromName("self", GetLineNumber(text, "self.")),
                GetUnion(_objectMembers, "func"));
        }

        [TestMethod]
        public void TestCrossModule() {
            var text1 = @"
import mod2
";
            var text2 = @"
x = 42
";

            PermutedTest("mod", new[] { text1, text2 }, (pe) => {
                AssertContainsExactly(pe[0].Analysis.GetMembersFromName("mod2", 1), "x");
            });
        }

        [TestMethod]
        public void TestCrossModuleCall() {
            var text1 = @"
import mod2
y = mod2.f('abc')
";
            var text2 = @"
def f(x):
	return x
";

            PermutedTest("mod", new[] { text1, text2 }, (pe) => {
                AssertContainsExactly(pe[1].Analysis.GetTypesFromName("x", GetLineNumber(text2, "return x")), StringType);
                AssertContainsExactly(pe[0].Analysis.GetTypesFromName("y", GetLineNumber(text1, "y")), StringType);
            });
        }

        [TestMethod]
        public void TestCrossModuleCallType() {
            var text1 = @"
import mod2
y = mod2.c('abc').x
";
            var text2 = @"
class c:
    def __init__(self, x):
        self.x = x
";

            PermutedTest("mod", new[] { text1, text2 }, (pe) => {
                AssertContainsExactly(pe[1].Analysis.GetTypesFromName("x", GetLineNumber(text2, "= x")), StringType);
                AssertContainsExactly(pe[0].Analysis.GetTypesFromName("y", GetLineNumber(text1, "y")), StringType);
            });
        }

        [TestMethod]
        public void TestCrossModuleCallType2() {
            var text1 = @"
from mod2 import c
class x(object):
    def Foo(self):
        y = c('abc').x
";
            var text2 = @"
class c:
    def __init__(self, x):
        self.x = x
";

            PermutedTest("mod", new[] { text1, text2 }, (pe) => {
                AssertContainsExactly(pe[1].Analysis.GetTypesFromName("x", GetLineNumber(text2, "= x")), StringType);
                AssertContainsExactly(pe[0].Analysis.GetTypesFromName("y", GetLineNumber(text1, "y =")), StringType);
            });
        }

        [TestMethod]
        public void TestCrossModuleFuncAndType() {
            var text1 = @"
class Something(object):
    def f(self): pass
    def g(self): pass


def SomeFunc():
    x = Something()
    return x
";
            var text2 = @"
from mod1 import SomeFunc

x = SomeFunc()
";

            var text3 = @"
from mod2 import x
a = x
";

            PermutedTest("mod", new[] { text1, text2, text3 }, (pe) => {
                AssertContainsExactly(pe[2].Analysis.GetMembersFromName("a", GetLineNumber(text3, "a = ")),
                    GetUnion(_objectMembers, "f", "g"));
            });
        }

        [TestMethod]
        public void TestMembersAfterError() {
            var text = @"
class X(object):
    def f(self):
        return self.
        
    def g(self):
        pass
        
    def h(self):
        pass
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetMembersFromName("self", GetLineNumber(text, "self.")),
                GetUnion(_objectMembers, "f", "g", "h"));
        }


        [TestMethod]
        public void TestProperty() {
            var text = @"
class x(object):
    @property
    def SomeProp(self):
        return 42

a = x().SomeProp
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetTypesFromName("a", GetLineNumber(text, "a =")), IntType);
        }

        [TestMethod]
        public void TestStaticMethod() {
            var text = @"
class x(object):
    @staticmethod
    def StaticMethod(value):
        return value

a = x().StaticMethod(4.0)
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetTypesFromName("a", GetLineNumber(text, "a = ")), FloatType);
        }

        [TestMethod]
        public void TestClassMethod() {
            var text = @"
class x(object):
    @classmethod
    def ClassMethod(cls):
        return cls

a = x().ClassMethod()
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetTypesFromName("a", GetLineNumber(text, "a =")), TypeType);

            var exprs = new[] { "x.ClassMethod", "x().ClassMethod" };
            foreach (var expr in exprs) {
                var sigs = entry.GetSignatures(expr, GetLineNumber(text, "a = ")).ToArray();
                Assert.AreEqual(1, sigs.Length);
                Assert.AreEqual(sigs[0].Parameters.Length, 0); // cls is implicitly implied
            }
        }

        [TestMethod]
        public void TestUserDescriptor() {
            var text = @"
class mydesc(object):
    def __get__(self, inst, ctx):
        return 42

class C(object):
    x = mydesc()

foo = C.x
bar = C().x
";
            var entry = ProcessText(text);
            var mems = entry.GetMembers("C().x", GetLineNumber(text, "bar ="));
            AssertContainsExactly(mems.Select(m => m.Name), _intMembers);

            mems = entry.GetMembers("C.x", GetLineNumber(text, "bar ="));
            AssertContainsExactly(mems.Select(m => m.Name), _intMembers);

            AssertContainsExactly(entry.GetTypesFromName("foo", GetLineNumber(text, "foo = ")), IntType);
            AssertContainsExactly(entry.GetTypesFromName("bar", GetLineNumber(text, "bar = ")), IntType);

            AssertContainsExactly(entry.GetTypesFromName("ctx", GetLineNumber(text, "return 42")), TypeType);
            AssertContainsExactly(entry.GetTypesFromName("inst", GetLineNumber(text, "return 42")), NoneType);

            text = @"
class mydesc(object):
    def __get__(self, inst, ctx):
        return 42

class C(object):
    x = mydesc()
    def instfunc(self):
        pass

bar = C().x
";

            entry = ProcessText(text);
            AssertContains(entry.GetMembers("inst", GetLineNumber(text, "return 42")).Select(m => m.Name), "instfunc");
        }

        [TestMethod]
        public void TestAssignSelf() {
            var text = @"
class x(object):
    def __init__(self):
        self.x = 'abc'
    def f(self):
        pass
";
            var entry = ProcessText(text);
            AssertContains(entry.GetMembersFromName("self", GetLineNumber(text, "pass")), "x");
            AssertContainsExactly(entry.GetMembers("self.x", GetLineNumber(text, "pass")).Select(m => m.Name), _strMembers);
        }

        class EmptyAnalysisCookie : IAnalysisCookie {
            public static EmptyAnalysisCookie Instance = new EmptyAnalysisCookie();
            public string GetLine(int lineNo) {
                throw new NotImplementedException();
            }
        }


        [TestMethod]
        public void TestMoveClass() {
            var fooSrc = GetSourceUnit("from bar import C", @"foo.py");

            var barSrc = GetSourceUnit(@"
class C(object):
    pass
", @"bar.py");

            var bazSrc = GetSourceUnit(@"
class C(object):
    pass
", @"baz.py");

            var state = new PythonAnalyzer(Interpreter, PythonLanguageVersion.V27);

            var foo = state.AddModule("foo", @"foo.py", EmptyAnalysisCookie.Instance);
            var bar = state.AddModule("bar", @"bar.py", EmptyAnalysisCookie.Instance);
            var baz = state.AddModule("baz", @"baz.py", EmptyAnalysisCookie.Instance);

            Prepare(foo, fooSrc);
            Prepare(bar, barSrc);
            Prepare(baz, bazSrc);

            foo.Analyze();
            bar.Analyze();
            baz.Analyze();

            Assert.AreEqual(foo.Analysis.GetValues("C", 1).First().Description, "class C");
            Assert.IsTrue(foo.Analysis.GetValues("C", 1).First().Location.FilePath.EndsWith("bar.py"));

            barSrc = GetSourceUnit(@"
", @"bar.py");

            // delete the class..
            Prepare(bar, barSrc);
            bar.Analyze();
            bar.AnalysisGroup.AnalyzeQueuedEntries();

            Assert.AreEqual(foo.Analysis.GetValues("C", 1).ToArray().Length, 0);

            fooSrc = GetSourceUnit("from baz import C", @"foo.py");
            Prepare(foo, fooSrc);

            foo.Analyze();

            Assert.AreEqual(foo.Analysis.GetValues("C", 1).First().Description, "class C");
            Assert.IsTrue(foo.Analysis.GetValues("C", 1).First().Location.FilePath.EndsWith("baz.py"));
        }

        [TestMethod]
        public void TestPackage() {
            var src1 = GetSourceUnit("", @"C:\\Test\\Lib\\foo\\__init__.py");

            var src2 = GetSourceUnit(@"
from foo.y import abc
import foo.y as y
", @"C:\\Test\\Lib\\foo\\x.py");

            var src3 = GetSourceUnit(@"
abc = 42
", @"C:\\Test\\Lib\\foo\\y.py");

            var state = new PythonAnalyzer(Interpreter, PythonLanguageVersion.V27);

            var package = state.AddModule("foo", @"C:\\Test\\Lib\\foo\\__init__.py", EmptyAnalysisCookie.Instance);
            var x = state.AddModule("foo.x", @"C:\\Test\\Lib\\foo\\x.py", EmptyAnalysisCookie.Instance);
            var y = state.AddModule("foo.y", @"C:\\Test\\Lib\\foo\\y.py", EmptyAnalysisCookie.Instance);

            Prepare(package, src1);
            Prepare(x, src2);
            Prepare(y, src3);

            package.Analyze();
            x.Analyze();
            y.Analyze();

            Assert.AreEqual(x.Analysis.GetValues("y", 1).First().Description, "Python module foo.y");
            AssertContainsExactly(x.Analysis.GetTypesFromName("abc", 1), IntType);
        }

        [TestMethod]
        public void TestPackageRelativeImport() {
            string tempPath = Path.GetTempPath();
            Directory.CreateDirectory(Path.Combine(tempPath, "foo"));

            var files = new[] { 
                new { Content = "from .y import abc", FullPath = Path.Combine(tempPath, "foo\\__init__.py") },
                new { Content = "from .y import abc", FullPath = Path.Combine(tempPath, "foo\\x.py") } ,
                new { Content = "abc = 42",           FullPath = Path.Combine(tempPath, "foo\\y.py") } 
            };

            var srcs = new TextReader[files.Length];
            for(int i = 0; i<files.Length; i++) {
                srcs[i] = GetSourceUnit(files[i].Content, files[i].FullPath);
                File.WriteAllText(files[i].FullPath, files[i].Content);
            }
            
            var src1 = srcs[0];
            var src2 = srcs[1];
            var src3 = srcs[2];

            var state = new PythonAnalyzer(Interpreter, PythonLanguageVersion.V27);

            var package = state.AddModule("foo", files[0].FullPath, EmptyAnalysisCookie.Instance);
            var x = state.AddModule("foo.x", files[1].FullPath, EmptyAnalysisCookie.Instance);
            var y = state.AddModule("foo.y", files[2].FullPath, EmptyAnalysisCookie.Instance);

            Prepare(package, src1);
            Prepare(x, src2);
            Prepare(y, src3);

            package.Analyze();
            x.Analyze();
            y.Analyze();

            AssertContainsExactly(x.Analysis.GetTypesFromName("abc", 1), IntType);
            AssertContainsExactly(package.Analysis.GetTypesFromName("abc", 1), IntType);
        }

        [TestMethod]
        public void TestPackageRelativeImportAliasedMember() {
            // similar to unittest package which has unittest.main which contains a function called "main".
            // Make sure we see the function, not the module.
            string tempPath = Path.GetTempPath();
            Directory.CreateDirectory(Path.Combine(tempPath, "foo"));

            var files = new[] { 
                new { Content = "from .y import y", FullPath = Path.Combine(tempPath, "foo\\__init__.py") },
                new { Content = "def y(): pass",    FullPath = Path.Combine(tempPath, "foo\\y.py") } 
            };

            var srcs = new TextReader[files.Length];
            for (int i = 0; i < files.Length; i++) {
                srcs[i] = GetSourceUnit(files[i].Content, files[i].FullPath);
                File.WriteAllText(files[i].FullPath, files[i].Content);
            }

            var src1 = srcs[0];
            var src2 = srcs[1];

            var state = new PythonAnalyzer(Interpreter, PythonLanguageVersion.V27);

            var package = state.AddModule("foo", files[0].FullPath, EmptyAnalysisCookie.Instance);
            var y = state.AddModule("foo.y", files[1].FullPath, EmptyAnalysisCookie.Instance);

            Prepare(package, src1);
            Prepare(y, src2);

            package.Analyze();
            y.Analyze();

            AssertContainsExactly(package.Analysis.GetTypesFromName("y", 1), FunctionType, ModuleType);
        }


        /// <summary>
        /// Verify that the analyzer has the proper algorithm for turning a filename into a package name
        /// </summary>
        //[TestMethod] //FIXME, use files which exist somewhere
        public void TestPathToModuleName() {
            string nzmathPath = Path.Combine(Environment.GetEnvironmentVariable("DLR_ROOT"), @"External.LCA_RESTRICTED\Languages\IronPython\Math");

            Assert.AreEqual(PythonAnalyzer.PathToModuleName(Path.Combine(nzmathPath, @"nzmath\factor\__init__.py")), "nzmath.factor");
            Assert.AreEqual(PythonAnalyzer.PathToModuleName(Path.Combine(nzmathPath, @"nzmath\factor\find.py")), "nzmath.factor.find");
        }

        [TestMethod]
        public void TestDefaults() {
            var text = @"
def f(x = 42):
    return x
    
a = f()
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetTypesFromName("a", GetLineNumber(text, "a =")), IntType);
        }

        [TestMethod]
        public void TestClassInit() {
            var text = @"
class X:
    def __init__(self, value):
        self.value = value

a = X(2)
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetTypesFromName("value", GetLineNumber(text, " = value")), IntType);
        }

        [TestMethod]
        public void TestClassNew() {
            var text = @"
class X:
    def __new__(cls, value):
        res = object.__new__(cls)
        res.value = value
        return res

a = X(2)
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetTypesFromName("value", GetLineNumber(text, " = value")), IntType);
        }

        public static IEnumerable<string> GetVariableDescriptions(ModuleAnalysis entry, string variable, int position) {
            return entry.GetValues(variable, position).Select(m => m.Description);
        }

        public static IEnumerable<string> GetVariableShortDescriptions(ModuleAnalysis entry, string variable, int position) {
            return entry.GetValues(variable, position).Select(m => m.ShortDescription);
        }

        [TestMethod]
        public void TestIsInstance() {
            var text = @"
x = None


if True:
    pass
    assert isinstance(x, int)
    z = 100      
    pass    
else:
    pass
    assert isinstance(x, str)
    y = 200
    pass
    




if isinstance(x, tuple):    
    foo = 300
    pass
";

            var entry = ProcessText(text);
            AssertContainsExactly(GetVariableDescriptions(entry, "x", GetLineNumber(text, "z =")), "int");
            AssertContainsExactly(GetVariableDescriptions(entry, "x", GetLineNumber(text, "z =") + 1), "int");
            AssertContainsExactly(GetVariableDescriptions(entry, "x", GetLineNumber(text, "z =") - 2), "None");
            AssertContainsExactly(GetVariableDescriptions(entry, "x", GetLineNumber(text, "y =")), "str");
            AssertContainsExactly(GetVariableDescriptions(entry, "x", GetLineNumber(text, "y =") + 1), "str");
            AssertContainsExactly(GetVariableDescriptions(entry, "x", GetLineNumber(text, "y =") - 2), "None");
            AssertContainsExactly(GetVariableDescriptions(entry, "x", GetLineNumber(text, "foo =")), "tuple");
            AssertContainsExactly(GetVariableDescriptions(entry, "x", GetLineNumber(text, "foo =") + 1), "tuple");

            VerifyReferences(
                entry.GetVariables("x", 1),
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(7, 23, VariableType.Reference),
                new VariableLocation(12, 23, VariableType.Reference),
                new VariableLocation(20, 15, VariableType.Reference)
            );

            VerifyReferences(
                UniqifyVariables(entry.GetVariables("x", GetLineNumber(text, "z ="))),
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(7, 23, VariableType.Reference),
                new VariableLocation(12, 23, VariableType.Reference),
                new VariableLocation(20, 15, VariableType.Reference)
            );

            VerifyReferences(
                UniqifyVariables(entry.GetVariables("x", GetLineNumber(text, "z =") + 1)),
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(7, 23, VariableType.Reference),
                new VariableLocation(12, 23, VariableType.Reference),
                new VariableLocation(20, 15, VariableType.Reference)
            );

            VerifyReferences(
                UniqifyVariables(entry.GetVariables("x", GetLineNumber(text, "z =") - 2)),
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(7, 23, VariableType.Reference),
                new VariableLocation(12, 23, VariableType.Reference),
                new VariableLocation(20, 15, VariableType.Reference)
            );

            VerifyReferences(
                UniqifyVariables(entry.GetVariables("x", GetLineNumber(text, "y ="))),
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(7, 23, VariableType.Reference),
                new VariableLocation(12, 23, VariableType.Reference),
                new VariableLocation(20, 15, VariableType.Reference)
            );

            VerifyReferences(
                UniqifyVariables(entry.GetVariables("x", GetLineNumber(text, "y =") + 1)),
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(7, 23, VariableType.Reference),
                new VariableLocation(12, 23, VariableType.Reference),
                new VariableLocation(20, 15, VariableType.Reference)
            );

            VerifyReferences(
                UniqifyVariables(entry.GetVariables("x", GetLineNumber(text, "y =") - 2)),
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(7, 23, VariableType.Reference),
                new VariableLocation(12, 23, VariableType.Reference),
                new VariableLocation(20, 15, VariableType.Reference)
            );

            text = @"
def f(a):    
    def g():
        nonlocal a
        print(a)
        assert isinstance(a, int)
        pass
";

            entry = ProcessText(text, PythonLanguageVersion.V32);
            AssertContainsExactly(GetVariableDescriptions(entry, "a", GetLineNumber(text, "f(a)")));
            AssertContainsExactly(GetVariableDescriptions(entry, "a", GetLineNumber(text, "pass")), "int");
            AssertContainsExactly(GetVariableDescriptions(entry, "a", GetLineNumber(text, "pass") - 2));

            text = @"x = None


if True:
    pass
    assert isinstance(x, int)
    z = 100      
    
    pass    

print(z)";

            entry = ProcessText(text);
            AssertContainsExactly(GetVariableDescriptions(entry, "z", GetLineNumber(text, "z =")), "int");
            AssertContainsExactly(GetVariableDescriptions(entry, "z", 1), "int");
            AssertContainsExactly(GetVariableDescriptions(entry, "z", GetLineNumber(text, "print(z)") - 2), "int");

            VerifyReferences(
                UniqifyVariables(entry.GetVariables("z", GetLineNumber(text, "print(z)"))),
                new VariableLocation(7, 5, VariableType.Definition),
                new VariableLocation(11, 7, VariableType.Reference)
            );

            VerifyReferences(
                UniqifyVariables(entry.GetVariables("z", GetLineNumber(text, "z ="))),
                new VariableLocation(7, 5, VariableType.Definition),
                new VariableLocation(11, 7, VariableType.Reference)
            );
        }

        [TestMethod]
        public void TestQuickInfo() {
            var text = @"
import sys
a = 41.0
b = 42L
c = 'abc'
x = (2, 3, 4)
y = [2, 3, 4]
z = 43

class foo(object):
    @property
    def f(self): pass

    def g(self): pass
    
d = foo()

def f():
    print 'hello'
    return 'abc'

def g():
    return c.Length


class return_func_class:
    def return_func(self):
        '''some help'''
        return self.return_func
";
            var entry = ProcessText(text);

            AssertContainsExactly(GetVariableDescriptions(entry, "foo()", 1), "foo instance");
            AssertContainsExactly(GetVariableDescriptions(entry, "int()", 1), "int");
            AssertContainsExactly(GetVariableDescriptions(entry, "a", 1), "float");
            AssertContainsExactly(GetVariableDescriptions(entry, "a", 1), "float");
            AssertContainsExactly(GetVariableDescriptions(entry, "b", 1), "long");
            AssertContainsExactly(GetVariableDescriptions(entry, "c", 1), "str");
            AssertContainsExactly(entry.GetValues("x", 1).Select(v => v.Description.Substring(0, 5)), "tuple");
            AssertContainsExactly(entry.GetValues("y", 1).Select(v => v.Description.Substring(0, 4)), "list");
            AssertContainsExactly(GetVariableDescriptions(entry, "z", 1), "int");
            AssertContainsExactly(GetVariableDescriptions(entry, "min", 1), "built-in function min");
            AssertContainsExactly(GetVariableDescriptions(entry, "list.append", 1), "built-in method append");
            AssertContainsExactly(GetVariableDescriptions(entry, "\"abc\".Length", 1));
            AssertContainsExactly(GetVariableDescriptions(entry, "c.Length", 1));
            AssertContainsExactly(GetVariableDescriptions(entry, "d", 1), "foo instance");
            AssertContainsExactly(GetVariableDescriptions(entry, "sys", 1), "built-in module sys");
            AssertContainsExactly(GetVariableDescriptions(entry, "f", 1), "def f(...)");
            AssertContainsExactly(GetVariableDescriptions(entry, "foo.f", 1), "def f(...)");
            AssertContainsExactly(GetVariableDescriptions(entry, "foo().g", 1), "method g of foo objects ");
            AssertContainsExactly(GetVariableDescriptions(entry, "foo", 1), "class foo");
            //AssertContainsExactly(GetVariableDescriptions(entry, "System.StringSplitOptions.RemoveEmptyEntries", 0), "field of type StringSplitOptions");
            AssertContainsExactly(GetVariableDescriptions(entry, "g", 1), "def g(...)");    // return info could be better
            //AssertContainsExactly(GetVariableDescriptions(entry, "System.AppDomain.DomainUnload", 1), "event of type System.EventHandler");
            AssertContainsExactly(GetVariableDescriptions(entry, "None", 1), "None");
            AssertContainsExactly(GetVariableDescriptions(entry, "f.func_name", 1), "property of type str");

            // method which returns it's self, we shouldn't stack overflow producing the help...
            AssertContainsExactly(GetVariableDescriptions(entry, "return_func_class().return_func", 1), @"method return_func of return_func_class objects  -> method return_func of return_func_class objects ...

some help

some help");
        }

        [TestMethod]
        public void TestCompletionDocumentation() {
            var text = @"
import sys
a = 41.0
b = 42L
c = 'abc'
x = (2, 3, 4)
y = [2, 3, 4]
z = 43

class foo(object):
    @property
    def f(self): pass

    def g(self): pass
    
d = foo()

def f():
    print 'hello'
    return 'abc'

def g():
    return c.Length
";
            var entry = ProcessText(text);

            AssertContains(GetCompletionDocumentation(entry, "f", "func_name", 0).First(), "-> str", " = value");
            AssertContains(GetCompletionDocumentation(entry, "", "int", 0).First(), "integer");
            AssertContains(GetCompletionDocumentation(entry, "", "min", 0).First(), "min(");
        }

        [TestMethod]
        public void TestMemberType() {
            var text = @"
import sys
a = 41.0
b = 42L
c = 'abc'
x = (2, 3, 4)
y = [2, 3, 4]
z = 43

class foo(object):
    @property
    def f(self): pass

    def g(self): pass
    
d = foo()

def f():
    print 'hello'
    return 'abc'

def g():
    return c.Length
";
            var entry = ProcessText(text);


            Assert.AreEqual(GetMember(entry, "f", "func_name", 0).First().MemberType, PythonMemberType.Property);
            Assert.AreEqual(GetMember(entry, "list", "append", 0).First().MemberType, PythonMemberType.Method);
            Assert.AreEqual(GetMember(entry, "y", "append", 0).First().MemberType, PythonMemberType.Method);
            Assert.AreEqual(GetMember(entry, "", "int", 0).First().MemberType, PythonMemberType.Class);
            Assert.AreEqual(GetMember(entry, "", "min", 0).First().MemberType, PythonMemberType.Function);
            Assert.AreEqual(GetMember(entry, "", "sys", 0).First().MemberType, PythonMemberType.Module);
        }

        private void AssertContains(string source, params string[] values) {
            foreach (var v in values) {
                if (!source.Contains(v)) {
                    Assert.Fail(String.Format("Failed to find {0} in {1}", v, source));
                }
            }
        }

        public static IEnumerable<string> GetCompletionDocumentation(ModuleAnalysis entry, string variable, string memberName, int position) {
            return GetMember(entry, variable, memberName, position).Select(m => m.Documentation);
        }

        public static IEnumerable<MemberResult> GetMember(ModuleAnalysis entry, string variable, string memberName, int position) {
            return entry.GetMembers(variable, position).Where(m => m.Name == memberName);
        }

        [TestMethod]
        public void TestRecurisveDataStructures() {
            var text = @"
d = {}
d[0] = d
";
            var entry = ProcessText(text);

            AssertContainsExactly(GetVariableDescriptions(entry, "d", 1), "dict({int : dict})");
        }

        /// <summary>
        /// Variable is refered to in the base class, defined in the derived class, we should know the type information.
        /// </summary>
        [TestMethod]
        public void TestBaseReferencedDerivedDefined() {
            var text = @"
class Base(object):
    def f(self):
        x = self.map

class Derived(Base):
    def __init__(self):
        self.map = {}

pass
";

            var entry = ProcessText(text);
            var members = entry.GetMembers("Derived()", GetLineNumber(text, "pass")).ToArray();
            var map = members.First(x => x.Name == "map");

            Assert.AreEqual(map.MemberType, PythonMemberType.Field);
        }


        /// <summary>
        /// Test case where we have a member but we don't have any type information for the member.  It should
        /// still show up as a member.
        /// </summary>
        [TestMethod]
        public void TestNoTypesButIsMember() {
            var text = @"
def f(x, y):
	C(x, y)

class C(object):
    def __init__(self, x, y):
        self.x = x
        self.y = y

f(1)
";

            var entry = ProcessText(text);
            var members = entry.GetMembers("C()", GetLineNumber(text, "f(1")).ToArray();
            AssertContainsExactly(members.Where(mem => !mem.Name.StartsWith("__")).Select(x => x.Name), "x", "y");
        }

        /// <summary>
        /// Test case where we have a member but we don't have any type information for the member.  It should
        /// still show up as a member.
        /// </summary>
        [TestMethod]
        public void SequenceFromSequence() {
            var text = @"
x = []
x.append(1)

t = (1, )

class MyIndexer(object):
    def __getitem__(self, index):
        return 1

ly = list(x)
lz = list(MyIndexer())

ty = tuple(x)
tz = tuple(MyIndexer())

lyt = list(t)
tyt = tuple(t)

pass
";

            var entry = ProcessText(text);
            
            var vars = new List<IAnalysisValue>(entry.GetValues("x[0]", GetLineNumber(text, "pass")));            
            Assert.AreEqual(vars.Count, 1);
            Assert.AreEqual(IntType, vars[0].PythonType);

            foreach (string value in new[] { "ly", "lz", "ty", "tz", "lyt", "tyt" }) {
                vars = new List<IAnalysisValue>(entry.GetValues(value + "[0]", GetLineNumber(text, "pass")));
                Assert.AreEqual(vars.Count, 1);
                Assert.AreEqual(vars[0].PythonType, IntType);
            }
        }

#if FALSE
        [TestMethod]
        public void SaveStdLib() {
            // only run this once...
            if (GetType() == typeof(AnalysisTest)) {
                var stdLib = AnalyzeStdLib();

                string tmpFolder = Path.Combine(Path.GetTempPath(), "6666d700-a6d8-4e11-8b73-3ba99a61e27b" /*Guid.NewGuid().ToString()*/);
                Directory.CreateDirectory(tmpFolder);

                new SaveAnalysis().Save(stdLib, tmpFolder);

                File.Copy(Path.Combine(CPythonInterpreterFactory.GetBaselineDatabasePath(), "__builtin__.idb"), Path.Combine(tmpFolder, "__builtin__.idb"), true);

                var newPs = new PythonAnalyzer(new CPythonInterpreter(new TypeDatabase(tmpFolder)), PythonLanguageVersion.V27);
            }
        }
#endif


        [TestMethod]
        public void SubclassFindAllRefs() {
            string text = @"
class Base(object):
    def __init__(self):
        self.foo()
    
    def foo(self): 
        pass
    
    
class Derived(Base):
    def foo(self): 
        'x'
";

            var entry = ProcessText(text);

            var vars = entry.GetVariables("self.foo", GetLineNumber(text, "'x'"));
            VerifyReferences(UniqifyVariables(vars), new VariableLocation(11, 9, VariableType.Definition), new VariableLocation(6, 9, VariableType.Definition), new VariableLocation(4, 14, VariableType.Reference));

            vars = entry.GetVariables("self.foo", GetLineNumber(text, "pass"));
            VerifyReferences(UniqifyVariables(vars), new VariableLocation(11, 9, VariableType.Definition), new VariableLocation(6, 9, VariableType.Definition), new VariableLocation(4, 14, VariableType.Reference));

            vars = entry.GetVariables("self.foo", GetLineNumber(text, "self.foo"));
            VerifyReferences(UniqifyVariables(vars), new VariableLocation(11, 9, VariableType.Definition), new VariableLocation(6, 9, VariableType.Definition), new VariableLocation(4, 14, VariableType.Reference));
        }
        
        /// <summary>
        /// Verifies that constructing lists / tuples from more lists/tuples doesn't cause an infinite analysis as we keep creating more lists/tuples.
        /// </summary>
        [TestMethod]
        public void TestListRecursion() {
            string text = @"
def f(x):
    print abc
    return f(list(x))

abc = f(())
";

            var entry = ProcessText(text);

            //var vars = entry.GetVariables("foo", GetLineNumber(text, "'x'"));

        }

        [TestMethod]
        public void TypeAtEndOfMethod() {
            string text = @"
class Foo(object):
    def bar(self, a):
        pass
    
    
    def foo(self): 
        pass

x = Foo()
x.bar(100)
";

            var entry = ProcessText(text);

            AssertContainsExactly(entry.GetTypesFromName("a", GetLineNumber(text, "foo") - 1), IntType);
        }

        [TestMethod]
        public void TypeIntersectionUserDefinedTypes() {
            string text = @"
class C1(object):
    def foo(self): pass

class C2(object):
    def bar(self): pass

c = C1()
c.foo()
c = C2()

";

            var entry = ProcessText(text);
            var members = entry.GetMembers("a", GetLineNumber(text, "c = C2()"), GetMemberOptions.IntersectMultipleResults);
            AssertDoesntContain(members.Select(x => x.Name), "foo");
        }

        [TestMethod]
        public void UpdateMethodMultiFiles() {
            string text1 = @"
def f(abc):
    pass
";

            string text2 = @"
import mod1
mod1.f(42)
";
            
            var state = new PythonAnalyzer(Interpreter, PythonLanguageVersion.V26);
            
            // add both files to the project
            var entry1 = state.AddModule("mod1", "mod1", null);
            var entry2 = state.AddModule("mod2", "mod2", null);

            // analyze both files
            Prepare(entry1, GetSourceUnit(text1, "mod1"), PythonLanguageVersion.V26);
            entry1.Analyze();
            Prepare(entry2, GetSourceUnit(text2, "mod2"), PythonLanguageVersion.V26);
            entry2.Analyze();

            AssertContainsExactly(entry1.Analysis.GetTypesFromName("abc", GetLineNumber(text1, "pass") ), IntType);

            // re-analyze project1, we should still know about the type info provided by module2
            Prepare(entry1, GetSourceUnit(text1, "mod1"), PythonLanguageVersion.V26);
            entry1.Analyze();
            
            AssertContainsExactly(entry1.Analysis.GetTypesFromName("abc", GetLineNumber(text1, "pass") ), IntType);
        }

        [TestMethod]
        public void TestMetaClasses() {
            
            string text = @"class C(type):
    def f(self):
        print('C.f')

    def x(self, var):
        pass


class D(object):
    __metaclass__ = C
    @classmethod
    def g(cls):
        print cls.g


    def inst_method(self):
        pass
    ";
            
            var entry = ProcessText(text);
            Assert.AreEqual(entry.GetSignatures("cls.f", GetLineNumber(text, "print cls.g")).First().Parameters.Length, 0);
            Assert.AreEqual(entry.GetSignatures("cls.g", GetLineNumber(text, "print cls.g")).First().Parameters.Length, 0);
            Assert.AreEqual(entry.GetSignatures("cls.x", GetLineNumber(text, "print cls.g")).First().Parameters.Length, 1);
            Assert.AreEqual(entry.GetSignatures("cls.inst_method", GetLineNumber(text, "print cls.g")).First().Parameters.Length, 1);

            text = @"class C(type):
    def f(self):
        print('C.f')

    def x(self, var):
        pass


class D(object, metaclass = C):
    @classmethod
    def g(cls):
        print(cls.g)


    def inst_method(self):
        pass
    ";

            entry = ProcessText(text, PythonLanguageVersion.V32);
            Assert.AreEqual(entry.GetSignatures("cls.f", GetLineNumber(text, "print(cls.g)")).First().Parameters.Length, 0);
            Assert.AreEqual(entry.GetSignatures("cls.g", GetLineNumber(text, "print(cls.g)")).First().Parameters.Length, 0);
            Assert.AreEqual(entry.GetSignatures("cls.x", GetLineNumber(text, "print(cls.g)")).First().Parameters.Length, 1);
            Assert.AreEqual(entry.GetSignatures("cls.inst_method", GetLineNumber(text, "print(cls.g)")).First().Parameters.Length, 1);



        }

        /// <summary>
        /// Tests assigning odd things to the metaclass variable.
        /// </summary>
        [TestMethod]
        public void TestMethodClassNegative() {
            var assigns = new[] { "[1,2,3]", "(1,2)", "1", "abc", "1.0", "lambda x: 42", "C.f", "C().f", "f", "{2:3}" };

            foreach (var assign in assigns) {
                string text = @"
class C(object): 
    def f(self): pass

def f():  pass

class D(object):
    __metaclass__ = " + assign + @"
    @classmethod
    def g(cls):
        print cls.g


    def inst_method(self):
        pass
    ";

                ProcessText(text);
            }
        }

        protected IEnumerable<IAnalysisVariable> UniqifyVariables(IEnumerable<IAnalysisVariable> vars) {
            Dictionary<LocationInfo, IAnalysisVariable> res = new Dictionary<LocationInfo,IAnalysisVariable>();
            foreach (var v in vars) {
                if (!res.ContainsKey(v.Location) || res[v.Location].Type == VariableType.Value) {
                    res[v.Location] = v;
                }
            }

            return res.Values;

        }

        #endregion

        #region Helpers

        private static string[] GetMembers(object obj, bool showClr) {
            var dir = showClr ? ClrModule.DirClr(obj) : ClrModule.Dir(obj);
            int len = dir.__len__();
            string[] result = new string[len];
            for (int i = 0; i < len; i++) {
                Assert.IsTrue(dir[i] is string);
                result[i] = dir[i] as string;
            }
            return result;
        }



        /// <summary>
        /// Returns all the permutations of the set [0 ... n-1]
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        private static IEnumerable<List<int>> Permutations(int n) {
            if (n <= 0) {
                yield return new List<int>();
            } else {
                foreach (var prev in Permutations(n - 1)) {
                    for (int i = n - 1; i >= 0; i--) {
                        var result = new List<int>(prev);
                        result.Insert(i, n - 1);
                        yield return result;
                    }
                }
            }
        }

        private IEnumerable<IPythonProjectEntry[]> MakeModulePermutations(string prefix, string[] code) {
            foreach (var p in Permutations(code.Length)) {
                var result = new IPythonProjectEntry[code.Length];
                var state = new PythonAnalyzer(Interpreter, PythonLanguageVersion.V27);
                for (int i = 0; i < code.Length; i++) {
                    result[p[i]] = state.AddModule(prefix + (p[i] + 1).ToString(), "foo", null);
                }
                for (int i = 0; i < code.Length; i++) {
                    Prepare(result[p[i]], GetSourceUnit(code[p[i]]));
                }
                for (int i = 0; i < code.Length; i++) {
                    result[p[i]].Analyze();
                }
                yield return result;
            }
        }

        /// <summary>
        /// For a given set of module definitions, build analysis info for each unique permutation
        /// of the ordering of the defintions and run the test against each analysis.
        /// </summary>
        /// <param name="prefix">Prefix for the module names. The first source text will become prefix + "1", etc.</param>
        /// <param name="code">The source code for each of the modules</param>
        /// <param name="test">The test to run against the analysis</param>
        private void PermutedTest(string prefix, string[] code, Action<IPythonProjectEntry[]> test) {
            foreach (var pe in MakeModulePermutations(prefix, code)) {
                test(pe);
                Console.WriteLine("--- End Permutation ---");
            }
        }


        private static string[] GetUnion(params object[] objs) {
            var result = new HashSet<string>();
            foreach (var obj in objs) {
                if (obj is string) {
                    result.Add((string)obj);
                } else if (obj is IEnumerable<string>) {
                    result.UnionWith((IEnumerable<string>)obj);
                } else {
                    throw new NotImplementedException("Non-string member");
                }
            }
            return result.ToArray();
        }

        private static string[] GetIntersection(IEnumerable<string> first, params IEnumerable<string>[] remaining) {
            var result = new HashSet<string>(first);
            foreach (var obj in remaining) {
                result.IntersectWith((IEnumerable<string>)obj);
            }
            return result.ToArray();
        }

        #endregion
    }

    static class ModuleAnalysisExtensions {
        /// <summary>
        /// TODO: This method should go away, it's only being used for tests, and the tests should be using GetMembersFromExpression
        /// which may need to be cleaned up.
        /// </summary>
        public static IEnumerable<string> GetMembersFromName(this ModuleAnalysis analysis, string name, int lineNumber) {
            return analysis.GetMembers(name, lineNumber).Select(m => m.Name);
        }
    }
}

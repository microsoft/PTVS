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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using IronPython.Runtime;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.Scripting.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace AnalysisTests {
    [TestClass]
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
            foreach (var type in new[] { typeof(AnalysisTest) }) {
                if (type.IsDefined(typeof(TestClassAttribute), false)) {

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
                        Console.WriteLine("Running tests against: {0}", instType.FullName);
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

            if (inst != null) {
                Console.WriteLine();
                if (failures == 0) {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("No failures");
                } else {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("{0} failures", failures);
                }
                Console.ForegroundColor = fg;
            }
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

            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("foo", code.IndexOf("pass")), Interpreter.GetBuiltinType(BuiltinTypeId.Tuple));
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("bar", code.IndexOf("pass")), Interpreter.GetBuiltinType(BuiltinTypeId.Dict));

            code = @"def f(*foo):
    pass

f(42)
";
            entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("foo", code.IndexOf("pass")), Interpreter.GetBuiltinType(BuiltinTypeId.Tuple));
            AssertUtil.ContainsExactly(entry.GetValuesByIndex("foo[0]", code.IndexOf("pass")).Select(x => x.PythonType), Interpreter.GetBuiltinType(BuiltinTypeId.Int));

            code = @"def f(*foo):
    pass

f(42, 'abc')
";
            entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("foo", code.IndexOf("pass")), Interpreter.GetBuiltinType(BuiltinTypeId.Tuple));
            AssertUtil.ContainsExactly(entry.GetValuesByIndex("foo[0]", code.IndexOf("pass")).Select(x => x.PythonType), Interpreter.GetBuiltinType(BuiltinTypeId.Int), Interpreter.GetBuiltinType(BuiltinTypeId.Bytes));

            code = @"def f(**bar):
    pass

f(x=42)
";
            entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("bar", code.IndexOf("pass")), Interpreter.GetBuiltinType(BuiltinTypeId.Dict));
            AssertUtil.ContainsExactly(entry.GetValuesByIndex("bar['foo']", code.IndexOf("pass")).Select(x => x.PythonType), Interpreter.GetBuiltinType(BuiltinTypeId.Int));


            code = @"def f(**bar):
    pass

f(x=42, y = 'abc')
";
            entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("bar", code.IndexOf("pass")), Interpreter.GetBuiltinType(BuiltinTypeId.Dict));
            AssertUtil.ContainsExactly(entry.GetValuesByIndex("bar['foo']", code.IndexOf("pass")).Select(x => x.PythonType), Interpreter.GetBuiltinType(BuiltinTypeId.Int), Interpreter.GetBuiltinType(BuiltinTypeId.Bytes));
        }

        [TestMethod]
        public void TestCartesianStarArgs() {
            var code = @"def f(a, **args):
    args['foo'] = a
    return args['foo']


x = f(42)
y = f('abc')";

            var entry = ProcessText(code);

            AssertUtil.Contains(entry.GetValuesByIndex("x", code.IndexOf("x =")).Select(x => x.PythonType.Name), "int");
            AssertUtil.Contains(entry.GetValuesByIndex("y", code.IndexOf("x =")).Select(x => x.PythonType.Name), "str");


            code = @"def f(a, **args):
    for i in xrange(2):
        if i == 1:
            return args['foo']
        else:
            args['foo'] = a

x = f(42)
y = f('abc')";

            entry = ProcessText(code);

            AssertUtil.Contains(entry.GetValuesByIndex("x", code.IndexOf("x =")).Select(x => x.PythonType.Name), "int");
            AssertUtil.Contains(entry.GetValuesByIndex("y", code.IndexOf("x =")).Select(x => x.PythonType.Name), "str");
        }

        [TestMethod]
        public void TestCartesianRecursive() {
            var code = @"def f(a, *args):
    f(a, args)
    return a


x = f(42)";

            var entry = ProcessText(code);

            AssertUtil.Contains(entry.GetValuesByIndex("x", code.IndexOf("x =")).Select(x => x.PythonType.Name), "int");
        }

        [TestMethod]
        public void TestCartesianSimple() {
            var code = @"def f(a):
    return a


x = f(42)
y = f('foo')";

            var entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetValuesByIndex("x", code.IndexOf("x =")).Select(x => x.PythonType.Name), "int");
            AssertUtil.ContainsExactly(entry.GetValuesByIndex("y", code.IndexOf("y =")).Select(x => x.PythonType.Name), "str");
        }


        [TestMethod]
        public void TestCartesianLocals() {
            var code = @"def f(a):
    b = a
    return b


x = f(42)
y = f('foo')";

            var entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetValuesByIndex("x", code.IndexOf("x =")).Select(x => x.PythonType.Name), "int");
            AssertUtil.ContainsExactly(entry.GetValuesByIndex("y", code.IndexOf("y =")).Select(x => x.PythonType.Name), "str");
        }
        /*
        [TestMethod]
        public void TestCartesianContainerFactory() {
            var code = @"def list_fact(ctor):
    x = []
    for abc in xrange(10):
        x.append(ctor(abc))
    return x


a = list_fact(int)[0]
b = list_fact(str)[0]
";

            var entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetValuesByIndex("a", code.IndexOf("a =")).Select(x => x.PythonType.Name), "int");
            AssertUtil.ContainsExactly(entry.GetValuesByIndex("b", code.IndexOf("b =")).Select(x => x.PythonType.Name), "str");
        }*/

        [TestMethod]
        public void TestCartesianLocalsIsInstance() {
            var code = @"def f(a, c):
    if isinstance(c, int):
        b = a
        return b
    else:
        b = a
        return b


x = f(42, 'bar')
y = f('foo', 'bar')";

            var entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetValuesByIndex("x", code.IndexOf("x =")).Select(x => x.PythonType.Name), "int");
            AssertUtil.ContainsExactly(entry.GetValuesByIndex("y", code.IndexOf("y =")).Select(x => x.PythonType.Name), "str");
        }

        [TestMethod]
        public void TestImportAs() {
            var entry = ProcessText(@"import sys as s, array as a");

            AssertUtil.Contains(entry.GetMembersByIndex("s", 1).Select(x => x.Name), "winver");
            AssertUtil.Contains(entry.GetMembersByIndex("a", 1).Select(x => x.Name), "ArrayType");

            entry = ProcessText(@"import sys as s");
            AssertUtil.Contains(entry.GetMembersByIndex("s", 1).Select(x => x.Name), "winver");
        }

        [TestMethod]
        public void DictionaryKeyValues() {
            var code = @"x = {'abc': 42, 'bar': 'baz'}

i = x['abc']
s = x['bar']
";
            var entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetValuesByIndex("i", code.IndexOf("i =")).Select(x => x.PythonType.Name), "int");
            AssertUtil.ContainsExactly(entry.GetValuesByIndex("s", code.IndexOf("s =")).Select(x => x.PythonType.Name), "str");
        }

        [TestMethod]
        public void TestImportStar() {
            var entry = ProcessText(@"
from nt import *
            ");

            var members = entry.GetMembersByIndex("", 1).Select(x => x.Name);

            AssertUtil.Contains(members, "abort");

            entry = ProcessText(@"");

            // make sure abort hasn't become a builtin, if so this test needs to be updated
            // with a new name
            members = entry.GetMembersByIndex("", 1).Select(x => x.Name);
            foreach (var member in members) {
                if(member == "abort") {
                    Assert.Fail("abort has become a builtin, or a clean module includes it for some reason");
                }
            }
        }

        [TestMethod]
        public void TestImportTrailingComma() {
            var entry = ProcessText(@"
import nt,
            ");

            var members = entry.GetMembersByIndex("nt", 1).Select(x => x.Name);

            AssertUtil.Contains(members, "abort");
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
                UniqifyVariables(mod2.Analysis.GetVariablesByIndex("D", text2.IndexOf("class D"))),
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


            VerifyReferences(UniqifyVariables(mod1.Analysis.GetVariablesByIndex("SomeMethod", text1.IndexOf("SomeMethod"))), 
                new VariableLocation(5, 9, VariableType.Definition), new VariableLocation(5, 20, VariableType.Reference));

            // mutate 1st file
            text1 = text1.Substring(0, text1.IndexOf("    def")) + Environment.NewLine + text1.Substring(text1.IndexOf("    def"));
            Prepare(mod1, GetSourceUnit(text1, "mod1"));
            mod1.Analyze();

            VerifyReferences(UniqifyVariables(mod1.Analysis.GetVariablesByIndex("SomeMethod", text1.IndexOf("SomeMethod"))),
                new VariableLocation(6, 9, VariableType.Definition), new VariableLocation(5, 20, VariableType.Reference));

            // mutate 2nd file
            text2 = Environment.NewLine + text2;
            Prepare(mod2, GetSourceUnit(text2, "mod1"));
            mod2.Analyze();

            VerifyReferences(UniqifyVariables(mod1.Analysis.GetVariablesByIndex("SomeMethod", text1.IndexOf("SomeMethod"))),
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

            AssertUtil.ContainsExactly(entry.GetMembersFromNameByIndex("self", code.IndexOf("_C__X")), "__X", "__init__", "__doc__", "__class__");
            AssertUtil.ContainsExactly(entry.GetMembersFromNameByIndex("self", code.IndexOf("print")), "_C__X", "__init__", "__doc__", "__class__");

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

            AssertUtil.Equals(
                entry.GetSignaturesByIndex("self.f", code.IndexOf("self.f")).First().Parameters.Select(x => x.Name),
                "_C__A"
            );
            AssertUtil.Equals(
                entry.GetSignaturesByIndex("self.f", code.IndexOf("marker")).First().Parameters.Select(x => x.Name),
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
            AssertUtil.ContainsExactly(entry.GetMembersFromNameByIndex("self", code.IndexOf("self.__f")), GetUnion(_objectMembers, "__f", "__init__"));
            AssertUtil.ContainsExactly(entry.GetMembersFromNameByIndex("self", code.IndexOf("marker")), GetUnion(_objectMembers, "_C__f", "__init__"));

            code = @"
class C(object):
    __FOO = 42

    def f(self):
        abc = C.__FOO  # Completion should work here


xyz = C._C__FOO  # Advanced members completion should work here
";
            entry = ProcessText(code);
            AssertUtil.ContainsExactly(entry.GetMembersFromNameByIndex("C", code.IndexOf("abc = ")), GetUnion(_objectMembers, "__FOO", "f"));
            AssertUtil.ContainsExactly(entry.GetMembersFromNameByIndex("C", code.IndexOf("xyz = ")), GetUnion(_objectMembers, "_C__FOO", "f"));

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
            AssertUtil.ContainsExactly(entry.GetMembersByIndex("self.foo", code.IndexOf("self.foo")).Select(x => x.Name), _intMembers);
            AssertUtil.Contains(entry.GetMembersByIndex("self", code.IndexOf("self.foo")).Select(x => x.Name), "abc");
        }

        [TestMethod]
        public void TestMro() {
            // Successful: MRO is A B C D E F object
            var code = @"
O = object
class F(O): pass
class E(O): pass
class D(O): pass
class C(D,F): pass
class B(D,E): pass
class A(B,C): pass

a = A()
";

            var entry = ProcessText(code);

            var clsA = entry.GetValuesByIndex("A", code.IndexOf("a =")).FirstOrDefault() as Microsoft.PythonTools.Analysis.Values.ClassInfo;
            Assert.IsNotNull(clsA);
            var mroA = clsA.GetMro().SelectMany(ns => ns.Select(n => n.ShortDescription)).ToList();
            AssertUtil.ContainsExactly(mroA, "A", "B", "C", "D", "E", "F", "type object");

            // Unsuccessful: cannot order X and Y
            code = @"
O = object
class X(O): pass
class Y(O): pass
class A(X, Y): pass
class B(Y, X): pass
class C(A, B): pass

c = C()
";

            entry = ProcessText(code);

            var clsC = entry.GetValuesByIndex("C", code.IndexOf("c =")).FirstOrDefault() as Microsoft.PythonTools.Analysis.Values.ClassInfo;
            Assert.IsNotNull(clsC);
            Assert.IsNull(clsC.GetMro());

            // Unsuccessful: cannot order F and E
            code = @"
class F(object): remember2buy='spam'
class E(F): remember2buy='eggs'
class G(F,E): pass
G.remember2buy
";

            entry = ProcessText(code);
            var clsG = entry.GetValuesByIndex("G", code.IndexOf("G.remember2buy")).FirstOrDefault() as Microsoft.PythonTools.Analysis.Values.ClassInfo;
            Assert.IsNotNull(clsG);
            Assert.IsNull(clsG.GetMro());


            // Successful: exchanging bases of G fixes the ordering issue
            code = @"
class F(object): remember2buy='spam'
class E(F): remember2buy='eggs'
class G(E,F): pass
G.remember2buy
";

            entry = ProcessText(code);
            clsG = entry.GetValuesByIndex("G", code.IndexOf("G.remember2buy")).FirstOrDefault() as Microsoft.PythonTools.Analysis.Values.ClassInfo;
            Assert.IsNotNull(clsG);
            var mroG = clsG.GetMro().SelectMany(ns => ns.Select(n => n.ShortDescription)).ToList();
            AssertUtil.ContainsExactly(mroG, "G", "E", "F", "type object");

            // Successful: MRO is Z K1 K2 K3 D A B C E object
            code = @"
class A(object): pass
class B(object): pass
class C(object): pass
class D(object): pass
class E(object): pass
class K1(A,B,C): pass
class K2(D,B,E): pass
class K3(D,A):   pass
class Z(K1,K2,K3): pass
z = Z()
";

            entry = ProcessText(code);
            var clsZ = entry.GetValuesByIndex("Z", code.IndexOf("z =")).FirstOrDefault() as Microsoft.PythonTools.Analysis.Values.ClassInfo;
            Assert.IsNotNull(clsZ);
            var mroZ = clsZ.GetMro().SelectMany(ns => ns.Select(n => n.ShortDescription)).ToList();
            AssertUtil.ContainsExactly(mroZ, "Z", "K1", "K2", "K3", "D", "A", "B", "C", "E", "type object");

            // Successful: MRO is Z K1 K2 K3 D A B C E object
            code = @"
class A(int): pass
class B(float): pass
class C(str): pass
z = None
";

            entry = ProcessText(code);
            clsA = entry.GetValuesByIndex("A", code.IndexOf("z =")).FirstOrDefault() as Microsoft.PythonTools.Analysis.Values.ClassInfo;
            Assert.IsNotNull(clsA);
            mroA = clsA.GetMro().SelectMany(ns => ns.Select(n => n.ShortDescription)).ToList();
            AssertUtil.ContainsExactly(mroA, "A", "type int");

            var clsB = entry.GetValuesByIndex("B", code.IndexOf("z =")).FirstOrDefault() as Microsoft.PythonTools.Analysis.Values.ClassInfo;
            Assert.IsNotNull(clsB);
            var mroB = clsB.GetMro().SelectMany(ns => ns.Select(n => n.ShortDescription)).ToList();
            AssertUtil.ContainsExactly(mroB, "B", "type float");
            
            clsC = entry.GetValuesByIndex("C", code.IndexOf("z =")).FirstOrDefault() as Microsoft.PythonTools.Analysis.Values.ClassInfo;
            Assert.IsNotNull(clsC);
            var mroC = clsC.GetMro().SelectMany(ns => ns.Select(n => n.ShortDescription)).ToList();
            AssertUtil.ContainsExactly(mroC, "C", "type str");
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

            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("a", 1), GeneratorType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("b", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("c", 1), IntType);

            entry = ProcessText(@"
def f(x):
    yield x

a = f(42)
b = a.next()

for c in f():
    print c
            ");

            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("a", 1), GeneratorType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("b", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("c", 1), IntType);

            var text = @"
def f():
    yield 1
    x = yield 2

a = f()
b = a.next()
c = a.send('abc')";
            entry = ProcessText(text);

            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("a", 1), GeneratorType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("b", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("c", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("x", text.IndexOf("yield 2")), StringType);
        }

        
        [TestMethod]
        public void TestListComprehensions() {/*
            var entry = ProcessText(@"
x = [2,3,4]
y = [a for a in x]
z = y[0]
            ");

            AssertUtil.ContainsExactly(entry.GetTypesFromName("z", 0), IntType);*/

            string text = @"
def f(abc):
    print abc

[f(x) for x in [2,3,4]]
";

            var entry = ProcessText(text);


            var vars = entry.GetVariablesByIndex("f", text.IndexOf("[f(x"));
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

            var vars = entry.GetVariablesByIndex("a", text.IndexOf("a = "));
            VerifyReferences(UniqifyVariables(vars), new VariableLocation(2, 1, VariableType.Definition), new VariableLocation(4, 11, VariableType.Reference));
            
            vars = entry.GetVariablesByIndex("b", text.IndexOf("b = "));
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

            var vars = entry.GetVariablesByIndex("self.__x", text.IndexOf("self.__"));
            VerifyReferences(UniqifyVariables(vars), new VariableLocation(3, 9, VariableType.Definition), new VariableLocation(7, 14, VariableType.Reference));
        }

        [TestMethod]
        public void TestGeneratorComprehensions() {/*
            var entry = ProcessText(@"
x = [2,3,4]
y = [a for a in x]
z = y[0]
            ");

            AssertUtil.ContainsExactly(entry.GetTypesFromName("z", 0), IntType);*/

            string text = @"
def f(abc):
    print abc

(f(x) for x in [2,3,4])
";

            var entry = ProcessText(text);


            var vars = entry.GetVariablesByIndex("f", text.IndexOf("(f(x"));
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
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("some_str", 1), StringType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("some_int", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("some_bool", 1), BoolType);
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

            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("a", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("b", 1), StringType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("c", 1), StringType);
        }

        [TestMethod]
        public void TestGetAttr() {
            var entry = ProcessText(@"
class x(object):
    def __init__(self, value):
        self.value = value
        
a = x(42)
b = getattr(a, 'value')
c = getattr(a, 'dne', 'foo')
d = getattr(a, 'value', 'foo')
");

            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("b", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("c", 1), StringType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("d", 1), IntType, StringType);
        }
        
        [TestMethod]
        public void TestListAppend() {
            var entry = ProcessText(@"
x = []
x.append('abc')
y = x[0]
");

            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("y", 1), StringType);

            entry = ProcessText(@"
x = []
x.extend(('abc', ))
y = x[0]
");
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("y", 1), StringType);

            entry = ProcessText(@"
x = []
x.insert(0, 'abc')
y = x[0]
");
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("y", 1), StringType);

            entry = ProcessText(@"
x = []
x.append('abc')
y = x.pop()
");

            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("y", 1), StringType);

            entry = ProcessText(@"
class ListTest(object):
    def reset(self):
        self.items = []
        self.pushItem(self)
    def pushItem(self, item):
        self.items.append(item)

a = ListTest()
b = a.items[0]");

            AssertUtil.Contains(entry.GetMembersFromNameByIndex("b", 1), "pushItem");
        }

        [TestMethod]
        public void TestSlicing() {
            var entry = ProcessText(@"
x = [2]
y = x[:-1]
z = y[0]
");

            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("z", 1), IntType);

            entry = ProcessText(@"
x = (2, 3, 4)
y = x[:-1]
z = y[0]
");

            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("z", 1), IntType);

            var text = @"
lit = 'abc'           
inst = str.lower()       

slit = lit[1:2]
ilit = lit[1]
sinst = inst[1:2]
iinst = inst[1]
";

            entry = ProcessText(text);

            foreach (var name in new[] { "slit", "ilit", "sinst", "iinst" }) {
                AssertUtil.ContainsExactly(entry.GetValuesByIndex(name, text.IndexOf(name + " = ")).Select(x => x.PythonType.Name), "str");
            }
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
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("some_str", 1), StringType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("some_int", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("some_bool", 1), BoolType);
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

            var result = entry.GetSignaturesByIndex("C", 1).ToArray();
            Assert.AreEqual(result.Length, 1);
            Assert.AreEqual(result[0].Parameters.Length, 0);

            result = entry.GetSignaturesByIndex("D", 1).ToArray();
            Assert.AreEqual(result.Length, 1);
            Assert.AreEqual(result[0].Parameters.Length, 0);

            result = entry.GetSignaturesByIndex("E", 1).ToArray();
            Assert.AreEqual(result.Length, 1);
            Assert.AreEqual(result[0].Parameters.Length, 0);

            result = entry.GetSignaturesByIndex("F", 1).ToArray();
            Assert.AreEqual(result.Length, 1);
            Assert.AreEqual(result[0].Parameters.Length, 1);

            result = entry.GetSignaturesByIndex("G", 1).ToArray();
            Assert.AreEqual(result.Length, 1);
            Assert.AreEqual(result[0].Parameters.Length, 0);

            result = entry.GetSignaturesByIndex("H", 1).ToArray();
            Assert.AreEqual(result.Length, 1);
            Assert.AreEqual(result[0].Parameters.Length, 1);
        }

        /// <summary>
        /// http://pytools.codeplex.com/workitem/798
        /// </summary>
        [TestMethod]
        public void TestListSubclassSignatures() {
            var text = @"
class C(list):
    pass

a = C()
a.count";
            var entry = ProcessText(text);

            var result = entry.GetSignaturesByIndex("a.count", text.IndexOf("a.count")).ToArray();
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

            var result = entry.GetSignaturesByIndex("f", 1).ToArray();
            Assert.AreEqual(result.Length, 1);
            Assert.AreEqual(result[0].Documentation, "func doc");

            result = entry.GetSignaturesByIndex("C", 1).ToArray();
            Assert.AreEqual(result.Length, 1);
            Assert.AreEqual(result[0].Documentation, "class doc");

            result = entry.GetSignaturesByIndex("funicode", 1).ToArray();
            Assert.AreEqual(result.Length, 1);
            Assert.AreEqual(result[0].Documentation, "unicode func doc");

            result = entry.GetSignaturesByIndex("Cunicode", 1).ToArray();
            Assert.AreEqual(result.Length, 1);
            Assert.AreEqual(result[0].Documentation, "unicode class doc");
        }

        [TestMethod]
        public void TestEllipsis() {
            var entry = ProcessText(@"
x = ...
            ", PythonLanguageVersion.V31);

            var result = new List<IPythonType>(entry.GetTypesFromNameByIndex("x", 1));
            Assert.AreEqual(result.Count, 1);
            Assert.AreEqual(result[0].Name, "ellipsis");
        }

        [TestMethod]
        public void TestBackquote() {
            var entry = ProcessText(@"x = `42`");

            var result = new List<IPythonType>(entry.GetTypesFromNameByIndex("x", 1));
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
                var result = entry.GetSignaturesByIndex(test, 1).ToArray();
                Assert.AreEqual(result.Length, 1);                
                Assert.AreEqual(result[0].Parameters.Length, 0);
            }

            entry = ProcessText(@"
const = [].append
constructed = list().append
");

            testCapitalize = new[] { "const", "constructed" };
            foreach (var test in testCapitalize) {
                var result = entry.GetSignaturesByIndex(test, 1).ToArray();
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


            AssertUtil.ContainsExactly(GetTypes(entry.GetValuesByIndex("e1", text.IndexOf(", e1"))), Interpreter.ImportModule("__builtin__").GetMember(Interpreter.CreateModuleContext(), "TypeError"));

            AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("e2", text.IndexOf(", e2"))), "MyException instance");
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
            AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("y", text.IndexOf("y ="))), UnicodeStringType);
            AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("y1", text.IndexOf("y1 ="))), "str");
            AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("bar", text.IndexOf("bar ="))), "str");
            AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("bar2", text.IndexOf("bar2 ="))), UnicodeStringType);
        }

        public virtual string UnicodeStringType {
            get {
                return "unicode";
            }
        }


        [TestMethod]
        public void TestStringMultiply() {
            var text = @"
x = u'abc %d'
y = x * 100


x1 = 'abc %d'
y1 = x1 * 100

foo = 'abc %d'.lower()
bar = foo * 100

foo2 = u'abc' + u'%d'
bar2 = foo2 * 100";

            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("y", text.IndexOf("y ="))), UnicodeStringType);
            AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("y1", text.IndexOf("y1 ="))), "str");
            AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("bar", text.IndexOf("bar ="))), "str");
            AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("bar2", text.IndexOf("bar2 ="))), UnicodeStringType);

            text = @"
x = u'abc %d'
y = 100 * x


x1 = 'abc %d'
y1 = 100 * x1

foo = 'abc %d'.lower()
bar = 100 * foo

foo2 = u'abc' + u'%d'
bar2 = 100 * foo2";

            entry = ProcessText(text);
            AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("y", text.IndexOf("y ="))), UnicodeStringType);
            AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("y1", text.IndexOf("y1 ="))), "str");
            AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("bar", text.IndexOf("bar ="))), "str");
            AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("bar2", text.IndexOf("bar2 ="))), UnicodeStringType);
        }

        [TestMethod]
        public void TestBinaryOperators() {
            var operators = new[] {
                new { Method = "add", Operator = "+" },
                new { Method = "sub", Operator = "-" },
                new { Method = "mul", Operator = "*" },
                new { Method = "div", Operator = "/" },
                new { Method = "mod", Operator = "%" },
                new { Method = "and", Operator = "&" },
                new { Method = "or", Operator = "|" },
                new { Method = "xor", Operator = "^" },
                new { Method = "lshift", Operator = "<<" },
                new { Method = "rshift", Operator = ">>" },
                new { Method = "pow", Operator = "**" },
                new { Method = "floordiv", Operator = "//" },
            };

            var text = @"
class ForwardResult(object):
    pass

class ReverseResult(object):
    pass

class C(object):
    def __{0}__(self, other):
        return ForwardResult()

    def __r{0}__(self, other):
        return ReverseResult()

a = C() {1} 42
b = 42 {1} C()
c = [] {1} C()
d = C() {1} []
e = () {1} C()
f = C() {1} ()
g = C() {1} 42.0
h = 42.0 {1} C()
i = C() {1} 42L
j = 42L {1} C()
k = C() {1} 42j
l = 42j {1} C()
";

            foreach (var test in operators) {
                Console.WriteLine(test.Operator);
                var entry = ProcessText(String.Format(text, test.Method, test.Operator));

                AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("a", text.IndexOf("a ="))), "ForwardResult instance");
                AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("b", text.IndexOf("b ="))), "ReverseResult instance");
                AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("c", text.IndexOf("c ="))), "ReverseResult instance");
                AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("d", text.IndexOf("d ="))), "ForwardResult instance");
                AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("e", text.IndexOf("e ="))), "ReverseResult instance");
                AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("f", text.IndexOf("f ="))), "ForwardResult instance");
                AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("g", text.IndexOf("g ="))), "ForwardResult instance");
                AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("h", text.IndexOf("h ="))), "ReverseResult instance");
                AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("i", text.IndexOf("i ="))), "ForwardResult instance");
                AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("j", text.IndexOf("j ="))), "ReverseResult instance");
                AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("k", text.IndexOf("k ="))), "ForwardResult instance");
                AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("l", text.IndexOf("l ="))), "ReverseResult instance");
            }
        }

        [TestMethod]
        public void TestSequenceMultiply() {
            var text = @"
x = ()
y = x * 100

x1 = (1,2,3)
y1 = x1 * 100

foo = [1,2,3]
bar = foo * 100

foo2 = []
bar2 = foo2 * 100";

            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("y",   text.IndexOf("y ="))), "tuple");
            AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("y1",  text.IndexOf("y1 ="))), "tuple");
            AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("bar", text.IndexOf("bar ="))), "list");
            AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("bar2",text.IndexOf("bar2 ="))), "list");

            text = @"
x = ()
y = 100 * x

x1 = (1,2,3)
y1 = 100 * x1

foo = [1,2,3]
bar = 100 * foo 

foo2 = []
bar2 = 100 * foo2";

            entry = ProcessText(text);
            AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("y",    text.IndexOf("y ="))), "tuple");
            AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("y1",   text.IndexOf("y1 ="))), "tuple");
            AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("bar",  text.IndexOf("bar ="))), "list");
            AssertUtil.ContainsExactly(GetTypeNames(entry.GetValuesByIndex("bar2", text.IndexOf("bar2 ="))), "list");
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
            AssertUtil.Contains(entry.GetMembersByIndex("self.foo", text.IndexOf("self.foo")).Select(x => x.Name), "nodesc_method");
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
            VerifyReferences(entry.GetVariablesByIndex("self.abc", text.IndexOf("self.abc")), new VariableLocation(9, 14, VariableType.Definition), new VariableLocation(10, 18, VariableType.Reference), new VariableLocation(11, 20, VariableType.Reference));
            VerifyReferences(entry.GetVariablesByIndex("foo", text.IndexOf("= foo")), new VariableLocation(8, 24, VariableType.Definition), new VariableLocation(9, 20, VariableType.Reference));
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
            VerifyReferences(entry.GetVariablesByIndex("self.abc", text.IndexOf("self.abc")), new VariableLocation(5, 14, VariableType.Definition), new VariableLocation(6, 18, VariableType.Reference), new VariableLocation(7, 20, VariableType.Reference));
            VerifyReferences(entry.GetVariablesByIndex("foo", text.IndexOf("= foo")), new VariableLocation(4, 24, VariableType.Definition), new VariableLocation(5, 20, VariableType.Reference));

            text = @"
# add ref w/ type info
class D(object):
    def __init__(self, foo):
        self.abc = foo
        del self.abc
        print self.abc

D(42)";
            entry = ProcessText(text);

            VerifyReferences(entry.GetVariablesByIndex("self.abc", text.IndexOf("self.abc")), new VariableLocation(5, 14, VariableType.Definition), new VariableLocation(6, 18, VariableType.Reference), new VariableLocation(7, 20, VariableType.Reference));
            VerifyReferences(entry.GetVariablesByIndex("foo", text.IndexOf("= foo")), new VariableLocation(4, 24, VariableType.Definition), new VariableLocation(5, 20, VariableType.Reference));
            VerifyReferences(UniqifyVariables(entry.GetVariablesByIndex("D", text.IndexOf("D(42)"))), new VariableLocation(9, 1, VariableType.Reference), new VariableLocation(3, 7, VariableType.Definition));

            // function definitions
            text = @"
def f(): pass

x = f()";
            entry = ProcessText(text);
            VerifyReferences(UniqifyVariables(entry.GetVariablesByIndex("f", text.IndexOf("x ="))), new VariableLocation(4, 5, VariableType.Reference), new VariableLocation(2, 5, VariableType.Definition));

            

            text = @"
def f(): pass

x = f";
            entry = ProcessText(text);
            VerifyReferences(UniqifyVariables(entry.GetVariablesByIndex("f", text.IndexOf("x ="))), new VariableLocation(4, 5, VariableType.Reference), new VariableLocation(2, 5, VariableType.Definition));

            // class variables
            text = @"

class D(object):
    abc = 42
    print abc
    del abc
";
            entry = ProcessText(text);

            VerifyReferences(entry.GetVariablesByIndex("abc", text.IndexOf("abc =")), new VariableLocation(4, 5, VariableType.Definition), new VariableLocation(5, 11, VariableType.Reference), new VariableLocation(6, 9, VariableType.Reference));

            // class definition
            text = @"
class D(object): pass

a = D
";
            entry = ProcessText(text);
            VerifyReferences(UniqifyVariables(entry.GetVariablesByIndex("D", text.IndexOf("a ="))), new VariableLocation(4, 5, VariableType.Reference), new VariableLocation(2, 7, VariableType.Definition));

            // method definition
            text = @"
class D(object): 
    def f(self): pass

a = D().f()
";
            entry = ProcessText(text);
            VerifyReferences(UniqifyVariables(entry.GetVariablesByIndex("D().f", text.IndexOf("a ="))), 
                new VariableLocation(5, 9, VariableType.Reference), new VariableLocation(3, 9, VariableType.Definition));

            // globals
            text = @"
abc = 42
print abc
del abc
";
            entry = ProcessText(text);
            VerifyReferences(entry.GetVariablesByIndex("abc", text.IndexOf("abc =")), new VariableLocation(4, 5, VariableType.Reference), new VariableLocation(2, 1, VariableType.Definition), new VariableLocation(3, 7, VariableType.Reference));

            // parameters
            text = @"
def f(abc):
    print abc
    abc = 42
    del abc
";
            entry = ProcessText(text);
            VerifyReferences(entry.GetVariablesByIndex("abc", text.IndexOf("abc =")), new VariableLocation(2, 7, VariableType.Definition), new VariableLocation(4, 5, VariableType.Definition), new VariableLocation(3, 11, VariableType.Reference), new VariableLocation(5, 9, VariableType.Reference));
        
        
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
            VerifyReferences(entry.GetVariablesByIndex("abc", text.IndexOf("try:")), 
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
            VerifyReferences(entry.GetVariablesByIndex("abc", text.IndexOf("x =")),
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
                entry.GetVariablesByIndex("a", text.IndexOf("print(a)")),
                new VariableLocation(6, 9, VariableType.Definition),
                new VariableLocation(4, 15, VariableType.Reference),
                new VariableLocation(5, 27, VariableType.Reference)
            );


            entry = ProcessText(text);
            VerifyReferences(
                entry.GetVariablesByIndex("a", text.IndexOf("def g")),
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
                            i--;
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

            VerifyReferences(UniqifyVariables(barMod.Analysis.GetVariablesByIndex("abc", barText.IndexOf("abc"))),
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

def m(x = math.atan2(1, 0)): pass
");

            var tests = new[] {
                new { FuncName = "f", ParamName="x = None" },
                new { FuncName = "g", ParamName="x = {}" },
                new { FuncName = "h", ParamName="x = {...}" },
                new { FuncName = "i", ParamName="x = []" },
                new { FuncName = "j", ParamName="x = [...]" },
                new { FuncName = "k", ParamName="x = ()" },
                new { FuncName = "l", ParamName="x = (...)" },
                new { FuncName = "m", ParamName="x = math.atan2(1,0)" },
            };

            foreach (var test in tests) {
                var result = entry.GetSignaturesByIndex(test.FuncName, 1).ToArray();
                Assert.AreEqual(result.Length, 1);
                Assert.AreEqual(result[0].Parameters.Length, 1);
                Assert.AreEqual(result[0].Parameters[0].Name, test.ParamName);
            }
        }

        [TestMethod]
        public void SpecialDictMethodsCrossUnitAnalysis() {
            // dict methods which return lists
            foreach (var method in new[] { "x.itervalues()", "x.keys()", "x.iterkeys()", "x.values()"}) {
                Debug.WriteLine(method);

                var code = @"x = {}
abc = " + method + @"
def f(z):
    z[42] = 100

f(x)
for foo in abc:
    print(foo)";

                var entry = ProcessText(code);
                var values = entry.GetValuesByIndex("foo", code.IndexOf("print(foo)")).ToArray();
                Assert.AreEqual(1, values.Length);
                Assert.AreEqual(Interpreter.GetBuiltinType(BuiltinTypeId.Int), values[0].PythonType);
            }

            // dict methods which return a key or value
            foreach (var method in new[] { "x.get(42)", "x.pop()" }) {
                Debug.WriteLine(method);

                var code = @"x = {}
abc = " + method + @"
def f(z):
    z[42] = 100

f(x)
foo = abc";

                var entry = ProcessText(code);
                var values = entry.GetValuesByIndex("foo", code.IndexOf("foo =")).ToArray();
                Assert.AreEqual(1, values.Length);
                Assert.AreEqual(Interpreter.GetBuiltinType(BuiltinTypeId.Int), values[0].PythonType);
            }

            // dict methods which return a key/value tuple
            // dict methods which return a key or value
            foreach (var method in new[] { "x.popitem()" }) {
                Debug.WriteLine(method);

                var code = @"x = {}
abc = " + method + @"
def f(z):
    z[42] = 100

f(x)
foo = abc";

                var entry = ProcessText(code);
                var values = entry.GetValuesByIndex("foo", code.IndexOf("foo =")).ToArray();
                Assert.AreEqual(1, values.Length);
                Assert.AreEqual("tuple of int", values[0].Description);
            }

            // dict methods which return a list of key/value tuple
            foreach (var method in new[] { "x.iteritems()", "x.items()" }) {
                Debug.WriteLine(method);

                var code = @"x = {}
abc = " + method + @"
def f(z):
    z[42] = 100

f(x)
for foo in abc:
    print(foo)";

                var entry = ProcessText(code);
                var values = entry.GetValuesByIndex("foo", code.IndexOf("print(foo)")).ToArray();
                Assert.AreEqual(1, values.Length);
                Assert.AreEqual("tuple of int", values[0].Description);
            }
        }

        /// <summary>
        /// Verifies that list indicies don't accumulate classes across multiple analysis
        /// </summary>
        [TestMethod]
        public void ListIndiciesCrossModuleAnalysis() {
            for (int i = 0; i < 2; i++) {
                var state = new PythonAnalyzer(Interpreter, PythonLanguageVersion.V27);
                var code1 = "l = []";
                var code2 = @"class C(object):
    pass

a = C()
import mod1
mod1.l.append(a)
";

                var mod1 = state.AddModule("mod1", "mod1", null);
                Prepare(mod1, GetSourceUnit(code1, "mod1"));
                var mod2 = state.AddModule("mod2", "mod2", null);
                Prepare(mod2, GetSourceUnit(code2, "mod2"));

                mod1.Analyze(true);
                mod2.Analyze(true);
                
                mod1.AnalysisGroup.AnalyzeQueuedEntries();
                if (i == 0) {
                    // re-preparing shouldn't be necessary
                    Prepare(mod2, GetSourceUnit(code2, "mod2"));
                }

                mod2.Analyze(true);
                mod2.AnalysisGroup.AnalyzeQueuedEntries();

                var listValue = mod1.Analysis.GetValuesByIndex("l", 0).ToArray();
                Assert.AreEqual(1, listValue.Length);
                Assert.AreEqual("list of C instance", listValue[0].Description);

                var values = mod1.Analysis.GetValuesByIndex("l[0]", 0).ToArray();
                Assert.AreEqual(1, values.Length);
                Assert.AreEqual("C instance", values[0].Description);
            }
        }

        [TestMethod]
        public void SpecialListMethodsCrossUnitAnalysis() {
            var code = @"x = []
def f(z):
    z.append(100)
    
f(x)
for foo in x:
    print(foo)


bar = x.pop()
";

            var entry = ProcessText(code);
            var values = entry.GetValuesByIndex("foo", code.IndexOf("print(foo)")).ToArray();
            Assert.AreEqual(1, values.Length);
            Assert.AreEqual(Interpreter.GetBuiltinType(BuiltinTypeId.Int), values[0].PythonType);

            values = entry.GetValuesByIndex("bar", code.IndexOf("bar = ")).ToArray();
            Assert.AreEqual(1, values.Length);
            Assert.AreEqual(Interpreter.GetBuiltinType(BuiltinTypeId.Int), values[0].PythonType);
        }

        [TestMethod]
        public void SetLiteral() {
            var code = @"
x = {2, 3, 4}
for abc in x:
    print(abc)
";
            var entry = ProcessText(code);
            var values = entry.GetValuesByIndex("x", code.IndexOf("x = ")).ToArray();
            Assert.AreEqual(values.Length, 1);
            Assert.AreEqual(values[0].ShortDescription, "set");
            Assert.AreEqual("set({int})", values[0].Description);

            values = entry.GetValuesByIndex("abc", code.IndexOf("print(abc)")).ToArray();
            Assert.AreEqual(values.Length, 3);
            Assert.AreEqual(values[0].ShortDescription, "int");
        }

        [TestMethod]
        public void TestGetVariablesDictionaryGet() {
            var entry = ProcessText(@"
x = {42:'abc'}
            ");

            foreach (var varRef in entry.GetValuesByIndex("x.get", 1)) {
                Assert.AreEqual("bound built-in method get", varRef.Description);
            }
        }

        [TestMethod]
        public void TestDictMethods() {
            var entry = ProcessText(@"
x = {42:'abc'}
            ");

            Assert.AreEqual(entry.GetValuesByIndex("x.items()[0][0]", 1).Select(x => x.PythonType).First(), IntType);
            Assert.AreEqual(entry.GetValuesByIndex("x.items()[0][1]", 1).Select(x => x.PythonType).First(), StringType);
            Assert.AreEqual(entry.GetValuesByIndex("x.keys()[0]", 1).Select(x => x.PythonType).First(), IntType);
            Assert.AreEqual(entry.GetValuesByIndex("x.values()[0]", 1).Select(x => x.PythonType).First(), StringType);
            Assert.AreEqual(entry.GetValuesByIndex("x.pop(1)", 1).Select(x => x.PythonType).First(), StringType);
            Assert.AreEqual(entry.GetValuesByIndex("x.popitem()[0]", 1).Select(x => x.PythonType).First(), IntType);
            Assert.AreEqual(entry.GetValuesByIndex("x.popitem()[1]", 1).Select(x => x.PythonType).First(), StringType);
            Assert.AreEqual(entry.GetValuesByIndex("x.iterkeys().next()", 1).Select(x => x.PythonType).First(), IntType);
            Assert.AreEqual(entry.GetValuesByIndex("x.itervalues().next()", 1).Select(x => x.PythonType).First(), StringType);
            Assert.AreEqual(entry.GetValuesByIndex("x.iteritems().next()[0]", 1).Select(x => x.PythonType).First(), IntType);
            Assert.AreEqual(entry.GetValuesByIndex("x.iteritems().next()[1]", 1).Select(x => x.PythonType).First(), StringType);
        }

        [TestMethod]
        public void TestDictUpdate() {
            var entry = ProcessText(@"
a = {42:100}
b = {}
b.update(a)
");

            Assert.AreEqual(entry.GetValuesByIndex("b.items()[0][0]", 1).Select(x => x.PythonType).First(), IntType);
            Assert.AreEqual(entry.GetValuesByIndex("b.items()[0][1]", 1).Select(x => x.PythonType).First(), IntType);
        }

        [TestMethod]
        public void TestDictEnum() {
            var entry = ProcessText(@"
for x in {42:'abc'}:
    print(x)
");

            Assert.AreEqual(entry.GetValuesByIndex("x", 1).Select(x => x.PythonType).First(), IntType);
        }

        [TestMethod]
        public void TestFutureDivision() {
            var entry = ProcessText(@"
from __future__ import division
x = 1/2
            ");

            Assert.AreEqual(entry.GetValuesByIndex("x", 1).Select(x => x.PythonType).First(), FloatType);
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

            foreach (var varRef in entry.GetValuesByIndex("b", 1)) {
                Assert.AreEqual("method f of C objects \r\ndoc string", varRef.Description);
            }
        }

        [TestMethod]
        public void TestLambdaExpression() {
            var entry = ProcessText(@"
x = lambda a: a
y = x(42)
");

            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("y", 1), IntType);

            entry = ProcessText(@"
def f(a):
    return a

x = lambda b: f(b)
y = x(42)
");

            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("y", 1), IntType);

        }

        [TestMethod]
        public void TestLambdaScoping() {
            var code = @"def f(l1, l2):
    l1('abc')
    l2(42)


x = []
y = ()
f(lambda x=x:x, lambda x=y:x)";

            var entry = ProcessText(code);
            
            // default value, should be a list
            var members = entry.GetMembersByIndex("x", code.IndexOf("lambda x=") + 10, GetMemberOptions.None).Select(x => x.Completion);
            AssertUtil.Contains(members, "pop");
            AssertUtil.DoesntContain(members, "real");
            AssertUtil.DoesntContain(members, "rfind");

            // parameter used in the lambda, should be list and str
            members = entry.GetMembersByIndex("x", code.IndexOf("lambda x=") + 12, GetMemberOptions.None).Select(x => x.Completion);            
            AssertUtil.Contains(members, "pop");
            AssertUtil.Contains(members, "upper");

            // default value in the 2nd lambda, should be list
            members = entry.GetMembersByIndex("y", code.IndexOf("lambda x=") + 24, GetMemberOptions.None).Select(x => x.Completion);
            AssertUtil.Contains(members, "count");
            AssertUtil.DoesntContain(members, "pop");

            // value in the 2nd lambda, should be tuple and int
            members = entry.GetMembersByIndex("x", code.IndexOf("lambda x=") + 26, GetMemberOptions.None).Select(x => x.Completion);            
            AssertUtil.Contains(members, "count");
            AssertUtil.Contains(members, "real");
        }

        [TestMethod]
        public void TestFunctionScoping() {
            var code = @"x = 100

def f(x = x):
    x

f('abc')
";

            var entry = ProcessText(code);

            VerifyReferences(entry.GetVariablesByIndex("x", code.IndexOf("def") + 6),
                new VariableLocation(3, 7, VariableType.Definition),
                new VariableLocation(4, 5, VariableType.Reference));

            VerifyReferences(entry.GetVariablesByIndex("x", code.IndexOf("def") + 10),
                new VariableLocation(1, 1, VariableType.Definition),
                new VariableLocation(3, 11, VariableType.Reference));

            VerifyReferences(entry.GetVariablesByIndex("x", code.IndexOf("    x") + 4),
                new VariableLocation(3, 7, VariableType.Definition),
                new VariableLocation(4, 5, VariableType.Reference));
        }

        [TestMethod]
        public void TestRecursiveClass() {
            var entry = ProcessText(@"
cls = object

class cls(cls): 
    abc = 42
");

            entry.GetMembersFromNameByIndex("cls", 1);
            AssertUtil.ContainsExactly(entry.GetMembersByIndex("cls().abc", 1).Select(member => member.Name), _intMembers);
            AssertUtil.ContainsExactly(entry.GetMembersByIndex("cls.abc", 1).Select(member => member.Name), _intMembers);
            var sigs = entry.GetSignaturesByIndex("cls", 1).ToArray();
            Assert.AreEqual(2, sigs.Length);    // 1 for object, one for cls
            Assert.AreEqual(null, sigs.First().Documentation);            
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

            AssertUtil.ContainsExactly(entry.GetMembersByIndex("foo", 1).Select(member => member.Name), _intMembers);
            var sigs = entry.GetSignaturesByIndex("cls().f", 1).ToArray();
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

                    AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("a", text.IndexOf("pass")), ComplexType);
                    AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("b", text.IndexOf("pass")), IntType);
                    AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("c", text.IndexOf("pass")), StringType);
                }
            }
        }

        [TestMethod]
        public void TestBadKeywordArguments() {
            var code = @"def f(a, b):
    return a

x = 100
z = f(a=42, x)";

            var entry = ProcessText(code);
            var values = entry.GetValuesByIndex("z", code.IndexOf("z =")).ToArray();
            Assert.AreEqual(1, values.Length);

            Assert.AreEqual(values.First().Description, "int");
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

                    AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("a", text.IndexOf("pass")), ComplexType);
                    AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("b", text.IndexOf("pass")), IntType);
                    AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("c", text.IndexOf("pass")), StringType);
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

            var fifty = entry.GetVariablesNoBuiltinsByIndex(text.IndexOf("abc.foo")).ToSet();
            AssertUtil.ContainsExactly(fifty, "C", "D", "a", "abc", "self", "x");

            var three = entry.GetVariablesNoBuiltinsByIndex(text.IndexOf("def bar") + 1).ToSet();
            AssertUtil.ContainsExactly(three, "C", "D", "bar");

            var allFifty = entry.GetMembersFromNameByIndex("abc", text.IndexOf("abc.foo")).ToSet();
            AssertUtil.ContainsExactly(allFifty, GetUnion(_objectMembers, "baz", "foo"));

            var xTypes = entry.GetTypesFromNameByIndex("x", text.IndexOf("abc.foo")).ToSet();
            AssertUtil.ContainsExactly(xTypes, ListType, StringType, TupleType);

            var xMembers = entry.GetMembersFromNameByIndex("x", text.IndexOf("abc.foo")).ToSet();
            AssertUtil.ContainsExactly(xMembers, GetIntersection(_strMembers, _listMembers));
        }

        public static int GetIndex(string text, string substring) {
            return text.IndexOf(substring);
        }

        [TestMethod]
        public void TestBuiltins() {
            var text = @"
booltypetrue = True
booltypefalse = False
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("booltypetrue", 1), BoolType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("booltypefalse", 1), BoolType);
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
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("a", text.IndexOf("print")), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("b", text.IndexOf("print")), ListType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("a", text.IndexOf("x, y")));
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("b", text.IndexOf("x, y")));
        }

        [TestMethod]
        public void TestDictionaryAssign() {
            var text = @"
x = {'abc': 42}
y = x['foo']
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("y", 1), IntType);
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
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("a", text.IndexOf("print")), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("b", text.IndexOf("print")), ListType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("a", text.IndexOf("x, y")));
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("b", text.IndexOf("x, y")));
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
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("a", text.IndexOf("print")), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("b", text.IndexOf("print")), ListType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("a", text.IndexOf("x, y")));
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("b", text.IndexOf("x, y")));
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
            AssertUtil.ContainsExactly(entry.GetVariablesNoBuiltinsByIndex(1), "a", "x");
            AssertUtil.ContainsExactly(entry.GetMembersFromNameByIndex("x", 1), GetUnion(_objectMembers, "abc"));
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
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("a", text.IndexOf("print")), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("b", text.IndexOf("print")), StringType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("c", text.IndexOf("print")), ListType);
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
            var foo = entry.GetMembersFromNameByIndex("foo", text.IndexOf("print foo"));
            AssertUtil.ContainsExactly(foo, GetUnion(_objectMembers, "x_method"));
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
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("xvar", text.IndexOf("pass")), ListType);
        }

        internal static readonly Regex ValidParameterName = new Regex(@"^(\*|\*\*)?[a-z_][a-z0-9_]*( *=.+)?", RegexOptions.IgnoreCase);
        internal static string GetSafeParameterName(ParameterResult result) {
            var match = ValidParameterName.Match(result.Name);

            return match.Success ? match.Value : result.Name;
        }


        /// <summary>
        /// http://pytools.codeplex.com/workitem/799
        /// </summary>
        [TestMethod]
        public void TestOverrideCompletions() {
            var text = @"
class bar(list):
    pass
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(
                entry.GetOverrideableByIndex(text.IndexOf("pass")).Where(res => res.Name == "__init__").Select(
                    x => string.Join(", ", x.Parameters.Select(GetSafeParameterName))
                ), 
                this is IronPythonAnalysisTest ? "self, enumerable" : "self, sequence"
            );
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
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("foo", text.IndexOf("pass")), StringType);
            AssertUtil.ContainsExactly(entry.GetMembersFromNameByIndex("self", text.IndexOf("pass")), GetUnion(_objectMembers, "abc"));
        }

        [TestMethod]
        public void TestBuiltinRetval() {
            var text = @"
x = [2,3,4]
a = x.index(2)
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("x", text.IndexOf("x =")).ToSet(), ListType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("a", text.IndexOf("a =")).ToSet(), IntType);
        }

        [TestMethod]
        public void TestBuiltinFuncRetval() {
            var text = @"
x = ord('a')
y = range(5)
";

            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("x", text.IndexOf("x = ")).ToSet(), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("y", text.IndexOf("y = ")).ToSet(), ListType);
        }

        [TestMethod]
        public void TestFunctionMembers() {
            var text = @"
def f(x): pass
f.abc = 32
";
            var entry = ProcessText(text);
            AssertUtil.Contains(entry.GetMembersFromNameByIndex("f", 1), "abc");

            text = @"
def f(x): pass

";
            entry = ProcessText(text);
            AssertUtil.DoesntContain(entry.GetMembersFromNameByIndex("f", 1), "x");
            AssertUtil.ContainsExactly(entry.GetMembersFromNameByIndex("f", 1), _functionMembers);

            AssertUtil.ContainsExactly(entry.GetMembersFromNameByIndex("f.func_name", 1), _strMembers);
        }


        [TestMethod]
        public void TestRangeIteration() {
            var text = @"
for i in range(5):
    pass
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("i", text.IndexOf("for i")).ToSet(), IntType);
        }

        [TestMethod]
        public void TestBuiltinImport() {
            var text = @"
import sys
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetVariablesNoBuiltinsByIndex(1), "sys");
            Assert.IsTrue(entry.GetMembersFromNameByIndex("sys", 1).Any((s) => s == "winver"));
        }

        [TestMethod]
        public void TestBuiltinImportInFunc() {
            var text = @"
def f():
    import sys
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetVariablesNoBuiltinsByIndex(text.IndexOf("sys")), "f", "sys");
            AssertUtil.Contains(entry.GetMembersFromNameByIndex("sys", text.IndexOf("sys")), "winver");
        }

        [TestMethod]
        public void TestBuiltinImportInClass() {
            var text = @"
class C:
    import sys
";
            var entry = ProcessText(text);

            AssertUtil.ContainsExactly(entry.GetVariablesNoBuiltinsByIndex(text.IndexOf("sys")), "C", "sys");
            Assert.IsTrue(entry.GetMembersFromNameByIndex("sys", text.IndexOf("sys")).Any((s) => s == "winver"));
        }

        [TestMethod]
        public void TestNoImportClr() {
            var text = @"
x = 'abc'
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("x", 1), StringType);
            AssertUtil.ContainsExactly(entry.GetMembersFromNameByIndex("x", 1), _strMembers);
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
            AssertUtil.ContainsExactly(entry.GetMembersFromNameByIndex("other", text.IndexOf("other.g")), "g", "__doc__", "__class__");
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("x", text.IndexOf("x =")), ListType, StringType);
            AssertUtil.ContainsExactly(entry.GetMembersFromNameByIndex("x", text.IndexOf("x =")),
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
            Assert.AreEqual(1, entry.GetValuesByIndex("self.abc", text.IndexOf("self.abc")).ToList().Count);
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
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("x", 1), ListType);
        }

        [TestMethod]
        public void TestReturnArg() {
            var text = @"
def g(a):
    return a

x = g(1)
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("x", 1), IntType);
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
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("x", 1), IntType);
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
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("foo", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetMembersFromNameByIndex("foo", 1), _intMembers);
            AssertUtil.ContainsExactly(entry.GetMembersFromNameByIndex("a", 1), "abc", "func", "__doc__", "__class__");
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
            // TODO: AssertUtil.ContainsExactly(entry.GetTypesFromName("foo", 0), ListType);
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
            AssertUtil.ContainsExactly(entry.GetMembersFromNameByIndex("self", text.IndexOf("self.")),
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
                AssertUtil.ContainsExactly(pe[0].Analysis.GetMembersFromNameByIndex("mod2", 1), "x");
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
                AssertUtil.ContainsExactly(pe[1].Analysis.GetTypesFromNameByIndex("x", text2.IndexOf("return x")), StringType);
                AssertUtil.ContainsExactly(pe[0].Analysis.GetTypesFromNameByIndex("y", text1.IndexOf("y")), StringType);
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
                AssertUtil.ContainsExactly(pe[1].Analysis.GetTypesFromNameByIndex("x", text2.IndexOf("= x")), StringType);
                AssertUtil.ContainsExactly(pe[0].Analysis.GetTypesFromNameByIndex("y", text1.IndexOf("y")), StringType);
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
                AssertUtil.ContainsExactly(pe[1].Analysis.GetTypesFromNameByIndex("x", text2.IndexOf("= x")), StringType);
                AssertUtil.ContainsExactly(pe[0].Analysis.GetTypesFromNameByIndex("y", text1.IndexOf("y =")), StringType);
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
                AssertUtil.ContainsExactly(pe[2].Analysis.GetMembersFromNameByIndex("a", text3.IndexOf("a = ")),
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
            AssertUtil.ContainsExactly(entry.GetMembersFromNameByIndex("self", text.IndexOf("self.")),
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
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("a", text.IndexOf("a =")), IntType);
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
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("a", text.IndexOf("a = ")), FloatType);
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
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("a", text.IndexOf("a =")), TypeType);

            var exprs = new[] { "x.ClassMethod", "x().ClassMethod" };
            foreach (var expr in exprs) {
                var sigs = entry.GetSignaturesByIndex(expr, text.IndexOf("a = ")).ToArray();
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
            var mems = entry.GetMembersByIndex("C().x", text.IndexOf("bar ="));
            AssertUtil.ContainsExactly(mems.Select(m => m.Name), _intMembers);

            mems = entry.GetMembersByIndex("C.x", text.IndexOf("bar ="));
            AssertUtil.ContainsExactly(mems.Select(m => m.Name), _intMembers);

            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("foo", text.IndexOf("foo = ")), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("bar", text.IndexOf("bar = ")), IntType);

            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("ctx", text.IndexOf("return 42")), TypeType);
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("inst", text.IndexOf("return 42")), NoneType);

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
            AssertUtil.Contains(entry.GetMembersByIndex("inst", text.IndexOf("return 42")).Select(m => m.Name), "instfunc");
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
            AssertUtil.Contains(entry.GetMembersFromNameByIndex("self", text.IndexOf("pass")), "x");
            AssertUtil.ContainsExactly(entry.GetMembersByIndex("self.x", text.IndexOf("pass")).Select(m => m.Name), _strMembers);
        }

        [TestMethod]
        public void TestAssignToMissingMember() {
            var text = @"
class test():
    x = 0;
    y = 1;
t = test()
t.x, t. =
";
            // http://pytools.codeplex.com/workitem/733

            // this just shouldn't crash, we should handle the malformed code, not much to inspect afterwards...
            var entry = ProcessText(text);
        }

        class EmptyAnalysisCookie : IAnalysisCookie {
            public static EmptyAnalysisCookie Instance = new EmptyAnalysisCookie();
            public string GetLine(int lineNo) {
                throw new NotImplementedException();
            }
        }

        /*
        [TestMethod]
        public void TestMemLeak() {
            
            var state = new PythonAnalyzer(Interpreter, PythonLanguageVersion.V27);

            var bar = state.AddModule("bar", @"bar.py", EmptyAnalysisCookie.Instance);
            var baz = state.AddModule("baz", @"baz.py", EmptyAnalysisCookie.Instance);

            while (true) {
                var barSrc = GetSourceUnit(@"
import sys
from baz import D

class C(object):
    def f(self, b):
        x = sys.version
        y = sys.exc_clear()
        a = []
        a.append(b)
        return a

a = C()
z = a.f(D())
min(a, D())

", @"bar.py");

                var bazSrc = GetSourceUnit(@"
from bar import C

class D(object):
    def g(self, a):
        pass

a = D()
a.f(C())
z = C().f(42)

min(a, D())
", @"baz.py");


                Prepare(bar, barSrc);
                Prepare(baz, bazSrc);

                bar.Analyze();
                baz.Analyze();
            }            
        }
        
        [TestMethod]
        public void TestMemLeak2() {
            AnalyzeDirLeak(@"C:\Source\TCP0\Open_Source\Incubation\azure");
        }*/

        private void AnalyzeDirLeak(string dir) {
            List<string> files = new List<string>();
            CollectFiles(dir, files);

            List<FileStreamReader> sourceUnits = new List<FileStreamReader>();
            foreach (string file in files) {
                sourceUnits.Add(
                    new FileStreamReader(file)
                );
            }

            Stopwatch sw = new Stopwatch();

            sw.Start();
            long start0 = sw.ElapsedMilliseconds;
            var projectState = new PythonAnalyzer(Interpreter, PythonLanguageVersion.V27);
            var modules = new List<IPythonProjectEntry>();
            foreach (var sourceUnit in sourceUnits) {
                modules.Add(projectState.AddModule(PythonAnalyzer.PathToModuleName(sourceUnit.Path), sourceUnit.Path, null));
            }
            long start1 = sw.ElapsedMilliseconds;
            Console.WriteLine("AddSourceUnit: {0} ms", start1 - start0);

            var nodes = new List<Microsoft.PythonTools.Parsing.Ast.PythonAst>();
            for (int i = 0; i < modules.Count; i++) {
                PythonAst ast = null;
                try {
                    var sourceUnit = sourceUnits[i];

                    ast = Parser.CreateParser(sourceUnit, PythonLanguageVersion.V27).ParseFile();
                } catch (Exception) {
                }
                nodes.Add(ast);
            }
            long start2 = sw.ElapsedMilliseconds;
            Console.WriteLine("Parse: {0} ms", start2 - start1);

            for (int i = 0; i < modules.Count; i++) {
                var ast = nodes[i];

                if (ast != null) {
                    modules[i].UpdateTree(ast, null);
                }
            }

            long start3 = sw.ElapsedMilliseconds;
            for (int i = 0; i < modules.Count; i++) {
                Console.WriteLine("Analyzing {1}: {0} ms", sw.ElapsedMilliseconds - start3, sourceUnits[i].Path);
                var ast = nodes[i];
                if (ast != null) {
                    modules[i].Analyze(true);
                }
            }
            if (modules.Count > 0) {
                Console.WriteLine("Analyzing queue");
                modules[0].AnalysisGroup.AnalyzeQueuedEntries();
            }

            int index = -1;
            for(int i = 0; i<modules.Count; i++) {
                if (((ProjectEntry)modules[i]).ModuleName == "azure.servicebus.servicebusservice") {
                    index = i;
                    break;
                }
            }
            while (true) {
                using(var reader = new FileStreamReader(modules[index].FilePath)) {
                    var ast = Parser.CreateParser(reader, PythonLanguageVersion.V27).ParseFile();

                    modules[index].UpdateTree(ast, null);
                }

                modules[index].Analyze(true);
                modules[index].AnalysisGroup.AnalyzeQueuedEntries();
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

            Assert.AreEqual(foo.Analysis.GetValuesByIndex("C", 1).First().Description, "class C");
            Assert.IsTrue(foo.Analysis.GetValuesByIndex("C", 1).First().Location.FilePath.EndsWith("bar.py"));

            barSrc = GetSourceUnit(@"
", @"bar.py");

            // delete the class..
            Prepare(bar, barSrc);
            bar.Analyze();
            bar.AnalysisGroup.AnalyzeQueuedEntries();

            Assert.AreEqual(foo.Analysis.GetValuesByIndex("C", 1).ToArray().Length, 0);

            fooSrc = GetSourceUnit("from baz import C", @"foo.py");
            Prepare(foo, fooSrc);

            foo.Analyze();

            Assert.AreEqual(foo.Analysis.GetValuesByIndex("C", 1).First().Description, "class C");
            Assert.IsTrue(foo.Analysis.GetValuesByIndex("C", 1).First().Location.FilePath.EndsWith("baz.py"));
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

            Assert.AreEqual(x.Analysis.GetValuesByIndex("y", 1).First().Description, "Python module foo.y");
            AssertUtil.ContainsExactly(x.Analysis.GetTypesFromNameByIndex("abc", 1), IntType);
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

            AssertUtil.ContainsExactly(x.Analysis.GetTypesFromNameByIndex("abc", 1), IntType);
            AssertUtil.ContainsExactly(package.Analysis.GetTypesFromNameByIndex("abc", 1), IntType);
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

            AssertUtil.ContainsExactly(package.Analysis.GetTypesFromNameByIndex("y", 1), FunctionType, ModuleType);
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
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("a", text.IndexOf("a =")), IntType);
        }

        [TestMethod]
        public void TestDecorator() {
            var text1 = @"
import mod2

inst = mod2.MyClass()

@inst.mydec
def f():
    return 42
    

";

            var text2 = @"
import mod1

class MyClass(object):
	def mydec(self, x):
		return x()
";

            PermutedTest("mod", new[] { text1, text2 }, (pe) => {
                AssertUtil.ContainsExactly(
                    pe[1].Analysis.GetValuesByIndex("MyClass().mydec(mod1.f)", 1).Select(value => value.PythonType), 
                    IntType
                );
            });
        }


        [TestMethod]
        public void TestDecoratorFlow() {
            var text1 = @"
import mod2

inst = mod2.MyClass()

@inst.filter(foo=42)
def f():
    return 42
    
";

            var text2 = @"
import mod1

class MyClass(object):
    def filter(self, name=None, filter_func=None, **flags):
        # @register.filter()
        def dec(func):
            return self.filter_function(func, **flags)
        return dec
    def filter_function(self, func, **flags):
        name = getattr(func, ""_decorated_function"", func).__name__
        return self.filter(name, func, **flags)
";

            PermutedTest("mod", new[] { text1, text2 }, (pe) => {
                AssertUtil.ContainsExactly(
                    pe[1].Analysis.GetValuesByIndex("filter_func", text2.IndexOf("# @register.filter()")).Select(value => value.PythonType),
                    FunctionType,
                    NoneType
                );
            });
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
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("value", text.IndexOf(" = value")), IntType);
        }

        /// <summary>
        /// Verifies that regardless of how we get to imports/function return values that
        /// we properly understand the imported value.
        /// </summary>
        [TestMethod]
        public void ImportScopesOrder() {
            var text1 = @"
import mod2
import mmap as mm

import sys
def f():
    return sys

def g():
    return re

def h():
    return mod2.sys

def i():
    return op

def j():
    return mm

def k():
    return mod2.impp

import operator as op

import re

";

            var text2 = @"
import sys
import imp as impp
";
            PermutedTest("mod", new[] { text1, text2 }, entries => {
                AssertUtil.ContainsExactly(
                    GetVariableDescriptionsByIndex(entries[0].Analysis, "g", 1), 
                    "def g(...) -> built-in module re"
                );
                AssertUtil.ContainsExactly(
                    GetVariableDescriptionsByIndex(entries[0].Analysis, "f", 1),
                    "def f(...) -> built-in module sys"
                );
                AssertUtil.ContainsExactly(
                    GetVariableDescriptionsByIndex(entries[0].Analysis, "h", 1),
                    "def h(...) -> built-in module sys"
                );
                AssertUtil.ContainsExactly(
                    GetVariableDescriptionsByIndex(entries[0].Analysis, "i", 1),
                    "def i(...) -> built-in module operator"
                );
                AssertUtil.ContainsExactly(
                    GetVariableDescriptionsByIndex(entries[0].Analysis, "j", 1),
                    "def j(...) -> built-in module mmap"
                );
                AssertUtil.ContainsExactly(
                    GetVariableDescriptionsByIndex(entries[0].Analysis, "k", 1),
                    "def k(...) -> built-in module imp"
                );
            });
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
            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("value", text.IndexOf(" = value")), IntType);
        }

        public static IEnumerable<string> GetVariableDescriptionsByIndex(ModuleAnalysis entry, string variable, int index) {
            return entry.GetValuesByIndex(variable, index).Select(m => m.Description);
        }

        public static IEnumerable<string> GetVariableShortDescriptionsByIndex(ModuleAnalysis entry, string variable, int index) {
            return entry.GetValuesByIndex(variable, index).Select(m => m.ShortDescription);
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
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "x", text.IndexOf("z =")), "int");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "x", text.IndexOf("z =") + 1), "int");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "x", text.IndexOf("pass")), "None");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "x", text.IndexOf("y =")), "str");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "x", text.IndexOf("y =") + 1), "str");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "x", text.IndexOf("else:") + 7), "None");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "x", text.IndexOf("foo =")), "tuple");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "x", text.LastIndexOf("pass")), "tuple");

            VerifyReferences(
                entry.GetVariablesByIndex("x", 1),
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(7, 23, VariableType.Reference),
                new VariableLocation(12, 23, VariableType.Reference),
                new VariableLocation(20, 15, VariableType.Reference)
            );

            VerifyReferences(
                UniqifyVariables(entry.GetVariablesByIndex("x", text.IndexOf("z ="))),
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(7, 23, VariableType.Reference),
                new VariableLocation(12, 23, VariableType.Reference),
                new VariableLocation(20, 15, VariableType.Reference)
            );

            VerifyReferences(
                UniqifyVariables(entry.GetVariablesByIndex("x", text.IndexOf("z =") + 1)),
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(7, 23, VariableType.Reference),
                new VariableLocation(12, 23, VariableType.Reference),
                new VariableLocation(20, 15, VariableType.Reference)
            );

            VerifyReferences(
                UniqifyVariables(entry.GetVariablesByIndex("x", text.IndexOf("z =") - 2)),
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(7, 23, VariableType.Reference),
                new VariableLocation(12, 23, VariableType.Reference),
                new VariableLocation(20, 15, VariableType.Reference)
            );

            VerifyReferences(
                UniqifyVariables(entry.GetVariablesByIndex("x", text.IndexOf("y ="))),
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(7, 23, VariableType.Reference),
                new VariableLocation(12, 23, VariableType.Reference),
                new VariableLocation(20, 15, VariableType.Reference)
            );

            VerifyReferences(
                UniqifyVariables(entry.GetVariablesByIndex("x", text.IndexOf("y =") + 1)),
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(7, 23, VariableType.Reference),
                new VariableLocation(12, 23, VariableType.Reference),
                new VariableLocation(20, 15, VariableType.Reference)
            );

            VerifyReferences(
                UniqifyVariables(entry.GetVariablesByIndex("x", text.IndexOf("y =") - 2)),
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
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "a", text.IndexOf("f(a)")));
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "a", text.IndexOf("pass")), "int");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "a", text.IndexOf("print(a)")));

            text = @"x = None


if True:
    pass
    assert isinstance(x, int)
    z = 100      
    
    pass    

print(z)";

            entry = ProcessText(text);
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "z", text.IndexOf("z =")), "int");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "z", 1), "int");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "z", text.IndexOf("print(z)") - 2), "int");

            VerifyReferences(
                UniqifyVariables(entry.GetVariablesByIndex("z", text.IndexOf("print(z)"))),
                new VariableLocation(7, 5, VariableType.Definition),
                new VariableLocation(11, 7, VariableType.Reference)
            );

            VerifyReferences(
                UniqifyVariables(entry.GetVariablesByIndex("z", text.IndexOf("z ="))),
                new VariableLocation(7, 5, VariableType.Definition),
                new VariableLocation(11, 7, VariableType.Reference)
            );

            // http://pytools.codeplex.com/workitem/636
            
            // this just shouldn't crash, we should handle the malformed code, not much to inspect afterwards...

            entry = ProcessText("if isinstance(x, list):\r\n");
            entry = ProcessText("if isinstance(x, list):");
        }

        [TestMethod]
        public void NestedIsInstance() {
            var code = @"
def f():
    x = None
    y = None

    assert isinstance(x, int)
    z = x

    assert isinstance(y, int)
    w = y

    pass";

            var entry = ProcessText(code);
            var values = entry.GetValuesByIndex("z", code.IndexOf("z = x")).ToArray();
            Assert.AreEqual(1, values.Length);
            Assert.AreEqual("int", values.First().Description);

            values = entry.GetValuesByIndex("w", code.IndexOf("w = y")).ToArray();
            Assert.AreEqual("int", values.First().Description);
        }

        [TestMethod]
        public void TestIsInstanceUserDefinedType() {
            var text = @"
class C(object):
    def f(self):
        pass

def f(a):
    assert isinstance(a, C)
    print(a)
    pass
";

            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "a", text.IndexOf("print(a)")), "C instance");
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

def h():
    return f
    return g

class return_func_class:
    def return_func(self):
        '''some help'''
        return self.return_func
";
            var entry = ProcessText(text);

            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "foo()", 1), "foo instance");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "int()", 1), "int");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "a", 1), "float");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "a", 1), "float");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "b", 1), "long");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "c", 1), "str");
            AssertUtil.ContainsExactly(entry.GetValuesByIndex("x", 1).Select(v => v.Description.Substring(0, 5)), "tuple");
            AssertUtil.ContainsExactly(entry.GetValuesByIndex("y", 1).Select(v => v.Description.Substring(0, 4)), "list");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "z", 1), "int");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "min", 1), "built-in function min");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "list.append", 1), "built-in method append");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "\"abc\".Length", 1));
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "c.Length", 1));
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "d", 1), "foo instance");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "sys", 1), "built-in module sys");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "f", 1), "def f(...) -> str");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "foo.f", 1), "def f(...)");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "foo().g", 1), "method g of foo objects ");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "foo", 1), "class foo");
            //AssertUtil.ContainsExactly(GetVariableDescriptions(entry, "System.StringSplitOptions.RemoveEmptyEntries", 0), "field of type StringSplitOptions");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "g", 1), "def g(...)");    // return info could be better
            //AssertUtil.ContainsExactly(GetVariableDescriptions(entry, "System.AppDomain.DomainUnload", 1), "event of type System.EventHandler");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "None", 1), "None");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "f.func_name", 1), "property of type str");
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "h", 1), "def h(...) -> def f(...) -> str, def g(...)");

            // method which returns it's self, we shouldn't stack overflow producing the help...
            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "return_func_class().return_func", 1), @"method return_func of return_func_class objects  -> method return_func of return_func_class objects ...

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

            AssertUtil.Contains(GetCompletionDocumentationByIndex(entry, "f", "func_name", 1).First(), "-> str", " = value");
            AssertUtil.Contains(GetCompletionDocumentationByIndex(entry, "", "int", 1).First(), "integer");
            AssertUtil.Contains(GetCompletionDocumentationByIndex(entry, "", "min", 1).First(), "min(");
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


            Assert.AreEqual(GetMemberByIndex(entry, "f", "func_name", 1).First().MemberType, PythonMemberType.Property);
            Assert.AreEqual(GetMemberByIndex(entry, "list", "append", 1).First().MemberType, PythonMemberType.Method);
            Assert.AreEqual(GetMemberByIndex(entry, "y", "append", 1).First().MemberType, PythonMemberType.Method);
            Assert.AreEqual(GetMemberByIndex(entry, "", "int", 1).First().MemberType, PythonMemberType.Class);
            Assert.AreEqual(GetMemberByIndex(entry, "", "min", 1).First().MemberType, PythonMemberType.Function);
            Assert.AreEqual(GetMemberByIndex(entry, "", "sys", 1).First().MemberType, PythonMemberType.Module);
        }

        public static IEnumerable<string> GetCompletionDocumentationByIndex(ModuleAnalysis entry, string variable, string memberName, int index) {
            return GetMemberByIndex(entry, variable, memberName, index).Select(m => m.Documentation);
        }

        public static IEnumerable<MemberResult> GetMemberByIndex(ModuleAnalysis entry, string variable, string memberName, int index) {
            return entry.GetMembersByIndex(variable, index).Where(m => m.Name == memberName);
        }

        [TestMethod]
        public void TestRecurisveDataStructures() {
            var text = @"
d = {}
d[0] = d
";
            var entry = ProcessText(text);

            AssertUtil.ContainsExactly(GetVariableDescriptionsByIndex(entry, "d", 1), "dict({int : dict})");
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
            var members = entry.GetMembersByIndex("Derived()", text.IndexOf("pass")).ToArray();
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
            var members = entry.GetMembersByIndex("C()", text.IndexOf("f(1")).ToArray();
            AssertUtil.ContainsExactly(members.Where(mem => !mem.Name.StartsWith("__")).Select(x => x.Name), "x", "y");
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

            var vars = new List<IAnalysisValue>(entry.GetValuesByIndex("x[0]", text.IndexOf("pass")));            
            Assert.AreEqual(1, vars.Count);
            Assert.AreEqual(IntType, vars[0].PythonType);

            foreach (string value in new[] { "ly", "lz", "ty", "tz", "lyt", "tyt" }) {
                vars = new List<IAnalysisValue>(entry.GetValuesByIndex(value + "[0]", text.IndexOf("pass")));
                Assert.AreEqual(1, vars.Count, "value: {0}", value);
                Assert.AreEqual(IntType, vars[0].PythonType, "value: {0}", value);
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

            var vars = entry.GetVariablesByIndex("self.foo", text.IndexOf("'x'"));
            VerifyReferences(UniqifyVariables(vars), new VariableLocation(11, 9, VariableType.Definition), new VariableLocation(6, 9, VariableType.Definition), new VariableLocation(4, 14, VariableType.Reference));

            vars = entry.GetVariablesByIndex("self.foo", text.IndexOf("pass"));
            VerifyReferences(UniqifyVariables(vars), new VariableLocation(11, 9, VariableType.Definition), new VariableLocation(6, 9, VariableType.Definition), new VariableLocation(4, 14, VariableType.Reference));

            vars = entry.GetVariablesByIndex("self.foo", text.IndexOf("self.foo"));
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

            AssertUtil.ContainsExactly(entry.GetTypesFromNameByIndex("a", text.IndexOf("foo") - 10), IntType);
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
            var members = entry.GetMembersByIndex("a", text.IndexOf("c = C2()"), GetMemberOptions.IntersectMultipleResults);
            AssertUtil.DoesntContain(members.Select(x => x.Name), "foo");
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

            AssertUtil.ContainsExactly(entry1.Analysis.GetTypesFromNameByIndex("abc", text1.IndexOf("pass")), IntType);

            // re-analyze project1, we should still know about the type info provided by module2
            Prepare(entry1, GetSourceUnit(text1, "mod1"), PythonLanguageVersion.V26);
            entry1.Analyze();

            AssertUtil.ContainsExactly(entry1.Analysis.GetTypesFromNameByIndex("abc", text1.IndexOf("pass")), IntType);
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
            Assert.AreEqual(entry.GetSignaturesByIndex("cls.f", text.IndexOf("print cls.g")).First().Parameters.Length, 0);
            Assert.AreEqual(entry.GetSignaturesByIndex("cls.g", text.IndexOf("print cls.g")).First().Parameters.Length, 0);
            Assert.AreEqual(entry.GetSignaturesByIndex("cls.x", text.IndexOf("print cls.g")).First().Parameters.Length, 1);
            Assert.AreEqual(entry.GetSignaturesByIndex("cls.inst_method", text.IndexOf("print cls.g")).First().Parameters.Length, 1);

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
            Assert.AreEqual(entry.GetSignaturesByIndex("cls.f", text.IndexOf("print(cls.g)")).First().Parameters.Length, 0);
            Assert.AreEqual(entry.GetSignaturesByIndex("cls.g", text.IndexOf("print(cls.g)")).First().Parameters.Length, 0);
            Assert.AreEqual(entry.GetSignaturesByIndex("cls.x", text.IndexOf("print(cls.g)")).First().Parameters.Length, 1);
            Assert.AreEqual(entry.GetSignaturesByIndex("cls.inst_method", text.IndexOf("print(cls.g)")).First().Parameters.Length, 1);



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

        [TestMethod]
        public void TestFromImport() {
            ProcessText("from #   blah");
        }

        [TestMethod]
        public void TestSelfNestedMethod() {
            // http://pytools.codeplex.com/workitem/648
            var code = @"class MyClass:
    def func1(self):
        def func2(a, b):
            return a

        return func2('abc', 123)

x = MyClass().func1()
";

            var entry = ProcessText(code);

            var values = entry.GetValuesByIndex("x", code.IndexOf("x = ")).Select(x => x.PythonType.Name);
            AssertUtil.ContainsExactly(values, "str");
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
        public static IEnumerable<string> GetMembersFromNameByIndex(this ModuleAnalysis analysis, string name, int index) {
            return analysis.GetMembersByIndex(name, index).Select(m => m.Name);
        }
    }
}

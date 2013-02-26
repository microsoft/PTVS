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
using System.Threading;
using System.Threading.Tasks;
using IronPython.Runtime;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Values;
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

        #region Test Cases

        [TestMethod, Priority(0)]
        public void CheckInterpreter() {
            Assert.AreEqual(Interpreter.GetBuiltinType((BuiltinTypeId)(-1)), null);
            Assert.IsTrue(IntType.ToString() != "");
        }

        [TestMethod, Priority(0)]
        public void SpecialArgTypes() {
            var code = @"def f(*foo, **bar):
    pass
";
            var entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("foo", code.IndexOf("pass")), TupleType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("bar", code.IndexOf("pass")), Interpreter.GetBuiltinType(BuiltinTypeId.Dict));

            code = @"def f(*foo):
    pass

f(42)
";
            entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("foo", code.IndexOf("pass")), TupleType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("foo[0]", code.IndexOf("pass")), IntType);

            code = @"def f(*foo):
    pass

f(42, 'abc')
";
            entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("foo", code.IndexOf("pass")), TupleType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("foo[0]", code.IndexOf("pass")), IntType, BytesType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("foo[1]", code.IndexOf("pass")), IntType, BytesType);

            code = @"def f(*foo):
    pass

f(42, 'abc')
f('abc', 42)
";
            entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("foo", code.IndexOf("pass")), TupleType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("foo[0]", code.IndexOf("pass")), IntType, BytesType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("foo[1]", code.IndexOf("pass")), IntType, BytesType);

            code = @"def f(**bar):
    y = bar['foo']
    pass

f(x=42)
";
            entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("bar", code.IndexOf("pass")), Interpreter.GetBuiltinType(BuiltinTypeId.Dict));
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("bar['foo']", code.IndexOf("pass")), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y", code.IndexOf("pass")), IntType);


            code = @"def f(**bar):
    z = bar['foo']
    pass

f(x=42, y = 'abc')
";
            entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("bar", code.IndexOf("pass")), Interpreter.GetBuiltinType(BuiltinTypeId.Dict));
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("bar['foo']", code.IndexOf("pass")), IntType, BytesType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("z", code.IndexOf("pass")), IntType, BytesType);
        }

        [TestMethod, Priority(0)]
        public void ExcessPositionalArguments() {
            var code = @"def f(a, *args):
    return args[0]

x = f('abc', 1)
y = f(1, 'abc')
z = f(None, 'abc', 1)
";
            var entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", code.IndexOf("x = ")), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y", code.IndexOf("y = ")), BytesType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("z", code.IndexOf("z = ")), BytesType, IntType);
        }

        [TestMethod, Priority(0)]
        public void ExcessNamedArguments() {
            var code = @"def f(a, **args):
    return args[a]

x = f(a='b', b=1)
y = f(a='c', c=1.3)
z = f(a='b', b='abc')
w = f(a='p', p=1, q='abc')
";
            var entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", code.IndexOf("x = ")), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y", code.IndexOf("y = ")), FloatType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("z", code.IndexOf("z = ")), BytesType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("w", code.IndexOf("w = ")), BytesType, IntType);
        }

        [TestMethod, Priority(0), Timeout(5000)]
        public void RecursiveListComprehensionV32() {
            var code = @"
def f(x):
    x = []
    x = [i for i in x]
    x = (i for i in x)
    f(x)
";

            ProcessText(code, PythonLanguageVersion.V32);
            // If we complete processing then we have succeeded
        }

        [TestMethod, Priority(0)]
        public void CartesianStarArgs() {
            var code = @"def f(a, **args):
    args['foo'] = a
    return args['foo']


x = f(42)
y = f('abc')";

            var entry = ProcessText(code);

            AssertUtil.Contains(entry.GetTypesByIndex("x", code.IndexOf("x =")), IntType);
            AssertUtil.Contains(entry.GetTypesByIndex("y", code.IndexOf("x =")), BytesType);


            code = @"def f(a, **args):
    for i in xrange(2):
        if i == 1:
            return args['foo']
        else:
            args['foo'] = a

x = f(42)
y = f('abc')";

            entry = ProcessText(code);

            AssertUtil.Contains(entry.GetTypesByIndex("x", code.IndexOf("x =")), IntType);
            AssertUtil.Contains(entry.GetTypesByIndex("y", code.IndexOf("x =")), BytesType);
        }

        [TestMethod, Priority(0)]
        public void CartesianRecursive() {
            var code = @"def f(a, *args):
    f(a, args)
    return a


x = f(42)";

            var entry = ProcessText(code);

            AssertUtil.Contains(entry.GetTypesByIndex("x", code.IndexOf("x =")), IntType);
        }

        [TestMethod, Priority(0)]
        public void CartesianSimple() {
            var code = @"def f(a):
    return a


x = f(42)
y = f('foo')";

            var entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", code.IndexOf("x =")), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y", code.IndexOf("y =")), BytesType);
        }


        [TestMethod, Priority(0)]
        public void CartesianLocals() {
            var code = @"def f(a):
    b = a
    return b


x = f(42)
y = f('foo')";

            var entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", code.IndexOf("x =")), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y", code.IndexOf("y =")), BytesType);
        }

        [TestMethod, Priority(0)]
        public void CartesianClosures() {
            var code = @"def f(a):
    def g():
        return a
    return g()


x = f(42)
y = f('foo')";

            var entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", code.IndexOf("x =")), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y", code.IndexOf("y =")), BytesType);
        }

        [TestMethod, Priority(0)]
        public void CartesianContainerFactory() {
            var code = @"def list_fact(ctor):
    x = []
    for abc in xrange(10):
        x.append(ctor(abc))
    return x


a = list_fact(int)[0]
b = list_fact(str)[0]
";

            var entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", code.IndexOf("a =")), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", code.IndexOf("b =")), BytesType);
        }

        [TestMethod, Priority(0)]
        public void CartesianLocalsIsInstance() {
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

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", code.IndexOf("x =")), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y", code.IndexOf("y =")), BytesType);
        }

        [TestMethod, Priority(0)]
        public void ImportAs() {
            var entry = ProcessText(@"import sys as s, array as a");

            AssertUtil.Contains(entry.GetMemberNamesByIndex("s", 1), "winver");
            AssertUtil.Contains(entry.GetMemberNamesByIndex("a", 1), "ArrayType");

            entry = ProcessText(@"import sys as s");
            AssertUtil.Contains(entry.GetMemberNamesByIndex("s", 1), "winver");
        }

        [TestMethod, Priority(0)]
        public void DictionaryKeyValues() {
            var code = @"x = {'abc': 42, 'bar': 'baz'}

i = x['abc']
s = x['bar']
";
            var entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("i", code.IndexOf("i =")), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("s", code.IndexOf("s =")), BytesType);
        }

        [TestMethod, Priority(0)]
        public void RecursiveLists() {
            var code = @"x = []
x.append(x)

y = []
y.append(y)

def f(a):
    return a[0]

x2 = f(x)
y2 = f(y)
";
            var entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", code.IndexOf("x =")), ListType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y", code.IndexOf("y =")), ListType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x2", code.IndexOf("x2 =")), ListType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y2", code.IndexOf("y2 =")), ListType);
        }

        [TestMethod, Priority(0)]
        public void RecursiveDictionaryKeyValues() {
            var code = @"x = {'abc': 42, 'bar': 'baz'}
x['abc'] = x
x[x] = 'abc'

i = x['abc']
s = x['abc']['abc']['abc']['bar']
t = x[x]
";
            var entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("i", code.IndexOf("i =")), IntType, Interpreter.GetBuiltinType(BuiltinTypeId.Dict));
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("s", code.IndexOf("s =")), BytesType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("t", code.IndexOf("t =")), BytesType);

            code = @"x = {'y': None, 'value': 123 }
y = { 'x': x, 'value': 'abc' }
x['y'] = y

i = x['y']['x']['value']
s = y['x']['y']['value']
";
            entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("i", code.IndexOf("i =")), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("s", code.IndexOf("s =")), BytesType);
        }

        [TestMethod, Priority(0)]
        public void RecursiveTuples() {
            var code = @"class A(object):
    def __init__(self):
        self.top = None

    def fn(self, x, y):
        top = self.top
        if x > y:
            self.fn(y, x)
            return
        self.top = x, y, top

    def pop(self):
        self.top = self.top[2]

    def original(self, item=None):
        if item == None:
            item = self.top
        if item[2] != None:
            self.original(item[2])

        x, y, _ = item

a=A()
a.fn(1, 2)
a.fn(3, 4)
a.fn(5, 6)
a.fn(7, 8)
a.fn(9, 10)
a.fn(11, 12)
a.fn(13, 14)
x1, y1, _1 = a.top
a.original()
";

            var entry = ProcessText(code);

            var expectedIntType1 = IntType;
            var expectedIntType2 = new[] { IntType };
            var expectedTupleType1 = new[] { TupleType, NoneType };
            var expectedTupleType2 = new[] { TupleType, NoneType };
            if (this is StdLibAnalysisTest) {
                expectedIntType1 = PyObjectType;
                expectedIntType2 = new[] { IntType, PyObjectType };
                expectedTupleType1 = new[] { PyObjectType, NoneType };
                expectedTupleType2 = new[] { TupleType, PyObjectType, NoneType };
            }

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x1", code.IndexOf("x1, y1, _1 =")), expectedIntType1);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y1", code.IndexOf("x1, y1, _1 =")), expectedIntType1);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("_1", code.IndexOf("x1, y1, _1 =")), expectedTupleType1);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", code.IndexOf("x, y, _ =")), expectedIntType2);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y", code.IndexOf("x, y, _ =")), expectedIntType2);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("_", code.IndexOf("x, y, _ =")), expectedTupleType2);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("self.top", code.IndexOf("if item ==")), expectedTupleType2);
        }

        [TestMethod, Priority(0)]
        public void RecursiveSequences() {
            var code = @"
x = []
x.append(x)
x.append(1)
x.append(3.14)
x.append('abc')
x.append(x)
y = x[0]
";
            var entry = ProcessText(code);

            // Completing analysis is the main test, but we'll also ensure that
            // the right types are in the list.

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y", 1), ListType, IntType, FloatType, BytesType);
        }

        [TestMethod, Priority(0)]
        public void CombinedTupleSignatures() {
            var code = @"def a():
    if x:
        return (1, True)
    elif y:
        return (1, True)
    else:
        return (2, False)

x = a()
";
            var entry = ProcessText(code);

            var func = entry.GetValuesByIndex("a", 0).OfType<FunctionInfo>().FirstOrDefault();
            Assert.IsNotNull(func);
            var sb = new StringBuilder();
            func.AddReturnTypeString(sb);
            Assert.AreEqual(" -> tuple", sb.ToString());
        }

        [TestMethod, Priority(0)]
        public void ImportStar() {
            var entry = ProcessText(@"
from nt import *
            ");

            var members = entry.GetMemberNamesByIndex("", 1);

            AssertUtil.Contains(members, "abort");

            entry = ProcessText(@"");

            // make sure abort hasn't become a builtin, if so this test needs to be updated
            // with a new name
            if (entry.GetMemberNamesByIndex("", 1).Contains("abort")) {
                Assert.Fail("abort has become a builtin, or a clean module includes it for some reason");
            }
        }

        [TestMethod, Priority(0)]
        public void ImportTrailingComma() {
            var entry = ProcessText(@"
import nt,
            ");

            var members = entry.GetMemberNamesByIndex("nt", 1);

            AssertUtil.Contains(members, "abort");
        }

        [TestMethod, Priority(0)]
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

            mod1.Analyze(CancellationToken.None, true);
            mod2.Analyze(CancellationToken.None, true);
            mod1.AnalysisGroup.AnalyzeQueuedEntries(CancellationToken.None);

            VerifyReferences(
                UniqifyVariables(mod2.Analysis.GetVariablesByIndex("D", text2.IndexOf("class D"))),
                new VariableLocation(2, 7, VariableType.Definition),
                new VariableLocation(4, 5, VariableType.Reference)
            );
        }


        [TestMethod, Priority(0)]
        public void MutatingReferences() {
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

            mod1.Analyze(CancellationToken.None);
            mod2.Analyze(CancellationToken.None);


            VerifyReferences(UniqifyVariables(mod1.Analysis.GetVariablesByIndex("SomeMethod", text1.IndexOf("SomeMethod"))),
                new VariableLocation(5, 9, VariableType.Definition), new VariableLocation(5, 20, VariableType.Reference));

            // mutate 1st file
            text1 = text1.Substring(0, text1.IndexOf("    def")) + Environment.NewLine + text1.Substring(text1.IndexOf("    def"));
            Prepare(mod1, GetSourceUnit(text1, "mod1"));
            mod1.Analyze(CancellationToken.None);

            VerifyReferences(UniqifyVariables(mod1.Analysis.GetVariablesByIndex("SomeMethod", text1.IndexOf("SomeMethod"))),
                new VariableLocation(6, 9, VariableType.Definition), new VariableLocation(5, 20, VariableType.Reference));

            // mutate 2nd file
            text2 = Environment.NewLine + text2;
            Prepare(mod2, GetSourceUnit(text2, "mod1"));
            mod2.Analyze(CancellationToken.None);

            VerifyReferences(UniqifyVariables(mod1.Analysis.GetVariablesByIndex("SomeMethod", text1.IndexOf("SomeMethod"))),
                new VariableLocation(6, 9, VariableType.Definition), new VariableLocation(6, 20, VariableType.Reference));

        }

        [TestMethod, Priority(0)]
        public void PrivateMembers() {
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

            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("self", code.IndexOf("_C__X")), "__X", "__init__", "__doc__", "__class__");
            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("self", code.IndexOf("print")), "_C__X", "__init__", "__doc__", "__class__");

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
            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("self", code.IndexOf("self.__f")), GetUnion(_objectMembers, "__f", "__init__"));
            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("self", code.IndexOf("marker")), GetUnion(_objectMembers, "_C__f", "__init__"));

            code = @"
class C(object):
    __FOO = 42

    def f(self):
        abc = C.__FOO  # Completion should work here


xyz = C._C__FOO  # Advanced members completion should work here
";
            entry = ProcessText(code);
            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("C", code.IndexOf("abc = ")), GetUnion(_objectMembers, "__FOO", "f"));
            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("C", code.IndexOf("xyz = ")), GetUnion(_objectMembers, "_C__FOO", "f"));

        }

        [TestMethod, Priority(0)]
        public void BaseInstanceVariable() {
            var code = @"
class C:
    def __init__(self):
        self.abc = 42


class D(C):
    def __init__(self):        
        self.foo = self.abc
";

            var entry = ProcessText(code);
            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("self.foo", code.IndexOf("self.foo")), _intMembers);
            AssertUtil.Contains(entry.GetMemberNamesByIndex("self", code.IndexOf("self.foo")), "abc");
        }

        [TestMethod, Priority(0)]
        public void Mro() {
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

            var clsA = entry.GetValuesByIndex("A", code.IndexOf("a =")).FirstOrDefault() as ClassInfo;
            Assert.IsNotNull(clsA);
            var mroA = clsA.Mro.SelectMany(ns => ns.Select(n => n.ShortDescription)).ToList();
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

            var clsC = entry.GetValuesByIndex("C", code.IndexOf("c =")).FirstOrDefault() as ClassInfo;
            Assert.IsNotNull(clsC);
            Assert.IsFalse(clsC.Mro.IsValid);

            // Unsuccessful: cannot order F and E
            code = @"
class F(object): remember2buy='spam'
class E(F): remember2buy='eggs'
class G(F,E): pass
G.remember2buy
";

            entry = ProcessText(code);
            var clsG = entry.GetValuesByIndex("G", code.IndexOf("G.remember2buy")).FirstOrDefault() as ClassInfo;
            Assert.IsNotNull(clsG);
            Assert.IsFalse(clsG.Mro.IsValid);


            // Successful: exchanging bases of G fixes the ordering issue
            code = @"
class F(object): remember2buy='spam'
class E(F): remember2buy='eggs'
class G(E,F): pass
G.remember2buy
";

            entry = ProcessText(code);
            clsG = entry.GetValuesByIndex("G", code.IndexOf("G.remember2buy")).FirstOrDefault() as ClassInfo;
            Assert.IsNotNull(clsG);
            var mroG = clsG.Mro.SelectMany(ns => ns.Select(n => n.ShortDescription)).ToList();
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
            var clsZ = entry.GetValuesByIndex("Z", code.IndexOf("z =")).FirstOrDefault() as ClassInfo;
            Assert.IsNotNull(clsZ);
            var mroZ = clsZ.Mro.SelectMany(ns => ns.Select(n => n.ShortDescription)).ToList();
            AssertUtil.ContainsExactly(mroZ, "Z", "K1", "K2", "K3", "D", "A", "B", "C", "E", "type object");

            // Successful: MRO is Z K1 K2 K3 D A B C E object
            code = @"
class A(int): pass
class B(float): pass
class C(str): pass
z = None
";

            entry = ProcessText(code);
            clsA = entry.GetValuesByIndex("A", code.IndexOf("z =")).FirstOrDefault() as ClassInfo;
            Assert.IsNotNull(clsA);
            mroA = clsA.Mro.SelectMany(ns => ns.Select(n => n.ShortDescription)).ToList();
            AssertUtil.ContainsExactly(mroA, "A", "type int");

            var clsB = entry.GetValuesByIndex("B", code.IndexOf("z =")).FirstOrDefault() as ClassInfo;
            Assert.IsNotNull(clsB);
            var mroB = clsB.Mro.SelectMany(ns => ns.Select(n => n.ShortDescription)).ToList();
            AssertUtil.ContainsExactly(mroB, "B", "type float");

            clsC = entry.GetValuesByIndex("C", code.IndexOf("z =")).FirstOrDefault() as ClassInfo;
            Assert.IsNotNull(clsC);
            var mroC = clsC.Mro.SelectMany(ns => ns.Select(n => n.ShortDescription)).ToList();
            AssertUtil.ContainsExactly(mroC, "C", "type str");
        }

        [TestMethod, Priority(0)]
        public void Iterator() {
            var entry = ProcessText(@"
A = [1, 2, 3]
B = 'abc'
C = [1.0, 'a', 3]

iA = iter(A)
iB = iter(B)
iC = iter(C)
", PythonLanguageVersion.V27);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("iA", 1), Interpreter.GetBuiltinType(BuiltinTypeId.ListIterator));
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("iB", 1), Interpreter.GetBuiltinType(BuiltinTypeId.StrIterator));
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("iC", 1), Interpreter.GetBuiltinType(BuiltinTypeId.ListIterator));

            entry = ProcessText(@"
A = [1, 2, 3]
B = 'abc'
C = [1.0, 'a', 3]

iA = A.__iter__()
iB = B.__iter__()
iC = C.__iter__()
", PythonLanguageVersion.V27);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("iA", 1), Interpreter.GetBuiltinType(BuiltinTypeId.ListIterator));
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("iB", 1), Interpreter.GetBuiltinType(BuiltinTypeId.StrIterator));
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("iC", 1), Interpreter.GetBuiltinType(BuiltinTypeId.ListIterator));


            entry = ProcessText(@"
A = [1, 2, 3]
B = 'abc'
C = [1.0, 'a', 3]

iA, iB, iC = A.__iter__(), B.__iter__(), C.__iter__()
a = iA.next()
b = next(iB)
_next = next
c = _next(iC)
", PythonLanguageVersion.V27);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", 1), BytesType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("c", 1), IntType, BytesType, FloatType);

            if (!(this is IronPythonAnalysisTest)) {
                entry = ProcessText(@"
A = [1, 2, 3]
B = 'abc'
C = [1.0, 'a', 3]

iA, iB, iC = A.__iter__(), B.__iter__(), C.__iter__()
a = iA.__next__()
b = next(iB)
_next = next
c = _next(iC)
", PythonLanguageVersion.V30);

                AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", 1), IntType);
                AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", 1), UnicodeType);
                AssertUtil.ContainsExactly(entry.GetTypesByIndex("c", 1), IntType, UnicodeType, FloatType);
            }

            entry = ProcessText(@"
iA = iter(lambda: 1, 2)
iB = iter(lambda: 'abc', None)
iC = iter(lambda: 1, 'abc')

a = next(iA)
b = next(iB)
c = next(iC)
", PythonLanguageVersion.V27);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", 1), BytesType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("c", 1), IntType);

            if (!(this is IronPythonAnalysisTest)) {
                entry = ProcessText(@"
iA = iter(lambda: 1, 2)
iB = iter(lambda: 'abc', None)
iC = iter(lambda: 1, 'abc')

a = next(iA)
b = next(iB)
c = next(iC)
", PythonLanguageVersion.V30);

                AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", 1), IntType);
                AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", 1), UnicodeType);
                AssertUtil.ContainsExactly(entry.GetTypesByIndex("c", 1), IntType);
            }
        }

        [TestMethod, Priority(0)]
        public void Generator2x() {
            var entry = ProcessText(@"
def f():
    yield 1
    yield 2
    yield 3

a = f()
b = a.next()

for c in f():
    print c
d = a.__next__()
            ", PythonLanguageVersion.V27);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", 1), GeneratorType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("c", 1), IntType);
            if (this is IronPythonAnalysisTest) {
                AssertUtil.ContainsExactly(entry.GetTypesByIndex("d", 1));
            } else {
                AssertUtil.ContainsExactly(entry.GetTypesByIndex("d", 1), PyObjectType);
            }

            entry = ProcessText(@"
def f(x):
    yield x

a1 = f(42)
b1 = a1.next()
a2 = f('abc')
b2 = a2.next()

for c in f():
    print c
d = a1.__next__()
            ", PythonLanguageVersion.V27);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a1", 1), GeneratorType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b1", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a2", 1), GeneratorType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b2", 1), BytesType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("c", 1));
            if (this is IronPythonAnalysisTest) {
                AssertUtil.ContainsExactly(entry.GetTypesByIndex("d", 1));
            } else {
                AssertUtil.ContainsExactly(entry.GetTypesByIndex("d", 1), PyObjectType);
            }

            var text = @"
def f():
    yield 1
    x = yield 2

a = f()
b = a.next()
c = a.send('abc')
d = a.__next__()";
            entry = ProcessText(text, PythonLanguageVersion.V27);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", 1), GeneratorType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("c", 1), IntType);
            if (this is IronPythonAnalysisTest) {
                AssertUtil.ContainsExactly(entry.GetTypesByIndex("d", 1));
            } else {
                AssertUtil.ContainsExactly(entry.GetTypesByIndex("d", 1), PyObjectType);
            }
        }

        [TestMethod, Priority(0)]
        public void Generator3x() {
            if (this is IronPythonAnalysisTest) {
                Assert.Inconclusive("IronPython does not yet support __next__() method");
            }

            var entry = ProcessText(@"
def f():
    yield 1
    yield 2
    yield 3

a = f()
b = a.__next__()

for c in f():
    print c

d = a.next()
            ", PythonLanguageVersion.V30);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", 1), GeneratorType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("c", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("d", 1), PyObjectType);

            entry = ProcessText(@"
def f(x):
    yield x

a1 = f(42)
b1 = a1.__next__()
a2 = f('abc')
b2 = a2.__next__()

for c in f(42):
    print c
d = a1.next()
            ", PythonLanguageVersion.V30);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a1", 1), GeneratorType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b1", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a2", 1), GeneratorType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b2", 1), UnicodeType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("c", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("d", 1), PyObjectType);

            var text = @"
def f():
    yield 1
    x = yield 2

a = f()
b = a.__next__()
c = a.send('abc')
d = a.next()";
            entry = ProcessText(text, PythonLanguageVersion.V30);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", 1), GeneratorType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("c", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", text.IndexOf("yield 2")), UnicodeType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("d", 1), PyObjectType);
        }

        [TestMethod, Priority(0)]
        public void GeneratorDelegation() {
            if (this is IronPythonAnalysisTest) {
                Assert.Inconclusive("IronPython does not yet support yield from.");
            }

            var text = @"
def f():
    yield 1
    yield 2
    yield 3

def g():
    yield from f()

a = g()
a2 = iter(a)
b = next(a)

for c in g():
    print(c)
";
            var entry = ProcessText(text, PythonLanguageVersion.V33);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", 1), GeneratorType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a2", 1), GeneratorType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("c", 1), IntType);

            text = @"
def f(x):
    yield from x

a = f([42, 1337])
b = a.__next__()

#for c in f([42, 1337]):
#    print(c)
";
            entry = ProcessText(text, PythonLanguageVersion.V33);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", 1), GeneratorType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", 1), IntType);
            //AssertUtil.ContainsExactly(entry.GetTypesByIndex("c", 1), IntType);

            text = @"
def g():
    yield 1
    x = yield 2

def f(fn):
    yield from fn()

a = f(g)
b = a.__next__()
c = a.send('abc')
";
            entry = ProcessText(text, PythonLanguageVersion.V33);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", 1), GeneratorType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("c", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", text.IndexOf("yield 2")), UnicodeType);

            text = @"
def g():
    yield 1
    return 'abc'

def f(fn):
    x = yield from fn()

a = f(g)
b = a.__next__()
";
            entry = ProcessText(text, PythonLanguageVersion.V33);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", 1), GeneratorType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", text.IndexOf("yield from fn()")), UnicodeType);

            text = @"
def g():
    yield 1
    return 'abc', 1.5

def h(fn):
    return (yield from fn())

def f(fn):
    x, y = yield from h(fn)

a = f(g)
b = next(a)
";
            entry = ProcessText(text, PythonLanguageVersion.V33);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", 1), GeneratorType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", text.IndexOf("yield from h(fn)")), UnicodeType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y", text.IndexOf("yield from h(fn)")), FloatType);
        }


        [TestMethod, Priority(0)]
        public void ListComprehensions() {/*
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

        [TestMethod, Priority(0)]
        public void LambdaInComprehension() {
            var text = "x = [(lambda a:[a**i for i in range(a+1)])(j) for j in range(5)]";

            var entry = ProcessText(text, PythonLanguageVersion.V33);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", 1), ListType);

            entry = ProcessText(text, PythonLanguageVersion.V30);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", 1), ListType);

            entry = ProcessText(text, PythonLanguageVersion.V27);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", 1), ListType);
        }

        [TestMethod, Priority(0)]
        public void Comprehensions() {
            var text = @"
x = 10; g = (i for i in range(x)); x = 5
x = 10; t = False; g = ((i,j) for i in range(x) if t for j in range(x))
x = 5; t = True;
[(i,j) for i in range(10) for j in range(5)]
[ x for x in range(10) if x % 2 if x % 3 ]
list(x for x in range(10) if x % 2 if x % 3)
[x for x, in [(4,), (5,), (6,)]]
list(x for x, in [(7,), (8,), (9,)])
";
            var entry = ProcessText(text, PythonLanguageVersion.V27);
            Assert.IsNotNull(entry);

            entry = ProcessText(text, PythonLanguageVersion.V32);
            Assert.IsNotNull(entry);
        }

        [TestMethod, Priority(0)]
        public void ExecReferences() {
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

        [TestMethod, Priority(0)]
        public void PrivateMemberReferences() {
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

        [TestMethod, Priority(0)]
        public void GeneratorComprehensions() {
            var text = @"
x = [2,3,4]
y = (a for a in x)
for z in y:
    print z
";

            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("z", text.IndexOf("print")), IntType);

            text = @"
x = [2,3,4]
y = (a for a in x)

def f(iterable):
    for z in iterable:
        print z

f(y)
";

            entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("z", text.IndexOf("print")), IntType);

            text = @"
x = [True, False, None]

def f(iterable):
    result = None
    for i in iterable:
        if i:
            result = i
    return result

y = f(i for i in x)
";

            entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y", text.IndexOf("y =")), BoolType, NoneType);


            text = @"
def f(abc):
    print abc

(f(x) for x in [2,3,4])
";

            entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("abc", text.IndexOf("print")), IntType);

            var vars = entry.GetVariablesByIndex("f", text.IndexOf("(f(x"));
            VerifyReferences(UniqifyVariables(vars), new VariableLocation(2, 5, VariableType.Definition), new VariableLocation(5, 2, VariableType.Reference));
        }


        [TestMethod, Priority(0)]
        public void ForSequence() {
            var entry = ProcessText(@"
x = [('abc', 42, True), ('abc', 23, False),]
for some_str, some_int, some_bool in x:
    print some_str		
    print some_int		
    print some_bool	    
");
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("some_str", 1), BytesType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("some_int", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("some_bool", 1), BoolType);
        }

        [TestMethod, Priority(0)]
        public void DynamicAttributes() {
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

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", 1), BytesType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("c", 1), BytesType);
        }

        [TestMethod, Priority(0)]
        public void GetAttr() {
            var entry = ProcessText(@"
class x(object):
    def __init__(self, value):
        self.value = value
        
a = x(42)
b = getattr(a, 'value')
c = getattr(a, 'dne', 'foo')
d = getattr(a, 'value', 'foo')
");

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("c", 1), BytesType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("d", 1), IntType, BytesType);
        }

        [TestMethod, Priority(0)]
        public void ListAppend() {
            var entry = ProcessText(@"
x = []
x.append('abc')
y = x[0]
");

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y", 1), BytesType);

            entry = ProcessText(@"
x = []
x.extend(('abc', ))
y = x[0]
");
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y", 1), BytesType);

            entry = ProcessText(@"
x = []
x.insert(0, 'abc')
y = x[0]
");
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y", 1), BytesType);

            entry = ProcessText(@"
x = []
x.append('abc')
y = x.pop()
");

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y", 1), BytesType);

            entry = ProcessText(@"
class ListTest(object):
    def reset(self):
        self.items = []
        self.pushItem(self)
    def pushItem(self, item):
        self.items.append(item)

a = ListTest()
b = a.items[0]");

            AssertUtil.Contains(entry.GetMemberNamesByIndex("b", 1), "pushItem");
        }

        [TestMethod, Priority(0)]
        public void Slicing() {
            var entry = ProcessText(@"
x = [2]
y = x[:-1]
z = y[0]
");

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("z", 1), IntType);

            entry = ProcessText(@"
x = (2, 3, 4)
y = x[:-1]
z = y[0]
");

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("z", 1), IntType);

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
                AssertUtil.ContainsExactly(entry.GetTypesByIndex(name, text.IndexOf(name + " = ")), BytesType);
            }
        }

        [TestMethod, Priority(0)]
        public void ConstantIndex() {
            var entry = ProcessText(@"
ZERO = 0
ONE = 1
TWO = 2
x = ['abc', 42, True)]


some_str = x[ZERO]
some_int = x[ONE]
some_bool = x[TWO]
");
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("some_str", 1), BytesType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("some_int", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("some_bool", 1), BoolType);
        }

        [TestMethod, Priority(0)]
        public void CtorSignatures() {
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
        [TestMethod, Priority(0)]
        public void ListSubclassSignatures() {
            var text = @"
class C(list):
    pass

a = C()
a.count";
            var entry = ProcessText(text);

            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("a", text.IndexOf("a =")), "C instance");
            var result = entry.GetSignaturesByIndex("a.count", text.IndexOf("a.count")).ToArray();
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(1, result[0].Parameters.Length);
        }


        [TestMethod, Priority(0)]
        public void DocStrings() {
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

        [TestMethod, Priority(0)]
        public void Ellipsis() {
            var entry = ProcessText(@"
x = ...
            ", PythonLanguageVersion.V31);

            var result = new List<IPythonType>(entry.GetTypesByIndex("x", 1));
            Assert.AreEqual(result.Count, 1);
            Assert.AreEqual(result[0].Name, "ellipsis");
        }

        [TestMethod, Priority(0)]
        public void Backquote() {
            var entry = ProcessText(@"x = `42`");

            var result = new List<IPythonType>(entry.GetTypesByIndex("x", 1));
            Assert.AreEqual(result.Count, 1);
            Assert.AreEqual(result[0].Name, "str");
        }

        [TestMethod, Priority(0)]
        public void BuiltinMethodSignatures() {
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

        [TestMethod, Priority(0)]
        public void Del() {
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

        [TestMethod, Priority(0)]
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


            AssertUtil.ContainsExactly(entry.GetTypesByIndex("e1", text.IndexOf(", e1")), Interpreter.ImportModule("__builtin__").GetMember(Interpreter.CreateModuleContext(), "TypeError"));

            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("e2", text.IndexOf(", e2")), "MyException instance");
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

        [TestMethod, Priority(0)]
        public void ConstantMath() {

            var text2x = @"
a = 1. + 2. + 3. # no type info for a, b or c
b = 1 + 2. + 3.
c = 1. + 2 + 3.
d = 1. + 2. + 3 # d is 'int', should be 'float'
e = 1 + 1L # e is a 'float', should be 'long' under v2.x (error under v3.x)
f = 1 / 2 # f is 'int', should be 'float' under v3.x";

            var res = ProcessText(text2x, PythonLanguageVersion.V27);
            AssertUtil.ContainsExactly(res.GetTypesByIndex("a", text2x.IndexOf("a =")), FloatType);
            AssertUtil.ContainsExactly(res.GetTypesByIndex("b", text2x.IndexOf("b =")), FloatType);
            AssertUtil.ContainsExactly(res.GetTypesByIndex("c", text2x.IndexOf("c =")), FloatType);
            AssertUtil.ContainsExactly(res.GetTypesByIndex("d", text2x.IndexOf("d =")), FloatType);
            AssertUtil.ContainsExactly(res.GetTypesByIndex("e", text2x.IndexOf("e =")), Interpreter.GetBuiltinType(BuiltinTypeId.Long));
            AssertUtil.ContainsExactly(res.GetTypesByIndex("f", text2x.IndexOf("f =")), IntType);

            var text3x = @"
a = 1. + 2. + 3. # no type info for a, b or c
b = 1 + 2. + 3.
c = 1. + 2 + 3.
d = 1. + 2. + 3 # d is 'int', should be 'float'
f = 1 / 2 # f is 'int', should be 'float' under v3.x";


            res = ProcessText(text3x, PythonLanguageVersion.V30);
            AssertUtil.ContainsExactly(res.GetTypesByIndex("a", text3x.IndexOf("a =")), FloatType);
            AssertUtil.ContainsExactly(res.GetTypesByIndex("b", text3x.IndexOf("b =")), FloatType);
            AssertUtil.ContainsExactly(res.GetTypesByIndex("c", text3x.IndexOf("c =")), FloatType);
            AssertUtil.ContainsExactly(res.GetTypesByIndex("d", text3x.IndexOf("d =")), FloatType);
            AssertUtil.ContainsExactly(res.GetTypesByIndex("f", text3x.IndexOf("f =")), FloatType);
        }

        [TestMethod, Priority(0)]
        public void StringConcatenation() {
            var text = @"
x = u'abc'
y = x + u'dEf'


x1 = 'abc'
y1 = x1 + 'def'

foo = 'abc'.lower()
bar = foo + 'Def'

foo2 = u'ab' + u'cd'
bar2 = foo2 + u'ef'";

            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y", text.IndexOf("y =")), UnicodeType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y1", text.IndexOf("y1 =")), BytesType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("bar", text.IndexOf("bar =")), BytesType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("bar2", text.IndexOf("bar2 =")), UnicodeType);
        }

        [TestMethod, Priority(0)]
        public void StringFormatting() {
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
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y", text.IndexOf("y =")), UnicodeType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y1", text.IndexOf("y1 =")), BytesType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("bar", text.IndexOf("bar =")), BytesType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("bar2", text.IndexOf("bar2 =")), UnicodeType);
        }

        public virtual string UnicodeStringType {
            get {
                return "unicode";
            }
        }


        [TestMethod, Priority(0)]
        public void StringMultiply() {
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
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("y", text.IndexOf("y =")), UnicodeStringType);
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("y1", text.IndexOf("y1 =")), "str");
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("bar", text.IndexOf("bar =")), "str");
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("bar2", text.IndexOf("bar2 =")), UnicodeStringType);

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
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("y", text.IndexOf("y =")), UnicodeStringType);
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("y1", text.IndexOf("y1 =")), "str");
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("bar", text.IndexOf("bar =")), "str");
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("bar2", text.IndexOf("bar2 =")), UnicodeStringType);
        }

        [TestMethod, Priority(0)]
        public void BinaryOperators() {
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

                AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("a", text.IndexOf("a =")), "ForwardResult instance");
                AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("b", text.IndexOf("b =")), "ReverseResult instance");
                AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("c", text.IndexOf("c =")), "ReverseResult instance");
                AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("d", text.IndexOf("d =")), "ForwardResult instance");
                AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("e", text.IndexOf("e =")), "ReverseResult instance");
                AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("f", text.IndexOf("f =")), "ForwardResult instance");
                AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("g", text.IndexOf("g =")), "ForwardResult instance");
                AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("h", text.IndexOf("h =")), "ReverseResult instance");
                AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("i", text.IndexOf("i =")), "ForwardResult instance");
                AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("j", text.IndexOf("j =")), "ReverseResult instance");
                AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("k", text.IndexOf("k =")), "ForwardResult instance");
                AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("l", text.IndexOf("l =")), "ReverseResult instance");
            }
        }

        [TestMethod, Priority(0)]
        public void SequenceMultiply() {
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
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("y", text.IndexOf("y =")), "tuple");
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("y1", text.IndexOf("y1 =")), "tuple");
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("bar", text.IndexOf("bar =")), "list");
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("bar2", text.IndexOf("bar2 =")), "list");

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
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("y", text.IndexOf("y =")), "tuple");
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("y1", text.IndexOf("y1 =")), "tuple");
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("bar", text.IndexOf("bar =")), "list");
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("bar2", text.IndexOf("bar2 =")), "list");
        }

        [TestMethod, Priority(0)]
        public void SequenceContains() {
            var text = @"
a_tuple = ()
a_list = []
a_set = { 1 }
a_dict = {}
a_string = 'abc'

t1 = 100 in a_tuple
t2 = 100 not in a_tuple

l1 = 100 in a_list
l2 = 100 not in a_list

s1 = 100 in a_set
s2 = 100 not in a_set

d1 = 100 in a_dict
d2 = 100 not in a_dict

r1 = 100 in a_string
r2 = 100 not in a_string
";

            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("t1", text.IndexOf("t1 =")), BoolType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("t2", text.IndexOf("t1 =")), BoolType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("l1", text.IndexOf("t1 =")), BoolType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("l2", text.IndexOf("t1 =")), BoolType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("s1", text.IndexOf("t1 =")), BoolType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("s2", text.IndexOf("t1 =")), BoolType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("d1", text.IndexOf("t1 =")), BoolType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("d2", text.IndexOf("t1 =")), BoolType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("r1", text.IndexOf("t1 =")), BoolType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("r2", text.IndexOf("t1 =")), BoolType);

        }

        [TestMethod, Priority(0)]
        public void DescriptorNoDescriptor() {
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
            AssertUtil.Contains(entry.GetMemberNamesByIndex("self.foo", text.IndexOf("self.foo")), "nodesc_method");
        }

        /// <summary>
        /// Verifies that a line in triple quoted string which ends with a \ (eating the newline) doesn't throw
        /// off our newline tracking.
        /// </summary>
        [TestMethod, Priority(0)]
        public void ReferencesTripleQuotedStringWithBackslash() {
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
            VerifyReferences(entry.GetVariablesByIndex("self.abc", text.IndexOf("self.abc")),
                new VariableLocation(9, 14, VariableType.Definition),
                new VariableLocation(10, 18, VariableType.Reference),
                new VariableLocation(11, 20, VariableType.Reference));
            VerifyReferences(entry.GetVariablesByIndex("foo", text.IndexOf("= foo")),
                new VariableLocation(8, 24, VariableType.Definition),
                new VariableLocation(9, 20, VariableType.Reference));
        }

        [TestMethod, Priority(0)]
        public void References() {
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
            VerifyReferences(entry.GetVariablesByIndex("self.abc", text.IndexOf("self.abc")),
                new VariableLocation(5, 14, VariableType.Definition),
                new VariableLocation(6, 18, VariableType.Reference),
                new VariableLocation(7, 20, VariableType.Reference));
            VerifyReferences(entry.GetVariablesByIndex("foo", text.IndexOf("= foo")),
                new VariableLocation(4, 24, VariableType.Definition),
                new VariableLocation(5, 20, VariableType.Reference));

            text = @"
# add ref w/ type info
class D(object):
    def __init__(self, foo):
        self.abc = foo
        del self.abc
        print self.abc

D(42)";
            entry = ProcessText(text);

            VerifyReferences(entry.GetVariablesByIndex("self.abc", text.IndexOf("self.abc")),
                new VariableLocation(5, 14, VariableType.Definition),
                new VariableLocation(6, 18, VariableType.Reference),
                new VariableLocation(7, 20, VariableType.Reference));
            VerifyReferences(entry.GetVariablesByIndex("foo", text.IndexOf("= foo")),
                new VariableLocation(4, 24, VariableType.Definition),
                new VariableLocation(5, 20, VariableType.Reference));
            VerifyReferences(UniqifyVariables(entry.GetVariablesByIndex("D", text.IndexOf("D(42)"))),
                new VariableLocation(9, 1, VariableType.Reference),
                new VariableLocation(3, 7, VariableType.Definition));

            // function definitions
            text = @"
def f(): pass

x = f()";
            entry = ProcessText(text);
            VerifyReferences(UniqifyVariables(entry.GetVariablesByIndex("f", text.IndexOf("x ="))),
                new VariableLocation(4, 5, VariableType.Reference),
                new VariableLocation(2, 5, VariableType.Definition));



            text = @"
def f(): pass

x = f";
            entry = ProcessText(text);
            VerifyReferences(UniqifyVariables(entry.GetVariablesByIndex("f", text.IndexOf("x ="))),
                new VariableLocation(4, 5, VariableType.Reference),
                new VariableLocation(2, 5, VariableType.Definition));

            // class variables
            text = @"

class D(object):
    abc = 42
    print abc
    del abc
";
            entry = ProcessText(text);

            VerifyReferences(entry.GetVariablesByIndex("abc", text.IndexOf("abc =")),
                new VariableLocation(4, 5, VariableType.Definition),
                new VariableLocation(5, 11, VariableType.Reference),
                new VariableLocation(6, 9, VariableType.Reference));

            // class definition
            text = @"
class D(object): pass

a = D
";
            entry = ProcessText(text);
            VerifyReferences(UniqifyVariables(entry.GetVariablesByIndex("D", text.IndexOf("a ="))),
                new VariableLocation(4, 5, VariableType.Reference),
                new VariableLocation(2, 7, VariableType.Definition));

            // method definition
            text = @"
class D(object): 
    def f(self): pass

a = D().f()
";
            entry = ProcessText(text);
            VerifyReferences(UniqifyVariables(entry.GetVariablesByIndex("D().f", text.IndexOf("a ="))),
                new VariableLocation(5, 9, VariableType.Reference),
                new VariableLocation(3, 9, VariableType.Definition));

            // globals
            text = @"
abc = 42
print abc
del abc
";
            entry = ProcessText(text);
            VerifyReferences(entry.GetVariablesByIndex("abc", text.IndexOf("abc =")),
                new VariableLocation(4, 5, VariableType.Reference),
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(3, 7, VariableType.Reference));

            // parameters
            text = @"
def f(abc):
    print abc
    abc = 42
    del abc
";
            entry = ProcessText(text);
            VerifyReferences(entry.GetVariablesByIndex("abc", text.IndexOf("abc =")),
                new VariableLocation(2, 7, VariableType.Definition),
                new VariableLocation(4, 5, VariableType.Definition),
                new VariableLocation(3, 11, VariableType.Reference),
                new VariableLocation(5, 9, VariableType.Reference));


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


            // grammar test - expressions
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
                entry.GetVariablesByIndex("a", text.IndexOf("a = ")),
                //new VariableLocation(2, 7, VariableType.Definition),
                new VariableLocation(4, 15, VariableType.Reference),
                new VariableLocation(5, 27, VariableType.Reference),
                new VariableLocation(6, 9, VariableType.Definition)
            );
            VerifyReferences(
                entry.GetVariablesByIndex("a", text.IndexOf("print(a)")),
                //new VariableLocation(2, 7, VariableType.Definition),
                new VariableLocation(4, 15, VariableType.Reference),
                new VariableLocation(5, 27, VariableType.Reference),
                new VariableLocation(6, 9, VariableType.Definition)
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
                        StringBuilder error = new StringBuilder(String.Format("Failed to find VariableLocation({0}, {1}, VariableType.{2}) in" + Environment.NewLine, expected.StartLine, expected.StartCol, expected.Type));
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


        [TestMethod, Priority(0)]
        public void ReferencesCrossModule() {
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

            fooMod.Analyze(CancellationToken.None);
            barMod.Analyze(CancellationToken.None);

            VerifyReferences(UniqifyVariables(barMod.Analysis.GetVariablesByIndex("abc", barText.IndexOf("abc"))),
                new VariableLocation(1, 7, VariableType.Definition),     // definition 
                new VariableLocation(2, 17, VariableType.Reference),     // import
                new VariableLocation(4, 1, VariableType.Reference)       // call
            );
        }

        [TestMethod, Priority(0)]
        public void ReferencesCrossMultiModule() {
            var state = new PythonAnalyzer(Interpreter, PythonLanguageVersion.V27);

            var fooText = @"
from barbaz import abc

abc()
";
            var barText = "class abc1(object): pass";
            var bazText = "class abc2(object): pass";
            var barBazText = @"from bar import abc1 as abc
from baz import abc2 as abc";

            var fooMod = state.AddModule("foo", "foo", null);
            Prepare(fooMod, GetSourceUnit(fooText, "mod1"));
            var barMod = state.AddModule("bar", "bar", null);
            Prepare(barMod, GetSourceUnit(barText, "mod2"));
            var bazMod = state.AddModule("baz", "baz", null);
            Prepare(bazMod, GetSourceUnit(bazText, "mod3"));
            var barBazMod = state.AddModule("barbaz", "barbaz", null);
            Prepare(barBazMod, GetSourceUnit(barBazText, "mod4"));

            fooMod.Analyze(CancellationToken.None, true);
            barMod.Analyze(CancellationToken.None, true);
            bazMod.Analyze(CancellationToken.None, true);
            barBazMod.Analyze(CancellationToken.None, true);
            fooMod.AnalysisGroup.AnalyzeQueuedEntries(CancellationToken.None);
            barMod.AnalysisGroup.AnalyzeQueuedEntries(CancellationToken.None);
            bazMod.AnalysisGroup.AnalyzeQueuedEntries(CancellationToken.None);
            barBazMod.AnalysisGroup.AnalyzeQueuedEntries(CancellationToken.None);

            VerifyReferences(UniqifyVariables(barMod.Analysis.GetVariablesByIndex("abc1", barText.IndexOf("abc1"))),
                new VariableLocation(1, 7, VariableType.Definition),
                new VariableLocation(1, 25, VariableType.Reference)
            );
            VerifyReferences(UniqifyVariables(bazMod.Analysis.GetVariablesByIndex("abc2", bazText.IndexOf("abc2"))),
                new VariableLocation(1, 7, VariableType.Definition),
                new VariableLocation(2, 25, VariableType.Reference)
            );
            VerifyReferences(UniqifyVariables(fooMod.Analysis.GetVariablesByIndex("abc", 0)),
                new VariableLocation(1, 7, VariableType.Value),         // possible value
                //new VariableLocation(1, 7, VariableType.Value),       // appears twice for two modules, but cannot test that
                new VariableLocation(2, 20, VariableType.Definition),   // import
                new VariableLocation(4, 1, VariableType.Reference),     // call
                new VariableLocation(1, 25, VariableType.Definition),   // import in bar
                new VariableLocation(2, 25, VariableType.Definition)    // import in baz
            );
        }

        private static void LocationNames(List<IAnalysisVariable> vars, StringBuilder error) {
            foreach (var var in vars) { //.OrderBy(v => v.Location.Line).ThenBy(v => v.Location.Column)) {
                error.AppendFormat("   new VariableLocation({0}, {1}, VariableType.{2}),", var.Location.Line, var.Location.Column, var.Type);
                error.AppendLine();
            }
        }

        [TestMethod, Priority(0)]
        public void SignatureDefaults() {
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

        [TestMethod, Priority(0)]
        public void SpecialDictMethodsCrossUnitAnalysis() {
            // dict methods which return lists
            foreach (var method in new[] { "x.itervalues()", "x.keys()", "x.iterkeys()", "x.values()" }) {
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
                Assert.AreEqual(IntType, values[0].PythonType);
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
                Assert.AreEqual(IntType, values[0].PythonType);
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
        [TestMethod, Priority(0)]
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

                mod1.Analyze(CancellationToken.None, true);
                mod2.Analyze(CancellationToken.None, true);

                mod1.AnalysisGroup.AnalyzeQueuedEntries(CancellationToken.None);
                if (i == 0) {
                    // re-preparing shouldn't be necessary
                    Prepare(mod2, GetSourceUnit(code2, "mod2"));
                }

                mod2.Analyze(CancellationToken.None, true);
                mod2.AnalysisGroup.AnalyzeQueuedEntries(CancellationToken.None);

                var listValue = mod1.Analysis.GetValuesByIndex("l", 0).ToArray();
                Assert.AreEqual(1, listValue.Length);
                Assert.AreEqual("list of C instance", listValue[0].Description);

                var values = mod1.Analysis.GetValuesByIndex("l[0]", 0).ToArray();
                Assert.AreEqual(1, values.Length);
                Assert.AreEqual("C instance", values[0].Description);
            }
        }

        [TestMethod, Priority(0)]
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
            Assert.AreEqual(IntType, values[0].PythonType);

            values = entry.GetValuesByIndex("bar", code.IndexOf("bar = ")).ToArray();
            Assert.AreEqual(1, values.Length);
            Assert.AreEqual(IntType, values[0].PythonType);
        }

        [TestMethod, Priority(0)]
        public void SetLiteral() {
            var code = @"
x = {2, 3, 4}
for abc in x:
    print(abc)
";
            var entry = ProcessText(code);
            var values = entry.GetValuesByIndex("x", code.IndexOf("x = ")).ToArray();
            Assert.AreEqual(1, values.Length);
            Assert.AreEqual("set", values[0].ShortDescription);
            Assert.AreEqual("set of int", values[0].Description);

            values = entry.GetValuesByIndex("abc", code.IndexOf("print(abc)")).ToArray();
            Assert.AreEqual(1, values.Length);
            Assert.AreEqual("int", values[0].ShortDescription);
        }

        [TestMethod, Priority(0)]
        public void GetVariablesDictionaryGet() {
            var entry = ProcessText(@"
x = {42:'abc'}
            ");

            foreach (var varRef in entry.GetValuesByIndex("x.get", 1)) {
                Assert.AreEqual("bound built-in method get", varRef.Description);
            }
        }

        [TestMethod, Priority(0)]
        public void DictMethods() {
            var entry = ProcessText(@"
x = {42:'abc'}
            ", PythonLanguageVersion.V27);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x.items()[0][0]", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x.items()[0][1]", 1), BytesType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x.keys()[0]", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x.values()[0]", 1), BytesType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x.pop(1)", 1), BytesType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x.popitem()[0]", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x.popitem()[1]", 1), BytesType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x.iterkeys().next()", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x.itervalues().next()", 1), BytesType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x.iteritems().next()[0]", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x.iteritems().next()[1]", 1), BytesType);
        }

        [TestMethod, Priority(0)]
        public void DictUpdate() {
            var entry = ProcessText(@"
a = {42:100}
b = {}
b.update(a)
");

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b.items()[0][0]", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b.items()[0][1]", 1), IntType);
        }

        [TestMethod, Priority(0)]
        public void DictEnum() {
            var entry = ProcessText(@"
for x in {42:'abc'}:
    print(x)
");

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", 1), IntType);
        }

        [TestMethod, Priority(0)]
        public void FutureDivision() {
            var entry = ProcessText(@"
from __future__ import division
x = 1/2
            ");

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", 1), FloatType);
        }

        [TestMethod, Priority(0)]
        public void BoundMethodDescription() {
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

        [TestMethod, Priority(0)]
        public void LambdaExpression() {
            var entry = ProcessText(@"
x = lambda a: a
y = x(42)
");

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y", 1), IntType);

            entry = ProcessText(@"
def f(a):
    return a

x = lambda b: f(b)
y = x(42)
");

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y", 1), IntType);

        }

        [TestMethod, Priority(0)]
        public void LambdaScoping() {
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

        [TestMethod, Priority(0)]
        public void FunctionScoping() {
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

        [TestMethod, Priority(0)]
        public void RecursiveClass() {
            var entry = ProcessText(@"
cls = object

class cls(cls): 
    abc = 42
");

            entry.GetMemberNamesByIndex("cls", 1);
            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("cls().abc", 1), _intMembers);
            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("cls.abc", 1), _intMembers);
            var sigs = entry.GetSignaturesByIndex("cls", 1).ToArray();
            Assert.AreEqual(2, sigs.Length);    // 1 for object, one for cls
            Assert.AreEqual(null, sigs.First().Documentation);
        }

        [TestMethod, Priority(0)]
        public void BadMethod() {
            var entry = ProcessText(@"
class cls(object): 
    def f(): 
        'help'
        return 42

abc = cls()
foo = abc.f()
");

            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("foo", 1), _intMembers);
            var sigs = entry.GetSignaturesByIndex("cls().f", 1).ToArray();
            Assert.AreEqual(1, sigs.Length);
            Assert.AreEqual("help", sigs[0].Documentation);
        }

        [TestMethod, Priority(0)]
        public void KeywordArguments() {
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
                    "f(c = 'abc', b = 42, a = 3j)", "f(3j, c = 'abc', b = 42)", "f(3j, 42, c = 'abc')",
                    "f(c = 'abc', b = 42, a = 3j, d = 42)",  // extra argument
                    "f(3j, 42, 'abc', d = 42)",
                };

                foreach (var testCall in testCalls) {
                    var text = decl + Environment.NewLine + testCall;
                    Console.WriteLine(text);
                    var entry = ProcessText(text);

                    AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", text.IndexOf("pass")), ComplexType);
                    AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", text.IndexOf("pass")), IntType);
                    AssertUtil.ContainsExactly(entry.GetTypesByIndex("c", text.IndexOf("pass")), BytesType);
                }
            }
        }

        [TestMethod, Priority(0)]
        public void BadKeywordArguments() {
            var code = @"def f(a, b):
    return a

x = 100
z = f(a=42, x)";

            var entry = ProcessText(code);
            var values = entry.GetValuesByIndex("z", code.IndexOf("z =")).ToArray();
            Assert.AreEqual(1, values.Length);

            Assert.AreEqual(values.First().Description, "int");
        }

        //[TestMethod, Priority(2)]
        public void PositionalSplat() {
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

                    AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", text.IndexOf("pass")), ComplexType);
                    AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", text.IndexOf("pass")), IntType);
                    AssertUtil.ContainsExactly(entry.GetTypesByIndex("c", text.IndexOf("pass")), BytesType);
                }
            }
        }
        [TestMethod, Priority(0)]
        public void ForwardRef() {
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

            var allFifty = entry.GetMemberNamesByIndex("abc", text.IndexOf("abc.foo")).ToSet();
            AssertUtil.ContainsExactly(allFifty, GetUnion(_objectMembers, "baz", "foo"));

            var xTypes = entry.GetTypesByIndex("x", text.IndexOf("abc.foo")).ToSet();
            AssertUtil.ContainsExactly(xTypes, ListType, BytesType, TupleType);

            var xMembers = entry.GetMemberNamesByIndex("x", text.IndexOf("abc.foo")).ToSet();
            AssertUtil.ContainsExactly(xMembers, GetIntersection(_strMembers, _listMembers));
        }

        public static int GetIndex(string text, string substring) {
            return text.IndexOf(substring);
        }

        [TestMethod, Priority(0)]
        public void Builtins() {
            var text = @"
booltypetrue = True
booltypefalse = False
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("booltypetrue", 1), BoolType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("booltypefalse", 1), BoolType);
        }

        [TestMethod, Priority(0)]
        public void DictionaryFunctionTable() {
            var text = @"
def f(a, b):
    print a, b
    
def g(a, b):
    x, y = a, b

x = {'foo': f, 'bar' : g}
x['foo'](42, [])
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", text.IndexOf("print")), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", text.IndexOf("print")), ListType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", text.IndexOf("x, y")));
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", text.IndexOf("x, y")));
        }

        [TestMethod, Priority(0)]
        public void DictionaryAssign() {
            var text = @"
x = {'abc': 42}
y = x['foo']
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y", 1), IntType);
        }

        [TestMethod, Priority(0)]
        public void DictionaryFunctionTableGet2() {
            var text = @"
def f(a, b):
    print a, b
    
def g(a, b):
    x, y = a, b

x = {'foo': f, 'bar' : g}
x.get('foo')(42, [])
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", text.IndexOf("print")), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", text.IndexOf("print")), ListType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", text.IndexOf("x, y")));
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", text.IndexOf("x, y")));
        }

        [TestMethod, Priority(0)]
        public void DictionaryFunctionTableGet() {
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
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", text.IndexOf("print")), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", text.IndexOf("print")), ListType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", text.IndexOf("x, y")));
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", text.IndexOf("x, y")));
        }

        [TestMethod, Priority(0)]
        public void SimpleGlobals() {
            var text = @"
class x(object):
    def abc(self):
        pass
        
a = x()
x.abc()
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetVariablesNoBuiltinsByIndex(1), "a", "x");
            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("x", 1), GetUnion(_objectMembers, "abc"));
        }

        [TestMethod, Priority(0)]
        public void FuncCallInIf() {
            var text = @"
def Method(a, b, c):
    print a, b, c
    
if not Method(42, 'abc', []):
    pass
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", text.IndexOf("print")), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", text.IndexOf("print")), BytesType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("c", text.IndexOf("print")), ListType);
        }

        [TestMethod, Priority(0)]
        public void WithStatement() {
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
            var foo = entry.GetMemberNamesByIndex("foo", text.IndexOf("print foo"));
            AssertUtil.ContainsExactly(foo, GetUnion(_objectMembers, "x_method"));
        }

        [TestMethod, Priority(0)]
        public void OverrideFunction() {
            var text = @"
class bar(object):
    def Call(self, xvar, yvar):
        pass

class baz(bar):
    def Call(self, xvar, yvar):
        x = 42
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
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("xvar", text.IndexOf("x = 42")), ListType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("xvar", text.IndexOf("pass")));
        }

        internal static readonly Regex ValidParameterName = new Regex(@"^(\*|\*\*)?[a-z_][a-z0-9_]*( *=.+)?", RegexOptions.IgnoreCase);
        internal static string GetSafeParameterName(ParameterResult result) {
            var match = ValidParameterName.Match(result.Name);

            return match.Success ? match.Value : result.Name;
        }


        /// <summary>
        /// http://pytools.codeplex.com/workitem/799
        /// </summary>
        [TestMethod, Priority(0)]
        public void OverrideCompletions() {
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

            // Ensure that nested classes are correctly resolved.
            text = @"
class bar(int):
    class foo(dict):
        pass
";
            entry = ProcessText(text);
            var barItems = entry.GetOverrideableByIndex(text.IndexOf("    pass")).Select(x => x.Name).ToSet();
            var fooItems = entry.GetOverrideableByIndex(text.IndexOf("pass")).Select(x => x.Name).ToSet();
            Assert.IsFalse(barItems.Contains("keys"));
            Assert.IsFalse(barItems.Contains("items"));
            Assert.IsTrue(barItems.Contains("bit_length"));

            Assert.IsTrue(fooItems.Contains("keys"));
            Assert.IsTrue(fooItems.Contains("items"));
            Assert.IsFalse(fooItems.Contains("bit_length"));
        }


        [TestMethod, Priority(0)]
        public void SimpleMethodCall() {
            var text = @"
class x(object):
    def abc(self, foo):
        pass
        
a = x()
a.abc('abc')
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("foo", text.IndexOf("pass")), BytesType);
            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("self", text.IndexOf("pass")), GetUnion(_objectMembers, "abc"));
        }

        [TestMethod, Priority(0)]
        public void BuiltinRetval() {
            var text = @"
x = [2,3,4]
a = x.index(2)
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", text.IndexOf("x =")).ToSet(), ListType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", text.IndexOf("a =")).ToSet(), IntType);
        }

        [TestMethod, Priority(0)]
        public void BuiltinFuncRetval() {
            var text = @"
x = ord('a')
y = range(5)
";

            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", text.IndexOf("x = ")).ToSet(), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y", text.IndexOf("y = ")).ToSet(), ListType);
        }

        [TestMethod, Priority(0)]
        public void FunctionMembers() {
            var text = @"
def f(x): pass
f.abc = 32
";
            var entry = ProcessText(text);
            AssertUtil.Contains(entry.GetMemberNamesByIndex("f", 1), "abc");

            text = @"
def f(x): pass

";
            entry = ProcessText(text);
            AssertUtil.DoesntContain(entry.GetMemberNamesByIndex("f", 1), "x");
            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("f", 1), _functionMembers);

            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("f.func_name", 1), _strMembers);
        }


        [TestMethod, Priority(0)]
        public void RangeIteration() {
            var text = @"
for i in range(5):
    pass
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("i", text.IndexOf("for i")).ToSet(), IntType);
        }

        [TestMethod, Priority(0)]
        public void BuiltinImport() {
            var text = @"
import sys
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetVariablesNoBuiltinsByIndex(1), "sys");
            Assert.IsTrue(entry.GetMemberNamesByIndex("sys", 1).Any((s) => s == "winver"));
        }

        [TestMethod, Priority(0)]
        public void BuiltinImportInFunc() {
            var text = @"
def f():
    import sys
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetVariablesNoBuiltinsByIndex(text.IndexOf("sys")), "f", "sys");
            AssertUtil.Contains(entry.GetMemberNamesByIndex("sys", text.IndexOf("sys")), "winver");
        }

        [TestMethod, Priority(0)]
        public void BuiltinImportInClass() {
            var text = @"
class C:
    import sys
";
            var entry = ProcessText(text);

            AssertUtil.ContainsExactly(entry.GetVariablesNoBuiltinsByIndex(text.IndexOf("sys")), "C", "sys");
            Assert.IsTrue(entry.GetMemberNamesByIndex("sys", text.IndexOf("sys")).Any((s) => s == "winver"));
        }

        [TestMethod, Priority(0)]
        public void NoImportClr() {
            var text = @"
x = 'abc'
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", 1), BytesType);
            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("x", 1), _strMembers);
        }

        [TestMethod, Priority(0)]
        public void MutualRecursion() {
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
            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("other", text.IndexOf("other.g")), "g", "__doc__", "__class__");
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", text.IndexOf("x =")), ListType, BytesType);
            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("x", text.IndexOf("x =")),
                GetIntersection(_listMembers, _strMembers));
        }

        [TestMethod, Priority(0)]
        public void MutualGeneratorRecursion() {
            var text = @"
class C:
    def f(self, other, depth):
        if depth == 0:
            yield 'abc'
        yield next(other.g(self, depth - 1))

class D:
    def g(self, other, depth):
        if depth == 0:
            yield ['d', 'e', 'f']
        
        yield next(other.f(self, depth - 1))

x = next(D().g(C(), 42))

";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", text.IndexOf("x =")), ListType, BytesType);
        }

        [TestMethod, Priority(0)]
        public void DistinctGenerators() {
            var text = @"
def f(x):
    return x

def g(x):
    yield f(x)

class S0(object): pass
it = g(S0())
val = next(it)

" + string.Join("\r\n", Enumerable.Range(1, 100).Select(i => string.Format("class S{0}(object): pass\r\nf(S{0}())", i)));
            Console.WriteLine(text);

            // Ensure the returned generators are distinct
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("it", text.IndexOf("it =")), GeneratorType);
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("val", text.IndexOf("val =")), "S0 instance");
        }

        [TestMethod, Priority(0)]
        public void ForwardRefVars() {
            var text = @"
class x(object):
    def __init__(self, val):
        self.abc = []
    
x(42)
x('abc')
x([])
";
            var entry = ProcessText(text);
            var values = entry.GetValuesByIndex("self.abc", text.IndexOf("self.abc")).ToList();
            Assert.AreEqual(1, values.Count);
        }

        [TestMethod, Priority(0)]
        public void ReturnFunc() {
            var text = @"
def g():
    return []

def f():
    return g
    
x = f()()
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", 1), ListType);
        }

        [TestMethod, Priority(0)]
        public void ReturnArg() {
            var text = @"
def g(a):
    return a

x = g(1)
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", 1), IntType);
        }

        [TestMethod, Priority(0)]
        public void ReturnArg2() {
            var text = @"

def f(a):
    def g():
        return a
    return g

x = f(2)()
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", 1), IntType);
        }

        [TestMethod, Priority(0)]
        public void MemberAssign() {
            var text = @"
class C:
    def func(self):
        self.abc = 42

a = C()
a.func()
foo = a.abc
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("foo", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("foo", 1), _intMembers);
            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("a", 1), "abc", "func", "__doc__", "__class__");
        }

        [TestMethod, Priority(0)]
        public void MemberAssign2() {
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

        [TestMethod, Priority(0)]
        public void UnfinishedDot() {
            // the partial dot should be ignored and we shouldn't see g as
            // a member of D
            var text = @"
class D(object):
    def func(self):
        self.
        
def g(a, b, c): pass
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("self", text.IndexOf("self.")),
                GetUnion(_objectMembers, "func"));
        }

        [TestMethod, Priority(0)]
        public void CrossModule() {
            var text1 = @"
import mod2
";
            var text2 = @"
x = 42
";

            PermutedTest("mod", new[] { text1, text2 }, (pe) => {
                AssertUtil.ContainsExactly(pe[0].Analysis.GetMemberNamesByIndex("mod2", 1), "x");
            });
        }

        [TestMethod, Priority(0)]
        public void CrossModuleCall() {
            var text1 = @"
import mod2
y = mod2.f('abc')
";
            var text2 = @"
def f(x):
    return x
";

            PermutedTest("mod", new[] { text1, text2 }, (pe) => {
                AssertUtil.ContainsExactly(pe[1].Analysis.GetTypesByIndex("x", text2.IndexOf("return x")), BytesType);
                AssertUtil.ContainsExactly(pe[0].Analysis.GetTypesByIndex("y", text1.IndexOf("y")), BytesType);
            });
        }

        [TestMethod, Priority(0)]
        public void CrossModuleCallType() {
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
                AssertUtil.ContainsExactly(pe[1].Analysis.GetTypesByIndex("x", text2.IndexOf("= x")), BytesType);
                AssertUtil.ContainsExactly(pe[0].Analysis.GetTypesByIndex("y", text1.IndexOf("y")), BytesType);
            });
        }

        [TestMethod, Priority(0)]
        public void CrossModuleCallType2() {
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
                AssertUtil.ContainsExactly(pe[1].Analysis.GetTypesByIndex("x", text2.IndexOf("= x")), BytesType);
                AssertUtil.ContainsExactly(pe[0].Analysis.GetTypesByIndex("y", text1.IndexOf("y =")), BytesType);
            });
        }

        [TestMethod, Priority(0)]
        public void CrossModuleFuncAndType() {
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
                AssertUtil.ContainsExactly(pe[2].Analysis.GetMemberNamesByIndex("a", text3.IndexOf("a = ")),
                    GetUnion(_objectMembers, "f", "g"));
            });
        }

        [TestMethod, Priority(0)]
        public void MembersAfterError() {
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
            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("self", text.IndexOf("self.")),
                GetUnion(_objectMembers, "f", "g", "h"));
        }


        [TestMethod, Priority(0)]
        public void Property() {
            var text = @"
class x(object):
    @property
    def SomeProp(self):
        return 42

a = x().SomeProp
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", text.IndexOf("a =")), IntType);
        }

        [TestMethod, Priority(0)]
        public void StaticMethod() {
            var text = @"
class x(object):
    @staticmethod
    def StaticMethod(value):
        return value

a = x().StaticMethod(4.0)
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", text.IndexOf("a = ")), FloatType);
        }

        [TestMethod, Priority(0)]
        public void ClassMethod() {
            var text = @"
class x(object):
    @classmethod
    def ClassMethod(cls):
        return cls

a = x().ClassMethod()
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("a", text.IndexOf("a =")), "x");

            var exprs = new[] { "x.ClassMethod", "x().ClassMethod" };
            foreach (var expr in exprs) {
                var sigs = entry.GetSignaturesByIndex(expr, text.IndexOf("a = ")).ToArray();
                Assert.AreEqual(1, sigs.Length);
                Assert.AreEqual(sigs[0].Parameters.Length, 0); // cls is implicitly implied
            }
        }

        [TestMethod, Priority(0)]
        public void UserDescriptor() {
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
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("foo", text.IndexOf("foo = ")), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("bar", text.IndexOf("bar = ")), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("C.x", text.IndexOf("bar = ")), IntType);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("ctx", text.IndexOf("return 42")), TypeType);
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("inst", text.IndexOf("return 42")), "None", "C instance");

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
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("inst", text.IndexOf("return 42")), "C instance");
            AssertUtil.Contains(entry.GetMemberNamesByIndex("inst", text.IndexOf("return 42")), "instfunc");
        }

        [TestMethod, Priority(0)]
        public void AssignSelf() {
            var text = @"
class x(object):
    def __init__(self):
        self.x = 'abc'
    def f(self):
        pass
";
            var entry = ProcessText(text);
            AssertUtil.Contains(entry.GetMemberNamesByIndex("self", text.IndexOf("pass")), "x");
            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("self.x", text.IndexOf("pass")), _strMembers);
        }

        [TestMethod, Priority(0)]
        public void AssignToMissingMember() {
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

        static void AnalyzeLeak(Action func, int minutesBeforeMeasure = 1, int minutesBeforeAssert = 1) {
            long RUN_TIME = minutesBeforeMeasure * 60 * 1000;
            long LEAK_TIME = minutesBeforeAssert * 60 * 1000;

            var sw = new Stopwatch();
            sw.Start();
            for (var start = sw.ElapsedMilliseconds; start + RUN_TIME > sw.ElapsedMilliseconds; ) {
                func();
            }

            var memory1 = GC.GetTotalMemory(true);

            for (var start = sw.ElapsedMilliseconds; start + LEAK_TIME > sw.ElapsedMilliseconds; ) {
                func();
            }

            var memory2 = GC.GetTotalMemory(true);

            var delta = memory2 - memory1;
            Trace.TraceInformation("Usage after {0} minute(s): {1}", minutesBeforeMeasure, memory1);
            Trace.TraceInformation("Usage after {0} minute(s): {1}", minutesBeforeAssert, memory2);
            Trace.TraceInformation("Change: {0}", delta);

            Assert.AreEqual((double)memory1, (double)memory2, memory2 * 0.1, string.Format("Memory increased by {0}", delta));
        }

        //[TestMethod, Timeout(5 * 60 * 1000), Priority(2)]
        public void MemLeak() {
            if (GetType() != typeof(AnalysisTest)) {
                Assert.Inconclusive("Do not need to run this multiple times");
            }

            var state = new PythonAnalyzer(Interpreter, PythonLanguageVersion.V27);

            var bar = state.AddModule("bar", @"bar.py", EmptyAnalysisCookie.Instance);
            var baz = state.AddModule("baz", @"baz.py", EmptyAnalysisCookie.Instance);

            AnalyzeLeak(() => {
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

                bar.Analyze(CancellationToken.None);
                baz.Analyze(CancellationToken.None);
            });
        }

        //[TestMethod, Timeout(15 * 60 * 1000), Priority(2)]
        public void MemLeak2() {
            if (GetType() != typeof(AnalysisTest)) {
                Assert.Inconclusive("Do not need to run this multiple times");
            }

            bool anyTested = false;

            foreach (var ver in PythonPaths.Versions) {
                var azureDir = Path.Combine(ver.LibPath, "site-packages", "azure");
                if (Directory.Exists(azureDir)) {
                    anyTested = true;
                    AnalyzeDirLeak(azureDir);
                }
            }

            if (!anyTested) {
                Assert.Inconclusive("Test requires Azure SDK to be installed");
            }
        }

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
            Trace.TraceInformation("AddSourceUnit: {0} ms", start1 - start0);

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
            Trace.TraceInformation("Parse: {0} ms", start2 - start1);

            for (int i = 0; i < modules.Count; i++) {
                var ast = nodes[i];

                if (ast != null) {
                    modules[i].UpdateTree(ast, null);
                }
            }

            long start3 = sw.ElapsedMilliseconds;
            for (int i = 0; i < modules.Count; i++) {
                Trace.TraceInformation("Analyzing {1}: {0} ms", sw.ElapsedMilliseconds - start3, sourceUnits[i].Path);
                var ast = nodes[i];
                if (ast != null) {
                    modules[i].Analyze(CancellationToken.None, true);
                }
            }
            if (modules.Count > 0) {
                Trace.TraceInformation("Analyzing queue");
                modules[0].AnalysisGroup.AnalyzeQueuedEntries(CancellationToken.None);
            }

            int index = -1;
            for (int i = 0; i < modules.Count; i++) {
                if (((ProjectEntry)modules[i]).ModuleName == "azure.servicebus.servicebusservice") {
                    index = i;
                    break;
                }
            }
            AnalyzeLeak(() => {
                using (var reader = new FileStreamReader(modules[index].FilePath)) {
                    var ast = Parser.CreateParser(reader, PythonLanguageVersion.V27).ParseFile();

                    modules[index].UpdateTree(ast, null);
                }

                modules[index].Analyze(CancellationToken.None, true);
                modules[index].AnalysisGroup.AnalyzeQueuedEntries(CancellationToken.None);
            });
        }

        [TestMethod, Priority(1)]
        public void CancelAnalysis() {
            var ver = PythonPaths.Versions.LastOrDefault(v => v != null);
            if (ver == null) {
                Assert.Inconclusive("Test requires Python installation");
            }

            var cancelSource = new CancellationTokenSource();
            var analysisStopped = new ManualResetEvent(false);
            var thread = new Thread(_ => {
                try {
                    new AnalysisTest().AnalyzeDir(ver.LibPath, ver.Version, cancel: cancelSource.Token);
                    analysisStopped.Set();
                } catch (ThreadAbortException) {
                    Console.WriteLine("Thread was aborted");
                }
            });

            thread.Start();
            // Allow 10 seconds for parsing to complete and analysis to start
            Thread.Sleep(10000);
            cancelSource.Cancel();

            if (!analysisStopped.WaitOne(15000)) {
                thread.Abort();
                Assert.Fail("Analysis did not abort within 5 seconds");
            }
        }

        [TestMethod, Priority(0)]
        public void MoveClass() {
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

            foo.Analyze(CancellationToken.None);
            bar.Analyze(CancellationToken.None);
            baz.Analyze(CancellationToken.None);

            Assert.AreEqual(foo.Analysis.GetValuesByIndex("C", 1).First().Description, "class C");
            Assert.IsTrue(foo.Analysis.GetValuesByIndex("C", 1).First().Location.FilePath.EndsWith("bar.py"));

            barSrc = GetSourceUnit(@"
", @"bar.py");

            // delete the class..
            Prepare(bar, barSrc);
            bar.Analyze(CancellationToken.None);
            bar.AnalysisGroup.AnalyzeQueuedEntries(CancellationToken.None);

            Assert.AreEqual(foo.Analysis.GetValuesByIndex("C", 1).ToArray().Length, 0);

            fooSrc = GetSourceUnit("from baz import C", @"foo.py");
            Prepare(foo, fooSrc);

            foo.Analyze(CancellationToken.None);

            Assert.AreEqual(foo.Analysis.GetValuesByIndex("C", 1).First().Description, "class C");
            Assert.IsTrue(foo.Analysis.GetValuesByIndex("C", 1).First().Location.FilePath.EndsWith("baz.py"));
        }

        [TestMethod, Priority(0)]
        public void Package() {
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

            package.Analyze(CancellationToken.None);
            x.Analyze(CancellationToken.None);
            y.Analyze(CancellationToken.None);

            Assert.AreEqual(x.Analysis.GetValuesByIndex("y", 1).First().Description, "Python module foo.y");
            AssertUtil.ContainsExactly(x.Analysis.GetTypesByIndex("abc", 1), IntType);
        }

        [TestMethod, Priority(0)]
        public void PackageRelativeImport() {
            string tempPath = Path.GetTempPath();
            Directory.CreateDirectory(Path.Combine(tempPath, "foo"));

            var files = new[] { 
                new { Content = "from .y import abc", FullPath = Path.Combine(tempPath, "foo\\__init__.py") },
                new { Content = "from .y import abc", FullPath = Path.Combine(tempPath, "foo\\x.py") } ,
                new { Content = "abc = 42",           FullPath = Path.Combine(tempPath, "foo\\y.py") } 
            };

            var srcs = new TextReader[files.Length];
            for (int i = 0; i < files.Length; i++) {
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

            package.Analyze(CancellationToken.None);
            x.Analyze(CancellationToken.None);
            y.Analyze(CancellationToken.None);

            AssertUtil.ContainsExactly(x.Analysis.GetTypesByIndex("abc", 1), IntType);
            AssertUtil.ContainsExactly(package.Analysis.GetTypesByIndex("abc", 1), IntType);
        }

        [TestMethod, Priority(0)]
        public void PackageRelativeImportAliasedMember() {
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

            package.Analyze(CancellationToken.None);
            y.Analyze(CancellationToken.None);

            AssertUtil.ContainsExactly(package.Analysis.GetTypesByIndex("y", 1), FunctionType, ModuleType);
        }


        /// <summary>
        /// Verify that the analyzer has the proper algorithm for turning a filename into a package name
        /// </summary>
        //[TestMethod, Priority(0)] //FIXME, use files which exist somewhere
        public void PathToModuleName() {
            string nzmathPath = Path.Combine(Environment.GetEnvironmentVariable("DLR_ROOT"), @"External.LCA_RESTRICTED\Languages\IronPython\Math");

            Assert.AreEqual(PythonAnalyzer.PathToModuleName(Path.Combine(nzmathPath, @"nzmath\factor\__init__.py")), "nzmath.factor");
            Assert.AreEqual(PythonAnalyzer.PathToModuleName(Path.Combine(nzmathPath, @"nzmath\factor\find.py")), "nzmath.factor.find");
        }

        [TestMethod, Priority(0)]
        public void Defaults() {
            var text = @"
def f(x = 42):
    return x
    
a = f()
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", text.IndexOf("a =")), IntType);
        }

        [TestMethod, Priority(0)]
        public void Decorator() {
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
        return x
";

            PermutedTest("mod", new[] { text1, text2 }, (pe) => {
                AssertUtil.ContainsExactly(pe[0].Analysis.GetTypesByIndex("f", 1), FunctionType);
                AssertUtil.ContainsExactly(pe[1].Analysis.GetTypesByIndex("mod1.f", 1), FunctionType);
                AssertUtil.ContainsExactly(pe[1].Analysis.GetTypesByIndex("MyClass().mydec(mod1.f)", 1), FunctionType);
            });
        }


        [TestMethod, Priority(0)]
        public void DecoratorFlow() {
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
                    pe[1].Analysis.GetTypesByIndex("filter_func", text2.IndexOf("# @register.filter()")),
                    FunctionType,
                    NoneType
                );
            });
        }

        [TestMethod, Priority(0)]
        public void DecoratorTypes() {
            var text = @"
def nop(fn):
    def wrap():
        return fn()
    wp = wrap
    return wp

@nop
def a_tuple():
    return (1, 2, 3)

@nop
def a_list():
    return [1, 2, 3]

@nop
def a_float():
    return 0.1

@nop
def a_string():
    return 'abc'

x = a_tuple()
y = a_list()
z = a_float()
w = a_string()
";
            var entry = ProcessText(text);
            var index = text.Length;
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", index), TupleType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y", index), ListType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("z", index), FloatType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("w", index), BytesType);

            text = @"
def as_list(fn):
    def wrap(v):
        if v == 0:
            return list(fn())
        elif v == 1:
            return set(fn(*args, **kwargs))
        else:
            return str(fn())
    return wrap

@as_list
def items():
    return (1, 2, 3)

items2 = as_list(lambda: (1, 2, 3))

x = items(0)
";
            entry = ProcessText(text);
            index = text.IndexOf("x = ");
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("items", index), entry.GetTypesByIndex("items2", index));
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", index), ListType, SetType, BytesType);
        }

        [TestMethod, Priority(0)]
        public void DecoratorOverflow() {
            var text1 = @"
import mod2

@mod2.decorator_b
def decorator_a(fn):
    return fn
    

";

            var text2 = @"
import mod1

@mod1.decorator_a
def decorator_b(fn):
    return fn
";

            PermutedTest("mod", new[] { text1, text2 }, (pe) => {
                // Neither decorator is callable, but at least analysis completed
                Assert.AreEqual(0, pe[0].Analysis.GetValuesByIndex("decorator_a", 1).Count());
                Assert.AreEqual(0, pe[1].Analysis.GetValuesByIndex("decorator_b", 1).Count());
            });
        }

        [TestMethod, Priority(0)]
        public void ClassInit() {
            var text = @"
class X:
    def __init__(self, value):
        self.value = value

a = X(2)
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("value", text.IndexOf(" = value")), IntType);
        }

        /// <summary>
        /// Verifies that regardless of how we get to imports/function return values that
        /// we properly understand the imported value.
        /// </summary>
        [TestMethod, Priority(0)]
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
                    entries[0].Analysis.GetDescriptionsByIndex("g", 1),
                    "def g(...) -> built-in module re"
                );
                AssertUtil.ContainsExactly(
                    entries[0].Analysis.GetDescriptionsByIndex("f", 1),
                    "def f(...) -> built-in module sys"
                );
                AssertUtil.ContainsExactly(
                    entries[0].Analysis.GetDescriptionsByIndex("h", 1),
                    "def h(...) -> built-in module sys"
                );
                AssertUtil.ContainsExactly(
                    entries[0].Analysis.GetDescriptionsByIndex("i", 1),
                    "def i(...) -> built-in module operator"
                );
                AssertUtil.ContainsExactly(
                    entries[0].Analysis.GetDescriptionsByIndex("j", 1),
                    "def j(...) -> built-in module mmap"
                );
                AssertUtil.ContainsExactly(
                    entries[0].Analysis.GetDescriptionsByIndex("k", 1),
                    "def k(...) -> built-in module imp"
                );
            });
        }

        [TestMethod, Priority(0)]
        public void ClassNew() {
            var text = @"
class X:
    def __new__(cls, value):
        res = object.__new__(cls)
        res.value = value
        return res

a = X(2)
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("cls", text.IndexOf(" = value")), "X");
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("value", text.IndexOf(" = value")), IntType);
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("res", text.IndexOf("res.value = ")), "X instance");
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("a", text.IndexOf("a = ")), "X instance");
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a.value", text.IndexOf("a = ")), IntType);
        }

        [TestMethod, Priority(0)]
        public void Global() {
            var text = @"
x = None
y = None
def f():
    def g():
        global x, y
        x = 123
        y = 123
    return x, y

a, b = f()
";

            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", text.IndexOf("a,")), NoneType, IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", text.IndexOf("a,")), NoneType, IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", text.IndexOf("a,")), NoneType, IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y", text.IndexOf("a,")), NoneType, IntType);
        }

        [TestMethod, Priority(0)]
        public void Nonlocal() {
            var text = @"
def f():
    x = None
    y = None
    def g():
        nonlocal x, y
        x = 123
        y = 234
    return x, y

a, b = f()
";

            var entry = ProcessText(text, PythonLanguageVersion.V32);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", text.IndexOf("x =")), NoneType, IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y", text.IndexOf("y =")), NoneType, IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", text.IndexOf("nonlocal")), NoneType, IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("y", text.IndexOf("nonlocal")), NoneType, IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", text.IndexOf("a,")), NoneType, IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", text.IndexOf("a,")), NoneType, IntType);

            VerifyReferences(
                entry.GetVariablesByIndex("x", text.IndexOf("x =")),
                new VariableLocation(3, 5, VariableType.Definition),
                new VariableLocation(6, 18, VariableType.Reference),
                new VariableLocation(7, 9, VariableType.Definition),
                new VariableLocation(9, 12, VariableType.Reference)
            );

            VerifyReferences(
                entry.GetVariablesByIndex("y", text.IndexOf("x =")),
                new VariableLocation(4, 5, VariableType.Definition),
                new VariableLocation(6, 21, VariableType.Reference),
                new VariableLocation(8, 9, VariableType.Definition),
                new VariableLocation(9, 15, VariableType.Reference)
            );


            text = @"
def f(x):
    def g():
        nonlocal x
        x = 123
    return x

a = f(None)
";

            entry = ProcessText(text, PythonLanguageVersion.V32);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", text.IndexOf("a =")), NoneType, IntType);
        }

        [TestMethod, Priority(0)]
        public void IsInstance() {
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
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", text.IndexOf("z =")), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", text.IndexOf("z =") + 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", text.IndexOf("pass")), NoneType, IntType, BytesType, TupleType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", text.IndexOf("y =")), BytesType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", text.IndexOf("y =") + 1), BytesType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", text.IndexOf("else:") + 7), NoneType, IntType, BytesType, TupleType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", text.IndexOf("foo =")), TupleType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", text.LastIndexOf("pass")), TupleType);

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

f('abc')
";

            entry = ProcessText(text, PythonLanguageVersion.V32);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", text.IndexOf("f(a)")));
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", text.IndexOf("def g()")), IntType, UnicodeType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", text.IndexOf("pass")), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", text.IndexOf("print(a)")), IntType, UnicodeType);

            text = @"x = None


if True:
    pass
    assert isinstance(x, int)
    z = 100
    
    pass

print(z)";

            entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("z", text.IndexOf("z =")), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("z", 1), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("z", text.IndexOf("print(z)") - 2), IntType);

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

        [TestMethod, Priority(0)]
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
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("z", code.IndexOf("z = x")), IntType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("w", code.IndexOf("w = y")), IntType);
        }

        [TestMethod, Priority(0)]
        public void IsInstanceUserDefinedType() {
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
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("a", text.IndexOf("print(a)")), "C instance");
        }

        [TestMethod, Priority(0)]
        public void IsInstanceNested() {
            var text = @"
class R: pass

def fn(a, b, c):
    result = R()
    assert isinstance(a, str)
    result.a = a

    assert isinstance(b, type) or isinstance(b, tuple)
    if isinstance(b, tuple):
        for x in b:
            assert isinstance(x, type)
    result.b = b

    assert isinstance(c, str)
    result.c = c
    return result

r1 = fn('foo', (int, str), 'bar')
r2 = fn(123, None, 4.5)

# b1 and b2 will be type or object, since the elements of `type` are assumed to
# be objects.
b1 = r1.b[0]
b2 = r2.b[0]
";

            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("r1.a", text.IndexOf("r1 =")), BytesType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("r1.b", text.IndexOf("r1 =")), TypeType, TupleType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b1", text.IndexOf("b1 =")), TypeType, PyObjectType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("r1.c", text.IndexOf("r1 =")), BytesType);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("r2.a", text.IndexOf("r2 =")), BytesType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("r2.b", text.IndexOf("r2 =")), TypeType, TupleType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b2", text.IndexOf("b2 =")), TypeType, PyObjectType);
            AssertUtil.ContainsExactly(entry.GetTypesByIndex("r2.c", text.IndexOf("r2 =")), BytesType);
        }

        [TestMethod, Priority(0)]
        public void QuickInfo() {
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


def docstr_func():
    '''useful documentation'''
    return 42
";
            var entry = ProcessText(text);

            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("foo()", 1), "foo instance");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("int()", 1), "int");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("a", 1), "float");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("a", 1), "float");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("b", 1), "long");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("c", 1), "str");
            AssertUtil.ContainsExactly(entry.GetValuesByIndex("x", 1).Select(v => v.Description.Substring(0, 5)), "tuple");
            AssertUtil.ContainsExactly(entry.GetValuesByIndex("y", 1).Select(v => v.Description.Substring(0, 4)), "list");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("z", 1), "int");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("min", 1), "built-in function min");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("list.append", 1), "built-in method append");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("\"abc\".Length", 1));
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("c.Length", 1));
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("d", 1), "foo instance");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("sys", 1), "built-in module sys");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("f", 1), "def f(...) -> str");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("foo.f", 1), "def f(...)\r\ndeclared in foo");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("foo().g", 1), "method g of foo objects ");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("foo", 1), "class foo");
            //AssertUtil.ContainsExactly(entry.GetVariableDescriptionsByIndex("System.StringSplitOptions.RemoveEmptyEntries", 1), "field of type StringSplitOptions");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("g", 1), "def g(...)");    // return info could be better
            //AssertUtil.ContainsExactly(entry.GetVariableDescriptionsByIndex("System.AppDomain.DomainUnload", 1), "event of type System.EventHandler");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("None", 1), "None");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("f.func_name", 1), "property of type str");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("h", 1), "def h(...) -> def f(...) -> str, def g(...)");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("docstr_func", 1), "def docstr_func(...) -> int\r\nuseful documentation");

            // method which returns it's self, we shouldn't stack overflow producing the help...
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("return_func_class().return_func", 1), @"method return_func of return_func_class objects  -> method return_func of return_func_class objects 
some help");
        }

        [TestMethod, Priority(0)]
        public void CompletionDocumentation() {
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

            AssertUtil.Contains(entry.GetCompletionDocumentationByIndex("f", "func_name", 1).First(), "-> str", " = value");
            AssertUtil.Contains(entry.GetCompletionDocumentationByIndex("", "int", 1).First(), "integer");
            AssertUtil.Contains(entry.GetCompletionDocumentationByIndex("", "min", 1).First(), "min(");
        }

        [TestMethod, Priority(0)]
        public void MemberType() {
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


            Assert.AreEqual(entry.GetMemberByIndex("f", "func_name", 1).First().MemberType, PythonMemberType.Property);
            Assert.AreEqual(entry.GetMemberByIndex("list", "append", 1).First().MemberType, PythonMemberType.Method);
            Assert.AreEqual(entry.GetMemberByIndex("y", "append", 1).First().MemberType, PythonMemberType.Method);
            Assert.AreEqual(entry.GetMemberByIndex("", "int", 1).First().MemberType, PythonMemberType.Class);
            Assert.AreEqual(entry.GetMemberByIndex("", "min", 1).First().MemberType, PythonMemberType.Function);
            Assert.AreEqual(entry.GetMemberByIndex("", "sys", 1).First().MemberType, PythonMemberType.Module);
        }

        [TestMethod, Priority(0)]
        public void RecurisveDataStructures() {
            var text = @"
d = {}
d[0] = d
";
            var entry = ProcessText(text);

            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("d", 1), "dict({int : dict})");
        }

        /// <summary>
        /// Variable is refered to in the base class, defined in the derived class, we should know the type information.
        /// </summary>
        [TestMethod, Priority(0)]
        public void BaseReferencedDerivedDefined() {
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
        [TestMethod, Priority(0)]
        public void NoTypesButIsMember() {
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
            var members = entry.GetMemberNamesByIndex("C()", text.IndexOf("f(1")).Where(x => !x.StartsWith("__"));
            AssertUtil.ContainsExactly(members, "x", "y");
        }

        /// <summary>
        /// Test case where we have a member but we don't have any type information for the member.  It should
        /// still show up as a member.
        /// </summary>
        [TestMethod, Priority(0)]
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
        [TestMethod, Priority(0)]
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


        [TestMethod, Priority(0)]
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
        [TestMethod, Priority(0)]
        public void ListRecursion() {
            string text = @"
def f(x):
    print abc
    return f(list(x))

abc = f(())
";

            var entry = ProcessText(text);

            //var vars = entry.GetVariables("foo", GetLineNumber(text, "'x'"));

        }

        [TestMethod, Priority(0)]
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

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("a", text.IndexOf("foo") - 10), IntType);
        }

        [TestMethod, Priority(0)]
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
            var members = entry.GetMemberNamesByIndex("a", text.IndexOf("c = C2()"), GetMemberOptions.IntersectMultipleResults);
            AssertUtil.DoesntContain(members, "foo");
        }

        [TestMethod, Priority(0)]
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
            entry1.Analyze(CancellationToken.None);
            Prepare(entry2, GetSourceUnit(text2, "mod2"), PythonLanguageVersion.V26);
            entry2.Analyze(CancellationToken.None);

            AssertUtil.ContainsExactly(entry1.Analysis.GetTypesByIndex("abc", text1.IndexOf("pass")), IntType);

            // re-analyze project1, we should still know about the type info provided by module2
            Prepare(entry1, GetSourceUnit(text1, "mod1"), PythonLanguageVersion.V26);
            entry1.Analyze(CancellationToken.None);

            AssertUtil.ContainsExactly(entry1.Analysis.GetTypesByIndex("abc", text1.IndexOf("pass")), IntType);
        }

        [TestMethod, Priority(0)]
        public void MetaClasses() {

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
        [TestMethod, Priority(0)]
        public void InvalidMetaClassValues() {
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

            foreach (var assign in assigns) {
                string text = @"
class C(object): 
    def f(self): pass

def f():  pass

class D(metaclass = " + assign + @"):
    @classmethod
    def g(cls):
        print cls.g


    def inst_method(self):
        pass
    ";

                ProcessText(text, PythonLanguageVersion.V32);
            }
        }

        [TestMethod, Priority(0)]
        public void FromImport() {
            ProcessText("from #   blah");
        }

        [TestMethod, Priority(0)]
        public void SelfNestedMethod() {
            // http://pytools.codeplex.com/workitem/648
            var code = @"class MyClass:
    def func1(self):
        def func2(a, b):
            return a

        return func2('abc', 123)

x = MyClass().func1()
";

            var entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("x", code.IndexOf("x = ")), BytesType);
        }

        [TestMethod, Priority(0)]
        public void Super() {
            var code = @"
class Base1(object):
    def base_func(self, x): pass
    def base1_func(self): pass
class Base2(object):
    def base_func(self, x, y, z): pass
    def base2_func(self): pass
class Derived1(Base1, Base2):
    def derived1_func(self):
        print('derived1_func')
class Derived2(Base2, Base1):
    def derived2_func(self):
        print('derived2_func')
class Derived3(object):
    def derived3_func(self):
        cls = Derived1
        cls = Derived2
        print('derived3_func')
";
            foreach (var langVersion in new[] { PythonLanguageVersion.V27, PythonLanguageVersion.V32 }) {
                var entry = ProcessText(code, langVersion);

                // super(Derived1)
                {
                    // Member from derived class should not be present
                    Assert.IsFalse(entry.GetMembersByIndex("super(Derived1)", code.IndexOf("print('derived1_func')")).Any(member => member.Name == "derived1_func"));

                    // Members from both base classes with distinct names should be present, and should have all parameters including self
                    Assert.AreEqual(1, entry.GetSignaturesByIndex("super(Derived1).base1_func", code.IndexOf("print('derived1_func')")).First().Parameters.Length); // (self)
                    Assert.AreEqual(1, entry.GetSignaturesByIndex("super(Derived1).base2_func", code.IndexOf("print('derived1_func')")).First().Parameters.Length); // (self)

                    // Only one member with clashing names should be present, and it should be from Base1
                    var sigs = entry.GetSignaturesByIndex("super(Derived1).base_func", code.IndexOf("print('derived1_func')")).ToArray();
                    Assert.AreEqual(1, sigs.Length);
                    Assert.AreEqual(2, sigs[0].Parameters.Length); // (self, x)
                }

                // super(Derived2)
                {
                    // Only one member with clashing names should be present, and it should be from Base2
                    var sigs = entry.GetSignaturesByIndex("super(Derived2).base_func", code.IndexOf("print('derived2_func')")).ToArray();
                    Assert.AreEqual(1, sigs.Length);
                    Assert.AreEqual(4, sigs[0].Parameters.Length); // (self, x, y, z)
                }

                // super(Derived1, self), or Py3k magic super() to the same effect
                var supers = new List<string> { "super(Derived1, self)" };
                if (langVersion == PythonLanguageVersion.V32) {
                    supers.Add("super()");
                }
                foreach (var super in supers) {
                    // Member from derived class should not be present
                    Assert.IsFalse(entry.GetMembersByIndex(super, code.IndexOf("print('derived1_func')")).Any(member => member.Name == "derived1_func"));

                    // Members from both base classes with distinct names should be present, but shouldn't have self
                    Assert.AreEqual(0, entry.GetSignaturesByIndex(super + ".base1_func", code.IndexOf("print('derived1_func')")).First().Parameters.Length); // ()
                    Assert.AreEqual(0, entry.GetSignaturesByIndex(super + ".base2_func", code.IndexOf("print('derived1_func')")).First().Parameters.Length); // ()

                    // Only one member with clashing names should be present, and it should be from Base1
                    var sigs = entry.GetSignaturesByIndex(super + ".base_func", code.IndexOf("print('derived1_func')")).ToArray();
                    Assert.AreEqual(1, sigs.Length);
                    Assert.AreEqual(1, sigs[0].Parameters.Length); // (x)
                }

                // super(Derived2, self), or Py3k magic super() to the same effect
                supers = new List<string> { "super(Derived2, self)" };
                if (langVersion == PythonLanguageVersion.V32) {
                    supers.Add("super()");
                }
                foreach (var super in supers) {
                    // Only one member with clashing names should be present, and it should be from Base2
                    var sigs = entry.GetSignaturesByIndex(super + ".base_func", code.IndexOf("print('derived2_func')")).ToArray();
                    Assert.AreEqual(1, sigs.Length);
                    Assert.AreEqual(3, sigs[0].Parameters.Length); // (x, y, z)
                }

                // super(Derived1 union Derived1)
                {
                    // Members with clashing names from both potential bases should be unioned
                    var sigs = entry.GetSignaturesByIndex("super(cls).base_func", code.IndexOf("print('derived3_func')")).ToArray();
                    Assert.AreEqual(2, sigs.Length);
                    Assert.IsTrue(sigs.Any(overload => overload.Parameters.Length == 2)); // (self, x)
                    Assert.IsTrue(sigs.Any(overload => overload.Parameters.Length == 4)); // (self, x, y, z)
                }
            }
        }

        protected IEnumerable<IAnalysisVariable> UniqifyVariables(IEnumerable<IAnalysisVariable> vars) {
            Dictionary<LocationInfo, IAnalysisVariable> res = new Dictionary<LocationInfo, IAnalysisVariable>();
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
                    result[p[i]].Analyze(CancellationToken.None);
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

    [TestClass]
    public class StdLibAnalysisTest : AnalysisTest {
        public StdLibAnalysisTest() {
        }

        public StdLibAnalysisTest(IPythonInterpreter interpreter)
            : base(interpreter) {
        }

        protected override AnalysisLimits GetLimits() {
            return AnalysisLimits.GetStandardLibraryLimits();
        }
    }

    static class ModuleAnalysisExtensions {
        /// <summary>
        /// TODO: This method should go away, it's only being used for tests, and the tests should be using GetMembersFromExpression
        /// which may need to be cleaned up.
        /// </summary>
        public static IEnumerable<string> GetMemberNamesByIndex(this ModuleAnalysis analysis, string exprText, int index, GetMemberOptions options = GetMemberOptions.IntersectMultipleResults) {
            return analysis.GetMembersByIndex(exprText, index, options).Select(m => m.Name);
        }

        public static IEnumerable<IPythonType> GetTypesByIndex(this ModuleAnalysis analysis, string exprText, int index) {
            return analysis.GetValuesByIndex(exprText, index).Select(m => m.PythonType);
        }

        public static IEnumerable<string> GetDescriptionsByIndex(this ModuleAnalysis entry, string variable, int index) {
            return entry.GetValuesByIndex(variable, index).Select(m => m.Description);
        }

        public static IEnumerable<string> GetShortDescriptionsByIndex(this ModuleAnalysis entry, string variable, int index) {
            return entry.GetValuesByIndex(variable, index).Select(m => m.ShortDescription);
        }

        public static IEnumerable<string> GetCompletionDocumentationByIndex(this ModuleAnalysis entry, string variable, string memberName, int index) {
            return entry.GetMemberByIndex(variable, memberName, index).Select(m => m.Documentation);
        }

        public static IEnumerable<MemberResult> GetMemberByIndex(this ModuleAnalysis entry, string variable, string memberName, int index) {
            return entry.GetMembersByIndex(variable, index).Where(m => m.Name == memberName);
        }

    }
}

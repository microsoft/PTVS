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
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.Scripting.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace AnalysisTests {
    [TestClass]
    public partial class AnalysisTest : BaseAnalysisTest {
        public AnalysisTest() {
        }

        public AnalysisTest(IPythonInterpreterFactory factory, IPythonInterpreter interpreter)
            : base(factory, interpreter) {
        }

        #region Test Cases

        [TestMethod, Priority(0)]
        public void CheckInterpreter() {
            try {
                Interpreter.GetBuiltinType((BuiltinTypeId)(-1));
                Assert.Fail("Expected KeyNotFoundException");
            } catch (KeyNotFoundException) {
            }
            var intType = Interpreter.GetBuiltinType(BuiltinTypeId.Int);
            Assert.IsTrue(intType.ToString() != "");
        }

        [TestMethod, Priority(0)]
        public void SpecialArgTypes() {
            var code = @"def f(*fob, **oar):
    pass
";
            var entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("fob", code.IndexOf("pass")), BuiltinTypeId.Tuple);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("oar", code.IndexOf("pass")), BuiltinTypeId.Dict);

            code = @"def f(*fob):
    pass

f(42)
";
            entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("fob", code.IndexOf("pass")), BuiltinTypeId.Tuple);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("fob[0]", code.IndexOf("pass")), BuiltinTypeId.Int);

            code = @"def f(*fob):
    pass

f(42, 'abc')
";
            entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("fob", code.IndexOf("pass")), BuiltinTypeId.Tuple);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("fob[0]", code.IndexOf("pass")), BuiltinTypeId.Int, BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("fob[1]", code.IndexOf("pass")), BuiltinTypeId.Int, BuiltinTypeId_Str);

            code = @"def f(*fob):
    pass

f(42, 'abc')
f('abc', 42)
";
            entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("fob", code.IndexOf("pass")), BuiltinTypeId.Tuple);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("fob[0]", code.IndexOf("pass")), BuiltinTypeId.Int, BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("fob[1]", code.IndexOf("pass")), BuiltinTypeId.Int, BuiltinTypeId_Str);

            code = @"def f(**oar):
    y = oar['fob']
    pass

f(x=42)
";
            entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("oar", code.IndexOf("pass")), BuiltinTypeId.Dict);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("oar['fob']", code.IndexOf("pass")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", code.IndexOf("pass")), BuiltinTypeId.Int);


            code = @"def f(**oar):
    z = oar['fob']
    pass

f(x=42, y = 'abc')
";
            entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("oar", code.IndexOf("pass")), BuiltinTypeId.Dict);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("oar['fob']", code.IndexOf("pass")), BuiltinTypeId.Int, BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("z", code.IndexOf("pass")), BuiltinTypeId.Int, BuiltinTypeId_Str);
        }

        [TestMethod, Priority(0)]
        public void TestPackageImportStar() {
            var fobInit = GetSourceUnit("from oar import *", @"C:\Test\Lib\fob\__init__.py");
            var oarInit = GetSourceUnit("from baz import *", @"C:\Test\Lib\fob\oar\__init__.py");
            var baz = GetSourceUnit("import quox\r\nfunc = quox.func", @"C:\Test\Lib\fob\oar\baz.py");
            var quox = GetSourceUnit("def func(): return 42", @"C:\Test\Lib\fob\oar\quox.py");

            var state = CreateAnalyzer();

            AnalysisLog.Output = Console.Out;
            try {
                var fobInitState = state.AddModule("fob", @"C:\Test\Lib\fob\__init__.py");
                var oarInitState = state.AddModule("fob.oar", @"C:\Test\Lib\fob\oar\__init__.py");
                var bazState = state.AddModule("fob.oar.baz", @"C:\Test\Lib\fob\oar\baz.py");
                var quoxState = state.AddModule("fob.oar.quox", @"C:\Test\Lib\fob\oar\quox.py");

                Prepare(fobInitState, fobInit);
                Prepare(oarInitState, oarInit);
                Prepare(bazState, baz);
                Prepare(quoxState, quox);

                fobInitState.Analyze(CancellationToken.None, true);
                oarInitState.Analyze(CancellationToken.None, true);
                bazState.Analyze(CancellationToken.None, true);
                quoxState.Analyze(CancellationToken.None, true);
                state.AnalyzeQueuedEntries(CancellationToken.None);

                AssertUtil.ContainsExactly(quoxState.Analysis.GetDescriptionsByIndex("func", 1), "def func() -> int");
                AssertUtil.ContainsExactly(bazState.Analysis.GetDescriptionsByIndex("func", 1), "def func() -> int");
                AssertUtil.ContainsExactly(oarInitState.Analysis.GetDescriptionsByIndex("func", 1), "def func() -> int");
                AssertUtil.ContainsExactly(fobInitState.Analysis.GetDescriptionsByIndex("func", 1), "def func() -> int");
            } finally {
                try {
                    AnalysisLog.Flush();
                } finally {
                    AnalysisLog.Output = null;
                }
            }
        }

        [TestMethod, Priority(0)]
        public void TestClassAssignSameName() {
            var text = @"x = 123

class A:
    x = x
    pass

class B:
    x = 3.1415
    x = x
";

            var entry = ProcessText(text);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", 0), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", text.IndexOf("x =")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", text.IndexOf("pass")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("A.x", 0), BuiltinTypeId.Int);
            
            // Arguably this should only be float, but since we don't support
            // definite assignment having both int and float is correct now.
            //
            // It also means we handle this case consistently:
            //
            // class B(object):
            //     if False:
            //         x = 3.1415
            //     x = x
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("B.x", 0), BuiltinTypeId.Int, BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public void TestFunctionAssignSameName() {
            var text = @"x = 123

def f():
    x = x
    return x

y = f()
";

            var entry = ProcessText(text);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", 0), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", text.IndexOf("x =")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", text.IndexOf("return")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", 0), BuiltinTypeId.Int);
        }

        /// <summary>
        /// Binary operators should assume their result type
        /// https://pytools.codeplex.com/workitem/1575
        /// 
        /// Slicing should assume the incoming type
        /// https://pytools.codeplex.com/workitem/1581
        /// </summary>
        [TestMethod, Priority(0)]
        public void TestBuiltinOperatorsFallback() {
            var code = @"import array

slice = array.array('b', b'abcdef')[2:3]
add = array.array('b', b'abcdef') + array.array('b', b'fob')
";
            var entry = ProcessText(code);

            AssertUtil.ContainsExactly(
                entry.GetTypesByIndex("slice", code.IndexOf("slice = ")).Select(x => x.Name), 
                "array"
            );
            AssertUtil.ContainsExactly(
                entry.GetTypesByIndex("add", code.IndexOf("add = ")).Select(x => x.Name), 
                "array"
            );
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

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", code.IndexOf("x = ")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", code.IndexOf("y = ")), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("z", code.IndexOf("z = ")), BuiltinTypeId_Str, BuiltinTypeId.Int);
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

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", code.IndexOf("x = ")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", code.IndexOf("y = ")), BuiltinTypeId.Float);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("z", code.IndexOf("z = ")), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("w", code.IndexOf("w = ")), BuiltinTypeId_Str, BuiltinTypeId.Int);
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
    args['fob'] = a
    return args['fob']


x = f(42)
y = f('abc')";

            var entry = ProcessText(code);

            AssertUtil.Contains(entry.GetTypeIdsByIndex("x", code.IndexOf("x =")), BuiltinTypeId.Int);
            AssertUtil.Contains(entry.GetTypeIdsByIndex("y", code.IndexOf("x =")), BuiltinTypeId_Str);


            code = @"def f(a, **args):
    for i in xrange(2):
        if i == 1:
            return args['fob']
        else:
            args['fob'] = a

x = f(42)
y = f('abc')";

            entry = ProcessText(code);

            AssertUtil.Contains(entry.GetTypeIdsByIndex("x", code.IndexOf("x =")), BuiltinTypeId.Int);
            AssertUtil.Contains(entry.GetTypeIdsByIndex("y", code.IndexOf("x =")), BuiltinTypeId_Str);
        }

        [TestMethod, Priority(0)]
        public void CartesianRecursive() {
            var code = @"def f(a, *args):
    f(a, args)
    return a


x = f(42)";

            var entry = ProcessText(code);

            AssertUtil.Contains(entry.GetTypeIdsByIndex("x", code.IndexOf("x =")), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void CartesianSimple() {
            var code = @"def f(a):
    return a


x = f(42)
y = f('fob')";

            var entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", code.IndexOf("x =")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", code.IndexOf("y =")), BuiltinTypeId_Str);
        }


        [TestMethod, Priority(0)]
        public void CartesianLocals() {
            var code = @"def f(a):
    b = a
    return b


x = f(42)
y = f('fob')";

            var entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", code.IndexOf("x =")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", code.IndexOf("y =")), BuiltinTypeId_Str);
        }

        [TestMethod, Priority(0)]
        public void CartesianClosures() {
            var code = @"def f(a):
    def g():
        return a
    return g()


x = f(42)
y = f('fob')";

            var entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", code.IndexOf("x =")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", code.IndexOf("y =")), BuiltinTypeId_Str);
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

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", code.IndexOf("a =")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", code.IndexOf("b =")), BuiltinTypeId_Str);
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


x = f(42, 'oar')
y = f('fob', 'oar')";

            var entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", code.IndexOf("x =")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", code.IndexOf("y =")), BuiltinTypeId_Str);
        }

        [TestMethod, Priority(0)]
        public void CartesianMerge() {
            var limits = GetLimits();
            // Ensure we include enough calls
            var callCount = limits.CallDepth * limits.DecreaseCallDepth + 1;
            var code = new StringBuilder(@"def f(a):
    return g(a)

def g(b):
    return h(b)

def h(c):
    return c

");
            for (int i = 0; i < callCount; ++i) {
                code.AppendLine("x = g(123)");
            }
            code.AppendLine("y = f(3.1415)");

            var text = code.ToString();
            Console.WriteLine(text);
            var entry = ProcessText(text);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", 0), BuiltinTypeId.Int, BuiltinTypeId.Float);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", 0), BuiltinTypeId.Int, BuiltinTypeId.Float);
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
            var code = @"x = {'abc': 42, 'oar': 'baz'}

i = x['abc']
s = x['oar']
";
            var entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("i", code.IndexOf("i =")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("s", code.IndexOf("s =")), BuiltinTypeId_Str);
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

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", code.IndexOf("x =")), BuiltinTypeId.List);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", code.IndexOf("y =")), BuiltinTypeId.List);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x2", code.IndexOf("x2 =")), BuiltinTypeId.List);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y2", code.IndexOf("y2 =")), BuiltinTypeId.List);
        }

        [TestMethod, Priority(0)]
        public void RecursiveDictionaryKeyValues() {
            var code = @"x = {'abc': 42, 'oar': 'baz'}
x['abc'] = x
x[x] = 'abc'

i = x['abc']
s = x['abc']['abc']['abc']['oar']
t = x[x]
";
            var entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("i", code.IndexOf("i =")), BuiltinTypeId.Int, BuiltinTypeId.Dict);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("s", code.IndexOf("s =")), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("t", code.IndexOf("t =")), BuiltinTypeId_Str);

            code = @"x = {'y': None, 'value': 123 }
y = { 'x': x, 'value': 'abc' }
x['y'] = y

i = x['y']['x']['value']
s = y['x']['y']['value']
";
            entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("i", code.IndexOf("i =")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("s", code.IndexOf("s =")), BuiltinTypeId_Str);
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

            var expectedIntType1 = new[] { BuiltinTypeId.Int };
            var expectedIntType2 = new[] { BuiltinTypeId.Int };
            var expectedTupleType1 = new[] { BuiltinTypeId.Tuple, BuiltinTypeId.NoneType };
            var expectedTupleType2 = new[] { BuiltinTypeId.Tuple, BuiltinTypeId.NoneType };

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x1", code.IndexOf("x1, y1, _1 =")), expectedIntType1);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y1", code.IndexOf("x1, y1, _1 =")), expectedIntType1);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("_1", code.IndexOf("x1, y1, _1 =")), expectedTupleType1);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", code.IndexOf("x, y, _ =")), expectedIntType2);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", code.IndexOf("x, y, _ =")), expectedIntType2);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("_", code.IndexOf("x, y, _ =")), expectedTupleType2);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("self.top", code.IndexOf("if item ==")), expectedTupleType2);
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

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", 1), BuiltinTypeId.List, BuiltinTypeId.Int, BuiltinTypeId.Float, BuiltinTypeId_Str);
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
            FunctionInfo.AddReturnTypeString(sb, func.GetReturnValue);
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
            using (var state = PythonAnalyzer.CreateSynchronously(InterpreterFactory, Interpreter)) {

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
                    new VariableLocation(2, 7, VariableType.Definition, "mod2"),
                    new VariableLocation(4, 5, VariableType.Reference, "mod1")
                );
            }
        }


        [TestMethod, Priority(0)]
        public void MutatingReferences() {
            using (var state = PythonAnalyzer.CreateSynchronously(InterpreterFactory, Interpreter)) {

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

            AssertUtil.AreEqual(
                entry.GetSignaturesByIndex("self.f", code.IndexOf("self.f")).First().Parameters.Select(x => x.Name),
                "_C__A"
            );
            AssertUtil.AreEqual(
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
    __FOB = 42

    def f(self):
        abc = C.__FOB  # Completion should work here


xyz = C._C__FOB  # Advanced members completion should work here
";
            entry = ProcessText(code);
            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("C", code.IndexOf("abc = ")), GetUnion(_objectMembers, "__FOB", "f"));
            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("C", code.IndexOf("xyz = ")), GetUnion(_objectMembers, "_C__FOB", "f"));

        }

        [TestMethod, Priority(0)]
        public void BaseInstanceVariable() {
            var code = @"
class C:
    def __init__(self):
        self.abc = 42


class D(C):
    def __init__(self):        
        self.fob = self.abc
";

            var entry = ProcessText(code);
            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("self.fob", code.IndexOf("self.fob")), _intMembers);
            AssertUtil.Contains(entry.GetMemberNamesByIndex("self", code.IndexOf("self.fob")), "abc");
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
            Assert.IsFalse(clsC._mro.IsValid);

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
            Assert.IsFalse(clsG._mro.IsValid);


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
            AssertUtil.ContainsExactly(mroA, "A", "type int", "type object");

            var clsB = entry.GetValuesByIndex("B", code.IndexOf("z =")).FirstOrDefault() as ClassInfo;
            Assert.IsNotNull(clsB);
            var mroB = clsB.Mro.SelectMany(ns => ns.Select(n => n.ShortDescription)).ToList();
            AssertUtil.ContainsExactly(mroB, "B", "type float", "type object");

            clsC = entry.GetValuesByIndex("C", code.IndexOf("z =")).FirstOrDefault() as ClassInfo;
            Assert.IsNotNull(clsC);
            var mroC = clsC.Mro.SelectMany(ns => ns.Select(n => n.ShortDescription)).ToList();
            AssertUtil.ContainsExactly(mroC, "C", "type str", "type object");
        }

        [TestMethod, Priority(0)]
        public void ImportStarMro() {
            PermutedTest(
                "mod",
                new[] { 
                    @"
class Test_test1(object):
    def test_A(self):
        pass
",
 @"from mod1 import *

class Test_test2(Test_test1):
    def test_newtest(self):pass"
                },
                (modules) => {
                    var mro = modules[1].Analysis.GetValuesByIndex("Test_test2", 0).First().Mro.ToArray();
                    Assert.AreEqual(3, mro.Length);
                    Assert.AreEqual("Test_test2", mro[0].First().Name);
                    Assert.AreEqual("Test_test1", mro[1].First().Name);
                    Assert.AreEqual("object", mro[2].First().Name);
                }
            );
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

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("iA", 1), BuiltinTypeId.ListIterator);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("B", 1), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("iB", 1), BuiltinTypeId_StrIterator);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("iC", 1), BuiltinTypeId.ListIterator);

            entry = ProcessText(@"
A = [1, 2, 3]
B = 'abc'
C = [1.0, 'a', 3]

iA = A.__iter__()
iB = B.__iter__()
iC = C.__iter__()
", PythonLanguageVersion.V27);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("iA", 1), BuiltinTypeId.ListIterator);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("B", 1), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("iB", 1), BuiltinTypeId_StrIterator);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("iC", 1), BuiltinTypeId.ListIterator);


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

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", 1), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("c", 1), BuiltinTypeId.Int, BuiltinTypeId_Str, BuiltinTypeId.Float);

            if (SupportsPython3) {
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

                AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", 1), BuiltinTypeId.Int);
                AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", 1), BuiltinTypeId.Unicode);
                AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("c", 1), BuiltinTypeId.Int, BuiltinTypeId.Unicode, BuiltinTypeId.Float);
            }

            entry = ProcessText(@"
iA = iter(lambda: 1, 2)
iB = iter(lambda: 'abc', None)
iC = iter(lambda: 1, 'abc')

a = next(iA)
b = next(iB)
c = next(iC)
", PythonLanguageVersion.V27);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", 1), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("c", 1), BuiltinTypeId.Int);

            if (SupportsPython3) {
                entry = ProcessText(@"
iA = iter(lambda: 1, 2)
iB = iter(lambda: 'abc', None)
iC = iter(lambda: 1, 'abc')

a = next(iA)
b = next(iB)
c = next(iC)
", PythonLanguageVersion.V30);

                AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", 1), BuiltinTypeId.Int);
                AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", 1), BuiltinTypeId.Unicode);
                AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("c", 1), BuiltinTypeId.Int);
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

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", 1), BuiltinTypeId.Generator);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("c", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("d", 1));

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

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a1", 1), BuiltinTypeId.Generator);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b1", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a2", 1), BuiltinTypeId.Generator);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b2", 1), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("c", 1));
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("d", 1));

            var text = @"
def f():
    yield 1
    x = yield 2

a = f()
b = a.next()
c = a.send('abc')
d = a.__next__()";
            entry = ProcessText(text, PythonLanguageVersion.V27);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", 1), BuiltinTypeId.Generator);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("c", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("d", 1));
        }

        [TestMethod, Priority(0)]
        public void Generator3x() {
            if (!SupportsPython3) {
                return;
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

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", 1), BuiltinTypeId.Generator);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("c", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("d", 1));

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

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a1", 1), BuiltinTypeId.Generator);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b1", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a2", 1), BuiltinTypeId.Generator);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b2", 1), BuiltinTypeId.Unicode);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("c", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("d", 1));

            var text = @"
def f():
    yield 1
    x = yield 2

a = f()
b = a.__next__()
c = a.send('abc')
d = a.next()";
            entry = ProcessText(text, PythonLanguageVersion.V30);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", 1), BuiltinTypeId.Generator);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("c", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", text.IndexOf("yield 2")), BuiltinTypeId.Unicode);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("d", 1));
        }

        [TestMethod, Priority(0)]
        public void GeneratorDelegation() {
            if (!SupportsPython3) {
                // IronPython does not yet support yield from.
                return;
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

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", 1), BuiltinTypeId.Generator);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a2", 1), BuiltinTypeId.Generator);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("c", 1), BuiltinTypeId.Int);

            text = @"
def f(x):
    yield from x

a = f([42, 1337])
b = a.__next__()

#for c in f([42, 1337]):
#    print(c)
";
            entry = ProcessText(text, PythonLanguageVersion.V33);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", 1), BuiltinTypeId.Generator);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", 1), BuiltinTypeId.Int);
            //AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("c", 1), BuiltinTypeId.Int);

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

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", 1), BuiltinTypeId.Generator);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("c", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", text.IndexOf("yield 2")), BuiltinTypeId.Unicode);

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

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", 1), BuiltinTypeId.Generator);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", text.IndexOf("yield from fn()")), BuiltinTypeId.Unicode);

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

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", 1), BuiltinTypeId.Generator);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", text.IndexOf("yield from h(fn)")), BuiltinTypeId.Unicode);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", text.IndexOf("yield from h(fn)")), BuiltinTypeId.Float);
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
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", 1), BuiltinTypeId.List);

            entry = ProcessText(text, PythonLanguageVersion.V30);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", 1), BuiltinTypeId.List);

            entry = ProcessText(text, PythonLanguageVersion.V27);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", 1), BuiltinTypeId.List);
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
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("z", text.IndexOf("print")), BuiltinTypeId.Int);

            text = @"
x = [2,3,4]
y = (a for a in x)

def f(iterable):
    for z in iterable:
        print z

f(y)
";

            entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("z", text.IndexOf("print")), BuiltinTypeId.Int);

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
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", text.IndexOf("y =")), BuiltinTypeId.Bool, BuiltinTypeId.NoneType);


            text = @"
def f(abc):
    print abc

(f(x) for x in [2,3,4])
";

            entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("abc", text.IndexOf("print")), BuiltinTypeId.Int);

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
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("some_str", 1), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("some_int", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("some_bool", 1), BuiltinTypeId.Bool);
        }

        [TestMethod, Priority(0)]
        public void ForIterator() {
            var code = @"
class X(object):
    def __iter__(self): return self
    def __next__(self): return 123

class Y(object):
    def __iter__(self): return X()

for i in Y():
    pass
";
            var entry = ProcessText(code, PythonLanguageVersion.V34);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("i", code.IndexOf("pass")), BuiltinTypeId.Int);
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

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", 1), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("c", 1), BuiltinTypeId_Str);
        }

        [TestMethod, Priority(0)]
        public void GetAttr() {
            var entry = ProcessText(@"
class x(object):
    def __init__(self, value):
        self.value = value
        
a = x(42)
b = getattr(a, 'value')
c = getattr(a, 'dne', 'fob')
d = getattr(a, 'value', 'fob')
");

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("c", 1), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("d", 1), BuiltinTypeId.Int, BuiltinTypeId_Str);
        }

        [TestMethod, Priority(0)]
        public void SetAttr() {
            var entry = ProcessText(@"
class X(object):
    pass
x = X()

setattr(x, 'a', 123)
object.__setattr__(x, 'b', 3.1415)

a = x.a
b = x.b
");

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", 1), BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public void NoGetAttrForSlots() {
            var code = @"class A(object):
    def __getattr__(self, key):
        return f

def f(x, y):
    x # should be unknown
    y # should be int

a = A()
a(123, None)
a.__call__(None, 123)
";
            var entry = ProcessText(code);

            // FIXME: https://pytools.codeplex.com/workitem/2898 (for IronPython only)
            AssertUtil.DoesntContain(entry.GetTypeIdsByIndex("x", code.IndexOf("x #")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", code.IndexOf("y #")), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void VarsSpecialization() {
            var entry = ProcessText(@"
x = vars()
k = x.keys()[0]
v = x['a']
");

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", 1), BuiltinTypeId.Dict);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("k", 1), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("v", 1), BuiltinTypeId.Object);
        }

        [TestMethod, Priority(0)]
        public void DirSpecialization() {
            var entry = ProcessText(@"
x = dir()
v = x[0]
");

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", 1), BuiltinTypeId.List);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("v", 1), BuiltinTypeId_Str);
        }

        [TestMethod, Priority(0)]
        public void ListAppend() {
            var entry = ProcessText(@"
x = []
x.append('abc')
y = x[0]
");

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", 1), BuiltinTypeId_Str);

            entry = ProcessText(@"
x = []
x.extend(('abc', ))
y = x[0]
");
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", 1), BuiltinTypeId_Str);

            entry = ProcessText(@"
x = []
x.insert(0, 'abc')
y = x[0]
");
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", 1), BuiltinTypeId_Str);

            entry = ProcessText(@"
x = []
x.append('abc')
y = x.pop()
");

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", 1), BuiltinTypeId_Str);

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

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("z", 1), BuiltinTypeId.Int);

            entry = ProcessText(@"
x = (2, 3, 4)
y = x[:-1]
z = y[0]
");

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("z", 1), BuiltinTypeId.Int);

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
                AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex(name, text.IndexOf(name + " = ")), BuiltinTypeId_Str);
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
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("some_str", 1), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("some_int", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("some_bool", 1), BuiltinTypeId.Bool);
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

class CUnicode:
    u'''unicode class doc'''

class CNewStyle(object):
    '''new-style class doc'''

class CInherited(CNewStyle):
    pass

class CInit:
    '''class doc'''
    def __init__(self):
        '''init doc'''
        pass

class CUnicodeInit:
    u'''unicode class doc'''
    def __init__(self):
        u'''unicode init doc'''
        pass

class CNewStyleInit(object):
    '''new-style class doc'''
    def __init__(self):
        '''new-style init doc'''
        pass

class CInheritedInit(CNewStyleInit):
    pass
");

            var result = entry.GetSignaturesByIndex("f", 1).ToArray();
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("func doc", result[0].Documentation);

            result = entry.GetSignaturesByIndex("C", 1).ToArray();
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("class doc", result[0].Documentation);

            result = entry.GetSignaturesByIndex("funicode", 1).ToArray();
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("unicode func doc", result[0].Documentation);

            result = entry.GetSignaturesByIndex("CUnicode", 1).ToArray();
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("unicode class doc", result[0].Documentation);

            result = entry.GetSignaturesByIndex("CNewStyle", 1).ToArray();
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("new-style class doc", result[0].Documentation);

            result = entry.GetSignaturesByIndex("CInherited", 1).ToArray();
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("new-style class doc", result[0].Documentation);

            result = entry.GetSignaturesByIndex("CInit", 1).ToArray();
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("init doc", result[0].Documentation);

            result = entry.GetSignaturesByIndex("CUnicodeInit", 1).ToArray();
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("unicode init doc", result[0].Documentation);

            result = entry.GetSignaturesByIndex("CNewStyleInit", 1).ToArray();
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("new-style init doc", result[0].Documentation);

            result = entry.GetSignaturesByIndex("CInheritedInit", 1).ToArray();
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("new-style init doc", result[0].Documentation);
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
del fob
del fob[2]
del fob.oar
del (fob)
del fob, oar
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
            public readonly string FilePath;

            public VariableLocation(int startLine, int startCol, VariableType type) {
                StartLine = startLine;
                StartCol = startCol;
                Type = type;
            }

            public VariableLocation(int startLine, int startCol, VariableType type, string filePath)
            : this(startLine, startCol, type) {
                FilePath = filePath;
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
            AssertUtil.ContainsExactly(res.GetTypeIdsByIndex("a", text2x.IndexOf("a =")), BuiltinTypeId.Float);
            AssertUtil.ContainsExactly(res.GetTypeIdsByIndex("b", text2x.IndexOf("b =")), BuiltinTypeId.Float);
            AssertUtil.ContainsExactly(res.GetTypeIdsByIndex("c", text2x.IndexOf("c =")), BuiltinTypeId.Float);
            AssertUtil.ContainsExactly(res.GetTypeIdsByIndex("d", text2x.IndexOf("d =")), BuiltinTypeId.Float);
            AssertUtil.ContainsExactly(res.GetTypeIdsByIndex("e", text2x.IndexOf("e =")), BuiltinTypeId.Long);
            AssertUtil.ContainsExactly(res.GetTypeIdsByIndex("f", text2x.IndexOf("f =")), BuiltinTypeId.Int);

            var text3x = @"
a = 1. + 2. + 3. # no type info for a, b or c
b = 1 + 2. + 3.
c = 1. + 2 + 3.
d = 1. + 2. + 3 # d is 'int', should be 'float'
f = 1 / 2 # f is 'int', should be 'float' under v3.x";


            res = ProcessText(text3x, PythonLanguageVersion.V30);
            AssertUtil.ContainsExactly(res.GetTypeIdsByIndex("a", text3x.IndexOf("a =")), BuiltinTypeId.Float);
            AssertUtil.ContainsExactly(res.GetTypeIdsByIndex("b", text3x.IndexOf("b =")), BuiltinTypeId.Float);
            AssertUtil.ContainsExactly(res.GetTypeIdsByIndex("c", text3x.IndexOf("c =")), BuiltinTypeId.Float);
            AssertUtil.ContainsExactly(res.GetTypeIdsByIndex("d", text3x.IndexOf("d =")), BuiltinTypeId.Float);
            AssertUtil.ContainsExactly(res.GetTypeIdsByIndex("f", text3x.IndexOf("f =")), BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public void StringConcatenation() {
            var text = @"
x = u'abc'
y = x + u'dEf'


x1 = 'abc'
y1 = x1 + 'def'

fob = 'abc'.lower()
oar = fob + 'Def'

fob2 = u'ab' + u'cd'
oar2 = fob2 + u'ef'";

            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", text.IndexOf("y =")), BuiltinTypeId.Unicode);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y1", text.IndexOf("y1 =")), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("oar", text.IndexOf("oar =")), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("oar2", text.IndexOf("oar2 =")), BuiltinTypeId.Unicode);
        }

        [TestMethod, Priority(0)]
        public void StringFormatting() {
            var text = @"
x = u'abc %d'
y = x % (42, )


x1 = 'abc %d'
y1 = x1 % (42, )

fob = 'abc %d'.lower()
oar = fob % (42, )

fob2 = u'abc' + u'%d'
oar2 = fob2 % (42, )";

            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", text.IndexOf("y =")), BuiltinTypeId.Unicode);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y1", text.IndexOf("y1 =")), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("oar", text.IndexOf("oar =")), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("oar2", text.IndexOf("oar2 =")), BuiltinTypeId.Unicode);
        }

        public virtual BuiltinTypeId BuiltinTypeId_Str {
            get {
                return BuiltinTypeId.Bytes;
            }
        }

        public virtual BuiltinTypeId BuiltinTypeId_StrIterator {
            get {
                return BuiltinTypeId.BytesIterator;
            }
        }


        [TestMethod, Priority(0)]
        public void StringMultiply() {
            var text = @"
x = u'abc %d'
y = x * 100


x1 = 'abc %d'
y1 = x1 * 100

fob = 'abc %d'.lower()
oar = fob * 100

fob2 = u'abc' + u'%d'
oar2 = fob2 * 100";

            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", text.IndexOf("y =")), BuiltinTypeId.Unicode);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", text.IndexOf("y =")), BuiltinTypeId.Unicode);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y1", text.IndexOf("y1 =")), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("oar", text.IndexOf("oar =")), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("oar2", text.IndexOf("oar2 =")), BuiltinTypeId.Unicode);

            text = @"
x = u'abc %d'
y = 100 * x


x1 = 'abc %d'
y1 = 100 * x1

fob = 'abc %d'.lower()
oar = 100 * fob

fob2 = u'abc' + u'%d'
oar2 = 100 * fob2";

            entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", text.IndexOf("y =")), BuiltinTypeId.Unicode);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y1", text.IndexOf("y1 =")), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("oar", text.IndexOf("oar =")), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("oar2", text.IndexOf("oar2 =")), BuiltinTypeId.Unicode);
        }

        [TestMethod, Priority(0)]
        public void NotOperator() {
            var text = @"

class C(object):
    def __nonzero__(self):
        pass

    def __bool__(self):
        pass

a = not C()
";

            var entry = ProcessText(text, PythonLanguageVersion.V27);
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("a", 0), "bool");

            entry = ProcessText(text, PythonLanguageVersion.V33);
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("a", 0), "bool");
        }

        [TestMethod, Priority(0)]
        public void UnaryOperators() {
            var operators = new[] {
                new { Method = "pos", Operator = "+" },
                new { Method = "neg", Operator = "-" },
                new { Method = "invert", Operator = "~" },
            };

            var text = @"
class Result(object):
    pass

class C(object):
    def __{0}__(self):
        return Result()

a = {1}C()
b = {1}{1}C()
";

            foreach (var test in operators) {
                Console.WriteLine(test.Operator);
                var entry = ProcessText(String.Format(text, test.Method, test.Operator));

                AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("a", text.IndexOf("a =")), "Result instance");
                AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("b", text.IndexOf("b =")), "Result instance");
            }
        }

        [TestMethod, Priority(0)]
        public void BinaryOperators() {
            var operators = new[] {
                new { Method = "add", Operator = "+", Version = PythonLanguageVersion.V27 },
                new { Method = "sub", Operator = "-", Version = PythonLanguageVersion.V27 },
                new { Method = "mul", Operator = "*", Version = PythonLanguageVersion.V27 },
                new { Method = "div", Operator = "/", Version = PythonLanguageVersion.V27 },
                new { Method = "mod", Operator = "%", Version = PythonLanguageVersion.V27 },
                new { Method = "and", Operator = "&", Version = PythonLanguageVersion.V27 },
                new { Method = "or", Operator = "|", Version = PythonLanguageVersion.V27 },
                new { Method = "xor", Operator = "^", Version = PythonLanguageVersion.V27 },
                new { Method = "lshift", Operator = "<<", Version = PythonLanguageVersion.V27 },
                new { Method = "rshift", Operator = ">>", Version = PythonLanguageVersion.V27 },
                new { Method = "pow", Operator = "**", Version = PythonLanguageVersion.V27 },
                new { Method = "floordiv", Operator = "//", Version = PythonLanguageVersion.V27 },
                new { Method = "matmul", Operator = "@", Version = PythonLanguageVersion.V35 },
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
m = C()
m {1}= m
";

            foreach (var test in operators) {
                Console.WriteLine(test.Operator);
                var entry = ProcessText(String.Format(text, test.Method, test.Operator), test.Version);

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
                // We assume that augmented assignments keep their type
                AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("m", text.IndexOf("m " + test.Operator)), "C instance");
            }
        }

        [TestMethod, Priority(0)]
        public void SequenceConcat() {
            var text = @"
x1 = ()
y1 = x1 + ()
y1v = y1[0]

x2 = (1,2,3)
y2 = x2 + (4.0,5.0,6.0)
y2v = y2[0]

x3 = [1,2,3]
y3 = x3 + [4.0,5.0,6.0]
y3v = y3[0]
";

            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x1", 1), BuiltinTypeId.Tuple);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y1", 1), BuiltinTypeId.Tuple);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y1v", 1));
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y2", 1), BuiltinTypeId.Tuple);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y2v", 1), BuiltinTypeId.Int, BuiltinTypeId.Float);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y3", 1), BuiltinTypeId.List);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y3v", 1), BuiltinTypeId.Int, BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public void SequenceMultiply() {
            var text = @"
x = ()
y = x * 100

x1 = (1,2,3)
y1 = x1 * 100

fob = [1,2,3]
oar = fob * 100

fob2 = []
oar2 = fob2 * 100";

            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("y", text.IndexOf("y =")), "tuple");
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("y1", text.IndexOf("y1 =")), "tuple");
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("oar", text.IndexOf("oar =")), "list");
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("oar2", text.IndexOf("oar2 =")), "list");

            text = @"
x = ()
y = 100 * x

x1 = (1,2,3)
y1 = 100 * x1

fob = [1,2,3]
oar = 100 * fob 

fob2 = []
oar2 = 100 * fob2";

            entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("y", text.IndexOf("y =")), "tuple");
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("y1", text.IndexOf("y1 =")), "tuple");
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("oar", text.IndexOf("oar =")), "list");
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("oar2", text.IndexOf("oar2 =")), "list");
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
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("t1", text.IndexOf("t1 =")), BuiltinTypeId.Bool);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("t2", text.IndexOf("t1 =")), BuiltinTypeId.Bool);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("l1", text.IndexOf("t1 =")), BuiltinTypeId.Bool);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("l2", text.IndexOf("t1 =")), BuiltinTypeId.Bool);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("s1", text.IndexOf("t1 =")), BuiltinTypeId.Bool);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("s2", text.IndexOf("t1 =")), BuiltinTypeId.Bool);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("d1", text.IndexOf("t1 =")), BuiltinTypeId.Bool);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("d2", text.IndexOf("t1 =")), BuiltinTypeId.Bool);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("r1", text.IndexOf("t1 =")), BuiltinTypeId.Bool);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("r2", text.IndexOf("t1 =")), BuiltinTypeId.Bool);

        }

        [TestMethod, Priority(0)]
        public void DescriptorNoDescriptor() {
            var text = @"
class NoDescriptor:   
       def nodesc_method(self): pass

 class SomeClass:
    fob = NoDescriptor()

    def f(self):
        self.fob
        pass
";

            var entry = ProcessText(text);
            AssertUtil.Contains(entry.GetMemberNamesByIndex("self.fob", text.IndexOf("self.fob")), "nodesc_method");
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
    def __init__(self, fob):
        self.abc = fob
        del self.abc
        print self.abc

";
            var entry = ProcessText(text);
            VerifyReferences(entry.GetVariablesByIndex("self.abc", text.IndexOf("self.abc")),
                new VariableLocation(9, 14, VariableType.Definition),
                new VariableLocation(10, 18, VariableType.Reference),
                new VariableLocation(11, 20, VariableType.Reference));
            VerifyReferences(entry.GetVariablesByIndex("fob", text.IndexOf("= fob")),
                new VariableLocation(8, 24, VariableType.Definition),
                new VariableLocation(9, 20, VariableType.Reference));
        }

        [TestMethod, Priority(0)]
        public void References() {
            // instance variables
            var text = @"
# add ref w/o type info
class C(object):
    def __init__(self, fob):
        self.abc = fob
        del self.abc
        print self.abc

";
            var entry = ProcessText(text);
            VerifyReferences(entry.GetVariablesByIndex("self.abc", text.IndexOf("self.abc")),
                new VariableLocation(5, 14, VariableType.Definition),
                new VariableLocation(6, 18, VariableType.Reference),
                new VariableLocation(7, 20, VariableType.Reference));
            VerifyReferences(entry.GetVariablesByIndex("fob", text.IndexOf("= fob")),
                new VariableLocation(4, 24, VariableType.Definition),
                new VariableLocation(5, 20, VariableType.Reference));

            text = @"
# add ref w/ type info
class D(object):
    def __init__(self, fob):
        self.abc = fob
        del self.abc
        print self.abc

D(42)";
            entry = ProcessText(text);

            VerifyReferences(entry.GetVariablesByIndex("self.abc", text.IndexOf("self.abc")),
                new VariableLocation(5, 14, VariableType.Definition),
                new VariableLocation(6, 18, VariableType.Reference),
                new VariableLocation(7, 20, VariableType.Reference));
            VerifyReferences(entry.GetVariablesByIndex("fob", text.IndexOf("= fob")),
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

            // named arguments
            text = @"
def f(abc):
    print abc

f(abc = 123)
";
            entry = ProcessText(text);
            VerifyReferences(entry.GetVariablesByIndex("abc", text.IndexOf("abc")),
                new VariableLocation(2, 7, VariableType.Definition),
                new VariableLocation(3, 11, VariableType.Reference),
                new VariableLocation(5, 3, VariableType.Reference));

            // grammar test - statements
            text = @"
def f(abc):
    try: pass
    except abc: pass

    try: pass
    except TypeError, abc: pass    

    abc, oar = 42, 23
    abc[23] = 42
    abc.fob = 42
    abc += 2

    class D(abc): pass

    for x in abc: print x

    import abc
    from xyz import abc
    from xyz import oar as abc

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

    for x in fob: 
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
    x = abc.fob
    
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
                            have.Type == expected.Type &&
                            (expected.FilePath == null || have.Location.FilePath == expected.FilePath)) {
                            vars.RemoveAt(i);
                            removed++;
                            removedOne = found = true;
                            i--;
                        }
                    }

                    if (!found) {
                        var error = new StringBuilder();
                        error.AppendFormat("Failed to find VariableLocation({0}, {1}, VariableType.{2}", expected.StartLine, expected.StartCol, expected.Type);
                        if (expected.FilePath != null) {
                            error.AppendFormat(", \"{0}\"", expected.FilePath);
                        }
                        error.AppendLine(") in");
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
            using (var state = PythonAnalyzer.CreateSynchronously(InterpreterFactory, Interpreter)) {

                var fobText = @"
from oar import abc

abc()
";
                var oarText = "class abc(object): pass";

                var fobMod = state.AddModule("fob", "fob", null);
                Prepare(fobMod, GetSourceUnit(fobText, "mod1"));
                var oarMod = state.AddModule("oar", "oar", null);
                Prepare(oarMod, GetSourceUnit(oarText, "mod2"));

                fobMod.Analyze(CancellationToken.None);
                oarMod.Analyze(CancellationToken.None);

                VerifyReferences(UniqifyVariables(oarMod.Analysis.GetVariablesByIndex("abc", oarText.IndexOf("abc"))),
                    new VariableLocation(1, 7, VariableType.Definition, "oar"),     // definition 
                    new VariableLocation(2, 17, VariableType.Reference, "fob"),     // import
                    new VariableLocation(4, 1, VariableType.Reference, "fob")       // call
                );
            }
        }

        [TestMethod, Priority(0)]
        public void ReferencesCrossMultiModule() {
            using (var state = PythonAnalyzer.CreateSynchronously(InterpreterFactory, Interpreter)) {

                var fobText = @"
from oarbaz import abc

abc()
";
                var oarText = "class abc1(object): pass";
                var bazText = "class abc2(object): pass";
                var oarBazText = @"from oar import abc1 as abc
from baz import abc2 as abc";

                var fobMod = state.AddModule("fob", "fob", null);
                Prepare(fobMod, GetSourceUnit(fobText, "mod1"));
                var oarMod = state.AddModule("oar", "oar", null);
                Prepare(oarMod, GetSourceUnit(oarText, "mod2"));
                var bazMod = state.AddModule("baz", "baz", null);
                Prepare(bazMod, GetSourceUnit(bazText, "mod3"));
                var oarBazMod = state.AddModule("oarbaz", "oarbaz", null);
                Prepare(oarBazMod, GetSourceUnit(oarBazText, "mod4"));

                fobMod.Analyze(CancellationToken.None, true);
                oarMod.Analyze(CancellationToken.None, true);
                bazMod.Analyze(CancellationToken.None, true);
                oarBazMod.Analyze(CancellationToken.None, true);
                fobMod.AnalysisGroup.AnalyzeQueuedEntries(CancellationToken.None);
                oarMod.AnalysisGroup.AnalyzeQueuedEntries(CancellationToken.None);
                bazMod.AnalysisGroup.AnalyzeQueuedEntries(CancellationToken.None);
                oarBazMod.AnalysisGroup.AnalyzeQueuedEntries(CancellationToken.None);

                VerifyReferences(UniqifyVariables(oarMod.Analysis.GetVariablesByIndex("abc1", oarText.IndexOf("abc1"))),
                    new VariableLocation(1, 7, VariableType.Definition),
                    new VariableLocation(1, 25, VariableType.Reference)
                );
                VerifyReferences(UniqifyVariables(bazMod.Analysis.GetVariablesByIndex("abc2", bazText.IndexOf("abc2"))),
                    new VariableLocation(1, 7, VariableType.Definition),
                    new VariableLocation(2, 25, VariableType.Reference)
                );
                VerifyReferences(UniqifyVariables(fobMod.Analysis.GetVariablesByIndex("abc", 0)),
                    new VariableLocation(1, 7, VariableType.Value),         // possible value
                    //new VariableLocation(1, 7, VariableType.Value),       // appears twice for two modules, but cannot test that
                    new VariableLocation(2, 20, VariableType.Definition),   // import
                    new VariableLocation(4, 1, VariableType.Reference),     // call
                    new VariableLocation(1, 25, VariableType.Definition),   // import in oar
                    new VariableLocation(2, 25, VariableType.Definition)    // import in baz
                );
            }
        }

        private static void LocationNames(List<IAnalysisVariable> vars, StringBuilder error) {
            bool careAboutNames = (vars.Select(v => v.Location.FilePath).Distinct().Count() > 1);
            foreach (var var in vars) { //.OrderBy(v => v.Location.Line).ThenBy(v => v.Location.Column)) {
                if (careAboutNames) {
                    error.AppendFormat("   new VariableLocation({0}, {1}, VariableType.{2}, \"{3}\"),", var.Location.Line, var.Location.Column, var.Type, var.Location.FilePath);
                } else {
                    error.AppendFormat("   new VariableLocation({0}, {1}, VariableType.{2}),", var.Location.Line, var.Location.Column, var.Type);
                }
                error.AppendLine();
            }
        }

        [TestMethod, Priority(0)]
        public void ReferencesGenerators() {
            var text = @"
[f for f in x]
[x for x in f]
(g for g in y)
(y for y in g)
";
            var entry = ProcessText(text, PythonLanguageVersion.V33);
            VerifyReferences(entry.GetVariablesByIndex("f", text.IndexOf("f for")),
                new VariableLocation(2, 2, VariableType.Reference),
                new VariableLocation(2, 8, VariableType.Definition)
            );
            VerifyReferences(entry.GetVariablesByIndex("x", text.IndexOf("x for")),
                new VariableLocation(3, 2, VariableType.Reference),
                new VariableLocation(3, 8, VariableType.Definition)
            );
            VerifyReferences(entry.GetVariablesByIndex("g", text.IndexOf("g for")),
                new VariableLocation(4, 2, VariableType.Reference),
                new VariableLocation(4, 8, VariableType.Definition)
            );
            VerifyReferences(entry.GetVariablesByIndex("y", text.IndexOf("y for")),
                new VariableLocation(5, 2, VariableType.Reference),
                new VariableLocation(5, 8, VariableType.Definition)
            );

            entry = ProcessText(text, PythonLanguageVersion.V27);
            // Index variable leaks out of list comprehension
            VerifyReferences(entry.GetVariablesByIndex("f", text.IndexOf("f for")),
                new VariableLocation(2, 2, VariableType.Reference),
                new VariableLocation(2, 8, VariableType.Definition),
                new VariableLocation(3, 13, VariableType.Reference)
            );
            VerifyReferences(entry.GetVariablesByIndex("x", text.IndexOf("x for")),
                new VariableLocation(3, 2, VariableType.Reference),
                new VariableLocation(3, 8, VariableType.Definition)
            );
            VerifyReferences(entry.GetVariablesByIndex("g", text.IndexOf("g for")),
                new VariableLocation(4, 2, VariableType.Reference),
                new VariableLocation(4, 8, VariableType.Definition)
            );
            VerifyReferences(entry.GetVariablesByIndex("y", text.IndexOf("y for")),
                new VariableLocation(5, 2, VariableType.Reference),
                new VariableLocation(5, 8, VariableType.Definition)
            );
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
                new { FuncName = "f", ParamName="x", DefaultValue = "None" },
                new { FuncName = "g", ParamName="x", DefaultValue = "{}" },
                new { FuncName = "h", ParamName="x", DefaultValue = "{...}" },
                new { FuncName = "i", ParamName="x", DefaultValue = "[]" },
                new { FuncName = "j", ParamName="x", DefaultValue="[...]" },
                new { FuncName = "k", ParamName="x", DefaultValue = "()" },
                new { FuncName = "l", ParamName="x", DefaultValue = "(...)" },
                new { FuncName = "m", ParamName="x", DefaultValue = "math.atan2(1,0)" },
            };

            foreach (var test in tests) {
                var result = entry.GetSignaturesByIndex(test.FuncName, 1).ToArray();
                Assert.AreEqual(result.Length, 1);
                Assert.AreEqual(result[0].Parameters.Length, 1);
                Assert.AreEqual(result[0].Parameters[0].Name, test.ParamName);
                Assert.AreEqual(result[0].Parameters[0].DefaultValue, test.DefaultValue);
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
for fob in abc:
    print(fob)";

                var entry = ProcessText(code);
                var values = entry.GetValuesByIndex("fob", code.IndexOf("print(fob)")).ToArray();
                Assert.AreEqual(1, values.Length);
                Assert.AreEqual(BuiltinTypeId.Int, values[0].PythonType.TypeId);
            }

            // dict methods which return a key or value
            foreach (var method in new[] { "x.get(42)", "x.pop()" }) {
                Debug.WriteLine(method);

                var code = @"x = {}
abc = " + method + @"
def f(z):
    z[42] = 100

f(x)
fob = abc";

                var entry = ProcessText(code);
                var values = entry.GetValuesByIndex("fob", code.IndexOf("fob =")).ToArray();
                Assert.AreEqual(1, values.Length);
                Assert.AreEqual(BuiltinTypeId.Int, values[0].PythonType.TypeId);
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
fob = abc";

                var entry = ProcessText(code);
                var values = entry.GetValuesByIndex("fob", code.IndexOf("fob =")).ToArray();
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
for fob in abc:
    print(fob)";

                var entry = ProcessText(code);
                var values = entry.GetValuesByIndex("fob", code.IndexOf("print(fob)")).ToArray();
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
                using (var state = PythonAnalyzer.CreateSynchronously(InterpreterFactory, Interpreter)) {
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
        }

        [TestMethod, Priority(0)]
        public void SpecialListMethodsCrossUnitAnalysis() {
            var code = @"x = []
def f(z):
    z.append(100)
    
f(x)
for fob in x:
    print(fob)


oar = x.pop()
";

            var entry = ProcessText(code);
            var values = entry.GetValuesByIndex("fob", code.IndexOf("print(fob)")).ToArray();
            Assert.AreEqual(1, values.Length);
            Assert.AreEqual(BuiltinTypeId.Int, values[0].PythonType.TypeId);

            values = entry.GetValuesByIndex("oar", code.IndexOf("oar = ")).ToArray();
            Assert.AreEqual(1, values.Length);
            Assert.AreEqual(BuiltinTypeId.Int, values[0].PythonType.TypeId);
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
        public void SetOperators() {
            var entry = ProcessText(@"
x = {1, 2, 3}
y = {3.14, 2.718}

x_or_y = x | y
x_and_y = x & y
x_sub_y = x - y
x_xor_y = x ^ y

y_or_x = y | x
y_and_x = y & x
y_sub_x = y - x
y_xor_x = y ^ x

x_or_y_0 = next(iter(x_or_y))
x_and_y_0 = next(iter(x_and_y))
x_sub_y_0 = next(iter(x_sub_y))
x_xor_y_0 = next(iter(x_xor_y))

y_or_x_0 = next(iter(y_or_x))
y_and_x_0 = next(iter(y_and_x))
y_sub_x_0 = next(iter(y_sub_x))
y_xor_x_0 = next(iter(y_xor_x))
");
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", 0), BuiltinTypeId.Set);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", 0), BuiltinTypeId.Set);
            foreach (var op in new[] { "or", "and", "sub", "xor" }) {
                AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x_" + op + "_y", 0), BuiltinTypeId.Set);
                AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y_" + op + "_x", 0), BuiltinTypeId.Set);

                if (op == "or") {
                    AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x_" + op + "_y_0", 0), BuiltinTypeId.Int, BuiltinTypeId.Float);
                    AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y_" + op + "_x_0", 0), BuiltinTypeId.Int, BuiltinTypeId.Float);
                } else {
                    AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x_" + op + "_y_0", 0), BuiltinTypeId.Int);
                    AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y_" + op + "_x_0", 0), BuiltinTypeId.Float);
                }
            }

        }

        [TestMethod, Priority(0)]
        public void GetVariablesDictionaryGet() {
            var entry = ProcessText(@"x = {42:'abc'}");

            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("x.get", 0), "bound built-in method get");
        }

        [TestMethod, Priority(0)]
        public void DictMethods() {
            var entry = ProcessText(@"
x = {42:'abc'}
            ", PythonLanguageVersion.V27);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x.items()[0][0]", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x.items()[0][1]", 1), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x.keys()[0]", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x.values()[0]", 1), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x.pop(1)", 1), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x.popitem()[0]", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x.popitem()[1]", 1), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x.iterkeys().next()", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x.itervalues().next()", 1), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x.iteritems().next()[0]", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x.iteritems().next()[1]", 1), BuiltinTypeId_Str);
        }

        [TestMethod, Priority(0)]
        public void DictUpdate() {
            var entry = ProcessText(@"
a = {42:100}
b = {}
b.update(a)
");

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b.items()[0][0]", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b.items()[0][1]", 1), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void DictEnum() {
            var entry = ProcessText(@"
for x in {42:'abc'}:
    print(x)
");

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", 1), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void FutureDivision() {
            var entry = ProcessText(@"
from __future__ import division
x = 1/2
            ");

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", 1), BuiltinTypeId.Float);
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

            entry = ProcessText(@"
class C(object):
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

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", 1), BuiltinTypeId.Int);

            entry = ProcessText(@"
def f(a):
    return a

x = lambda b: f(b)
y = x(42)
");

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", 1), BuiltinTypeId.Int);

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
            AssertUtil.ContainsExactly(sigs.Select(s => s.Documentation), null, "The most base type");
        }

        [TestMethod, Priority(0)]
        public void BadMethod() {
            var entry = ProcessText(@"
class cls(object): 
    def f(): 
        'help'
        return 42

abc = cls()
fob = abc.f()
");

            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("fob", 1), _intMembers);
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

                    AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", text.IndexOf("pass")), BuiltinTypeId.Complex);
                    AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", text.IndexOf("pass")), BuiltinTypeId.Int);
                    AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("c", text.IndexOf("pass")), BuiltinTypeId_Str);
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

        [TestMethod, Priority(0)]
        public void PositionalSplat() {
            var funcDef = @"def f(a, b, c, *d): 
    pass";
            var classWithInit = @"class f(object):
    def __init__(self, a, b, c, *d):
        pass";
            var classWithNew = @"class f(object):
    def __new__(cls, a, b, c, *d):
        pass";
            var method = @"class x(object):
    def g(self, a, b, c, *d):
        pass

f = x().g";
            var decls = new[] { funcDef, classWithInit, classWithNew, method };

            foreach (var decl in decls) {
                string[] testCalls = new[] { 
                    "f(*(3j, 42, 'abc'))", 
                    "f(*[3j, 42, 'abc'])", 
                    "f(*(3j, 42, 'abc', 4L))",  // extra argument
                    "f(*[3j, 42, 'abc', 4L])",  // extra argument
                    "f(3j, *(42, 'abc'))",
                    "f(3j, 42, *('abc',))",
                    "f(3j, *(42, 'abc', 4L))",
                    "f(3j, 42, *('abc', 4L))",
                    "f(3j, 42, 'abc', *[4L])",
                    "f(3j, 42, 'abc', 4L)"
                };

                foreach (var testCall in testCalls) {
                    var text = decl + Environment.NewLine + testCall;
                    Console.WriteLine(testCall);
                    var entry = ProcessText(text);

                    AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", text.IndexOf("pass")), BuiltinTypeId.Complex);
                    AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", text.IndexOf("pass")), BuiltinTypeId.Int);
                    AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("c", text.IndexOf("pass")), BuiltinTypeId_Str);
                    AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("d", text.IndexOf("pass")), BuiltinTypeId.Tuple);
                    if (testCall.Contains("4L")) {
                        AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("d[0]", text.IndexOf("pass")), BuiltinTypeId.Long);
                    }
                }
            }
        }

        [TestMethod, Priority(0)]
        public void KeywordSplat() {
            var funcDef = @"def f(a, b, c, **d): 
    pass";
            var classWithInit = @"class f(object):
    def __init__(self, a, b, c, **d):
        pass";
            var classWithNew = @"class f(object):
    def __new__(cls, a, b, c, **d):
        pass";
            var method = @"class x(object):
    def g(self, a, b, c, **d):
        pass

f = x().g";
            var decls = new[] { funcDef, classWithInit, classWithNew, method };

            foreach (var decl in decls) {
                string[] testCalls = new[] { 
                    "f(**{'a': 3j, 'b': 42, 'c': 'abc'})", 
                    "f(**{'c': 'abc', 'b': 42, 'a': 3j})", 
                    "f(**{'a': 3j, 'b': 42, 'c': 'abc', 'x': 4L})",  // extra argument
                    "f(3j, **{'b': 42, 'c': 'abc'})",
                    "f(3j, 42, **{'c': 'abc'})"
                };

                foreach (var testCall in testCalls) {
                    var text = decl + Environment.NewLine + testCall;
                    Console.WriteLine(testCall);
                    var entry = ProcessText(text);

                    AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", text.IndexOf("pass")), BuiltinTypeId.Complex);
                    AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", text.IndexOf("pass")), BuiltinTypeId.Int);
                    AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("c", text.IndexOf("pass")), BuiltinTypeId_Str);
                    AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("d", text.IndexOf("pass")), BuiltinTypeId.Dict);
                    if (testCall.Contains("4L")) {
                        AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("d['x']", text.IndexOf("pass")), BuiltinTypeId.Long);
                    }
                }
            }
        }

        [TestMethod, Priority(0)]
        public void ForwardRef() {
            var text = @"

class D(object):
    def oar(self, x):
        abc = C()
        abc.fob(2)
        a = abc.fob(2.0)
        a.oar(('a', 'b', 'c', 'd'))

class C(object):
    def fob(self, x):
        D().oar('abc')
        D().oar(['a', 'b', 'c'])
        return D()
    def baz(self): pass
";
            var entry = ProcessText(text);

            var fifty = entry.GetVariablesNoBuiltinsByIndex(text.IndexOf("abc.fob")).ToSet();
            AssertUtil.ContainsExactly(fifty, "C", "D", "a", "abc", "self", "x");

            var three = entry.GetVariablesNoBuiltinsByIndex(text.IndexOf("def oar") + 1).ToSet();
            AssertUtil.ContainsExactly(three, "C", "D", "oar");

            var allFifty = entry.GetMemberNamesByIndex("abc", text.IndexOf("abc.fob")).ToSet();
            AssertUtil.ContainsExactly(allFifty, GetUnion(_objectMembers, "baz", "fob"));

            var xTypes = entry.GetTypeIdsByIndex("x", text.IndexOf("abc.fob")).ToSet();
            AssertUtil.ContainsExactly(xTypes, BuiltinTypeId.List, BuiltinTypeId_Str, BuiltinTypeId.Tuple);

            var xMembers = entry.GetMemberNamesByIndex("x", text.IndexOf("abc.fob")).ToSet();
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
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("booltypetrue", 1), BuiltinTypeId.Bool);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("booltypefalse", 1), BuiltinTypeId.Bool);
        }

        [TestMethod, Priority(0)]
        public void DictionaryFunctionTable() {
            var text = @"
def f(a, b):
    print a, b
    
def g(a, b):
    x, y = a, b

x = {'fob': f, 'oar' : g}
x['fob'](42, [])
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", text.IndexOf("print")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", text.IndexOf("print")), BuiltinTypeId.List);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", text.IndexOf("x, y")));
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", text.IndexOf("x, y")));
        }

        [TestMethod, Priority(0)]
        public void DictionaryAssign() {
            var text = @"
x = {'abc': 42}
y = x['fob']
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", 1), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void DictionaryFunctionTableGet2() {
            var text = @"
def f(a, b):
    print a, b
    
def g(a, b):
    x, y = a, b

x = {'fob': f, 'oar' : g}
x.get('fob')(42, [])
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", text.IndexOf("print")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", text.IndexOf("print")), BuiltinTypeId.List);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", text.IndexOf("x, y")));
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", text.IndexOf("x, y")));
        }

        [TestMethod, Priority(0)]
        public void DictionaryFunctionTableGet() {
            var text = @"
def f(a, b):
    print a, b
    
def g(a, b):
    x, y = a, b

x = {'fob': f, 'oar' : g}
y = x.get('fob', None)
if y is not None:
    y(42, [])
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", text.IndexOf("print")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", text.IndexOf("print")), BuiltinTypeId.List);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", text.IndexOf("x, y")));
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", text.IndexOf("x, y")));
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
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", text.IndexOf("print")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", text.IndexOf("print")), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("c", text.IndexOf("print")), BuiltinTypeId.List);
        }

        [TestMethod, Priority(0)]
        public void WithStatement() {
            var text = @"
class X(object):
    def x_method(self): pass
    def __enter__(self): return self
    def __exit__(self, exc_type, exc_value, traceback): return False
       
class Y(object):
    def y_method(self): pass
    def __enter__(self): return 123
    def __exit__(self, exc_type, exc_value, traceback): return False
 
with X() as x:
    pass #x

with Y() as y:
    pass #y
    
with X():
    pass
";
            var entry = ProcessText(text);
            AssertUtil.ContainsAtLeast(entry.GetMemberNamesByIndex("x", text.IndexOf("pass #x")), "x_method");
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", text.IndexOf("pass #y")), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void OverrideFunction() {
            var text = @"
class oar(object):
    def Call(self, xvar, yvar):
        pass

class baz(oar):
    def Call(self, xvar, yvar):
        x = 42
        pass

class Cxxxx(object):
    def __init__(self):
        self.fob = baz()
        
    def Cmeth(self, avar, bvar):
        self.fob.Call(avar, bvar)
        


abc = Cxxxx()
abc.Cmeth(['fob'], 'oar')
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("xvar", text.IndexOf("x = 42")), BuiltinTypeId.List);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("xvar", text.IndexOf("pass")));
        }


        [TestMethod, Priority(0)]
        public void FunctionOverloads() {
            var text = @"
def f(a, b, c=0):
    pass

f(1, 1, 1)
f(3.14, 3.14, 3.14)
f('a', 'b', 'c')
f(1, 3.14, 'c')
f('a', 'b', 1)
";
            var entry = ProcessText(text);
            var f = entry.GetSignaturesByIndex("f", 0).Select(sig => {
                return string.Format(
                    "{0}({1})",
                    sig.Name,
                    string.Join(
                        ", ",
                        sig.Parameters
                        .Select(
                            p => {
                                if (String.IsNullOrWhiteSpace(p.DefaultValue)) {
                                    return string.Format("{0} := ({1})", p.Name, p.Type);
                                } else {
                                    return string.Format("{0} = {1} := ({2})", p.Name, p.DefaultValue, p.Type);
                                }
                            }
                        )
                    )
                );
            }).ToList();
            foreach (var sig in f) {
                Console.WriteLine(sig);
            }
            Assert.AreEqual(6, f.Count);
            AssertUtil.ContainsExactly(f,
                "f(a := (), b := (), c = 0 := (int))",
                "f(a := (int), b := (int), c = 0 := (int))",
                "f(a := (float), b := (float), c = 0 := (float, int))",
                "f(a := (str), b := (str), c = 0 := (int, str))",
                "f(a := (int), b := (float), c = 0 := (int, str))",
                "f(a := (str), b := (str), c = 0 := (int))"
            );
        }

        internal static readonly Regex ValidParameterName = new Regex(@"^(\*|\*\*)?[a-z_][a-z0-9_]*( *=.+)?", RegexOptions.IgnoreCase);
        internal static string GetSafeParameterName(ParameterResult result) {
            var match = ValidParameterName.Match(result.Name);

            return match.Success ? match.Value : result.Name;
        }


        protected virtual string ListInitParameterName {
            get { return "sequence"; }
        }

        /// <summary>
        /// http://pytools.codeplex.com/workitem/799
        /// </summary>
        [TestMethod, Priority(0)]
        public void OverrideCompletions() {
            var text = @"
class oar(list):
    pass
";
            var entry = ProcessText(text);
            var init = entry.GetOverrideableByIndex(text.IndexOf("pass")).Single(r => r.Name == "__init__");
            AssertUtil.AreEqual(init.Parameters.Select(GetSafeParameterName), "self", ListInitParameterName);

            // Ensure that nested classes are correctly resolved.
            text = @"
class oar(int):
    class fob(dict):
        pass
";
            entry = ProcessText(text);
            var oarItems = entry.GetOverrideableByIndex(text.IndexOf("    pass")).Select(x => x.Name).ToSet();
            var fobItems = entry.GetOverrideableByIndex(text.IndexOf("pass")).Select(x => x.Name).ToSet();
            AssertUtil.DoesntContain(oarItems, "keys");
            AssertUtil.DoesntContain(oarItems, "items");
            AssertUtil.ContainsAtLeast(oarItems, "bit_length");

            AssertUtil.ContainsAtLeast(fobItems, "keys", "items");
            AssertUtil.DoesntContain(fobItems, "bit_length");
        }


        [TestMethod, Priority(0)]
        public void SimpleMethodCall() {
            var text = @"
class x(object):
    def abc(self, fob):
        pass
        
a = x()
a.abc('abc')
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("fob", text.IndexOf("pass")), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("self", text.IndexOf("pass")), GetUnion(_objectMembers, "abc"));
        }

        [TestMethod, Priority(0)]
        public void BuiltinRetval() {
            var text = @"
x = [2,3,4]
a = x.index(2)
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", text.IndexOf("x =")).ToSet(), BuiltinTypeId.List);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", text.IndexOf("a =")).ToSet(), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void BuiltinFuncRetval() {
            var text = @"
x = ord('a')
y = range(5)
";

            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", text.IndexOf("x = ")).ToSet(), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", text.IndexOf("y = ")).ToSet(), BuiltinTypeId.List);
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
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("i", text.IndexOf("for i")).ToSet(), BuiltinTypeId.Int);
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
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", 1), BuiltinTypeId_Str);
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
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", text.IndexOf("x =")), BuiltinTypeId.List, BuiltinTypeId_Str);
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
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", text.IndexOf("x =")), BuiltinTypeId.List, BuiltinTypeId_Str);
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
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("it", text.IndexOf("it =")), BuiltinTypeId.Generator);
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
            Assert.AreEqual(4, values.Count);
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
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", 1), BuiltinTypeId.List);
        }

        [TestMethod, Priority(0)]
        public void ReturnArg() {
            var text = @"
def g(a):
    return a

x = g(1)
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", 1), BuiltinTypeId.Int);
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
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", 1), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void MemberAssign() {
            var text = @"
class C:
    def func(self):
        self.abc = 42

a = C()
a.func()
fob = a.abc
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("fob", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("fob", 1), _intMembers);
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

fob = D().func2()
";
            var entry = ProcessText(text);
            // TODO: AssertUtil.ContainsExactly(entry.GetTypesFromName("fob", 0), ListType);
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
                AssertUtil.ContainsExactly(pe[1].Analysis.GetTypeIdsByIndex("x", text2.IndexOf("return x")), BuiltinTypeId_Str);
                AssertUtil.ContainsExactly(pe[0].Analysis.GetTypeIdsByIndex("y", text1.IndexOf("y")), BuiltinTypeId_Str);
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
                AssertUtil.ContainsExactly(pe[1].Analysis.GetTypeIdsByIndex("x", text2.IndexOf("= x")), BuiltinTypeId_Str);
                AssertUtil.ContainsExactly(pe[0].Analysis.GetTypeIdsByIndex("y", text1.IndexOf("y")), BuiltinTypeId_Str);
            });
        }

        [TestMethod, Priority(0)]
        public void CrossModuleCallType2() {
            var text1 = @"
from mod2 import c
class x(object):
    def Fob(self):
        y = c('abc').x
";
            var text2 = @"
class c:
    def __init__(self, x):
        self.x = x
";

            PermutedTest("mod", new[] { text1, text2 }, (pe) => {
                AssertUtil.ContainsExactly(pe[1].Analysis.GetTypeIdsByIndex("x", text2.IndexOf("= x")), BuiltinTypeId_Str);
                AssertUtil.ContainsExactly(pe[0].Analysis.GetTypeIdsByIndex("y", text1.IndexOf("y =")), BuiltinTypeId_Str);
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
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", text.IndexOf("a =")), BuiltinTypeId.Int);
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
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", text.IndexOf("a = ")), BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public void InheritedStaticMethod() {
            var text = @"
class x(object):
    @staticmethod
    def StaticMethod(value):
        return value

class y(x):
    pass

a = y().StaticMethod(4.0)
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", text.IndexOf("a = ")), BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public void ClassMethod() {
            var text = @"
class x(object):
    @classmethod
    def ClassMethod(cls):
        return cls

a = x().ClassMethod()
b = x.ClassMethod()
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("a", text.IndexOf("a =")), "x");
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("b", text.IndexOf("b =")), "x");
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("cls", text.IndexOf("return")), "x");

            var exprs = new[] { "x.ClassMethod", "x().ClassMethod" };
            foreach (var expr in exprs) {
                var sigs = entry.GetSignaturesByIndex(expr, text.IndexOf("a = ")).ToArray();
                Assert.AreEqual(1, sigs.Length);
                Assert.AreEqual(sigs[0].Parameters.Length, 0); // cls is implicitly implied
            }

            text = @"
class x(object):
    @classmethod
    def UncalledClassMethod(cls):
        return cls
";
            entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("cls", text.IndexOf("return")), "x");
        }

        [TestMethod, Priority(0)]
        public void InheritedClassMethod() {
            var text = @"
class x(object):
    @classmethod
    def ClassMethod(cls):
        return cls

class y(x):
    pass

a = y().ClassMethod()
b = y.ClassMethod()
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("a", text.IndexOf("a =")), "y");
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("b", text.IndexOf("b =")), "y");
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("cls", text.IndexOf("return")), "x", "y");

            var exprs = new[] { "y.ClassMethod", "y().ClassMethod" };
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

fob = C.x
oar = C().x
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("fob", text.IndexOf("fob = ")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("oar", text.IndexOf("oar = ")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("C.x", text.IndexOf("oar = ")), BuiltinTypeId.Int);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("ctx", text.IndexOf("return 42")), BuiltinTypeId.Type);
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("inst", text.IndexOf("return 42")), "None", "C instance");

            text = @"
class mydesc(object):
    def __get__(self, inst, ctx):
        return 42

class C(object):
    x = mydesc()
    def instfunc(self):
        pass

oar = C().x
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
            using (var state = PythonAnalyzer.CreateSynchronously(InterpreterFactory, Interpreter)) {

                var oar = state.AddModule("oar", @"oar.py", EmptyAnalysisCookie.Instance);
                var baz = state.AddModule("baz", @"baz.py", EmptyAnalysisCookie.Instance);

                AnalyzeLeak(() => {
                    var oarSrc = GetSourceUnit(@"
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

", @"oar.py");

                    var bazSrc = GetSourceUnit(@"
from oar import C

class D(object):
    def g(self, a):
        pass

a = D()
a.f(C())
z = C().f(42)

min(a, D())
", @"baz.py");


                    Prepare(oar, oarSrc);
                    Prepare(baz, bazSrc);

                    oar.Analyze(CancellationToken.None);
                    baz.Analyze(CancellationToken.None);
                });
            }
        }

        //[TestMethod, Timeout(15 * 60 * 1000), Priority(2)]
        public void MemLeak2() {
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
            using (var projectState = PythonAnalyzer.CreateSynchronously(InterpreterFactory, Interpreter)) {
                var modules = new List<IPythonProjectEntry>();
                foreach (var sourceUnit in sourceUnits) {
                    modules.Add(projectState.AddModule(ModulePath.FromFullPath(sourceUnit.Path).ModuleName, sourceUnit.Path, null));
                }
                long start1 = sw.ElapsedMilliseconds;
                Trace.TraceInformation("AddSourceUnit: {0} ms", start1 - start0);

                var nodes = new List<Microsoft.PythonTools.Parsing.Ast.PythonAst>();
                for (int i = 0; i < modules.Count; i++) {
                    PythonAst ast = null;
                    try {
                        var sourceUnit = sourceUnits[i];

                        ast = Parser.CreateParser(sourceUnit, InterpreterFactory.GetLanguageVersion()).ParseFile();
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
                        var ast = Parser.CreateParser(reader, InterpreterFactory.GetLanguageVersion()).ParseFile();

                        modules[index].UpdateTree(ast, null);
                    }

                    modules[index].Analyze(CancellationToken.None, true);
                    modules[index].AnalysisGroup.AnalyzeQueuedEntries(CancellationToken.None);
                });
            }
        }

        [TestMethod, Priority(1)]
        public void CancelAnalysis() {
            var ver = PythonPaths.Versions.LastOrDefault(v => v != null);
            if (ver == null) {
                Assert.Inconclusive("Test requires Python installation");
            }

            var cancelSource = new CancellationTokenSource();
            var task = Task.Run(() => {
                new AnalysisTest().AnalyzeDir(ver.LibPath, ver.Version, cancel: cancelSource.Token);
            });

            // Allow 10 seconds for parsing to complete and analysis to start
            cancelSource.CancelAfter(TimeSpan.FromSeconds(10));

            if (!task.Wait(TimeSpan.FromSeconds(15))) {
                task.Dispose();
                Assert.Fail("Analysis did not abort within 5 seconds");
            }
        }

        [TestMethod, Priority(0)]
        public void MoveClass() {
            var fobSrc = GetSourceUnit("from oar import C", @"fob.py");

            var oarSrc = GetSourceUnit(@"
class C(object):
    pass
", @"oar.py");

            var bazSrc = GetSourceUnit(@"
class C(object):
    pass
", @"baz.py");

            using (var state = PythonAnalyzer.CreateSynchronously(InterpreterFactory, Interpreter)) {

                var fob = state.AddModule("fob", @"fob.py", EmptyAnalysisCookie.Instance);
                var oar = state.AddModule("oar", @"oar.py", EmptyAnalysisCookie.Instance);
                var baz = state.AddModule("baz", @"baz.py", EmptyAnalysisCookie.Instance);

                Prepare(fob, fobSrc);
                Prepare(oar, oarSrc);
                Prepare(baz, bazSrc);

                fob.Analyze(CancellationToken.None);
                oar.Analyze(CancellationToken.None);
                baz.Analyze(CancellationToken.None);

                Assert.AreEqual(fob.Analysis.GetValuesByIndex("C", 1).First().Description, "class C");
                Assert.IsTrue(fob.Analysis.GetValuesByIndex("C", 1).First().Locations.Single().FilePath.EndsWith("oar.py"));

                oarSrc = GetSourceUnit(@"
", @"oar.py");

                // delete the class..
                Prepare(oar, oarSrc);
                oar.Analyze(CancellationToken.None);
                oar.AnalysisGroup.AnalyzeQueuedEntries(CancellationToken.None);

                Assert.AreEqual(fob.Analysis.GetValuesByIndex("C", 1).ToArray().Length, 0);

                fobSrc = GetSourceUnit("from baz import C", @"fob.py");
                Prepare(fob, fobSrc);

                fob.Analyze(CancellationToken.None);

                Assert.AreEqual(fob.Analysis.GetValuesByIndex("C", 1).First().Description, "class C");
                Assert.IsTrue(fob.Analysis.GetValuesByIndex("C", 1).First().Locations.Single().FilePath.EndsWith("baz.py"));
            }
        }

        [TestMethod, Priority(0)]
        public void Package() {
            var src1 = GetSourceUnit("", @"C:\\Test\\Lib\\fob\\__init__.py");

            var src2 = GetSourceUnit(@"
from fob.y import abc
import fob.y as y
", @"C:\\Test\\Lib\\fob\\x.py");

            var src3 = GetSourceUnit(@"
abc = 42
", @"C:\\Test\\Lib\\fob\\y.py");

            using (var state = PythonAnalyzer.CreateSynchronously(InterpreterFactory, Interpreter)) {

                var package = state.AddModule("fob", @"C:\\Test\\Lib\\fob\\__init__.py", EmptyAnalysisCookie.Instance);
                var x = state.AddModule("fob.x", @"C:\\Test\\Lib\\fob\\x.py", EmptyAnalysisCookie.Instance);
                var y = state.AddModule("fob.y", @"C:\\Test\\Lib\\fob\\y.py", EmptyAnalysisCookie.Instance);

                Prepare(package, src1);
                Prepare(x, src2);
                Prepare(y, src3);

                package.Analyze(CancellationToken.None);
                x.Analyze(CancellationToken.None);
                y.Analyze(CancellationToken.None);

                Assert.AreEqual(x.Analysis.GetValuesByIndex("y", 1).First().Description, "Python module fob.y");
                AssertUtil.ContainsExactly(x.Analysis.GetTypeIdsByIndex("abc", 1), BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public void PackageRelativeImport() {
            string tempPath = TestData.GetTempPath("fob");

            var files = new[] { 
                new { Content = "from .y import abc", FullPath = Path.Combine(tempPath, "__init__.py") },
                new { Content = "from .y import abc", FullPath = Path.Combine(tempPath, "x.py") } ,
                new { Content = "abc = 42",           FullPath = Path.Combine(tempPath, "y.py") } 
            };

            var srcs = new TextReader[files.Length];
            for (int i = 0; i < files.Length; i++) {
                srcs[i] = GetSourceUnit(files[i].Content, files[i].FullPath);
                File.WriteAllText(files[i].FullPath, files[i].Content);
            }

            var src1 = srcs[0];
            var src2 = srcs[1];
            var src3 = srcs[2];

            using (var state = PythonAnalyzer.CreateSynchronously(InterpreterFactory, Interpreter)) {

                var package = state.AddModule("fob", files[0].FullPath, EmptyAnalysisCookie.Instance);
                var x = state.AddModule("fob.x", files[1].FullPath, EmptyAnalysisCookie.Instance);
                var y = state.AddModule("fob.y", files[2].FullPath, EmptyAnalysisCookie.Instance);

                Prepare(package, src1);
                Prepare(x, src2);
                Prepare(y, src3);

                package.Analyze(CancellationToken.None);
                x.Analyze(CancellationToken.None);
                y.Analyze(CancellationToken.None);

                AssertUtil.ContainsExactly(x.Analysis.GetTypeIdsByIndex("abc", 1), BuiltinTypeId.Int);
                AssertUtil.ContainsExactly(package.Analysis.GetTypeIdsByIndex("abc", 1), BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public void PackageRelativeImportAliasedMember() {
            // similar to unittest package which has unittest.main which contains a function called "main".
            // Make sure we see the function, not the module.
            string tempPath = TestData.GetTempPath("fob");

            var files = new[] { 
                new { Content = "from .y import y", FullPath = Path.Combine(tempPath, "__init__.py") },
                new { Content = "def y(): pass",    FullPath = Path.Combine(tempPath, "y.py") } 
            };

            var srcs = new TextReader[files.Length];
            for (int i = 0; i < files.Length; i++) {
                srcs[i] = GetSourceUnit(files[i].Content, files[i].FullPath);
                File.WriteAllText(files[i].FullPath, files[i].Content);
            }

            var src1 = srcs[0];
            var src2 = srcs[1];

            using (var state = PythonAnalyzer.CreateSynchronously(InterpreterFactory, Interpreter)) {
                var package = state.AddModule("fob", files[0].FullPath, EmptyAnalysisCookie.Instance);
                var y = state.AddModule("fob.y", files[1].FullPath, EmptyAnalysisCookie.Instance);

                Prepare(package, src1);
                Prepare(y, src2);

                package.Analyze(CancellationToken.None);
                y.Analyze(CancellationToken.None);

                AssertUtil.ContainsExactly(package.Analysis.GetTypesByIndex("y", 1),
                    package.Analysis.ProjectState.Types[BuiltinTypeId.Function],
                    package.Analysis.ProjectState.Types[BuiltinTypeId.Module]
                );
            }
        }


        /// <summary>
        /// Verify that the analyzer has the proper algorithm for turning a filename into a package name
        /// </summary>
        [TestMethod, Priority(0)]
        public void ModulePathFromFullPath() {
            var basePath = @"C:\Not\A\Real\Path\";

            // Replace the usual File.Exists(p + '__init__.py') check so we can
            // test without real files.
            var packagePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                basePath + @"A\",
                basePath + @"A\B\"
            };

            Func<string, bool> isPackage = p => {
                Console.WriteLine("isPackage({0})", p);
                return packagePaths.Contains(p);
            };

            // __init__ files appear in the full name but not the module name.
            var mp = ModulePath.FromFullPath(Path.Combine(basePath, "A", "B", "__init__.py"), isPackage: isPackage);
            Assert.AreEqual("A.B", mp.ModuleName);
            Assert.AreEqual("A.B.__init__", mp.FullName);
            Assert.AreEqual("__init__", mp.Name);

            mp = ModulePath.FromFullPath(Path.Combine(basePath, "A", "B", "Module.py"), isPackage: isPackage);
            Assert.AreEqual("A.B.Module", mp.ModuleName);

            // Ensure we don't go back past the top-level directory if specified
            mp = ModulePath.FromFullPath(
                Path.Combine(basePath, "A", "B", "Module.py"),
                Path.Combine(basePath, "A"),
                isPackage
            );
            Assert.AreEqual("B.Module", mp.ModuleName);
        }

        [TestMethod, Priority(0)]
        public void Defaults() {
            var text = @"
def f(x = 42):
    return x
    
a = f()
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", text.IndexOf("a =")), BuiltinTypeId.Int);
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
                AssertUtil.ContainsExactly(pe[0].Analysis.GetTypeIdsByIndex("f", 1), BuiltinTypeId.Function);
                AssertUtil.ContainsExactly(pe[1].Analysis.GetTypeIdsByIndex("mod1.f", 1), BuiltinTypeId.Function);
                AssertUtil.ContainsExactly(pe[1].Analysis.GetTypeIdsByIndex("MyClass().mydec(mod1.f)", 1), BuiltinTypeId.Function);
            });
        }


        [TestMethod, Priority(0)]
        public void DecoratorFlow() {
            var text1 = @"
import mod2

inst = mod2.MyClass()

@inst.filter(fob=42)
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
                    pe[1].Analysis.GetTypeIdsByIndex("filter_func", text2.IndexOf("# @register.filter()")),
                    BuiltinTypeId.Function,
                    BuiltinTypeId.NoneType
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
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", index), BuiltinTypeId.Tuple);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", index), BuiltinTypeId.List);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("z", index), BuiltinTypeId.Float);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("w", index), BuiltinTypeId_Str);

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
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("items", index), entry.GetTypeIdsByIndex("items2", index));
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", index), BuiltinTypeId.List, BuiltinTypeId.Set, BuiltinTypeId_Str);
        }

        [TestMethod, Priority(0)]
        public void DecoratorReturnTypes() {
            // https://pytools.codeplex.com/workitem/1694
            var text = @"# without decorator
def returnsGiven(parm):
    return parm

retGivenInt = returnsGiven(1)
retGivenString = returnsGiven('str')
retGivenBool = returnsGiven(True)

# with decorator without wrap
def decoratorFunctionTakesArg1(f):
    def wrapped_f(arg):
        return f(arg)
    return wrapped_f

@decoratorFunctionTakesArg1
def returnsGivenWithDecorator1(parm):
    return parm

retGivenInt1 = returnsGivenWithDecorator1(1)
retGivenString1 = returnsGivenWithDecorator1('str')
retGivenBool1 = returnsGivenWithDecorator1(True)

# with decorator with wrap
def decoratorFunctionTakesArg2():
    def wrap(f):
        def wrapped_f(arg):
            return f(arg)
        return wrapped_f
    return wrap

@decoratorFunctionTakesArg2()
def returnsGivenWithDecorator2(parm):
    return parm

retGivenInt2 = returnsGivenWithDecorator2(1)
retGivenString2 = returnsGivenWithDecorator2('str')
retGivenBool2 = returnsGivenWithDecorator2(True)";

            var entry = ProcessText(text);

            foreach (var suffix in new[] { "", "1", "2" }) {
                AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("retGivenInt" + suffix, 0), BuiltinTypeId.Int);
                AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("retGivenString" + suffix, 0), BuiltinTypeId_Str);
                AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("retGivenBool" + suffix, 0), BuiltinTypeId.Bool);
            }
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
                AssertUtil.ContainsExactly(pe[0].Analysis.GetTypeIdsByIndex("decorator_a", 1));
                AssertUtil.ContainsExactly(pe[1].Analysis.GetTypeIdsByIndex("decorator_b", 1));
            });
        }

        [TestMethod, Priority(0)]
        public void ProcessDecorators() {
            var text = @"
def d(fn):
    return []

@d
def my_fn():
    return None
";

            var sourceUnit = GetSourceUnit(text, "fob");
            var state = CreateAnalyzer();
            state.Limits.ProcessCustomDecorators = true;
            var entry = state.AddModule("fob", "fob", null);
            Prepare(entry, sourceUnit);
            entry.Analyze(CancellationToken.None);

            AssertUtil.ContainsExactly(
                entry.Analysis.GetTypeIdsByIndex("my_fn", 0),
                BuiltinTypeId.List
            );
            AssertUtil.ContainsExactly(
                entry.Analysis.GetTypeIdsByIndex("fn", text.IndexOf("return")),
                BuiltinTypeId.Function
            );
        }

        [TestMethod, Priority(0)]
        public void NoProcessDecorators() {
            var text = @"
def d(fn):
    return []

@d
def my_fn():
    return None
";

            var sourceUnit = GetSourceUnit(text, "fob");
            var state = CreateAnalyzer();
            state.Limits.ProcessCustomDecorators = false;
            var entry = state.AddModule("fob", "fob", null);
            Prepare(entry, sourceUnit);
            entry.Analyze(CancellationToken.None);
            state.Limits.ProcessCustomDecorators = true;

            AssertUtil.ContainsExactly(
                entry.Analysis.GetTypeIdsByIndex("my_fn", 0),
                BuiltinTypeId.Function
            );
            AssertUtil.ContainsExactly(
                entry.Analysis.GetTypeIdsByIndex("fn", text.IndexOf("return")),
                BuiltinTypeId.Function
            );
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
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("value", text.IndexOf(" = value")), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void InstanceCall() {
            var text = @"
class X:
    def __call__(self, value):
        return value

x = X()

a = x(2)
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", -1), BuiltinTypeId.Int);
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
                    "def g() -> built-in module re"
                );
                AssertUtil.ContainsExactly(
                    entries[0].Analysis.GetDescriptionsByIndex("f", 1),
                    "def f() -> built-in module sys"
                );
                AssertUtil.ContainsExactly(
                    entries[0].Analysis.GetDescriptionsByIndex("h", 1),
                    "def h() -> built-in module sys"
                );
                AssertUtil.ContainsExactly(
                    entries[0].Analysis.GetDescriptionsByIndex("i", 1),
                    "def i() -> built-in module operator"
                );
                AssertUtil.ContainsExactly(
                    entries[0].Analysis.GetDescriptionsByIndex("j", 1),
                    "def j() -> built-in module mmap"
                );
                AssertUtil.ContainsExactly(
                    entries[0].Analysis.GetDescriptionsByIndex("k", 1),
                    "def k() -> built-in module imp"
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
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("value", text.IndexOf(" = value")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("res", text.IndexOf("res.value = ")), "X instance");
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("a", text.IndexOf("a = ")), "X instance");
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a.value", text.IndexOf("a = ")), BuiltinTypeId.Int);
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
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", text.IndexOf("a,")), BuiltinTypeId.NoneType, BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", text.IndexOf("a,")), BuiltinTypeId.NoneType, BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", text.IndexOf("a,")), BuiltinTypeId.NoneType, BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", text.IndexOf("a,")), BuiltinTypeId.NoneType, BuiltinTypeId.Int);
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
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", text.IndexOf("x =")), BuiltinTypeId.NoneType, BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", text.IndexOf("y =")), BuiltinTypeId.NoneType, BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", text.IndexOf("nonlocal")), BuiltinTypeId.NoneType, BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", text.IndexOf("nonlocal")), BuiltinTypeId.NoneType, BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", text.IndexOf("a,")), BuiltinTypeId.NoneType, BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b", text.IndexOf("a,")), BuiltinTypeId.NoneType, BuiltinTypeId.Int);

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
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", text.IndexOf("a =")), BuiltinTypeId.NoneType, BuiltinTypeId.Int);
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
    fob = 300
    pass
";

            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", text.IndexOf("z =")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", text.IndexOf("z =") + 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", text.IndexOf("pass")), BuiltinTypeId.NoneType, BuiltinTypeId.Int, BuiltinTypeId_Str, BuiltinTypeId.Tuple);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", text.IndexOf("y =")), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", text.IndexOf("y =") + 1), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", text.IndexOf("else:") + 7), BuiltinTypeId.NoneType, BuiltinTypeId.Int, BuiltinTypeId_Str, BuiltinTypeId.Tuple);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", text.IndexOf("fob =")), BuiltinTypeId.Tuple);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", text.LastIndexOf("pass")), BuiltinTypeId.Tuple);

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
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", text.IndexOf("f(a)")));
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", text.IndexOf("def g()")), BuiltinTypeId.Int, BuiltinTypeId.Unicode);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", text.IndexOf("pass")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", text.IndexOf("print(a)")), BuiltinTypeId.Int, BuiltinTypeId.Unicode);

            text = @"x = None


if True:
    pass
    assert isinstance(x, int)
    z = 100
    
    pass

print(z)";

            entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("z", text.IndexOf("z =")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("z", 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("z", text.IndexOf("print(z)") - 2), BuiltinTypeId.Int);

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
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("z", code.IndexOf("z = x")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("w", code.IndexOf("w = y")), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void NestedIsInstance1908() {
            // https://pytools.codeplex.com/workitem/1908
            var code = @"
def f(x):
    y = object()    
    assert isinstance(x, int)
    if isinstance(y, float):
        print('hi')

    pass
";

            var entry = ProcessText(code);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", code.IndexOf("pass")), BuiltinTypeId.Object, BuiltinTypeId.Float);
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

r1 = fn('fob', (int, str), 'oar')
r2 = fn(123, None, 4.5)

# b1 and b2 will only be type (from the tuple), since indexing into 'type'
# will result in nothing
b1 = r1.b[0]
b2 = r2.b[0]
";

            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("r1.a", text.IndexOf("r1 =")), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("r1.b", text.IndexOf("r1 =")), BuiltinTypeId.Type, BuiltinTypeId.Tuple);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b1", text.IndexOf("b1 =")), BuiltinTypeId.Type);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("r1.c", text.IndexOf("r1 =")), BuiltinTypeId_Str);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("r2.a", text.IndexOf("r2 =")), BuiltinTypeId_Str);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("r2.b", text.IndexOf("r2 =")), BuiltinTypeId.Type, BuiltinTypeId.Tuple);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("b2", text.IndexOf("b2 =")), BuiltinTypeId.Type);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("r2.c", text.IndexOf("r2 =")), BuiltinTypeId_Str);
        }

        [TestMethod, Priority(0)]
        public void IsInstanceReferences() {
            var text = @"def fob():
    oar = get_b()
    assert isinstance(oar, float)

    if oar.complex:
        raise IndexError

    return oar";

            var entry = ProcessText(text);

            for (int i = text.IndexOf("oar", 0); i >= 0; i = text.IndexOf("oar", i + 1)) {
                VerifyReferences(UniqifyVariables(entry.GetVariablesByIndex("oar", i)),
                    new VariableLocation(2, 5, VariableType.Definition),
                    new VariableLocation(3, 23, VariableType.Reference),
                    new VariableLocation(5, 8, VariableType.Reference),
                    new VariableLocation(8, 12, VariableType.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public void DecoratorReferences() {
            var text = @"from functools import wraps

def d(f):
    @wraps(f)
    def wrapped(*a, **kw):
        return f(*a, **kw)
    return wrapped

@d
def g(p):
    return p

n1 = g(1)";

            var entry = ProcessText(text);

            VerifyReferences(UniqifyVariables(entry.GetVariablesByIndex("d", 0)),
                new VariableLocation(3, 5, VariableType.Definition),
                new VariableLocation(9, 2, VariableType.Reference)
            );

            VerifyReferences(UniqifyVariables(entry.GetVariablesByIndex("g", 0)),
                new VariableLocation(10, 5, VariableType.Definition),
                new VariableLocation(13, 6, VariableType.Reference)
            );

            // Decorators that don't use @wraps will expose the wrapper function
            // as a value.
            text = @"def d(f):
    def wrapped(*a, **kw):
        return f(*a, **kw)
    return wrapped

@d
def g(p):
    return p

n1 = g(1)";

            entry = ProcessText(text);

            VerifyReferences(UniqifyVariables(entry.GetVariablesByIndex("d", 0)),
                new VariableLocation(1, 5, VariableType.Definition),
                new VariableLocation(6, 2, VariableType.Reference)
            );

            VerifyReferences(UniqifyVariables(entry.GetVariablesByIndex("g", 0)),
                new VariableLocation(7, 5, VariableType.Definition),
                new VariableLocation(10, 6, VariableType.Reference),
                new VariableLocation(2, 9, VariableType.Value)
            );
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

class fob(object):
    @property
    def f(self): pass

    def g(self): pass
    
d = fob()

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

def with_params(a, b, c):
    pass

def with_params_default(a, b, c = 100):
    pass

def with_params_default_2(a, b, c = []):
    pass

def with_params_default_3(a, b, c = ()):
    pass

def with_params_default_4(a, b, c = {}):
    pass

def with_params_default_2a(a, b, c = [None]):
    pass

def with_params_default_3a(a, b, c = (None, )):
    pass

def with_params_default_4a(a, b, c = {42: 100}):
    pass

def with_params_default_starargs(*args, **kwargs):
    pass
";
            var entry = ProcessText(text);

            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("fob()", 1), "fob instance");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("int()", 1), "int");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("a", 1), "float");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("a", 1), "float");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("b", 1), "long");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("c", 1), "str");
            AssertUtil.ContainsExactly(entry.GetValuesByIndex("x", 1).Select(v => v.Description.Substring(0, 5)), "tuple");
            AssertUtil.ContainsExactly(entry.GetValuesByIndex("y", 1).Select(v => v.Description.Substring(0, 4)), "list");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("z", 1), "int");
            Assert.IsTrue(entry.GetDescriptionsByIndex("min", 1).First().StartsWith("built-in function min"));
            Assert.IsTrue(entry.GetDescriptionsByIndex("min", 1).First().Contains("min(x: object)"));
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("list.append", 1), "built-in method append");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("\"abc\".Length", 1));
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("c.Length", 1));
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("d", 1), "fob instance");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("sys", 1), "built-in module sys");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("f", 1), "def f() -> str");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("fob.f", 1), "def f(self)\r\ndeclared in fob");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("fob().g", 1), "method g of fob objects ");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("fob", 1), "class fob");
            //AssertUtil.ContainsExactly(entry.GetVariableDescriptionsByIndex("System.StringSplitOptions.RemoveEmptyEntries", 1), "field of type StringSplitOptions");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("g", 1), "def g()");    // return info could be better
            //AssertUtil.ContainsExactly(entry.GetVariableDescriptionsByIndex("System.AppDomain.DomainUnload", 1), "event of type System.EventHandler");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("None", 1), "None");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("f.func_name", 1), "property of type str");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("h", 1), "def h() -> def f() -> str, def g()");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("docstr_func", 1), "def docstr_func() -> int\r\nuseful documentation");

            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("with_params", 1), "def with_params(a, b, c)");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("with_params_default", 1), "def with_params_default(a, b, c = 100)");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("with_params_default_2", 1), "def with_params_default_2(a, b, c = [])");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("with_params_default_3", 1), "def with_params_default_3(a, b, c = ())");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("with_params_default_4", 1), "def with_params_default_4(a, b, c = {})");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("with_params_default_2a", 1), "def with_params_default_2a(a, b, c = [...])");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("with_params_default_3a", 1), "def with_params_default_3a(a, b, c = (...))");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("with_params_default_4a", 1), "def with_params_default_4a(a, b, c = {...})");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("with_params_default_starargs", 1), "def with_params_default_starargs(*args, **kwargs)");

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

class fob(object):
    @property
    def f(self): pass

    def g(self): pass
    
d = fob()

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

class fob(object):
    @property
    def f(self): pass

    def g(self): pass
    
d = fob()

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

            var vars = new List<AnalysisValue>(entry.GetValuesByIndex("x[0]", text.IndexOf("pass")));
            Assert.AreEqual(1, vars.Count);
            Assert.AreEqual(BuiltinTypeId.Int, vars[0].PythonType.TypeId);

            foreach (string value in new[] { "ly", "lz", "ty", "tz", "lyt", "tyt" }) {
                vars = new List<AnalysisValue>(entry.GetValuesByIndex(value + "[0]", text.IndexOf("pass")));
                Assert.AreEqual(1, vars.Count, "value: {0}", value);
                Assert.AreEqual(BuiltinTypeId.Int, vars[0].PythonType.TypeId, "value: {0}", value);
            }
        }

#if FALSE
        [TestMethod, Priority(0)]
        public void SaveStdLib() {
            // only run this once...
            if (GetType() == typeof(AnalysisTest)) {
                var stdLib = AnalyzeStdLib();

                string tmpFolder = TestData.GetTempPath("6666d700-a6d8-4e11-8b73-3ba99a61e27b");

                new SaveAnalysis().Save(stdLib, tmpFolder);

                File.Copy(Path.Combine(PythonInterpreterFactory.GetBaselineDatabasePath(), "__builtin__.idb"), Path.Combine(tmpFolder, "__builtin__.idb"), true);

                var newPs = new PythonAnalyzer(new CPythonInterpreter(new TypeDatabase(tmpFolder)), PythonLanguageVersion.V27);
            }
        }
#endif


        [TestMethod, Priority(0)]
        public void SubclassFindAllRefs() {
            string text = @"
class Base(object):
    def __init__(self):
        self.fob()
    
    def fob(self): 
        pass
    
    
class Derived(Base):
    def fob(self): 
        'x'
";

            var entry = ProcessText(text);

            var vars = entry.GetVariablesByIndex("self.fob", text.IndexOf("'x'"));
            VerifyReferences(UniqifyVariables(vars), new VariableLocation(11, 9, VariableType.Definition), new VariableLocation(6, 9, VariableType.Definition), new VariableLocation(4, 14, VariableType.Reference));

            vars = entry.GetVariablesByIndex("self.fob", text.IndexOf("pass"));
            VerifyReferences(UniqifyVariables(vars), new VariableLocation(11, 9, VariableType.Definition), new VariableLocation(6, 9, VariableType.Definition), new VariableLocation(4, 14, VariableType.Reference));

            vars = entry.GetVariablesByIndex("self.fob", text.IndexOf("self.fob"));
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

            //var vars = entry.GetVariables("fob", GetLineNumber(text, "'x'"));

        }

        [TestMethod, Priority(0)]
        public void TypeAtEndOfMethod() {
            string text = @"
class Fob(object):
    def oar(self, a):
        pass
        
        
    def fob(self): 
        pass

x = Fob()
x.oar(100)
";

            var entry = ProcessText(text);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("a", text.IndexOf("fob") - 10), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void TypeIntersectionUserDefinedTypes() {
            string text = @"
class C1(object):
    def fob(self): pass

class C2(object):
    def oar(self): pass

c = C1()
c.fob()
c = C2()

";

            var entry = ProcessText(text);
            var members = entry.GetMemberNamesByIndex("a", text.IndexOf("c = C2()"), GetMemberOptions.IntersectMultipleResults);
            AssertUtil.DoesntContain(members, "fob");
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

            using (var state = PythonAnalyzer.CreateSynchronously(InterpreterFactory, Interpreter)) {
                // add both files to the project
                var entry1 = state.AddModule("mod1", "mod1", null);
                var entry2 = state.AddModule("mod2", "mod2", null);

                // analyze both files
                Prepare(entry1, GetSourceUnit(text1, "mod1"), InterpreterFactory.GetLanguageVersion());
                entry1.Analyze(CancellationToken.None);
                Prepare(entry2, GetSourceUnit(text2, "mod2"), InterpreterFactory.GetLanguageVersion());
                entry2.Analyze(CancellationToken.None);

                AssertUtil.ContainsExactly(entry1.Analysis.GetTypeIdsByIndex("abc", text1.IndexOf("pass")), BuiltinTypeId.Int);

                // re-analyze project1, we should still know about the type info provided by module2
                Prepare(entry1, GetSourceUnit(text1, "mod1"), InterpreterFactory.GetLanguageVersion());
                entry1.Analyze(CancellationToken.None);

                AssertUtil.ContainsExactly(entry1.Analysis.GetTypeIdsByIndex("abc", text1.IndexOf("pass")), BuiltinTypeId.Int);
            }
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

            //var entry = ProcessText(text);
            //Assert.AreEqual(entry.GetSignaturesByIndex("cls.f", text.IndexOf("print cls.g")).First().Parameters.Length, 0);
            //Assert.AreEqual(entry.GetSignaturesByIndex("cls.g", text.IndexOf("print cls.g")).First().Parameters.Length, 0);
            //Assert.AreEqual(entry.GetSignaturesByIndex("cls.x", text.IndexOf("print cls.g")).First().Parameters.Length, 1);
            //Assert.AreEqual(entry.GetSignaturesByIndex("cls.inst_method", text.IndexOf("print cls.g")).First().Parameters.Length, 1);

            if (SupportsPython3) {
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

                var entry = ProcessText(text, PythonLanguageVersion.V32);
                Assert.AreEqual(entry.GetSignaturesByIndex("cls.f", text.IndexOf("print(cls.g)")).First().Parameters.Length, 0);
                Assert.AreEqual(entry.GetSignaturesByIndex("cls.g", text.IndexOf("print(cls.g)")).First().Parameters.Length, 0);
                Assert.AreEqual(entry.GetSignaturesByIndex("cls.x", text.IndexOf("print(cls.g)")).First().Parameters.Length, 1);
                Assert.AreEqual(entry.GetSignaturesByIndex("cls.inst_method", text.IndexOf("print(cls.g)")).First().Parameters.Length, 1);
            }


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

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", code.IndexOf("x = ")), BuiltinTypeId_Str);
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

        [TestMethod, Priority(0)]
        public void ParameterAnnotation() {
            var text = @"
s = None
def f(s: s = 123):
    return s
";
            var entry = ProcessText(text, PythonLanguageVersion.V33);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("s", text.IndexOf("s:")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("s", text.IndexOf("s =")), BuiltinTypeId.NoneType);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("s", text.IndexOf("return s")), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void ParameterAnnotationLambda() {
            var text = @"
s = None
def f(s: lambda s: s > 0 = 123):
    return s
";
            var entry = ProcessText(text, PythonLanguageVersion.V33);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("s", text.IndexOf("s:")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("s", text.IndexOf("s >")), BuiltinTypeId.NoneType);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("s", text.IndexOf("return s")), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void ReturnAnnotation() {
            var text = @"
s = None
def f(s = 123) -> s:
    return s
";
            var entry = ProcessText(text, PythonLanguageVersion.V33);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("s", text.IndexOf("(s =") + 1), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("s", text.IndexOf("s:")), BuiltinTypeId.NoneType);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("s", text.IndexOf("return s")), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void FunctoolsPartial() {
            var text = @"
from _functools import partial

def fob(a, b, c, d):
    return a, b, c, d

sanity = fob(123, 3.14, 'abc', [])

fob_1 = partial(fob, 123, 3.14, 'abc', [])
result_1 = fob_1()

fob_2 = partial(fob, d = [], c = 'abc', b = 3.14, a = 123)
result_2 = fob_2()

fob_3 = partial(fob, 123, 3.14)
result_3 = fob_3('abc', [])

fob_4 = partial(fob, c = 'abc', d = [])
result_4 = fob_4(123, 3.14)

func_from_fob_1 = fob_1.func
args_from_fob_1 = fob_1.args
keywords_from_fob_2 = fob_2.keywords
";
            var entry = ProcessText(text, PythonLanguageVersion.V27);

            foreach (var name in new[] {
                "sanity",
                "result_1",
                "result_2",
                "result_3",
                "result_4",
                "args_from_fob_1"
            }) {
                var resultList = entry.GetValuesByIndex(name, 0).ToArray();
                Assert.AreEqual(1, resultList.Length, name + ".Types.Length");
                Assert.IsInstanceOfType(resultList[0], typeof(SequenceInfo), name + ".Types[0]");
                var result = (SequenceInfo)resultList[0];
                AssertTupleContains(result, BuiltinTypeId.Int, BuiltinTypeId.Float, BuiltinTypeId_Str, BuiltinTypeId.List);
            }

            var fobList = entry.GetValuesByIndex("fob", 0).ToArray();
            Assert.AreEqual(1, fobList.Length, "fob.Types.Length");
            Assert.IsInstanceOfType(fobList[0], typeof(FunctionInfo), "fob.Types[0]");
            var fob = (FunctionInfo)fobList[0];
            fobList = entry.GetValuesByIndex("func_from_fob_1", 0).ToArray();
            Assert.AreEqual(1, fobList.Length, "func_from_fob_1.Types.Length");
            Assert.IsInstanceOfType(fobList[0], typeof(FunctionInfo), "func_from_fob_1.Types[0]");
            Assert.AreSame(fob, fobList[0]);

            var kwList = entry.GetValuesByIndex("keywords_from_fob_2", 0).ToArray();
            Assert.AreEqual(1, kwList.Length, "kwList.Types.Length");
            Assert.IsInstanceOfType(kwList[0], typeof(DictionaryInfo), "kwList.Types[0]");
        }

        [TestMethod, Priority(0)]
        public void FunctoolsWraps() {
            var text = @"
from functools import wraps, update_wrapper

def decorator1(fn):
    @wraps(fn)
    def wrapper(*args, **kwargs):
        fn(*args, **kwargs)
        return 'decorated'
    return wrapper

@decorator1
def test1():
    return 'undecorated'

def test2():
    pass

def test2a():
    pass

test2.test_attr = 123
update_wrapper(test2a, test2, ('test_attr',))

test1_result = test1()
";

            var functools = @"
from _functools import partial

# These functions will be specialized
def wraps(f):
    pass

def update_wrapper(wrapper, wrapped, assigned, updated):
    pass
";

            var state = CreateAnalyzer();
            var functoolsEntry = state.AddModule("functools", "functools", null);
            Prepare(functoolsEntry, GetSourceUnit(functools));
            var textEntry = state.AddModule("fob", "fob", null);
            Prepare(textEntry, GetSourceUnit(text));
            functoolsEntry.Analyze(CancellationToken.None, true);
            textEntry.Analyze(CancellationToken.None, true);
            state.AnalyzeQueuedEntries(CancellationToken.None);
            var entry = textEntry.Analysis;

            Assert.AreEqual(1, entry.GetValuesByIndex("test1", 0).Count());
            var nameList = entry.GetValuesByIndex("test1.__name__", 0).ToArray();
            Assert.AreEqual(1, nameList.Length);
            Assert.AreEqual("test1", nameList[0].GetConstantValueAsString(), "test1.__name__");
            var wrappedList = entry.GetValuesByIndex("test1.__wrapped__", 0).ToArray();
            Assert.AreEqual(1, wrappedList.Length);
            Assert.IsInstanceOfType(wrappedList[0], typeof(FunctionInfo), "typeof(test1.__wrapped__)");
            var returnList = entry.GetValuesByIndex("test1_result", 0).ToArray();
            Assert.AreEqual(1, returnList.Length);
            Assert.AreEqual("decorated", returnList[0].GetConstantValueAsString(), "test1()");

            // __name__ should not have been changed by update_wrapper
            nameList = entry.GetValuesByIndex("test2.__name__", 0).ToArray();
            Assert.AreEqual(1, nameList.Length);
            Assert.AreEqual("test2", nameList[0].GetConstantValueAsString(), "test2.__name__");
            nameList = entry.GetValuesByIndex("test2a.__name__", 0).ToArray();
            Assert.AreEqual(1, nameList.Length);
            Assert.AreEqual("test2a", nameList[0].GetConstantValueAsString(), "test2a.__name__");

            // test_attr should have been copied by update_wrapper
            var test2 = entry.GetValuesByIndex("test2.test_attr", 0).ToArray();
            Assert.AreEqual(1, test2.Length);
            Assert.AreEqual(BuiltinTypeId.Int, test2[0].TypeId);
            var test2a = entry.GetValuesByIndex("test2a.test_attr", 0).ToArray();
            Assert.AreEqual(1, test2a.Length);
            Assert.AreEqual(BuiltinTypeId.Int, test2a[0].TypeId);
        }

        private static void AssertTupleContains(SequenceInfo tuple, params BuiltinTypeId[] id) {
            var expected = string.Join(", ", id);
            var actual = string.Join(", ", tuple.IndexTypes.Select(t => {
                var t2 = t.TypesNoCopy;
                if (t2.Count == 1) {
                    return t2.Single().TypeId.ToString();
                } else {
                    return "{" + string.Join(", ", t2.Select(t3 => t3.TypeId).OrderBy(t3 => t3)) + "}";
                }
            }));
            if (tuple.IndexTypes
                .Zip(id, (t1, id2) => t1.TypesNoCopy.Count == 1 && t1.TypesNoCopy.Single().TypeId == id2)
                .Any(b => !b)) {
                Assert.Fail(string.Format("Expected <{0}>. Actual <{1}>.", expected, actual));
            }
        }


        [TestMethod, Priority(0)]
        public void ValidatePotentialModuleNames() {
            // Validating against the structure given in
            // http://www.python.org/dev/peps/pep-0328/

            var entry = new MockPythonProjectEntry {
                ModuleName = "package.subpackage1.moduleX",
                FilePath = "C:\\package\\subpackage1\\moduleX.py"
            };
            
            // Without absolute_import, we should see these two possibilities
            // for a regular import.
            AssertUtil.ContainsExactly(
                PythonAnalyzer.ResolvePotentialModuleNames(entry, "moduleY", false),
                "package.subpackage1.moduleY",
                "moduleY"
            );

            // With absolute_import, we should see the two possibilities for a
            // regular import, but in the opposite order.
            AssertUtil.ContainsExactly(
                PythonAnalyzer.ResolvePotentialModuleNames(entry, "moduleY", true),
                "moduleY",
                "package.subpackage1.moduleY"
            );
            
            // Regardless of absolute import, we should see these results for
            // relative imports.
            foreach (var absoluteImport in new[] { true, false }) {
                Console.WriteLine("Testing with absoluteImport = {0}", absoluteImport);

                AssertUtil.ContainsExactly(
                    PythonAnalyzer.ResolvePotentialModuleNames(entry, ".moduleY", absoluteImport),
                    "package.subpackage1.moduleY"
                );
                AssertUtil.ContainsExactly(
                    PythonAnalyzer.ResolvePotentialModuleNames(entry, ".", absoluteImport),
                    "package.subpackage1"
                );
                AssertUtil.ContainsExactly(
                    PythonAnalyzer.ResolvePotentialModuleNames(entry, "..subpackage1", absoluteImport),
                    "package.subpackage1"
                );
                AssertUtil.ContainsExactly(
                    PythonAnalyzer.ResolvePotentialModuleNames(entry, "..subpackage2.moduleZ", absoluteImport),
                    "package.subpackage2.moduleZ"
                );
                AssertUtil.ContainsExactly(
                    PythonAnalyzer.ResolvePotentialModuleNames(entry, "..moduleA", absoluteImport),
                    "package.moduleA"
                );

                // Despite what PEP 328 says, this relative import never succeeds.
                AssertUtil.ContainsExactly(
                    PythonAnalyzer.ResolvePotentialModuleNames(entry, "...package", absoluteImport)
                );
            }
        }

        [TestMethod, Priority(0)]
        public void MultilineFunctionDescription() {
            var code = @"class A:
    def fn(self):
        return lambda: 123
";
            var entry = ProcessText(code);

            Assert.AreEqual(
                entry.GetDescriptionsByIndex("A.fn", 0).Single().Replace("\r\n", "\n"),
                "def fn(self) -> lambda : 123 -> int\n    declared in A.fn\ndeclared in A"
            );
        }

        [TestMethod, Priority(0)]
        public void SysModulesSetSpecialization() {
            var code = @"import sys
modules = sys.modules

modules['name_in_modules'] = None
";
            code += string.Join(
                Environment.NewLine,
                Enumerable.Range(0, 100).Select(i => string.Format("sys.modules['name{0}'] = None", i))
            );

            var entry = ProcessText(code);

            var sysObj = entry.GetValuesByIndex("sys", 0).Single();
            Assert.IsInstanceOfType(sysObj, typeof(SysModuleInfo));
            var sys = sysObj as SysModuleInfo;

            var modules = entry.GetValuesByIndex("modules", 0).Single();
            Assert.IsInstanceOfType(modules, typeof(SysModuleInfo.SysModulesDictionaryInfo));

            AssertUtil.ContainsExactly(
                sys.Modules.Keys,
                Enumerable.Range(0, 100).Select(i => string.Format("name{0}", i))
                    .Concat(new[] { "name_in_modules" })
            );
        }

        [TestMethod, Priority(0)]
        public void SysModulesGetSpecialization() {
            var code = @"import sys
modules = sys.modules

modules['value_in_modules'] = 'abc'
modules['value_in_modules'] = 123
value_in_modules = modules['value_in_modules']
builtins = modules['__builtin__']
builtins2 = modules.get('__builtin__')
builtins3 = modules.pop('__builtin__')
";

            var entry = ProcessText(code);

            AssertUtil.ContainsExactly(
                entry.GetTypeIdsByIndex("value_in_modules", 0),
                BuiltinTypeId.Int
                // Not BuiltinTypeId_Str because it only keeps the last value
            );

            AssertUtil.ContainsExactly(entry.GetValuesByIndex("builtins", 0).Select(av => av.Name), "__builtin__");
            AssertUtil.ContainsExactly(entry.GetValuesByIndex("builtins2", 0).Select(av => av.Name), "__builtin__");
            AssertUtil.ContainsExactly(entry.GetValuesByIndex("builtins3", 0).Select(av => av.Name), "__builtin__");
        }

        [TestMethod, Priority(0)]
        public void ClassInstanceAttributes() {
            var code = @"
class A:
    abc = 123

p1 = A.abc
p2 = A().abc
a = A()
a.abc = 3.1415
p4 = A().abc
p3 = a.abc
";
            var entry = ProcessText(code);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("p1", 0), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("p3", 0), BuiltinTypeId.Int, BuiltinTypeId.Float);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("p4", 0), BuiltinTypeId.Int, BuiltinTypeId.Float);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("p2", 0), BuiltinTypeId.Int, BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public void RecursiveGetDescriptor() {
            // see https://pytools.codeplex.com/workitem/2955
            var entry = ProcessText(@"
class WithGet:
    __get__ = WithGet()

class A:
    wg = WithGet()

x = A().wg");

            Assert.IsNotNull(entry);
        }

        [TestMethod, Priority(0)]
        public void Coroutine() {
            var code = @"
async def g():
    return 123

async def f():
    x = await g()
    g2 = g()
    y = await g2
";
            var entry = ProcessText(code, PythonLanguageVersion.V35);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("x", code.IndexOf("x =")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", code.IndexOf("y =")), BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("g2", code.IndexOf("g2 =")), BuiltinTypeId.Generator);
        }

        [TestMethod, Priority(0)]
        public void AsyncWithStatement() {
            var text = @"
class X(object):
    def x_method(self): pass
    async def __aenter__(self): return self
    async def __aexit__(self, exc_type, exc_value, traceback): return False

class Y(object):
    def y_method(self): pass
    async def __aenter__(self): return 123
    async def __aexit__(self, exc_type, exc_value, traceback): return False

async def f():
    async with X() as x:
        pass #x

    async with Y() as y:
        pass #y
";
            var entry = ProcessText(text, PythonLanguageVersion.V35);
            AssertUtil.ContainsAtLeast(entry.GetMemberNamesByIndex("x", text.IndexOf("pass #x")), "x_method");
            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("y", text.IndexOf("pass #y")), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void AsyncForIterator() {
            var code = @"
class X:
    async def __aiter__(self): return self
    async def __anext__(self): return 123

class Y:
    async def __aiter__(self): return X()

async def f():
    async for i in Y():
        pass
";
            var entry = ProcessText(code, PythonLanguageVersion.V35);

            AssertUtil.ContainsExactly(entry.GetTypeIdsByIndex("i", code.IndexOf("pass")), BuiltinTypeId.Int);
        }



        #endregion

        #region Helpers

        protected IEnumerable<IAnalysisVariable> UniqifyVariables(IEnumerable<IAnalysisVariable> vars) {
            Dictionary<LocationInfo, IAnalysisVariable> res = new Dictionary<LocationInfo, IAnalysisVariable>();
            foreach (var v in vars) {
                if (!res.ContainsKey(v.Location) || res[v.Location].Type == VariableType.Value) {
                    res[v.Location] = v;
                }
            }

            return res.Values;

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
                using (var state = PythonAnalyzer.CreateSynchronously(InterpreterFactory, Interpreter)) {
                    state.Limits = GetLimits();

                    for (int i = 0; i < code.Length; i++) {
                        result[p[i]] = state.AddModule(prefix + (p[i] + 1).ToString(), "fob", null);
                    }
                    for (int i = 0; i < code.Length; i++) {
                        Prepare(result[p[i]], GetSourceUnit(code[p[i]]));
                    }
                    for (int i = 0; i < code.Length; i++) {
                        result[p[i]].Analyze(CancellationToken.None, true);
                    }

                    state.AnalyzeQueuedEntries(CancellationToken.None);

                    yield return result;
                }
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

        public StdLibAnalysisTest(IPythonInterpreterFactory factory, IPythonInterpreter interpreter)
            : base(factory, interpreter) {
        }

        protected override AnalysisLimits GetLimits() {
            return AnalysisLimits.GetStandardLibraryLimits();
        }
    }

    public static class ModuleAnalysisExtensions {
        public static IEnumerable<string> GetMemberNamesByIndex(this ModuleAnalysis analysis, string exprText, int index, GetMemberOptions options = GetMemberOptions.IntersectMultipleResults) {
            return analysis.GetMembersByIndex(exprText, index, options).Select(m => m.Name);
        }

        public static IEnumerable<IPythonType> GetTypesByIndex(this ModuleAnalysis analysis, string exprText, int index) {
            return analysis.GetValuesByIndex(exprText, index).Select(m => m.PythonType);
        }

        public static IEnumerable<BuiltinTypeId> GetTypeIdsByIndex(this ModuleAnalysis analysis, string exprText, int index) {
            return analysis.GetValuesByIndex(exprText, index).Select(m => {
                if (m.PythonType.TypeId != BuiltinTypeId.Unknown) {
                    return m.PythonType.TypeId;
                }

                var state = analysis.ProjectState;
                if (m == state._noneInst) {
                    return BuiltinTypeId.NoneType;
                }

                var bci = m as BuiltinClassInfo;
                if (bci == null) {
                    var bii = m as BuiltinInstanceInfo;
                    if (bii != null) {
                        bci = bii.ClassInfo;
                    }
                }
                if (bci != null) {
                    int count = (int)BuiltinTypeIdExtensions.LastTypeId;
                    for (int i = 1; i <= count; ++i) {
                        var bti = (BuiltinTypeId)i;
                        if (!bti.IsVirtualId() && analysis.ProjectState.ClassInfos[bti] == bci) {
                            return bti;
                        }
                    }
                }

                return BuiltinTypeId.Unknown;
            });
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

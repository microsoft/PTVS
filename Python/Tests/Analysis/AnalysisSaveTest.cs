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
using System.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools;
using TestUtilities;
using TestUtilities.Python;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace AnalysisTests {
    [TestClass]
    public class AnalysisSaveTest : BaseAnalysisTest {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy(includeTestData: false);
        }

        [TestMethod, Priority(0)]
        public void General() {
            string code = @"def f(a, *b, **c): pass

def f1(x = 42): pass
def f2(x = []): pass

class C(object): 
    @property
    def f(self):
        return 42
    def g(self):
        return 100

class D(C): 
    x = C().g

abc = 42
fob = int

class X(object): pass
class Y(object): pass

union = X()
union = Y()

list_of_int = [1, 2, 3]
tuple_of_str = 'a', 'b', 'c'

m = max

class Aliased(object):
    def f(self):
        pass

def Aliased(fob):
    pass
";

            using (var newPs = SaveLoad(PythonLanguageVersion.V27, new AnalysisModule("test", "test.py", code))) {
                AssertUtil.Contains(newPs.Analyzer.GetModules().Select(x => x.Name), "test");

                string codeText = @"
import test
abc = test.abc
fob = test.fob
a = test.C()
cf = a.f
cg = a.g()
dx = test.D().x
scg = test.C.g
f1 = test.f1
union = test.union
list_of_int = test.list_of_int
tuple_of_str = test.tuple_of_str
f1 = test.f1
f2 = test.f2
m = test.m
Aliased = test.Aliased
";
                var newMod = newPs.NewModule("baz", codeText);
                int pos = codeText.LastIndexOf('\n');

                AssertUtil.ContainsExactly(newMod.Analysis.GetTypeIdsByIndex("abc", pos), BuiltinTypeId.Int);
                AssertUtil.ContainsExactly(newMod.Analysis.GetTypeIdsByIndex("cf", pos), BuiltinTypeId.Int);
                AssertUtil.ContainsExactly(newMod.Analysis.GetTypeIdsByIndex("cg", pos), BuiltinTypeId.Int);
                Assert.AreEqual("function f1", newMod.Analysis.GetValuesByIndex("f1", pos).First().Description);
                Assert.AreEqual("bound method x", newMod.Analysis.GetValuesByIndex("dx", pos).First().Description);
                Assert.AreEqual("function g", newMod.Analysis.GetValuesByIndex("scg", pos).First().Description);
                var unionMembers = new List<AnalysisValue>(newMod.Analysis.GetValuesByIndex("union", pos));
                Assert.AreEqual(unionMembers.Count, 2);
                AssertUtil.ContainsExactly(unionMembers.Select(x => x.PythonType.Name), "X", "Y");

                var list = newMod.Analysis.GetValuesByIndex("list_of_int", pos).First();
                AssertUtil.ContainsExactly(newMod.Analysis.GetShortDescriptionsByIndex("list_of_int", pos), "list of int");
                AssertUtil.ContainsExactly(newMod.Analysis.GetShortDescriptionsByIndex("tuple_of_str", pos), "tuple of str");

                AssertUtil.ContainsExactly(newMod.Analysis.GetShortDescriptionsByIndex("fob", pos), "type int");

                var result = newMod.Analysis.GetSignaturesByIndex("f1", pos).ToArray();
                Assert.AreEqual(1, result.Length);
                Assert.AreEqual(1, result[0].Parameters.Length);
                Assert.AreEqual("int", result[0].Parameters[0].Type);

                result = newMod.Analysis.GetSignaturesByIndex("m", pos).ToArray();
                Assert.AreEqual(6, result.Length);
                
                var members = newMod.Analysis.GetMembersByIndex("Aliased", pos, GetMemberOptions.None);
                AssertUtil.Contains(members.Select(x => x.Name), "f");
                AssertUtil.Contains(members.Select(x => x.Name), "__self__");
            }
        }

        [TestMethod, Priority(0)]
        public void OverloadDocString() {
            string code = @"
class FunctionNoRetType(object):
    def __init__(self, value):
        pass

class Aliased(object):
    '''class doc'''
    pass

def Aliased(fob):
    '''function doc'''
    pass

def Overloaded():
    '''help 1'''
    pass

def Overloaded():
    '''help 2'''
    pass

def Overloaded():
    '''help 2'''
    pass
";

            using (var newPs = SaveLoad(PythonLanguageVersion.V27, new AnalysisModule("test", "test.py", code))) {
                AssertUtil.Contains(newPs.Analyzer.GetModules().Select(x => x.Name), "test");

                string codeText = @"
import test
Aliased = test.Aliased
FunctionNoRetType = test.FunctionNoRetType
Overloaded = test.Overloaded
";
                var newMod = newPs.NewModule("baz", codeText);
                int pos = codeText.LastIndexOf('\n');

                var allMembers = newMod.Analysis.GetAllAvailableMembersByIndex(pos, GetMemberOptions.None);

                Assert.AreEqual("class doc\r\n\r\nfunction doc", allMembers.First(x => x.Name == "Aliased").Documentation);
                Assert.AreEqual(1, newMod.Analysis.GetSignaturesByIndex("FunctionNoRetType", pos).ToArray().Length);

                Assert.AreEqual("help 1\r\n\r\nhelp 2", newMod.Analysis.GetMembersByIndex("test", pos).Where(x => x.Name == "Overloaded").First().Documentation);
            }
        }

        [TestMethod, Priority(0)]
        public void Inheritance() {
            string code = @"
class WithInstanceMembers(object):
    def __init__(self):
        self.fob = 42

class WithMemberFunctions(object):
    def oar(self):
        pass

class SingleInheritance(WithMemberFunctions):
    def baz(self):
        pass

class DoubleInheritance(SingleInheritance):
    pass

class MultipleInheritance(WithInstanceMembers, WithMemberFunctions):
    pass
";

            using (var newPs = SaveLoad(PythonLanguageVersion.V27, new AnalysisModule("test", "test.py", code))) {
                AssertUtil.Contains(newPs.Analyzer.GetModules().Select(x => x.Name), "test");

                string codeText = @"
import test
WithInstanceMembers = test.WithInstanceMembers
WithMemberFunctions = test.WithMemberFunctions
SingleInheritance = test.SingleInheritance
DoubleInheritance = test.DoubleInheritance
MultipleInheritance = test.MultipleInheritance
";
                var newMod = newPs.NewModule("baz", codeText);
                int pos = codeText.LastIndexOf('\n');

                // instance attributes are present
                var instMembers = newMod.Analysis.GetMembersByIndex("WithInstanceMembers", pos);
                var fobMembers = instMembers.Where(x => x.Name == "fob");
                Assert.AreNotEqual(null, fobMembers.FirstOrDefault().Name);

                AssertUtil.ContainsAtLeast(newMod.Analysis.GetMemberNamesByIndex("WithMemberFunctions", pos), "oar");
                AssertUtil.ContainsAtLeast(newMod.Analysis.GetMemberNamesByIndex("SingleInheritance", pos), "oar", "baz");
                AssertUtil.ContainsAtLeast(newMod.Analysis.GetMemberNamesByIndex("DoubleInheritance", pos), "oar", "baz");
                AssertUtil.ContainsAtLeast(newMod.Analysis.GetMemberNamesByIndex("MultipleInheritance", pos), "fob", "oar");
            }
        }

        [TestMethod, Priority(0)]
        public void MultiplyDefinedClasses() {
            string code = @"
class MultiplyDefinedClass(object): pass
class MultiplyDefinedClass(object): pass

def ReturningMultiplyDefinedClass():
    return MultiplyDefinedClass()
";

            using (var newPs = SaveLoad(PythonLanguageVersion.V27, new AnalysisModule("test", "test.py", code))) {
                AssertUtil.Contains(newPs.Analyzer.GetModules().Select(x => x.Name), "test");

                string codeText = @"
import test
MultiplyDefinedClass = test.MultiplyDefinedClass
ReturningMultiplyDefinedClass = test.ReturningMultiplyDefinedClass
";
                var newMod = newPs.NewModule("baz", codeText);
                int pos = codeText.LastIndexOf('\n');

                var mdc = newMod.Analysis.GetValuesByIndex("MultiplyDefinedClass", pos).ToList();
                Assert.AreEqual(2, mdc.Count);

                var rmdc = newMod.Analysis.GetValuesByIndex("ReturningMultiplyDefinedClass", pos).OfType<BuiltinFunctionInfo>().Single();
                AssertUtil.ContainsExactly(rmdc.Function.Overloads.Single().ReturnType, mdc.Select(p => p.PythonType));
            }
        }

        [TestMethod, Priority(0)]
        public void RecursionClasses() {
            string code = @"
class C(object): pass

C.abc = C
";
            using (var newPs = SaveLoad(PythonLanguageVersion.V27, new AnalysisModule("test", "test.py", code))) {
            }
        }

        [TestMethod, Priority(0)]
        public void RecursionSequenceClasses() {
            string code = @"
C = []
C.append(C)
";
            using (var newPs = SaveLoad(PythonLanguageVersion.V27, new AnalysisModule("test", "test.py", code))) {
                string code2 = @"import test
C = test.C";
                var newMod = newPs.NewModule("test2", code2);

                AssertUtil.ContainsExactly(newMod.Analysis.GetShortDescriptionsByIndex("C", 0), "list of list");
            }
        }

        [TestMethod, Priority(0)]
        public void ModuleRef() {
            string fob = @"
import oar
x = oar.f()
";
            string oar = @"
def f(): return 42";

            using (var newPs = SaveLoad(PythonLanguageVersion.V27, new AnalysisModule("fob", "fob.py", fob), new AnalysisModule("oar", "oar.py", oar))) {
                string code = @"
import fob
abc = fob.x
";
                var newMod = newPs.NewModule("baz", code);

                Assert.AreEqual(newMod.Analysis.GetValuesByIndex("abc", code.LastIndexOf('\n')).First().PythonType, newPs.Analyzer.Interpreter.GetBuiltinType(BuiltinTypeId.Int));
            }
        }

        [TestMethod, Priority(0)]
        public void CrossModuleTypeRef() {
            string fob = @"
class Fob(object):
    pass
";
            string oar = @"
from fob import *
";

            string baz = @"
from oar import *
";

            using (var newPs = SaveLoad(
                PythonLanguageVersion.V27,
                new AnalysisModule("fob", "fob.py", fob),
                new AnalysisModule("oar", "oar.py", oar),
                new AnalysisModule("baz", "baz.py", baz)
            )) {
                string code = @"
import oar, baz
oar_Fob = oar.Fob
baz_Fob = baz.Fob
";
                var newMod = newPs.NewModule("fez", code);

                AssertUtil.ContainsExactly(newMod.Analysis.GetShortDescriptionsByIndex("oar_Fob", 0), "class Fob");
                AssertUtil.ContainsExactly(newMod.Analysis.GetShortDescriptionsByIndex("baz_Fob", 0), "class Fob");
            }
        }

        [TestMethod, Priority(0)]
        public void FunctionOverloads() {
            string code = @"
def f(a, b):
    return a * b

f(1, 2)
f(3, 'abc')
f([1, 2], 3)
";
            using (var newPs = SaveLoad(PythonLanguageVersion.V27, new AnalysisModule("test", "test.py", code))) {
                var newMod = newPs.NewModule("test2", "from test import f; x = f()");

                AssertUtil.ContainsExactly(newMod.Analysis.GetShortDescriptionsByIndex("x", 0), "int", "str", "list of int");
            }
        }

        [TestMethod, Priority(0)]
        public void StandaloneMethods() {
            string code = @"
class A(object):
    def f(self, a, b):
        pass

cls_f = A.f
inst_f = A().f
";
            using (var newPs = SaveLoad(PythonLanguageVersion.V27, new AnalysisModule("test", "test.py", code))) {
                var newMod = newPs.NewModule("test2", "from test import cls_f, inst_f");

                var clsSig = newMod.Analysis.GetSignaturesByIndex("cls_f", 0).Single();
                var instSig = newMod.Analysis.GetSignaturesByIndex("inst_f", 0).Single();

                var clsProto = string.Format("cls_f({0})", string.Join(", ", clsSig.Parameters.Select(p => p.Name)));
                var instProto = string.Format("inst_f({0})", string.Join(", ", instSig.Parameters.Select(p => p.Name)));
                Console.WriteLine(clsProto);
                Console.WriteLine(instProto);

                Assert.AreEqual(3, clsSig.Parameters.Length, clsProto);
                Assert.AreEqual(2, instSig.Parameters.Length, instProto);

                Assert.AreEqual("self", clsSig.Parameters[0].Name);
                Assert.AreEqual("a", clsSig.Parameters[1].Name);
                Assert.AreEqual("b", clsSig.Parameters[2].Name);

                Assert.AreEqual("a", instSig.Parameters[0].Name);
                Assert.AreEqual("b", instSig.Parameters[1].Name);
            }
        }

        [TestMethod, Priority(0)]
        public void DefaultParameters() {
            var code = @"
def f(x = None): pass

def g(x = {}): pass

def h(x = {2:3}): pass

def i(x = []): pass

def j(x = [None]): pass

def k(x = ()): pass

def l(x = (2, )): pass

def m(x = math.atan2(1, 0)): pass
";

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

            using (var newPs = SaveLoad(PythonLanguageVersion.V27, new AnalysisModule("test", "test.py", code))) {
                var entry = newPs.NewModule("test2", "from test import *");
                foreach (var test in tests) {
                    var result = entry.Analysis.GetSignaturesByIndex(test.FuncName, 1).ToArray();
                    Assert.AreEqual(result.Length, 1);
                    Assert.AreEqual(result[0].Parameters.Length, 1);
                    Assert.AreEqual(result[0].Parameters[0].Name, test.ParamName);
                    Assert.AreEqual(result[0].Parameters[0].DefaultValue, test.DefaultValue);
                }
            }
        }

        [TestMethod, Priority(0)]
        public void ChildModuleThroughSysModules() {
            var code = @"# This is what the os module does

import test_import_1
import test_import_2
import sys

sys.modules['test.imported'] = test_import_1
sys.modules['test.imported'] = test_import_2
";

            using (var newPs = SaveLoad(PythonLanguageVersion.V27,
                new AnalysisModule("test", "test.py", code),
                new AnalysisModule("test_import_1", "test_import_1.py", "n = 1"),
                new AnalysisModule("test_import_2", "test_import_2.py", "f = 3.1415")
            )) {
                var entry = newPs.NewModule("test2", "import test as t; import test.imported as p");
                AssertUtil.ContainsExactly(
                    entry.Analysis.GetMemberNamesByIndex("t", 0),
                    "test_import_1",
                    "test_import_2",
                    "imported"
                );
                var p = entry.Analysis.GetValuesByIndex("p", 0).ToList();
                Assert.AreEqual(2, p.Count);
                Assert.IsInstanceOfType(p[0], typeof(BuiltinModule));
                Assert.IsInstanceOfType(p[1], typeof(BuiltinModule));
                AssertUtil.ContainsExactly(p.Select(m => m.Name), "test_import_1", "test_import_2");

                AssertUtil.ContainsExactly(entry.Analysis.GetTypeIdsByIndex("p.n", 0), BuiltinTypeId.Int);
                AssertUtil.ContainsExactly(entry.Analysis.GetTypeIdsByIndex("p.f", 0), BuiltinTypeId.Float);
            }
        }

        [TestMethod, Priority(0)]
        public void SpecializedCallableWithNoOriginal() {
            string code = @"
import unittest

# skipIf is specialized and calling it returns a callable
# with no original value to save.
x = unittest.skipIf(False)
";
            using (var newPs = SaveLoad(PythonLanguageVersion.V27, new AnalysisModule("test", "test.py", code))) {
                var entry = newPs.NewModule("test2", "import test; x = test.x");
                AssertUtil.ContainsExactly(entry.Analysis.GetTypeIdsByIndex("x", 0));
            }
        }

        private SaveLoadResult SaveLoad(PythonLanguageVersion version, params AnalysisModule[] modules) {
            IPythonProjectEntry[] entries = new IPythonProjectEntry[modules.Length];

            var fact = InterpreterFactory;
            var interp = Interpreter;
            if (version != fact.GetLanguageVersion()) {
                fact = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(version.ToVersion());
                interp = fact.CreateInterpreter();
            }

            var codeFolder = TestData.GetTempPath(randomSubPath: true);
            var dbFolder = Path.Combine(codeFolder, "DB");
            Directory.CreateDirectory(codeFolder);
            Directory.CreateDirectory(dbFolder);

            var state = PythonAnalyzer.CreateSynchronously(fact, interp, SharedDatabaseState.BuiltinName2x);
            for (int i = 0; i < modules.Length; i++) {
                var fullname = Path.Combine(codeFolder, modules[i].Filename);
                File.WriteAllText(fullname, modules[i].Code);
                entries[i] = state.AddModule(modules[i].ModuleName, fullname);
                Prepare(entries[i], new StringReader(modules[i].Code), version);
            }

            for (int i = 0; i < modules.Length; i++) {
                entries[i].Analyze(CancellationToken.None, false);
            }

            new SaveAnalysis().Save(state, dbFolder);

            File.Copy(
                Path.Combine(PythonTypeDatabase.BaselineDatabasePath, "__builtin__.idb"),
                Path.Combine(dbFolder, "__builtin__.idb"),
                true
            );

            var loadFactory = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(
                version.ToVersion(),
                null,
                dbFolder
            );
            return new SaveLoadResult(PythonAnalyzer.CreateSynchronously(loadFactory), codeFolder);
        }

        class AnalysisModule {
            public readonly string ModuleName;
            public readonly string Code;
            public readonly string Filename;

            public AnalysisModule(string moduleName, string filename, string code) {
                ModuleName = moduleName;
                Code = code;
                Filename = filename;
            }
        }

        class SaveLoadResult : IDisposable {
            private readonly string _dir;
            public readonly PythonAnalyzer Analyzer;

            public SaveLoadResult(PythonAnalyzer analyzer, string dir) {
                Analyzer = analyzer;
                _dir = dir;
            }

            public IPythonProjectEntry NewModule(string name, string code) {
                var entry = Analyzer.AddModule(name, name + ".py");
                Prepare(entry, new StringReader(code), PythonLanguageVersion.V27);
                entry.Analyze(CancellationToken.None);
                return entry;
            }

            #region IDisposable Members

            public void Dispose() {
                //Directory.Delete(_dir, true);
            }

            #endregion
        }

    }
}

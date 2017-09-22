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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

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
                AssertUtil.Contains(newPs.Analyzer.Analyzer.GetModules().Select(x => x.Name), "test");

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
                Assert.AreEqual("function f1(x = 42)", newMod.Analysis.GetValuesByIndex("f1", pos).First().Description);
                Assert.AreEqual("bound method x", newMod.Analysis.GetValuesByIndex("dx", pos).First().Description);
                Assert.AreEqual("function test.C.g(self)", newMod.Analysis.GetValuesByIndex("scg", pos).First().Description);
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

def Overloaded(a):
    '''help 1'''
    pass

def Overloaded(a, b):
    '''help 2'''
    pass

def Overloaded(a, b):
    '''help 2'''
    pass
";

            using (var newPs = SaveLoad(PythonLanguageVersion.V27, new AnalysisModule("test", "test.py", code))) {
                AssertUtil.Contains(newPs.Analyzer.Analyzer.GetModules().Select(x => x.Name), "test");

                string codeText = @"
import test
Aliased = test.Aliased
FunctionNoRetType = test.FunctionNoRetType
Overloaded = test.Overloaded
";
                var newMod = newPs.NewModule("baz", codeText);
                int pos = codeText.LastIndexOf('\n');

                var allMembers = newMod.Analysis.GetAllAvailableMembersByIndex(pos, GetMemberOptions.None);

                Assert.AreEqual("class test.Aliased\r\nclass doc\r\n\r\nfunction Aliased(fob)\r\nfunction doc", allMembers.First(x => x.Name == "Aliased").Documentation);
                newPs.Analyzer.AssertHasParameters("FunctionNoRetType", "value");

                //var doc = newMod.Analysis.GetMembersByIndex("test", pos).Where(x => x.Name == "Overloaded").First();
                // help 2 should be first because it has more parameters
                //Assert.AreEqual("function Overloaded(a, b)\r\nhelp 2\r\n\r\nfunction Overloaded(a)\r\nhelp 1", doc.Documentation);
                AssertUtil.ContainsExactly(newPs.Analyzer.GetDescriptions("test.Overloaded"), "function Overloaded(a, b)\r\nhelp 2", "function Overloaded(a)\r\nhelp 1");
            }
        }

        [TestMethod, Priority(1)]
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
                AssertUtil.Contains(newPs.Analyzer.Analyzer.GetModules().Select(x => x.Name), "test");

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

                newPs.Analyzer.AssertHasAttr("WithMemberFunctions", "oar");
                newPs.Analyzer.AssertHasAttr("SingleInheritance", "oar", "baz");
                newPs.Analyzer.AssertHasAttr("DoubleInheritance", "oar", "baz");
                newPs.Analyzer.AssertHasAttr("MultipleInheritance", "fob", "oar");
            }
        }

        [TestMethod, Priority(1)]
        public void MultiplyDefinedClasses() {
            string code = @"
class MultiplyDefinedClass(object): pass
class MultiplyDefinedClass(object): pass

def ReturningMultiplyDefinedClass():
    return MultiplyDefinedClass()
";

            using (var newPs = SaveLoad(PythonLanguageVersion.V27, new AnalysisModule("test", "test.py", code))) {
                AssertUtil.Contains(newPs.Analyzer.Analyzer.GetModules().Select(x => x.Name), "test");

                string codeText = @"
import test
MultiplyDefinedClass = test.MultiplyDefinedClass
ReturningMultiplyDefinedClass = test.ReturningMultiplyDefinedClass
";
                var newMod = newPs.NewModule("baz", codeText);

                var mdc = newPs.Analyzer.GetValues("MultiplyDefinedClass");
                Assert.AreEqual(2, mdc.Length);

                var rmdc = newPs.Analyzer.GetValue<BuiltinFunctionInfo>("ReturningMultiplyDefinedClass");
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
                newPs.NewModule("test2", code2);
                newPs.Analyzer.AssertDescription("C", "list of list");
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
                newPs.NewModule("baz", code);
                newPs.Analyzer.AssertIsInstance("abc", BuiltinTypeId.Int);
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
                newPs.NewModule("fez", code);

                newPs.Analyzer.AssertDescription("oar_Fob", "class fob.Fob");
                newPs.Analyzer.AssertDescription("baz_Fob", "class fob.Fob");
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
                newPs.NewModule("test2", "from test import f; x = f()");
                newPs.Analyzer.AssertIsInstance("x", BuiltinTypeId.Int, BuiltinTypeId.Str, BuiltinTypeId.List);
                var lst = newPs.Analyzer.GetValues("x")
                    .OfType<SequenceBuiltinInstanceInfo>()
                    .Where(s => s.TypeId == BuiltinTypeId.List)
                    .Single();
                Assert.AreEqual("list of int", lst.ShortDescription);
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

                newPs.Analyzer.AssertHasParameters("cls_f", "self", "a", "b");
                newPs.Analyzer.AssertHasParameters("inst_f", "a", "b");
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

            var fact = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(version.ToVersion());
            var interp = fact.CreateInterpreter();

            var dbFolder = TestData.GetTempPath();

            var state = new PythonAnalysis(fact, interp, SharedDatabaseState.BuiltinName2x);
            state.CreateProjectOnDisk = true;
            for (int i = 0; i < modules.Length; i++) {
                state.AddModule(modules[i].ModuleName, modules[i].Code, modules[i].Filename);
            }

            state.WaitForAnalysis();

            new SaveAnalysis().Save(state.Analyzer, dbFolder);

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
            return new SaveLoadResult(CreateAnalyzer(loadFactory), state.CodeFolder);
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
            public readonly PythonAnalysis Analyzer;

            public SaveLoadResult(PythonAnalysis analyzer, string dir) {
                Analyzer = analyzer;
                _dir = dir;
            }

            public IPythonProjectEntry NewModule(string name, string code) {
                var entry = Analyzer.AddModule(name, code);
                Analyzer.WaitForAnalysis();
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

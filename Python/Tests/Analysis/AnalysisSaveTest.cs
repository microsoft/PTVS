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
using TestUtilities;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace AnalysisTests {
    [TestClass]
    public class AnalysisSaveTest : BaseAnalysisTest {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            TestData.Deploy();
        }

        [TestMethod, Priority(0)]
        public void SaveLoad() {
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
foo = int

class X(object): pass
class Y(object): pass

union = X()
union = Y()

list_of_int = [1, 2, 3]
tuple_of_str = 'a', 'b', 'c'

import nt
m = max

class FunctionNoRetType(object):
    def __init__(self, value):
        pass
        
class Aliased(object):
    '''class doc'''
    def f(self):
        pass
        
def Aliased(foo):
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

class WithInstanceMembers(object):
    def __init__(self):
        self.foo = 42

class WithMemberFunctions(object):
    def bar(self):
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
abc = test.abc
foo = test.foo
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
FunctionNoRetType = test.FunctionNoRetType
Overloaded = test.Overloaded
WithInstanceMembers = test.WithInstanceMembers
WithMemberFunctions = test.WithMemberFunctions
SingleInheritance = test.SingleInheritance
DoubleInheritance = test.DoubleInheritance
MultipleInheritance = test.MultipleInheritance
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

                AssertUtil.ContainsExactly(newMod.Analysis.GetShortDescriptionsByIndex("foo", pos), "type int");

                var result = newMod.Analysis.GetSignaturesByIndex("f1", pos).ToArray();
                Assert.AreEqual(1, result.Length);
                Assert.AreEqual(1, result[0].Parameters.Length);
                Assert.AreEqual("int", result[0].Parameters[0].Type);

                result = newMod.Analysis.GetSignaturesByIndex("m", pos).ToArray();
                Assert.AreEqual(6, result.Length);
                
                var members = newMod.Analysis.GetMembersByIndex("Aliased", pos, GetMemberOptions.None);
                AssertUtil.Contains(members.Select(x => x.Name), "f");
                AssertUtil.Contains(members.Select(x => x.Name), "__self__");

                var allMembers = newMod.Analysis.GetAllAvailableMembersByIndex(pos, GetMemberOptions.None);

                Assert.AreEqual("class doc\r\n\r\nfunction doc", allMembers.First(x => x.Name == "Aliased").Documentation);
                Assert.AreEqual(1, newMod.Analysis.GetSignaturesByIndex("FunctionNoRetType", pos).ToArray().Length);

                Assert.AreEqual("help 1\r\n\r\nhelp 2", newMod.Analysis.GetMembersByIndex("test", pos).Where(x => x.Name == "Overloaded").First().Documentation);

                // instance attributes are present
                var instMembers = newMod.Analysis.GetMembersByIndex("WithInstanceMembers", pos);
                var fooMembers = instMembers.Where(x => x.Name == "foo");
                Assert.AreNotEqual(null, fooMembers.FirstOrDefault().Name);

                AssertUtil.ContainsAtLeast(newMod.Analysis.GetMemberNamesByIndex("WithMemberFunctions", pos), "bar");
                AssertUtil.ContainsAtLeast(newMod.Analysis.GetMemberNamesByIndex("SingleInheritance", pos), "bar", "baz");
                AssertUtil.ContainsAtLeast(newMod.Analysis.GetMemberNamesByIndex("DoubleInheritance", pos), "bar", "baz");
                AssertUtil.ContainsAtLeast(newMod.Analysis.GetMemberNamesByIndex("MultipleInheritance", pos), "foo", "bar");
            }
        }

        [TestMethod, Priority(0)]
        public void SaveRecursionClasses() {
            string code = @"
class C(object): pass

C.abc = C
";
            using (var newPs = SaveLoad(PythonLanguageVersion.V27, new AnalysisModule("test", "test.py", code))) {
            }
        }

        [TestMethod, Priority(0)]
        public void SaveRecursionSequenceClasses() {
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
        public void SaveModuleRef() {
            string foo = @"
import bar
x = bar.f()
";
            string bar = @"
def f(): return 42";

            using (var newPs = SaveLoad(PythonLanguageVersion.V27, new AnalysisModule("foo", "foo.py", foo), new AnalysisModule("bar", "bar.py", bar))) {
                string code = @"
import foo
abc = foo.x
";
                var newMod = newPs.NewModule("baz", code);

                Assert.AreEqual(newMod.Analysis.GetValuesByIndex("abc", code.LastIndexOf('\n')).First().PythonType, newPs.Analyzer.Interpreter.GetBuiltinType(BuiltinTypeId.Int));
            }
        }

        [TestMethod, Priority(0)]
        public void SaveFunctionOverloads() {
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



        private SaveLoadResult SaveLoad(PythonLanguageVersion version, params AnalysisModule[] modules) {
            IPythonProjectEntry[] entries = new IPythonProjectEntry[modules.Length];

            var state = new PythonAnalyzer(Interpreter, version);
            for (int i = 0; i < modules.Length; i++) {
                entries[i] = state.AddModule(modules[i].ModuleName, modules[i].Filename);
                Prepare(entries[i], new StringReader(modules[i].Code), version);

            }

            for (int i = 0; i < modules.Length; i++) {
                entries[i].Analyze(CancellationToken.None);
            }

            string tmpFolder = Path.Combine(Path.GetTempPath(), "6666d700-a6d8-4e11-8b73-3ba99a61e27b" /*Guid.NewGuid().ToString()*/);
            Directory.CreateDirectory(tmpFolder);

            new SaveAnalysis().Save(state, tmpFolder);

            File.Copy(Path.Combine(PythonTypeDatabase.BaselineDatabasePath, "__builtin__.idb"), Path.Combine(tmpFolder, "__builtin__.idb"), true);

            return new SaveLoadResult(
                new PythonAnalyzer(InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(version.ToVersion(), tmpFolder)),
                tmpFolder
            );
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

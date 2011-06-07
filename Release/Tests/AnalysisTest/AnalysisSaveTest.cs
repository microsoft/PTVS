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
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace AnalysisTest {
    [TestClass]
    [DeploymentItem(@"..\\PythonTools\\CompletionDB\\", "CompletionDB")]
    public class AnalysisSaveTest : BaseAnalysisTest {        
        
        [TestMethod]
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

import nt
m = max
";

            using (var newPs = SaveLoad(new AnalysisModule("test", "test.py", code))) {
                AssertContains(newPs.Analyzer.GetModules().Select(x => x.Name), "test");

                var newMod = newPs.NewModule("baz", @"
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
f1 = test.f1
f2 = test.f2
m = test.m
");

                Assert.AreEqual(newPs.Analyzer.Interpreter.GetBuiltinType(BuiltinTypeId.Int), newMod.Analysis.GetValues("abc", 3).First().PythonType);
                Assert.AreEqual(newPs.Analyzer.Interpreter.GetBuiltinType(BuiltinTypeId.Int), newMod.Analysis.GetValues("cf", 3).First().PythonType);
                Assert.AreEqual(newPs.Analyzer.Interpreter.GetBuiltinType(BuiltinTypeId.Int), newMod.Analysis.GetValues("cg", 3).First().PythonType);
                Assert.AreEqual("function f1", newMod.Analysis.GetValues("f1", 3).First().Description);
                Assert.AreEqual("bound method x", newMod.Analysis.GetValues("dx", 3).First().Description);
                Assert.AreEqual("function g", newMod.Analysis.GetValues("scg", 3).First().Description);
                var unionMembers = new List<IAnalysisValue>(newMod.Analysis.GetValues("union", 3));
                Assert.AreEqual(unionMembers.Count, 2);
                AssertContainsExactly(unionMembers.Select(x => x.PythonType.Name), "X", "Y");

                Assert.AreEqual("type int", newMod.Analysis.GetValues("foo", 3).First().ShortDescription);

                var result = newMod.Analysis.GetSignatures("f1", 3).ToArray();
                Assert.AreEqual(result.Length, 1);
                Assert.AreEqual(result[0].Parameters.Length, 1);
                Assert.AreEqual(result[0].Parameters[0].Type, "int");

                result = newMod.Analysis.GetSignatures("m", 3).ToArray();
                Assert.AreEqual(result.Length, 6);
            }
        }

        [TestMethod]
        public void SaveRecursionClasses() {
            string code = @"
class C(object): pass

C.abc = C
";
            using (var newPs = SaveLoad(new AnalysisModule("test", "test.py", code))) {
            }
        }

        [TestMethod]
        public void SaveModuleRef() {
            string foo = @"
import bar
x = bar.f()
";
            string bar = @"
def f(): return 42";

            using (var newPs = SaveLoad(new AnalysisModule("foo", "foo.py", foo), new AnalysisModule("bar", "bar.py", bar))) {
                var newMod = newPs.NewModule("baz", @"
import foo
abc = foo.x
");

                Assert.AreEqual(newMod.Analysis.GetValues("abc", 3).First().PythonType, newPs.Analyzer.Interpreter.GetBuiltinType(BuiltinTypeId.Int));
            }
        }

        private SaveLoadResult SaveLoad(params AnalysisModule[] modules) {
            IPythonProjectEntry[] entries = new IPythonProjectEntry[modules.Length];

            var state = new PythonAnalyzer(Interpreter, PythonLanguageVersion.V27);
            for (int i = 0; i < modules.Length; i++) {
                entries[i] = state.AddModule(modules[i].ModuleName, modules[i].Filename);
                Prepare(entries[i], new StringReader(modules[i].Code), PythonLanguageVersion.V27);

            }

            for (int i = 0; i < modules.Length; i++) {
                entries[i].Analyze();
            }

            string tmpFolder = Path.Combine(Path.GetTempPath(), "6666d700-a6d8-4e11-8b73-3ba99a61e27b" /*Guid.NewGuid().ToString()*/);
            Directory.CreateDirectory(tmpFolder);

            new SaveAnalysis().Save(state, tmpFolder);

            File.Copy(Path.Combine(PythonTypeDatabase.GetBaselineDatabasePath(), "__builtin__.idb"), Path.Combine(tmpFolder, "__builtin__.idb"), true);

            return new SaveLoadResult(
                new PythonAnalyzer(new CPythonInterpreter(new PythonTypeDatabase(tmpFolder)), PythonLanguageVersion.V27),
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
                entry.Analyze();
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

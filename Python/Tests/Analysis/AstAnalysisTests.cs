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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Ast;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;
using Ast = Microsoft.PythonTools.Parsing.Ast;

namespace AnalysisTests {
    [TestClass]
    public class AstAnalysisTests {
        [TestInitialize]
        public void TestInitialize() => TestEnvironmentImpl.TestInitialize();

        public TestContext TestContext { get; set; }

        private string _analysisLog;
        private string _moduleCache;

        public AstAnalysisTests() {
            _moduleCache = null;
        }

        [TestCleanup]
        public void Cleanup() {
            if (TestContext.CurrentTestOutcome != UnitTestOutcome.Passed) {
                if (_analysisLog != null) {
                    Console.WriteLine("Analysis log:");
                    Console.WriteLine(_analysisLog);
                }

                if (_moduleCache != null) {
                    Console.WriteLine("Module cache:");
                    Console.WriteLine(_moduleCache);
                }
            }

            TestEnvironmentImpl.TestCleanup();
        }

        private static PythonAnalysis CreateAnalysis(PythonVersion version) {
            version.AssertInstalled();
            var opts = new InterpreterFactoryCreationOptions {
                DatabasePath = TestData.GetTempPath("AstAnalysisCache"),
                UseExistingCache = false
            };

            Trace.TraceInformation("Cache Path: " + opts.DatabasePath);

            return new PythonAnalysis(() => new AstPythonInterpreterFactory(
                version.Configuration,
                opts
            ));
        }

        private static PythonAnalysis CreateAnalysis() {
            return CreateAnalysis(PythonPaths.Versions.OrderByDescending(p => p.Version).FirstOrDefault());
        }

        #region Test cases

        [TestMethod, Priority(0)]
        public void AstClasses() {
            var mod = Parse("Classes.py", PythonLanguageVersion.V35);
            AssertUtil.ContainsExactly(mod.GetMemberNames(null),
                "C1", "C2", "C3", "C4", "C5",
                "D", "E",
                "F1",
                "f"
            );

            Assert.IsInstanceOfType(mod.GetMember(null, "C1"), typeof(AstPythonType));
            Assert.IsInstanceOfType(mod.GetMember(null, "C2"), typeof(AstPythonType));
            Assert.IsInstanceOfType(mod.GetMember(null, "C3"), typeof(AstPythonType));
            Assert.IsInstanceOfType(mod.GetMember(null, "C4"), typeof(AstPythonType));
            Assert.IsInstanceOfType(mod.GetMember(null, "C5"), typeof(AstPythonType));
            Assert.IsInstanceOfType(mod.GetMember(null, "D"), typeof(AstPythonType));
            Assert.IsInstanceOfType(mod.GetMember(null, "E"), typeof(AstPythonType));
            Assert.IsInstanceOfType(mod.GetMember(null, "F1"), typeof(AstPythonType));
            Assert.IsInstanceOfType(mod.GetMember(null, "f"), typeof(AstPythonFunction));

            var C1 = (IPythonType)mod.GetMember(null, "C1");
            Assert.AreEqual("C1", C1.Documentation);

            var C5 = (IPythonType)mod.GetMember(null, "C5");
            Assert.AreEqual("C1", C5.Documentation);

            var F1 = (IMemberContainer)mod.GetMember(null, "F1");
            AssertUtil.ContainsExactly(F1.GetMemberNames(null),
                "F2", "F3", "F6", "__class__", "__bases__"
            );
            var F6 = (IPythonType)F1.GetMember(null, "F6");
            Assert.AreEqual("C1", F6.Documentation);

            Assert.IsInstanceOfType(F1.GetMember(null, "F2"), typeof(AstPythonType));
            Assert.IsInstanceOfType(F1.GetMember(null, "F3"), typeof(AstPythonType));
            Assert.IsInstanceOfType(F1.GetMember(null, "__class__"), typeof(AstPythonType));
            Assert.IsInstanceOfType(F1.GetMember(null, "__bases__"), typeof(AstPythonSequence));
        }

        [TestMethod, Priority(0)]
        public void AstFunctions() {
            var mod = Parse("Functions.py", PythonLanguageVersion.V35);
            AssertUtil.ContainsExactly(mod.GetMemberNames(null),
                "f", "f2", "g", "h",
                "C"
            );

            Assert.IsInstanceOfType(mod.GetMember(null, "f"), typeof(AstPythonFunction));
            Assert.IsInstanceOfType(mod.GetMember(null, "f2"), typeof(AstPythonFunction));
            Assert.IsInstanceOfType(mod.GetMember(null, "g"), typeof(AstPythonFunction));
            Assert.IsInstanceOfType(mod.GetMember(null, "h"), typeof(AstPythonFunction));
            Assert.IsInstanceOfType(mod.GetMember(null, "C"), typeof(AstPythonType));

            var f = (IPythonFunction)mod.GetMember(null, "f");
            Assert.AreEqual("f", f.Documentation);

            var f2 = (IPythonFunction)mod.GetMember(null, "f2");
            Assert.AreEqual("f", f2.Documentation);

            var C = (IMemberContainer)mod.GetMember(null, "C");
            AssertUtil.ContainsExactly(C.GetMemberNames(null),
                "i", "j", "C2", "__class__", "__bases__"
            );

            Assert.IsInstanceOfType(C.GetMember(null, "i"), typeof(AstPythonFunction));
            Assert.IsInstanceOfType(C.GetMember(null, "j"), typeof(AstPythonFunction));
            Assert.IsInstanceOfType(C.GetMember(null, "C2"), typeof(AstPythonType));
            Assert.IsInstanceOfType(C.GetMember(null, "__class__"), typeof(AstPythonType));
            Assert.IsInstanceOfType(C.GetMember(null, "__bases__"), typeof(AstPythonSequence));

            var C2 = (IMemberContainer)C.GetMember(null, "C2");
            AssertUtil.ContainsExactly(C2.GetMemberNames(null),
                "k", "__class__", "__bases__"
            );

            Assert.IsInstanceOfType(C2.GetMember(null, "k"), typeof(AstPythonFunction));
            Assert.IsInstanceOfType(C2.GetMember(null, "__class__"), typeof(AstPythonType));
            Assert.IsInstanceOfType(C2.GetMember(null, "__bases__"), typeof(AstPythonSequence));
        }

        [TestMethod, Priority(0)]
        public void AstValues() {
            using (var entry = CreateAnalysis()) {
                try {
                    entry.SetSearchPaths(TestData.GetPath(@"TestData\AstAnalysis"));
                    entry.AddModule("test-module", "from Values import *");
                    entry.WaitForAnalysis();

                    entry.AssertHasAttr("",
                        "x", "y", "z", "pi", "l", "t", "d", "s",
                        "X", "Y", "Z", "PI", "L", "T", "D", "S"
                    );

                    entry.AssertIsInstance("x", BuiltinTypeId.Int);
                    entry.AssertIsInstance("y", BuiltinTypeId.Str);
                    entry.AssertIsInstance("z", BuiltinTypeId.Bytes);
                    entry.AssertIsInstance("pi", BuiltinTypeId.Float);
                    entry.AssertIsInstance("l", BuiltinTypeId.List);
                    entry.AssertIsInstance("t", BuiltinTypeId.Tuple);
                    entry.AssertIsInstance("d", BuiltinTypeId.Dict);
                    entry.AssertIsInstance("s", BuiltinTypeId.Set);
                    entry.AssertIsInstance("X", BuiltinTypeId.Int);
                    entry.AssertIsInstance("Y", BuiltinTypeId.Str);
                    entry.AssertIsInstance("Z", BuiltinTypeId.Bytes);
                    entry.AssertIsInstance("PI", BuiltinTypeId.Float);
                    entry.AssertIsInstance("L", BuiltinTypeId.List);
                    entry.AssertIsInstance("T", BuiltinTypeId.Tuple);
                    entry.AssertIsInstance("D", BuiltinTypeId.Dict);
                    entry.AssertIsInstance("S", BuiltinTypeId.Set);
                } finally {
                    _analysisLog = entry.GetLogContent(CultureInfo.InvariantCulture);
                }
            }
        }

        [TestMethod, Priority(0)]
        public void AstMultiValues() {
            using (var entry = CreateAnalysis()) {
                try {
                    entry.SetSearchPaths(TestData.GetPath(@"TestData\AstAnalysis"));
                    entry.AddModule("test-module", "from MultiValues import *");
                    entry.WaitForAnalysis();

                    entry.AssertHasAttr("",
                        "x", "y", "z", "l", "t", "s",
                        "XY", "XYZ", "D"
                    );

                    entry.AssertIsInstance("x", BuiltinTypeId.Int);
                    entry.AssertIsInstance("y", BuiltinTypeId.Str);
                    entry.AssertIsInstance("z", BuiltinTypeId.Bytes);
                    entry.AssertIsInstance("l", BuiltinTypeId.List);
                    entry.AssertIsInstance("t", BuiltinTypeId.Tuple);
                    entry.AssertIsInstance("s", BuiltinTypeId.Set);
                    entry.AssertIsInstance("XY", BuiltinTypeId.Int, BuiltinTypeId.Str);
                    entry.AssertIsInstance("XYZ", BuiltinTypeId.Int, BuiltinTypeId.Str, BuiltinTypeId.Bytes);
                    entry.AssertIsInstance("D", BuiltinTypeId.List, BuiltinTypeId.Tuple, BuiltinTypeId.Dict, BuiltinTypeId.Set);
                } finally {
                    _analysisLog = entry.GetLogContent(CultureInfo.InvariantCulture);
                }
            }
        }

        [TestMethod, Priority(0)]
        public void AstImports() {
            var mod = Parse("Imports.py", PythonLanguageVersion.V35);
            AssertUtil.ContainsExactly(mod.GetMemberNames(null),
                "version_info", "a_made_up_module"
            );
        }

        [TestMethod, Priority(0)]
        public void AstReturnTypes() {
            using (var entry = CreateAnalysis()) {
                try {
                    entry.SetSearchPaths(TestData.GetPath(@"TestData\AstAnalysis"));
                    entry.AddModule("test-module", @"from ReturnValues import *
R_str = r_str()
R_object = r_object()
R_A1 = A()
R_A2 = A.r_A()
R_A3 = R_A1.r_A()");
                    entry.WaitForAnalysis();

                    entry.AssertHasAttr("",
                        "r_a", "r_b", "r_str", "r_object", "A",
                        "R_str", "R_object", "R_A1", "R_A2", "R_A3"
                    );

                    entry.AssertIsInstance("R_str", BuiltinTypeId.Str);
                    entry.AssertIsInstance("R_object", BuiltinTypeId.Object);
                    entry.AssertIsInstance("R_A1", BuiltinTypeId.Type);
                    entry.AssertIsInstance("R_A2", BuiltinTypeId.Type);
                    entry.AssertIsInstance("R_A3", BuiltinTypeId.Type);
                    entry.AssertDescription("R_A1", "A");
                    entry.AssertDescription("R_A2", "A");
                    entry.AssertDescription("R_A3", "A");
                } finally {
                    _analysisLog = entry.GetLogContent(CultureInfo.InvariantCulture);
                }
            }
        }

        [TestMethod, Priority(0)]
        public void AstInstanceMembers() {
            using (var entry = CreateAnalysis()) {
                try {
                    entry.SetSearchPaths(TestData.GetPath(@"TestData\AstAnalysis"));
                    entry.AddModule("test-module", "from InstanceMethod import f1, f2");
                    entry.WaitForAnalysis();

                    entry.AssertHasAttr("", "f1", "f2");

                    entry.AssertIsInstance("f1", BuiltinTypeId.BuiltinFunction);
                    entry.AssertIsInstance("f2", BuiltinTypeId.BuiltinMethodDescriptor);

                    var func = entry.GetValue<BuiltinFunctionInfo>("f1");
                    var method = entry.GetValue<BoundBuiltinMethodInfo>("f2");
                } finally {
                    _analysisLog = entry.GetLogContent(CultureInfo.InvariantCulture);
                }
            }
        }
        [TestMethod, Priority(0)]
        public void AstInstanceMembers_Random() {
            using (var entry = CreateAnalysis()) {
                try {
                    entry.AddModule("test-module", "from random import *");
                    entry.WaitForAnalysis();

                    foreach (var fnName in new[] { "seed", "randrange", "gauss" }) {
                        entry.AssertIsInstance(fnName, BuiltinTypeId.BuiltinMethodDescriptor);
                        var func = entry.GetValue<BoundBuiltinMethodInfo>(fnName);
                        Assert.AreNotEqual(0, func.Overloads.Count(), $"{fnName} overloads");
                        Assert.AreNotEqual(0, func.Overloads.ElementAt(0).Parameters.Length, $"{fnName} parameters");
                    }
                } finally {
                    _analysisLog = entry.GetLogContent(CultureInfo.InvariantCulture);
                }
            }
        }

        [TestMethod, Priority(0)]
        public void AstLibraryMembers_Datetime() {
            using (var entry = CreateAnalysis()) {
                try {
                    entry.AddModule("test-module", "import datetime");
                    entry.WaitForAnalysis();

                    var dtClass = entry.GetTypes("datetime.datetime").FirstOrDefault(t => t.MemberType == PythonMemberType.Class && t.Name == "datetime");
                    Assert.IsNotNull(dtClass);

                    var dayProperty = dtClass.GetMember(entry.ModuleContext, "day");
                    Assert.IsNotNull(dayProperty);
                    Assert.AreEqual(PythonMemberType.Property, dayProperty.MemberType);

                    var prop = dayProperty as AstPythonProperty;
                    Assert.IsTrue(prop.IsReadOnly);
                    Assert.AreEqual(BuiltinTypeId.Int, prop.Type.TypeId);

                    var nowMethod = dtClass.GetMember(entry.ModuleContext, "now");
                    Assert.IsNotNull(nowMethod);
                    Assert.AreEqual(PythonMemberType.Method, nowMethod.MemberType);

                    var func = nowMethod as AstPythonFunction;
                    Assert.IsTrue(func.IsClassMethod);

                    Assert.AreEqual(1, func.Overloads.Count);
                    var overload = func.Overloads[0];
                    Assert.IsNotNull(overload);
                    Assert.AreEqual(1, overload.ReturnType.Count);
                    Assert.AreEqual("datetime", overload.ReturnType[0].Name);
                } finally {
                    _analysisLog = entry.GetLogContent(CultureInfo.InvariantCulture);
                }
            }
        }

        [TestMethod, Priority(0)]
        public void AstComparisonTypeInference() {
            using (var entry = CreateAnalysis()) {
                try {
                    var code = @"
class BankAccount(object):
    def __init__(self, initial_balance=0):
        self.balance = initial_balance
    def withdraw(self, amount):
        self.balance -= amount
    def overdrawn(self):
        return self.balance < 0
";
                    entry.AddModule("test-module", code);
                    entry.WaitForAnalysis();

                    var moduleEntry = entry.Modules.First().Value;

                    var varDef = moduleEntry.Analysis.Scope.AllVariables.First(x => x.Key == "BankAccount").Value;
                    var clsInfo = varDef.Types.First(x => x is ClassInfo).First() as ClassInfo;
                    var overdrawn = clsInfo.Scope.GetVariable("overdrawn").Types.First() as FunctionInfo;

                    Assert.AreEqual(1, overdrawn.Overloads.Count());
                    var overload = overdrawn.Overloads.First();
                    Assert.IsNotNull(overload);
                    Assert.AreEqual(1, overload.ReturnType.Count);
                    Assert.AreEqual("bool", overload.ReturnType[0]);
                } finally {
                    _analysisLog = entry.GetLogContent(CultureInfo.InvariantCulture);
                }
            }
        }

        [TestMethod, Priority(0)]
        public void AstSearchPathsThroughFactory() {
            using (var evt = new ManualResetEvent(false))
            using (var analysis = CreateAnalysis()) {
                try {
                    var fact = (AstPythonInterpreterFactory)analysis.Analyzer.InterpreterFactory;
                    var interp = (AstPythonInterpreter)analysis.Analyzer.Interpreter;

                    interp.ModuleNamesChanged += (s, e) => evt.Set();

                    fact.SetCurrentSearchPaths(new[] { TestData.GetPath("TestData\\AstAnalysis") });
                    Assert.IsTrue(evt.WaitOne(1000), "Timeout waiting for paths to update");
                    AssertUtil.ContainsAtLeast(interp.GetModuleNames(), "Values");
                    Assert.IsNotNull(interp.ImportModule("Values"), "Module was not available");

                    evt.Reset();
                    fact.SetCurrentSearchPaths(new string[0]);
                    Assert.IsTrue(evt.WaitOne(1000), "Timeout waiting for paths to update");
                    AssertUtil.DoesntContain(interp.GetModuleNames(), "Values");
                    Assert.IsNull(interp.ImportModule("Values"), "Module was not removed");
                } finally {
                    _analysisLog = analysis.GetLogContent(CultureInfo.InvariantCulture);
                }
            }
        }

        [TestMethod, Priority(0)]
        public void AstSearchPathsThroughAnalyzer() {
            using (var evt = new AutoResetEvent(false))
            using (var analysis = CreateAnalysis()) {
                try {
                    var fact = (AstPythonInterpreterFactory)analysis.Analyzer.InterpreterFactory;
                    var interp = (AstPythonInterpreter)analysis.Analyzer.Interpreter;

                    interp.ModuleNamesChanged += (s, e) => evt.Set();

                    analysis.Analyzer.SetSearchPaths(new[] { TestData.GetPath("TestData\\AstAnalysis") });
                    Assert.IsTrue(evt.WaitOne(1000), "Timeout waiting for paths to update");
                    AssertUtil.ContainsAtLeast(interp.GetModuleNames(), "Values");
                    Assert.IsNotNull(interp.ImportModule("Values"), "Module was not available");

                    analysis.Analyzer.SetSearchPaths(new string[0]);
                    Assert.IsTrue(evt.WaitOne(1000), "Timeout waiting for paths to update");
                    AssertUtil.DoesntContain(interp.GetModuleNames(), "Values");
                    Assert.IsNull(interp.ImportModule("Values"), "Module was not removed");
                } finally {
                    _analysisLog = analysis.GetLogContent(CultureInfo.InvariantCulture);
                }
            }
        }

        [TestMethod, Priority(0)]
        public void AstMro() {
            var O = new AstPythonType("O");
            var A = new AstPythonType("A");
            var B = new AstPythonType("B");
            var C = new AstPythonType("C");
            var D = new AstPythonType("D");
            var E = new AstPythonType("E");
            var F = new AstPythonType("F");

            F.SetBases(null, new[] { O });
            E.SetBases(null, new[] { O });
            D.SetBases(null, new[] { O });
            C.SetBases(null, new[] { D, F });
            B.SetBases(null, new[] { D, E });
            A.SetBases(null, new[] { B, C });

            AssertUtil.AreEqual(AstPythonType.CalculateMro(A).Select(t => t.Name), "A", "B", "C", "D", "E", "F", "O");
            AssertUtil.AreEqual(AstPythonType.CalculateMro(B).Select(t => t.Name), "B", "D", "E", "O");
            AssertUtil.AreEqual(AstPythonType.CalculateMro(C).Select(t => t.Name), "C", "D", "F", "O");
        }

        private static IPythonModule Parse(string path, PythonLanguageVersion version) {
            var interpreter = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(version.ToVersion()).CreateInterpreter();
            if (!Path.IsPathRooted(path)) {
                path = TestData.GetPath(Path.Combine("TestData", "AstAnalysis", path));
            }
            return PythonModuleLoader.FromFile(interpreter, path, version);
        }

        [TestMethod, Priority(0)]
        public void ScrapedTypeWithWrongModule() {
            var version = PythonPaths.Versions
                .Concat(PythonPaths.AnacondaVersions)
                .LastOrDefault(v => Directory.Exists(Path.Combine(v.PrefixPath, "Lib", "site-packages", "numpy")));
            version.AssertInstalled();
            Console.WriteLine("Using {0}", version.PrefixPath);
            using (var analysis = CreateAnalysis(version)) {
                try {
                    var entry = analysis.AddModule("test-module", "import numpy.core.numeric as NP; ndarray = NP.ndarray");
                    analysis.WaitForAnalysis(CancellationTokens.After15s);

                    var cls = analysis.GetValue<BuiltinClassInfo>("ndarray");
                    Assert.IsNotNull(cls);
                } finally {
                    _analysisLog = analysis.GetLogContent(CultureInfo.InvariantCulture);
                }
            }
        }

        [TestMethod, Priority(0)]
        public void ScrapedSpecialFloats() {
            using (var analysis = CreateAnalysis(PythonPaths.Versions.LastOrDefault(v => v.Version.Is3x()))) {
                try {
                    var entry = analysis.AddModule("test-module", "import math; inf = math.inf; nan = math.nan");
                    analysis.WaitForAnalysis(CancellationTokens.After15s);

                    var math = analysis.GetValue<AnalysisValue>("math");
                    Assert.IsNotNull(math);

                    var inf = analysis.GetValue<ConstantInfo>("inf");
                    Assert.AreEqual(BuiltinTypeId.Float, inf.TypeId);

                    var nan = analysis.GetValue<ConstantInfo>("nan");
                    Assert.AreEqual(BuiltinTypeId.Float, nan.TypeId);
                } finally {
                    _analysisLog = analysis.GetLogContent(CultureInfo.InvariantCulture);
                }
            }
        }

        #endregion

        #region Black-box sanity tests
        // "Do we crash?"

        [TestMethod, Priority(0)]
        public void AstBuiltinScrapeV36() => AstBuiltinScrape(PythonPaths.Python36_x64 ?? PythonPaths.Python36);

        [TestMethod, Priority(0)]
        public void AstBuiltinScrapeV35() => AstBuiltinScrape(PythonPaths.Python35_x64 ?? PythonPaths.Python35);

        [TestMethod, Priority(0)]
        public void AstBuiltinScrapeV34() => AstBuiltinScrape(PythonPaths.Python34_x64 ?? PythonPaths.Python34);

        [TestMethod, Priority(0)]
        public void AstBuiltinScrapeV33() => AstBuiltinScrape(PythonPaths.Python33_x64 ?? PythonPaths.Python33);

        [TestMethod, Priority(0)]
        public void AstBuiltinScrapeV32() => AstBuiltinScrape(PythonPaths.Python32_x64 ?? PythonPaths.Python32);

        [TestMethod, Priority(0)]
        public void AstBuiltinScrapeV31() => AstBuiltinScrape(PythonPaths.Python31_x64 ?? PythonPaths.Python31);

        [TestMethod, Priority(0)]
        public void AstBuiltinScrapeV27() => AstBuiltinScrape(PythonPaths.Python27_x64 ?? PythonPaths.Python27);

        [TestMethod, Priority(0)]
        public void AstBuiltinScrapeV26() => AstBuiltinScrape(PythonPaths.Python26_x64 ?? PythonPaths.Python26);


        private void AstBuiltinScrape(PythonVersion version) {
            AstScrapedPythonModule.KeepAst = true;
            version.AssertInstalled();
            using (var analysis = CreateAnalysis(version)) {
                try {
                    var fact = (AstPythonInterpreterFactory)analysis.Analyzer.InterpreterFactory;
                    var interp = (AstPythonInterpreter)analysis.Analyzer.Interpreter;
                    var ctxt = interp.CreateModuleContext();

                    var mod = interp.ImportModule(interp.BuiltinModuleName);
                    Assert.IsInstanceOfType(mod, typeof(AstBuiltinsPythonModule));
                    mod.Imported(ctxt);

                    var modPath = fact.GetCacheFilePath(fact.Configuration.InterpreterPath);
                    if (File.Exists(modPath)) {
                        _moduleCache = File.ReadAllText(modPath);
                    }

                    var errors = ((AstScrapedPythonModule)mod).ParseErrors ?? Enumerable.Empty<string>();
                    foreach (var err in errors) {
                        Console.WriteLine(err);
                    }
                    Assert.AreEqual(0, errors.Count(), "Parse errors occurred");

                    var seen = new HashSet<string>();
                    foreach (var stmt in ((Ast.SuiteStatement)((AstScrapedPythonModule)mod).Ast.Body).Statements) {
                        if (stmt is Ast.ClassDefinition cd) {
                            Assert.IsTrue(seen.Add(cd.Name), $"Repeated use of {cd.Name} at index {cd.StartIndex} in {modPath}");
                        } else if (stmt is Ast.FunctionDefinition fd) {
                            Assert.IsTrue(seen.Add(fd.Name), $"Repeated use of {fd.Name} at index {fd.StartIndex} in {modPath}");
                        } else if (stmt is Ast.AssignmentStatement assign && assign.Left.FirstOrDefault() is Ast.NameExpression n) {
                            Assert.IsTrue(seen.Add(n.Name), $"Repeated use of {n.Name} at index {n.StartIndex} in {modPath}");
                        }
                    }

                    // Ensure we can get all the builtin types
                    foreach (BuiltinTypeId v in Enum.GetValues(typeof(BuiltinTypeId))) {
                        var type = interp.GetBuiltinType(v);
                        Assert.IsNotNull(type, v.ToString());
                        Assert.IsInstanceOfType(type, typeof(AstPythonBuiltinType), $"Did not find {v}");
                    }

                    // Ensure we cannot see or get builtin types directly
                    AssertUtil.DoesntContain(
                        mod.GetMemberNames(null),
                        Enum.GetNames(typeof(BuiltinTypeId)).Select(n => $"__{n}")
                    );

                    foreach (var id in Enum.GetNames(typeof(BuiltinTypeId))) {
                        Assert.IsNull(mod.GetMember(null, $"__{id}"), id);
                    }
                } finally {
                    _analysisLog = analysis.GetLogContent(CultureInfo.InvariantCulture);
                }
            }
        }

        [TestMethod, TestCategory("60s"), Priority(0)]
        public async Task FullStdLibV36() {
            var v = PythonPaths.Versions.FirstOrDefault(pv => pv.Version == PythonLanguageVersion.V36);
            await FullStdLibTest(v);
        }


        [TestMethod, TestCategory("60s"), Priority(0)]
        public async Task FullStdLibV35() {
            var v = PythonPaths.Versions.FirstOrDefault(pv => pv.Version == PythonLanguageVersion.V35);
            await FullStdLibTest(v);
        }

        [TestMethod, TestCategory("60s"), Priority(0)]
        public async Task FullStdLibV34() {
            var v = PythonPaths.Versions.FirstOrDefault(pv => pv.Version == PythonLanguageVersion.V34);
            await FullStdLibTest(v);
        }

        [TestMethod, TestCategory("60s"), Priority(0)]
        public async Task FullStdLibV33() {
            var v = PythonPaths.Versions.FirstOrDefault(pv => pv.Version == PythonLanguageVersion.V33);
            await FullStdLibTest(v);
        }

        [TestMethod, TestCategory("60s"), Priority(0)]
        public async Task FullStdLibV32() {
            var v = PythonPaths.Versions.FirstOrDefault(pv => pv.Version == PythonLanguageVersion.V32);
            await FullStdLibTest(v);
        }

        [TestMethod, TestCategory("60s"), Priority(0)]
        public async Task FullStdLibV31() {
            var v = PythonPaths.Versions.FirstOrDefault(pv => pv.Version == PythonLanguageVersion.V31);
            await FullStdLibTest(v);
        }

        [TestMethod, TestCategory("60s"), Priority(0)]
        public async Task FullStdLibV27() {
            var v = PythonPaths.Versions.FirstOrDefault(pv => pv.Version == PythonLanguageVersion.V27);
            await FullStdLibTest(v);
        }

        [TestMethod, TestCategory("60s"), Priority(0)]
        public async Task FullStdLibV26() {
            var v = PythonPaths.Versions.FirstOrDefault(pv => pv.Version == PythonLanguageVersion.V26);
            await FullStdLibTest(v);
        }

        [TestMethod, TestCategory("60s"), Priority(1)]
        [Timeout(10 * 60 * 1000)]
        public async Task FullStdLibAnaconda3() {
            var v = PythonPaths.Anaconda36_x64 ?? PythonPaths.Anaconda36;
            await FullStdLibTest(v,
                // Crashes Python on import
                "sklearn.linear_model.cd_fast",
                // Crashes Python on import
                "sklearn.cluster._k_means_elkan"
            );
        }

        [TestMethod, TestCategory("60s"), Priority(1)]
        [Timeout(10 * 60 * 1000)]
        public async Task FullStdLibAnaconda2() {
            var v = PythonPaths.Anaconda27_x64 ?? PythonPaths.Anaconda27;
            await FullStdLibTest(v,
                // Fails to import due to SxS manifest issues
                "dde",
                "win32ui"
            );
        }

        private async Task FullStdLibTest(PythonVersion v, params string[] skipModules) {
            v.AssertInstalled();
            var factory = new AstPythonInterpreterFactory(v.Configuration, new InterpreterFactoryCreationOptions {
                DatabasePath = TestData.GetTempPath(),
                UseExistingCache = false
            });
            var modules = ModulePath.GetModulesInLib(v.PrefixPath).ToList();

            var skip = new HashSet<string>(skipModules);
            skip.UnionWith(new[] {
                "matplotlib.backends._backend_gdk",
                "matplotlib.backends._backend_gtkagg",
                "matplotlib.backends._gtkagg",
            });

            bool anySuccess = false;
            bool anyExtensionSuccess = false, anyExtensionSeen = false;
            bool anyParseError = false;

            using (var analyzer = new PythonAnalysis(factory)) {
                try {
                    var tasks = new List<Task<Tuple<ModulePath, IPythonModule>>>();

                    var interp = (AstPythonInterpreter)analyzer.Analyzer.Interpreter;
                    foreach (var m in skip) {
                        interp.AddUnimportableModule(m);
                    }

                    foreach (var r in modules
                        .Where(m => !skip.Contains(m.ModuleName))
                        .GroupBy(m => {
                            int i = m.FullName.IndexOf('.');
                            return i <= 0 ? m.FullName : m.FullName.Remove(i);
                        })
                        .AsParallel()
                        .SelectMany(g => g.Select(m => Tuple.Create(m, interp.ImportModule(m.ModuleName))))
                    ) {
                        var modName = r.Item1;
                        var mod = r.Item2;

                        anyExtensionSeen |= modName.IsNativeExtension;
                        if (mod == null) {
                            Trace.TraceWarning("failed to import {0} from {1}", modName.ModuleName, modName.SourceFile);
                        } else if (mod is AstScrapedPythonModule smod) {
                            if (smod.ParseErrors?.Any() ?? false) {
                                anyParseError = true;
                                Trace.TraceError("Parse errors in {0}", modName.SourceFile);
                                foreach (var e in smod.ParseErrors) {
                                    Trace.TraceError(e);
                                }
                            } else {
                                anySuccess = true;
                                anyExtensionSuccess |= modName.IsNativeExtension;
                                mod.GetMemberNames(analyzer.ModuleContext).ToList();
                            }
                        } else if (mod is AstPythonModule) {
                            // pass
                        } else {
                            Trace.TraceError("imported {0} as type {1}", modName.ModuleName, mod.GetType().FullName);
                        }
                    }
                } finally {
                    _analysisLog = analyzer.GetLogContent(CultureInfo.InvariantCulture);
                }
            }
            Assert.IsTrue(anySuccess, "failed to import any modules at all");
            Assert.IsTrue(anyExtensionSuccess || !anyExtensionSeen, "failed to import all extension modules");
            Assert.IsFalse(anyParseError, "parse errors occurred");
        }

        #endregion

        #region Type Annotation tests
        [TestMethod, Priority(0)]
        public void AstTypeAnnotationConversion() {
            using (var analysis = CreateAnalysis()) {
                try {
                    analysis.SetSearchPaths(TestData.GetPath(@"TestData\AstAnalysis"));
                    analysis.AddModule("test-module", @"from ReturnAnnotations import *
x = f()
y = g()");
                    analysis.WaitForAnalysis();

                    analysis.AssertIsInstance("x", BuiltinTypeId.Int);
                    analysis.AssertIsInstance("y", BuiltinTypeId.Str);
                    analysis.AssertIsInstance("p", 30, "...");
                } finally {
                    _analysisLog = analysis.GetLogContent(CultureInfo.InvariantCulture);
                }
            }
        }
        #endregion

        #region Type Shed tests

        private static PythonVersion VersionWithTypeShed =>
            PythonPaths.Versions.LastOrDefault(v => Directory.Exists(Path.Combine(v.PrefixPath, "Lib", "site-packages", "typeshed")));

        [TestMethod, Priority(0)]
        public void TypeShedElementTree() {
            using (var analysis = CreateAnalysis(VersionWithTypeShed)) {
                try {
                    var entry = analysis.AddModule("test-module", @"import xml.etree.ElementTree as ET

e = ET.Element()
e2 = e.makeelement()
iterfind = e.iterfind
l = iterfind()");
                    analysis.WaitForAnalysis();

                    analysis.AssertHasParameters("ET.Element", "tag", "attrib", "**extra");
                    analysis.AssertHasParameters("e.makeelement", "tag", "attrib");
                    analysis.AssertHasParameters("iterfind", "path", "namespaces");
                    analysis.AssertIsInstance("l", BuiltinTypeId.List);
                } finally {
                    _analysisLog = analysis.GetLogContent(CultureInfo.InvariantCulture);
                }
            }
        }

        [TestMethod, Priority(0)]
        public void TypeShedChildModules() {
            string[] expected;

            using (var analysis = CreateAnalysis(VersionWithTypeShed)) {
                analysis.SetLimits(new AnalysisLimits() { UseTypeStubPackages = false });
                try {
                    var entry = analysis.AddModule("test-module", @"import urllib");
                    analysis.WaitForAnalysis();

                    expected = analysis.Analyzer.GetModuleMembers(entry.AnalysisContext, new[] { "urllib" }, false)
                        .Select(m => m.Name)
                        .OrderBy(n => n)
                        .ToArray();
                    Assert.AreNotEqual(0, expected.Length);
                    AssertUtil.ContainsAtLeast(expected, "parse", "request");
                } finally {
                    _analysisLog = analysis.GetLogContent(CultureInfo.InvariantCulture);
                }
            }

            using (var analysis = CreateAnalysis(VersionWithTypeShed)) {
                try {
                    var entry = analysis.AddModule("test-module", @"import urllib");
                    analysis.WaitForAnalysis();

                    var mods = analysis.Analyzer.GetModuleMembers(entry.AnalysisContext, new[] { "urllib" }, false)
                        .Select(m => m.Name)
                        .OrderBy(n => n)
                        .ToArray();
                    AssertUtil.ArrayEquals(expected, mods);
                } finally {
                    _analysisLog = analysis.GetLogContent(CultureInfo.InvariantCulture);
                }
            }
        }

        [TestMethod, Priority(0)]
        public void TypeShedSysExcInfo() {
            using (var analysis = CreateAnalysis(VersionWithTypeShed)) {
                try {
                    var entry = analysis.AddModule("test-module", @"import sys

e1, e2, e3 = sys.exc_info()");
                    analysis.WaitForAnalysis();

                    analysis.AssertIsInstance("e1", "BaseException", "Type", "Unknown");
                    analysis.AssertIsInstance("e2", "BaseException", "Type", "Unknown");
                    analysis.AssertIsInstance("e3", "BaseException", "Type", "Unknown");
                } finally {
                    _analysisLog = analysis.GetLogContent(CultureInfo.InvariantCulture);
                }
            }
        }

        #endregion
    }
}

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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting;
using System.Threading;
using AnalysisTests;
using Microsoft.IronPythonTools.Interpreter;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;

namespace IronPythonTests {
    /// <summary>
    /// Analysis tests run against the IronPython interpreter including tests specific to IronPython.
    /// </summary>
    [TestClass]
    public class IronPythonAnalysisTest : AnalysisTest {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        protected override bool SupportsPython3 => false;

        protected override string ListInitParameterName {
            get { return "enumerable"; }
        }

        protected override IModuleContext DefaultContext {
            get { return IronPythonModuleContext.DontShowClrInstance;}
        }

        private readonly IronPythonInterpreterFactoryProvider _factoryProvider = new IronPythonInterpreterFactoryProvider();

        protected override IPythonInterpreterFactory DefaultFactoryV2 => _factoryProvider.GetInterpreterFactory(IronPythonInterpreterFactoryProvider.GetInterpreterId(InterpreterArchitecture.x64));

        protected override IPythonInterpreterFactory DefaultFactoryV3 => null;

        protected override PythonAnalysis CreateAnalyzerInternal(IPythonInterpreterFactory factory) {
            var analysis = new IronPythonAnalysis(factory);
            analysis.SetSearchPaths(Environment.CurrentDirectory);
            return analysis;
        }

        [TestMethod, Priority(0)]
        public void Generics() {
            var text = @"
import clr
clr.AddReference('AnalysisTests')
from AnalysisTests.DotNetAnalysis import *

y = GenericType()
zzz = y.ReturnsGenericParam()
";
            var entry = ProcessText(text);

            AssertUtil.ContainsExactly(entry.GetMemberNames("zzz", 1), "GetEnumerator", "__doc__", "__iter__", "__repr__");
        }

        [TestMethod, Priority(0)]
        public void Constructors() {
            var text = @"
from System import AccessViolationException
n = AccessViolationException.__new__
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(
                entry.GetSignatures("n", 1)
                .Select(x => FormatSignature(x)), 
                "__new__(cls: AccessViolationException)",
                "__new__(cls: AccessViolationException, message: str)",
                "__new__(cls: AccessViolationException, message: str, innerException: Exception)",
                "__new__(cls: AccessViolationException, info: SerializationInfo, context: StreamingContext)"
            );
        }

        private static string FormatSignature(IOverloadResult sig) {
            return sig.Name + 
                "(" +
                String.Join(", ", sig.Parameters.Select(param => param.Name + ": " + param.Type)) +
                ")";
        }

        [TestMethod, Priority(0)]
        public void ImportClr() {
            var text = @"
import clr
x = 'abc'
";
            var entry = ProcessText(text);
            entry.AssertHasAttr("x", "Length");
        }

        [TestMethod, Priority(0)]
        public void ClrAddReference() {
            var text = @"
import clr
clr.AddReference('System.Drawing')
from System.Drawing import Point
";
            var entry = ProcessText(text);
            var members = entry.GetMemberNames("Point", text.IndexOf("from System.")).ToList();

            Assert.AreEqual(35, members.Count);
        }

        [TestMethod, Priority(0)]
        public void ClrAddReferenceByName() {
            var text = @"
import clr
clr.AddReferenceByName('Microsoft.Scripting')
from Microsoft.Scripting import SourceUnit
";
            var entry = ProcessText(text);
            Assert.AreEqual(40, entry.GetMemberNames("SourceUnit", text.IndexOf("from Microsoft.")).ToList().Count);
        }

        [TestMethod, Priority(0)]
        public void Enum() {
            var entry = ProcessText(@"
import System
x = System.StringComparison.OrdinalIgnoreCase
            ");

            var x = entry.GetValue<AnalysisValue>("x", 1);
            Debug.Assert(x.MemberType == PythonMemberType.EnumInstance);
            Assert.AreEqual(x.MemberType, PythonMemberType.EnumInstance);
        }

        [TestMethod, Priority(0)]
        public void Color() {

            var entry = ProcessText(@"
import clr
clr.AddReference('PresentationFramework')
clr.AddReference('PresentationCore')

from System.Windows.Media import Colors

class C(object):
    def __init__(self):
        if False:
            self.some_color = Colors.Black
        else:
            self.some_color = Colors.White


a = C()
b = a.some_color
");

            AssertUtil.ContainsExactly(entry.GetTypes("b", 1).Select(x => x.Name), "Color");
        }

        [TestMethod, Priority(0)]
        public void BuiltinTypeSignatures() {
            var entry = ProcessText(@"
import System
x = str
x = int

y = str
y = int
");

            var result = entry.GetSignatures("System.Collections.Generic.Dictionary[int, int]", 1).ToArray();
            Assert.AreEqual(6, result.Length);

            // 2 possible types
            result = entry.GetSignatures("System.Collections.Generic.Dictionary[x, int]", 1).ToArray();
            Assert.AreEqual(12, result.Length);

            // 4 possible types
            result = entry.GetSignatures("System.Collections.Generic.Dictionary[x, y]", 1).ToArray();
            Assert.AreEqual(24, result.Length);
        }

        [TestMethod, Priority(0)]
        public void EventReferences() {
            var text = @"
from System import EventHandler
def g():
    x = EventHandler(f)
    
def f(sender, args): pass
";
            var entry = ProcessText(text);
            entry.AssertReferences("f", text.IndexOf("x ="),
                new VariableLocation(4, 22, VariableType.Reference),
                new VariableLocation(6, 1, VariableType.Value),
                new VariableLocation(6, 5, VariableType.Definition));

            text = @"
from System import EventHandler
def f(sender, args): pass

x = EventHandler(f)";
            entry = ProcessText(text);
            entry.AssertReferences("f", text.IndexOf("x ="),
                new VariableLocation(3, 1, VariableType.Value),
                new VariableLocation(3, 5, VariableType.Definition),
                new VariableLocation(5, 18, VariableType.Reference));

            // left hand side is unknown, right hand side should still have refs added
            text = @"
from System import EventHandler
def f(sender, args): pass

a.fob += EventHandler(f)
";
            entry = ProcessText(text);
            entry.AssertReferences("f", text.IndexOf("a.fob +="),
                new VariableLocation(3, 1, VariableType.Value),
                new VariableLocation(3, 5, VariableType.Definition),
                new VariableLocation(5, 23, VariableType.Reference));
        }

        [TestMethod, Priority(0)]
        public void SystemFromImport() {
            var text = @"
from System import Environment
Environment.GetCommandLineArgs()
";
            var entry = ProcessText(text);
            Assert.IsTrue(entry.GetMemberNames("Environment", 1).Any(s => s == "CommandLine"));
        }

        [TestMethod, Priority(0)]
        public void ImportAsIpy() {
            var text = @"
import System.Collections as coll
";
            var entry = ProcessText(text);
            Assert.IsTrue(entry.GetMemberNames("coll", 1).Any(s => s == "ArrayList"));
        }

        [TestMethod, Priority(0)]
        public void SystemImport() {
            var text = @"
import System
System.Environment.GetCommandLineArgs()
x = System.Environment
";
            var entry = ProcessText(text);
            var system = entry.GetMemberNames("System", 1).ToSet();
            // defined in mscorlib
            AssertUtil.Contains(system, "AccessViolationException");
            // defined in System
            AssertUtil.Contains(system, "CodeDom");

            AssertUtil.Contains(entry.GetMemberNames("x", 1), "GetEnvironmentVariables");
        }

        [TestMethod, Priority(0)]
        public void SystemMembers() {
            var text = @"
import System
System.Environment.GetCommandLineArgs()
x = System.Environment
args = x.GetCommandLineArgs()
";
            var entry = ProcessText(text);

            var args = entry.GetTypes("args", text.IndexOf("args =")).Select(x => x.Name).ToSet();
            AssertUtil.ContainsExactly(args, "Array[str]");

            Assert.IsTrue(entry.GetMemberNames("args", text.IndexOf("args =")).Any(s => s == "AsReadOnly"));
        }

        [TestMethod, Priority(0)]
        public void NamespaceMembers() {
            var text = @"
import System
x = System.Collections
";
            var entry = ProcessText(text);
            var x = entry.GetMemberNames("x", text.IndexOf("x =")).ToSet();
            Assert.IsTrue(x.Contains("Generic"));
            Assert.IsTrue(x.Contains("ArrayList"));
        }

        [TestMethod, Priority(0)]
        public void GenericIndexing() {
            // indexing into a generic type should know how the type info
            // flows through
            var text = @"
from System.Collections.Generic import List
x = List[int]()
";
            var entry = ProcessText(text);

            // AreEqual(entry.GetMembersFromName('x', len(text) - 1), 
            //     get_intersect_members_clr(List[int]))
            var self = new List<string>(entry.GetMemberNames("x", text.IndexOf("x =")));
            Assert.IsTrue(self.Contains("AddRange"));
        }

        [TestMethod, Priority(0)]
        public void ReturnTypesCollapsing() {
            // indexing into a generic type should know how the type info
            // flows through
            var text = @"
from System import AppDomain
asm = AppDomain.CurrentDomain.DefineDynamicAssembly()
mod = asm.DefineDynamicModule()
mod.
";
            var entry = ProcessText(text, allowParseErrors: true);
            var tooltips = entry.GetMember("mod", "CreateGlobalFunctions", text.IndexOf("mod ="))
                .Select(m => m.Documentation)
                .ToArray();
            Assert.AreEqual(1, tooltips.Length);
#if IPY
            var indexes = tooltips[0].FindIndexesOf("CreateGlobalFunctions").ToArray();
            Assert.AreEqual(1, indexes.Length);
#endif
        }

        [TestMethod, Priority(0)]
        public void IronPythonMro() {
            var text = @"
from System import DivideByZeroException
";
            var entry = ProcessText(text);
            var dbzEx = entry.GetValue<BuiltinClassInfo>("DivideByZeroException");
            // Check values from IPythonType MRO
            AssertUtil.ContainsExactly(dbzEx.PythonType.Mro.Select(t => t.Name),
                "DivideByZeroException",
                "ArithmeticException",
                "SystemException",
                "Exception",
                "object");
            // Check values from BuiltinClassInfo MRO
            AssertUtil.ContainsExactly(dbzEx.Mro.Select(t => t.First().Name),
                "DivideByZeroException",
                "ArithmeticException",
                "SystemException",
                "Exception",
                "object");
        }

        [TestMethod, Priority(0)]
        public void AssignEvent() {
            var text = @"
import System

def f(sender, args):
    pass
    
System.AppDomain.CurrentDomain.AssemblyLoad += f
";
            var entry = ProcessText(text);
            Assert.IsTrue(entry.GetMemberNames("args", text.IndexOf("pass")).Any(s => s == "LoadedAssembly"));
        }

        [TestMethod, Priority(0)]
        public void EventMemberType() {
            var text = @"from System import AppDomain";
            var entry = ProcessText(text);
            var mem = entry.GetMember("AppDomain", "AssemblyLoad").Single();
            Assert.AreEqual(mem.MemberType, PythonMemberType.Event);
        }

        [TestMethod, Priority(0)]
        public void NegCallProperty() {
            // invalid code, this shouldn't crash us.
            var text = @"
import System
x = System.String.Length()
y = System.Environment.CurrentDirectory()
";
            ProcessText(text);
        }

        [TestMethod, Priority(0)]
        public void QuickInfoClr() {
            var text = @"
import System
from System.Collections import ArrayList

e = System.Collections.ArrayList()

def g():
    return System
";
            var entry = ProcessText(text);

            AssertUtil.ContainsExactly(entry.GetDescriptions("System", 1), "System");
            AssertUtil.ContainsExactly(entry.GetDescriptions("System.String.Length", 1), "property of type int");
            AssertUtil.ContainsExactly(entry.GetDescriptions("System.Environment.CurrentDirectory", 1), "str");
            AssertUtil.ContainsExactly(entry.GetDescriptions("e", 1), "ArrayList");
            AssertUtil.ContainsExactly(entry.GetShortDescriptions("ArrayList", 1), "type ArrayList");
            AssertUtil.ContainsExactly(entry.GetDescriptions("e.Count", 1), "int");
            AssertUtil.ContainsExactly(entry.GetShortDescriptions("System.DBNull.Value", 1), "DBNull");
            AssertUtil.ContainsExactly(entry.GetShortDescriptions("System.StringSplitOptions", 1), "type StringSplitOptions");
            //AssertUtil.ContainsExactly(entry.GetVariableDescriptionsByIndex("\"abc\".Length", 1), "int");
            //AssertUtil.ContainsExactly(entry.GetVariableDescriptionsByIndex("c.Length", 1), "int");
            //AssertUtil.ContainsExactly(entry.GetVariableDescriptionsByIndex("System.StringSplitOptions.RemoveEmptyEntries", 0), "field of type StringSplitOptions");
            AssertUtil.ContainsExactly(entry.GetDescriptions("g", 1), "test-module.g() -> System");    // return info could be better
            //AssertUtil.ContainsExactly(entry.GetVariableDescriptionsByIndex("System.AppDomain.DomainUnload", 1), "event of type System.EventHandler");
        }

        [TestMethod, Priority(0)]
        public void BuiltinMethodSignaturesClr() {
            var entry = ProcessText(@"
import clr
const = """".Contains
constructed = str().Contains
");

            string[] testContains = new[] { "const", "constructed" };
            foreach (var test in testContains) {
                var result = entry.GetSignatures(test, 1).ToArray();
                Assert.AreEqual(1, result.Length);
                Assert.AreEqual(1, result[0].Parameters.Length);
                Assert.AreEqual("value", result[0].Parameters[0].Name);
                Assert.IsFalse(result[0].Parameters[0].IsOptional);
            }

        }

        [TestMethod, Priority(0)]
        public void BuiltinMethodDocumentationClr() {
            var entry = ProcessText(@"
import wpf
from System.Windows import Window
w = Window()
w.Activate
");

            var result = entry.GetValue<AnalysisValue>("w.Activate", 0);
            Console.WriteLine("Docstring was: <{0}>", result.Documentation);
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.Documentation));
        }

        /*
        [TestMethod, Priority(0)]
        public void OverrideParams() {
            var text = @"
import System

class MyArrayList(System.Collections.ArrayList):
    def AddRange(self, col):
        x = col
";
            var entry = ProcessText(text);
            var x = entry.GetMembersFromName("x", text.IndexOf("x = col")).ToSet();
            AssertUtil.ContainsExactly(x, GetMembers(ClrModule.GetPythonType(typeof(System.Collections.ICollection)), true));
        }*/


        /// <summary>
        /// Verify importing wpf will add a reference to the WPF assemblies
        /// </summary>
        [TestMethod, Priority(0)]
        public void WpfReferences() {
            var entry = ProcessText(@"
import wpf
from System.Windows.Media import Colors
");

            AssertUtil.Contains(entry.GetMemberNames("Colors", 1), "Blue");
            AssertUtil.Contains(entry.GetMemberNames("wpf", 1), "LoadComponent");
        }

        [TestMethod, Priority(0)]
        public void XamlEmptyXName() {
            // [Python Tools] Adding attribute through XAML in IronPython application crashes VS.
            // http://pytools.codeplex.com/workitem/743
            using (var analyzer = CreateAnalyzer()) {
                string xamlPath = TestData.GetPath(@"TestData\Xaml\EmptyXName.xaml");
                string pyPath = TestData.GetPath(@"TestData\Xaml\EmptyXName.py");
                var xamlEntry = analyzer.Analyzer.AddXamlFile(xamlPath);
                var pyEntry = analyzer.AddModule("EmptyXName", File.ReadAllText(pyPath), pyPath);

                xamlEntry.ParseContent(new FileStreamReader(xamlPath), null);

                using (var stream = new FileStreamReader(pyPath)) {
                    var parser = Parser.CreateParser(stream, PythonLanguageVersion.V27, new ParserOptions() { BindReferences = true });
                    using (var p = pyEntry.BeginParse()) {
                        p.Tree = parser.ParseFile();
                        p.Complete();
                    }
                }

                pyEntry.Analyze(CancellationToken.None);
            }
        }
    }
}

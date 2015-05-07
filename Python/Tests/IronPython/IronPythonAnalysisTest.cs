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
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting;
using System.Threading;
using AnalysisTests;
using IronPython.Runtime;
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
        private string[] _objectMembersClr, _strMembersClr;

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            PythonTestData.Deploy();
        }

        protected override bool SupportsPython3 {
            get { return false; }
        }

        protected override string ListInitParameterName {
            get { return "enumerable"; }
        }

        protected override IModuleContext DefaultContext {
            get { return IronPythonModuleContext.DontShowClrInstance;}
        }

        protected override bool ShouldUseUnicodeLiterals(PythonLanguageVersion version) {
            return true;
        }

        public IronPythonAnalysisTest()
            : base(new IronPythonInterpreterFactory(ProcessorArchitecture.X86), CreateInterpreter()) {
            var objectType = Interpreter.GetBuiltinType(BuiltinTypeId.Object);
            _objectMembersClr = objectType.GetMemberNames(IronPythonModuleContext.ShowClrInstance).ToArray();
            var stringType = Interpreter.GetBuiltinType(BuiltinTypeId.Str);
            _strMembersClr = stringType.GetMemberNames(IronPythonModuleContext.ShowClrInstance).ToArray();

            Assert.IsTrue(_objectMembers.Length < _objectMembersClr.Length);
            Assert.IsTrue(_strMembers.Length < _strMembersClr.Length);
        }

        private static IPythonInterpreter CreateInterpreter() {
            var res = new IronPythonInterpreter(InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(PythonLanguageVersion.V27.ToVersion()));
            res.Remote.AddAssembly(new ObjectHandle(typeof(IronPythonAnalysisTest).Assembly));
            return res;
        }

        public override BuiltinTypeId BuiltinTypeId_Str {
            get {
                return BuiltinTypeId.Unicode;
            }
        }

        public override BuiltinTypeId BuiltinTypeId_StrIterator {
            get {
                // IronPython does not distinguish between string iterators, and
                // since BytesIterator < UnicodeIterator, it is the one returned
                // for iter("").
                return BuiltinTypeId.BytesIterator;
            }
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
            var entry = ProcessText(text, analysisDirs: new[] { Environment.CurrentDirectory });
            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("zzz", 1), "GetEnumerator", "__doc__", "__iter__", "__repr__");
        }


        [TestMethod, Priority(0)]
        public void ImportClr() {
            var text = @"
import clr
x = 'abc'
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetMemberNamesByIndex("x", 1), _strMembersClr);
        }

        [TestMethod, Priority(0)]
        public void ClrAddReference() {
            var text = @"
import clr
clr.AddReference('System.Drawing')
from System.Drawing import Point
";
            var entry = ProcessText(text);
            var members = entry.GetMemberNamesByIndex("Point", text.IndexOf("from System.")).ToList();

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
            Assert.AreEqual(40, entry.GetMemberNamesByIndex("SourceUnit", text.IndexOf("from Microsoft.")).ToList().Count);
        }

        [TestMethod, Priority(0)]
        public void Enum() {
            var entry = ProcessText(@"
import System
x = System.StringComparison.OrdinalIgnoreCase
            ");

            var x = entry.GetValuesByIndex("x", 1).First();
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

            AssertUtil.ContainsExactly(entry.GetTypesByIndex("b", 1).Select(x => x.Name), "Color");
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

            var result = entry.GetSignaturesByIndex("System.Collections.Generic.Dictionary[int, int]", 1).ToArray();
            Assert.AreEqual(6, result.Length);

            // 2 possible types
            result = entry.GetSignaturesByIndex("System.Collections.Generic.Dictionary[x, int]", 1).ToArray();
            Assert.AreEqual(12, result.Length);

            // 4 possible types
            result = entry.GetSignaturesByIndex("System.Collections.Generic.Dictionary[x, y]", 1).ToArray();
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
            VerifyReferences(UniqifyVariables(entry.GetVariablesByIndex("f", text.IndexOf("x ="))), new VariableLocation(4, 22, VariableType.Reference), new VariableLocation(6, 5, VariableType.Definition));

            text = @"
from System import EventHandler
def f(sender, args): pass

x = EventHandler(f)";
            entry = ProcessText(text);
            VerifyReferences(UniqifyVariables(entry.GetVariablesByIndex("f", text.IndexOf("x ="))), new VariableLocation(5, 18, VariableType.Reference), new VariableLocation(3, 5, VariableType.Definition));

            // left hand side is unknown, right hand side should still have refs added
            text = @"
from System import EventHandler
def f(sender, args): pass

a.fob += EventHandler(f)
";
            entry = ProcessText(text);
            VerifyReferences(UniqifyVariables(entry.GetVariablesByIndex("f", text.IndexOf("a.fob +="))), new VariableLocation(5, 23, VariableType.Reference), new VariableLocation(3, 5, VariableType.Definition));
        }

        [TestMethod, Priority(0)]
        public void SystemFromImport() {
            var text = @"
from System import Environment
Environment.GetCommandLineArgs()
";
            var entry = ProcessText(text);
            Assert.IsTrue(entry.GetMemberNamesByIndex("Environment", 1).Any(s => s == "CommandLine"));
        }

        [TestMethod, Priority(0)]
        public void ImportAsIpy() {
            var text = @"
import System.Collections as coll
";
            var entry = ProcessText(text);
            Assert.IsTrue(entry.GetMemberNamesByIndex("coll", 1).Any(s => s == "ArrayList"));
        }

        [TestMethod, Priority(0)]
        public void SystemImport() {
            var text = @"
import System
System.Environment.GetCommandLineArgs()
x = System.Environment
";
            var entry = ProcessText(text);
            var system = entry.GetMemberNamesByIndex("System", 1).ToSet();
            // defined in mscorlib
            AssertUtil.Contains(system, "AccessViolationException");
            // defined in System
            AssertUtil.Contains(system, "CodeDom");

            AssertUtil.Contains(entry.GetMemberNamesByIndex("x", 1), "GetEnvironmentVariables");
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

            var args = entry.GetTypesByIndex("args", text.IndexOf("args =")).Select(x => x.Name).ToSet();
            AssertUtil.ContainsExactly(args, "Array[str]");

            Assert.IsTrue(entry.GetMemberNamesByIndex("args", text.IndexOf("args =")).Any(s => s == "AsReadOnly"));
        }

        [TestMethod, Priority(0)]
        public void NamespaceMembers() {
            var text = @"
import System
x = System.Collections
";
            var entry = ProcessText(text);
            var x = entry.GetMemberNamesByIndex("x", text.IndexOf("x =")).ToSet();
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
            var self = new List<string>(entry.GetMemberNamesByIndex("x", text.IndexOf("x =")));
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
            var entry = ProcessText(text);
            var tooltips = entry.GetMembersByIndex("mod", text.IndexOf("mod ="))
                .Where(m => m.Name == "CreateGlobalFunctions")
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
            var dbzEx = entry.GetValuesByIndex("DivideByZeroException", 0).First() as BuiltinClassInfo;
            Assert.IsNotNull(dbzEx);
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
            Assert.IsTrue(entry.GetMemberNamesByIndex("args", text.IndexOf("pass")).Any(s => s == "LoadedAssembly"));
        }

        [TestMethod, Priority(0)]
        public void EventMemberType() {
            var text = @"from System import AppDomain";
            var entry = ProcessText(text);
            var mem = entry.GetMembersByIndex("AppDomain", 1).Where(x => x.Name == "AssemblyLoad").First();
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

            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("System", 1), "built-in module System");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("System.String.Length", 1), "property of type int");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("System.Environment.CurrentDirectory", 1), "str");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("e", 1), "ArrayList");
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("ArrayList", 1), "type ArrayList");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("e.Count", 1), "int");
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("System.DBNull.Value", 1), "DBNull");
            AssertUtil.ContainsExactly(entry.GetShortDescriptionsByIndex("System.StringSplitOptions", 1), "type StringSplitOptions");
            //AssertUtil.ContainsExactly(entry.GetVariableDescriptionsByIndex("\"abc\".Length", 1), "int");
            //AssertUtil.ContainsExactly(entry.GetVariableDescriptionsByIndex("c.Length", 1), "int");
            //AssertUtil.ContainsExactly(entry.GetVariableDescriptionsByIndex("System.StringSplitOptions.RemoveEmptyEntries", 0), "field of type StringSplitOptions");
            AssertUtil.ContainsExactly(entry.GetDescriptionsByIndex("g", 1), "def g() -> built-in module System");    // return info could be better
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
                var result = entry.GetSignaturesByIndex(test, 1).ToArray();
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

            var result = entry.GetValuesByIndex("w.Activate", 1).ToArray();
            Assert.AreEqual(1, result.Length);
            Console.WriteLine("Docstring was: <{0}>", result[0].Documentation);
            Assert.IsFalse(string.IsNullOrWhiteSpace(result[0].Documentation));
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

            AssertUtil.Contains(entry.GetMemberNamesByIndex("Colors", 1), "Blue");
            AssertUtil.Contains(entry.GetMemberNamesByIndex("wpf", 1), "LoadComponent");
        }

        [TestMethod, Priority(0)]
        public void XamlEmptyXName() {
            // [Python Tools] Adding attribute through XAML in IronPython application crashes VS.
            // http://pytools.codeplex.com/workitem/743
            using (var analyzer = PythonAnalyzer.CreateSynchronously(InterpreterFactory, Interpreter)) {
                string xamlPath = TestData.GetPath(@"TestData\Xaml\EmptyXName.xaml");
                string pyPath = TestData.GetPath(@"TestData\Xaml\EmptyXName.py");
                var xamlEntry = analyzer.AddXamlFile(xamlPath);
                var pyEntry = analyzer.AddModule("EmptyXName", pyPath);

                xamlEntry.ParseContent(new FileStreamReader(xamlPath), null);

                using (var parser = Parser.CreateParser(new FileStreamReader(pyPath), PythonLanguageVersion.V27, new ParserOptions() { BindReferences = true })) {
                    pyEntry.UpdateTree(parser.ParseFile(), null);
                }

                pyEntry.Analyze(CancellationToken.None);
            }
        }

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

    }
}

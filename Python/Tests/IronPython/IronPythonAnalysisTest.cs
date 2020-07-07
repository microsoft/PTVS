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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.IronPythonTools.Interpreter;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace IronPythonTests {
    [TestClass]
    public class IronPythonAnalysisTest {
        private readonly IronPythonInterpreterFactoryProvider _factoryProvider = new IronPythonInterpreterFactoryProvider();
        private List<IDisposable> _toDispose;

        [TestInitialize]
        public void TestInitialize() {
            TestEnvironmentImpl.TestInitialize();
        }

        [TestCleanup]
        public void TestCleanup() {
            TestEnvironmentImpl.TestCleanup();
        }

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        #region Test Cases

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void ImportClr() {
            var text = @"
import clr
x = 'abc'
";
            var entry = ProcessText(text);
            entry.AssertHasAttr("x", "Length");
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void ClrAddReferenceByName() {
            var text = @"
import clr
clr.AddReferenceByName('Microsoft.Scripting')
from Microsoft.Scripting import SourceUnit
";
            var entry = ProcessText(text);
            Assert.AreEqual(40, entry.GetMemberNames("SourceUnit", text.IndexOf("from Microsoft.")).ToList().Count);
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void Enum() {
            var entry = ProcessText(@"
import System
x = System.StringComparison.OrdinalIgnoreCase
            ");

            var x = entry.GetValue<AnalysisValue>("x", 1);
            Debug.Assert(x.MemberType == PythonMemberType.EnumInstance);
            Assert.AreEqual(x.MemberType, PythonMemberType.EnumInstance);
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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

        [TestMethod, Priority(UnitTestPriority.P1)]
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

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void SystemFromImport() {
            var text = @"
from System import Environment
Environment.GetCommandLineArgs()
";
            var entry = ProcessText(text);
            Assert.IsTrue(entry.GetMemberNames("Environment", 1).Any(s => s == "CommandLine"));
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void ImportAsIpy() {
            var text = @"
import System.Collections as coll
";
            var entry = ProcessText(text);
            Assert.IsTrue(entry.GetMemberNames("coll", 1).Any(s => s == "ArrayList"));
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void IronPythonMro() {
            var text = @"
from System import DivideByZeroException
";
            var entry = ProcessText(text);
            var dbzEx = entry.GetValue<AnalysisValue>("DivideByZeroException");
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

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void EventMemberType() {
            var text = @"from System import AppDomain";
            var entry = ProcessText(text);
            var mem = entry.GetMember("AppDomain", "AssemblyLoad").Single();
            Assert.AreEqual(mem.MemberType, PythonMemberType.Event);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void NegCallProperty() {
            // invalid code, this shouldn't crash us.
            var text = @"
import System
x = System.String.Length()
y = System.Environment.CurrentDirectory()
";
            ProcessText(text);
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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
        [TestMethod, Priority(UnitTestPriority.P1)]
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
        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void WpfReferences() {
            var entry = ProcessText(@"
import wpf
from System.Windows.Media import Colors
");

            AssertUtil.Contains(entry.GetMemberNames("Colors", 1), "Blue");
            AssertUtil.Contains(entry.GetMemberNames("wpf", 1), "LoadComponent");
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void XamlEmptyXName() {
            // [Python Tools] Adding attribute through XAML in IronPython application crashes VS.
            // http://pytools.codeplex.com/workitem/743
            using (var analyzer = CreateAnalyzer()) {
                string xamlPath = TestData.GetPath(@"TestData\Xaml\EmptyXName.xaml");
                string pyPath = TestData.GetPath(@"TestData\Xaml\EmptyXName.py");
                var xamlEntry = (IXamlProjectEntry)((IDotNetPythonInterpreter)analyzer.Analyzer.Interpreter).AddXamlEntry(xamlPath, new Uri(xamlPath));
                var pyEntry = analyzer.AddModule("EmptyXName", File.ReadAllText(pyPath), pyPath);

                xamlEntry.ParseContent(new StreamReader(xamlPath), null);

                using (var stream = new StreamReader(pyPath)) {
                    var parser = Parser.CreateParser(stream, PythonLanguageVersion.V27, new ParserOptions() { BindReferences = true });
                    using (var p = pyEntry.BeginParse()) {
                        p.Tree = parser.ParseFile();
                        p.Complete();
                    }
                }

                pyEntry.Analyze(CancellationToken.None);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void CheckInterpreterV2() {
            using (var interp = DefaultFactoryV2.CreateInterpreter()) {
                try {
                    interp.GetBuiltinType((BuiltinTypeId)(-1));
                    Assert.Fail("Expected KeyNotFoundException");
                } catch (KeyNotFoundException) {
                }
                var intType = interp.GetBuiltinType(BuiltinTypeId.Int);
                Assert.IsTrue(intType.ToString() != "");
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void SpecialArgTypes() {
            var code = @"def f(*fob, **oar):
    pass
";
            var entry = ProcessTextV2(code);

            entry.AssertIsInstance("fob", code.IndexOf("pass"), BuiltinTypeId.Tuple);
            entry.AssertIsInstance("oar", code.IndexOf("pass"), BuiltinTypeId.Dict);

            code = @"def f(*fob):
    pass

f(42)
";
            entry = ProcessTextV2(code);

            entry.AssertIsInstance("fob", code.IndexOf("pass"), BuiltinTypeId.Tuple);
            entry.AssertIsInstance("fob[0]", code.IndexOf("pass"), BuiltinTypeId.Int);

            code = @"def f(*fob):
    pass

f(42, 'abc')
";
            entry = ProcessTextV2(code);

            entry.AssertIsInstance("fob", code.IndexOf("pass"), BuiltinTypeId.Tuple);
            entry.AssertIsInstance("fob[0]", code.IndexOf("pass"), BuiltinTypeId.Int, BuiltinTypeId.Str);
            entry.AssertIsInstance("fob[1]", code.IndexOf("pass"), BuiltinTypeId.Int, BuiltinTypeId.Str);

            code = @"def f(*fob):
    pass

f(42, 'abc')
f('abc', 42)
";
            entry = ProcessTextV2(code);

            entry.AssertIsInstance("fob", code.IndexOf("pass"), BuiltinTypeId.Tuple);
            entry.AssertIsInstance("fob[0]", code.IndexOf("pass"), BuiltinTypeId.Int, BuiltinTypeId.Str);
            entry.AssertIsInstance("fob[1]", code.IndexOf("pass"), BuiltinTypeId.Int, BuiltinTypeId.Str);

            code = @"def f(**oar):
    y = oar['fob']
    pass

f(x=42)
";
            entry = ProcessTextV2(code);

            entry.AssertIsInstance("oar", code.IndexOf("pass"), BuiltinTypeId.Dict);
            entry.AssertIsInstance("oar['fob']", code.IndexOf("pass"), BuiltinTypeId.Int);
            entry.AssertIsInstance("y", code.IndexOf("pass"), BuiltinTypeId.Int);


            code = @"def f(**oar):
    z = oar['fob']
    pass

f(x=42, y = 'abc')
";
            entry = ProcessTextV2(code);

            entry.AssertIsInstance("oar", code.IndexOf("pass"), BuiltinTypeId.Dict);
            entry.AssertIsInstance("oar['fob']", code.IndexOf("pass"), BuiltinTypeId.Int, BuiltinTypeId.Str);
            entry.AssertIsInstance("z", code.IndexOf("pass"), BuiltinTypeId.Int, BuiltinTypeId.Str);
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void TestPackageImportStar() {
            var analyzer = CreateAnalyzer();

            var fob = analyzer.AddModule("fob", "from oar import *", "fob\\__init__.py");
            var oar = analyzer.AddModule("fob.oar", "from .baz import *", "fob\\oar\\__init__.py");
            var baz = analyzer.AddModule("fob.oar.baz", "import fob.oar.quox as quox\r\nfunc = quox.func");
            var quox = analyzer.AddModule("fob.oar.quox", "def func(): return 42");
            analyzer.ReanalyzeAll();

            analyzer.AssertDescription(fob, "func", "fob.oar.quox.func() -> int");
            analyzer.AssertDescription(oar, "func", "fob.oar.quox.func() -> int");
            analyzer.AssertDescription(baz, "func", "fob.oar.quox.func() -> int");
            analyzer.AssertDescription(quox, "func", "fob.oar.quox.func() -> int");
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void TestClassAssignSameName() {
            var text = @"x = 123

class A:
    x = x
    pass

class B:
    x = 3.1415
    x = x
";

            var entry = ProcessTextV2(text);

            entry.AssertIsInstance("x", BuiltinTypeId.Int);
            entry.AssertIsInstance("x", BuiltinTypeId.Int);
            entry.AssertIsInstance("x", BuiltinTypeId.Int);
            entry.AssertIsInstance("A.x", BuiltinTypeId.Int);

            // Arguably this should only be float, but since we don't support
            // definite assignment having both int and float is correct now.
            //
            // It also means we handle this case consistently:
            //
            // class B(object):
            //     if False:
            //         x = 3.1415
            //     x = x
            entry.AssertIsInstance("B.x", BuiltinTypeId.Int, BuiltinTypeId.Float);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void TestFunctionAssignSameName() {
            var text = @"x = 123

def f():
    x = x
    return x

y = f()
";

            var entry = ProcessTextV2(text);

            entry.AssertIsInstance("x", BuiltinTypeId.Int);
            entry.AssertIsInstance("x", BuiltinTypeId.Int);
            entry.AssertIsInstance("x", BuiltinTypeId.Int);
            entry.AssertIsInstance("y", BuiltinTypeId.Int);
        }

        /// <summary>
        /// Binary operators should assume their result type
        /// https://pytools.codeplex.com/workitem/1575
        /// 
        /// Slicing should assume the incoming type
        /// https://pytools.codeplex.com/workitem/1581
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void TestBuiltinOperatorsFallback() {
            var code = @"import array

slice = array.array('b', b'abcdef')[2:3]
add = array.array('b', b'abcdef') + array.array('b', b'fob')
";
            var entry = ProcessTextV2(code);

            AssertUtil.ContainsExactly(
                entry.GetTypes("slice", code.IndexOf("slice = ")).Select(x => x.Name),
                "array"
            );
            AssertUtil.ContainsExactly(
                entry.GetTypes("add", code.IndexOf("add = ")).Select(x => x.Name),
                "array"
            );
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void ExcessPositionalArguments() {
            var code = @"def f(a, *args):
    return args[0]

x = f('abc', 1)
y = f(1, 'abc')
z = f(None, 'abc', 1)
";
            var entry = ProcessTextV2(code);

            entry.AssertIsInstance("x", BuiltinTypeId.Int);
            entry.AssertIsInstance("y", BuiltinTypeId.Str);
            entry.AssertIsInstance("z", BuiltinTypeId.Str, BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void ExcessNamedArguments() {
            var code = @"def f(a, **args):
    return args[a]

x = f(a='b', b=1)
y = f(a='c', c=1.3)
z = f(a='b', b='abc')
w = f(a='p', p=1, q='abc')
";
            var entry = ProcessTextV2(code);

            entry.AssertIsInstance("x", BuiltinTypeId.Int);
            entry.AssertIsInstance("y", BuiltinTypeId.Float);
            entry.AssertIsInstance("z", BuiltinTypeId.Str);
            entry.AssertIsInstance("w", BuiltinTypeId.Str, BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING), Timeout(5000)]
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

        [TestMethod, Priority(UnitTestPriority.P2_FAILING)]
        [TestCategory("ExpectFail")]
        public void CartesianStarArgs() {
            // TODO: Figure out whether this is useful behaviour
            // It currently does not work because we no longer treat
            // the dict created by **args as a lasting object - it
            // exists solely for receiving arguments.

            var code = @"def f(a, **args):
    args['fob'] = a
    return args['fob']


x = f(42)
y = f('abc')";

            var entry = ProcessText(code);

            entry.AssertIsInstance("x", BuiltinTypeId.Int);
            entry.AssertIsInstance("y", BuiltinTypeId.Str);


            code = @"def f(a, **args):
    for i in xrange(2):
        if i == 1:
            return args['fob']
        else:
            args['fob'] = a

x = f(42)
y = f('abc')";

            entry = ProcessTextV2(code);

            entry.AssertIsInstance("x", BuiltinTypeId.Int);
            entry.AssertIsInstance("y", BuiltinTypeId.Str);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void CartesianRecursive() {
            var code = @"def f(a, *args):
    f(a, args)
    return a


x = f(42)";

            var entry = ProcessTextV2(code);

            AssertUtil.Contains(entry.GetTypeIds("x"), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void CartesianSimple() {
            var code = @"def f(a):
    return a


x = f(42)
y = f('fob')";

            var entry = ProcessTextV2(code);

            entry.AssertIsInstance("x", BuiltinTypeId.Int);
            entry.AssertIsInstance("y", BuiltinTypeId.Str);
        }


        [TestMethod, Priority(UnitTestPriority.P1)]
        public void CartesianLocals() {
            var code = @"def f(a):
    b = a
    return b


x = f(42)
y = f('fob')";

            var entry = ProcessTextV2(code);

            entry.AssertIsInstance("x", BuiltinTypeId.Int);
            entry.AssertIsInstance("y", BuiltinTypeId.Str);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void CartesianClosures() {
            var code = @"def f(a):
    def g():
        return a
    return g()


x = f(42)
y = f('fob')";

            var entry = ProcessTextV2(code);

            entry.AssertIsInstance("x", BuiltinTypeId.Int);
            entry.AssertIsInstance("y", BuiltinTypeId.Str);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void CartesianContainerFactory() {
            var code = @"def list_fact(ctor):
    x = []
    for abc in xrange(10):
        x.append(ctor(abc))
    return x


a = list_fact(int)[0]
b = list_fact(str)[0]
";

            var entry = ProcessTextV2(code);

            entry.AssertIsInstance("a", BuiltinTypeId.Int);
            entry.AssertIsInstance("b", BuiltinTypeId.Str);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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

            var entry = ProcessTextV2(code);

            entry.AssertIsInstance("x", BuiltinTypeId.Int);
            entry.AssertIsInstance("y", BuiltinTypeId.Str);
        }

        //        [TestMethod, Priority(UnitTestPriority.P1)]
        //        public void CartesianMerge() {
        //            var limits = GetLimits();
        //            // Ensure we include enough calls
        //            var callCount = limits.CallDepth * limits.DecreaseCallDepth + 1;
        //            var code = new StringBuilder(@"def f(a):
        //    return g(a)

        //def g(b):
        //    return h(b)

        //def h(c):
        //    return c

        //");
        //            for (int i = 0; i < callCount; ++i) {
        //                code.AppendLine("x = g(123)");
        //            }
        //            code.AppendLine("y = f(3.1415)");

        //            var text = code.ToString();
        //            Console.WriteLine(text);
        //            var entry = ProcessTextV2(text);

        //            entry.AssertIsInstance("x", BuiltinTypeId.Int, BuiltinTypeId.Float);
        //            entry.AssertIsInstance("y", BuiltinTypeId.Int, BuiltinTypeId.Float);
        //        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void ImportAs() {
            var entry = ProcessTextV2(@"import sys as s, array as a");

            AssertUtil.Contains(entry.GetMemberNames("s"), "winver");
            AssertUtil.Contains(entry.GetMemberNames("a"), "ArrayType");

            entry = ProcessTextV2(@"import sys as s");
            AssertUtil.Contains(entry.GetMemberNames("s"), "winver");
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void DictionaryKeyValues() {
            var code = @"x = {'abc': 42, 'oar': 'baz'}

i = x['abc']
s = x['oar']
";
            var entry = ProcessTextV2(code);

            entry.AssertIsInstance("i", BuiltinTypeId.Int);
            entry.AssertIsInstance("s", BuiltinTypeId.Str);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
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
            var entry = ProcessTextV2(code);

            entry.AssertIsInstance("x", BuiltinTypeId.List);
            entry.AssertIsInstance("y", BuiltinTypeId.List);
            entry.AssertIsInstance("x2", BuiltinTypeId.List);
            entry.AssertIsInstance("y2", BuiltinTypeId.List);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void RecursiveDictionaryKeyValues() {
            var code = @"x = {'abc': 42, 'oar': 'baz'}
x['abc'] = x
x[x] = 'abc'

i = x['abc']
s = x['abc']['abc']['abc']['oar']
t = x[x]
";
            var entry = ProcessTextV2(code);

            entry.AssertIsInstance("i", BuiltinTypeId.Int, BuiltinTypeId.Dict);
            entry.AssertIsInstance("s", BuiltinTypeId.Str);
            entry.AssertIsInstance("t", BuiltinTypeId.Str);

            code = @"x = {'y': None, 'value': 123 }
y = { 'x': x, 'value': 'abc' }
x['y'] = y

i = x['y']['x']['value']
s = y['x']['y']['value']
";
            entry = ProcessTextV2(code);

            entry.AssertIsInstance("i", BuiltinTypeId.Int);
            entry.AssertIsInstance("s", BuiltinTypeId.Str);
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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
            for (int retries = 100; retries > 0; --retries) {
                var entry = ProcessTextV2(code);

                var expectedIntType1 = new[] { BuiltinTypeId.Int };
                var expectedIntType2 = new[] { BuiltinTypeId.Int };
                var expectedTupleType1 = new[] { BuiltinTypeId.Tuple, BuiltinTypeId.NoneType };
                var expectedTupleType2 = new[] { BuiltinTypeId.Tuple, BuiltinTypeId.NoneType };

                entry.AssertIsInstance("x1", expectedIntType1);
                entry.AssertIsInstance("y1", expectedIntType1);
                entry.AssertIsInstance("_1", expectedTupleType1);
                entry.AssertIsInstance("item", code.IndexOf("x, y, _"), expectedTupleType1);
                entry.AssertIsInstance("x", code.IndexOf("x, y, _"), expectedIntType2);
                entry.AssertIsInstance("y", code.IndexOf("x, y, _"), expectedIntType2);
                entry.AssertIsInstance("_", code.IndexOf("x, y, _"), expectedTupleType2);
                entry.AssertIsInstance("self.top", code.IndexOf("x, y, _"), expectedTupleType2);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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
            var entry = ProcessTextV2(code);

            // Completing analysis is the main test, but we'll also ensure that
            // the right types are in the list.

            entry.AssertIsInstance("y", BuiltinTypeId.List, BuiltinTypeId.Int, BuiltinTypeId.Float, BuiltinTypeId.Str);
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void ImportStar() {
            var entry = ProcessText(@"
from nt import *
            ");

            var members = entry.GetMemberNames("", 1);

            AssertUtil.Contains(members, "abort");

            entry = ProcessText(@"");

            // make sure abort hasn't become a builtin, if so this test needs to be updated
            // with a new name
            if (entry.GetMemberNames("", 1).Contains("abort")) {
                Assert.Fail("abort has become a builtin, or a clean module includes it for some reason");
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void ImportTrailingComma() {
            var entry = ProcessText(@"
import nt,
            ", allowParseErrors: true);

            var members = entry.GetMemberNames("nt", 1);

            AssertUtil.Contains(members, "abort");
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void ImportStarCorrectRefs() {
            var text1 = @"
from mod2 import *

a = D()
";

            var text2 = @"
class D(object):
    pass
";

            var state = CreateAnalyzer();
            var mod1 = state.AddModule("mod1", text1);
            var mod2 = state.AddModule("mod2", text2);
            state.WaitForAnalysis(new CancellationTokenSource(15000).Token);

            state.AssertReferences(mod2, "D", 0,
                new VariableLocation(2, 1, VariableType.Value, "mod2"),
                new VariableLocation(2, 7, VariableType.Definition, "mod2"),
                new VariableLocation(4, 5, VariableType.Reference, "mod1")
            );
        }


        [TestMethod, Priority(UnitTestPriority.P1)]
        public void MutatingReferences() {
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

            var state = CreateAnalyzer();
            var mod1 = state.AddModule("mod1", text1);
            var mod2 = state.AddModule("mod2", text2);
            state.WaitForAnalysis();

            state.AssertReferences(mod1, "SomeMethod", text1.IndexOf("SomeMethod"),
                new VariableLocation(5, 5, VariableType.Value),
                new VariableLocation(5, 9, VariableType.Definition),
                new VariableLocation(5, 20, VariableType.Reference)
            );

            // mutate 1st file
            text1 = text1.Substring(0, text1.IndexOf("    def")) + Environment.NewLine + text1.Substring(text1.IndexOf("    def"));
            state.UpdateModule(mod1, text1);
            state.WaitForAnalysis();

            state.AssertReferences(mod1, "SomeMethod", text1.IndexOf("SomeMethod"),
                new VariableLocation(6, 5, VariableType.Value),
                new VariableLocation(6, 9, VariableType.Definition),
                new VariableLocation(5, 20, VariableType.Reference)
            );

            // mutate 2nd file
            text2 = Environment.NewLine + text2;
            state.UpdateModule(mod2, text2);
            state.UpdateModule(mod1, null);
            state.ReanalyzeAll();

            state.AssertReferences(mod1, "SomeMethod", text1.IndexOf("SomeMethod"),
                new VariableLocation(6, 5, VariableType.Value),
                new VariableLocation(6, 9, VariableType.Definition),
                new VariableLocation(6, 20, VariableType.Reference)
            );
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void MutatingCalls() {
            var text1 = @"
def f(abc):
    return abc
";

            var text2 = @"
import mod1
z = mod1.f(42)
";

            var state = CreateAnalyzer();
            var mod1 = state.AddModule("mod1", text1);
            var mod2 = state.AddModule("mod2", text2);
            state.WaitForAnalysis();

            state.AssertIsInstance(mod2, "z", BuiltinTypeId.Int);
            state.AssertIsInstance(mod1, "abc", text1.IndexOf("return abc"), BuiltinTypeId.Int);

            // change caller in text2
            text2 = @"
import mod1
z = mod1.f('abc')
";
            state.UpdateModule(mod2, text2);
            state.UpdateModule(mod1, null);
            state.WaitForAnalysis();

            state.AssertIsInstance(mod2, "z", BuiltinTypeId.Str);
            state.AssertIsInstance(mod1, "abc", text1.IndexOf("return abc"), BuiltinTypeId.Str);
        }

        /* Doesn't pass, we don't have a way to clear the assignments across modules...
        [TestMethod, Priority(UnitTestPriority.P1)]
        public void MutatingVariables() {
            using (var state = PythonAnalyzer.CreateSynchronously(InterpreterFactory, Interpreter)) {

                var text1 = @"
print(x)
";

                var text2 = @"
import mod1
mod1.x = x
";


                var text3 = @"
import mod2
mod2.x = 42
";
                var mod1 = state.AddModule("mod1", "mod1", null);
                Prepare(mod1, GetSourceUnit(text1, "mod1"));
                var mod2 = state.AddModule("mod2", "mod2", null);
                Prepare(mod2, GetSourceUnit(text2, "mod2"));
                var mod3 = state.AddModule("mod3", "mod3", null);
                Prepare(mod3, GetSourceUnit(text3, "mod3"));

                mod3.Analyze(CancellationToken.None);
                mod2.Analyze(CancellationToken.None);
                mod1.Analyze(CancellationToken.None);

                state.AnalyzeQueuedEntries(CancellationToken.None);

                AssertUtil.ContainsExactly(
                    mod1.Analysis.GetDescriptionsByIndex("x", text1.IndexOf("x")),
                    "int"
                );
                
                text3 = @"
import mod2
mod2.x = 'abc'
";

                Prepare(mod3, GetSourceUnit(text3, "mod3"));
                mod3.Analyze(CancellationToken.None);
                state.AnalyzeQueuedEntries(CancellationToken.None);
                state.AnalyzeQueuedEntries(CancellationToken.None);

                AssertUtil.ContainsExactly(
                    mod1.Analysis.GetDescriptionsByIndex("x", text1.IndexOf("x")),
                    "str"
                );
            }
        }
        */

        [TestMethod, Priority(UnitTestPriority.P1)]
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
            var entry = ProcessTextV2(code);

            entry.AssertHasAttrExact("self", code.IndexOf("_C__X"), "__X", "__init__", "__doc__", "__class__");
            entry.AssertHasAttrExact("self", code.IndexOf("print"), "_C__X", "__init__", "__doc__", "__class__");

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
            entry = ProcessTextV2(code);

            entry.AssertHasParameters("self.f", code.IndexOf("self.f"), "_C__A");
            entry.AssertHasParameters("self.f", code.IndexOf("marker"), "_C__A");

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

            entry = ProcessTextV2(code);
            entry.AssertHasAttr("self", code.IndexOf("self.__f"), "__f", "__init__");
            entry.AssertHasAttr("self", code.IndexOf("marker"), "_C__f", "__init__");

            code = @"
class C(object):
    __FOB = 42

    def f(self):
        abc = C.__FOB  # Completion should work here


xyz = C._C__FOB  # Advanced members completion should work here
";
            entry = ProcessTextV2(code);
            entry.AssertHasAttr("C", code.IndexOf("abc = "), "__FOB", "f");
            entry.AssertHasAttr("C", code.IndexOf("xyz = "), "_C__FOB", "f");

        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void BaseInstanceVariable() {
            var code = @"
class C:
    def __init__(self):
        self.abc = 42


class D(C):
    def __init__(self):        
        self.fob = self.abc
";

            var entry = ProcessTextV2(code);
            entry.AssertHasAttrExact("self.fob", code.IndexOf("self.fob"), entry.IntMembers);
            entry.AssertHasAttr("self", code.IndexOf("self.fob"), "abc");
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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

            var entry = ProcessTextV2(code);

            var clsA = entry.GetValue<IClassInfo>("A");
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

            entry = ProcessTextV2(code);

            var clsC = entry.GetValue<IClassInfo>("C");
            Assert.IsFalse(clsC.Mro.IsValid);

            // Unsuccessful: cannot order F and E
            code = @"
class F(object): remember2buy='spam'
class E(F): remember2buy='eggs'
class G(F,E): pass
G.remember2buy
";

            entry = ProcessTextV2(code);
            var clsG = entry.GetValue<IClassInfo>("G");
            Assert.IsFalse(clsG.Mro.IsValid);


            // Successful: exchanging bases of G fixes the ordering issue
            code = @"
class F(object): remember2buy='spam'
class E(F): remember2buy='eggs'
class G(E,F): pass
G.remember2buy
";

            entry = ProcessTextV2(code);
            clsG = entry.GetValue<IClassInfo>("G");
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

            entry = ProcessTextV2(code);
            var clsZ = entry.GetValue<IClassInfo>("Z");
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

            entry = ProcessTextV2(code);
            clsA = entry.GetValue<IClassInfo>("A");
            mroA = clsA.Mro.SelectMany(ns => ns.Select(n => n.ShortDescription)).ToList();
            AssertUtil.ContainsExactly(mroA, "A", "type int", "type object");

            var clsB = entry.GetValue<IClassInfo>("B");
            var mroB = clsB.Mro.SelectMany(ns => ns.Select(n => n.ShortDescription)).ToList();
            AssertUtil.ContainsExactly(mroB, "B", "type float", "type object");

            clsC = entry.GetValue<IClassInfo>("C");
            var mroC = clsC.Mro.SelectMany(ns => ns.Select(n => n.ShortDescription)).ToList();
            AssertUtil.ContainsExactly(mroC, "C", "type str", "type basestring", "type object");

            entry = ProcessTextV2(code);
            clsA = entry.GetValue<IClassInfo>("A");
            mroA = clsA.Mro.SelectMany(ns => ns.Select(n => n.ShortDescription)).ToList();
            AssertUtil.ContainsExactly(mroA, "A", "type int", "type object");

            clsB = entry.GetValue<IClassInfo>("B");
            mroB = clsB.Mro.SelectMany(ns => ns.Select(n => n.ShortDescription)).ToList();
            AssertUtil.ContainsExactly(mroB, "B", "type float", "type object");

            clsC = entry.GetValue<IClassInfo>("C");
            mroC = clsC.Mro.SelectMany(ns => ns.Select(n => n.ShortDescription)).ToList();
            AssertUtil.ContainsExactly(mroC, "C", "type str", "type object");
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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
                entry => {
                    var mro = entry.GetValue<IClassInfo>(entry.Modules["mod2"], "Test_test2").Mro.ToArray();
                    AssertUtil.AreEqual(mro.Select(m => m.First().Name), "Test_test2", "Test_test1", "object");
                }
            );
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void Iterator() {
            var entry = ProcessText(@"
A = [1, 2, 3]
B = 'abc'
C = [1.0, 'a', 3]

iA = iter(A)
iB = iter(B)
iC = iter(C)
", PythonLanguageVersion.V27);

            entry.AssertIsInstance("iA", BuiltinTypeId.ListIterator);
            entry.AssertIsInstance("B", BuiltinTypeId.Str);
            entry.AssertIsInstance("iB", BuiltinTypeId.StrIterator);
            entry.AssertIsInstance("iC", BuiltinTypeId.ListIterator);

            entry = ProcessText(@"
A = [1, 2, 3]
B = 'abc'
C = [1.0, 'a', 3]

iA = A.__iter__()
iB = B.__iter__()
iC = C.__iter__()
", PythonLanguageVersion.V27);

            entry.AssertIsInstance("iA", BuiltinTypeId.ListIterator);
            entry.AssertIsInstance("B", BuiltinTypeId.Str);
            entry.AssertIsInstance("iB", BuiltinTypeId.StrIterator);
            entry.AssertIsInstance("iC", BuiltinTypeId.ListIterator);


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

            entry.AssertIsInstance("a", BuiltinTypeId.Int);
            entry.AssertIsInstance("b", BuiltinTypeId.Str);
            entry.AssertIsInstance("c", BuiltinTypeId.Int, BuiltinTypeId.Str, BuiltinTypeId.Float);

            entry = ProcessText(@"
iA = iter(lambda: 1, 2)
iB = iter(lambda: 'abc', None)
iC = iter(lambda: 1, 'abc')

a = next(iA)
b = next(iB)
c = next(iC)
", PythonLanguageVersion.V27);

            entry.AssertIsInstance("a", BuiltinTypeId.Int);
            entry.AssertIsInstance("b", BuiltinTypeId.Str);
            entry.AssertIsInstance("c", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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

            entry.AssertIsInstance("a", BuiltinTypeId.Generator);
            entry.AssertIsInstance("b", BuiltinTypeId.Int);
            entry.AssertIsInstance("c", BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIds("d", 1));

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

            entry.AssertIsInstance("a1", BuiltinTypeId.Generator);
            entry.AssertIsInstance("b1", BuiltinTypeId.Int);
            entry.AssertIsInstance("a2", BuiltinTypeId.Generator);
            entry.AssertIsInstance("b2", BuiltinTypeId.Str);
            AssertUtil.ContainsExactly(entry.GetTypeIds("c", 1));
            AssertUtil.ContainsExactly(entry.GetTypeIds("d", 1));

            var text = @"
def f():
    yield 1
    x = yield 2

a = f()
b = a.next()
c = a.send('abc')
d = a.__next__()";
            entry = ProcessText(text, PythonLanguageVersion.V27);

            entry.AssertIsInstance("a", BuiltinTypeId.Generator);
            entry.AssertIsInstance("b", BuiltinTypeId.Int);
            entry.AssertIsInstance("c", BuiltinTypeId.Int);
            AssertUtil.ContainsExactly(entry.GetTypeIds("d", 1));
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void Generator3x() {
            var entry = ProcessTextV2(@"
def f():
    yield 1
    yield 2
    yield 3

a = f()
b = a.__next__()

for c in f():
    print(c)

d = a.next()");

            entry.AssertIsInstance("a", BuiltinTypeId.Generator);
            entry.AssertIsInstance("b", BuiltinTypeId.Int);
            entry.AssertIsInstance("c", BuiltinTypeId.Int);
            entry.AssertIsInstance("d");

            entry = ProcessTextV2(@"
def f(x):
    yield x

a1 = f(42)
b1 = a1.__next__()
a2 = f('abc')
b2 = a2.__next__()

for c in f(42):
    print(c)
d = a1.next()");

            entry.AssertIsInstance("a1", BuiltinTypeId.Generator);
            entry.AssertIsInstance("b1", BuiltinTypeId.Int);
            entry.AssertIsInstance("a2", BuiltinTypeId.Generator);
            entry.AssertIsInstance("b2", BuiltinTypeId.Str);
            entry.AssertIsInstance("c", BuiltinTypeId.Int);
            entry.AssertIsInstance("d");

            var text = @"
def f():
    yield 1
    x = yield 2

a = f()
b = a.__next__()
c = a.send('abc')
d = a.next()";
            entry = ProcessTextV2(text);

            entry.AssertIsInstance("a", BuiltinTypeId.Generator);
            entry.AssertIsInstance("b", BuiltinTypeId.Int);
            entry.AssertIsInstance("c", BuiltinTypeId.Int);
            entry.AssertIsInstance("x", text.IndexOf("x ="), BuiltinTypeId.Str);
            entry.AssertIsInstance("d");
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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

            entry.AssertReferences("f",
                new VariableLocation(2, 1, VariableType.Value),
                new VariableLocation(2, 5, VariableType.Definition),
                new VariableLocation(5, 2, VariableType.Reference));
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void LambdaInComprehension() {
            var text = "x = [(lambda a:[a**i for i in range(a+1)])(j) for j in range(5)]";

            var entry = ProcessText(text, PythonLanguageVersion.V33);
            entry.AssertIsInstance("x", BuiltinTypeId.List);

            entry = ProcessText(text, PythonLanguageVersion.V30);
            entry.AssertIsInstance("x", BuiltinTypeId.List);

            entry = ProcessText(text, PythonLanguageVersion.V27);
            entry.AssertIsInstance("x", BuiltinTypeId.List);
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void ExecReferences() {
            string text = @"
a = {}
b = """"
exec b in a
";

            var entry = ProcessTextV2(text);

            entry.AssertReferences("a", new VariableLocation(2, 1, VariableType.Definition), new VariableLocation(4, 11, VariableType.Reference));
            entry.AssertReferences("b", new VariableLocation(3, 1, VariableType.Definition), new VariableLocation(4, 6, VariableType.Reference));
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void PrivateMemberReferences() {
            string text = @"
class C:
    def __x(self):
        pass

    def y(self):
        self.__x()

    def g(self):
        self._C__x()
";

            var entry = ProcessTextV2(text);

            entry.AssertReferences("self.__x", text.IndexOf("self.__"),
                new VariableLocation(3, 5, VariableType.Value),
                new VariableLocation(3, 9, VariableType.Definition),
                new VariableLocation(7, 14, VariableType.Reference),
                new VariableLocation(10, 14, VariableType.Reference));
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void GeneratorComprehensions() {
            var text = @"
x = [2,3,4]
y = (a for a in x)
for z in y:
    print z
";

            var entry = ProcessTextV2(text);
            entry.AssertIsInstance("z", BuiltinTypeId.Int);

            text = @"
x = [2,3,4]
y = (a for a in x)

def f(iterable):
    for z in iterable:
        print z

f(y)
";

            entry = ProcessTextV2(text);
            entry.AssertIsInstance("z", text.IndexOf("print "), BuiltinTypeId.Int);

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

            entry = ProcessTextV2(text);
            entry.AssertIsInstance("y", BuiltinTypeId.Bool, BuiltinTypeId.NoneType);


            text = @"
def f(abc):
    print abc

(f(x) for x in [2,3,4])
";

            entry = ProcessTextV2(text);
            entry.AssertIsInstance("abc", text.IndexOf("print"), BuiltinTypeId.Int);
            entry.AssertReferences("f",
                new VariableLocation(2, 1, VariableType.Value),
                new VariableLocation(2, 5, VariableType.Definition),
                new VariableLocation(5, 2, VariableType.Reference));
        }


        [TestMethod, Priority(UnitTestPriority.P1)]
        public void ForSequence() {
            var entry = ProcessText(@"
x = [('abc', 42, True), ('abc', 23, False),]
for some_str, some_int, some_bool in x:
    print some_str
    print some_int
    print some_bool
");
            entry.AssertIsInstance("some_str", BuiltinTypeId.Str);
            entry.AssertIsInstance("some_int", BuiltinTypeId.Int);
            entry.AssertIsInstance("some_bool", BuiltinTypeId.Bool);
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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

            entry.AssertIsInstance("i", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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

            entry.AssertIsInstance("a", BuiltinTypeId.Int);
            entry.AssertIsInstance("b", BuiltinTypeId.Str);
            entry.AssertIsInstance("c", BuiltinTypeId.Str);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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

            entry.AssertIsInstance("b", BuiltinTypeId.Int);
            entry.AssertIsInstance("c", BuiltinTypeId.Str);
            entry.AssertIsInstance("d", BuiltinTypeId.Int, BuiltinTypeId.Str);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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

            entry.AssertIsInstance("a", BuiltinTypeId.Int);
            entry.AssertIsInstance("b", BuiltinTypeId.Float);
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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
            var entry = ProcessTextV2(code);

            // FIXME: https://pytools.codeplex.com/workitem/2898 (for IronPython only)
            entry.AssertIsInstance("x", code.IndexOf("x #"), BuiltinTypeId.NoneType);
            entry.AssertIsInstance("y", code.IndexOf("y #"), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void VarsSpecialization() {
            var entry = ProcessText(@"
x = vars()
k = x.keys()[0]
v = x['a']
");

            entry.AssertIsInstance("x", BuiltinTypeId.Dict);
            entry.AssertIsInstance("k", BuiltinTypeId.Str);
            entry.AssertIsInstance("v", BuiltinTypeId.Object);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void DirSpecialization() {
            var entry = ProcessText(@"
x = dir()
v = x[0]
");

            entry.AssertIsInstance("x", BuiltinTypeId.List);
            entry.AssertIsInstance("v", BuiltinTypeId.Str);
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void BuiltinSpecializations() {
            var entry = CreateAnalyzer();
            entry.AddModule("test-module", @"
expect_int = abs(1)
expect_float = abs(2.3)
expect_object = abs(object())
expect_str = abs('')

expect_bool = all()
expect_bool = any()
expect_str = ascii()
expect_str = bin()
expect_bool = callable()
expect_str = chr()
expect_list = dir()
expect_str = dir()[0]
expect_object = eval()
expect_str = format()
expect_dict = globals()
expect_object = globals()['']
expect_bool = hasattr()
expect_int = hash()
expect_str = hex()
expect_int = id()
expect_bool = isinstance()
expect_bool = issubclass()
expect_int = len()
expect_dict = locals()
expect_object = locals()['']
expect_str = oct()
expect_TextIOWrapper = open('')
expect_BufferedIOBase = open('', 'b')
expect_int = ord()
expect_int = pow(1, 1)
expect_float = pow(1.0, 1.0)
expect_str = repr()
expect_int = round(1)
expect_float = round(1.1)
expect_float = round(1, 1)
expect_list = sorted([0, 1, 2])
expect_int = sum(1, 2)
expect_float = sum(2.0, 3.0)
expect_dict = vars()
expect_object = vars()['']
");
            // The open() specialization uses classes from the io module,
            // so provide them here.
            entry.AddModule("io", @"
class TextIOWrapper(object): pass
class BufferedIOBase(object): pass
", "io.py");
            entry.WaitForAnalysis();

            entry.AssertIsInstance("expect_object", BuiltinTypeId.Object);
            entry.AssertIsInstance("expect_bool", BuiltinTypeId.Bool);
            entry.AssertIsInstance("expect_int", BuiltinTypeId.Int);
            entry.AssertIsInstance("expect_float", BuiltinTypeId.Float);
            entry.AssertIsInstance("expect_str", BuiltinTypeId.Str);
            entry.AssertIsInstance("expect_list", BuiltinTypeId.List);
            entry.AssertIsInstance("expect_dict", BuiltinTypeId.Dict);
            Assert.AreEqual("TextIOWrapper", entry.GetValue<IInstanceInfo>("expect_TextIOWrapper")?.ClassInfo?.Name);
            Assert.AreEqual("BufferedIOBase", entry.GetValue<IInstanceInfo>("expect_BufferedIOBase")?.ClassInfo?.Name);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void ListAppend() {
            var entry = ProcessText(@"
x = []
x.append('abc')
y = x[0]
");

            entry.AssertIsInstance("y", BuiltinTypeId.Str);

            entry = ProcessText(@"
x = []
x.extend(('abc', ))
y = x[0]
");
            entry.AssertIsInstance("y", BuiltinTypeId.Str);

            entry = ProcessText(@"
x = []
x.insert(0, 'abc')
y = x[0]
");
            entry.AssertIsInstance("y", BuiltinTypeId.Str);

            entry = ProcessText(@"
x = []
x.append('abc')
y = x.pop()
");

            entry.AssertIsInstance("y", BuiltinTypeId.Str);

            entry = ProcessText(@"
class ListTest(object):
    def reset(self):
        self.items = []
        self.pushItem(self)
    def pushItem(self, item):
        self.items.append(item)

a = ListTest().items
b = a[0]");

            entry.AssertIsInstance("a", BuiltinTypeId.List);
            entry.AssertIsInstance("b", "ListTest");
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void Slicing() {
            var entry = ProcessText(@"
x = [2]
y = x[:-1]
z = y[0]
");

            entry.AssertIsInstance("z", BuiltinTypeId.Int);

            entry = ProcessText(@"
x = (2, 3, 4)
y = x[:-1]
z = y[0]
");

            entry.AssertIsInstance("z", BuiltinTypeId.Int);

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
                entry.AssertIsInstance(name, text.IndexOf(name + " = "), BuiltinTypeId.Str);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void ConstantIndex() {
            var entry = ProcessText(@"
ZERO = 0
ONE = 1
TWO = 2
x = ['abc', 42, True]


some_str = x[ZERO]
some_int = x[ONE]
some_bool = x[TWO]
");
            entry.AssertIsInstance("some_str", BuiltinTypeId.Str);
            entry.AssertIsInstance("some_int", BuiltinTypeId.Int);
            entry.AssertIsInstance("some_bool", BuiltinTypeId.Bool);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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

            var result = entry.GetSignatures("C");
            Assert.AreEqual(result.Length, 1);
            Assert.AreEqual(result[0].Parameters.Length, 0);

            result = entry.GetSignatures("D");
            Assert.AreEqual(result.Length, 1);
            Assert.AreEqual(result[0].Parameters.Length, 0);

            result = entry.GetSignatures("E");
            Assert.AreEqual(result.Length, 1);
            Assert.AreEqual(result[0].Parameters.Length, 0);

            result = entry.GetSignatures("F");
            Assert.AreEqual(result.Length, 1);
            Assert.AreEqual(result[0].Parameters.Length, 1);

            result = entry.GetSignatures("G");
            Assert.AreEqual(result.Length, 1);
            Assert.AreEqual(result[0].Parameters.Length, 0);

            result = entry.GetSignatures("H");
            Assert.AreEqual(result.Length, 1);
            Assert.AreEqual(result[0].Parameters.Length, 1);
        }

        /// <summary>
        /// http://pytools.codeplex.com/workitem/798
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1)]
        public void ListSubclassSignatures() {
            var text = @"
class C(list):
    pass

a = C()
a.count";
            var entry = ProcessTextV2(text);

            entry.AssertIsInstance("a", text.IndexOf("a ="), "C");
            var result = entry.GetSignatures("a.count");
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(1, result[0].Parameters.Length);
        }


        [TestMethod, Priority(UnitTestPriority.P1)]
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

            var result = entry.GetSignatures("f");
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("func doc", result[0].Documentation);

            result = entry.GetSignatures("C");
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("class doc", result[0].Documentation);

            result = entry.GetSignatures("funicode");
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("unicode func doc", result[0].Documentation);

            result = entry.GetSignatures("CUnicode");
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("unicode class doc", result[0].Documentation);

            result = entry.GetSignatures("CNewStyle");
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("new-style class doc", result[0].Documentation);

            result = entry.GetSignatures("CInherited");
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("new-style class doc", result[0].Documentation);

            result = entry.GetSignatures("CInit");
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("init doc", result[0].Documentation);

            result = entry.GetSignatures("CUnicodeInit");
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("unicode init doc", result[0].Documentation);

            result = entry.GetSignatures("CNewStyleInit");
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("new-style init doc", result[0].Documentation);

            result = entry.GetSignatures("CInheritedInit");
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("new-style init doc", result[0].Documentation);
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void Ellipsis() {
            var entry = ProcessText(@"
x = ...
            ", PythonLanguageVersion.V31);

            var result = new List<IPythonType>(entry.GetTypes("x", 1));
            Assert.AreEqual(result.Count, 1);
            Assert.AreEqual(result[0].Name, "ellipsis");
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void Backquote() {
            var entry = ProcessText(@"x = `42`");

            var result = new List<IPythonType>(entry.GetTypes("x", 1));
            Assert.AreEqual(result.Count, 1);
            Assert.AreEqual(result[0].Name, "str");
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void BuiltinMethodSignatures() {
            var entry = ProcessText(@"
const = """".capitalize
constructed = str().capitalize
");

            string[] testCapitalize = new[] { "const", "constructed" };
            foreach (var test in testCapitalize) {
                var result = entry.GetSignatures(test, 1).ToArray();
                Assert.AreEqual(1, result.Length, $"Expected one signature for {test}");
                Assert.AreEqual(0, result[0].Parameters.Length, $"Expected no parameters for {test}.capitalize");
            }

            entry = ProcessText(@"
const = [].append
constructed = list().append
");

            var testAppend = new[] { "const", "constructed" };
            foreach (var test in testAppend) {
                var result = entry.GetSignatures(test, 1).ToArray();
                Console.WriteLine(string.Join(Environment.NewLine, result.Select(s => $"{s.Name}({string.Join(", ", s.Parameters.Select(p => p.Name))})")));
                Assert.AreEqual(1, result.Length, $"Expected one signature for {test}");
                Assert.AreEqual(1, result[0].Parameters.Length, $"Expected one parameter for {test}.append");
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void Del() {
            string text = @"
del fob
del fob[2]
del fob.oar
del (fob)
del fob, oar
";
            var entry = ProcessTextV2(text);

            // We do no analysis on del statements, nothing to test
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void TryExcept() {
            string text = @"
class MyException(Exception): pass

def f():
    try:
        pass
    except TypeError, e1:
        pass

def g():
    try:
        pass
    except MyException, e2:
        pass
";
            var entry = ProcessTextV2(text);

            AssertUtil.ContainsExactly(entry.GetTypes("e1", text.IndexOf(", e1")), entry.GetBuiltin("TypeError"));
            entry.AssertIsInstance("e2", text.IndexOf(", e2"), "MyException");
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void ConstantMath() {

            var text2x = @"
a = 1. + 2. + 3. # no type info for a, b or c
b = 1 + 2. + 3.
c = 1. + 2 + 3.
d = 1. + 2. + 3 # d is 'int', should be 'float'
e = 1 + 1L # e is a 'float', should be 'long' under v2.x (error under v3.x)
f = 1 / 2 # f is 'int', should be 'float' under v3.x";

            var entry = ProcessTextV2(text2x);
            entry.AssertIsInstance("a", BuiltinTypeId.Float);
            entry.AssertIsInstance("b", BuiltinTypeId.Float);
            entry.AssertIsInstance("c", BuiltinTypeId.Float);
            entry.AssertIsInstance("d", BuiltinTypeId.Float);
            entry.AssertIsInstance("e", BuiltinTypeId.Long);
            entry.AssertIsInstance("f", BuiltinTypeId.Int);

            var text3x = @"
a = 1. + 2. + 3. # no type info for a, b or c
b = 1 + 2. + 3.
c = 1. + 2 + 3.
d = 1. + 2. + 3 # d is 'int', should be 'float'
f = 1 / 2 # f is 'int', should be 'float' under v3.x";


            entry = ProcessTextV2(text3x);
            entry.AssertIsInstance("a", BuiltinTypeId.Float);
            entry.AssertIsInstance("b", BuiltinTypeId.Float);
            entry.AssertIsInstance("c", BuiltinTypeId.Float);
            entry.AssertIsInstance("d", BuiltinTypeId.Float);
            entry.AssertIsInstance("f", BuiltinTypeId.Float);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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

            var entry = ProcessTextV2(text);
            entry.AssertIsInstance("y", BuiltinTypeId.Unicode);
            entry.AssertIsInstance("y1", BuiltinTypeId.Str);
            entry.AssertIsInstance("oar", BuiltinTypeId.Str);
            entry.AssertIsInstance("oar2", BuiltinTypeId.Unicode);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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

            var entry = ProcessTextV2(text);
            entry.AssertIsInstance("y", BuiltinTypeId.Unicode);
            entry.AssertIsInstance("y1", BuiltinTypeId.Str);
            entry.AssertIsInstance("oar", BuiltinTypeId.Str);
            entry.AssertIsInstance("oar2", BuiltinTypeId.Unicode);
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void StringFormattingV36() {
            var text = @"
y = f'abc {42}'
ry = rf'abc {42}'
yr = fr'abc {42}'
fadd = f'abc{42}' + f'{42}'

def f(val):
    print(val)
f'abc {f(42)}'
";

            var entry = ProcessText(text, PythonLanguageVersion.V36);
            entry.AssertIsInstance("y", BuiltinTypeId.Str);
            entry.AssertIsInstance("ry", BuiltinTypeId.Str);
            entry.AssertIsInstance("yr", BuiltinTypeId.Str);
            entry.AssertIsInstance("fadd", BuiltinTypeId.Str);

            // TODO: Enable analysis of f-strings
            //entry.AssertIsInstance("val",  BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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

            var entry = ProcessTextV2(text);
            entry.AssertIsInstance("x", BuiltinTypeId.Unicode);
            entry.AssertIsInstance("y", BuiltinTypeId.Unicode);
            entry.AssertIsInstance("y1", BuiltinTypeId.Str);
            entry.AssertIsInstance("oar", BuiltinTypeId.Str);
            entry.AssertIsInstance("oar2", BuiltinTypeId.Unicode);

            text = @"
x = u'abc %d'
y = 100 * x


x1 = 'abc %d'
y1 = 100 * x1

fob = 'abc %d'.lower()
oar = 100 * fob

fob2 = u'abc' + u'%d'
oar2 = 100 * fob2";

            entry = ProcessTextV2(text);
            entry.AssertIsInstance("y", BuiltinTypeId.Unicode);
            entry.AssertIsInstance("y1", BuiltinTypeId.Str);
            entry.AssertIsInstance("oar", BuiltinTypeId.Str);
            entry.AssertIsInstance("oar2", BuiltinTypeId.Unicode);
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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
            AssertUtil.ContainsExactly(entry.GetShortDescriptions("a", 0), "bool");

            entry = ProcessText(text, PythonLanguageVersion.V33);
            AssertUtil.ContainsExactly(entry.GetShortDescriptions("a", 0), "bool");
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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

                entry.AssertIsInstance("a", text.IndexOf("a ="), "Result");
                entry.AssertIsInstance("b", text.IndexOf("b ="), "Result");
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void TrueDividePython3x() {
            var text = @"
class C:
    def __truediv__(self, other):
        return 42
    def __rtruediv__(self, other):
        return 3.0

a = C()
b = a / 'abc'
c = 'abc' / a
";

            var entry = ProcessText(text, PythonLanguageVersion.V35);
            AssertUtil.ContainsExactly(entry.GetShortDescriptions("b", text.IndexOf("b =")), "int");
            AssertUtil.ContainsExactly(entry.GetShortDescriptions("c", text.IndexOf("c =")), "float");
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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
                var code = String.Format(text, test.Method, test.Operator);
                if (test.Version.Is3x()) {
                    code = code.Replace("42L", "42");
                }
                using (var state = ProcessText(code, test.Version)) {
                    state.AssertIsInstance("a", text.IndexOf("a ="), "ForwardResult");
                    state.AssertIsInstance("b", text.IndexOf("b ="), "ReverseResult");
                    state.AssertIsInstance("c", text.IndexOf("c ="), "ReverseResult");
                    state.AssertIsInstance("d", text.IndexOf("d ="), "ForwardResult");
                    state.AssertIsInstance("e", text.IndexOf("e ="), "ReverseResult");
                    state.AssertIsInstance("f", text.IndexOf("f ="), "ForwardResult");
                    state.AssertIsInstance("g", text.IndexOf("g ="), "ForwardResult");
                    state.AssertIsInstance("h", text.IndexOf("h ="), "ReverseResult");
                    state.AssertIsInstance("i", text.IndexOf("i ="), "ForwardResult");
                    state.AssertIsInstance("j", text.IndexOf("j ="), "ReverseResult");
                    state.AssertIsInstance("k", text.IndexOf("k ="), "ForwardResult");
                    state.AssertIsInstance("l", text.IndexOf("l ="), "ReverseResult");
                    // We assume that augmented assignments keep their type
                    state.AssertIsInstance("m", code.IndexOf("m " + test.Operator), "C");
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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

            var entry = ProcessTextV2(text);
            entry.AssertIsInstance("x1", BuiltinTypeId.Tuple);
            entry.AssertIsInstance("y1", BuiltinTypeId.Tuple);
            AssertUtil.ContainsExactly(entry.GetTypeIds("y1v", 1));
            entry.AssertIsInstance("y2", BuiltinTypeId.Tuple);
            entry.AssertIsInstance("y2v", BuiltinTypeId.Int, BuiltinTypeId.Float);
            entry.AssertIsInstance("y3", BuiltinTypeId.List);
            entry.AssertIsInstance("y3v", BuiltinTypeId.Int, BuiltinTypeId.Float);
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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

            var entry = ProcessTextV2(text);
            entry.AssertDescription("y", text.IndexOf("y ="), "tuple");
            entry.AssertDescription("y1", text.IndexOf("y1 ="), "tuple[int]");
            entry.AssertDescription("oar", text.IndexOf("oar ="), "list[int]");
            entry.AssertDescription("oar2", text.IndexOf("oar2 ="), "list");

            text = @"
x = ()
y = 100 * x

x1 = (1,2,3)
y1 = 100 * x1

fob = [1,2,3]
oar = 100 * fob 

fob2 = []
oar2 = 100 * fob2";

            entry = ProcessTextV2(text);
            entry.AssertDescription("y", text.IndexOf("y ="), "tuple");
            entry.AssertDescription("y1", text.IndexOf("y1 ="), "tuple[int]");
            entry.AssertDescription("oar", text.IndexOf("oar ="), "list[int]");
            entry.AssertDescription("oar2", text.IndexOf("oar2 ="), "list");
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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

            var entry = ProcessTextV2(text);
            entry.AssertIsInstance("t1", BuiltinTypeId.Bool);
            entry.AssertIsInstance("t2", BuiltinTypeId.Bool);
            entry.AssertIsInstance("l1", BuiltinTypeId.Bool);
            entry.AssertIsInstance("l2", BuiltinTypeId.Bool);
            entry.AssertIsInstance("s1", BuiltinTypeId.Bool);
            entry.AssertIsInstance("s2", BuiltinTypeId.Bool);
            entry.AssertIsInstance("d1", BuiltinTypeId.Bool);
            entry.AssertIsInstance("d2", BuiltinTypeId.Bool);
            entry.AssertIsInstance("r1", BuiltinTypeId.Bool);
            entry.AssertIsInstance("r2", BuiltinTypeId.Bool);

        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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

            var entry = ProcessTextV2(text);
            AssertUtil.Contains(entry.GetMemberNames("self.fob", text.IndexOf("self.fob")), "nodesc_method");
        }

        /// <summary>
        /// Verifies that a line in triple quoted string which ends with a \ (eating the newline) doesn't throw
        /// off our newline tracking.
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1)]
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
            var entry = ProcessTextV2(text);
            entry.AssertReferences("self.abc", text.IndexOf("self.abc"),
                new VariableLocation(9, 14, VariableType.Definition),
                new VariableLocation(10, 18, VariableType.Reference),
                new VariableLocation(11, 20, VariableType.Reference));
            entry.AssertReferences("fob", text.IndexOf("= fob"),
                new VariableLocation(8, 24, VariableType.Definition),
                new VariableLocation(9, 20, VariableType.Reference));
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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
            var entry = ProcessTextV2(text);
            entry.AssertReferences("self.abc", text.IndexOf("self.abc"),
                new VariableLocation(5, 14, VariableType.Definition),
                new VariableLocation(6, 18, VariableType.Reference),
                new VariableLocation(7, 20, VariableType.Reference));
            entry.AssertReferences("fob", text.IndexOf("= fob"),
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
            entry = ProcessTextV2(text);

            entry.AssertReferences("self.abc", text.IndexOf("self.abc"),
                new VariableLocation(5, 14, VariableType.Definition),
                new VariableLocation(6, 18, VariableType.Reference),
                new VariableLocation(7, 20, VariableType.Reference));
            entry.AssertReferences("fob", text.IndexOf("= fob"),
                new VariableLocation(4, 24, VariableType.Definition),
                new VariableLocation(5, 20, VariableType.Reference));
            entry.AssertReferences("D", text.IndexOf("D(42)"),
                new VariableLocation(9, 1, VariableType.Reference),
                new VariableLocation(3, 1, VariableType.Value),
                new VariableLocation(3, 7, VariableType.Definition));

            // function definitions
            text = @"
def f(): pass

x = f()";
            entry = ProcessTextV2(text);
            entry.AssertReferences("f", text.IndexOf("x ="),
                new VariableLocation(2, 1, VariableType.Value),
                new VariableLocation(2, 5, VariableType.Definition),
                new VariableLocation(4, 5, VariableType.Reference));



            text = @"
def f(): pass

x = f";
            entry = ProcessTextV2(text);
            entry.AssertReferences("f", text.IndexOf("x ="),
                new VariableLocation(2, 1, VariableType.Value),
                new VariableLocation(2, 5, VariableType.Definition),
                new VariableLocation(4, 5, VariableType.Reference));

            // class variables
            text = @"

class D(object):
    abc = 42
    print abc
    del abc
";
            entry = ProcessTextV2(text);

            entry.AssertReferences("abc", text.IndexOf("abc ="),
                new VariableLocation(4, 5, VariableType.Definition),
                new VariableLocation(5, 11, VariableType.Reference),
                new VariableLocation(6, 9, VariableType.Reference));

            // class definition
            text = @"
class D(object): pass

a = D
";
            entry = ProcessTextV2(text);
            entry.AssertReferences("D", text.IndexOf("a ="),
                new VariableLocation(2, 1, VariableType.Value),
                new VariableLocation(2, 7, VariableType.Definition),
                new VariableLocation(4, 5, VariableType.Reference));

            // method definition
            text = @"
class D(object): 
    def f(self): pass

a = D().f()
";
            entry = ProcessTextV2(text);
            entry.AssertReferences("D().f", text.IndexOf("a ="),
                new VariableLocation(3, 5, VariableType.Value),
                new VariableLocation(3, 9, VariableType.Definition),
                new VariableLocation(5, 9, VariableType.Reference));

            // globals
            text = @"
abc = 42
print abc
del abc
";
            entry = ProcessTextV2(text);
            entry.AssertReferences("abc", text.IndexOf("abc ="),
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(4, 5, VariableType.Reference),
                new VariableLocation(3, 7, VariableType.Reference));

            // parameters
            text = @"
def f(abc):
    print abc
    abc = 42
    del abc
";
            entry = ProcessTextV2(text);
            entry.AssertReferences("abc", text.IndexOf("abc ="),
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
            entry = ProcessTextV2(text);
            entry.AssertReferences("abc", text.IndexOf("abc"),
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
    except TypeError: pass
    else:
        abc
";
            entry = ProcessTextV2(text);
            entry.AssertReferences("abc", text.IndexOf("try:"),
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
            entry = ProcessTextV2(text);
            entry.AssertReferences("abc", text.IndexOf("x ="),
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
            entry = ProcessTextV2(text);
            entry.AssertReferences("a", text.IndexOf("a = "),
                //new VariableLocation(2, 7, VariableType.Definition),
                new VariableLocation(4, 15, VariableType.Reference),
                new VariableLocation(5, 27, VariableType.Reference),
                new VariableLocation(6, 9, VariableType.Definition)
            );
            entry.AssertReferences("a", text.IndexOf("print(a)"),
                //new VariableLocation(2, 7, VariableType.Definition),
                new VariableLocation(4, 15, VariableType.Reference),
                new VariableLocation(5, 27, VariableType.Reference),
                new VariableLocation(6, 9, VariableType.Definition)
            );

        }


        [TestMethod, Priority(UnitTestPriority.P1)]
        public void ListDictArgReferences() {
            var text = @"
def f(*a, **k):
    x = a[1]
    y = k['a']

#out
a = 1
k = 2
";
            var entry = ProcessText(text);
            entry.AssertReferences("a", text.IndexOf("a["),
                new VariableLocation(2, 8, VariableType.Definition),
                new VariableLocation(3, 9, VariableType.Reference)
            );
            entry.AssertReferences("k", text.IndexOf("k["),
                new VariableLocation(2, 13, VariableType.Definition),
                new VariableLocation(4, 9, VariableType.Reference)
            );
            entry.AssertReferences("a", text.IndexOf("#out"),
                new VariableLocation(7, 1, VariableType.Definition)
            );
            entry.AssertReferences("k", text.IndexOf("#out"),
                new VariableLocation(8, 1, VariableType.Definition)
            );
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void KeywordArgReferences() {
            var text = @"
def f(a):
    pass

f(a=1)
";
            var entry = ProcessText(text);
            entry.AssertReferences("a", text.IndexOf("a"),
                new VariableLocation(2, 7, VariableType.Definition),
                new VariableLocation(5, 3, VariableType.Reference)
            );
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void ReferencesCrossModule() {
            var fobText = @"
from oar import abc

abc()
";
            var oarText = "class abc(object): pass";

            var state = CreateAnalyzer();
            var fobMod = state.AddModule("fob", fobText);
            var oarMod = state.AddModule("oar", oarText);
            state.WaitForAnalysis();

            state.AssertReferences(oarMod, "abc", 0,
                new VariableLocation(1, 1, VariableType.Value, "oar"),          // definition range
                new VariableLocation(1, 7, VariableType.Definition, "oar"),     // definition name
                new VariableLocation(2, 17, VariableType.Reference, "fob"),     // import
                new VariableLocation(4, 1, VariableType.Reference, "fob")       // call
            );
        }

        [TestMethod, Priority(UnitTestPriority.P2_FAILING), TestCategory("ExpectFail")]
        public void SuperclassMemberReferencesCrossModule() {
            // https://github.com/Microsoft/PTVS/issues/2271

            var fobText = @"
from oar import abc

class bcd(abc):
    def test(self):
        self.x
";
            var oarText = @"class abc(object):
    def __init__(self):
        self.x = 123
";

            var state = CreateAnalyzer();
            var fobMod = state.AddModule("fob", fobText);
            var oarMod = state.AddModule("oar", oarText);
            state.WaitForAnalysis();

            state.AssertReferences(fobMod, "self.x", oarText.IndexOfEnd("self.x"),
                new VariableLocation(3, 14, VariableType.Definition, "oar"),
                new VariableLocation(6, 14, VariableType.Reference, "fob")
            );
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void ReferencesCrossMultiModule() {
            var fobText = @"
from oarbaz import abc

abc()
";
            var oarText = "class abc1(object): pass";
            var bazText = "\n\n\n\nclass abc2(object): pass";
            var oarBazText = @"from oar import abc1 as abc
from baz import abc2 as abc";

            var state = CreateAnalyzer(DefaultFactoryV2);

            var fobMod = state.AddModule("fob", fobText);
            var oarMod = state.AddModule("oar", oarText);
            var bazMod = state.AddModule("baz", bazText);
            var oarBazMod = state.AddModule("oarbaz", oarBazText);

            state.ReanalyzeAll();

            state.AssertReferences(oarMod, "abc1", oarText.IndexOf("abc1"),
                new VariableLocation(1, 1, VariableType.Value),
                new VariableLocation(1, 7, VariableType.Definition),
                new VariableLocation(1, 17, VariableType.Reference, "oarbaz"),
                new VariableLocation(1, 25, VariableType.Reference, "oarbaz")
            );
            state.AssertReferences(bazMod, "abc2", bazText.IndexOf("abc2"),
                new VariableLocation(5, 1, VariableType.Value),
                new VariableLocation(5, 7, VariableType.Definition),
                new VariableLocation(2, 17, VariableType.Reference, "oarbaz"),
                new VariableLocation(2, 25, VariableType.Reference, "oarbaz")
            );
            state.AssertReferences(fobMod, "abc", 0,
                new VariableLocation(1, 1, VariableType.Value, "oar"),
                new VariableLocation(5, 1, VariableType.Value, "baz"),
                new VariableLocation(1, 17, VariableType.Reference, "oarbaz"),
                new VariableLocation(1, 25, VariableType.Reference, "oarbaz"),    // as
                new VariableLocation(2, 17, VariableType.Reference, "oarbaz"),
                new VariableLocation(2, 25, VariableType.Reference, "oarbaz"),    // as
                new VariableLocation(2, 20, VariableType.Reference, "fob"),    // import
                new VariableLocation(4, 1, VariableType.Reference, "fob")     // call
            );
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void ImportStarReferences() {
            var state = CreateAnalyzer();
            var fobMod = state.AddModule("fob", @"
CONSTANT = 1
class Class: pass
def fn(): pass");
            var oarMod = state.AddModule("oar", @"from fob import *



x = CONSTANT
c = Class()
f = fn()");

            state.ReanalyzeAll();

            state.AssertReferences(oarMod, "CONSTANT", 0,
                new VariableLocation(2, 1, VariableType.Definition, "fob"),
                new VariableLocation(5, 5, VariableType.Reference, "oar")
            );
            state.AssertReferences(oarMod, "Class", 0,
                new VariableLocation(3, 1, VariableType.Value, "fob"),
                new VariableLocation(3, 7, VariableType.Definition, "fob"),
                new VariableLocation(6, 5, VariableType.Reference, "oar")
            );
            state.AssertReferences(oarMod, "fn", 0,
                new VariableLocation(4, 1, VariableType.Value, "fob"),
                new VariableLocation(4, 5, VariableType.Definition, "fob"),
                new VariableLocation(7, 5, VariableType.Reference, "oar")
            );
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void ImportAsReferences() {
            var state = CreateAnalyzer();
            var fobMod = state.AddModule("fob", @"
CONSTANT = 1
class Class: pass
def fn(): pass");
            var oarMod = state.AddModule("oar", @"from fob import CONSTANT as CO, Class as Cl, fn as f



x = CO
c = Cl()
g = f()");

            state.ReanalyzeAll();

            state.AssertReferences(oarMod, "CO", 0,
                new VariableLocation(1, 17, VariableType.Reference, "oar"),
                new VariableLocation(1, 29, VariableType.Reference, "oar"),
                new VariableLocation(2, 1, VariableType.Definition, "fob"),
                new VariableLocation(5, 5, VariableType.Reference, "oar")
            );
            state.AssertReferences(oarMod, "Cl", 0,
                new VariableLocation(1, 33, VariableType.Reference, "oar"),
                new VariableLocation(1, 42, VariableType.Reference, "oar"),
                new VariableLocation(3, 1, VariableType.Value, "fob"),
                new VariableLocation(3, 7, VariableType.Definition, "fob"),
                new VariableLocation(6, 5, VariableType.Reference, "oar")
            );
            state.AssertReferences(oarMod, "f", 0,
                new VariableLocation(1, 46, VariableType.Reference, "oar"),
                new VariableLocation(1, 52, VariableType.Reference, "oar"),
                new VariableLocation(4, 1, VariableType.Value, "fob"),
                new VariableLocation(4, 5, VariableType.Definition, "fob"),
                new VariableLocation(7, 5, VariableType.Reference, "oar")
            );

            state.AssertReferences(fobMod, "CONSTANT", 0,
                new VariableLocation(1, 17, VariableType.Reference, "oar"),
                new VariableLocation(1, 29, VariableType.Reference, "oar"),
                new VariableLocation(2, 1, VariableType.Definition, "fob"),
                new VariableLocation(5, 5, VariableType.Reference, "oar")
            );
            state.AssertReferences(fobMod, "Class", 0,
                new VariableLocation(1, 33, VariableType.Reference, "oar"),
                new VariableLocation(1, 42, VariableType.Reference, "oar"),
                new VariableLocation(3, 1, VariableType.Value, "fob"),
                new VariableLocation(3, 7, VariableType.Definition, "fob"),
                new VariableLocation(6, 5, VariableType.Reference, "oar")
            );
            state.AssertReferences(fobMod, "fn", 0,
                new VariableLocation(1, 46, VariableType.Reference, "oar"),
                new VariableLocation(1, 52, VariableType.Reference, "oar"),
                new VariableLocation(4, 1, VariableType.Value, "fob"),
                new VariableLocation(4, 5, VariableType.Definition, "fob"),
                new VariableLocation(7, 5, VariableType.Reference, "oar")
            );
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void ReferencesGeneratorsV3() {
            var text = @"
[f for f in x]
[x for x in f]
(g for g in y)
(y for y in g)
";
            using (var entry = ProcessTextV2(text)) {
                entry.AssertReferences("f", text.IndexOf("f for"),
                    new VariableLocation(2, 2, VariableType.Reference),
                    new VariableLocation(2, 8, VariableType.Definition)
                );
                entry.AssertReferences("x", text.IndexOf("x for"),
                    new VariableLocation(3, 2, VariableType.Reference),
                    new VariableLocation(3, 8, VariableType.Definition)
                );
                entry.AssertReferences("g", text.IndexOf("g for"),
                    new VariableLocation(4, 2, VariableType.Reference),
                    new VariableLocation(4, 8, VariableType.Definition)
                );
                entry.AssertReferences("y", text.IndexOf("y for"),
                    new VariableLocation(5, 2, VariableType.Reference),
                    new VariableLocation(5, 8, VariableType.Definition)
                );
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void ReferencesGeneratorsV2() {
            var text = @"
[f for f in x]
[x for x in f]
(g for g in y)
(y for y in g)
";
            using (var entry = ProcessTextV2(text)) {
                // Index variable leaks out of list comprehension
                entry.AssertReferences("f", text.IndexOf("f for"),
                    new VariableLocation(2, 2, VariableType.Reference),
                    new VariableLocation(2, 8, VariableType.Definition),
                    new VariableLocation(3, 13, VariableType.Reference)
                );
                entry.AssertReferences("x", text.IndexOf("x for"),
                    new VariableLocation(3, 2, VariableType.Reference),
                    new VariableLocation(3, 8, VariableType.Definition)
                );
                entry.AssertReferences("g", text.IndexOf("g for"),
                    new VariableLocation(4, 2, VariableType.Reference),
                    new VariableLocation(4, 8, VariableType.Definition)
                );
                entry.AssertReferences("y", text.IndexOf("y for"),
                    new VariableLocation(5, 2, VariableType.Reference),
                    new VariableLocation(5, 8, VariableType.Definition)
                );
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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
                new { FuncName = "m", ParamName="x", DefaultValue = "math.atan2(1, 0)" },
            };

            foreach (var test in tests) {
                var result = entry.GetSignatures(test.FuncName, 1).ToArray();
                Assert.AreEqual(1, result.Length);
                Assert.AreEqual(1, result[0].Parameters.Length);
                Assert.AreEqual(test.ParamName, result[0].Parameters[0].Name);
                Assert.AreEqual(test.DefaultValue, result[0].Parameters[0].DefaultValue);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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
                entry.AssertIsInstance("fob", code.IndexOf("print(fob)"), BuiltinTypeId.Int);
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
                entry.AssertIsInstance("fob", BuiltinTypeId.Int);
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
                entry.AssertIsInstance("fob", BuiltinTypeId.Tuple);
                entry.AssertDescription("fob", "tuple[int]");
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
                int i = code.IndexOf("print(fob)");
                entry.AssertIsInstance("fob", i, BuiltinTypeId.Tuple);
                entry.AssertDescription("fob", i, "tuple[int]");
            }
        }

        /// <summary>
        /// Verifies that list indicies don't accumulate classes across multiple analysis
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1)]
        public void ListIndiciesCrossModuleAnalysis() {
            for (int i = 0; i < 2; i++) {
                var code1 = "l = []";
                var code2 = @"class C(object):
    pass

a = C()
import mod1
mod1.l.append(a)
";

                var state = CreateAnalyzer();
                var mod1 = state.AddModule("mod1", code1);
                var mod2 = state.AddModule("mod2", code2);
                state.ReanalyzeAll();

                if (i == 0) {
                    // re-preparing shouldn't be necessary
                    state.UpdateModule(mod2, code2);
                }

                mod2.PreAnalyze();
                state.WaitForAnalysis();

                state.AssertDescription("l", "list[C]");
                state.AssertIsInstance("l[0]", "C");
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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
            entry.AssertIsInstance("fob", code.IndexOf("print(fob)"), BuiltinTypeId.Int);
            entry.AssertIsInstance("oar", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void SetLiteral() {
            var code = @"
x = {2, 3, 4}
for abc in x:
    print(abc)
";
            var entry = ProcessText(code);
            entry.AssertDescription("x", "set[int]");
            entry.AssertIsInstance("abc", code.IndexOf("print(abc)"), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
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
            entry.AssertIsInstance("x", BuiltinTypeId.Set);
            entry.AssertIsInstance("y", BuiltinTypeId.Set);
            foreach (var op in new[] { "or", "and", "sub", "xor" }) {
                entry.AssertIsInstance("x_" + op + "_y", BuiltinTypeId.Set);
                entry.AssertIsInstance("y_" + op + "_x", BuiltinTypeId.Set);

                if (op == "or") {
                    entry.AssertIsInstance("x_" + op + "_y_0", BuiltinTypeId.Int, BuiltinTypeId.Float);
                    entry.AssertIsInstance("y_" + op + "_x_0", BuiltinTypeId.Int, BuiltinTypeId.Float);
                } else {
                    entry.AssertIsInstance("x_" + op + "_y_0", BuiltinTypeId.Int);
                    entry.AssertIsInstance("y_" + op + "_x_0", BuiltinTypeId.Float);
                }
            }

        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void GetVariablesDictionaryGet() {
            var entry = ProcessText(@"x = {42:'abc'}");
            entry.AssertDescription("x.get", "bound built-in method get");
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void DictMethods() {
            var entry = ProcessTextV2("x = {42:'abc'}");

            entry.AssertIsInstance("x.items()[0][0]", BuiltinTypeId.Int);
            entry.AssertIsInstance("x.items()[0][1]", BuiltinTypeId.Str);
            entry.AssertIsInstance("x.keys()[0]", BuiltinTypeId.Int);
            entry.AssertIsInstance("x.values()[0]", BuiltinTypeId.Str);
            entry.AssertIsInstance("x.pop(1)", BuiltinTypeId.Str);
            entry.AssertIsInstance("x.popitem()[0]", BuiltinTypeId.Int);
            entry.AssertIsInstance("x.popitem()[1]", BuiltinTypeId.Str);
            entry.AssertIsInstance("x.iterkeys().next()", BuiltinTypeId.Int);
            entry.AssertIsInstance("x.itervalues().next()", BuiltinTypeId.Str);
            entry.AssertIsInstance("x.iteritems().next()[0]", BuiltinTypeId.Int);
            entry.AssertIsInstance("x.iteritems().next()[1]", BuiltinTypeId.Str);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void DictUpdate() {
            var entry = ProcessTextV2(@"
a = {42:100}
b = {}
b.update(a)
");

            entry.AssertIsInstance("b.items()[0][0]", BuiltinTypeId.Int);
            entry.AssertIsInstance("b.items()[0][1]", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void DictEnum() {
            var entry = ProcessText(@"
for x in {42:'abc'}:
    print(x)
");

            entry.AssertIsInstance("x", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void FutureDivision() {
            var entry = ProcessText(@"
from __future__ import division
x = 1/2
            ");

            entry.AssertIsInstance("x", BuiltinTypeId.Float);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void BoundMethodDescription() {
            var entry = ProcessText(@"
class C:
    def f(self):
        'doc string'

a = C()
b = a.f
            ");

            entry.AssertDescription("b", "method f of test-module.C objects");

            entry = ProcessText(@"
class C(object):
    def f(self):
        'doc string'

a = C()
b = a.f
            ");

            entry.AssertDescription("b", "method f of test-module.C objects");
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void LambdaExpression() {
            var entry = ProcessText(@"
x = lambda a: a
y = x(42)
");

            entry.AssertIsInstance("y", BuiltinTypeId.Int);

            entry = ProcessText(@"
def f(a):
    return a

x = lambda b: f(b)
y = x(42)
");

            entry.AssertIsInstance("y", BuiltinTypeId.Int);

        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void LambdaScoping() {
            var code = @"def f(l1, l2):
    l1('abc')
    l2(42)


x = []
y = ()
f(lambda x=x:x, lambda x=y:x)";

            var entry = ProcessText(code);

            // default value, should be a list
            entry.AssertIsInstance("x", code.IndexOfEnd("lambda x=x"), BuiltinTypeId.List);

            // parameter used in the lambda, should be list and str
            entry.AssertIsInstance("x", code.IndexOfEnd("lambda x=x:x"), BuiltinTypeId.List, BuiltinTypeId.Str);

            // default value in the 2nd lambda, should be tuple
            entry.AssertIsInstance("y", code.IndexOfEnd("lambda x=y"), BuiltinTypeId.Tuple);

            // value in the 2nd lambda, should be tuple and int
            entry.AssertIsInstance("x", code.IndexOfEnd("lambda x=y:x"), BuiltinTypeId.Tuple, BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void FunctionScoping() {
            var code = @"x = 100

def f(x = x):
    x

f('abc')
";

            var entry = ProcessText(code);

            entry.AssertReferences("x", code.IndexOf("def") + 6,
                new VariableLocation(3, 7, VariableType.Definition),
                new VariableLocation(4, 5, VariableType.Reference));

            entry.AssertReferences("x", code.IndexOf("def") + 10,
                new VariableLocation(1, 1, VariableType.Definition),
                new VariableLocation(3, 11, VariableType.Reference));

            entry.AssertReferences("x", code.IndexOf("    x") + 4,
                new VariableLocation(3, 7, VariableType.Definition),
                new VariableLocation(4, 5, VariableType.Reference));
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void RecursiveClass() {
            var entry = ProcessText(@"
cls = object

class cls(cls):
    abc = 42
");

            entry.GetMemberNames("cls", 1);
            entry.AssertIsInstance("cls().abc", BuiltinTypeId.Int);
            entry.AssertIsInstance("cls.abc", BuiltinTypeId.Int);

            AssertUtil.Contains(string.Join(Environment.NewLine, entry.GetCompletionDocumentation("", "cls")),
                "cls",
                "object"
            );
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void BadMethod() {
            var entry = ProcessText(@"
class cls(object): 
    def f(): 
        'help'
        return 42

abc = cls()
fob = abc.f()
");

            entry.AssertIsInstance("fob", BuiltinTypeId.Int);

            var sig = entry.GetSignatures("cls().f", 1).Single();
            Assert.AreEqual("help", sig.Documentation);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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
                    int i = text.IndexOf("pass");

                    entry.AssertIsInstance("a", i, BuiltinTypeId.Complex);
                    entry.AssertIsInstance("b", i, BuiltinTypeId.Int);
                    entry.AssertIsInstance("c", i, BuiltinTypeId.Str);
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void BadKeywordArguments() {
            var code = @"def f(a, b):
    return a

x = 100
z = f(a=42, x)";

            var entry = ProcessText(code, allowParseErrors: true);
            entry.AssertIsInstance("z", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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
                    int i = text.IndexOf("pass");

                    entry.AssertIsInstance("a", i, BuiltinTypeId.Complex);
                    entry.AssertIsInstance("b", i, BuiltinTypeId.Int);
                    entry.AssertIsInstance("c", i, BuiltinTypeId.Str);
                    entry.AssertIsInstance("d", i, BuiltinTypeId.Tuple);
                    if (testCall.Contains("4L")) {
                        entry.AssertIsInstance("d[0]", i, BuiltinTypeId.Long);
                    }
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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
                    //"f(**{'a': 3j, 'b': 42, 'c': 'abc'})",
                    //"f(**{'c': 'abc', 'b': 42, 'a': 3j})",
                    "f(**{'a': 3j, 'b': 42, 'c': 'abc', 'x': 4L})",  // extra argument
                    //"f(3j, **{'b': 42, 'c': 'abc'})",
                    //"f(3j, 42, **{'c': 'abc'})"
                };

                foreach (var testCall in testCalls) {
                    var text = decl + Environment.NewLine + testCall;
                    Console.WriteLine(testCall);
                    var entry = ProcessText(text);
                    int i = text.IndexOf("pass");

                    entry.AssertIsInstance("a", i, BuiltinTypeId.Complex);
                    entry.AssertIsInstance("b", i, BuiltinTypeId.Int);
                    entry.AssertIsInstance("c", i, BuiltinTypeId.Str);
                    entry.AssertIsInstance("d", i, BuiltinTypeId.Dict);
                    if (testCall.Contains("4L")) {
                        entry.AssertIsInstance("d['x']", i, BuiltinTypeId.Long);
                    }
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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
            var entry = ProcessTextV2(text);

            var fifty = entry.GetNamesNoBuiltins(text.IndexOf("abc.fob"), includeDunder: false);
            AssertUtil.ContainsExactly(fifty, "C", "D", "a", "abc", "self", "x");

            var three = entry.GetNamesNoBuiltins(text.IndexOf("def oar") + 1, includeDunder: false);
            AssertUtil.ContainsExactly(three, "C", "D", "oar");

            entry.AssertHasAttr("abc", text.IndexOf("abc.fob"), "baz", "fob");

            entry.AssertIsInstance("x", text.IndexOf("abc.fob"), BuiltinTypeId.List, BuiltinTypeId.Str, BuiltinTypeId.Tuple);

            AssertUtil.ContainsExactly(
                entry.GetMemberNames("x", text.IndexOf("abc.fob"), GetMemberOptions.IntersectMultipleResults),
                entry.StrMembers.Intersect(entry.ListMembers)
            );
        }

        public static int GetIndex(string text, string substring) {
            return text.IndexOf(substring);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void Builtins() {
            var text = @"
booltypetrue = True
booltypefalse = False
";
            var entry = ProcessTextV2(text);
            entry.AssertIsInstance("booltypetrue", BuiltinTypeId.Bool);
            entry.AssertIsInstance("booltypefalse", BuiltinTypeId.Bool);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void DictionaryFunctionTable() {
            var text = @"
def f(a, b):
    print(a, b)
    
def g(a, b):
    x, y = a, b

x = {'fob': f, 'oar' : g}
x['fob'](42, [])
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("a", text.IndexOf("print"), BuiltinTypeId.Int);
            entry.AssertIsInstance("b", text.IndexOf("print"), BuiltinTypeId.List);
            entry.AssertIsInstance("a", text.IndexOf("x, y"));
            entry.AssertIsInstance("b", text.IndexOf("x, y"));
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void DictionaryAssign() {
            var text = @"
x = {'abc': 42}
y = x['fob']
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("y", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void DictionaryFunctionTableGet2() {
            var text = @"
def f(a, b):
    print(a, b)
    
def g(a, b):
    x, y = a, b

x = {'fob': f, 'oar' : g}
x.get('fob')(42, [])
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("a", text.IndexOf("print"), BuiltinTypeId.Int);
            entry.AssertIsInstance("b", text.IndexOf("print"), BuiltinTypeId.List);
            entry.AssertIsInstance("a", text.IndexOf("x, y"));
            entry.AssertIsInstance("b", text.IndexOf("x, y"));
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void DictionaryFunctionTableGet() {
            var text = @"
def f(a, b):
    print(a, b)
    
def g(a, b):
    x, y = a, b

x = {'fob': f, 'oar' : g}
y = x.get('fob', None)
if y is not None:
    y(42, [])
";
            var entry = ProcessTextV2(text);
            entry.AssertIsInstance("a", text.IndexOf("print"), BuiltinTypeId.Int);
            entry.AssertIsInstance("b", text.IndexOf("print"), BuiltinTypeId.List);
            entry.AssertIsInstance("a", text.IndexOf("x, y"));
            entry.AssertIsInstance("b", text.IndexOf("x, y"));
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void SimpleGlobals() {
            var text = @"
class x(object):
    def abc(self):
        pass
        
a = x()
x.abc()
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetNamesNoBuiltins(includeDunder: false), "a", "x");
            entry.AssertHasAttr("x", "abc");
            entry.AssertHasAttr("x", entry.ObjectMembers);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void FuncCallInIf() {
            var text = @"
def Method(a, b, c):
    print a, b, c
    
if not Method(42, 'abc', []):
    pass
";
            var entry = ProcessTextV2(text);
            entry.AssertIsInstance("a", text.IndexOf("print"), BuiltinTypeId.Int);
            entry.AssertIsInstance("b", text.IndexOf("print"), BuiltinTypeId.Str);
            entry.AssertIsInstance("c", text.IndexOf("print"), BuiltinTypeId.List);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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
            entry.AssertHasAttr("x", text.IndexOf("pass #x"), "x_method");
            entry.AssertIsInstance("y", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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
            entry.AssertIsInstance("xvar", text.IndexOf("x = 42"), BuiltinTypeId.List);
            entry.AssertIsInstance("xvar", text.IndexOf("pass"));
        }


        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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
            var f = entry.GetSignatures("f", 0).Select(sig => {
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
            AssertUtil.ContainsExactly(f, "f(a := (float, int, str), b := (float, int, str), c = 0 := (float, int, str))");
        }

        internal static readonly Regex ValidParameterName = new Regex(@"^(\*|\*\*)?[a-z_][a-z0-9_]*( *=.+)?", RegexOptions.IgnoreCase);
        internal static string GetSafeParameterName(ParameterResult result) {
            var match = ValidParameterName.Match(result.Name);

            return match.Success ? match.Value : result.Name;
        }

        /// <summary>
        /// http://pytools.codeplex.com/workitem/799
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void OverrideCompletions() {
            var text = @"
class oar(list):
    pass
";
            var entry = ProcessTextV2(text);

            var init = entry.GetOverrideable(text.IndexOf("pass")).Single(r => r.Name == "append");
            AssertUtil.AreEqual(init.Parameters.Select(GetSafeParameterName), "self", "value");

            entry = ProcessTextV2(text);

            init = entry.GetOverrideable(text.IndexOf("pass")).Single(r => r.Name == "append");
            AssertUtil.AreEqual(init.Parameters.Select(GetSafeParameterName), "self", "value");

            // Ensure that nested classes are correctly resolved.
            text = @"
class oar(int):
    class fob(dict):
        pass
";
            entry = ProcessTextV2(text);
            var oarItems = entry.GetOverrideable(text.IndexOf("    pass")).Select(x => x.Name).ToSet();
            var fobItems = entry.GetOverrideable(text.IndexOf("pass")).Select(x => x.Name).ToSet();
            AssertUtil.DoesntContain(oarItems, "keys");
            AssertUtil.DoesntContain(oarItems, "items");
            AssertUtil.ContainsAtLeast(oarItems, "bit_length");

            AssertUtil.ContainsAtLeast(fobItems, "keys", "items");
            AssertUtil.DoesntContain(fobItems, "bit_length");
        }

        /// <summary>
        /// https://github.com/Microsoft/PTVS/issues/995
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1)]
        public void DictCtor() {
            var text = @"
d1 = dict({2:3})
x1 = d1[2]

d2 = dict(x = 2)
x2 = d2['x']

d3 = dict(**{2:3})
x3 = d3[2]
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("x1", BuiltinTypeId.Int);
            entry.AssertIsInstance("x2", BuiltinTypeId.Int);
            entry.AssertIsInstance("x3", BuiltinTypeId.Int);
        }

        /// <summary>
        /// https://github.com/Microsoft/PTVS/issues/995
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1)]
        public void SpecializedOverride() {
            var text = @"
class simpledict(dict): pass

class getdict(dict):
    def __getitem__(self, index):
        return 'abc'


d1 = simpledict({2:3})
x1 = d1[2]

d2 = simpledict(x = 2)
x2 = d2['x']

d3 = simpledict(**{2:3})
x3 = d3[2]

d4 = getdict({2:3})
x4 = d4[2]

d5 = simpledict(**{2:'blah'})
x5 = d5[2]
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("x1", BuiltinTypeId.Int);
            entry.AssertIsInstance("x2", BuiltinTypeId.Int);
            entry.AssertIsInstance("x3", BuiltinTypeId.Int);
            entry.AssertIsInstance("x4", BuiltinTypeId.Str);
            entry.AssertIsInstance("x5", BuiltinTypeId.Str);
        }

        /// <summary>
        /// https://github.com/Microsoft/PTVS/issues/995
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1)]
        public void SpecializedOverride2() {
            var text = @"
class setdict(dict):
    def __setitem__(self, index):
        pass

a = setdict()
a[42] = 100
b = a[42]
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("b");
        }

        /// <summary>
        /// We shouldn't use instance members when invoking special methods
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void IterNoInstance() {
            var text = @"
class me(object):
    pass


a = me()
a.__getitem__ = lambda x: 42

for v in a: pass
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("v", text.IndexOf("pass"));

            text = @"
class me(object):
    pass


a = me()
a.__iter__ = lambda: (yield 42)

for v in a: pass
";
            entry = ProcessText(text);
            entry.AssertIsInstance("v", text.IndexOf("pass"));
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void SimpleMethodCall() {
            var text = @"
class x(object):
    def abc(self, fob):
        pass
        
a = x()
a.abc('abc')
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("fob", text.IndexOf("pass"), BuiltinTypeId.Str);
            entry.AssertHasAttr("self", text.IndexOf("pass"), "abc");
            entry.AssertHasAttr("self", text.IndexOf("pass"), entry.ObjectMembers);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void BuiltinRetval() {
            var text = @"
x = [2,3,4]
a = x.index(2)
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("x", BuiltinTypeId.List);
            entry.AssertIsInstance("a", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void BuiltinFuncRetval() {
            var text = @"
x = ord('a')
y = range(5)
";

            var entry = ProcessText(text);
            entry.AssertIsInstance("x", BuiltinTypeId.Int);
            entry.AssertIsInstance("y", BuiltinTypeId.List);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void FunctionMembers() {
            var text = @"
def f(x): pass
f.abc = 32
";
            var entry2 = ProcessTextV2(text);
            entry2.AssertHasAttr("f", "abc");

            text = @"
def f(x): pass

";
            entry2 = ProcessTextV2(text);
            entry2.AssertHasAttr("f", entry2.FunctionMembers);
            entry2.AssertNotHasAttr("f", "x");
            entry2.AssertIsInstance("f.func_name", BuiltinTypeId.Str);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void RangeIteration() {
            var text = @"
for i in range(5):
    pass
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("i", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void BuiltinImport() {
            var text = @"
import sys
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetNamesNoBuiltins(includeDunder: false), "sys");
            entry.AssertHasAttr("sys", "winver");
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void BuiltinImportInFunc() {
            var text = @"
def f():
    import sys
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetNamesNoBuiltins(text.IndexOf("sys"), includeDunder: false), "sys", "f");
            entry.AssertHasAttr("sys", text.IndexOf("sys"), "winver");
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void BuiltinImportInClass() {
            var text = @"
class C:
    import sys
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetNamesNoBuiltins(text.IndexOf("sys"), includeDunder: false), "sys", "C");
            entry.AssertHasAttr("sys", text.IndexOf("sys"), "winver");
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void NoImportClr() {
            var text = @"
x = 'abc'
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("x", BuiltinTypeId.Str);
            entry.AssertHasAttrExact("x", entry.StrMembers);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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
            entry.AssertHasAttrExact("other", text.IndexOf("other.g"), "g", "__doc__", "__class__");
            entry.AssertIsInstance("x", BuiltinTypeId.List, BuiltinTypeId.Str);
            entry.AssertHasAttrExact("x", entry.ListMembers.Union(entry.StrMembers).ToArray());
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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
            entry.AssertIsInstance("x", BuiltinTypeId.List, BuiltinTypeId.Str);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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
            entry.AssertIsInstance("it", BuiltinTypeId.Generator);
            entry.AssertIsInstance("val", "S0");
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void ForwardRefVars() {
            var text = @"
class x(object):
    def __init__(self, val):
        self.abc = [val]
    
x(42)
x('abc')
x([])
";
            var entry = ProcessText(text);
            var values = entry.GetValues("self.abc", text.IndexOf("self.abc"));
            Assert.AreEqual(1, values.Length);
            entry.AssertDescription("self.abc", text.IndexOf("self.abc"), "list");
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void ReturnFunc() {
            var text = @"
def g():
    return []

def f():
    return g
    
x = f()()
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("x", BuiltinTypeId.List);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void ReturnArg() {
            var text = @"
def g(a):
    return a

x = g(1)
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("x", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void ReturnArg2() {
            var text = @"

def f(a):
    def g():
        return a
    return g

x = f(2)()
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("x", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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
            entry.AssertIsInstance("fob", BuiltinTypeId.Int);
            entry.AssertHasAttrExact("fob", entry.IntMembers);
            entry.AssertHasAttrExact("a", "abc", "func", "__doc__", "__class__");
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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
            Assert.Inconclusive("Test not yet implemented");
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void AnnotatedAssign() {
            var text = @"
x : int = 42

class C:
    y : int = 42

    def func(self):
        self.abc : int = 42

a = C()
a.func()
fob1 = a.abc
fob2 = a.y
fob3 = x
";
            var entry = ProcessText(text, PythonLanguageVersion.V36);
            entry.AssertIsInstance("fob1", BuiltinTypeId.Int);
            entry.AssertIsInstance("fob2", BuiltinTypeId.Int);
            entry.AssertIsInstance("fob3", BuiltinTypeId.Int);
            entry.AssertHasAttr("a", "abc", "func", "y", "__doc__", "__class__");

            text = @"
def f(val):
    print(val)

class C:
    def __init__(self, y):
        self.y = y

x:f(42) = 1
x:C(42) = 1
";
            entry = ProcessText(text, PythonLanguageVersion.V36);
            entry.AssertIsInstance("val", text.IndexOf("print"), BuiltinTypeId.Int);
            entry.AssertIsInstance("y", text.IndexOf("self."), BuiltinTypeId.Int);
        }


        [TestMethod, Priority(UnitTestPriority.P1)]
        public void UnfinishedDot() {
            // the partial dot should be ignored and we shouldn't see g as
            // a member of D
            var text = @"
class D(object):
    def func(self):
        self.
        
def g(a, b, c): pass
";
            var entry = ProcessText(text, allowParseErrors: true);
            entry.AssertHasAttr("self", text.IndexOf("self."), "func");
            entry.AssertHasAttr("self", text.IndexOf("self."), entry.ObjectMembers);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void CrossModule() {
            var text1 = @"
import mod2
";
            var text2 = @"
x = 42
";

            PermutedTest("mod", new[] { text1, text2 }, state => {
                AssertUtil.ContainsExactly(
                    state.GetMemberNames(state.Modules["mod1"], "mod2", 0).Where(n => n.Length < 4 || !n.StartsWith("__") || !n.EndsWith("__")),
                    "x"
                );
            });
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void CrossModuleCall() {
            var text1 = @"
import mod2
y = mod2.f('abc')
";
            var text2 = @"
def f(x):
    return x
";

            PermutedTest("mod", new[] { text1, text2 }, state => {
                state.AssertIsInstance(state.Modules["mod2"], "x", text2.IndexOf("return x"), BuiltinTypeId.Str);
                state.AssertIsInstance(state.Modules["mod1"], "y", BuiltinTypeId.Str);
            });
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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

            PermutedTest("mod", new[] { text1, text2 }, state => {
                state.AssertIsInstance(state.Modules["mod2"], "x", text2.IndexOf("= x"), BuiltinTypeId.Str);
                state.AssertIsInstance(state.Modules["mod1"], "y", BuiltinTypeId.Str);
            });
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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

            PermutedTest("mod", new[] { text1, text2 }, state => {
                state.AssertIsInstance(state.Modules["mod2"], "x", text2.IndexOf("= x"), BuiltinTypeId.Str);
                state.AssertIsInstance(state.Modules["mod1"], "y", text1.IndexOf("y ="), BuiltinTypeId.Str);
            });
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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

            PermutedTest("mod", new[] { text1, text2, text3 }, state => {
                state.AssertHasAttr(state.Modules["mod3"], "a", 0, "f", "g");
                state.AssertHasAttr(state.Modules["mod3"], "a", 0, state.ObjectMembers);
            });
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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
            var entry = ProcessText(text, allowParseErrors: true);
            entry.AssertHasAttr("self", text.IndexOf("self."), "f", "g", "h");
            entry.AssertHasAttr("self", text.IndexOf("self."), entry.ObjectMembers);
        }


        [TestMethod, Priority(UnitTestPriority.P1)]
        public void Property() {
            var text = @"
class x(object):
    @property
    def SomeProp(self):
        return 42

a = x().SomeProp
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("a", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void StaticMethod() {
            var text = @"
class x(object):
    @staticmethod
    def StaticMethod(value):
        return value

a = x().StaticMethod(4.0)
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("a", BuiltinTypeId.Float);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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
            entry.AssertIsInstance("a", BuiltinTypeId.Float);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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
            entry.AssertDescription("a", "x");
            entry.AssertDescription("b", "x");
            entry.AssertDescription("cls", text.IndexOf("return"), "x");

            var exprs = new[] { "x.ClassMethod", "x().ClassMethod" };
            foreach (var expr in exprs) {
                // cls is implied, so expect no parameters
                entry.AssertHasParameters(expr);
            }

            text = @"
class x(object):
    @classmethod
    def UncalledClassMethod(cls):
        return cls
";
            entry = ProcessText(text);
            entry.AssertDescription("cls", text.IndexOf("return"), "x");
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
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
            var entry = ProcessTextV2(text);
            AssertUtil.ContainsExactly(entry.GetShortDescriptions("a"), "x", "y");
            AssertUtil.ContainsExactly(entry.GetShortDescriptions("b"), "x", "y");
            var desc = string.Join(Environment.NewLine, entry.GetCompletionDocumentation("", "cls", text.IndexOf("return")));
            AssertUtil.Contains(desc, "x", "y");

            var exprs = new[] { "y.ClassMethod", "y().ClassMethod" };
            foreach (var expr in exprs) {
                // cls is implied, so expect no parameters
                entry.AssertHasParameters(expr);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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
            entry.AssertIsInstance("fob", BuiltinTypeId.Int);
            entry.AssertIsInstance("oar", BuiltinTypeId.Int);
            entry.AssertIsInstance("C.x", BuiltinTypeId.Int);

            entry.AssertIsInstance("ctx", text.IndexOf("return"), BuiltinTypeId.Type);
            entry.AssertIsInstance("inst", text.IndexOf("return 42"), "None", "C");

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
            entry.AssertIsInstance("inst", text.IndexOf("return 42"), "C");
            entry.AssertHasAttr("inst", text.IndexOf("return 42"), "instfunc");
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void AssignSelf() {
            var text = @"
class x(object):
    def __init__(self):
        self.x = 'abc'
    def f(self):
        pass
";
            var entry = ProcessText(text);
            entry.AssertHasAttr("self", text.IndexOf("pass"), "x");
            entry.AssertIsInstance("self.x", text.IndexOf("pass"), BuiltinTypeId.Str);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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
            var entry = ProcessText(text, allowParseErrors: true);
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
            for (var start = sw.ElapsedMilliseconds; start + RUN_TIME > sw.ElapsedMilliseconds;) {
                func();
            }

            var memory1 = GC.GetTotalMemory(true);

            for (var start = sw.ElapsedMilliseconds; start + LEAK_TIME > sw.ElapsedMilliseconds;) {
                func();
            }

            var memory2 = GC.GetTotalMemory(true);

            var delta = memory2 - memory1;
            Trace.TraceInformation("Usage after {0} minute(s): {1}", minutesBeforeMeasure, memory1);
            Trace.TraceInformation("Usage after {0} minute(s): {1}", minutesBeforeAssert, memory2);
            Trace.TraceInformation("Change: {0}", delta);

            Assert.AreEqual((double)memory1, (double)memory2, memory2 * 0.1, string.Format("Memory increased by {0}", delta));
        }

        //[TestMethod, UnitTestPriority(2), Timeout(5 * 60 * 1000)]
        public void MemLeak() {
            var state = CreateAnalyzer();
            var oar = state.AddModule("oar", "");
            var baz = state.AddModule("baz", "");

            AnalyzeLeak(() => {
                state.UpdateModule(oar, @"
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

");

                state.UpdateModule(baz, @"
from oar import C

class D(object):
    def g(self, a):
        pass

a = D()
a.f(C())
z = C().f(42)

min(a, D())
");
            });
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void MoveClass() {
            var fobSrc = "";

            var oarSrc = @"
class C(object):
    pass
";

            var bazSrc = @"
class C(object):
    pass
";

            using (var state = CreateAnalyzer()) {
                var fob = state.AddModule("fob", fobSrc);
                var oar = state.AddModule("oar", oarSrc);
                var baz = state.AddModule("baz", bazSrc);

                state.UpdateModule(fob, "from oar import C");
                state.WaitForAnalysis();

                state.WaitForAnalysis();

                state.AssertDescription(fob, "C", "C");
                state.AssertReferencesInclude(fob, "C", 0,
                    new VariableLocation(1, 17, VariableType.Reference, fob.FilePath),
                    new VariableLocation(2, 1, VariableType.Value, oar.FilePath)
                );

                // delete the class..
                state.UpdateModule(oar, "");
                state.WaitForAnalysis();

                state.AssertIsInstance(fob, "C");

                state.UpdateModule(fob, "from baz import C");
                state.WaitForAnalysis();

                state.AssertDescription(fob, "C", "C");
                state.AssertReferencesInclude(fob, "C", 0,
                    new VariableLocation(1, 17, VariableType.Reference, fob.FilePath),
                    new VariableLocation(2, 1, VariableType.Value, baz.FilePath)
                );
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void Package() {
            var src1 = "";

            var src2 = @"
from fob.y import abc
import fob.y as y
";

            var src3 = @"
abc = 42
";

            using (var state = CreateAnalyzer()) {
                var package = state.AddModule("fob", src1, "fob\\__init__.py");
                var x = state.AddModule("fob.x", src2);
                var y = state.AddModule("fob.y", src3);
                state.WaitForAnalysis();

                state.AssertDescription(x, "y", "Python module fob.y");
                state.AssertIsInstance(x, "abc", BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void PackageRelativeImport() {
            using (var state = CreateAnalyzer()) {
                state.CreateProjectOnDisk = true;

                var package = state.AddModule("fob", "from .y import abc", "fob\\__init__.py");
                var x = state.AddModule("fob.x", "from .y import abc");
                var y = state.AddModule("fob.y", "abc = 42");

                state.WaitForAnalysis();

                state.AssertIsInstance(x, "abc", BuiltinTypeId.Int);
                state.AssertIsInstance(package, "abc", BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void PackageRelativeImportPep328() {
            var imports = new Dictionary<string, string>() {
                { "from .moduleY import spam", "spam"},
                { "from .moduleY import spam as ham", "ham"},
                { "from . import moduleY", "moduleY.spam" },
                { "from ..subpackage1 import moduleY", "moduleY.spam"},
                { "from ..subpackage2.moduleZ import eggs", "eggs"},
                { "from ..moduleA import foo", "foo" },
                { "from ...package import bar", "bar"}
            };

            foreach (var imp in imports) {
                using (var state = CreateAnalyzer()) {
                    state.CreateProjectOnDisk = true;

                    var package = state.AddModule("package", "def bar():\n  pass\n", @"package\__init__.py");
                    var modA = state.AddModule("package.moduleA", "def foo():\n  pass\n", @"package\moduleA.py");

                    var sub1 = state.AddModule("package.subpackage1", string.Empty, @"package\subpackage1\__init__.py");
                    var modX = state.AddModule("package.subpackage1.moduleX", imp.Key, @"package\subpackage1\moduleX.py");
                    var modY = state.AddModule("package.subpackage1.moduleY", "def spam():\n  pass\n", @"package\subpackage1\moduleY.py");

                    var sub2 = state.AddModule("package.subpackage2", string.Empty, @"package\subpackage2\__init__.py");
                    var modZ = state.AddModule("package.subpackage2.moduleZ", "def eggs():\n  pass\n", @"package\subpackage2\moduleZ.py");

                    state.WaitForAnalysis();
                    state.AssertIsInstance(modX, imp.Value, BuiltinTypeId.Function);
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void PackageRelativeImportAliasedMember() {
            // similar to unittest package which has unittest.main which contains a function called "main".
            // Make sure we see the function, not the module.
            using (var state = CreateAnalyzer()) {
                state.CreateProjectOnDisk = true;

                var package = state.AddModule("fob", "from .y import y", "fob\\__init__.py");
                var y = state.AddModule("fob.y", "def y(): pass");

                state.WaitForAnalysis();

                state.AssertIsInstance(package, "y", BuiltinTypeId.Module, BuiltinTypeId.Function);
            }
        }


        [TestMethod, Priority(UnitTestPriority.P0)]
        public void Defaults() {
            var text = @"
def f(x = 42):
    return x
    
a = f()
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("a", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
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

            PermutedTest("mod", new[] { text1, text2 }, state => {
                state.AssertIsInstance(state.Modules["mod1"], "f", BuiltinTypeId.Function);
                state.AssertIsInstance(state.Modules["mod2"], "mod1.f", BuiltinTypeId.Function);
                state.AssertIsInstance(state.Modules["mod2"], "MyClass().mydec(mod1.f)", BuiltinTypeId.Function);
            });
        }


        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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

            PermutedTest("mod", new[] { text1, text2 }, state => {
                // Ensure we ended up with a function
                state.AssertIsInstance(state.Modules["mod1"], "f", 0, BuiltinTypeId.Function);

                // Ensure we passed a function in to the decorator (def dec(func))
                //state.AssertIsInstance(state.Modules["mod2"], "func", text2.IndexOf("return self.filter_function("), BuiltinTypeId.Function);

                // Ensure we saw the function passed *through* the decorator
                state.AssertIsInstance(state.Modules["mod2"], "func", text2.IndexOf("return self.filter("), BuiltinTypeId.Function);

                // Ensure we saw the function passed *back into* the original decorator constructor
                state.AssertIsInstance(
                    state.Modules["mod2"], "filter_func", text2.IndexOf("# @register.filter()"),
                    BuiltinTypeId.Function,
                    BuiltinTypeId.NoneType
                );
            });
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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
            var entry = ProcessTextV2(text);
            entry.AssertIsInstance("x", BuiltinTypeId.Tuple);
            entry.AssertIsInstance("y", BuiltinTypeId.List);
            entry.AssertIsInstance("z", BuiltinTypeId.Float);
            entry.AssertIsInstance("w", BuiltinTypeId.Str);

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
            entry = ProcessTextV2(text);
            entry.AssertIsInstance("items", entry.GetTypeIds("items2").ToArray());
            entry.AssertIsInstance("x", BuiltinTypeId.List, BuiltinTypeId.Set, BuiltinTypeId.Str);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void DecoratorReturnTypes_NoDecorator() {
            // https://pytools.codeplex.com/workitem/1694
            var text = @"# without decorator
def returnsGiven(parm):
    return parm

retGivenInt = returnsGiven(1)
retGivenString = returnsGiven('str')
retGivenBool = returnsGiven(True)
";

            var entry = ProcessText(text);

            entry.AssertIsInstance("retGivenInt", BuiltinTypeId.Int);
            entry.AssertIsInstance("retGivenString", BuiltinTypeId.Str);
            entry.AssertIsInstance("retGivenBool", BuiltinTypeId.Bool);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void DecoratorReturnTypes_DecoratorNoParams() {
            // https://pytools.codeplex.com/workitem/1694
            var text = @"# with decorator without wrap
def decoratorFunctionTakesArg1(f):
    def wrapped_f(arg):
        return f(arg)
    return wrapped_f

@decoratorFunctionTakesArg1
def returnsGivenWithDecorator1(parm):
    return parm

retGivenInt = returnsGivenWithDecorator1(1)
retGivenString = returnsGivenWithDecorator1('str')
retGivenBool = returnsGivenWithDecorator1(True)
";

            var entry = ProcessText(text);

            entry.AssertIsInstance("retGivenInt", BuiltinTypeId.Int);
            entry.AssertIsInstance("retGivenString", BuiltinTypeId.Str);
            entry.AssertIsInstance("retGivenBool", BuiltinTypeId.Bool);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void DecoratorReturnTypes_DecoratorWithParams() {
            // https://pytools.codeplex.com/workitem/1694
            var text = @"
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

retGivenInt = returnsGivenWithDecorator2(1)
retGivenString = returnsGivenWithDecorator2('str')
retGivenBool = returnsGivenWithDecorator2(True)";

            var entry = ProcessText(text);

            entry.AssertIsInstance("retGivenInt", BuiltinTypeId.Int);
            entry.AssertIsInstance("retGivenString", BuiltinTypeId.Str);
            entry.AssertIsInstance("retGivenBool", BuiltinTypeId.Bool);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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

            PermutedTest("mod", new[] { text1, text2 }, state => {
                // Neither decorator is callable, but at least analysis completed
                state.AssertIsInstance(state.Modules["mod1"], "decorator_a", BuiltinTypeId.Function);
                state.AssertIsInstance(state.Modules["mod2"], "decorator_b", BuiltinTypeId.Function);
            });
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void ProcessDecorators() {
            var text = @"
def d(fn):
    return []

@d
def my_fn():
    return None
";

            var entry = CreateAnalyzer();
            entry.Analyzer.Limits.ProcessCustomDecorators = true;
            entry.AddModule("fob", text);
            entry.WaitForAnalysis();

            entry.AssertIsInstance("my_fn", BuiltinTypeId.List);
            entry.AssertIsInstance("fn", text.IndexOf("return"), BuiltinTypeId.Function);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void NoProcessDecorators() {
            var text = @"
def d(fn):
    return []

@d
def my_fn():
    return None
";

            var entry = CreateAnalyzer();
            entry.Analyzer.Limits.ProcessCustomDecorators = false;
            entry.AddModule("fob", text);
            entry.WaitForAnalysis();

            entry.AssertIsInstance("my_fn", BuiltinTypeId.Function);
            entry.AssertIsInstance("fn", text.IndexOf("return"), BuiltinTypeId.Function);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void DecoratorReferences() {
            var text = @"
def d1(f):
    return f
class d2:
    def __call__(self, f): return f

@d1
def func_d1(): pass
@d2()
def func_d2(): pass

@d1
class cls_d1(object): pass
@d2()
class cls_d2(object): pass
";
            var entry = ProcessText(text);
            entry.AssertReferences("d1",
                new VariableLocation(2, 1, VariableType.Value),
                new VariableLocation(2, 5, VariableType.Definition),
                new VariableLocation(7, 2, VariableType.Reference),
                new VariableLocation(12, 2, VariableType.Reference)
            );
            entry.AssertReferences("d2",
                new VariableLocation(4, 1, VariableType.Value),
                new VariableLocation(4, 7, VariableType.Definition),
                new VariableLocation(9, 2, VariableType.Reference),
                new VariableLocation(14, 2, VariableType.Reference)
            );
            AssertUtil.ContainsExactly(entry.GetValues("f", 18).Select(v => v.MemberType), PythonMemberType.Function, PythonMemberType.Class);
            AssertUtil.ContainsExactly(entry.GetValues("f", 66).Select(v => v.MemberType), PythonMemberType.Function, PythonMemberType.Class);
            AssertUtil.ContainsExactly(entry.GetValues("func_d1").Select(v => v.MemberType), PythonMemberType.Function);
            AssertUtil.ContainsExactly(entry.GetValues("func_d2").Select(v => v.MemberType), PythonMemberType.Function);
            AssertUtil.ContainsExactly(entry.GetValues("cls_d1").Select(v => v.MemberType), PythonMemberType.Class);
            AssertUtil.ContainsExactly(entry.GetValues("cls_d2").Select(v => v.MemberType), PythonMemberType.Class);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void DecoratorClass() {
            var text = @"
def dec1(C):
    def sub_method(self): pass
    C.sub_method = sub_method
    return C

@dec1
class MyBaseClass1(object):
    def base_method(self): pass

def dec2(C):
    class MySubClass(C):
        def sub_method(self): pass
    return MySubClass

@dec2
class MyBaseClass2(object):
    def base_method(self): pass

mc1 = MyBaseClass1()
mc2 = MyBaseClass2()
";
            var entry = ProcessText(text);
            AssertUtil.ContainsAtLeast(entry.GetMemberNames("mc1", 0, GetMemberOptions.None), "base_method", "sub_method");
            entry.AssertIsInstance("mc2", "MySubClass");
            AssertUtil.ContainsAtLeast(entry.GetMemberNames("mc2", 0, GetMemberOptions.None), /*"base_method",*/ "sub_method");
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void ClassInit() {
            var text = @"
class X:
    def __init__(self, value):
        self.value = value

a = X(2)
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("a.value", 0, BuiltinTypeId.Int);
            entry.AssertIsInstance("value", text.IndexOf("self."), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void InstanceCall() {
            var text = @"
class X:
    def __call__(self, value):
        return value

x = X()

a = x(2)
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("a", BuiltinTypeId.Int);
        }

        /// <summary>
        /// Verifies that regardless of how we get to imports/function return values that
        /// we properly understand the imported value.
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1)]
        public void ImportScopesOrder() {
            var text1 = @"
import _io
import mod2
import mmap as mm

import sys
def f():
    return sys

def g():
    return _io

def h():
    return mod2.sys

def i():
    import zlib
    return zlib

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
            PermutedTest("mod", new[] { text1, text2 }, state => {
                state.DefaultModule = "mod1";
                state.AssertDescription("g", "mod1.g() -> _io");
                state.AssertDescription("f", "mod1.f() -> sys");
                state.AssertDescription("h", "mod1.h() -> sys");
                state.AssertDescription("i", "mod1.i() -> zlib");
                state.AssertDescription("j", "mod1.j() -> mmap");
                state.AssertDescription("k", "mod1.k() -> imp");
            });
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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
            entry.AssertDescription("cls", text.IndexOf("= value"), "X");
            entry.AssertIsInstance("value", text.IndexOf("res.value = "), BuiltinTypeId.Int);
            entry.AssertIsInstance("res", text.IndexOf("res.value = "), "X");
            entry.AssertIsInstance("a", text.IndexOf("a = "), "X");
            entry.AssertIsInstance("a.value", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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
            entry.AssertIsInstance("a", BuiltinTypeId.NoneType, BuiltinTypeId.Int);
            entry.AssertIsInstance("b", BuiltinTypeId.NoneType, BuiltinTypeId.Int);
            entry.AssertIsInstance("x", BuiltinTypeId.NoneType, BuiltinTypeId.Int);
            entry.AssertIsInstance("y", BuiltinTypeId.NoneType, BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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

            var entry = ProcessTextV2(text);
            entry.AssertIsInstance("x", text.IndexOf("nonlocal"), BuiltinTypeId.NoneType, BuiltinTypeId.Int);
            entry.AssertIsInstance("y", text.IndexOf("nonlocal"), BuiltinTypeId.NoneType, BuiltinTypeId.Int);
            entry.AssertIsInstance("x", text.IndexOf("return"), BuiltinTypeId.NoneType, BuiltinTypeId.Int);
            entry.AssertIsInstance("y", text.IndexOf("return"), BuiltinTypeId.NoneType, BuiltinTypeId.Int);
            entry.AssertIsInstance("a", BuiltinTypeId.NoneType, BuiltinTypeId.Int);
            entry.AssertIsInstance("b", BuiltinTypeId.NoneType, BuiltinTypeId.Int);

            entry.AssertReferences("x", text.IndexOf("x ="),
                new VariableLocation(3, 5, VariableType.Definition),
                new VariableLocation(6, 18, VariableType.Reference),
                new VariableLocation(7, 9, VariableType.Definition),
                new VariableLocation(9, 12, VariableType.Reference)
            );

            entry.AssertReferences("y", text.IndexOf("x ="),
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

            entry = ProcessTextV2(text);
            entry.AssertIsInstance("a", BuiltinTypeId.NoneType, BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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
            entry.AssertIsInstance("x", text.IndexOf("z ="), BuiltinTypeId.Int);
            entry.AssertIsInstance("x", text.IndexOf("z =") + 1, BuiltinTypeId.Int);
            entry.AssertIsInstance("x", text.IndexOf("pass"), BuiltinTypeId.NoneType, BuiltinTypeId.Int, BuiltinTypeId.Str, BuiltinTypeId.Tuple);
            entry.AssertIsInstance("x", text.IndexOf("y ="), BuiltinTypeId.Str);
            entry.AssertIsInstance("x", text.IndexOf("y =") + 1, BuiltinTypeId.Str);
            entry.AssertIsInstance("x", text.IndexOf("else:") + 7, BuiltinTypeId.NoneType, BuiltinTypeId.Int, BuiltinTypeId.Str, BuiltinTypeId.Tuple);
            entry.AssertIsInstance("x", text.IndexOf("fob ="), BuiltinTypeId.Tuple);
            entry.AssertIsInstance("x", text.LastIndexOf("pass"), BuiltinTypeId.Tuple);

            entry.AssertReferences("x",
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(7, 23, VariableType.Reference),
                new VariableLocation(12, 23, VariableType.Reference),
                new VariableLocation(20, 15, VariableType.Reference)
            );

            entry.AssertReferences("x", text.IndexOf("z ="),
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(7, 23, VariableType.Reference),
                new VariableLocation(12, 23, VariableType.Reference),
                new VariableLocation(20, 15, VariableType.Reference)
            );

            entry.AssertReferences("x", text.IndexOf("z =") + 1,
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(7, 23, VariableType.Reference),
                new VariableLocation(12, 23, VariableType.Reference),
                new VariableLocation(20, 15, VariableType.Reference)
            );

            entry.AssertReferences("x", text.IndexOf("z =") - 2,
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(7, 23, VariableType.Reference),
                new VariableLocation(12, 23, VariableType.Reference),
                new VariableLocation(20, 15, VariableType.Reference)
            );

            entry.AssertReferences("x", text.IndexOf("y ="),
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(7, 23, VariableType.Reference),
                new VariableLocation(12, 23, VariableType.Reference),
                new VariableLocation(20, 15, VariableType.Reference)
            );

            entry.AssertReferences("x", text.IndexOf("y =") + 1,
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(7, 23, VariableType.Reference),
                new VariableLocation(12, 23, VariableType.Reference),
                new VariableLocation(20, 15, VariableType.Reference)
            );

            entry.AssertReferences("x", text.IndexOf("y =") - 2,
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

            entry = ProcessTextV2(text);
            entry.AssertIsInstance("a");
            entry.AssertIsInstance("a", text.IndexOf("def g()"), BuiltinTypeId.Int, BuiltinTypeId.Unicode);
            entry.AssertIsInstance("a", text.IndexOf("pass"), BuiltinTypeId.Int);
            entry.AssertIsInstance("a", text.IndexOf("print(a)"), BuiltinTypeId.Int, BuiltinTypeId.Unicode);

            text = @"x = None


if True:
    pass
    assert isinstance(x, int)
    z = 100
    
    pass

print(z)";

            entry = ProcessText(text);
            entry.AssertIsInstance("z", BuiltinTypeId.Int);
            entry.AssertIsInstance("z", text.IndexOf("z ="), BuiltinTypeId.Int);
            entry.AssertIsInstance("z", text.Length - 1, BuiltinTypeId.Int);

            entry.AssertReferences("z",
                new VariableLocation(7, 5, VariableType.Definition),
                new VariableLocation(11, 7, VariableType.Reference)
            );

            entry.AssertReferences("z", text.IndexOf("z ="),
                new VariableLocation(7, 5, VariableType.Definition),
                new VariableLocation(11, 7, VariableType.Reference)
            );

            // http://pytools.codeplex.com/workitem/636

            // this just shouldn't crash, we should handle the malformed code, not much to inspect afterwards...

            entry = ProcessText("if isinstance(x, list):\r\n", allowParseErrors: true);
            entry = ProcessText("if isinstance(x, list):", allowParseErrors: true);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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
            entry.AssertIsInstance("z", code.IndexOf("pass"), BuiltinTypeId.Int);
            entry.AssertIsInstance("w", code.IndexOf("pass"), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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
            entry.AssertIsInstance("y", code.IndexOf("pass"), BuiltinTypeId.Object, BuiltinTypeId.Float);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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
            entry.AssertIsInstance("a", text.IndexOf("print(a)"), "C");
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void IsInstanceNested() {
            var text = @"
class R: pass

def fn(a, b, c):
    result = R()
    assert isinstance(a, str)
    result.a = a

    assert isinstance(b, type)
    if isinstance(b, tuple):
        pass
    result.b = b

    assert isinstance(c, str)
    result.c = c
    return result

r1 = fn('fob', (int, str), 'oar')
r2 = fn(123, None, 4.5)
";

            var entry = ProcessText(text);
            entry.AssertIsInstance("r1.a", BuiltinTypeId.Str);
            entry.AssertIsInstance("r1.b", BuiltinTypeId.Type, BuiltinTypeId.Tuple);
            entry.AssertIsInstance("r1.c", BuiltinTypeId.Str);

            entry.AssertIsInstance("r2.a", BuiltinTypeId.Str);
            entry.AssertIsInstance("r2.b", BuiltinTypeId.Type, BuiltinTypeId.Tuple);
            entry.AssertIsInstance("r2.c", BuiltinTypeId.Str);
        }

        private static IEnumerable<string> DumpScopesToStrings(IScope scope) {
            yield return scope.Name;
            foreach (var child in scope.Children) {
                foreach (var s in DumpScopesToStrings(child)) {
                    yield return "  " + s;
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void IsInstanceAndLambdaScopes() {
            // https://github.com/Microsoft/PTVS/issues/2801
            var text = @"if isinstance(p, dict):
    v = [i for i in (lambda x: x)()]";

            var entry = ProcessTextV2(text);
            var scope = entry.Modules[entry.DefaultModule].Analysis.Scope;
            var dump = string.Join(Environment.NewLine, DumpScopesToStrings(scope));

            Console.WriteLine($"Actual:{Environment.NewLine}{dump}");

            Assert.AreEqual(entry.DefaultModule + @"
  <statements>
  <isinstance scope>
    <comprehension scope>
      <lambda>
        <statements>
  <statements>", dump);
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void IsInstanceReferences() {
            var text = @"def fob():
    oar = get_b()
    assert isinstance(oar, float)

    if oar.complex:
        raise IndexError

    return oar";

            var entry = ProcessText(text);

            for (int i = text.IndexOf("oar", 0); i >= 0; i = text.IndexOf("oar", i + 1)) {
                entry.AssertReferences("oar", i,
                    new VariableLocation(2, 5, VariableType.Definition),
                    new VariableLocation(3, 23, VariableType.Reference),
                    new VariableLocation(5, 8, VariableType.Reference),
                    new VariableLocation(8, 12, VariableType.Reference)
                );
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void FunctoolsDecoratorReferences() {
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

            entry.AssertReferences("d",
                new VariableLocation(3, 1, VariableType.Value),
                new VariableLocation(3, 5, VariableType.Definition),
                new VariableLocation(9, 2, VariableType.Reference)
            );

            entry.AssertReferences("g",
                new VariableLocation(4, 5, VariableType.Value),
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

            entry.AssertReferences("d",
                new VariableLocation(1, 1, VariableType.Value),
                new VariableLocation(1, 5, VariableType.Definition),
                new VariableLocation(6, 2, VariableType.Reference)
            );

            entry.AssertReferences("g",
                new VariableLocation(2, 5, VariableType.Value),
                new VariableLocation(7, 5, VariableType.Definition),
                new VariableLocation(10, 6, VariableType.Reference)
            );
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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
            var entry = ProcessTextV2(text);

            entry.AssertIsInstance("fob()", "fob");
            entry.AssertDescription("int()", "int");
            entry.AssertDescription("a", "float");
            entry.AssertDescription("a", "float");
            entry.AssertDescription("b", "long");
            entry.AssertDescription("c", "str");
            entry.AssertIsInstance("x", BuiltinTypeId.Tuple);
            entry.AssertIsInstance("y", BuiltinTypeId.List);
            entry.AssertDescription("z", "int");
            entry.AssertDescriptionContains("min", "min(");
            entry.AssertDescriptionContains("list.append", "list.append(");
            entry.AssertIsInstance("\"abc\".Length");
            entry.AssertIsInstance("c.Length");
            entry.AssertIsInstance("d", "fob");
            entry.AssertDescription("sys", "sys");
            entry.AssertDescription("f", "test-module.f() -> str");
            entry.AssertDescription("fob.f", "test-module.fob.f(self: fob)\r\ndeclared in fob");
            entry.AssertDescription("fob().g", "method g of test-module.fob objects");
            entry.AssertDescription("fob", "class test-module.fob(object)");
            //AssertUtil.ContainsExactly(entry.GetVariableDescriptionsByIndex("System.StringSplitOptions.RemoveEmptyEntries", 1), "field of type StringSplitOptions");
            entry.AssertDescription("g", "test-module.g()");    // return info could be better
            //AssertUtil.ContainsExactly(entry.GetVariableDescriptionsByIndex("System.AppDomain.DomainUnload", 1), "event of type System.EventHandler");
            entry.AssertDescription("None", "None");
            entry.AssertDescription("f.func_name", "property of type str");
            entry.AssertDescription("h", "test-module.h() -> test-module.f() -> str, test-module.g()");
            entry.AssertDescription("docstr_func", "test-module.docstr_func() -> int");
            entry.AssertDocumentation("docstr_func", "useful documentation");

            entry.AssertDescription("with_params", "test-module.with_params(a, b, c)");
            entry.AssertDescription("with_params_default", "test-module.with_params_default(a, b, c: int=100)");
            entry.AssertDescription("with_params_default_2", "test-module.with_params_default_2(a, b, c: list=[])");
            entry.AssertDescription("with_params_default_3", "test-module.with_params_default_3(a, b, c: tuple=())");
            entry.AssertDescription("with_params_default_4", "test-module.with_params_default_4(a, b, c: dict={})");
            entry.AssertDescription("with_params_default_2a", "test-module.with_params_default_2a(a, b, c: list=[...])");
            entry.AssertDescription("with_params_default_3a", "test-module.with_params_default_3a(a, b, c: tuple=(...))");
            entry.AssertDescription("with_params_default_4a", "test-module.with_params_default_4a(a, b, c: dict={...})");
            entry.AssertDescription("with_params_default_starargs", "test-module.with_params_default_starargs(*args, **kwargs)");

            // method which returns itself, we shouldn't stack overflow producing the help...
            entry.AssertDescription("return_func_class().return_func", "method return_func of test-module.return_func_class objects...");
            entry.AssertDocumentation("return_func_class().return_func", "some help");
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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

            AssertUtil.Contains(entry.GetCompletionDocumentation("", "d", 1).First(), "fob");
            AssertUtil.Contains(entry.GetCompletionDocumentation("", "int", 1).First(), "integer");
            AssertUtil.Contains(entry.GetCompletionDocumentation("", "min", 1).First(), "min(");
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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


            entry.AssertAttrIsType("f", "func_name", PythonMemberType.Property);
            entry.AssertAttrIsType("f", "func_name", PythonMemberType.Property);
            entry.AssertAttrIsType("list", "append", PythonMemberType.Method);
            entry.AssertAttrIsType("y", "append", PythonMemberType.Method);
            entry.AssertAttrIsType("", "int", PythonMemberType.Class);
            entry.AssertAttrIsType("", "min", PythonMemberType.Function);
            entry.AssertAttrIsType("", "sys", PythonMemberType.Module);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void RecurisveDataStructures() {
            var text = @"
d = {}
d[0] = d
";
            var entry = ProcessTextV2(text);

            entry.AssertDescription("d", "dict({int : dict})");
        }

        /// <summary>
        /// Variable is refered to in the base class, defined in the derived class, we should know the type information.
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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
            entry.AssertAttrIsType("Derived()", "map", PythonMemberType.Field);
        }


        /// <summary>
        /// Test case where we have a member but we don't have any type information for the member.  It should
        /// still show up as a member.
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1)]
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
            entry.AssertHasAttr("C()", "x", "y");
        }

        /// <summary>
        /// Test case where we have a member but we don't have any type information for the member.  It should
        /// still show up as a member.
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1)]
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
";

            var entry = ProcessText(text);
            entry.AssertIsInstance("x[0]", BuiltinTypeId.Int);

            foreach (string value in new[] { "ly", "lz", "ty", "tz", "lyt", "tyt" }) {
                entry.AssertIsInstance(value + "[0]", BuiltinTypeId.Int);
            }
        }

#if FALSE
        [TestMethod, Priority(UnitTestPriority.P1)]
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


        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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

            var refs = new[] {
                new VariableLocation(4, 14, VariableType.Reference),
                new VariableLocation(6, 5, VariableType.Value),
                new VariableLocation(6, 9, VariableType.Definition),
                new VariableLocation(11, 5, VariableType.Value),
                new VariableLocation(11, 9, VariableType.Definition),
            };

            entry.AssertReferences("self.fob", text.IndexOf("'x'"), refs);
            entry.AssertReferences("self.fob", text.IndexOf("pass"), refs);
            entry.AssertReferences("self.fob", text.IndexOf("self.fob"), refs);
        }

        /// <summary>
        /// Verifies that constructing lists / tuples from more lists/tuples doesn't cause an infinite analysis as we keep creating more lists/tuples.
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1)]
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

        [TestMethod, Priority(UnitTestPriority.P1)]
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
            AssertUtil.DoesntContain(entry.GetMemberNames("c", 0, GetMemberOptions.IntersectMultipleResults), new[] { "fob", "oar" });
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void UpdateMethodMultiFiles() {
            string text1 = @"
def f(abc):
    pass
";

            string text2 = @"
import mod1
mod1.f(42)
";

            var state = CreateAnalyzer();

            // add both files to the project
            var entry1 = state.AddModule("mod1", text1);
            var entry2 = state.AddModule("mod2", text2);

            state.WaitForAnalysis();
            state.AssertIsInstance(entry1, "abc", text1.IndexOf("pass"), BuiltinTypeId.Int);

            // re-analyze project1, we should still know about the type info provided by module2
            state.UpdateModule(entry1, null);
            state.WaitForAnalysis();

            state.AssertIsInstance(entry1, "abc", text1.IndexOf("pass"), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void MetaClassesV2() {

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

            var entry = ProcessTextV2(text);
            int i = text.IndexOf("print cls.g");
            entry.AssertHasParameters("cls.f", i);
            entry.AssertHasParameters("cls.g", i);
            entry.AssertHasParameters("cls.x", i, "var");
            entry.AssertHasParameters("cls.inst_method", i, "self");
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void MetaClassesV3() {
            var text = @"class C(type):
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

            var entry = ProcessTextV2(text);
            int i = text.IndexOf("print(cls.g)");
            entry.AssertHasParameters("cls.f", i);
            entry.AssertHasParameters("cls.g", i);
            entry.AssertHasParameters("cls.x", i, "var");
            entry.AssertHasParameters("cls.inst_method", i, "self");
        }

        /// <summary>
        /// Tests assigning odd things to the metaclass variable.
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1)]
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

                ProcessTextV2(text);
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

                ProcessTextV2(text, allowParseErrors: true);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void FromImport() {
            ProcessText("from #   blah", allowParseErrors: true);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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

            entry.AssertIsInstance("x", BuiltinTypeId.Str);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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
            var entry = ProcessText(code);

            // super(Derived1)
            {
                // Member from derived class should not be present
                entry.AssertNotHasAttr("super(Derived1)", code.IndexOf("print('derived1_func')"), "derived1_func");

                // Members from both base classes with distinct names should be present, and should have all parameters including self
                entry.AssertHasParameters("super(Derived1).base1_func", code.IndexOf("print('derived1_func')"), "self");
                entry.AssertHasParameters("super(Derived1).base2_func", code.IndexOf("print('derived1_func')"), "self");

                // Only one member with clashing names should be present, and it should be from Base1
                entry.AssertHasParameters("super(Derived1).base_func", code.IndexOf("print('derived1_func')"), "self", "x");
            }

            // super(Derived2)
            {
                // Only one member with clashing names should be present, and it should be from Base2
                entry.AssertHasParameters("super(Derived2).base_func", code.IndexOf("print('derived2_func')"), "self", "x", "y", "z");
            }

            // super(Derived1, self), or Py3k magic super() to the same effect
            int i = code.IndexOf("print('derived1_func')");
            entry.AssertNotHasAttr("super(Derived1, self)", i, "derived1_func");
            entry.AssertHasParameters("super(Derived1, self).base1_func", i);
            entry.AssertHasParameters("super(Derived1, self).base2_func", i);
            entry.AssertHasParameters("super(Derived1, self).base_func", i, "x");

            if (entry.Analyzer.LanguageVersion.Is3x()) {
                entry.AssertNotHasAttr("super()", i, "derived1_func");
                entry.AssertHasParameters("super().base1_func", i);
                entry.AssertHasParameters("super().base2_func", i);
                entry.AssertHasParameters("super().base_func", i, "x");
            }

            // super(Derived2, self), or Py3k magic super() to the same effect
            i = code.IndexOf("print('derived2_func')");
            entry.AssertHasParameters("super(Derived2, self).base_func", i, "x", "y", "z");
            if (entry.Analyzer.LanguageVersion.Is3x()) {
                entry.AssertHasParameters("super().base_func", i, "x", "y", "z");
            }

            // super(Derived1 union Derived1)
            {
                // Members with clashing names from both potential bases should be unioned
                var sigs = entry.GetSignatures("super(cls).base_func", code.IndexOf("print('derived3_func')"));
                Assert.AreEqual(2, sigs.Length);
                Assert.IsTrue(sigs.Any(overload => overload.Parameters.Length == 2)); // (self, x)
                Assert.IsTrue(sigs.Any(overload => overload.Parameters.Length == 4)); // (self, x, y, z)
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void ParameterAnnotation() {
            var text = @"
s = None
def f(s: s = 123):
    return s
";
            var entry = ProcessTextV2(text);

            entry.AssertIsInstance("s", text.IndexOf("s:"), BuiltinTypeId.Int, BuiltinTypeId.NoneType);
            entry.AssertIsInstance("s", text.IndexOf("s ="), BuiltinTypeId.NoneType);
            entry.AssertIsInstance("s", text.IndexOf("return"), BuiltinTypeId.Int, BuiltinTypeId.NoneType);
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void ParameterAnnotationLambda() {
            var text = @"
s = None
def f(s: lambda s: s > 0 = 123):
    return s
";
            var entry = ProcessTextV2(text);

            entry.AssertIsInstance("s", text.IndexOf("s:"), BuiltinTypeId.Int);
            entry.AssertIsInstance("s", text.IndexOf("s >"), BuiltinTypeId.NoneType);
            entry.AssertIsInstance("s", text.IndexOf("return"), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void ReturnAnnotation() {
            var text = @"
s = None
def f(s = 123) -> s:
    return s
";
            var entry = ProcessTextV2(text);

            entry.AssertIsInstance("s", text.IndexOf("(s =") + 1, BuiltinTypeId.Int);
            entry.AssertIsInstance("s", text.IndexOf("s:"), BuiltinTypeId.NoneType);
            entry.AssertIsInstance("s", text.IndexOf("return"), BuiltinTypeId.Int);
        }
        
        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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
    '''doc'''
    return 'undecorated'

def test2():
    pass

def test2a():
    pass

test2.test_attr = 123
update_wrapper(test2a, test2, ('test_attr',))

test1_result = test1()
";

            var state = CreateAnalyzer();
            var textEntry = state.AddModule("fob", text);
            state.WaitForAnalysis();

            state.AssertConstantEquals("test1.__name__", "test1");
            state.AssertConstantEquals("test1.__doc__", "doc");
            var fi = state.GetValue<IFunctionInfo>("test1");
            Assert.AreEqual("doc", fi.Documentation);
            state.GetValue<IFunctionInfo>("test1.__wrapped__");
            Assert.AreEqual(2, state.GetValue<IFunctionInfo>("test1").Overloads.Count());
            state.AssertConstantEquals("test1_result", "decorated");

            // __name__ should not have been changed by update_wrapper
            state.AssertConstantEquals("test2.__name__", "test2");
            state.AssertConstantEquals("test2a.__name__", "test2a");

            // test_attr should have been copied by update_wrapper
            state.AssertIsInstance("test2.test_attr", BuiltinTypeId.Int);
            state.AssertIsInstance("test2a.test_attr", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void MultilineFunctionDescription() {
            var code = @"class A:
    def fn(self):
        return lambda: 123
";
            var entry = ProcessText(code);

            Assert.AreEqual(
                "test-module.A.fn(self: A) -> lambda: 123 -> int\ndeclared in A",
                entry.GetDescriptions("A.fn", 0).Single().Replace("\r\n", "\n")
            );
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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

            var entry = ProcessTextV2(code);

            entry.AssertIsInstance("value_in_modules", BuiltinTypeId.Int);

            Assert.AreEqual("__builtin__", entry.GetValue<AnalysisValue>("builtins").Name);
            Assert.AreEqual("__builtin__", entry.GetValue<AnalysisValue>("builtins2").Name);
            Assert.AreEqual("__builtin__", entry.GetValue<AnalysisValue>("builtins3").Name);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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

            entry.AssertIsInstance("p1", BuiltinTypeId.Int);
            entry.AssertIsInstance("p3", BuiltinTypeId.Int, BuiltinTypeId.Float);
            entry.AssertIsInstance("p4", BuiltinTypeId.Int, BuiltinTypeId.Float);
            entry.AssertIsInstance("p2", BuiltinTypeId.Int, BuiltinTypeId.Float);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
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

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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

            entry.AssertIsInstance("x", code.IndexOf("x ="), BuiltinTypeId.Int);
            entry.AssertIsInstance("y", code.IndexOf("x ="), BuiltinTypeId.Int);
            entry.AssertIsInstance("g2", code.IndexOf("x ="), BuiltinTypeId.Generator);
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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
            entry.AssertHasAttr("x", text.IndexOf("pass #x"), "x_method");
            entry.AssertIsInstance("y", text.IndexOf("pass #y"), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
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

            entry.AssertIsInstance("i", code.IndexOf("pass"), BuiltinTypeId.Int);
        }


        [TestMethod, Priority(UnitTestPriority.P1)]
        public void RecursiveDecorators() {
            // See https://github.com/Microsoft/PTVS/issues/542
            // Should not crash/OOM
            var code = @"
def f():
    def d(fn):
        @f()
        def g(): pass

    return d
";

            ProcessText(code);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void NullNamedArgument() {
            CallDelegate callable = (node, unit, args, keywordArgNames) => {
                bool anyNull = false;
                Console.WriteLine("fn({0})", string.Join(", ", keywordArgNames.Select(n => {
                    if (n == null) {
                        anyNull = true;
                        return "(null)";
                    } else {
                        return n.Name + "=(value)";
                    }
                })));
                Assert.IsFalse(anyNull, "Some arguments were null");
                return AnalysisSet.Empty;
            };

            using (var state = CreateAnalyzer(allowParseErrors: true)) {
                state.Analyzer.SpecializeFunction("NullNamedArgument", "fn", callable);

                var entry1 = state.AddModule("NullNamedArgument", "def fn(**kwargs): pass");
                var entry2 = state.AddModule("test", "import NullNamedArgument; NullNamedArgument.fn(a=0, ]]])");
                state.WaitForAnalysis();
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void DefaultModuleAttributes() {
            var entry3 = ProcessTextV2("x = 1");
            AssertUtil.ContainsExactly(entry3.GetNamesNoBuiltins(), "__builtins__", "__file__", "__name__", "__package__", "__cached__", "__spec__", "x");
            var package = entry3.AddModule("package", "", Path.Combine(TestData.GetTempPath("package"), "__init__.py"));
            AssertUtil.ContainsExactly(entry3.GetNamesNoBuiltins(package), "__path__", "__builtins__", "__file__", "__name__", "__package__", "__cached__", "__spec__");

            entry3.AssertIsInstance("__file__", BuiltinTypeId.Unicode);
            entry3.AssertIsInstance("__name__", BuiltinTypeId.Unicode);
            entry3.AssertIsInstance("__package__", BuiltinTypeId.Unicode);
            entry3.AssertIsInstance(package, "__path__", BuiltinTypeId.List);

            var entry2 = ProcessTextV2("x = 1");
            AssertUtil.ContainsExactly(entry2.GetNamesNoBuiltins(), "__builtins__", "__file__", "__name__", "__package__", "x");

            entry2.AssertIsInstance("__file__", BuiltinTypeId.Bytes);
            entry2.AssertIsInstance("__name__", BuiltinTypeId.Bytes);
            entry2.AssertIsInstance("__package__", BuiltinTypeId.Bytes);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void CrossModuleBaseClasses() {
            var analyzer = CreateAnalyzer();
            var entryA = analyzer.AddModule("A", @"class ClsA(object): pass");
            var entryB = analyzer.AddModule("B", @"from A import ClsA
class ClsB(ClsA): pass

x = ClsB.x");
            analyzer.WaitForAnalysis();
            analyzer.AssertIsInstance(entryB, "x");

            analyzer.UpdateModule(entryA, @"class ClsA(object): x = 123");
            entryA.PreAnalyze();
            analyzer.WaitForAnalysis();
            analyzer.AssertIsInstance(entryB, "x", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void UndefinedVariableDiagnostic() {
            PythonAnalysis entry;
            string code;


            code = @"a = b + c
class D(b): pass
d()
D()
(e for e in e if e)
{f for f in f if f}
[g for g in g if g]

def func(b, c):
    b, c, d     # b, c are defined here
b, c, d         # but they are undefined here
";
            entry = ProcessTextV2(code);
            entry.AssertDiagnostics(
                "used-before-assignment:unknown variable 'b':(1, 5) - (1, 6)",
                "used-before-assignment:unknown variable 'c':(1, 9) - (1, 10)",
                "used-before-assignment:unknown variable 'b':(2, 9) - (2, 10)",
                "used-before-assignment:unknown variable 'd':(3, 1) - (3, 2)",
                "used-before-assignment:unknown variable 'e':(5, 13) - (5, 14)",
                "used-before-assignment:unknown variable 'f':(6, 13) - (6, 14)",
                "used-before-assignment:unknown variable 'g':(7, 13) - (7, 14)",
                "used-before-assignment:unknown variable 'd':(10, 11) - (10, 12)",
                "used-before-assignment:unknown variable 'b':(11, 1) - (11, 2)",
                "used-before-assignment:unknown variable 'c':(11, 4) - (11, 5)",
                "used-before-assignment:unknown variable 'd':(11, 7) - (11, 8)"
            );

            // Ensure all of these cases correctly generate no warning
            code = @"
for x in []:
    (_ for _ in x)
    [_ for _ in x]
    {_ for _ in x}
    {_ : _ for _ in x}

import sys
from sys import not_a_real_name_but_no_warning_anyway

def f(v = sys.version, u = not_a_real_name_but_no_warning_anyway):
    pass

with f() as v2:
    pass

";
            entry = ProcessTextV2(code);
            entry.AssertDiagnostics();
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void UncallableObjectDiagnostic() {
            var code = @"class MyClass:
    pass

class MyCallableClass:
    def __call__(self): return 123

mc = MyClass()
mcc = MyCallableClass()

x = mc()
y = mcc()
";
            var entry = ProcessTextV2(code);
            entry.AssertIsInstance("x");
            entry.AssertIsInstance("y", BuiltinTypeId.Int);
            entry.AssertDiagnostics(
                "not-callable:'MyClass' may not be callable:(10, 5) - (10, 7)"
            );
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void OsPathMembers() {
            var code = @"import os.path as P
";
            var version = PythonPaths.Versions.LastOrDefault(v => v.IsCPython && File.Exists(v.InterpreterPath));
            version.AssertInstalled();
            var entry = CreateAnalyzer(new Microsoft.PythonTools.Interpreter.Ast.AstPythonInterpreterFactory(
                version.Configuration,
                new InterpreterFactoryCreationOptions { DatabasePath = TestData.GetTempPath(), WatchFileSystem = false }
            ));
            entry.AddModule("test-module", code);
            entry.WaitForAnalysis();
            AssertUtil.ContainsAtLeast(entry.GetMemberNames("P"), "abspath", "dirname");
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void UnassignedClassMembers() {
            var code = @"
from typing import NamedTuple

class Employee(NamedTuple):
    name: str
    id: int = 3

e = Employee('Guido')
";
            var version = PythonPaths.Versions.LastOrDefault(v => v.IsCPython && File.Exists(v.InterpreterPath));
            version.AssertInstalled();
            var entry = CreateAnalyzer(new Microsoft.PythonTools.Interpreter.Ast.AstPythonInterpreterFactory(
                version.Configuration,
                new InterpreterFactoryCreationOptions { DatabasePath = TestData.GetTempPath(), WatchFileSystem = false }
            ));
            entry.AddModule("test-module", code);
            entry.WaitForAnalysis();
            AssertUtil.ContainsAtLeast(entry.GetMemberNames("e"), "name", "id");
        }

        #endregion

        #region Helpers
        private IPythonInterpreterFactory DefaultFactoryV2 => _factoryProvider.GetInterpreterFactory(IronPythonInterpreterFactoryProvider.GetInterpreterId(InterpreterArchitecture.x64));
        private IModuleContext DefaultContext => IronPythonModuleContext.DontShowClrInstance;
        private AnalysisLimits GetLimits() => AnalysisLimits.GetDefaultLimits();

        private PythonAnalysis CreateAnalyzerInternal(IPythonInterpreterFactory factory) {
            var analysis = new IronPythonAnalysis(factory);
            analysis.SetSearchPaths(Environment.CurrentDirectory);
            return analysis;
        }

        private PythonAnalysis CreateAnalyzer(IPythonInterpreterFactory factory = null, bool allowParseErrors = false) {
            var analysis = CreateAnalyzerInternal(factory ?? DefaultFactoryV2);
            analysis.AssertOnParseErrors = !allowParseErrors;
            analysis.Analyzer.EnableDiagnostics = true;
            analysis.ModuleContext = DefaultContext;
            analysis.SetLimits(GetLimits());

            if (_toDispose == null) {
                _toDispose = new List<IDisposable>();
            }
            _toDispose.Add(analysis);

            return analysis;
        }

        private PythonAnalysis ProcessTextV2(string text, bool allowParseErrors = false) {
            var analysis = CreateAnalyzer(DefaultFactoryV2, allowParseErrors);
            analysis.AddModule("test-module", text).WaitForCurrentParse();
            analysis.WaitForAnalysis();
            return analysis;
        }

        private PythonAnalysis ProcessText(string text, PythonLanguageVersion version = PythonLanguageVersion.None, bool allowParseErrors = false) {
            // TODO: Analyze against multiple versions when the version is None
            if (version == PythonLanguageVersion.None) {
                return ProcessTextV2(text, allowParseErrors);
            }

            var analysis = CreateAnalyzer(InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(version.ToVersion()), allowParseErrors);
            analysis.AddModule("test-module", text).WaitForCurrentParse();
            analysis.WaitForAnalysis();
            return analysis;
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

        private IEnumerable<PythonAnalysis> MakeModulePermutations(string prefix, string[] code) {
            foreach (var p in Permutations(code.Length)) {
                using (var state = CreateAnalyzer()) {
                    for (int i = 0; i < code.Length; i++) {
                        state.AddModule(string.Format(CultureInfo.InvariantCulture, "{0}{1}", prefix, p[i] + 1), code[p[i]]);
                    }

                    state.WaitForAnalysis();

                    yield return state;
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
        private void PermutedTest(string prefix, string[] code, Action<PythonAnalysis> test) {
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

        #endregion
    }
}

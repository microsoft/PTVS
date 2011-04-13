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

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using IronPython.Hosting;
using Microsoft.IronPythonTools.Interpreter;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnalysisTest {
    /// <summary>
    /// Analysis tests run against the IronPython interpreter including tests specific to IronPython.
    /// </summary>
    [TestClass]
    public class IronPythonAnalysisTest : AnalysisTest {
        private string[] _objectMembersClr, _strMembersClr;

        public IronPythonAnalysisTest() : base(CreateInterperter()) {
            _objectMembersClr = PyObjectType.GetMemberNames(IronPythonModuleContext.ShowClrInstance).ToArray();
            _strMembersClr = StringType.GetMemberNames(IronPythonModuleContext.ShowClrInstance).ToArray();

            Assert.IsTrue(_objectMembers.Length < _objectMembersClr.Length);
            Assert.IsTrue(_strMembers.Length < _strMembersClr.Length);
        }

        private static IPythonInterpreter CreateInterperter() {
            return new IronPythonInterpreter(Python.CreateEngine());
        }

        [TestMethod]
        public void TestImportClr() {
            var text = @"
import clr
x = 'abc'
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetMembersFromName("x", 1), _strMembersClr);
        }

        [TestMethod]
        public void TestClrAddReference() {
            var text = @"
import clr
clr.AddReference('System.Drawing')
from System.Drawing import Point
";
            var entry = ProcessText(text);
            var members = entry.GetMembersFromName("Point", GetLineNumber(text, "from System.")).ToList();

            Assert.AreEqual(35, members.Count);
        }

        [TestMethod]
        public void TestClrAddReferenceByName() {
            var text = @"
import clr
clr.AddReferenceByName('Microsoft.Scripting')
from Microsoft.Scripting import SourceUnit
";
            var entry = ProcessText(text);
            Assert.AreEqual(40, entry.GetMembersFromName("SourceUnit", GetLineNumber(text, "from Microsoft.")).ToList().Count);
        }

        [TestMethod]
        public void TestEnum() {
            var entry = ProcessText(@"
import System
x = System.StringComparison.OrdinalIgnoreCase
            ");

            var x = entry.GetValues("x", 1).First();
            Debug.Assert(x.ResultType == PythonMemberType.EnumInstance);
            Assert.AreEqual(x.ResultType, PythonMemberType.EnumInstance);
        }

        [TestMethod]
        public void TestColor() {

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

            AssertContainsExactly(entry.GetTypesFromName("b", 1).Select(x => x.Name), "Color");
        }

        [TestMethod]
        public void TestBuiltinTypeSignatures() {
            var entry = ProcessText(@"
import System
x = str
x = int

y = str
y = int
");

            var result = entry.GetSignatures("System.Collections.Generic.Dictionary[int, int]", 1).ToArray();
            Assert.AreEqual(result.Length, 6);

            // 2 possible types
            result = entry.GetSignatures("System.Collections.Generic.Dictionary[x, int]", 1).ToArray();
            Assert.AreEqual(result.Length, 12);

            // 4 possible types
            result = entry.GetSignatures("System.Collections.Generic.Dictionary[x, y]", 1).ToArray();
            Assert.AreEqual(result.Length, 24);
        }

        [TestMethod]
        public void TestEventReferences() {
            var text = @"
from System import EventHandler
def g():
    x = EventHandler(f)
    
def f(sender, args): pass
";
            var entry = ProcessText(text);
            VerifyReferences(entry.GetVariables("f", GetLineNumber(text, "x =")), new VariableLocation(4, 22, VariableType.Reference), new VariableLocation(6, 1, VariableType.Definition));

            text = @"
from System import EventHandler
def f(sender, args): pass

x = EventHandler(f)";
            entry = ProcessText(text);
            VerifyReferences(entry.GetVariables("f", GetLineNumber(text, "x =")), new VariableLocation(5, 18, VariableType.Reference), new VariableLocation(3, 1, VariableType.Definition));

            // left hand side is unknown, right hand side should still have refs added
            text = @"
from System import EventHandler
def f(sender, args): pass

a.foo += EventHandler(f)
";
            entry = ProcessText(text);
            VerifyReferences(entry.GetVariables("f", GetLineNumber(text, "a.foo +=")), new VariableLocation(5, 23, VariableType.Reference), new VariableLocation(3, 1, VariableType.Definition));
        }

        [TestMethod]
        public void TestSystemFromImport() {
            var text = @"
from System import Environment
Environment.GetCommandLineArgs()
";
            var entry = ProcessText(text);
            Assert.IsTrue(entry.GetMembersFromName("Environment", 1).Any(s => s == "CommandLine"));
        }

        [TestMethod]
        public void TestImportAs() {
            var text = @"
import System.Collections as coll
";
            var entry = ProcessText(text);
            Assert.IsTrue(entry.GetMembersFromName("coll", 1).Any(s => s == "ArrayList"));
        }

        [TestMethod]
        public void TestSystemImport() {
            var text = @"
import System
System.Environment.GetCommandLineArgs()
x = System.Environment
";
            var entry = ProcessText(text);
            var system = entry.GetMembersFromName("System", 1).ToSet();
            // defined in mscorlib
            AssertContains(system, "AccessViolationException");
            // defined in System
            AssertContains(system, "CodeDom");

            AssertContains(entry.GetMembersFromName("x", 1), "GetEnvironmentVariables");
        }

        [TestMethod]
        public void TestSystemMembers() {
            var text = @"
import System
System.Environment.GetCommandLineArgs()
x = System.Environment
args = x.GetCommandLineArgs()
";
            var entry = ProcessText(text);

            var args = entry.GetTypesFromName("args", GetLineNumber(text, "args =")).ToSet();
            AssertContainsExactly(args, ((IronPythonInterpreter)Interpreter).GetTypeFromType(typeof(string[])));

            Assert.IsTrue(entry.GetMembersFromName("args", GetLineNumber(text, "args =")).Any(s => s == "AsReadOnly"));
        }

        [TestMethod]
        public void TestNamespaceMembers() {
            var text = @"
import System
x = System.Collections
";
            var entry = ProcessText(text);
            var x = entry.GetMembersFromName("x", GetLineNumber(text, "x =")).ToSet();
            Assert.IsTrue(x.Contains("Generic"));
            Assert.IsTrue(x.Contains("ArrayList"));
        }

        [TestMethod]
        public void TestGenericIndexing() {
            // indexing into a generic type should know how the type info
            // flows through
            var text = @"
from System.Collections.Generic import List
x = List[int]()
";
            var entry = ProcessText(text);

            // AreEqual(entry.GetMembersFromName('x', len(text) - 1), 
            //     get_intersect_members_clr(List[int]))
            var self = new List<string>(entry.GetMembersFromName("x", GetLineNumber(text, "x =")));
            Assert.IsTrue(self.Contains("AddRange"));
        }

        [TestMethod]
        public void TestReturnTypesCollapsing() {
            // indexing into a generic type should know how the type info
            // flows through
            var text = @"
from System import AppDomain
asm = AppDomain.CurrentDomain.DefineDynamicAssembly()
mod = asm.DefineDynamicModule()
mod.
";
            var entry = ProcessText(text);
            var tooltips = entry.GetMembers("mod.", GetLineNumber(text, "mod ="))
                .Where(m => m.Name == "CreateGlobalFunctions")
                .Select(m => m.Documentation)
                .ToArray();
            Assert.AreEqual(1, tooltips.Length);
#if IPY
            var indexes = tooltips[0].FindIndexesOf("CreateGlobalFunctions").ToArray();
            Assert.AreEqual(1, indexes.Length);
#endif
        }

        [TestMethod]
        public void TestAssignEvent() {
            var text = @"
import System

def f(sender, args):
    pass
    
System.AppDomain.CurrentDomain.AssemblyLoad += f
";
            var entry = ProcessText(text);
            Assert.IsTrue(entry.GetMembersFromName("args", GetLineNumber(text, "pass")).Any(s => s == "LoadedAssembly"));
        }

        [TestMethod]
        public void TestEventMemberType() {
            var text = @"from System import AppDomain";
            var entry = ProcessText(text);
            var mem = entry.GetMembers("AppDomain.", 1).Where(x =>x.Name == "AssemblyLoad").First();
            Assert.AreEqual(mem.MemberType, PythonMemberType.Event);
        }

        [TestMethod]
        public void TestNegCallProperty() {
            // invalid code, this shouldn't crash us.
            var text = @"
import System
x = System.String.Length()
y = System.Environment.CurrentDirectory()
";
            ProcessText(text);
        }

        [TestMethod]
        public void TestQuickInfoClr() {
            var text = @"
import System
from System.Collections import ArrayList

e = System.Collections.ArrayList()

def g():
    return System
    return c.Length
";
            var entry = ProcessText(text);

            AssertContainsExactly(GetVariableDescriptions(entry, "System.String.Length", 1), "property of type int");
            AssertContainsExactly(GetVariableDescriptions(entry, "System.Environment.CurrentDirectory", 1), "str");
            AssertContainsExactly(GetVariableDescriptions(entry, "e", 1), "ArrayList");
            AssertContainsExactly(GetVariableShortDescriptions(entry, "ArrayList", 1), "type ArrayList");
            AssertContainsExactly(GetVariableDescriptions(entry, "e.Count", 1), "int");
            AssertContainsExactly(GetVariableDescriptions(entry, "System.DBNull.Value", 1), "DBNull");
            AssertContainsExactly(GetVariableShortDescriptions(entry, "System.StringSplitOptions", 1), "type StringSplitOptions");
            //AssertContainsExactly(GetVariableDescriptions(entry, "\"abc\".Length", 1), "int");
            //AssertContainsExactly(GetVariableDescriptions(entry, "c.Length", 1), "int");
            //AssertContainsExactly(GetVariableDescriptions(entry, "System.StringSplitOptions.RemoveEmptyEntries", 0), "field of type StringSplitOptions");
            AssertContainsExactly(GetVariableDescriptions(entry, "g", 1), "def g(...)");    // return info could be better
            //AssertContainsExactly(GetVariableDescriptions(entry, "System.AppDomain.DomainUnload", 1), "event of type System.EventHandler");
        }

        [TestMethod]
        public void TestBuiltinMethodSignaturesClr() {
            var entry = ProcessText(@"
import clr
const = """".Contains
constructed = str().Contains
");

            string[] testContains = new[] { "const", "constructed" };
            foreach (var test in testContains) {
                var result = entry.GetSignatures(test, 1).ToArray();
                Assert.AreEqual(result.Length, 1);
                Assert.AreEqual(result[0].Parameters.Length, 1);
                Assert.AreEqual(result[0].Parameters[0].Name, "value");
                Assert.AreEqual(result[0].Parameters[0].IsOptional, false);
            }

        }
        /*
        [TestMethod]
        public void TestOverrideParams() {
            var text = @"
import System

class MyArrayList(System.Collections.ArrayList):
    def AddRange(self, col):
        x = col
";
            var entry = ProcessText(text);
            var x = entry.GetMembersFromName("x", text.IndexOf("x = col")).ToSet();
            AssertContainsExactly(x, GetMembers(ClrModule.GetPythonType(typeof(System.Collections.ICollection)), true));
        }*/

#if IPY
        /// <summary>
        /// Verify importing wpf will add a reference to the WPF assemblies
        /// </summary>
        [TestMethod]
        public void TestWpfReferences() {
            var entry = ProcessText(@"
import wpf
from System.Windows.Media import Colors
");

            AssertContains(entry.GetMembersFromName("Colors", 1), "Blue");
        }
#endif

    }
}

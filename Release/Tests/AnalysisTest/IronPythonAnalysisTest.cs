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
using IronPython.Hosting;
using Microsoft.IronPythonTools.Interpreter;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.Remoting;

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
            var res = new IronPythonInterpreter(new IronPythonInterpreterFactory());
            res.Remote.AddAssembly(new ObjectHandle(typeof(IronPythonAnalysisTest).Assembly));
            return res;
        }

        public override string UnicodeStringType {
            get {
                return "str";
            }
        }

        [TestMethod]
        public void TestGenerics() {
            var text = @"
import clr
clr.AddReference('AnalysisTest')
from AnalysisTest.DotNetAnalysis import *

y = GenericType()
zzz = y.ReturnsGenericParam()
";
            var entry = ProcessText(text, analysisDirs: new[] { Environment.CurrentDirectory });
            AssertContainsExactly(entry.GetMembersFromNameByIndex("zzz", 1), "GetEnumerator", "__doc__", "__iter__", "__repr__");
        }


        [TestMethod]
        public void TestImportClr() {
            var text = @"
import clr
x = 'abc'
";
            var entry = ProcessText(text);
            AssertContainsExactly(entry.GetMembersFromNameByIndex("x", 1), _strMembersClr);
        }

        [TestMethod]
        public void TestClrAddReference() {
            var text = @"
import clr
clr.AddReference('System.Drawing')
from System.Drawing import Point
";
            var entry = ProcessText(text);
            var members = entry.GetMembersFromNameByIndex("Point", text.IndexOf("from System.")).ToList();

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
            Assert.AreEqual(40, entry.GetMembersFromNameByIndex("SourceUnit", text.IndexOf("from Microsoft.")).ToList().Count);
        }

        [TestMethod]
        public void TestEnum() {
            var entry = ProcessText(@"
import System
x = System.StringComparison.OrdinalIgnoreCase
            ");

            var x = entry.GetValuesByIndex("x", 1).First();
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

            AssertContainsExactly(entry.GetTypesFromNameByIndex("b", 1).Select(x => x.Name), "Color");
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

            var result = entry.GetSignaturesByIndex("System.Collections.Generic.Dictionary[int, int]", 1).ToArray();
            Assert.AreEqual(result.Length, 6);

            // 2 possible types
            result = entry.GetSignaturesByIndex("System.Collections.Generic.Dictionary[x, int]", 1).ToArray();
            Assert.AreEqual(result.Length, 12);

            // 4 possible types
            result = entry.GetSignaturesByIndex("System.Collections.Generic.Dictionary[x, y]", 1).ToArray();
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

a.foo += EventHandler(f)
";
            entry = ProcessText(text);
            VerifyReferences(UniqifyVariables(entry.GetVariablesByIndex("f", text.IndexOf("a.foo +="))), new VariableLocation(5, 23, VariableType.Reference), new VariableLocation(3, 5, VariableType.Definition));
        }

        [TestMethod]
        public void TestSystemFromImport() {
            var text = @"
from System import Environment
Environment.GetCommandLineArgs()
";
            var entry = ProcessText(text);
            Assert.IsTrue(entry.GetMembersFromNameByIndex("Environment", 1).Any(s => s == "CommandLine"));
        }

        [TestMethod]
        public void TestImportAsIpy() {
            var text = @"
import System.Collections as coll
";
            var entry = ProcessText(text);
            Assert.IsTrue(entry.GetMembersFromNameByIndex("coll", 1).Any(s => s == "ArrayList"));
        }

        [TestMethod]
        public void TestSystemImport() {
            var text = @"
import System
System.Environment.GetCommandLineArgs()
x = System.Environment
";
            var entry = ProcessText(text);
            var system = entry.GetMembersFromNameByIndex("System", 1).ToSet();
            // defined in mscorlib
            AssertContains(system, "AccessViolationException");
            // defined in System
            AssertContains(system, "CodeDom");

            AssertContains(entry.GetMembersFromNameByIndex("x", 1), "GetEnvironmentVariables");
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

            var args = entry.GetTypesFromNameByIndex("args", text.IndexOf("args =")).Select(x => x.Name).ToSet();
            AssertContainsExactly(args, "Array[str]");

            Assert.IsTrue(entry.GetMembersFromNameByIndex("args", text.IndexOf("args =")).Any(s => s == "AsReadOnly"));
        }

        [TestMethod]
        public void TestNamespaceMembers() {
            var text = @"
import System
x = System.Collections
";
            var entry = ProcessText(text);
            var x = entry.GetMembersFromNameByIndex("x", text.IndexOf("x =")).ToSet();
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
            var self = new List<string>(entry.GetMembersFromNameByIndex("x", text.IndexOf("x =")));
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

        [TestMethod]
        public void TestAssignEvent() {
            var text = @"
import System

def f(sender, args):
    pass
    
System.AppDomain.CurrentDomain.AssemblyLoad += f
";
            var entry = ProcessText(text);
            Assert.IsTrue(entry.GetMembersFromNameByIndex("args", text.IndexOf("pass")).Any(s => s == "LoadedAssembly"));
        }

        [TestMethod]
        public void TestEventMemberType() {
            var text = @"from System import AppDomain";
            var entry = ProcessText(text);
            var mem = entry.GetMembersByIndex("AppDomain", 1).Where(x => x.Name == "AssemblyLoad").First();
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

            AssertContainsExactly(GetVariableDescriptionsByIndex(entry, "System.String.Length", 1), "property of type int");
            AssertContainsExactly(GetVariableDescriptionsByIndex(entry, "System.Environment.CurrentDirectory", 1), "str");
            AssertContainsExactly(GetVariableDescriptionsByIndex(entry, "e", 1), "ArrayList");
            AssertContainsExactly(GetVariableShortDescriptionsByIndex(entry, "ArrayList", 1), "type ArrayList");
            AssertContainsExactly(GetVariableDescriptionsByIndex(entry, "e.Count", 1), "int");
            AssertContainsExactly(GetVariableDescriptionsByIndex(entry, "System.DBNull.Value", 1), "DBNull");
            AssertContainsExactly(GetVariableShortDescriptionsByIndex(entry, "System.StringSplitOptions", 1), "type StringSplitOptions");
            //AssertContainsExactly(GetVariableDescriptions(entry, "\"abc\".Length", 1), "int");
            //AssertContainsExactly(GetVariableDescriptions(entry, "c.Length", 1), "int");
            //AssertContainsExactly(GetVariableDescriptions(entry, "System.StringSplitOptions.RemoveEmptyEntries", 0), "field of type StringSplitOptions");
            AssertContainsExactly(GetVariableDescriptionsByIndex(entry, "g", 1), "def g(...)");    // return info could be better
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
                var result = entry.GetSignaturesByIndex(test, 1).ToArray();
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

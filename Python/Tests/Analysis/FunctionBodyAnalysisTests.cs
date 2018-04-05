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

using System.Linq;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    [TestClass]
    public class FunctionBodyAnalysisTests : BaseAnalysisTest {
        protected override bool SupportsPython3 => true;

        [TestMethod, Priority(0)]
        public void ParameterInfoSet() {
            var text = @"def f(a, b):
    pass

f(1, 3.14)
a = 7.28
b = 3.14
";
            var entry = ProcessTextV3(text);

            // Ensure that calls update the special section of values, but not the
            // ones in the normal function scope
            var scope = entry.GetValue<FunctionInfo>("f").AnalysisUnit.Scope;
            var a = scope.GetVariable("a").TypesNoCopy.Single();
            var b = scope.GetVariable("b").TypesNoCopy.Single();
            Assert.IsInstanceOfType(a, typeof(ParameterInfo));
            Assert.IsInstanceOfType(b, typeof(ParameterInfo));

            // Ensure that normal variable lookup inside a function resolves to the
            // actual values.
            entry.AssertIsInstance("a", text.IndexOf("pass"), BuiltinTypeId.Int);
            entry.AssertIsInstance("b", text.IndexOf("pass"), BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public void ParameterInfoReturnValue() {
            var text = @"def f(a, b):
    return a

r_a = f(1, 3.14)
r_b = f(b=1, a=3.14)
r_a = f(1, 3.14)
";
            var entry = ProcessTextV3(text);

            // Ensure that the internal return type is correct
            var f = entry.GetValue<FunctionInfo>("f");
            var rv = ((FunctionAnalysisUnit)f.AnalysisUnit).ReturnValue.TypesNoCopy.Single();
            Assert.IsInstanceOfType(rv, typeof(ParameterInfo));
            Assert.AreEqual("a", ((ParameterInfo)rv).Name);

            // Ensure that normal resolution returns the union
            AssertUtil.ContainsExactly(f.GetReturnValue().Select(v => v.TypeId), BuiltinTypeId.Int, BuiltinTypeId.Float);
            entry.AssertIsInstance("f()", 0, BuiltinTypeId.Int, BuiltinTypeId.Float);

            // Ensure that specific calls return the specific type
            entry.AssertIsInstance("r_a", 0, BuiltinTypeId.Int);
            entry.AssertIsInstance("r_b", 0, BuiltinTypeId.Float);
            // Unevaluated calls return all types
            entry.AssertIsInstance("f(1)", 0, BuiltinTypeId.Int, BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public void ChainedParameterInfoReturnValue() {
            var text = @"def f(a, b):
    return a

def g(x, y):
    return f(x, y)

r_a = g(1, 3.14)
r_b = g(y=1, x=3.14)
r_a = g(1, 3.14)
";
            var entry = ProcessTextV3(text);

            // Ensure that the internal return types are correct
            var f = entry.GetValue<FunctionInfo>("f");
            var rv = ((FunctionAnalysisUnit)f.AnalysisUnit).ReturnValue.TypesNoCopy.Single();
            Assert.IsInstanceOfType(rv, typeof(ParameterInfo));
            Assert.AreEqual("a", ((ParameterInfo)rv).Name);

            var g = entry.GetValue<FunctionInfo>("g");
            rv = ((FunctionAnalysisUnit)g.AnalysisUnit).ReturnValue.TypesNoCopy.Single();
            Assert.IsInstanceOfType(rv, typeof(ParameterInfo));
            Assert.AreEqual("x", ((ParameterInfo)rv).Name);

            // Ensure that normal resolution returns the union
            AssertUtil.ContainsExactly(f.GetReturnValue().Select(v => v.TypeId), BuiltinTypeId.Int, BuiltinTypeId.Float);
            AssertUtil.ContainsExactly(g.GetReturnValue().Select(v => v.TypeId), BuiltinTypeId.Int, BuiltinTypeId.Float);
            entry.AssertIsInstance("f()", 0, BuiltinTypeId.Int, BuiltinTypeId.Float);
            entry.AssertIsInstance("g()", 0, BuiltinTypeId.Int, BuiltinTypeId.Float);

            // Ensure that the callee also has corrcet values
            entry.AssertIsInstance("a", text.IndexOf("return a"), BuiltinTypeId.Int, BuiltinTypeId.Float);
            entry.AssertIsInstance("b", text.IndexOf("return a"), BuiltinTypeId.Int, BuiltinTypeId.Float);

            // Ensure that specific calls return the specific type
            entry.AssertIsInstance("r_a", 0, BuiltinTypeId.Int);
            entry.AssertIsInstance("r_b", 0, BuiltinTypeId.Float);
            // Unevaluated calls return all types
            entry.AssertIsInstance("g(1)", 0, BuiltinTypeId.Int, BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public void LazyMemberOnParameter() {
            var text = @"class C:
    x = 123
class D:
    x = 3.14

def f(v):
    return v.x

c = f(C())
d = f(D())";
            var entry = ProcessTextV3(text);

            entry.AssertIsInstance("v", text.IndexOf("return"), "C", "D");

            entry.AssertIsInstance("c", BuiltinTypeId.Int);
            entry.AssertIsInstance("d", BuiltinTypeId.Float);
        }
    }
}

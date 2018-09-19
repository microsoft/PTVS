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

using System.IO;
using System.Linq;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Ast;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    [TestClass]
    public class InheritanceTests : BaseAnalysisTest {
        [TestInitialize]
        public void TestInitialize() => TestEnvironmentImpl.TestInitialize();

        [TestCleanup]
        public void TestCleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod]
        public void AbstractMethodReturnTypeIgnored() {
            var python = (PythonPaths.Python36_x64 ?? PythonPaths.Python36);
            python.AssertInstalled();
            var analyzer = CreateAnalyzer(
                new AstPythonInterpreterFactory(python.Configuration, new InterpreterFactoryCreationOptions { WatchFileSystem = false })
            );
            analyzer.AddModule("test-module", @"
import abc

class A:
    @abc.abstractmethod
    def virt():
        pass

class B(A):
    def virt():
        return 42

a = A()
b = a.virt()
");

            // this example is artificial, as generally one should not be able to call A.virt
            // but it is the easiest way to reproduce
            analyzer.WaitForAnalysis();

            analyzer.AssertIsInstance("b", BuiltinTypeId.Int);
        }
    }
}

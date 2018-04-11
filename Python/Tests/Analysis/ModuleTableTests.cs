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
using System.Linq;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;

namespace AnalysisTests {
    [TestClass]
    public class ModuleTableTests {
        [TestInitialize]
        public void TestInitialize() => TestEnvironmentImpl.TestInitialize();

        [TestCleanup]
        public void TestCleanup() => TestEnvironmentImpl.TestCleanup();

        class MockPythonModule : IPythonModule {
            private readonly string _name;

            public MockPythonModule(string name) {
                _name = name;
            }

            public string Documentation => "";
            public PythonMemberType MemberType => PythonMemberType.Module;
            public string Name => _name;
            public IEnumerable<string> GetChildrenModules() => Enumerable.Empty<string>();
            public IMember GetMember(IModuleContext context, string name) => null;
            public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) => Enumerable.Empty<string>();
            public void Imported(IModuleContext context) { }
        }

        [TestMethod]
        public void RemovedModule() {
            var id = Guid.NewGuid().ToString();
            var config = new InterpreterConfiguration(id, id, version: new Version(3, 5));
            var fact = new MockPythonInterpreterFactory(config);
            var interp = new MockPythonInterpreter(fact);
            var analyzer = PythonAnalyzer.CreateSynchronously(fact, interp);
            var modules = new ModuleTable(analyzer, interp);

            var orig = modules.Select(kv => kv.Key).ToSet();

            interp.AddModule("test", new MockPythonModule("test"));

            ModuleReference modref;
            Assert.IsTrue(modules.TryImport("test", out modref));

            interp.RemoveModule("test", retainName: true);

            modules.ReInit();
            Assert.IsFalse(modules.TryImport("test", out modref));
        }
    }
}

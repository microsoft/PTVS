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

using Microsoft.PythonTools.Common.Parsing;
using Microsoft.PythonTools.Debugger.Concord;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DebuggerTests {
    [TestClass]
    public class PythonRuntimeInfoTests {
        [TestMethod, Priority(0)]
        public void RecognizesPython315ModuleNames() {
            var moduleNames = new[] {
                "python315.dll",
                "python315_d.dll",
                "python315t.dll",
                "python315t_d.dll",
            };

            foreach (var moduleName in moduleNames) {
                var version = PythonDLLs.GetPythonLanguageVersion(moduleName);
                Assert.AreEqual(PythonLanguageVersion.V315, version, moduleName);
                Assert.IsTrue(version >= PythonLanguageVersion.V314, moduleName);
            }
        }

        [TestMethod, Priority(0)]
        public void RecognizesFreeThreadedModuleNamesSincePython313() {
            Assert.AreEqual(PythonLanguageVersion.V313, PythonDLLs.GetPythonLanguageVersion("python313t.dll"));
            Assert.AreEqual(PythonLanguageVersion.V313, PythonDLLs.GetPythonLanguageVersion("python313t_d.dll"));
            Assert.AreEqual(PythonLanguageVersion.V314, PythonDLLs.GetPythonLanguageVersion("python314t.dll"));
            Assert.AreEqual(PythonLanguageVersion.V314, PythonDLLs.GetPythonLanguageVersion("python314t_d.dll"));
            Assert.AreEqual(PythonLanguageVersion.None, PythonDLLs.GetPythonLanguageVersion("python312t.dll"));
            Assert.AreEqual(PythonLanguageVersion.None,
                PythonDLLs.GetPythonLanguageVersion("python399999999999999999999999t.dll"));
        }
    }
}

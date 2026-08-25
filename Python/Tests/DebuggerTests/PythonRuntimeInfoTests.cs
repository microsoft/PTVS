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
using Microsoft.PythonTools.Debugger.Concord.Proxies;
using Microsoft.PythonTools.Debugger.Concord.Proxies.Structs;
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
            }
        }

        [TestMethod, Priority(0)]
        public void StackReferenceHelpersClearTagBitsByVersion() {
            const ulong taggedExecutable = 0x1234567BUL;

            Assert.AreEqual(taggedExecutable,
                PointerProxy.RemoveTagBits(taggedExecutable,
                    PyInterpreterFrame.GetStackReferenceTagMask(PythonLanguageVersion.V313)));
            Assert.AreEqual(0x12345678UL,
                PointerProxy.RemoveTagBits(taggedExecutable,
                    PyInterpreterFrame.GetStackReferenceTagMask(PythonLanguageVersion.V314)));
            Assert.AreEqual(0x12345678UL,
                PointerProxy.RemoveTagBits(taggedExecutable,
                    PyInterpreterFrame.GetStackReferenceTagMask(PythonLanguageVersion.V315)));
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

        [TestMethod, Priority(0)]
        public void EvalFrameFunctionNameDependsOnLanguageVersion() {
            Assert.AreEqual("PyEval_EvalFrameEx", PyFrameObject.GetEvalFrameFunctionName(PythonLanguageVersion.V27));
            Assert.AreEqual("PyEval_EvalFrameEx", PyFrameObject.GetEvalFrameFunctionName(PythonLanguageVersion.V35));
            Assert.AreEqual("_PyEval_EvalFrameDefault", PyFrameObject.GetEvalFrameFunctionName(PythonLanguageVersion.V36));
            Assert.AreEqual("_PyEval_EvalFrameDefault", PyFrameObject.GetEvalFrameFunctionName(PythonLanguageVersion.V313));
        }

        [TestMethod, Priority(0)]
        public void IsEvalFrameNameMatchesEvalFrameFunction() {
            Assert.IsTrue(PyFrameObject.IsEvalFrameName("PyEval_EvalFrameEx", PythonLanguageVersion.V35));
            Assert.IsTrue(PyFrameObject.IsEvalFrameName("_PyEval_EvalFrameDefault", PythonLanguageVersion.V313));

            // The eval frame function is version specific, so the name for one version must not
            // match a different version.
            Assert.IsFalse(PyFrameObject.IsEvalFrameName("_PyEval_EvalFrameDefault", PythonLanguageVersion.V35));
            Assert.IsFalse(PyFrameObject.IsEvalFrameName("PyEval_EvalFrameEx", PythonLanguageVersion.V313));

            Assert.IsFalse(PyFrameObject.IsEvalFrameName("PyRun_StringFlags", PythonLanguageVersion.V313));
            Assert.IsFalse(PyFrameObject.IsEvalFrameName("", PythonLanguageVersion.V313));
        }

        [TestMethod, Priority(0)]
        public void IsEvalFrameNameHandlesFramesWithoutSymbols() {
            // DkmStackWalkFrame.BasicSymbolInfo is null for frames with no symbol information
            // available, so a null method name must return false rather than throw. Regression
            // test for a NullReferenceException that crashed Visual Studio during stack walks.
            Assert.IsFalse(PyFrameObject.IsEvalFrameName(null, PythonLanguageVersion.V313));
            Assert.IsFalse(PyFrameObject.IsEvalFrameName(null, PythonLanguageVersion.V35));
            Assert.IsFalse(PyFrameObject.IsEvalFrameName(null, PythonLanguageVersion.None));
        }
    }
}

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

using System.IO;
using System.Linq;
using EnvDTE;
using EnvDTE90a;
using TestUtilities;
using TestUtilities.UI;
using TestUtilities.UI.Python;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace DebuggerUITests {
    public class MixedModeDebugProjectUITests {
        #region Test Cases

        /// <summary>
        /// Verify that debugging starts and a breakpoint is hit.
        /// </summary>
        public void DebugPurePythonProject(PythonVisualStudioApp app, string interpreter) {
            using (app.SelectDefaultInterpreter(FindInterpreter(interpreter))) {
                StartHelloWorldAndBreak(app);

                app.Dte.Debugger.Go(WaitForBreakOrEnd: true);
                Assert.AreEqual(dbgDebugMode.dbgDesignMode, app.Dte.Debugger.CurrentMode);
            }
        }

        /// <summary>
        /// Verify mixed-mode (native+Python) debugging launch for supported (Python3+) interpreters.
        /// </summary>
        public void DebugMixedModePythonProject(PythonVisualStudioApp app, string interpreter) {
            using (app.SelectDefaultInterpreter(FindInterpreter(interpreter))) {
                if (interpreter.StartsWith("Python27")) {
                    Assert.Inconclusive("Mixed-mode debugging not supported for Python 2.x");
                }
                DebugProjectUITests.OpenProjectAndBreak(app, @"TestData\MixedModeHelloWorld.sln", "Program.py", 1);
                app.Dte.Debugger.Go(WaitForBreakOrEnd: true);
                Assert.AreEqual(dbgDebugMode.dbgDesignMode, app.Dte.Debugger.CurrentMode);
            }
        }

        public void DebugMixedModeStepping(PythonVisualStudioApp app, string interpreter) {
            using (app.SelectDefaultInterpreter(FindInterpreter(interpreter)))
            using (new DebugProjectUITests.DotNotWaitOnNormalExit(app.Dte)) {
                DebugProjectUITests.OpenProjectAndBreak(app, @"TestData\MixedModeHelloWorld.sln", "Stepping.py", 4);

                app.Dte.Debugger.StepOver(true);
                AssertCurrentLine(app, 5);
                app.Dte.Debugger.StepInto(true);
                AssertCurrentLine(app, 1);
                app.Dte.Debugger.StepOver(true);
                AssertCurrentLine(app, 2);
                app.Dte.Debugger.StepOut(true);
                AssertCurrentLine(app, 5);
                app.Dte.Debugger.StepOver(true);
                AssertCurrentLine(app, 6);
                app.Dte.Debugger.StepInto(true);
                AssertCurrentLine(app, 1);
                app.Dte.Debugger.StepOver(true);
                AssertCurrentLine(app, 2);
                app.Dte.Debugger.StepOut(true);
                AssertCurrentLine(app, 6);
                app.Dte.Debugger.StepOver(true);
                AssertCurrentLine(app, 7);
                app.Dte.Debugger.StepOver(true);
                AssertCurrentLine(app, 8);
                Assert.AreEqual("3", app.Dte.Debugger.GetExpression("result").Value);
                Assert.AreEqual("7", app.Dte.Debugger.GetExpression("keyword_result").Value);
                Assert.AreEqual("3", app.Dte.Debugger.GetExpression("native_result").Value);

                app.Dte.Debugger.StepOver(true);
                AssertCurrentLine(app, 10);
                app.Dte.Debugger.StepOver(true);
                AssertCurrentLine(app, 14);
                app.Dte.Debugger.StepInto(true);
                AssertCurrentLine(app, 11);
                app.Dte.Debugger.StepOver(true);
                AssertCurrentLine(app, 12);
                app.Dte.Debugger.StepOut(true);
                AssertCurrentLine(app, 14);
                app.Dte.Debugger.StepOver(true);
                AssertCurrentLine(app, 15);
                Assert.AreEqual("True", app.Dte.Debugger.GetExpression("comparison_result").Value);

                app.Dte.Debugger.Go(WaitForBreakOrEnd: true);
                Assert.AreEqual(dbgDebugMode.dbgDesignMode, app.Dte.Debugger.CurrentMode);
            }
        }

        #endregion

        #region Helpers

        private static void AssertCurrentLine(VisualStudioApp app, uint line) {
            Assert.AreEqual(dbgDebugMode.dbgBreakMode, app.Dte.Debugger.CurrentMode);
            var frame = (StackFrame2)app.Dte.Debugger.CurrentStackFrame;
            Assert.AreEqual("Stepping.py", Path.GetFileName(frame.FileName));
            Assert.AreEqual(line, frame.LineNumber);
        }

        private static void StartHelloWorldAndBreak(VisualStudioApp app) {
            DebugProjectUITests.OpenProjectAndBreak(app, @"TestData\MixedModeHelloWorld.sln", "Program.py", 1);
        }

        private static bool HasSymbols(PythonVersion interpreter) {
            if (interpreter == null) {
                return false;
            }

            // For simplicity, require the test machine to have the .pdb files
            // next to their binaries.
            return File.Exists(Path.ChangeExtension(interpreter.Configuration.InterpreterPath, "pdb"));
        }

        private static PythonVersion FindInterpreter(string pythonVersion) {
            var interpreter = PythonPaths.GetVersionsByName(pythonVersion, HasSymbols).FirstOrDefault();
            if (interpreter == null) {
                Assert.Inconclusive($"Interpreter '{pythonVersion}' not installed.");
            }

            return interpreter;
        }

        #endregion
    }
}
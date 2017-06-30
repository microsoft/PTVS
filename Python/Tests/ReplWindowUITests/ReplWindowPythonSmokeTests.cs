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
using System.Linq;
using System.Threading;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;
using TestUtilities.UI.Python;

namespace ReplWindowUITests {
    /// <summary>
    /// These tests must be run for all supported versions of Python that may
    /// use the REPL.
    /// </summary>
    [TestClass, Ignore]
    public abstract class ReplWindowPythonSmokeTests {
        static ReplWindowPythonSmokeTests() {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        internal abstract PythonReplWindowProxySettings Settings {
            get;
        }

        internal virtual ReplWindowProxy Prepare(
            bool enableAttach = false,
            bool useIPython = false,
            bool addNewLineAtEndOfFullyTypedWord = false
        ) {
            var s = Settings;
            if (s.Version == null) {
                Assert.Inconclusive("Interpreter missing for " + GetType().Name);
            }

            if (addNewLineAtEndOfFullyTypedWord != s.AddNewLineAtEndOfFullyTypedWord) {
                s = object.ReferenceEquals(s, Settings) ? s.Clone() : s;
                s.AddNewLineAtEndOfFullyTypedWord = addNewLineAtEndOfFullyTypedWord;
            }

            return ReplWindowProxy.Prepare(s, useIPython: useIPython);
        }

        [TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public virtual void ExecuteInReplSysArgv() {
            using (var app = new PythonVisualStudioApp())
            using (new DefaultInterpreterSetter(InterpreterFactoryCreator.CreateInterpreterFactory(Settings.Version.Configuration))) {
                app.ServiceProvider.GetUIThread().Invoke(() => {
                    app.ServiceProvider.GetPythonToolsService().InteractiveBackendOverride = "standard";
                });

                var project = app.OpenProject(@"TestData\SysArgvRepl.sln");

                using (var interactive = app.ExecuteInInteractive(project, Settings)) {
                    interactive.WaitForTextEnd("Program.py']", ">");
                }
            }
        }

        [TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public virtual void ExecuteInReplSysArgvScriptArgs() {
            using (var app = new PythonVisualStudioApp())
            using (new DefaultInterpreterSetter(InterpreterFactoryCreator.CreateInterpreterFactory(Settings.Version.Configuration))) {
                app.ServiceProvider.GetUIThread().Invoke(() => {
                    app.ServiceProvider.GetPythonToolsService().InteractiveBackendOverride = "standard";
                });

                var project = app.OpenProject(@"TestData\SysArgvScriptArgsRepl.sln");

                using (var interactive = app.ExecuteInInteractive(project, Settings)) {
                    interactive.WaitForTextEnd(@"Program.py', '-source', 'C:\\Projects\\BuildSuite', '-destination', 'C:\\Projects\\TestOut', '-pattern', '*.txt', '-recurse', 'true']", ">");
                }
            }
        }

        [TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public virtual void ExecuteInReplUnicodeFilename() {
            using (var app = new PythonVisualStudioApp())
            using (new DefaultInterpreterSetter(InterpreterFactoryCreator.CreateInterpreterFactory(Settings.Version.Configuration))) {
                app.ServiceProvider.GetUIThread().Invoke(() => {
                    app.ServiceProvider.GetPythonToolsService().InteractiveBackendOverride = "standard";
                });

                var project = app.OpenProject(PythonTestData.GetUnicodePathSolution());

                using (var interactive = app.ExecuteInInteractive(project, Settings)) {
                    interactive.WaitForTextEnd("hello world from unicode path", ">");
                }
            }
        }

        [TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public virtual void CwdImport() {
            using (var interactive = Prepare()) {
                interactive.SubmitCode("import sys\nsys.path");
                interactive.SubmitCode("import os\nos.chdir(r'" + TestData.GetPath("TestData\\ReplCwd") + "')");

                var importErrorFormat = ((PythonReplWindowProxySettings)interactive.Settings).ImportError;
                interactive.SubmitCode("import module1");
                interactive.WaitForTextEnd(string.Format(importErrorFormat, "module1").Split('\n').Concat(new[] { ">" }).ToArray());

                interactive.SubmitCode("import module2");
                interactive.WaitForTextEnd(string.Format(importErrorFormat, "module2").Split('\n').Concat(new[] { ">" }).ToArray());

                interactive.SubmitCode("os.chdir('A')");
                interactive.WaitForTextEnd(">os.chdir('A')", ">");

                interactive.SubmitCode("import module1");
                interactive.WaitForTextEnd(">import module1", ">");

                interactive.SubmitCode("import module2");
                interactive.WaitForTextEnd(string.Format(importErrorFormat, "module2").Split('\n').Concat(new[] { ">" }).ToArray());

                interactive.SubmitCode("os.chdir('..\\B')");
                interactive.WaitForTextEnd(">os.chdir('..\\B')", ">");

                interactive.SubmitCode("import module2");
                interactive.WaitForTextEnd(">import module2", ">");
            }
        }

        [TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public virtual void QuitAndReset() {
            using (var interactive = Prepare()) {
                interactive.SubmitCode("quit()");
                interactive.WaitForText(">quit()", "The interactive Python process has exited.", ">");
                interactive.Reset();

                interactive.WaitForText(">quit()", "The interactive Python process has exited.", "Resetting Python state.", ">");
                interactive.SubmitCode("42");

                interactive.WaitForTextEnd(">42", "42", ">");
            }
        }

        [TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public virtual void PrintAllCharacters() {
            using (var interactive = Prepare()) {
                interactive.SubmitCode("print(\"" +
                    string.Join("", Enumerable.Range(0, 256).Select(i => string.Format("\\x{0:X2}", i))) +
                    "\\nDONE\")",
                    timeout: TimeSpan.FromSeconds(10.0)
                );

                interactive.WaitForTextEnd("DONE", ">");
            }
        }

        protected static void WaitForMode(EnvDTE.Debugger debugger, EnvDTE.dbgDebugMode mode) {
            for (int i = 0; i < 30 && debugger.CurrentMode != mode; i++) {
                Thread.Sleep(1000);
            }

            Assert.AreEqual(mode, debugger.CurrentMode);
        }
    }
}

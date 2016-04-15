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
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.PythonTools;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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

            if (enableAttach != s.EnableAttach) {
                s = object.ReferenceEquals(s, Settings) ? s.Clone() : s;
                s.EnableAttach = enableAttach;
            }
            if (addNewLineAtEndOfFullyTypedWord != s.AddNewLineAtEndOfFullyTypedWord) {
                s = object.ReferenceEquals(s, Settings) ? s.Clone() : s;
                s.AddNewLineAtEndOfFullyTypedWord = addNewLineAtEndOfFullyTypedWord;
            }
            if (useIPython) {
                s = object.ReferenceEquals(s, Settings) ? s.Clone() : s;
                s.PrimaryPrompt = ">>> ";
                s.UseInterpreterPrompts = false;
            }

            return ReplWindowProxy.Prepare(s, useIPython: useIPython);
        }

        [TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public virtual void ExecuteInReplSysArgv() {
            using (var interactive = Prepare())
            using (new DefaultInterpreterSetter(interactive.Analyzer.InterpreterFactory)) {
                var project = interactive.App.OpenProject(@"TestData\SysArgvRepl.sln");

                interactive.App.ExecuteCommand("Python.ExecuteInInteractive");
                interactive.WaitForTextEnd("Program.py']", ">");
            }
        }

        [TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public virtual void ExecuteInReplSysArgvScriptArgs() {
            using (var interactive = Prepare())
            using (new DefaultInterpreterSetter(interactive.Analyzer.InterpreterFactory)) {
                var project = interactive.App.OpenProject(@"TestData\SysArgvScriptArgsRepl.sln");

                interactive.App.ExecuteCommand("Python.ExecuteInInteractive");
                interactive.WaitForTextEnd(@"Program.py', '-source', 'C:\\Projects\\BuildSuite', '-destination', 'C:\\Projects\\TestOut', '-pattern', '*.txt', '-recurse', 'true']", ">");
            }
        }

        [TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public virtual void ExecuteInReplUnicodeFilename() {
            using (var interactive = Prepare())
            using (new DefaultInterpreterSetter(interactive.Analyzer.InterpreterFactory)) {
                var project = interactive.App.OpenProject(@"TestData\UnicodePathä.sln");

                interactive.App.ExecuteCommand("Python.ExecuteInInteractive");
                interactive.WaitForTextEnd("hello world from unicode path", ">");
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
                interactive.WaitForTextEnd(string.Format(importErrorFormat, "module1"), ">");

                interactive.SubmitCode("import module2");
                interactive.WaitForTextEnd(string.Format(importErrorFormat, "module2"), ">");

                interactive.SubmitCode("os.chdir('A')");
                interactive.WaitForTextEnd(">os.chdir('A')", ">");

                interactive.SubmitCode("import module1");
                interactive.WaitForTextEnd(">import module1", ">");

                interactive.SubmitCode("import module2");
                interactive.WaitForTextEnd(string.Format(importErrorFormat, "module2"), ">");

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
                interactive.WaitForText(">quit()", "The Python REPL process has exited", ">");
                interactive.Reset();

                interactive.WaitForText(">quit()", "The Python REPL process has exited", "Resetting execution engine", ">");
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

        [TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public virtual void AttachReplTest() {
            using (var interactive = Prepare(enableAttach: true)) {
                var app = interactive.App;
                var project = app.OpenProject(@"TestData\DebuggerProject.sln");

                Assert.IsNotNull(PythonToolsPackage.GetStartupProject(app.ServiceProvider), "Startup project was not set");
                Assert.IsTrue(interactive.Settings.EnableAttach, "EnableAttach was not set");

                using (var dis = new DefaultInterpreterSetter(interactive.Analyzer.InterpreterFactory)) {
                    Assert.AreEqual(dis.CurrentDefault.Configuration.Description, project.GetPythonProject().GetInterpreterFactory().Configuration.Description);

                    interactive.Reset();
                    interactive.ClearScreen();

                    const string attachCmd = "$attach";
                    interactive.SubmitCode(attachCmd);
                    app.OnDispose(() => {
                        if (app.Dte.Debugger.CurrentMode != EnvDTE.dbgDebugMode.dbgDesignMode) {
                            app.DismissAllDialogs();
                            try {
                                app.ExecuteCommand("Debug.StopDebugging");
                            } catch (COMException) {
                            }
                            WaitForMode(app.Dte.Debugger, EnvDTE.dbgDebugMode.dbgDesignMode);
                        }
                    });

                    app.Dte.Debugger.Breakpoints.Add(File: "BreakpointTest.py", Line: 1);
                    interactive.WaitForText(">" + attachCmd, ">");

                    WaitForMode(app.Dte.Debugger, EnvDTE.dbgDebugMode.dbgRunMode);

                    interactive.Show();

                    const string import = "import BreakpointTest";
                    interactive.SubmitCode(import, wait: false);
                    interactive.WaitForText(">" + attachCmd, ">" + import, "");

                    WaitForMode(app.Dte.Debugger, EnvDTE.dbgDebugMode.dbgBreakMode);

                    Assert.AreEqual(EnvDTE.dbgEventReason.dbgEventReasonBreakpoint, app.Dte.Debugger.LastBreakReason);
                    Assert.AreEqual(app.Dte.Debugger.BreakpointLastHit.FileLine, 1);

                    app.ExecuteCommand("Debug.DetachAll");

                    WaitForMode(app.Dte.Debugger, EnvDTE.dbgDebugMode.dbgDesignMode);

                    interactive.WaitForText(">" + attachCmd, ">" + import, "hello", ">");
                }
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

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

using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.PythonTools;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;

namespace ReplWindowUITests {
    /// <summary>
    /// These tests must be run for all supported versions of Python that may
    /// use the REPL.
    /// </summary>
    [TestClass, Ignore]
    public abstract class ReplWindowPythonSmokeTests {
        static ReplWindowPythonSmokeTests() {
            PythonTestData.Deploy();
        }

        internal abstract ReplWindowProxySettings Settings {
            get;
        }

        internal virtual ReplWindowProxy Prepare(
            bool enableAttach = false,
            bool useIPython = false
        ) {
            var s = Settings;
            if (s.Version == null) {
                Assert.Inconclusive("Interpreter missing for " + GetType().Name);
            }

            if (enableAttach != s.EnableAttach) {
                s = s.Clone();
                s.EnableAttach = enableAttach;
            }

            return ReplWindowProxy.Prepare(s, useIPython: useIPython);
        }

        [TestMethod, Priority(0)]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public virtual void ExecuteInReplSysArgv() {
            using (var interactive = Prepare())
            using (new DefaultInterpreterSetter(interactive.TextView.GetAnalyzer().InterpreterFactory)) {
                var project = interactive.App.OpenProject(@"TestData\SysArgvRepl.sln");

                interactive.App.ExecuteCommand("Python.ExecuteInInteractive");
                interactive.WaitForTextEnd("Program.py']", ">");
            }
        }

        [TestMethod, Priority(0)]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public virtual void ExecuteInReplSysArgvScriptArgs() {
            using (var interactive = Prepare())
            using (new DefaultInterpreterSetter(interactive.TextView.GetAnalyzer().InterpreterFactory)) {
                var project = interactive.App.OpenProject(@"TestData\SysArgvScriptArgsRepl.sln");

                interactive.App.ExecuteCommand("Python.ExecuteInInteractive");
                interactive.WaitForTextEnd(@"Program.py', '-source', 'C:\\Projects\\BuildSuite', '-destination', 'C:\\Projects\\TestOut', '-pattern', '*.txt', '-recurse', 'true']", ">");
            }
        }

        [TestMethod, Priority(0)]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public virtual void ExecuteInReplUnicodeFilename() {
            using (var interactive = Prepare())
            using (new DefaultInterpreterSetter(interactive.TextView.GetAnalyzer().InterpreterFactory)) {
                var project = interactive.App.OpenProject(@"TestData\UnicodePathä.sln");

                interactive.App.ExecuteCommand("Python.ExecuteInInteractive");
                interactive.WaitForTextEnd("hello world from unicode path", ">");
            }
        }

        [TestMethod, Priority(0)]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public virtual void CwdImport() {
            using (var interactive = Prepare()) {
                interactive.SubmitCode("import sys\nsys.path");
                interactive.SubmitCode("import os\nos.chdir(r'" + TestData.GetPath("TestData\\ReplCwd") + "')");

                interactive.SubmitCode("import module1");
                interactive.WaitForTextEnd(string.Format(interactive.Settings.ImportError, "module1"), ">");

                interactive.SubmitCode("import module2");
                interactive.WaitForTextEnd(string.Format(interactive.Settings.ImportError, "module2"), ">");

                interactive.SubmitCode("os.chdir('A')");
                interactive.WaitForTextEnd(">os.chdir('A')", ">");

                interactive.SubmitCode("import module1");
                interactive.WaitForTextEnd(">import module1", ">");

                interactive.SubmitCode("import module2");
                interactive.WaitForTextEnd(string.Format(interactive.Settings.ImportError, "module2"), ">");

                interactive.SubmitCode("os.chdir('..\\B')");
                interactive.WaitForTextEnd(">os.chdir('..\\B')", ">");

                interactive.SubmitCode("import module2");
                interactive.WaitForTextEnd(">import module2", ">");
            }
        }

        [TestMethod, Priority(0)]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
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

        [TestMethod, Priority(0)]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public virtual void AttachReplTest() {
            using (var interactive = Prepare(enableAttach: true)) {
                var app = interactive.App;
                var project = app.OpenProject(@"TestData\DebuggerProject.sln");
                Assert.IsNotNull(PythonToolsPackage.GetStartupProject(), "Startup project was not set");
                Assert.IsTrue(interactive.Settings.EnableAttach, "EnableAttach was not set");

                using (var dis = new DefaultInterpreterSetter(interactive.TextView.GetAnalyzer().InterpreterFactory)) {
                    Assert.AreEqual(dis.CurrentDefault.Description, project.GetPythonProject().GetInterpreterFactory().Description);

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

                    ((IVsWindowFrame)((ToolWindowPane)interactive.Window).Frame).Show();

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

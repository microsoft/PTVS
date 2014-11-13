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

using System.Threading;
using Microsoft.PythonTools;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.UI;
using Keyboard = TestUtilities.UI.Keyboard;

namespace ReplWindowUITests {
    /// <summary>
    /// These tests are run for important versions of Python that use or support
    /// IPython mode in the REPL.
    /// </summary>
    [TestClass, Ignore]
    public abstract class ReplWindowPythonIPythonTests : ReplWindowPythonTests {
        internal virtual ReplWindowProxy PrepareIPython() {
            return Prepare(useIPython: true);
        }

        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void IPythonMode() {
            using (var interactive = PrepareIPython()) {
                interactive.SubmitCode("x = 42\n?x");

                interactive.WaitForText(
                    ">x = 42",
                    ">?x",
                    interactive.Settings.IPythonIntDocumentation,
                    ">"
                );
            }
        }

        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void IPythonCtrlBreakAborts() {
            using (var interactive = PrepareIPython()) {
                var code = "while True: pass";
                interactive.Type(code + "\n", waitForLastLine: false);
                interactive.WaitForText(">" + code, ".", "");

                Thread.Sleep(2000);

                interactive.CancelExecution();

                // we can potentially get different output depending on where the Ctrl-C gets caught.
                try {
                    interactive.WaitForTextStart(">while True: pass", ".", "KeyboardInterrupt caught in kernel");
                } catch {
                    interactive.WaitForTextStart(">while True: pass", ".",
                        "---------------------------------------------------------------------------",
                        "KeyboardInterrupt                         Traceback (most recent call last)");
                }
            }
        }

        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void IPythonSimpleCompletion() {
            using (var interactive = PrepareIPython()) {
                interactive.AddNewLineAtEndOfFullyTypedWord = false;

                interactive.SubmitCode("x = 42");
                interactive.WaitForText(">x = 42", ">");
                interactive.ClearScreen();

                Keyboard.Type("x.");

                using (var sh = interactive.WaitForSession<ICompletionSession>()) {
                    // commit entry
                    sh.Commit();
                    sh.WaitForSessionDismissed();
                }

                interactive.WaitForText(">x." + interactive.Settings.IntFirstMember);

                // clear input at repl
                interactive.ClearInput();

                // try it again, and dismiss the session
                Keyboard.Type("x.");
                using (interactive.WaitForSession<ICompletionSession>()) { }
            }
        }

        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void IPythonSimpleSignatureHelp() {
            using (var interactive = PrepareIPython()) {
                Assert.IsNotNull(interactive);

                interactive.SubmitCode("def f(): pass");
                interactive.WaitForText(">def f(): pass", ">");

                Keyboard.Type("f(");

                using (var sh = interactive.WaitForSession<ISignatureHelpSession>()) {
                    Assert.AreEqual("<no docstring>", sh.Session.SelectedSignature.Documentation);
                }
            }
        }

        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void IPythonInlineGraph() {
            using (var interactive = PrepareIPython()) {
                interactive.SubmitCode(@"from pylab import *
x = linspace(0, 4*pi)
plot(x, x)");
                interactive.WaitForTextStart(
                    ">from pylab import *",
                    ">x = linspace(0, 4*pi)",
                    ">plot(x, x)",
                    "Out["
                );

                Thread.Sleep(2000);

                var tags = WaitForTags(interactive);
                Assert.AreEqual(1, tags.Length);
            }
        }

        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void IPythonStartInInteractive() {
            using (var interactive = PrepareIPython())
            using (new DefaultInterpreterSetter(interactive.Window.TextView.GetAnalyzer().InterpreterFactory)) {
                var project = interactive.App.OpenProject(@"TestData\InteractiveFile.sln");

                interactive.App.ExecuteCommand("Python.ExecuteInInteractive");
                interactive.WaitForTextEnd("Program.pyabcdef", ">");
            }
        }

        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void ExecuteInIPythonReplSysArgv() {
            using (var interactive = PrepareIPython())
            using (new DefaultInterpreterSetter(interactive.TextView.GetAnalyzer().InterpreterFactory)) {
                var project = interactive.App.OpenProject(@"TestData\SysArgvRepl.sln");

                interactive.App.ExecuteCommand("Python.ExecuteInInteractive");
                interactive.WaitForTextEnd("Program.py']", ">");
            }
        }

        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void ExecuteInIPythonReplSysArgvScriptArgs() {
            using (var interactive = PrepareIPython())
            using (new DefaultInterpreterSetter(interactive.TextView.GetAnalyzer().InterpreterFactory)) {
                var project = interactive.App.OpenProject(@"TestData\SysArgvScriptArgsRepl.sln");

                interactive.App.ExecuteCommand("Python.ExecuteInInteractive");
                interactive.WaitForTextEnd(@"Program.py', '-source', 'C:\\Projects\\BuildSuite', '-destination', 'C:\\Projects\\TestOut', '-pattern', '*.txt', '-recurse', 'true']", ">");
            }
        }
    }
}

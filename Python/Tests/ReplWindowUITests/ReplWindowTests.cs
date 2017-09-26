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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using TestUtilities;
using TestUtilities.UI;
using TestUtilities.UI.Python;
using Keyboard = TestUtilities.UI.Keyboard;

namespace ReplWindowUITests {
    //[TestClass, Ignore]
    public abstract class ReplWindowTests {
        internal abstract ReplWindowProxySettings Settings { get; }

        private ReplWindowProxy Prepare(PythonVisualStudioApp app, bool addNewLineAtEndOfFullyTypedWord = false) {
            var s = Settings;
            if (addNewLineAtEndOfFullyTypedWord != s.AddNewLineAtEndOfFullyTypedWord) {
                s = s.Clone();
                s.AddNewLineAtEndOfFullyTypedWord = addNewLineAtEndOfFullyTypedWord;
            }

            return ReplWindowProxy.Prepare(app, s);
        }

        #region Miscellaneous tests

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void ClearInputHelper(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                interactive.Type("1 + ", commitLastLine: false);
                interactive.WaitForText(">1 + ");
                interactive.ClearInput();

                interactive.Type("2");
                interactive.WaitForText(">2", "2", ">");
            }
        }

        #endregion

        #region Signature Help tests

        /// <summary>
        /// "def f(): pass" + 2 ENTERS
        /// f( should bring signature help up
        /// 
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void SimpleSignatureHelp(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                const string code = "def f(): pass";
                interactive.SubmitCode(code);
                interactive.WaitForText(">" + code, ">");
                WaitForAnalysis(interactive);

                Keyboard.Type("f(");

                using (var sh = interactive.WaitForSession<ISignatureHelpSession>()) {
                    Assert.AreEqual("f()", sh.Session.SelectedSignature.Content);
                    sh.Dismiss();
                    sh.WaitForSessionDismissed();
                }
            }
        }

        /// <summary>
        /// "def f(a, b=1, c="d"): pass" + 2 ENTERS
        /// f( should bring signature help up and show default values and types
        /// 
        /// </summary>
        [Ignore] // https://github.com/Microsoft/PTVS/issues/2689
        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void SignatureHelpDefaultValue(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                const string code = "def f(a, b=1, c=\"d\"): pass";
                interactive.SubmitCode(code + "\n");
                interactive.WaitForText(">" + code, ">");
                WaitForAnalysis(interactive);

                Keyboard.Type("f(");

                using (var sh = interactive.WaitForSession<ISignatureHelpSession>()) {
                    Assert.AreEqual("f(a, b: int = 1, c: str = 'd')", sh.Session.SelectedSignature.Content);
                    sh.Dismiss();
                    sh.WaitForSessionDismissed();
                }
            }
        }

        #endregion

        #region Completion tests

        /// <summary>
        /// "x = 42"
        /// "x." should bring up intellisense completion
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void SimpleCompletion(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                const string code = "x = 42";
                interactive.SubmitCode(code);
                interactive.WaitForText(">" + code, ">");

                Keyboard.Type("x.");
                interactive.WaitForText(">" + code, ">x.");

                using (var sh = interactive.WaitForSession<ICompletionSession>()) {
                    Assert.IsNotNull(sh.Session.SelectedCompletionSet);

                    // commit entry
                    sh.Commit();
                    sh.WaitForSessionDismissed();
                    interactive.WaitForText(
                        ">" + code,
                        ">x." + ((ReplWindowProxySettings)interactive.Settings).IntFirstMember
                    );
                }

                // clear input at repl
                interactive.ClearInput();

                // try it again, and dismiss the session
                Keyboard.Type("x.");
                using (interactive.WaitForSession<ICompletionSession>()) { }
            }
        }

        /// <summary>
        /// "x = 42"
        /// "x " should not bring up any completions.
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void SimpleCompletionSpaceNoCompletion(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                const string code = "x = 42";
                interactive.SubmitCode(code);
                interactive.WaitForText(">" + code, ">");

                // x<space> should not bring up a completion session
                Keyboard.Type("x ");

                interactive.WaitForText(">" + code, ">x ");

                interactive.AssertNoSession();
            }
        }

        /// <summary>
        /// x = 42; x.car[enter] – should type "car" not complete to "conjugate"
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void CompletionWrongText(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                const string code = "x = 42";
                interactive.SubmitCode(code);
                interactive.WaitForText(">" + code, ">");

                Keyboard.Type("x.");

                using (var sh = interactive.WaitForSession<ICompletionSession>()) {
                    Keyboard.Type("car\r");
                    sh.WaitForSessionDismissed();
                }
                interactive.WaitForText(
                    ">" + code,
                    ">x.car",
                    "Traceback (most recent call last):",
                    "  File \"<" + ((ReplWindowProxySettings)interactive.Settings).SourceFileName + ">\", line 1, in <module>",
                    "AttributeError: 'int' object has no attribute 'car'", ">"
                );
            }
        }

        /// <summary>
        /// x = 42; x.conjugate[enter] – should respect enter completes option,
        /// and should respect enter at end of word completes option.  When it
        /// does execute the text the output should be on the next line.
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void CompletionFullTextWithoutNewLine(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app, addNewLineAtEndOfFullyTypedWord: false)) {
                const string code = "x = 42";
                interactive.SubmitCode(code);
                interactive.WaitForText(">" + code, ">");
                WaitForAnalysis(interactive);

                Keyboard.Type("x.");
                using (var sh = interactive.WaitForSession<ICompletionSession>()) {
                    Keyboard.Type("real\r");
                    sh.WaitForSessionDismissed();
                }
                interactive.WaitForText(">" + code, ">x.real");
            }
        }

        /// <summary>
        /// x = 42; x.conjugate[enter] – should respect enter completes option,
        /// and should respect enter at end of word completes option.  When it
        /// does execute the text the output should be on the next line.
        /// </summary>
        [Ignore] // https://github.com/Microsoft/PTVS/issues/2755
        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void CompletionFullTextWithNewLine(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app, addNewLineAtEndOfFullyTypedWord: true)) {

                const string code = "x = 42";
                interactive.SubmitCode(code);
                interactive.WaitForText(">" + code, ">");
                WaitForAnalysis(interactive);

                Keyboard.Type("x.");
                using (var sh = interactive.WaitForSession<ICompletionSession>()) {
                    Keyboard.Type("real\r");
                    sh.WaitForSessionDismissed();
                }

                interactive.WaitForText(">" + code, ">x.real", "42", ">");
            }
        }

        /// <summary>
        /// With AutoListIdentifiers on, all [a-zA-Z_] should bring up
        /// completions
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void AutoListIdentifierCompletions(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                // the App instance preserves this property for us already
                ((PythonVisualStudioApp)interactive.App).Options.Intellisense.AutoListIdentifiers = true;
                Keyboard.Type("x = ");

                // 'x' should bring up a completion session
                foreach (var c in "abcdefghijklmnopqrstuvwxyz_ABCDEFGHIJKLMNOPQRSTUVWXYZ") {
                    Keyboard.Type(c.ToString());

                    using (var sh = interactive.WaitForSession<ICompletionSession>()) {
                        sh.Dismiss();
                    }

                    Keyboard.Backspace();
                }

                // 'x' should not bring up a completion session
                // Don't check too many items, since asserting that no session
                // starts is slow.
                foreach (var c in "1([{") {
                    Keyboard.Type(c.ToString());

                    interactive.AssertNoSession();

                    Keyboard.Backspace();
                }
            }
        }


        #endregion

        #region Input/output redirection tests

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void TestStdOutRedirected(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                // Spaces after the module name prevent autocomplete from changing them.
                // In particular, 'subprocess' does not appear in the default database,
                // but '_subprocess' does.
                const string code = "import subprocess , sys ";
                const string code2 = "x = subprocess.Popen([sys.executable, '-c', 'print(42)'], stdout=sys.stdout).wait()";

                interactive.SubmitCode(code + "\n" + code2);
                interactive.WaitForText(
                    ">" + code,
                    ">" + code2,
                    ((ReplWindowProxySettings)interactive.Settings).Print42Output,
                    ">"
                );
            }
        }

        /// <summary>
        /// Calling input while executing user code.  This should let the user start typing.
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void TestRawInput(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                EnsureInputFunction(interactive);

                interactive.SubmitCode("x = input()");
                interactive.WaitForText(">x = input()", "<");

                Keyboard.Type("hello\r");
                interactive.WaitForText(">x = input()", "<hello", "", ">");

                interactive.SubmitCode("print(x)");
                interactive.WaitForText(">x = input()", "<hello", "", ">print(x)", "hello", ">");
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void OnlyTypeInRawInput(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                EnsureInputFunction(interactive);

                interactive.SubmitCode("input()");
                interactive.WaitForText(">input()", "<");

                Keyboard.Type("hel");
                interactive.WaitForText(">input()", "<hel");

                // attempt to type in the previous submission should not do anything
                Keyboard.Type(Key.Up);
                Keyboard.Type("lo");
                interactive.WaitForText(">input()", "<hel");

                Keyboard.Type(Key.Down);
                Keyboard.Type("lo");
                interactive.WaitForText(">input()", "<hello");
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void DeleteCharactersInRawInput(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                EnsureInputFunction(interactive);

                interactive.Type("input()");
                interactive.WaitForText(">input()", "<");

                Keyboard.Type("hello");
                interactive.WaitForText(">input()", "<hello");

                interactive.Backspace(3);
                interactive.WaitForText(">input()", "<he");
            }
        }

        /// <summary>
        /// Calling ReadInput while no code is running - this should remove the
        /// prompt and let the user type input
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void TestIndirectInput(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                var t = Task.Run(() => interactive.Window.ReadStandardInput());

                // prompt should disappear
                interactive.WaitForText("<");

                Keyboard.Type("abc\r");
                interactive.WaitForText("<abc", "", ">");

                var text = t.Result?.ReadToEnd();
                Assert.AreEqual("abc\r\n", text);
            }
        }

        #endregion

        #region Keyboard tests

        /// <summary>
        /// Enter in a middle of a line should insert new line
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void EnterInMiddleOfLine(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                const string code = "def f(): #fob";
                // This is a keyboard test, so use Keyboard.Type directly
                Keyboard.Type(code);
                Keyboard.Type(Key.Left);
                Keyboard.Type(Key.Left);
                Keyboard.Type(Key.Left);
                Keyboard.Type(Key.Left);
                Keyboard.Type(Key.Enter);
                Keyboard.Type("pass");
                Keyboard.PressAndRelease(Key.Enter, Key.LeftCtrl);
                interactive.WaitForText(">def f(): ", ".    pass#fob", ">");
            }
        }

        /// <summary>
        /// LineBreak should insert a new line and not submit.
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void LineBreak(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                const string quotes = "\"\"\"";
                // This is a keyboard test, so use Keyboard.Type directly
                Keyboard.Type(quotes);
                Keyboard.Type(Key.Enter);
                Keyboard.PressAndRelease(Key.Enter, Key.LeftShift);
                Keyboard.Type(quotes);
                Keyboard.Type(Key.Enter);
                Keyboard.Type(Key.Enter);

                interactive.WaitForText(
                    ">" + quotes,
                    ".",
                    "." + quotes,
                    "'\\n\\n'",
                    ">",
                    ">"
                );
            }
        }

        /// <summary>
        /// Tests entering a single line of text, moving to the middle, and pressing enter.
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void LineBreakInMiddleOfLine(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                Keyboard.Type("def f(): print('hello')");
                interactive.WaitForText(">def f(): print('hello')");

                // move to left of print
                for (int i = 0; i < 14; i++) {
                    Keyboard.Type(Key.Left);
                }

                Keyboard.Type(Key.Enter);

                interactive.WaitForText(">def f(): ", ".    print('hello')");
            }
        }

        /// <summary>
        /// "x=42" left left ctrl-enter should commit assignment
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void CtrlEnterCommits(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                const string code = "x = 42";
                // This is a keyboard test, so use Keyboard.Type directly
                Keyboard.Type(code);
                Keyboard.Type(Key.Left);
                Keyboard.Type(Key.Left);
                Keyboard.PressAndRelease(Key.Enter, Key.LeftCtrl);
                interactive.WaitForText(">" + code, ">");
            }
        }

        /// <summary>
        /// Escape should clear both lines
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void EscapeClearsMultipleLines(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                const string code = "def f(): #fob";
                // This is a keyboard test, so use Keyboard.Type directly
                Keyboard.Type(code);
                Keyboard.Type(Key.Left);
                Keyboard.Type(Key.Left);
                Keyboard.Type(Key.Left);
                Keyboard.Type(Key.Left);
                Keyboard.Type(Key.Enter);
                Keyboard.Type(Key.Tab);
                Keyboard.Type("pass");
                Keyboard.Type(Key.Escape);
                interactive.WaitForText(">");
            }
        }

        /// <summary>
        /// Ctrl-Enter on previous input should paste input to end of buffer 
        /// (doing it again should paste again – appending onto previous input)
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void CtrlEnterOnPreviousInput(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                const string code = "def f(): pass";
                interactive.SubmitCode(code);
                interactive.WaitForText(">" + code, ">");

                // This is a keyboard test, so use Keyboard.Type directly
                Keyboard.Type(Key.Left);
                Keyboard.Type(Key.Up);
                Keyboard.Type(Key.Right);
                Keyboard.PressAndRelease(Key.Enter, Key.LeftCtrl);

                interactive.WaitForText(">" + code, ">" + code);

                Keyboard.PressAndRelease(Key.Escape);

                interactive.WaitForText(">" + code, ">");
            }
        }

        /// <summary>
        /// Type some text, hit Ctrl-Enter, should execute current line and not
        /// require a secondary prompt.
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void CtrlEnterForceCommit(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                const string code = "def f(): pass";
                Keyboard.Type(code);

                interactive.WaitForText(">" + code);

                Keyboard.PressAndRelease(Key.Enter, Key.LeftCtrl);

                interactive.WaitForText(">" + code, ">");
            }
        }

        /// <summary>
        /// Type a function definition, go to next line, type pass, navigate
        /// left, hit ctrl-enter, should immediately execute func def.
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void CtrlEnterMultiLineForceCommit(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                Keyboard.Type("def f():\rpass");

                interactive.WaitForText(">def f():", ".    pass");

                Keyboard.Type(Key.Left);
                Keyboard.Type(Key.Left);
                Keyboard.Type(Key.Left);
                Keyboard.PressAndRelease(Key.Enter, Key.LeftCtrl);

                interactive.WaitForText(">def f():", ".    pass", ">");
            }
        }

        /// <summary>
        /// Tests backspacing pass the prompt to the previous line
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void BackspacePrompt(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                interactive.Type("def f():\npass", commitLastLine: false);
                interactive.WaitForText(">def f():", ".    pass");

                interactive.Backspace(9);
                interactive.WaitForText(">def f():");

                interactive.Type("abc", commitLastLine: false);
                interactive.WaitForText(">def f():abc");

                interactive.Backspace(3);
                interactive.WaitForText(">def f():");

                interactive.Type("\npass", commitLastLine: false);
                interactive.WaitForText(">def f():", ".    pass");
            }
        }
        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void BackspaceSmartDedent(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                interactive.Type("   ", commitLastLine: false);
                interactive.WaitForText(">   ");

                // smart dedent shouldn't delete 3 spaces
                interactive.Backspace();
                interactive.WaitForText(">  ");

                interactive.Type("  ", commitLastLine: false);
                interactive.WaitForText(">    ");

                // spaces aren't in virtual space, we should delete only one
                interactive.Backspace();
                interactive.WaitForText(">   ");
            }
        }

        /// <summary>
        /// Tests pressing back space when to the left of the caret we have the
        /// secondary prompt.  The secondary prompt should be removed and the
        /// lines should be joined.
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void BackspaceSecondaryPrompt(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                interactive.Type("def f():\nx = 42\ny = 100", commitLastLine: false);

                interactive.WaitForText(">def f():", ".    x = 42", ".    y = 100");

                Keyboard.Type(Key.Home);
                Keyboard.Type(Key.Home);
                Keyboard.Type(Key.Up);
                Keyboard.Type(Key.Back);

                interactive.WaitForText(">def f():    x = 42", ".    y = 100");
            }
        }

        /// <summary>
        /// Tests deleting when the secondary prompt is highlighted as part of the selection
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void BackspaceSecondaryPromptSelected(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                interactive.Type("def f():\nprint('hi')", commitLastLine: false);
                interactive.WaitForText(">def f():", ".    print('hi')");

                Keyboard.Type(Key.Home);
                Keyboard.Type(Key.Home);
                Keyboard.Type(Key.Left);
                Keyboard.PressAndRelease(Key.End, Key.LeftShift);
                Keyboard.Type(Key.Back);

                interactive.WaitForText(">def f():", ".");
            }
        }

        /// <summary>
        /// Tests deleting when the secondary prompt is highlighted as part of the selection
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void DeleteSecondaryPromptSelected(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                interactive.Type("def f():\nprint('hi')", commitLastLine: false);
                interactive.WaitForText(">def f():", ".    print('hi')");

                Keyboard.Type(Key.Home);
                Keyboard.Type(Key.Home);
                Keyboard.Type(Key.Left);
                Keyboard.PressAndRelease(Key.End, Key.LeftShift);
                Keyboard.Type(Key.Delete);

                interactive.WaitForText(">def f():", ".");
            }
        }

        /// <summary>
        /// Tests typing when the secondary prompt is highlighted as part of the selection
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void EditTypeSecondaryPromptSelected(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                interactive.Type("def f():\nprint('hi')", commitLastLine: false);
                interactive.WaitForText(">def f():", ".    print('hi')");

                Keyboard.Type(Key.Home);
                Keyboard.Type(Key.Home);
                Keyboard.Type(Key.Left);
                Keyboard.PressAndRelease(Key.End, Key.LeftShift);
                Keyboard.Type("pass");

                interactive.WaitForText(">def f():", ".pass");
            }
        }

        /// <summary>
        /// Pressing delete with no text selected, it should delete the proceeding character.
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void TestDelNoTextSelected(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                interactive.Type("abc", commitLastLine: false);
                interactive.WaitForText(">abc");

                Keyboard.Type(Key.Home);
                Keyboard.Type(Key.Delete);

                interactive.WaitForText(">bc");
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void TestDelAtEndOfLine(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                interactive.Type("def f():\nprint('hello')", commitLastLine: false);
                interactive.WaitForText(">def f():", ".    print('hello')");

                // go to end of 1st line
                Keyboard.Type(Key.Home);
                Keyboard.Type(Key.Home);
                Keyboard.Type(Key.Up);
                Keyboard.Type(Key.End);

                // press delete
                interactive.App.ExecuteCommand("Edit.Delete");
                interactive.WaitForText(">def f():    print('hello')");
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void TestDelAtEndOfBuffer(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                interactive.Type("def f():\nprint('hello')", commitLastLine: false);
                interactive.WaitForText(">def f():", ".    print('hello')");

                interactive.App.ExecuteCommand("Edit.Delete");
                interactive.WaitForText(">def f():", ".    print('hello')");
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void TestDelInOutput(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                interactive.SubmitCode("print('hello')");
                interactive.WaitForText(">print('hello')", "hello", ">");

                Keyboard.Type(Key.Left);
                Keyboard.Type(Key.Up);

                interactive.App.ExecuteCommand("Edit.Delete");
                interactive.WaitForText(">print('hello')", "hello", ">");
            }
        }

        #endregion

        #region Cancel tests

        /// <summary>
        /// while True: pass / Right Click -> Break Execution (or Ctrl-Break) should break execution
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void CtrlBreakInterrupts(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                var code = "while True: pass";
                interactive.Type(code + "\n", waitForLastLine: false);
                interactive.WaitForText(">" + code, ".", "");

                interactive.CancelExecution();

                if (((ReplWindowProxySettings)interactive.Settings).KeyboardInterruptHasTracebackHeader) {
                    interactive.WaitForTextStart(">" + code, "Traceback (most recent call last):");
                }
                interactive.WaitForTextEnd("KeyboardInterrupt", ">");
            }
        }

        /// <summary>
        /// while True: pass / Right Click -> Break Execution (or Ctrl-Break)
        /// should break execution
        /// 
        /// This version runs for 1/2 second which kicks in the running UI.
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void CtrlBreakInterruptsLongRunning(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                var code = "while True: pass";
                interactive.Type(code + "\n", waitForLastLine: false);
                interactive.WaitForText(">" + code, ".", "");

                Thread.Sleep(500);

                interactive.CancelExecution();

                if (((ReplWindowProxySettings)interactive.Settings).KeyboardInterruptHasTracebackHeader) {
                    interactive.WaitForTextStart(">" + code, "Traceback (most recent call last):");
                }
                interactive.WaitForTextEnd("KeyboardInterrupt", ">");
            }
        }

        /// <summary>
        /// Ctrl-Break while running should result in a new prompt
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void CtrlBreakNotRunning(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                interactive.WaitForText(">");

                try {
                    interactive.App.ExecuteCommand("PythonInteractive.CancelExecution");
                    Assert.Fail("CancelExecution should not be available");
                } catch {
                }

                interactive.WaitForText(">");
            }
        }

        /// <summary>
        /// Enter "while True: pass", then hit up/down arrow, should move the caret in the edit buffer
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void CursorWhileCodeIsRunning(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                var code = "while True: pass";
                interactive.Type(code + "\n", waitForLastLine: false);
                interactive.WaitForText(">" + code, ".", "");

                interactive.App.ExecuteCommand("Edit.LineUp");
                interactive.App.ExecuteCommand("Edit.LineUp");
                interactive.App.ExecuteCommand("Edit.LineEnd");
                interactive.App.ExecuteCommand("Edit.LineStartExtend");
                interactive.App.ExecuteCommand("Edit.Copy");

                interactive.CancelExecution();

                if (((ReplWindowProxySettings)interactive.Settings).KeyboardInterruptHasTracebackHeader) {
                    interactive.WaitForTextStart(">" + code, "Traceback (most recent call last):");
                } else {
                    interactive.WaitForTextStart(">" + code);
                }
                interactive.WaitForTextEnd("KeyboardInterrupt", ">");

                interactive.ClearScreen();
                interactive.WaitForText(">");

                interactive.App.ExecuteCommand("Edit.Paste");

                interactive.WaitForText(">" + code);
            }
        }

        #endregion

        #region History tests

        [Ignore] // https://github.com/Microsoft/PTVS/issues/2757
        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void HistoryUpdateDef(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                interactive.Type("def f():\nprint('hi')\n");
                interactive.WaitForText(">def f():", ".    print('hi')", ".", ">");

                interactive.PreviousHistoryItem();
                // delete i')
                interactive.Backspace(3);

                interactive.Type("ello')\n");

                interactive.WaitForText(
                    ">def f():", ".    print('hi')", ".",
                    ">def f():", ".    print('hello')", ".",
                    ">"
                );

                interactive.PreviousHistoryItem();
                interactive.WaitForText(
                    ">def f():", ".    print('hi')", ".",
                    ">def f():", ".    print('hello')", ".",
                    ">def f():", ".    print('hello')"
                );

                interactive.PreviousHistoryItem();
                interactive.WaitForText(
                    ">def f():", ".    print('hi')", ".",
                    ">def f():", ".    print('hello')", ".",
                    ">def f():", ".    print('hi')"
                );
            }
        }

        [Ignore] // https://github.com/Microsoft/PTVS/issues/2757
        //[TestMethod, Priority(1)]
        //[TestCategory("Interactive")]
        //[TestCategory("Installed")]
        public virtual void HistoryAppendDef(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                Keyboard.Type("def f():\rprint('hi')\r\r");

                interactive.WaitForText(
                    ">def f():",
                    ".    print('hi')",
                    ".",
                    ">"
                );

                interactive.PreviousHistoryItem();
                Keyboard.Type("\r");
                Keyboard.Type("print('hello')\r\r");

                interactive.WaitForText(
                    ">def f():",
                    ".    print('hi')",
                    ".",
                    ">def f():",
                    ".    print('hi')",
                    ".    print('hello')",
                    ".",
                    ">"
                );
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void HistoryBackForward(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                const string code1 = "x = 23";
                const string code2 = "y = 5";
                interactive.SubmitCode(code1);
                interactive.WaitForText(">" + code1, ">");

                interactive.SubmitCode(code2);
                interactive.WaitForText(">" + code1, ">" + code2, ">");

                interactive.PreviousHistoryItem();
                interactive.WaitForText(">" + code1, ">" + code2, ">" + code2);

                interactive.PreviousHistoryItem();
                interactive.WaitForText(">" + code1, ">" + code2, ">" + code1);

                interactive.NextHistoryItem();
                interactive.WaitForText(">" + code1, ">" + code2, ">" + code2);
            }
        }

        /// <summary>
        /// Test that maximum length of history is enforced and stores correct items.
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void HistoryMaximumLength(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                const int historyMax = 50;

                var expected = new List<string>();
                for (int i = 0; i < historyMax + 1; i++) {
                    string cmd = "x = " + i;
                    expected.Add(">" + cmd);
                    interactive.Type(cmd);

                    interactive.WaitForText(expected.Concat(new[] { ">" }));
                }

                // add an extra item for the current input which we'll update as
                // we go through the history
                expected.Add(">");
                for (int i = 0; i < historyMax; i++) {
                    interactive.PreviousHistoryItem();

                    expected[expected.Count - 1] = expected[expected.Count - i - 2];
                    interactive.WaitForText(expected);
                }
                // end of history, one more up shouldn't do anything
                interactive.PreviousHistoryItem();
                interactive.WaitForText(expected);
            }
        }

        /// <summary>
        /// Test that we remember a partially typed input when we move to the history.
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void HistoryUncommittedInput1(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                const string code1 = "x = 42", code2 = "y = 100";
                interactive.Type(code1);
                interactive.WaitForText(">" + code1, ">");

                // type, don't commit
                interactive.Type(code2, commitLastLine: false);
                interactive.WaitForText(">" + code1, ">" + code2);

                // move away from the input
                interactive.PreviousHistoryItem();
                interactive.WaitForText(">" + code1, ">" + code1);

                // move back to the input
                interactive.NextHistoryItem();
                interactive.WaitForText(">" + code1, ">" + code2);

                interactive.ClearInput();
                interactive.WaitForText(">" + code1, ">");
            }
        }

        /// <summary>
        /// Test that we don't restore on submit an uncomitted input saved for history.
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void HistoryUncommittedInput2(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                interactive.SubmitCode("1");
                interactive.WaitForText(">1", "1", ">");

                interactive.Type("blah", commitLastLine: false);
                interactive.WaitForText(">1", "1", ">blah");

                interactive.PreviousHistoryItem();
                interactive.WaitForText(">1", "1", ">1");

                interactive.SubmitCurrentText();
                interactive.WaitForText(">1", "1", ">1", "1", ">");
            }
        }

        /// <summary>
        /// Define function "def f():\r\n    print 'hi'", scroll back up to
        /// history, add print "hello" to 2nd line, enter, scroll back through
        /// both function definitions
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void HistorySearch(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                const string code1 = ">x = 42";
                const string code2 = ">x = 10042";
                const string code3 = ">x = 300";
                interactive.SubmitCode(code1.Substring(1));
                interactive.WaitForText(code1, ">");

                interactive.SubmitCode(code2.Substring(1));
                interactive.WaitForText(code1, code2, ">");

                interactive.SubmitCode(code3.Substring(1));
                interactive.WaitForText(code1, code2, code3, ">");

                interactive.Type("42", commitLastLine: false);
                interactive.WaitForText(code1, code2, code3, ">42");

                interactive.PreviousHistoryItem(search: true);
                interactive.WaitForText(code1, code2, code3, code2);

                interactive.PreviousHistoryItem(search: true);
                interactive.WaitForText(code1, code2, code3, code1);

                interactive.PreviousHistoryItem(search: true);
                interactive.WaitForText(code1, code2, code3, code1);

                interactive.NextHistoryItem(search: true);
                interactive.WaitForText(code1, code2, code3, code2);

                interactive.NextHistoryItem(search: true);
                interactive.WaitForText(code1, code2, code3, code2);
            }
        }

        #endregion

        #region Clipboard tests

        [Ignore] // https://github.com/Microsoft/PTVS/issues/2682
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void CommentPaste(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                const string comment = "# fob oar baz";
                interactive.ClearInput();
                interactive.Paste(comment);
                interactive.WaitForText(">" + comment);

                interactive.ClearInput();
                interactive.Paste(comment + "\r\n");
                interactive.WaitForText(">" + comment, ".");

                interactive.ClearInput();
                interactive.Paste(comment + "\r\ndef f():\r\n    pass");
                interactive.WaitForText(">" + comment, ".def f():", ".    pass");

                interactive.ClearInput();
                interactive.Paste(comment + "\r\n" + comment);
                interactive.WaitForText(">" + comment, "." + comment);

                interactive.ClearInput();
                interactive.Paste(comment + "\r\n" + comment + "\r\n");
                interactive.WaitForText(">" + comment, "." + comment, ".");
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void CsvPaste(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                interactive.Invoke(() => {
                    var dataObject = new DataObject();
                    dataObject.SetText("fob");
                    var stream = new MemoryStream(UTF8Encoding.Default.GetBytes("\"abc,\",\"fob\",\"\"\"fob,\"\"\",oar,baz\"x\"oar,\"baz,\"\"x,\"\"oar\",,    ,oar,\",\"\",\"\"\",baz\"x\"'oar,\"baz\"\"x\"\"',oar\",\"\"\"\",\"\"\",\"\"\",\",\",\\\r\n1,2,3,4,9,10,11,12,13,19,33,22,,,,,,\r\n4,5,6,5,2,3,4,3,1,20,44,33,,,,,,\r\n7,8,9,6,3,4,0,9,4,33,55,33,,,,,,"));
                    dataObject.SetData(DataFormats.CommaSeparatedValue, stream);
                    Clipboard.SetDataObject(dataObject, true);
                });

                interactive.App.ExecuteCommand("Edit.Paste");

                interactive.WaitForText(
                    ">[",
                    ".  ['abc,', '\"fob\"', '\"fob,\"', 'oar', 'baz\"x\"oar', 'baz,\"x,\"oar', None, None, 'oar', ',\",\"', 'baz\"x\"\\'oar', 'baz\"x\"\\',oar', '\"\"\"\"', '\",\"', ',', '\\\\'],",
                    ".  [1, 2, 3, 4, 9, 10, 11, 12, 13, 19, 33, 22, None, None, None, None, None, None],",
                    ".  [4, 5, 6, 5, 2, 3, 4, 3, 1, 20, 44, 33, None, None, None, None, None, None],",
                    ".  [7, 8, 9, 6, 3, 4, 0, 9, 4, 33, 55, 33, None, None, None, None, None, None],",
                    ".]",
                    "."
                );
            }
        }

        /// <summary>
        /// Tests cut when the secondary prompt is highlighted as part of the
        /// selection
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void EditCutIncludingPrompt(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                interactive.Type("def f():\nprint('hi')", commitLastLine: false);
                interactive.WaitForText(">def f():", ".    print('hi')");

                Keyboard.Type(Key.Home);
                Keyboard.Type(Key.Home);
                Keyboard.Type(Key.Left);
                interactive.App.ExecuteCommand("Edit.LineEndExtend");
                interactive.App.ExecuteCommand("Edit.Cut");

                interactive.WaitForText(">def f():", ".");

                interactive.App.ExecuteCommand("Edit.Paste");

                interactive.WaitForText(">def f():", ".     print('hi')");
            }
        }

        /// <summary>
        /// Tests pasting when the secondary prompt is highlighted as part of the selection
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void EditPasteSecondaryPromptSelected(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                interactive.Invoke((Action)(() => {
                    Clipboard.SetText("    pass", TextDataFormat.Text);
                }));

                interactive.Type("def f():\nprint('hi')", commitLastLine: false);
                interactive.WaitForText(">def f():", ".    print('hi')");

                Keyboard.Type(Key.Home);
                Keyboard.Type(Key.Home);
                Keyboard.Type(Key.Left);
                interactive.App.ExecuteCommand("Edit.LineEndExtend");
                interactive.App.ExecuteCommand("Edit.Paste");

                // >>> def f():
                // ...     print('hi')
                //    ^^^^^^^^^^^^^^^^
                // replacing selection including the prompt replaces the current line content:
                //
                // >>> def f():
                // ... pass
                interactive.WaitForText(">def f():", ".    pass");
            }
        }

        /// <summary>
        /// Tests pasting when the secondary prompt is highlighted as part of the selection
        /// 
        /// Same as EditPasteSecondaryPromptSelected, but the selection is reversed so that the
        /// caret is in the prompt.
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void EditPasteSecondaryPromptSelectedInPromptMargin(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                interactive.Type("def f():\nprint('hi')", commitLastLine: false);
                interactive.WaitForText(">def f():", ".    print('hi')");

                interactive.App.ExecuteCommand("Edit.LineStartExtend");
                interactive.App.ExecuteCommand("Edit.LineStartExtend");
                interactive.Paste("    pass");

                // >>> def f():
                // ...     print('hi')
                //    ^^^^^^^^^^^^^^^^
                // replacing selection including the prompt replaces the current line content:
                //
                // >>> def f():
                // ... pass
                interactive.WaitForText(">def f():", ".    pass");
            }
        }

        #endregion

        #region Command tests

        /// <summary>
        /// Tests entering an unknown repl commmand
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void ReplCommandUnknown(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                const string code = "$unknown";
                interactive.SubmitCode(code);
                interactive.WaitForText(">" + code, "Unknown command '$unknown', use '$help' for a list of commands.", ">");
            }
        }

        /// <summary>
        /// Tests entering an unknown repl commmand
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void ReplCommandComment(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                const string code = "$$ quox oar baz";
                interactive.SubmitCode(code);
                interactive.WaitForText(">" + code, ">");
            }
        }

        /// <summary>
        /// Tests using the $cls clear screen command
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void ClearScreenCommand(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                interactive.Type("$cls", commitLastLine: false);
                interactive.WaitForText(">$cls");

                interactive.SubmitCurrentText();
                interactive.WaitForText(">");
            }
        }

        /// <summary>
        /// Tests REPL command help
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void ReplCommandHelp(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                interactive.SubmitCode("$help");

                interactive.WaitForTextStart(
                    ">$help",
                    "Keyboard shortcuts:",
                    "  Enter                If the current submission appears to be complete, evaluate it.  Otherwise, insert a new line.",
                    "  Ctrl-Enter           Within the current submission, evaluate the current submission."
                );
            }
        }

        /// <summary>
        /// Tests REPL command $load, with a simple script.
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void CommandsLoadScript(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                const string content = "print('hello world')";
                string path;

                using (FileUtils.TemporaryTextFile(out path, content)) {
                    string command = "$load " + path;
                    interactive.SubmitCode(command);

                    interactive.WaitForTextStart(
                        ">" + command,
                        ">" + content,
                        "hello world"
                    );
                }
            }
        }

        /// <summary>
        /// Tests REPL command $load, with a simple script.
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void CommandsLoadScriptWithQuotes(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                const string content = "print('hello world')";
                string path;

                using (FileUtils.TemporaryTextFile(out path, content)) {
                    string command = "$load \"" + path + "\"";
                    interactive.SubmitCode(command);

                    interactive.WaitForTextStart(
                        ">" + command,
                        ">" + content,
                        "hello world"
                    );
                }
            }
        }

        /// <summary>
        /// Tests REPL command $load, with multiple statements including a class definition.
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void CommandsLoadScriptWithClass(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                // http://pytools.codeplex.com/workitem/632
                const string content = @"class C(object):
    pass

c = C()
";
                string path;

                using (FileUtils.TemporaryTextFile(out path, content)) {
                    string command = "$load " + path;
                    interactive.SubmitCode(command);

                    interactive.WaitForTextStart(
                        ">" + command,
                        ">class C(object):",
                        ".    pass",
                        ".",
                        ">c = C()",
                        ">"
                    );
                }
            }
        }

        /// <summary>
        /// Tests $load command with file that includes multiple submissions.
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void CommandsLoadScriptMultipleSubmissions(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                const string content = @"def fob():
    print('hello')
$wait 10
%% blah
$wait 20
fob()
";
                string path;

                using (FileUtils.TemporaryTextFile(out path, content)) {
                    string command = "$load " + path;
                    interactive.SubmitCode(command);

                    interactive.WaitForTextStart(
                        ">" + command,
                        ">def fob():",
                        ".    print('hello')",
                        ".",
                        ">$wait 10",
                        ">$wait 20",
                        ">fob()",
                        "hello"
                    );
                }
            }
        }

        /// <summary>
        /// Tests that ClearScreen doesn't cancel pending submissions queue.
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void CommandsLoadScriptMultipleSubmissionsWithClearScreen(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                const string content = @"def fob():
    print('hello')
%% blah
1+1
$cls
1+2
";
                string path;

                using (FileUtils.TemporaryTextFile(out path, content)) {
                    string command = "$load " + path;
                    interactive.SubmitCode(command);

                    interactive.WaitForText(">1+2", "3", ">");
                }
            }
        }

        #endregion

        #region Insert Code tests

        /// <summary>
        /// Inserts code to REPL while input is accepted. 
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void InsertCode(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                interactive.Window.InsertCode("1");
                interactive.Window.InsertCode("+");
                interactive.Window.InsertCode("2");

                interactive.WaitForText(">1+2");

                interactive.App.ExecuteCommand("Edit.CharLeft");
                interactive.App.ExecuteCommand("Edit.CharLeftExtend");

                interactive.WaitForText(">1+2");

                interactive.Window.InsertCode("*");

                interactive.WaitForText(">1*2");
            }
        }

        /// <summary>
        /// Inserts code to REPL while submission execution is in progress. 
        /// The inserted input should be appended to uncommitted input and show up when the execution is finished/aborted.
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public virtual void InsertCodeWhileRunning(PythonVisualStudioApp app) {
            using (var interactive = Prepare(app)) {
                var code = "while True: pass";
                interactive.Type(code + "\n", waitForLastLine: false);
                interactive.WaitForText(">" + code, ".", "");

                Thread.Sleep(50);
                interactive.Window.InsertCode("1");
                Thread.Sleep(50);
                interactive.Window.InsertCode("+");
                Thread.Sleep(50);
                interactive.Window.InsertCode("1");
                Thread.Sleep(50);

                interactive.CancelExecution();

                if (((ReplWindowProxySettings)interactive.Settings).KeyboardInterruptHasTracebackHeader) {
                    interactive.WaitForTextStart(">" + code, "Traceback (most recent call last):");
                } else {
                    interactive.WaitForTextStart(">" + code);
                }

                interactive.WaitForTextEnd("KeyboardInterrupt", ">1+1");

                interactive.SubmitCurrentText();
                interactive.WaitForTextEnd(">1+1", "2", ">");
            }
        }

        #endregion

        #region Helper methods

        static void EnsureInputFunction(ReplWindowProxy interactive) {
            var settings = (ReplWindowProxySettings)interactive.Settings;
            if (settings.RawInput != "input") {
                interactive.SubmitCode("input = " + settings.RawInput);
                interactive.ClearScreen();
            }
        }

        static void WaitForAnalysis(ReplWindowProxy interactive) {
            var stopAt = DateTime.Now.Add(TimeSpan.FromSeconds(60));
            interactive.GetAnalyzer().WaitForCompleteAnalysis(_ => DateTime.Now < stopAt);
            if (DateTime.Now >= stopAt) {
                Assert.Fail("Timeout waiting for complete analysis");
            }
            // Most of the time we're waiting to ensure that IntelliSense will
            // work, which normally requires a bit more time.
            Thread.Sleep(500);
        }

        #endregion
    }

    [TestClass]
    public class ReplWindowTestsDefaultPrompt : ReplWindowTests {
        internal override ReplWindowProxySettings Settings {
            get {
                return new ReplWindowProxySettings {
                    Version = PythonPaths.Python27 ?? PythonPaths.Python27_x64
                };
            }
        }
    }

    static class ReplWindowProxyExtensions {
        public static VsProjectAnalyzer GetAnalyzer(this ReplWindowProxy proxy) {
            return ((IPythonInteractiveIntellisense)proxy.Window.Evaluator).Analyzer;
        }
    }
}

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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using TestUtilities;
using Keyboard = TestUtilities.UI.Keyboard;

namespace ReplWindowUITests {
    [TestClass, Ignore]
    public abstract class ReplWindowTests {
        internal abstract ReplWindowProxySettings Settings { get; }

        private ReplWindowProxy Prepare() {
            return ReplWindowProxy.Prepare(Settings);
        }

        #region Miscellaneous tests

        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void ClearInputHelper() {
            using (var interactive = Prepare()) {
                interactive.Type("1 + ", commitLastLine: false);
                interactive.WaitForText(">1 + ");
                interactive.ClearInput();

                interactive.Type("2");
                interactive.WaitForText(">2", "2", ">");
            }
        }

        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void ReplWindowOptions() {
            using (var interactive = Prepare()) {
                interactive.SetOptionValue(ReplOptions.CommandPrefix, "%");
                Assert.AreEqual(interactive.GetOptionValue(ReplOptions.CommandPrefix), "%");
                interactive.SetOptionValue(ReplOptions.CommandPrefix, "$");
                Assert.AreEqual(interactive.GetOptionValue(ReplOptions.CommandPrefix), "$");

                Assert.AreEqual(interactive.GetOptionValue(ReplOptions.PrimaryPrompt), interactive.Settings.PrimaryPrompt);
                Assert.AreEqual(interactive.GetOptionValue(ReplOptions.SecondaryPrompt), interactive.Settings.SecondaryPrompt);

                Assert.AreEqual(interactive.GetOptionValue(ReplOptions.DisplayPromptInMargin), !interactive.Settings.InlinePrompts);
                Assert.AreEqual(interactive.GetOptionValue(ReplOptions.ShowOutput), true);

                Assert.AreEqual(interactive.GetOptionValue(ReplOptions.UseSmartUpDown), true);

                AssertUtil.Throws<InvalidOperationException>(
                    () => interactive.SetOptionValue(ReplOptions.PrimaryPrompt, 42)
                );
                AssertUtil.Throws<InvalidOperationException>(
                    () => interactive.SetOptionValue(ReplOptions.PrimaryPrompt, null)
                );
                AssertUtil.Throws<InvalidOperationException>(
                    () => interactive.SetOptionValue((ReplOptions)(-1), null)
                );
            }
        }

        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void Reset() {
            using (var interactive = Prepare()) {
                // TODO (bug): Reset doesn't clean up completely. 
                Assert.Inconclusive("TODO (bug): Reset doesn't clean up completely.");
                // This causes a space to appear in the buffer even after reseting the REPL.

                // const string print1 = "print 1,";
                // Keyboard.Type(print1);
                // Keyboard.Type(Key.Enter);
                // 
                // interactive.WaitForText(">" + print1, "1", ">");

                // CtrlBreakInStandardInput();
            }
        }

        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void PromptPositions() {
            using (var interactive = Prepare()) {
                // TODO (bug): this insert a space somwhere in the socket stream
                Assert.Inconclusive("TODO (bug): this insert a space somwhere in the socket stream");

                // const string print1 = "print 1,";
                // Keyboard.Type(print1);
                // Keyboard.Type(Key.Enter);
                // 
                // interactive.WaitForText(">" + print1, "1", ">");

                // The data received from the socket include " ", so it's not a REPL issue.
                // const string print2 = "print 2,";
                // Keyboard.Type(print2);
                // Keyboard.Type(Key.Enter);
                // 
                // interactive.WaitForText(">" + print1, "1", ">" + print2, "2", ">");
            }
        }

        #endregion

        #region Signature Help tests

        /// <summary>
        /// "def f(): pass" + 2 ENTERS
        /// f( should bring signature help up
        /// 
        /// </summary>
        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void SimpleSignatureHelp() {
            using (var interactive = Prepare()) {
                const string code = "def f(): pass";
                interactive.SubmitCode(code);
                interactive.WaitForText(">" + code, ">");
                interactive.WaitForAnalysis();

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
        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void SignatureHelpDefaultValue() {
            using (var interactive = Prepare()) {
                const string code = "def f(a, b=1, c=\"d\"): pass";
                interactive.SubmitCode(code);
                interactive.WaitForText(">" + code, ">");
                interactive.WaitForAnalysis();

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
        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void SimpleCompletion() {
            using (var interactive = Prepare()) {
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
                    interactive.WaitForText(">" + code, ">x." + interactive.Settings.IntFirstMember);
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
        /// "x " should not being up any completions.
        /// </summary>
        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void SimpleCompletionSpaceNoCompletion() {
            using (var interactive = Prepare()) {
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
        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void CompletionWrongText() {
            using (var interactive = Prepare()) {
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
                    "  File \"<" + interactive.Settings.SourceFileName + ">\", line 1, in <module>",
                    "AttributeError: 'int' object has no attribute 'car'", ">"
                );
            }
        }

        /// <summary>
        /// x = 42; x.conjugate[enter] – should respect enter completes option,
        /// and should respect enter at end of word completes option.  When it
        /// does execute the text the output should be on the next line.
        /// </summary>
        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void CompletionFullTextWithoutNewLine() {
            using (var interactive = Prepare()) {
                interactive.AddNewLineAtEndOfFullyTypedWord = false;

                const string code = "x = 42";
                interactive.SubmitCode(code);
                interactive.WaitForText(">" + code, ">");

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
        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void CompletionFullTextWithNewLine() {
            using (var interactive = Prepare()) {
                interactive.AddNewLineAtEndOfFullyTypedWord = true;

                const string code = "x = 42";
                interactive.SubmitCode(code);
                interactive.WaitForText(">" + code, ">");
                interactive.WaitForAnalysis();

                Keyboard.Type("x.");
                using (var sh = interactive.WaitForSession<ICompletionSession>()) {
                    Keyboard.Type("real\r");
                    sh.WaitForSessionDismissed();
                }

                interactive.WaitForText(">" + code, ">x.real", "42", ">");
            }
        }

        #endregion

        #region Input/output redirection tests

        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void TestStdOutRedirected() {
            using (var interactive = Prepare()) {
                interactive.RequirePrimaryPrompt();

                // Spaces after the module name prevent autocomplete from changing them.
                // In particular, 'subprocess' does not appear in the default database,
                // but '_subprocess' does.
                const string code = "import subprocess , sys ";
                const string code2 = "x = subprocess.Popen([sys.executable, '-c', 'print(42)'], stdout=sys.stdout).wait()";

                interactive.SubmitCode(code + "\n" + code2);
                interactive.WaitForText(
                    ">" + code,
                    ">" + code2,
                    interactive.Settings.Print42Output,
                    ">"
                );
            }
        }

        /// <summary>
        /// Calling input while executing user code.  This should let the user start typing.
        /// </summary>
        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void TestRawInput() {
            using (var interactive = Prepare()) {
                interactive.EnsureInputFunction();
                interactive.SetOptionValue(ReplOptions.StandardInputPrompt, "INPUT: ");

                interactive.SubmitCode("x = input()");
                interactive.WaitForText(">x = input()", "<");

                Keyboard.Type("hello\r");
                interactive.WaitForText(">x = input()", "<hello", ">");

                interactive.SubmitCode("print(x)");
                interactive.WaitForText(">x = input()", "<hello", ">print(x)", "hello", ">");
            }
        }

        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void OnlyTypeInRawInput() {
            using (var interactive = Prepare()) {
                interactive.EnsureInputFunction();
                interactive.SetOptionValue(ReplOptions.StandardInputPrompt, "INPUT: ");

                interactive.SubmitCode("input()");
                interactive.WaitForText(">input()", "<");

                Keyboard.Type("hel");
                interactive.WaitForText(">input()", "<hel");

                // attempt to type in the previous submission should move the
                // cursor back to the end of the stdin:
                Keyboard.Type(Key.Up);
                Keyboard.Type("lo");
                interactive.WaitForText(">input()", "<hello");
            }
        }

        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void DeleteCharactersInRawInput() {
            using (var interactive = Prepare()) {
                interactive.EnsureInputFunction();
                interactive.SetOptionValue(ReplOptions.StandardInputPrompt, "INPUT: ");

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
        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void TestIndirectInput() {
            using (var interactive = Prepare()) {
                interactive.SetOptionValue(ReplOptions.StandardInputPrompt, "INPUT: ");
                var t = Task.Run(() => interactive.Window.ReadStandardInput());

                // prompt should disappear
                interactive.WaitForText("<");

                Keyboard.Type("abc\r");
                interactive.WaitForText("<abc", ">");

                var text = t.Result;
                Assert.AreEqual("abc\r\n", text);
            }
        }

        #endregion

        #region Keyboard tests

        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void EnterAtBeginningOfLine() {
            using (var interactive = Prepare()) {
                // TODO (bug): complete statement detection
                Assert.Inconclusive("TODO (bug): complete statement detection");
                //                
                // 
                //    const string code = "\"\"\"";
                //    Keyboard.Type(code);
                //    Keyboard.Type(Key.Enter);
                //    Keyboard.Type(Key.Enter);
                //    interactive.WaitForText(">\"\"\"", ".", ".");

                //    Keyboard.Type("a");
                //    Keyboard.Type(Key.Left);
                //    Keyboard.Type(Key.Enter);
                //    interactive.WaitForText(">\"\"\"", ".", ".", ".a");
            }
        }

        /// <summary>
        /// Enter in a middle of a line should insert new line
        /// </summary>
        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void EnterInMiddleOfLine() {
            using (var interactive = Prepare()) {
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
        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void LineBreak() {
            using (var interactive = Prepare()) {
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
                    ".",
                    "'\\n\\n'",
                    ">"
                );
            }
        }

        /// <summary>
        /// Tests entering a single line of text, moving to the middle, and pressing enter.
        /// </summary>
        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void LineBreakInMiddleOfLine() {
            using (var interactive = Prepare()) {
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
        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void CtrlEnterCommits() {
            using (var interactive = Prepare()) {
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
        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void EscapeClearsMultipleLines() {
            using (var interactive = Prepare()) {
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
        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void CtrlEnterOnPreviousInput() {
            using (var interactive = Prepare()) {
                const string code = "def f(): pass";
                interactive.SubmitCode(code);
                interactive.WaitForText(">" + code, ">");

                // This is a keyboard test, so use Keyboard.Type directly
                Keyboard.Type(Key.Left);
                Keyboard.Type(Key.Up);
                Keyboard.Type(Key.Right);
                Keyboard.PressAndRelease(Key.Enter, Key.LeftCtrl);

                interactive.WaitForText(">" + code, ">" + code, ".");

                Keyboard.PressAndRelease(Key.Escape);

                interactive.WaitForText(">" + code, ">");
            }
        }

        /// <summary>
        /// Type some text, hit Ctrl-Enter, should execute current line and not
        /// require a secondary prompt.
        /// </summary>
        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void CtrlEnterForceCommit() {
            using (var interactive = Prepare()) {
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
        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void CtrlEnterMultiLineForceCommit() {
            using (var interactive = Prepare()) {
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
        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void BackspacePrompt() {
            using (var interactive = Prepare()) {
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
        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void BackspaceSmartDedent() {
            using (var interactive = Prepare()) {
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
        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void BackspaceSecondaryPrompt() {
            using (var interactive = Prepare()) {
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
        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void BackspaceSecondaryPromptSelected() {
            using (var interactive = Prepare()) {
                interactive.RequireSecondaryPrompt();

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
        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void DeleteSecondaryPromptSelected() {
            using (var interactive = Prepare()) {
                interactive.RequireSecondaryPrompt();

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
        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void EditTypeSecondaryPromptSelected() {
            using (var interactive = Prepare()) {
                interactive.RequireSecondaryPrompt();

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
        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void TestDelNoTextSelected() {
            using (var interactive = Prepare()) {
                interactive.Type("abc", commitLastLine: false);
                interactive.WaitForText(">abc");

                Keyboard.Type(Key.Home);
                Keyboard.Type(Key.Delete);

                interactive.WaitForText(">bc");
            }
        }

        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void TestDelAtEndOfLine() {
            using (var interactive = Prepare()) {
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

        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void TestDelAtEndOfBuffer() {
            using (var interactive = Prepare()) {
                interactive.Type("def f():\nprint('hello')", commitLastLine: false);
                interactive.WaitForText(">def f():", ".    print('hello')");

                interactive.App.ExecuteCommand("Edit.Delete");
                interactive.WaitForText(">def f():", ".    print('hello')");
            }
        }

        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void TestDelInOutput() {
            using (var interactive = Prepare()) {
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
        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void CtrlBreakInterrupts() {
            using (var interactive = Prepare()) {
                var code = "while True: pass";
                interactive.Type(code + "\n", waitForLastLine: false);
                interactive.WaitForText(">" + code, ".", "");

                interactive.CancelExecution();

                if (interactive.Settings.KeyboardInterruptHasTracebackHeader) {
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
        [TestMethod, Priority(1)]
        [HostType("VSTestHost")]
        public virtual void CtrlBreakInterruptsLongRunning() {
            using (var interactive = Prepare()) {
                var code = "while True: pass";
                interactive.Type(code + "\n", waitForLastLine: false);
                interactive.WaitForText(">" + code, ".", "");

                Thread.Sleep(500);

                interactive.CancelExecution();

                if (interactive.Settings.KeyboardInterruptHasTracebackHeader) {
                    interactive.WaitForTextStart(">" + code, "Traceback (most recent call last):");
                }
                interactive.WaitForTextEnd("KeyboardInterrupt", ">");
            }
        }

        /// <summary>
        /// Ctrl-Break while running should result in a new prompt
        /// </summary>
        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void CtrlBreakNotRunning() {
            using (var interactive = Prepare()) {
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
        /// Ctrl-Break while entering standard input should return an empty input and append a primary prompt.
        /// </summary>
        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void CtrlBreakInStandardInput() {
            using (var interactive = Prepare()) {
                interactive.EnsureInputFunction();
                interactive.SetOptionValue(ReplOptions.StandardInputPrompt, "INPUT: ");

                interactive.WaitForText(">");

                interactive.SubmitCode("input()");
                interactive.WaitForText(">input()", "<");

                Keyboard.Type("ignored");
                interactive.WaitForText(">input()", "<ignored");

                interactive.CancelExecution();

                interactive.WaitForText(
                    ">input()",
                    "<ignored",
                    "''",
                    ">"
                );

                interactive.PreviousHistoryItem();
                interactive.SubmitCurrentText();

                interactive.WaitForText(
                    ">input()",
                    "<ignored",
                    "''",
                    ">input()",
                    "<"
                );

                Keyboard.Type("ignored2");

                interactive.WaitForText(
                    ">input()",
                    "<ignored",
                    "''",
                    ">input()",
                    "<ignored2"
                );

                interactive.CancelExecution(1);

                interactive.WaitForText(
                    ">input()",
                    "<ignored",
                    "''",
                    ">input()",
                    "<ignored2",
                    "''",
                    ">"
                );
            }
        }

        /// <summary>
        /// Enter "while True: pass", then hit up/down arrow, should move the caret in the edit buffer
        /// </summary>
        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void CursorWhileCodeIsRunning() {
            using (var interactive = Prepare()) {
                var code = "while True: pass";
                interactive.Type(code + "\n", waitForLastLine: false);
                interactive.WaitForText(">" + code, ".", "");

                interactive.App.ExecuteCommand("Edit.LineUp");
                interactive.App.ExecuteCommand("Edit.LineUp");
                interactive.App.ExecuteCommand("Edit.LineEnd");
                interactive.App.ExecuteCommand("Edit.LineStartExtend");
                interactive.App.ExecuteCommand("Edit.Copy");

                interactive.CancelExecution();

                if (interactive.Settings.KeyboardInterruptHasTracebackHeader) {
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

        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void HistoryUpdateDef() {
            using (var interactive = Prepare()) {
                Keyboard.Type("def f():\rprint('hi')\r\r");
                interactive.WaitForText(">def f():", ".    print('hi')", ".", ">");

                interactive.PreviousHistoryItem();
                // delete i')
                interactive.Backspace(3);

                Keyboard.Type("ello')\r\r");

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

        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void HistoryAppendDef() {
            using (var interactive = Prepare()) {
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

        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void HistoryBackForward() {
            using (var interactive = Prepare()) {
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
        [TestMethod, Priority(1)]
        [HostType("VSTestHost")]
        public virtual void HistoryMaximumLength() {
            using (var interactive = Prepare()) {
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
        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void HistoryUncommittedInput1() {
            using (var interactive = Prepare()) {
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
        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void HistoryUncommittedInput2() {
            using (var interactive = Prepare()) {
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
        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void HistorySearch() {
            using (var interactive = Prepare()) {
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

        [TestMethod, Priority(1)]
        [HostType("VSTestHost")]
        public virtual void CommentPaste() {
            using (var interactive = Prepare()) {
                const string comment = "# fob oar baz";
                interactive.ClearInput();
                interactive.Paste(comment);
                interactive.WaitForText(">" + comment);

                interactive.ClearInput();
                interactive.Paste(comment + "\r\n");
                interactive.WaitForText(">" + comment);

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

        /// <summary>
        /// def f(): pass
        /// 
        /// X
        /// 
        /// def g(): pass
        /// 
        /// </summary>
        [TestMethod, Priority(1)]
        [HostType("VSTestHost")]
        public virtual void PasteWithError() {
            using (var interactive = Prepare()) {
                interactive.RequirePrimaryPrompt();

                const string code = @"def f(): pass

X

def g(): pass

";
                interactive.Paste(code);

                interactive.WaitForText(
                    ">def f(): pass",
                    ".",
                    ">X",
                    "Traceback (most recent call last):",
                    "  File \"<" + interactive.Settings.SourceFileName + ">\", line 1, in <module>",
                    "NameError: name 'X' is not defined",
                    ">"
                );
            }
        }

        [TestMethod, Priority(1)]
        [HostType("VSTestHost")]
        public virtual void CsvPaste() {
            using (var interactive = Prepare()) {
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

        [TestMethod, Priority(0)]
        [TestCategory("Interactive"), TestCategory("RequiresKeyboard")]
        [HostType("VSTestHost")]
        public virtual void SelectAll() {
            using (var interactive = Prepare()) {
                // Python interactive window.  Type $help for a list of commands.
                // >>> def fob():
                // ...     print('hi')
                // ...     return 123
                // ... 
                // >>> fob()
                // hi
                // 123
                // >>> input()
                // blah
                // 'blah'
                // >>> 

                interactive.RequireSecondaryPrompt();
                interactive.SetOptionValue(ReplOptions.StandardInputPrompt, "INPUT: ");
                interactive.EnsureInputFunction();
                interactive.Type(@"def fob():
print('hi')
return 123

fob()
input()");

                interactive.WaitForText(
                    ">def fob():",
                    ".    print('hi')",
                    ".    return 123",
                    ".",
                    ">fob()",
                    "hi",
                    "123",
                    ">input()",
                    "<"
                );

                interactive.Type("blah");
                interactive.WaitForTextEnd("<blah", "'blah'", ">");

                var text = interactive.Window.TextView.TextBuffer.CurrentSnapshot.GetText();
                var firstPrimaryPrompt = text.IndexOf(interactive.Settings.PrimaryPrompt);
                var hiLiteral = text.IndexOf("'hi'");
                var firstSecondaryPrompt = text.IndexOf(interactive.Settings.SecondaryPrompt);
                var fobCall = text.IndexOf("fob()\r");
                var hiOutput = text.IndexOf("hi", fobCall) + 1;
                var oneTwoThreeOutput = text.IndexOf("123", fobCall) + 1;
                var blahStdIn = text.IndexOf("blah") + 2;
                var blahOutput = text.IndexOf("'blah'") + 2;

                var firstSubmission = string.Format("def fob():\r\n{0}    print('hi')\r\n{0}    return 123\r\n{0}\r\n", interactive.Settings.SecondaryPrompt);
                if (interactive.Settings.InlinePrompts) {
                    AssertContainingRegion(interactive, firstPrimaryPrompt + 0, firstSubmission);
                    AssertContainingRegion(interactive, firstPrimaryPrompt + 1, firstSubmission);
                    AssertContainingRegion(interactive, firstPrimaryPrompt + 2, firstSubmission);
                    AssertContainingRegion(interactive, firstPrimaryPrompt + 3, firstSubmission);
                    AssertContainingRegion(interactive, firstPrimaryPrompt + 4, firstSubmission);
                    AssertContainingRegion(interactive, hiLiteral, firstSubmission);
                    AssertContainingRegion(interactive, firstSecondaryPrompt + 0, firstSubmission);
                    AssertContainingRegion(interactive, firstSecondaryPrompt + 1, firstSubmission);
                    AssertContainingRegion(interactive, firstSecondaryPrompt + 2, firstSubmission);
                    AssertContainingRegion(interactive, firstSecondaryPrompt + 3, firstSubmission);
                    AssertContainingRegion(interactive, firstSecondaryPrompt + 4, firstSubmission);
                }
                AssertContainingRegion(interactive, fobCall, "fob()\r\n");
                AssertContainingRegion(interactive, hiOutput, "hi\r\n123\r\n");
                AssertContainingRegion(interactive, oneTwoThreeOutput, "hi\r\n123\r\n");
                AssertContainingRegion(interactive, blahStdIn, "blah\r\n");
                AssertContainingRegion(interactive, blahOutput, "'blah'\r\n");
            }
        }

        /// <summary>
        /// Tests cut when the secondary prompt is highlighted as part of the
        /// selection
        /// </summary>
        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void EditCutIncludingPrompt() {
            using (var interactive = Prepare()) {
                interactive.RequireSecondaryPrompt();

                interactive.Type("def f():\nprint('hi')", commitLastLine: false);
                interactive.WaitForText(">def f():", ".    print('hi')");

                Keyboard.Type(Key.Home);
                Keyboard.Type(Key.Home);
                Keyboard.Type(Key.Left);
                interactive.App.ExecuteCommand("Edit.LineEndExtend");
                interactive.App.ExecuteCommand("Edit.Cut");

                interactive.WaitForText(">def f():", ".");

                interactive.App.ExecuteCommand("Edit.Paste");

                interactive.WaitForText(">def f():", ".    print('hi')");
            }
        }

        /// <summary>
        /// Tests pasting when the secondary prompt is highlighted as part of the selection
        /// </summary>
        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void EditPasteSecondaryPromptSelected() {
            using (var interactive = Prepare()) {
                interactive.RequireSecondaryPrompt();

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
        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void EditPasteSecondaryPromptSelectedInPromptMargin() {
            using (var interactive = Prepare()) {
                interactive.RequireSecondaryPrompt();

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
        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void ReplCommandUnknown() {
            using (var interactive = Prepare()) {
                const string code = "$unknown";
                interactive.SubmitCode(code);
                interactive.WaitForText(">" + code, "Unknown command 'unknown', use \"$help\" for help", ">");
            }
        }

        /// <summary>
        /// Tests entering an unknown repl commmand
        /// </summary>
        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void ReplCommandComment() {
            using (var interactive = Prepare()) {
                const string code = "$$ quox oar baz";
                interactive.SubmitCode(code);
                interactive.WaitForText(">" + code, ">");
            }
        }

        /// <summary>
        /// Tests using the $cls clear screen command
        /// </summary>
        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void ClearScreenCommand() {
            using (var interactive = Prepare()) {
                interactive.Type("$cls", commitLastLine: false);
                interactive.WaitForText(">$cls");

                interactive.SubmitCurrentText();
                interactive.WaitForText(">");
            }
        }

        /// <summary>
        /// Tests REPL command help
        /// </summary>
        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void ReplCommandHelp() {
            using (var interactive = Prepare()) {
                interactive.SubmitCode("$help");

                interactive.WaitForTextStart(
                    ">$help",
                    string.Format("  {0,-24}  {1}", "$help", "Show a list of REPL commands")
                );
            }
        }

        /// <summary>
        /// Tests REPL command $load, with a simple script.
        /// </summary>
        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void CommandsLoadScript() {
            using (var interactive = Prepare()) {
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
        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void CommandsLoadScriptWithQuotes() {
            using (var interactive = Prepare()) {
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
        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void CommandsLoadScriptWithClass() {
            using (var interactive = Prepare()) {
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
        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void CommandsLoadScriptMultipleSubmissions() {
            using (var interactive = Prepare()) {
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
        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void CommandsLoadScriptMultipleSubmissionsWithClearScreen() {
            using (var interactive = Prepare()) {
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
        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void InsertCode() {
            using (var interactive = Prepare()) {
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
        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void InsertCodeWhileRunning() {
            using (var interactive = Prepare()) {
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

                if (interactive.Settings.KeyboardInterruptHasTracebackHeader) {
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

        #region Projection Buffer tests

        /// <summary>
        /// Replacing a snippet including a single line break with another
        /// snippet that also includes a single line break (line delta is zero).
        /// </summary>
        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void TestProjectionBuffers_ZeroLineDeltaChange() {
            using (var interactive = Prepare()) {
                interactive.Invoke(() => {
                    var buffer = interactive.Window.CurrentLanguageBuffer;
                    buffer.Replace(new Span(0, 0), "def f():\r\n    pass");
                    VerifyProjectionSpans((ReplWindow)interactive.Window);

                    var snapshot = buffer.CurrentSnapshot;

                    var spanToReplace = new Span("def f():".Length, "\r\n    ".Length);
                    Assert.AreEqual("\r\n    ", snapshot.GetText(spanToReplace));

                    using (var edit = buffer.CreateEdit(EditOptions.None, null, null)) {
                        edit.Replace(spanToReplace.Start, spanToReplace.Length, "\r\n");
                        edit.Apply();
                    }

                    VerifyProjectionSpans((ReplWindow)interactive.Window);
                });
            }
        }

        /// <summary>
        /// Verifies that current langauge buffer is projected as:
        /// {Prompt1}{Language Span}\r\n
        /// {Prompt2}{Language Span}\r\n
        /// {Prompt2}{Language Span}
        /// </summary>
        private static void VerifyProjectionSpans(ReplWindow repl) {
            var projectionSpans = repl.ProjectionSpans;
            var projectionBuffer = repl.TextBuffer;
            var snapshot = repl.CurrentLanguageBuffer.CurrentSnapshot;

            int firstLangSpan = projectionSpans.Count - snapshot.LineCount * 2 + 1;
            int projectionLine = projectionBuffer.CurrentSnapshot.LineCount - snapshot.LineCount;
            for (int i = firstLangSpan; i < projectionSpans.Count; i += 2, projectionLine++) {
                // prompt:
                if (i == firstLangSpan) {
                    Assert.IsTrue(projectionSpans[i - 1].Kind == ReplSpanKind.Prompt);
                } else {
                    Assert.IsTrue(projectionSpans[i - 1].Kind == ReplSpanKind.SecondaryPrompt);
                }

                // language span:
                Assert.IsTrue(projectionSpans[i].Kind == ReplSpanKind.Language);
                var trackingSpan = projectionSpans[i].TrackingSpan;
                Assert.IsNotNull(trackingSpan);

                var text = trackingSpan.GetText(snapshot);
                var lineBreak = projectionBuffer.CurrentSnapshot.GetLineFromLineNumber(projectionLine).GetLineBreakText();
                Assert.IsTrue(text.EndsWith(lineBreak));
                Assert.IsTrue(text.IndexOf("\n") == -1 || text.IndexOf("\n") >= text.Length - lineBreak.Length);

            }
        }

        #endregion

        #region Helper methods

        internal static void AssertContainingRegion(ReplWindowProxy interactive, int position, string expectedText) {
            SnapshotSpan? span = ((ReplWindow)interactive.Window).GetContainingRegion(
                new SnapshotPoint(interactive.TextView.TextBuffer.CurrentSnapshot, position)
            );
            Assert.IsNotNull(span);
            Assert.AreEqual(expectedText, span.Value.GetText());
        }


        #endregion
    }

    [TestClass]
    public class ReplWindowTestsDefaultPrompt : ReplWindowTests {
        internal override ReplWindowProxySettings Settings {
            get {
                return new ReplWindowProxySettings {
                    Version = PythonPaths.Python27 ?? PythonPaths.Python27_x64,
                    InlinePrompts = true
                };
            }
        }
    }

    [TestClass]
    public class ReplWindowTestsGlyphPrompt : ReplWindowTests {
        internal override ReplWindowProxySettings Settings {
            get {
                return new ReplWindowProxySettings {
                    Version = PythonPaths.Python27 ?? PythonPaths.Python27_x64,
                    InlinePrompts = false
                };
            }
        }

        // The following tests are skipped when running in this class
        public override void CtrlEnterOnPreviousInput() { }
        public override void BackspaceSecondaryPromptSelected() { }
        public override void DeleteSecondaryPromptSelected() { }
        public override void EditTypeSecondaryPromptSelected() { }
        public override void TestDelAtEndOfLine() { }
        public override void CursorWhileCodeIsRunning() { }
        public override void HistoryBackForward() { }
        public override void PasteWithError() { }
        public override void EditCutIncludingPrompt() { }
        public override void EditPasteSecondaryPromptSelected() { }
        public override void EditPasteSecondaryPromptSelectedInPromptMargin() { }

    }
}

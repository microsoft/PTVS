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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using Microsoft.IronPythonTools.Interpreter;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Repl;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using PythonToolsTests;
using TestUtilities;
using TestUtilities.Mocks;
using TestUtilities.Python;
using TestUtilities.UI;
using TestUtilities.UI.Python;
using Keyboard = TestUtilities.UI.Keyboard;

namespace ReplWindowUITests {
    /// <summary>
    /// Unit tests that don't apply to specific interpreters and should only
    /// be run once.
    /// </summary>
    [TestClass]
    public class ReplWindowTests {
        internal static readonly CPythonInterpreterFactoryProvider InterpFactory = new CPythonInterpreterFactoryProvider();

        [TestMethod, Priority(0)]
        public void CanExecuteText() {
            // http://pytools.codeplex.com/workitem/606
            var eval = new PythonReplEvaluator(
                InterpFactory.GetInterpreterFactories().First(fact => fact.Id == new Guid("{2AF0F10D-7135-4994-9156-5D01C9C11B7E}") && fact.Configuration.Version == new Version(2, 7)),
                null,
                new ReplTestReplOptions()
            );

            Assert.AreEqual(eval.CanExecuteText("x = \\"), false);
            Assert.AreEqual(eval.CanExecuteText("x = \\\r\n42\r\n\r\n"), true);
        }

        /// <summary>
        /// Directed unit tests for the repl evaluator's spliting code into individual statements...
        /// </summary>
        [TestMethod, Priority(0)]
        public void ReplSplitCodeTest() {
            // http://pytools.codeplex.com/workitem/606
            var eval = new PythonReplEvaluator(
                InterpFactory.GetInterpreterFactories().First(fact => fact.Id == new Guid("{2AF0F10D-7135-4994-9156-5D01C9C11B7E}") && fact.Configuration.Version == new Version(2, 7)),
                null,
                new ReplTestReplOptions()
            );

            var testCases = new[] {
                new { 
                    Code = @"def f():
    pass

def g():
    pass

f()
g()",
                    Expected = new[] { "def f():\r\n    pass\r\n", "def g():\r\n    pass\r\n", "f()", "g()" }
                },
                new {
                    Code = @"def f():
    pass

f()

def g():
    pass

f()
g()",
                    Expected = new[] { "def f():\r\n    pass\r\n", "f()", "def g():\r\n    pass\r\n", "f()", "g()" }
                },
                new {
                    Code = @"def f():
    pass

f()
f()

def g():
    pass

f()
g()",
                    Expected = new[] { "def f():\r\n    pass\r\n", "f()", "f()", "def g():\r\n    pass\r\n", "f()", "g()" }
                }
            };

            foreach (var testCase in testCases) {
                var got = eval.SplitCode(testCase.Code).ToArray();
                Assert.AreEqual(testCase.Expected.Length, got.Length);
                for (int i = 0; i < got.Length; i++) {
                    Assert.AreEqual(testCase.Expected[i], got[i]);
                }
            }
        }
    }

    public abstract class ReplWindowTestHelperBase {
        static ReplWindowTestHelperBase() {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        // Should be called from [TestInitialize] methods
        protected void TestInitialize() {
            // Writes the interpreter version to the console so it can be
            // viewed in the output for the test.
            Console.WriteLine(InterpreterDescription);
            PythonVersion.AssertInstalled();
        }

        /// <summary>
        /// Opens the interactive window, clears the screen.
        /// </summary>
        internal InteractiveWindow Prepare(bool reopenOnly = false, string description = null) {
            using (var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.SuppressCloseAllOnDispose();

                if (!reopenOnly) {
                    ConfigurePrompts();
                }

                description = description ?? InterpreterDescription;

                try {
                    GetPythonAutomation().OpenInteractive(description);
                } catch (KeyNotFoundException) {
                    // Can't open it, but may still be able to find it.
                }
                var interactive = app.GetInteractiveWindow(description);
                if (interactive == null) {
                    Assert.Inconclusive("Need " + description);
                }

                interactive.WaitForIdleState();
                app.Element.SetFocus();
                interactive.Element.SetFocus();

                if (!reopenOnly) {
                    interactive.ClearScreen();
                    interactive.ReplWindow.ClearHistory();
                    interactive.WaitForReadyState();

                    interactive.Reset();
                    var task = interactive.ReplWindow.Evaluator.ExecuteText("print('READY')");
                    Assert.IsTrue(task.Wait(10000), "ReplWindow did not initialize in time");
                    Assert.AreEqual(ExecutionResult.Success, task.Result);
                    interactive.WaitForTextEnd("READY", ReplPrompt);

                    interactive.ClearScreen();
                    interactive.ReplWindow.ClearHistory();
                }
                interactive.WaitForReadyState();
                return interactive;
            }
        }

        protected void ForceReset() {
            using (var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.SuppressCloseAllOnDispose();
                var interactive = app.GetInteractiveWindow(InterpreterDescription);
                interactive.Reset();
            }
        }

        protected virtual void ConfigurePrompts() {
            var options = GetInteractiveOptions();

            options.InlinePrompts = true;
            options.UseInterpreterPrompts = false;
            options.PrimaryPrompt = ReplPrompt;
            options.SecondaryPrompt = SecondPrompt;
        }

        protected IPythonInteractiveOptions GetInteractiveOptions(string name = null) {
            name = name ?? InterpreterDescription;
            Debug.Assert(name.EndsWith(" Interactive"));
            var options = ((IPythonOptions)VsIdeTestHostContext.Dte.GetObject("VsPython")).GetInteractiveOptions(name.Substring(0, name.Length - " Interactive".Length));
            if (options == null) {
                Assert.Inconclusive("Need " + name);
            }
            return options;
        }

        protected static IVsPython GetPythonAutomation() {
            return ((IVsPython)VsIdeTestHostContext.Dte.GetObject("VsPython"));
        }

        protected abstract PythonVersion PythonVersion { get; }

        protected abstract string InterpreterDescription { get; }

        protected virtual bool IPythonSupported {
            get { return true; }
        }

        protected virtual string RawInput {
            get { return "raw_input"; }
        }

        protected virtual string IntFirstMember {
            get { return "conjugate"; }
        }

        protected virtual string ReplPrompt {
            get { return ">>> "; }
        }

        protected virtual string SecondPrompt {
            get { return "... "; }
        }

        protected virtual string SourceFileName {
            get { return "stdin"; }
        }

        protected virtual bool KeyboardInterruptHasTracebackHeader {
            get { return true; }
        }

        protected virtual string Print42Output {
            get { return "42"; }
        }

        protected virtual bool CanRedirectSubprocess {
            get { return true; }
        }

        protected virtual IEnumerable<string> IntDocumentation {
            get {
                yield return "Type:       int";
                //yield return "Base Class: <type 'int'>";
                yield return "String Form:42";
                //yield return "AnalysisValue:  Interactive";
                yield return "Docstring:";
                yield return "int(x[, base]) -> integer";
                yield return "";
                yield return "Convert a string or number to an integer, if possible.  A floating point";
                yield return "argument will be truncated towards zero (this does not include a string";
                yield return "representation of a floating point number!)  When converting a string, use";
                yield return "the optional base.  It is an error to supply a base when converting a";
                yield return "non-string.  If base is zero, the proper base is guessed based on the";
                yield return "string content.  If the argument is outside the integer range a";
                yield return "long object will be returned instead.";

            }
        }
    }

    [TestClass]
    public class Python26ReplWindowTests : ReplWindowTestHelperBase {
        [TestInitialize]
        public void Initialize() {
#if !(PY_ALL || PY_26)
            // Because this is the base class, we can't exclude it from the
            // other builds. Instead, we'll simply skip all the tests.
            Assert.Inconclusive("Test not part of this run");
#endif
            TestInitialize();
        }

        /// <summary>
        /// “def f(): pass” + 2 ENTERS
        /// f( should bring signature help up
        /// 
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SimpleSignatureHelp() {
            var interactive = Prepare();

            Assert.AreNotEqual(null, interactive);

            const string code = "def f(): pass";
            Keyboard.Type(code + "\r\r");

            interactive.WaitForText(ReplPrompt + code, SecondPrompt, ReplPrompt);
            var stopAt = DateTime.Now.AddSeconds(20.0);
            interactive.TextView.GetAnalyzer().WaitForCompleteAnalysis(_ => DateTime.Now < stopAt);
            if (DateTime.Now >= stopAt) {
                Assert.Fail("Timeout waiting for complete analysis");
            }

            Keyboard.Type("f(");

            interactive.WaitForText(ReplPrompt + code, SecondPrompt, ReplPrompt + "f(");

            using (var sh = interactive.WaitForSession<ISignatureHelpSession>()) {
                Assert.AreEqual("f()", sh.Session.SelectedSignature.Content);
                Keyboard.PressAndRelease(Key.Escape);

                interactive.WaitForSessionDismissed();
            }
        }

        /// <summary>
        /// “def f(a, b=1, c="d"): pass” + 2 ENTERS
        /// f( should bring signature help up and show default values and types
        /// 
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SignatureHelpDefaultValue() {
            var interactive = Prepare();

            Assert.AreNotEqual(null, interactive);

            const string code = "def f(a, b=1, c=\"d\"): pass";
            Keyboard.Type(code + "\r\r");

            interactive.WaitForText(ReplPrompt + code, SecondPrompt, ReplPrompt);
            var stopAt = DateTime.Now.AddSeconds(20.0);
            interactive.TextView.GetAnalyzer().WaitForCompleteAnalysis(_ => DateTime.Now < stopAt);
            if (DateTime.Now >= stopAt) {
                Assert.Fail("Timeout waiting for complete analysis");
            }

            Keyboard.Type("f(");

            interactive.WaitForText(ReplPrompt + code, SecondPrompt, ReplPrompt + "f(");

            using (var sh = interactive.WaitForSession<ISignatureHelpSession>()) {
                Assert.AreEqual("f(a, b: int = 1, c: str = 'd')", sh.Session.SelectedSignature.Content);
                Keyboard.PressAndRelease(Key.Escape);

                interactive.WaitForSessionDismissed();
            }
        }

        /// <summary>
        /// “x = 42”
        /// “x.” should bring up intellisense completion
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SimpleCompletion() {
            var interactive = Prepare();

            const string code = "x = 42";
            Keyboard.Type(code + "\r");

            interactive.WaitForText(ReplPrompt + code, ReplPrompt);

            Keyboard.Type("x.");

            interactive.WaitForText(ReplPrompt + code, ReplPrompt + "x.");

            using (var sh = interactive.WaitForSession<ICompletionSession>()) {
                Assert.IsNotNull(sh.Session.SelectedCompletionSet);

                StringBuilder completions = new StringBuilder();
                completions.AppendLine(sh.Session.SelectedCompletionSet.DisplayName);

                foreach (var completion in sh.Session.SelectedCompletionSet.Completions) {
                    completions.Append(completion.InsertionText);
                }

                // commit entry
                Keyboard.PressAndRelease(Key.Tab);
                interactive.WaitForText(ReplPrompt + code, ReplPrompt + "x." + IntFirstMember);
                interactive.WaitForSessionDismissed();
            }

            // clear input at repl
            Keyboard.PressAndRelease(Key.Escape);

            // try it again, and dismiss the session
            Keyboard.Type("x.");
            using (interactive.WaitForSession<ICompletionSession>()) { }
        }

        /// <summary>
        /// “x = 42”
        /// “x “ should not being up any completions.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SimpleCompletionSpaceNoCompletion() {
            var interactive = Prepare();

            const string code = "x = 42";
            Keyboard.Type(code + "\r");

            interactive.WaitForText(ReplPrompt + code, ReplPrompt);

            // x<space> should not bring up a completion session
            Keyboard.Type("x ");

            interactive.WaitForText(ReplPrompt + code, ReplPrompt + "x ");

            interactive.WaitForSessionDismissed();
        }

        /// <summary>
        /// “x = 42”
        /// “x “ should not being up any completions.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestStdOutRedirected() {
            if (ReplPrompt.Length == 0) {
                // Test requires primary prompt
                return;
            }
            if (!CanRedirectSubprocess) {
                // Cannot redirect stdout from subprocess
                return;
            }

            var interactive = Prepare();

            // Spaces after the module name prevent autocomplete from changing them.
            // In particular, 'subprocess' does not appear in the default database,
            // but '_subprocess' does.
            const string code = "import subprocess , sys ";
            Keyboard.Type(code + "\r");

            interactive.WaitForText(ReplPrompt + code, ReplPrompt);
            interactive.WaitForReadyState();

            const string code2 = "x = subprocess.Popen(['C:\\\\python27\\\\python.exe', '-c', 'print 42'], stdout=sys.stdout).wait()";
            Keyboard.Type(code2 + "\r");

            interactive.WaitForText(ReplPrompt + code, ReplPrompt + code2, Print42Output, ReplPrompt);
        }

        /// <summary>
        /// Pasting CSV data
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CsvPaste() {
            var interactive = Prepare();

            ((UIElement)interactive.TextView).Dispatcher.Invoke((Action)(() => {
                var dataObject = new System.Windows.DataObject();
                dataObject.SetText("foo");
                var stream = new MemoryStream(UTF8Encoding.Default.GetBytes("\"abc,\",\"foo\",\"\"\"foo,\"\"\",bar,baz\"x\"bar,\"baz,\"\"x,\"\"bar\",,    ,bar,\",\"\",\"\"\",baz\"x\"'bar,\"baz\"\"x\"\"',bar\",\"\"\"\",\"\"\",\"\"\",\",\",\\\r\n1,2,3,4,9,10,11,12,13,19,33,22,,,,,,\r\n4,5,6,5,2,3,4,3,1,20,44,33,,,,,,\r\n7,8,9,6,3,4,0,9,4,33,55,33,,,,,,"));
                dataObject.SetData(DataFormats.CommaSeparatedValue, stream);
                Clipboard.SetDataObject(dataObject, true);
            }));

            Keyboard.ControlV();

            string line1 = "[";
            string line2 = "  ['abc,', '\"foo\"', '\"foo,\"', 'bar', 'baz\"x\"bar', 'baz,\"x,\"bar', None, None, 'bar', ',\",\"', 'baz\"x\"\\'bar', 'baz\"x\"\\',bar', '\"\"\"\"', '\",\"', ',', '\\\\'],";
            string line3 = "  [1, 2, 3, 4, 9, 10, 11, 12, 13, 19, 33, 22, None, None, None, None, None, None],";
            string line4 = "  [4, 5, 6, 5, 2, 3, 4, 3, 1, 20, 44, 33, None, None, None, None, None, None],";
            string line5 = "  [7, 8, 9, 6, 3, 4, 0, 9, 4, 33, 55, 33, None, None, None, None, None, None],";
            string line6 = "]";

            interactive.WaitForText(
                ReplPrompt + line1,
                SecondPrompt + line2,
                SecondPrompt + line3,
                SecondPrompt + line4,
                SecondPrompt + line5,
                SecondPrompt + line6,
                SecondPrompt);
        }

        /// <summary>
        /// x = 42; x.car[enter] – should type “car” not complete to “conjugate”
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CompletionWrongText() {
            var interactive = Prepare();

            const string code = "x = 42";
            Keyboard.Type(code + "\r");

            interactive.WaitForText(ReplPrompt + code, ReplPrompt);

            Keyboard.Type("x.");
            interactive.WaitForSession<ICompletionSession>();
            Keyboard.Type("car");
            Keyboard.Type(Key.Enter);

            interactive.WaitForSessionDismissed();
            interactive.WaitForText(ReplPrompt + code, ReplPrompt + "x.car", "Traceback (most recent call last):", "  File \"<" + SourceFileName + ">\", line 1, in <module>", "AttributeError: 'int' object has no attribute 'car'", ReplPrompt);
        }

        /// <summary>
        /// x = 42; x.conjugate[enter] – should respect enter completes option, and should respect enter at end of word 
        /// completes option.  When it does execute the text the output should be on the next line.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CompletionFullText() {
            var options = (IPythonOptions)VsIdeTestHostContext.Dte.GetObject("VsPython");
            options.Intellisense.AddNewLineAtEndOfFullyTypedWord = false;

            var interactive = Prepare();

            Keyboard.Type("pass\r");
            interactive.WaitForText(ReplPrompt + "pass", ReplPrompt);
            interactive.ClearScreen();
            interactive.WaitForText(ReplPrompt);

            const string code = "x = 42";
            Keyboard.Type(code + "\r");

            interactive.WaitForText(ReplPrompt + code, ReplPrompt);

            Keyboard.Type("x.");
            interactive.WaitForSession<ICompletionSession>();
            Keyboard.Type("real");
            Keyboard.Type(Key.Enter);

            interactive.WaitForSessionDismissed();
            interactive.WaitForText(ReplPrompt + code, ReplPrompt + "x.real");

            // try again w/ option flipped
            options.Intellisense.AddNewLineAtEndOfFullyTypedWord = true;
            interactive = Prepare();

            Keyboard.Type(code + "\r");

            interactive.WaitForText(ReplPrompt + code, ReplPrompt);

            Keyboard.Type("x.");
            interactive.WaitForSession<ICompletionSession>();
            Keyboard.Type("real");
            Keyboard.Type(Key.Enter);

            interactive.WaitForSessionDismissed();
            interactive.WaitForText(ReplPrompt + code, ReplPrompt + "x.real", "42", ReplPrompt);
        }

        /// <summary>
        /// Enter in a middle of a line should insert new line
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void EnterInMiddleOfLine() {
            var interactive = Prepare();

            const string code = "def f(): #foo";
            Keyboard.Type(code);
            Keyboard.Type(Key.Left);
            Keyboard.Type(Key.Left);
            Keyboard.Type(Key.Left);
            Keyboard.Type(Key.Left);
            Keyboard.Type(Key.Enter);
            Keyboard.Type("pass");
            Keyboard.PressAndRelease(Key.Enter, Key.LeftCtrl);
            interactive.WaitForText(ReplPrompt + "def f(): ", SecondPrompt + "    pass#foo", ReplPrompt);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Reset() {
            var interactive = Prepare();

            // TODO (bug): Reset doesn't clean up completely. 
            // This causes a space to appear in the buffer even after reseting the REPL.

            // const string print1 = "print 1,";
            // Keyboard.Type(print1);
            // Keyboard.Type(Key.Enter);
            // 
            // interactive.WaitForText(ReplPrompt + print1, "1", ReplPrompt);

            // CtrlBreakInStandardInput();
        }

        /// <summary>
        /// Prompts always inserted at the beginning of a line.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void PromptPositions() {
            var interactive = Prepare();

            // TODO (bug): this insert a space somwhere in the socket stream
            // const string print1 = "print 1,";
            // Keyboard.Type(print1);
            // Keyboard.Type(Key.Enter);
            // 
            // interactive.WaitForText(ReplPrompt + print1, "1", ReplPrompt);

            // The data received from the socket include " ", so it's not a REPL issue.
            // const string print2 = "print 2,";
            // Keyboard.Type(print2);
            // Keyboard.Type(Key.Enter);
            // 
            // interactive.WaitForText(ReplPrompt + print1, "1", ReplPrompt + print2, "2", ReplPrompt);
        }

        /// <summary>
        /// Enter in a middle of a line should insert new line
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void EnterAtBeginningOfLine() {
            // TODO (bug): complete statement detection
            Assert.Fail("TODO (bug): complete statement detection");
            //    var interactive = Prepare();
            // 
            //    const string code = "\"\"\"";
            //    Keyboard.Type(code);
            //    Keyboard.Type(Key.Enter);
            //    Keyboard.Type(Key.Enter);
            //    interactive.WaitForText(ReplPrompt + "\"\"\"", SecondPrompt, SecondPrompt);

            //    Keyboard.Type("a");
            //    Keyboard.Type(Key.Left);
            //    Keyboard.Type(Key.Enter);
            //    interactive.WaitForText(ReplPrompt + "\"\"\"", SecondPrompt, SecondPrompt, SecondPrompt + "a");
        }

        /// <summary>
        /// LineBReak should insert a new line and not submit.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void LineBreak() {
            var interactive = Prepare();

            const string quotes = "\"\"\"";
            Keyboard.Type(quotes);
            Keyboard.Type(Key.Enter);
            Keyboard.PressAndRelease(Key.Enter, Key.LeftShift);
            Keyboard.Type(quotes);
            Keyboard.Type(Key.Enter);
            Keyboard.Type(Key.Enter);

            interactive.WaitForText(
                ReplPrompt + quotes,
                SecondPrompt,
                SecondPrompt + quotes,
                SecondPrompt,
                "'\\n\\n'",
                ReplPrompt);
        }

        /// <summary>
        /// Escape should clear both lines
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void EscapeClearsMultipleLines() {
            var interactive = Prepare();

            const string code = "def f(): #foo";
            Keyboard.Type(code);
            Keyboard.Type(Key.Left);
            Keyboard.Type(Key.Left);
            Keyboard.Type(Key.Left);
            Keyboard.Type(Key.Left);
            Keyboard.Type(Key.Enter);
            Keyboard.Type(Key.Tab);
            Keyboard.Type("pass");
            Keyboard.Type(Key.Escape);
            interactive.WaitForText(ReplPrompt);
        }

        /// <summary>
        /// “x=42” left left ctrl-enter should commit assignment
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CtrlEnterCommits() {
            var interactive = Prepare();

            const string code = "x = 42";
            Keyboard.Type(code);
            Keyboard.Type(Key.Left);
            Keyboard.Type(Key.Left);
            Keyboard.PressAndRelease(Key.Enter, Key.LeftCtrl);
            interactive.WaitForText(ReplPrompt + "x = 42", ReplPrompt);
        }

        /// <summary>
        /// while True: pass / Right Click -> Break Execution (or Ctrl-Break) should break execution
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CtrlBreakInterrupts() {
            var interactive = Prepare();

            const string code = "while True: pass\r\n";
            Keyboard.Type(code);
            interactive.WaitForText(ReplPrompt + "while True: pass", SecondPrompt, "");

            interactive.CancelExecution();

            if (KeyboardInterruptHasTracebackHeader) {
                interactive.WaitForTextStart(ReplPrompt + "while True: pass", SecondPrompt, "Traceback (most recent call last):");
            }
            interactive.WaitForTextEnd("KeyboardInterrupt", ReplPrompt);
        }

        /// <summary>
        /// Ctrl-Break while running should result in a new prompt
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CtrlBreakNotRunning() {
            var interactive = Prepare();

            interactive.WaitForText(ReplPrompt);

            try {
                VsIdeTestHostContext.Dte.ExecuteCommand("OtherContextMenus.InteractiveConsole.CancelExecution");
                Assert.Fail("CancelExecution should not be available");
            } catch {
            }

            interactive.WaitForText(ReplPrompt);
        }

        /// <summary>
        /// Ctrl-Break while entering standard input should return an empty input and append a primary prompt.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CtrlBreakInStandardInput() {
            var interactive = Prepare();

            interactive.WithStandardInputPrompt("INPUT: ", (stdInputPrompt) => {
                interactive.WaitForText(ReplPrompt);

                Keyboard.Type(RawInput + "()" + "\r");
                interactive.WaitForText(ReplPrompt + RawInput + "()", stdInputPrompt);

                Keyboard.Type("ignored");
                interactive.WaitForText(ReplPrompt + RawInput + "()", stdInputPrompt + "ignored");

                interactive.CancelExecution();

                interactive.WaitForText(
                    ReplPrompt + RawInput + "()",
                    stdInputPrompt + "ignored",
                                     "''",
                    ReplPrompt
                );

                Keyboard.Type(Key.Up); // prev history
                Keyboard.Type(Key.Enter);

                interactive.WaitForText(
                    ReplPrompt + RawInput + "()",
                    stdInputPrompt + "ignored",
                                     "''",
                    ReplPrompt + RawInput + "()",
                    stdInputPrompt
                );

                Keyboard.Type("ignored2");

                interactive.WaitForText(
                    ReplPrompt + RawInput + "()",
                    stdInputPrompt + "ignored",
                                     "''",
                    ReplPrompt + RawInput + "()",
                    stdInputPrompt + "ignored2"
                );

                interactive.CancelExecution(1);

                interactive.WaitForText(
                    ReplPrompt + RawInput + "()",
                    stdInputPrompt + "ignored",
                                     "''",
                    ReplPrompt + RawInput + "()",
                    stdInputPrompt + "ignored2",
                                     "''",
                    ReplPrompt
                );
            });
        }

        protected virtual string UnicodeStringPrefix {
            get {
                return "u";
            }
        }

        /// <summary>
        /// while True: pass / Right Click -> Break Execution (or Ctrl-Break) should break execution
        /// 
        /// This version runs for 1/2 second which kicks in the running UI.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CtrlBreakInterruptsLongRunning() {
            var interactive = Prepare();

            const string code = "while True: pass\r\n";
            Keyboard.Type(code);
            interactive.WaitForText(ReplPrompt + "while True: pass", SecondPrompt, "");

            System.Threading.Thread.Sleep(500);

            interactive.CancelExecution();

            if (KeyboardInterruptHasTracebackHeader) {
                interactive.WaitForTextStart(ReplPrompt + "while True: pass", SecondPrompt, "Traceback (most recent call last):");
            }
            interactive.WaitForTextEnd("KeyboardInterrupt", ReplPrompt);
        }

        /// <summary>
        /// Ctrl-Enter on previous input should paste input to end of buffer (doing it again should paste again – appending onto previous input)
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CtrlEnterOnPreviousInput() {
            var interactive = Prepare();

            const string code = "def f(): pass";
            Keyboard.Type(code + "\r\r");

            interactive.WaitForText(ReplPrompt + code, SecondPrompt, ReplPrompt);

            Keyboard.Type(Key.Left);
            Keyboard.Type(Key.Up);
            Keyboard.Type(Key.Up);
            Keyboard.Type(Key.Right);
            Keyboard.PressAndRelease(Key.Enter, Key.LeftCtrl);

            interactive.WaitForText(ReplPrompt + code, SecondPrompt, ReplPrompt + code, SecondPrompt);

            Keyboard.PressAndRelease(Key.Escape);

            interactive.WaitForText(ReplPrompt + code, SecondPrompt, ReplPrompt);
        }

        /// <summary>
        /// Type some text, hit Ctrl-Enter, should execute current line
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CtrlEnterForceCommit() {
            var interactive = Prepare();

            const string code = "def f(): pass";
            Keyboard.Type(code);

            interactive.WaitForText(ReplPrompt + code);

            Keyboard.PressAndRelease(Key.Enter, Key.LeftCtrl);

            interactive.WaitForText(ReplPrompt + code, ReplPrompt);
        }

        /// <summary>
        /// Type a function definition, go to next line, type pass, navigate left, hit ctrl-enter, should immediately execute func def.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CtrlEnterMultiLineForceCommit() {
            var interactive = Prepare();

            const string code = "def f():";
            Keyboard.Type(code);
            Keyboard.Type(Key.Enter);
            Keyboard.Type("pass");

            interactive.WaitForText(ReplPrompt + code, SecondPrompt + "    pass");

            Keyboard.Type(Key.Left);
            Keyboard.Type(Key.Left);
            Keyboard.Type(Key.Left);
            Keyboard.PressAndRelease(Key.Enter, Key.LeftCtrl);

            interactive.WaitForText(ReplPrompt + code, SecondPrompt + "    pass", ReplPrompt);
        }

        /// <summary>
        /// Define function “def f():\r\n    print ‘hi’”, scroll back up to history, add print “hello” to 2nd line, enter, 
        /// scroll back through both function definitions
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void HistoryUpdateDef() {
            var interactive = Prepare();

            string hiCode = "def f():\r\n" + SecondPrompt + "    print('hi')\r\n" + SecondPrompt;
            string helloCode = "def f():\r\n" + SecondPrompt + "    print('hello')\r\n" + SecondPrompt;
            string helloCodeNotCommitted = "def f():\r\n" + SecondPrompt + "    print('hello')";
            string hiCodeNotCommitted = "def f():\r\n" + SecondPrompt + "    print('hi')";
            Keyboard.Type("def f():\r");
            Keyboard.Type("print('hi')\r\r");

            interactive.WaitForText(ReplPrompt + hiCode, ReplPrompt);

            Keyboard.Type(Key.Up);
            // delete 'hi'
            Keyboard.Type(Key.Back);
            Keyboard.Type(Key.Back);
            Keyboard.Type(Key.Back);
            Keyboard.Type(Key.Back);
            Keyboard.Type(Key.Back);

            Keyboard.Type("'hello')");
            Keyboard.Type(Key.Enter);
            Keyboard.Type(Key.Enter);

            interactive.WaitForText(ReplPrompt + hiCode, ReplPrompt + helloCode, ReplPrompt);

            Keyboard.Type(Key.Up);
            interactive.WaitForText(ReplPrompt + hiCode, ReplPrompt + helloCode, ReplPrompt + helloCodeNotCommitted);

            Keyboard.Type(Key.Up);
            interactive.WaitForText(ReplPrompt + hiCode, ReplPrompt + helloCode, ReplPrompt + hiCodeNotCommitted);
        }

        /// <summary>
        /// Define function “def f():\r\n    print ‘hi’”, scroll back up to history, add print “hello” to 2nd line, enter, 
        /// scroll back through both function definitions
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void HistoryAppendDef() {
            var interactive = Prepare();

            Keyboard.Type("def f():\r");
            Keyboard.Type("print('hi')\r\r");

            interactive.WaitForText(
                ReplPrompt + "def f():",
                SecondPrompt + "    print('hi')",
                SecondPrompt,
                ReplPrompt
            );

            Keyboard.Type(Key.Up);
            Keyboard.Type(Key.Enter);
            Keyboard.Type("print('hello')\r\r");

            interactive.WaitForText(
                ReplPrompt + "def f():",
                SecondPrompt + "    print('hi')",
                SecondPrompt,
                ReplPrompt + "def f():",
                SecondPrompt + "    print('hi')",
                SecondPrompt + "    print('hello')",
                SecondPrompt,
                ReplPrompt
            );
        }

        /// <summary>
        /// Define function “def f():\r\n    print ‘hi’”, scroll back up to history, add print “hello” to 2nd line, enter, 
        /// scroll back through both function definitions
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void HistoryBackForward() {
            var interactive = Prepare();

            const string code1 = "x = 23";
            const string code2 = "y = 5";
            Keyboard.Type(code1 + "\r");

            interactive.WaitForText(ReplPrompt + code1, ReplPrompt);

            Keyboard.Type(code2 + "\r");
            interactive.WaitForText(ReplPrompt + code1, ReplPrompt + code2, ReplPrompt);

            Keyboard.Type(Key.Up);
            interactive.WaitForText(ReplPrompt + code1, ReplPrompt + code2, ReplPrompt + code2);

            Keyboard.Type(Key.Up);
            interactive.WaitForText(ReplPrompt + code1, ReplPrompt + code2, ReplPrompt + code1);

            Keyboard.Type(Key.Down);
            interactive.WaitForText(ReplPrompt + code1, ReplPrompt + code2, ReplPrompt + code2);
        }

        /// <summary>
        /// Test that maximum length of history is enforced and stores correct items.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void HistoryMaximumLength() {
            var interactive = Prepare();

            const int historyMax = 50;

            List<string> expected = new List<string>();
            for (int i = 0; i < historyMax + 1; i++) {
                string cmd = "x = " + i;
                expected.Add(ReplPrompt + cmd);
                Keyboard.Type(cmd + "\r");

                // add the empty prompt, check, then remove it
                expected.Add(ReplPrompt);
                interactive.WaitForText(expected.ToArray());
                expected.RemoveAt(expected.Count - 1);
            }

            // add an extra item for the current input which we'll update as we go through the history
            expected.Add(ReplPrompt);
            for (int i = 0; i < historyMax; i++) {
                Keyboard.Type(Key.Up);


                expected[expected.Count - 1] = expected[expected.Count - i - 2];
                interactive.WaitForText(expected.ToArray());
            }
            // end of history, one more up shouldn't do anything
            Keyboard.Type(Key.Up);
            interactive.WaitForText(expected.ToArray());
        }

        /// <summary>
        /// Test that we remember a partially typed input when we move to the history.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void HistoryUncommittedInput1() {
            var interactive = Prepare();

            const string code1 = "x = 42", code2 = "y = 100";
            Keyboard.Type(code1 + "\r");

            interactive.WaitForText(ReplPrompt + code1, ReplPrompt);

            // type, don't commit
            Keyboard.Type(code2);
            interactive.WaitForText(ReplPrompt + code1, ReplPrompt + code2);

            // move away from the input
            Keyboard.Type(Key.Up);
            interactive.WaitForText(ReplPrompt + code1, ReplPrompt + code1);

            // move back to the input
            Keyboard.Type(Key.Down);
            interactive.WaitForText(ReplPrompt + code1, ReplPrompt + code2);

            Keyboard.Type(Key.Escape);
            interactive.WaitForText(ReplPrompt + code1, ReplPrompt);
        }

        /// <summary>
        /// Test that we don't restore on submit an uncomitted input saved for history.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void HistoryUncommittedInput2() {
            var interactive = Prepare();

            Keyboard.Type("1\r");
            interactive.WaitForText(ReplPrompt + "1", "1", ReplPrompt);

            Keyboard.Type("2\r");
            interactive.WaitForText(ReplPrompt + "1", "1", ReplPrompt + "2", "2", ReplPrompt);

            Keyboard.Type("3\r");
            interactive.WaitForText(ReplPrompt + "1", "1", ReplPrompt + "2", "2", ReplPrompt + "3", "3", ReplPrompt);

            Keyboard.Type("blah");
            interactive.WaitForText(ReplPrompt + "1", "1", ReplPrompt + "2", "2", ReplPrompt + "3", "3", ReplPrompt + "blah");

            Keyboard.Type(Key.Up);
            interactive.WaitForText(ReplPrompt + "1", "1", ReplPrompt + "2", "2", ReplPrompt + "3", "3", ReplPrompt + "3");

            Keyboard.Type(Key.Enter);
            interactive.WaitForText(ReplPrompt + "1", "1", ReplPrompt + "2", "2", ReplPrompt + "3", "3", ReplPrompt + "3", "3", ReplPrompt);
        }

        /// <summary>
        /// Define function “def f():\r\n    print ‘hi’”, scroll back up to history, add print “hello” to 2nd line, enter, 
        /// scroll back through both function definitions
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void HistorySearch() {
            var interactive = Prepare();

            const string code1 = "x = 42";
            const string code2 = "x = 10042";
            const string code3 = "x = 300";
            Keyboard.Type(code1 + "\r");

            interactive.WaitForText(ReplPrompt + code1, ReplPrompt);

            Keyboard.Type(code2 + "\r");
            interactive.WaitForText(ReplPrompt + code1, ReplPrompt + code2, ReplPrompt);

            Keyboard.Type(code3 + "\r");
            interactive.WaitForText(ReplPrompt + code1, ReplPrompt + code2, ReplPrompt + code3, ReplPrompt);

            Keyboard.Type("42");
            interactive.WaitForText(ReplPrompt + code1, ReplPrompt + code2, ReplPrompt + code3, ReplPrompt + "42");

            VsIdeTestHostContext.Dte.ExecuteCommand("OtherContextMenus.InteractiveConsole.SearchHistoryPrevious");
            interactive.WaitForText(ReplPrompt + code1, ReplPrompt + code2, ReplPrompt + code3, ReplPrompt + code2);

            VsIdeTestHostContext.Dte.ExecuteCommand("OtherContextMenus.InteractiveConsole.SearchHistoryPrevious");
            interactive.WaitForText(ReplPrompt + code1, ReplPrompt + code2, ReplPrompt + code3, ReplPrompt + code1);

            VsIdeTestHostContext.Dte.ExecuteCommand("OtherContextMenus.InteractiveConsole.SearchHistoryPrevious");
            interactive.WaitForText(ReplPrompt + code1, ReplPrompt + code2, ReplPrompt + code3, ReplPrompt + code1);

            VsIdeTestHostContext.Dte.ExecuteCommand("OtherContextMenus.InteractiveConsole.SearchHistoryNext");
            interactive.WaitForText(ReplPrompt + code1, ReplPrompt + code2, ReplPrompt + code3, ReplPrompt + code2);

            VsIdeTestHostContext.Dte.ExecuteCommand("OtherContextMenus.InteractiveConsole.SearchHistoryNext");
            interactive.WaitForText(ReplPrompt + code1, ReplPrompt + code2, ReplPrompt + code3, ReplPrompt + code2);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void RegressionImportSysBackspace() {
            var item = (IPythonOptions)VsIdeTestHostContext.Dte.GetObject("VsPython");
            item.Intellisense.AddNewLineAtEndOfFullyTypedWord = true;

            var interactive = Prepare();

            const string importCode = "import sys";
            Keyboard.Type(importCode + "\r");

            interactive.WaitForText(ReplPrompt + importCode, ReplPrompt);

            Keyboard.Type("sys");

            interactive.WaitForText(ReplPrompt + importCode, ReplPrompt + "sys");

            Keyboard.Type(Key.Back);
            Keyboard.Type(Key.Back);

            interactive.WaitForText(ReplPrompt + importCode, ReplPrompt + "s");
            Keyboard.Type(Key.Back);

            interactive.WaitForText(ReplPrompt + importCode, ReplPrompt);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void RegressionImportMultipleModules() {
            var item = (IPythonOptions)VsIdeTestHostContext.Dte.GetObject("VsPython");
            item.Intellisense.AddNewLineAtEndOfFullyTypedWord = true;

            var interactive = Prepare();

            Keyboard.Type("import ");

            using (var sh = interactive.WaitForSession<ICompletionSession>()) {
                var names = sh.Session.SelectedCompletionSet.Completions.Select(c => c.DisplayText).ToList();
                var nameset = new HashSet<string>(names);

                Assert.AreEqual(names.Count, nameset.Count, "Module names were duplicated");
            }
        }

        /// <summary>
        /// Enter “while True: pass”, then hit up/down arrow, should move the caret in the edit buffer
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CursorWhileCodeIsRunning() {
            var interactive = Prepare();
            try {
                const string code = "while True: pass\r\n";
                Keyboard.Type(code);
                interactive.WaitForText(ReplPrompt + "while True: pass", SecondPrompt, "");

                Keyboard.Type(Key.Up);
                Keyboard.Type(Key.Up);
                for (int i = 0; i < ReplPrompt.Length; i++) {
                    Keyboard.Type(Key.Right);
                }

                Keyboard.PressAndRelease(Key.End, Key.LeftShift);
                Keyboard.PressAndRelease(Key.C, Key.LeftCtrl);

                System.Threading.Thread.Sleep(100);

                interactive.CancelExecution();

                if (KeyboardInterruptHasTracebackHeader) {
                    interactive.WaitForTextStart(ReplPrompt + "while True: pass", SecondPrompt, "Traceback (most recent call last):");
                } else {
                    interactive.WaitForTextStart(ReplPrompt + "while True: pass", SecondPrompt);
                }
                interactive.WaitForTextEnd("KeyboardInterrupt", ReplPrompt);

                interactive.ClearScreen();
                interactive.WaitForText(ReplPrompt);
                Keyboard.ControlV();

                interactive.WaitForText(ReplPrompt + "while True: pass");
            } finally {
                // make sure we leave the test in a reasonable state, even if the test fails.
                interactive.Reset();
            }
        }

        /// <summary>
        /// Type “raise Exception()”, hit enter, raise Exception() should have appropriate syntax color highlighting.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SyntaxHighlightingRaiseException() {
            GetInteractiveOptions().ExecutionMode = "Standard";
            var interactive = Prepare();
            try {
                const string code = "raise Exception()";
                Keyboard.Type(code + "\r");

                interactive.WaitForText(ReplPrompt + code, "Traceback (most recent call last):", "  File \"<" + SourceFileName + ">\", line 1, in <module>", "Exception", ReplPrompt);

                var snapshot = interactive.ReplWindow.TextView.TextBuffer.CurrentSnapshot;
                var span = new SnapshotSpan(snapshot, new Span(0, snapshot.Length));
                var classifications = interactive.Classifier.GetClassificationSpans(span);

                Assert.AreEqual(classifications[0].ClassificationType.Classification, PredefinedClassificationTypeNames.Keyword);
                Assert.AreEqual(classifications[1].ClassificationType.Classification, PredefinedClassificationTypeNames.Identifier);
                Assert.AreEqual(classifications[2].ClassificationType.Classification, "Python grouping");

                Assert.AreEqual(classifications[0].Span.GetText(), "raise");
                Assert.AreEqual(classifications[1].Span.GetText(), "Exception");
                Assert.AreEqual(classifications[2].Span.GetText(), "()");
            } finally {
                interactive.Reset();
            }
        }

        /// <summary>
        /// Tests entering an unknown repl commmand
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ReplCommandUnknown() {
            var interactive = Prepare();

            const string code = "$unknown";
            Keyboard.Type(code + "\r");
            interactive.WaitForReadyState();

            interactive.WaitForText(ReplPrompt + code, "Unknown command 'unknown', use \"$help\" for help", ReplPrompt);
        }


        /// <summary>
        /// Tests entering an unknown repl commmand
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ReplCommandComment() {
            var interactive = Prepare();

            const string code = "$$ quox bar baz";
            Keyboard.Type(code + "\r");
            interactive.WaitForReadyState();

            interactive.WaitForText(ReplPrompt + code, ReplPrompt);
        }

        /// <summary>
        /// Tests backspacing pass the prompt to the previous line
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void BackspacePrompt() {
            var interactive = Prepare();

            Keyboard.Type("def f():\rpass");

            interactive.WaitForText(ReplPrompt + "def f():", SecondPrompt + "    pass");

            for (int i = 0; i < 9; i++) {
                Keyboard.Type(Key.Back);
            }

            interactive.WaitForText(ReplPrompt + "def f():");

            Keyboard.Type("abc");

            interactive.WaitForText(ReplPrompt + "def f():abc");

            for (int i = 0; i < 3; i++) {
                Keyboard.Type(Key.Back);
            }

            interactive.WaitForText(ReplPrompt + "def f():");

            Keyboard.Type("\rpass");

            interactive.WaitForText(ReplPrompt + "def f():", SecondPrompt + "    pass");
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void BackspaceSmartDedent() {
            var interactive = Prepare();

            Keyboard.Type("   ");
            interactive.WaitForText(ReplPrompt + "   ");

            // smart dedent shouldn't delete 3 spaces
            Keyboard.Press(Key.Back);
            interactive.WaitForText(ReplPrompt + "  ");

            Keyboard.Type("  ");
            interactive.WaitForText(ReplPrompt + "    ");

            // spaces aren't in virtual space, we shuld delete only one
            Keyboard.Press(Key.Back);
            interactive.WaitForText(ReplPrompt + "   ");
        }

        /// <summary>
        /// Inserts code to REPL while input is accepted. 
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void InsertCode() {
            var interactive = Prepare();

            interactive.ReplWindow.InsertCode("1");
            interactive.ReplWindow.InsertCode("+");
            interactive.ReplWindow.InsertCode("2");

            interactive.WaitForText(
                ReplPrompt + "1+2"
            );

            Keyboard.Type(Key.Left);
            Keyboard.PressAndRelease(Key.Left, Key.LeftShift);

            interactive.WaitForText(
                ReplPrompt + "1+2"
            );

            interactive.ReplWindow.InsertCode("*");

            interactive.WaitForText(
                ReplPrompt + "1*2"
            );
        }

        /// <summary>
        /// Inserts code to REPL while submission execution is in progress. 
        /// The inserted input should be appended to uncommitted input and show up when the execution is finished/aborted.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void InsertCodeWhileRunning() {
            var interactive = Prepare();

            Keyboard.Type("while True: pass\r\n");
            interactive.WaitForText(ReplPrompt + "while True: pass", SecondPrompt, "");

            System.Threading.Thread.Sleep(50);
            interactive.ReplWindow.InsertCode("1");
            System.Threading.Thread.Sleep(50);
            interactive.ReplWindow.InsertCode("+");
            System.Threading.Thread.Sleep(50);
            interactive.ReplWindow.InsertCode("1");
            System.Threading.Thread.Sleep(50);

            interactive.CancelExecution();

            if (KeyboardInterruptHasTracebackHeader) {
                interactive.WaitForTextStart(ReplPrompt + "while True: pass", SecondPrompt, "Traceback (most recent call last):");
            } else {
                interactive.WaitForTextStart(ReplPrompt + "while True: pass", SecondPrompt);
            }

            interactive.WaitForTextEnd(
                "KeyboardInterrupt",
                ReplPrompt + "1+1"
            );

            Keyboard.Type(Key.Enter);

            interactive.WaitForTextEnd(
                ReplPrompt + "1+1",
                              "2",
                ReplPrompt
            );
        }

        /// <summary>
        /// Tests REPL command help
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ReplCommandHelp() {
            var interactive = Prepare();

            const string code = "$help";
            Keyboard.Type(code + "\r");

            interactive.WaitForTextStart(ReplPrompt + code, String.Format("  {0,-24}  {1}", "$help", "Show a list of REPL commands"));
        }

        /// <summary>
        /// Tests REPL command $load, with a simple script.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CommandsLoadScript() {
            var interactive = Prepare();

            string command = "$load " + TestData.GetPath(@"TestData\TestScript.txt");
            Keyboard.Type(command + "\r");

            interactive.WaitForTextStart(
                ReplPrompt + command,
                ReplPrompt + "print('hello world')",
                "hello world"
            );
        }

        /// <summary>
        /// Tests REPL command $load, with a simple script.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CommandsLoadScriptWithQuotes() {
            var interactive = Prepare();

            string command = "$load " + "\"" + TestData.GetPath(@"TestData\TestScript.txt") + "\"";
            Keyboard.Type(command + "\r");

            interactive.WaitForTextStart(
                ReplPrompt + command,
                ReplPrompt + "print('hello world')",
                "hello world"
            );
        }

        /// <summary>
        /// Tests REPL command $load, with multiple statements including a class definition.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CommandsLoadScriptWithClass() {
            // http://pytools.codeplex.com/workitem/632
            var interactive = Prepare();

            string command = "$load " + TestData.GetPath(@"TestData\TestScriptClass.txt");
            Keyboard.Type(command + "\r");

            interactive.WaitForTextStart(
                ReplPrompt + command,
                ReplPrompt + "class C(object):",
                SecondPrompt + "    pass",
                SecondPrompt,
                ReplPrompt + "c = C()",
                ReplPrompt
            );
        }

        /// <summary>
        /// Tests $load command with file that includes multiple submissions.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CommandsLoadScriptMultipleSubmissions() {
            var interactive = Prepare();

            var tempFile = Path.GetTempFileName();
            try {
                File.WriteAllText(tempFile,
@"def foo():
    print('hello')
$wait 10
%% blah
$wait 20
foo()
");
                string command = "$load " + tempFile;
                Keyboard.Type(command + "\r");

                interactive.WaitForTextStart(
                    ReplPrompt + command,
                    ReplPrompt + "def foo():",
                    SecondPrompt + "    print('hello')",
                    SecondPrompt,
                    ReplPrompt + "$wait 10",
                    ReplPrompt + "$wait 20",
                    ReplPrompt + "foo()",
                    "hello"
                );
            } finally {
                File.Delete(tempFile);
            }
        }

        /// <summary>
        /// Tests that ClearScreen doesn't cancel pending submissions queue.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CommandsLoadScriptMultipleSubmissionsWithClearScreen() {
            var interactive = Prepare();

            var tempFile = Path.GetTempFileName();
            try {
                File.WriteAllText(tempFile,
@"def foo():
    print('hello')
%% blah
1+1
$cls
1+2
");
                string command = "$load " + tempFile;
                Keyboard.Type(command + "\r");

                interactive.WaitForTextStart(
                    ReplPrompt + "1+2",
                                 "3"
                );
            } finally {
                File.Delete(tempFile);
            }
        }

        /// <summary>
        /// Tests inline images
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void InlineImage() {
            var interactive = Prepare();

            const string importSys = "import sys ";
            const string getReplModule = "repl = sys.modules['visualstudio_py_repl'].BACKEND ";
            Keyboard.Type(importSys + "\r");
            interactive.WaitForText(ReplPrompt + importSys, ReplPrompt);

            Keyboard.Type(getReplModule + "\r");

            interactive.WaitForText(ReplPrompt + importSys, ReplPrompt + getReplModule, ReplPrompt);

            interactive.ClearScreen();
            interactive.WaitForText(ReplPrompt);

            string loadImage = String.Format("repl.send_image(\"{0}\")", TestData.GetPath(@"TestData\TestImage.png").Replace("\\", "\\\\"));
            Keyboard.Type(loadImage + "\r");
            interactive.WaitForText(ReplPrompt + loadImage, "", "", ReplPrompt);

            // check that we got a tag inserted
            var compModel = (IComponentModel)VsIdeTestHostContext.ServiceProvider.GetService(typeof(SComponentModel));
            var aggFact = compModel.GetService<IViewTagAggregatorFactoryService>();
            var textview = interactive.ReplWindow.TextView;
            var aggregator = aggFact.CreateTagAggregator<IntraTextAdornmentTag>(textview);
            var snapshot = textview.TextBuffer.CurrentSnapshot;
            var tags = WaitForTags(textview, aggregator, snapshot);
            Assert.AreEqual(1, tags.Length);

            var size = tags[0].Tag.Adornment.RenderSize;

            // now add some more code to cause the image to minimize
            const string nopCode = "x = 2";
            Keyboard.Type(nopCode);
            interactive.WaitForText(ReplPrompt + loadImage, "", "", ReplPrompt + nopCode);

            Keyboard.Type(Key.Enter);
            interactive.WaitForText(ReplPrompt + loadImage, "", "", ReplPrompt + nopCode, ReplPrompt);

            // let image minimize...
            System.Threading.Thread.Sleep(200);
            for (int i = 0; i < 10; i++) {
                tags = WaitForTags(textview, aggregator, snapshot);
                Assert.AreEqual(1, tags.Length);

                var sizeTmp = tags[0].Tag.Adornment.RenderSize;
                if (sizeTmp.Height < size.Height && sizeTmp.Width < size.Width) {
                    break;
                }
                System.Threading.Thread.Sleep(200);
            }

            // make sure it's minimized
            var size2 = tags[0].Tag.Adornment.RenderSize;
            Assert.IsTrue(size2.Height < size.Height);
            Assert.IsTrue(size2.Width < size.Width);
            /*
            Point screenPoint = new Point(0, 0);
            ((UIElement)textview).Dispatcher.Invoke((Action)(() => {
                screenPoint = tags[0].Tag.Adornment.PointToScreen(new Point(10, 10));
            }));
            Mouse.MoveTo(screenPoint);

            Mouse.Click(MouseButton.Left);

            Keyboard.PressAndRelease(Key.OemPlus, Key.LeftCtrl);*/
            //Keyboard.Type(Key.Escape);
        }

        private static IMappingTagSpan<IntraTextAdornmentTag>[] WaitForTags(ITextView textView, ITagAggregator<IntraTextAdornmentTag> aggregator, ITextSnapshot snapshot) {
            IMappingTagSpan<IntraTextAdornmentTag>[] tags = null;
            ((UIElement)textView).Dispatcher.Invoke((Action)(() => {
                for (int i = 0; i < 100; i++) {
                    tags = aggregator.GetTags(new SnapshotSpan(snapshot, new Span(0, snapshot.Length))).ToArray();
                    if (tags.Length > 0) {
                        break;
                    }
                    System.Threading.Thread.Sleep(100);
                }
            }));

            return tags;
        }

        /// <summary>
        /// Tests pressing back space when to the left of the caret we have the secondary prompt.  The secondary prompt
        /// should be removed and the lines should be joined.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void EditDeleteSecondPrompt() {
            var interactive = Prepare();

            Keyboard.Type("def f():\rx = 42\ry = 100");

            interactive.WaitForText(ReplPrompt + "def f():", SecondPrompt + "    x = 42", SecondPrompt + "    y = 100");

            Keyboard.Type(Key.Home);
            Keyboard.Type(Key.Home);
            Keyboard.Type(Key.Up);
            Keyboard.Type(Key.Back);

            interactive.WaitForText(ReplPrompt + "def f():    x = 42", SecondPrompt + "    y = 100");

            Keyboard.PressAndRelease(Key.Escape);

            interactive.WaitForText(ReplPrompt);
        }

        /// <summary>
        /// Tests entering a single line of text, moving to the middle, and pressing enter.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void EditInsertInMiddleOfLine() {
            var interactive = Prepare();

            Keyboard.Type("def f(): print('hello')");
            interactive.WaitForText(ReplPrompt + "def f(): print('hello')");

            // move to left of print
            for (int i = 0; i < 14; i++) {
                Keyboard.Type(Key.Left);
            }

            Keyboard.Type(Key.Enter);

            interactive.WaitForText(ReplPrompt + "def f(): ", SecondPrompt + "    print('hello')");

            Keyboard.PressAndRelease(Key.Escape);

            interactive.WaitForText(ReplPrompt);
        }

        /// <summary>
        /// Tests using the $cls clear screen command
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ClearScreenCommand() {
            var interactive = Prepare();

            Keyboard.Type("$cls");
            interactive.WaitForText(ReplPrompt + "$cls");

            Keyboard.Type(Key.Enter);

            interactive.WaitForText(ReplPrompt);
        }

        /// <summary>
        /// Tests deleting when the secondary prompt is highlighted as part of the selection
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void EditDeleteSecondaryPromptSelected() {
            if (SecondPrompt.Length > 0) {
                for (int i = 0; i < 2; i++) {
                    var interactive = Prepare();

                    Keyboard.Type("def f():\rprint('hi')");
                    interactive.WaitForText(ReplPrompt + "def f():", SecondPrompt + "    print('hi')");

                    Keyboard.Type(Key.Home);
                    Keyboard.Type(Key.Home);
                    Keyboard.Type(Key.Left);
                    Keyboard.PressAndRelease(Key.End, Key.LeftShift);
                    if (i == 1) {
                        Keyboard.Type(Key.Back);
                    } else {
                        Keyboard.Type(Key.Delete);
                    }

                    interactive.WaitForText(ReplPrompt + "def f():", SecondPrompt);

                    Keyboard.PressAndRelease(Key.Escape);

                    interactive.WaitForText(ReplPrompt);
                }
            }
        }

        /// <summary>
        /// Tests typing when the secondary prompt is highlighted as part of the selection
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void EditTypeSecondaryPromptSelected() {
            if (SecondPrompt.Length > 0) {
                var interactive = Prepare();

                Keyboard.Type("def f():\rprint('hi')");
                interactive.WaitForText(ReplPrompt + "def f():", SecondPrompt + "    print('hi')");

                Keyboard.Type(Key.Home);
                Keyboard.Type(Key.Home);
                Keyboard.Type(Key.Left);
                Keyboard.PressAndRelease(Key.End, Key.LeftShift);
                Keyboard.Type("pass");

                interactive.WaitForText(ReplPrompt + "def f():", SecondPrompt + "pass");

                Keyboard.PressAndRelease(Key.Escape);

                interactive.WaitForText(ReplPrompt);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SelectAll() {
            var interactive = Prepare();

            // Python interactive window.  Type $help for a list of commands.
            // >>> def foo():
            // ...     print('hi')
            // ...     return 123
            // ... 
            // >>> foo()
            // hi
            // 123
            // >>> raw_input()
            // blah
            // 'blah'
            // >>> 

            Keyboard.Type("def foo():\rprint('hi')\rreturn 123\r\r");
            interactive.WaitForIdleState();
            Keyboard.Type("foo()\r");
            interactive.WaitForIdleState();
            Keyboard.Type(RawInput + "()\r");
            interactive.WaitForIdleState();
            Keyboard.Type("blah\r");
            interactive.WaitForIdleState();

            interactive.WaitForText(
                ReplPrompt + "def foo():",
                SecondPrompt + "    print('hi')",
                SecondPrompt + "    return 123",
                SecondPrompt,
                ReplPrompt + "foo()",
                "hi",
                "123",
                ReplPrompt + RawInput + "()",
                "blah",
                "'blah'",
                ReplPrompt
            );

            var text = interactive.TextView.TextBuffer.CurrentSnapshot.GetText();
            var firstPrimaryPrompt = text.IndexOf(ReplPrompt);
            var hiLiteral = text.IndexOf("'hi'");
            var firstSecondaryPrompt = text.IndexOf(SecondPrompt);
            var fooCall = text.IndexOf("foo()\r");
            var hiOutput = text.IndexOf("hi", fooCall) + 1;
            var oneTwoThreeOutput = text.IndexOf("123", fooCall) + 1;
            var blahStdIn = text.IndexOf("blah") + 2;
            var blahOutput = text.IndexOf("'blah'") + 2;

            var firstSubmission = string.Format("def foo():\r\n{0}    print('hi')\r\n{0}    return 123\r\n{0}\r\n", SecondPrompt);
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
            AssertContainingRegion(interactive, fooCall, "foo()\r\n");
            AssertContainingRegion(interactive, hiOutput, "hi\r\n123\r\n");
            AssertContainingRegion(interactive, oneTwoThreeOutput, "hi\r\n123\r\n");
            AssertContainingRegion(interactive, blahStdIn, "blah\r\n");
            AssertContainingRegion(interactive, blahOutput, "'blah'\r\n");
        }

        private void AssertContainingRegion(InteractiveWindow interactive, int position, string expectedText) {
            SnapshotSpan? span = ((ReplWindow)interactive.ReplWindow).GetContainingRegion(
                new SnapshotPoint(interactive.TextView.TextBuffer.CurrentSnapshot, position)
            );
            Assert.IsNotNull(span);
            Assert.AreEqual(expectedText, span.Value.GetText());
        }

        /// <summary>
        /// Tests typing when the secondary prompt is highlighted as part of the selection
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void EditCutIncludingPrompt() {
            if (string.IsNullOrEmpty(SecondPrompt)) {
                // Test requires secondary prompt
                return;
            }
            var interactive = Prepare();

            Keyboard.Type("def f():\rprint('hi')");
            interactive.WaitForText(ReplPrompt + "def f():", SecondPrompt + "    print('hi')");

            Keyboard.Type(Key.Home);
            Keyboard.Type(Key.Home);
            Keyboard.Type(Key.Left);
            Keyboard.PressAndRelease(Key.End, Key.LeftShift);
            Keyboard.PressAndRelease(Key.X, Key.LeftCtrl);

            interactive.WaitForText(ReplPrompt + "def f():", SecondPrompt);

            Keyboard.ControlV();

            interactive.WaitForText(ReplPrompt + "def f():", SecondPrompt + "    print('hi')");

            Keyboard.PressAndRelease(Key.Escape);

            interactive.WaitForText(ReplPrompt);
        }

        /// <summary>
        /// Tests pasting when the secondary prompt is highlighted as part of the selection
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void EditPasteSecondaryPromptSelected() {
            if (string.IsNullOrEmpty(SecondPrompt)) {
                // Test requires secondary prompt
                return;
            }
            var interactive = Prepare();

            var textview = interactive.ReplWindow.TextView;

            ((UIElement)textview).Dispatcher.Invoke((Action)(() => {
                Clipboard.SetText("    pass", TextDataFormat.Text);
            }));

            Keyboard.Type("def f():\rprint('hi')");
            interactive.WaitForText(ReplPrompt + "def f():", SecondPrompt + "    print('hi')");

            Keyboard.Type(Key.Home);
            Keyboard.Type(Key.Home);
            Keyboard.Type(Key.Left);
            Keyboard.PressAndRelease(Key.End, Key.LeftShift);
            Keyboard.ControlV();

            // >>> def f():
            // ...     print('hi')
            //    ^^^^^^^^^^^^^^^^
            // replacing selection including the prompt replaces the current line content:
            //
            // >>> def f():
            // ... pass
            interactive.WaitForText(ReplPrompt + "def f():", SecondPrompt + "    pass");

            Keyboard.PressAndRelease(Key.Escape);

            interactive.WaitForText(ReplPrompt);
        }

        /// <summary>
        /// Tests pasting when the secondary prompt is highlighted as part of the selection
        /// 
        /// Same as EditPasteSecondaryPromptSelected, but the selection is reversed so that the
        /// caret is in the prompt.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void EditPasteSecondaryPromptSelectedInPromptMargin() {
            if (string.IsNullOrEmpty(SecondPrompt)) {
                // Test requires secondary prompt
                return;
            }
            var interactive = Prepare();

            var textview = interactive.ReplWindow.TextView;

            ((UIElement)textview).Dispatcher.Invoke((Action)(() => {
                Clipboard.SetText("    pass", TextDataFormat.Text);
            }));

            Keyboard.Type("def f():\rprint('hi')");
            interactive.WaitForText(ReplPrompt + "def f():", SecondPrompt + "    print('hi')");

            Keyboard.Press(Key.LeftShift);
            Keyboard.PressAndRelease(Key.Home);
            Keyboard.Type(Key.Home);
            Keyboard.Type(Key.Left);
            Keyboard.Release(Key.LeftShift);
            Keyboard.ControlV();

            // >>> def f():
            // ...     print('hi')
            //    ^^^^^^^^^^^^^^^^
            // replacing selection including the prompt replaces the current line content:
            //
            // >>> def f():
            // ... pass
            interactive.WaitForText(ReplPrompt + "def f():", SecondPrompt + "    pass");

            Keyboard.PressAndRelease(Key.Escape);

            interactive.WaitForText(ReplPrompt);
        }

        /// <summary>
        /// Tests getting/setting the repl window options.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ReplWindowOptions() {
            if (string.IsNullOrEmpty(SecondPrompt)) {
                // Test requires secondary prompt
                return;
            }
            var interactive = Prepare();
            var window = interactive.ReplWindow;

            window.SetOptionValue(ReplOptions.CommandPrefix, "%");
            Assert.AreEqual(window.GetOptionValue(ReplOptions.CommandPrefix), "%");
            window.SetOptionValue(ReplOptions.CommandPrefix, "$");
            Assert.AreEqual(window.GetOptionValue(ReplOptions.CommandPrefix), "$");

            Assert.AreEqual(window.GetOptionValue(ReplOptions.PrimaryPrompt), ReplPrompt);
            Assert.AreEqual(window.GetOptionValue(ReplOptions.SecondaryPrompt), SecondPrompt);

            Assert.AreEqual(window.GetOptionValue(ReplOptions.DisplayPromptInMargin), false);
            Assert.AreEqual(window.GetOptionValue(ReplOptions.ShowOutput), true);

            Assert.AreEqual(window.GetOptionValue(ReplOptions.UseSmartUpDown), true);

            AssertUtil.Throws<InvalidOperationException>(
                () => window.SetOptionValue(ReplOptions.PrimaryPrompt, 42)
            );
            AssertUtil.Throws<InvalidOperationException>(
                () => window.SetOptionValue(ReplOptions.PrimaryPrompt, null)
            );
            AssertUtil.Throws<InvalidOperationException>(
                () => window.SetOptionValue((ReplOptions)(-1), null)
            );
        }

        /// <summary>
        /// Calling input while executing user code.  This should let the user start typing.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestRawInput() {
            var interactive = Prepare();

            interactive.WithStandardInputPrompt("INPUT: ", (stdInputPrompt) => {
                string inputCode = "x = " + RawInput + "()";
                string text = "hello";
                string printCode = "print(x)";
                Keyboard.Type(inputCode + "\r");
                interactive.WaitForText(ReplPrompt + inputCode, stdInputPrompt);

                Keyboard.Type(text + "\r");
                interactive.WaitForText(ReplPrompt + inputCode, stdInputPrompt + text, ReplPrompt);

                Keyboard.Type(printCode + "\r");

                interactive.WaitForText(ReplPrompt + inputCode, stdInputPrompt + text, ReplPrompt + printCode, text, ReplPrompt);
            });
        }

        /// <summary>
        /// Calling input while executing user code.  This should let the user start typing.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DeleteSelectionInRawInput() {
            var interactive = Prepare();

            try {
                interactive.WithStandardInputPrompt("INPUT: ", stdInputPrompt => {
                    Keyboard.Type(RawInput + "()\r");
                    interactive.WaitForText(ReplPrompt + RawInput + "()", stdInputPrompt);

                    Keyboard.Type("hel");
                    interactive.WaitForText(ReplPrompt + RawInput + "()", stdInputPrompt + "hel");

                    // attempt to type in the previous submission should move the cursor back to the end of the stdin:
                    Keyboard.Type(Key.Up);
                    Keyboard.Type("lo");
                    interactive.WaitForText(ReplPrompt + RawInput + "()", stdInputPrompt + "hello");

                    Keyboard.PressAndRelease(Key.Left, Key.LeftShift);
                    Keyboard.PressAndRelease(Key.Left, Key.LeftShift);
                    Keyboard.PressAndRelease(Key.Left, Key.LeftShift);
                    Keyboard.Press(Key.Delete);

                    interactive.WaitForText(ReplPrompt + RawInput + "()", stdInputPrompt + "he");
                });
            } finally {
                interactive.Reset();
            }
        }

        /// <summary>
        /// Replacing a snippet including a single line break with another snippet that also includes a single line break (line delta is zero).
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestProjectionBuffers_ZeroLineDeltaChange() {
            var interactive = Prepare();
            ManualResetEvent signal = new ManualResetEvent(false);

            interactive.DispatchAndWait(signal, () => {
                var buffer = interactive.ReplWindow.CurrentLanguageBuffer;
                buffer.Replace(new Span(0, 0), "def f():\r\n    pass");
                VerifyProjectionSpans((ReplWindow)interactive.ReplWindow);

                var snapshot = buffer.CurrentSnapshot;

                var spanToReplace = new Span("def f():".Length, "\r\n    ".Length);
                Assert.AreEqual("\r\n    ", snapshot.GetText(spanToReplace));

                using (var edit = buffer.CreateEdit(EditOptions.None, null, null)) {
                    edit.Replace(spanToReplace.Start, spanToReplace.Length, "\r\n");
                    edit.Apply();
                }

                VerifyProjectionSpans((ReplWindow)interactive.ReplWindow);
            });
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

        /// <summary>
        /// Pressing delete with no text selected, it should delete the proceeding character.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestDelNoTextSelected() {
            var interactive = Prepare();

            string inputCode = "abc";
            Keyboard.Type(inputCode);
            interactive.WaitForText(ReplPrompt + inputCode);

            Keyboard.Type(Key.Home);
            Keyboard.Type(Key.Delete);

            interactive.WaitForText(ReplPrompt + "bc");
        }

        /// <summary>
        /// Pressing delete with no text selected, it should delete the proceeding character.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestDelAtEndOfLine() {
            var interactive = Prepare();

            string inputCode = "def f():";
            const string autoIndent = "    ";
            Keyboard.Type(inputCode);
            interactive.WaitForText(ReplPrompt + inputCode);

            Keyboard.Type("\r");
            interactive.WaitForText(ReplPrompt + inputCode, SecondPrompt);

            string printCode = "print('hello')";
            Keyboard.Type(printCode);
            interactive.WaitForText(ReplPrompt + inputCode, SecondPrompt + autoIndent + printCode);

            // go to end of 1st line
            Keyboard.Type(Key.Home);
            Keyboard.Type(Key.Home);
            Keyboard.Type(Key.Up);
            Keyboard.Type(Key.End);

            // press delete
            Keyboard.Type(Key.Delete);

            interactive.WaitForText(ReplPrompt + inputCode + autoIndent + printCode);
        }

        /// <summary>
        /// Pressing delete with no text selected, it should delete the proceeding character.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestDelAtEndOfBuffer() {
            var interactive = Prepare();

            string inputCode = "def f():";
            const string autoIndent = "    ";
            Keyboard.Type(inputCode);
            interactive.WaitForText(ReplPrompt + inputCode);

            Keyboard.Type("\r");
            interactive.WaitForText(ReplPrompt + inputCode, SecondPrompt);

            string printCode = "print('hello')";
            Keyboard.Type(printCode);
            interactive.WaitForText(ReplPrompt + inputCode, SecondPrompt + autoIndent + printCode);

            Keyboard.Type(Key.Delete);
            interactive.WaitForText(ReplPrompt + inputCode, SecondPrompt + autoIndent + printCode);
        }

        /// <summary>
        /// Type some text that leaves auto-indent at the end of the input and also outputs, make sure the auto indent (regression for http://pytools.codeplex.com/workitem/92)
        /// is gone before we do the input.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestPrintWithParens() {
            var interactive = Prepare();

            string inputCode = "print ('a',";
            const string autoIndent = "       ";
            Keyboard.Type(inputCode);
            interactive.WaitForText(ReplPrompt + inputCode);
            Keyboard.Type("\r");
            interactive.WaitForText(ReplPrompt + inputCode, SecondPrompt);
            const string b = "'b',";
            Keyboard.Type(b + "\r");
            interactive.WaitForText(ReplPrompt + inputCode, SecondPrompt + autoIndent + b, SecondPrompt);
            const string c = "'c')";
            Keyboard.Type(c + "\r");
            interactive.WaitForText(ReplPrompt + inputCode, SecondPrompt + autoIndent + b, SecondPrompt + autoIndent + c, SecondPrompt);
            Keyboard.Type(Key.Back);    // remove prompt, we should be indented at same level as the print statement
            interactive.WaitForText(ReplPrompt + inputCode, SecondPrompt + autoIndent + b, SecondPrompt + autoIndent + c);
        }

        /// <summary>
        /// Make sure that we can successfully delete an autoindent inputted span (regression for http://pytools.codeplex.com/workitem/93)
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestUndeletableIndent() {
            var interactive = Prepare();

            string inputCode = "print ('a',";
            const string autoIndent = "       ";
            Keyboard.Type(inputCode);
            interactive.WaitForText(ReplPrompt + inputCode);
            Keyboard.Type("\r");
            interactive.WaitForText(ReplPrompt + inputCode, SecondPrompt);
            const string b = "'b',";
            Keyboard.Type(b + "\r");
            interactive.WaitForText(ReplPrompt + inputCode, SecondPrompt + autoIndent + b, SecondPrompt);
            const string c = "'c')";
            Keyboard.Type(c + "\r");
            interactive.WaitForText(ReplPrompt + inputCode, SecondPrompt + autoIndent + b, SecondPrompt + autoIndent + c, SecondPrompt);
            Keyboard.Type("\r");

            interactive.WaitForText(ReplPrompt + inputCode, SecondPrompt + autoIndent + b, SecondPrompt + autoIndent + c, SecondPrompt, PrintAbcOutput, ReplPrompt);
        }

        protected virtual string PrintAbcOutput {
            get {
                return "('a', 'b', 'c')";
            }
        }

        /// <summary>
        /// Pressing delete with no text selected, it should delete the proceeding character.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestDelInOutput() {
            var interactive = Prepare();

            string inputCode = "print('abc')";
            Keyboard.Type(inputCode);
            interactive.WaitForText(ReplPrompt + inputCode);
            Keyboard.Type("\r");
            interactive.WaitForText(ReplPrompt + inputCode, "abc", ReplPrompt);

            Keyboard.Type(Key.Left);
            Keyboard.Type(Key.Up);
            Keyboard.Type(Key.Delete);

            interactive.WaitForText(ReplPrompt + inputCode, "abc", ReplPrompt);
        }

        /// <summary>
        /// Calling ReadInput while no code is running - this should remove the prompt and let the user type input
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestIndirectInput() {
            var interactive = Prepare();

            interactive.WithStandardInputPrompt("INPUT: ", (stdInputPrompt) => {
                string text = null;
                ThreadPool.QueueUserWorkItem(
                    x => {
                        text = interactive.ReplWindow.ReadStandardInput();
                    });


                // prompt should disappear
                interactive.WaitForText(stdInputPrompt);

                Keyboard.Type("abc");
                interactive.WaitForText(stdInputPrompt + "abc");
                Keyboard.Type("\r");
                interactive.WaitForText(stdInputPrompt + "abc", ReplPrompt);

                Assert.AreEqual(text, "abc\r\n");
            });
        }

        /// <summary>
        /// Simple test case of Ipython execution mode.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestIPythonMode() {
            if (!IPythonSupported) {
                // Requires IPython
                return;
            }

            GetInteractiveOptions().ExecutionMode = "IPython";
            InteractiveWindow interactive = null;
            try {
                interactive = Prepare();
                string assignCode = "x = 42";
                string inspectCode = "?x";
                Keyboard.Type(assignCode + "\r");

                interactive.WaitForText(ReplPrompt + assignCode, ReplPrompt);
                interactive.WaitForReadyState();

                Keyboard.Type(inspectCode + "\r");
                interactive.WaitForText(new[] { ReplPrompt + assignCode, ReplPrompt + inspectCode }.Concat(IntDocumentation).Concat(new[] { ReplPrompt }).ToArray());
            } finally {
                GetInteractiveOptions().ExecutionMode = "Standard";
                ForceReset();
            }
        }

        /// <summary>
        /// Simple test case of Ipython execution mode.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestIPythonCtrlBreakAborts() {
            if (!IPythonSupported) {
                // Requires IPython
                return;
            }

            GetInteractiveOptions().ExecutionMode = "IPython";
            InteractiveWindow interactive = null;
            try {
                interactive = Prepare();
                const string code = "while True: pass\r\n";
                Keyboard.Type(code);
                interactive.WaitForText(ReplPrompt + "while True: pass", SecondPrompt, "");

                System.Threading.Thread.Sleep(2000);

                interactive.CancelExecution();

                // we can potentially get different output depending on where the Ctrl-C gets caught.
                bool failedFirstCheck = false;
                try {
                    interactive.WaitForTextStart(ReplPrompt + "while True: pass", SecondPrompt,
                        "KeyboardInterrupt caught in kernel");
                } catch {
                    failedFirstCheck = true;
                }

                if (failedFirstCheck) {
                    interactive.WaitForTextStart(ReplPrompt + "while True: pass", SecondPrompt,
                        "---------------------------------------------------------------------------",
                        "KeyboardInterrupt                         Traceback (most recent call last)");
                }

            } finally {
                GetInteractiveOptions().ExecutionMode = "Standard";
                ForceReset();
            }
        }

        /// <summary>
        /// “x = 42”
        /// “x.” should bring up intellisense completion
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void IPythonSimpleCompletion() {
            if (!IPythonSupported) {
                // Requires IPython
                return;
            }

            PythonToolsPackage.Instance.AdvancedEditorOptionsPage.AddNewLineAtEndOfFullyTypedWord = false;
            GetInteractiveOptions().ExecutionMode = "IPython";
            InteractiveWindow interactive = null;
            try {
                interactive = Prepare();
                const string code = "x = 42";
                Keyboard.Type(code + "\r");

                interactive.WaitForText(ReplPrompt + code, ReplPrompt);
                interactive.WaitForReadyState();

                Keyboard.Type("x.");

                interactive.WaitForText(ReplPrompt + code, ReplPrompt + "x.");

                using (var sh = interactive.WaitForSession<ICompletionSession>()) {
                    StringBuilder completions = new StringBuilder();
                    completions.AppendLine(sh.Session.SelectedCompletionSet.DisplayName);

                    foreach (var completion in sh.Session.SelectedCompletionSet.Completions) {
                        completions.Append(completion.InsertionText);
                    }

                    string x = completions.ToString();

                    // commit entry
                    Keyboard.PressAndRelease(Key.Tab);
                    interactive.WaitForText(ReplPrompt + code, ReplPrompt + "x." + IntFirstMember);
                    interactive.WaitForSessionDismissed();
                }

                // clear input at repl
                Keyboard.PressAndRelease(Key.Escape);

                // try it again, and dismiss the session
                Keyboard.Type("x.");
                using (interactive.WaitForSession<ICompletionSession>()) {
                    Keyboard.PressAndRelease(Key.Escape);
                    interactive.WaitForSessionDismissed();
                }
            } finally {
                GetInteractiveOptions().ExecutionMode = "Standard";
                ForceReset();
            }
        }

        /// <summary>
        /// “def f(): pass” + 2 ENTERS
        /// f( should bring signature help up
        /// 
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void IPythonSimpleSignatureHelp() {
            if (!IPythonSupported) {
                // Requires IPython
                return;
            }

            GetInteractiveOptions().ExecutionMode = "IPython";
            InteractiveWindow interactive = null;
            try {
                interactive = Prepare();
                Assert.IsNotNull(interactive);

                const string code = "def f(): pass";
                Keyboard.Type(code + "\r\r");

                interactive.WaitForText(ReplPrompt + code, SecondPrompt, ReplPrompt);

                System.Threading.Thread.Sleep(1000);

                Keyboard.Type("f(");

                interactive.WaitForText(ReplPrompt + code, SecondPrompt, ReplPrompt + "f(");

                using (var sh = interactive.WaitForSession<ISignatureHelpSession>()) {
                    Assert.AreEqual("<no docstring>", sh.Session.SelectedSignature.Documentation);

                    Keyboard.PressAndRelease(Key.Escape);

                    interactive.WaitForSessionDismissed();
                }
            } finally {
                GetInteractiveOptions().ExecutionMode = "Standard";
                ForceReset();
            }
        }

        /// <summary>
        /// Simple test case of Ipython execution mode.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestIPythonInlineGraph() {
            if (!IPythonSupported) {
                // Requires IPython
                return;
            }

            GetInteractiveOptions().ExecutionMode = "IPython";
            InteractiveWindow interactive = null;
            try {
                interactive = Prepare();

                var replWindow = interactive.ReplWindow;
                replWindow.Submit(new[] { "from pylab import *", "x = linspace(0, 4*pi)", "plot(x, x)" });
                interactive.WaitForTextStart(new[] { ReplPrompt + "from pylab import *", ReplPrompt + "x = linspace(0, 4*pi)", ReplPrompt + "plot(x, x)", "Out[" });

                System.Threading.Thread.Sleep(2000);

                var compModel = (IComponentModel)VsIdeTestHostContext.ServiceProvider.GetService(typeof(SComponentModel));
                var aggFact = compModel.GetService<IViewTagAggregatorFactoryService>();
                var textview = interactive.ReplWindow.TextView;
                var aggregator = aggFact.CreateTagAggregator<IntraTextAdornmentTag>(textview);
                var snapshot = textview.TextBuffer.CurrentSnapshot;
                var tags = WaitForTags(textview, aggregator, snapshot);
                Assert.AreEqual(1, tags.Length);

            } finally {
                GetInteractiveOptions().ExecutionMode = "Standard";
                ForceReset();
            }
        }

        /// <summary>
        /// Simple test case of Ipython Debug->Execute in Interactive which then accesses __file__
        /// during the script.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestIPythonStartInInteractive() {
            if (!IPythonSupported) {
                // Requires IPython
                return;
            }


            GetInteractiveOptions().ExecutionMode = "IPython";
            InteractiveWindow interactive = null;
            try {
                interactive = Prepare();
                using (new DefaultInterpreterSetter(interactive.TextView.GetAnalyzer().InterpreterFactory))
                using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                    var project = app.OpenProject(@"TestData\InteractiveFile.sln");

                    VsIdeTestHostContext.Dte.ExecuteCommand("Debug.ExecuteFileinPythonInteractive");
                    Assert.AreNotEqual(null, interactive);

                    interactive.WaitForTextEnd("Program.pyabcdef", ReplPrompt);
                }
            } finally {
                GetInteractiveOptions().ExecutionMode = "Standard";
                ForceReset();
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ExecuteInReplSysArgv() {
            var interactive = Prepare();
            using (new DefaultInterpreterSetter(interactive.TextView.GetAnalyzer().InterpreterFactory))
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                var project = app.OpenProject(@"TestData\SysArgvRepl.sln");

                VsIdeTestHostContext.Dte.ExecuteCommand("Debug.ExecuteFileinPythonInteractive");
                Assert.AreNotEqual(null, interactive);

                interactive.WaitForTextEnd("Program.py']", ReplPrompt);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ExecuteInReplSysArgvScriptArgs() {
            var interactive = Prepare();
            using (new DefaultInterpreterSetter(interactive.TextView.GetAnalyzer().InterpreterFactory))
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                var project = app.OpenProject(@"TestData\SysArgvScriptArgsRepl.sln");

                VsIdeTestHostContext.Dte.ExecuteCommand("Debug.ExecuteFileinPythonInteractive");
                Assert.AreNotEqual(null, interactive);

                interactive.WaitForTextEnd(@"Program.py', '-source', 'C:\\Projects\\BuildSuite', '-destination', 'C:\\Projects\\TestOut', '-pattern', '*.txt', '-recurse', 'true']", ReplPrompt);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ExecuteInIPythonReplSysArgv() {
            if (!IPythonSupported) {
                // Requires IPython
                return;
            }

            GetInteractiveOptions().ExecutionMode = "IPython";
            InteractiveWindow interactive;
            try {
                interactive = Prepare();
                using (new DefaultInterpreterSetter(interactive.TextView.GetAnalyzer().InterpreterFactory))
                using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                    var project = app.OpenProject(@"TestData\SysArgvRepl.sln");

                    VsIdeTestHostContext.Dte.ExecuteCommand("Debug.ExecuteFileinPythonInteractive");
                    Assert.AreNotEqual(null, interactive);

                    interactive.WaitForTextEnd("Program.py']", ReplPrompt);
                }
            } finally {
                GetInteractiveOptions().ExecutionMode = "Standard";
                ForceReset();
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ExecuteInIPythonReplSysArgvScriptArgs() {
            if (!IPythonSupported) {
                // Requires IPython
                return;
            }

            GetInteractiveOptions().ExecutionMode = "IPython";
            InteractiveWindow interactive;
            try {
                interactive = Prepare();
                using (new DefaultInterpreterSetter(interactive.TextView.GetAnalyzer().InterpreterFactory))
                using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                    var project = app.OpenProject(@"TestData\SysArgvScriptArgsRepl.sln");

                    VsIdeTestHostContext.Dte.ExecuteCommand("Debug.ExecuteFileinPythonInteractive");
                    Assert.AreNotEqual(null, interactive);

                    interactive.WaitForTextEnd(@"Program.py', '-source', 'C:\\Projects\\BuildSuite', '-destination', 'C:\\Projects\\TestOut', '-pattern', '*.txt', '-recurse', 'true']", ReplPrompt);
                }
            } finally {
                GetInteractiveOptions().ExecutionMode = "Standard";
                ForceReset();
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ExecuteInReplUnicodeFilename() {
            var interactive = Prepare();
            using (new DefaultInterpreterSetter(interactive.TextView.GetAnalyzer().InterpreterFactory))
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                var project = app.OpenProject(@"TestData\UnicodePathä.sln");

                VsIdeTestHostContext.Dte.ExecuteCommand("Debug.ExecuteFileinPythonInteractive");
                Assert.AreNotEqual(null, interactive);

                interactive.WaitForTextEnd("hello world from unicode path", ReplPrompt);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AttachReplTest() {
            using (var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte)) {
                var project = app.OpenProject(@"TestData\DebuggerProject.sln");
                PythonToolsPackage.Instance.AdvancedEditorOptionsPage.AddNewLineAtEndOfFullyTypedWord = true;
                GetInteractiveOptions().EnableAttach = true;
                try {
                    var interactive = Prepare();

                    using (new DefaultInterpreterSetter(interactive.TextView.GetAnalyzer().InterpreterFactory)) {
                        const string attachCmd = "$attach";
                        Keyboard.Type(attachCmd + "\r");

                        VsIdeTestHostContext.Dte.Debugger.Breakpoints.Add(File: "BreakpointTest.py", Line: 1);
                        interactive.WaitForText(ReplPrompt + attachCmd, ReplPrompt);

                        DebuggerUITests.DebugProject.WaitForMode(EnvDTE.dbgDebugMode.dbgRunMode);

                        interactive = Prepare(reopenOnly: true);

                        const string import = "import BreakpointTest";
                        Keyboard.Type(import + "\r");
                        interactive.WaitForText(ReplPrompt + attachCmd, ReplPrompt + import, "");

                        DebuggerUITests.DebugProject.WaitForMode(EnvDTE.dbgDebugMode.dbgBreakMode);

                        Assert.AreEqual(VsIdeTestHostContext.Dte.Debugger.BreakpointLastHit.FileLine, 1);

                        VsIdeTestHostContext.Dte.ExecuteCommand("Debug.DetachAll");

                        DebuggerUITests.DebugProject.WaitForMode(EnvDTE.dbgDebugMode.dbgDesignMode);

                        interactive.WaitForText(ReplPrompt + attachCmd, ReplPrompt + import, "hello", ReplPrompt);
                    }
                } finally {
                    PythonToolsPackage.Instance.AdvancedEditorOptionsPage.AddNewLineAtEndOfFullyTypedWord = false;
                    GetInteractiveOptions().EnableAttach = false;
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Comments() {
            var interactive = Prepare();

            const string code = "# foo";
            Keyboard.Type(code + "\r");

            interactive.WaitForText(ReplPrompt + code, SecondPrompt);

            const string code2 = "# bar";
            Keyboard.Type(code2 + "\r");

            interactive.WaitForText(ReplPrompt + code, SecondPrompt + code2, SecondPrompt);

            Keyboard.Type("\r");
            interactive.WaitForText(ReplPrompt + code, SecondPrompt + code2, SecondPrompt, ReplPrompt);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CommentPaste() {
            var interactive = Prepare();

            const string comment = "# foo bar baz";
            PasteTextTest(
                interactive,
                comment,
                ReplPrompt + comment
            );

            PasteTextTest(
                interactive,
                comment + "\r\n",
                ReplPrompt + comment
            );

            PasteTextTest(
                interactive,
                comment + "\r\ndef f():\r\n    pass",
                ReplPrompt + comment, SecondPrompt + "def f():", SecondPrompt + "    pass"
            );

            PasteTextTest(
                interactive,
                comment + "\r\n" + comment,
                ReplPrompt + comment, SecondPrompt + comment
            );

            PasteTextTest(
                interactive,
                comment + "\r\n" + comment + "\r\n",
                ReplPrompt + comment, SecondPrompt + comment, SecondPrompt
            );
        }

        /// <summary>
        /// def f(): pass
        /// 
        /// X
        /// 
        /// def g(): pass
        /// 
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void PasteWithError() {
            var interactive = Prepare();

            const string code = @"def f(): pass

X

def g(): pass

";
            PasteTextTest(
                interactive,
                code,
                ReplPrompt + "def f(): pass",
                SecondPrompt,
                ReplPrompt + "X",
                "Traceback (most recent call last):",
                "  File \"<" + SourceFileName + ">\", line 1, in <module>",
                "NameError: name 'X' is not defined",
                ReplPrompt
            );

        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void QuitAndReset() {
            var interactive = Prepare();
            Keyboard.Type("quit()\n");
            interactive.WaitForTextEnd("The Python REPL process has exited", ReplPrompt);
            interactive.Reset();

            interactive.WaitForTextEnd("Resetting execution engine", ReplPrompt);
            Keyboard.Type("42\n");

            interactive.WaitForTextEnd(ReplPrompt + "42", "42", ReplPrompt);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ImportCompletions() {
            var interactive = Prepare();

#if PY_ALL || PY_IRON27
            if (this is IronPythonReplTests) {
                Keyboard.Type("import clr \n");
            }
#endif

            Keyboard.Type("import ");
            using (var sh = interactive.WaitForSession<ICompletionSession>()) {
                var names = sh.Session.SelectedCompletionSet.Completions.Select(c => c.DisplayText).ToList();

                foreach (var name in names) {
                    Console.WriteLine(name);
                }
                foreach (var name in names) {
                    Assert.IsFalse(name.Contains('.'), name + " contained a dot");
                }
            }

            Keyboard.Type("os.");
            using (var sh = interactive.WaitForSession<ICompletionSession>()) {
                var names = sh.Session.SelectedCompletionSet.Completions.Select(c => c.DisplayText).ToList();
                AssertUtil.ContainsExactly(names, "path");
            }

            interactive.ClearScreen();
        }


        private void PasteTextTest(InteractiveWindow interactive, string code, params string[] expected) {
            ((UIElement)interactive.TextView).Dispatcher.Invoke((Action)(() => {
                Clipboard.SetText(code, TextDataFormat.Text);
            }));
            Keyboard.ControlV();
            interactive.WaitForText(expected);
            Keyboard.PressAndRelease(Key.Escape);
        }

        protected override string InterpreterDescription {
            get { return "Python 2.6 Interactive"; }
        }

        protected override PythonVersion PythonVersion {
            get {
                return PythonPaths.Python26;
            }
        }
    }




#if PY_ALL || PY_27 || PY_IRON27
    [TestClass]
    public class Python27ReplWindowTests : Python26ReplWindowTests {
        [TestInitialize]
        public new void Initialize() {
#if !(PY_ALL || PY_27)
            // Because this is the base class for the IronPython tests, we
            // can't exclude it from that build. We'll just skip all the tests.
            Assert.Inconclusive("Test not part of this run");
#endif
            TestInitialize();
        }

        /// <summary>
        /// https://pytools.codeplex.com/workitem/869
        /// REPL adds extra new lines
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void NewLinesOverTime() {
            var interactive = Prepare();

            Assert.AreNotEqual(null, interactive);

            const string code = "def f():\rprint 42,\rimport time \rtime.sleep(1)\rprint 100,\r\rf()\r";
            Keyboard.Type(code);

            interactive.WaitForTextEnd("42 100", ReplPrompt);
        }

        /// <summary>
        /// Test Ipython execution mode supporting multiline paste
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestIPythonMultiStatementPaste() {
            if (!IPythonSupported) {
                // Requires IPython
                return;
            }

            GetInteractiveOptions().ExecutionMode = "IPython";
            InteractiveWindow interactive = null;
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                try {
                    var project = app.OpenProject(@"TestData\Repl.sln");

                    var program = project.ProjectItems.Item("Program.py");

                    interactive = Prepare();

                    var window = program.Open();
                    window.Activate();
                    var doc = app.GetDocument(program.Document.FullName);
                    doc.Invoke(() =>
                        doc.TextView.Selection.Select(
                            new SnapshotSpan(
                                doc.TextView.TextBuffer.CurrentSnapshot,
                                0,
                                doc.TextView.TextBuffer.CurrentSnapshot.Length
                            ),
                            false
                        )
                    );

                    VsIdeTestHostContext.Dte.ExecuteCommand("Edit.SendtoInteractive");

                    interactive.WaitForText(
                        ReplPrompt + "def f():",
                        SecondPrompt + "    return 42",
                        SecondPrompt,
                        SecondPrompt + "100",
                        SecondPrompt,
                        "Out[2]: 100",
                        ReplPrompt
                    );
                } finally {
                    GetInteractiveOptions().ExecutionMode = "Standard";
                    ForceReset();
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void VirtualEnvironmentSendToInteractive() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                var project = app.OpenProject(@"TestData\VirtualEnv.sln");

                var program = project.ProjectItems.Item("Program.py");

                var window = program.Open();
                window.Activate();
                var doc = app.GetDocument(program.Document.FullName);
                doc.Invoke(() =>
                    doc.TextView.Selection.Select(
                        new SnapshotSpan(
                            doc.TextView.TextBuffer.CurrentSnapshot,
                            0,
                            doc.TextView.TextBuffer.CurrentSnapshot.Length
                        ),
                        false
                    )
                );

                // This is required to open the window, since VS automation is
                // currently unable to find virtual environments. Once the window
                // is open, UI automation will find it by the description.
                VsIdeTestHostContext.Dte.ExecuteCommand("Edit.SendtoInteractive");

                var interactive = Prepare(description: "env (Python 2.7) Interactive");
                VsIdeTestHostContext.Dte.ExecuteCommand("Edit.SendtoInteractive");

                var expectedText = new List<string>();

                using (var reader = TestData.Read(@"TestData\VirtualEnv\Program.py")) {
                    for (var line = reader.ReadLine(); line != null; line = reader.ReadLine()) {
                        expectedText.Add(ReplPrompt + line);
                    }
                }
                expectedText.Add(TestData.GetPath(@"TestData\VirtualEnv\env\Scripts\python.exe"));
                expectedText.Add(ReplPrompt);

                interactive.WaitForText(expectedText);
            }
        }

        protected override string InterpreterDescription {
            get {
                return "Python 2.7 Interactive";
            }
        }

        protected override PythonVersion PythonVersion {
            get {
                return PythonPaths.Python27;
            }
        }

        protected override string IntFirstMember {
            get {
                // bit_length was added in 2.7
                return "bit_length";
            }
        }
    }
#endif

    public abstract class Python3kReplWindowTests : Python26ReplWindowTests {
        protected override string RawInput {
            get {
                return "input";
            }
        }

        protected override string IntFirstMember {
            get {
                return "bit_length";
            }
        }

        protected override string UnicodeStringPrefix {
            get {
                return "";
            }
        }

        protected override string PrintAbcOutput {
            get {
                return "a b c";
            }
        }

        protected override string Print42Output {
            get {
                return "42\r";
            }
        }

        protected override IEnumerable<string> IntDocumentation {
            get {
                yield return "Type:       int";
                //yield return "Base Class: <type 'int'>";
                yield return "String Form:42";
                //yield return "AnalysisValue:  Interactive";
                yield return "Docstring:";
                yield return "int(x[, base]) -> integer";
                yield return "";
                yield return "Convert a string or number to an integer, if possible.  A floating";
                yield return "point argument will be truncated towards zero (this does not include a";
                yield return "string representation of a floating point number!)  When converting a";
                yield return "string, use the optional base.  It is an error to supply a base when";
                yield return "converting a non-string.";
            }
        }
    }



#if PY_ALL || PY_30
    [TestClass]
    public class Python30ReplWindowTests : Python3kReplWindowTests {
        [TestInitialize]
        public new void Initialize() {
            TestInitialize();
        }

        protected override string InterpreterDescription {
            get {
                return "Python 3.0 Interactive";
            }
        }

        protected override PythonVersion PythonVersion {
            get {
                return PythonPaths.Python30;
            }
        }

        protected override bool IPythonSupported {
            get {
                return false;
            }
        }
    }
#endif

#if PY_ALL || PY_31
    [TestClass]
    public class Python31ReplWindowTests : Python3kReplWindowTests {
        [TestInitialize]
        public new void Initialize() {
            TestInitialize();
        }

        protected override string InterpreterDescription {
            get {
                return "Python 3.1 Interactive";
            }
        }

        protected override PythonVersion PythonVersion {
            get {
                return PythonPaths.Python31;
            }
        }

        protected override bool IPythonSupported {
            get {
                return false;
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ExecuteProjectUnicodeFile() {
            var interactive = Prepare();
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                var project = app.OpenProject(@"TestData\UnicodeRepl.sln");

                VsIdeTestHostContext.Dte.ExecuteCommand("Debug.ExecuteFileinPythonInteractive");
                Assert.AreNotEqual(null, interactive);

                interactive.WaitForTextEnd("Hello, world!", ReplPrompt);
            }
        }
    }
#endif


#if PY_ALL || PY_32
    [TestClass]
    public class Python32ReplWindowTests : Python3kReplWindowTests {
        [TestInitialize]
        public new void Initialize() {
            TestInitialize();
        }

        protected override string InterpreterDescription {
            get {
                return "Python 3.2 Interactive";
            }
        }

        protected override PythonVersion PythonVersion {
            get {
                return PythonPaths.Python32;
            }
        }
    }
#endif

#if PY_ALL || PY_33
    [TestClass]
    public class Python33ReplWindowTests : Python3kReplWindowTests {
        [TestInitialize]
        public new void Initialize() {
            TestInitialize();
        }

        protected override string InterpreterDescription {
            get {
                return "Python 3.3 Interactive";
            }
        }

        protected override PythonVersion PythonVersion {
            get {
                return PythonPaths.Python33;
            }
        }
    }
#endif

#if PY_ALL || PY_34
    [TestClass]
    public class Python34ReplWindowTests : Python3kReplWindowTests {
        [TestInitialize]
        public new void Initialize() {
            TestInitialize();
        }

        protected override string InterpreterDescription {
            get {
                return "Python 3.4 Interactive";
            }
        }

        protected override PythonVersion PythonVersion {
            get {
                return PythonPaths.Python34;
            }
        }
    }
#endif



#if PY_ALL || PY_26
    [TestClass]
    public class Python26x64ReplWindowTests : Python26ReplWindowTests {
        [TestInitialize]
        public new void Initialize() {
            TestInitialize();
        }

        protected override string InterpreterDescription {
            get {
                return "Python 64-bit 2.6 Interactive";
            }
        }

        protected override PythonVersion PythonVersion {
            get {
                return PythonPaths.Python26_x64;
            }
        }
    }
#endif

#if PY_ALL || PY_27
    [TestClass]
    public class Python27x64ReplWindowTests : Python27ReplWindowTests {
        [TestInitialize]
        public new void Initialize() {
            TestInitialize();
        }

        protected override string InterpreterDescription {
            get {
                return "Python 64-bit 2.7 Interactive";
            }
        }

        protected override PythonVersion PythonVersion {
            get {
                return PythonPaths.Python27_x64;
            }
        }
    }
#endif

#if PY_ALL || PY_30
    [TestClass]
    public class Python30x64ReplWindowTests : Python30ReplWindowTests {
        [TestInitialize]
        public new void Initialize() {
            TestInitialize();
        }

        protected override string InterpreterDescription {
            get {
                return "Python 64-bit 3.0 Interactive";
            }
        }

        protected override PythonVersion PythonVersion {
            get {
                return PythonPaths.Python30_x64;
            }
        }
    }
#endif

#if PY_ALL || PY_31
    [TestClass]
    public class Python31x64ReplWindowTests : Python31ReplWindowTests {
        [TestInitialize]
        public new void Initialize() {
            TestInitialize();
        }

        protected override string InterpreterDescription {
            get {
                return "Python 64-bit 3.1 Interactive";
            }
        }

        protected override PythonVersion PythonVersion {
            get {
                return PythonPaths.Python31_x64;
            }
        }
    }
#endif

#if PY_ALL || PY_32
    [TestClass]
    public class Python32x64ReplWindowTests : Python32ReplWindowTests {
        [TestInitialize]
        public new void Initialize() {
            TestInitialize();
        }

        protected override string InterpreterDescription {
            get {
                return "Python 64-bit 3.2 Interactive";
            }
        }

        protected override PythonVersion PythonVersion {
            get {
                return PythonPaths.Python32_x64;
            }
        }
    }
#endif

#if PY_ALL || PY_33
    [TestClass]
    public class Python33x64ReplWindowTests : Python33ReplWindowTests {
        [TestInitialize]
        public new void Initialize() {
            TestInitialize();
        }

        protected override string InterpreterDescription {
            get {
                return "Python 64-bit 3.3 Interactive";
            }
        }

        protected override PythonVersion PythonVersion {
            get {
                return PythonPaths.Python33_x64;
            }
        }
    }
#endif

#if PY_ALL || PY_34
    [TestClass]
    public class Python34x64ReplWindowTests : Python34ReplWindowTests {
        [TestInitialize]
        public new void Initialize() {
            TestInitialize();
        }

        protected override string InterpreterDescription {
            get {
                return "Python 64-bit 3.4 Interactive";
            }
        }

        protected override PythonVersion PythonVersion {
            get {
                return PythonPaths.Python34_x64;
            }
        }
    }
#endif

#if PY_ALL || PY_27
    [TestClass]
    public class PrimaryPromptOnlyPython27ReplWindowTests : Python27ReplWindowTests {
        [TestInitialize]
        public new void Initialize() {
            TestInitialize();
        }

        protected override string ReplPrompt {
            get {
                return "> ";
            }
        }

        protected override string SecondPrompt {
            get {
                return "";
            }
        }

        protected override void ConfigurePrompts() {
            var options = GetInteractiveOptions();
            options.InlinePrompts = true;
            options.UseInterpreterPrompts = false;
            options.PrimaryPrompt = ReplPrompt;
            options.SecondaryPrompt = SecondPrompt;
        }
    }

    [TestClass]
    public class GlyphPromptPython27ReplWindowTests : Python27ReplWindowTests {
        [TestInitialize]
        public new void Initialize() {
            TestInitialize();
        }

        protected override string ReplPrompt {
            get {
                return "";
            }
        }

        protected override string SecondPrompt {
            get {
                return "";
            }
        }

        protected override bool IPythonSupported {
            get {
                return false;
            }
        }

        protected override void ConfigurePrompts() {
            var options = GetInteractiveOptions();
            options.InlinePrompts = false;
            options.UseInterpreterPrompts = false;
            options.PrimaryPrompt = ">";
            options.SecondaryPrompt = "*";
        }
    }
#endif

#if PY_ALL || PY_33
    [TestClass]
    public class PrimaryPromptOnlyPython33ReplWindowTests : Python33ReplWindowTests {
        [TestInitialize]
        public new void Initialize() {
            TestInitialize();
        }

        protected override string ReplPrompt {
            get {
                return "> ";
            }
        }

        protected override string SecondPrompt {
            get {
                return "";
            }
        }

        protected override void ConfigurePrompts() {
            var options = GetInteractiveOptions();
            options.InlinePrompts = true;
            options.UseInterpreterPrompts = false;
            options.PrimaryPrompt = ReplPrompt;
            options.SecondaryPrompt = SecondPrompt;
        }
    }

    [TestClass]
    public class GlyphPromptPython33ReplWindowTests : Python33ReplWindowTests {
        [TestInitialize]
        public new void Initialize() {
            TestInitialize();
        }

        protected override string ReplPrompt {
            get {
                return "";
            }
        }

        protected override string SecondPrompt {
            get {
                return "";
            }
        }

        protected override bool IPythonSupported {
            get {
                return false;
            }
        }

        protected override void ConfigurePrompts() {
            var options = GetInteractiveOptions();
            options.InlinePrompts = false;
            options.UseInterpreterPrompts = false;
            options.PrimaryPrompt = ">";
            options.SecondaryPrompt = "*";
        }
    }
#endif
}
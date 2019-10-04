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
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudioTools;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;
using TestUtilities.UI.Python;
using Keyboard = TestUtilities.UI.Keyboard;

namespace ReplWindowUITests {
    public class ReplWindowUITests {
        #region Smoke tests

        public void ExecuteInReplSysArgv(PythonVisualStudioApp app, string interpreter) {
            Settings = ReplWindowSettings.FindSettingsForInterpreter(interpreter);
            using (app.SelectDefaultInterpreter(Settings.Version)) {
                app.ServiceProvider.GetUIThread().Invoke(() => {
                    app.ServiceProvider.GetPythonToolsService().InteractiveBackendOverride = ReplWindowProxy.StandardBackend;
                });

                var project = app.OpenProject(@"TestData\SysArgvRepl.sln");

                using (var interactive = app.ExecuteInInteractive(project, Settings)) {
                    interactive.WaitForTextEnd("Program.py']", ">");
                }
            }
        }

        public void ExecuteInReplSysArgvScriptArgs(PythonVisualStudioApp app, string interpreter) {
            Settings = ReplWindowSettings.FindSettingsForInterpreter(interpreter);
            using (app.SelectDefaultInterpreter(Settings.Version)) {
                app.ServiceProvider.GetUIThread().Invoke(() => {
                    app.ServiceProvider.GetPythonToolsService().InteractiveBackendOverride = ReplWindowProxy.StandardBackend;
                });

                var project = app.OpenProject(@"TestData\SysArgvScriptArgsRepl.sln");

                using (var interactive = app.ExecuteInInteractive(project, Settings)) {
                    interactive.WaitForTextEnd(@"Program.py', '-source', 'C:\\Projects\\BuildSuite', '-destination', 'C:\\Projects\\TestOut', '-pattern', '*.txt', '-recurse', 'true']", ">");
                }
            }
        }

        public void ExecuteInReplSysPath(PythonVisualStudioApp app, string interpreter) {
            Settings = ReplWindowSettings.FindSettingsForInterpreter(interpreter);
            using (app.SelectDefaultInterpreter(Settings.Version)) {
                app.ServiceProvider.GetUIThread().Invoke(() => {
                    app.ServiceProvider.GetPythonToolsService().InteractiveBackendOverride = ReplWindowProxy.StandardBackend;
                });

                var sln = app.CopyProjectForTest(@"TestData\ReplSysPath.sln");
                var project = app.OpenProject(sln);

                using (var interactive = app.ExecuteInInteractive(project, Settings)) {
                    interactive.WaitForTextEnd("DONE", ">");
                }
            }
        }

        public void ExecuteInReplUnicodeFilename(PythonVisualStudioApp app, string interpreter) {
            Settings = ReplWindowSettings.FindSettingsForInterpreter(interpreter);
            using (app.SelectDefaultInterpreter(Settings.Version)) {
                app.ServiceProvider.GetUIThread().Invoke(() => {
                    app.ServiceProvider.GetPythonToolsService().InteractiveBackendOverride = ReplWindowProxy.StandardBackend;
                });

                var sln = TestData.GetTempPath();
                File.Copy(TestData.GetPath("TestData", "UnicodePath.sln"), Path.Combine(sln, "UnicodePathä.sln"));
                FileUtils.CopyDirectory(TestData.GetPath("TestData", "UnicodePath"), Path.Combine(sln, "UnicodePathä"));
                var project = app.OpenProject(Path.Combine(sln, "UnicodePathä.sln"));

                using (var interactive = app.ExecuteInInteractive(project, Settings)) {
                    interactive.WaitForTextEnd("hello world from unicode path", ">");
                }
            }
        }

        public void CwdImport(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
                var folder = PathUtils.TrimEndSeparator(TestData.GetTempPath());
                FileUtils.CopyDirectory(TestData.GetPath("TestData", "ReplCwd"), folder);

                interactive.SubmitCode("import sys\nsys.path");
                interactive.SubmitCode("import os\nos.chdir(r'" + folder + "')");

                var importErrorFormat = ((ReplWindowProxySettings)interactive.Settings).ImportError;
                interactive.SubmitCode("import module1");
                interactive.WaitForTextEnd(string.Format(importErrorFormat + "\n>", "module1").Split('\n'));

                interactive.SubmitCode("import module2");
                interactive.WaitForTextEnd(string.Format(importErrorFormat + "\n>", "module2").Split('\n'));

                interactive.SubmitCode("os.chdir('A')");
                interactive.WaitForTextEnd(">os.chdir('A')", ">");

                interactive.SubmitCode("import module1");
                interactive.WaitForTextEnd(">import module1", ">");

                interactive.SubmitCode("import module2");
                interactive.WaitForTextEnd(string.Format(importErrorFormat + "\n>", "module2").Split('\n'));

                interactive.SubmitCode("os.chdir('..\\B')");
                interactive.WaitForTextEnd(">os.chdir('..\\B')", ">");

                interactive.SubmitCode("import module2");
                interactive.WaitForTextEnd(">import module2", ">");
            }
        }

        public void QuitAndReset(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
                interactive.SubmitCode("quit()");
                interactive.WaitForText(">quit()", "The interactive Python process has exited.", ">");
                interactive.Reset();

                interactive.WaitForText(">quit()", "The interactive Python process has exited.", "Resetting Python state.", ">");
                interactive.SubmitCode("42");

                interactive.WaitForTextEnd(">42", "42", ">");
            }
        }

        public void PrintAllCharacters(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
                interactive.SubmitCode("print(\"" +
                    string.Join("", Enumerable.Range(0, 256).Select(i => string.Format("\\x{0:X2}", i))) +
                    "\\nDONE\")",
                    timeout: TimeSpan.FromSeconds(10.0)
                );

                interactive.WaitForTextEnd("DONE", ">");
            }
        }

        #endregion

        #region Basic tests

        public void RegressionImportSysBackspace(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
                const string importCode = ">import sys";
                interactive.SubmitCode(importCode.Substring(1));
                interactive.WaitForText(importCode, ">");

                interactive.Type("sys", commitLastLine: false);

                interactive.WaitForText(importCode, ">sys");

                interactive.Backspace(2);

                interactive.WaitForText(importCode, ">s");
                interactive.Backspace();

                interactive.WaitForText(importCode, ">");
            }
        }

        public void RegressionImportMultipleModules(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter, addNewLineAtEndOfFullyTypedWord: true)) {
                Keyboard.Type("import ");

                using (var sh = interactive.WaitForSession<ICompletionSession>()) {
                    Assert.IsNotNull(sh.Session.SelectedCompletionSet, "No selected completion set");
                    var names = sh.Session.SelectedCompletionSet.Completions.Select(c => c.DisplayText).ToList();
                    var nameset = new HashSet<string>(names);

                    Assert.AreEqual(names.Count, nameset.Count, "Module names were duplicated");
                }
            }
        }

        /// <summary>
        /// Type "raise Exception()", hit enter, raise Exception() should have
        /// appropriate syntax color highlighting.
        /// </summary>
        public void SyntaxHighlightingRaiseException(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter))
            using (var newClassifications = new AutoResetEvent(false)) {
                const string code = "raise Exception()";
                interactive.Classifier.ClassificationChanged += (s, e) => newClassifications.SetIfNotDisposed();

                interactive.SubmitCode(code);

                interactive.WaitForText(
                    ">" + code,
                    "Traceback (most recent call last):",
                    "  File \"<" + ((ReplWindowProxySettings)interactive.Settings).SourceFileName + ">\", line 1, in <module>",
                    "Exception",
                    ">"
                );

                var snapshot = interactive.TextView.TextBuffer.CurrentSnapshot;
                var span = new SnapshotSpan(snapshot, new Span(0, snapshot.Length));
                Assert.IsTrue(newClassifications.WaitOne(10000), "Timed out waiting for classification");
                var classifications = interactive.Classifier.GetClassificationSpans(span);
                Console.WriteLine("Classifications:");
                foreach (var c in classifications) {
                    Console.WriteLine("{0} ({1})", c.Span.GetText(), c.ClassificationType.Classification);
                }

                Assert.AreEqual(5, classifications.Count());
                Assert.AreEqual(PredefinedClassificationTypeNames.Keyword, classifications[0].ClassificationType.Classification);
                Assert.AreEqual(PredefinedClassificationTypeNames.Identifier, classifications[1].ClassificationType.Classification);
                Assert.AreEqual("Python grouping", classifications[2].ClassificationType.Classification);
                Assert.AreEqual(PredefinedClassificationTypeNames.WhiteSpace, classifications[3].ClassificationType.Classification);

                Assert.AreEqual("raise", classifications[0].Span.GetText());
                Assert.AreEqual("Exception", classifications[1].Span.GetText());
                Assert.AreEqual("()", classifications[2].Span.GetText());
            }
        }

        /// <summary>
        /// Type some text that leaves auto-indent at the end of the input and
        /// also outputs, make sure the auto indent is gone before we do the
        /// input. (regression for http://pytools.codeplex.com/workitem/92)
        /// </summary>
        public void PrintWithParens(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
                const string inputCode = ">print ('a',";
                const string autoIndent = ".       ";
                interactive.Type(inputCode.Substring(1));
                interactive.WaitForText(inputCode, ".");
                const string b = "'b',";
                interactive.Type(b);
                interactive.WaitForText(inputCode, autoIndent + b, ".");
                const string c = "'c')";
                interactive.Type(c);
                interactive.WaitForText(
                    inputCode,
                    autoIndent + b,
                    autoIndent + c,
                    Settings.Version.Configuration.Version.Major == 2 ? "('a', 'b', 'c')" : "a b c",
                    ">"
                );
            }
        }

        /// <summary>
        /// Make sure that we can successfully delete an autoindent inputted span
        /// (regression for http://pytools.codeplex.com/workitem/93)
        /// </summary>
        public void UndeletableIndent(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
                const string inputCode = ">print (('a',";
                const string autoIndent = ".        ";
                interactive.Type(inputCode.Substring(1));
                interactive.WaitForText(inputCode, ".");
                const string b = "'b',";
                interactive.Type(b);
                interactive.WaitForText(inputCode, autoIndent + b, ".");
                const string c = "'c'))";
                interactive.Type(c);
                interactive.WaitForText(inputCode, autoIndent + b, autoIndent + c, "('a', 'b', 'c')", ">");
                interactive.SubmitCurrentText();
                interactive.WaitForText(inputCode, autoIndent + b, autoIndent + c, "('a', 'b', 'c')", ">", ">");
            }
        }

        public void InlineImage(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
                interactive.SubmitCode(@"import sys
repl = sys.modules['ptvsd.repl'].BACKEND
repl is not None");
                interactive.WaitForTextEnd(
                    ">import sys",
                    ">repl = sys.modules['ptvsd.repl'].BACKEND",
                    ">repl is not None",
                    "True",
                    ">"
                );

                Thread.Sleep(500);
                interactive.ClearScreen();
                interactive.WaitForText(">");

                // load a 600 x 600 at 96dpi image
                string loadImage = string.Format(
                    "repl.send_image(\"{0}\")",
                    TestData.GetPath(@"TestData\TestImage.png").Replace("\\", "\\\\")
                );
                interactive.SubmitCode(loadImage);
                interactive.WaitForText(">" + loadImage, ">");

                // check that we got a tag inserted
                var tags = WaitForTags(interactive);
                Assert.AreEqual(1, tags.Length);

                var size = tags[0].Tag.Adornment.RenderSize;
                Assert.IsTrue(size.Width > 0 && size.Width <= 600);
                Assert.IsTrue(size.Height > 0 && size.Height <= 600);
            }
        }

        public void ImportCompletions(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
                if (((ReplWindowProxySettings)interactive.Settings).Version.IsIronPython) {
                    interactive.SubmitCode("import clr");
                    interactive.WaitForText(">import clr", ">");
                }

                Keyboard.Type("import ");
                List<string> names;
                using (var sh = interactive.WaitForSession<ICompletionSession>()) {
                    Assert.IsNotNull(sh.Session.SelectedCompletionSet, "No selected completion set");
                    names = sh.Session.SelectedCompletionSet.Completions.Select(c => c.DisplayText).ToList();
                }

                Console.WriteLine(string.Join(Environment.NewLine, names));

                foreach (var name in names) {
                    Assert.IsFalse(name.Contains('.'), name + " contained a dot");
                }

                Keyboard.Type("os.");
                using (var sh = interactive.WaitForSession<ICompletionSession>()) {
                    Assert.IsNotNull(sh.Session.SelectedCompletionSet, "No selected completion set");
                    names = sh.Session.SelectedCompletionSet.Completions.Select(c => c.DisplayText).ToList();
                    AssertUtil.ContainsExactly(names, "path");
                }
                interactive.ClearInput();
            }
        }

        public void Comments(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
                const string code = "# fob";
                Keyboard.Type(code + "\r");

                interactive.WaitForText(">" + code, ".");

                const string code2 = "# oar";
                Keyboard.Type(code2 + "\r");

                interactive.WaitForText(">" + code, "." + code2, ".");

                Keyboard.Type("\r");
                interactive.WaitForText(">" + code, "." + code2, ".", ">");
            }
        }

        public void NoSnippets(PythonVisualStudioApp app, string interpreter) {
            // https://pytools.codeplex.com/workitem/2945 is the reason for
            // disabling snippets; https://pytools.codeplex.com/workitem/2947 is
            // where we will re-enable them when they work properly.
            using (var interactive = Prepare(app, interpreter)) {
                int spaces = interactive.TextView.Options.GetOptionValue(DefaultOptions.IndentSizeOptionId);
                int textWidth = interactive.CurrentPrimaryPrompt.Length + 3;

                int totalChars = spaces;
                while (totalChars < textWidth) {
                    totalChars += spaces;
                }

                Keyboard.Type("def\t");
                interactive.WaitForText(">def" + new string(' ', totalChars - textWidth));
            }
        }

        public void TestPydocRedirected(PythonVisualStudioApp app, string interpreter) {
            // We run this test on multiple interpreters because pydoc
            // output redirection has changed on Python 3.x
            // https://github.com/Microsoft/PTVS/issues/2531
            using (var interactive = Prepare(app, interpreter)) {
                interactive.SubmitCode("help(exit)");
                interactive.WaitForText(
                    ">help(exit)",
                    Settings.ExitHelp,
                    ">"
                );
            }
        }

        #endregion

        #region IPython tests

        public void IPythonMode(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = PrepareIPython(app, interpreter)) {
                interactive.SubmitCode("x = 42\n?x");

                interactive.WaitForText(
                    ">x = 42",
                    ">?x",
                    ((ReplWindowProxySettings)interactive.Settings).IPythonIntDocumentation,
                    "",
                    ">"
                );
            }
        }

        public void IPythonCtrlBreakAborts(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = PrepareIPython(app, interpreter)) {
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

        public void IPythonSimpleCompletion(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = PrepareIPython(app, interpreter, addNewLineAtEndOfFullyTypedWord: false)) {
                interactive.SubmitCode("x = 42");
                interactive.WaitForText(">x = 42", ">");
                interactive.ClearScreen();

                Keyboard.Type("x.");

                using (var sh = interactive.WaitForSession<ICompletionSession>()) {
                    // commit entry
                    sh.Commit();
                    sh.WaitForSessionDismissed();
                }

                interactive.WaitForText(">x." + ((ReplWindowProxySettings)interactive.Settings).IntFirstMember);

                // clear input at repl
                interactive.ClearInput();

                // try it again, and dismiss the session
                Keyboard.Type("x.");
                using (interactive.WaitForSession<ICompletionSession>()) { }
            }
        }

        public void IPythonSimpleSignatureHelp(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = PrepareIPython(app, interpreter)) {
                Assert.IsNotNull(interactive);

                interactive.SubmitCode("def f(): pass");
                interactive.WaitForText(">def f(): pass", ">");

                Keyboard.Type("f(");

                using (var sh = interactive.WaitForSession<ISignatureHelpSession>()) {
                    var parts = new string[] {
                        "Signature: f()",
                        "Source:    def f(): pass",
                        "File:      ",
                        "Type:      function",
                    };
                    foreach (var expected in parts) {
                        Assert.IsTrue(sh.Session.SelectedSignature.Documentation.Contains(expected));
                    }
                }
            }
        }

        public void IPythonInlineGraph(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = PrepareIPython(app, interpreter)) {
                interactive.SubmitCode(@"from pylab import *
x = linspace(0, 4*pi)
plot(x, x)");
                interactive.WaitForAnyLineContainsText("matplotlib.lines.Line2D");

                Thread.Sleep(2000);

                var tags = WaitForTags(interactive);
                Assert.AreEqual(1, tags.Length);
            }
        }

        public void IPythonStartInInteractive(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = PrepareIPython(app, interpreter))
            using (app.SelectDefaultInterpreter(interactive.Settings.Version)) {
                var project = interactive.App.OpenProject(@"TestData\InteractiveFile.sln");

                interactive.App.ExecuteCommand("Python.ExecuteInInteractive");
                interactive.WaitForAnyLineContainsText("Program.pyabcdef");
            }
        }

        public void ExecuteInIPythonReplSysArgv(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = PrepareIPython(app, interpreter))
            using (app.SelectDefaultInterpreter(interactive.Settings.Version)) {
                var project = interactive.App.OpenProject(@"TestData\SysArgvRepl.sln");

                interactive.App.ExecuteCommand("Python.ExecuteInInteractive");
                interactive.WaitForAnyLineContainsText("Program.py']");
            }
        }

        public void ExecuteInIPythonReplSysArgvScriptArgs(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = PrepareIPython(app, interpreter))
            using (app.SelectDefaultInterpreter(interactive.Settings.Version)) {
                var project = interactive.App.OpenProject(@"TestData\SysArgvScriptArgsRepl.sln");

                interactive.App.ExecuteCommand("Python.ExecuteInInteractive");
                interactive.WaitForAnyLineContainsText(@"Program.py', '-source', 'C:\\Projects\\BuildSuite', '-destination', 'C:\\Projects\\TestOut', '-pattern', '*.txt', '-recurse', 'true']");
            }
        }

        #endregion

        #region Advanced Miscellaneous tests

        public void ClearInputHelper(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
                interactive.Type("1 + ", commitLastLine: false);
                interactive.WaitForText(">1 + ");
                interactive.ClearInput();

                interactive.Type("2");
                interactive.WaitForText(">2", "2", ">");
            }
        }

        #endregion

        #region Advanced Signature Help tests

        /// <summary>
        /// "def f(): pass" + 2 ENTERS
        /// f( should bring signature help up
        /// 
        /// </summary>
        // LSC
        //public void SimpleSignatureHelp(PythonVisualStudioApp app, string interpreter) {
        //    using (var interactive = Prepare(app, interpreter)) {
        //        const string code = "def f(): pass";
        //        interactive.SubmitCode(code);
        //        interactive.WaitForText(">" + code, ">");
        //        WaitForAnalysis(interactive);

        //        Keyboard.Type("f(");

        //        using (var sh = interactive.WaitForSession<ISignatureHelpSession>()) {
        //            Assert.AreEqual("f()", sh.Session.SelectedSignature.Content);
        //            sh.Dismiss();
        //            sh.WaitForSessionDismissed();
        //        }
        //    }
        //}

        /// <summary>
        /// "def f(a, b=1, c="d"): pass" + 2 ENTERS
        /// f( should bring signature help up and show default values and types
        /// 
        /// </summary>
        // LSC
        //public void SignatureHelpDefaultValue(PythonVisualStudioApp app, string interpreter) {
        //    using (var interactive = Prepare(app, interpreter)) {
        //        const string code = "def f(a, b=1, c=\"d\"): pass";
        //        interactive.SubmitCode(code + "\n");
        //        interactive.WaitForText(">" + code, ">");
        //        WaitForAnalysis(interactive);

        //        Keyboard.Type("f(");

        //        using (var sh = interactive.WaitForSession<ISignatureHelpSession>()) {
        //            Assert.AreEqual("f(a, b: int = 1, c: str = 'd')", sh.Session.SelectedSignature.Content);
        //            sh.Dismiss();
        //            sh.WaitForSessionDismissed();
        //        }
        //    }
        //}

        #endregion

        #region Advanced Completion tests

        /// <summary>
        /// "x = 42"
        /// "x." should bring up intellisense completion
        /// </summary>
        public void SimpleCompletion(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void SimpleCompletionSpaceNoCompletion(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void CompletionWrongText(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        // LSC
        //public void CompletionFullTextWithoutNewLine(PythonVisualStudioApp app, string interpreter) {
        //    using (var interactive = Prepare(app, interpreter, addNewLineAtEndOfFullyTypedWord: false)) {
        //        const string code = "x = 42";
        //        interactive.SubmitCode(code);
        //        interactive.WaitForText(">" + code, ">");
        //        WaitForAnalysis(interactive);

        //        Keyboard.Type("x.");
        //        using (var sh = interactive.WaitForSession<ICompletionSession>()) {
        //            Keyboard.Type("real\r");
        //            sh.WaitForSessionDismissed();
        //        }
        //        interactive.WaitForText(">" + code, ">x.real");
        //    }
        //}

        /// <summary>
        /// x = 42; x.conjugate[enter] – should respect enter completes option,
        /// and should respect enter at end of word completes option.  When it
        /// does execute the text the output should be on the next line.
        /// </summary>
        // LSC
        //public void CompletionFullTextWithNewLine(PythonVisualStudioApp app, string interpreter) {
        //    using (var interactive = Prepare(app, interpreter, addNewLineAtEndOfFullyTypedWord: true)) {

        //        const string code = "x = 42";
        //        interactive.SubmitCode(code);
        //        interactive.WaitForText(">" + code, ">");
        //        WaitForAnalysis(interactive);

        //        Keyboard.Type("x.");
        //        using (var sh = interactive.WaitForSession<ICompletionSession>()) {
        //            Keyboard.Type("real\r");
        //            sh.WaitForSessionDismissed();
        //        }

        //        interactive.WaitForText(">" + code, ">x.real", "42", ">");
        //    }
        //}

        /// <summary>
        /// With AutoListIdentifiers on, all [a-zA-Z_] should bring up
        /// completions
        /// </summary>
        public void AutoListIdentifierCompletions(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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

        #region Advanced Input/output redirection tests

        public void TestStdOutRedirected(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void TestRawInput(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
                EnsureInputFunction(interactive);

                interactive.SubmitCode("x = input()");
                interactive.WaitForText(">x = input()", "<");

                Keyboard.Type("hello\r");
                interactive.WaitForText(">x = input()", "<hello", "", ">");

                interactive.SubmitCode("print(x)");
                interactive.WaitForText(">x = input()", "<hello", "", ">print(x)", "hello", ">");
            }
        }

        public void OnlyTypeInRawInput(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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

        public void DeleteCharactersInRawInput(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void TestIndirectInput(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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

        #region Advanced Keyboard tests

        /// <summary>
        /// Enter in a middle of a line should insert new line
        /// </summary>
        public void EnterInMiddleOfLine(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void LineBreak(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void LineBreakInMiddleOfLine(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void CtrlEnterCommits(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void EscapeClearsMultipleLines(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void CtrlEnterOnPreviousInput(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void CtrlEnterForceCommit(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void CtrlEnterMultiLineForceCommit(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void BackspacePrompt(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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

        public void BackspaceSmartDedent(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void BackspaceSecondaryPrompt(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void BackspaceSecondaryPromptSelected(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void DeleteSecondaryPromptSelected(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void EditTypeSecondaryPromptSelected(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void TestDelNoTextSelected(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
                interactive.Type("abc", commitLastLine: false);
                interactive.WaitForText(">abc");

                Keyboard.Type(Key.Home);
                Keyboard.Type(Key.Delete);

                interactive.WaitForText(">bc");
            }
        }

        public void TestDelAtEndOfLine(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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

        public void TestDelAtEndOfBuffer(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
                interactive.Type("def f():\nprint('hello')", commitLastLine: false);
                interactive.WaitForText(">def f():", ".    print('hello')");

                interactive.App.ExecuteCommand("Edit.Delete");
                interactive.WaitForText(">def f():", ".    print('hello')");
            }
        }

        public void TestDelInOutput(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
                interactive.SubmitCode("print('hello')");
                interactive.WaitForText(">print('hello')", "hello", ">");

                Keyboard.Type(Key.Left);
                Keyboard.Type(Key.Up);

                interactive.App.ExecuteCommand("Edit.Delete");
                interactive.WaitForText(">print('hello')", "hello", ">");
            }
        }

        #endregion

        #region Advanced Cancel tests

        /// <summary>
        /// while True: pass / Right Click -> Break Execution (or Ctrl-Break) should break execution
        /// </summary>
        public void CtrlBreakInterrupts(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void CtrlBreakInterruptsLongRunning(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void CtrlBreakNotRunning(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void CursorWhileCodeIsRunning(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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

        #region Advanced History tests

        public void HistoryUpdateDef(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
                interactive.Type("def f():\nprint('hi')\n");
                interactive.WaitForText(">def f():", ".    print('hi')", ".", ">");

                interactive.PreviousHistoryItem();
                // delete i')
                interactive.Backspace(4);

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
                    ">def f():", ".    print('hello')", "."
                );

                interactive.PreviousHistoryItem();
                interactive.WaitForText(
                    ">def f():", ".    print('hi')", ".",
                    ">def f():", ".    print('hello')", ".",
                    ">def f():", ".    print('hi')", "."
                );
            }
        }

        public void HistoryAppendDef(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
                Keyboard.Type("def f():\rprint('hi')\r\r");

                interactive.WaitForText(
                    ">def f():",
                    ".    print('hi')",
                    ".",
                    ">"
                );

                interactive.PreviousHistoryItem();
                Keyboard.Type("    print('hello')\r\r");

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

        public void HistoryBackForward(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void HistoryMaximumLength(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void HistoryUncommittedInput1(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void HistoryUncommittedInput2(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void HistorySearch(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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

        #region Advanced Clipboard tests

        public void CommentPaste(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
                // Pasting with a newline will submit
                interactive.Paste(comment + "\r\n" + comment + "\r\n");
                interactive.WaitForText(">" + comment, "." + comment, ".", ">");
            }
        }

        public void CsvPaste(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void EditCutIncludingPrompt(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void EditPasteSecondaryPromptSelected(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void EditPasteSecondaryPromptSelectedInPromptMargin(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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

        #region Advanced Command tests

        /// <summary>
        /// Tests entering an unknown repl commmand
        /// </summary>
        public void ReplCommandUnknown(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
                const string code = "$unknown";
                interactive.SubmitCode(code);
                interactive.WaitForText(">" + code, "Unknown command '$unknown', use '$help' for a list of commands.", ">");
            }
        }

        /// <summary>
        /// Tests entering an unknown repl commmand
        /// </summary>
        public void ReplCommandComment(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
                const string code = "$$ quox oar baz";
                interactive.SubmitCode(code);
                interactive.WaitForText(">" + code, ">");
            }
        }

        /// <summary>
        /// Tests using the $cls clear screen command
        /// </summary>
        public void ClearScreenCommand(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
                interactive.Type("$cls", commitLastLine: false);
                interactive.WaitForText(">$cls");

                interactive.SubmitCurrentText();
                interactive.WaitForText(">");
            }
        }

        /// <summary>
        /// Tests REPL command help
        /// </summary>
        public void ReplCommandHelp(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void CommandsLoadScript(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void CommandsLoadScriptWithQuotes(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void CommandsLoadScriptWithClass(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void CommandsLoadScriptMultipleSubmissions(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void CommandsLoadScriptMultipleSubmissionsWithClearScreen(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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

        #region Advanced Insert Code tests

        /// <summary>
        /// Inserts code to REPL while input is accepted. 
        /// </summary>
        public void InsertCode(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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
        public void InsertCodeWhileRunning(PythonVisualStudioApp app, string interpreter) {
            using (var interactive = Prepare(app, interpreter)) {
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

        #region Advanced Launch Configuration Tests

        public void PythonPathIgnored(PythonVisualStudioApp app, string interpreter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (new PythonServiceGeneralOptionsSetter(pyService, clearGlobalPythonPath: true))
            using (new EnvironmentVariableSetter("PYTHONPATH", @"C:\MyPythonPath1;C:\MyPythonPath2"))
            using (var interactive = Prepare(app, interpreter)) {
                interactive.SubmitCode(@"import sys");
                interactive.SubmitCode(@"import os");
                interactive.SubmitCode(@"[p for p in sys.path if 'MyPythonPath' in p]");
                interactive.WaitForTextEnd(
                    @"[]",
                    ">"
                );
                interactive.SubmitCode("os.environ.get('PYTHONPATH')");
                interactive.WaitForTextEnd(
                    "''",
                    ">"
                );
            }
        }

        public void PythonPathNotIgnored(PythonVisualStudioApp app, string interpreter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (new PythonServiceGeneralOptionsSetter(pyService, clearGlobalPythonPath: false))
            using (new EnvironmentVariableSetter("PYTHONPATH", @"C:\MyPythonPath1;C:\MyPythonPath2"))
            using (var interactive = Prepare(app, interpreter)) {
                interactive.SubmitCode(@"import sys");
                interactive.SubmitCode(@"import os");
                interactive.SubmitCode(@"[p for p in sys.path if 'MyPythonPath' in p]");
                interactive.WaitForTextEnd(
                    @"['C:\\MyPythonPath1', 'C:\\MyPythonPath2']",
                    ">"
                );
                interactive.SubmitCode("os.environ.get('PYTHONPATH')");
                interactive.WaitForTextEnd(
                    @"'C:\\MyPythonPath1;C:\\MyPythonPath2'",
                    ">"
                );
            }
        }

        public void PythonPathNotIgnoredButMissing(PythonVisualStudioApp app, string interpreter) {
            var pyService = app.ServiceProvider.GetUIThread().Invoke(() => app.ServiceProvider.GetPythonToolsService());
            using (new PythonServiceGeneralOptionsSetter(pyService, clearGlobalPythonPath: false))
            using (new EnvironmentVariableSetter("PYTHONPATH", null))
            using (var interactive = Prepare(app, interpreter)) {
                interactive.SubmitCode("import os");
                interactive.SubmitCode("os.environ.get('PYTHONPATH')");
                interactive.WaitForTextEnd(
                    "''",
                    ">"
                );
            }
        }

        #endregion

        #region Helper methods

        private ReplWindowProxySettings Settings { get; set; }

        private ReplWindowProxy PrepareIPython(
            PythonVisualStudioApp app,
            string interpreter,
            bool addNewLineAtEndOfFullyTypedWord = false
        ) {
            return Prepare(app, interpreter, useIPython: true, addNewLineAtEndOfFullyTypedWord: addNewLineAtEndOfFullyTypedWord);
        }

        private ReplWindowProxy Prepare(
            PythonVisualStudioApp app,
            string interpreter,
            bool enableAttach = false,
            bool useIPython = false,
            bool addNewLineAtEndOfFullyTypedWord = false
        ) {
            Settings = ReplWindowSettings.FindSettingsForInterpreter(interpreter);
            var s = Settings;
            if (s.Version == null) {
                Assert.Inconclusive("Interpreter missing for " + GetType().Name);
            }

            if (addNewLineAtEndOfFullyTypedWord != s.AddNewLineAtEndOfFullyTypedWord) {
                s = object.ReferenceEquals(s, Settings) ? s.Clone() : s;
                s.AddNewLineAtEndOfFullyTypedWord = addNewLineAtEndOfFullyTypedWord;
            }

            return ReplWindowProxy.Prepare(app, s, projectName: null, workspaceName: null, useIPython: useIPython);
        }

        private static void EnsureInputFunction(ReplWindowProxy interactive) {
            var settings = (ReplWindowProxySettings)interactive.Settings;
            if (settings.RawInput != "input") {
                interactive.SubmitCode("input = " + settings.RawInput);
                interactive.ClearScreen();
            }
        }

        // LSC
        //private static void WaitForAnalysis(ReplWindowProxy interactive) {
        //    var stopAt = DateTime.Now.Add(TimeSpan.FromSeconds(60));
        //    interactive.GetAnalyzer().WaitForCompleteAnalysis(_ => DateTime.Now < stopAt);
        //    if (DateTime.Now >= stopAt) {
        //        Assert.Fail("Timeout waiting for complete analysis");
        //    }
        //    // Most of the time we're waiting to ensure that IntelliSense will
        //    // work, which normally requires a bit more time.
        //    Thread.Sleep(500);
        //}

        private static IMappingTagSpan<IntraTextAdornmentTag>[] WaitForTags(ReplWindowProxy interactive) {
            var aggFact = interactive.App.ComponentModel.GetService<IViewTagAggregatorFactoryService>();
            var textView = interactive.TextView;
            var aggregator = aggFact.CreateTagAggregator<IntraTextAdornmentTag>(textView);
            var snapshot = textView.TextBuffer.CurrentSnapshot;

            IMappingTagSpan<IntraTextAdornmentTag>[] tags = null;
            ((UIElement)textView).Dispatcher.Invoke((Action)(() => {
                for (int i = 0; i < 100; i++) {
                    tags = aggregator.GetTags(new SnapshotSpan(snapshot, new Span(0, snapshot.Length))).ToArray();
                    if (tags.Length > 0) {
                        break;
                    }
                    Thread.Sleep(100);
                }
            }));

            Assert.IsNotNull(tags, "Unable to find tags");
            return tags;
        }

        #endregion
    }

    // LSC
    //static class ReplWindowProxyExtensions {
    //    public static VsProjectAnalyzer GetAnalyzer(this ReplWindowProxy proxy) {
    //        return ((IPythonInteractiveIntellisense)proxy.Window.Evaluator).Analyzer;
    //    }
    //}
}

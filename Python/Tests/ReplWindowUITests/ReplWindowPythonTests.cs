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
using System.Linq;
using System.Threading;
using System.Windows;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using TestUtilities;
using TestUtilities.UI;
using TestUtilities.UI.Python;
using Keyboard = TestUtilities.UI.Keyboard;

namespace ReplWindowUITests {
    /// <summary>
    /// These tests are run for the important versions of Python that support
    /// the REPL.
    /// </summary>
    [TestClass, Ignore]
    public abstract class ReplWindowPythonTests : ReplWindowPythonSmokeTests {
        [TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public virtual void RegressionImportSysBackspace() {
            using (var interactive = Prepare()) {
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

        [TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public virtual void RegressionImportMultipleModules() {
            using (var interactive = Prepare(addNewLineAtEndOfFullyTypedWord: true)) {
                Keyboard.Type("import ");

                using (var sh = interactive.WaitForSession<ICompletionSession>()) {
                    var names = sh.Session.SelectedCompletionSet.Completions.Select(c => c.DisplayText).ToList();
                    Assert.IsNotNull(sh.Session.SelectedCompletionSet, "No selected completion set");
                    var nameset = new HashSet<string>(names);

                    Assert.AreEqual(names.Count, nameset.Count, "Module names were duplicated");
                }
            }
        }

        /// <summary>
        /// Type "raise Exception()", hit enter, raise Exception() should have
        /// appropriate syntax color highlighting.
        /// </summary>
        [TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public virtual void SyntaxHighlightingRaiseException() {
            using (var interactive = Prepare())
            using (var newClassifications = new AutoResetEvent(false)) {
                const string code = "raise Exception()";
                interactive.Classifier.ClassificationChanged += (s, e) => newClassifications.SetIfNotDisposed();

                interactive.SubmitCode(code);

                interactive.WaitForText(
                    ">" + code,
                    "Traceback (most recent call last):",
                    "  File \"<" + ((PythonReplWindowProxySettings)interactive.Settings).SourceFileName + ">\", line 1, in <module>",
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

                Assert.AreEqual(3, classifications.Count());
                Assert.AreEqual(classifications[0].ClassificationType.Classification, PredefinedClassificationTypeNames.Keyword);
                Assert.AreEqual(classifications[1].ClassificationType.Classification, PredefinedClassificationTypeNames.Identifier);
                Assert.AreEqual(classifications[2].ClassificationType.Classification, "Python grouping");

                Assert.AreEqual(classifications[0].Span.GetText(), "raise");
                Assert.AreEqual(classifications[1].Span.GetText(), "Exception");
                Assert.AreEqual(classifications[2].Span.GetText(), "()");
            }
        }

        /// <summary>
        /// Type some text that leaves auto-indent at the end of the input and
        /// also outputs, make sure the auto indent is gone before we do the
        /// input. (regression for http://pytools.codeplex.com/workitem/92)
        /// </summary>
        [TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public virtual void PrintWithParens() {
            using (var interactive = Prepare()) {
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
        [TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public virtual void UndeletableIndent() {
            using (var interactive = Prepare()) {
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

        [TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public virtual void InlineImage() {
            using (var interactive = Prepare()) {
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

        [TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public virtual void ImportCompletions() {
            using (var interactive = Prepare()) {
                if (((PythonReplWindowProxySettings)interactive.Settings).Version.IsIronPython) {
                    interactive.SubmitCode("import clr");
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

        [Ignore] // https://github.com/Microsoft/PTVS/issues/2682
        [TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public virtual void Comments() {
            using (var interactive = Prepare()) {
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

        [TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public virtual void NoSnippets() {
            // https://pytools.codeplex.com/workitem/2945 is the reason for
            // disabling snippets; https://pytools.codeplex.com/workitem/2947 is
            // where we will re-enable them when they work properly.
            using (var interactive = Prepare()) {
                int spaces = interactive.TextView.Options.GetOptionValue(DefaultOptions.IndentSizeOptionId);
                int textWidth = interactive.Settings.PrimaryPrompt.Length + 3;

                int totalChars = spaces;
                while (totalChars < textWidth) {
                    totalChars += spaces;
                }

                Keyboard.Type("def\t");
                interactive.WaitForText(">def" + new string(' ', totalChars - textWidth));
            }
        }

        [TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public virtual void TestPydocRedirected() {
            // We run this test on multiple interpreters because pydoc
            // output redirection has changed on Python 3.x
            // https://github.com/Microsoft/PTVS/issues/2531
            using (var interactive = Prepare()) {
                interactive.SubmitCode("help(exit)");
                interactive.WaitForText(
                    ">help(exit)",
                    Settings.ExitHelp,
                    ">"
                );
            }
        }

        #region Helper Methods

        internal static IMappingTagSpan<IntraTextAdornmentTag>[] WaitForTags(ReplWindowProxy interactive) {
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
}

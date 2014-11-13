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
using Keyboard = TestUtilities.UI.Keyboard;

namespace ReplWindowUITests {
    /// <summary>
    /// These tests are run for the important versions of Python that support
    /// the REPL.
    /// </summary>
    [TestClass, Ignore]
    public abstract class ReplWindowPythonTests : ReplWindowPythonSmokeTests {
        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
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

        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void RegressionImportMultipleModules() {
            using (var interactive = Prepare()) {
                interactive.AddNewLineAtEndOfFullyTypedWord = true;

                Keyboard.Type("import ");

                using (var sh = interactive.WaitForSession<ICompletionSession>()) {
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
        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void SyntaxHighlightingRaiseException() {
            using (var interactive = Prepare()) {
                const string code = "raise Exception()";
                interactive.SubmitCode(code);

                interactive.WaitForText(
                    ">" + code,
                    "Traceback (most recent call last):",
                    "  File \"<" + interactive.Settings.SourceFileName + ">\", line 1, in <module>",
                    "Exception",
                    ">"
                );

                var snapshot = interactive.TextView.TextBuffer.CurrentSnapshot;
                var span = new SnapshotSpan(snapshot, new Span(0, snapshot.Length));
                var classifications = interactive.Classifier.GetClassificationSpans(span);

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
        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
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
                interactive.WaitForText(inputCode, autoIndent + b, autoIndent + c, ".");
                interactive.Backspace();    // remove prompt, we should be indented at same level as the print statement
                interactive.WaitForText(inputCode, autoIndent + b, autoIndent + c);
            }
        }

        /// <summary>
        /// Make sure that we can successfully delete an autoindent inputted span
        /// (regression for http://pytools.codeplex.com/workitem/93)
        /// </summary>
        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
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
                interactive.WaitForText(inputCode, autoIndent + b, autoIndent + c, ".");
                interactive.SubmitCurrentText();

                interactive.WaitForText(inputCode, autoIndent + b, autoIndent + c, ".", "('a', 'b', 'c')", ">");
            }
        }

        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void InlineImage() {
            using (var interactive = Prepare()) {
                interactive.SubmitCode(@"import sys
repl = sys.modules['visualstudio_py_repl'].BACKEND
repl is not None");
                interactive.WaitForTextEnd(">repl is not None", "True", ">");

                Thread.Sleep(500);
                interactive.ClearScreen();
                interactive.WaitForText(">");

                string loadImage = string.Format(
                    "repl.send_image(\"{0}\")",
                    TestData.GetPath(@"TestData\TestImage.png").Replace("\\", "\\\\")
                );
                interactive.SubmitCode(loadImage);
                interactive.WaitForText(">" + loadImage, "", "", ">");

                // check that we got a tag inserted
                var tags = WaitForTags(interactive);
                Assert.AreEqual(1, tags.Length);

                var size = tags[0].Tag.Adornment.RenderSize;

                // now add some more code to cause the image to minimize
                const string nopCode = "x = 2";
                interactive.SubmitCode(nopCode);
                interactive.WaitForText(">" + loadImage, "", "", ">" + nopCode, ">");

                // let image minimize...
                Thread.Sleep(200);
                for (int i = 0; i < 10; i++) {
                    tags = WaitForTags(interactive);
                    Assert.AreEqual(1, tags.Length);

                    var sizeTmp = tags[0].Tag.Adornment.RenderSize;
                    if (sizeTmp.Height < size.Height && sizeTmp.Width < size.Width) {
                        break;
                    }
                    Thread.Sleep(200);
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
        }

        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
        public virtual void ImportCompletions() {
            using (var interactive = Prepare()) {
                if (interactive.Settings.Version.IsIronPython) {
                    interactive.SubmitCode("import clr");
                }

                Keyboard.Type("import ");
                List<string> names;
                using (var sh = interactive.WaitForSession<ICompletionSession>()) {
                    names = sh.Session.SelectedCompletionSet.Completions.Select(c => c.DisplayText).ToList();
                }

                Console.WriteLine(string.Join(Environment.NewLine, names));

                foreach (var name in names) {
                    Assert.IsFalse(name.Contains('.'), name + " contained a dot");
                }

                Keyboard.Type("os.");
                using (var sh = interactive.WaitForSession<ICompletionSession>()) {
                    names = sh.Session.SelectedCompletionSet.Completions.Select(c => c.DisplayText).ToList();
                    AssertUtil.ContainsExactly(names, "path");
                }
                interactive.ClearInput();
            }
        }

        [TestMethod, Priority(0)]
        [HostType("VSTestHost")]
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

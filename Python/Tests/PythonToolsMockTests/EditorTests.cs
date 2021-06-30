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

extern alias analysis;
extern alias pythontools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using analysis::Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools.MockVsTests;
using pythontools::Microsoft.PythonTools;
using pythontools::Microsoft.PythonTools.Intellisense;
using TestUtilities;

namespace PythonToolsMockTests {
    [TestClass]
    public class EditorTests {
        [ClassInitialize]
        public static void Initialize(TestContext context) {
            AssertListener.Initialize();
            VsProjectAnalyzer.DefaultTimeout = 10000;
            VsProjectAnalyzer.AssertOnRequestFailure = true;
        }

        [TestInitialize]
        public void OnTestInitialized() {
            MockPythonToolsPackage.SuppressTaskProvider = true;
        }

        [TestCleanup]
        public void OnTestCleanup() {
            MockPythonToolsPackage.SuppressTaskProvider = false;
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void BuiltinFunctionSigHelp() {
            using (var view = new PythonEditor()) {
                view.TypeAndWaitForAnalysis("min");
                view.Type("(");

                for (int retries = 10; retries > 0; --retries) {
                    using (var sh = view.View.WaitForSession<ISignatureHelpSession>()) {
                        var doc = sh.Session.Signatures[0].Documentation;
                        if (doc.Contains("still being calculated")) {
                            view.VS.Sleep(100);
                            continue;
                        }
                        AssertUtil.AreEqual(new Regex(@".*min\([^)]+\).*"), doc);
                        break;
                    }
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void SigHelpInClass() {
            using (var view = new PythonEditor()) {
                view.TypeAndWaitForAnalysis("class C(): pass\n");
                view.MoveCaret(1, 9);

                view.ParamInfo();

                view.View.AssertNoIntellisenseSession();
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void BuiltinFunctionCompletions() {
            using (var view = new PythonEditor()) {
                view.TypeAndWaitForAnalysis("min");
                view.Type(".");

                using (var sh = view.View.WaitForSession<ICompletionSession>()) {
                    AssertUtil.Contains(sh.Session.Completions(), "__call__");
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void FilterCompletions() {
            using (var view = new PythonEditor()) {
                view.TypeAndWaitForAnalysis("min");
                view.Type(".");

                using (var sh = view.View.WaitForSession<ICompletionSession>()) {
                    AssertUtil.Contains(sh.Session.Completions(), "__call__");

                    view.Type("class");

                    AssertUtil.DoesntContain(sh.Session.Completions(), "__call__");
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void DotCompletes() {
            using (var view = new PythonEditor()) {
                view.TypeAndWaitForAnalysis("min");
                view.Type(".");

                using (var sh = view.View.WaitForSession<ICompletionSession>()) {
                    AssertUtil.Contains(sh.Session.Completions(), "__call__");

                    view.Type("class.");

                    Assert.AreEqual("min.__class__.", view.Text);
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void NonIdentifierDismisses() {
            using (var view = new PythonEditor()) {
                view.TypeAndWaitForAnalysis("min");
                view.Type(".");

                using (var sh = view.View.WaitForSession<ICompletionSession>()) {
                    AssertUtil.Contains(sh.Session.Completions(), "__call__");

                    view.Type("#");

                    Assert.IsTrue(sh.Session.IsDismissed);
                }
                view.View.AssertNoIntellisenseSession();
                Assert.AreEqual("min.#", view.Text);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void EnterCommits() {
            using (var view = new PythonEditor()) {
                view.TypeAndWaitForAnalysis("min");
                view.Type(".");

                using (var sh = view.View.WaitForSession<ICompletionSession>()) {
                    AssertUtil.ContainsAtLeast(sh.Session.Completions(), "__class__");
                    view.Type("class\r");
                }

                Assert.AreEqual("min.__class__", view.Text);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void EnterDismisses() {
            using (var view = new PythonEditor()) {
                view.AdvancedOptions.EnterCommitsIntellisense = false;
                view.AdvancedOptions.AutoListMembers = true;

                view.TypeAndWaitForAnalysis("min");
                view.Type(".");

                using (var sh = view.View.WaitForSession<ICompletionSession>()) {
                    AssertUtil.ContainsAtLeast(sh.Session.Completions(), "__class__");

                    view.Type("class\r");
                }
                Assert.AreEqual("min.class\r\n", view.Text);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void EnterCommitsCompleteNoNewLine() {
            using (var view = new PythonEditor()) {
                view.AdvancedOptions.AddNewLineAtEndOfFullyTypedWord = true;
                view.AdvancedOptions.AutoListMembers = true;
                view.AdvancedOptions.AutoListIdentifiers = false;
                view.AdvancedOptions.HideAdvancedMembers = false;

                view.Type("min.__");

                using (var sh = view.View.WaitForSession<ICompletionSession>()) {
                    AssertUtil.ContainsAtLeast(sh.Session.Completions(), "__class__");

                    view.Type("class__\r");
                }
                Assert.AreEqual("min.__class__\r\n", view.Text);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void TabCommits() {
            using (var view = new PythonEditor()) {
                view.AdvancedOptions.EnterCommitsIntellisense = false;
                view.AdvancedOptions.AutoListMembers = true;
                view.AdvancedOptions.AutoListIdentifiers = false;
                view.AdvancedOptions.HideAdvancedMembers = false;

                view.Type("min.");

                using (var sh = view.View.WaitForSession<ICompletionSession>()) {
                    AssertUtil.ContainsAtLeast(sh.Session.Completions(), "__class__");

                    view.Type("class\t");
                }

                Assert.AreEqual("min.__class__", view.Text);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void DecoratorCompletions() {
            using (var view = new PythonEditor()) {
                view.Type("@");

                using (var sh = view.View.WaitForSession<ICompletionSession>()) {
                    AssertUtil.ContainsAtLeast(sh.Session.Completions(), "property", "staticmethod");
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void DecoratorNonCompletions() {
            using (var view = new PythonEditor()) {
                view.Type("a = b @");

                view.View.AssertNoIntellisenseSession();
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void AutoListIdentifierCompletions() {
            using (var view = new PythonEditor()) {
                view.AdvancedOptions.AutoListIdentifiers = true;

                view.Type("a = ");

                foreach (var c in "abcdefghijklmnopqrstuvwxyz_ABCDEFGHIJKLMNOPQRSTUVWXYZ") {
                    // x<space> should bring up a completion session
                    Console.WriteLine("Typing {0}", c);
                    view.Type(c.ToString());

                    using (var sh = view.View.WaitForSession<ICompletionSession>()) {
                        sh.Session.Dismiss();
                    }

                    view.Backspace();
                }

                view.View.AssertNoIntellisenseSession();

                // x<space> should not bring up a completion session
                // Don't check too many items, since asserting that no session
                // starts is slow.
                foreach (var c in "1234567890([{") {
                    Console.WriteLine("Typing {0}", c);
                    view.Type(c.ToString());

                    view.View.AssertNoIntellisenseSession();

                    view.Backspace();
                }
            }
        }

        private void AutoListTest(string code, params int[] triggerAtIndex) {
            AutoListTest(code, PythonLanguageVersion.V33, triggerAtIndex);
        }

        private void AutoListTest(string code, PythonLanguageVersion version, params int[] triggerAtIndex) {
            using (var view = new PythonEditor(version: version)) {
                view.AdvancedOptions.AutoListIdentifiers = true;
                view.AdvancedOptions.AutoListMembers = true;

                int lastStart = 0;
                string text;
                foreach (var _i in triggerAtIndex) {
                    bool expectCompletions = _i >= 0;
                    int expected = _i > 0 ? _i : -_i;

                    text = code.Substring(lastStart, expected - lastStart);
                    if (!string.IsNullOrEmpty(text)) {
                        Console.WriteLine("Typing '{0}' [{1}, {2})", text, lastStart, expected);
                        view.Type(text);

                        using (var sh = view.View.WaitForSession<ICompletionSession>(false)) {
                            // Having a session here is okay as long as nothing is selected
                            var hasCommittableCompletion = sh?.Session?.SelectedCompletionSet?.SelectionStatus?.IsSelected ?? false;
                            if (hasCommittableCompletion) {
                                sh.Session.Dismiss();
                                Assert.Fail($"Completion for {text} should not have any item selected");
                            } else if (sh != null) {

                                sh.Session.Dismiss();
                            }
                        }
                    }

                    lastStart = expected;

                    if (expectCompletions) {
                        text = code.Substring(expected, 1);
                        Console.WriteLine("Typing '{0}' [{1}, {2}) and expect completions", text, expected, expected + 1);
                        view.Type(text);

                        using (var sh = view.View.WaitForSession<ICompletionSession>()) {
                            sh.Session.Dismiss();
                        }

                        lastStart = expected + 1;
                    }
                }
                text = code.Substring(lastStart);
                if (!string.IsNullOrEmpty(text)) {
                    Console.WriteLine("Typing '{0}' [{1}, {2})", text, lastStart, code.Length);
                    view.Type(text);

                    view.View.AssertNoIntellisenseSession();
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void AutoListInDef() {
            AutoListTest("def fn(p:a, q=b) -> x", 9, 14, 20);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void AutoListInAssignment() {
            AutoListTest("a, b, c = a, b, c", 10, 13, 16);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void AutoListInClass() {
            AutoListTest("class F(o, p):", 8, 11);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void AutoListInLambda() {
            AutoListTest("a = lambda x, y: p", 4, 17);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void AutoListInWith() {
            AutoListTest("with a as b, c(x) as d:", 5, 13, -22);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void AutoListInLiterals() {
            AutoListTest("[a, b, c]", 1, 4, 7);
            AutoListTest("{a, b, c}", 1, 4, 7);
            AutoListTest("(a, b, c)", 1, 4, 7);
            AutoListTest("{a: b, c: d, e: f}", 1, 4, 7, 10, 13, 16);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void AutoListInComprehensions() {
            // TODO: Make completions trigger after spaces
            // eg: AutoListTest("[a for a in b]", 1, 3, 9, 12);

            AutoListTest("[a for a in b for c in d if x]", 1, -7, 12, -18, 23, 28);
            AutoListTest("{a for a in b for c in d if x}", 1, -7, 12, -18, 23, 28);
            AutoListTest("(a for a in b for c in d if x)", 1, -7, 12, -18, 23, 28);
            AutoListTest("{a: b for a, b in b for c, d in e if x}", 1, 4, -10, -13, 18, 32, 37);
            AutoListTest("x = [a for a in b for c in d if x]", 0, 5, -11, 16, -22, 27, 32);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void AutoListInStatements() {
            AutoListTest("assert a", 0, -6, 7);
            AutoListTest("a += b", 0, 5);
            AutoListTest("del a", 0, -3, 4);
            AutoListTest("exec a", PythonLanguageVersion.V27, -4, 5);
            AutoListTest("for a in b", -3, -5, 9);
            AutoListTest("if a", -2, 3);
            AutoListTest("global a", -6, 7);
            AutoListTest("nonlocal a", PythonLanguageVersion.V33, -8, 9);
            AutoListTest("print a", PythonLanguageVersion.V27, -5, 6);
            AutoListTest("return a", -6, 7);
            AutoListTest("while a", -5, 6);
            AutoListTest("yield a", -5, 6);
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void DisableAutoCompletions() {
            using (var view = new PythonEditor()) {
                view.AdvancedOptions.AutoListMembers = false;
                view.AdvancedOptions.AutoListIdentifiers = false;

                foreach (var t in new[] { "a", "a.", "import " }) {
                    Console.WriteLine("Typed " + t);
                    view.Type(t);

                    view.View.AssertNoIntellisenseSession();

                    view.Clear();
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void CompletionsAtEndOfLastChildScope() {
            using (var view = new PythonEditor(@"class A:
    def f(param1, param2):
        y = 234

        

class B:
    pass
")) {
                view.MoveCaret(5, 9);
                view.TypeAndWaitForAnalysis("p");
                view.MemberList();
                using (var sh = view.View.WaitForSession<ICompletionSession>()) {
                    AssertUtil.ContainsAtLeast(sh.Session.Completions(), "param1", "param2");
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void NewlineWithinComment() {
            using (var view = new PythonEditor(@"# comment")) {
                view.MoveCaret(1, 1);
                view.Enter();
                Assert.AreEqual(2, view.CurrentSnapshot.LineCount);
                Assert.AreEqual("", view.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
                Assert.AreEqual("# comment", view.CurrentSnapshot.GetLineFromLineNumber(1).GetText());
            }

            using (var view = new PythonEditor(@"# comment")) {
                view.MoveCaret(1, 3);
                view.Enter();
                Assert.AreEqual(2, view.CurrentSnapshot.LineCount);
                Assert.AreEqual("# ", view.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
                Assert.AreEqual("# comment", view.CurrentSnapshot.GetLineFromLineNumber(1).GetText());
            }

            using (var view = new PythonEditor(@"# comment")) {
                view.MoveCaret(1, 10);
                view.Enter();
                Assert.AreEqual(2, view.CurrentSnapshot.LineCount);
                Assert.AreEqual("# comment", view.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
                Assert.AreEqual("", view.CurrentSnapshot.GetLineFromLineNumber(1).GetText());
            }

            using (var view = new PythonEditor(@"    # comment")) {
                view.MoveCaret(1, 7);
                view.Enter();
                Assert.AreEqual(2, view.CurrentSnapshot.LineCount);
                Assert.AreEqual("    # ", view.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
                Assert.AreEqual("    # comment", view.CurrentSnapshot.GetLineFromLineNumber(1).GetText());
            }
        }
    }

    static class IntellisenseTestExtensions {
        public static IEnumerable<string> Completions(this ICompletionSession session) {
            Assert.IsNotNull(session);
            Assert.AreEqual(1, session.CompletionSets.Count);

            return session.CompletionSets[0].Completions.Select(x => x.InsertionText);
        }

        public static void Clear(this MockVsTextView view) {
            var snapshot = view.View.TextSnapshot;
            using (var edit = snapshot.TextBuffer.CreateEdit()) {
                edit.Delete(new Microsoft.VisualStudio.Text.Span(0, snapshot.Length));
                edit.Apply();
            }
        }

        public static PythonToolsService GetPyService(this MockVs session) {
            var service = session.ServiceProvider.GetPythonToolsService();
            Assert.IsNotNull(service, "PythonToolsService is unavailable");
            return service;
        }
    }
}

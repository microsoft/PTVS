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
using System.Text.RegularExpressions;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.MockVsTests;
using TestUtilities;

namespace PythonToolsMockTests {
    [TestClass]
    public class EditorTests {
        [ClassInitialize]
        public static void Initialize(TestContext context) {
            AssertListener.Initialize();
        }

        [TestMethod, TestCategory("Mock")]
        public void BuiltinFunctionSigHelp() {
            using (var view = new PythonEditor()) {
                view.Type("min(");

                for (int retries = 10; retries > 0; --retries) {
                    using (var sh = view.View.WaitForSession<ISignatureHelpSession>()) {
                        var doc = sh.Session.Signatures[0].Documentation;
                        if (doc.Contains("still being calculated")) {
                            view.VS.Sleep(100);
                            continue;
                        }
                        AssertUtil.AreEqual(new Regex(@"^min\(x\: object\).+?"), doc);
                        break;
                    }
                }
            }
        }

        [TestMethod, TestCategory("Mock")]
        public void BuiltinFunctionCompletions() {
            using (var view = new PythonEditor()) {
                view.Type("min.");

                using (var sh = view.View.WaitForSession<ICompletionSession>()) {
                    AssertUtil.Contains(sh.Session.Completions(), "__call__");
                }
            }
        }

        [TestMethod, TestCategory("Mock")]
        public void FilterCompletions() {
            using (var view = new PythonEditor()) {
                view.Type("min.");

                using (var sh = view.View.WaitForSession<ICompletionSession>()) {
                    AssertUtil.Contains(sh.Session.Completions(), "__call__");

                    view.Type("class");

                    AssertUtil.DoesntContain(sh.Session.Completions(), "__call__");
                }
            }
        }

        [TestMethod, TestCategory("Mock")]
        public void DotCompletes() {
            using (var view = new PythonEditor()) {
                view.Type("min.");

                using (var sh = view.View.WaitForSession<ICompletionSession>()) {
                    AssertUtil.Contains(sh.Session.Completions(), "__call__");

                    view.Type("class.");

                    Assert.AreEqual("min.__class__.", view.Text);
                }
            }
        }

        [TestMethod, TestCategory("Mock")]
        public void NonIdentifierDismisses() {
            using (var view = new PythonEditor()) {
                view.Type("min.");

                using (var sh = view.View.WaitForSession<ICompletionSession>()) {
                    AssertUtil.Contains(sh.Session.Completions(), "__call__");

                    view.Type("#");

                    Assert.IsTrue(sh.Session.IsDismissed);
                }
                view.View.AssertNoIntellisenseSession();
                Assert.AreEqual("min.#", view.Text);
            }
        }

        [TestMethod, TestCategory("Mock")]
        public void EnterCommits() {
            using (var view = new PythonEditor()) {
                view.Type("min.");

                using (var sh = view.View.WaitForSession<ICompletionSession>()) {
                    AssertUtil.ContainsAtLeast(sh.Session.Completions(), "__class__");
                    view.Type("class\r");
                }

                Assert.AreEqual("min.__class__", view.Text);
            }
        }

        [TestMethod, TestCategory("Mock")]
        public void EnterDismisses() {
            using (var view = new PythonEditor()) {
                view.AdvancedOptions.EnterCommitsIntellisense = false;
                view.AdvancedOptions.AutoListMembers = true;

                view.Type("min.");

                using (var sh = view.View.WaitForSession<ICompletionSession>()) {
                    AssertUtil.ContainsAtLeast(sh.Session.Completions(), "__class__");

                    view.Type("class\r");
                }
                Assert.AreEqual("min.class\r\n", view.Text);
            }
        }

        [TestMethod, TestCategory("Mock")]
        public void EnterCommitsCompleteNoNewLine() {
            using (var view = new PythonEditor()) {
                view.AdvancedOptions.AddNewLineAtEndOfFullyTypedWord = true;
                view.AdvancedOptions.AutoListMembers = true;
                view.AdvancedOptions.AutoListIdentifiers = false;
                view.AdvancedOptions.HideAdvancedMembers = false;

                view.Type("min.");

                using (var sh = view.View.WaitForSession<ICompletionSession>()) {
                    AssertUtil.ContainsAtLeast(sh.Session.Completions(), "__class__");

                    view.Type("__class__\r");
                }
                Assert.AreEqual("min.__class__\r\n", view.Text);
            }
        }

        [TestMethod, TestCategory("Mock")]
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

        [TestMethod, TestCategory("Mock")]
        public void DecoratorCompletions() {
            using (var view = new PythonEditor()) {
                view.Type("@");

                using (var sh = view.View.WaitForSession<ICompletionSession>()) {
                    AssertUtil.ContainsAtLeast(sh.Session.Completions(), "property", "staticmethod");
                }
            }
        }

        [TestMethod, TestCategory("Mock")]
        public void DecoratorNonCompletions() {
            using (var view = new PythonEditor()) {
                view.Type("a = b @");

                view.View.AssertNoIntellisenseSession();
            }
        }

        [TestMethod, TestCategory("Mock")]
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

                    view.View.Backspace();
                }

                view.View.AssertNoIntellisenseSession();

                // x<space> should not bring up a completion session
                // Don't check too many items, since asserting that no session
                // starts is slow.
                foreach (var c in "1234567890([{") {
                    Console.WriteLine("Typing {0}", c);
                    view.Type(c.ToString());

                    view.View.AssertNoIntellisenseSession();

                    view.View.Backspace();
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
                    Console.WriteLine("Typing '{0}' [{1}, {2})", text, lastStart, expected);
                    view.Type(text);

                    view.View.AssertNoIntellisenseSession();
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

        [TestMethod, TestCategory("Mock")]
        public void AutoListInDef() {
            AutoListTest("def fn(p:a, q=b) -> x", 9, 14, 20);
        }

        [TestMethod, TestCategory("Mock")]
        public void AutoListInAssignment() {
            AutoListTest("a, b, c = a, b, c", 10, 13, 16);
        }

        [TestMethod, TestCategory("Mock")]
        public void AutoListInClass() {
            AutoListTest("class F(o, p):", 8, 11);
        }

        [TestMethod, TestCategory("Mock")]
        public void AutoListInLambda() {
            AutoListTest("a = lambda x, y: p", 4, 17);
        }

        [TestMethod, TestCategory("Mock")]
        public void AutoListInWith() {
            AutoListTest("with a as b, c(x) as d:", 5, 13, -22);
        }

        [TestMethod, TestCategory("Mock")]
        public void AutoListInLiterals() {
            AutoListTest("[a, b, c]", 1, 4, 7);
            AutoListTest("{a, b, c}", 1, 4, 7);
            AutoListTest("(a, b, c)", 1, 4, 7);
            AutoListTest("{a: b, c: d, e: f}", 1, 4, 7, 10, 13, 16);
        }

        [TestMethod, TestCategory("Mock")]
        public void AutoListInComprehensions() {
            // TODO: Make completions trigger after spaces
            // eg: AutoListTest("[a for a in b]", 1, 3, 9, 12);

            AutoListTest("[a for a in b for c in d if x]", 1, 12, 23, 28);
            AutoListTest("{a for a in b for c in d if x}", 1, 12, 23, 28);
            AutoListTest("(a for a in b for c in d if x)", 1, 12, 23, 28);
            AutoListTest("{a: b for a, b in b for c, d in e if x}", 1, 4, 18, 32, 37);
        }

        [TestMethod, TestCategory("Mock")]
        public void AutoListInStatements() {
            AutoListTest("assert a", -6, 7);
            AutoListTest("a += b", 5);
            AutoListTest("del a", -3, 4);
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

        [TestMethod, TestCategory("Mock")]
        public void DisableAutoCompletions() {
            using (var view = new PythonEditor()) {
                view.AdvancedOptions.AutoListMembers = false;
                view.AdvancedOptions.AutoListIdentifiers = false;

                foreach (var t in new[] { "a", "a.", "import " }) {
                    Console.WriteLine("Typed " + t);
                    view.Type(t);

                    view.View.AssertNoIntellisenseSession();

                    view.View.Clear();
                }
            }
        }

        [TestMethod, TestCategory("Mock")]
        public void CompletionsAtEndOfLastChildScope() {
            using (var view = new PythonEditor(@"class A:
    def f(param1, param2):
        y = 234

        

class B:
    pass
")) {
                view.View.MoveCaret(5, 9);
                view.Type("p");
                view.View.MemberList();
                using (var sh = view.View.WaitForSession<ICompletionSession>()) {
                    AssertUtil.ContainsAtLeast(sh.Session.Completions(), "param1", "param2");
                }
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

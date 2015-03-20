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
        
        [TestMethod]
        public void BuiltinFunctionSigHelp() {
            var view = CreateViewAndAnalyze();
            view.Type("min(");

            var session = view.TopSession as ISignatureHelpSession;
            Assert.IsNotNull(session);

            AssertUtil.AreEqual(new Regex(@".+?min\(x\: object\).+?"), session.Signatures[0].Documentation);
        }

        [TestMethod]
        public void BuiltinFunctionCompletions() {
            var view = CreateViewAndAnalyze();
            view.Type("min.");

            var session = view.TopSession as ICompletionSession;
            
            AssertUtil.Contains(session.Completions(), "__call__");
        }

        [TestMethod]
        public void FilterCompletions() {
            var view = CreateViewAndAnalyze();
            view.Type("min.");

            var session = view.TopSession as ICompletionSession;

            AssertUtil.Contains(session.Completions(), "__call__");

            view.Type("class");

            AssertUtil.DoesntContain(session.Completions(), "__call__");
        }

        [TestMethod]
        public void DotCompletes() {
            var view = CreateViewAndAnalyze();
            view.Type("min.");

            var session = view.TopSession as ICompletionSession;

            AssertUtil.Contains(session.Completions(), "__call__");

            view.Type("class.");

            Assert.AreEqual("min.__class__.", view.Text);
        }

        [TestMethod]
        public void NonIdentifierDismisses() {
            var view = CreateViewAndAnalyze();
            view.Type("min.");

            var session = view.TopSession as ICompletionSession;
            AssertUtil.Contains(session.Completions(), "__call__");

            view.Type("#");

            Assert.IsNull(view.TopSession);
            Assert.AreEqual("min.#", view.Text);
        }

        [TestMethod]
        public void EnterCommits() {
            var view = CreateViewAndAnalyze();
            view.Type("min.");

            var session = view.TopSession as ICompletionSession;

            view.Type("class\r");

            Assert.AreEqual("min.__class__", view.Text);
        }

        [TestMethod]
        public void EnterDismisses() {
            var vs = new MockVs();
            vs.GetPyService().AdvancedOptions.EnterCommitsIntellisense = false;
            vs.GetPyService().AdvancedOptions.AutoListMembers = true;
            var view = CreateViewAndAnalyze(vs);
            view.Type("min.");

            var session = view.TopSession as ICompletionSession;

            view.Type("class\r");

            Assert.AreEqual("min.class\r\n", view.Text);
        }

        [TestMethod]
        public void EnterCommitsCompleteNoNewLine() {
            using (var vs = new MockVs()) {
                var opts = vs.GetPyService().AdvancedOptions;
                bool oldANL = opts.AddNewLineAtEndOfFullyTypedWord;
                bool oldALM = opts.AutoListMembers;
                bool oldALI = opts.AutoListIdentifiers;
                bool oldHAM = opts.HideAdvancedMembers;
                opts.AddNewLineAtEndOfFullyTypedWord = true;
                opts.AutoListMembers = true;
                opts.AutoListIdentifiers = false;
                opts.HideAdvancedMembers = false;
                vs.OnDispose(() => {
                    opts.AddNewLineAtEndOfFullyTypedWord = oldANL;
                    opts.AutoListMembers = oldALM;
                    opts.AutoListIdentifiers = oldALI;
                    opts.HideAdvancedMembers = oldHAM;
                });
                var view = CreateViewAndAnalyze(vs);
                view.Type("min.");

                var session = view.TopSession as ICompletionSession;

                view.Type("__class__\r");

                Assert.AreEqual("min.__class__\r\n", view.Text);
            }
        }

        [TestMethod]
        public void TabCommits() {
            using (var vs = new MockVs()) {
                var opts = vs.GetPyService().AdvancedOptions;
                bool oldECI = opts.EnterCommitsIntellisense;
                bool oldALM = opts.AutoListMembers;
                bool oldALI = opts.AutoListIdentifiers;
                bool oldHAM = opts.HideAdvancedMembers;
                opts.EnterCommitsIntellisense = false;
                opts.AutoListMembers = true;
                opts.AutoListIdentifiers = false;
                opts.HideAdvancedMembers = false;
                vs.OnDispose(() => {
                    opts.EnterCommitsIntellisense = oldECI;
                    opts.AutoListMembers = oldALM;
                    opts.AutoListIdentifiers = oldALI;
                    opts.HideAdvancedMembers = oldHAM;
                });

                var view = CreateViewAndAnalyze(vs);
                view.Type("min.");

                var session = view.TopSession as ICompletionSession;
                Console.WriteLine(string.Join("\n", session.Completions()));
                view.Type("class\t");

                Assert.AreEqual("min.__class__", view.Text);
            }
        }

        [TestMethod]
        public void DecoratorCompletions() {
            var view = CreateViewAndAnalyze();
            view.Type("@");

            var session = view.TopSession as ICompletionSession;

            AssertUtil.ContainsAtLeast(session.Completions(), "property", "staticmethod");
        }

        [TestMethod]
        public void DecoratorNonCompletions() {
            var view = CreateViewAndAnalyze();
            view.Type("a = b @");

            Assert.IsNull(view.TopSession as ICompletionSession);
        }

        [TestMethod]
        public void AutoListIdentifierCompletions() {
            using (var vs = new MockVs()) {
                var options = vs.GetPyService().AdvancedOptions;
                var oldALI = options.AutoListIdentifiers;
                options.AutoListIdentifiers = true;
                vs.OnDispose(() => options.AutoListIdentifiers = oldALI);

                var view = CreateViewAndAnalyze(vs);
                view.Type("a = ");

                foreach (var c in "abcdefghijklmnopqrstuvwxyz_ABCDEFGHIJKLMNOPQRSTUVWXYZ") {
                    // x<space> should bring up a completion session
                    Console.WriteLine("Typing {0}", c);
                    view.Type(c.ToString());

                    using (var sh = view.WaitForSession<ICompletionSession>()) {
                        sh.Session.Dismiss();
                    }

                    view.Backspace();
                }

                view.AssertNoIntellisenseSession();

                // x<space> should not bring up a completion session
                // Don't check too many items, since asserting that no session
                // starts is slow.
                foreach (var c in "1234567890([{") {
                    Console.WriteLine("Typing {0}", c);
                    view.Type(c.ToString());

                    view.AssertNoIntellisenseSession();

                    view.Backspace();
                }
            }
        }

        private void AutoListTest(string code, params int[] triggerAtIndex) {
            AutoListTest(code, PythonLanguageVersion.V33, triggerAtIndex);
        }

        private void AutoListTest(string code, PythonLanguageVersion version, params int[] triggerAtIndex) {
            using (var vs = new MockVs()) {
                var interpreterService = vs.GetPyService()._interpreterOptionsService;
                var oldDefault = interpreterService.DefaultInterpreter;
                interpreterService.DefaultInterpreter = interpreterService.Interpreters
                    .First(i => i.Configuration.Version == version.ToVersion());
                vs.OnDispose(() => interpreterService.DefaultInterpreter = oldDefault);

                var options = vs.GetPyService().AdvancedOptions;
                var oldALI = options.AutoListIdentifiers;
                var oldALM = options.AutoListMembers;
                options.AutoListIdentifiers = true;
                options.AutoListMembers = true;
                vs.OnDispose(() => {
                    options.AutoListIdentifiers = oldALI;
                    options.AutoListMembers = oldALM;
                });

                var view = CreateViewAndAnalyze(vs);

                int lastStart = 0;
                string text;
                foreach (var _i in triggerAtIndex) {
                    bool expectCompletions = _i >= 0;
                    int expected = _i > 0 ? _i : -_i;

                    text = code.Substring(lastStart, expected - lastStart);
                    Console.WriteLine("Typing '{0}' [{1}, {2})", text, lastStart, expected);
                    view.Type(text);

                    view.AssertNoIntellisenseSession();
                    lastStart = expected;

                    if (expectCompletions) {
                        text = code.Substring(expected, 1);
                        Console.WriteLine("Typing '{0}' [{1}, {2}) and expect completions", text, expected, expected + 1);
                        view.Type(text);

                        using (var sh = view.WaitForSession<ICompletionSession>()) {
                            sh.Session.Dismiss();
                        }

                        lastStart = expected + 1;
                    }
                }
                text = code.Substring(lastStart);
                if (!string.IsNullOrEmpty(text)) {
                    Console.WriteLine("Typing '{0}' [{1}, {2})", text, lastStart, code.Length);
                    view.Type(text);

                    view.AssertNoIntellisenseSession();
                }
            }
        }

        [TestMethod]
        public void AutoListInDef() {
            AutoListTest("def fn(p:a, q=b) -> x", 9, 14, 20);
        }

        [TestMethod]
        public void AutoListInAssignment() {
            AutoListTest("a, b, c = a, b, c", 10, 13, 16);
        }

        [TestMethod]
        public void AutoListInClass() {
            AutoListTest("class F(o, p):", 8, 11);
        }

        [TestMethod]
        public void AutoListInLambda() {
            AutoListTest("a = lambda x, y: p", 4, 17);
        }

        [TestMethod]
        public void AutoListInWith() {
            AutoListTest("with a as b, c as d:", 5, 13);
        }

        [TestMethod]
        public void AutoListInLiterals() {
            AutoListTest("[a, b, c]", 1, 4, 7);
            AutoListTest("{a, b, c}", 1, 4, 7);
            AutoListTest("(a, b, c)", 1, 4, 7);
            AutoListTest("{a: b, c: d, e: f}", 1, 4, 7, 10, 13, 16);
        }

        [TestMethod]
        public void AutoListInComprehensions() {
            // TODO: Make completions trigger after spaces
            // eg: AutoListTest("[a for a in b]", 1, 3, 9, 12);

            AutoListTest("[a for a in b for c in d if x]", 1, 12, 23, 28);
            AutoListTest("{a for a in b for c in d if x}", 1, 12, 23, 28);
            AutoListTest("(a for a in b for c in d if x)", 1, 12, 23, 28);
            AutoListTest("{a: b for a, b in b for c, d in e if x}", 1, 4, 18, 32, 37);
        }

        [TestMethod]
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

        [TestMethod]
        public void DisableAutoCompletions() {
            using (var vs = new MockVs()) {
                var options = vs.GetPyService().AdvancedOptions;
                var oldALM = options.AutoListMembers;
                var oldALI = options.AutoListIdentifiers;
                options.AutoListMembers = false;
                options.AutoListIdentifiers = false;
                vs.OnDispose(() => {
                    options.AutoListMembers = oldALM;
                    options.AutoListIdentifiers = oldALI;
                });

                var view = CreateViewAndAnalyze(vs);

                foreach (var t in new[] { "a", "a.", "import " }) {
                    Console.WriteLine("Typed " + t);
                    view.Type(t);

                    view.AssertNoIntellisenseSession();

                    view.Clear();
                }
            }
        }

        [TestMethod]
        public void CompletionsAtEndOfLastChildScope() {
            using (var vs = new MockVs()) {
                var view = CreateViewAndAnalyze(vs, @"class A:
    def f(param1, param2):
        y = 234

        

class B:
    pass
");

                view.MoveCaret(5, 9);
                view.Type("p");
                var controller = view.View.Properties.GetProperty<IntellisenseController>(typeof(IntellisenseController));
                controller.TriggerCompletionSession(false);
                using (var sh = view.WaitForSession<ICompletionSession>()) {
                    AssertUtil.ContainsAtLeast(
                        sh.Session.Completions(),
                        "param1",
                        "param2"
                    );
                }
            }
        }


        private static MockVsTextView CreateViewAndAnalyze(MockVs vs = null, string content = null) {
            if (vs == null) {
                vs = new MockVs();
                // Ensure these options are set correctly if we're creating the
                // instance. Otherwise, the caller is responsible.
                var opts = vs.GetPyService().AdvancedOptions;
                opts.AutoListMembers = true;
                opts.AutoListIdentifiers = false;
            }
            var view = vs.CreateTextView(PythonCoreConstants.ContentType, content ?? "");
            view.View.GetAnalyzer(vs.ServiceProvider).WaitForCompleteAnalysis(x => true);
            return view;
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

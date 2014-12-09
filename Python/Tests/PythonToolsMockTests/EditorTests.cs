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
using Microsoft.PythonTools;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools.MockVsTests;
using TestUtilities;

namespace PythonToolsMockTests {
    [TestClass]
    public class EditorTests {
        [TestMethod]
        public void BuiltinFunctionSigHelp() {
            var view = CreateViewAndAnalyze();
            view.Type("min(");

            var session = view.TopSession as ISignatureHelpSession;
            Assert.IsNotNull(session);

            Assert.AreNotEqual(-1, session.Signatures[0].Documentation.IndexOf("min(x: object)"));
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
            var view = CreateViewAndAnalyze(vs);
            view.Type("min.");

            var session = view.TopSession as ICompletionSession;

            view.Type("class\r");

            Assert.AreEqual("min.class\r\n", view.Text);
        }

        [TestMethod]
        public void EnterCommitsCompleteNoNewLine() {
            var vs = new MockVs();
            vs.GetPyService().AdvancedOptions.AddNewLineAtEndOfFullyTypedWord = true;
            var view = CreateViewAndAnalyze(vs);
            view.Type("min.");

            var session = view.TopSession as ICompletionSession;

            view.Type("__class__\r");

            Assert.AreEqual("min.__class__\r\n", view.Text);
        }

        [TestMethod]
        public void TabCommits() {
            var vs = new MockVs();
            vs.GetPyService().AdvancedOptions.EnterCommitsIntellisense = false;
            var view = CreateViewAndAnalyze(vs);
            view.Type("min.");

            var session = view.TopSession as ICompletionSession;

            view.Type("class\t");

            Assert.AreEqual("min.__class__", view.Text);
        }

        [TestMethod]
        public void DecoratorCompletions() {
            var view = CreateViewAndAnalyze();
            view.Type("@");

            var session = view.TopSession as ICompletionSession;

            AssertUtil.ContainsAtLeast(session.Completions(), "property", "staticmethod");
        }

        private static MockVsTextView CreateViewAndAnalyze(MockVs vs = null) {
            if (vs == null) {
                vs = new MockVs();
            }
            var view = vs.CreateTextView(
                PythonCoreConstants.ContentType,
                Path.Combine(Environment.CurrentDirectory, "foo.py")
            );
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

        public static PythonToolsService GetPyService(this MockVs session) {
            return (PythonToolsService)session.ServiceProvider.GetService(typeof(PythonToolsService));
        }
    }
}

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

#if !DEV14_OR_LATER

using System;
using System.Linq;
using System.Threading;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudioTools.MockVsTests;
using TestUtilities;
using TestUtilities.Mocks;
using TestUtilities.Python;

namespace PythonToolsTests {
#if DEV14_OR_LATER
#pragma warning disable 0618
#endif

    [TestClass]
    public class SmartTagTests {
        public static IContentType PythonContentType = new MockContentType(
            PythonCoreConstants.ContentType,
            new IContentType[0]
        );

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy(includeTestData: false);
        }

        [TestMethod]
        public void SmartTags1() {
            for (int i = 0; i < 4; ++i) {
                var buffer = new MockTextBuffer("sys", PythonContentType);
                var session = GetSmartTagSession(buffer, i);
                Assert.AreEqual(1, session.ActionSets.Count);
                AssertUtil.ContainsExactly(
                    session.ActionSets.SelectMany(s => s.Actions).Select(s => s.DisplayText),
                    "import sys"
                );
            }
        }

        [TestMethod]
        public void SmartTags2() {
            var buffer = new MockTextBuffer("executable", PythonContentType);
            var session = GetSmartTagSession(buffer, 1);
            Assert.AreEqual(1, session.ActionSets.Count);
            AssertUtil.ContainsExactly(
                session.ActionSets.SelectMany(s => s.Actions).Select(s => s.DisplayText),
                "from sys import executable"
            );
        }

        [TestMethod]
        public void NoSmartTagsInComment() {
            var buffer = new MockTextBuffer("# sys", PythonContentType);
            Assert.IsNull(GetSmartTagSession(buffer, -2, assertIfNoSession: false));
        }

        [TestMethod]
        public void NoSmartTagsInKnownName() {
            var buffer = new MockTextBuffer("sys = 123\nsys", PythonContentType);
            Assert.IsNull(GetSmartTagSession(buffer, -2, assertIfNoSession: false));
        }

        [TestMethod]
        public void NoSmartTagsInImport() {
            var buffer = new MockTextBuffer("import sys", PythonContentType);
            Assert.IsNull(GetSmartTagSession(buffer, -2, assertIfNoSession: false));
        }

        [TestMethod]
        public void NoSmartTagsInParameterName() {
            var buffer = new MockTextBuffer("def f(executable):\npass", PythonContentType);
            Assert.IsNull(GetSmartTagSession(buffer, 8, assertIfNoSession: false));
        }


        private static ISmartTagSession GetSmartTagSession(
            MockTextBuffer buffer,
            int index,
            PythonLanguageVersion version = PythonLanguageVersion.V27,
            bool assertIfNoSession = true
        ) {
            if (index < 0) {
                index += buffer.CurrentSnapshot.Length;
            }

            var fact = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(version.ToVersion());
            var sp = PythonToolsTestUtilities.CreateMockServiceProvider();
            var analyzer = new VsProjectAnalyzer(sp, fact, new[] { fact });
            buffer.AddProperty(typeof(VsProjectAnalyzer), analyzer);

            var classifierProvider = new PythonClassifierProvider(
                new MockContentTypeRegistryService(PythonCoreConstants.ContentType),
                sp
            );
            classifierProvider._classificationRegistry = new MockClassificationTypeRegistryService();
            classifierProvider.GetClassifier(buffer);

            var view = new MockTextView(buffer);
            var monitoredBuffer = analyzer.MonitorTextBuffer(view, buffer);
            analyzer.WaitForCompleteAnalysis(x => true);
            while (((IPythonProjectEntry)buffer.GetProjectEntry()).Analysis == null) {
                System.Threading.Thread.Sleep(500);
            }
            analyzer.StopMonitoringTextBuffer(monitoredBuffer.BufferParser, view);

            view.TextViewModel = new MockTextViewModel { DataBuffer = buffer, EditBuffer = buffer };
            var snapshot = buffer.CurrentSnapshot;
            var broker = new MockSmartTagBroker();

            broker.SourceProviders.Add(new SmartTagSourceProvider(sp));

            var cont = new SmartTagController(broker, view);
            view.Caret.MoveTo(new SnapshotPoint(snapshot, index));
            cont.ShowSmartTag();

            var session = broker.GetSessions(view).FirstOrDefault();
            Assert.IsNull(session, "Session should not be active");

            var task = Volatile.Read(ref cont._curTask);
            if (assertIfNoSession) {
                Assert.IsNotNull(task, "Session should have task running");
            } else if (task == null) {
                // No session and no task means we won't ever get a session here
                return null;
            }

            // Simulate repeatedly starting a session like the idle loop would
            for (int retries = 1000; session == null && retries > 0; --retries) {
                Thread.Sleep(10);
                cont.ShowSmartTag();

                session = broker.GetSessions(view).FirstOrDefault();
            }

            if (assertIfNoSession) {
                Assert.IsNotNull(session, "No session is active");
                Assert.IsFalse(session.IsDismissed, "Session should not be dismissed");
            }

            if (session != null) {
                Console.WriteLine("Found session with following tags:");
                foreach (var t in session.ActionSets.SelectMany(s => s.Actions).Select(s => s.DisplayText)) {
                    Console.WriteLine("  {0}", t);
                }
            }

            return session;
        }
    }
}

#endif
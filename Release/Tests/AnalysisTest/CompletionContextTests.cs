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

using System.Threading;
using IronPython.Hosting;
using Microsoft.IronPythonTools.Interpreter;
using Microsoft.PythonTools.Intellisense;
using Microsoft.Scripting.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Utilities;

namespace AnalysisTest.Mocks {
    [TestClass]
    public class CompletionContextTests {
        public static IContentType PythonContentType = new MockContentType("Python", new IContentType[0]);
        public static ScriptEngine PythonEngine = Python.CreateEngine();

        [TestMethod]
        public void Scenario_MemberCompletion() {
            // TODO: Negative tests
            //       Import / from import tests
            MemberCompletionTest(-1, "x = 2\r\nx.", "x.");
            
            // combining various partial expressions with previous expressions
            var prefixes = new[] { "", "(", "a = ", "f(", "l[", "{", "if " };
            var exprs = new[] { "x[0].", "x(0).", "x", "x.y.", "f(x[2]).", "f(x, y).", "f({2:3}).", "f(a + b).", "f(a or b).", "{2:3}.", "f(x if False else y).", /*"(\r\nx\r\n)."*/ };
            foreach (var prefix in prefixes) {
                foreach (var expr in exprs) {
                    string test = prefix + expr;
                    //Console.WriteLine("   -- {0}", test);
                    MemberCompletionTest(-1, test, expr);
                }
            }
            
            var sigs = new[] { 
                new { Expr = "f(", Param = 0, Function="f" } ,
                new { Expr = "f(1,", Param = 1, Function="f" },
                new { Expr = "f(1, 2,", Param = 2, Function="f" }, 
                new { Expr = "f(1, (1, 2),", Param = 2, Function="f" }, 
                new { Expr = "f(1, a + b,", Param = 2, Function="f" }, 
                new { Expr = "f(1, a or b,", Param = 2, Function="f" }, 
                new { Expr = "f(1, a if True else b,", Param = 2, Function="f" }, 
                new { Expr = "a.f(1, a if True else b,", Param = 2, Function="a.f" }, 
                new { Expr = "a().f(1, a if True else b,", Param = 2, Function="a().f" }, 
                new { Expr = "a(2, 3, 4).f(1, a if True else b,", Param = 2, Function="a(2, 3, 4).f" }, 
                new { Expr = "a(2, (3, 4), 4).f(1, a if True else b,", Param = 2, Function="a(2, (3, 4), 4).f" }, 
            };
            
            foreach (var prefix in prefixes) {
                foreach (var sig in sigs) {
                    var test  = prefix + sig.Expr;
                    //Console.WriteLine("   -- {0}", test);
                    SignatureTest(-1, test, sig.Function, sig.Param);
                }
            }
        }

        [TestMethod]
        public void Scenario_GotoDefinition() {
            string code = @"
class C:
    def fff(self): pass

C().fff";

            //var emptyAnalysis = AnalyzeExpression(0, code);
            //AreEqual(emptyAnalysis.Expression, "");

            for (int i = -1; i >= -3; i--) {
                var analysis = AnalyzeExpression(i, code);
                Assert.AreEqual(analysis.Expression, "C().fff");
            }

            var classAnalysis = AnalyzeExpression(-6, code);
            Assert.AreEqual(classAnalysis.Expression, "C()");

            var defAnalysis = AnalyzeExpression(code.IndexOf("def fff")+4, code);
            Assert.AreEqual(defAnalysis.Expression, "fff");
        }

        private static ExpressionAnalysis AnalyzeExpression(int location, string sourceCode) {
            if (location < 0) {
                location = sourceCode.Length + location + 1;
            }

            var analyzer = new ProjectAnalyzer(new IronPythonInterpreterFactory(), new MockErrorProviderFactory());
            var buffer = new MockTextBuffer(sourceCode);
            buffer.AddProperty(typeof(ProjectAnalyzer), analyzer);
            var textView = new MockTextView(buffer);
            var item = analyzer.MonitorTextBuffer(textView.TextBuffer); // We leak here because we never un-monitor, but it's a test.
            while (!item.ProjectEntry.IsAnalyzed) {
                Thread.Sleep(100);
            }
            
            var snapshot = (MockTextSnapshot)buffer.CurrentSnapshot;

            return snapshot.AnalyzeExpression(new MockTrackingSpan(snapshot, location, location == snapshot.Length ? 0 : 1));
        }

        private static void MemberCompletionTest(int location, string sourceCode, string expectedExpression) {
            if (location < 0) {
                location = sourceCode.Length + location + 1;
            }

            var analyzer = new ProjectAnalyzer(new IronPythonInterpreterFactory(), new MockErrorProviderFactory());
            var buffer = new MockTextBuffer(sourceCode);
            buffer.AddProperty(typeof(ProjectAnalyzer), analyzer);
            var snapshot = (MockTextSnapshot)buffer.CurrentSnapshot;
            var context = snapshot.GetCompletions(new MockTrackingSpan(snapshot, location, 0));
            Assert.AreEqual(context.Text, expectedExpression);            
        }

        private static void SignatureTest(int location, string sourceCode, string expectedExpression, int paramIndex) {
            if (location < 0) {
                location = sourceCode.Length + location;
            }

            var analyzer = new ProjectAnalyzer(new IronPythonInterpreterFactory(), new MockErrorProviderFactory());
            var buffer = new MockTextBuffer(sourceCode);
            buffer.AddProperty(typeof(ProjectAnalyzer), analyzer);
            var snapshot = (MockTextSnapshot)buffer.CurrentSnapshot;
            var context = snapshot.GetSignatures(new MockTrackingSpan(snapshot, location, 1));
            Assert.AreEqual(context.Text, expectedExpression);
            Assert.AreEqual(context.ParameterIndex, paramIndex);
        }

#if FALSE
        [TestMethod]
        public void Scenario_RemoteScriptFactory() {
            using (var factory = RemotePythonEvaluator.CreateFactory()) {
                var runtime = (ScriptRuntime)factory.CreateRuntime(Python.CreateRuntimeSetup(new Dictionary<string, object>()));
                StringWriter writer = new StringWriter();
                StringWriter errWriter = new StringWriter();

                runtime.IO.SetOutput(Stream.Null, writer);
                factory.SetConsoleOut(writer);
                factory.SetConsoleError(errWriter);

                // verify print goes to the correct output
                var engine = runtime.GetEngine("Python");
                engine.Execute("print 'hello'");
                var builder = writer.GetStringBuilder();

                Assert.AreEqual(builder.ToString(), "hello\r\n");
                builder.Clear();

                // verify Console.WriteLine is redirected
                engine.Execute("import System\nSystem.Console.WriteLine('hello')\n");
                Assert.AreEqual(builder.ToString(), "hello\r\n");
                builder.Clear();

                // verify Console.Error.WriteLine is redirected to stderr
                var errBuilder = errWriter.GetStringBuilder();
                engine.Execute("import System\nSystem.Console.Error.WriteLine('hello')\n");
                Assert.AreEqual(errBuilder.ToString(), "hello\r\n");
                errBuilder.Clear();

                // raise an exception, should be propagated back
                try {
                    engine.Execute("import System\nraise System.ArgumentException()\n");
                    Assert.AreEqual(true, false);
                } catch (ArgumentException) {
                }

                /*
                 // verify that all code runs on the same thread
                var scope = engine.CreateScope();
                engine.Execute("import System");

            
                List<object> res = new List<object>();
                for (int i = 0; i < 100; i++) {
                    ThreadPool.QueueUserWorkItem(
                        (x) => {
                            object value = engine.Execute("System.Threading.Thread.CurrentThread.ManagedThreadId", scope);
                            lock (res) {
                                res.Add(value);
                            }
                    });
                }

                while (res.Count != 100) {
                    Thread.Sleep(100);
                }

                for (int i = 1; i < res.Count; i++) {
                    if (!res[i - 1].Equals(res[i])) {
                        throw new Exception("running on multiple threads");
                    }
                }*/

                // create a long running work item, execute it, and then make sure we can continue to execute work items.
                ThreadPool.QueueUserWorkItem(x => {
                    engine.Execute("while True: pass");
                });
                Thread.Sleep(1000);
                factory.Abort();

                Assert.AreEqual((object)engine.Execute("42"), 42);
            }

            // check starting on an MTA thread
            using (var factory = new RemoteScriptFactory(ApartmentState.MTA)) {
                var runtime = (ScriptRuntime)factory.CreateRuntime(Python.CreateRuntimeSetup(new Dictionary<string, object>()));
                var engine = runtime.GetEngine("Python");
                Assert.AreEqual((object)engine.Execute("import System\nSystem.Threading.Thread.CurrentThread.ApartmentState == System.Threading.ApartmentState.MTA"), true);
            }

            // check starting on an STA thread
            using (var factory = new RemoteScriptFactory(ApartmentState.STA)) {
                var runtime = (ScriptRuntime)factory.CreateRuntime(Python.CreateRuntimeSetup(new Dictionary<string, object>()));
                var engine = runtime.GetEngine("Python");
                Assert.AreEqual((object)engine.Execute("import System\nSystem.Threading.Thread.CurrentThread.ApartmentState == System.Threading.ApartmentState.STA"), true);
            }
        }
#endif
    }
}

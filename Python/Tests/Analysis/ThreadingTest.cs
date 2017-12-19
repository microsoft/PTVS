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

extern alias analysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using analysis::Microsoft.PythonTools.Interpreter;
using analysis::Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;

namespace AnalysisTests {
    [TestClass]
    public class ThreadingTest {
        [TestInitialize]
        public void TestInitialize() {
            AnalysisLog.Reset();
            AssertListener.Initialize();
        }


        [TestMethod, Priority(0)]
        public void CrossThreadAnalysisCalls() {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var tasks = StartCrossThreadAnalysisCalls(cts.Token, PythonLanguageVersion.V34).ToArray();
            try {
                Task.WaitAny(tasks, cts.Token);
            } catch (OperationCanceledException) {
            }
            cts.Cancel();

            ExceptionDispatchInfo firstFail = null;
            bool multipleFail = false;
            foreach (var t in tasks) {
                if (t.IsCanceled || t.Exception == null) {
                    continue;
                }

                if (multipleFail) {
                    Console.WriteLine(t.Exception);
                } else if (firstFail != null) {
                    Console.WriteLine(firstFail);
                    Console.WriteLine(t.Exception);
                    firstFail = null;
                    multipleFail = true;
                } else if (t.Exception.InnerExceptions.Count == 1) {
                    firstFail = ExceptionDispatchInfo.Capture(t.Exception.InnerException);
                } else {
                    foreach (var exc in t.Exception.InnerExceptions) {
                        Console.WriteLine(exc);
                    }
                    multipleFail = true;
                }
            }

            if (multipleFail) {
                Assert.Fail("Errors occurred. See output for details.");
            } else if (firstFail != null) {
                firstFail.Throw();
            }
            
        }


        private static readonly IList<string> PythonTypes = new[] { "list", "tuple", "dict", "str" };

        private IEnumerable<Task> StartCrossThreadAnalysisCalls(
            CancellationToken cancel,
            PythonLanguageVersion version
        ) {
            var fact = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(version.ToVersion());
            var state = PythonAnalyzer.CreateSynchronously(fact);

            const string testCode = @"from mod{0:000} import test_func as other_test_func, MyClass as other_mc

c = None
def test_func(a, b={1}()):
    '''My test function'''
    globals c
    a = b
    a = {1}(a)
    b = other_test_func(a)
    c = other_mc.fn(b)
    return b

class MyClass:
    fn = test_func

my_test_func = test_func
my_test_func = other_test_func
my_test_func('abc')

mc = MyClass()
mc.fn([])
";

            var entries = Enumerable.Range(0, 100)
                .Select(i => {
                    var entry = state.AddModule(string.Format("mod{0:000}", i), string.Format("mod{0:000}.py", i));
                    using (var parser = Parser.CreateParser(new StringReader(testCode.FormatInvariant(i + 1, PythonTypes[i % PythonTypes.Count])), PythonLanguageVersion.V34))
                        entry.UpdateTree(parser.ParseFile(), null);
                    return entry;
                })
                .ToList();

            // One analysis before we start
            foreach (var e in entries) {
                e.Analyze(cancel, true);
            }
            state.AnalyzeQueuedEntries(cancel);

            // Repeatedly re-analyse the code
            yield return Task.Run(() => {
                var rnd = new Random();
                while (!cancel.IsCancellationRequested) {
                    var shufEntries = entries
                        .Select(e => Tuple.Create(rnd.Next(), e))
                        .OrderBy(t => t.Item1)
                        .Take(entries.Count / 2)
                        .Select(t => t.Item2)
                        .ToList();
                    foreach (var e in shufEntries) {
                        e.Analyze(cancel, true);
                    }

                    state.AnalyzeQueuedEntries(cancel);
                    Console.WriteLine("Analysis complete");
                    Thread.Sleep(1000);
                }
            }, cancel);

            // Repeatedly re-parse the code
            yield return Task.Run(() => {
                var rnd = new Random();
                while (!cancel.IsCancellationRequested) {
                    var shufEntries = entries
                        .Select((e, i) => Tuple.Create(rnd.Next(), e, i))
                        .OrderBy(t => t.Item1)
                        .Take(entries.Count / 4)
                        .ToList();
                    foreach (var t in shufEntries) {
                        var i = t.Item3;
                        using (var parser = Parser.CreateParser(new StringReader(testCode.FormatInvariant(i + 1, PythonTypes[i % PythonTypes.Count])), PythonLanguageVersion.V34))
                            t.Item2.UpdateTree(parser.ParseFile(), null);
                    }
                    Thread.Sleep(1000);
                }
            }, cancel);

            // Repeatedly request signatures
            yield return Task.Run(() => {
                var entry = entries[1];
                while (!cancel.IsCancellationRequested) {
                    var sigs = entry.Analysis.GetSignaturesByIndex("my_test_func", 0).ToList();

                    if (sigs.Any()) {
                        AssertUtil.ContainsExactly(
                            sigs.Select(s => s.Name),
                            "test_func"
                        );

                        foreach (var s in sigs) {
                            AssertUtil.ContainsExactly(s.Parameters.Select(p => p.Name), "a", "b");
                        }
                    }
                }
            }, cancel);

            // Repeated request variables and descriptions
            yield return Task.Run(() => {
                var entry = entries[1];
                while (!cancel.IsCancellationRequested) {
                    var descriptions = entry.Analysis.GetDescriptionsByIndex("my_test_func", 0).ToList();
                    descriptions = entry.Analysis.GetDescriptionsByIndex("c", 0).ToList();
                }
            }, cancel);

            // Repeated request members and documentation
            yield return Task.Run(() => {
                var entry = entries[1];
                while (!cancel.IsCancellationRequested) {
                    var descriptions = entry.Analysis.GetCompletionDocumentationByIndex("mc", "fn", 0).ToList();
                }
            }, cancel);
        }
    }
}

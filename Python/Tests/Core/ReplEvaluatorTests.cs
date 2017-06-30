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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools;
using TestUtilities;
using TestUtilities.Mocks;
using TestUtilities.Python;

namespace PythonToolsTests {
    [TestClass]
    public class ReplEvaluatorTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        [TestMethod, Priority(1)]
        public void ExecuteTest() {
            using (var evaluator = MakeEvaluator()) {
                var window = new MockReplWindow(evaluator);
                evaluator._Initialize(window);

                TestOutput(window, evaluator, "print 'hello'", true, "hello");
                TestOutput(window, evaluator, "42", true, "42");
                TestOutput(window, evaluator, "for i in xrange(2):  print i\n", true, "0", "1");
                TestOutput(window, evaluator, "raise Exception()\n", false, "Traceback (most recent call last):", "  File \"<stdin>\", line 1, in <module>", "Exception");

                TestOutput(window, evaluator, "try:\r\n    print 'hello'\r\nexcept:\r\n    print 'goodbye'\r\n    \r\n    ", true, "hello");
                TestOutput(window, evaluator, "try:\r\n    print 'hello'\r\nfinally:\r\n    print 'goodbye'\r\n    \r\n    ", true, "hello", "goodbye");

                TestOutput(window, evaluator, "import sys", true);
                TestOutput(window, evaluator, "sys.exit(0)", false);
            }
        }

        [TestMethod, Priority(3)]
        public void TestAbort() {
            using (var evaluator = MakeEvaluator()) {
                var window = new MockReplWindow(evaluator);
                evaluator._Initialize(window);

                TestOutput(
                    window,
                    evaluator,
                    "while True: pass\n",
                    false,
                    (completed) => {
                        Assert.IsTrue(!completed);
                        Thread.Sleep(1000);

                        evaluator.AbortExecution();
                    }, 
                    false, 
                    20000, 
                    "KeyboardInterrupt"
                );
            }
        }

        [TestMethod, Priority(1)]
        public void TestCanExecute() {
            using (var evaluator = MakeEvaluator()) {
                Assert.IsTrue(evaluator.CanExecuteCode("print 'hello'"));
                Assert.IsTrue(evaluator.CanExecuteCode("42"));
                Assert.IsTrue(evaluator.CanExecuteCode("for i in xrange(2):  print i\r\n\r\n"));
                Assert.IsTrue(evaluator.CanExecuteCode("raise Exception()\n"));

                Assert.IsFalse(evaluator.CanExecuteCode("try:\r\n    print 'hello'\r\nexcept:\r\n    print 'goodbye'\r\n    "));
                Assert.IsTrue(evaluator.CanExecuteCode("try:\r\n    print 'hello'\r\nexcept:\r\n    print 'goodbye'\r\n    \r\n"));
                Assert.IsFalse(evaluator.CanExecuteCode("try:\r\n    print 'hello'\r\nfinally:\r\n    print 'goodbye'\r\n    "));
                Assert.IsTrue(evaluator.CanExecuteCode("try:\r\n    print 'hello'\r\nfinally:\r\n    print 'goodbye'\r\n    \r\n"));
                Assert.IsFalse(evaluator.CanExecuteCode("x = \\"));
                Assert.IsTrue(evaluator.CanExecuteCode("x = \\\r\n42\r\n\r\n"));

                Assert.IsTrue(evaluator.CanExecuteCode(""));
                Assert.IsFalse(evaluator.CanExecuteCode(" "));
                Assert.IsFalse(evaluator.CanExecuteCode("# Comment"));
                Assert.IsTrue(evaluator.CanExecuteCode("\r\n"));
                Assert.IsFalse(evaluator.CanExecuteCode("\r\n#Comment"));
                Assert.IsFalse(evaluator.CanExecuteCode("# hello\r\n#world\r\n"));
            }
        }

        [TestMethod, Priority(3)]
        public async Task TestGetAllMembers() {
            using (var evaluator = MakeEvaluator()) {
                var window = new MockReplWindow(evaluator);
                await evaluator._Initialize(window);

                // Run the ExecuteText on another thread so that we don't continue
                // onto the REPL evaluation thread, which leads to GetMemberNames being
                // blocked as it's hogging the event loop.
                AutoResetEvent are = new AutoResetEvent(false);
                ThreadPool.QueueUserWorkItem(async (x) => {
                        await evaluator.ExecuteText("globals()['my_new_value'] = 123");
                        are.Set();
                    }
                );
                are.WaitOne(10000);
                var names = evaluator.GetMemberNames("");
                Assert.IsNotNull(names);
                AssertUtil.ContainsAtLeast(names.Select(m => m.Name), "my_new_value");
            }
        }

        [TestMethod, Priority(1)]
        public void ReplSplitCodeTest() {
            // http://pytools.codeplex.com/workitem/606

            var testCases = new[] {
                new { 
                    Code = @"def f():
    pass

def g():
    pass

f()
g()",
                    Expected = new[] { "def f():\r\n    pass\r\n", "def g():\r\n    pass\r\n", "f()", "g()" }
                },
                new {
                    Code = @"def f():
    pass

f()

def g():
    pass

f()
g()",
                    Expected = new[] { "def f():\r\n    pass\r\n", "f()", "def g():\r\n    pass\r\n", "f()", "g()" }
                },
                new {
                    Code = @"def f():
    pass

f()
f()

def g():
    pass

f()
g()",
                    Expected = new[] { "def f():\r\n    pass\r\n", "f()", "f()", "def g():\r\n    pass\r\n", "f()", "g()" }
                },
                new {
                    Code = @"    def f():
        pass

    f()
    f()

    def g():
        pass

    f()
    g()",
                    Expected = new[] { "def f():\r\n    pass\r\n", "f()", "f()", "def g():\r\n    pass\r\n", "f()", "g()" }
                },
                new {
                    Code = @"# Comment

f()
f()",
                    Expected = new[] { "# Comment\r\n\r\nf()\r\n", "f()" }
                }
            };

            using (var evaluator = MakeEvaluator()) {
                int counter = 0;
                foreach (var testCase in testCases) {
                    Console.WriteLine("Test case {0}", ++counter);
                    AssertUtil.AreEqual(ReplEditFilter.JoinToCompleteStatements(ReplEditFilter.SplitAndDedent(testCase.Code), Microsoft.PythonTools.Parsing.PythonLanguageVersion.V35), testCase.Expected);
                }
            }
        }

        private static PythonInteractiveEvaluator MakeEvaluator() {
            var python = PythonPaths.Python27 ?? PythonPaths.Python27_x64 ?? PythonPaths.Python26 ?? PythonPaths.Python26_x64;
            python.AssertInstalled();
            var provider = new SimpleFactoryProvider(python.InterpreterPath, python.InterpreterPath);
            var eval = new PythonInteractiveEvaluator(PythonToolsTestUtilities.CreateMockServiceProvider()) {
                Configuration = new LaunchConfiguration(python.Configuration)
            };
            Assert.IsTrue(eval._Initialize(new MockReplWindow(eval)).Result.IsSuccessful);
            return eval;
        }

        class SimpleFactoryProvider : IPythonInterpreterFactoryProvider {
            private readonly string _pythonExe;
            private readonly string _pythonWinExe;
            private readonly string _pythonLib;

            public SimpleFactoryProvider(string pythonExe, string pythonWinExe) {
                _pythonExe = pythonExe;
                _pythonWinExe = pythonWinExe;
                _pythonLib = Path.Combine(Path.GetDirectoryName(pythonExe), "Lib");
            }

            public IEnumerable<IPythonInterpreterFactory> GetInterpreterFactories() {
                yield return InterpreterFactoryCreator.CreateInterpreterFactory(new InterpreterConfiguration(
                    "Test Interpreter",
                    "Python 2.6 32-bit",
                    PathUtils.GetParent(_pythonExe),
                    _pythonExe,
                    _pythonWinExe,
                    "PYTHONPATH",
                    InterpreterArchitecture.x86,
                    new Version(2, 6),
                    InterpreterUIMode.CannotBeDefault
                ), new InterpreterFactoryCreationOptions {
                    WatchFileSystem = false
                });
            }

            public IEnumerable<InterpreterConfiguration> GetInterpreterConfigurations() {
                return GetInterpreterFactories().Select(x => x.Configuration);
            }

            public IPythonInterpreterFactory GetInterpreterFactory(string id) {
                return GetInterpreterFactories()
                    .Where(x => x.Configuration.Id == id)
                    .FirstOrDefault();
            }

            public object GetProperty(string id, string propName) => null;

            public event EventHandler InterpreterFactoriesChanged { add { } remove { } }
        }

        private static void TestOutput(MockReplWindow window, PythonInteractiveEvaluator evaluator, string code, bool success, params string[] expectedOutput) {
            TestOutput(window, evaluator, code, success, null, true, 3000, expectedOutput);
        }

        private static void TestOutput(MockReplWindow window, PythonInteractiveEvaluator evaluator, string code, bool success, Action<bool> afterExecute, bool equalOutput, int timeout = 3000, params string[] expectedOutput) {
            window.ClearScreen();

            bool completed = false;
            var task = evaluator.ExecuteText(code).ContinueWith(completedTask => {
                Assert.AreEqual(success, completedTask.Result.IsSuccessful);

                var output = success ? window.Output : window.Error;
                if (equalOutput) {
                    if (output.Length == 0) {
                        Assert.IsTrue(expectedOutput.Length == 0);
                    } else {
                        // don't count ending \n as new empty line
                        output = output.Replace("\r\n", "\n");
                        if (output[output.Length - 1] == '\n') {
                            output = output.Remove(output.Length - 1, 1);
                        }

                        var lines = output.Split('\n');
                        if (lines.Length != expectedOutput.Length) {
                            for (int i = 0; i < lines.Length; i++) {
                                Console.WriteLine("{0}: {1}", i, lines[i].ToString());
                            }
                        }

                        Assert.AreEqual(lines.Length, expectedOutput.Length);
                        for (int i = 0; i < expectedOutput.Length; i++) {
                            Assert.AreEqual(lines[i], expectedOutput[i]);
                        }
                    }
                } else {
                    foreach (var line in expectedOutput) {
                        Assert.IsTrue(output.Contains(line), string.Format("'{0}' does not contain '{1}'", output, line));
                    }
                }

                completed = true;
            });

            if (afterExecute != null) {
                afterExecute(completed);
            }

            try {
                task.Wait(timeout);
            } catch (AggregateException ex) {
                if (ex.InnerException != null) {
                    throw ex.InnerException;
                }
                throw;
            }

            if (!completed) {
                Assert.Fail(string.Format("command didn't complete in {0} seconds", timeout / 1000.0));
            }
        }

        [TestMethod, Priority(1)]
        public async Task NoInterpreterPath() {
            // http://pytools.codeplex.com/workitem/662

            var replEval = new PythonInteractiveEvaluator(PythonToolsTestUtilities.CreateMockServiceProvider()) {
                DisplayName = "Test Interpreter"
            };
            var replWindow = new MockReplWindow(replEval);
            await replEval._Initialize(replWindow);
            await replEval.ExecuteText("42");
            Console.WriteLine(replWindow.Error);
            Assert.IsTrue(
                replWindow.Error.Contains("Test Interpreter cannot be started"),
                "Expected: <Test Interpreter cannot be started>\r\nActual: <" + replWindow.Error + ">"
            );
        }

        [TestMethod, Priority(1)]
        public void BadInterpreterPath() {
            // http://pytools.codeplex.com/workitem/662

            var replEval = new PythonInteractiveEvaluator(PythonToolsTestUtilities.CreateMockServiceProvider()) {
                DisplayName = "Test Interpreter",
                Configuration = new LaunchConfiguration(new InterpreterConfiguration("InvalidInterpreter", "Test Interpreter", path: "C:\\Does\\Not\\Exist\\Some\\Interpreter.exe"))
            };
            var replWindow = new MockReplWindow(replEval);
            replEval._Initialize(replWindow);
            var execute = replEval.ExecuteText("42");
            var errorText = replWindow.Error;
            const string expected = "the associated Python environment could not be found.";

            if (!errorText.Contains(expected)) {
                Assert.Fail(string.Format(
                    "Did not find:\n{0}\n\nin:\n{1}",
                    expected,
                    errorText
                ));
            }
        }
    }
}

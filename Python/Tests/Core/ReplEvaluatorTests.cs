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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools;
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

        [TestMethod, Priority(0)]
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

        [TestMethod, Priority(0)]
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

                        evaluator.AbortCommand();
                    }, 
                    false, 
                    20000, 
                    "KeyboardInterrupt"
                );
            }
        }

        [TestMethod, Priority(0)]
        public void TestCanExecute() {
            using (var evaluator = MakeEvaluator()) {
                Assert.IsTrue(evaluator.CanExecuteText("print 'hello'"));
                Assert.IsTrue(evaluator.CanExecuteText("42"));
                Assert.IsTrue(evaluator.CanExecuteText("for i in xrange(2):  print i\r\n\r\n"));
                Assert.IsTrue(evaluator.CanExecuteText("raise Exception()\n"));

                Assert.IsTrue(evaluator.CanExecuteText("try:\r\n    print 'hello'\r\nexcept:\r\n    print 'goodbye'\r\n    \r\n    "));
                Assert.IsTrue(evaluator.CanExecuteText("try:\r\n    print 'hello'\r\nfinally:\r\n    print 'goodbye'\r\n    \r\n    "));
                Assert.IsFalse(evaluator.CanExecuteText("x = \\"));
                Assert.IsTrue(evaluator.CanExecuteText("x = \\\r\n42\r\n\r\n"));
            }
        }

        [TestMethod, Priority(0)]
        public async Task TestGetAllMembers() {
            using (var evaluator = MakeEvaluator()) {
                var window = new MockReplWindow(evaluator);
                await evaluator._Initialize(window);

                await evaluator.ExecuteText("globals()['my_new_value'] = 123");
                var names = evaluator.GetMemberNames("");
                Assert.IsNotNull(names);
                AssertUtil.ContainsAtLeast(names.Select(m => m.Name), "my_new_value");
            }
        }

        [TestMethod, Priority(0)]
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
                }
            };

            using (var evaluator = MakeEvaluator()) {
                foreach (var testCase in testCases) {
                    AssertUtil.AreEqual(evaluator.JoinCode(evaluator.SplitCode(testCase.Code)), testCase.Expected);
                }
            }
        }

        private static PythonReplEvaluator MakeEvaluator() {
            var python = PythonPaths.Python27 ?? PythonPaths.Python27_x64 ?? PythonPaths.Python26 ?? PythonPaths.Python26_x64;
            python.AssertInstalled();
            var provider = new SimpleFactoryProvider(python.InterpreterPath, python.InterpreterPath);
            var eval = new PythonReplEvaluator(provider.GetInterpreterFactories().First(), PythonToolsTestUtilities.CreateMockServiceProvider(), new ReplTestReplOptions());
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
                yield return InterpreterFactoryCreator.CreateInterpreterFactory(new InterpreterFactoryCreationOptions {
                    LanguageVersion = new Version(2, 6),
                    Description = "Python",
                    InterpreterPath = _pythonExe,
                    WindowInterpreterPath = _pythonWinExe,
                    LibraryPath = _pythonLib,
                    PathEnvironmentVariableName = "PYTHONPATH",
                    Architecture = ProcessorArchitecture.X86,
                    WatchLibraryForNewModules = false
                });
            }

            public event EventHandler InterpreterFactoriesChanged { add { } remove { } }
        }

        private static void TestOutput(MockReplWindow window, PythonReplEvaluator evaluator, string code, bool success, params string[] expectedOutput) {
            TestOutput(window, evaluator, code, success, null, true, 3000, expectedOutput);
        }

        private static void TestOutput(MockReplWindow window, PythonReplEvaluator evaluator, string code, bool success, Action<bool> afterExecute, bool equalOutput, int timeout = 3000, params string[] expectedOutput) {
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

        [TestMethod, Priority(0)]
        public void NoInterpreterPath() {
            // http://pytools.codeplex.com/workitem/662

            var emptyFact = InterpreterFactoryCreator.CreateInterpreterFactory(
                new InterpreterFactoryCreationOptions() {
                    Description = "Test Interpreter"
                }
            );
            var replEval = new PythonReplEvaluator(emptyFact, PythonToolsTestUtilities.CreateMockServiceProvider(), new ReplTestReplOptions());
            var replWindow = new MockReplWindow(replEval);
            replEval._Initialize(replWindow);
            var execute = replEval.ExecuteText("42");
            Console.WriteLine(replWindow.Error);
            Assert.IsTrue(
                replWindow.Error.Contains("Test Interpreter cannot be started"),
                "Expected: <Test Interpreter cannot be started>\r\nActual: <" + replWindow.Error + ">"
            );
        }

        [TestMethod, Priority(0)]
        public void BadInterpreterPath() {
            // http://pytools.codeplex.com/workitem/662

            var emptyFact = InterpreterFactoryCreator.CreateInterpreterFactory(
                new InterpreterFactoryCreationOptions() {
                    Description = "Test Interpreter",
                    InterpreterPath = "C:\\Does\\Not\\Exist\\Some\\Interpreter.exe"
                }
            );
            var replEval = new PythonReplEvaluator(emptyFact, PythonToolsTestUtilities.CreateMockServiceProvider(), new ReplTestReplOptions());
            var replWindow = new MockReplWindow(replEval);
            replEval._Initialize(replWindow);
            var execute = replEval.ExecuteText("42");
            var errorText = replWindow.Error;
            const string expected = 
                "The interactive window could not be started because the associated Python environment could not be found.\r\n" +
                "If this version of Python has recently been uninstalled, you can close this window.\r\n" +
                "Current interactive window is disconnected.";

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

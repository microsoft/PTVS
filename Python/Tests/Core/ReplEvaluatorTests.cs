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
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Mocks;
using TestUtilities.Python;

namespace PythonToolsTests {
#if INTERACTIVE_WINDOW
    using IReplEvaluator = IInteractiveEngine;
    using IReplWindow = IInteractiveWindow;
    using IReplWindowProvider = IInteractiveWindowProvider;
#endif

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
                evaluator.Initialize(window);

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
                evaluator.Initialize(window);

                TestOutput(window, evaluator, "while True: pass\n", false, (completed) => {
                        Thread.Sleep(200);
                        Assert.IsTrue(!completed);

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
                Assert.AreEqual(evaluator.CanExecuteText("print 'hello'"), true);
                Assert.AreEqual(evaluator.CanExecuteText("42"), true);
                Assert.AreEqual(evaluator.CanExecuteText("for i in xrange(2):  print i\r\n\r\n"), true);
                Assert.AreEqual(evaluator.CanExecuteText("raise Exception()\n"), true);

                Assert.AreEqual(evaluator.CanExecuteText("try:\r\n    print 'hello'\r\nexcept:\r\n    print 'goodbye'\r\n    \r\n    "), true);
                Assert.AreEqual(evaluator.CanExecuteText("try:\r\n    print 'hello'\r\nfinally:\r\n    print 'goodbye'\r\n    \r\n    "), true);
            }
        }

        private static PythonReplEvaluator MakeEvaluator() {
            var python = PythonPaths.Python27 ?? PythonPaths.Python27_x64 ?? PythonPaths.Python26 ?? PythonPaths.Python26_x64;
            python.AssertInstalled();
            var provider = new SimpleFactoryProvider(python.Path, python.Path);
            return new PythonReplEvaluator(provider.GetInterpreterFactories().First(), null, new ReplTestReplOptions());
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
                Assert.AreEqual(completedTask.Result.IsSuccessful, success);

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
                        Assert.IsTrue(output.IndexOf(line) != -1);
                    }
                }

                completed = true;
            });

            if (afterExecute != null) {
                afterExecute(completed);
            }

            task.Wait(timeout);

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
            var replEval = new PythonReplEvaluator(emptyFact, null, new ReplTestReplOptions());
            var replWindow = new MockReplWindow(replEval);
            replEval.Initialize(replWindow);
            var execute = replEval.ExecuteText("42");
            Assert.IsTrue(replWindow.Error.IndexOf("The interpreter Test Interpreter cannot be started.  The path to the interpreter has not been configured.") != -1);
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
            var replEval = new PythonReplEvaluator(emptyFact, null, new ReplTestReplOptions());
            var replWindow = new MockReplWindow(replEval);
            replEval.Initialize(replWindow);
            var execute = replEval.ExecuteText("42");
            Assert.IsTrue(replWindow.Error.IndexOf("Failed to start interactive process, the interpreter could not be found: C:\\Does\\Not\\Exist\\Some\\Interpreter.exe") != -1);
        }    
    }


    class ReplTestReplOptions : PythonReplEvaluatorOptions {
        public override bool EnableAttach {
            get { return true; }
        }

        public override string InterpreterOptions {
            get { return ""; }
        }

        public override string WorkingDirectory {
            get { return ""; }
        }

        public override string StartupScript {
            get { return null; }
        }

        public override string SearchPaths {
            get { return ""; }
        }

        public override string InterpreterArguments {
            get { return ""; }
        }

        public override VsProjectAnalyzer ProjectAnalyzer {
            get { return null; }
        }

        public override bool UseInterpreterPrompts {
            get { return true; }
        }

        public override string ExecutionMode {
            get { return ""; }
        }

        public override bool InlinePrompts {
            get { return false; }
        }

        public override bool ReplSmartHistory {
            get { return false; }
        }

        public override bool LiveCompletionsOnly {
            get { return false; }
        }

        public override string PrimaryPrompt {
            get { return ">>>"; }
        }

        public override string SecondaryPrompt {
            get { return "..."; }
        }
    }
}

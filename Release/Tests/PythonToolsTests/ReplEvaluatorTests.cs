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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities.Mocks;

namespace PythonToolsTests {
#if INTERACTIVE_WINDOW
    using IReplEvaluator = IInteractiveEngine;
    using IReplWindow = IInteractiveWindow;
    using IReplWindowProvider = IInteractiveWindowProvider;
#endif

    [TestClass]
    public class ReplEvaluatorTests {

        [TestMethod]
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

        [TestMethod]
        public void TestAbort() {
            using (var evaluator = MakeEvaluator()) {
                var window = new MockReplWindow(evaluator);
                evaluator.Initialize(window);

                TestOutput(window, evaluator, "while True: pass\n", false, (completed) => {
                    Thread.Sleep(200);
                    Assert.IsTrue(!completed);

                    evaluator.AbortCommand();
                }, "Traceback (most recent call last):", "  File \"<stdin>\", line 1, in <module>", "KeyboardInterrupt");
            }
        }

        [TestMethod]
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

        private static string FindPythonInterpreterDir(string version) {
            return (from path in Environment.GetEnvironmentVariable("PATH").Split(Path.PathSeparator)
                    let exePath = Path.Combine(path, "python.exe")
                    where File.Exists(exePath) && path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).EndsWith(version, StringComparison.OrdinalIgnoreCase)
                    select path).FirstOrDefault() ?? @"C:\Python" + version;
        }

        private static PythonReplEvaluator MakeEvaluator() {
            string pythonDir = FindPythonInterpreterDir("26");
            string pythonExe = Path.Combine(pythonDir, "python.exe");
            string pythonWinExe = Path.Combine(pythonDir, "pythonw.exe");
            
            return new PythonReplEvaluator(new SimpleFactoryProvider(pythonExe, pythonWinExe), Guid.Empty, new Version(2,6), null);
        }

        class SimpleFactoryProvider : IPythonInterpreterFactoryProvider {
            private readonly string _pythonExe;
            private readonly string _pythonWinExe;

            public SimpleFactoryProvider(string pythonExe, string pythonWinExe) {
                _pythonExe = pythonExe;
                _pythonWinExe = pythonWinExe;
            }
            
            public IEnumerable<IPythonInterpreterFactory> GetInterpreterFactories() {
                yield return new CPythonInterpreterFactory(new Version(2, 6), Guid.Empty, "Python", _pythonExe, _pythonWinExe, "PYTHONPATH", System.Reflection.ProcessorArchitecture.X86);
            }
        }

        private static void TestOutput(MockReplWindow window, PythonReplEvaluator evaluator, string code, bool success, params string[] expectedOutput) {
            TestOutput(window, evaluator, code, success, null, expectedOutput);
        }

        private static void TestOutput(MockReplWindow window, PythonReplEvaluator evaluator, string code, bool success, Action<bool> afterExecute, params string[] expectedOutput) {
            window.ClearScreen();

            bool completed = false;
            var task = evaluator.ExecuteText(code).ContinueWith(completedTask => {
                Assert.AreEqual(completedTask.Result.IsSuccessful, success);

                var output = window.Output;
                if (output.Length == 0) {
                    Assert.IsTrue(expectedOutput.Length == 0);
                } else {
                    // don't count ending \n as new empty line                    
                    output = output.Replace("\r\n", "\n");
                    if (output[output.Length - 1] == '\n') {
                        output.Remove(output.Length - 1, 1);
                    }

                    var lines = output.Split('\n');
                    if (lines.Length != expectedOutput.Length) {
                        Console.WriteLine(output.ToString());
                    }

                    Assert.AreEqual(lines.Length, expectedOutput.Length);
                    for (int i = 0; i < expectedOutput.Length; i++) {
                        Assert.AreEqual(lines[i], expectedOutput[i]);
                    }
                }

                completed = true;
            });

            if (afterExecute != null) {
                afterExecute(completed);
            }

            task.Wait(3000);

            if (!completed) {
                Assert.Fail("command didn't complete in 3 seconds");
            }
        }
    }
}

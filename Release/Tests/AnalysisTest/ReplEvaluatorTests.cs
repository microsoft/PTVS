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
using System.Text;
using System.Threading;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnalysisTest {
    [TestClass]
    [DeploymentItem(@"..\\PythonTools\\CompletionDB\\", "CompletionDB")]
    [DeploymentItem(@"..\\PythonTools\\visualstudio_py_repl.py")]
    public class ReplEvaluatorTests {

        [TestMethod]
        public void ExecuteTest() {
            using (var evaluator = MakeEvaluator()) {
                var window = new MockReplWindow();
                evaluator.Start(window);                

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
                var window = new MockReplWindow();
                evaluator.Start(window);

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

        private static PythonReplEvaluator MakeEvaluator() {
            return new PythonReplEvaluator(new CPythonInterpreterFactory(new Version(2, 6), Guid.Empty, "Python", "C:\\Python26\\python.exe", "C:\\Python26\\pythonw.exe", "PYTHONPATH", System.Reflection.ProcessorArchitecture.X86), null);
        }

        private static void TestOutput(MockReplWindow window, PythonReplEvaluator evaluator, string code, bool success, params string[] expectedOutput) {
            TestOutput(window, evaluator, code, success, null, expectedOutput);
        }

        private static void TestOutput(MockReplWindow window, PythonReplEvaluator evaluator, string code, bool success, Action<bool> afterExecute, params string[] expectedOutput) {
            StringBuilder output = window.Output;
            output.Clear();

            bool completed = false;
            evaluator.ExecuteText(code, (result) => {
                Assert.AreEqual(result.Success, success);

                if (output.Length == 0) {
                    Assert.IsTrue(expectedOutput.Length == 0);
                } else {
                    // don't count ending \n as new empty line                    
                    output.Replace("\r\n", "\n");
                    if (output[output.Length - 1] == '\n') {
                        output.Remove(output.Length - 1, 1);
                    }

                    var lines = output.ToString().Split('\n');
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

            for (int i = 0; i < 30 && !completed; i++) {
                Thread.Sleep(100);
            }

            if (!completed) {
                Assert.Fail("command didn't complete in 3 seconds");
            }
        }

        class MockReplWindow : IReplWindow {
            public StringBuilder Output = new StringBuilder();

            #region IReplWindow Members

            public event Action ReadyForInput;

            public Microsoft.VisualStudio.Text.Editor.IWpfTextView TextView {
                get { throw new NotImplementedException(); }
            }

            public Microsoft.VisualStudio.Text.ITextBuffer CurrentLanguageBuffer {
                get { throw new NotImplementedException(); }
            }

            public IReplEvaluator Evaluator {
                get { throw new NotImplementedException(); }
            }

            public string Title {
                get { throw new NotImplementedException(); }
            }

            public void ClearHistory() {
                throw new NotImplementedException();
            }

            public void ClearScreen() {
                throw new NotImplementedException();
            }

            public void Focus() {
                throw new NotImplementedException();
            }

            public void Cancel() {
                throw new NotImplementedException();
            }

            public void InsertCode(string text) {
                throw new NotImplementedException();
            }

            public void Submit(IEnumerable<string> inputs) {
                throw new NotImplementedException();
            }

            public void Reset() {
                throw new NotImplementedException();
            }

            public void AbortCommand() {
                throw new NotImplementedException();
            }

            public void WriteLine(string text) {
                throw new NotImplementedException();
            }

            public void WriteOutput(object value) {
                Output.Append(value.ToString());
            }

            public void WriteError(object value) {
                Output.Append(value.ToString());
            }

            public string ReadStandardInput() {
                throw new NotImplementedException();
            }

            public void SetOptionValue(ReplOptions option, object value) {
            }

            public object GetOptionValue(ReplOptions option) {
                throw new NotImplementedException();
            }

            #endregion

            
        }
    }
}

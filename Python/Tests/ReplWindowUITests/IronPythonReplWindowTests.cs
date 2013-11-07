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

using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using Microsoft.IronPythonTools.Interpreter;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Repl;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text.Classification;
using PythonToolsTests;
using TestUtilities;
using TestUtilities.Mocks;
using TestUtilities.UI.Python;
using Keyboard = TestUtilities.UI.Keyboard;

namespace ReplWindowUITests {
#if PY_ALL || PY_IRON27
    [TestClass]
    public class IronPythonReplTests : Python27ReplWindowTests {
        [TestInitialize]
        public new void Initialize() {
            TestInitialize();
        }

        protected override string InterpreterDescription {
            get {
                return "IronPython 2.7 Interactive";
            }
        }

        protected override PythonVersion PythonVersion {
            get {
                return PythonPaths.IronPython27;
            }
        }

        protected override bool IPythonSupported {
            get {
                return false;
            }
        }

        protected override string SourceFileName {
            get {
                return "string";
            }
        }

        protected override bool KeyboardInterruptHasTracebackHeader {
            get {
                return false;
            }
        }

        protected override bool CanRedirectSubprocess {
            get {
                return false;
            }
        }

        private IPythonInterpreterFactory IronPythonInterpreter {
            get {
                var provider = new IronPythonInterpreterFactoryProvider();
                return provider.GetInterpreterFactories().First();
            }
        }

        /// <summary>
        /// “x = 42”
        /// “x.” should bring up intellisense completion
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ResetRepl() {
            using (var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte)) {
                var interactive = Prepare(app);
                try {
                    const string code = "x = 42";
                    Keyboard.Type(code + "\r");

                    interactive.WaitForText(ReplPrompt + code, ReplPrompt);

                    Keyboard.Type("x.");

                    interactive.WaitForText(ReplPrompt + code, ReplPrompt + "x.");

                    using (var sh = interactive.WaitForSession<ICompletionSession>()) {
                        Assert.IsNotNull(sh.Session.SelectedCompletionSet);
                    }
                    Keyboard.Type(Key.Back);
                    Keyboard.Type(Key.Back);

                    interactive.Reset();

                    Keyboard.Type("x.");

                    System.Threading.Thread.Sleep(1000);

                    // and make sure we have no completions for the old buffers
                    var sessionStack = interactive.IntellisenseSessionStack;
                    Assert.IsNull(sessionStack.TopSession);
                } finally {
                    interactive.WaitForSessionDismissed();
                }
            }
        }

        [TestMethod, Priority(0)]
        public void IronPythonModuleName() {
            var replEval = new PythonReplEvaluator(IronPythonInterpreter, null, new ReplTestReplOptions());
            var replWindow = new MockReplWindow(replEval);
            replEval.Initialize(replWindow);
            replWindow.ClearScreen();
            var execute = replEval.ExecuteText("__name__");
            execute.Wait();
            Assert.AreEqual(execute.Result, ExecutionResult.Success);
            Assert.AreEqual(replWindow.Output, "'__main__'\r\n");
            replWindow.ClearScreen();
        }

        [TestMethod, Priority(0)]
        public void IronPythonSignatures() {
            var replEval = new PythonReplEvaluator(IronPythonInterpreter, null, new ReplTestReplOptions());
            var replWindow = new MockReplWindow(replEval);
            replEval.Initialize(replWindow);
            var execute = replEval.ExecuteText("from System import Array");
            execute.Wait();
            Assert.AreEqual(execute.Result, ExecutionResult.Success);

            var sigs = replEval.GetSignatureDocumentation("Array[int]");
            Assert.AreEqual(sigs.Length, 1);
            Assert.AreEqual("Array[int](: int)\r\n", sigs[0].Documentation);
        }

        [TestMethod, Priority(0)]
        public void IronPythonCommentInput() {
            // http://pytools.codeplex.com/workitem/649
            var replEval = new PythonReplEvaluator(IronPythonInterpreter, null, new ReplTestReplOptions());
            var replWindow = new MockReplWindow(replEval);
            replEval.Initialize(replWindow);
            var execute = replEval.ExecuteText("#fob\n1+2");
            execute.Wait();
            Assert.AreEqual(execute.Result, ExecutionResult.Success);
        }

        [TestMethod, Priority(0)]
        public void ConsoleWriteLineTest() {
            // http://pytools.codeplex.com/workitem/649
            var replEval = new PythonReplEvaluator(IronPythonInterpreter, null, new ReplTestReplOptions());
            var replWindow = new MockReplWindow(replEval);
            replEval.Initialize(replWindow);
            var execute = replEval.ExecuteText("import System");
            execute.Wait();
            Assert.AreEqual(execute.Result, ExecutionResult.Success);
            replWindow.ClearScreen();

            execute = replEval.ExecuteText("System.Console.WriteLine(42)");
            execute.Wait();
            Assert.AreEqual(replWindow.Output, "42\r\n");
            replWindow.ClearScreen();

            Assert.AreEqual(execute.Result, ExecutionResult.Success);

            execute = replEval.ExecuteText("System.Console.Write(42)");
            execute.Wait();

            Assert.AreEqual(execute.Result, ExecutionResult.Success);

            Assert.AreEqual(replWindow.Output, "42");
        }

        [TestMethod, Priority(0)]
        public void GenericMethodCompletions() {
            // http://pytools.codeplex.com/workitem/661
            var fact = IronPythonInterpreter;
            var replEval = new PythonReplEvaluator(fact, null, new ReplTestReplOptions());
            var replWindow = new MockReplWindow(replEval);
            replEval.Initialize(replWindow);
            var execute = replEval.ExecuteText("from System.Threading.Tasks import Task");
            execute.Wait();
            Assert.AreEqual(execute.Result, ExecutionResult.Success);
            replWindow.ClearScreen();

            execute = replEval.ExecuteText("def func1(): print 'hello world'\r\n\r\n");
            execute.Wait();
            replWindow.ClearScreen();

            Assert.AreEqual(execute.Result, ExecutionResult.Success);

            execute = replEval.ExecuteText("t = Task.Factory.StartNew(func1)");
            execute.Wait();
            Assert.AreEqual(execute.Result, ExecutionResult.Success);

            using (var analyzer = new VsProjectAnalyzer(fact, new[] { fact }, new MockErrorProviderFactory())) {
                replWindow.TextView.TextBuffer.Properties.AddProperty(typeof(VsProjectAnalyzer), analyzer);

                var names = replEval.GetMemberNames("t");
                foreach (var name in names) {
                    Debug.WriteLine(name.Name);
                }
            }
        }

        [TestMethod, Priority(0)]
        public void NoTraceFunction() {
            // http://pytools.codeplex.com/workitem/662
            var replEval = new PythonReplEvaluator(IronPythonInterpreter, null, new ReplTestReplOptions());
            var replWindow = new MockReplWindow(replEval);
            replEval.Initialize(replWindow);
            var execute = replEval.ExecuteText("import sys");
            execute.Wait();
            Assert.AreEqual(execute.Result, ExecutionResult.Success);
            replWindow.ClearScreen();

            execute = replEval.ExecuteText("sys.gettrace()");
            execute.Wait();
            Assert.AreEqual(replWindow.Output, "");
            replWindow.ClearScreen();
        }

        [TestMethod, Priority(0)]
        public void CommentFollowedByBlankLine() {
            // http://pytools.codeplex.com/workitem/659
            var replEval = new PythonReplEvaluator(IronPythonInterpreter, null, new ReplTestReplOptions());
            var replWindow = new MockReplWindow(replEval);
            replEval.Initialize(replWindow);
            var execute = replEval.ExecuteText("# fob\r\n\r\n    \r\n\t\t\r\na = 42");
            execute.Wait();
            Assert.AreEqual(execute.Result, ExecutionResult.Success);
            replWindow.ClearScreen();
        }



        [TestMethod, Priority(0)]
        public void AttachSupportMultiThreaded() {
            // http://pytools.codeplex.com/workitem/663
            var replEval = new PythonReplEvaluator(IronPythonInterpreter, null, new ReplTestReplOptions());
            var replWindow = new MockReplWindow(replEval);
            replEval.Initialize(replWindow);
            var code = new[] {
                "import threading",
                "def sayHello():\r\n    pass",
                "t1 = threading.Thread(target=sayHello)",
                "t1.start()",
                "t2 = threading.Thread(target=sayHello)",
                "t2.start()"
            };
            foreach (var line in code) {
                var execute = replEval.ExecuteText(line);
                execute.Wait();
                Assert.AreEqual(execute.Result, ExecutionResult.Success);
            }

            replWindow.ClearScreen();
            var finalExecute = replEval.ExecuteText("42");
            finalExecute.Wait();
            Assert.AreEqual(finalExecute.Result, ExecutionResult.Success);
            Assert.AreEqual(replWindow.Output, "42\r\n");
        }
    }


    [TestClass]
    public class IronPythonx64ReplTests : IronPythonReplTests {
        [TestInitialize]
        public new void Initialize() {
            TestInitialize();
        }

        protected override string InterpreterDescription {
            get {
                return "IronPython 64-bit 2.7 Interactive";
            }
        }

        protected override PythonVersion PythonVersion {
            get {
                return PythonPaths.IronPython27_x64;
            }
        }
    }
#endif
}


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
using System.Diagnostics;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;
using TestUtilities.UI.Python;

namespace ReplWindowUITests {
    [TestClass]
    public class SendToReplTests {
        static SendToReplTests() {
            AssertListener.Initialize();
        }

        /// <summary>
        /// Simple line-by-line tests which verify we submit when we get to
        /// the next statement.
        /// </summary>
        //[TestMethod, Priority(0)]
        //[TestCategory("Installed")]
        public virtual void SendToInteractiveLineByLine(PythonVisualStudioApp app) {
            RunOne(app, "Program.py",
                Input("if True:"),
                Input("    x = 1"),
                Input("    y = 2"),
                Skipped(5),

                Input("if True:").SubmitsPrevious,
                Input("    x = 1"),
                Input("    y = 2"),
                Input("else:"),
                Input("    x = 3"),
                Skipped(11),

                Input("if True:").SubmitsPrevious,
                Input("    x = 1"),
                Input("    y = 2"),
                Input("else:"),
                Input("    x = 3"),
                Input("print('hi')").Complete.Outputs("hi").SubmitsPrevious,
                EndOfInput
            );
        }

        /// <summary>
        /// Simple cell-by-cell tests which verify we submit when we get to
        /// the next statement.
        /// </summary>
        //[TestMethod, Priority(0)]
        //[TestCategory("Installed")]
        public virtual void SendToInteractiveCellByCell(PythonVisualStudioApp app) {
            RunOne(app, "Cells.py",
                Input(@"#%% cell 1
... x = 1
... 
>>> y = 2").Complete,

                MoveCaretRelative(1, 0),
                Input(@"# In[2]:
... x = 3
... 
>>> y = 4").Complete,

                MoveCaretRelative(2, 8),
                Input(@"# Preceding comment
... #%% cell 3
... if x > y:
...     print(x ** y)
... else:
...     print(y ** x)
... ").Complete.Outputs("64"),
                EndOfInput
            );
        }

        /// <summary>
        /// Line-by-line tests that verify we work with buffering code
        /// while it's executing.
        /// </summary>
        //[TestMethod, Priority(0)]
        //[TestCategory("Installed")]
        public virtual void SendToInteractiveDelayed(PythonVisualStudioApp app) {
            RunOne(app, "Delayed.py",
               Input("import time").Complete,
               Input("if True:"),
               Input("    time.sleep(5)"),
               Input("pass").NoOutputCheck.SubmitsPrevious.Complete,
               Input("if True:").NoOutputCheck,
               Input("    x = 1").NoOutputCheck,
               Input("    y = 2").NoOutputCheck,
               Input("print('hi')").Complete.Outputs("hi").SubmitsPrevious,
               EndOfInput
           );
        }

        /// <summary>
        /// Mixed line-by-line and selection, no buffering
        /// </summary>
        //[TestMethod, Priority(0)]
        //[TestCategory("Installed")]
        public virtual void SendToInteractiveSelection(PythonVisualStudioApp app) {
            RunOne(app, "Delayed.py",
               Input("import time").Complete,
               Selection(@"if True:
...     time.sleep(5)
... 
>>> ", 2, 1, 3, 18),
               MoveCaret(4, 1),
               Input("pass").NoOutputCheck.Complete,
               Input("if True:").NoOutputCheck,
               Input("    x = 1").NoOutputCheck,
               Input("    y = 2").NoOutputCheck,
               Input("print('hi')").Complete.Outputs("hi").SubmitsPrevious,
               EndOfInput
           );
        }

        /// <summary>
        /// Mixed line-by-line and selection, buffering while the selection
        /// is submitted.
        /// </summary>
        //[TestMethod, Priority(0)]
        //[TestCategory("Installed")]
        public virtual void SendToInteractiveSelectionNoWait(PythonVisualStudioApp app) {
            RunOne(app, "Delayed.py",
               Input("import time").Complete,
               Selection(@"if True:
...     time.sleep(5)
... 
>>> ", 2, 1, 3, 18).NoOutputCheck,
               MoveCaret(4, 1),
               Input("pass").NoOutputCheck.Complete,
               Input("if True:").NoOutputCheck,
               Input("    x = 1").NoOutputCheck,
               Input("    y = 2").NoOutputCheck,
               Input("print('hi')").Complete.Outputs("hi").SubmitsPrevious,
               EndOfInput
           );
        }


        private void RunOne(PythonVisualStudioApp app, string filename, params SendToStep[] inputs) {
            var project = app.OpenProject(@"TestData\SendToInteractive.sln");
            var program = project.ProjectItems.Item(filename);
            var window = program.Open();

            window.Activate();

            var doc = app.GetDocument(program.Document.FullName);
            doc.MoveCaret(new SnapshotPoint(doc.TextView.TextBuffer.CurrentSnapshot, 0));

            var interactive = ReplWindowProxy.Prepare(app, new ReplWindowPython35Tests().Settings, useIPython: false);

            interactive.ExecuteText("42").Wait();
            interactive.ClearScreen();

            WaitForText(interactive.TextView, ">>> ");

            var state = new StepState(interactive, app, doc, window);
            state.Content.Append(">>> ");

            foreach (var input in inputs) {
                input.Execute(state);
            }
        }

        public static void WaitForText(ITextView view, string text) {
            for (int i = 0; i < 100; i++) {
                if (view.TextBuffer.CurrentSnapshot.GetText().Replace("\r\n", "\n") != text.Replace("\r\n", "\n")) {
                    System.Threading.Thread.Sleep(100);
                } else {
                    break;
                }
            }

            Assert.AreEqual(
                text.Replace("\r\n", "\n").Replace("\n", "\r\n"),
                view.TextBuffer.CurrentSnapshot.GetText().Replace("\r\n", "\n").Replace("\n", "\r\n")
            );
        }

        /// <summary>
        /// An input that is complete and executes immediately.
        /// </summary>
        private static SelectionStep Selection(string text, int startLine, int startColumn, int endLine, int endColumn) {
            return new SelectionStep(text, startLine, startColumn, endLine, endColumn);
        }

        private static SendToStep MoveCaret(int line, int column) {
            return new MoveCaretStep(line, column);
        }

        private static SendToStep MoveCaretRelative(int lines, int columns) {
            return new MoveCaretRelativeStep(lines, columns);
        }

        /// <summary>
        /// An input that is complete and executes immediately.
        /// </summary>
        private static InputStep Input(string text) {
            return new InputStep(text, null, false);
        }

        private static SendToStep Output(string text) {
            return new OutputStep(text);
        }

        /// Checks that we've skipped blank lines after executing a previous input.
        /// </summary>
        private static SendToStep Skipped(int targetLine) {
            return new SkippedStep(targetLine);
        }

        private static SendToStep EndOfInput = new EndOfInputStep();

        class StepState {
            public readonly ReplWindowProxy Interactive;
            public readonly PythonVisualStudioApp App;
            public readonly EditorWindow Editor;
            public readonly StringBuilder Content;
            public readonly EnvDTE.Window Window;

            public StepState(ReplWindowProxy interactive, PythonVisualStudioApp app, EditorWindow editor, EnvDTE.Window window) {
                Interactive = interactive;
                App = app;
                Editor = editor;
                Window = window;
                Content = new StringBuilder();
            }

            public ITextView Document {
                get {
                    return Editor.TextView;
                }
            }

            public void CheckOutput() {
                WaitForText(Interactive.TextView, Content.ToString());
            }
        }


        abstract class SendToStep {
            public abstract void Execute(StepState state);
        }

        class MoveCaretStep : SendToStep {
            private readonly int _line, _column;

            public MoveCaretStep(int line, int column) {
                _line = line;
                _column = column;
            }

            public override void Execute(StepState state) {
                state.Window.Activate();

                state.Editor.Invoke(() => state.Document.Selection.Clear());

                state.Editor.MoveCaret(
                    _line,
                    _column
                );
            }
        }

        class MoveCaretRelativeStep : SendToStep {
            private readonly int _lines, _columns;

            public MoveCaretRelativeStep(int lines, int columns) {
                _lines = lines;
                _columns = columns;
            }

            public override void Execute(StepState state) {
                state.Window.Activate();

                int line = 0, column = 0;
                state.Editor.Invoke(() => {
                    state.Document.Selection.Clear();
                    var pos = state.Editor.TextView.Caret.Position.BufferPosition;
                    var posLine = pos.GetContainingLine();
                    line = posLine.LineNumber + 1;
                    column = pos.Position - posLine.Start.Position + 1;
                });

                state.Editor.MoveCaret(
                    line + _lines,
                    column + _columns
                );
            }
        }

        class SelectionStep : SendToStep {
            private readonly string _text;
            private readonly int _startColumn, _startLine, _endColumn, _endLine;
            private readonly bool _checkOutput;

            public SelectionStep(string text, int startLine, int startColumn, int endLine, int endColumn, bool checkOutput = true) {
                _text = text;
                _startColumn = startColumn;
                _startLine = startLine;
                _endColumn = endColumn;
                _endLine = endLine;
                _checkOutput = checkOutput;
            }

            public override void Execute(StepState state) {
                var curSnapshot = state.Document.TextBuffer.CurrentSnapshot;

                var start = curSnapshot.GetLineFromLineNumber(_startLine - 1).Start + _startColumn - 1;
                var end = curSnapshot.GetLineFromLineNumber(_endLine - 1).Start + _endColumn - 1;

                state.Content.Append(_text);

                state.Editor.Select(
                    _startLine,
                    _startColumn,
                    end - start
                );

                state.App.SendToInteractive();

                if (_checkOutput) {
                    state.CheckOutput();
                }
            }


            /// <summary>
            /// Indicates that this input should not wait for checking the
            /// input.
            /// </summary>
            public SelectionStep NoOutputCheck {
                get {
                    return new SelectionStep(
                        _text,
                        _startLine,
                        _startColumn,
                        _endLine,
                        _endColumn,
                        false
                    );
                }
            }
        }

        class InputStep : SendToStep {
            private readonly string _input, _output;
            private readonly bool _submits, _continues, _checkOutput;

            public InputStep(string input, string output, bool submits, bool continues = true, bool checkOutput = true) {
                _input = input;
                _output = output;
                _submits = submits;
                _continues = continues;
                _checkOutput = checkOutput;
            }

            public override void Execute(StepState state) {
                state.Window.Activate();

                if (_submits) {
                    // This input causes the previous input to be executed...
                    state.Content.Append("\r\n>>> ");
                }

                state.Content.Append(_input);
                state.Content.Append("\r\n");

                if (_output != null) {
                    state.Content.Append(_output);
                    state.Content.Append("\r\n");
                }

                if (_continues) {
                    state.Content.Append("... ");
                } else {
                    state.Content.Append(">>> ");
                }

                state.App.SendToInteractive();

                if (_checkOutput) {
                    state.CheckOutput();
                }
            }

            /// <summary>
            /// Indicates that this input will cause output to happen.
            /// </summary>
            /// <param name="output"></param>
            /// <returns></returns>
            public InputStep Outputs(string output) {
                return new InputStep(
                    _input,
                    output,
                    _submits,
                    _continues,
                    _checkOutput
                );
            }

            /// <summary>
            /// Indicates that when this input is submitted to the interactive window
            /// the previous input is finally executed.
            /// </summary>
            public InputStep SubmitsPrevious {
                get {
                    return new InputStep(
                        _input,
                        _output,
                        true,
                        _continues,
                        _checkOutput
                    );
                }
            }

            /// <summary>
            /// Indicates that this input is a complete statement and executes
            /// immediately
            /// </summary>
            public InputStep Complete {
                get {
                    return new InputStep(
                        _input,
                        _output,
                        _submits,
                        false,
                        _checkOutput
                    );
                }
            }

            /// <summary>
            /// Indicates that this input should not wait for checking the
            /// input.
            /// </summary>
            public InputStep NoOutputCheck {
                get {
                    return new InputStep(
                        _input,
                        _output,
                        _submits,
                        _continues,
                        false
                    );
                }
            }
        }

        class EndOfInputStep : SendToStep {
            public override void Execute(StepState state) {
                state.Window.Activate();
                state.App.SendToInteractive();

                state.CheckOutput();
            }
        }

        class OutputStep : SendToStep {
            public readonly string Output;

            public OutputStep(string output) {
                Output = output;
            }
            public override void Execute(StepState state) {
            }
        }

        class SkippedStep : SendToStep {
            private readonly int _targetLine;

            public SkippedStep(int targetLine) {
                _targetLine = targetLine;
            }

            public override void Execute(StepState state) {
                var curLine = state.Document.Caret.Position.BufferPosition.GetContainingLine();

                Assert.AreEqual(_targetLine, curLine.LineNumber + 1);

                state.CheckOutput();
            }
        }
    }
}

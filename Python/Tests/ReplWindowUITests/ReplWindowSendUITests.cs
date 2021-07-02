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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using TestUtilities;
using TestUtilities.UI;
using TestUtilities.UI.Python;

namespace ReplWindowUITests {
    public class ReplWindowSendUITests {
        #region Test Cases

        /// <summary>
        /// Simple line-by-line tests which verify we submit when we get to
        /// the next statement.
        /// </summary>
        public void SendToInteractiveLineByLine(PythonVisualStudioApp app) {
            RunFromProject(app, "Program.py",
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
        public void SendToInteractiveCellByCell(PythonVisualStudioApp app) {
            RunFromProject(app, "Cells.py",
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
        public void SendToInteractiveDelayed(PythonVisualStudioApp app) {
            RunFromProject(app, "Delayed.py",
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
        public void SendToInteractiveSelection(PythonVisualStudioApp app) {
            RunFromProject(app, "Delayed.py",
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
        public void SendToInteractiveSelectionNoWait(PythonVisualStudioApp app) {
            RunFromProject(app, "Delayed.py",
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

        /// <summary>
        /// Submit when read-only text is selected in REPL.
        /// </summary>
        public void SendToInteractiveOutputSelected(PythonVisualStudioApp app) {
            RunFromProject(app, "SelectOutput.py",
                Input("print('first')").Complete.Outputs("first"),
                SelectReplLine(1),
                Input("print('second')").Complete.Outputs("second"),
                SelectReplLine(2),
                Input("if True:"),
                Input("    x = 1"),
                SelectReplLine(2),
                Input("    y = 2"),
                Input("print('hi')").Complete.Outputs("hi").SubmitsPrevious,
                EndOfInput
            );
        }

        /// <summary>
        /// Verifies the workspace interpreter is used.
        /// </summary>
        public void SendToInteractiveWorkspaceInterpreter(PythonVisualStudioApp app) {
            RunFromWorkspace(app, "PrintInterpreter.py",
                Input("import sys").Complete,
                Input("print(sys.version_info[:2])").Complete.Outputs("(3, 7)"),
                EndOfInput
            );
        }

        /// <summary>
        /// Verifies the workspace package can be imported.
        /// </summary>
        public void SendToInteractiveWorkspacePackage(PythonVisualStudioApp app) {
            RunFromWorkspace(app, "ImportWorkspacePackage.py",
                Input("import package1").Complete,
                Input("print(package1.PACKAGE_MSG)").Complete.Outputs("Package Success"),
                EndOfInput
            );
        }

        /// <summary>
        /// Verifies the workspace search path package can be imported.
        /// </summary>
        public void SendToInteractiveWorkspaceSearchPathPackage(PythonVisualStudioApp app) {
            RunFromWorkspace(app, "ImportSearchPathPackage.py",
                Input("import folder1").Complete,
                Input("print(folder1.SEARCH_PATH_MSG)").Complete.Outputs("Search Path Success"),
                EndOfInput
            );
        }

        #endregion

        #region Helpers

        private void RunFromProject(PythonVisualStudioApp app, string filename, params SendToStep[] inputs) {
            PythonPaths.Python27_x64.AssertInstalled();
            PythonPaths.Python37_x64.AssertInstalled();

            // Set global default to 2.7 (different than workspace setting) to avoid false positive
            using (var defaultInterpreterSetter = app.SelectDefaultInterpreter(PythonPaths.Python27_x64)) {
                // SendToInteractive.pyproj uses Python 3.7 64-bit
                var settings = ReplWindowSettings.FindSettingsForInterpreter("Python37_x64");
                var sln = app.CopyProjectForTest(@"TestData\SendToInteractive.sln");
                var project = app.OpenProject(sln);
                var program = project.ProjectItems.Item(filename);
                var window = program.Open();
                window.Activate();
                var doc = app.GetDocument(program.Document.FullName);

                Run(app, inputs, settings, doc, window);
            }
        }

        private void RunFromWorkspace(PythonVisualStudioApp app, string filename, params SendToStep[] inputs) {
            PythonPaths.Python27_x64.AssertInstalled();
            PythonPaths.Python37_x64.AssertInstalled();

            // Set global default to 2.7 (different than workspace setting) to avoid false positive
            using (var defaultInterpreterSetter = app.SelectDefaultInterpreter(PythonPaths.Python27_x64)) {
                // "SendToInteractiveWorkspace" workspace uses Python 3.7 64-bit
                var settings = ReplWindowSettings.FindSettingsForInterpreter("Python37_x64");

                // Create a directory structure where we have a search path
                // folder located outside of the workspace folder.
                var parentFolder = TestData.GetTempPath();
                string workspaceFolder = Path.Combine(parentFolder, "SendToInteractiveWorkspace");
                string searchPathFolder = Path.Combine(parentFolder, "SendToInteractiveWorkspaceSearchPath");
                FileUtils.CopyDirectory(TestData.GetPath("TestData", "SendToInteractiveWorkspace"), workspaceFolder);
                FileUtils.CopyDirectory(TestData.GetPath("TestData", "SendToInteractiveWorkspaceSearchPath"), searchPathFolder);

                var provider = app.ComponentModel.GetService<IPythonWorkspaceContextProvider>();
                using (var initEvent = new AutoResetEvent(false)) {
                    provider.WorkspaceInitialized += (sender, e) => { initEvent.Set(); };

                    var documentFullPath = Path.Combine(workspaceFolder, filename);
                    app.OpenFolder(workspaceFolder);
                    var doc = app.OpenDocument(documentFullPath);
                    var window = app.OpenDocumentWindows.SingleOrDefault(w => PathUtils.IsSamePath(w.Document.FullName, documentFullPath));
                    Assert.IsNotNull(window, $"Could not get DTE window for {documentFullPath }");
                    Assert.IsTrue(initEvent.WaitOne(5000), $"Expected {nameof(provider.WorkspaceOpening)}.");

                    Run(app, inputs, settings, doc, window, workspaceName: "SendToInteractiveWorkspace");
                }
            }
        }

        private static void Run(PythonVisualStudioApp app, SendToStep[] inputs, ReplWindowProxySettings settings, EditorWindow doc, EnvDTE.Window window, string projectName = null, string workspaceName = null) {
            window.Activate();
            doc.MoveCaret(new SnapshotPoint(doc.TextView.TextBuffer.CurrentSnapshot, 0));
            app.WaitForCommandAvailable("Python.SendSelectionToInteractive", TimeSpan.FromSeconds(15));

            var interactive = ReplWindowProxy.Prepare(app, settings, projectName, workspaceName, useIPython: false);

            interactive.ExecuteText("42").Wait();
            interactive.ClearScreen();

            WaitForText(interactive.TextView, ">>> ");

            var state = new StepState(interactive, app, doc, window);
            state.Content.Append(">>> ");

            foreach (var input in inputs) {
                input.Execute(state);
            }
        }

        private static void WaitForText(ITextView view, string text) {
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

        /// <summary>
        /// Select a whole line in the REPL.
        /// </summary>
        private static SelectReplLineStep SelectReplLine(int line) {
            return new SelectReplLineStep(line);
        }

        /// <summary>
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

        class SelectReplLineStep : SendToStep {
            private readonly int _targetLine;

            public SelectReplLineStep(int targetLine) {
                _targetLine = targetLine;
            }

            public override void Execute(StepState state) {
                state.Editor.Invoke(() => {
                    var view = state.Interactive.TextView;
                    var line = view.TextSnapshot.GetLineFromLineNumber(_targetLine - 1);
                    var span = new SnapshotSpan(line.Start, line.End);
                    view.Selection.Select(span, false);
                });
            }
        }

        #endregion
    }
}

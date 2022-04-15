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
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.PythonTools.Editor.Core;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.InteractiveWindow.Shell;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudioTools;
using IServiceProvider = System.IServiceProvider;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Commands {
    /// <summary>
    /// Provides the command to send selected text from a buffer to the remote REPL window.
    /// 
    /// The command supports either sending a selection or sending line-by-line.  In line-by-line
    /// mode the user should be able to just hold down on Alt-Enter and have an entire script
    /// execute as if they ran it in the interactive window.  Focus will continue to remain in
    /// the active text view.
    /// 
    /// In selection mode the users selection is executed and focus is transfered to the interactive
    /// window and their selection remains unchanged.
    /// </summary>
    class SendToReplCommand : Command {
        protected readonly IServiceProvider _serviceProvider;
        private static string[] _newLineChars = new[] { "\r\n", "\n", "\r" };
        private static object _executedLastLine = new object();

        public SendToReplCommand(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
        }

        public override async void DoCommand(object sender, EventArgs args) {
            var activeView = CommonPackage.GetActiveTextView(_serviceProvider);
            var project = activeView.GetProjectAtCaret(_serviceProvider);
            var configuration = activeView.GetInterpreterConfigurationAtCaret(_serviceProvider);
            ITextSelection selection = activeView.Selection;
            ITextSnapshot snapshot = activeView.TextBuffer.CurrentSnapshot;
            var workspace = _serviceProvider.GetWorkspace();

            IVsInteractiveWindow repl;
            try {
                repl = ExecuteInReplCommand.EnsureReplWindow(_serviceProvider, configuration, project, workspace);
            } catch (MissingInterpreterException ex) {
                MessageBox.Show(ex.Message, Strings.ProductTitle);
                return;
            }

            string input;
            bool focusRepl = false, alwaysSubmit = false;

            if (selection.StreamSelectionSpan.Length > 0) {
                // Easy, just send the selection to the interactive window.
                input = activeView.Selection.StreamSelectionSpan.GetText();
                if (!input.EndsWithOrdinal("\n") && !input.EndsWithOrdinal("\r")) {
                    input += activeView.Options.GetNewLineCharacter();
                }
                focusRepl = true;
            } else if (!activeView.Properties.ContainsProperty(_executedLastLine)) {
                // No selection, and we haven't hit the end of the file in line-by-line mode.
                // Send the current line, and then move the caret to the next non-blank line.
                ITextSnapshotLine targetLine = snapshot.GetLineFromPosition(selection.Start.Position);
                var targetSpan = targetLine.Extent;

                // If the line is inside a code cell, expand the target span to
                // contain the entire cell.
                var cellStart = CodeCellAnalysis.FindStartOfCell(targetLine);
                if (cellStart != null) {
                    var cellEnd = CodeCellAnalysis.FindEndOfCell(cellStart, targetLine);
                    targetSpan = new SnapshotSpan(cellStart.Start, cellEnd.End);
                    targetLine = CodeCellAnalysis.FindEndOfCell(cellEnd, targetLine, includeWhitespace: true);
                    alwaysSubmit = true;
                }
                input = targetSpan.GetText();

                bool moved = false;
                while (targetLine.LineNumber < snapshot.LineCount - 1) {
                    targetLine = snapshot.GetLineFromLineNumber(targetLine.LineNumber + 1);
                    // skip over blank lines, unless it's the last line, in which case we want to land on it no matter what
                    if (!string.IsNullOrWhiteSpace(targetLine.GetText()) ||
                        targetLine.LineNumber == snapshot.LineCount - 1) {
                        activeView.Caret.MoveTo(new SnapshotPoint(snapshot, targetLine.Start));
                        activeView.Caret.EnsureVisible();
                        moved = true;
                        break;
                    }
                }

                if (!moved) {
                    // There's no where for the caret to go, don't execute the line if 
                    // we've already executed it.
                    activeView.Caret.PositionChanged += Caret_PositionChanged;
                    activeView.Properties[_executedLastLine] = _executedLastLine;
                }
            } else if ((repl.InteractiveWindow.CurrentLanguageBuffer?.CurrentSnapshot.Length ?? 0) != 0) {
                // We reached the end of the file but have some text buffered.  Execute it now.
                input = activeView.Options.GetNewLineCharacter();
            } else {
                // We've hit the end of the current text view and executed everything
                input = null;
            }

            if (input != null) {
                repl.Show(focusRepl);

                var inputs = repl.InteractiveWindow.Properties.GetOrCreateSingletonProperty(
                    () => new InteractiveInputs(repl.InteractiveWindow, _serviceProvider, alwaysSubmit)
                );

                await inputs.EnqueueAsync(input);
            }

            // Take focus back if REPL window has stolen it and we're in line-by-line mode.
            if (!focusRepl && !activeView.HasAggregateFocus) {
                var adapterService = _serviceProvider.GetComponentModel().GetService<VisualStudio.Editor.IVsEditorAdaptersFactoryService>();
                var tv = adapterService.GetViewAdapter(activeView);
                tv.SendExplicitFocus();
            }
        }

        private static void Caret_PositionChanged(object sender, CaretPositionChangedEventArgs e) {
            e.TextView.Properties.RemoveProperty(_executedLastLine);
            e.TextView.Caret.PositionChanged -= Caret_PositionChanged;
        }

        public override int? EditFilterQueryStatus(ref VisualStudio.OLE.Interop.OLECMD cmd, IntPtr pCmdText) {
            var activeView = CommonPackage.GetActiveTextView(_serviceProvider);

            InterpreterConfiguration config;
            if ((config = activeView?.GetInterpreterConfigurationAtCaret(_serviceProvider)) != null) {
                if (activeView.Selection.Mode == TextSelectionMode.Box ||
                    config?.IsRunnable() != true) {
                    cmd.cmdf = (uint)(OLECMDF.OLECMDF_SUPPORTED);
                } else {
                    cmd.cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                }
            } else {
                cmd.cmdf = (uint)(OLECMDF.OLECMDF_INVISIBLE);
            }

            return VSConstants.S_OK;
        }

        public override EventHandler BeforeQueryStatus {
            get {
                return (sender, args) => {
                    ((OleMenuCommand)sender).Visible = false;
                    ((OleMenuCommand)sender).Supported = false;
                };
            }
        }

        public override int CommandId {
            get { return (int)PkgCmdIDList.cmdidSendToRepl; }
        }


        class InteractiveInputs {
            private readonly LinkedList<string> _pendingInputs = new LinkedList<string>();
            private readonly IServiceProvider _serviceProvider;
            private readonly IInteractiveWindow _window;
            private readonly bool _submitAtEnd;

            public InteractiveInputs(IInteractiveWindow window, IServiceProvider serviceProvider, bool submitAtEnd) {
                _window = window;
                _serviceProvider = serviceProvider;
                _window.ReadyForInput += () => ProcessQueuedInputAsync().DoNotWait();
                _submitAtEnd = submitAtEnd;
            }

            public async Task EnqueueAsync(string input) {
                _pendingInputs.AddLast(input);
                if (!_window.IsRunning) {
                    await ProcessQueuedInputAsync();
                }
            }

            /// <summary>
            /// Pends the next input line to the current input buffer, optionally executing it
            /// if it forms a complete statement.
            /// </summary>
            private async Task ProcessQueuedInputAsync() {
                if (_pendingInputs.First == null) {
                    return;
                }

                var textView = _window.TextView;
                var eval = _window.GetPythonEvaluator();

                bool supportsMultipleStatements = false;
                if (eval != null) {
                    supportsMultipleStatements = await eval.GetSupportsMultipleStatementsAsync();
                }

                // Process all of our pending inputs until we get a complete statement
                while (_pendingInputs.First != null) {
                    string current = _pendingInputs.First.Value;
                    _pendingInputs.RemoveFirst();

                    MoveCaretToEndOfCurrentInput();

                    var statements = RecombineInput(
                        current,
                        _window.CurrentLanguageBuffer?.CurrentSnapshot.GetText(),
                        supportsMultipleStatements,
                        await textView.GetLanguageVersionAsync(_serviceProvider),
                        textView.Options.GetNewLineCharacter()
                    );

                    if (statements.Count > 0) {
                        // If there was more than one statement then save those for execution later...
                        var input = statements[0];
                        for (int i = statements.Count - 1; i > 0; i--) {
                            _pendingInputs.AddFirst(statements[i]);
                        }

                        _window.InsertCode(input);

                        string fullCode = _window.CurrentLanguageBuffer.CurrentSnapshot.GetText();
                        if (_window.Evaluator.CanExecuteCode(fullCode)) {
                            // the code is complete, execute it now
                            _window.Operations.ExecuteInput();
                            return;
                        }

                        _window.InsertCode(textView.Options.GetNewLineCharacter());

                        if (_submitAtEnd && _pendingInputs.First == null) {
                            _window.Operations.ExecuteInput();
                        }
                    }
                }
            }

            /// <summary>
            /// Takes the new input and appends it to any existing input that's been entered so far.  The combined
            /// input is then split into multiple top-level statements that will be executed one-by-one.
            /// 
            /// Also handles any dedents necessary to make the input a valid input, which usually would only
            /// apply if we have no input so far.
            /// </summary>
            private static List<string> RecombineInput(
                string input,
                string pendingInput,
                bool supportsMultipleStatements,
                PythonLanguageVersion version,
                string newLineCharacter
            ) {
                // Combine the current input text with the newly submitted text.  This will prevent us
                // from dedenting code when doing line-by-line submissions of things like:
                // if True:
                //      x = 1
                // 
                // So that we don't dedent "x = 1" when we submit it by its self.

                var combinedText = (pendingInput ?? string.Empty) + input;
                var oldLineCount = string.IsNullOrEmpty(pendingInput) ?
                    0 :
                    pendingInput.Split(_newLineChars, StringSplitOptions.None).Length - 1;

                // The split and join will not alter the number of lines that are fed in and returned but
                // may change the text by dedenting it if we hadn't submitted the "if True:" in the
                // code above.
                var split = ReplEditFilter.SplitAndDedent(combinedText);
                IEnumerable<string> joinedLines;
                if (!supportsMultipleStatements) {
                    joinedLines = ReplEditFilter.JoinToCompleteStatements(split, version, false);
                } else {
                    joinedLines = new[] { string.Join(newLineCharacter, split) };
                }

                // Remove any of the lines that were previously inputted into the buffer and also
                // remove any extra newlines in the submission.
                List<string> res = new List<string>();
                foreach (var inputLine in joinedLines) {
                    var actualLines = inputLine.Split(_newLineChars, StringSplitOptions.None);
                    var newLine = ReplEditFilter.FixEndingNewLine(
                        string.Join(newLineCharacter, actualLines.Skip(oldLineCount))
                    );

                    res.Add(newLine);

                    oldLineCount -= actualLines.Count();
                    if (oldLineCount < 0) {
                        oldLineCount = 0;
                    }
                }

                return res;
            }

            private void MoveCaretToEndOfCurrentInput() {
                var textView = _window.TextView;
                var curLangBuffer = _window.CurrentLanguageBuffer;
                SnapshotPoint? curLangPoint = null;

                // If anything is selected we need to clear it before inserting new code
                textView.Selection.Clear();

                // Find out if caret position is where code can be inserted.
                // Caret must be in the area mappable to the language buffer.
                if (!textView.Caret.InVirtualSpace) {
                    curLangPoint = textView.MapDownToBuffer(textView.Caret.Position.BufferPosition, curLangBuffer);
                }

                if (curLangPoint == null) {
                    // Sending to the interactive window is like appending the input to the end, we don't
                    // respect the current caret position or selection.  We use InsertCode which uses the
                    // current caret position, so first we need to ensure the caret is in the input buffer, 
                    // otherwise inserting code does nothing.
                    SnapshotPoint? viewPoint = textView.BufferGraph.MapUpToBuffer(
                        new SnapshotPoint(curLangBuffer.CurrentSnapshot, curLangBuffer.CurrentSnapshot.Length),
                        PointTrackingMode.Positive,
                        PositionAffinity.Successor,
                        textView.TextBuffer
                    );

                    if (!viewPoint.HasValue) {
                        // Unable to map language buffer to view.
                        // Try moving caret to the end of the view then.
                        viewPoint = new SnapshotPoint(
                            textView.TextBuffer.CurrentSnapshot,
                            textView.TextBuffer.CurrentSnapshot.Length
                        );
                    }

                    textView.Caret.MoveTo(viewPoint.Value);
                }
            }
        }
    }
}

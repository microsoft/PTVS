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
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudioTools;
using IServiceProvider = System.IServiceProvider;
using Clipboard = System.Windows.Forms.Clipboard;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.PythonTools.Project;

namespace Microsoft.PythonTools.Repl {
    class ReplEditFilter : IOleCommandTarget {
        private readonly IVsTextView _vsTextView;
        private readonly ITextView _textView;
        private readonly IInteractiveWindow _interactive;
        private readonly IEditorOperations _editorOps;
        private readonly IServiceProvider _serviceProvider;
        private readonly IComponentModel _componentModel;
        private readonly PythonToolsService _pyService;
        private readonly IOleCommandTarget _next;

        private readonly SelectableReplEvaluator _selectEval;
        private string[] _currentEvaluatorNames;
        private Dictionary<string, string> _currentEvaluators;

        private bool _scopeListVisible;
        private string[] _currentScopes;


        private ReplEditFilter(
            IVsTextView vsTextView,
            ITextView textView,
            IEditorOperations editorOps,
            IServiceProvider serviceProvider,
            IOleCommandTarget next
        ) {
            _vsTextView = vsTextView;
            _textView = textView;
            _editorOps = editorOps;
            _serviceProvider = serviceProvider;
            _componentModel = _serviceProvider.GetComponentModel();
            _pyService = _serviceProvider.GetPythonToolsService();
            _interactive = _textView.TextBuffer.GetInteractiveWindow();
            _next = next;

            if (_interactive != null) {
                _selectEval = _interactive.Evaluator as SelectableReplEvaluator;
            }

            if (_selectEval != null) {
                _selectEval.EvaluatorChanged += EvaluatorChanged;
                _selectEval.AvailableEvaluatorsChanged += AvailableEvaluatorsChanged;
            }

            var mse = _interactive?.Evaluator as IMultipleScopeEvaluator;
            if (mse != null) {
                _scopeListVisible = mse.EnableMultipleScopes;
                mse.AvailableScopesChanged += AvailableScopesChanged;
                mse.MultipleScopeSupportChanged += MultipleScopeSupportChanged;
            }

            if (_next == null && _interactive != null) {
                ErrorHandler.ThrowOnFailure(vsTextView.AddCommandFilter(this, out _next));
            }
        }

        public static ReplEditFilter GetOrCreate(
            IServiceProvider serviceProvider,
            IComponentModel componentModel,
            ITextView textView,
            IOleCommandTarget next = null
        ) {
            var editorFactory = componentModel.GetService<IVsEditorAdaptersFactoryService>();
            var opsFactory = componentModel.GetService<IEditorOperationsFactoryService>();
            var vsTextView = editorFactory.GetViewAdapter(textView);

            if (textView.TextBuffer.GetInteractiveWindow() == null) {
                return null;
            }

            return textView.Properties.GetOrCreateSingletonProperty(() => new ReplEditFilter(
                vsTextView,
                textView,
                opsFactory.GetEditorOperations(textView),
                serviceProvider,
                next
            ));
        }

        public static ReplEditFilter GetOrCreate(
            IServiceProvider serviceProvider,
            IComponentModel componentModel,
            IVsTextView vsTextView,
            IOleCommandTarget next = null
        ) {
            var editorFactory = componentModel.GetService<IVsEditorAdaptersFactoryService>();
            var opsFactory = componentModel.GetService<IEditorOperationsFactoryService>();
            var textView = editorFactory.GetWpfTextView(vsTextView);

            if (textView.TextBuffer.GetInteractiveWindow() == null) {
                return null;
            }

            return textView.Properties.GetOrCreateSingletonProperty(() => new ReplEditFilter(
                vsTextView,
                textView,
                opsFactory.GetEditorOperations(textView),
                serviceProvider,
                next
            ));
        }

        private void EvaluatorChanged(object sender, EventArgs e) {
            Debug.Assert(ReferenceEquals(sender, _selectEval), "Event raised from wrong evaluator");
        }

        private void AvailableEvaluatorsChanged(object sender, EventArgs e) {
            Debug.Assert(ReferenceEquals(sender, _selectEval), "Event raised from wrong evaluator");

            _currentEvaluators = new Dictionary<string, string>();
            foreach (var eval in _selectEval.AvailableEvaluators) {
                if (!_currentEvaluators.ContainsKey(eval.Key)) {
                    _currentEvaluators[eval.Key] = eval.Value;
                }
            }
            _currentEvaluatorNames = _currentEvaluators.Keys.OrderBy(s => s).ToArray();
        }

        int IOleCommandTarget.Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            var eval = _interactive.Evaluator;
            
            // preprocessing
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97) {
                switch ((VSConstants.VSStd97CmdID)nCmdID) {
                    case VSConstants.VSStd97CmdID.Cut:
                        if (_editorOps.CutSelection()) {
                            return VSConstants.S_OK;
                        }
                        break;
                    case VSConstants.VSStd97CmdID.Copy:
                        if (_editorOps.CopySelection()) {
                            return VSConstants.S_OK;
                        }
                        break;
                    case VSConstants.VSStd97CmdID.Paste:
                        //string pasting = eval.FormatClipboard() ?? Clipboard.GetText();
                        //if (pasting != null) {
                        //    //PasteReplCode(eval, pasting);
                        //
                        //    return VSConstants.S_OK;
                        //}
                        break;
                }
            } else if (pguidCmdGroup == CommonConstants.Std2KCmdGroupGuid) {
                //switch ((VSConstants.VSStd2KCmdID)nCmdID) {
                //}
            } else if (pguidCmdGroup == GuidList.guidPythonToolsCmdSet) {
                switch (nCmdID) {
                    case PkgCmdIDList.comboIdReplScopes:
                        ScopeComboBoxHandler(pvaIn, pvaOut);
                        return VSConstants.S_OK;
                    case PkgCmdIDList.comboIdReplEvaluators:
                        EvaluatorComboBoxHandler(pvaIn, pvaOut);
                        return VSConstants.S_OK;

                    case PkgCmdIDList.comboIdReplScopesGetList:
                        ScopeComboBoxGetList(pvaOut);
                        return VSConstants.S_OK;
                    case PkgCmdIDList.comboIdReplEvaluatorsGetList:
                        EvaluatorComboBoxGetList(pvaOut);
                        return VSConstants.S_OK;

                    case PkgCmdIDList.cmdidNewInteractiveWindow:
                        return CloneInteractiveWindow();
                }

            }

            return _next.Exec(pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        private const OLECMDF CommandEnabled = OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED;
        private const OLECMDF CommandDisabledAndHidden = OLECMDF.OLECMDF_INVISIBLE | OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_DEFHIDEONCTXTMENU;

        private OLECMDF QueryStatus(Guid pguidCmdGroup, uint cmdID) {
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97) {
                //switch ((VSConstants.VSStd97CmdID)cmdID) {
                //}
            } else if (pguidCmdGroup == GuidList.guidPythonToolsCmdSet) {
                switch (cmdID) {
                    case PkgCmdIDList.comboIdReplScopes:
                        return _scopeListVisible ? CommandEnabled : CommandDisabledAndHidden;

                    case PkgCmdIDList.comboIdReplEvaluators:
                        return _selectEval != null ? CommandEnabled : CommandDisabledAndHidden;

                    case PkgCmdIDList.cmdidNewInteractiveWindow:
                        return _selectEval != null ? CommandEnabled : CommandDisabledAndHidden;
                }
            } else if (pguidCmdGroup == CommonConstants.Std2KCmdGroupGuid) {
                //switch ((VSConstants.VSStd2KCmdID)cmdID) {
                //}
            }

            return 0;
        }

        /// <summary>
        /// Called from VS to see what commands we support.  
        /// </summary>
        int IOleCommandTarget.QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {
            bool any = false;
            for (int i = 0; i < cCmds; ++i) {
                OLECMDF f = QueryStatus(pguidCmdGroup, prgCmds[i].cmdID);
                if ((f & OLECMDF.OLECMDF_SUPPORTED) != 0) {
                    prgCmds[i].cmdf = (uint)f;
                    any = true;
                }
            }

            if (any) {
                return VSConstants.S_OK;
            }

            return _next.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        private async void AvailableScopesChanged(object sender, EventArgs e) {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var mse = _interactive.Evaluator as IMultipleScopeEvaluator;
            _currentScopes = (mse?.GetAvailableScopes() ?? Enumerable.Empty<string>()).ToArray();
        }

        private void MultipleScopeSupportChanged(object sender, EventArgs e) {
            var mse = _interactive.Evaluator as IMultipleScopeEvaluator;
            _scopeListVisible = (_interactive.Evaluator as IMultipleScopeEvaluator)?.EnableMultipleScopes ?? false;
        }

        /// <summary>
        /// Handles getting or setting the current value of the combo box.
        /// </summary>
        private void ScopeComboBoxHandler(IntPtr newValue, IntPtr outCurrentValue) {
            // getting the current value
            if (outCurrentValue != IntPtr.Zero) {
                var mse = _interactive.Evaluator as IMultipleScopeEvaluator;
                Marshal.GetNativeVariantForObject(mse?.CurrentScopeName, outCurrentValue);
            }

            // setting the current value
            if (newValue != IntPtr.Zero) {
                SetCurrentScope((string)Marshal.GetObjectForNativeVariant(newValue));
            }
        }

        /// <summary>
        /// Handles getting or setting the current value of the combo box.
        /// </summary>
        private void EvaluatorComboBoxHandler(IntPtr newValue, IntPtr outCurrentValue) {
            if (_selectEval == null) {
                return;
            }
            
            // getting the current value
            if (outCurrentValue != IntPtr.Zero) {
                Marshal.GetNativeVariantForObject(
                    (_selectEval.Evaluator as PythonInteractiveEvaluator)?.DisplayName ?? "<unknown>",
                    outCurrentValue
                );
            }

            // setting the current value
            if (newValue != IntPtr.Zero) {
                var text = (string)Marshal.GetObjectForNativeVariant(newValue);
                if (string.IsNullOrEmpty(text)) {
                    return;
                }

                string id;
                if (_currentEvaluators.TryGetValue(text, out id) && QuerySetEvaluator(text, id)) {
                    _selectEval.SetEvaluator(id);
                }
            }
        }

        /// <summary>
        /// Gets the list of scopes that should be available in the combo box.
        /// </summary>
        private void ScopeComboBoxGetList(IntPtr outList) {
            if (_currentScopes != null) {
                Debug.Assert(outList != IntPtr.Zero);

                Marshal.GetNativeVariantForObject(_currentScopes, outList);
            }
        }

        /// <summary>
        /// Gets the list of evaluators that should be available in the combo box.
        /// </summary>
        private void EvaluatorComboBoxGetList(IntPtr outList) {
            if (_selectEval == null) {
                return;
            }
            if (_currentEvaluatorNames == null) {
                // First call, so grab evaluators now
                AvailableEvaluatorsChanged(_selectEval, EventArgs.Empty);
            }
            if (_currentEvaluatorNames != null) {
                Marshal.GetNativeVariantForObject(_currentEvaluatorNames, outList);
            }
        }

        internal void SetCurrentScope(string newItem) {
            string activeCode = _interactive.CurrentLanguageBuffer.CurrentSnapshot.GetText();
            (_interactive.Evaluator as IMultipleScopeEvaluator)?.SetScope(newItem);
            _interactive.InsertCode(activeCode);
        }

        private bool QuerySetEvaluator(string newEvaluator, string newEvaluatorId) {
            // TODO: Check saved preference

            var td = new TaskDialog(_serviceProvider) {
                Title = SR.ProductName,
                MainInstruction = "Really change to " + newEvaluator,
                VerificationText = "Remember my selection",
                AllowCancellation = true
            };
            var sameWin = new TaskDialogButton(
                "Change this tab",
                "Your previous work may be lost when we change environment."
            );
            var newWin = new TaskDialogButton(
                "Open in new tab",
                "Your existing state will still be available when you switch windows."
            );
            td.Buttons.Add(sameWin);
            td.Buttons.Add(newWin);
            var result = td.ShowModal();
            if (result == sameWin) {
                if (td.SelectedVerified) {
                    // TODO: Save preference
                }
                return true;
            } else if (result == newWin) {
                if (td.SelectedVerified) {
                    // TODO: Save preference
                }

                var provider = _componentModel.GetService<InteractiveWindowProvider>();
                var wnd = provider.Create(newEvaluatorId);
                wnd.Show(true);
            }
            return false;
        }

        private int CloneInteractiveWindow() {
            var provider = _componentModel.GetService<InteractiveWindowProvider>();
            var wnd = provider.Create(_selectEval.CurrentEvaluator);
            wnd.Show(true);

            return VSConstants.S_OK;
        }

        private static void PasteReplCode(BasePythonReplEvaluator eval, string pasting) {
            // there's some text in the buffer...
            var view = eval.Window.TextView;
            var caret = view.Caret;

            if (view.Selection.IsActive && !view.Selection.IsEmpty) {
                foreach (var span in view.Selection.SelectedSpans) {
                    foreach (var normalizedSpan in view.BufferGraph.MapDownToBuffer(span, SpanTrackingMode.EdgeInclusive, eval.Window.CurrentLanguageBuffer)) {
                        normalizedSpan.Snapshot.TextBuffer.Delete(normalizedSpan);
                    }
                }
            }

            var curBuffer = eval.Window.CurrentLanguageBuffer;
            var inputPoint = view.BufferGraph.MapDownToBuffer(
                caret.Position.BufferPosition,
                PointTrackingMode.Positive,
                curBuffer,
                PositionAffinity.Successor
            );


            // if we didn't find a location then see if we're in a prompt, and if so, then we want
            // to insert after the prompt.
            if (caret.Position.BufferPosition != eval.Window.TextView.TextBuffer.CurrentSnapshot.Length) {
                for (int i = caret.Position.BufferPosition + 1;
                    inputPoint == null && i <= eval.Window.TextView.TextBuffer.CurrentSnapshot.Length;
                    i++) {
                    inputPoint = view.BufferGraph.MapDownToBuffer(
                        new SnapshotPoint(eval.Window.TextView.TextBuffer.CurrentSnapshot, i),
                        PointTrackingMode.Positive,
                        curBuffer,
                        PositionAffinity.Successor
                    );
                }
            }

            if (inputPoint == null) {
                // we didn't find a point to insert, insert at the beginning.
                inputPoint = new SnapshotPoint(curBuffer.CurrentSnapshot, 0);
            }

            // we want to insert the pasted code at the caret, but we also want to
            // respect the stepping.  So first grab the code before and after the caret.
            string startText = curBuffer.CurrentSnapshot.GetText(0, inputPoint.Value);

            string endText = curBuffer.CurrentSnapshot.GetText(
                inputPoint.Value,
                curBuffer.CurrentSnapshot.Length - inputPoint.Value);


            var splitCode = eval.JoinCode(eval.SplitCode(startText + pasting + endText)).ToList();
            curBuffer.Delete(new Span(0, curBuffer.CurrentSnapshot.Length));

            if (splitCode.Count == 1) {
                curBuffer.Insert(0, splitCode[0]);
                var viewPoint = view.BufferGraph.MapUpToBuffer(
                    new SnapshotPoint(curBuffer.CurrentSnapshot, Math.Min(inputPoint.Value.Position + pasting.Length, curBuffer.CurrentSnapshot.Length)),
                    PointTrackingMode.Positive,
                    PositionAffinity.Successor,
                    view.TextBuffer
                );

                if (viewPoint != null) {
                    view.Caret.MoveTo(viewPoint.Value);
                }
            } else if (splitCode.Count != 0) {
                var lastCode = splitCode[splitCode.Count - 1];
                splitCode.RemoveAt(splitCode.Count - 1);

                eval.Window.ReadyForInput += new PendLastSplitCode(eval.CurrentWindow, lastCode).AppendCode;
                eval.Window.Submit(splitCode);
            } else {
                eval.Window.CurrentLanguageBuffer.Insert(0, startText + pasting + endText);
            }
        }

        class PendLastSplitCode {
            public readonly IInteractiveWindow Window;
            public readonly string Text;

            public PendLastSplitCode(IInteractiveWindow window, string text) {
                Window = window;
                Text = text;
            }

            public void AppendCode() {
                if (((PythonReplEvaluator)Window.Evaluator)._lastExecutionResult.Result.IsSuccessful) {
                    Window.CurrentLanguageBuffer.Insert(0, Text);
                }
                Window.ReadyForInput -= AppendCode;
            }
        }

        internal void DoIdle(IOleComponentManager compMgr) {
        }
    }
}

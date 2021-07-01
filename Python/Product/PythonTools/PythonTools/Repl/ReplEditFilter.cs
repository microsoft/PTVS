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

using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudioTools;
using IServiceProvider = System.IServiceProvider;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Repl
{
    class ReplEditFilter : IOleCommandTarget
    {
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
        )
        {
            _vsTextView = vsTextView;
            _textView = textView;
            _editorOps = editorOps;
            _serviceProvider = serviceProvider;
            _componentModel = _serviceProvider.GetComponentModel();
            _pyService = _serviceProvider.GetPythonToolsService();
            _interactive = _textView.TextBuffer.GetInteractiveWindow();
            _next = next;

            if (_interactive != null)
            {
                _selectEval = _interactive.Evaluator as SelectableReplEvaluator;
            }

            if (_selectEval != null)
            {
                _selectEval.EvaluatorChanged += EvaluatorChanged;
                _selectEval.AvailableEvaluatorsChanged += AvailableEvaluatorsChanged;
            }

            var mse = _interactive?.Evaluator as IMultipleScopeEvaluator;
            if (mse != null)
            {
                _scopeListVisible = mse.EnableMultipleScopes;
                mse.AvailableScopesChanged += AvailableScopesChanged;
                mse.MultipleScopeSupportChanged += MultipleScopeSupportChanged;
            }

            if (_next == null && _interactive != null)
            {
                ErrorHandler.ThrowOnFailure(vsTextView.AddCommandFilter(this, out _next));
            }
        }

        public static ReplEditFilter GetOrCreate(
            IServiceProvider serviceProvider,
            IComponentModel componentModel,
            ITextView textView,
            IOleCommandTarget next = null
        )
        {
            var editorFactory = componentModel.GetService<IVsEditorAdaptersFactoryService>();
            var opsFactory = componentModel.GetService<IEditorOperationsFactoryService>();
            var vsTextView = editorFactory.GetViewAdapter(textView);

            if (textView.TextBuffer.GetInteractiveWindow() == null)
            {
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
        )
        {
            var editorFactory = componentModel.GetService<IVsEditorAdaptersFactoryService>();
            var opsFactory = componentModel.GetService<IEditorOperationsFactoryService>();
            var textView = editorFactory.GetWpfTextView(vsTextView);

            if (textView.TextBuffer.GetInteractiveWindow() == null)
            {
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

        private void EvaluatorChanged(object sender, EventArgs e)
        {
            Debug.Assert(ReferenceEquals(sender, _selectEval), "Event raised from wrong evaluator");
        }

        private void AvailableEvaluatorsChanged(object sender, EventArgs e)
        {
            Debug.Assert(ReferenceEquals(sender, _selectEval), "Event raised from wrong evaluator");

            _currentEvaluators = new Dictionary<string, string>();
            foreach (var eval in _selectEval.AvailableEvaluators)
            {
                if (!_currentEvaluators.ContainsKey(eval.Key))
                {
                    _currentEvaluators[eval.Key] = eval.Value;
                }
            }
            _currentEvaluatorNames = _currentEvaluators.Keys.OrderBy(s => s).ToArray();
        }

        int IOleCommandTarget.Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            try
            {
                return ExecWorker(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            }
            catch (Exception ex)
            {
                ex.ReportUnhandledException(_serviceProvider, GetType());
                return VSConstants.E_FAIL;
            }
        }

        private int ExecWorker(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            var eval = _interactive.Evaluator;

            // preprocessing
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97)
            {
                switch ((VSConstants.VSStd97CmdID)nCmdID)
                {
                    case VSConstants.VSStd97CmdID.Cut:
                        if (_editorOps.CutSelection())
                        {
                            return VSConstants.S_OK;
                        }
                        break;
                    case VSConstants.VSStd97CmdID.Copy:
                        if (_editorOps.CopySelection())
                        {
                            return VSConstants.S_OK;
                        }
                        break;
                    case VSConstants.VSStd97CmdID.Paste:
                        string pasting = eval.FormatClipboard();
                        if (pasting != null)
                        {
                            PasteReplCode(
                                _interactive,
                                pasting,
                                (eval as PythonInteractiveEvaluator)?.LanguageVersion ?? PythonLanguageVersion.None
                            ).DoNotWait();

                            return VSConstants.S_OK;
                        }
                        break;
                }
            }
            else if (pguidCmdGroup == CommonConstants.Std2KCmdGroupGuid)
            {
                switch ((VSConstants.VSStd2KCmdID)nCmdID)
                {
                    case VSConstants.VSStd2KCmdID.CANCEL:
                        var controller = IntellisenseControllerProvider.GetController(_textView);
                        if (controller != null && controller.DismissCompletionSession())
                        {
                            return VSConstants.S_OK;
                        }
                        break;
                }
            }
            else if (pguidCmdGroup == GuidList.guidPythonToolsCmdSet)
            {
                switch (nCmdID)
                {
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

                    case PkgCmdIDList.cmdidOpenInteractiveScopeInEditor:
                        var path = GetCurrentScopeSourcePath();
                        if (!string.IsNullOrEmpty(path))
                        {
                            PythonToolsPackage.NavigateTo(_serviceProvider, path, Guid.Empty, 0);
                            return VSConstants.S_OK;
                        }
                        break;
                }

            }

            return _next.Exec(pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        private const OLECMDF CommandEnabled = OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED;
        private const OLECMDF CommandDisabled = OLECMDF.OLECMDF_SUPPORTED;
        private const OLECMDF CommandDisabledAndHidden = OLECMDF.OLECMDF_INVISIBLE | OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_DEFHIDEONCTXTMENU;

        private OLECMDF QueryStatus(Guid pguidCmdGroup, uint cmdID)
        {
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97)
            {
                //switch ((VSConstants.VSStd97CmdID)cmdID) {
                //}
            }
            else if (pguidCmdGroup == GuidList.guidPythonToolsCmdSet)
            {
                switch (cmdID)
                {
                    case PkgCmdIDList.comboIdReplScopes:
                        return _scopeListVisible ? CommandEnabled : CommandDisabledAndHidden;

                    case PkgCmdIDList.comboIdReplEvaluators:
                        return _selectEval != null ? CommandEnabled : CommandDisabledAndHidden;

                    case PkgCmdIDList.cmdidNewInteractiveWindow:
                        return _selectEval != null ? CommandEnabled : CommandDisabledAndHidden;

                    case PkgCmdIDList.cmdidOpenInteractiveScopeInEditor:
                        if (_scopeListVisible)
                        {
                            if (string.IsNullOrEmpty(GetCurrentScopeSourcePath()))
                            {
                                return CommandDisabled;
                            }
                            else
                            {
                                return CommandEnabled;
                            }
                        }
                        return CommandDisabledAndHidden;
                }
            }
            else if (pguidCmdGroup == CommonConstants.Std2KCmdGroupGuid)
            {
                //switch ((VSConstants.VSStd2KCmdID)cmdID) {
                //}
            }

            return 0;
        }

        /// <summary>
        /// Called from VS to see what commands we support.  
        /// </summary>
        int IOleCommandTarget.QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            bool any = false;
            for (int i = 0; i < cCmds; ++i)
            {
                OLECMDF f = QueryStatus(pguidCmdGroup, prgCmds[i].cmdID);
                if ((f & OLECMDF.OLECMDF_SUPPORTED) != 0)
                {
                    prgCmds[i].cmdf = (uint)f;
                    any = true;
                }
            }

            if (any)
            {
                return VSConstants.S_OK;
            }

            return _next.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        private async void AvailableScopesChanged(object sender, EventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var mse = _interactive.Evaluator as IMultipleScopeEvaluator;
            _currentScopes = (mse?.GetAvailableScopes() ?? Enumerable.Empty<string>()).ToArray();
        }

        private void MultipleScopeSupportChanged(object sender, EventArgs e)
        {
            var mse = _interactive.Evaluator as IMultipleScopeEvaluator;
            _scopeListVisible = (_interactive.Evaluator as IMultipleScopeEvaluator)?.EnableMultipleScopes ?? false;
        }

        /// <summary>
        /// Handles getting or setting the current value of the combo box.
        /// </summary>
        private void ScopeComboBoxHandler(IntPtr newValue, IntPtr outCurrentValue)
        {
            // getting the current value
            if (outCurrentValue != IntPtr.Zero)
            {
                var mse = _interactive.Evaluator as IMultipleScopeEvaluator;
                Marshal.GetNativeVariantForObject(mse?.CurrentScopeName, outCurrentValue);
            }

            // setting the current value
            if (newValue != IntPtr.Zero)
            {
                SetCurrentScope((string)Marshal.GetObjectForNativeVariant(newValue));
            }
        }

        /// <summary>
        /// Handles getting or setting the current value of the combo box.
        /// </summary>
        private void EvaluatorComboBoxHandler(IntPtr newValue, IntPtr outCurrentValue)
        {
            if (_selectEval == null)
            {
                return;
            }

            // getting the current value
            if (outCurrentValue != IntPtr.Zero)
            {
                Marshal.GetNativeVariantForObject(
                    (_selectEval.Evaluator as IPythonInteractiveEvaluator)?.DisplayName ?? "<unknown>",
                    outCurrentValue
                );
            }

            // setting the current value
            if (newValue != IntPtr.Zero)
            {
                var text = (string)Marshal.GetObjectForNativeVariant(newValue);
                if (string.IsNullOrEmpty(text))
                {
                    return;
                }

                try
                {
                    var id = _currentEvaluators[text];
                    var q = _selectEval.IsDisconnected ? true : QuerySetEvaluator(text, id);
                    if (q == true)
                    {
                        // Switch this window
                        _selectEval.CurrentWindow.WriteLine(Strings.ReplSwitchEvaluator);
                        _selectEval.SetEvaluator(id);
                    }
                    else if (q == false)
                    {
                        // Open a new window
                        var provider = _componentModel.GetService<InteractiveWindowProvider>();
                        var wnd = provider.Create(id);
                        wnd.Show(true);
                    }
                }
                catch (KeyNotFoundException ex)
                {
                    // Should never be missing an item, but if we are, report it
                    // now for maximum context.
                    ex.ReportUnhandledException(_serviceProvider, GetType());
                }
            }
        }

        /// <summary>
        /// Gets the list of scopes that should be available in the combo box.
        /// </summary>
        private void ScopeComboBoxGetList(IntPtr outList)
        {
            if (_currentScopes != null)
            {
                Debug.Assert(outList != IntPtr.Zero);

                Marshal.GetNativeVariantForObject(_currentScopes, outList);
            }
        }

        /// <summary>
        /// Gets the list of evaluators that should be available in the combo box.
        /// </summary>
        private void EvaluatorComboBoxGetList(IntPtr outList)
        {
            if (_selectEval == null)
            {
                return;
            }
            if (_currentEvaluatorNames == null)
            {
                // First call, so grab evaluators now
                AvailableEvaluatorsChanged(_selectEval, EventArgs.Empty);
            }
            if (_currentEvaluatorNames != null)
            {
                Marshal.GetNativeVariantForObject(_currentEvaluatorNames, outList);
            }
        }

        internal void SetCurrentScope(string newItem)
        {
            string activeCode = _interactive.CurrentLanguageBuffer.CurrentSnapshot.GetText();
            (_interactive.Evaluator as IMultipleScopeEvaluator)?.SetScope(newItem);
            _interactive.InsertCode(activeCode);
        }

        private string GetCurrentScopeSourcePath()
        {
            var path = (_interactive.Evaluator as IMultipleScopeEvaluator)?.CurrentScopePath;
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            foreach (var ext in PythonConstants.SourceFileExtensionsArray)
            {
                var source = Path.ChangeExtension(path, ext);
                if (File.Exists(source))
                {
                    return source;
                }
            }

            return null;
        }

        private bool? QuerySetEvaluator(string newEvaluator, string newEvaluatorId)
        {
            var opts = _serviceProvider.GetPythonToolsService().SuppressDialogOptions;
            var opt = opts.SwitchEvaluator;
            if (opt == "AlwaysSwitch")
            {
                return true;
            }
            else if (opt == "AlwaysOpenNew")
            {
                return false;
            }

            var td = new TaskDialog(_serviceProvider)
            {
                Title = Strings.ProductTitle,
                MainInstruction = Strings.ReplQuerySwitchEvaluator.FormatUI(newEvaluator),
                Content = Strings.ReplQuerySwitchEvaluatorHint,
                VerificationText = Strings.RememberMySelection,
                AllowCancellation = true
            };
            var sameWin = new TaskDialogButton(Strings.ReplQuerySwitchThisTab, Strings.ReplQuerySwitchThisTabHint);
            var newWin = new TaskDialogButton(Strings.ReplQuerySwitchNewTab, Strings.ReplQuerySwitchNewTabHint);
            td.Buttons.Add(sameWin);
            td.Buttons.Add(newWin);
            td.Buttons.Add(TaskDialogButton.Cancel);
            var result = td.ShowModal();
            if (result == sameWin)
            {
                if (td.SelectedVerified)
                {
                    opts.SwitchEvaluator = "AlwaysSwitch";
                    opts.Save();
                }
                return true;
            }
            else if (result == newWin)
            {
                if (td.SelectedVerified)
                {
                    opts.SwitchEvaluator = "AlwaysOpenNew";
                    opts.Save();
                }
                return false;
            }
            return null;
        }

        private int CloneInteractiveWindow()
        {
            var provider = _componentModel.GetService<InteractiveWindowProvider>();
            var wnd = provider.Create(_selectEval.CurrentEvaluator);
            wnd.Show(true);

            return VSConstants.S_OK;
        }

        private static async Task PasteReplCode(
            IInteractiveWindow window,
            string pasting,
            PythonLanguageVersion version
        )
        {
            // there's some text in the buffer...
            var view = window.TextView;
            var caret = view.Caret;

            if (view.Selection.IsActive && !view.Selection.IsEmpty)
            {
                foreach (var span in view.Selection.SelectedSpans)
                {
                    foreach (var normalizedSpan in view.BufferGraph.MapDownToBuffer(span, SpanTrackingMode.EdgeInclusive, window.CurrentLanguageBuffer))
                    {
                        normalizedSpan.Snapshot.TextBuffer.Delete(normalizedSpan);
                    }
                }
            }

            var curBuffer = window.CurrentLanguageBuffer;
            if (curBuffer.CurrentSnapshot.Length > 0)
            {
                // There is existing content in the buffer, so let's just insert and
                // return. We do submit any statements.
                window.InsertCode(pasting);
                return;
            }

            var inputPoint = view.BufferGraph.MapDownToBuffer(
                caret.Position.BufferPosition,
                PointTrackingMode.Positive,
                curBuffer,
                PositionAffinity.Successor
            );


            // if we didn't find a location then see if we're in a prompt, and if so, then we want
            // to insert after the prompt.
            if (caret.Position.BufferPosition != window.TextView.TextBuffer.CurrentSnapshot.Length)
            {
                for (int i = caret.Position.BufferPosition + 1;
                    inputPoint == null && i <= window.TextView.TextBuffer.CurrentSnapshot.Length;
                    i++)
                {
                    inputPoint = view.BufferGraph.MapDownToBuffer(
                        new SnapshotPoint(window.TextView.TextBuffer.CurrentSnapshot, i),
                        PointTrackingMode.Positive,
                        curBuffer,
                        PositionAffinity.Successor
                    );
                }
            }

            bool submitLast = pasting.EndsWithOrdinal("\n");

            if (inputPoint == null)
            {
                // we didn't find a point to insert, insert at the beginning.
                inputPoint = new SnapshotPoint(curBuffer.CurrentSnapshot, 0);
            }

            // we want to insert the pasted code at the caret, but we also want to
            // respect the stepping.  So first grab the code before and after the caret.
            var splitCode = JoinToCompleteStatements(SplitAndDedent(pasting), version).ToList();
            curBuffer.Delete(new Span(0, curBuffer.CurrentSnapshot.Length));

            bool supportMultiple = await window.GetSupportsMultipleStatements();

            if (supportMultiple)
            {
                window.InsertCode(string.Join(Environment.NewLine, splitCode));
            }
            else if (splitCode.Count == 1)
            {
                curBuffer.Insert(0, splitCode[0]);
                var viewPoint = view.BufferGraph.MapUpToBuffer(
                    new SnapshotPoint(curBuffer.CurrentSnapshot, Math.Min(inputPoint.Value.Position + pasting.Length, curBuffer.CurrentSnapshot.Length)),
                    PointTrackingMode.Positive,
                    PositionAffinity.Successor,
                    view.TextBuffer
                );

                if (viewPoint != null)
                {
                    view.Caret.MoveTo(viewPoint.Value);
                }
            }
            else if (splitCode.Count != 0)
            {
                var lastCode = splitCode[splitCode.Count - 1];
                splitCode.RemoveAt(splitCode.Count - 1);

                while (splitCode.Any())
                {
                    var code = splitCode[0];
                    splitCode.RemoveAt(0);
                    await window.SubmitAsync(new[] { code });

                    supportMultiple = await window.GetSupportsMultipleStatements();
                    if (supportMultiple)
                    {
                        // Might have changed while we were executing
                        break;
                    }
                }

                if (supportMultiple)
                {
                    // Insert all remaning lines of code
                    lastCode = string.Join(Environment.NewLine, splitCode);
                }

                window.InsertCode(lastCode);
            }
            else
            {
                window.InsertCode(pasting);
            }

            if (submitLast)
            {
                if (window.Evaluator.CanExecuteCode(window.CurrentLanguageBuffer.CurrentSnapshot.GetText()))
                {
                    window.Operations.ExecuteInput();
                }
                else
                {
                    window.InsertCode("\n");
                }
            }
        }

        internal IEnumerable<string> TrimIndent(IEnumerable<string> lines)
        {
            string indent = null;
            foreach (var line in lines)
            {
                if (indent == null)
                {
                    indent = line.Substring(0, line.TakeWhile(char.IsWhiteSpace).Count());
                }

                if (line.StartsWithOrdinal(indent))
                {
                    yield return line.Substring(indent.Length);
                }
                else
                {
                    yield return line.TrimStart();
                }
            }
        }

        internal static IEnumerable<string> JoinToCompleteStatements(IEnumerable<string> lines, PythonLanguageVersion version, bool fixNewLine = true)
        {
            StringBuilder temp = new StringBuilder();
            string prevText = null;
            ParseResult? prevParseResult = null;

            using (var e = new PeekableEnumerator<string>(lines))
            {
                bool skipNextMoveNext = false;
                while (skipNextMoveNext || e.MoveNext())
                {
                    skipNextMoveNext = false;
                    var line = e.Current;

                    if (e.HasNext)
                    {
                        temp.AppendLine(line);
                    }
                    else
                    {
                        temp.Append(line);
                    }
                    string newCode = temp.ToString();

                    var parser = Parser.CreateParser(new StringReader(newCode), version);
                    ParseResult result;
                    parser.ParseInteractiveCode(out result);

                    // if this parse is invalid then we need more text to be valid.
                    // But if this text is invalid and the previous parse was incomplete
                    // then appending more text won't fix things - the code in invalid, the user
                    // needs to fix it, so let's not break it up which would prevent that from happening.
                    if (result == ParseResult.Empty)
                    {
                        if (!String.IsNullOrWhiteSpace(newCode))
                        {
                            // comment line, include w/ following code.
                            prevText = newCode;
                            prevParseResult = result;
                        }
                        else
                        {
                            temp.Clear();
                        }
                    }
                    else if (result == ParseResult.Complete)
                    {
                        yield return FixEndingNewLine(newCode, fixNewLine);
                        temp.Clear();

                        prevParseResult = null;
                        prevText = null;
                    }
                    else if (ShouldAppendCode(prevParseResult, result))
                    {
                        prevText = newCode;
                        prevParseResult = result;
                    }
                    else if (prevText != null)
                    {
                        // we have a complete input
                        yield return FixEndingNewLine(prevText, fixNewLine);
                        temp.Clear();

                        // reparse this line so our state remains consistent as if we just started out.
                        skipNextMoveNext = true;
                        prevParseResult = null;
                        prevText = null;
                    }
                    else
                    {
                        prevParseResult = result;
                    }
                }
            }

            if (temp.Length > 0)
            {
                yield return FixEndingNewLine(temp.ToString(), fixNewLine);
            }
        }

        internal static IEnumerable<string> SplitAndDedent(string code)
        {
            var lines = code.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            if (lines.Length == 0)
            {
                return lines;
            }

            var leadingIndent = lines[0].Substring(0, lines[0].TakeWhile(char.IsWhiteSpace).Count());
            if (!lines.All(line => line.StartsWithOrdinal(leadingIndent) || string.IsNullOrEmpty(line)))
            {
                return lines;
            }

            return lines.Select(line =>
            {
                if (string.IsNullOrEmpty(line))
                {
                    return line;
                }
                return line.Substring(leadingIndent.Length);
            });
        }

        public static string FixEndingNewLine(string prevText, bool fixNewLine = true)
        {
            if (!fixNewLine)
            {
                return prevText;
            }

            if ((prevText.IndexOf('\n') == prevText.LastIndexOf('\n')) &&
                (prevText.IndexOf('\r') == prevText.LastIndexOf('\r')))
            {
                prevText = prevText.TrimEnd();
            }
            else if (prevText.EndsWithOrdinal("\r\n\r\n"))
            {
                prevText = prevText.Substring(0, prevText.Length - 2);
            }
            else if (prevText.EndsWithOrdinal("\n\n") || prevText.EndsWithOrdinal("\r\r"))
            {
                prevText = prevText.Substring(0, prevText.Length - 1);
            }
            return prevText;
        }

        private static bool ShouldAppendCode(ParseResult? prevParseResult, ParseResult result)
        {
            if (result == ParseResult.Invalid)
            {
                if (prevParseResult == ParseResult.IncompleteStatement || prevParseResult == ParseResult.Invalid)
                {
                    return false;
                }
            }
            return true;
        }
    }
}

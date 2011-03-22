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
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.Repl {
    /// <summary>
    /// Provides implementation of a Repl Window built on top of the VS editor using projection buffers.
    /// 
    /// TODO: We should condense committed language buffers into a single language buffer and save the
    /// classifications from the previous language buffer if the perf of having individual buffers
    /// starts having problems w/ a large number of inputs.
    /// </summary>
    [Guid(ReplWindow.TypeGuid)]
    class ReplWindow : ToolWindowPane, IOleCommandTarget, IReplWindow, IVsFindTarget {
        public const string TypeGuid = "5adb6033-611f-4d39-a193-57a717115c0f";

        private AutoResetEvent _inputEvent = new AutoResetEvent(false);
        private bool _adornmentToMinimize = false;

        private bool _showOutput, _useSmartUpDown, _isRunning;

        private IIntellisenseSessionStack _sessionStack;

        private Stopwatch _sw;
        private string _unCommittedInput;
        private DispatcherTimer _executionTimer;
        private Cursor _oldCursor;
        private List<IReplCommand> _commands;
        private IWpfTextViewHost _textViewHost;
        private IEditorOperations _editorOperations;

        private readonly IComponentModel/*!*/ _componentModel;
        private readonly IReplEvaluator/*!*/ _evaluator;
        private readonly IContentType/*!*/ _contentType;
        private readonly List<PendingInput>/*!*/ _pendingInput;
        private readonly History/*!*/ _history;
        private readonly OutputBuffer _buffer;
        private readonly List<ITrackingSpan> _secondaryPrompts = new List<ITrackingSpan>(); // prompts we've discarded and can re-use 
        private Dictionary<int, ReplSpan> _prompts = new Dictionary<int, ReplSpan>(); // mapping of line # to prompt on that line


        // We use one or two regions to protect span [0, input start) from modifications:
        private readonly IReadOnlyRegion[]/*!*/ _readOnlyRegions;

        private string/*!*/ _commandPrefix = "%";
        private string/*!*/ _prompt = "» ";        // prompt for primary input
        private string/*!*/ _secondPrompt = "";    // prompt for 2nd and additional lines
        private string/*!*/ _inputPrompt = "";     // prompt for I/O input
        private string _inputValue;
        private bool _displayPromptInMargin, _formattedPrompts;
        private int? _inputStart;
        private int _currentInput = 1;
        private IVsFindTarget _findTarget;
        private IVsTextView _view;

        //
        // Command filter chain: 
        // window -> pre-language -> language services -> post-language -> editor
        //
        private IOleCommandTarget _preLanguageCommandFilter;
        private IOleCommandTarget _languageServiceCommandFilter;
        private IOleCommandTarget _postLanguageCommandFilter;
        private IOleCommandTarget _editorCommandFilter;

        //
        // A list of scopes if this REPL is multi-scoped
        // 
        private string[] _currentScopes;
        private bool _scopeListVisible;
        
        private readonly Guid _langSvcGuid;
        private readonly string _replId;
        private readonly IProjectionBufferFactoryService _projectionFactory;   // the factory service for creating ellison 
        private readonly ITextBufferFactoryService _bufferFactory;
        private readonly IClassifierAggregatorService _classifierAgg;
        private ITextBuffer _outputBuffer, _inputBuffer, _promptBuffer;
        private ReplAggregateClassifier _primaryClassifier;
        private IProjectionBuffer _projectionBuffer;
        private readonly IReplWindowCreationListener[] _creationListeners;
        private readonly List<ReplSpan> _spans = new List<ReplSpan>();       // List of spans we've added to the projection buffer
        private ITextBuffer _currentLanguageBuffer;
        private readonly List<OutputColors> _outputColors = new List<OutputColors>();

        private static Guid DefaultFileType = new Guid(0x8239bec4, 0xee87, 0x11d0, 0x8c, 0x98, 0x0, 0xc0, 0x4f, 0xc2, 0xab, 0x22);
        private static readonly char[] _whitespaceChars = new[] { '\r', '\n', ' ', '\t' };
        private const string _boxSelectionCutCopyTag = "MSDEVColumnSelect";

        public ReplWindow(IComponentModel/*!*/ model, IReplEvaluator/*!*/ evaluator, IContentType/*!*/ contentType, string/*!*/ title, Guid languageServiceGuid, string replId, IReplWindowCreationListener[] listeners) {
            Contract.Assert(evaluator != null);
            Contract.Assert(contentType != null);
            Contract.Assert(title != null);
            Contract.Assert(model != null);
            
            _replId = replId;
            _langSvcGuid = languageServiceGuid;
            _creationListeners = listeners;
            _buffer = new OutputBuffer(this);

            // Set the window title reading it from the resources.z
            Caption = title;

            // Set the image that will appear on the tab of the window frame
            // when docked with an other window
            // The resource ID correspond to the one defined in the resx file
            // while the Index is the offset in the bitmap strip. Each image in
            // the strip being 16x16.
            BitmapResourceID = 301;
            BitmapIndex = 1;

            _componentModel = model;
            _evaluator = evaluator;
            _contentType = contentType;
            
            Contract.Requires(_commandPrefix != null && _prompt != null);
            
            _readOnlyRegions = new IReadOnlyRegion[2];
            _pendingInput = new List<PendingInput>();
            
            _history = new History();

            _projectionFactory = model.GetService<IProjectionBufferFactoryService>();
            _bufferFactory = model.GetService<ITextBufferFactoryService>();
            _classifierAgg = model.GetService<IClassifierAggregatorService>();

            SetupOutput();
        }

        public string ReplId { get { return _replId; } }

        public Guid LanguageServiceGuid {
            get {
                return _langSvcGuid;
            }
        }

        private List<IReplCommand>/*!*/ CreateCommands() {
            var commands = new List<IReplCommand>();
            var commandTypes = new HashSet<Type>();
            foreach (var command in _componentModel.GetExtensions<IReplCommand>()) {
                // avoid duplicate commands
                if (commandTypes.Contains(command.GetType())) {
                    continue;
                } else {
                    commandTypes.Add(command.GetType());
                }

                commands.Add(command);
            }
            return commands;
        }

        #region To Be Moved Into Contracts?

        public IEditorOperations EditorOperations {
            get {
                return _editorOperations;
            }
        }

        public IComponentModel ComponentModel {
            get {
                return _componentModel;
            }
        }

        public ITextBuffer/*!*/ TextBuffer {
            get { return TextView.TextBuffer; }
        }

        public ITextSnapshot CurrentSnapshot {
            get { return TextBuffer.CurrentSnapshot; }
        }

        /// <summary>
        /// Gets the current snapshot of the output buffer
        /// </summary>
        public ITextSnapshot InputSnapshot {
            get { return _currentLanguageBuffer.CurrentSnapshot; }
        }

        class EditResolver : IProjectionEditResolver {
            private readonly ReplWindow _replWindow;
            
            public EditResolver(ReplWindow replWindow) {
                _replWindow = replWindow;
            }

            #region IProjectionEditResolver Members

            public void FillInInsertionSizes(SnapshotPoint projectionInsertionPoint, ReadOnlyCollection<SnapshotPoint> sourceInsertionPoints, string insertionText, IList<int> insertionSizes) {
                // We always favor the last buffer of our language type.  This handles cases where we're on a boundary between a prompt and a language 
                // buffer - we favor the language buffer because the prompts cannot be edited.  In the cast of two language buffers this also works because
                // our spans are laid out like:
                // <lang span 1 including newline>
                // <prompt span><lang span 2>
                // 
                // In the case where the prompts are in the margin we have an insertion conflicct between the two language spans.  But because
                // lang span 1 includes the new line in order to be oun the boundary we need to be on lang span 2's line.
                // 
                // This works the same way w/ our input buffer where the input buffer present instead of <lang span 2>.
                for (int i = sourceInsertionPoints.Count - 1; i >= 0; i--) {
                    var insertionBuffer = sourceInsertionPoints[i].Snapshot.TextBuffer;
                    if (insertionBuffer.ContentType == _replWindow._contentType ||
                        insertionBuffer == _replWindow._inputBuffer) {
                        insertionSizes[i] = insertionText.Length;
                        break;
                    }
                }                
            }

            public void FillInReplacementSizes(SnapshotSpan projectionReplacementSpan, ReadOnlyCollection<SnapshotSpan> sourceReplacementSpans, string insertionText, IList<int> insertionSizes) {
                ;
            }

            public int GetTypicalInsertionPosition(SnapshotPoint projectionInsertionPoint, ReadOnlyCollection<SnapshotPoint> sourceInsertionPoints) {
                return 0;
            }

            #endregion
        }

        private void CreateTextViewHost() {
            var adapterFactory = ComponentModel.GetService<IVsEditorAdaptersFactoryService>();
            var provider = (Microsoft.VisualStudio.OLE.Interop.IServiceProvider)GetService(typeof(Microsoft.VisualStudio.OLE.Interop.IServiceProvider));

            var contentTypeRegistry = _componentModel.GetService<IContentTypeRegistryService>();
            var textContentType = contentTypeRegistry.GetContentType("text");
            var replContentType = contentTypeRegistry.GetContentType(ReplConstants.ReplContentTypeName);
            var replOutputContentType = contentTypeRegistry.GetContentType(ReplConstants.ReplOutputContentTypeName);

            _outputBuffer = _bufferFactory.CreateTextBuffer(replOutputContentType);
            _outputBuffer.Properties.AddProperty(ReplOutputClassifier.ColorKey, _outputColors);
            _inputBuffer = _bufferFactory.CreateTextBuffer();
            _promptBuffer = _bufferFactory.CreateTextBuffer();

            var projBuffer = _projectionFactory.CreateProjectionBuffer(
                new EditResolver(this),
                new object[0],
                ProjectionBufferOptions.None,
                replContentType);

            var bufferAdapter = adapterFactory.CreateVsTextBufferAdapterForSecondaryBuffer(provider, projBuffer);

            // we need to set IReplProptProvider property before TextViewHost is instantiated so that ReplPromptTaggerProvider can bind to it 
            projBuffer.Properties.AddProperty(typeof(ReplWindow), this);

            // Create and inititalize text view adapter.
            // WARNING: This might trigger various services like IntelliSense, margins, taggers, etc.
            IVsTextView textViewAdapter = adapterFactory.CreateVsTextViewAdapter(provider, GetReplRoles());

            // make us a code window so we'll have the same colors as a normal code window.
            IVsTextEditorPropertyContainer propContainer;
            ErrorHandler.ThrowOnFailure(((IVsTextEditorPropertyCategoryContainer)textViewAdapter).GetPropertyCategory(Microsoft.VisualStudio.Editor.DefGuidList.guidEditPropCategoryViewMasterSettings, out propContainer));
            propContainer.SetProperty(VSEDITPROPID.VSEDITPROPID_ViewComposite_AllCodeWindowDefaults, true);
            propContainer.SetProperty(VSEDITPROPID.VSEDITPROPID_ViewGlobalOpt_AutoScrollCaretOnTextEntry, true);

            textViewAdapter.Initialize(
                (IVsTextLines)bufferAdapter,
                IntPtr.Zero,
                (uint)TextViewInitFlags.VIF_HSCROLL | (uint)TextViewInitFlags.VIF_VSCROLL | (uint)TextViewInitFlags3.VIF_NO_HWND_SUPPORT,
                new[] { new INITVIEW { fSelectionMargin = 0, fWidgetMargin = 0, fVirtualSpace = 0, fDragDropMove = 1 } }
            );                         


            // disable change tracking because everything will be changed
            var res = adapterFactory.GetWpfTextViewHost(textViewAdapter);
            var options = res.TextView.Options;

            // propagate language options to our text view
            IVsTextManager textMgr = (IVsTextManager)ReplWindowPackage.GetGlobalService(typeof(SVsTextManager));
            var langPrefs = new LANGPREFERENCES[1];
            langPrefs[0].guidLang = LanguageServiceGuid;
            ErrorHandler.ThrowOnFailure(textMgr.GetUserPreferences(null, null, langPrefs, null));

            options.SetOptionValue(DefaultTextViewHostOptions.ChangeTrackingId, false);
            options.SetOptionValue(DefaultOptions.ConvertTabsToSpacesOptionId, langPrefs[0].fInsertTabs == 0);
            options.SetOptionValue(DefaultOptions.TabSizeOptionId, (int)langPrefs[0].uTabSize);
            options.SetOptionValue(DefaultOptions.IndentSizeOptionId, (int)langPrefs[0].uIndentSize);

            res.HostControl.Name = GetName(Title);

            _projectionBuffer = projBuffer;

            // get our classifier...
            _primaryClassifier = projBuffer.Properties.GetProperty<ReplAggregateClassifier>(typeof(ReplAggregateClassifier));

            // aggreggate output classifications
            var outputClassifier = _classifierAgg.GetClassifier(_outputBuffer);
            _primaryClassifier.AddClassifier(_projectionBuffer, _outputBuffer, outputClassifier);

            _textViewHost = res;
        }

        /// <summary>
        /// Createsa and adds a new language buffer to the projection buffer.
        /// </summary>
        private void AddLanguageBuffer() {
            _currentLanguageBuffer = _bufferFactory.CreateTextBuffer(_contentType);
            _currentLanguageBuffer.Properties.AddProperty(typeof(IReplEvaluator), _evaluator);
            
            // add the whole buffer to the projection buffer and set it up to expand to the right as text is appended
            AddInitialLanguageSpan();

            // get the real classifier, and have our classifier start listening and forwarding events            
            var contentClassifier = _classifierAgg.GetClassifier(_currentLanguageBuffer);
            _primaryClassifier.AddClassifier(_projectionBuffer, _currentLanguageBuffer, contentClassifier);
        }

        private void AddInitialLanguageSpan() {
            var span = CreateLanguageTrackingSpan(new Span(0, 0));
            _spans.Add(new ReplSpan(span, ReplSpanKind.Language));
            _projectionBuffer.InsertSpan(_spans.Count - 1, span);
        }
       
        /// <summary>
        /// Produces a name which is compatible with x:Name requirements (starts with a letter/underscore, contains
        /// only letter, numbers, or underscores).
        /// </summary>
        private static string GetName(string title) {
            if (title.Length == 0) {
                return "InteractiveWindowHost";
            }

            StringBuilder res = new StringBuilder();
            if (!Char.IsLetter(title[0])) {
                res.Append('_');
            }

            foreach (char c in title) {
                if (Char.IsLetter(c) || Char.IsDigit(c) || c == '_') {
                    res.Append(c);
                }
            }
            res.Append("Host");
            return res.ToString();
        }

        protected ITextViewRoleSet/*!*/ GetReplRoles() {
            var textEditorFactoryService = ComponentModel.GetService<ITextEditorFactoryService>();            
            return textEditorFactoryService.CreateTextViewRoleSet(
                PredefinedTextViewRoles.Analyzable, 
                PredefinedTextViewRoles.Editable, 
                PredefinedTextViewRoles.Interactive, 
                PredefinedTextViewRoles.Zoomable, 
                PredefinedTextViewRoles.Document, 
                ReplConstants.ReplTextViewRole
            );
        }

        #endregion

        #region IReplWindow

        /// <summary>
        /// See IReplWindow
        /// </summary>
        public IWpfTextView/*!*/ TextView {
            get { 
                return _textViewHost.TextView; 
            }
        }

        /// <summary>
        /// See IReplWindow
        /// </summary>
        public IReplEvaluator/*!*/ Evaluator {
            get {
                return _evaluator;
            }
        }

        /// <summary>
        /// See IReplWindow
        /// </summary>
        public string/*!*/ Title {
            get {
                return Caption;
            }
        }

        /// <summary>
        /// See IReplWindow
        /// </summary>
        public void ClearScreen() {
            if (!CheckAccess()) {
                Dispatcher.Invoke(new Action(() => ClearScreen()));
                return;
            }

            RemoveProtection();
            
            _secondaryPrompts.Clear();
            _adornmentToMinimize = false;
            InlineReplAdornmentProvider.RemoveAllAdornments(TextView);

            // remove all the spans except our initial span from the projection buffer
            _projectionBuffer.DeleteSpans(0, _spans.Count);
            _spans.Clear();
            _prompts.Clear();

            // then clear our output and prompt buffers
            using (var edit = _outputBuffer.CreateEdit()) {
                edit.Delete(0, _outputBuffer.CurrentSnapshot.Length);
                edit.Apply();
            }
            using (var edit = _promptBuffer.CreateEdit()) {
                edit.Delete(0, _promptBuffer.CurrentSnapshot.Length);
                edit.Apply();
            }
            using (var edit = _inputBuffer.CreateEdit()) {
                edit.Delete(0, _inputBuffer.CurrentSnapshot.Length);
                edit.Apply();
            }
            _currentInput = 1;
            _outputColors.Clear();
        }

        /// <summary>
        /// See IReplWindow
        /// </summary>
        public void Focus() {
            var textView = TextView;

            IInputElement input = textView as IInputElement;
            if (input != null) {
                Keyboard.Focus(input);
            }
        }

        /// <summary>
        /// See IReplWindow
        /// </summary>
        public void PasteText(string text) {
            if (!CheckAccess()) {
                Dispatcher.BeginInvoke(new Action(() => PasteText(text)));
                return;
            }

            if (text.IndexOf('\n') >= 0 || _isRunning) {
                if (IsCaretProtected) {
                    _editorOperations.MoveToEndOfDocument(false);
                }

                _pendingInput.AddRange(SplitLines(text));

                if (!_isRunning) {
                    ProcessPendingInput();
                }
            } else {
                if (_editorOperations.SelectedText != "") {
                    _editorOperations.Delete();
                }

                _editorOperations.InsertText(text);
            }
        }

        /// <summary>
        /// See IReplWindow
        /// </summary>
        public void Reset() {
            WriteLine("Resetting execution engine");
            Evaluator.Reset();
        }

        public void AbortCommand() {
            if (_isRunning) {
                Evaluator.AbortCommand();
            } else {
                UIThread(() => {
                    EnsureNewLine();
                    PrepareForInput();
                });
            }
        }

        /// <summary>
        /// See IReplWindow
        /// </summary>
        public void WriteLine(string text) {
            _buffer.Write(text + _textViewHost.TextView.Options.GetNewLineCharacter());
        }

        public void WriteOutput(object output) {
            UIThread(() => {
                Write(output);
            });
        }

        public void WriteError(object output) {
            UIThread(() => {
                Write(output, error: true);
            });
        }

        public string ReadInput() {
            // shouldn't be called on the UI thread because we'll hang
            Debug.Assert(!CheckAccess());

            bool wasRunning = _isRunning;

            UIThread(() => {
                RemoveProtection();
                // TODO: What do we do if we weren't running?
                if (_isRunning) {
                    _isRunning = false;
                } else if (_spans.Count > 0 && _spans[_spans.Count - 1].Kind == ReplSpanKind.Language) {
                    // we need to remove our input prompt.
                    _projectionBuffer.DeleteSpans(_spans.Count - 2, 2);
                    _spans.RemoveRange(_spans.Count - 2, 2);                    
                }

                _buffer.Flush();

                AddInputSpans();

                Caret.EnsureVisible();
                ResetCursor();

                _isRunning = false;
                _unCommittedInput = null;

                // we need to update margin prompt after the new _inputPoint is set:
                if (_displayPromptInMargin) {
                    var promptChanged = PromptChanged;
                    if (promptChanged != null) {
                        promptChanged(new SnapshotSpan(CurrentSnapshot, new Span(CurrentSnapshot.Length, 0)));
                    }
                }

                _inputStart = _inputBuffer.CurrentSnapshot.Length;

                ApplyProtection();
            });

            _inputEvent.WaitOne();
            _inputStart = null;

            UIThread(() => {
                RemoveProtection();

                // replace previous span w/ a span that won't grow...
                Debug.Assert(_spans[_spans.Count - 1].Kind == ReplSpanKind.StandardInput);

                var newSpan = _spans[_spans.Count - 1] = new ReplSpan(
                    new CustomTrackingSpan(
                        _inputBuffer.CurrentSnapshot,
                        _spans[_spans.Count - 1].Span.GetSpan(_inputBuffer.CurrentSnapshot),
                        PointTrackingMode.Negative,
                        PointTrackingMode.Negative
                    ),
                    ReplSpanKind.StandardInput
                );
                _projectionBuffer.ReplaceSpans(_spans.Count - 1, 1, new[] { newSpan.Span }, EditOptions.None, null);

                if (wasRunning) {
                    _isRunning = true;
                } else {
                    PrepareForInput();
                }
                ApplyProtection();
            });

            _history.Add(_inputValue);
            return _inputValue;
        }

        private void AddInputSpans() {
            
            if (!_displayPromptInMargin) {
                AppendPrompt(_inputPrompt, ReplSpanKind.StandardInputPrompt);
            } else {
                AppendPrompt("", ReplSpanKind.StandardInputPrompt);
            }


            var span = new CustomTrackingSpan(
                _inputBuffer.CurrentSnapshot,
                new Span(_inputBuffer.CurrentSnapshot.Length, 0)
            );
            var promptSpan = new ReplSpan(span, ReplSpanKind.StandardInput);
            _spans.Add(promptSpan);

            _prompts[_projectionBuffer.CurrentSnapshot.GetLineNumberFromPosition(CurrentSnapshot.Length)] = promptSpan;
            _projectionBuffer.InsertSpan(_spans.Count - 1, span);
        }

        /// <summary>
        /// Sets the current value for the specified option.
        /// </summary>
        public void SetOptionValue(ReplOptions option, object value) {
            Exception toThrow = null;
            UIThread(() => {
                try {
                    switch (option) {
                        case ReplOptions.CommandPrefix: _commandPrefix = CheckOption<string>(option, value); break;
                        case ReplOptions.DisplayPromptInMargin:
                            bool prevValue = _displayPromptInMargin;
                            _displayPromptInMargin = CheckOption<bool>(option, value);

                            if (prevValue != _displayPromptInMargin) {
                                TextView.Options.SetOptionValue(DefaultTextViewHostOptions.GlyphMarginId, _displayPromptInMargin);

                                if (_displayPromptInMargin) {
                                    UpdatePrompts(ReplSpanKind.Prompt, _prompt, "");
                                    UpdatePrompts(ReplSpanKind.SecondaryPrompt, _secondPrompt, "");
                                } else {
                                    UpdatePrompts(ReplSpanKind.Prompt, "", _prompt);
                                    UpdatePrompts(ReplSpanKind.SecondaryPrompt, "", _secondPrompt);
                                }
                            }
                            break;
                        case ReplOptions.PrimaryPrompt:
                            if (value == null) {
                                throw new InvalidOperationException("Primary prompt cannot be null");
                            }
                            string oldPrompt = _prompt;
                            _prompt = CheckOption<string>(option, value);
                            if (!_displayPromptInMargin) {
                                // update the prompts
                                UpdatePrompts(ReplSpanKind.Prompt, oldPrompt, _prompt);
                            } else {
                                InvalidatePromptGlyphs();
                            }
                            break;
                        case ReplOptions.SecondaryPrompt:
                            oldPrompt = _secondPrompt;
                            _secondPrompt = CheckOption<string>(option, value) ?? "";
                            if (!_displayPromptInMargin) {
                                UpdatePrompts(ReplSpanKind.SecondaryPrompt, oldPrompt, _secondPrompt);
                            } else {
                                InvalidatePromptGlyphs();
                            }
                            break;
                        case ReplOptions.StandardInputPrompt:
                            if (value == null) {
                                throw new InvalidOperationException("Primary prompt cannot be null");
                            }
                            oldPrompt = _inputPrompt;
                            _inputPrompt = CheckOption<string>(option, value);
                            if (!_displayPromptInMargin) {
                                // update the prompts
                                UpdatePrompts(ReplSpanKind.StandardInputPrompt, oldPrompt, _inputPrompt);
                            } else {
                                InvalidatePromptGlyphs();
                            }
                            break;
                        case ReplOptions.UseSmartUpDown: _useSmartUpDown = CheckOption<bool>(option, value); break;
                        case ReplOptions.ShowOutput:
                            _buffer.Flush();
                            _showOutput = CheckOption<bool>(option, value);
                            break;
                        case ReplOptions.SupportAnsiColors:
                            _buffer.Flush();
                            _buffer.ProcessAnsiEscapes = CheckOption<bool>(option, value);
                            break;
                        case ReplOptions.FormattedPrompts:
                            bool oldFormattedPrompts = _formattedPrompts;
                            _formattedPrompts = CheckOption<bool>(option, value);
                            if (oldFormattedPrompts != _formattedPrompts) {
                                UpdatePrompts(ReplSpanKind.StandardInputPrompt, null, _inputPrompt);
                                UpdatePrompts(ReplSpanKind.Prompt, null, _prompt);
                                UpdatePrompts(ReplSpanKind.SecondaryPrompt, null, _secondPrompt);
                            }
                            break;
                        default:
                            throw new InvalidOperationException(String.Format("Unknown option: {0}", option));
                    }
                } catch (Exception e) {
                    toThrow = e;
                }
            });
            if (toThrow != null) {
                // throw exception on original thread, not the UI thread.
                throw toThrow;
            }
        }

        private void InvalidatePromptGlyphs() {
            var promptChanged = PromptChanged;
            if (promptChanged != null) {
                promptChanged(new SnapshotSpan(CurrentSnapshot, new Span(0, CurrentSnapshot.Length)));
            }
        }

        private void UpdatePrompts(ReplSpanKind promptKind, string oldPrompt, string newPrompt) {
            if ((oldPrompt != newPrompt || oldPrompt == null) && _spans.Count > 0)  {                
                if (promptKind == ReplSpanKind.SecondaryPrompt) {
                    _secondaryPrompts.Clear();
                }
                RemoveProtection();

                // find and replace all the prompts
                int curInput = 1;
                for (int i = 0; i < _spans.Count; i++) {
                    if (_spans[i].Kind == promptKind) {
                        int location = _promptBuffer.CurrentSnapshot.Length;
                        _promptBuffer.Insert(
                            _promptBuffer.CurrentSnapshot.Length,
                            FormatPrompt(newPrompt, promptKind == ReplSpanKind.Prompt ? curInput : curInput - 1) + " "
                        );
                        var newPromptSpan = new Span(location, newPrompt.Length);

                        var promptSpan = _promptBuffer.CurrentSnapshot.CreateTrackingSpan(
                            newPromptSpan,
                            SpanTrackingMode.EdgeExclusive
                        );
                        _projectionBuffer.ReplaceSpans(i, 1, new[] { promptSpan }, EditOptions.None, null);

                        IReadOnlyRegion readOnlyRegion = null;
                        if (promptKind == ReplSpanKind.SecondaryPrompt) {
                            using (var readonlyEdit = _promptBuffer.CreateReadOnlyRegionEdit()) {
                                readOnlyRegion = readonlyEdit.CreateReadOnlyRegion(newPromptSpan);
                                readonlyEdit.Apply();
                            }
                        }

                        _spans[i] = new ReplSpan(
                            promptSpan,
                            promptKind,
                            readOnlyRegion
                        );
                    }

                    if (_spans[i].Kind == ReplSpanKind.Prompt) {
                        curInput++;
                    }
                }
                ApplyProtection();
                
            }
        }

        private T CheckOption<T>(ReplOptions option, object o) {
            if (!(o is T)) {
                throw new InvalidOperationException(String.Format(
                    "Got wrong type ({0}) for option {1}",
                    o == null ? "null" : o.GetType().Name,
                    option.ToString())
                );
            }

            return (T)o;
        }

        /// <summary>
        /// Gets the current value for the specified option.
        /// </summary>
        public object GetOptionValue(ReplOptions option) {
            switch (option) {
                case ReplOptions.CommandPrefix: return _commandPrefix;
                case ReplOptions.DisplayPromptInMargin: return _displayPromptInMargin;
                case ReplOptions.PrimaryPrompt: return _prompt;
                case ReplOptions.SecondaryPrompt: return _secondPrompt;
                case ReplOptions.ShowOutput: return _showOutput;
                case ReplOptions.UseSmartUpDown: return _useSmartUpDown;
                case ReplOptions.SupportAnsiColors: return _buffer.ProcessAnsiEscapes;
                case ReplOptions.FormattedPrompts: return _formattedPrompts;
                default:
                    throw new InvalidOperationException(String.Format("Unknown option: {0}", option));
            }
        }

        #endregion

        #region Commands

        /// <summary>
        /// Clears the current input
        /// </summary>
        public void Cancel() {
            ClearInput();
            _editorOperations.MoveToEndOfDocument(false);
            _unCommittedInput = null;
        }

        /// <summary>
        /// Scrolls through history or moves the caret through the text view.
        /// </summary>
        public void SmartUpArrow() {
            UIThread(() => {
                if (!((IIntellisenseCommandTarget)this.SessionStack).ExecuteKeyboardCommand(IntellisenseKeyboardCommand.Up)) {
                    // uparrow and downarrow at the end of input or with empty input rotate history
                    // with multi-line input, uparrow and downarrow move around in text
                    if (!_isRunning && CaretAtEnd && _useSmartUpDown) {
                        HistoryPrevious();
                    } else {
                        _editorOperations.MoveLineUp(false);
                    }
                }
            });
        }

        public void HistoryPrevious() {
            var found = _history.FindPrevious("");
            if (found != null) {
                StoreUncommittedInput();
                SetActiveInput(found);
            }
        }

        /// <summary>
        /// Scrolls down through history or moves the caret through the text view.
        /// </summary>
        public void SmartDownArrow() {
            UIThread(() => {
                if (!((IIntellisenseCommandTarget)this.SessionStack).ExecuteKeyboardCommand(IntellisenseKeyboardCommand.Down)) {
                    if (!_isRunning && CaretAtEnd && _useSmartUpDown) {
                        HistoryNext();
                    } else {
                        _editorOperations.MoveLineDown(false);
                    }
                }
            });
        }

        public void HistoryNext() {
            var found = _history.FindNext("");
            if (found != null) {
                StoreUncommittedInput();
                SetActiveInput(found);
            } else {
                InsertUncommittedInput();
            }
        }

        /// <summary>
        /// Moves to the beginning of the line.
        /// </summary>
        public void Home(bool extendSelection) {
            UIThread(() => {
                if (((IIntellisenseCommandTarget)this.SessionStack).ExecuteKeyboardCommand(IntellisenseKeyboardCommand.Home)) {
                    return;
                } else if (SessionStack.TopSession != null) {
                    this.SessionStack.TopSession.Dismiss();
                }

                var caret = Caret;
                var currentInput = MakeInputSpan();

                if (currentInput != null && !_displayPromptInMargin) {
                    var start = currentInput.GetSpan(InputSnapshot).Start;

                    int lineNumber = InputSnapshot.GetLineNumberFromPosition(start.Position);
                    
                    var langPoint = TextView.BufferGraph.MapDownToFirstMatch(
                        caret.Position.BufferPosition,
                        PointTrackingMode.Positive,
                        x => x.TextBuffer.ContentType == _contentType,
                        PositionAffinity.Successor);

                    if (langPoint == null) {
                        // we're on some random line or in prompts, just go to the beginning of the buffer
                        _editorOperations.MoveToStartOfLine(extendSelection);
                    } else {
                        int lineNo = caret.Position.BufferPosition.GetContainingLine().LineNumber;
                        int columnOffset = _prompts[lineNo].Span.GetSpan(_prompts[lineNo].Span.TextBuffer.CurrentSnapshot).Length;
                        
                        var moveTo = caret.Position.BufferPosition.GetContainingLine().Start + columnOffset;
                        
                        if (extendSelection) {
                            VirtualSnapshotPoint anchor = TextView.Selection.AnchorPoint;
                            caret.MoveTo(moveTo);
                            TextView.Selection.Select(anchor.TranslateTo(TextView.TextSnapshot), TextView.Caret.Position.VirtualBufferPosition);
                        } else {
                            TextView.Selection.Clear();
                            caret.MoveTo(moveTo);
                        }
                    }
                } else {
                    _editorOperations.MoveToStartOfLine(extendSelection);
                }
            });
        }

        /// <summary>
        /// Pastes from the clipboard into the text view
        /// </summary>
        public bool PasteClipboard() {
            return UIThread(() => {
                string format = _evaluator.FormatClipboard();
                if (format != null) {
                    PasteText(format);
                } else if (Clipboard.ContainsText()) {
                    PasteText(Clipboard.GetText());
                } else {
                    return false;
                }
                return true;
            });
        }

        #endregion

        #region IReplPromptProvider Members

        public ReplSpanKind HasPromptForLine(ITextSnapshot/*!*/ snapshot, int lineNumber) {
            ReplSpan ps;
            if (_prompts.TryGetValue(lineNumber, out ps)) {
                return ps.Kind;
            }
            return ReplSpanKind.None;
        }

        public event Action<SnapshotSpan> PromptChanged;

        internal string/*!*/ Prompt {
            get { return _prompt; }
        }

        internal string/*!*/ SecondaryPrompt {
            get { return _secondPrompt; }
        }

        internal string/*!*/ InputPrompt {
            get { return _inputPrompt; }
        }

        public Control/*!*/ HostControl {
            get { return _textViewHost.HostControl; }
        }

        #endregion

        #region Protected Methods

        protected override void Dispose(bool disposing) {
            if (disposing) {
                Evaluator.Dispose();

                _buffer.Dispose();

                _commands = null;
            }
        }

        #endregion

        #region Internal Methods

        internal bool CanExecuteInput() {
            var input = ActiveInput;
            if (input.Trim().Length == 0) {
                // Always allow "execution" of a blank line.
                // This will just close the current prompt and start a new one
                return true;
            }

            // Ignore any whitespace past the insertion point when determining
            // whether or not we're at the end of the input
            var pt = RelativeInsertionPoint;
            var atEnd = (pt == input.Length) || (pt >= 0 && input.Substring(pt).Trim().Length == 0);
            if (!atEnd) {
                return false;
            }

            // A command is never multi-line, so always try to execute something which looks like a command
            if (input.StartsWith(_commandPrefix)) {
                return true;
            }

            return Evaluator.CanExecuteText(input);
        }

        #endregion

        #region Private Methods

        private Dispatcher Dispatcher {
            get {
                return ((FrameworkElement)TextView).Dispatcher;
            }
        }

        private int CurrentInsertionPoint {
            get { return Caret.Position.BufferPosition.Position; }
        }

        private bool CaretAtEnd {
            get { return CurrentInsertionPoint == CurrentSnapshot.Length; }
        }

        /// <summary>
        /// Returns the full text of the current active input.
        /// </summary>
        private string ActiveInput {
            get {
                return _currentLanguageBuffer.CurrentSnapshot.GetText();
            }
        }

        /// <summary>
        /// Returns the insertion point relative to the current language buffer.
        /// </summary>
        private int RelativeInsertionPoint {
            get {
                var langPoint = _textViewHost.TextView.BufferGraph.MapDownToBuffer(
                    new SnapshotPoint(
                        _projectionBuffer.CurrentSnapshot,
                       CurrentInsertionPoint
                    ),
                    PointTrackingMode.Positive,
                    _currentLanguageBuffer,
                    PositionAffinity.Predecessor
                );
                if (langPoint != null) {
                    return langPoint.Value;
                }
                return InputSnapshot.Length;
            }
        }
        
        private IIntellisenseSessionStack SessionStack {
            get {
                if (_sessionStack == null) {
                    IIntellisenseSessionStackMapService stackMapService = ComponentModel.GetService<IIntellisenseSessionStackMapService>();
                    _sessionStack = stackMapService.GetStackForTextView(TextView);
                }

                return _sessionStack;
            }
        }

        private bool IsCaretProtected {
            get {
                return _readOnlyRegions[0] != null &&
                    Caret.Position.BufferPosition.Position < _readOnlyRegions[0].Span.GetStartPoint(TextView.TextBuffer.CurrentSnapshot).Position;
            }
        }

        public bool CaretInCodeInputRegion {
            get {
                var point = TextView.BufferGraph.MapDownToBuffer(
                    Caret.Position.BufferPosition,
                    PointTrackingMode.Positive,
                    _currentLanguageBuffer,
                    PositionAffinity.Successor
                );

                return point != null;
            }
        }

        public bool CaretInStandardInputRegion {
            get {

                var point = TextView.BufferGraph.MapDownToBuffer(
                    Caret.Position.BufferPosition,
                    PointTrackingMode.Positive,
                    _inputBuffer,
                    PositionAffinity.Successor
                );

                return point != null;
            }
        }

        private bool CheckAccess() {
            return Dispatcher.CheckAccess();
        }

        private void AppendInput(string text) {
            Debug.Assert(CheckAccess());

            var inputSpan = _spans[_spans.Count - 1];
            Debug.Assert(inputSpan.Kind == ReplSpanKind.Language | inputSpan.Kind == ReplSpanKind.StandardInput);
            Debug.Assert(inputSpan.Span.TrackingMode == SpanTrackingMode.Custom);

            var buffer = inputSpan.Span.TextBuffer;
            var span = inputSpan.Span.GetSpan(buffer.CurrentSnapshot);
            using (var edit = buffer.CreateEdit()) {
                edit.Insert(edit.Snapshot.Length, text);
                edit.Apply();
            }

            var replSpan = _spans[_spans.Count - 1] = new ReplSpan(
                new CustomTrackingSpan(
                    buffer.CurrentSnapshot,
                    new Span(span.Start, span.Length + text.Length)
                ),
                inputSpan.Kind
            );
            _projectionBuffer.ReplaceSpans(_spans.Count - 1, 1, new[] { replSpan.Span }, EditOptions.None, null);

            Caret.EnsureVisible();
        }

        private void EnsureNewLine() {
            using (var edit = _currentLanguageBuffer.CreateEdit()) {
                edit.Insert(edit.Snapshot.Length, _textViewHost.TextView.Options.GetNewLineCharacter());
                edit.Apply();
            }

            if (!CaretAtEnd) {
                _editorOperations.MoveToEndOfDocument(false);
            }
        }

        private ITrackingSpan MakeInputSpan() {            
            if (_isRunning) {
                return null;
            }

            var snapshot = InputSnapshot;
            int len = snapshot.Length;
            
            return snapshot.CreateTrackingSpan(new Span(0, len), SpanTrackingMode.EdgeExclusive);
        }

        private void OnAdornmentLoaded(object source, EventArgs e) {
            // Make sure the caret line is rendered
            DoEvents();
            Caret.EnsureVisible();
        }
        
        private void OnInlineAdornmentAdded() {
            _adornmentToMinimize = true;
        }

        private bool ProcessPendingInput() {
            Debug.Assert(CheckAccess());

            while (_pendingInput.Count > 0) {
                var line = _pendingInput[0];
                _pendingInput.RemoveAt(0);
                AppendInput(line.Text);
                _editorOperations.MoveToEndOfDocument(false);
                if (line.HasNewline) {
                    int len = _currentLanguageBuffer.CurrentSnapshot.Length;

                    EnsureNewLine();

                    if (TryExecuteInput()) {
                        return true;
                    }

                    InsertSecondaryPrompt(len);
                }
            }
            return false;
        }

        private bool RemoveProtection() {
            bool wasProtected = false;
            using (var edit = TextBuffer.CreateReadOnlyRegionEdit()) {
                foreach (var region in _readOnlyRegions) {
                    if (region != null) {
                        edit.RemoveReadOnlyRegion(region);
                        wasProtected = true;
                    }
                }
                edit.Apply();
                _readOnlyRegions[0] = _readOnlyRegions[1] = null;
            }

            return wasProtected;
        }

        /// <summary>
        /// Protects the read only portion of the buffer.  We protect the entire buffer but if we're reading
        /// input we allow insertions at the end.
        /// </summary>
        private void ApplyProtection() {
            if (_isRunning) {
                ApplyProtection(SpanTrackingMode.EdgeInclusive, EdgeInsertionMode.Deny);
            } else {
                ApplyProtection(SpanTrackingMode.EdgeExclusive, EdgeInsertionMode.Allow);
            }
        }

        private void ApplyProtection(SpanTrackingMode trackingMode, EdgeInsertionMode insertionMode) {
            using (var edit = TextBuffer.CreateReadOnlyRegionEdit()) {
                int end = CurrentSnapshot.Length;
                IReadOnlyRegion region0 = edit.CreateReadOnlyRegion(new Span(0, end), trackingMode, insertionMode);

                // Create a second read-only region to prevent insert at start of buffer.
                IReadOnlyRegion region1 = (end > 0) ? edit.CreateReadOnlyRegion(new Span(0, 0), SpanTrackingMode.EdgeExclusive, EdgeInsertionMode.Deny) : null;

                edit.Apply();
                _readOnlyRegions[0] = region0;
                _readOnlyRegions[1] = region1;
            }
        }

        private bool ExecuteReplCommand(string commandLine) {
            commandLine = commandLine.Trim();
            IReplCommand commandFn = null;
            string args, command = null;
            if (commandLine.Length == 0 || commandLine == "help") {
                ShowReplHelp();
                return true;
            } else if (commandLine.Substring(0, 1) == _commandPrefix) { // TODO ??
                // REPL-level comment; do nothing
                return true;
            } else {
                command = commandLine.Split(' ')[0];
                args = commandLine.Substring(command.Length).Trim();
                commandFn = _commands.Find(x => x.Command == command);
            }

            if (commandFn == null) {
                WriteLine(String.Format("Unknown command '{0}', use \"{1}help\" for help", command, _commandPrefix));
                return false;
            }

            // commandFn is either an Action or Action<string>
            commandFn.Execute(this, args);
            return true;
        }

        public bool TryExecuteInput() {
            bool tryIt = CanExecuteInput();
            if (tryIt) {
                ExecuteText();
            }
            return tryIt;
        }

        private void PerformWrite(Action action) {
            if (!CheckAccess()) {
                Dispatcher.Invoke(new Action(() => PerformWrite(action)));
                return;
            }

            bool wasProtected = RemoveProtection();
            try {
                action();
            } finally {
                if (wasProtected) {
                    ApplyProtection();
                }
            }
        }

        /// <summary>
        /// Execute and then call the callback function with the result text.
        /// </summary>
        /// <param name="processResult"></param>
        internal void ExecuteText() {
            PerformWrite(() => {
                // Ensure that the REPL doesn't try to execute if it is already
                // executing.  If this invariant can no longer be maintained more of
                // the code in this method will need to be bullet-proofed
                if (_isRunning) {
                    return;
                }

                var text = ActiveInput;

                if (_adornmentToMinimize) {
                    InlineReplAdornmentProvider.MinimizeLastInlineAdornment(TextView);
                    _adornmentToMinimize = false;
                }
                
                var span = CreateInputSpan(text);

                _isRunning = true;

                // Following method assumes that _isRunning will be cleared before 
                // the following method is called again.
                StartCursorTimer();

                _sw = Stopwatch.StartNew();
                if (text.Trim().Length == 0) {
                    // Special case to avoid round-trip when remoting
                    FinishExecute(new ExecutionResult(true));
                } else if (text.StartsWith(_commandPrefix)) {
                    _history.Last.Command = true;
                    var status = ExecuteReplCommand(text.Substring(_commandPrefix.Length));
                    FinishExecute(new ExecutionResult(status));
                } else if (!Evaluator.ExecuteText(ActiveInput, FinishExecute)) {
                    FinishExecute(new ExecutionResult(false));
                }
            });
        }

        private ITrackingSpan CreateInputSpan(string text) {
            TextView.Selection.Clear();

            // output point moves to after the current input
            var span = MakeInputSpan();

            if (text.Length > 0) {
                _history.Add(text.TrimEnd(_whitespaceChars));
            }
            return span;
        }

        private void FinishExecute(ExecutionResult result) {
            PerformWrite(() => {
                _sw.Stop();
                _buffer.Flush();
                
                if (_history.Last != null) {
                    _history.Last.Duration = _sw.Elapsed.Seconds;
                }
                if (!result.Success) {
                    if (_history.Last != null) {
                        _history.Last.Failed = true;
                    }
                }
                PrepareForInput();
            });
        }

        private void SmartExecute() {
            Debug.Assert(CheckAccess());

            if (CaretInCodeInputRegion) {
                EnsureNewLine();
                ExecuteText();
            } else if(!CaretInStandardInputRegion) {
                string input = GetCurrentSelectedInput();
                if (input != null) {
                    _editorOperations.MoveToEndOfDocument(false);
                    SetActiveInput(input);
                }
            }
        }

        private string GetCurrentSelectedInput() {
            var pt = TextView.BufferGraph.MapDownToFirstMatch(
                Caret.Position.BufferPosition,
                PointTrackingMode.Positive,
                x => x.TextBuffer.ContentType == _contentType,
                PositionAffinity.Successor
            );

            if (pt != null) {
                return pt.Value.Snapshot.GetText();
            }
            return null;
        }
        
        private void SetupOutput() {
            _showOutput = true;
        }

        private void ShowReplHelp() {
            var cmdnames = new List<IReplCommand>(_commands.Where(x => x.Command != null));
            cmdnames.Sort((x, y) => String.Compare(x.Command, y.Command));

            const string helpFmt = "  {0,-16}  {1}";
            WriteLine(string.Format(helpFmt, "help", "Show a list of REPL commands"));

            foreach (var cmd in cmdnames) {
                WriteLine(string.Format(helpFmt, cmd.Command, cmd.Description));
            }
        }

        private void PrepareForInput() {
            _buffer.Flush();
            _buffer.ResetColors();

            TrimAutoIndentWhiteSpace();

            AddPrimaryPrompt();

            Caret.EnsureVisible();

            AddLanguageBuffer();

            ResetCursor();
            _isRunning = false;
            _unCommittedInput = null;
            ProcessPendingInput();

            // we need to update margin prompt after the new _inputPoint is set:
            if (_displayPromptInMargin) {
                var promptChanged = PromptChanged;
                if (promptChanged != null) {
                    promptChanged(new SnapshotSpan(CurrentSnapshot, new Span(CurrentSnapshot.Length, 0)));
                }
            }
        }

        private void AddPrimaryPrompt() {
            ReplSpan promptSpan;
            
            if (!_displayPromptInMargin) {
                promptSpan = AppendPrompt(_prompt);
            } else {
                promptSpan = AppendPrompt("");
            }

            _currentInput++;
            _prompts[_projectionBuffer.CurrentSnapshot.GetLineNumberFromPosition(CurrentSnapshot.Length)] = promptSpan;
        }

        private void TrimAutoIndentWhiteSpace() {
            int curLength = CurrentSnapshot.Length;
            if (curLength > 0) {
                // remove any leading white space on the current line which was inserted via auto indent
                var line = _projectionBuffer.CurrentSnapshot.GetLineFromPosition(curLength);
                if (line.Length != 0 && line.GetText().Trim().Length == 0) {
                    _projectionBuffer.Delete(line.Extent);
                }
            }
        }

        private ReplSpan AppendPrompt(string prompt, ReplSpanKind promptKind = ReplSpanKind.Prompt) {
            prompt = FormatPrompt(prompt, _currentInput);

            int outLen = _promptBuffer.CurrentSnapshot.Length;
            _promptBuffer.Insert(outLen, prompt);

            var span = _promptBuffer.CurrentSnapshot.CreateTrackingSpan(
                new Span(outLen, prompt.Length),
                SpanTrackingMode.EdgeExclusive
            );
            var replSpan = new ReplSpan(span, promptKind);
            _spans.Add(replSpan);
            _projectionBuffer.InsertSpan(_spans.Count - 1, span);
            return replSpan;
        }

        /// <summary>
        /// Appends text to the output buffer and makes it visible in the projection buffer.
        /// </summary>
        /// <param name="outStr"></param>
        private void AppendOutput(ConsoleColor color, string outStr) {
            AppendTextFromBuffer(color, outStr, _outputBuffer);
        }

        /// <summary>
        /// Appends text to the error buffer and makes it visible in the projection buffer.
        /// </summary>
        /// <param name="outStr"></param>
        private void AppendError(ConsoleColor color, string outStr) {
            AppendTextFromBuffer(color, outStr, _outputBuffer);
        }

        /// <summary>
        /// Worker for appending text into a buffer and adding it to the project buffer.
        /// </summary>
        private void AppendTextFromBuffer(ConsoleColor color, string outStr, ITextBuffer buffer) {
            int outLen = buffer.CurrentSnapshot.Length;
            buffer.Insert(outLen, outStr);

            var span = new CustomTrackingSpan(
                buffer.CurrentSnapshot,
                new Span(outLen, outStr.Length),
                PointTrackingMode.Negative,
                PointTrackingMode.Negative
            );

            var replSpan = new ReplSpan(span, ReplSpanKind.Output);
            _outputColors.Add(new OutputColors(outLen, outStr.Length, color));
            
            bool appended = false;
            
            if (!_isRunning) {
                // insert output before current input
                List<KeyValuePair<int, ITrackingSpan>> promptUpdates = new List<KeyValuePair<int, ITrackingSpan>>();

                for (int i = _spans.Count - 1; i >= 0; i--) {
                    var curKind = _spans[i].Kind;
                    
                    if (curKind == ReplSpanKind.Prompt || curKind == ReplSpanKind.SecondaryPrompt) {
                        var pt = TextView.BufferGraph.MapUpToBuffer(
                            _spans[i].Span.GetStartPoint(_spans[i].Span.TextBuffer.CurrentSnapshot),
                            PointTrackingMode.Positive,
                            PositionAffinity.Successor,
                            _projectionBuffer
                        );
                        Debug.Assert(pt != null);

                        promptUpdates.Add(
                            new KeyValuePair<int,ITrackingSpan>(
                                pt.Value.GetContainingLine().LineNumber,
                                _spans[i].Span
                            )
                        );
                    }

                    if (curKind == ReplSpanKind.Prompt) {
                        _spans.Insert(i, replSpan);
                        _projectionBuffer.InsertSpan(i, span);
                        appended = true;
                        break;
                    }
                }

                if (appended) {
                    for (int i = 0; i < promptUpdates.Count; i++) {
                        var keyValue = promptUpdates[i];

                        var oldSpan = _prompts[keyValue.Key];
                        _prompts.Remove(keyValue.Key);
                        var pt = TextView.BufferGraph.MapUpToBuffer(
                            keyValue.Value.GetStartPoint(keyValue.Value.TextBuffer.CurrentSnapshot),
                            PointTrackingMode.Positive,
                            PositionAffinity.Successor,
                            _projectionBuffer
                        );
                        Debug.Assert(pt != null);
                        _prompts[pt.Value.GetContainingLine().LineNumber] = oldSpan;
                    }
                }
            }

            if (!appended) {
                _spans.Add(replSpan);
                _projectionBuffer.InsertSpan(_spans.Count - 1, span);
            }
        }

        private void ResetCursor() {
            if (_executionTimer != null) {
                _executionTimer.Stop();
            }
            if (_oldCursor != null) {
                ((ContentControl)TextView).Cursor = _oldCursor;
            }
            /*if (_oldCaretBrush != null) {
                CurrentView.Caret.RegularBrush = _oldCaretBrush;
            }*/

            _oldCursor = null;
            //_oldCaretBrush = null;
            _executionTimer = null;
        }

        private void StartCursorTimer() {
            // Save the old value of the caret brush so it can be restored
            // after execution has finished
            //_oldCaretBrush = CurrentView.Caret.RegularBrush;

            // Set the caret's brush to transparent so it isn't shown blinking
            // while code is executing in the REPL
            //CurrentView.Caret.RegularBrush = Brushes.Transparent;

            var timer = new DispatcherTimer();
            timer.Tick += SetRunningCursor;
            timer.Interval = new TimeSpan(0, 0, 0, 0, 250);
            _executionTimer = timer;
            timer.Start();
        }

        private void SetRunningCursor(object sender, EventArgs e) {
            var view = (ContentControl)TextView;

            // Save the old value of the cursor so it can be restored
            // after execution has finished
            _oldCursor = view.Cursor;

            // TODO: Design work to come up with the correct cursor to use
            // Set the repl's cursor to the "executing" cursor
            view.Cursor = Cursors.Wait;

            // Stop the timeer so it doesn't fire again
            _executionTimer.Stop();
        }

        private void StoreUncommittedInput() {
            if (_unCommittedInput == null && !string.IsNullOrEmpty(ActiveInput)) {
                _unCommittedInput = ActiveInput;
            }
        }

        private void InsertUncommittedInput() {
            if (_unCommittedInput != null) {
                SetActiveInput(_unCommittedInput);
                _unCommittedInput = null;
            }
        }

        internal void OutputBufferedText(ConsoleColor color, string text) {
            AppendOutput(color, text);
            Caret.EnsureVisible();
        }

        internal void OutputBufferedError(ConsoleColor color, string text) {
            AppendError(color, text);
            Caret.EnsureVisible();
        }

        private void Write(object text, bool error = false) {
            if (_showOutput && !TryShowObject(text)) {
                // buffer the text
                _buffer.Write(text.ToString(), isError: error);
            }
        }

        private bool TryShowObject(object obj) {
            UIElement element = obj as UIElement;
            if (element != null) {
                _buffer.Flush();
                InlineReplAdornmentProvider.AddInlineAdornment(TextView, element, OnAdornmentLoaded);
                OnInlineAdornmentAdded();
                WriteLine(String.Empty);
                WriteLine(String.Empty);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Clear the current input region and move the caret to the right
        /// place for entering new text
        /// </summary>
        private void VsCancel() {
            // if there's an intellisense session we cancel that, otherwise we clear the current input.
            if (!((IIntellisenseCommandTarget)this.SessionStack).ExecuteKeyboardCommand(IntellisenseKeyboardCommand.Escape)) {
                Cancel();
            }
        }

        /// <summary>
        /// Sets the active input to the specified text w/o executing it.
        /// </summary>
        private void SetActiveInput(string text) {
            string newLine = _textViewHost.TextView.Options.GetNewLineCharacter();
            while (text.EndsWith(newLine)) {
                text = text.Substring(0, text.Length - newLine.Length);
            }

            ClearInput();

            var inputs = SplitLines(text);
            foreach (var line in inputs) {
                AppendInput(line.Text);
                if (line.HasNewline) {
                    int len = _currentLanguageBuffer.CurrentSnapshot.Length;
                    EnsureNewLine();

                    InsertSecondaryPrompt(len);
                }
            }

            //Caret.MoveTo(Math.Min(position, CurrentSnapshot.Length));
        }

        private void ClearInput() {
            bool isStdIn = false;
            for (int i = _spans.Count - 1; i >= 0; i--) {
                isStdIn = _spans[i].Kind == ReplSpanKind.StandardInputPrompt;
                if (_spans[i].Kind == ReplSpanKind.Prompt || isStdIn) {
                    
                    Debug.Assert(i != _spans.Count - 1);

                    _projectionBuffer.DeleteSpans(i + 1, _spans.Count - (i + 1));
                    _spans.RemoveRange(i + 1, _spans.Count - (i + 1));
                    break;
                } else if (_spans[i].Kind == ReplSpanKind.SecondaryPrompt) {
                    _secondaryPrompts.Add(_spans[i].Span);

                    // remove the read-only region
                    using (var readOnlyEdit = _promptBuffer.CreateReadOnlyRegionEdit()) {
                        readOnlyEdit.RemoveReadOnlyRegion(_spans[i].ReadOnlyRegion);
                        readOnlyEdit.Apply();
                    }
                }
            }

            if (!isStdIn) {
                _currentLanguageBuffer.Delete(new Span(0, _currentLanguageBuffer.CurrentSnapshot.Length));
                AddInitialLanguageSpan();

                CheckLanguageSpans();
            } else {
                Debug.Assert(_inputStart != null);
                _inputBuffer.Delete(Span.FromBounds(_inputStart.Value, _inputBuffer.CurrentSnapshot.Length));
                AddInputSpans();
            }
        }
        
        private T UIThread<T>(Func<T> func) {
            if (!CheckAccess()) {
                return (T)Dispatcher.Invoke(func);
            }
            return func();
        }

        private void UIThread(Action action) {
            if (!CheckAccess()) {
                Dispatcher.Invoke(action);
                return;
            }
            action();
        }

        private static void DoEvents() {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action<DispatcherFrame>(f => f.Continue = false),
                frame
                );
            Dispatcher.PushFrame(frame);
        }

        private static List<PendingInput> SplitLines(string text) {
            List<PendingInput> lines = new List<PendingInput>();
            int curStart = 0;
            for (int i = 0; i < text.Length; i++) {
                if (text[i] == '\r') {
                    if (i < text.Length - 1 && text[i + 1] == '\n') {
                        lines.Add(new PendingInput(text.Substring(curStart, i - curStart), true));
                        curStart = i + 2;
                        i++; // skip \n
                    } else {
                        lines.Add(new PendingInput(text.Substring(curStart, i - curStart), true));
                        curStart = i + 1;
                    }
                } else if (text[i] == '\n') {
                    lines.Add(new PendingInput(text.Substring(curStart, i - curStart), true));
                    curStart = i + 1;
                }
            }
            if (curStart < text.Length) {
                lines.Add(new PendingInput(text.Substring(curStart, text.Length - curStart), false));
            }

            return lines;
        }

        private IEditorOperations GetEditorOperations(IWpfTextView textView) {
            IEditorOperationsFactoryService factory = ComponentModel.GetService<IEditorOperationsFactoryService>();
            return factory.GetEditorOperations(textView);
        }

        #endregion

        #region Scopes

        private void InitializeScopeList() {
            IMultipleScopeEvaluator multiScopeEval = _evaluator as IMultipleScopeEvaluator;
            if (multiScopeEval != null) {
                _scopeListVisible = IsMultiScopeEnabled();
                UpdateScopeList(this, EventArgs.Empty);
                multiScopeEval.AvailableScopesChanged += UpdateScopeList;
                multiScopeEval.MultipleScopeSupportChanged += MultipleScopeSupportChanged;
            }
        }

        internal void SetCurrentScope(string newItem) {
            StoreUncommittedInput();
            WriteLine(String.Format("Current scope changed to {0}", newItem));
            ((IMultipleScopeEvaluator)_evaluator).SetScope(newItem);
            InsertUncommittedInput();
        }

        private void UpdateScopeList(object sender, EventArgs e) {
            if (!CheckAccess()) {
                Dispatcher.BeginInvoke(new Action(() => UpdateScopeList(sender, e)));
                return;
            }

            _currentScopes = ((IMultipleScopeEvaluator)_evaluator).GetAvailableScopes().ToArray();
        }

        private bool IsMultiScopeEnabled() {
            var multiScope = Evaluator as IMultipleScopeEvaluator;
            return multiScope != null && multiScope.EnableMultipleScopes;
        }

        private void MultipleScopeSupportChanged(object sender, EventArgs e) {
            _scopeListVisible = IsMultiScopeEnabled();
        }

        /// <summary>
        /// Handles getting or setting the current value of the combo box.
        /// </summary>
        private void ScopeComboBoxHandler(IntPtr newValue, IntPtr outCurrentValue) {
            // getting the current value
            if (outCurrentValue != IntPtr.Zero) {
                Marshal.GetNativeVariantForObject(((IMultipleScopeEvaluator)Evaluator).CurrentScopeName, outCurrentValue);
            }

            // setting the current value
            if (newValue != IntPtr.Zero) {
                SetCurrentScope((string)Marshal.GetObjectForNativeVariant(newValue));
            }
        }

        /// <summary>
        /// Gets the list of scopes that should be available in the combo box.
        /// </summary>
        private void ScopeComboBoxGetList(IntPtr outList) {
            Debug.Assert(outList != IntPtr.Zero);
            Marshal.GetNativeVariantForObject(_currentScopes, outList);
        }

        #endregion

        private sealed class CommandFilter : IOleCommandTarget {
            private readonly ReplWindow _replWindow;
            private readonly bool _preLanguage;

            public CommandFilter(ReplWindow vsReplWindow, bool preLanguage) {
                _replWindow = vsReplWindow;
                _preLanguage = preLanguage;
            }

            public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {
                if (_preLanguage) {
                    return _replWindow.PreLanguageCommandFilterQueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
                } else {
                    return _replWindow.PostLanguageCommandFilterQueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
                }
            }

            public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
                if (_preLanguage) {
                    return _replWindow.PreLanguageCommandFilterExec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                } else {
                    return _replWindow.PostLanguageCommandFilterExec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                }
            }
        }

        #region Window IOleCommandTarget

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {
            var nextTarget = _preLanguageCommandFilter;

            if (pguidCmdGroup == GuidList.guidReplWindowCmdSet) {
                switch (prgCmds[0].cmdID) {
                    case PkgCmdIDList.cmdidReplHistoryNext:
                    case PkgCmdIDList.cmdidReplHistoryPrevious:
                    case PkgCmdIDList.cmdidSmartExecute:
                    case PkgCmdIDList.cmdidBreakLine:
                        prgCmds[0].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_DEFHIDEONCTXTMENU);
                        return VSConstants.S_OK;

                    case PkgCmdIDList.comboIdReplScopes:
                        if (_scopeListVisible) {
                            prgCmds[0].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_DEFHIDEONCTXTMENU);
                        } else {
                            prgCmds[0].cmdf = (uint)(OLECMDF.OLECMDF_INVISIBLE | OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_DEFHIDEONCTXTMENU);
                        }
                        return VSConstants.S_OK;
                }
            }

            return nextTarget.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            var nextTarget = _preLanguageCommandFilter;
            
            if (pguidCmdGroup == GuidList.guidReplWindowCmdSet) {
                switch (nCmdID) {
                    case PkgCmdIDList.cmdidBreakRepl:
                        AbortCommand();
                        return VSConstants.S_OK;

                    case PkgCmdIDList.cmdidResetRepl:
                        Reset();
                        return VSConstants.S_OK;

                    case PkgCmdIDList.cmdidSmartExecute:
                        SmartExecute();
                        return VSConstants.S_OK;

                    case PkgCmdIDList.cmdidReplHistoryNext: 
                        HistoryNext(); 
                        return VSConstants.S_OK;

                    case PkgCmdIDList.cmdidReplHistoryPrevious: 
                        HistoryPrevious(); 
                        return VSConstants.S_OK;

                    case PkgCmdIDList.cmdidReplClearScreen: 
                        ClearScreen(); 
                        PrepareForInput();
                        ApplyProtection();
                        return VSConstants.S_OK;

                    case PkgCmdIDList.cmdidBreakLine: 
                        ExecuteText(); 
                        return VSConstants.S_OK;

                    case PkgCmdIDList.comboIdReplScopes:
                        ScopeComboBoxHandler(pvaIn, pvaOut);
                        return VSConstants.S_OK;

                    case PkgCmdIDList.comboIdReplScopesGetList:
                        ScopeComboBoxGetList(pvaOut);
                        return VSConstants.S_OK;
                }
            }

            return nextTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);            
        }

        #endregion

        #region Pre-langauge service IOleCommandTarget

        private int PreLanguageCommandFilterQueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {
            return _languageServiceCommandFilter.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        private int PreLanguageCommandFilterExec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            var nextTarget = _languageServiceCommandFilter;

            if (pguidCmdGroup == VSConstants.VSStd2K) {
                switch ((VSConstants.VSStd2KCmdID)nCmdID) {
                    case VSConstants.VSStd2KCmdID.RETURN:
                        int res = VSConstants.S_OK;
                        if (_inputStart != null) {
                            var enterPoint = TextView.BufferGraph.MapDownToBuffer(
                                TextView.Caret.Position.BufferPosition,
                                PointTrackingMode.Positive,
                                _inputBuffer,
                                PositionAffinity.Successor
                            );

                            if (enterPoint != null && enterPoint.Value.Position >= _inputStart.Value) {
                                using (var edit = _inputBuffer.CreateEdit()) {
                                    edit.Insert(edit.Snapshot.Length, _textViewHost.TextView.Options.GetNewLineCharacter());
                                    edit.Apply();
                                }
                                _inputValue = _inputBuffer.CurrentSnapshot.GetText(_inputStart.Value, _inputBuffer.CurrentSnapshot.Length - _inputStart.Value);
                                _inputEvent.Set();
                            }
                            return res;
                        } else {
                            var enterPoint = TextView.BufferGraph.MapDownToBuffer(
                                TextView.Caret.Position.BufferPosition,
                                PointTrackingMode.Positive,
                                _currentLanguageBuffer,
                                PositionAffinity.Successor
                            );

                            if (enterPoint != null) {
                                var caretLine = TextView.Caret.Position.BufferPosition.GetContainingLine();

                                res = nextTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);

                                if (caretLine.LineNumber < TextView.Caret.Position.BufferPosition.GetContainingLine().LineNumber) {
                                    // if we appended a new line, try and execute the text


                                    if (TryExecuteInput()) {
                                        Caret.EnsureVisible();

                                        return res;
                                    }
                                    Caret.EnsureVisible();

                                    InsertSecondaryPrompt(enterPoint.Value.Position);
                                }

                                return res;
                            }
                        }
                        break;

                    case VSConstants.VSStd2KCmdID.BACKSPACE:
                        if (!TextView.Selection.IsEmpty) {
                            if (CutOrDelete(false)) {
                                return VSConstants.S_OK;
                            }
                        } else if (TryRemovePromptForBackspace()) {
                            res = nextTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                            CheckLanguageSpans();
                            return res;
                        }
                        break;

                    case VSConstants.VSStd2KCmdID.CANCEL: 
                        VsCancel();
                        return VSConstants.S_OK;

                    case VSConstants.VSStd2KCmdID.UP:
                        SmartUpArrow();
                        return VSConstants.S_OK;

                    case VSConstants.VSStd2KCmdID.DOWN:
                        SmartDownArrow();
                        return VSConstants.S_OK;

                    case VSConstants.VSStd2KCmdID.BOL:
                        Home(false);
                        return VSConstants.S_OK;

                    case VSConstants.VSStd2KCmdID.BOL_EXT:
                        Home(true);
                        return VSConstants.S_OK;

                    case VSConstants.VSStd2KCmdID.SHOWCONTEXTMENU:
                        ShowContextMenu();
                        return VSConstants.S_OK;

                    case VSConstants.VSStd2KCmdID.TYPECHAR:
                        char typedChar = (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
                        if (!CaretInCodeInputRegion && !CaretInStandardInputRegion) {
                            EditorOperations.MoveToEndOfDocument(false);
                        }
                            
                        if (!TextView.Selection.IsEmpty) {
                            // delete selected text first
                            CutOrDelete(false);
                        }
                        break;
                }
            } else if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97) {
                switch ((VSConstants.VSStd97CmdID)nCmdID) {
                    case VSConstants.VSStd97CmdID.Paste:
                        if (!TextView.Selection.IsEmpty) {
                            CutOrDelete(false);
                        }
                        PasteClipboard();
                        return VSConstants.S_OK;

                    case VSConstants.VSStd97CmdID.Cut:
                        if (!TextView.Selection.IsEmpty && CutOrDelete(true)) {
                            return VSConstants.S_OK;
                        }
                        break;

                    case VSConstants.VSStd97CmdID.Delete:
                        if (TextView.Selection.IsEmpty) {
                            var caretLine = TextView.Caret.ContainingTextViewLine;
                            var pos = TextView.Caret.Position.BufferPosition;

                            var langLoc = TextView.BufferGraph.MapDownToBuffer(
                                pos,
                                PointTrackingMode.Positive,
                                _currentLanguageBuffer,
                                PositionAffinity.Predecessor
                            );

                            // langLoc is null if the user deletes in the output, prompt, or old language buffers
                            if (langLoc != null && langLoc.Value != _currentLanguageBuffer.CurrentSnapshot.Length) {
                                // get the next character in outer buffer
                                var nextLangLinePoint = TextView.BufferGraph.MapUpToBuffer(
                                    langLoc.Value + caretLine.LineBreakLength + 1,
                                    PointTrackingMode.Positive,
                                    PositionAffinity.Predecessor,
                                    _projectionBuffer
                                ) - 1;

                                // we should always map back up into the buffer
                                Debug.Assert(nextLangLinePoint != null);

                                if (caretLine.End == pos &&
                                    TryRemovePromptForBackspace(nextLangLinePoint.Value)) {
                                    _currentLanguageBuffer.Delete(new Span(langLoc.Value, caretLine.LineBreakLength));
                                    return VSConstants.S_OK;
                                }
                            }
                        } else if (CutOrDelete(false)) {
                            return VSConstants.S_OK;
                        }
                        break;
                }
            }

            return nextTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        #endregion

        #region Post-language service IOleCommandTarget

        private int PostLanguageCommandFilterQueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {
            return _editorCommandFilter.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        private int PostLanguageCommandFilterExec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            var nextTarget = _editorCommandFilter;

            // TODO:
            // if (pguidCmdGroup == VSConstants.VSStd2K) {
            //     switch ((VSConstants.VSStd2KCmdID)nCmdID) {
            //         case VSConstants.VSStd2KCmdID.RETURN:
            //             // RETURN that is not handled by any language service is a "try submit" command
            // 
            //             var enterPoint = TextView.BufferGraph.MapDownToBuffer(
            //                 TextView.Caret.Position.BufferPosition,
            //                 PointTrackingMode.Positive,
            //                 _currentLanguageBuffer,
            //                 PositionAffinity.Successor
            //             );
            // 
            //             if (enterPoint != null) {
            //                 if (TryExecuteInput()) {
            //                     Caret.EnsureVisible();
            //                     return VSConstants.S_OK;
            //                 }
            //                 Caret.EnsureVisible();
            // 
            //                 InsertSecondaryPrompt(enterPoint.Value.Position);
            //                 // return VSConstants.S_OK;
            //             }
            //             break;
            //     }
            // }

            return nextTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        #endregion

        private bool CutOrDelete(bool isCut) {
            bool includesPrompts = false, includesCode = false;

            // do some preliminary checks on what we're intersecting with
            foreach (var span in TextView.Selection.SelectedSpans) {
                // check if we intersect w/ output or non-interactive buffers, these are read only and the cut/delete fails.
                var mapping = TextView.BufferGraph.MapDownToFirstMatch(span, SpanTrackingMode.EdgeInclusive,
                    x => (x.ContentType == _contentType && x.TextBuffer != _currentLanguageBuffer) || x.TextBuffer == _outputBuffer || x.TextBuffer == _inputBuffer 
                );
                if (mapping.Count > 0) {
                    return true;
                }

                // check if we intersect w/ prompts, if we don't then a normal cut/delete can handle this.
                mapping = TextView.BufferGraph.MapDownToBuffer(
                    span,
                    SpanTrackingMode.EdgeInclusive,
                    _promptBuffer
                );

                if (mapping.Count > 0) {
                    includesPrompts = true;
                }

                // check if we contain code (we don't allow just deleting prompts)
                mapping = TextView.BufferGraph.MapDownToFirstMatch(
                    span,
                    SpanTrackingMode.EdgeInclusive,
                    x => x.ContentType == _contentType
                );

                if (mapping.Count > 0) {
                    includesCode = true;
                }
            }

            if ((includesPrompts || TextView.Selection.Mode == TextSelectionMode.Box) && includesCode) {
                // remove the selected text.  To keep this simple we build up the new string which we'll 
                // have at the end first, and then we just clear the current input and insert the newly modified input.
                StringBuilder cutText = new StringBuilder();
                StringBuilder text = new StringBuilder(_currentLanguageBuffer.CurrentSnapshot.GetText());
                int deleted = 0;

                foreach (var span in TextView.Selection.SelectedSpans) {
                    foreach (var langSpan in TextView.BufferGraph.MapDownToBuffer(
                        span,
                        SpanTrackingMode.EdgeExclusive,
                        _currentLanguageBuffer)) {

                        text.Remove(langSpan.Start.Position - deleted, langSpan.Length);
                        deleted += langSpan.Length;
                        cutText.Append(langSpan.GetText());
                    }
                }

                ClearInput();
                SetActiveInput(text.ToString());

                if (isCut) {
                    // copy the data to the clipboard
                    var data = new DataObject();
                    if (TextView.Selection.Mode == TextSelectionMode.Box) {
                        data.SetData(_boxSelectionCutCopyTag, new object());
                    }
                    data.SetText(cutText.ToString());
                    Clipboard.SetDataObject(data, true);
                }

                return true;
            }
            return false;
        }

        private bool TryRemovePromptForBackspace() {
            return TryRemovePromptForBackspace(TextView.Caret.Position.BufferPosition.Position);
        }

        /// <summary>
        /// When there's a secondary prompt and the user hits backspace right before it we need to clear the secondary prompt
        /// and merge together the two existing language spaces.
        /// </summary>
        private bool TryRemovePromptForBackspace(int caretPos) {
            int length = 0;

            // we walk the spans here isntead of using Buffergraph.MapDownTo because that won't map down to
            // a zero length span (which we'll have if the secondary prompt is empty - we keep zero-length prompts
            // in place in case the prompts change).
            for (int i = 0; i < _spans.Count && length <= caretPos; i++) {
                var curSpan = _spans[i];
                switch(curSpan.Kind) {
                    case ReplSpanKind.Language:
                    case ReplSpanKind.Output:
                    case ReplSpanKind.Prompt:
                    case ReplSpanKind.StandardInputPrompt:
                        length += curSpan.Span.GetSpan(curSpan.Span.TextBuffer.CurrentSnapshot).Length;
                        break;
                    case ReplSpanKind.SecondaryPrompt:
                        length += curSpan.Span.GetSpan(_promptBuffer.CurrentSnapshot).Length;
                        if (length == caretPos) {
                            // we are deleting a secondary prompt, remove the protection and then the prompt, and
                            // condense any existing code onto the previous line
                            using (var readOnlyEdit = _promptBuffer.CreateReadOnlyRegionEdit()) {
                                readOnlyEdit.RemoveReadOnlyRegion(curSpan.ReadOnlyRegion);
                                readOnlyEdit.Apply();
                            }

                            // recycle the secondary prompt
                            _secondaryPrompts.Add(curSpan.Span);

                            Debug.Assert(i < _spans.Count - 1);  // we should have at least one language span after us
                            var oldLangSpan = _spans[i + 1];

                            // remove the spans
                            _projectionBuffer.DeleteSpans(i, 2);
                            _spans.RemoveRange(i, 2);

                            // combine the two adjecent language spans back together
                            Debug.Assert(i > 0);
                            Debug.Assert(_spans[i - 1].Kind == ReplSpanKind.Language);

                            var span = Span.FromBounds(
                                _spans[i - 1].Span.GetStartPoint(_currentLanguageBuffer.CurrentSnapshot),
                                oldLangSpan.Span.GetEndPoint(_currentLanguageBuffer.CurrentSnapshot)
                            );
                            ITrackingSpan trackingSpan = null;
                            if (i == _spans.Count) {
                                trackingSpan = CreateLanguageTrackingSpan(span);
                            } else {
                                trackingSpan = CreateOldLanguageTrackingSpan(span);
                            }

                            _spans[i - 1] = new ReplSpan(trackingSpan, ReplSpanKind.Language);

                            _projectionBuffer.ReplaceSpans(i - 1, 1, new[] { _spans[i - 1].Span }, EditOptions.None, null);

                            // move the caret to the beginning of the current line so the delete deletes the new line
                            var lineStart = TextView.BufferGraph.MapUpToBuffer(
                                oldLangSpan.Span.GetStartPoint(_currentLanguageBuffer.CurrentSnapshot),
                                PointTrackingMode.Positive,
                                PositionAffinity.Successor,
                                _projectionBuffer
                            );
                            if (lineStart != null) {
                                Caret.MoveTo(lineStart.Value);
                            }

                            return true;
                        }
                        break;
                }
            }
            return false;
        }

        /// <summary>
        /// Inserts the secondary prompt into the buffer.  position is where we want to logically insert
        /// the prompt within the language buffer.
        /// </summary>
        /// <param name="position"></param>
        private void InsertSecondaryPrompt(int position) {
            // save the location for the margin prompts
            var pt = TextView.BufferGraph.MapUpToBuffer(
                new SnapshotPoint(_currentLanguageBuffer.CurrentSnapshot, position),
                PointTrackingMode.Positive,
                PositionAffinity.Successor,
                _projectionBuffer
            );
            

            // insert the secondary prompt
            
            // {} marks a output span, [] marks a language span, we have:
            // {>>> }[text entered]
            // [auto-indent]
            //
            // we want to change to:
            // {>>> }[lang span]
            // {... }[auto-indent]
            //
            // The prompt may be empty but we'll still enter it so we can later update spans
            // if the prompt changes.

            // first, insert the secondary prompt into the output buffer
            var prevLine = _currentLanguageBuffer.CurrentSnapshot.GetLineFromPosition(position);
            var oldLangSpan = prevLine.Extent;

            // update the spans in the projection buffer
            ITrackingSpan secondPromptSpan;
            if (_secondaryPrompts.Count > 0) {
                // reuse a secondary prompt which we've discarded due to scrolling through history / clearing the current input
                // (this way the output buffer doesn't continue to grow due to using history)
                secondPromptSpan = _secondaryPrompts[_secondaryPrompts.Count - 1];
                _secondaryPrompts.RemoveAt(_secondaryPrompts.Count - 1);
            } else {
                int oldPromptBufferLen = _promptBuffer.CurrentSnapshot.Length;

                string secondPrompt = FormatPrompt(_secondPrompt, _currentInput - 1);
                using (var edit = _promptBuffer.CreateEdit()) {
                    edit.Insert(oldPromptBufferLen, _displayPromptInMargin ? " " :  secondPrompt + " ");
                    edit.Apply();
                }

                int promptLen = (_displayPromptInMargin || String.IsNullOrEmpty(secondPrompt)) ? 
                    0 : 
                    _promptBuffer.CurrentSnapshot.Length - oldPromptBufferLen - 1;
                
                secondPromptSpan = _promptBuffer.CurrentSnapshot.CreateTrackingSpan(
                    new Span(oldPromptBufferLen, promptLen),
                    SpanTrackingMode.EdgeExclusive
                );
            }

            var newLine = _currentLanguageBuffer.CurrentSnapshot.GetLineFromLineNumber(prevLine.LineNumber + 1);
            var updatedLangSpan = CreateOldLanguageTrackingSpan(oldLangSpan);

            var newLangSpan = CreateLanguageTrackingSpan(Span.FromBounds(position, newLine.EndIncludingLineBreak.Position));

            var newLangText = newLangSpan.GetText(_currentLanguageBuffer.CurrentSnapshot);
            var newLineChar = _textViewHost.TextView.Options.GetNewLineCharacter();
            if (newLangText.StartsWith(newLineChar)) {
                // move new line into previous input
                updatedLangSpan = CreateOldLanguageTrackingSpan(
                    new Span(oldLangSpan.Start, oldLangSpan.Length + newLineChar.Length)
                );

                newLangSpan = CreateLanguageTrackingSpan(
                    Span.FromBounds(position + newLineChar.Length, newLine.EndIncludingLineBreak.Position)
                );
            }

            // replace the language span of the previous line and then insert the prompt
            // and new line
            int targetSpan = FirstLanguageSpanIndexForCurrentInput + prevLine.LineNumber * SpansPerLineOfInput;
            _projectionBuffer.ReplaceSpans(
                targetSpan,
                1,
                new object[] { updatedLangSpan, secondPromptSpan, newLangSpan },
                EditOptions.None,
                null
            );

            // mark the secondary prompt as read-only and hold onto the region so we can clear it later
            IReadOnlyRegion readOnlySecondPrompt;
            using (var readOnlyEdit = _promptBuffer.CreateReadOnlyRegionEdit()) {
                readOnlySecondPrompt = readOnlyEdit.CreateReadOnlyRegion(
                    secondPromptSpan.GetSpan(_promptBuffer.CurrentSnapshot)
                );
                readOnlyEdit.Apply();
            }

            // update our tracking state of the spans
            _spans[targetSpan] = new ReplSpan(updatedLangSpan, ReplSpanKind.Language);
            var promptSpan = new ReplSpan(secondPromptSpan, ReplSpanKind.SecondaryPrompt, readOnlySecondPrompt);
            _spans.Insert(targetSpan + 1, promptSpan);
            _spans.Insert(targetSpan + 2, new ReplSpan(newLangSpan, ReplSpanKind.Language));

            if (pt != null) {
                _prompts[pt.Value.GetContainingLine().LineNumber + 1] = promptSpan;
                var promptChanged = PromptChanged;
                if (promptChanged != null) {
                    promptChanged(new SnapshotSpan(CurrentSnapshot, new Span(CurrentSnapshot.Length, 0)));
                }
            }

            CheckLanguageSpans();
        }

        private string FormatPrompt(string prompt, int currentInput) {
            if (!_formattedPrompts) {
                return prompt;
            }

            StringBuilder res = null;
            for (int i = 0; i < prompt.Length; i++) {
                if (prompt[i] == '\\' && i < prompt.Length - 1) {
                    if (res == null) {
                        res = new StringBuilder(prompt, 0, i, prompt.Length);
                    }
                    switch (prompt[++i]) {
                        case '\\': res.Append('\\'); break;
                        case '#': res.Append(currentInput.ToString()); break;
                        case 'D': res.Append(DateTime.Today.ToString()); break;
                        case 'T': res.Append(DateTime.Now.ToString()); break;
                        default:
                            res.Append('\\');
                            res.Append(prompt[i + 1]);
                            break;
                    }
                    
                } else if (res != null) {
                    res.Append(prompt[i]);
                }
            }

            if (res != null) {
                return res.ToString();
            }
            return prompt;
        }

        /// <summary>
        /// Verifies that our language spans are following the pattern:
        /// {Prompt1}{Language Span}
        /// {Prompt2}{Language Span}
        /// {Prompt2}{Language Span}
        /// 
        /// This just checks the current input.
        /// </summary>
        [Conditional("DEBUG")]
        private void CheckLanguageSpans() {
            int firstLangSpan = FirstLanguageSpanIndexForCurrentInput - 2;
            Debug.Assert(_spans[firstLangSpan - 1].Kind == ReplSpanKind.Prompt);
            for (int i = firstLangSpan; i < _spans.Count; i += 2) {
                Debug.Assert(_spans[i].Kind == ReplSpanKind.Language);

                if (i < _spans.Count - 1) {
                    Debug.Assert(_spans[i + 1].Kind == ReplSpanKind.SecondaryPrompt);
                }
            }
        }

        /// <summary>
        /// The first language span for the current input (this is only valid while we're inserting new lines, otherwise it's
        /// off by 2)
        /// </summary>
        private int FirstLanguageSpanIndexForCurrentInput {
            get {
                return _spans.Count - (_currentLanguageBuffer.CurrentSnapshot.LineCount - 1) * 2 + 1;
            }
        }

        private const int SpansPerLineOfInput = 2;

        /// <summary>
        /// Creates the language span for the last line of the active input.  This span
        /// is effectively edge inclusive so it will grow as the user types at the end.
        /// </summary>
        private ITrackingSpan CreateLanguageTrackingSpan(Span span) {
            return new CustomTrackingSpan(
                _currentLanguageBuffer.CurrentSnapshot,
                span);
        }

        /// <summary>
        /// Creates the tracking span for a line previous in the input.  This span
        /// is negative tracking on the end so when the user types at the beginning of
        /// the next line we don't grow with the change.
        /// </summary>
        /// <param name="span"></param>
        /// <returns></returns>
        private ITrackingSpan CreateOldLanguageTrackingSpan(Span span) {
            return new CustomTrackingSpan(
                _currentLanguageBuffer.CurrentSnapshot,
                span,
                PointTrackingMode.Negative,
                PointTrackingMode.Negative
            );
        }

        #region VS Initialization

        protected override void OnCreate() {
            CreateTextViewHost();

            var textView = _textViewHost.TextView;

            _view = ComponentModel.GetService<IVsEditorAdaptersFactoryService>().GetViewAdapter(textView);
            _findTarget = _view as IVsFindTarget;

            _postLanguageCommandFilter = new CommandFilter(this, preLanguage: false);
            ErrorHandler.ThrowOnFailure(_view.AddCommandFilter(_postLanguageCommandFilter, out _editorCommandFilter));

            // may add command filters
            foreach (var listener in _creationListeners) {
                listener.ReplWindowCreated(this);
            }

            textView.Options.SetOptionValue(DefaultTextViewHostOptions.HorizontalScrollBarId, false);
            textView.Options.SetOptionValue(DefaultTextViewHostOptions.LineNumberMarginId, false);
            textView.Options.SetOptionValue(DefaultTextViewHostOptions.OutliningMarginId, false);
            textView.Options.SetOptionValue(DefaultTextViewOptions.WordWrapStyleId, WordWrapStyles.WordWrap);

            _editorOperations = GetEditorOperations(textView);

            _commands = CreateCommands();

            textView.TextBuffer.Properties.AddProperty(typeof(IReplEvaluator), _evaluator);

            // Anything that reads options should wait until after this call so the evaluator can set the options first
            Evaluator.Start(this);

            textView.Options.SetOptionValue(DefaultTextViewHostOptions.GlyphMarginId, _displayPromptInMargin);

            // the margin publishes itself in the properties upon creation:
            // textView.TextBuffer.Properties.TryGetProperty(typeof(ReplMargin), out _margin);

            //if (_evaluator.DisplayPromptInMargin) {
            //    _margin = _textViewHost.GetTextViewMargin(PredefinedMarginNames.Glyph);
            //}

            // may add command filters
            PrepareForInput();

            ApplyProtection();
            InitializeScopeList();
            SetDefaultFontSize(ComponentModel, textView);
        }

        public override void OnToolWindowCreated() {
            Guid commandUiGuid = Microsoft.VisualStudio.VSConstants.GUID_TextEditorFactory;
            ((IVsWindowFrame)Frame).SetGuidProperty((int)__VSFPROPID.VSFPROPID_InheritKeyBindings, ref commandUiGuid);

            _preLanguageCommandFilter = new CommandFilter(this, preLanguage: true);
            ErrorHandler.ThrowOnFailure(_view.AddCommandFilter(_preLanguageCommandFilter, out _languageServiceCommandFilter));

            base.OnToolWindowCreated();

            // add our toolbar which  is defined in our VSCT file
            var frame = (IVsWindowFrame)Frame;
            object otbh;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.GetProperty((int)__VSFPROPID.VSFPROPID_ToolbarHost, out otbh));
            IVsToolWindowToolbarHost tbh = otbh as IVsToolWindowToolbarHost;
            Guid guidPerfMenuGroup = GuidList.guidReplWindowCmdSet;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(tbh.AddToolbar(VSTWT_LOCATION.VSTWT_TOP, ref guidPerfMenuGroup, PkgCmdIDList.menuIdReplToolbar));
        }

        /// <summary>
        /// Sets the default font size to match that of a normal editor buffer.
        /// </summary>
        private void SetDefaultFontSize(IComponentModel model, IWpfTextView textView) {
            var formatMapSvc = model.GetService<IClassificationFormatMapService>();
            var fontsAndColorsSvc = model.GetService<IVsFontsAndColorsInformationService>();
            var fontCat = new VisualStudio.Editor.FontsAndColorsCategory(
                    _langSvcGuid,
                    Microsoft.VisualStudio.Editor.DefGuidList.guidTextEditorFontCategory,
                    Microsoft.VisualStudio.Editor.DefGuidList.guidTextEditorFontCategory
                    );

            var fontInfo = fontsAndColorsSvc.GetFontAndColorInformation(fontCat);
            var fontPrefs = fontInfo.GetFontAndColorPreferences();
            var font = System.Drawing.Font.FromHfont(fontPrefs.hRegularViewFont);

            var classMap = formatMapSvc.GetClassificationFormatMap(textView);
            var defaultProps = classMap.DefaultTextProperties;
            defaultProps = defaultProps.SetFontRenderingEmSize(font.Size);
            classMap.DefaultTextProperties = defaultProps;
        }

        public void ShowContextMenu() {
            var uishell = (IVsUIShell)GetService(typeof(SVsUIShell));
            if (uishell != null) {
                var pt = System.Windows.Forms.Cursor.Position;
                var pnts = new[] { new POINTS { x = (short)pt.X, y = (short)pt.Y } };
                var guid = GuidList.guidReplWindowCmdSet;
                int hr = uishell.ShowContextMenu(
                    0,
                    ref guid,
                    0x2100,
                    pnts,
                    TextView as IOleCommandTarget);

                ErrorHandler.ThrowOnFailure(hr);
            }
        }

        #endregion

        #region Overridden Methods

        /// <summary>
        /// This property returns the control that should be hosted in the Tool Window.
        /// It can be either a FrameworkElement (for easy creation of toolwindows hosting WPF content), 
        /// or it can be an object implementing one of the IVsUIWPFElement or IVsUIWin32Element interfaces.
        /// </summary>
        public override object Content {
            get {
                Debug.Assert(_textViewHost != null);
                return _textViewHost;
            }
            set { }
        }

        #endregion

        #region IVsFindTarget Members

        public int Find(string pszSearch, uint grfOptions, int fResetStartPoint, IVsFindHelper pHelper, out uint pResult) {
            if (_findTarget != null) {
                return _findTarget.Find(pszSearch, grfOptions, fResetStartPoint, pHelper, out pResult);
            }
            pResult = 0;
            return VSConstants.E_NOTIMPL;
        }

        public int GetCapabilities(bool[] pfImage, uint[] pgrfOptions) {
            if (_findTarget != null) {
                return _findTarget.GetCapabilities(pfImage, pgrfOptions);
            }
            return VSConstants.E_NOTIMPL;
        }

        public int GetCurrentSpan(TextSpan[] pts) {
            if (_findTarget != null) {
                return _findTarget.GetCurrentSpan(pts);
            }
            return VSConstants.E_NOTIMPL;
        }

        public int GetFindState(out object ppunk) {
            if (_findTarget != null) {
                return _findTarget.GetFindState(out ppunk);
            }
            ppunk = null;
            return VSConstants.E_NOTIMPL;

        }

        public int GetMatchRect(RECT[] prc) {
            if (_findTarget != null) {
                return _findTarget.GetMatchRect(prc);
            }
            return VSConstants.E_NOTIMPL;
        }

        public int GetProperty(uint propid, out object pvar) {
            if (_findTarget != null) {
                return _findTarget.GetProperty(propid, out pvar);
            }
            pvar = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetSearchImage(uint grfOptions, IVsTextSpanSet[] ppSpans, out IVsTextImage ppTextImage) {
            if (_findTarget != null) {
                return _findTarget.GetSearchImage(grfOptions, ppSpans, out ppTextImage);
            }
            ppTextImage = null;
            return VSConstants.E_NOTIMPL;
        }

        public int MarkSpan(TextSpan[] pts) {
            if (_findTarget != null) {
                return _findTarget.MarkSpan(pts);
            }
            return VSConstants.E_NOTIMPL;
        }

        public int NavigateTo(TextSpan[] pts) {
            if (_findTarget != null) {
                return _findTarget.NavigateTo(pts);
            }
            return VSConstants.E_NOTIMPL;
        }

        public int NotifyFindTarget(uint notification) {
            if (_findTarget != null) {
                return _findTarget.NotifyFindTarget(notification);
            }
            return VSConstants.E_NOTIMPL;
        }

        public int Replace(string pszSearch, string pszReplace, uint grfOptions, int fResetStartPoint, IVsFindHelper pHelper, out int pfReplaced) {
            if (_findTarget != null) {
                return _findTarget.Replace(pszSearch, pszReplace, grfOptions, fResetStartPoint, pHelper, out pfReplaced);
            }
            pfReplaced = 0;
            return VSConstants.E_NOTIMPL;
        }

        public int SetFindState(object pUnk) {
            if (_findTarget != null) {
                return _findTarget.SetFindState(pUnk);
            }
            return VSConstants.E_NOTIMPL;
        }

        #endregion

        /// <summary>
        /// See IReplWindow
        /// </summary>
        private ITextCaret Caret {
            get {
                return TextView.Caret;
            }
        }

        private struct PendingInput {
            public readonly string Text;
            public readonly bool HasNewline;

            public PendingInput(string text, bool hasNewline) {
                Text = text;
                HasNewline = hasNewline;
            }
        }
    }
}

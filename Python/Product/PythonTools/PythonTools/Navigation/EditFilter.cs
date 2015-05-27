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
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Editor.Core;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Navigation;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Refactoring;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
#if DEV14_OR_LATER
using Microsoft.VisualStudio.InteractiveWindow;
#else
using Microsoft.VisualStudio.Repl;
#endif
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Navigation;

namespace Microsoft.PythonTools.Language {
    using IServiceProvider = System.IServiceProvider;    
#if DEV14_OR_LATER
    using IReplWindow = IInteractiveWindow;
#endif

    /// <summary>
    /// IOleCommandTarget implementation for interacting with various editor commands.  This enables
    /// wiring up most of our features to the VisualStudio editor.  We currently support:
    ///     Goto Definition
    ///     Find All References
    ///     Show Member List
    ///     Complete Word
    ///     Enable/Disable Outlining
    ///     Comment/Uncomment block
    /// 
    /// We also support Python specific commands via this class.  Currently these commands are
    /// added by updating our CommandTable class to contain a new command.  These commands also need
    /// to be registered in our .vsct file so that VS knows about them.
    /// </summary>
    internal sealed class EditFilter : IOleCommandTarget {
        private readonly ITextView _textView;
        private readonly IEditorOperations _editorOps;
        private readonly IServiceProvider _serviceProvider;
        private readonly IComponentModel _componentModel;
        private readonly PythonToolsService _pyService;
        internal IOleCommandTarget _next;
#if DEV14_OR_LATER
        //
        // A list of scopes if this REPL is multi-scoped
        // 
        private string[] _currentScopes;
        private bool _scopeListVisible;
#endif

        public EditFilter(ITextView textView, IEditorOperations editorOps, IServiceProvider serviceProvider) {
            _textView = textView;
            _textView.Properties[typeof(EditFilter)] = this;
            _editorOps = editorOps;
            _serviceProvider = serviceProvider;
            _componentModel = _serviceProvider.GetComponentModel();
            _pyService = _serviceProvider.GetPythonToolsService();

            BraceMatcher.WatchBraceHighlights(textView, _componentModel);
#if DEV14_OR_LATER
            InitializeScopeList();
#endif
        }

        internal void AttachKeyboardFilter(IVsTextView vsTextView) {
            if (_next == null) {
                ErrorHandler.ThrowOnFailure(vsTextView.AddCommandFilter(this, out _next));
            }
        }

        /// <summary>
        /// Implements Goto Definition.  Called when the user selects Goto Definition from the 
        /// context menu or hits the hotkey associated with Goto Definition.
        /// 
        /// If there is 1 and only one definition immediately navigates to it.  If there are
        /// no references displays a dialog box to the user.  Otherwise it opens the find
        /// symbols dialog with the list of results.
        /// </summary>
        private int GotoDefinition() {
            UpdateStatusForIncompleteAnalysis();

            var analysis = _textView.GetExpressionAnalysis(_serviceProvider);

            Dictionary<LocationInfo, SimpleLocationInfo> references, definitions, values;
            GetDefsRefsAndValues(_serviceProvider, analysis, out definitions, out references, out values);

            if ((values.Count + definitions.Count) == 1) {
                if (values.Count != 0) {
                    foreach (var location in values.Keys) {
                        GotoLocation(location);
                        break;
                    }
                } else {
                    foreach (var location in definitions.Keys) {
                        GotoLocation(location);
                        break;
                    }
                }
            } else if (values.Count + definitions.Count == 0) {
                if (String.IsNullOrWhiteSpace(analysis.Expression)) {
                    MessageBox.Show(String.Format("Cannot go to definition.  The cursor is not on a symbol."), "Python Tools for Visual Studio");
                } else {
                    MessageBox.Show(String.Format("Cannot go to definition \"{0}\"", analysis.Expression), "Python Tools for Visual Studio");
                }
            } else if (definitions.Count == 0) {
                ShowFindSymbolsDialog(analysis, new SymbolList("Values", StandardGlyphGroup.GlyphForwardType, values.Values));
            } else if (values.Count == 0) {
                ShowFindSymbolsDialog(analysis, new SymbolList("Definitions", StandardGlyphGroup.GlyphLibrary, definitions.Values));
            } else {
                ShowFindSymbolsDialog(analysis,
                    new LocationCategory("Goto Definition", 
                        new SymbolList("Definitions", StandardGlyphGroup.GlyphLibrary, definitions.Values),
                        new SymbolList("Values", StandardGlyphGroup.GlyphForwardType, values.Values)
                    )
                );
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Moves the caret to the specified location, staying in the current text view 
        /// if possible.
        /// 
        /// https://pytools.codeplex.com/workitem/1649
        /// </summary>
        private void GotoLocation(LocationInfo location) {
            Debug.Assert(location != null);
            Debug.Assert(location.Line > 0);
            Debug.Assert(location.Column > 0);

            if (CommonUtils.IsSamePath(location.FilePath, _textView.GetFilePath())) {
                var viewAdapter = GetViewAdapter();
                viewAdapter.SetCaretPos(location.Line - 1, location.Column - 1);
                viewAdapter.CenterLines(location.Line - 1, 1);
            } else {
                location.GotoSource(_serviceProvider);
            }
        }

        private IVsTextView GetViewAdapter() {
            var adapterFactory = _componentModel.GetService<IVsEditorAdaptersFactoryService>();
            var viewAdapter = adapterFactory.GetViewAdapter(_textView);
            return viewAdapter;
        }

        /// <summary>
        /// Implements Find All References.  Called when the user selects Find All References from
        /// the context menu or hits the hotkey associated with find all references.
        /// 
        /// Always opens the Find Symbol Results box to display the results.
        /// </summary>
        private int FindAllReferences() {
            UpdateStatusForIncompleteAnalysis();

            var analysis = _textView.GetExpressionAnalysis(_serviceProvider);

            var locations = GetFindRefLocations(_serviceProvider, analysis);

            ShowFindSymbolsDialog(analysis, locations);

            return VSConstants.S_OK;
        }

        internal static LocationCategory GetFindRefLocations(IServiceProvider serviceProvider, ExpressionAnalysis analysis) {
            Dictionary<LocationInfo, SimpleLocationInfo> references, definitions, values;
            GetDefsRefsAndValues(serviceProvider, analysis, out definitions, out references, out values);

            var locations = new LocationCategory("Find All References",
                    new SymbolList("Definitions", StandardGlyphGroup.GlyphLibrary, definitions.Values),
                    new SymbolList("Values", StandardGlyphGroup.GlyphForwardType, values.Values),
                    new SymbolList("References", StandardGlyphGroup.GlyphReference, references.Values)
                );
            return locations;
        }

        private static void GetDefsRefsAndValues(IServiceProvider serviceProvider, ExpressionAnalysis provider, out Dictionary<LocationInfo, SimpleLocationInfo> definitions, out Dictionary<LocationInfo, SimpleLocationInfo> references, out Dictionary<LocationInfo, SimpleLocationInfo> values) {
            references = new Dictionary<LocationInfo, SimpleLocationInfo>();
            definitions = new Dictionary<LocationInfo, SimpleLocationInfo>();
            values = new Dictionary<LocationInfo,SimpleLocationInfo>();

            foreach (var v in provider.Variables) {
                if (v.Location.FilePath == null) {
                    // ignore references in the REPL
                    continue;
                }

                switch (v.Type) {
                    case VariableType.Definition:
                        values.Remove(v.Location);
                        definitions[v.Location] = new SimpleLocationInfo(serviceProvider, provider.Expression, v.Location, StandardGlyphGroup.GlyphGroupField);
                        break;
                    case VariableType.Reference:
                        references[v.Location] = new SimpleLocationInfo(serviceProvider, provider.Expression, v.Location, StandardGlyphGroup.GlyphGroupField);
                        break;
                    case VariableType.Value:
                        if (!definitions.ContainsKey(v.Location)) {
                            values[v.Location] = new SimpleLocationInfo(serviceProvider, provider.Expression, v.Location, StandardGlyphGroup.GlyphGroupField);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Opens the find symbols dialog with a list of results.  This is done by requesting
        /// that VS does a search against our library GUID.  Our library then responds to
        /// that request by extracting the prvoided symbol list out and using that for the
        /// search results.
        /// </summary>
        private void ShowFindSymbolsDialog(ExpressionAnalysis provider, IVsNavInfo symbols) {
            // ensure our library is loaded so find all references will go to our library
            _serviceProvider.GetService(typeof(IPythonLibraryManager));

            if (!string.IsNullOrEmpty(provider.Expression)) {
                var findSym = (IVsFindSymbol)_serviceProvider.GetService(typeof(SVsObjectSearch));
                VSOBSEARCHCRITERIA2 searchCriteria = new VSOBSEARCHCRITERIA2();
                searchCriteria.eSrchType = VSOBSEARCHTYPE.SO_ENTIREWORD;
                searchCriteria.pIVsNavInfo = symbols;
                searchCriteria.grfOptions = (uint)_VSOBSEARCHOPTIONS2.VSOBSO_LISTREFERENCES;
                searchCriteria.szName = provider.Expression;

                Guid guid = Guid.Empty;
                //  new Guid("{a5a527ea-cf0a-4abf-b501-eafe6b3ba5c6}")
                ErrorHandler.ThrowOnFailure(findSym.DoSearch(new Guid(CommonConstants.LibraryGuid), new VSOBSEARCHCRITERIA2[] { searchCriteria }));
            } else {
                var statusBar = (IVsStatusbar)_serviceProvider.GetService(typeof(SVsStatusbar));
                statusBar.SetText("The caret must be on valid expression to find all references.");
            }
        }

        internal class LocationCategory : SimpleObjectList<SymbolList>, IVsNavInfo, ICustomSearchListProvider {
            private readonly string _name;

            internal LocationCategory(string name, params SymbolList[] locations) {
                _name = name;

                foreach (var location in locations) {
                    if (location.Children.Count > 0) {
                        Children.Add(location);
                    }
                }
            }
           
            public override uint CategoryField(LIB_CATEGORY lIB_CATEGORY) {
                return (uint)(_LIB_LISTTYPE.LLT_HIERARCHY | _LIB_LISTTYPE.LLT_MEMBERS | _LIB_LISTTYPE.LLT_PACKAGE);
            }

            #region IVsNavInfo Members

            public int EnumCanonicalNodes(out IVsEnumNavInfoNodes ppEnum) {
                ppEnum = new NodeEnumerator<SymbolList>(Children);
                return VSConstants.S_OK;
            }

            public int EnumPresentationNodes(uint dwFlags, out IVsEnumNavInfoNodes ppEnum) {
                ppEnum = new NodeEnumerator<SymbolList>(Children);
                return VSConstants.S_OK;
            }

            public int GetLibGuid(out Guid pGuid) {
                pGuid = Guid.Empty;
                return VSConstants.S_OK;
            }

            public int GetSymbolType(out uint pdwType) {
                pdwType = (uint)_LIB_LISTTYPE2.LLT_MEMBERHIERARCHY;
                return VSConstants.S_OK;
            }

            #endregion

            #region ICustomSearchListProvider Members

            public IVsSimpleObjectList2 GetSearchList() {
                return this;
            }

            #endregion
        }

        internal class SimpleLocationInfo : SimpleObject, IVsNavInfoNode {
            private readonly LocationInfo _locationInfo;
            private readonly StandardGlyphGroup _glyphType;
            private readonly string _pathText, _lineText;
            private readonly IServiceProvider _serviceProvider;

            public SimpleLocationInfo(IServiceProvider serviceProvider, string searchText, LocationInfo locInfo, StandardGlyphGroup glyphType) {
                _serviceProvider = serviceProvider;
                _locationInfo = locInfo;
                _glyphType = glyphType;
                _pathText = GetSearchDisplayText();
                _lineText = _locationInfo.ProjectEntry.GetLine(_locationInfo.Line);
            }

            public override string Name {
                get {
                    return _locationInfo.FilePath;
                }
            }

            public override string GetTextRepresentation(VSTREETEXTOPTIONS options) {
                if (options == VSTREETEXTOPTIONS.TTO_DEFAULT) {
                    return _pathText + _lineText.Trim();
                }
                return String.Empty;
            }

            private string GetSearchDisplayText() {
                return String.Format("{0} - ({1}, {2}): ",
                    _locationInfo.FilePath,
                    _locationInfo.Line,
                    _locationInfo.Column);
            }

            public override string UniqueName {
                get {
                    return _locationInfo.FilePath;
                }
            }

            public override bool CanGoToSource {
                get {
                    return true;
                }
            }

            public override VSTREEDISPLAYDATA DisplayData {
                get {
                    var res = new VSTREEDISPLAYDATA();
                    res.Image = res.SelectedImage = (ushort)_glyphType;
                    res.State = (uint)_VSTREEDISPLAYSTATE.TDS_FORCESELECT;

                    // This code highlights the text but it gets the wrong region.  This should be re-enabled
                    // and highlight the correct region.

                    //res.ForceSelectStart = (ushort)(_pathText.Length + _locationInfo.Column - 1);
                    //res.ForceSelectLength = (ushort)_locationInfo.Length;
                    return res;
                }
            }

            public override void GotoSource(VSOBJGOTOSRCTYPE SrcType) {
                _locationInfo.GotoSource(_serviceProvider);
            }

            #region IVsNavInfoNode Members

            public int get_Name(out string pbstrName) {
                pbstrName = _locationInfo.FilePath;
                return VSConstants.S_OK;
            }

            public int get_Type(out uint pllt) {
                pllt = 16; // (uint)_LIB_LISTTYPE2.LLT_MEMBERHIERARCHY;
                return VSConstants.S_OK;
            }

            #endregion
        }

        internal class SymbolList : SimpleObjectList<SimpleLocationInfo>, IVsNavInfo, IVsNavInfoNode, ICustomSearchListProvider, ISimpleObject {
            private readonly string _name;
            private readonly StandardGlyphGroup _glyphGroup;

            internal SymbolList(string description, StandardGlyphGroup glyphGroup, IEnumerable<SimpleLocationInfo> locations) {
                _name = description;
                _glyphGroup = glyphGroup;
                Children.AddRange(locations);
            }

            public override uint CategoryField(LIB_CATEGORY lIB_CATEGORY) {
                return (uint)(_LIB_LISTTYPE.LLT_MEMBERS | _LIB_LISTTYPE.LLT_PACKAGE);
            }

            #region ISimpleObject Members

            public bool CanDelete {
                get { return false; }
            }

            public bool CanGoToSource {
                get { return false; }
            }

            public bool CanRename {
                get { return false; }
            }

            public string Name {
                get { return _name; }
            }

            public string UniqueName {
                get { return _name; }
            }

            public string FullName {
                get {
                    return _name;
                }
            }

            public string GetTextRepresentation(VSTREETEXTOPTIONS options) {
                switch(options) {
                    case VSTREETEXTOPTIONS.TTO_DISPLAYTEXT:
                        return _name;
                }
                return null;
            }

            public string TooltipText {
                get { return null; }
            }

            public object BrowseObject {
                get { return null; }
            }

            public System.ComponentModel.Design.CommandID ContextMenuID {
                get { return null; }
            }

            public VSTREEDISPLAYDATA DisplayData {
                get { 
                    var res = new VSTREEDISPLAYDATA();
                    res.Image = res.SelectedImage = (ushort)_glyphGroup;
                    return res;
                }
            }

            public void Delete() {
            }

            public void DoDragDrop(OleDataObject dataObject, uint grfKeyState, uint pdwEffect) {
            }

            public void Rename(string pszNewName, uint grfFlags) {
            }

            public void GotoSource(VSOBJGOTOSRCTYPE SrcType) {
            }

            public void SourceItems(out IVsHierarchy ppHier, out uint pItemid, out uint pcItems) {
                ppHier = null;
                pItemid = 0;
                pcItems = 0;
            }

            public uint EnumClipboardFormats(_VSOBJCFFLAGS _VSOBJCFFLAGS, VSOBJCLIPFORMAT[] rgcfFormats) {
                return VSConstants.S_OK;
            }

            public void FillDescription(_VSOBJDESCOPTIONS _VSOBJDESCOPTIONS, IVsObjectBrowserDescription3 pobDesc) {
            }

            public IVsSimpleObjectList2 FilterView(uint ListType) {
                return this;
            }

            #endregion

            #region IVsNavInfo Members

            public int EnumCanonicalNodes(out IVsEnumNavInfoNodes ppEnum) {
                ppEnum = new NodeEnumerator<SimpleLocationInfo>(Children);
                return VSConstants.S_OK;
            }

            public int EnumPresentationNodes(uint dwFlags, out IVsEnumNavInfoNodes ppEnum) {
                ppEnum = new NodeEnumerator<SimpleLocationInfo>(Children);
                return VSConstants.S_OK;
            }

            public int GetLibGuid(out Guid pGuid) {
                pGuid = Guid.Empty;
                return VSConstants.S_OK;
            }

            public int GetSymbolType(out uint pdwType) {
                pdwType = (uint)_LIB_LISTTYPE2.LLT_MEMBERHIERARCHY;
                return VSConstants.S_OK;
            }

            #endregion

            #region ICustomSearchListProvider Members

            public IVsSimpleObjectList2 GetSearchList() {
                return this;
            }

            #endregion

            #region IVsNavInfoNode Members

            public int get_Name(out string pbstrName) {
                pbstrName = "name";
                return VSConstants.S_OK;
            }

            public int get_Type(out uint pllt) {
                pllt = 16; // (uint)_LIB_LISTTYPE2.LLT_MEMBERHIERARCHY;
                return VSConstants.S_OK;
            }

            #endregion
        }

        class NodeEnumerator<T> : IVsEnumNavInfoNodes where T : IVsNavInfoNode {
            private readonly List<T> _locations;
            private IEnumerator<T> _locationEnum;

            public NodeEnumerator(List<T> locations) {
                _locations = locations;
                Reset();
            }

            #region IVsEnumNavInfoNodes Members

            public int Clone(out IVsEnumNavInfoNodes ppEnum) {
                ppEnum = new NodeEnumerator<T>(_locations);
                return VSConstants.S_OK;
            }

            public int Next(uint celt, IVsNavInfoNode[] rgelt, out uint pceltFetched) {
                pceltFetched = 0;
                while (celt-- != 0 && _locationEnum.MoveNext()) {
                    rgelt[pceltFetched++] = _locationEnum.Current;
                }
                return VSConstants.S_OK;
            }

            public int Reset() {
                _locationEnum = _locations.GetEnumerator();
                return VSConstants.S_OK;
            }

            public int Skip(uint celt) {
                while (celt-- != 0) {
                    _locationEnum.MoveNext();
                }
                return VSConstants.S_OK;
            }

            #endregion
        }

        private void UpdateStatusForIncompleteAnalysis() {
            var statusBar = (IVsStatusbar)_serviceProvider.GetService(typeof(SVsStatusbar));
            var analyzer = _textView.GetAnalyzer(_serviceProvider);
            if (analyzer != null && analyzer.IsAnalyzing) {
                statusBar.SetText("Python source analysis is not up to date");
            }
        }

        #region IOleCommandTarget Members

        /// <summary>
        /// Called from VS when we should handle a command or pass it on.
        /// </summary>
        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            // preprocessing
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97) {
                switch ((VSConstants.VSStd97CmdID)nCmdID) {
                    case VSConstants.VSStd97CmdID.Paste:
                        PythonReplEvaluator eval;
                        if (_textView.Properties.TryGetProperty(typeof(PythonReplEvaluator), out eval)) {
                            string pasting = eval.FormatClipboard() ?? Clipboard.GetText();
                            if (pasting != null) {
                                PasteReplCode(eval, pasting);
                                
                                return VSConstants.S_OK;
                            }
                        } else {
                            string updated = RemoveReplPrompts(_pyService, _textView.Options.GetNewLineCharacter());
                            if (updated != null) {
                                _editorOps.ReplaceSelection(updated);
                                return VSConstants.S_OK;
                            }
                        }
                        break;
                    case VSConstants.VSStd97CmdID.GotoDefn: return GotoDefinition();
                    case VSConstants.VSStd97CmdID.FindReferences: return FindAllReferences();
                }
            } else if (pguidCmdGroup == CommonConstants.Std2KCmdGroupGuid) {
                OutliningTaggerProvider.OutliningTagger tagger;
                switch ((VSConstants.VSStd2KCmdID)nCmdID) {
#if !DEV14_OR_LATER
                    case (VSConstants.VSStd2KCmdID)147:
                        // ECMD_SMARTTASKS  defined in stdidcmd.h, but not in MPF
                        // if the user is typing to fast for us to update the smart tags on the idle event
                        // then we want to update them before VS pops them up.
                        UpdateSmartTags();
                        break;
#endif
                    case VSConstants.VSStd2KCmdID.FORMATDOCUMENT:
                        FormatCode(new SnapshotSpan(_textView.TextBuffer.CurrentSnapshot, 0, _textView.TextBuffer.CurrentSnapshot.Length), false);
                        return VSConstants.S_OK;
                    case VSConstants.VSStd2KCmdID.FORMATSELECTION:
                        FormatCode(_textView.Selection.StreamSelectionSpan.SnapshotSpan, true);
                        return VSConstants.S_OK;
                    case VSConstants.VSStd2KCmdID.SHOWMEMBERLIST:
                    case VSConstants.VSStd2KCmdID.COMPLETEWORD:
                        var controller = _textView.Properties.GetProperty<IntellisenseController>(typeof(IntellisenseController));
                        if (controller != null) {
                            IntellisenseController.ForceCompletions = true;
                            try {
                                controller.TriggerCompletionSession(
                                    (VSConstants.VSStd2KCmdID)nCmdID == VSConstants.VSStd2KCmdID.COMPLETEWORD,
                                    true
                                );
                            } finally {
                                IntellisenseController.ForceCompletions = false;
                            }
                            return VSConstants.S_OK;
                        }
                        break;

                    case VSConstants.VSStd2KCmdID.QUICKINFO:
                        controller = _textView.Properties.GetProperty<IntellisenseController>(typeof(IntellisenseController));
                        if (controller != null) {
                            controller.TriggerQuickInfo();
                            return VSConstants.S_OK;
                        }
                        break;

                    case VSConstants.VSStd2KCmdID.PARAMINFO:
                        controller = _textView.Properties.GetProperty<IntellisenseController>(typeof(IntellisenseController));
                        if (controller != null) {
                            controller.TriggerSignatureHelp();
                            return VSConstants.S_OK;
                        }
                        break;

                    case VSConstants.VSStd2KCmdID.OUTLN_STOP_HIDING_ALL:
                        tagger = _textView.GetOutliningTagger();
                        if (tagger != null) {
                            tagger.Disable();
                        }
                        // let VS get the event as well
                        break;

                    case VSConstants.VSStd2KCmdID.OUTLN_START_AUTOHIDING:
                        tagger = _textView.GetOutliningTagger();
                        if (tagger != null) {
                            tagger.Enable();
                        }
                        // let VS get the event as well
                        break;

                    case VSConstants.VSStd2KCmdID.COMMENT_BLOCK:
                    case VSConstants.VSStd2KCmdID.COMMENTBLOCK:
                        if (_textView.CommentOrUncommentBlock(comment: true)) {
                            return VSConstants.S_OK;
                        }
                        break;

                    case VSConstants.VSStd2KCmdID.UNCOMMENT_BLOCK:
                    case VSConstants.VSStd2KCmdID.UNCOMMENTBLOCK:
                        if (_textView.CommentOrUncommentBlock(comment: false)) {
                            return VSConstants.S_OK;
                        }
                        break;
                    case VSConstants.VSStd2KCmdID.EXTRACTMETHOD:
                        ExtractMethod();
                        return VSConstants.S_OK;
                    case VSConstants.VSStd2KCmdID.RENAME:
                        RefactorRename();
                        return VSConstants.S_OK;
                }
            } else if (pguidCmdGroup == GuidList.guidPythonToolsCmdSet) {
                switch (nCmdID) {
#if DEV14_OR_LATER
                    case PkgCmdIDList.comboIdReplScopes:
                        ScopeComboBoxHandler(pvaIn, pvaOut);
                        return VSConstants.S_OK;

                    case PkgCmdIDList.comboIdReplScopesGetList:
                        ScopeComboBoxGetList(pvaOut);
                        return VSConstants.S_OK;
#endif
                    case PkgCmdIDList.cmdidRefactorRenameIntegratedShell:
                        RefactorRename();
                        return VSConstants.S_OK;
                    case PkgCmdIDList.cmdidExtractMethodIntegratedShell:
                        ExtractMethod();
                        return VSConstants.S_OK;
                    default:
                        lock (PythonToolsPackage.CommandsLock) {
                            foreach (var command in PythonToolsPackage.Commands.Keys) {
                                if (command.CommandId == nCmdID) {
                                    command.DoCommand(this, EventArgs.Empty);
                                    return VSConstants.S_OK;
                                }
                            }
                        }
                        break;
                }

            }

            return _next.Exec(pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

#if DEV14_OR_LATER
        private void InitializeScopeList() {
            var interactive = _textView.TextBuffer.GetInteractiveWindow();
            if (interactive != null) {
                IMultipleScopeEvaluator multiScopeEval = interactive.Evaluator as IMultipleScopeEvaluator;
                if (multiScopeEval != null) {
                    _scopeListVisible = IsMultiScopeEnabled();
                    multiScopeEval.AvailableScopesChanged += UpdateScopeList;
                    multiScopeEval.MultipleScopeSupportChanged += MultipleScopeSupportChanged;
                }
            }
        }

        private void UpdateScopeList(object sender, EventArgs e) {
            if (!((UIElement)_textView).Dispatcher.CheckAccess()) {
                ((UIElement)_textView).Dispatcher.BeginInvoke(new Action(() => UpdateScopeList(sender, e)));
                return;
            }

            var interactive = _textView.TextBuffer.GetInteractiveWindow();
            if (interactive != null) {
                _currentScopes = ((IMultipleScopeEvaluator)interactive.Evaluator).GetAvailableScopes().ToArray();
            }
        }

        private bool IsMultiScopeEnabled() {
            var interactive = _textView.TextBuffer.GetInteractiveWindow();
            if (interactive != null) {
                var multiScope = interactive.Evaluator as IMultipleScopeEvaluator;
                return multiScope != null && multiScope.EnableMultipleScopes;
            }
            return false;
        }

        private void MultipleScopeSupportChanged(object sender, EventArgs e) {
            _scopeListVisible = IsMultiScopeEnabled();
        }

        /// <summary>
        /// Handles getting or setting the current value of the combo box.
        /// </summary>
        private void ScopeComboBoxHandler(IntPtr newValue, IntPtr outCurrentValue) {
            var interactive = _textView.TextBuffer.GetInteractiveWindow();
            if (interactive != null) {
                // getting the current value
                if (outCurrentValue != IntPtr.Zero) {
                    Marshal.GetNativeVariantForObject(((IMultipleScopeEvaluator)interactive.Evaluator).CurrentScopeName, outCurrentValue);
                }

                // setting the current value
                if (newValue != IntPtr.Zero) {
                    SetCurrentScope((string)Marshal.GetObjectForNativeVariant(newValue));
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

        internal void SetCurrentScope(string newItem) {
            var interactive = _textView.TextBuffer.GetInteractiveWindow();
            if (interactive != null) {
                string activeCode = interactive.CurrentLanguageBuffer.CurrentSnapshot.GetText();
                ((IMultipleScopeEvaluator)interactive.Evaluator).SetScope(newItem);
                interactive.InsertCode(activeCode);
            }
        }
#endif

        private bool ExtractMethod() {
            return new MethodExtractor(_serviceProvider, _textView).ExtractMethod(new ExtractMethodUserInput(_serviceProvider));
        }

        private void FormatCode(SnapshotSpan span, bool selectResult) {
            var options = _pyService.GetCodeFormattingOptions();
            options.NewLineFormat = _textView.Options.GetNewLineCharacter();
            new CodeFormatter(_serviceProvider, _textView, options).FormatCode(span, selectResult);
        }

        internal void RefactorRename() {
            var analyzer = _textView.GetAnalyzer(_serviceProvider);
            if (analyzer.IsAnalyzing) {
                var dialog = new WaitForCompleteAnalysisDialog(analyzer);

                var res = dialog.ShowModal();
                if (res != true) {
                    // user cancelled dialog before analysis completed...
                    return;
                }
            }

            new VariableRenamer(_textView, _serviceProvider).RenameVariable(
                new RenameVariableUserInput(_serviceProvider),
                (IVsPreviewChangesService)_serviceProvider.GetService(typeof(SVsPreviewChangesService))
            );
        }

        private static void PasteReplCode(PythonReplEvaluator eval, string pasting) {
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

#if DEV14_OR_LATER
                eval.Window.ReadyForInput += new PendLastSplitCode(eval.CurrentWindow, lastCode).AppendCode;
#else
                eval.Window.ReadyForInput += new PendLastSplitCode(eval.Window, lastCode).AppendCode;
#endif
                eval.Window.Submit(splitCode);
            } else {
                eval.Window.CurrentLanguageBuffer.Insert(0, startText + pasting + endText);
            }
        }

        class PendLastSplitCode {
            public readonly IReplWindow Window;
            public readonly string Text;

            public PendLastSplitCode(IReplWindow window, string text) {
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

        private const uint CommandDisabledAndHidden = (uint)(OLECMDF.OLECMDF_INVISIBLE | OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_DEFHIDEONCTXTMENU);
        /// <summary>
        /// Called from VS to see what commands we support.  
        /// </summary>
        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97) {
                for (int i = 0; i < cCmds; i++) {
                    switch ((VSConstants.VSStd97CmdID)prgCmds[i].cmdID) {
                        case VSConstants.VSStd97CmdID.GotoDefn:
                            prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                            return VSConstants.S_OK;
                        case VSConstants.VSStd97CmdID.FindReferences:
                            prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                            return VSConstants.S_OK;
                    }
                }
            } else if (pguidCmdGroup == GuidList.guidPythonToolsCmdSet) {
                for (int i = 0; i < cCmds; i++) {
                    switch (prgCmds[i].cmdID) {
#if DEV14_OR_LATER
                        case PkgCmdIDList.comboIdReplScopes:
                            if (_scopeListVisible) {
                                prgCmds[0].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                            } else {
                                prgCmds[0].cmdf = CommandDisabledAndHidden;
                            }
                            return VSConstants.S_OK;
#endif

                        case PkgCmdIDList.cmdidRefactorRenameIntegratedShell:
                            // C# provides the refactor context menu for the main VS command outside
                            // of the integrated shell.  In the integrated shell we provide our own
                            // command for it so these still show up.
#if DEV10
                            if (!IsCSharpInstalled()) {
                                QueryStatusRename(prgCmds, i);
                            } else 
#endif
                            {
                                prgCmds[i].cmdf = CommandDisabledAndHidden;
                            }
                            return VSConstants.S_OK;
                        case PkgCmdIDList.cmdidExtractMethodIntegratedShell:
                            // C# provides the refactor context menu for the main VS command outside
                            // of the integrated shell.  In the integrated shell we provide our own
                            // command for it so these still show up.
#if DEV10
                            if (!IsCSharpInstalled()) {
                                QueryStatusExtractMethod(prgCmds, i);
                            } else 
#endif
                            {
                                prgCmds[i].cmdf = CommandDisabledAndHidden;
                            }
                            return VSConstants.S_OK;
                        default:
                            lock (PythonToolsPackage.CommandsLock) {
                                foreach (var command in PythonToolsPackage.Commands.Keys) {
                                    if (command.CommandId == prgCmds[i].cmdID) {
                                        int? res = command.EditFilterQueryStatus(ref prgCmds[i], pCmdText);
                                        if (res != null) {
                                            return res.Value;
                                        }
                                    }
                                }
                            }
                            break;
                    }
                }
            } else if (pguidCmdGroup == CommonConstants.Std2KCmdGroupGuid) {
                OutliningTaggerProvider.OutliningTagger tagger;
                for (int i = 0; i < cCmds; i++) {
                    switch ((VSConstants.VSStd2KCmdID)prgCmds[i].cmdID) {
                        case VSConstants.VSStd2KCmdID.FORMATDOCUMENT:
                        case VSConstants.VSStd2KCmdID.FORMATSELECTION:

                        case VSConstants.VSStd2KCmdID.SHOWMEMBERLIST:
                        case VSConstants.VSStd2KCmdID.COMPLETEWORD:
                        case VSConstants.VSStd2KCmdID.QUICKINFO:
                        case VSConstants.VSStd2KCmdID.PARAMINFO:
                            prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                            return VSConstants.S_OK;

                        case VSConstants.VSStd2KCmdID.OUTLN_STOP_HIDING_ALL:
                            tagger = _textView.GetOutliningTagger();
                            if (tagger != null && tagger.Enabled) {
                                prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                            }
                            return VSConstants.S_OK;

                        case VSConstants.VSStd2KCmdID.OUTLN_START_AUTOHIDING:
                            tagger = _textView.GetOutliningTagger();
                            if (tagger != null && !tagger.Enabled) {
                                prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                            }
                            return VSConstants.S_OK;

                        case VSConstants.VSStd2KCmdID.COMMENT_BLOCK:
                        case VSConstants.VSStd2KCmdID.COMMENTBLOCK:
                        case VSConstants.VSStd2KCmdID.UNCOMMENT_BLOCK:
                        case VSConstants.VSStd2KCmdID.UNCOMMENTBLOCK:
                            prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                            return VSConstants.S_OK;
                        case VSConstants.VSStd2KCmdID.EXTRACTMETHOD:
                            QueryStatusExtractMethod(prgCmds, i);
                            return VSConstants.S_OK;
                        case VSConstants.VSStd2KCmdID.RENAME:
                            QueryStatusRename(prgCmds, i);
                            return VSConstants.S_OK;
                    }
                }
            }


            return _next.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

#if DEV10
        private bool IsCSharpInstalled() {
            IVsShell shell = (IVsShell)_serviceProvider.GetService(typeof(IVsShell));
            Guid csharpPacakge = GuidList.guidCSharpProjectPacakge;
            int installed;
            ErrorHandler.ThrowOnFailure(
                shell.IsPackageInstalled(ref csharpPacakge, out installed)
            );
            return installed != 0;
        }
#endif

        private void QueryStatusExtractMethod(OLECMD[] prgCmds, int i) {
            var activeView = CommonPackage.GetActiveTextView(_serviceProvider);

            if (_textView.TextBuffer.ContentType.IsOfType(PythonCoreConstants.ContentType)) {
                if (_textView.Selection.IsEmpty || 
                    _textView.Selection.Mode == TextSelectionMode.Box ||
                    String.IsNullOrWhiteSpace(_textView.Selection.StreamSelectionSpan.GetText())) {
                    prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_SUPPORTED);
                } else {
                    prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                }
            } else {
                prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_INVISIBLE);
            }
        }

        private void QueryStatusRename(OLECMD[] prgCmds, int i) {
            IWpfTextView activeView = CommonPackage.GetActiveTextView(_serviceProvider);
            if (_textView.TextBuffer.ContentType.IsOfType(PythonCoreConstants.ContentType)) {
                prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
            } else {
                prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_INVISIBLE);
            }
        }

#endregion

        internal static string RemoveReplPrompts(PythonToolsService pyService, string newline) {
            if (pyService.AdvancedOptions.PasteRemovesReplPrompts) {
                string text = Clipboard.GetText();
                if (text != null) {
                    string[] lines = text.Replace("\r\n", "\n").Split('\n');

                    bool allPrompts = true;
                    foreach (var line in lines) {
                        if (!(line.StartsWith("... ") || line.StartsWith(">>> "))) {
                            if (!String.IsNullOrWhiteSpace(line)) {
                                allPrompts = false;
                                break;
                            }
                        }
                    }

                    if (allPrompts) {
                        for (int i = 0; i < lines.Length; i++) {
                            if (!String.IsNullOrWhiteSpace(lines[i])) {
                                lines[i] = lines[i].Substring(4);
                            }
                        }

                        return String.Join(newline, lines);
                    }
                }
            }
            return null;
        }

        internal void DoIdle(IOleComponentManager compMgr) {
#if !DEV14_OR_LATER
            UpdateSmartTags();
#endif
        }

#if !DEV14_OR_LATER
        private void UpdateSmartTags() {
            SmartTagController controller;
            if (_textView.Properties.TryGetProperty<SmartTagController>(typeof(SmartTagController), out controller)) {
                controller.ShowSmartTag();
            }
        }
#endif
    }
}

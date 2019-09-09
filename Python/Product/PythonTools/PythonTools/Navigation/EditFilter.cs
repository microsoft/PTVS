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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Editor.Core;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Navigation;
using Microsoft.PythonTools.Refactoring;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Navigation;
using IServiceProvider = System.IServiceProvider;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Language {
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
        private readonly PythonEditorServices _editorServices;
        private readonly IVsTextView _vsTextView;
        private readonly ITextView _textView;
        private readonly IOleCommandTarget _next;

        private EditFilter(
            PythonEditorServices editorServices,
            IVsTextView vsTextView,
            ITextView textView,
            IOleCommandTarget next
        ) {
            _editorServices = editorServices;
            _vsTextView = vsTextView;
            _textView = textView;
            _next = next;

            BraceMatcher.WatchBraceHighlights(_editorServices, textView);

            if (_next == null && vsTextView != null) {
                ErrorHandler.ThrowOnFailure(vsTextView.AddCommandFilter(this, out _next));
            }
        }

        public static EditFilter GetOrCreate(
            PythonEditorServices editorServices,
            ITextView textView,
            IOleCommandTarget next = null
        ) {
            var vsTextView = editorServices.EditorAdaptersFactoryService.GetViewAdapter(textView);
            return textView.Properties.GetOrCreateSingletonProperty(() => new EditFilter(
                editorServices,
                vsTextView,
                textView,
                next
            ));
        }

        public static EditFilter GetOrCreate(
            PythonEditorServices editorServices,
            IVsTextView vsTextView,
            IOleCommandTarget next = null
        ) {
            var textView = editorServices.EditorAdaptersFactoryService.GetWpfTextView(vsTextView);
            return textView.Properties.GetOrCreateSingletonProperty(() => new EditFilter(
                editorServices,
                vsTextView,
                textView,
                next
            ));
        }

        /// <summary>
        /// Implements Goto Definition.  Called when the user selects Goto Definition from the 
        /// context menu or hits the hotkey associated with Goto Definition.
        /// 
        /// If there is 1 and only one definition immediately navigates to it.  If there are
        /// no references displays a dialog box to the user.  Otherwise it opens the find
        /// symbols dialog with the list of results.
        /// </summary>
        private async void GotoDefinition() {
            UpdateStatusForIncompleteAnalysis();

            var caret = _textView.GetPythonCaret();
            var analysis = _textView.GetAnalysisAtCaret(_editorServices.Site);
            if (analysis != null && caret != null) {
                var defs = await analysis.Analyzer.AnalyzeExpressionAsync(analysis, caret.Value, ExpressionAtPointPurpose.FindDefinition);
                if (defs == null) {
                    return;
                }
                Dictionary<LocationInfo, SimpleLocationInfo> references, definitions, values;
                GetDefsRefsAndValues(analysis.Analyzer, _editorServices.Site, defs.Expression, defs.Variables, out definitions, out references, out values);

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
                    if (String.IsNullOrWhiteSpace(defs.Expression)) {
                        MessageBox.Show(Strings.CannotGoToDefn, Strings.ProductTitle);
                    } else {
                        MessageBox.Show(Strings.CannotGoToDefn_Name.FormatUI(defs.Expression), Strings.ProductTitle);
                    }
                } else if (definitions.Count == 0) {
                    ShowFindSymbolsDialog(defs.Expression, new SymbolList(Strings.SymbolListValues, StandardGlyphGroup.GlyphForwardType, values.Values));
                } else if (values.Count == 0) {
                    ShowFindSymbolsDialog(defs.Expression, new SymbolList(Strings.SymbolListDefinitions, StandardGlyphGroup.GlyphLibrary, definitions.Values));
                } else {
                    ShowFindSymbolsDialog(defs.Expression,
                        new LocationCategory(
                            new SymbolList(Strings.SymbolListDefinitions, StandardGlyphGroup.GlyphLibrary, definitions.Values),
                            new SymbolList(Strings.SymbolListValues, StandardGlyphGroup.GlyphForwardType, values.Values)
                        )
                    );
                }
            }
        }

        /// <summary>
        /// Moves the caret to the specified location, staying in the current text view 
        /// if possible.
        /// 
        /// https://pytools.codeplex.com/workitem/1649
        /// </summary>
        private void GotoLocation(LocationInfo location) {
            Debug.Assert(location != null);
            Debug.Assert(location.StartLine > 0);
            Debug.Assert(location.StartColumn > 0);

            if (PathUtils.IsSamePath(location.FilePath, _textView.GetFilePath())) {
                var viewAdapter = _vsTextView;
                viewAdapter.SetCaretPos(location.StartLine - 1, location.StartColumn - 1);
                viewAdapter.CenterLines(location.StartLine - 1, 1);
            } else {
                PythonToolsPackage.NavigateTo(
                    _editorServices.Site,
                    location.FilePath,
                    Guid.Empty,
                    location.StartLine - 1,
                    location.StartColumn - 1
                );
            }
        }

        /// <summary>
        /// Implements Find All References.  Called when the user selects Find All References from
        /// the context menu or hits the hotkey associated with find all references.
        /// 
        /// Always opens the Find Symbol Results box to display the results.
        /// </summary>
        private async void FindAllReferences() {
            UpdateStatusForIncompleteAnalysis();

            var caret = _textView.GetPythonCaret();
            var analysis = _textView.GetAnalysisAtCaret(_editorServices.Site);
            if (analysis != null && caret != null) {
                var references = await analysis.Analyzer.AnalyzeExpressionAsync(analysis, caret.Value, ExpressionAtPointPurpose.FindDefinition);
                if (references == null) {
                    return;
                }

                var locations = GetFindRefLocations(analysis.Analyzer, _editorServices.Site, references.Expression, references.Variables);

                ShowFindSymbolsDialog(references.Expression, locations);
            }
        }

        internal static LocationCategory GetFindRefLocations(VsProjectAnalyzer analyzer, IServiceProvider serviceProvider, string expr, IReadOnlyList<AnalysisVariable> analysis) {
            Dictionary<LocationInfo, SimpleLocationInfo> references, definitions, values;
            GetDefsRefsAndValues(analyzer, serviceProvider, expr, analysis, out definitions, out references, out values);

            var locations = new LocationCategory(
                new SymbolList(Strings.SymbolListDefinitions, StandardGlyphGroup.GlyphLibrary, definitions.Values),
                new SymbolList(Strings.SymbolListValues, StandardGlyphGroup.GlyphForwardType, values.Values),
                new SymbolList(Strings.SymbolListReferences, StandardGlyphGroup.GlyphReference, references.Values)
            );
            return locations;
        }

        private static void GetDefsRefsAndValues(VsProjectAnalyzer analyzer, IServiceProvider serviceProvider, string expr, IReadOnlyList<AnalysisVariable> variables, out Dictionary<LocationInfo, SimpleLocationInfo> definitions, out Dictionary<LocationInfo, SimpleLocationInfo> references, out Dictionary<LocationInfo, SimpleLocationInfo> values) {
            references = new Dictionary<LocationInfo, SimpleLocationInfo>();
            definitions = new Dictionary<LocationInfo, SimpleLocationInfo>();
            values = new Dictionary<LocationInfo, SimpleLocationInfo>();

            if (variables == null) {
                Debug.Fail("unexpected null variables");
                return;
            }

            foreach (var v in variables) {
                if (v?.Location == null) {
                    Debug.Fail("unexpected null variable or location");
                    continue;
                }
                if (v.Location.FilePath == null) {
                    // ignore references in the REPL
                    continue;
                }

                switch (v.Type) {
                    case VariableType.Definition:
                        values.Remove(v.Location);
                        definitions[v.Location] = new SimpleLocationInfo(analyzer, serviceProvider, expr, v.Location, StandardGlyphGroup.GlyphGroupField);
                        break;
                    case VariableType.Reference:
                        references[v.Location] = new SimpleLocationInfo(analyzer, serviceProvider, expr, v.Location, StandardGlyphGroup.GlyphGroupField);
                        break;
                    case VariableType.Value:
                        if (!definitions.ContainsKey(v.Location)) {
                            values[v.Location] = new SimpleLocationInfo(analyzer, serviceProvider, expr, v.Location, StandardGlyphGroup.GlyphGroupField);
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
        private void ShowFindSymbolsDialog(string expr, IVsNavInfo symbols) {
            // ensure our library is loaded so find all references will go to our library
            _editorServices.Site.GetService(typeof(IPythonLibraryManager));

            if (!string.IsNullOrEmpty(expr)) {
                var findSym = (IVsFindSymbol)_editorServices.Site.GetService(typeof(SVsObjectSearch));
                VSOBSEARCHCRITERIA2 searchCriteria = new VSOBSEARCHCRITERIA2();
                searchCriteria.eSrchType = VSOBSEARCHTYPE.SO_ENTIREWORD;
                searchCriteria.pIVsNavInfo = symbols;
                searchCriteria.grfOptions = (uint)_VSOBSEARCHOPTIONS2.VSOBSO_LISTREFERENCES;
                searchCriteria.szName = expr;

                Guid guid = Guid.Empty;
                //  new Guid("{a5a527ea-cf0a-4abf-b501-eafe6b3ba5c6}")
                ErrorHandler.ThrowOnFailure(findSym.DoSearch(new Guid(CommonConstants.LibraryGuid), new VSOBSEARCHCRITERIA2[] { searchCriteria }));
            } else {
                var statusBar = (IVsStatusbar)_editorServices.Site.GetService(typeof(SVsStatusbar));
                statusBar.SetText(Strings.FindReferencesCaretMustBeOnValidExpression);
            }
        }

        internal class LocationCategory : SimpleObjectList<SymbolList>, IVsNavInfo, ICustomSearchListProvider {
            internal LocationCategory(params SymbolList[] locations) {
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
            private readonly IServiceProvider _serviceProvider;
            private readonly LocationInfo _locationInfo;
            private readonly StandardGlyphGroup _glyphType;
            private readonly string _pathText, _lineText;

            public SimpleLocationInfo(VsProjectAnalyzer analyzer, IServiceProvider serviceProvider, string searchText, LocationInfo locInfo, StandardGlyphGroup glyphType) {
                _serviceProvider = serviceProvider;
                _locationInfo = locInfo;
                _glyphType = glyphType;
                _pathText = GetSearchDisplayText();
                AnalysisEntry entry = analyzer.GetAnalysisEntryFromPath(_locationInfo.FilePath);
                if (entry != null) {
                    _lineText = entry.GetLine(_locationInfo.StartLine) ?? "";
                } else {
                    _lineText = "";
                }
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
                return $"{_locationInfo.FilePath} - {_locationInfo.Span.Start}: ";
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
                PythonToolsPackage.NavigateTo(
                    _serviceProvider,
                    _locationInfo.FilePath,
                    Guid.Empty,
                    _locationInfo.StartLine - 1,
                    _locationInfo.StartColumn - 1
                );
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
                foreach (var location in locations) {
                    Children.Add(location);
                }
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
            private readonly IList<T> _locations;
            private IEnumerator<T> _locationEnum;

            public NodeEnumerator(IList<T> locations) {
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
            var statusBar = (IVsStatusbar)_editorServices.Site.GetService(typeof(SVsStatusbar));
            var analyzer = _textView.GetAnalyzerAtCaret(_editorServices.Site);
            if (analyzer != null && analyzer.IsAnalyzing) {
                statusBar.SetText(Strings.SourceAnalysisNotUpToDate);
            }
        }

        #region IOleCommandTarget Members

        /// <summary>
        /// Called from VS when we should handle a command or pass it on.
        /// </summary>
        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            try {
                return ExecWorker(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            } catch (Exception ex) {
                ex.ReportUnhandledException(_editorServices.Site, GetType());
                return VSConstants.E_FAIL;
            }
        }

        private int ExecWorker(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            // preprocessing
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97) {
                switch ((VSConstants.VSStd97CmdID)nCmdID) {
                    case VSConstants.VSStd97CmdID.Paste:
                        if (!_editorServices.Python.AdvancedOptions.PasteRemovesReplPrompts) {
                            // Not stripping prompts, so don't use our logic
                            break;
                        }
                        var beforePaste = _textView.TextSnapshot;
                        if (_editorServices.EditOperationsFactory.GetEditorOperations(_textView).Paste()) {
                            var afterPaste = _textView.TextSnapshot;
                            var um = _editorServices.UndoManagerFactory.GetTextBufferUndoManager(afterPaste.TextBuffer);
                            using (var undo = um.TextBufferUndoHistory.CreateTransaction(Strings.RemoveReplPrompts)) {
                                if (ReplPromptHelpers.RemovePastedPrompts(beforePaste, afterPaste)) {
                                    undo.Complete();
                                }
                            }
                            return VSConstants.S_OK;
                        }
                        break;
                    case VSConstants.VSStd97CmdID.GotoDefn: GotoDefinition(); return VSConstants.S_OK;
                    case VSConstants.VSStd97CmdID.FindReferences: FindAllReferences(); return VSConstants.S_OK;
                }
            } else if (pguidCmdGroup == VSConstants.VsStd12) {
                switch ((VSConstants.VSStd12CmdID)nCmdID) {
                    case VSConstants.VSStd12CmdID.PeekDefinition:
                        if (_editorServices.PeekBroker != null &&
                            !_textView.Roles.Contains(PredefinedTextViewRoles.EmbeddedPeekTextView) &&
                            !_textView.Roles.Contains(PredefinedTextViewRoles.CodeDefinitionView)) {
                            _editorServices.PeekBroker.TriggerPeekSession(_textView, PredefinedPeekRelationships.Definitions.Name);
                            return VSConstants.S_OK;
                        }
                        break;
                }
            } else if (pguidCmdGroup == CommonConstants.Std2KCmdGroupGuid) {
                SnapshotPoint? pyPoint;
                IntellisenseController controller;
                switch ((VSConstants.VSStd2KCmdID)nCmdID) {
                    case VSConstants.VSStd2KCmdID.RETURN:
                        pyPoint = _textView.GetPythonCaret();
                        if (pyPoint != null) {
                            // https://github.com/Microsoft/PTVS/issues/241
                            // If the current line is a full line comment and we
                            // are splitting the text, automatically insert the
                            // comment marker on the new line.
                            var line = pyPoint.Value.GetContainingLine();
                            var lineText = pyPoint.Value.Snapshot.GetText(line.Start, pyPoint.Value - line.Start);
                            int comment = lineText.IndexOf('#');
                            if (comment >= 0 &&
                                pyPoint.Value < line.End &&
                                line.Start + comment < pyPoint.Value &&
                                string.IsNullOrWhiteSpace(lineText.Remove(comment))
                            ) {
                                int extra = lineText.Skip(comment + 1).TakeWhile(char.IsWhiteSpace).Count() + 1;
                                using (var edit = line.Snapshot.TextBuffer.CreateEdit()) {
                                    edit.Insert(
                                        pyPoint.Value.Position,
                                        _textView.Options.GetNewLineCharacter() + lineText.Substring(0, comment + extra)
                                    );
                                    edit.Apply();
                                }
                                
                                return VSConstants.S_OK;
                            }
                        }
                        break;

                    case VSConstants.VSStd2KCmdID.FORMATDOCUMENT:
                        FormatDocumentAsync().DoNotWait();
                        return VSConstants.S_OK;
                    case VSConstants.VSStd2KCmdID.FORMATSELECTION:
                        FormatSelectionAsync().DoNotWait();
                        return VSConstants.S_OK;
                    case VSConstants.VSStd2KCmdID.SHOWMEMBERLIST:
                    case VSConstants.VSStd2KCmdID.COMPLETEWORD:
                        if (_textView.Properties.TryGetProperty(typeof(IntellisenseController), out controller)) {
                            controller.TriggerCompletionSession(
                                (VSConstants.VSStd2KCmdID)nCmdID == VSConstants.VSStd2KCmdID.COMPLETEWORD,
                                '\0',
                                true
                            ).DoNotWait();
                            return VSConstants.S_OK;
                        }
                        break;

                    case VSConstants.VSStd2KCmdID.QUICKINFO:
                        if (_textView.Properties.TryGetProperty(typeof(IntellisenseController), out controller)) {
                            controller.TriggerQuickInfoAsync().DoNotWait();
                            return VSConstants.S_OK;
                        }
                        break;

                    case VSConstants.VSStd2KCmdID.PARAMINFO:
                        if (_textView.Properties.TryGetProperty(typeof(IntellisenseController), out controller)) {
                            controller.TriggerSignatureHelp();
                            return VSConstants.S_OK;
                        }
                        break;
                    case VSConstants.VSStd2KCmdID.OUTLN_STOP_HIDING_ALL:
                        _textView.GetOutliningTagger()?.Disable(_textView.TextSnapshot);
                        // let VS get the event as well
                        break;

                    case VSConstants.VSStd2KCmdID.OUTLN_START_AUTOHIDING:
                        _textView.GetOutliningTagger()?.Enable(_textView.TextSnapshot);
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
                    case PkgCmdIDList.cmdidRefactorRenameIntegratedShell:
                        RefactorRename();
                        return VSConstants.S_OK;
                    case PkgCmdIDList.cmdidExtractMethodIntegratedShell:
                        ExtractMethod();
                        return VSConstants.S_OK;
                    case CommonConstants.StartDebuggingCmdId:
                    case CommonConstants.StartWithoutDebuggingCmdId:
                        PythonToolsPackage.LaunchFile(_editorServices.Site, _textView.GetFilePath(), nCmdID == CommonConstants.StartDebuggingCmdId, true);
                        return VSConstants.S_OK;
                }

            }

            return _next.Exec(pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }


        private void ExtractMethod() {
            new Refactoring.MethodExtractor(_editorServices, _textView).ExtractMethod(new ExtractMethodUserInput(_editorServices.Site)).DoNotWait();
        }

        internal async Task FormatDocumentAsync() {
            var pyPoint = _textView.GetPythonCaret();
            if (pyPoint != null) {
                await FormatCodeAsync(new SnapshotSpan(pyPoint.Value.Snapshot, 0, pyPoint.Value.Snapshot.Length), false);
            }
        }

        internal async Task FormatSelectionAsync() {
            var snapshotSpan = _textView.Selection.StreamSelectionSpan.SnapshotSpan;
            foreach (var span in _textView.BufferGraph.MapDownToFirstMatch(snapshotSpan, SpanTrackingMode.EdgeInclusive, EditorExtensions.IsPythonContent)) {
                await FormatCodeAsync(span, true);
            }
        }

        private async Task FormatCodeAsync(SnapshotSpan span, bool selectResult) {
            var entry = span.Snapshot.TextBuffer.TryGetAnalysisEntry();
            if (entry == null) {
                return;
            }

            var options = _editorServices.Python.GetCodeFormattingOptions();
            options.NewLineFormat = _textView.Options.GetNewLineCharacter();

            await entry.Analyzer.FormatCodeAsync(span, _textView, options, selectResult);
        }

        internal void RefactorRename() {
            var analyzer = _textView.GetAnalyzerAtCaret(_editorServices.Site);
            if (analyzer.IsAnalyzing) {
                var dialog = new WaitForCompleteAnalysisDialog(analyzer);

                var res = dialog.ShowModal();
                if (res != true) {
                    // user cancelled dialog before analysis completed...
                    return;
                }
            }

            new VariableRenamer(_textView, _editorServices.Site).RenameVariable(
                new RenameVariableUserInput(_editorServices.Site),
                (IVsPreviewChangesService)_editorServices.Site.GetService(typeof(SVsPreviewChangesService))
            ).DoNotWait();
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
            } else if (pguidCmdGroup == VSConstants.VsStd12) {
                for (int i = 0; i < cCmds; i++) {
                    switch ((VSConstants.VSStd12CmdID)prgCmds[i].cmdID) {
                        case VSConstants.VSStd12CmdID.PeekDefinition:
                            // Since our provider supports standalone files,
                            // the predicate isn't invoked but it needs to be non null.
                            var canPeek = _editorServices.PeekBroker?.CanTriggerPeekSession(
                                _textView,
                                PredefinedPeekRelationships.Definitions.Name,
                                isStandaloneFilePredicate: (string filename) => false
                            );
                            prgCmds[i].cmdf = (uint)OLECMDF.OLECMDF_SUPPORTED;
                            prgCmds[0].cmdf |= (uint)(canPeek == true ? OLECMDF.OLECMDF_ENABLED : OLECMDF.OLECMDF_INVISIBLE);
                            return VSConstants.S_OK;
                    }
                }
            } else if (pguidCmdGroup == GuidList.guidPythonToolsCmdSet) {
                for (int i = 0; i < cCmds; i++) {
                    switch (prgCmds[i].cmdID) {
                        case PkgCmdIDList.cmdidRefactorRenameIntegratedShell:
                            // C# provides the refactor context menu for the main VS command outside
                            // of the integrated shell.  In the integrated shell we provide our own
                            // command for it so these still show up.
                            prgCmds[i].cmdf = CommandDisabledAndHidden;
                            return VSConstants.S_OK;
                        case PkgCmdIDList.cmdidExtractMethodIntegratedShell:
                            // C# provides the refactor context menu for the main VS command outside
                            // of the integrated shell.  In the integrated shell we provide our own
                            // command for it so these still show up.
                            prgCmds[i].cmdf = CommandDisabledAndHidden;
                            return VSConstants.S_OK;
                        case CommonConstants.StartDebuggingCmdId:
                        case CommonConstants.StartWithoutDebuggingCmdId:
                            // Don't enable the command when the file is null or doesn't exist,
                            // which can happen in the diff window.
                            if (File.Exists(_textView.GetFilePath())) {
                                prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                            } else {
                                prgCmds[i].cmdf = CommandDisabledAndHidden;
                            }
                            return VSConstants.S_OK;
                        default:
                            lock (CommonPackage.CommandsLock) {
                                foreach (var command in CommonPackage.Commands.Keys) {
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
                            if (_textView.GetOutliningTagger()?.Enabled == true) {
                                prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                                return VSConstants.S_OK;
                            }
                            break;

                        case VSConstants.VSStd2KCmdID.OUTLN_START_AUTOHIDING:
                            if (_textView.GetOutliningTagger()?.Enabled == false) {
                                prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                                return VSConstants.S_OK;
                            }
                            break;

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

        private void QueryStatusExtractMethod(OLECMD[] prgCmds, int i) {
            switch (Refactoring.MethodExtractor.CanExtract(_textView)) {
                case true:
                    prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                    break;
                case false:
                    prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_SUPPORTED);
                    break;
                case null:
                    prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_INVISIBLE);
                    break;
            }
        }

        private void QueryStatusRename(OLECMD[] prgCmds, int i) {
            var analyzer = _textView.GetAnalyzerAtCaret(_editorServices.Site);
            if (analyzer != null && _textView.GetPythonBufferAtCaret() != null) {
                prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
            } else {
                prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_INVISIBLE);
            }
        }

#endregion

        internal void DoIdle(IOleComponentManager compMgr) {
        }
    }
}

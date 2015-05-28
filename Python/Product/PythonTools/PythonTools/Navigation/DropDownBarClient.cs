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
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Windows.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Navigation {
    /// <summary>
    /// Implements the navigation bar which appears above a source file in the editor.
    /// 
    /// The navigation bar consists of two drop-down boxes.  On the left hand side is a list
    /// of top level constructs.  On the right hand side are list of nested constructs for the
    /// currently selected top-level construct.
    /// 
    /// When the user moves the caret the current selections are automatically updated.  If the
    /// user is inside of a top level construct but not inside any of the available nested 
    /// constructs then the first element of the nested construct list is selected and displayed
    /// grayed out.  If the user is inside of no top level constructs then the 1st top-level
    /// construct is selected and displayed as grayed out.  It's first top-level construct is
    /// also displayed as being grayed out.
    /// 
    /// The most difficult part of this is handling the transitions from one state to another.
    /// We need to change the current selections due to events from two sources:  The first is selections
    /// in the drop down and the 2nd is the user navigating within the source code.  When a change
    /// occurs we may need to update the left hand side (along w/ a corresponding update to the right
    /// hand side) or we may need to update the right hand side.  If we are transitioning from
    /// being outside of a known element to being in a known element we also need to refresh 
    /// the drop down to remove grayed out elements.
    /// </summary>
    class DropDownBarClient : IVsDropdownBarClient {
        private IPythonProjectEntry _projectEntry;                      // project entry which gets updated with new ASTs for us to inspect.
        private ReadOnlyCollection<DropDownEntryInfo> _topLevelEntries; // entries for top-level members of the file
        private ReadOnlyCollection<DropDownEntryInfo> _nestedEntries;   // entries for nested members in the file
        private readonly Dispatcher _dispatcher;                        // current dispatcher so we can get back to our thread
        private IWpfTextView _textView;                                 // text view we're drop downs for
        private IVsDropdownBar _dropDownBar;                            // drop down bar - used to refresh when changes occur
        private int _curTopLevelIndex = -1, _curNestedIndex = -1;       // currently selected indices for each oar
        private readonly IServiceProvider _serviceProvider;
        private IntPtr _imageList;

        private static readonly ReadOnlyCollection<DropDownEntryInfo> EmptyEntries = new ReadOnlyCollection<DropDownEntryInfo>(new DropDownEntryInfo[0]);

        private const int TopLevelComboBoxId = 0;
        private const int NestedComboBoxId = 1;

        public DropDownBarClient(IServiceProvider serviceProvider, IWpfTextView textView, IPythonProjectEntry pythonProjectEntry) {
            Utilities.ArgumentNotNull("serviceProvider", serviceProvider);
            Utilities.ArgumentNotNull("textView", textView);
            Utilities.ArgumentNotNull("pythonProjectEntry", pythonProjectEntry);

            _serviceProvider = serviceProvider;
            _projectEntry = pythonProjectEntry;
            _projectEntry.OnNewParseTree += ParserOnNewParseTree;
            _textView = textView;
            _topLevelEntries = _nestedEntries = EmptyEntries;
            _dispatcher = Dispatcher.CurrentDispatcher;
            _textView.Caret.PositionChanged += CaretPositionChanged;
        }

        internal void Unregister() {
            _projectEntry.OnNewParseTree -= ParserOnNewParseTree;
            _textView.Caret.PositionChanged -= CaretPositionChanged;
        }

        public void UpdateView(IWpfTextView textView) {
            if (_textView != textView) {
                _textView.Caret.PositionChanged -= CaretPositionChanged;
                _textView = textView;
                _textView.Caret.PositionChanged += CaretPositionChanged;
                CaretPositionChanged(this, new CaretPositionChangedEventArgs(null, _textView.Caret.Position, _textView.Caret.Position));
            }
        }

        #region IVsDropdownBarClient Members

        /// <summary>
        /// Gets the attributes for the specified combo box.  We return the number of elements that we will
        /// display, the various attributes that VS should query for next (text, image, and attributes of
        /// the text such as being grayed out), along with the appropriate image list.
        /// 
        /// We always return the # of entries based off our entries list, the exact same image list, and
        /// we have VS query for text, image, and text attributes all the time.
        /// </summary>
        public int GetComboAttributes(int iCombo, out uint pcEntries, out uint puEntryType, out IntPtr phImageList) {
            uint count = 0;

            switch (iCombo) {
                case TopLevelComboBoxId:
                    CalculateTopLevelEntries();
                    count = (uint)_topLevelEntries.Count;
                    break;
                case NestedComboBoxId:
                    CalculateNestedEntries();
                    count = (uint)_nestedEntries.Count;
                    break;
            }

            pcEntries = count;
            puEntryType = (uint)(DROPDOWNENTRYTYPE.ENTRY_TEXT | DROPDOWNENTRYTYPE.ENTRY_IMAGE | DROPDOWNENTRYTYPE.ENTRY_ATTR);
            phImageList = GetImageList();
            return VSConstants.S_OK;
        }
        
        public int GetComboTipText(int iCombo, out string pbstrText) {
            pbstrText = null;
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Gets the entry attributes for the given combo box and index.
        /// 
        /// We always use plain text unless we are not inside of a valid entry
        /// for the given combo box.  In that case we ensure the 1st item
        /// is selected and we gray out the 1st entry.
        /// </summary>
        public int GetEntryAttributes(int iCombo, int iIndex, out uint pAttr) {
            pAttr = (uint)DROPDOWNFONTATTR.FONTATTR_PLAIN;
            
            if (iIndex == 0) {
                switch (iCombo) {
                    case TopLevelComboBoxId:
                        if (_curTopLevelIndex == -1) {
                            pAttr = (uint)DROPDOWNFONTATTR.FONTATTR_GRAY;
                        }
                        break;
                    case NestedComboBoxId:
                        if (_curNestedIndex == -1) {
                            pAttr = (uint)DROPDOWNFONTATTR.FONTATTR_GRAY;
                        }
                        break;
                }
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Gets the image which is associated with the given index for the
        /// given combo box.
        /// </summary>
        public int GetEntryImage(int iCombo, int iIndex, out int piImageIndex) {
            piImageIndex = 0;

            switch (iCombo) {
                case TopLevelComboBoxId:
                    var topLevel = _topLevelEntries;
                    if (iIndex < topLevel.Count) {
                        piImageIndex = topLevel[iIndex].ImageListIndex;
                    }
                    break;
                case NestedComboBoxId:
                    var nested = _nestedEntries;
                    if (iIndex < nested.Count) {
                        piImageIndex = nested[iIndex].ImageListIndex;
                    }
                    break;
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Gets the text which is displayed for the given index for the
        /// given combo box.
        /// </summary>
        public int GetEntryText(int iCombo, int iIndex, out string ppszText) {
            ppszText = String.Empty;
            switch (iCombo) {
                case TopLevelComboBoxId:
                    var topLevel = _topLevelEntries;
                    if (iIndex < topLevel.Count) {
                        ppszText = topLevel[iIndex].Name;
                    }
                    break;
                case NestedComboBoxId:
                    var nested = _nestedEntries;
                    if (iIndex < nested.Count) {
                        ppszText = nested[iIndex].Name;
                    }
                    break;
            }

            return VSConstants.S_OK;
        }

        public int OnComboGetFocus(int iCombo) {
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Called when the user selects an item from the drop down.  We will
        /// update the caret to beat the correct location, move the view port
        /// so that the code is centered on the screen, and we may refresh
        /// the combo box so that the 1st item is no longer grayed out if
        /// the user was originally outside of valid selection.
        /// </summary>
        public int OnItemChosen(int iCombo, int iIndex) {
            if (_dropDownBar == null) {
                return VSConstants.E_UNEXPECTED;
            }
            switch (iCombo) {
                case TopLevelComboBoxId:
                    _dropDownBar.RefreshCombo(NestedComboBoxId, 0);
                    var topLevel = _topLevelEntries;
                    if (iIndex < topLevel.Count) {
                        int oldIndex = _curTopLevelIndex;
                        _curTopLevelIndex = iIndex;
                        if (oldIndex == -1) {
                            _dropDownBar.RefreshCombo(TopLevelComboBoxId, iIndex);
                        }
                        CenterAndFocus(topLevel[iIndex].Start.Index);
                    }
                    break;
                case NestedComboBoxId:
                    var nested = _nestedEntries;
                    if (iIndex < nested.Count) {
                        int oldIndex = _curNestedIndex;
                        _curNestedIndex = iIndex;
                        if (oldIndex == -1) {
                            _dropDownBar.RefreshCombo(NestedComboBoxId, iIndex);
                        }
                        CenterAndFocus(nested[iIndex].Start.Index);
                    }
                    break;
            }
            return VSConstants.S_OK;
        }
        
        public int OnItemSelected(int iCombo, int iIndex) {
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Called by VS to provide us with the drop down bar.  We can call back
        /// on the drop down bar to force VS to refresh the combo box or change
        /// the current selection.
        /// </summary>
        public int SetDropdownBar(IVsDropdownBar pDropdownBar) {
            _dropDownBar = pDropdownBar;
            if (_dropDownBar != null) {
                CaretPositionChanged(this, new CaretPositionChangedEventArgs(null, _textView.Caret.Position, _textView.Caret.Position));
            }
            
            return VSConstants.S_OK;
        }

        #endregion
        
        #region Selection Synchronization

        private void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e) {
            int newPosition = e.NewPosition.BufferPosition.Position;

            var topLevel = _topLevelEntries;
            int curTopLevel = _curTopLevelIndex;

            if (curTopLevel != -1 && curTopLevel < topLevel.Count) {
                if (newPosition >= topLevel[curTopLevel].Start.Index && newPosition <= topLevel[curTopLevel].End.Index) {
                    UpdateNestedComboSelection(newPosition);
                } else {
                    FindActiveTopLevelComboSelection(newPosition, topLevel);
                }
            } else {
                FindActiveTopLevelComboSelection(newPosition, topLevel);
            }
        }

        private void FindActiveTopLevelComboSelection(int newPosition, ReadOnlyCollection<DropDownEntryInfo> topLevel) {
            if (_dropDownBar == null) {
                return;
            }

            int oldTopLevel = _curTopLevelIndex;

            // left side has changed
            bool found = false;
            for (int i = 0; i < topLevel.Count; i++) {
                if (newPosition >= topLevel[i].Start.Index && newPosition <= topLevel[i].End.Index) {
                    _curTopLevelIndex = i;

                    // we found a new left hand side
                    if (oldTopLevel == -1) {
                        // we've selected something new, we need to refresh the combo to
                        // to remove the grayed out entry
                        _dropDownBar.RefreshCombo(TopLevelComboBoxId, i);
                    } else {
                        // changing from one top-level to another, just update the selection
                        _dropDownBar.SetCurrentSelection(TopLevelComboBoxId, i);
                    }

                    // update the nested entries
                    CalculateNestedEntries();
                    _dropDownBar.RefreshCombo(NestedComboBoxId, 0);
                    UpdateNestedComboSelection(newPosition);
                    found = true;
                    break;
                }
            }

            if (!found) {
                // there's no associated entry, we should disable the bar
                _curTopLevelIndex = -1;
                _curNestedIndex = -1;

                if (oldTopLevel != -1) {
                    // we need to refresh to clear both combo boxes since there is no associated entry
                    _dropDownBar.RefreshCombo(TopLevelComboBoxId, -1);
                    _dropDownBar.RefreshCombo(NestedComboBoxId, -1);
                }
            }

            UpdateNestedComboSelection(newPosition);
        }

        private void UpdateNestedComboSelection(int newPosition) {
            // left side has not changed, check rhs
            int curNested = _curNestedIndex;
            var nested = _nestedEntries;

            if (curNested != -1 && curNested < nested.Count) {
                if (newPosition < nested[curNested].Start.Index || newPosition > nested[curNested].End.Index) {
                    // right hand side has changed
                    FindActiveNestedSelection(newPosition, nested);
                }
            } else {
                FindActiveNestedSelection(newPosition, nested);
            }
        }

        private void FindActiveNestedSelection(int newPosition, ReadOnlyCollection<DropDownEntryInfo> nested) {
            if (_dropDownBar == null) {
                return;
            }

            int oldNested = _curNestedIndex;

            bool found = false;
            
            if (_curTopLevelIndex != -1) {
                for (int i = 0; i < nested.Count; i++) {
                    if (newPosition >= nested[i].Start.Index && newPosition <= nested[i].End.Index) {
                        _curNestedIndex = i;

                        if (oldNested == -1) {
                            // we've selected something new, we need to refresh the combo to
                            // to remove the grayed out entry
                            _dropDownBar.RefreshCombo(NestedComboBoxId, i);
                        } else {
                            // changing from one nested to another, just update the selection
                            _dropDownBar.SetCurrentSelection(NestedComboBoxId, i);
                        }

                        found = true;
                        break;
                    }
                }
            }

            if (!found) {
                // there's no associated entry, we should disable the bar
                _curNestedIndex = -1;

                // we need to refresh to clear the nested combo box since there is no associated nested entry
                _dropDownBar.RefreshCombo(NestedComboBoxId, -1);
            }
        }

        #endregion

        #region Entry Calculation

        /// <summary>
        /// Data structure used for tracking elements of the drop down box in the navigation bar.
        /// </summary>
        struct DropDownEntryInfo {
            public readonly Statement Body;

            public DropDownEntryInfo(Statement body) {
                Body = body;
            }

            /// <summary>
            /// Gets the name which should be displayed for the text in the drop down.
            /// </summary>
            public string Name {
                get {
                    ClassDefinition klass = Body as ClassDefinition;
                    if (klass != null) {
                        return klass.Name;
                    }

                    FunctionDefinition func = Body as FunctionDefinition;
                    if (func != null) {
                        return func.Name;
                    }

                    return String.Empty;
                }
            }

            /// <summary>
            /// Gets the index in our image list which should be used for the icon to display
            /// next to the drop down element.
            /// </summary>
            public int ImageListIndex {
                get {
                    ImageListOverlay overlay = ImageListOverlay.ImageListOverlayNone;
                    string name = Name;
                    if (name != null && name.StartsWith("_") && !(name.StartsWith("__") && name.EndsWith("__"))) {
                        overlay = ImageListOverlay.ImageListOverlayPrivate;
                    }

                    FunctionDefinition funcDef;
                    if (Body is ClassDefinition) {
                        return GetImageListIndex(ImageListKind.Class, overlay);
                    } else if ((funcDef = Body as FunctionDefinition) != null) {
                        return GetImageListIndex(GetImageListKind(funcDef), overlay);
                    }

                    return 0;
                }
            }

            private static ImageListKind GetImageListKind(FunctionDefinition funcDef) {
                ImageListKind imageKind = ImageListKind.Method;
                if (funcDef.Decorators != null && funcDef.Decorators.Decorators.Count == 1) {
                    foreach (var decorator in funcDef.Decorators.Decorators) {
                        NameExpression nameExpr = decorator as NameExpression;
                        if (nameExpr != null) {
                            if (nameExpr.Name == "property") {
                                imageKind = ImageListKind.Property;
                                break;
                            } else if (nameExpr.Name == "staticmethod") {
                                imageKind = ImageListKind.StaticMethod;
                                break;
                            } else if (nameExpr.Name == "classmethod") {
                                imageKind = ImageListKind.ClassMethod;
                                break;
                            }
                        }
                    }
                }
                return imageKind;
            }

            /// <summary>
            /// Gets the location where the language element associated with the drop
            /// down entry begins.
            /// </summary>
            public SourceLocation Start {
                get {
                    ClassDefinition klass = Body as ClassDefinition;
                    if (klass != null) {
                        return klass.GetStart(klass.GlobalParent);
                    }

                    FunctionDefinition func = Body as FunctionDefinition;
                    if (func != null) {
                        return func.GetStart(func.GlobalParent);
                    }

                    return SourceLocation.None;
                }
            }

            /// <summary>
            /// Gets the location where the language element associated with the
            /// drop down ends.
            /// </summary>
            public SourceLocation End {
                get {
                    ClassDefinition klass = Body as ClassDefinition;
                    if (klass != null) {
                        return klass.GetEnd(klass.GlobalParent);
                    }

                    FunctionDefinition func = Body as FunctionDefinition;
                    if (func != null) {
                        return func.GetEnd(func.GlobalParent);
                    }

                    return SourceLocation.None;
                }
            }
        }

        /// <summary>
        /// An enum which is synchronized with our image list for the various
        /// kinds of images which are available.  This can be combined with the 
        /// ImageListOverlay to select an image for the appropriate member type
        /// and indicate the appropiate visiblity.  These can be combined with
        /// GetImageListIndex to get the final index.
        /// 
        /// Most of these are unused as we're just using an image list shipped
        /// by the VS SDK.
        /// </summary>
        enum ImageListKind {
            Class,
            Unknown1,
            Unknown2,
            Enum,
            Unknown3,
            Lightning,
            Unknown4,
            BlueBox,
            Key,
            BlueStripe,
            ThreeDashes,
            TwoBoxes,
            Method,
            StaticMethod,
            Unknown6,
            Namespace,
            Unknown7,
            Property,
            Unknown8,
            Unknown9,
            Unknown10,
            Unknown11,
            Unknown12,
            Unknown13,
            ClassMethod
        }

        /// <summary>
        /// Indicates the overlay kind which should be used for a drop down members
        /// image.  The overlay kind typically indicates visibility.
        /// 
        /// Most of these are unused as we're just using an image list shipped
        /// by the VS SDK.
        /// </summary>
        enum ImageListOverlay {
            ImageListOverlayNone,
            ImageListOverlayLetter,
            ImageListOverlayBlue,
            ImageListOverlayKey,
            ImageListOverlayPrivate,
            ImageListOverlayArrow,
        }

        /// <summary>
        /// Turns an image list kind / overlay into the proper index in the image list.
        /// </summary>
        private static int GetImageListIndex(ImageListKind kind, ImageListOverlay overlay) {
            return ((int)kind) * 6 + (int)overlay;
        }

        /// <summary>
        /// Reads our image list from our DLLs resource stream.
        /// </summary>
        private IntPtr GetImageList() {
            if (_imageList == IntPtr.Zero) {
                var shell = _serviceProvider.GetService(typeof(SVsShell)) as IVsShell;
                if (shell != null) {
                    object obj;
                    var hr = shell.GetProperty((int)__VSSPROPID.VSSPROPID_ObjectMgrTypesImgList, out obj);
                    if (ErrorHandler.Succeeded(hr) && obj != null) {
                        _imageList = (IntPtr)(int)obj;
                    }
                }
            }
            return _imageList;
        }

        /// <summary>
        /// Helper function for calculating all of the drop down entries that are available
        /// in the given suite statement.  Called to calculate both the members of top-level
        /// code and class bodies.
        /// </summary>
        private static ReadOnlyCollection<DropDownEntryInfo> CalculateEntries(SuiteStatement suite) {
            List<DropDownEntryInfo> newEntries = new List<DropDownEntryInfo>();

            if (suite != null) {
                foreach (Statement stmt in suite.Statements) {
                    if (stmt is ClassDefinition || stmt is FunctionDefinition) {
                        newEntries.Add(new DropDownEntryInfo(stmt));
                    }
                }
            }

            newEntries.Sort(ComparisonFunction);
            return new ReadOnlyCollection<DropDownEntryInfo>(newEntries);
        }

        private static int ComparisonFunction(DropDownEntryInfo x, DropDownEntryInfo y) {
            return CompletionComparer.UnderscoresLast.Compare(x.Name, y.Name);
        }

        /// <summary>
        /// Calculates the members of the drop down for top-level members.
        /// </summary>
        private void CalculateTopLevelEntries() {
            PythonAst ast = _projectEntry.Tree;
            if (ast != null) {
                _topLevelEntries = CalculateEntries(ast.Body as SuiteStatement);
            }
        }

        /// <summary>
        /// Calculates the members of the drop down for nested members
        /// based upon the currently selected top-level member.
        /// </summary>
        private void CalculateNestedEntries() {
            var entries = _topLevelEntries;
            int topLevelIndex = _curTopLevelIndex;
            if (entries.Count == 0) {
                _nestedEntries = EmptyEntries;
            } else if (topLevelIndex < entries.Count) {
                var info = entries[topLevelIndex == -1 ? 0 : topLevelIndex];

                ClassDefinition klass = info.Body as ClassDefinition;
                if (klass != null) {
                    _nestedEntries = CalculateEntries(klass.Body as SuiteStatement);
                } else {
                    _nestedEntries = EmptyEntries;
                }
            }            
        }

        #endregion

        #region Implementation Details

        /// <summary>
        /// Wired to parser event for when the parser has completed parsing a new tree and we need
        /// to update the navigation bar with the new data.
        /// </summary>
        private void ParserOnNewParseTree(object sender, EventArgs e) {
            var dropDownBar = _dropDownBar;
            if (dropDownBar != null) {
                _curNestedIndex = -1;
                _curTopLevelIndex = -1;
                Action callback = () => {
                    CalculateTopLevelEntries();
                    CaretPositionChanged(this, new CaretPositionChangedEventArgs(null, _textView.Caret.Position, _textView.Caret.Position));
                };
                _dispatcher.BeginInvoke(callback, DispatcherPriority.Background);
            }
        }

        /// <summary>
        /// Moves the caret to the specified index in the current snapshot.  Then updates the view port
        /// so that caret will be centered.  Finally moves focus to the text view so the user can 
        /// continue typing.
        /// </summary>
        private void CenterAndFocus(int index) {
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextBuffer.CurrentSnapshot, index));

            _textView.ViewScroller.EnsureSpanVisible(
                new SnapshotSpan(_textView.TextBuffer.CurrentSnapshot, index, 1),
                EnsureSpanVisibleOptions.AlwaysCenter
            );

            ((System.Windows.Controls.Control)_textView).Focus();
        }

        #endregion

        internal void UpdateProjectEntry(IProjectEntry newEntry) {
            if (newEntry is IPythonProjectEntry) {
                _projectEntry.OnNewParseTree -= ParserOnNewParseTree;
                _projectEntry = (IPythonProjectEntry)newEntry;
                _projectEntry.OnNewParseTree += ParserOnNewParseTree;
            }
        }
    }
}

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
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudioTools;
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
    class DropDownBarClient : IVsDropdownBarClient, IPythonTextBufferInfoEventSink {
        private readonly Dispatcher _dispatcher;                        // current dispatcher so we can get back to our thread
        private readonly PythonEditorServices _services;
        private ITextView _textView;                                    // text view we're drop downs for
        private IVsDropdownBar _dropDownBar;                            // drop down bar - used to refresh when changes occur
        private NavigationInfo _navigations;
        private readonly object _navigationsLock = new object();
        private readonly IServiceProvider _serviceProvider;
        private readonly UIThreadBase _uiThread;
        private IntPtr _imageList;

        private const int NavigationLevels = 2;
        private int[] _curSelection = new int[NavigationLevels];

        public DropDownBarClient(IServiceProvider serviceProvider, ITextView textView) {
            Utilities.ArgumentNotNull(nameof(serviceProvider), serviceProvider);
            Utilities.ArgumentNotNull(nameof(textView), textView);

            _serviceProvider = serviceProvider;
            _uiThread = _serviceProvider.GetUIThread();
            _services = _serviceProvider.GetComponentModel().GetService<PythonEditorServices>();
            _textView = textView;
            _dispatcher = Dispatcher.CurrentDispatcher;
            _textView.Caret.PositionChanged += CaretPositionChanged;
            foreach (var tb in PythonTextBufferInfo.GetAllFromView(textView)) {
                tb.AddSink(this, this);
            }
            textView.BufferGraph.GraphBuffersChanged += BufferGraph_GraphBuffersChanged;
            for (int i = 0; i < NavigationLevels; i++) {
                _curSelection[i] = -1;
            }
        }

        private void BufferGraph_GraphBuffersChanged(object sender, VisualStudio.Text.Projection.GraphBuffersChangedEventArgs e) {
            foreach (var b in e.RemovedBuffers) {
                PythonTextBufferInfo.TryGetForBuffer(b)?.RemoveSink(typeof(DropDownBarClient));
            }
            foreach (var b in e.AddedBuffers) {
                _services.GetBufferInfo(b).AddSink(typeof(DropDownBarClient), this);
            }
        }

        internal int Register(IVsDropdownBarManager manager) {
            IVsDropdownBar dropDownBar;
            int hr = manager.GetDropdownBar(out dropDownBar);
            if (ErrorHandler.Succeeded(hr) && dropDownBar != null) {
                hr = manager.RemoveDropdownBar();
                if (!ErrorHandler.Succeeded(hr)) {
                    return hr;
                }
            }

            int res = manager.AddDropdownBar(2, this);
            if (ErrorHandler.Succeeded(res)) {
                // A buffer may have multiple DropDownBarClients, given one may
                // open multiple CodeWindows over a single buffer using
                // Window/New Window
                var clients = _textView.Properties.GetOrCreateSingletonProperty(
                    typeof(DropDownBarClient),
                    () => new List<DropDownBarClient>()
                );
                clients.Add(this);
            }

            return res;
        }

        internal int Unregister(IVsDropdownBarManager manager) {
            _textView.Caret.PositionChanged -= CaretPositionChanged;

            // A buffer may have multiple DropDownBarClients, given one may open multiple CodeWindows
            // over a single buffer using Window/New Window
            List<DropDownBarClient> clients;
            if (_textView.Properties.TryGetProperty(typeof(DropDownBarClient), out clients)) {
                clients.Remove(this);
                if (clients.Count == 0) {
                    _textView.Properties.RemoveProperty(typeof(DropDownBarClient));
                }
            }
            foreach (var tb in PythonTextBufferInfo.GetAllFromView(_textView)) {
                tb.RemoveSink(this);
            }
#if DEBUG
            IVsDropdownBar existing;
            IVsDropdownBarClient existingClient;
            if (ErrorHandler.Succeeded(manager.GetDropdownBar(out existing)) &&
                ErrorHandler.Succeeded(existing.GetClient(out existingClient))) {
                Debug.Assert(existingClient == this, "Unregistering the wrong dropdown client");
            }
#endif

            return manager.RemoveDropdownBar();
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
            var navigation = GetNavigation(iCombo);
            if (navigation == null || navigation.Children == null) {
                pcEntries = 0;
            } else {
                pcEntries = (uint)navigation.Children.Length;
            }

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

            lock (_navigationsLock) {
                if (iIndex == 0) {
                    var cur = GetCurrentNavigation(iCombo);
                    if (cur == null) {
                        pAttr = (uint)DROPDOWNFONTATTR.FONTATTR_GRAY;
                    }
                }
            }

            return VSConstants.S_OK;
        }

        private NavigationInfo GetNewNavigation(int depth, int index) {
            var path = new int[depth + 1];
            Array.Copy(_curSelection, path, depth + 1);
            path[depth] = index;
            return GetNavigationInfo(path);
        }

        private NavigationInfo GetCurrentNavigation(int depth) {
            var path = new int[depth + 1];
            Array.Copy(_curSelection, path, depth + 1);
            return GetNavigationInfo(path);
        }

        private NavigationInfo GetNavigation(int depth) {
            var path = new int[depth];
            Array.Copy(_curSelection, path, depth);
            return GetNavigationInfo(path);
        }

        private NavigationInfo GetNavigationInfo(params int[] path) {
            lock (_navigationsLock) {
                var cur = _navigations;
                for (int i = 0; i < path.Length && cur != null; i++) {
                    int p = path[i];
                    if (p < 0 || p >= cur.Children.Length) {
                        return null;
                    }
                    cur = cur.Children[p];
                }
                return cur;
            }
        }

        /// <summary>
        /// Gets the image which is associated with the given index for the
        /// given combo box.
        /// </summary>
        public int GetEntryImage(int iCombo, int iIndex, out int piImageIndex) {
            piImageIndex = 0;

            var curNav = GetNavigation(iCombo);
            if (curNav != null && iIndex < curNav.Children.Length) {
                var child = curNav.Children[iIndex];

                ImageListOverlay overlay = ImageListOverlay.ImageListOverlayNone;
                string name = child.Name;
                if (name != null && name.StartsWithOrdinal("_") &&
                    !(name.StartsWithOrdinal("__") && name.EndsWithOrdinal("__"))) {
                    overlay = ImageListOverlay.ImageListOverlayPrivate;
                }

                ImageListKind kind;
                switch (child.Kind) {
                    case NavigationKind.Class: kind = ImageListKind.Class; break;
                    case NavigationKind.Function: kind = ImageListKind.Method; break;
                    case NavigationKind.ClassMethod: kind = ImageListKind.ClassMethod; break;
                    case NavigationKind.Property: kind = ImageListKind.Property; break;
                    case NavigationKind.StaticMethod: kind = ImageListKind.StaticMethod; break;
                    default: kind = ImageListKind.ThreeDashes; break;
                }

                piImageIndex = GetImageListIndex(kind, overlay);
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Gets the text which is displayed for the given index for the
        /// given combo box.
        /// </summary>
        public int GetEntryText(int iCombo, int iIndex, out string ppszText) {
            ppszText = String.Empty;

            var curNav = GetNavigation(iCombo);
            if (curNav != null && iIndex < curNav.Children.Length) {
                var child = curNav.Children[iIndex];
                ppszText = child.Name;
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

            int oldIndex = _curSelection[iCombo];
            var newNavigation = GetNewNavigation(iCombo, iIndex);
            _curSelection[iCombo] = iIndex;

            if (newNavigation != null) {
                if (oldIndex == -1) {
                    _dropDownBar.RefreshCombo(iCombo, iIndex);
                }
                CenterAndFocus(newNavigation.Span.Start);
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

            List<KeyValuePair<int, int>> changes = new List<KeyValuePair<int, int>>();
            lock (_navigationsLock) {
                var cur = _navigations;
                for (int level = 0; level < _curSelection.Length; level++) {
                    if (cur == null) {
                        // no valid children, we'll invalidate these...
                        changes.Add(new KeyValuePair<int, int>(_curSelection[level], -1));
                        continue;
                    }

                    bool found = false;
                    if (cur.Children != null) {
                        for (int i = 0; i < cur.Children.Length; i++) {
                            if (newPosition >= cur.Children[i].Span.Start && newPosition <= cur.Children[i].Span.End) {
                                changes.Add(new KeyValuePair<int, int>(_curSelection[level], i));
                                _curSelection[level] = i;
                                cur = cur.Children[i];
                                found = true;
                                break;
                            }
                        }
                    }

                    if (!found) {
                        // continue processing to update the subselections...
                        cur = null;
                        changes.Add(new KeyValuePair<int, int>(_curSelection[level], -1));
                        _curSelection[level] = -1;
                    }
                }
            }

            for (int i = 0; i < changes.Count; i++) {
                var change = changes[i];
                var oldValue = change.Key;
                var newValue = change.Value;

                if (_dropDownBar != null && oldValue != newValue) {
                    if (oldValue == -1 || newValue == -1) {
                        // we've selected something new, we need to refresh the combo to
                        // to remove the grayed out entry
                        _dropDownBar.RefreshCombo(i, newValue);
                    } else {
                        // changing from one top-level to another, just update the selection
                        _dropDownBar.SetCurrentSelection(i, newValue);
                    }
                }
            }
        }

        #endregion

        #region Entry Calculation

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

        #endregion

        #region Implementation Details

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

        /// <summary>
        /// Wired to parser event for when the parser has completed parsing a new tree and we need
        /// to update the navigation bar with the new data.
        /// </summary>
        async Task IPythonTextBufferInfoEventSink.PythonTextBufferEventAsync(PythonTextBufferInfo sender, PythonTextBufferInfoEventArgs e) {
            if (e.Event == PythonTextBufferInfoEvents.NewParseTree) {
                AnalysisEntry analysisEntry = e.AnalysisEntry;
                await RefreshNavigationsFromAnalysisEntry(analysisEntry);
            }
        }

        internal async Task RefreshNavigationsFromAnalysisEntry(AnalysisEntry analysisEntry) {
            var dropDownBar = _dropDownBar;
            if (dropDownBar == null) {
                return;
            }

            var navigations = await _uiThread.InvokeTask(() => analysisEntry.Analyzer.GetNavigationsAsync(_textView.TextSnapshot));
            lock (_navigationsLock) {
                _navigations = navigations;
                for (int i = 0; i < _curSelection.Length; i++) {
                    _curSelection[i] = -1;
                }
            }

            Action callback = () => CaretPositionChanged(
                this,
                new CaretPositionChangedEventArgs(
                    _textView,
                    _textView.Caret.Position,
                    _textView.Caret.Position
                )
            );

            try {
                await _dispatcher.BeginInvoke(callback, DispatcherPriority.Background);
            } catch (TaskCanceledException) {
            }
        }

        #endregion
    }
}

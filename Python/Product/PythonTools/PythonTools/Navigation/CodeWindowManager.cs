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
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Language;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Navigation {
    class CodeWindowManager : IVsCodeWindowManager, IVsCodeWindowEvents {
        private readonly IVsCodeWindow _window;
        private readonly ITextBuffer _textBuffer;
        private static readonly HashSet<CodeWindowManager> _windows = new HashSet<CodeWindowManager>();
        private uint _cookieVsCodeWindowEvents;
        private DropDownBarClient _client;
        private static IVsEditorAdaptersFactoryService _vsEditorAdaptersFactoryService = null;

        static CodeWindowManager() {
            PythonToolsPackage.Instance.OnIdle += OnIdle;
        }

        public CodeWindowManager(IVsCodeWindow codeWindow, IWpfTextView textView) {
            _window = codeWindow;
            _textBuffer = textView.TextBuffer;
        }

        private static void OnIdle(object sender, ComponentManagerEventArgs e) {
            foreach (var window in _windows) {
                if (e.ComponentManager.FContinueIdle() == 0) {
                    break;
                }

                IVsTextView vsTextView;
                if (ErrorHandler.Succeeded(window._window.GetLastActiveView(out vsTextView)) && vsTextView != null) {
                    var wpfTextView = VsEditorAdaptersFactoryService.GetWpfTextView(vsTextView);
                    if (wpfTextView != null) {
                        EditFilter editFilter;
                        if (wpfTextView.Properties.TryGetProperty(typeof(EditFilter), out editFilter) && editFilter != null) {
                            editFilter.DoIdle(e.ComponentManager);
                        }
                    }
                }
            }
        }

        #region IVsCodeWindowManager Members

        public int AddAdornments() {
            _windows.Add(this);

            IVsTextView textView;

            if (ErrorHandler.Succeeded(_window.GetPrimaryView(out textView))) {
                ((IVsCodeWindowEvents)this).OnNewView(textView);
            }

            if (ErrorHandler.Succeeded(_window.GetSecondaryView(out textView))) {
                ((IVsCodeWindowEvents)this).OnNewView(textView);
            }

            if (PythonToolsPackage.Instance.LangPrefs.NavigationBar) {
                return AddDropDownBar();
            }

            return VSConstants.S_OK;
        }

        private int AddDropDownBar() {
            var cpc = (IConnectionPointContainer)_window;
            if (cpc != null) {
                IConnectionPoint cp;
                cpc.FindConnectionPoint(typeof(IVsCodeWindowEvents).GUID, out cp);
                if (cp != null) {
                    cp.Advise(this, out _cookieVsCodeWindowEvents);
                }
            }

            var pythonProjectEntry = _textBuffer.GetAnalysis() as IPythonProjectEntry;
            if (pythonProjectEntry == null) {
                return VSConstants.E_FAIL;
            }

            IWpfTextView wpfTextView = null;
            IVsTextView vsTextView;
            if (ErrorHandler.Succeeded(_window.GetLastActiveView(out vsTextView)) && vsTextView != null) {
                wpfTextView = VsEditorAdaptersFactoryService.GetWpfTextView(vsTextView);
            }
            if (wpfTextView == null) {
                return VSConstants.E_FAIL;
            }

            _client = new DropDownBarClient(wpfTextView, pythonProjectEntry);
            
            IVsDropdownBarManager manager = (IVsDropdownBarManager)_window;

            IVsDropdownBar dropDownBar;
            int hr = manager.GetDropdownBar(out dropDownBar);
            if (ErrorHandler.Succeeded(hr) && dropDownBar != null) {
                hr = manager.RemoveDropdownBar();
                if (!ErrorHandler.Succeeded(hr)) {
                    return hr;
                }
            }

            int res = manager.AddDropdownBar(2, _client);
            if (ErrorHandler.Succeeded(res)) {
                // A buffer may have multiple DropDownBarClients, given one may open multiple CodeWindows
                // over a single buffer using Window/New Window
                List<DropDownBarClient> listDropDownBarClient;
                if (!_textBuffer.Properties.TryGetProperty(typeof(DropDownBarClient), out listDropDownBarClient) || listDropDownBarClient == null) {
                    listDropDownBarClient = new List<DropDownBarClient>();
                    _textBuffer.Properties[typeof(DropDownBarClient)] = listDropDownBarClient;
                }
                listDropDownBarClient.Add(_client);
            }
            return res;
        }

        private int RemoveDropDownBar() {
            var cpc = (IConnectionPointContainer)_window;
            if (cpc != null) {
                IConnectionPoint cp;
                cpc.FindConnectionPoint(typeof(IVsCodeWindowEvents).GUID, out cp);
                if (cp != null) {
                    cp.Unadvise(_cookieVsCodeWindowEvents);
                }
            }

            if (_client != null) {
                IVsDropdownBarManager manager = (IVsDropdownBarManager)_window;
                _client.Unregister();
                // A buffer may have multiple DropDownBarClients, given one may open multiple CodeWindows
                // over a single buffer using Window/New Window
                List<DropDownBarClient> listDropDownBarClient;
                if (_textBuffer.Properties.TryGetProperty(typeof(DropDownBarClient), out listDropDownBarClient) && listDropDownBarClient != null) {
                    listDropDownBarClient.Remove(_client);
                    if (listDropDownBarClient.Count == 0) {
                        _textBuffer.Properties.RemoveProperty(typeof(DropDownBarClient));
                    }
                }
                _client = null;
                return manager.RemoveDropdownBar();
            }
            return VSConstants.S_OK;
        }

        public int OnNewView(IVsTextView pView) {
            // NO-OP We use IVsCodeWindowEvents to track text view lifetime
            return VSConstants.S_OK;
        }
            
        public int RemoveAdornments() {
            _windows.Remove(this);

            IVsTextView textView;

            if (ErrorHandler.Succeeded(_window.GetPrimaryView(out textView))) {
                ((IVsCodeWindowEvents)this).OnCloseView(textView);
            }

            if (ErrorHandler.Succeeded(_window.GetSecondaryView(out textView))) {
                ((IVsCodeWindowEvents)this).OnCloseView(textView);
            }

            return RemoveDropDownBar();
        }

        public static void ToggleNavigationBar(bool fEnable) {
            foreach (var window in _windows) {
                if (fEnable) {
                    ErrorHandler.ThrowOnFailure(window.AddDropDownBar());
                } else {
                    ErrorHandler.ThrowOnFailure(window.RemoveDropDownBar());
                }
            }
        }

        #endregion

        #region IVsCodeWindowEvents Members

        int IVsCodeWindowEvents.OnNewView(IVsTextView vsTextView) {
            var wpfTextView = VsEditorAdaptersFactoryService.GetWpfTextView(vsTextView);
            if (wpfTextView != null) {
                var factory = PythonToolsPackage.ComponentModel.GetService<IEditorOperationsFactoryService>();
                var editFilter = new EditFilter(wpfTextView, factory.GetEditorOperations(wpfTextView));
                editFilter.AttachKeyboardFilter(vsTextView);
#if DEV11_OR_LATER
                var viewFilter = new TextViewFilter();
                viewFilter.AttachFilter(vsTextView);
#endif
                wpfTextView.GotAggregateFocus += OnTextViewGotAggregateFocus;
            }
            return VSConstants.S_OK;
        }

        int IVsCodeWindowEvents.OnCloseView(IVsTextView vsTextView) {
            var wpfTextView = VsEditorAdaptersFactoryService.GetWpfTextView(vsTextView);
            if (wpfTextView != null) {
                wpfTextView.GotAggregateFocus -= OnTextViewGotAggregateFocus;
            }
            return VSConstants.S_OK;
        }

        private void OnTextViewGotAggregateFocus(object sender, EventArgs e) {
            var wpfTextView = sender as IWpfTextView;
            if (wpfTextView != null) {
                if (_client != null) {
                    _client.UpdateView(wpfTextView);
                }
            }
        }

        private static IVsEditorAdaptersFactoryService VsEditorAdaptersFactoryService {
            get {
                if (_vsEditorAdaptersFactoryService == null) {
                    _vsEditorAdaptersFactoryService = PythonToolsPackage.ComponentModel.GetService<IVsEditorAdaptersFactoryService>();
                }
                return _vsEditorAdaptersFactoryService;
            }
        }

        #endregion
    }
}

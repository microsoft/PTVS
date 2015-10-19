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
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Language;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudioTools;
using IServiceProvider = System.IServiceProvider;

namespace Microsoft.PythonTools.Navigation {
    class CodeWindowManager : IVsCodeWindowManager, IVsCodeWindowEvents {
        private readonly IServiceProvider _serviceProvider;
        private readonly IVsCodeWindow _window;
        private IWpfTextView _curView;
        private readonly PythonToolsService _pyService;
        private uint _cookieVsCodeWindowEvents;
        private DropDownBarClient _client;
        private IVsEditorAdaptersFactoryService _vsEditorAdaptersFactoryService;

        public CodeWindowManager(IServiceProvider serviceProvider, IVsCodeWindow codeWindow) {
            _serviceProvider = serviceProvider;
            _window = codeWindow;
            _pyService = _serviceProvider.GetPythonToolsService();
        }
        
        #region IVsCodeWindowManager Members

        public int AddAdornments() {
            IVsTextView textView;

            if (ErrorHandler.Succeeded(_window.GetPrimaryView(out textView))) {
                ((IVsCodeWindowEvents)this).OnNewView(textView);
            }

            if (ErrorHandler.Succeeded(_window.GetSecondaryView(out textView))) {
                ((IVsCodeWindowEvents)this).OnNewView(textView);
            }

            if (_pyService.LangPrefs.NavigationBar) {
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

            IWpfTextView wpfTextView = null;
            IVsTextView vsTextView;
            if (ErrorHandler.Succeeded(_window.GetLastActiveView(out vsTextView)) && vsTextView != null) {
                wpfTextView = VsEditorAdaptersFactoryService.GetWpfTextView(vsTextView);
            }
            if (wpfTextView == null) {
                return VSConstants.E_FAIL;
            }

            IPythonProjectEntry pythonProjectEntry;
            if (!wpfTextView.TextBuffer.TryGetPythonProjectEntry(out pythonProjectEntry)) {
                return VSConstants.E_FAIL;
            }

            _client = new DropDownBarClient(_serviceProvider, wpfTextView, pythonProjectEntry);
            return _client.Register((IVsDropdownBarManager)_window);
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
                return _client.Unregister((IVsDropdownBarManager)_window);
            }
            return VSConstants.S_OK;
        }

        public int OnNewView(IVsTextView pView) {
            // NO-OP We use IVsCodeWindowEvents to track text view lifetime
            return VSConstants.S_OK;
        }
            
        public int RemoveAdornments() {
            IVsTextView textView;

            if (ErrorHandler.Succeeded(_window.GetPrimaryView(out textView))) {
                ((IVsCodeWindowEvents)this).OnCloseView(textView);
            }

            if (ErrorHandler.Succeeded(_window.GetSecondaryView(out textView))) {
                ((IVsCodeWindowEvents)this).OnCloseView(textView);
            }

            return RemoveDropDownBar();
        }

        public int ToggleNavigationBar(bool fEnable) {
            return fEnable ? AddDropDownBar() : RemoveDropDownBar();
        }

        #endregion

        #region IVsCodeWindowEvents Members

        int IVsCodeWindowEvents.OnNewView(IVsTextView vsTextView) {
            var wpfTextView = VsEditorAdaptersFactoryService.GetWpfTextView(vsTextView);
            if (wpfTextView != null) {
                var factory = ComponentModel.GetService<IEditorOperationsFactoryService>();
                var editFilter = new EditFilter(wpfTextView, factory.GetEditorOperations(wpfTextView), _serviceProvider);
                editFilter.AttachKeyboardFilter(vsTextView);
                new TextViewFilter(_serviceProvider, vsTextView);
                wpfTextView.GotAggregateFocus += OnTextViewGotAggregateFocus;
                wpfTextView.LostAggregateFocus += OnTextViewLostAggregateFocus;
            }
            return VSConstants.S_OK;
        }

        int IVsCodeWindowEvents.OnCloseView(IVsTextView vsTextView) {
            var wpfTextView = VsEditorAdaptersFactoryService.GetWpfTextView(vsTextView);
            if (wpfTextView != null) {
                wpfTextView.GotAggregateFocus -= OnTextViewGotAggregateFocus;
                wpfTextView.LostAggregateFocus -= OnTextViewLostAggregateFocus;
            }
            return VSConstants.S_OK;
        }

        private void OnTextViewGotAggregateFocus(object sender, EventArgs e) {
            var wpfTextView = sender as IWpfTextView;
            if (wpfTextView != null) {
                _curView = wpfTextView;
                if (_client != null) {
                    _client.UpdateView(wpfTextView);
                }
                _pyService.OnIdle += OnIdle;
            }
        }

        private void OnTextViewLostAggregateFocus(object sender, EventArgs e) {
            var wpfTextView = sender as IWpfTextView;
            if (wpfTextView != null) {
                _curView = null;
                _pyService.OnIdle -= OnIdle;
            }
        }

        private IComponentModel ComponentModel {
            get {
                return (IComponentModel)_serviceProvider.GetService(typeof(SComponentModel));
            }
        }

        private IVsEditorAdaptersFactoryService VsEditorAdaptersFactoryService {
            get {
                if (_vsEditorAdaptersFactoryService == null) {
                    _vsEditorAdaptersFactoryService = ComponentModel.GetService<IVsEditorAdaptersFactoryService>();
                }
                return _vsEditorAdaptersFactoryService;
            }
        }

        #endregion

        private void OnIdle(object sender, ComponentManagerEventArgs eventArgs) {
            if (_curView!= null) {
                EditFilter editFilter;
                if (_curView.Properties.TryGetProperty(typeof(EditFilter), out editFilter) && editFilter != null) {
                    editFilter.DoIdle((IOleComponentManager)_serviceProvider.GetService(typeof(SOleComponentManager)));
                }
            }
        }
    }
}

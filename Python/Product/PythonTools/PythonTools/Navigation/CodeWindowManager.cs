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
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Language;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudioTools;
using IServiceProvider = System.IServiceProvider;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Navigation {
    class CodeWindowManager : IVsCodeWindowManager, IVsCodeWindowEvents {
        private readonly IServiceProvider _serviceProvider;
        private readonly IVsCodeWindow _window;
        private IWpfTextView _curView;
        private readonly PythonToolsService _pyService;
        private uint _cookieVsCodeWindowEvents;
        private DropDownBarClient _client;
        private int _viewCount;
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

            AddDropDownBarAsync(_pyService).SilenceException<OperationCanceledException>().DoNotWait();

            return VSConstants.S_OK;
        }

        private async Task AddDropDownBarAsync(PythonToolsService service) {
            var prefs = await _pyService.GetLangPrefsAsync();
            if (prefs.NavigationBar) {
                AddDropDownBar();
            }
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

            AnalysisEntry entry;
            var entryService = _serviceProvider.GetEntryService();
            if (entryService == null || !entryService.TryGetAnalysisEntry(wpfTextView, out entry)) {
                return VSConstants.E_FAIL;
            }

            _client = new DropDownBarClient(_serviceProvider, wpfTextView, entry);
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
            _viewCount++;
            var wpfTextView = VsEditorAdaptersFactoryService.GetWpfTextView(vsTextView);
            if (wpfTextView != null) {
                var factory = ComponentModel.GetService<IEditorOperationsFactoryService>();
                EditFilter.GetOrCreate(_serviceProvider, ComponentModel, vsTextView);
                new TextViewFilter(_serviceProvider, vsTextView);
                wpfTextView.GotAggregateFocus += OnTextViewGotAggregateFocus;
                wpfTextView.LostAggregateFocus += OnTextViewLostAggregateFocus;
            }
            return VSConstants.S_OK;
        }

        int IVsCodeWindowEvents.OnCloseView(IVsTextView vsTextView) {
            _viewCount--;
            if (_viewCount == 0) {
                _pyService.CodeWindowClosed(_window);
            }
            _pyService.OnIdle -= OnIdle;
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

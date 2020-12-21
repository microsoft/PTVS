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
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Microsoft.PythonTools.Editor {
    class LanguagePreferences : IVsTextManagerEvents2, IDisposable {
        private readonly PythonToolsService _service;
        private readonly IVsTextManager _textMgr;
        private readonly uint _cookie;
        private LANGPREFERENCES _preferences;
        private bool _isDisposed;

        public LanguagePreferences(PythonToolsService service, Guid languageGuid) {
            _service = service;
            _service.Site.AssertShellIsInitialized();

            _textMgr = (IVsTextManager)service.Site.GetService(typeof(SVsTextManager));
            if (_textMgr == null) {
                throw new NotSupportedException("");
            }

            var langPrefs = new LANGPREFERENCES[1];
            langPrefs[0].guidLang = languageGuid;
            ErrorHandler.ThrowOnFailure(_textMgr.GetUserPreferences(null, null, langPrefs, null));
            _preferences = langPrefs[0];

            var guid = typeof(IVsTextManagerEvents2).GUID;
            IConnectionPoint connectionPoint = null;
            (_textMgr as IConnectionPointContainer)?.FindConnectionPoint(ref guid, out connectionPoint);
            if (connectionPoint != null) {
                connectionPoint.Advise(this, out _cookie);
            }
        }

        ~LanguagePreferences() {
            Dispose(false);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing) {
            if (_isDisposed) {
                return;
            }

            if (disposing) {
                if (_cookie != 0 && _textMgr != null) {
                    Guid guid = typeof(IVsTextManagerEvents2).GUID;
                    IConnectionPoint connectionPoint;
                    (_textMgr as IConnectionPointContainer).FindConnectionPoint(ref guid, out connectionPoint);
                    connectionPoint.Unadvise(_cookie);
                }
            }

            _isDisposed = true;
        }

        #region IVsTextManagerEvents2 Members

        public int OnRegisterMarkerType(int iMarkerType) {
            return VSConstants.S_OK;
        }

        public int OnRegisterView(IVsTextView pView) {
            return VSConstants.S_OK;
        }

        public int OnReplaceAllInFilesBegin() {
            return VSConstants.S_OK;
        }

        public int OnReplaceAllInFilesEnd() {
            return VSConstants.S_OK;
        }

        public int OnUnregisterView(IVsTextView pView) {
            return VSConstants.S_OK;
        }

        public int OnUserPreferencesChanged2(VIEWPREFERENCES2[] viewPrefs, FRAMEPREFERENCES2[] framePrefs, LANGPREFERENCES2[] langPrefs, FONTCOLORPREFERENCES2[] colorPrefs) {
            int hr = VSConstants.S_OK;
            if (langPrefs != null && langPrefs.Length > 0 && langPrefs[0].guidLang == this._preferences.guidLang) {
                _preferences.IndentStyle = langPrefs[0].IndentStyle;
                _preferences.fAutoListMembers = langPrefs[0].fAutoListMembers;
                _preferences.fAutoListParams = langPrefs[0].fAutoListParams;
                _preferences.fHideAdvancedAutoListMembers = langPrefs[0].fHideAdvancedAutoListMembers;
                if (_preferences.fDropdownBar != (_preferences.fDropdownBar = langPrefs[0].fDropdownBar)) {
                    foreach (var window in _service.CodeWindowManagers) {
                        hr = window.ToggleNavigationBar(_preferences.fDropdownBar != 0);
                        if (ErrorHandler.Failed(hr)) {
                            break;
                        }
                    }
                }
            }
            return VSConstants.S_OK;
        }

        #endregion

        #region Options

        public vsIndentStyle IndentMode {
            get {
                return _preferences.IndentStyle;
            }
        }

        public bool NavigationBar {
            get {
                // TODO: When this value changes we need to update all our views
                return _preferences.fDropdownBar != 0;
            }
        }

        public bool HideAdvancedMembers {
            get {
                return _preferences.fHideAdvancedAutoListMembers != 0;
            }
        }

        public bool AutoListMembers {
            get {
                return _preferences.fAutoListMembers != 0;
            }
        }

        public bool AutoListParams {
            get {
                return _preferences.fAutoListParams != 0;
            }
        }


        #endregion
    }
}

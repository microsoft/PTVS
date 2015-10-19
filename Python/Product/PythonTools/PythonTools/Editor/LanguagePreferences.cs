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
using Microsoft.PythonTools.Navigation;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Microsoft.PythonTools.Editor {
    class LanguagePreferences : IVsTextManagerEvents2 {
        PythonToolsService _service;
        LANGPREFERENCES _preferences;

        public LanguagePreferences(PythonToolsService service, LANGPREFERENCES preferences) {
            _preferences = preferences;
            _service = service;
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
                    foreach(var window in _service.CodeWindowManagers) {
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

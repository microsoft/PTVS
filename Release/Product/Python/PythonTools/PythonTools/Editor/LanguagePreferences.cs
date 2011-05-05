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

using Microsoft.PythonTools.Navigation;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Microsoft.PythonTools.Editor {
    class LanguagePreferences : IVsTextManagerEvents2 {
        LANGPREFERENCES _preferences;

        public LanguagePreferences(LANGPREFERENCES preferences) {
            _preferences = preferences;
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

        public int OnUserPreferencesChanged2(VIEWPREFERENCES2[] pViewPrefs, FRAMEPREFERENCES2[] pFramePrefs, LANGPREFERENCES2[] pLangPrefs, FONTCOLORPREFERENCES2[] pColorPrefs) {
            if (pLangPrefs != null) {
                _preferences.IndentStyle = pLangPrefs[0].IndentStyle;
                _preferences.fAutoListMembers = pLangPrefs[0].fAutoListMembers;
                _preferences.fAutoListParams = pLangPrefs[0].fAutoListParams;
                _preferences.fHideAdvancedAutoListMembers = pLangPrefs[0].fHideAdvancedAutoListMembers;
                if (_preferences.fDropdownBar != (_preferences.fDropdownBar = pLangPrefs[0].fDropdownBar)) {
                    CodeWindowManager.ToggleNavigationBar(_preferences.fDropdownBar != 0);                    
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

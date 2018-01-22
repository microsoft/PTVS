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
using System.Runtime.InteropServices;

namespace Microsoft.PythonTools.Options {
    [ComVisible(true)]
    public class PythonAdvancedEditorOptionsPage : PythonDialogPage {
        private PythonAdvancedEditorOptionsControl _window;

        // replace the default UI of the dialog page w/ our own UI.
        protected override System.Windows.Forms.IWin32Window Window {
            get {
                if (_window == null) {
                    _window = new PythonAdvancedEditorOptionsControl();
                    LoadSettingsFromStorage();
                }
                return _window;
            }
        }

        [Obsolete("Use PythonToolsService.AdvancedOptions instead")]
        public bool EnterCommitsIntellisense {
            get { return PyService.AdvancedOptions.EnterCommitsIntellisense; }
            set { PyService.AdvancedOptions.EnterCommitsIntellisense = value; }
        }

        [Obsolete("Use PythonToolsService.AdvancedOptions instead")]
        public bool IntersectMembers {
            get { return PyService.AdvancedOptions.IntersectMembers; }
            set { PyService.AdvancedOptions.IntersectMembers = value; }
        }

        [Obsolete("Use PythonToolsService.AdvancedOptions instead")]
        public bool FilterCompletions {
            get { return PyService.AdvancedOptions.FilterCompletions; }
            set { PyService.AdvancedOptions.FilterCompletions = value; }
        }

        [Obsolete("Use PythonToolsService.AdvancedOptions instead")]
        public bool AddNewLineAtEndOfFullyTypedWord {
            get { return PyService.AdvancedOptions.AddNewLineAtEndOfFullyTypedWord; }
            set { PyService.AdvancedOptions.AddNewLineAtEndOfFullyTypedWord = value; }
        }

        [Obsolete("Use PythonToolsService.AdvancedOptions instead")]
        public bool EnterOutliningModeOnOpen {
            get { return PyService.AdvancedOptions.EnterOutliningModeOnOpen; }
            set { PyService.AdvancedOptions.EnterOutliningModeOnOpen = value; }
        }

        [Obsolete("Use PythonToolsService.AdvancedOptions instead")]
        public bool PasteRemovesReplPrompts {
            get { return PyService.AdvancedOptions.PasteRemovesReplPrompts; }
            set { PyService.AdvancedOptions.PasteRemovesReplPrompts = value; }
        }

        [Obsolete("Use PythonToolsService.AdvancedOptions instead")]
        public string CompletionCommittedBy { 
            get { return PyService.AdvancedOptions.CompletionCommittedBy; }
            set { PyService.AdvancedOptions.CompletionCommittedBy = value; } 
        }

        [Obsolete("Use PythonToolsService.AdvancedOptions instead")]
        public bool ColorNames {
            get { return PyService.AdvancedOptions.ColorNames; }
            set { PyService.AdvancedOptions.ColorNames = value; }
        }

        [Obsolete("Use PythonToolsService.AdvancedOptions instead")]
        public bool ColorNamesWithAnalysis {
            get { return PyService.AdvancedOptions.ColorNamesWithAnalysis; }
            set { PyService.AdvancedOptions.ColorNamesWithAnalysis = value; }
        }

        public override void ResetSettings() {
            PyService.AdvancedOptions.Reset();
        }

        public override void LoadSettingsFromStorage() {
            // Load settings from storage.
            PyService.AdvancedOptions.Load();

            // Synchronize UI with backing properties.
            if (_window != null) {
                _window.SyncControlWithPageSettings(PyService);
            }
        }

        public override void SaveSettingsToStorage() {
            // Synchronize backing properties with UI.
            if (_window != null) {
                _window.SyncPageWithControlSettings(PyService);
            }

            // Save settings.
            PyService.AdvancedOptions.Save();
        }
    }
}

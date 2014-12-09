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
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Intellisense;

namespace Microsoft.PythonTools.Options {
    [ComVisible(true)]
    public class PythonAdvancedEditorOptionsPage : PythonDialogPage {
        private PythonAdvancedEditorOptionsControl _window;
        private const string _defaultCompletionChars = "{}[]().,:;+-*/%&|^~=<>#'\"\\";

        public PythonAdvancedEditorOptionsPage()
            : base("Advanced") {
        }

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
        public FuzzyMatchMode SearchMode {
            get { return PyService.AdvancedOptions.SearchMode; }
            set { PyService.AdvancedOptions.SearchMode = value; }
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

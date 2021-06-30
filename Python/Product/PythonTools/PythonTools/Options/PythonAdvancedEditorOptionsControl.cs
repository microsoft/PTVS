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

using System.Windows.Forms;

namespace Microsoft.PythonTools.Options {
    public partial class PythonAdvancedEditorOptionsControl : UserControl {
        public PythonAdvancedEditorOptionsControl() {
            InitializeComponent();
        }

        internal void SyncControlWithPageSettings(PythonToolsService pyService) {
            _enterCommits.Checked = pyService.AdvancedOptions.EnterCommitsIntellisense;
            _intersectMembers.Checked = pyService.AdvancedOptions.IntersectMembers;
            _filterCompletions.Checked = pyService.AdvancedOptions.FilterCompletions;
            _completionCommitedBy.Text = pyService.AdvancedOptions.CompletionCommittedBy;
            _newLineAfterCompleteCompletion.Checked = pyService.AdvancedOptions.AddNewLineAtEndOfFullyTypedWord;
            _outliningOnOpen.Checked = pyService.AdvancedOptions.EnterOutliningModeOnOpen;
            _pasteRemovesReplPrompts.Checked = pyService.AdvancedOptions.PasteRemovesReplPrompts;
            _colorNames.Checked = pyService.AdvancedOptions.ColorNames;
            _autoListIdentifiers.Checked = pyService.AdvancedOptions.AutoListIdentifiers;
        }

        internal void SyncPageWithControlSettings(PythonToolsService pyService) {
            pyService.AdvancedOptions.EnterCommitsIntellisense = _enterCommits.Checked;
            pyService.AdvancedOptions.IntersectMembers = _intersectMembers.Checked;
            pyService.AdvancedOptions.FilterCompletions = _filterCompletions.Checked;
            pyService.AdvancedOptions.CompletionCommittedBy = _completionCommitedBy.Text;
            pyService.AdvancedOptions.AddNewLineAtEndOfFullyTypedWord = _newLineAfterCompleteCompletion.Checked;
            pyService.AdvancedOptions.EnterOutliningModeOnOpen = _outliningOnOpen.Checked;
            pyService.AdvancedOptions.PasteRemovesReplPrompts = _pasteRemovesReplPrompts.Checked;
            pyService.AdvancedOptions.ColorNames = _colorNames.Checked;
            pyService.AdvancedOptions.AutoListIdentifiers = _autoListIdentifiers.Checked;
        }
    }
}

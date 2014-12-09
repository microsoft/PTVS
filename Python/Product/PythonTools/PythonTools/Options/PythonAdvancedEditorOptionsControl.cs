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
        }
    }
}

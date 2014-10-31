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

        internal void SyncControlWithPageSettings(PythonAdvancedEditorOptionsPage page) {
            _enterCommits.Checked = page.EnterCommitsIntellisense;
            _intersectMembers.Checked = page.IntersectMembers;
            _filterCompletions.Checked = page.FilterCompletions;
            _completionCommitedBy.Text = page.CompletionCommittedBy;
            _newLineAfterCompleteCompletion.Checked = page.AddNewLineAtEndOfFullyTypedWord;
            _outliningOnOpen.Checked = page.EnterOutliningModeOnOpen;
            _pasteRemovesReplPrompts.Checked = page.PasteRemovesReplPrompts;
            _colorNames.Checked = page.ColorNames;
        }

        internal void SyncPageWithControlSettings(PythonAdvancedEditorOptionsPage page) {
            page.EnterCommitsIntellisense = _enterCommits.Checked;
            page.IntersectMembers = _intersectMembers.Checked;
            page.FilterCompletions = _filterCompletions.Checked;
            page.CompletionCommittedBy = _completionCommitedBy.Text;
            page.AddNewLineAtEndOfFullyTypedWord = _newLineAfterCompleteCompletion.Checked;
            page.EnterOutliningModeOnOpen = _outliningOnOpen.Checked;
            page.PasteRemovesReplPrompts = _pasteRemovesReplPrompts.Checked;
            page.ColorNames = _colorNames.Checked;
        }
    }
}

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

using System.Runtime.InteropServices;

namespace Microsoft.PythonTools.Options {
    [ComVisible(true)]
    public class PythonAdvancedEditorOptionsPage : PythonDialogPage {
        private bool _enterCommitsIntellisense, _intersectMembers, _addNewLineAtEndOfFullyTypedWord, _enterOutliningMode, _pasteRemovesReplPrompts;
        private int _fillParagraphColumns;
        private PythonAdvancedEditorOptionsControl _window;
        private string _completionCommittedBy;
        private const string _defaultCompletionChars = "{}[]().,:;+-*/%&|^~=<>#'\"\\";

        public PythonAdvancedEditorOptionsPage()
            : base("Advanced") {
        }

        // replace the default UI of the dialog page w/ our own UI.
        protected override System.Windows.Forms.IWin32Window Window {
            get {
                if (_window == null) {
                    _window = new PythonAdvancedEditorOptionsControl();
                }
                return _window;
            }
        }

        #region Intellisense Options

        public bool EnterCommitsIntellisense {
            get { return _enterCommitsIntellisense; }
            set { _enterCommitsIntellisense = value; }
        }

        public bool IntersectMembers {
            get { return _intersectMembers; }
            set {
                _intersectMembers = value;
            }
        }

        public bool AddNewLineAtEndOfFullyTypedWord {
            get { return _addNewLineAtEndOfFullyTypedWord; }
            set { _addNewLineAtEndOfFullyTypedWord = value; }
        }

        public int FillParagraphColumns {
            get { return _fillParagraphColumns; }
            set { _fillParagraphColumns = value; }
        }

        public bool EnterOutliningModeOnOpen {
            get { return _enterOutliningMode; }
            set { _enterOutliningMode = value; }
        }

        public bool PasteRemovesReplPrompts {
            get { return _pasteRemovesReplPrompts; }
            set {
                _pasteRemovesReplPrompts = value;
            }
        }

        public string CompletionCommittedBy { 
            get { return _completionCommittedBy; } 
            set { _completionCommittedBy = value; } 
        }

        #endregion

        public override void ResetSettings() {
            _enterCommitsIntellisense = true;
            _intersectMembers = true;
            _addNewLineAtEndOfFullyTypedWord = false;
            _completionCommittedBy = _defaultCompletionChars;
            _enterOutliningMode = true;
            _fillParagraphColumns = 80;
            _pasteRemovesReplPrompts = true;
        }

        private const string EnterCommitsSetting = "EnterCommits";
        private const string IntersectMembersSetting = "IntersectMembers";
        private const string NewLineAtEndOfWordSetting = "NewLineAtEndOfWord";
        private const string CompletionCommittedBySetting = "CompletionCommittedBy";
        private const string EnterOutlingModeOnOpenSetting = "EnterOutlingModeOnOpen";
        private const string FillParagraphColumnsSetting = "FillParagraphColumns";
        private const string PasteRemovesReplPromptsSetting = "PasteRemovesReplPrompts";

        public override void LoadSettingsFromStorage() {
            _enterCommitsIntellisense = LoadBool(EnterCommitsSetting) ?? true;
            _intersectMembers = LoadBool(IntersectMembersSetting) ?? true;
            _addNewLineAtEndOfFullyTypedWord = LoadBool(NewLineAtEndOfWordSetting) ?? false;
            _completionCommittedBy = LoadString("CompletionCommittedBy") ?? _defaultCompletionChars;
            _enterOutliningMode = LoadBool(EnterOutlingModeOnOpenSetting) ?? true;
            _fillParagraphColumns = LoadInt(FillParagraphColumnsSetting) ?? 80;
            _pasteRemovesReplPrompts = LoadBool(PasteRemovesReplPromptsSetting) ?? true;
        }

        public override void SaveSettingsToStorage() {
            SaveBool(EnterCommitsSetting, _enterCommitsIntellisense);
            SaveBool(IntersectMembersSetting, _intersectMembers);
            SaveBool(NewLineAtEndOfWordSetting, _addNewLineAtEndOfFullyTypedWord);
            SaveString(CompletionCommittedBySetting, _completionCommittedBy);
            SaveBool(EnterOutlingModeOnOpenSetting, _enterOutliningMode);
            SaveInt(FillParagraphColumnsSetting, _fillParagraphColumns);
            SaveBool(PasteRemovesReplPromptsSetting, _pasteRemovesReplPrompts);
        }
    }
}

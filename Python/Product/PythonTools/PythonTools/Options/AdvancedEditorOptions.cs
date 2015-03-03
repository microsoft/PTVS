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

using Microsoft.PythonTools.Intellisense;

namespace Microsoft.PythonTools.Options {
    public sealed class AdvancedEditorOptions {
        private readonly PythonToolsService _service;

        private const string Category = "Advanced";

        private const string EnterCommitsSetting = "EnterCommits";
        private const string IntersectMembersSetting = "IntersectMembers";
        private const string NewLineAtEndOfWordSetting = "NewLineAtEndOfWord";
        private const string CompletionCommittedBySetting = "CompletionCommittedBy";
        private const string EnterOutlingModeOnOpenSetting = "EnterOutlingModeOnOpen";
        private const string PasteRemovesReplPromptsSetting = "PasteRemovesReplPrompts";
        private const string FilterCompletionsSetting = "FilterCompletions";
        private const string SearchModeSetting = "SearchMode";
        private const string ColorNamesSetting = "ColorNames";
        private const string ColorNamesWithAnalysisSetting = "ColorNamesWithAnalysis";
        private const string AutoListIdentifiersSetting = "AutoListIdentifiers";

        private const string _defaultCompletionChars = "{}[]().,:;+-*/%&|^~=<>#'\"\\";

        internal AdvancedEditorOptions(PythonToolsService service) {
            _service = service;
            Load();
        }

        public void Load() {
            EnterCommitsIntellisense = _service.LoadBool(EnterCommitsSetting, Category) ?? true;
            IntersectMembers = _service.LoadBool(IntersectMembersSetting, Category) ?? false;
            AddNewLineAtEndOfFullyTypedWord = _service.LoadBool(NewLineAtEndOfWordSetting, Category) ?? false;
            CompletionCommittedBy = _service.LoadString("CompletionCommittedBy", Category) ?? _defaultCompletionChars;
            EnterOutliningModeOnOpen = _service.LoadBool(EnterOutlingModeOnOpenSetting, Category) ?? true;
            PasteRemovesReplPrompts = _service.LoadBool(PasteRemovesReplPromptsSetting, Category) ?? true;
            FilterCompletions = _service.LoadBool(FilterCompletionsSetting, Category) ?? true;
            SearchMode = _service.LoadEnum<FuzzyMatchMode>(SearchModeSetting, Category) ?? FuzzyMatchMode.Default;
            ColorNames = _service.LoadBool(ColorNamesSetting, Category) ?? true;
            ColorNamesWithAnalysis = _service.LoadBool(ColorNamesWithAnalysisSetting, Category) ?? true;
            AutoListIdentifiers = _service.LoadBool(AutoListIdentifiersSetting, Category) ?? true;
        }

        public void Save() {
            _service.SaveBool(EnterCommitsSetting, Category, EnterCommitsIntellisense);
            _service.SaveBool(IntersectMembersSetting, Category, IntersectMembers);
            _service.SaveBool(NewLineAtEndOfWordSetting, Category, AddNewLineAtEndOfFullyTypedWord);
            _service.SaveString(CompletionCommittedBySetting, Category, CompletionCommittedBy);
            _service.SaveBool(EnterOutlingModeOnOpenSetting, Category, EnterOutliningModeOnOpen);
            _service.SaveBool(PasteRemovesReplPromptsSetting, Category, PasteRemovesReplPrompts);
            _service.SaveBool(FilterCompletionsSetting, Category, FilterCompletions);
            _service.SaveEnum(SearchModeSetting, Category, SearchMode);
            _service.SaveBool(ColorNamesSetting, Category, ColorNames);
            _service.SaveBool(ColorNamesWithAnalysisSetting, Category, ColorNamesWithAnalysis);
            _service.SaveBool(AutoListIdentifiersSetting, Category, AutoListIdentifiers);
        }

        public void Reset() {
            EnterCommitsIntellisense = true;
            IntersectMembers = true;
            AddNewLineAtEndOfFullyTypedWord = false;
            CompletionCommittedBy = _defaultCompletionChars;
            EnterOutliningModeOnOpen = true;
            PasteRemovesReplPrompts = true;
            FilterCompletions = true;
            SearchMode = FuzzyMatchMode.Default;
            ColorNames = true;
            ColorNamesWithAnalysis = true;
            AutoListIdentifiers = true;
        }

        public string CompletionCommittedBy {
            get;
            set;
        }

        public bool EnterCommitsIntellisense {
            get;
            set;
        }

        public bool AddNewLineAtEndOfFullyTypedWord {
            get;
            set;
        }

        public bool FilterCompletions {
            get;
            set;
        }

        public bool IntersectMembers {
            get;
            set;
        }

        public FuzzyMatchMode SearchMode {
            get;
            set;
        }

        public bool ColorNames {
            get;
            set;
        }

        public bool ColorNamesWithAnalysis {
            get;
            set;
        }

        public bool EnterOutliningModeOnOpen {
            get;
            set;
        }

        public bool PasteRemovesReplPrompts {
            get;
            set;
        }

        public bool AutoListMembers {
            get {
                return _service.LangPrefs.AutoListMembers;
            }
            set {
                var prefs = _service.GetLanguagePreferences();
                var val = value ? 1u : 0u;
                if (prefs.fAutoListMembers != val) {
                    prefs.fAutoListMembers = val;
                    _service.SetLanguagePreferences(prefs);
                }
            }
        }

        public bool HideAdvancedMembers {
            get {
                return _service.LangPrefs.HideAdvancedMembers;
            }
            set {
                var prefs = _service.GetLanguagePreferences();
                var val = value ? 1u : 0u;
                if (prefs.fHideAdvancedAutoListMembers != val) {
                    prefs.fHideAdvancedAutoListMembers = val;
                    _service.SetLanguagePreferences(prefs);
                }
            }
        }

        public bool AutoListIdentifiers {
            get;
            set;
        }
    }
}

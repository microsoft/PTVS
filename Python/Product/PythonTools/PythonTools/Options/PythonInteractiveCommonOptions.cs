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

using Microsoft.PythonTools.Repl;

namespace Microsoft.PythonTools.Options {
    /// <summary>
    /// Stores options related to the all interactive windows.
    /// </summary>
    class PythonInteractiveCommonOptions {
        private bool _smartHistory, _interpreterPrompts, _liveCompletionsOnly;
        private ReplIntellisenseMode _replIntellisenseMode;
        private string _priPrompt, _secPrompt;

        internal readonly PythonToolsService _pyService;

        internal readonly string _category;
        internal string _id;

        private const string DefaultPrompt = ">>> ";
        private const string DefaultSecondaryPrompt = "... ";

        private const string PrimaryPromptSetting = "PrimaryPrompt";
        private const string SecondaryPromptSetting = "SecondaryPrompt";
        private const string InlinePromptsSetting = "InlinePrompts";
        private const string UseInterpreterPromptsSetting = "UseInterpreterPrompts";
        private const string ReplIntellisenseModeSetting = "InteractiveIntellisenseMode";
        private const string SmartHistorySetting = "InteractiveSmartHistory";
        private const string LiveCompletionsOnlySetting = "LiveCompletionsOnly";

        internal PythonInteractiveCommonOptions(PythonToolsService pyService, string category, string id) {
            _pyService = pyService;
            _category = category;
            _id = id;
            _priPrompt = DefaultPrompt;
            _secPrompt = DefaultSecondaryPrompt;
            _interpreterPrompts = true;
            _replIntellisenseMode = ReplIntellisenseMode.DontEvaluateCalls;
            _smartHistory = true;
        }

        public string PrimaryPrompt {
            get { return _priPrompt; }
            set { _priPrompt = value; }
        }

        public string SecondaryPrompt {
            get { return _secPrompt; }
            set { _secPrompt = value; }
        }

        public bool UseInterpreterPrompts {
            get { return _interpreterPrompts; }
            set { _interpreterPrompts = value; }
        }

        internal ReplIntellisenseMode ReplIntellisenseMode {
            get { return _replIntellisenseMode; }
            set { _replIntellisenseMode = value; }
        }

        public bool ReplSmartHistory {
            get { return _smartHistory; }
            set { _smartHistory = value; }
        }

        public bool LiveCompletionsOnly {
            get { return _liveCompletionsOnly; }
            set { _liveCompletionsOnly = value; }
        }

        public void Load() {
            PrimaryPrompt = _pyService.LoadString(_id + PrimaryPromptSetting, _category) ?? DefaultPrompt;
            SecondaryPrompt = _pyService.LoadString(_id + SecondaryPromptSetting, _category) ?? DefaultSecondaryPrompt;
            UseInterpreterPrompts = _pyService.LoadBool(_id + UseInterpreterPromptsSetting, _category) ?? true;
            ReplIntellisenseMode = _pyService.LoadEnum<ReplIntellisenseMode>(_id + ReplIntellisenseModeSetting, _category) ?? ReplIntellisenseMode.DontEvaluateCalls;
            ReplSmartHistory = _pyService.LoadBool(_id + SmartHistorySetting, _category) ?? true;
            LiveCompletionsOnly = _pyService.LoadBool(_id + LiveCompletionsOnlySetting, _category) ?? false;
        }

        public void Save() {
            _pyService.SaveString(_id + PrimaryPromptSetting, _category, PrimaryPrompt);
            _pyService.SaveString(_id + SecondaryPromptSetting, _category, SecondaryPrompt);
            _pyService.SaveBool(_id + UseInterpreterPromptsSetting, _category, UseInterpreterPrompts);
            _pyService.SaveEnum<ReplIntellisenseMode>(_id + ReplIntellisenseModeSetting, _category, ReplIntellisenseMode);
            _pyService.SaveBool(_id + SmartHistorySetting, _category, ReplSmartHistory);
            _pyService.SaveBool(_id + LiveCompletionsOnlySetting, _category, LiveCompletionsOnly);
        }

        public void Reset() {
            PrimaryPrompt = DefaultPrompt;
            SecondaryPrompt = DefaultSecondaryPrompt;
            UseInterpreterPrompts = true;
            ReplIntellisenseMode = ReplIntellisenseMode.DontEvaluateCalls;
            ReplSmartHistory = true;
            LiveCompletionsOnly = false;
        }
    }
}

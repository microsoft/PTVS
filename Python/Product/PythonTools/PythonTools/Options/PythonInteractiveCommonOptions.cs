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

using Microsoft.PythonTools.Repl;

namespace Microsoft.PythonTools.Options {
    /// <summary>
    /// Stores options related to the all interactive windows.
    /// </summary>
    class PythonInteractiveCommonOptions {
        private bool _smartHistory, _interpreterPrompts, _inlinePrompts, _liveCompletionsOnly;
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
            _inlinePrompts = true;
            _interpreterPrompts = true;
            _replIntellisenseMode = Repl.ReplIntellisenseMode.DontEvaluateCalls;
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

        public bool InlinePrompts {
            get { return _inlinePrompts; }
            set { _inlinePrompts = value; }
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
            InlinePrompts = _pyService.LoadBool(_id + InlinePromptsSetting, _category) ?? true;
            UseInterpreterPrompts = _pyService.LoadBool(_id + UseInterpreterPromptsSetting, _category) ?? true;
            ReplIntellisenseMode = _pyService.LoadEnum<ReplIntellisenseMode>(_id + ReplIntellisenseModeSetting, _category) ?? ReplIntellisenseMode.DontEvaluateCalls;
            ReplSmartHistory = _pyService.LoadBool(_id + SmartHistorySetting, _category) ?? true;
            LiveCompletionsOnly = _pyService.LoadBool(_id + LiveCompletionsOnlySetting, _category) ?? false;
        }

        public void Save() {
            _pyService.SaveString(_id + PrimaryPromptSetting, _category, PrimaryPrompt);
            _pyService.SaveString(_id + SecondaryPromptSetting, _category, SecondaryPrompt);
            _pyService.SaveBool(_id + InlinePromptsSetting, _category, InlinePrompts);
            _pyService.SaveBool(_id + UseInterpreterPromptsSetting, _category, UseInterpreterPrompts);
            _pyService.SaveEnum<ReplIntellisenseMode>(_id + ReplIntellisenseModeSetting, _category, ReplIntellisenseMode);
            _pyService.SaveBool(_id + SmartHistorySetting, _category, ReplSmartHistory);
            _pyService.SaveBool(_id + LiveCompletionsOnlySetting, _category, LiveCompletionsOnly);
        }

        public void Reset() {
            PrimaryPrompt = DefaultPrompt;
            SecondaryPrompt = DefaultSecondaryPrompt;
            InlinePrompts = true;
            UseInterpreterPrompts = true;
            ReplIntellisenseMode = ReplIntellisenseMode.DontEvaluateCalls;
            ReplSmartHistory = true;
            LiveCompletionsOnly = false;
        }
    }
}

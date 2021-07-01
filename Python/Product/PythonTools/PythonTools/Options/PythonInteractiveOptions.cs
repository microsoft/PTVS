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

using Microsoft.PythonTools.Repl;

namespace Microsoft.PythonTools.Options
{
    /// <summary>
    /// Stores options related to the all interactive windows.
    /// </summary>
    class PythonInteractiveOptions
    {
        private bool _smartHistory, _liveCompletionsOnly;
        private ReplIntellisenseMode _completionMode;
        private string _scripts;

        private readonly PythonToolsService _pyService;
        private readonly string _category;

        private const string CompletionModeSetting = "CompletionMode";
        private const string UseSmartHistorySetting = "UseSmartHistory";
        private const string LiveCompletionsOnlySetting = "LiveCompletionsOnly";
        private const string ScriptsSetting = "Scripts";

        internal PythonInteractiveOptions(PythonToolsService pyService, string category)
        {
            _pyService = pyService;
            _category = category;
            _completionMode = ReplIntellisenseMode.DontEvaluateCalls;
            _smartHistory = true;
            _scripts = string.Empty;
            Load();
        }

        internal ReplIntellisenseMode CompletionMode
        {
            get { return _completionMode; }
            set { _completionMode = value; }
        }

        public bool UseSmartHistory
        {
            get { return _smartHistory; }
            set { _smartHistory = value; }
        }

        public bool LiveCompletionsOnly
        {
            get { return _liveCompletionsOnly; }
            set { _liveCompletionsOnly = value; }
        }

        public string Scripts
        {
            get { return _scripts; }
            set { _scripts = value ?? string.Empty; }
        }

        public void Load()
        {
            CompletionMode = _pyService.LoadEnum<ReplIntellisenseMode>(CompletionModeSetting, _category) ?? ReplIntellisenseMode.DontEvaluateCalls;
            UseSmartHistory = _pyService.LoadBool(UseSmartHistorySetting, _category) ?? true;
            LiveCompletionsOnly = _pyService.LoadBool(LiveCompletionsOnlySetting, _category) ?? false;
            Scripts = _pyService.LoadString(ScriptsSetting, _category) ?? string.Empty;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Save()
        {
            _pyService.SaveEnum(CompletionModeSetting, _category, CompletionMode);
            _pyService.SaveBool(UseSmartHistorySetting, _category, UseSmartHistory);
            _pyService.SaveBool(LiveCompletionsOnlySetting, _category, LiveCompletionsOnly);
            _pyService.SaveString(ScriptsSetting, _category, Scripts);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Reset()
        {
            CompletionMode = ReplIntellisenseMode.DontEvaluateCalls;
            UseSmartHistory = true;
            LiveCompletionsOnly = false;
            Scripts = string.Empty;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler Changed;
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.DsTools.Core.Services;
using Microsoft.PythonTools.VsCode.Settings;

namespace Microsoft.R.LanguageServer.Server.Settings {
    internal sealed class SettingsManager : ISettingsManager {
        private readonly REngineSettings _engineSettings = new REngineSettings();
        private readonly REditorSettings _editorSettings = new REditorSettings();
        private readonly RSettings _rSettings = new RSettings();

        public SettingsManager(IServiceManager serviceManager) {
            serviceManager
                .AddService(_engineSettings)
                .AddService(_editorSettings)
                .AddService(_rSettings);
        }

        public void Dispose() => _editorSettings.Dispose();

        public void UpdateSettings(LanguageServerSettings vscodeSettings) {

            var e = vscodeSettings.Editor;
            _editorSettings.FormatScope = e.FormatScope;
            _editorSettings.FormatOptions.BreakMultipleStatements = e.BreakMultipleStatements;
            _editorSettings.FormatOptions.IndentSize = e.TabSize;
            _editorSettings.FormatOptions.TabSize = e.TabSize;
            _editorSettings.FormatOptions.SpaceAfterKeyword = e.SpaceAfterKeyword;
            _editorSettings.FormatOptions.SpaceBeforeCurly = e.SpaceBeforeCurly;
            _editorSettings.FormatOptions.SpacesAroundEquals = e.SpacesAroundEquals;

            _editorSettings.LintOptions = vscodeSettings.Linting;
            _engineSettings.InterpreterIndex = vscodeSettings.Interpreter;

            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler SettingsChanged;

#pragma warning disable 67

        private sealed class REditorSettings : IREditorSettings {
            private readonly IEditorSettingsStorage _storage;

            public REditorSettings() {
                _storage = new EditorSettingsStorage();
                LintOptions = new LintOptions(() => _storage);
            }

            public void Dispose() => _storage.Dispose();

            public event EventHandler<EventArgs> SettingsChanged;
            public bool AutoFormat { get; } = true;
            public bool CompletionEnabled { get; } = true;
            public int IndentSize { get; } = 2;
            public IndentType IndentType { get; } = IndentType.Spaces;
            public int TabSize { get; } = 2;
            public IndentStyle IndentStyle { get; } = IndentStyle.Smart;
            public bool SyntaxCheckEnabled { get; } = true;
            public bool SignatureHelpEnabled { get; } = true;
            public bool InsertMatchingBraces { get; } = true;
            public bool FormatOnPaste { get; set; }
            public bool FormatScope { get; set; }
            public bool CommitOnSpace { get; set; } = false;
            public bool CommitOnEnter { get; set; } = true;
            public bool ShowCompletionOnFirstChar { get; set; } = true;
            public bool ShowCompletionOnTab { get; set; } = true;
            public bool SyntaxCheckInRepl { get; set; }
            public bool PartialArgumentNameMatch { get; set; }
            public bool EnableOutlining { get; set; }
            public bool SmartIndentByArgument { get; set; } = true;
            public RFormatOptions FormatOptions { get; set; } = new RFormatOptions();
            public ILintOptions LintOptions { get; set; }
        }
    }
}

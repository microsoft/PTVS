// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.PythonTools.VsCode.Settings;

namespace Microsoft.PythonTools.VsCode.Server.Settings {
    internal sealed class SettingsManager : ISettingsManager {
        private LanguageServerSettings _settings = new LanguageServerSettings();

        public void UpdateSettings(LanguageServerSettings vscodeSettings) {
            _settings = vscodeSettings;
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler SettingsChanged;
    }
}

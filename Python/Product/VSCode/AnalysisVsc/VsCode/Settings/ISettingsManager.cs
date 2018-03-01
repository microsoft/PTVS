// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.PythonTools.VsCode.Settings {
    /// <summary>
    /// Represents server than transforms VSCode settings to RTVS settings
    /// </summary>
    internal interface ISettingsManager {
        void UpdateSettings(LanguageServerSettings vscodeSettings);
        event EventHandler SettingsChanged;
    }
}

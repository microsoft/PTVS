// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.PythonTools.VsCode.Settings;

namespace Microsoft.PythonTools.VsCode.Server.Settings {
    /// <summary>
    /// Represents server than transforms VSCode settings to RTVS settings
    /// </summary>
    internal interface ISettingsManager {
        void UpdateSettings(LanguageServerSettings vscodeSettings);
        event EventHandler SettingsChanged;
    }
}

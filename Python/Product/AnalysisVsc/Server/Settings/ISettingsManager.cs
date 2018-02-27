// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.R.LanguageServer.Server.Settings {
    /// <summary>
    /// Represents server than transforms VSCode settings to RTVS settings
    /// </summary>
    internal interface ISettingsManager: IDisposable {
        void UpdateSettings(LanguageServerSettings vscodeSettings);
        event EventHandler SettingsChanged;
    }
}

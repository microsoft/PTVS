// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Based on https://github.com/CXuesong/LanguageServer.NET

namespace Microsoft.PythonTools.VsCode.Settings {
    /// <summary>
    /// Represents VS Code settings for R
    /// </summary>
    public sealed class SettingsRoot {
        /// <summary>
        /// Settings from VSCode 'Python' configuration section
        /// </summary>
        /// <remarks>
        /// Name of the settings member must match
        /// name of the configuration section in project.json
        /// </remarks>
        public LanguageServerSettings R { get; set; }
    }
}

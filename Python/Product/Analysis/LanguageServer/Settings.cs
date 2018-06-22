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

using System;
using System.ComponentModel;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Newtonsoft.Json;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    [Serializable]
    internal struct LanguageServerSettings {
        public readonly AutoCompleteSettings AutoComplete;
        public readonly DiagnosticsSettings Diagnostics;

        [JsonConstructor]
        public LanguageServerSettings(
            [DefaultJson] AutoCompleteSettings autoComplete,
            [DefaultJson] DiagnosticsSettings diagnostics) {

            AutoComplete = autoComplete;
            Diagnostics = diagnostics;
        }

        [Serializable]
        internal struct AutoCompleteSettings {
            public readonly bool ShowAdvancedMembers;

            [JsonConstructor]
            public AutoCompleteSettings([DefaultValue(true)] bool showAdvancedMembers) {
                ShowAdvancedMembers = showAdvancedMembers;
            }
        }

        [Serializable]
        internal struct DiagnosticsSettings {
            public readonly DiagnosticSeverity UnresolvedImports;

            [JsonConstructor]
            public DiagnosticsSettings([DefaultValue(DiagnosticSeverity.Warning)] DiagnosticSeverity unresolvedImports) {
                UnresolvedImports = unresolvedImports;
            }
        }
    }

    internal static class LanguageServerSettingsExtensions {
        public static bool Show(this DiagnosticSeverity severity) => severity != DiagnosticSeverity.Unspecified;
    }
}

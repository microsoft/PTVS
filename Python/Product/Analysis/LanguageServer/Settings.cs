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
using System.Collections.Generic;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    public sealed class LanguageServerSettings {
        public class PythonAnalysisOptions {
            public bool openFilesOnly;
            public object[] errors = Array.Empty<object>();
            public object[] warnings = Array.Empty<object>();
            public object[] information = Array.Empty<object>();
            public object[] disabled = Array.Empty<object>();

            private HashSet<object> _errors;
            private HashSet<object> _warnings;
            private HashSet<object> _information;
            private HashSet<object> _disabled;

            public DiagnosticSeverity GetEffectiveSeverity(object code, DiagnosticSeverity defaultSeverity) {
                Init();

                if (_disabled != null && _disabled.Contains(code)) {
                    return DiagnosticSeverity.Unspecified;
                }
                if (_errors != null && _errors.Contains(code)) {
                    return DiagnosticSeverity.Error;
                }
                if (_warnings != null && _warnings.Contains(code)) {
                    return DiagnosticSeverity.Warning;
                }
                if (_information != null && _information.Contains(code)) {
                    return DiagnosticSeverity.Information;
                }
                return defaultSeverity;
            }

            public bool Show(DiagnosticSeverity severity) => severity != DiagnosticSeverity.Unspecified;

            private void Init() {
                if (errors != null && errors.Length > 0) {
                    _errors = new HashSet<object>(errors);
                }
                if (warnings != null && warnings.Length > 0) {
                    _warnings = new HashSet<object>(warnings);
                }
                if (information != null && information.Length > 0) {
                    _information = new HashSet<object>(information);
                }
                if (disabled != null && disabled.Length > 0) {
                    _disabled = new HashSet<object>(disabled);
                }
            }
        }
        public readonly PythonAnalysisOptions analysis = new PythonAnalysisOptions();

        public class PythonCompletionOptions {
            public bool showAdvancedMembers = true;
        }
        public readonly PythonCompletionOptions completion = new PythonCompletionOptions();
    }
}

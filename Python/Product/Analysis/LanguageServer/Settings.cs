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

            private Dictionary<object, DiagnosticSeverity> _map;

            public DiagnosticSeverity GetEffectiveSeverity(object code, DiagnosticSeverity defaultSeverity) {
                Init();
                return _map.TryGetValue(code, out var severity) ? severity : defaultSeverity;
            }

            private void Init() {
                if (_map != null) {
                    return;
                }
                _map = new Dictionary<object, DiagnosticSeverity>();
                // disabled > error > warning > information
                foreach (var x in information) {
                    _map[x] = DiagnosticSeverity.Information;
                }
                foreach (var x in warnings) {
                    _map[x] = DiagnosticSeverity.Warning;
                }
                foreach (var x in errors) {
                    _map[x] = DiagnosticSeverity.Error;
                }
                foreach (var x in disabled) {
                    _map[x] = DiagnosticSeverity.Unspecified;
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

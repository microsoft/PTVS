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

using System.Collections.Generic;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    class DiagnosticsErrorSink : ErrorSink {
        private readonly string _source;
        private readonly List<Diagnostic> _diagnostics;

        public DiagnosticsErrorSink(string source, List<Diagnostic> diagnostics) {
            _source = source;
            _diagnostics = diagnostics;
        }

        public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

        public override void Add(string message, NewLineLocation[] lineLocations, int startIndex, int endIndex, int errorCode, Severity severity) {
            var d = new Diagnostic {
                code = errorCode,
                message = message,
                source = _source,
                severity = GetSeverity(severity),
                range = new Range {
                    start = NewLineLocation.IndexToLocation(lineLocations, startIndex),
                    end = NewLineLocation.IndexToLocation(lineLocations, endIndex)
                }
            };

            lock (_diagnostics) {
                _diagnostics.Add(d);
            }
        }

        public void ProcessTaskComment(object sender, CommentEventArgs e) {
            // TODO: Handle full map of settings
            if (!(e.Text?.StartsWith("TODO:") ?? false)) {
                return;
            }

            var d = new Diagnostic {
                message = e.Text,
                range = e.Span
            };

            lock (_diagnostics) {
                _diagnostics.Add(d);
            }
        }

        private static DiagnosticSeverity GetSeverity(Severity severity) {
            switch (severity) {
                case Severity.Ignore: return DiagnosticSeverity.Unspecified;
                case Severity.Warning: return DiagnosticSeverity.Warning;
                case Severity.Error: return DiagnosticSeverity.Error;
                case Severity.FatalError: return DiagnosticSeverity.Error;
                default: return DiagnosticSeverity.Unspecified;
            }
        }
    }
}

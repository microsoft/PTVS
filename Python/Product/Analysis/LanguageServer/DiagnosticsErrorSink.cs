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
using System.Linq;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    class DiagnosticsErrorSink : ErrorSink {
        private readonly string _source;
        private readonly Action<Diagnostic> _onDiagnostic;
        private readonly IReadOnlyList<KeyValuePair<string, DiagnosticSeverity>> _taskCommentMap;

        public DiagnosticsErrorSink(string source, Action<Diagnostic> onDiagnostic, IReadOnlyDictionary<string, DiagnosticSeverity> taskCommentMap = null) {
            _source = source;
            _onDiagnostic = onDiagnostic;
            _taskCommentMap = taskCommentMap?.ToArray();
        }

        public override void Add(string message, SourceSpan span, int errorCode, Severity severity) {
            var d = new Diagnostic {
                code = "E{0}".FormatInvariant(errorCode),
                message = message,
                source = _source,
                severity = GetSeverity(severity),
                range = span
            };

            _onDiagnostic(d);
        }

        public void ProcessTaskComment(object sender, CommentEventArgs e) {
            var text = e.Text.TrimStart('#').Trim();

            var d = new Diagnostic {
                message = text,
                range = e.Span,
                source = _source
            };

            bool found = false;
            foreach (var kv in _taskCommentMap.MaybeEnumerate().OrderByDescending(kv => kv.Key.Length)) {
                if (text.IndexOfOrdinal(kv.Key, ignoreCase: true) >= 0) {
                    d.code = kv.Key;
                    d.severity = kv.Value;
                    found = true;
                    break;
                }
            }

            if (found) {
                _onDiagnostic(d);
            }
        }

        internal static DiagnosticSeverity GetSeverity(Severity severity) {
            switch (severity) {
                case Severity.Ignore: return DiagnosticSeverity.Unspecified;
                case Severity.Information: return DiagnosticSeverity.Information;
                case Severity.Warning: return DiagnosticSeverity.Warning;
                case Severity.Error: return DiagnosticSeverity.Error;
                case Severity.FatalError: return DiagnosticSeverity.Error;
                default: return DiagnosticSeverity.Unspecified;
            }
        }

        internal static Severity GetSeverity(DiagnosticSeverity severity) {
            switch (severity) {
                case DiagnosticSeverity.Unspecified: return Severity.Ignore;
                case DiagnosticSeverity.Information: return Severity.Information;
                case DiagnosticSeverity.Warning: return Severity.Warning;
                case DiagnosticSeverity.Error: return Severity.Error;
                default: return Severity.Ignore;
            }
        }
    }
}

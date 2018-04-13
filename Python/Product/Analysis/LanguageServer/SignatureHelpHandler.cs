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
using System.Text;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    internal sealed class SignatureHelpHandler {
        private readonly ProjectFiles _projectFiles;
        private readonly ILogger _log;
        private readonly ClientCapabilities _clientCaps;

        public SignatureHelpHandler(ProjectFiles projectFiles, ClientCapabilities clientCaps, ILogger log) {
            _projectFiles = projectFiles;
            _log = log;
            _clientCaps = clientCaps;
        }

        public SignatureHelp GetSignatureHelp(TextDocumentPositionParams @params) {
            var uri = @params.textDocument.uri;
            _projectFiles.GetAnalysis(@params.textDocument, @params.position, @params._version, out var entry, out var tree);

            _log.TraceMessage($"Signatures in {uri} at {@params.position}");

            var analysis = entry?.Analysis;
            if (analysis == null) {
                _log.TraceMessage($"No analysis found for {uri}");
                return new SignatureHelp();
            }

            IEnumerable<IOverloadResult> overloads;
            int activeSignature = -1, activeParameter = -1;
            if (!string.IsNullOrEmpty(@params._expr)) {
                _log.TraceMessage($"Getting signatures for {@params._expr}");
                overloads = analysis.GetSignatures(@params._expr, @params.position);
            } else {
                var finder = new ExpressionFinder(tree, new GetExpressionOptions { Calls = true });
                var index = tree.LocationToIndex(@params.position);
                if (finder.GetExpression(@params.position) is CallExpression callExpr) {
                    _log.TraceMessage($"Getting signatures for {callExpr.ToCodeString(tree, CodeFormattingOptions.Traditional)}");
                    overloads = analysis.GetSignatures(callExpr.Target, @params.position);
                    activeParameter = -1;
                    if (callExpr.GetArgumentAtIndex(tree, index, out activeParameter) && activeParameter < 0) {
                        // Returned 'true' and activeParameter == -1 means that we are after 
                        // the trailing comma, so assume partially typed expression such as 'pow(x, y, |)
                        activeParameter = callExpr.Args.Count;
                    }
                } else {
                    _log.TraceMessage($"No signatures found in {uri} at {@params.position}");
                    return new SignatureHelp();
                }
            }

            var sigs = overloads.Select(ToSignatureInformation).ToArray();
            if (activeParameter >= 0 && activeSignature < 0) {
                // TODO: Better selection of active signature
                activeSignature = sigs
                    .Select((s, i) => Tuple.Create(s, i))
                    .OrderBy(t => t.Item1.parameters.Length)
                    .FirstOrDefault(t => t.Item1.parameters.Length > activeParameter)
                    ?.Item2 ?? -1;
            }

            activeSignature = activeSignature >= 0
                ? activeSignature
                : (sigs.Length > 0 ? 0 : -1);

            var sh = new SignatureHelp {
                signatures = sigs,
                activeSignature = activeSignature,
                activeParameter = activeParameter
            };
            return sh;
        }

        private SignatureInformation ToSignatureInformation(IOverloadResult overload) {
            var si = new SignatureInformation();

            if (_clientCaps?.textDocument?.signatureHelp?.signatureInformation?._shortLabel ?? false) {
                si.label = overload.Name;
            } else {
                si.label = "{0}({1})".FormatInvariant(
                    overload.Name,
                    string.Join(", ", overload.Parameters.Select(FormatParameter))
                );
            }

            si.documentation = string.IsNullOrEmpty(overload.Documentation) ? null : overload.Documentation;
            si.parameters = overload.Parameters.MaybeEnumerate().Select(p => new ParameterInformation {
                label = p.Name,
                documentation = string.IsNullOrEmpty(p.Documentation) ? null : p.Documentation,
                _type = p.Type,
                _defaultValue = p.DefaultValue
            }).ToArray();

            switch (SelectBestMarkup(_clientCaps.textDocument?.signatureHelp?.signatureInformation?.documentationFormat, MarkupKind.Markdown, MarkupKind.PlainText)) {
                case MarkupKind.Markdown:
                    var converter = new RestTextConverter();
                    if (!string.IsNullOrEmpty(si.documentation.value)) {
                        si.documentation.kind = MarkupKind.Markdown;
                        si.documentation.value = converter.ToMarkdown(si.documentation.value);
                    }
                    foreach (var p in si.parameters) {
                        if (!string.IsNullOrEmpty(p.documentation.value)) {
                            p.documentation.kind = MarkupKind.Markdown;
                            p.documentation.value = converter.ToMarkdown(p.documentation.value);
                        }
                    }
                    break;
            }

            si._returnTypes = (overload as IOverloadResult2)?.ReturnType.OrderBy(k => k).ToArray();
            return si;
        }

        private MarkupKind SelectBestMarkup(IEnumerable<MarkupKind> requested, params MarkupKind[] supported) {
            if (requested == null) {
                return supported.Last();
            }
            foreach (var k in requested) {
                if (supported.Contains(k)) {
                    return k;
                }
            }
            return MarkupKind.PlainText;
        }

        private string FormatParameter(ParameterResult p) {
            var res = new StringBuilder(p.Name);
            if (!string.IsNullOrEmpty(p.Type)) {
                res.Append(": ");
                res.Append(p.Type);
            }
            if (!string.IsNullOrEmpty(p.DefaultValue)) {
                res.Append('=');
                res.Append(p.DefaultValue);
            }
            return res.ToString();
        }
    }
}

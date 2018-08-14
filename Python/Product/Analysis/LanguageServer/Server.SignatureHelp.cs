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
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    public sealed partial class Server {
        public override async Task<SignatureHelp> SignatureHelp(TextDocumentPositionParams @params) {
            var uri = @params.textDocument.uri;
            ProjectFiles.GetAnalysis(@params.textDocument, @params.position, @params._version, out var entry, out var tree);

            TraceMessage($"Signatures in {uri} at {@params.position}");

            var analysis = entry != null ? await entry.GetAnalysisAsync(waitingTimeout: 50) : null;
            if (analysis == null) {
                TraceMessage($"No analysis found for {uri}");
                return new SignatureHelp();
            }

            IEnumerable<IOverloadResult> overloads;
            int activeSignature = -1, activeParameter = -1;
            if (!string.IsNullOrEmpty(@params._expr)) {
                TraceMessage($"Getting signatures for {@params._expr}");
                overloads = analysis.GetSignatures(@params._expr, @params.position);
            } else {
                var finder = new ExpressionFinder(tree, new GetExpressionOptions { Calls = true });
                var index = tree.LocationToIndex(@params.position);
                if (finder.GetExpression(@params.position) is CallExpression callExpr) {
                    TraceMessage($"Getting signatures for {callExpr.ToCodeString(tree, CodeFormattingOptions.Traditional)}");
                    overloads = analysis.GetSignatures(callExpr.Target, @params.position);
                    activeParameter = -1;
                    if (callExpr.GetArgumentAtIndex(tree, index, out activeParameter) && activeParameter < 0) {
                        // Returned 'true' and activeParameter == -1 means that we are after 
                        // the trailing comma, so assume partially typed expression such as 'pow(x, y, |)
                        activeParameter = callExpr.Args.Count;
                    }
                } else {
                    TraceMessage($"No signatures found in {uri} at {@params.position}");
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

            return new SignatureHelp {
                signatures = sigs,
                activeSignature = activeSignature,
                activeParameter = activeParameter
            };
        }

        private SignatureInformation ToSignatureInformation(IOverloadResult overload) {
            var si = new SignatureInformation();

            si.parameters = overload.Parameters.MaybeEnumerate().Select(p => new ParameterInformation {
                label = p.Name,
                documentation = string.IsNullOrEmpty(p.Documentation) ? null : p.Documentation,
                _type = p.Type,
                _defaultValue = p.DefaultValue
            }).ToArray();

            si._returnTypes = (overload as IOverloadResult2)?.ReturnType.OrderBy(k => k).ToArray();

            if (_clientCaps?.textDocument?.signatureHelp?.signatureInformation?._shortLabel ?? false) {
                si.label = overload.Name;
            } else {
                var doc = overload.Documentation;
                // Some function contain signature in the documentation. Example: print.
                // We want to use that signature in VS Code since it contains all arguments.
                if (si.parameters.Length == 0 && !string.IsNullOrEmpty(doc) && doc.StartsWithOrdinal($"{overload.Name}(")) {
                    return GetSignatureFromDoc(doc);
                }
                si.label = "{0}({1})".FormatInvariant(
                    overload.Name,
                    string.Join(", ", overload.Parameters.Select(FormatParameter))
                );
            }

            si.documentation = string.IsNullOrEmpty(overload.Documentation) ? null : overload.Documentation;
            var formatSetting = _clientCaps?.textDocument?.signatureHelp?.signatureInformation?.documentationFormat;
            si.documentation = GetMarkupContent(si.documentation.value, formatSetting);
            foreach (var p in si.parameters) {
                p.documentation = GetMarkupContent(p.documentation.value, formatSetting);
            }

            return si;
        }

        private static SignatureInformation GetSignatureFromDoc(string doc) {
            var si = new SignatureInformation();
            var firstLineBreak = doc.IndexOfAny(new[] { '\r', '\n' });
            si.label = firstLineBreak > 0 ? doc.Substring(0, firstLineBreak) : doc;
            si.documentation = doc.Substring(si.label.Length).TrimStart();
            si.parameters = GetParametersFromDoc(si.label);
            return si;
        }

        private static ParameterInformation[] GetParametersFromDoc(string doc) {
            var openBrace = doc.IndexOf('(');
            var closeBrace = doc.LastIndexOf(')');

            if (openBrace > 0 && closeBrace > 0) {
                var args = doc.Substring(openBrace + 1, closeBrace - openBrace - 1).Split(',');

                return args.Select(a => {
                    var i = a.IndexOf('=');
                    return new ParameterInformation {
                        label = i > 0 ? a.Substring(0, i).Trim() : a.Trim(),
                    };
                }).ToArray();
            }
            return Array.Empty<ParameterInformation>();
        }
        private static string FormatParameter(ParameterResult p) {
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

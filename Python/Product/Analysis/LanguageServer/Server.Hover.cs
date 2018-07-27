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

using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    public sealed partial class Server {
        private static readonly Hover EmptyHover = new Hover {
            contents = new MarkupContent { kind = MarkupKind.PlainText, value = string.Empty }
        };

        private DocumentationBuilder _displayTextBuilder;

        public override Task<Hover> Hover(TextDocumentPositionParams @params) => Hover(@params, CancellationToken.None);

        internal async Task<Hover> Hover(TextDocumentPositionParams @params, CancellationToken cancellationToken) {
            var uri = @params.textDocument.uri;
            ProjectFiles.GetAnalysis(@params.textDocument, @params.position, @params._version, out var entry, out var tree);

            TraceMessage($"Hover in {uri} at {@params.position}");

            var analysis = entry != null ? await entry.GetAnalysisAsync(50, cancellationToken) : null;
            if (analysis == null) {
                TraceMessage($"No analysis found for {uri}");
                return EmptyHover;
            }

            tree = GetParseTree(entry, uri, cancellationToken, out var version) ?? tree;

            Expression expr;
            SourceSpan? exprSpan;

            var finder = new ExpressionFinder(tree, GetExpressionOptions.Hover);
            expr = finder.GetExpression(@params.position) as Expression;
            exprSpan = expr?.GetSpan(tree);

            if (expr == null) {
                TraceMessage($"No hover info found in {uri} at {@params.position}");
                return EmptyHover;
            }

            TraceMessage($"Getting hover for {expr.ToCodeString(tree, CodeFormattingOptions.Traditional)}");

            // First try values from expression. This works for the import statement most of the time.
            var values = analysis.GetValues(expr, @params.position, null).ToList();
            if (values.Count == 0) {
                // See if this is hover over import statement
                var index = tree.LocationToIndex(@params.position);
                var w = new ImportedModuleNameWalker(entry, index, tree);
                tree.Walk(w);

                if (w.ImportedType != null) {
                    values = analysis.GetValues(w.ImportedType.Name, @params.position).ToList();
                } else {
                    var sb = new StringBuilder();
                    var span = SourceSpan.Invalid;
                    foreach (var n in w.ImportedModules) {
                        if (Analyzer.Modules.TryGetImportedModule(n.Name, out var modRef) && modRef.AnalysisModule != null) {
                            if (sb.Length > 0) {
                                sb.AppendLine();
                                sb.AppendLine();
                            }
                            sb.Append(_displayTextBuilder.GetModuleDocumentation(modRef));
                            span = span.IsValid ? span.Union(n.SourceSpan) : n.SourceSpan;
                        }
                    }
                    if (sb.Length > 0) {
                        return new Hover {
                            contents = sb.ToString(),
                            range = span
                        };
                    }
                }
            }

            if (values.Count > 0) {
                string originalExpr;
                if (expr is ConstantExpression || expr is ErrorExpression) {
                    originalExpr = null;
                } else {
                    originalExpr = @params._expr?.Trim();
                    if (string.IsNullOrEmpty(originalExpr)) {
                        originalExpr = expr.ToCodeString(tree, CodeFormattingOptions.Traditional);
                    }
                }

                var names = values.Select(GetFullTypeName).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToArray();
                var res = new Hover {
                    contents = GetMarkupContent(
                        _displayTextBuilder.GetDocumentation(values, originalExpr),
                        _clientCaps.textDocument?.hover?.contentFormat),
                    range = exprSpan,
                    _version = version?.Version,
                    _typeNames = names
                };
                return res;
            }

            return EmptyHover;
        }

        private static string GetFullTypeName(AnalysisValue value) {
            if (value is IHasQualifiedName qualName) {
                return qualName.FullyQualifiedName;
            }

            if (value is Values.InstanceInfo ii) {
                return GetFullTypeName(ii.ClassInfo);
            }

            if (value is Values.BuiltinInstanceInfo bii) {
                return GetFullTypeName(bii.ClassInfo);
            }

            return value?.Name;
        }
    }
}

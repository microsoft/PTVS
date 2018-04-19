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
using System.Threading.Tasks;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    public sealed partial class Server {
        private static Hover EmptyHover = new Hover {
            contents = new MarkupContent { kind = MarkupKind.PlainText, value = string.Empty }
        };
        private readonly DisplayTextBuilder _displayTextBuilder = new DisplayTextBuilder();

        public override async Task<Hover> Hover(TextDocumentPositionParams @params) {
            await _analyzerCreationTask;
            IfTestWaitForAnalysisComplete();

            var uri = @params.textDocument.uri;
            _projectFiles.GetAnalysis(@params.textDocument, @params.position, @params._version, out var entry, out var tree);

            TraceMessage($"Hover in {uri} at {@params.position}");

            var analysis = entry?.Analysis;
            if (analysis == null) {
                TraceMessage($"No analysis found for {uri}");
                return EmptyHover;
            }

            tree = GetParseTree(entry, uri, CancellationToken, out var version) ?? tree;

            var index = tree.LocationToIndex(@params.position);
            var w = new ImportedModuleNameWalker(entry.ModuleName, index);
            tree.Walk(w);
            if (!string.IsNullOrEmpty(w.ImportedName) &&
                _analyzer.Modules.TryImport(w.ImportedName, out var modRef)) {
                var doc = _displayTextBuilder.MakeModuleHoverText(modRef);
                MarkupContent mc = null;
                if (doc != null) {
                    switch (SelectBestMarkup(_clientCaps.textDocument?.hover?.contentFormat, MarkupKind.Markdown, MarkupKind.PlainText)) {
                        case MarkupKind.Markdown:
                            mc = new MarkupContent {
                                kind = MarkupKind.Markdown,
                                value = new RestTextConverter().ToMarkdown(doc)
                            };
                            break;
                    }
                }
                return new Hover { contents = (mc ?? doc) ?? string.Empty };
            }

            Expression expr;
            SourceSpan? exprSpan;
            Analyzer.InterpreterScope scope = null;

            if (!string.IsNullOrEmpty(@params._expr)) {
                TraceMessage($"Getting hover for {@params._expr}");
                expr = analysis.GetExpressionForText(@params._expr, @params.position, out scope, out var exprTree);
                // This span will not be valid within the document, but it will at least
                // have the correct length. If we have passed "_expr" then we are likely
                // planning to ignore the returned span anyway.
                exprSpan = expr?.GetSpan(exprTree);
            } else {
                var finder = new ExpressionFinder(tree, GetExpressionOptions.Hover);
                expr = finder.GetExpression(@params.position) as Expression;
                exprSpan = expr?.GetSpan(tree);
            }
            if (expr == null) {
                TraceMessage($"No hover info found in {uri} at {@params.position}");
                return EmptyHover;
            }

            TraceMessage($"Getting hover for {expr.ToCodeString(tree, CodeFormattingOptions.Traditional)}");
            var values = analysis.GetValues(expr, @params.position, scope).ToList();

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
                contents = _displayTextBuilder.MakeHoverText(values, originalExpr, _displayOptions),
                range = exprSpan,
                _version = version,
                _typeNames = names
            };
            switch (SelectBestMarkup(_clientCaps.textDocument?.hover?.contentFormat, MarkupKind.Markdown, MarkupKind.PlainText)) {
                case MarkupKind.Markdown:
                    res.contents = new MarkupContent {
                        kind = MarkupKind.Markdown,
                        value = new RestTextConverter().ToMarkdown(res.contents.value)
                    };
                    break;
            }
            return res;
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

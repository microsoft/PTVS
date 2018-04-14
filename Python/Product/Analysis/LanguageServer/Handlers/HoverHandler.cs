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
using System.Threading;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    internal sealed class HoverHandler : HandlerBase {
        private readonly DisplayTextBuilder _displayTextBuilder = new DisplayTextBuilder();

        public HoverHandler(PythonAnalyzer analyzer, ProjectFiles projectFiles, ClientCapabilities clientCaps, ILogger log) :
            base(analyzer, projectFiles, clientCaps, log) { }

        public Hover GetHover(TextDocumentPositionParams @params, InformationDisplayOptions displayOptions, CancellationToken token) {
            var uri = @params.textDocument.uri;
            ProjectFiles.GetAnalysis(@params.textDocument, @params.position, @params._version, out var entry, out var tree);

            Log.TraceMessage($"Hover in {uri} at {@params.position}");

            var analysis = entry?.Analysis;
            if (analysis == null) {
                Log.TraceMessage($"No analysis found for {uri}");
                return default(Hover);
            }

            tree = GetParseTree(entry, uri, token, out var version) ?? tree;

            var index = tree.LocationToIndex(@params.position);
            var w = new ImportedModuleNameWalker(entry.ModuleName, index);
            tree.Walk(w);
            if (!string.IsNullOrEmpty(w.ImportedName) &&
                Analyzer.Modules.TryImport(w.ImportedName, out var modRef)) {
                var contents = _displayTextBuilder.MakeModuleHoverText(modRef);
                if (contents != null) {
                    return new Hover { contents = contents };
                }
            }

            Expression expr;
            SourceSpan? exprSpan;
            Analyzer.InterpreterScope scope = null;

            if (!string.IsNullOrEmpty(@params._expr)) {
                Log.TraceMessage($"Getting hover for {@params._expr}");
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
                Log.TraceMessage($"No hover info found in {uri} at {@params.position}");
                return default(Hover);
            }

            Log.TraceMessage($"Getting hover for {expr.ToCodeString(tree, CodeFormattingOptions.Traditional)}");
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
                contents = new MarkupContent {
                    kind = MarkupKind.Markdown,
                    value = _displayTextBuilder.MakeHoverText(values, originalExpr, displayOptions)
                },
                range = exprSpan,
                _version = version,
                _typeNames = names
            };
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

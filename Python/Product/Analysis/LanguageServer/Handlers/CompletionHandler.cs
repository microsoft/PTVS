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
using System.Threading;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    internal sealed class CompletionHandler: HandlerBase {
        private static CompletionItem[] EmptyCompletion = new CompletionItem[0];

        public CompletionHandler(PythonAnalyzer analyzer, ProjectFiles projectFiles, ClientCapabilities clientCaps, ILogger log):
            base(analyzer, projectFiles, clientCaps, log) { }

        public CompletionItem[] GetCompletions(CompletionParams @params, LanguageServerSettings settings, CancellationToken token) {
            var uri = @params.textDocument.uri;

           var entry = ProjectFiles.GetEntry(@params.textDocument) as ProjectEntry;
            if (!(entry is IDocument doc)) {
                Log.TraceMessage($"No analysis found for {uri}");
                return EmptyCompletion;
            }

            var version = @params._version.HasValue ? @params._version.Value : doc.GetDocumentVersion(0);
            ProjectFiles.GetAnalysis(@params.textDocument, @params.position, version, out entry, out var tree);
            Log.TraceMessage($"Completions in {uri} at {@params.position}");

            tree = GetParseTree(entry, uri, token, out _) ?? tree;

            var analysis = entry?.Analysis;
            if (analysis == null) {
                Log.TraceMessage($"No analysis found for {uri}");
                return EmptyCompletion;
            }

            var opts = GetOptions(@params.context);
            var members = GetMembers(@params, entry, tree, analysis, opts);
            if (members == null) {
                Log.TraceMessage($"No members found in document {uri}");
                return EmptyCompletion;
            }

            var filtered = members
                .Where(m => settings.ShowAdvancedMembers ? true : !m.Name.StartsWith("__"))
                .Select(m => ToCompletionItem(m, opts));

            var filterKind = @params.context?._filterKind;
            if (filterKind.HasValue && filterKind != CompletionItemKind.None) {
                Log.TraceMessage($"Only returning {filterKind.Value} items");
                filtered = filtered.Where(m => m.kind == filterKind.Value);
            }

            return filtered.ToArray();
        }

        private IEnumerable<MemberResult> GetMembers(CompletionParams @params, ProjectEntry entry, PythonAst tree, ModuleAnalysis analysis, GetMemberOptions opts) {
            IEnumerable<MemberResult> members = null;
            Expression expr = null;
            if (!string.IsNullOrEmpty(@params._expr)) {
                Log.TraceMessage($"Completing expression {@params._expr}");

                if (@params.context?._filterKind == CompletionItemKind.Module) {
                    // HACK: Special case for child modules until #3798 is completed
                    members = entry.Analysis.ProjectState.GetModuleMembers(entry.Analysis.InterpreterContext, @params._expr.Split('.'));
                } else {
                    members = entry.Analysis.GetMembers(@params._expr, @params.position, opts);
                }
            } else {
                var finder = new ExpressionFinder(tree, GetExpressionOptions.EvaluateMembers);
                expr = finder.GetExpression(@params.position) as Expression;
                if (expr != null) {
                    Log.TraceMessage($"Completing expression {expr.ToCodeString(tree, CodeFormattingOptions.Traditional)}");
                    members = entry.Analysis.GetMembers(expr, @params.position, opts, null);
                } else {
                    Log.TraceMessage($"Completing all names");
                    members = entry.Analysis.GetAllAvailableMembers(@params.position, opts);
                }
            }

            if (@params.context?._includeAllModules ?? false) {
                var mods = Analyzer.GetModules();
                Log.TraceMessage($"Including {mods?.Length ?? 0} modules");
                members = members?.Concat(mods) ?? mods;
            }

            if (@params.context?._includeArgumentNames ?? false) {
                var finder = new ExpressionFinder(tree, new GetExpressionOptions { Calls = true });
                var index = tree.LocationToIndex(@params.position);
                if (finder.GetExpression(@params.position) is CallExpression callExpr &&
                    callExpr.GetArgumentAtIndex(tree, index, out _)) {
                    var argNames = analysis.GetSignatures(callExpr.Target, @params.position)
                        .SelectMany(o => o.Parameters).Select(p => p?.Name)
                        .Where(n => !string.IsNullOrEmpty(n))
                        .Distinct()
                        .Except(callExpr.Args.MaybeEnumerate().Select(a => a.Name).Where(n => !string.IsNullOrEmpty(n)))
                        .Select(n => new MemberResult($"{n}=", PythonMemberType.NamedArgument));

                    argNames = argNames.MaybeEnumerate().ToArray();
                    Log.TraceMessage($"Including {argNames.Count()} named arguments");
                    members = members?.Concat(argNames) ?? argNames;
                }
            }
            return members;
        }

        private GetMemberOptions GetOptions(CompletionContext? context) {
            var opts = GetMemberOptions.None;
            if (context.HasValue) {
                var c = context.Value;
                if (c._intersection) {
                    opts |= GetMemberOptions.IntersectMultipleResults;
                }
                if (c._statementKeywords ?? true) {
                    opts |= GetMemberOptions.IncludeStatementKeywords;
                }
                if (c._expressionKeywords ?? true) {
                    opts |= GetMemberOptions.IncludeExpressionKeywords;
                }
            } else {
                opts = GetMemberOptions.IncludeStatementKeywords | GetMemberOptions.IncludeExpressionKeywords;
            }
            return opts;
        }

        private CompletionItem ToCompletionItem(MemberResult m, GetMemberOptions opts) {
            var res = new CompletionItem {
                label = m.Name,
                insertText = m.Completion,
                documentation = m.Documentation,
                // Place regular items first, advanced entries last
                sortText = Char.IsLetter(m.Completion[0]) ? "1" : "2",
                kind = ToCompletionItemKind(m.MemberType),
                _kind = m.MemberType.ToString().ToLowerInvariant()
            };

            return res;
        }

        private CompletionItemKind ToCompletionItemKind(PythonMemberType memberType) {
            switch (memberType) {
                case PythonMemberType.Unknown: return CompletionItemKind.None;
                case PythonMemberType.Class: return CompletionItemKind.Class;
                case PythonMemberType.Instance: return CompletionItemKind.Value;
                case PythonMemberType.Delegate: return CompletionItemKind.Class;
                case PythonMemberType.DelegateInstance: return CompletionItemKind.Function;
                case PythonMemberType.Enum: return CompletionItemKind.Enum;
                case PythonMemberType.EnumInstance: return CompletionItemKind.EnumMember;
                case PythonMemberType.Function: return CompletionItemKind.Function;
                case PythonMemberType.Method: return CompletionItemKind.Method;
                case PythonMemberType.Module: return CompletionItemKind.Module;
                case PythonMemberType.Namespace: return CompletionItemKind.Module;
                case PythonMemberType.Constant: return CompletionItemKind.Constant;
                case PythonMemberType.Event: return CompletionItemKind.Event;
                case PythonMemberType.Field: return CompletionItemKind.Field;
                case PythonMemberType.Property: return CompletionItemKind.Property;
                case PythonMemberType.Multiple: return CompletionItemKind.Value;
                case PythonMemberType.Keyword: return CompletionItemKind.Keyword;
                case PythonMemberType.CodeSnippet: return CompletionItemKind.Snippet;
                case PythonMemberType.NamedArgument: return CompletionItemKind.Variable;
                default:
                    return CompletionItemKind.None;
            }
        }
    }
}

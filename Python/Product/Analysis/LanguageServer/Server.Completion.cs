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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    public sealed partial class Server {
        private TaskCompletionSource<bool> _analysisCts;

        public override async Task<CompletionList> Completion(CompletionParams @params) {
            await _analyzerCreationTask;
            IfTestWaitForAnalysisComplete();

            var uri = @params.textDocument.uri;
            // Make sure document is enqueued for processing
            var openFile = _openFiles.GetDocument(uri);

            _projectFiles.GetAnalysis(@params.textDocument, @params.position, @params._version, out var entry, out var tree);
            TraceMessage($"Completions in {uri} at {@params.position}");

            tree = GetParseTree(entry, uri, CancellationToken, out _) ?? tree;
            // Give analysis opportunity to complete
            if (!entry.IsComplete) {
                entry.OnNewAnalysis += OnNewAnalysis;
                _analysisCts = new TaskCompletionSource<bool>();
                _analysisCts.Task.Wait(300);
            }

            var analysis = entry?.Analysis;
            if (analysis == null) {
                TraceMessage($"No analysis found for {uri}");
                return new CompletionList();
            }

            var opts = GetOptions(@params.context);
            var ctxt = new CompletionAnalysis(analysis, tree, @params.position, opts, this);
            var members = ctxt.GetCompletionsFromString(@params._expr) ?? ctxt.GetCompletions();

            if (!_settings.ShowAdvancedMembers) {
                members = members.Where(m => !m.label.StartsWith("__"));
            }

            var filterKind = @params.context?._filterKind;
            if (filterKind.HasValue && filterKind != CompletionItemKind.None) {
                TraceMessage($"Only returning {filterKind.Value} items");
                members = members.Where(m => m.kind == filterKind.Value);
            }

            var res = new CompletionList {
                items = members.ToArray(),
                _applicableSpan = ctxt.Node?.GetSpan(tree),
                _expr = ctxt.ParentExpression?.ToCodeString(tree, CodeFormattingOptions.Traditional)
            };
            LogMessage(MessageType.Info, $"Found {res.items.Length} completions for {uri} at {@params.position} after filtering");
            return res;
        }

        public override Task<CompletionItem> CompletionItemResolve(CompletionItem item) {
            // TODO: Fill out missing values in item
            return Task.FromResult(item);
        }

        private void OnNewAnalysis(object sender, EventArgs e) {
            ((ProjectEntry)sender).OnNewAnalysis -= OnNewAnalysis;
            _analysisCts?.SetResult(true);
        }

        private GetMemberOptions GetOptions(CompletionContext? context) {
            var opts = GetMemberOptions.None;
            if (context.HasValue) {
                var c = context.Value;
                if (c._intersection) {
                    opts |= GetMemberOptions.IntersectMultipleResults;
                }
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

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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;
using Microsoft.PythonTools.LanguageServerClient;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.PythonTools.Repl.Completion {
    internal class ReplRemoteCompletionBroker {
        public async Task<Tuple<LSP.CompletionItem, Func<LSP.CompletionItem, Task<LSP.CompletionItem>>>[]> RequestCompletionsAsync(
            SnapshotPoint triggerPoint,
            CancellationToken token,
            CompletionTrigger trigger
        ) {
            var textBuffer = triggerPoint.Snapshot.TextBuffer;
            var client = PythonLanguageClient.FindLanguageClient(textBuffer);
            var window = textBuffer.GetInteractiveWindow();
            var eval = window?.Evaluator as IPythonInteractiveIntellisense;

            if (client != null && client.ReplDocument != null && client.ReplDocument.DocumentUri != null && eval != null) {
                if (eval.LiveCompletionsOnly) {
                    var result = await GetLiveCompletionsAsync(triggerPoint, trigger, eval, token);
                    return ToResolveTaskTuple(result);
                } else {
                    var result = await GetAnalysisAndLiveCompletionsAsync(triggerPoint, trigger, client, eval, token);
                    return ToResolveTaskTuple(result);
                }
            }

            return new Tuple<LSP.CompletionItem, Func<LSP.CompletionItem, Task<LSP.CompletionItem>>>[0];
        }

        public IEnumerable<string> GetCompletionCommitCharacters(IContentType contentType) {
            return new string[0];
        }

        public IEnumerable<string> GetCompletionTriggerCharacters(IContentType contentType) {
            return new string[] { "." };
        }

        public bool IsCompletionTriggerCharacter(IContentType contentType, char typedChar) {
            return typedChar == '.';
        }

        private async Task<LSP.CompletionItem[]> DoRequestAsync(
            PythonLanguageClient client,
            LSP.CompletionParams parameters,
            CancellationToken token = default(CancellationToken)
        ) {
            var res = await client.InvokeTextDocumentCompletionAsync(parameters, token);
            return res?.Items ?? Array.Empty<LSP.CompletionItem>();
        }

        private LSP.CompletionContext GetContextFromTrigger(CompletionTrigger trigger) {
            var context = new LSP.CompletionContext {
                TriggerCharacter = trigger.Character.ToString()
            };

            switch (trigger.Reason) {
                case CompletionTriggerReason.Invoke:
                case CompletionTriggerReason.InvokeAndCommitIfUnique:
                case CompletionTriggerReason.InvokeMatchingType:
                    context.TriggerKind = LSP.CompletionTriggerKind.Invoked;
                    break;

                case CompletionTriggerReason.Backspace:
                case CompletionTriggerReason.Deletion:
                case CompletionTriggerReason.FilterChange:
                case CompletionTriggerReason.Insertion:
                    context.TriggerKind = LSP.CompletionTriggerKind.TriggerCharacter;
                    break;

                default:
                    context.TriggerKind = default(LSP.CompletionTriggerKind);
                    break;
            }

            return context;
        }

        private async Task<LSP.CompletionItem[]> GetAnalysisAndLiveCompletionsAsync(
            SnapshotPoint triggerPoint,
            CompletionTrigger trigger,
            PythonLanguageClient client,
            IPythonInteractiveIntellisense eval,
            CancellationToken token
        ) {
            var analysisCompletionsTask = GetAnalysisCompletionsAsync(triggerPoint, trigger, client, token);
            var liveCompletionsTask = GetLiveCompletionsAsync(triggerPoint, trigger, eval, token);

            await Task.WhenAll(analysisCompletionsTask, liveCompletionsTask);

            var completions = MergeCompletions(analysisCompletionsTask.Result, liveCompletionsTask.Result);
            return completions;
        }

        private async Task<LSP.CompletionItem[]> GetLiveCompletionsAsync(
            SnapshotPoint triggerPoint,
            CompletionTrigger trigger,
            IPythonInteractiveIntellisense eval,
            CancellationToken token
        ) {
            await TaskScheduler.Default;

            string expression = GetExpressionAtPoint(triggerPoint);
            if (!string.IsNullOrEmpty(expression)) {
                var members = await eval.GetMemberNamesAsync(expression, token);

                var completions = members.Select(c => new LSP.CompletionItem {
                    Label = c.Name,
                    InsertText = c.Completion,
                    Documentation = c.Documentation,
                    Kind = LSP.CompletionItemKind.Variable,
                }).ToArray();

                return completions;
            }

            return new LSP.CompletionItem[0];
        }

        private static string GetExpressionAtPoint(SnapshotPoint triggerPoint) {
            // TODO: need to get the expression at the trigger point
            // this code here is just good enough to test simple cases
            // in old codebase, we used to get the expression from the analyzer
            var expression = triggerPoint.Snapshot.GetText();

            var finder = new ExpressionFinder(expression, PythonLanguageVersion.V37, FindExpressionOptions.Complete);
            var Node = finder.GetExpression(triggerPoint.Position);
            if(Node is MemberExpression me) {
                var target = me.Target;
                return target?.ToCodeString(finder.Ast);
            }

            return null;
        }

        private async Task<LSP.CompletionItem[]> GetAnalysisCompletionsAsync(
            SnapshotPoint triggerPoint,
            CompletionTrigger trigger,
            PythonLanguageClient client,
            CancellationToken token
        ) {
            var completionParams = new LSP.CompletionParams() {
                TextDocument = new LSP.TextDocumentIdentifier() {
                    Uri = client.ReplDocument.DocumentUri,
                },
                Position = client.ReplDocument.GetDocumentPosition(triggerPoint.GetPosition()),
            };

            if (trigger != null) {
                completionParams.Context = GetContextFromTrigger(trigger);
            }

            var completions = await DoRequestAsync(client, completionParams, token);

            return completions;
        }

        private static Tuple<LSP.CompletionItem, Func<LSP.CompletionItem, Task<LSP.CompletionItem>>>[] ToResolveTaskTuple(
            LSP.CompletionItem[] completionItems
        ) {
            // Python language server does not support resolving items ie.
            // adding documentation later, so we return a null resolve task.
            return completionItems
                .OrderBy(item => item.Label)
                .Select(item =>
                new Tuple<LSP.CompletionItem, Func<LSP.CompletionItem, Task<LSP.CompletionItem>>>(item, null)
            ).ToArray();
        }

        private LSP.CompletionItem[] MergeCompletions(
            LSP.CompletionItem[] analysisCompletions,
            LSP.CompletionItem[] liveCompletions
        ) {
            // This pretty simplistic comparison but it results in analysis having
            // priority over live completions which is what we want because analysis
            // has more accurate information. 
            return analysisCompletions.Union(liveCompletions, new CompletionItemComparer()).ToArray();
        }

        private class CompletionItemComparer : IEqualityComparer<LSP.CompletionItem> {
            public bool Equals(LSP.CompletionItem x, LSP.CompletionItem y) {
                return x.Label == y.Label;
            }

            public int GetHashCode(LSP.CompletionItem obj) {
                return obj.Label.GetHashCode();
            }
        }
    }
}

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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.LanguageServer.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.PythonTools.LanguageServerClient {
    internal class PythonLanguageClientMiddleLayer : ILanguageClientMiddleLayer {
        private readonly PythonSnippetManager _snippetManager;
        private readonly IInteractiveWindow _replWindow;
        private readonly PythonToolsService _pythonToolsService;

        public PythonLanguageClientMiddleLayer(PythonToolsService pythonToolsService, PythonSnippetManager snippetManager, IInteractiveWindow replWindow) {
            _pythonToolsService = pythonToolsService ?? throw new ArgumentNullException(nameof(pythonToolsService));
            _snippetManager = snippetManager ?? throw new ArgumentNullException(nameof(snippetManager));
            _replWindow = replWindow;
        }

        public bool CanHandle(string methodName) {
            switch (methodName) {
                case "textDocument/completion":
                    return true;
            }

            return false;
        }

        public async Task<JToken> HandleRequestAsync(string methodName, JToken methodParam, Func<JToken, Task<JToken>> sendRequest) {
            var result = await sendRequest(methodParam);

            switch (methodName) {
                case "textDocument/completion":
                    await HandleCompletionAsync(result);
                    break;
                default:
                    break;
            }

            return result;
        }

        public Task HandleNotificationAsync(string methodName, JToken methodParam, Func<JToken, Task> sendNotification) {
            return Task.CompletedTask;
        }

        private async Task HandleCompletionAsync(JToken serverCompletions) {
            // since we're implementing the REPL completion ourselves, we add the completions directly
            // rather than go through the middle layer (which would require extra serialization / deserialization)
            //await AddSnippetCompletionsAsync(serverCompletions);

            //if (_replWindow != null) {
            //    await AddReplCompletionsAsync(serverCompletions);
            //}
        }

        private async Task AddSnippetCompletionsAsync(JToken serverCompletions) {
            var jsonObj = (JObject)serverCompletions;
            var items = (JArray)jsonObj.GetValue("items");

            var snippets = await _snippetManager.GetAvailableSnippetsAsync();
            var snippetObjs = snippets.Select(
                s => new LSP.CompletionItem() {
                    Label = s.shortcut,
                    Documentation = s.description,
                    Kind = LSP.CompletionItemKind.Snippet,
                    InsertText = s.shortcut,
                    InsertTextFormat = LSP.InsertTextFormat.Plaintext // we expand ourselves
                }
            ).Select(JObject.FromObject);

            items.Merge(new JArray(snippetObjs));
        }

        //private async Task AddReplCompletionsAsync(JToken serverCompletions) {
        //    var jsonObj = (JObject)serverCompletions;
        //    var items = (JArray)jsonObj.GetValue("items");

        //    var evaluator = _replWindow.Evaluator as PythonCommonInteractiveEvaluator;
        //    if (_replWindow.Evaluator is SelectableReplEvaluator selEvaluator) {
        //        evaluator = selEvaluator.Evaluator as PythonCommonInteractiveEvaluator;
        //    }

        //    if (evaluator != null) {
        //        // TODO: this is the expression that was returned by the old analyzer
        //        var expression = string.Empty;
        //        var replMembers = await evaluator.GetMemberNamesAsync(expression);

        //        var replObjs = replMembers.Select(
        //            m => new LSP.CompletionItem() {
        //                Label = m.Name,
        //                Documentation = m.Documentation,
        //                Kind = LSP.CompletionItemKind.Variable, // TODO: map this type
        //                InsertText = m.Completion,
        //                InsertTextFormat = LSP.InsertTextFormat.Plaintext,
        //            }
        //        ).Select(JObject.FromObject);

        //        if (_pythonToolsService.InteractiveOptions.LiveCompletionsOnly) {
        //            items.Clear();
        //            items.Merge(new JArray(replObjs));
        //        } else if (replObjs.Any()) {
        //            // TODO: finish this, faster/better to merge JObject(s)
        //            // or deserialize the CompletionList, merge that and then serialize back?

        //            //var completions = serverCompletions.ToObject<LSP.CompletionList>();

        //            //items.Union()
        //            //foreach (var completionItem in replCompletions) {
        //            //    var completionObj = JObject.FromObject(completionItem);
        //            //    items.Add(completionItem);
        //            //}

        //            //members = members.Union(replMembers, CompletionMergeKeyComparer.Instance);
        //        }
        //    }
        //}
    }
}

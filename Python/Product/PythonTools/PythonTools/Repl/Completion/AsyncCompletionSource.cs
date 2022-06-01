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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Internal.VisualStudio.Shell.Interop;
using Microsoft.PythonTools.LanguageServerClient;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Threading;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;
using CompletionContext = Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data.CompletionContext;
using CompletionItem = Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data.CompletionItem;
using ServiceProvider = Microsoft.VisualStudio.Shell.ServiceProvider;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.Text.Projection;

namespace Microsoft.PythonTools.Repl.Completion {
    /// <summary>
    /// This class implements auto complete for the REPL windows
    /// </summary>
    /// <remarks>
    /// Theoretically this wouldn't be necessary if LanguageServer.Client.Implementation supported projection buffers.
    /// See issue: https://dev.azure.com/devdiv/DevDiv/_workitems/edit/1321167
    /// Most of the code in this file was copied from the LanguageServer.Client.Implementaton and adapted to work with ILanguageClient
    /// </remarks>
    internal class AsyncCompletionSource : IAsyncCompletionUniversalSource {
        public const string ResolvePropertyKey = "resolve";
        public const string ProtocolItemKey = "protocolItem";
        public const string LanguageClientKey = "languageClient";
        public const string TriggerPointKey = "triggerPoint";
        public const string IsIncompletePropertyName = "isIncomplete";

        /// <summary>
        /// Command used by Razor to retrigger completion
        /// </summary>
        private const string VSCodeTriggerCompletionCommandName = "editor.action.triggerSuggest";

        private readonly ITextView textView;
        private readonly ITextStructureNavigatorSelectorService navigatorService;
        private readonly IAsyncCompletionBroker editorCompletionBroker;
        private readonly PythonLanguageClient languageClient;
        private readonly string[] delimiterCharacters = new[] { " ", "\r", "\n", "\t" };
        private readonly char[] typicalDimissChars = new[] { ';', ' ' };
        private readonly char[] triggerCharacters;
        private readonly ImmutableHashSet<char> serverCommitCharacters;

        private CancellationToken retriggerToken;

        public AsyncCompletionSource(
            ITextView textView,
            ITextStructureNavigatorSelectorService navigatorService,
            IAsyncCompletionBroker editorCompletionBroker,
            PythonLanguageClient languageClient)
        {
            Requires.NotNull(textView, nameof(textView));
            Requires.NotNull(navigatorService, nameof(navigatorService));

            this.textView = textView;
            this.editorCompletionBroker = editorCompletionBroker;
            this.navigatorService = navigatorService;
            this.languageClient = languageClient;

            Requires.NotNull(this.navigatorService, nameof(this.navigatorService));

            // Cancel requests which are no longer needed and send telemetry
            this.textView.Closed += this.OnTextViewClosed;

            // Trigger characters actually come from Pylance but we know what they are.
            this.triggerCharacters = new char[] { '.', '[' };

            // Pylance does not support commit characters
            this.serverCommitCharacters = new char[] { }.ToImmutableHashSet();
        }

        public async Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token) {
            try {
                await this.ResolveCompletionItemAsync(item, token);
            } catch (Exception ex) when (!(ex is OperationCanceledException)) {
                return null;
            }

            if (token.IsCancellationRequested) {
                return null;
            }

            if (!item.Properties.TryGetProperty(ProtocolItemKey, out LSP.CompletionItem protocolItem)) {
                return null;
            }

            // Support the Detail property, but keep in mind that its use for tooltip will be deprecated soon.
            // We will use Detail property for CompletionItem.Suffix
            if (!string.IsNullOrWhiteSpace(protocolItem.Detail)) {
                return protocolItem.Detail;
            }

            // otherwise, parse the Documentation property
            if (protocolItem.Documentation != null) {
                var documentationBuilder = new StringBuilder();
                var content = protocolItem.Documentation.Value.Match(
                    s => s,
                    markupContent => {
                        if (markupContent.Kind == LSP.MarkupKind.Markdown) {
                            var codeBlocks = MarkdownUtil.ExtractCodeBlocks(markupContent.Value);
                            var joinedBlocks = string.Join(Environment.NewLine, codeBlocks);
                            if (string.IsNullOrEmpty(joinedBlocks)) {
                                return null;
                            }

                            return joinedBlocks;
                        }
                        else {
                            return markupContent.Value;
                        }
                    });

                if (!string.IsNullOrEmpty(content)) {
                    if (documentationBuilder.Length > 0) {
                        documentationBuilder.AppendLine();
                    }

                    documentationBuilder.Append(content);
                }

                return documentationBuilder.ToString();
            }

            return null;
        }

        public CompletionStartData InitializeCompletion(CompletionTrigger trigger, VisualStudio.Text.SnapshotPoint triggerLocation, CancellationToken token) {
            if (!this.ShouldTriggerCompletion(trigger, triggerLocation)) {
                return CompletionStartData.DoesNotParticipateInCompletion;
            } else {
                if (trigger.Reason == CompletionTriggerReason.Insertion) {
                    if (triggerCharacters.Contains(trigger.Character)) {
                        // When trigger character is typed,
                        // completion should be applicable to everything typed after the trigger character,
                        // i.e. start with length 0 at the trigger location
                        return new CompletionStartData(CompletionParticipation.ProvidesItems, new SnapshotSpan(triggerLocation, 0));
                    } else {
                        // When non trigger character is typed, completion should be applicable to the entire word
                        var applicableToSpan = this.GetApplicableToSpan(this.textView.TextSnapshot, triggerLocation.Position > 0 ? triggerLocation - 1 : triggerLocation);
                        return new CompletionStartData(CompletionParticipation.ProvidesItems, applicableToSpan);
                    }
                } else {
                    // When completion is invoked by other means than typing, it should be applicable to the entire word
                    var applicableToSpan = this.GetApplicableToSpan(this.textView.TextSnapshot, triggerLocation);
                    return new CompletionStartData(CompletionParticipation.ProvidesItems, applicableToSpan);
                }
            }
        }

        public CompletionContinuation HandleTypedChar(IAsyncCompletionSession session, VisualStudio.Language.Intellisense.AsyncCompletion.Data.CompletionItem selectedItem, SnapshotPoint location, char typedChar, CancellationToken token) {
            if (this.serverCommitCharacters.Contains(typedChar)) {
                return CompletionContinuation.Commit;
            }

            if (selectedItem?.CommitCharacters.Contains(typedChar) == true) {
                return CompletionContinuation.Commit;
            }

            if (this.ShouldContinue(typedChar)) {
                return CompletionContinuation.Continue;
            }

            return CompletionContinuation.Dismiss;
        }

        public CommitResult TryCommit(IAsyncCompletionSession session, ITextBuffer buffer, CompletionItem item, char typedChar, CancellationToken token) {
            if (item.Properties.TryGetProperty(AsyncCompletionSource.ProtocolItemKey, out LSP.CompletionItem protocolItem) &&
                item.Properties.TryGetProperty(AsyncCompletionSource.TriggerPointKey, out SnapshotPoint triggerLocation)) {
                var commitArray = protocolItem.CommitCharacters ?? new string[] { }; // Commit chars are normally empty

                // Tab, Enter and programmatical command should always commit regardless of commit characters on individual items.
                if (typedChar != '\0' &&
                    typedChar != '\t' &&
                    typedChar != '\n' &&
                    commitArray != null) {
                    bool commitCharacterFound = false;
                    for (var n = 0; n < commitArray.Length; n++) {
                        var commitString = commitArray[n];
                        if (!string.IsNullOrEmpty(commitString) &&
                            commitString[0] == typedChar) {
                            commitCharacterFound = true;
                        }
                    }

                    if (!commitCharacterFound) {
                        return new CommitResult(isHandled: false, behavior: CommitBehavior.CancelCommit);
                    }
                }

                // Contract with Roslyn: if an item has no insert text and no text edit,
                // attempt to resolve it to get these values.
                // We don't call resolve for all items because it's a network call on a typing hot path.
                if (protocolItem.InsertText == null && protocolItem.TextEdit == null
                    && item.Properties.TryGetProperty(ResolvePropertyKey, out Func<LSP.CompletionItem, CancellationToken, Task<LSP.CompletionItem>> resolver)
                    && resolver != null) {
                    try {
                        ThreadHelper.JoinableTaskFactory.Run(async () => {
                            await this.ResolveCompletionItemAsync(item, token);
                        });
                    } catch (Exception ex) when (!(ex is OperationCanceledException)) {
                        // We have not received the resolved item due to an error.
                        return new CommitResult(isHandled: false, behavior: CommitBehavior.CancelCommit);
                    }

                    if (token.IsCancellationRequested) {
                        // We have not received the resolved item due to a timeout.
                        return new CommitResult(isHandled: false, behavior: CommitBehavior.CancelCommit);
                    }
                }

                if (protocolItem.TextEdit != null || protocolItem.AdditionalTextEdits != null) {
                    // Completion text edits are computed when the completion session is first triggered. The lines typed
                    // after the completion session was started need to be deleted to revert the document to its original state.
                    var caretPositionAtBuffer = session.TextView.GetCaretPointAtSubjectBuffer(buffer);
                    if (caretPositionAtBuffer.HasValue) {
                        var deleteTextLength = caretPositionAtBuffer.Value.Position - triggerLocation.Position;
                        if (deleteTextLength > 0) {
                            var deleteSpan = new Span(triggerLocation.Position, deleteTextLength);
                            buffer.Delete(deleteSpan);
                        }

                        if (protocolItem.TextEdit != null) {
                            Utilities.ApplyTextEdit(protocolItem.TextEdit, triggerLocation.Snapshot, buffer);
                        } else if (protocolItem.InsertText != null) {
                            buffer.Replace(session.ApplicableToSpan.GetSpan(buffer.CurrentSnapshot), protocolItem.InsertText);
                        } else if (protocolItem.Label != null) {
                            buffer.Replace(session.ApplicableToSpan.GetSpan(buffer.CurrentSnapshot), protocolItem.Label);
                        }

                        if (protocolItem.AdditionalTextEdits != null) {
                            Utilities.ApplyTextEdits(protocolItem.AdditionalTextEdits, triggerLocation.Snapshot, buffer);
                        }

                        this.textView.Caret.EnsureVisible();

                        this.ExecuteCompletionCommand(languageClient, protocolItem, token);
                        return CommitResult.Handled;
                    }
                }

                this.ExecuteCompletionCommand(languageClient, protocolItem, token);
            }

            return CommitResult.Unhandled;
        }

        /// <summary>
        /// For LSP-style sessions (from d16.8 on)
        /// </summary>
        public async Task<CompletionContext> GetCompletionContextAsync(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token) {
            if (trigger.Reason == CompletionTriggerReason.Insertion
                && (!char.IsLetterOrDigit(trigger.Character) && !this.triggerCharacters.Contains(trigger.Character))) {
                // When typing, don't request completion unless user typed a trigger character or an identifier
                return CompletionContext.Empty;
            }

            // LSP spec requires that triggerKind is TriggerChar only if the character is among these designated as trigger characters.
            var isRegisteredTriggerCharacter = trigger.Reason == CompletionTriggerReason.Insertion && this.triggerCharacters.Contains(trigger.Character);
            var requestContext = Utilities.GetContextFromTrigger(trigger, isRegisteredTriggerCharacter);

            // It is important that this is the first await on the method to ensure ordering, awaiting on something else before this might scrambling the order of calls
            var completionContextTask = await this.GetCompletionContextTaskAsync(requestContext, triggerLocation, token).ConfigureAwait(false);

            if (token.IsCancellationRequested) {
                return CompletionContext.Empty; 
            }

            return await completionContextTask.ConfigureAwait(false);
        }

        /// <summary>
        /// For Roslyn-style sessions (up until d16.7)
        /// </summary>
        public async Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token) {
            // LSP spec requires that triggerKind is TriggerChar only if the character is among these designated as trigger characters.
            var isRegisteredTriggerCharacter = trigger.Reason == CompletionTriggerReason.Insertion && this.triggerCharacters.Contains(trigger.Character);
            var requestContext = Utilities.GetContextFromTrigger(trigger, isRegisteredTriggerCharacter);

            // It is important that this is the first await on the method to ensure ordering, awaiting on something else before this might scrambling the order of calls
            var completionContextTask = await this.GetCompletionContextTaskAsync(requestContext, triggerLocation, token).ConfigureAwait(false);

            return await completionContextTask.ConfigureAwait(false);
        }

        private async Task<Task<CompletionContext>> GetCompletionContextTaskAsync(LSP.CompletionContext requestContext, SnapshotPoint triggerLocation, CancellationToken token) {
            // TODO: Should this be using the ILanguageServiceBroker2 calls instead? That will include the necessary sync that the LanguageServiceBroker does
            if (token.IsCancellationRequested) {
                return System.Threading.Tasks.Task.FromResult(CompletionContext.Empty);
            }

            if (!triggerLocation.Snapshot.TextBuffer.IsReplBuffer()) {
                return System.Threading.Tasks.Task.FromResult(CompletionContext.Empty);
            }

            var itemBag = new ConcurrentBag<(ILanguageClient, LSP.CompletionItem, Func<LSP.CompletionItem, CancellationToken, Task<LSP.CompletionItem>>)>();
            var isIncomplete = false;

            Func<(ILanguageClient client, LSP.CompletionList completionList), System.Threading.Tasks.Task> progressAction = (partialResults) => {
                if (!(partialResults.client is PythonLanguageClient clientInstance)) {
                    return System.Threading.Tasks.Task.CompletedTask;
                }

                Func<LSP.CompletionItem, CancellationToken, Task<LSP.CompletionItem>> resolveCompletionTask = (i, c) => clientInstance.InvokeResolveAsync(i, c);
                foreach (var completionItem in partialResults.completionList.Items) {
                    itemBag.Add((clientInstance, completionItem, resolveCompletionTask));
                }

                isIncomplete |= partialResults.completionList.IsIncomplete;

                return System.Threading.Tasks.Task.CompletedTask;
            };

            // The following call enqueues the RPC request. We may not yield (await) before this call.
            var requestTask = await GetCompletionRequestAsync(triggerLocation, triggerLocation.Snapshot.TextBuffer, requestContext, token, progressAction)
                .ConfigureAwait(false);

            return this.CompletionResultsToContextAsync(requestTask, triggerLocation, itemBag, isIncomplete, token);
        }

        public async Task<Task<CompletionResults>> GetCompletionRequestAsync(
            SnapshotPoint triggerPoint,
            ITextBuffer documentBuffer,
            LSP.CompletionContext requestContext,
            CancellationToken token,
            Func<(ILanguageClient, LSP.CompletionList), System.Threading.Tasks.Task> progressAction = null) {
            bool isIncomplete = false;
            var items = new List<(ILanguageClient, LSP.CompletionItem, Func<LSP.CompletionItem, CancellationToken, Task<LSP.CompletionItem>>)>();
            var suggestionMode = false;

            // We need to use the REPL to make the request
            if (documentBuffer.Properties.TryGetProperty(typeof(IInteractiveEvaluator), out IInteractiveEvaluator evaluator) &&
                evaluator is SelectableReplEvaluator replEvaluator) { 
                var position = triggerPoint.GetPosition();
                var task = replEvaluator.GetAnalysisCompletions(position, requestContext, token);
                return this.CompletionListsToCompletionResultsAsync(task);
            }

            var results = new CompletionResults() { ResultsAreIncomplete = isIncomplete, Items = items.ToArray(), SuggestionMode = suggestionMode };
            return System.Threading.Tasks.Task.FromResult(results);
        }

        private async Task<CompletionResults> CompletionListsToCompletionResultsAsync(
            Task<object> completionTask) {
            var items = new List<(ILanguageClient client, LSP.CompletionItem completionItem, Func<LSP.CompletionItem, CancellationToken, Task<LSP.CompletionItem>> resolver)>();
            var suggestionMode = false;
            object taskResults = await completionTask.ConfigureAwait(false);
            var converted = ResultConverter.ConvertResult(taskResults as Newtonsoft.Json.Linq.JObject) as LSP.CompletionList[];
            Func<LSP.CompletionItem, CancellationToken, Task<LSP.CompletionItem>> resolveFunc = (c, t) => this.languageClient.InvokeResolveAsync(c, t);
            if (converted != null) {
                foreach (var completionItem in converted[0].Items) {
                    items.Add((this.languageClient, completionItem as LSP.CompletionItem, resolveFunc));
                }
            }

            return new CompletionResults() { ResultsAreIncomplete = converted != null ? converted[0].IsIncomplete : false, Items = items.ToArray(), SuggestionMode = suggestionMode };
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "Request needs to be prepared and called separately for synchronization")]
        private async Task<CompletionContext> CompletionResultsToContextAsync(
            Task<CompletionResults> data,
            SnapshotPoint triggerLocation,
            ConcurrentBag<(ILanguageClient, LSP.CompletionItem, Func<LSP.CompletionItem, CancellationToken, Task<LSP.CompletionItem>>)> itemBag,
            bool isIncomplete,
            CancellationToken token) {
            CompletionResults results;
            try {
                // Await the response to the RPC request
                results = await data.ConfigureAwait(false);
            } catch (Exception ex) when (!(ex is OperationCanceledException)) {
                throw;
            }

            if (token.IsCancellationRequested) {
                return CompletionContext.Empty;
            }
            if (triggerLocation.Snapshot.Version != triggerLocation.Snapshot.TextBuffer.CurrentSnapshot.Version) {
                return CompletionContext.Empty;
            }

            SuggestionItemOptions suggestionItemOptions = null;
            if (results.SuggestionMode) {
                suggestionItemOptions = new SuggestionItemOptions(string.Empty, string.Empty);
            }

            List<(ILanguageClient client, LSP.CompletionItem completionItem, Func<LSP.CompletionItem, CancellationToken, Task<LSP.CompletionItem>> resolver)> mergedItemList = itemBag.Concat(results.Items).ToList();
            var completionKindSet = new HashSet<LSP.CompletionItemKind>(mergedItemList.Select(value => value.completionItem.Kind));
            var completionFilters = new Dictionary<LSP.CompletionItemKind?, CompletionFilter>();
            foreach (var completionKind in completionKindSet) {
                var filterName = Utilities.GetFilterName(completionKind);
                var filterImage = Utilities.GetCompletionImage(completionKind);

                if (!string.IsNullOrEmpty(filterName) && filterImage != null) {
                    completionFilters.Add(completionKind, new CompletionFilter(filterName, filterName.Substring(0, 1), filterImage));
                }
            }

            // Initialize filters as available but not selected
            var allFilters = completionFilters.Values.Select(n => new CompletionFilterWithState(n, isAvailable: true)).ToImmutableArray();

            var itemsBuilder = ImmutableArray.CreateBuilder<CompletionItem>(mergedItemList.Count);
            foreach (var mergedItem in mergedItemList) {
                try {
                    var completionItem = this.CreateCompletionItem(mergedItem.client, mergedItem.completionItem, mergedItem.resolver, triggerLocation, completionFilters);
                    itemsBuilder.Add(completionItem);
                } catch (Exception) {
                    // It appears that we are in a broken state. This might be caused by coauthoring service
                    // taking mergedItem.client's snapshot out of sync of the triggerLocation.Snapshot.
                    // Send telemetry and don't provide any completion items.
                    return CompletionContext.Empty;
                }
            }

            return new CompletionContext(
                items: itemsBuilder.ToImmutable(),
                suggestionItemOptions: suggestionItemOptions,
                selectionHint: InitialSelectionHint.RegularSelection,
                filters: allFilters,
                isIncomplete: results.ResultsAreIncomplete | isIncomplete);
        }

        private SnapshotSpan GetApplicableToSpan(ITextSnapshot owningSnapshot, SnapshotPoint applicablePoint, LSP.CompletionItem item = null) {
            SnapshotSpan result = new SnapshotSpan();

            // See if this is from a text edit
            if (item?.TextEdit != null) {
                result = item.TextEdit.Range.ToSnapshotSpan(applicablePoint.Snapshot);
            } else {
                // Fallback to extent of word. At least, it will be a zero-length span at the trigger pointzero-
                // walk left and right until we reach the end of the word
                var leftExtent = applicablePoint;
                var rightExtent = applicablePoint;
                var stopCharacters = this.delimiterCharacters.Union(this.triggerCharacters.Select(c => c.ToString()));
                if (item != null && item.CommitCharacters?.Length > 0) {
                    stopCharacters = stopCharacters.Union(item.CommitCharacters);
                }

                while (leftExtent.Position > 0 && !stopCharacters.Contains(GetStringAt(leftExtent - 1))) {
                    leftExtent -= 1;
                }

                while (rightExtent.Position < applicablePoint.Snapshot.Length - 1 && !stopCharacters.Contains(GetStringAt(rightExtent))) {
                    rightExtent += 1;
                }

                result = new SnapshotSpan(leftExtent, rightExtent);
            }

            // If the original snapshot is a projection snapshot, map up to it
            if (owningSnapshot is IProjectionSnapshot projectionSnapshot) {
                var spans = projectionSnapshot.MapFromSourceSnapshot(result);
                if (spans != null && spans.Count > 0) {
                    result = new SnapshotSpan(projectionSnapshot, spans[0]);
                }
            }

            return result;


            string GetStringAt(SnapshotPoint point) {
                return point.GetChar().ToString(CultureInfo.InvariantCulture);
            }
        }

        private bool ShouldTriggerCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation) {
            if (!triggerLocation.Snapshot.TextBuffer.IsReplBuffer()) {
                return false;
            }

            if (trigger.Reason == CompletionTriggerReason.Invoke || trigger.Reason == CompletionTriggerReason.InvokeAndCommitIfUnique || trigger.Reason == CompletionTriggerReason.InvokeMatchingType) {
                return true;
            }

            // Enter does not trigger completion.
            if (trigger.Reason == CompletionTriggerReason.Insertion && trigger.Character == '\n') {
                return false;
            }

            if (trigger.Reason == CompletionTriggerReason.Backspace || trigger.Reason == CompletionTriggerReason.Deletion) {
                return false;
            }

            if (char.IsLetter(trigger.Character)) {
                return true;
            }

            if (trigger.Character.Equals(char.MinValue)) {
                return false;
            }

            return this.triggerCharacters.Contains(trigger.Character);
        }

        private CompletionItem CreateCompletionItem(ILanguageClient client, LSP.CompletionItem item, Func<LSP.CompletionItem, CancellationToken, Task<LSP.CompletionItem>> resolver, SnapshotPoint triggerLocation, Dictionary<LSP.CompletionItemKind?, CompletionFilter> completionFilters) {
            var completionImage = Utilities.GetCompletionImage(item.Kind);
            var completionItemFilters = ImmutableArray<CompletionFilter>.Empty;
            if (completionFilters.TryGetValue(item.Kind, out CompletionFilter filter)) {
                completionItemFilters = ImmutableArray.Create<CompletionFilter>(new CompletionFilter[] { filter });
            }

            var completionItem = new CompletionItem(
                displayText: item.Label,
                source: this,
                icon: completionImage,
                filters: completionItemFilters,
                suffix: string.Empty,
                insertText: item.InsertText ?? item.Label,
                sortText: item.SortText ?? item.Label,
                filterText: item.FilterText ?? item.Label,
                automationText: item.Label,
                attributeIcons: ImmutableArray<ImageElement>.Empty,
                commitCharacters: new char[0].ToImmutableArray(),
                applicableToSpan: this.GetApplicableToSpan(this.textView.TextSnapshot, triggerLocation, item),
                isCommittedAsSnippet: item.InsertTextFormat == LSP.InsertTextFormat.Snippet,
                isPreselected: item.Preselect);

            completionItem.Properties.AddProperty(ResolvePropertyKey, resolver);
            completionItem.Properties.AddProperty(LanguageClientKey, client);
            completionItem.Properties.AddProperty(ProtocolItemKey, item);
            completionItem.Properties.AddProperty(TriggerPointKey, triggerLocation);

            return completionItem;
        }

        /// <summary>
        /// Mutates <paramref name="completionItem"/> in the attempt to resolve properties
        /// of <see cref="LSP.CompletionItem"/> stored within <see cref="CompletionItem.Properties"/>.
        /// If properties have been already resolved, the method returns immediately.
        /// If resolution was impossible, the method returns immediately.
        /// </summary>
        /// <param name="completionItem"><see cref="CompletionItem"/> whose properties will be resolved.</param>
        /// <param name="token">Cancellation token</param>
        private async System.Threading.Tasks.Task ResolveCompletionItemAsync(CompletionItem completionItem, CancellationToken token) {
            if (completionItem.Properties.TryGetProperty(ProtocolItemKey, out LSP.CompletionItem protocolItem)) {
                if (completionItem.Properties.TryGetProperty(ResolvePropertyKey, out Func<LSP.CompletionItem, CancellationToken, Task<LSP.CompletionItem>> resolver) &&
                    resolver != null) {
                    var resolvedProtocolItem = await resolver(protocolItem, token).ConfigureAwait(false);
                    AugmentCompletionItem(protocolItem, resolvedProtocolItem);

                    // Remove the property so that we resolve only once
                    completionItem.Properties.RemoveProperty(ResolvePropertyKey);
                }
            }
        }

        /// <summary>
        /// Copy resolvable properties from <paramref name="resolved"/> to <paramref name="unresolved"/>.
        /// </summary>
        /// <param name="unresolved">Destination of properties</param>
        /// <param name="resolved">Source of properties</param>
        private static void AugmentCompletionItem(LSP.CompletionItem unresolved, LSP.CompletionItem resolved) {
            if (resolved == null) {
                return;
            }

            unresolved.TextEdit = resolved.TextEdit;
            unresolved.AdditionalTextEdits = resolved.AdditionalTextEdits;

            unresolved.Documentation = resolved.Documentation;

            // Update the Detail property, but keep in mind that soon,
            // it will be used as CompletionItem.Suffix and will need to be always available.
            unresolved.Detail = resolved.Detail;
        }

        private bool ShouldContinue(char typedChar) {
            for (var n = 0; n < this.typicalDimissChars.Length; n++) {
                if (typedChar == this.typicalDimissChars[n]) {
                    return false;
                }
            }

            return true;
        }

        private void ExecuteCompletionCommand(ILanguageClient client, LSP.CompletionItem completionItem, CancellationToken token) {
            if (completionItem.Command == null || token.IsCancellationRequested) {
                return;
            }

            var executeCommandParams = new LSP.ExecuteCommandParams() {
                Command = completionItem.Command.CommandIdentifier,
                Arguments = completionItem.Command.Arguments,
            };

            // Special handling for retriggering completion:
            // So far, language servers use the VS Code command name to retrigger.
            if (executeCommandParams.Command == VSCodeTriggerCompletionCommandName
                && this.editorCompletionBroker != null) {
                // Retrigger as soon as the current session dismisses
                this.retriggerToken = token;
                this.editorCompletionBroker.GetSession(this.textView).Dismissed += this.RetriggerWhenDismissed;
            } else {
                this.languageClient.InvokeCommandAsync(executeCommandParams, token).DoNotWait();
            }
        }

        /// <summary>
        /// Handle <see cref="IAsyncCompletionSession.Dismissed"/> event
        /// by triggering a new completion session. We expect that as a result of triggering new session,
        /// Editor will call into our <see cref="GetCompletionContextAsync(CompletionTrigger, SnapshotPoint, CancellationToken)"/>.
        /// </summary>
        /// <remarks>
        /// There can be only one session active at a time, and attempt to trigger a completion session
        /// results in a no-op when dispatched when existing session is active.
        /// When session dispatches the <see cref="IAsyncCompletionSession.Dismissed"/> event,
        /// it is safe to trigger a new session.
        /// </remarks>
        private void RetriggerWhenDismissed(object sender, EventArgs e) {
            if (this.retriggerToken.IsCancellationRequested) {
                return;
            }

            this.editorCompletionBroker?.TriggerCompletion(
                textView: this.textView,
                trigger: new CompletionTrigger(CompletionTriggerReason.Invoke, this.textView.TextSnapshot),
                triggerLocation: this.textView.Caret.Position.BufferPosition,
                token: this.retriggerToken);
        }

        private void OnTextViewClosed(object sender, EventArgs e) {
            this.textView.Closed -= this.OnTextViewClosed;
        }
    }
}

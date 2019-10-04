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
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using CompletionContext = Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data.CompletionContext;
using CompletionItem = Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data.CompletionItem;

namespace Microsoft.PythonTools.Repl.Completion {
    internal class ReplAsyncCompletionSource : IAsyncCompletionSource {
        private readonly ReplRemoteCompletionBroker _broker;
        private readonly ITextView _textView;
        private readonly ITextStructureNavigatorSelectorService _navigatorService;

        private const string ResolvePropertyKey = "resolve";
        public const string ProtocolItemKey = "protocolItem";
        public const string TriggerPointKey = "triggerPoint";

        public ReplAsyncCompletionSource(ReplRemoteCompletionBroker broker, ITextView textView, ITextStructureNavigatorSelectorService textStructureNavigatorSelectorService) {
            _broker = broker;
            _textView = textView;
            _navigatorService = textStructureNavigatorSelectorService;
        }

        public async Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token) {
            var completionResults = await _broker.RequestCompletionsAsync(triggerLocation, token, trigger);

            var completionKindSet = new HashSet<CompletionItemKind>(completionResults.Select(value => value.Item1.Kind));
            var completionFilters = new Dictionary<CompletionItemKind, CompletionFilter>();
            foreach (var completionKind in completionKindSet) {
                var completionKindName = Enum.GetName(typeof(CompletionItemKind), completionKind);
                var completionKindImage = GetCompletionImage(completionKind);

                if (!string.IsNullOrEmpty(completionKindName) && completionKindImage != null) {
                    completionFilters.Add(completionKind, new CompletionFilter(completionKindName, completionKindName.Substring(0, 1), completionKindImage));
                }
            }

            var processedResults = completionResults.Select(value => CreateCompletion(value.Item1, value.Item2, triggerLocation, completionFilters)).ToImmutableArray();

            var selectionHint = _broker.IsCompletionTriggerCharacter(triggerLocation.Snapshot.ContentType, trigger.Character)
                ? InitialSelectionHint.SoftSelection
                : InitialSelectionHint.RegularSelection;
            return new CompletionContext(processedResults, suggestionItemOptions: null, selectionHint: selectionHint);
        }

        public async Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token) {
            if (item.Properties.TryGetProperty(ResolvePropertyKey, out Func<Microsoft.VisualStudio.LanguageServer.Protocol.CompletionItem, Task<Microsoft.VisualStudio.LanguageServer.Protocol.CompletionItem>> resolver) &&
                resolver != null &&
                item.Properties.TryGetProperty(ProtocolItemKey, out Microsoft.VisualStudio.LanguageServer.Protocol.CompletionItem unresolvedItem)) {
                var resolvedItem = await resolver(unresolvedItem);
                if (resolvedItem != null) {
                    unresolvedItem.TextEdit = unresolvedItem.TextEdit ?? resolvedItem.TextEdit;
                    unresolvedItem.AdditionalTextEdits = unresolvedItem.AdditionalTextEdits ?? resolvedItem.AdditionalTextEdits;
                    return resolvedItem?.Detail;
                }
            }

            return null;
        }

        public CompletionStartData InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token) {
            if (!ShouldTriggerCompletion(trigger, triggerLocation)) {
                return CompletionStartData.DoesNotParticipateInCompletion;
            }

            if (triggerLocation > 0 &&
                !_broker.IsCompletionTriggerCharacter(triggerLocation.Snapshot.ContentType, (triggerLocation - 1).GetChar())) {
                var navigator = _navigatorService.GetTextStructureNavigator(triggerLocation.Snapshot.TextBuffer);
                var extent = navigator.GetExtentOfWord(triggerLocation - 1);

                if (extent.IsSignificant) {
                    // This code ensures that if the caret is right after a non-alphanumeric character, e.g. '(',
                    // or between non-alphanumeric characters, e.g. '""' or '<>', then we don't count those as part of
                    // the applicableTo span and neither filter the list to matches, nor delete those characters when we insert the item.
                    var extentText = extent.Span.GetText();
                    if (extentText.Length <= 2 && extentText.All(c => !char.IsLetterOrDigit(c))) {
                        return new CompletionStartData(CompletionParticipation.ProvidesItems, new SnapshotSpan(triggerLocation, triggerLocation));
                    }

                    return new CompletionStartData(CompletionParticipation.ProvidesItems, extent.Span);
                }

            }

            return new CompletionStartData(CompletionParticipation.ProvidesItems, new SnapshotSpan(triggerLocation, 0));
        }

        private bool ShouldTriggerCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation) {
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

            return _broker.IsCompletionTriggerCharacter(triggerLocation.Snapshot.ContentType, trigger.Character);
        }

        private CompletionItem CreateCompletion(
            Microsoft.VisualStudio.LanguageServer.Protocol.CompletionItem item,
            Func<Microsoft.VisualStudio.LanguageServer.Protocol.CompletionItem, Task<Microsoft.VisualStudio.LanguageServer.Protocol.CompletionItem>> resolver,
            SnapshotPoint triggerLocation,
            Dictionary<CompletionItemKind, CompletionFilter> completionFilters
        ) {
            var completionImage = GetCompletionImage(item.Kind);

            var completionItemFilters = ImmutableArray<CompletionFilter>.Empty;
            if (completionFilters.TryGetValue(item.Kind, out CompletionFilter filter)) {
                completionItemFilters = ImmutableArray.Create<CompletionFilter>(new CompletionFilter[] { filter });
            }

            var completionItem = new CompletionItem(item.Label, this, completionImage, completionItemFilters, string.Empty, item.InsertText ?? item.Label, item.SortText ?? item.Label, item.FilterText ?? item.Label, ImmutableArray<ImageElement>.Empty);
            completionItem.Properties.AddProperty(ResolvePropertyKey, resolver);
            completionItem.Properties.AddProperty(ProtocolItemKey, item);
            completionItem.Properties.AddProperty(TriggerPointKey, triggerLocation);

            return completionItem;
        }

        private ImageElement GetCompletionImage(CompletionItemKind kind) {
            var moniker = KnownMonikers.Method;
            switch (kind) {
                case CompletionItemKind.Text:
                    moniker = KnownMonikers.TextElement;
                    break;
                case CompletionItemKind.Method:
                    moniker = KnownMonikers.MethodPublic;
                    break;
                case CompletionItemKind.Function:
                    moniker = KnownMonikers.MethodPublic;
                    break;
                case CompletionItemKind.Constructor:
                    moniker = KnownMonikers.ClassPublic;
                    break;
                case CompletionItemKind.Field:
                    moniker = KnownMonikers.FieldPrivate;
                    break;
                case CompletionItemKind.Variable:
                    moniker = KnownMonikers.LocalVariable;
                    break;
                case CompletionItemKind.Class:
                    moniker = KnownMonikers.ClassPublic;
                    break;
                case CompletionItemKind.Interface:
                    moniker = KnownMonikers.InterfacePublic;
                    break;
                case CompletionItemKind.Module:
                    moniker = KnownMonikers.ModulePublic;
                    break;
                case CompletionItemKind.Property:
                    moniker = KnownMonikers.PropertyPublic;
                    break;
                case CompletionItemKind.Unit:
                    moniker = KnownMonikers.Numeric;
                    break;
                case CompletionItemKind.Value:
                    moniker = KnownMonikers.ValueType;
                    break;
                case CompletionItemKind.Enum:
                    moniker = KnownMonikers.EnumerationPublic;
                    break;
                case CompletionItemKind.Keyword:
                    moniker = KnownMonikers.IntellisenseKeyword;
                    break;
                case CompletionItemKind.Snippet:
                    moniker = KnownMonikers.Snippet;
                    break;
                case CompletionItemKind.Color:
                    moniker = KnownMonikers.ColorPalette;
                    break;
                case CompletionItemKind.File:
                    moniker = KnownMonikers.TextFile;
                    break;
                case CompletionItemKind.Reference:
                    moniker = KnownMonikers.Reference;
                    break;
                case CompletionItemKind.Folder:
                    moniker = KnownMonikers.FolderClosed;
                    break;
                case CompletionItemKind.EnumMember:
                    moniker = KnownMonikers.EnumerationItemPublic;
                    break;
                case CompletionItemKind.Constant:
                    moniker = KnownMonikers.ConstantPublic;
                    break;
                case CompletionItemKind.Struct:
                    moniker = KnownMonikers.StructurePublic;
                    break;
                case CompletionItemKind.Event:
                    moniker = KnownMonikers.EventPublic;
                    break;
                case CompletionItemKind.Operator:
                    moniker = KnownMonikers.Operator;
                    break;
                case CompletionItemKind.TypeParameter:
                    moniker = KnownMonikers.Type;
                    break;
                default:
                    break;
            }

            return new ImageElement(moniker.ToImageId());
        }
    }
}

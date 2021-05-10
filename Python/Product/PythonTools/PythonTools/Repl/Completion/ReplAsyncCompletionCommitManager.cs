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

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.PythonTools.LanguageServerClient;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Repl.Completion {
    internal class ReplAsyncCompletionCommitManager : IAsyncCompletionCommitManager {
        private readonly ReplRemoteCompletionBroker _broker;
        private readonly ITextView _textView;

        private readonly char[] typicalDimissChars = new[] { ';', ' ' };

        public ReplAsyncCompletionCommitManager(ReplRemoteCompletionBroker broker, ITextView textView) {
            _broker = broker;
            _textView = textView;

            var serverCommitTriggerCharacters = broker.GetCompletionCommitCharacters(textView.TextBuffer.ContentType)
                .Concat(broker.GetCompletionTriggerCharacters(textView.TextBuffer.ContentType));

            // completion characters returned from the language server are given as an array of strings. This converts the
            // string[] into a IEnumerable<char> by taking the first letter of each string and ommiting empty strings.
            PotentialCommitCharacters = serverCommitTriggerCharacters
                .Select(value => value.FirstOrDefault())
                .Where(c => c != default(char))
                .Concat(typicalDimissChars);
        }

        public IEnumerable<char> PotentialCommitCharacters { get; }

        public bool ShouldCommitCompletion(IAsyncCompletionSession session, SnapshotPoint location, char typedChar, CancellationToken token) {
            return true;
        }

        public CommitResult TryCommit(IAsyncCompletionSession session, ITextBuffer buffer, CompletionItem item, char typedChar, CancellationToken token) {
            if (item.Properties.TryGetProperty(ReplAsyncCompletionSource.ProtocolItemKey, out Microsoft.VisualStudio.LanguageServer.Protocol.CompletionItem completionItem) &&
                item.Properties.TryGetProperty(ReplAsyncCompletionSource.TriggerPointKey, out SnapshotPoint triggerLocation)) {
                // Tab and Enter characters should also commit.
                if (typedChar != '\t' && typedChar != '\n') {
                    bool isCommitCharacter = false;

                    if (completionItem.CommitCharacters != null && completionItem.CommitCharacters.Length != 0) {
                        // If there are commit characters set for the particular completion item, then it should take precedence.
                        foreach (var completionItemCommitCharacter in completionItem.CommitCharacters) {
                            if (completionItemCommitCharacter.Length > 0 && completionItemCommitCharacter[0] == typedChar) {
                                isCommitCharacter = true;
                                break;
                            }
                        }

                        if (!isCommitCharacter) {
                            return new CommitResult(isHandled: false, behavior: CommitBehavior.CancelCommit);
                        }
                    }

                    // TODO: Review if this should be removed if server passes in CommitTriggers for each CompletionItem.  This is in place to unblock VC headless VS demo for build 2019
                    if (!isCommitCharacter && this.typicalDimissChars.Contains(typedChar)) {
                        // If we got here it means the completion item has not specificed commit characters and we want to dismiss the commit and the intellisense session because it's a dismiss character.
                        return CommitResult.Handled;
                    }
                }

                if (completionItem.TextEdit != null || completionItem.AdditionalTextEdits != null) {
                    // Completion text edits are computed when the completion session is first triggered. The lines typed
                    // after the completion session was started need to be deleted to revert the document to its original state.
                    var caretPositionAtBuffer = session.TextView.GetCaretPointAtSubjectBuffer(buffer);
                    if (caretPositionAtBuffer.HasValue) {
                        var deleteTextLength = caretPositionAtBuffer.Value.Position - triggerLocation.Position;
                        if (deleteTextLength > 0) {
                            var deleteSpan = new Span(triggerLocation.Position, deleteTextLength);
                            buffer.Delete(deleteSpan);
                        }

                        if (completionItem.TextEdit != null) {
                            LspEditorUtilities.ApplyTextEdit(completionItem.TextEdit, triggerLocation.Snapshot, buffer);
                        } else if (completionItem.InsertText != null) {
                            buffer.Replace(session.ApplicableToSpan.GetSpan(buffer.CurrentSnapshot), completionItem.InsertText);
                        }

                        if (completionItem.AdditionalTextEdits != null) {
                            LspEditorUtilities.ApplyTextEdits(completionItem.AdditionalTextEdits, triggerLocation.Snapshot, buffer);
                        }

                        return CommitResult.Handled;
                    }
                }
            }

            return CommitResult.Unhandled;
        }
    }
}

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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Common.Core.Disposables;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.Text;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.PythonTools.LanguageServerClient {
    class ReplDocument : IDisposable {
        private readonly IServiceProvider _site;
        private readonly IInteractiveWindow _window;
        private readonly PythonLanguageClient _client;
        private readonly DisposableBag _disposableBag;
        private string _tempFilePath;
        private int _version;
        private int _previousCellsLineCount;
        private ITextBuffer2 _currentInputBuffer;

        public ReplDocument(IServiceProvider site, IInteractiveWindow window, PythonLanguageClient client) {
            _site = site ?? throw new ArgumentNullException(nameof(site));
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _disposableBag = new DisposableBag(nameof(ReplDocument));
        }

        public Uri DocumentUri { get; private set; }

        public async Task InitializeAsync() {
            _tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".py");
            File.WriteAllText(_tempFilePath, string.Empty);

            DocumentUri = new Uri(_tempFilePath);

            _window.SubmissionBufferAdded += OnSubmissionBufferAdded;
            _disposableBag.Add(() => {
                _window.SubmissionBufferAdded -= OnSubmissionBufferAdded;
            });

            _currentInputBuffer = null;
            _previousCellsLineCount = 0;

            var textDocItem = new LSP.TextDocumentItem {
                Uri = new Uri(_tempFilePath),
                Text = string.Empty,
                Version = _version,
            };

            await _client.InvokeTextDocumentDidOpenAsync(new LSP.DidOpenTextDocumentParams {
                TextDocument = textDocItem
            });

            if (_window.CurrentLanguageBuffer is ITextBuffer2 buffer) {
                SetCurrentBuffer(buffer);
            }
        }

        public async Task<object> GetCompletions(LSP.Position position, LSP.CompletionContext context, CancellationToken token) {
            var completionParams = new LSP.CompletionParams() {
                TextDocument = new LSP.TextDocumentIdentifier() {
                    Uri = DocumentUri,
                },
                Position = GetDocumentPosition(position),
                Context = context,
            };

            var res = await _client.InvokeTextDocumentCompletionAsync(completionParams, token);
            return res;
        }

        private void OnSubmissionBufferAdded(object sender, SubmissionBufferAddedEventArgs e) {
            if (e.NewBuffer is ITextBuffer2 buffer) {
                SetCurrentBuffer(buffer);
            }
        }

        private void SetCurrentBuffer(ITextBuffer2 buffer) {
            if (_currentInputBuffer != null) {
                _currentInputBuffer.ChangedHighPriority -= OnReplInputBufferBackgroundChange;
                _previousCellsLineCount += _currentInputBuffer.CurrentSnapshot.LineCount - 1;
            }

            _currentInputBuffer = buffer;

            buffer.ChangedHighPriority += OnReplInputBufferBackgroundChange;
        }

        private void OnReplInputBufferBackgroundChange(object sender, TextContentChangedEventArgs e) {
            OnReplDidChangeTextDocumentAsync(e.Before, e.After, e.Changes)
                .HandleAllExceptions(_site, GetType())
                .DoNotWait();
        }

        private async Task OnReplDidChangeTextDocumentAsync(ITextSnapshot before, ITextSnapshot after, IEnumerable<ITextChange> textChanges) {
            var contentChanges = new List<LSP.TextDocumentContentChangeEvent>();

            // The changes in textChanges all apply to the same original document state. The changes sent to the
            // server are expected to apply to the state as of the previous change in the list. To prevent the
            // changes from affecting one another we reverse the list.
            foreach (var textChange in textChanges.Reverse()) {
                var changeEvent = new LSP.TextDocumentContentChangeEvent {
                    Text = textChange.NewText,
                    Range = new LSP.Range {
                        Start = new LSP.Position {
                            Line = before.GetLineNumberFromPosition(textChange.OldSpan.Start) + _previousCellsLineCount,
                            Character = textChange.OldSpan.Start - before.GetLineFromPosition(textChange.OldSpan.Start).Start.Position
                        },
                        End = new LSP.Position {
                            Line = before.GetLineNumberFromPosition(textChange.OldSpan.End) + _previousCellsLineCount,
                            Character = textChange.OldSpan.End - before.GetLineFromPosition(textChange.OldSpan.End).Start.Position
                        },
                    },
                    RangeLength = textChange.OldSpan.Length
                };

                contentChanges.Add(changeEvent);
            }

            _version++;

            var changesOnlyParam = new LSP.DidChangeTextDocumentParams {
                ContentChanges = contentChanges.ToArray(),
                TextDocument = new LSP.VersionedTextDocumentIdentifier {
                    Version = _version,
                    Uri = new Uri(Path.GetFullPath(_tempFilePath)),
                }
            };

            await _client.InvokeTextDocumentDidChangeAsync(changesOnlyParam);
        }

        public LSP.Position GetDocumentPosition(LSP.Position currentCellPos) {
            return new LSP.Position {
                Line = currentCellPos.Line + _previousCellsLineCount,
                Character = currentCellPos.Character,
            };
        }

        public void Dispose() {
            if (_currentInputBuffer != null) {
                _currentInputBuffer.ChangedHighPriority -= OnReplInputBufferBackgroundChange;
            }

            _disposableBag.TryDispose();
        }
    }
}

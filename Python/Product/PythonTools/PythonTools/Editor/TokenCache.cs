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
using System.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace Microsoft.PythonTools.Editor {
    [DebuggerDisplay("{GetDebugView(),nq}")]
    internal struct LineTokenization : ITag {
        public readonly LineToken[] Tokens;
        public readonly object State;
        public readonly ITrackingSpan Line;

        public LineTokenization(IEnumerable<TokenInfo> tokens, object state, ITextSnapshotLine line) {
            Tokens = tokens.Select(t => new LineToken(t, line.EndIncludingLineBreak)).ToArray();
            State = state;
            Line = line.Snapshot.CreateTrackingSpan(line.ExtentIncludingLineBreak, SpanTrackingMode.EdgeNegative);
        }

        internal string GetDebugView() {
            var sb = new StringBuilder();
            if (State != null) {
                sb.Append(State != null ? "S " : "  ");
            }
            if (Tokens != null) {
                for (var i = 0; i < Tokens.Length; i++) {
                    sb.Append('[');
                    sb.Append(Tokens[i].Category);
                    sb.Append(']');
                }
            }
            return sb.ToString();
        }
    }

    internal struct LineToken {
        public LineToken(TokenInfo token, int lineLength) {
            Category = token.Category;
            Trigger = token.Trigger;
            Column = token.SourceSpan.Start.Column - 1;
            if (token.SourceSpan.Start.Line == token.SourceSpan.End.Line) {
                // Token on the same line is easy
                Length = token.SourceSpan.End.Column - token.SourceSpan.Start.Column;
            } else if (token.SourceSpan.End.Line == token.SourceSpan.Start.Line + 1 && token.SourceSpan.End.Column == 1) {
                // Token ending at the start of the next line is a known special case
                Length = lineLength - Column;
            } else {
                // Tokens spanning lines should not be added to a LineTokenization
                throw new ArgumentException("Cannot cache multiline token");
            }
        }

        public TokenCategory Category;
        public TokenTriggers Trigger;
        /// <summary>
        /// 0-based index where the token starts on the line.
        /// </summary>
        public int Column;
        /// <summary>
        /// Number of characters included in the token.
        /// </summary>
        public int Length;
    }

    struct TrackingTokenInfo {
        internal TrackingTokenInfo(LineToken token, int lineNumber, ITrackingSpan lineSpan) {
            if (lineNumber < 0) {
                throw new ArgumentOutOfRangeException(nameof(lineNumber));
            }
            LineToken = token;
            LineNumber = lineNumber;
            LineSpan = lineSpan;
        }

        private readonly LineToken LineToken;
        public readonly int LineNumber;
        private readonly ITrackingSpan LineSpan;

        public TokenCategory Category => LineToken.Category;
        public TokenTriggers Trigger => LineToken.Trigger;

        /// <summary>
        /// Returns true if the location is on the same line and between either end. If
        /// <see cref="IsAdjacent(SourceLocation)"/> is true, this will also be true.
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        public bool Contains(SourceLocation location) {
            var col = location.Column - 1;
            return location.Line - 1 == LineNumber &&
                (col >= LineToken.Column && col <= LineToken.Column + LineToken.Length);
        }

        /// <summary>
        /// Returns true if the location is on the same line and at either end of this token.
        /// </summary>
        public bool IsAdjacent(SourceLocation location) {
            var col = location.Column - 1;
            return location.Line - 1 == LineNumber &&
                (col == LineToken.Column || col == LineToken.Column + LineToken.Length);
        }

        public bool IsAtStart(SourceLocation location) {
            return location.Line - 1 == LineNumber && location.Column - 1 == LineToken.Column;
        }

        public bool IsAtEnd(SourceLocation location) {
            return location.Line - 1 == LineNumber && location.Column - 1 == LineToken.Column + LineToken.Length;
        }

        public TokenInfo ToTokenInfo() {
            return new TokenInfo {
                Category = Category,
                Trigger = Trigger,
                SourceSpan = ToSourceSpan()
            };
        }

        public SourceSpan ToSourceSpan() {
            return new SourceSpan(
                new SourceLocation(LineNumber + 1, LineToken.Column + 1),
                new SourceLocation(LineNumber + 1, LineToken.Column + LineToken.Length + 1)
            );
        }

        public SnapshotSpan ToSnapshotSpan(ITextSnapshot snapshot) {
            // Note that this assumes the content of the line has not changed
            // since the tokenization was created. Lines can move up and down
            // within the file, and this will handle it correctly, but when a
            // line is edited the span returned here may not be valid.
            var line = LineSpan.GetSpan(snapshot);

            var startCol = Math.Min(LineToken.Column, line.Length);
            var endCol = Math.Min(LineToken.Column + LineToken.Length, line.Length);

            return new SnapshotSpan(line.Start + startCol, line.Start + endCol);
        }

        public string GetText(ITextSnapshot snapshot) => ToSnapshotSpan(snapshot).GetText();
    }

    /// <summary>
    /// Represents cached information on line tokens from a given snapshot.
    /// </summary>
    /// <remarks>The tokenization snapshot is immutable in relation 
    /// to the text buffer snapshot. If text buffer is updated, 
    /// tokenization snapshot continues using buffer snapshot it was
    /// created on.</remarks>
    internal interface ILineTokenizationSnapshot : IDisposable {
        /// <summary>
        /// Gets the tokenization for the specified line.
        /// </summary>
        /// <param name="line">The line to get tokenization for.</param>
        /// <param name="lazyTokenizer">Tokenizer factory</param>
        LineTokenization GetLineTokenization(ITextSnapshotLine line, Lazy<Tokenizer> lazyTokenizer);
    }

    internal class TokenCache {
        private readonly object _lock = new object();
        private LineTokenizationMap _map = new LineTokenizationMap();

        // Controls 'copy on write' when buffer changes from a background thread
        private int _useCount;

        /// <summary>
        /// Obtains tokenization snapshot that is immutable 
        /// in relation to the text buffer snapshot.
        /// </summary>
        /// <returns></returns>
        internal ILineTokenizationSnapshot GetSnapshot() {
            lock (_lock) {
                _useCount++;
                return new LineTokenizationSnapshot(this, _map);
            }
        }

        /// <summary>
        /// Releases tokenization snapshot
        /// </summary>
        internal void Release(LineTokenizationMap map) {
            lock (_lock) {
                if (_map == map) {
                    if (_useCount == 0) {
                        throw new InvalidOperationException("Line tokenization map is not in use");
                    }
                    _useCount--;
                }
            }
        }

        internal void Clear() {
            lock (_lock) {
                _map = new LineTokenizationMap();
            }
        }

        internal void Update(TextContentChangedEventArgs e, Lazy<Tokenizer> lazyTokenizer) {
            lock (_lock) {
                // Copy on write. No need to copy if no one is using the map.
                if (_useCount > 0) {
                    _map = _map.Clone();
                    _useCount = 0;
                }
                _map.Update(e, lazyTokenizer);
            }
        }

        internal class LineTokenizationSnapshot : ILineTokenizationSnapshot {
            private readonly TokenCache _cache;
            private readonly LineTokenizationMap _map;

            internal LineTokenizationSnapshot(TokenCache cache, LineTokenizationMap map) {
                _cache = cache;
                _map = map;
            }
            void IDisposable.Dispose() => _cache.Release(_map);

            LineTokenization ILineTokenizationSnapshot.GetLineTokenization(ITextSnapshotLine line, Lazy<Tokenizer> lazyTokenizer)
                => _map.GetLineTokenization(line, lazyTokenizer);
        }

        internal class LineTokenizationMap {
            private readonly object _lock = new object();
            private LineTokenization[] _map;

            internal LineTokenizationMap() { }

            private LineTokenizationMap(LineTokenization[] map) {
                _map = map;
            }

            internal LineTokenizationMap Clone() {
                lock (_lock) {
                    LineTokenization[] map = null;
                    if (_map != null) {
                        map = new LineTokenization[_map.Length];
                        Array.Copy(map, _map, map.Length);
                    }
                    return new LineTokenizationMap(map);
                }
            }

            internal LineTokenization GetLineTokenization(ITextSnapshotLine line, Lazy<Tokenizer> lazyTokenizer) {
                var lineNumber = line.LineNumber;

                lock (_lock) {
                    EnsureCapacity(line.Snapshot.LineCount);
                    var start = IndexOfPreviousTokenization(lineNumber + 1, 0, out var lineTok);

                    while (++start <= lineNumber) {
                        var state = lineTok.State;

                        if (!TryGetTokenization(start, out lineTok)) {
                            _map[start] = lineTok = lazyTokenizer.Value.TokenizeLine(line.Snapshot.GetLineFromLineNumber(start), state);
                        }
                    }
                    return lineTok;
                }
            }

            internal void Update(TextContentChangedEventArgs e, Lazy<Tokenizer> lazyTokenizer) {
                var snapshot = e.After;
                lock (_lock) {
                    EnsureCapacity(snapshot.LineCount);

                    foreach (var change in e.Changes) {
                        var line = snapshot.GetLineNumberFromPosition(change.NewPosition) + 1;
                        if (change.LineCountDelta > 0) {
                            InsertLines(line, change.LineCountDelta);
                        } else if (change.LineCountDelta < 0) {
                            DeleteLines(line, Math.Min(-change.LineCountDelta, _map.Length - line));
                        }

                        ApplyChanges(new SnapshotSpan(snapshot, change.NewSpan), lazyTokenizer);
                    }
                }
            }

            private void ApplyChanges(SnapshotSpan span, Lazy<Tokenizer> lazyTokenizer) {
                var firstLine = span.Start.GetContainingLine().LineNumber;
                var lastLine = span.End.GetContainingLine().LineNumber;

                AssertCapacity(firstLine);

                // find the closest line preceding firstLine for which we know tokenizer state
                firstLine = IndexOfPreviousTokenization(firstLine, 0, out var lineTokenization) + 1;

                for (var lineNo = firstLine; lineNo < span.Snapshot.LineCount; ++lineNo) {
                    var line = span.Snapshot.GetLineFromLineNumber(lineNo);

                    var beforeState = lineTokenization.State;
                    _map[lineNo] = lineTokenization = lazyTokenizer.Value.TokenizeLine(line, beforeState);
                    var afterState = lineTokenization.State;

                    // stop if we visited all affected lines and the current line has no tokenization state
                    // or its previous state is the same as the new state.
                    if (lineNo > lastLine && (beforeState == null || beforeState.Equals(afterState))) {
                        break;
                    }
                }
            }

            /// <summary>
            /// Looks for the first cached tokenization preceding the given line.
            /// Returns the line we have a tokenization for or minLine - 1 if there is none.
            /// </summary>
            private int IndexOfPreviousTokenization(int line, int minLine, out LineTokenization tokenization) {
                line--;
                while (line >= minLine) {
                    if (_map[line].Tokens != null) {
                        tokenization = _map[line];
                        return line;
                    }
                    line--;
                }
                tokenization = default(LineTokenization);
                return minLine - 1;
            }

            private bool TryGetTokenization(int line, out LineTokenization tokenization) {
                tokenization = _map[line];
                if (tokenization.Tokens != null) {
                    return true;
                }
                tokenization = default(LineTokenization);
                return false;
            }

            private void EnsureCapacity(int capacity) {
                if (_map == null) {
                    _map = new LineTokenization[capacity];
                    return;
                }

                if (capacity > _map.Length) {
                    Array.Resize(ref _map, Math.Max(capacity, (_map.Length + 1) * 2));
                }
            }

            [Conditional("DEBUG")]
            private void AssertCapacity(int capacity) {
                Debug.Assert(_map != null);
                Debug.Assert(_map.Length > capacity);
            }

            private void DeleteLines(int index, int count) {
                if (index > _map.Length - count) {
                    throw new ArgumentOutOfRangeException(nameof(index), "Must be 'count' less than the size of the cache");
                }

                Array.Copy(_map, index + count, _map, index, _map.Length - index - count);
                for (var i = 0; i < count; i++) {
                    _map[_map.Length - i - 1] = default(LineTokenization);
                }
            }

            private void InsertLines(int index, int count) {
                Array.Copy(_map, index, _map, index + count, _map.Length - index - count);
                for (var i = 0; i < count; i++) {
                    _map[index + i] = default(LineTokenization);
                }
            }
        }
    }
}

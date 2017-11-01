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
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools {
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
            StringBuilder sb = new StringBuilder();
            if (State != null) {
                sb.Append(State != null ? "S " : "  ");
            }
            if (Tokens != null) {
                for (int i = 0; i < Tokens.Length; i++) {
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
            int col = location.Column - 1;
            return location.Line - 1 == LineNumber &&
                (col >= LineToken.Column && col <= LineToken.Column + LineToken.Length);
        }

        /// <summary>
        /// Returns true if the location is on the same line and at either end of this token.
        /// </summary>
        public bool IsAdjacent(SourceLocation location) {
            int col = location.Column - 1;
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

            int startCol = Math.Min(LineToken.Column, line.Length);
            int endCol = Math.Min(LineToken.Column + LineToken.Length, line.Length);

            return new SnapshotSpan(line.Start + startCol, line.Start + endCol);
        }

        public string GetText(ITextSnapshot snapshot) => ToSnapshotSpan(snapshot).GetText();
    }

    internal class TokenCache {
        private LineTokenization[] _map;

        // This lock is only used for reading/writing _map, and not
        // the contents of the array. Lock the array itself when
        // using it.
        private readonly object _mapLock = new object();

        internal TokenCache() {
            _map = null;
        }

        private LineTokenization[] Map {
            get {
                LineTokenization[] map;
                lock (_mapLock) {
                    map = _map;
                }
                if (map == null) {
                    throw new InvalidOperationException("uninitialized token cache");
                }
                return map;
            }
        }

        /// <summary>
        /// Looks for the first cached tokenization preceding the given line.
        /// Returns the line we have a tokenization for or minLine - 1 if there is none.
        /// </summary>
        internal int IndexOfPreviousTokenization(int line, int minLine, out LineTokenization tokenization) {
            if (line < 0) {
                throw new ArgumentOutOfRangeException("line", "Must be 0 or greater");
            }

            var map = Map;

            lock (map) {
                line--;
                while (line >= minLine) {
                    if (map[line].Tokens != null) {
                        tokenization = map[line];
                        return line;
                    }
                    line--;
                }
            }
            tokenization = default(LineTokenization);
            return minLine - 1;
        }

        internal bool TryGetTokenization(int line, out LineTokenization tokenization) {
            if (line < 0) {
                throw new ArgumentOutOfRangeException("line", "Must be 0 or greater");
            }

            tokenization = this[line];
            if (tokenization.Tokens != null) {
                return true;
            } else {
                tokenization = default(LineTokenization);
                return false;
            }
        }

        internal LineTokenization this[int line] {
            get {
                var map = Map;
                lock (map) {
                    return map[line];
                }
            }
            set {
                var map = Map;
                lock (map) {
                    map[line] = value;
                }
            }
        }

        internal void Clear() {
            _map = null;
        }

        internal void EnsureCapacity(int capacity) {
            lock (_mapLock) {
                if (_map == null) {
                    _map = new LineTokenization[capacity];
                    return;
                }

                if (capacity > _map.Length) {
                    Array.Resize(ref _map, Math.Max(capacity, (_map.Length + 1) * 2));
                }
            }
        }

        internal void DeleteLines(int index, int count) {
            var map = Map;
            if (index > map.Length - count) {
                throw new ArgumentOutOfRangeException("line", "Must be 'count' less than the size of the cache");
            }

            lock (map) {
                Array.Copy(map, index + count, map, index, map.Length - index - count);
                for (int i = 0; i < count; i++) {
                    map[map.Length - i - 1] = default(LineTokenization);
                }
            }
        }

        internal void InsertLines(int index, int count) {
            var map = Map;

            lock (map) {
                Array.Copy(map, index, map, index + count, map.Length - index - count);
                for (int i = 0; i < count; i++) {
                    map[index + i] = default(LineTokenization);
                }
            }
        }
    }
}

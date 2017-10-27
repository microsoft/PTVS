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
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools {
    [DebuggerDisplay("{GetDebugView(),nq}")]
    internal struct LineTokenization : ITag {
        public readonly LineToken[] Tokens;
        public readonly object State;

        public LineTokenization(IEnumerable<TokenInfo> tokens, object state, int fullLineLength) {
            Tokens = tokens.Select(t => new LineToken(t, fullLineLength)).ToArray();
            State = state;
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

    internal class TokenCache {
        private LineTokenization[] _map;

        internal TokenCache() {
            _map = null;
        }

        /// <summary>
        /// Looks for the first cached tokenization preceding the given line.
        /// Returns the line we have a tokenization for or minLine - 1 if there is none.
        /// </summary>
        internal int IndexOfPreviousTokenization(int line, int minLine, out LineTokenization tokenization) {
            if (line < 0) {
                throw new ArgumentOutOfRangeException("line", "Must be 0 or greater");
            }
            Utilities.CheckNotNull(_map);

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

        internal bool TryGetTokenization(int line, out LineTokenization tokenization) {
            if (line < 0) {
                throw new ArgumentOutOfRangeException("line", "Must be 0 or greater");
            }
            Utilities.CheckNotNull(_map);

            if (_map[line].Tokens != null) {
                tokenization = _map[line];
                return true;
            } else {
                tokenization = default(LineTokenization);
                return false;
            }
        }

        internal LineTokenization this[int line] {
            get {
                return _map[line];
            }
            set {
                _map[line] = value;
            }
        }

        internal void Clear() {
            _map = null;
        }

        internal void EnsureCapacity(int capacity) {
            if (_map == null) {
                _map = new LineTokenization[capacity];
            } else if (_map.Length < capacity) {
                Array.Resize(ref _map, Math.Max(capacity, (_map.Length + 1) * 2));
            }
        }

        internal void DeleteLines(int index, int count) {
            Utilities.CheckNotNull(_map);
            if (index > _map.Length - count) {
                throw new ArgumentOutOfRangeException("line", "Must be 'count' less than the size of the cache");
            }
            
            Array.Copy(_map, index + count, _map, index, _map.Length - index - count);
            for (int i = 0; i < count; i++) {
                _map[_map.Length - i - 1] = default(LineTokenization);
            }
        }

        internal void InsertLines(int index, int count) {
            Utilities.CheckNotNull(_map);

            Array.Copy(_map, index, _map, index + count, _map.Length - index - count);
            for (int i = 0; i < count; i++) {
                _map[index + i] = default(LineTokenization);
            }
        }
    }
}

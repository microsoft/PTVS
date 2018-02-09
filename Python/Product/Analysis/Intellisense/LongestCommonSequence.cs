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
using System.Linq;

namespace Microsoft.PythonTools.Intellisense {
    internal class LongestCommonSequence<T> {
        private readonly Func<T, T, bool> _equalityComparer;
        private readonly T[] _column;
        private readonly T[] _row;
        private readonly bool _isFlipped;

        public static IReadOnlyList<LcsDiff> Find(T[] oldItems, T[] newItems, Func<T, T, bool> equalityComparer)
            => new LongestCommonSequence<T>(oldItems, newItems, equalityComparer).Find();

        private LongestCommonSequence(T[] oldItems, T[] newItems, Func<T, T, bool> equalityComparer) {
            _equalityComparer = equalityComparer;
            if (newItems.Length < oldItems.Length) {
                _column = newItems;
                _row = oldItems;
                _isFlipped = true;
            } else {
                _column = oldItems;
                _row = newItems;
                _isFlipped = false;
            }
        }
            
        public IReadOnlyList<LcsDiff> Find() {
            var start = 0;
            var columnEnd = _column.Length - 1;
            var rowEnd = _row.Length - 1;

            while (start < _column.Length && AreEqualAt(start, start)) {
                start++;
            }

            while (start <= columnEnd && AreEqualAt(columnEnd, rowEnd)) {
                columnEnd--;
                rowEnd--;
            }

            // Smaller sequence consists of beginning and end of a larger one 
            if (start == columnEnd + 1) {
                return new List<LcsDiff> { CreateDiff(start, start, columnEnd, rowEnd, _isFlipped) };
            }

            // At this point, we know that AreEqualAt(start, start) == false; AreEqualAt(columnEnd, rowEnd) == false
            var path = FindPath(start, columnEnd, rowEnd);
            return ConvertPathToDiffs(path, start, columnEnd, rowEnd).ToList();
        }

        // Finds path returns array where array index represents column and value represents row
        // path[index] == -1 means that no matching row exist for 'index' column
        private int[] FindPath(int start, int columnEnd, int rowEnd) {
            var ranges = new Queue<Range>();
            ranges.Enqueue(new Range(start, columnEnd, start, rowEnd));

            var path = new int[columnEnd + 1 - start];
            for (var i = 0; i < path.Length; i++) {
                path[i] = -1;
            }

            // Share the same array for all score calculations
            var topScores = new int[path.Length + 1];
            var bottomScores = new int[path.Length + 1];
            var tempScores = new int[path.Length + 1];

            while (ranges.Count > 0) {
                var range = ranges.Dequeue();
                if (range.ColumnStart == range.ColumnEnd) {
                    TryFindRow(path, start, range);
                    continue;
                }

                if (range.RowStart == range.RowEnd) {
                    TryFindColumn(path, start, range);
                    continue;
                }

                var midRow = (range.RowStart + range.RowEnd) / 2;
                var topHasScores = FindScores(range.RowStart, midRow, range.ColumnStart, range.ColumnEnd, false, ref topScores, ref tempScores);
                var bottomHasScores = FindScores(midRow + 1, range.RowEnd, range.ColumnStart, range.ColumnEnd, true, ref bottomScores, ref tempScores);
                var scoresCount = range.ColumnEnd - range.ColumnStart + 2;
                var maxScoreIndex = FindMaxScoreIndex(topScores, bottomScores, scoresCount);
                var midColumn = range.ColumnStart + maxScoreIndex - 1;

                // No scores means that no matching elements were found in the half, no reason to scan it again
                // If top half is empty, maxScoreIndex == 0, if bottom half is empty, maxScoreIndex == scoresCount - 1
                if (topHasScores && maxScoreIndex > 0) {
                    ranges.Enqueue(new Range(range.ColumnStart, midColumn, range.RowStart, midRow));
                } 

                if (bottomHasScores && maxScoreIndex < scoresCount - 1) {
                    ranges.Enqueue(new Range(midColumn + 1, range.ColumnEnd, midRow + 1, range.RowEnd));
                }
            }

            return path;
        }

        private bool FindScores(int rowStart, int rowEnd, int columnStart, int columnEnd, bool isReversed, ref int[] scores, ref int[] previousScores) {
            var rowCount = rowEnd - rowStart + 1;
            var columnCount = columnEnd - columnStart + 1;

            previousScores[isReversed ? columnCount : 0] = 0;
            for (var i = 0; i < columnCount + 1; i++) {
                scores[i] = 0;
            }

            for (var i = 0; i < rowCount; i++) {
                // Swap columns instead of copying values
                // Do it at the beginning of the iteration so that 'scores' contains scores for 'rowEnd'
                var temp = previousScores;
                previousScores = scores;
                scores = temp;

                for (var j = 0; j < columnCount; j++) {
                    if (isReversed) {
                        SetScore(columnCount - j, columnCount - j - 1, columnEnd - j, rowEnd - i, scores, previousScores);
                    } else {
                        SetScore(j, j + 1, columnStart + j, rowStart + i, scores, previousScores);
                    }
                }
            }

            return scores[isReversed ? 0 : columnCount] > 0;
        }

        private void SetScore(int previousIndex, int index, int column, int row, int[] scores, int[] previousScores) 
            => scores[index] = AreEqualAt(column, row)
                ? previousScores[previousIndex] + 1
                : Math.Max(previousScores[index], scores[previousIndex]);

        private void TryFindRow(int[] path, int start, Range range) {
            for (var row = range.RowStart; row <= range.RowEnd; row++) {
                if (AreEqualAt(range.ColumnStart, row)) {
                    path[range.ColumnStart - start] = row;
                    return;
                }
            }
        }

        private void TryFindColumn(int[] path, int start, Range range) {
            for (var column = range.ColumnStart; column <= range.ColumnEnd; column++) {
                if (AreEqualAt(column, range.RowStart)) {
                    path[column - start] = range.RowStart;
                    return;
                }
            }
        }

        private IEnumerable<LcsDiff> ConvertPathToDiffs(int[] path, int start, int columnEnd, int rowEnd) {
            var columnStart = start;
            var rowStart = start;
            for (var i = 0; i < path.Length; i++) {
                if (path[i] == -1) {
                    continue;
                }

                var column = i + start;
                var row = path[i];

                // previousColumn = i - 1 + start
                // previousRow = path[i - 1]
                // row - previousRow == column - previousColumn <=> path[i] - path[i - 1] == 1
                if (i == 0 || path[i] - path[i - 1] != 1) {
                    yield return CreateDiff(columnStart, rowStart, column - 1, row - 1, _isFlipped);
                }

                columnStart = column + 1;
                rowStart = row + 1;
            }

            yield return CreateDiff(columnStart, rowStart, columnEnd, rowEnd, _isFlipped);
        }

        private bool AreEqualAt(int columnIndex, int rowIndex) 
            => _equalityComparer(_column[columnIndex], _row[rowIndex]);

        private static LcsDiff CreateDiff(int columnStart, int rowStart, int columnEnd, int rowEnd, bool isFlipped) 
            => isFlipped ? new LcsDiff(rowStart, rowEnd, columnStart, columnEnd) : new LcsDiff(columnStart, columnEnd, rowStart, rowEnd);

        private static int FindMaxScoreIndex(int[] topScores, int[] bottomScore, int scoresCount) {
            var sum = -1;
            var index = -1;
            for (var i = 0; i < scoresCount; i++) {
                if (topScores[i] + bottomScore[i] > sum) {
                    sum = topScores[i] + bottomScore[i];
                    index = i;
                }
            }

            return index;
        }

        private struct Range {
            public int ColumnStart { get; }
            public int ColumnEnd { get; }
            public int RowStart { get; }
            public int RowEnd { get; }

            public Range(int columnStart, int columnEnd, int rowStart, int rowEnd) {
                ColumnStart = columnStart;
                ColumnEnd = columnEnd;
                RowStart = rowStart;
                RowEnd = rowEnd;
            }
        }
    }

    public struct LcsDiff : IEquatable<LcsDiff> {
        public int OldStart { get; }
        public int NewStart { get; }
        public int OldEnd { get; }
        public int NewEnd { get; }
        public int OldLength => OldEnd - OldStart + 1;
        public int NewLength => NewEnd - NewStart + 1;

        public LcsDiff(int oldStart, int oldEnd, int newStart, int newEnd) {
            OldStart = oldStart;
            NewStart = newStart;
            OldEnd = oldEnd;
            NewEnd = newEnd;
        }

        public override bool Equals(object obj) 
            => obj is LcsDiff diff ? Equals(diff) : base.Equals(obj);

        public bool Equals(LcsDiff other) 
            => OldStart == other.OldStart && NewStart == other.NewStart && OldEnd == other.OldEnd && NewEnd == other.NewEnd;

        public static bool operator ==(LcsDiff x, LcsDiff y) => x.Equals(y);
        public static bool operator !=(LcsDiff x, LcsDiff y) => !x.Equals(y);

        public override int GetHashCode() {
            unchecked {
                var hashCode = OldStart;
                hashCode = (hashCode * 397) ^ NewStart;
                hashCode = (hashCode * 397) ^ OldEnd;
                hashCode = (hashCode * 397) ^ NewEnd;
                return hashCode;
            }
        }
    }
}
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
using System.Diagnostics;
using System.Globalization;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools {
    /// <summary>
    /// Represents a location in source code.
    /// </summary>
    [Serializable]
    [DebuggerDisplay("({_line}, {_column})")]
    public struct SourceLocation : IComparable<SourceLocation>, IEquatable<SourceLocation> {
        private readonly int _index;

        private readonly int _line;
        private readonly int _column;

        /// <summary>
        /// Creates a new source location.
        /// </summary>
        /// <param name="index">The index in the source stream the location represents (0-based).</param>
        /// <param name="line">The line in the source stream the location represents (1-based).</param>
        /// <param name="column">The column in the source stream the location represents (1-based).</param>
        public SourceLocation(int index, int line, int column) {
            ValidateLocation(index, line, column);

            _index = index;
            _line = line;
            _column = column;
        }

        /// <summary>
        /// Creates a new source location.
        /// </summary>
        /// <param name="line">The line in the source stream the location represents (1-based).</param>
        /// <param name="column">The column in the source stream the location represents (1-based).</param>
        public SourceLocation(int line, int column) {
            ValidateLocation(0, line, column);

            _index = -1;
            _line = line;
            _column = column;
        }

        private static void ValidateLocation(int index, int line, int column) {
            if (index < 0) {
                throw ErrorOutOfRange("index", 0);
            }
            if (line < 1) {
                throw ErrorOutOfRange("line", 1);
            }
            if (column < 1) {
                throw ErrorOutOfRange("column", 1);
            }
        }

        private static Exception ErrorOutOfRange(object p0, object p1) {
            return new ArgumentOutOfRangeException("{0} must be greater than or equal to {1}".FormatInvariant(p0, p1));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters")]
        private SourceLocation(int index, int line, int column, bool noChecks) {
            _index = index;
            _line = line;
            _column = column;
        }

        /// <summary>
        /// The index in the source stream the location represents (0-based).
        /// </summary>
        public int Index {
            get {
                if (_index < 0) {
                    throw new InvalidOperationException("Index is not valid");
                }
                return _index;
            }
        }

        /// <summary>
        /// The line in the source stream the location represents (1-based).
        /// </summary>
        public int Line {
            get { return _line; }
        }

        /// <summary>
        /// The column in the source stream the location represents (1-based).
        /// </summary>
        public int Column {
            get { return _column; }
        }

        /// <summary>
        /// Compares two specified location values to see if they are equal.
        /// </summary>
        /// <param name="left">One location to compare.</param>
        /// <param name="right">The other location to compare.</param>
        /// <returns>True if the locations are the same, False otherwise.</returns>
        public static bool operator ==(SourceLocation left, SourceLocation right) {
            return left._line == right._line && left._column == right._column;
        }

        /// <summary>
        /// Compares two specified location values to see if they are not equal.
        /// </summary>
        /// <param name="left">One location to compare.</param>
        /// <param name="right">The other location to compare.</param>
        /// <returns>True if the locations are not the same, False otherwise.</returns>
        public static bool operator !=(SourceLocation left, SourceLocation right) {
            return left._line != right._line || left._column != right._column;
        }

        /// <summary>
        /// Compares two specified location values to see if one is before the other.
        /// </summary>
        /// <param name="left">One location to compare.</param>
        /// <param name="right">The other location to compare.</param>
        /// <returns>True if the first location is before the other location, False otherwise.</returns>
        public static bool operator <(SourceLocation left, SourceLocation right) {
            return left._line < right._line || (left._line == right._line && left._column < right._column);
        }

        /// <summary>
        /// Compares two specified location values to see if one is after the other.
        /// </summary>
        /// <param name="left">One location to compare.</param>
        /// <param name="right">The other location to compare.</param>
        /// <returns>True if the first location is after the other location, False otherwise.</returns>
        public static bool operator >(SourceLocation left, SourceLocation right) {
            return left._line > right._line || (left._line == right._line && left._column > right._column);
        }

        /// <summary>
        /// Compares two specified location values to see if one is before or the same as the other.
        /// </summary>
        /// <param name="left">One location to compare.</param>
        /// <param name="right">The other location to compare.</param>
        /// <returns>True if the first location is before or the same as the other location, False otherwise.</returns>
        public static bool operator <=(SourceLocation left, SourceLocation right) {
            return left._line < right._line || (left._line == right._line && left._column <= right._column);
        }

        /// <summary>
        /// Compares two specified location values to see if one is after or the same as the other.
        /// </summary>
        /// <param name="left">One location to compare.</param>
        /// <param name="right">The other location to compare.</param>
        /// <returns>True if the first location is after or the same as the other location, False otherwise.</returns>
        public static bool operator >=(SourceLocation left, SourceLocation right) {
            return left._line > right._line || (left._line == right._line && left._column >= right._column);
        }

        /// <summary>
        /// Compares two specified location values.
        /// </summary>
        /// <param name="left">One location to compare.</param>
        /// <param name="right">The other location to compare.</param>
        /// <returns>0 if the locations are equal, -1 if the left one is less than the right one, 1 otherwise.</returns>
        public static int Compare(SourceLocation left, SourceLocation right) {
            if (left < right) return -1;
            if (left > right) return 1;

            return 0;
        }

        /// <summary>
        /// A location that is valid but represents no location at all.
        /// </summary>
        public static readonly SourceLocation None = new SourceLocation(0, 0xfeefee, 0, true);

        /// <summary>
        /// An invalid location.
        /// </summary>
        public static readonly SourceLocation Invalid = new SourceLocation(0, 0, 0, true);

        /// <summary>
        /// A minimal valid location.
        /// </summary>
        public static readonly SourceLocation MinValue = new SourceLocation(0, 1, 1);

        /// <summary>
        /// Whether the location is a valid location.
        /// </summary>
        /// <returns>True if the location is valid, False otherwise.</returns>
        public bool IsValid {
            get {
                return this._line > 0 && this._column > 0;
            }
        }

        /// <summary>
        /// Returns a new SourceLocation with modified column. This will never
        /// result in a column less than 1 (unless the original location is invalid),
        /// and will not modify the line number.
        /// </summary>
        public SourceLocation AddColumns(int columns) {
            if (!IsValid) {
                return Invalid;
            }

            int newIndex = this._index, newCol = this._column;

            // These comparisons have been arranged to allow columns to
            // be int.MaxValue without the arithmetic overflowing.
            // The naive version is shown as a comment.

            // if (this._column + columns > int.MaxValue)
            if (columns > int.MaxValue - this._column) {
                newCol = int.MaxValue;
                if (newIndex >= 0) {
                    newIndex = int.MaxValue;
                }
            // if (this._column + columns <= 0)
            } else if (columns == int.MinValue || (columns < 0 && this._column <= -columns)) {
                newCol = 1;
                if (newIndex >= 0) {
                    newIndex += 1 - this._column;
                }
            } else {
                newCol += columns;
                if (newIndex >= 0) {
                    newIndex += columns;
                }
            }
            return newIndex >= 0 ? new SourceLocation(newIndex, this._line, newCol) : new SourceLocation(this._line, newCol);
        }

        public override bool Equals(object obj) {
            if (!(obj is SourceLocation)) return false;

            SourceLocation other = (SourceLocation)obj;
            return other._line == _line && other._column == _column;
        }

        public override int GetHashCode() {
            return (_line << 16) ^ _column;
        }

        public override string ToString() {
            return "(" + _line + ", " + _column + ")";
        }

        internal string ToDebugString() {
            return "({0},{1},{2})".FormatInvariant(_index, _line, _column);
        }

        public bool Equals(SourceLocation other) {
            return other._line == _line && other._column == _column;
        }

        public int CompareTo(SourceLocation other) {
            int c = _line.CompareTo(other._line);
            if (c == 0) {
                return _column.CompareTo(other._column);
            }
            return c;
        }
    }
}
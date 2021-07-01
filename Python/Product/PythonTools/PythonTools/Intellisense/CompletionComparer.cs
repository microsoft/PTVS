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

namespace Microsoft.PythonTools.Intellisense
{
    /// <summary>
    /// Compares various types of completions.
    /// </summary>
    class CompletionComparer : IEqualityComparer<CompletionResult>, IComparer<CompletionResult>, IComparer<Completion>, IComparer<string>
    {
        /// <summary>
        /// A CompletionComparer that sorts names beginning with underscores to
        /// the end of the list.
        /// </summary>
        public static readonly CompletionComparer UnderscoresLast = new CompletionComparer(true);

        /// <summary>
        /// A CompletionComparer that sorts names beginning with underscores to
        /// the start of the list.
        /// </summary>
        public static readonly CompletionComparer UnderscoresFirst = new CompletionComparer(false);

        bool _sortUnderscoresLast;

        /// <summary>
        /// Compares two strings.
        /// </summary>
        public int Compare(string xName, string yName)
        {
            if (yName == null)
            {
                return xName == null ? 0 : -1;
            }
            else if (xName == null)
            {
                return yName == null ? 0 : 1;
            }

            if (_sortUnderscoresLast)
            {
                bool xUnder = xName.StartsWithOrdinal("__") && xName.EndsWithOrdinal("__");
                bool yUnder = yName.StartsWithOrdinal("__") && yName.EndsWithOrdinal("__");

                if (xUnder != yUnder)
                {
                    // The one that starts with an underscore comes later
                    return xUnder ? 1 : -1;
                }

                bool xSingleUnder = xName.StartsWithOrdinal("_");
                bool ySingleUnder = yName.StartsWithOrdinal("_");
                if (xSingleUnder != ySingleUnder)
                {
                    // The one that starts with an underscore comes later
                    return xSingleUnder ? 1 : -1;
                }
            }
            return String.Compare(xName, yName, StringComparison.CurrentCultureIgnoreCase);
        }

        private CompletionComparer(bool sortUnderscoresLast)
        {
            _sortUnderscoresLast = sortUnderscoresLast;
        }

        /// <summary>
        /// Compares two instances of <see cref="Completion"/> using their
        /// displayed text.
        /// </summary>
        public int Compare(Completion x, Completion y)
        {
            return Compare(x.DisplayText, y.DisplayText);
        }

        /// <summary>
        /// Compares two <see cref="CompletionResult"/> structures using their
        /// names.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public int Compare(CompletionResult x, CompletionResult y)
        {
            return Compare(x.Name, y.Name);
        }

        /// <summary>
        /// Compares two <see cref="CompletionResult"/> structures for equality.
        /// </summary>
        public bool Equals(CompletionResult x, CompletionResult y)
        {
            return x.Name.Equals(y.Name);
        }

        /// <summary>
        /// Gets the hash code for a <see cref="CompletionResult"/> structure.
        /// </summary>
        public int GetHashCode(CompletionResult obj)
        {
            return obj.Name.GetHashCode();
        }
    }

    /// <summary>
    /// A comparer for use with <see cref="Enumerable.Union{TSource}(IEnumerable{TSource}, IEnumerable{TSource}, IEqualityComparer{TSource})"/>
    /// to eliminate duplicate items with the same <see cref="CompletionResult.MergeKey"/>.
    /// </summary>
    class CompletionMergeKeyComparer : IEqualityComparer<CompletionResult>
    {
        public static readonly CompletionMergeKeyComparer Instance = new CompletionMergeKeyComparer();

        public bool Equals(CompletionResult x, CompletionResult y)
        {
            return x.MergeKey.Equals(y.MergeKey);
        }

        public int GetHashCode(CompletionResult obj)
        {
            return obj.MergeKey.GetHashCode();
        }
    }
}

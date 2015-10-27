// Visual Studio Shared Project
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
using System.Collections.ObjectModel;
using System.Linq;

namespace Microsoft.VisualStudioTools {
    static class ObservableCollectionExtensions {
        public static void Merge<T>(
            this ObservableCollection<T> left,
            IEnumerable<T> right,
            IEqualityComparer<T> compareId,
            IComparer<T> compareSortKey
        ) {
            var toAdd = new SortedList<T, T>(compareSortKey);
            var toRemove = new Dictionary<T, int>(compareId);
            var alsoRemove = new List<int>();
            int index = 0;
            foreach (var item in left) {
                if (toRemove.ContainsKey(item)) {
                    alsoRemove.Add(index);
                } else {
                    toRemove[item] = index;
                }
                index += 1;
            }

            foreach (var r in right.OrderBy(k => k, compareSortKey)) {
                if (toRemove.TryGetValue(r, out index)) {
                    toRemove.Remove(r);
                    left[index] = r;
                } else {
                    toAdd[r] = r;
                }
            }

            foreach (var removeAt in toRemove.Values.Concat(alsoRemove).OrderByDescending(i => i)) {
                left.RemoveAt(removeAt);
            }

            index = 0;
            foreach (var item in toAdd.Values) {
                while (index < left.Count && compareSortKey.Compare(left[index], item) <= 0) {
                    index += 1;
                }
                left.Insert(index, item);
            }
        }
    }
}

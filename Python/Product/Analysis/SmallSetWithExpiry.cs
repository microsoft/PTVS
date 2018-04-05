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

using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PythonTools.Analysis {
    struct SmallSetWithExpiry<T> : IEnumerable<T> where T : ICanExpire {
        object _set;

        public SmallSetWithExpiry(T item) {
            _set = item.IsAlive ? (object)item : null;
        }

        public SmallSetWithExpiry(IEnumerable<T> items) {
            var set = new HashSet<T>(items.Where(i => i.IsAlive));
            if (set.Count <= 1) {
                _set = set.FirstOrDefault();
            } else {
                _set = set;
            }
        }

        private static bool UnionInternalMutable(out object newSet, HashSet<T> items, T newItem) {
            items.RemoveWhere(i => !i.IsAlive);
            if (!items.Add(newItem)) {
                newSet = items;
                return false;
            }
            if (items.Count <= 1) {
                newSet = items.FirstOrDefault();
            } else {
                newSet = items;
            }
            return true;
        }

        private static bool UnionInternal(out object newSet, IEnumerable<T> items, T newItem) {
            bool seenNewItem = false;
            var set = new HashSet<T>();
            foreach (var i in items) {
                if (i.IsAlive) {
                    seenNewItem = seenNewItem || i.Equals(newItem);
                    set.Add(i);
                }
            }
            if (!seenNewItem) {
                set.Add(newItem);
            }

            if (set.Count <= 1) {
                newSet = set.FirstOrDefault();
            } else {
                newSet = set;
            }
            return !seenNewItem;
        }

        public bool Add(T item) {
            if (!item.IsAlive) {
                return false;
            }
            if (_set == null) {
                _set = item;
                return true;
            }
            if (_set is HashSet<T> set) {
                return UnionInternalMutable(out _set, set, item);
            }
            if (_set is SetOfTwo<T> set2) {
                return UnionInternal(out _set, set2, item);
            }
            if (_set is T t) {
                if (t.IsAlive && !t.Equals(item)) {
                    _set = new SetOfTwo<T>(t, item);
                } else if (!t.IsAlive) {
                    _set = item;
                } else {
                    return false;
                }
                return true;
            }
            return false;
        }

        private IEnumerable<T> AsEnumerable() {
            if (_set == null) {
                return Enumerable.Empty<T>();
            } else if (_set is T item) {
                if (item.IsAlive) {
                    return Enumerable.Repeat(item, 1);
                }
            } else if (_set is SetOfTwo<T> set2) {
                if (set2.Value1.IsAlive && set2.Value2.IsAlive) {
                    return set2;
                } else if (set2.Value1.IsAlive) {
                    return Enumerable.Repeat(set2.Value1, 1);
                } else if (set2.Value2.IsAlive) {
                    return Enumerable.Repeat(set2.Value2, 1);
                }
            } else if (_set is HashSet<T> set) {
                return set.Where(i => i.IsAlive);
            }
            return Enumerable.Empty<T>();
        }

        public IEnumerator<T> GetEnumerator() => AsEnumerable().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => AsEnumerable().GetEnumerator();

        public int Count {
            get {
                if (_set == null) {
                    return 0;
                } else if (_set is HashSet<T> set) {
                    return set.Count;
                }

                return 1;
            }
        }

    }
}

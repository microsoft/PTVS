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
using System.Linq;

namespace Microsoft.PythonTools.Infrastructure {
    static class EnumerableExtensions {
        public static IEnumerable<T> MaybeEnumerate<T>(this IEnumerable<T> source) {
            return source ?? Enumerable.Empty<T>();
        }

        private static T Identity<T>(T source) {
            return source;
        }

        public static IEnumerable<T> SelectMany<T>(this IEnumerable<IEnumerable<T>> source) {
            return source.SelectMany(Identity);
        }

        public static IEnumerable<T> Ordered<T>(this IEnumerable<T> source) {
            return source.OrderBy(Identity);
        }

        public static IEnumerable<T> Except<T>(this IEnumerable<T> source, T value) {
            return source.Where(v => {
                try {
                    return !v.Equals(value);
                } catch (NullReferenceException) {
                    return false;
                }
            });
        }

        private static TKey GetKey<TKey, TValue>(KeyValuePair<TKey, TValue> source) {
            return source.Key;
        }

        public static IEnumerable<TKey> Keys<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> source) {
            return source.Select(GetKey);
        }

        public static IEnumerable<KeyValuePair<TKey, TValue>> AsEnumerable<TKey, TValue>(
            this System.Collections.IDictionary source
        ) {
            foreach (System.Collections.DictionaryEntry entry in source) {
                yield return new KeyValuePair<TKey, TValue>((TKey)entry.Key, (TValue)entry.Value);
            }
        }

        private class TakeWhileCounter<T> {
            private ulong _remaining;

            public TakeWhileCounter(ulong count) {
                _remaining = count;
            }

            public bool ShouldTake(T value) {
                if (_remaining == 0) {
                    return false;
                }
                _remaining -= 1;
                return true;
            }
        }

        public static IEnumerable<T> Take<T>(this IEnumerable<T> source, ulong count) {
            return source.TakeWhile(new TakeWhileCounter<T>(count).ShouldTake);
        }

        public static IEnumerable<T> Take<T>(this IEnumerable<T> source, long count) {
            if (count > 0) {
                return source.TakeWhile(new TakeWhileCounter<T>((ulong)count).ShouldTake);
            }
            return Enumerable.Empty<T>();
        }

        public static int IndexOf<T>(this IEnumerable<T> source, T value) where T : IEquatable<T> {
            return source.IndexOf(value, EqualityComparer<T>.Default);
        }

        public static int IndexOf<T>(this IEnumerable<T> source, T value, IEqualityComparer<T> comparer) {
            int index = 0;
            foreach (var v in source) {
                if (comparer.Equals(value, v)) {
                    return index;
                }
                index += 1;
            }

            return -1;
        }

        public static int IndexOf<T>(this IEnumerable<T> source, Func<T, bool> predicate) {
            int index = 0;
            foreach (var v in source) {
                if (predicate(v)) {
                    return index;
                }
                index += 1;
            }

            return -1;
        }

        public static IEnumerable<T> TraverseBreadthFirst<T>(this T root, Func<T, IEnumerable<T>> selectChildren) {
            Queue<T> items = new Queue<T>();
            items.Enqueue(root);
            while (items.Count > 0) {
                var item = items.Dequeue();
                yield return item;

                IEnumerable<T> childen = selectChildren(item);
                if (childen == null) {
                    continue;
                }

                foreach (var child in childen) {
                    items.Enqueue(child);
                }
            }
        }

        public static IEnumerable<T> TraverseDepthFirst<T>(this T root, Func<T, IEnumerable<T>> selectChildren) {
            yield return root;

            var children = selectChildren(root);
            if (children != null) {
                foreach (T child in children) {
                    foreach (T t in TraverseDepthFirst(child, selectChildren)) {
                        yield return t;
                    }
                }
            }
        }
    }
}

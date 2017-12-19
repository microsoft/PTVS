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
using System.IO;
using System.Linq;
using System.Threading;

namespace Microsoft.PythonTools.Analysis.Infrastructure {
    class PathEqualityComparer : IEqualityComparer<string> {
        public static readonly PathEqualityComparer Instance = new PathEqualityComparer();

        private static readonly char[] InvalidPathChars = GetInvalidPathChars();

        private static char[] GetInvalidPathChars() {
            return Path.GetInvalidPathChars().Concat(new[] { '*', '?' }).ToArray();
        }

        // This is just a small cache to deal with the same keys being used
        // frequently in a loop.
        private class CacheItem {
            public string CompareKey;
            public long Accessed;
        }
        private readonly Dictionary<string, CacheItem> _compareKeyCache = new Dictionary<string, CacheItem>();
        private long _accessCount;
        private const int CACHE_UPPER_LIMIT = 32;

        private readonly StringComparer Ordinal = StringComparer.Ordinal;

        private PathEqualityComparer() { }

        private static string GetCompareKeyUncached(string path) {
            path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            var doubleSep = new string(Path.DirectorySeparatorChar, 2);
            string oldPath = null;
            while (oldPath != path) {
                oldPath = path;
                path = path.Replace(doubleSep, Path.DirectorySeparatorChar.ToString());
            }

            if (path.Length > 0 && path[path.Length - 1] == Path.DirectorySeparatorChar) {
                path = path.Remove(path.Length - 1);
            }

            // TODO: Don't uppercase on non-Windows platforms
            return path.ToUpperInvariant();
        }

        private CacheItem GetOrCreateCacheItem(string key, out bool created) {
            CacheItem item;
            created = true;
            var access = Interlocked.Increment(ref _accessCount);
            lock (_compareKeyCache) {
                if (_compareKeyCache.TryGetValue(key, out item)) {
                    created = false;
                }

                if (created) {
                    if (_compareKeyCache.Count > CACHE_UPPER_LIMIT) {
                        // Purge half the old items in the cache
                        foreach (var k in _compareKeyCache.OrderBy(kv => kv.Value.Accessed).Take(CACHE_UPPER_LIMIT / 2).Select(kv => kv.Key).ToArray()) {
                            _compareKeyCache.Remove(k);
                        }
                    }

                    _compareKeyCache[key] = item = new CacheItem { Accessed = access };
                }
            }

            if (!created) {
                lock (item) {
                    item.Accessed = access;
                }
            }

            return item;
        }

        private string GetCompareKey(string path) {
            var item = GetOrCreateCacheItem(path, out bool created);
            
            string result;
            lock (item) {
                if (created) {
                    item.CompareKey = result = GetCompareKeyUncached(path);
                } else {
                    result = item.CompareKey;
                }
            }

            // The only time the result is null is if we race with initialization.
            // This can only happen cross-thread, so let's loop until it is ready.
            while (result == null) {
                Thread.Yield();
                lock (item) {
                    result = item.CompareKey;
                }
            }

            return result;
        }

        public static bool IsValidPath(string x) {
            if (string.IsNullOrEmpty(x)) {
                return false;
            }
            return x.IndexOfAny(InvalidPathChars) < 0;
        }

        public bool StartsWith(string x, string prefix, bool allowFullMatch = true) {
            prefix = GetCompareKey(prefix);
            x = GetCompareKey(x);

            if (Ordinal.Equals(prefix, x)) {
                return allowFullMatch;
            }

            return x.StartsWith(prefix + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        }

        public bool Equals(string x, string y) {
            return Ordinal.Equals(GetCompareKey(x), GetCompareKey(y));
        }

        public int GetHashCode(string obj) {
            return Ordinal.GetHashCode(GetCompareKey(obj));
        }
    }
}

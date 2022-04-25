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

using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PythonTools.Common.Core.Extensions {
    public class EmptyDictionary<TKey, TValue>: IReadOnlyDictionary<TKey, TValue> {
        public static readonly IReadOnlyDictionary<TKey, TValue> Instance = new EmptyDictionary<TKey, TValue>();

        public TValue this[TKey key] => throw new KeyNotFoundException();
        public IEnumerable<TKey> Keys => Enumerable.Empty<TKey>();
        public IEnumerable<TValue> Values => Enumerable.Empty<TValue>();
        public int Count => 0;
        public bool ContainsKey(TKey key) => false;
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => Enumerable.Empty<KeyValuePair<TKey, TValue>>().GetEnumerator();

        public bool TryGetValue(TKey key, out TValue value) {
            value = default;
            return false;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

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

namespace Microsoft.PythonTools.Common.Core.Extensions {
    public static class ArrayExtensions {
        public static int IndexOf<T>(this T[] array, Func<T, bool> predicate) {
            for (var i = 0; i < array.Length; i++) {
                if (predicate(array[i])) {
                    return i;
                }
            }

            return -1;
        }

        public static int IndexOf<T, TValue>(this T[] array, TValue value, Func<T, TValue, bool> predicate) {
            for (var i = 0; i < array.Length; i++) {
                if (predicate(array[i], value)) {
                    return i;
                }
            }

            return -1;
        }

        public static TCollection AddIfNotNull<TCollection, TItem>(this TCollection list, TItem item)
            where TCollection : ICollection<TItem>
            where TItem : class {
            if (item == null) {
                return list;
            }

            list.Add(item);
            return list;
        }

        public static TCollection AddIfNotNull<TCollection, TItem>(this TCollection list, params TItem[] items)
            where TCollection : ICollection<TItem>
            where TItem : class {
            foreach (var item in items) {
                list.AddIfNotNull(item);
            }

            return list;
        }
    }
}

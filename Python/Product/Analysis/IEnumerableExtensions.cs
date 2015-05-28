/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PythonTools.Analysis {
    static class IEnumerableExtensions {
        private static T Identity<T>(T source) {
            return source;
        }

        public static IEnumerable<T> SelectMany<T>(this IEnumerable<IEnumerable<T>> source) {
            return source.SelectMany(Identity<IEnumerable<T>>);
        }

        public static bool AnyContains<T>(this IEnumerable<IEnumerable<T>> source, T value) {
            foreach (var set in source) {
                if (set.Contains(value)) {
                    return true;
                }
            }
            return false;
        }

        public static bool AnyContains(this IEnumerable<IAnalysisSet> source, AnalysisValue value) {
            foreach (var set in source) {
                if (set.Contains(value)) {
                    return true;
                }
            }
            return false;
        }

        private static TKey GetKey<TKey, TValue>(KeyValuePair<TKey, TValue> source) {
            return source.Key;
        }

        public static IEnumerable<TKey> Keys<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> source) {
            return source.Select(GetKey);
        }
    }
}

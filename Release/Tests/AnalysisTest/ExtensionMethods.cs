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

namespace AnalysisTest {
    public static class ExtensionMethods {
        public static IEnumerable<int> FindIndexesOf(this string s, string substring) {
            int pos = 0;
            while (true) {
                pos = s.IndexOf(substring, pos);
                if (pos < 0) {
                    break;
                }
                yield return pos;
                pos++;
            }
        }

        public static HashSet<T> ToSet<T>(this IEnumerable<T> enumeration) {
            return new HashSet<T>(enumeration);
        }

        public static bool ContainsExactly<T>(this HashSet<T> set, IEnumerable<T> values) {
            if (set.Count != values.Count()) {
                return false;
            }
            foreach (var value in values) {
                if (!set.Contains(value)) {
                    return false;
                }
            }
            return true;
        }
    }

    
}

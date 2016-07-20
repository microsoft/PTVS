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

using System.Collections.Generic;

namespace Microsoft.PythonTools.Analysis {
    internal static class HashSetExtensions {
        internal static bool AddValue<T>(ref ISet<T> references, T value) {
            if (references == null) {
                references = new SetOfOne<T>(value);
                return true;
            } else if (references is SetOfOne<T>) {
                if (!references.Contains(value)) {
                    references = new SetOfTwo<T>(((SetOfOne<T>)references).Value, value);
                    return true;
                }
            } else if (references is SetOfTwo<T>) {
                if (!references.Contains(value)) {
                    references = new HashSet<T>(references);
                    return references.Add(value);
                }
            } else {
                return references.Add(value);
            }
            return false;
        }

    }
}

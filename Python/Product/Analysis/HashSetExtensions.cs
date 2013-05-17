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

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

using System;
using System.Diagnostics;

namespace Microsoft.PythonTools.Parsing.Ast {
    public abstract class Expression : Node {
        internal static Expression[] EmptyArray = new Expression[0];

        internal virtual string CheckAssign() {
            return "can't assign to " + NodeName;
        }

        internal virtual string CheckAugmentedAssign() {
            if (CheckAssign() != null) {
                return "illegal expression for augmented assignment";
            }

            return null;
        }

        internal virtual string CheckDelete() {
            return "can't delete " + NodeName;
        }
    }
}

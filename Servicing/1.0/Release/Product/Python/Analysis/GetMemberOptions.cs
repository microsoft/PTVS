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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    [Flags]
    public enum GetMemberOptions {
        None,
        /// <summary>
        /// When an expression resolves to multiple types the intersection of members is returned.  When this flag
        /// is not present the union of all members is returned.
        /// </summary>
        IntersectMultipleResults = 0x01,

        HideAdvancedMembers = 0x02,
    }

    internal static class GetMemberOptionsExtensions {
        public static bool Intersect(this GetMemberOptions self) {
            return (self & GetMemberOptions.IntersectMultipleResults) != 0;
        }
        public static bool HideAdvanced(this GetMemberOptions self) {
            return (self & GetMemberOptions.HideAdvancedMembers) != 0;
        }
    }
}

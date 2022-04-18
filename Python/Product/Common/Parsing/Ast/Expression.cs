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

using Microsoft.PythonTools.Common.Core.Extensions;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public abstract class Expression : Node {
        internal Expression() {
        }

        internal virtual string CheckAssign() => "can't assign to " + NodeName;

        internal virtual string CheckAugmentedAssign() {
            if (CheckAssign() != null) {
                return "illegal expression for augmented assignment";
            }

            return null;
        }

        internal virtual string CheckDelete() => "can't delete " + NodeName;

        internal virtual string CheckAssignExpr() => Strings.NamedAssignmentWithErrorMsg.FormatInvariant(NodeName);
    }
}

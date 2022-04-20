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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;
using Microsoft.PythonTools.Common.Core.Collections;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public abstract class SequenceExpression : Expression {
        protected SequenceExpression(ImmutableArray<Expression> items) {
            Items = items;
        }

        public ImmutableArray<Expression> Items { get; }

        internal override string CheckAssign() {
            for (var i = 0; i < Items.Count; i++) {
                var e = Items[i];
                if (e is StarredExpression se && se.StarCount == 1) {
                    continue;
                }

                if (e.CheckAssign() != null) {
                    // we don't return the same message here as CPython doesn't seem to either, 
                    // for example ((yield a), 2,3) = (2,3,4) gives a different error than
                    // a = yield 3 = yield 4.
                    return "can't assign to " + e.NodeName;
                }
            }
            return null;

        }

        internal override string CheckDelete() {
            for (var i = 0; i < Items.Count; i++) {
                var e = Items[i];
                if (e.CheckDelete() != null) {
                    // we don't return the same message here as CPython doesn't seem to either, 
                    // for example ((yield a), 2,3) = (2,3,4) gives a different error than
                    // a = yield 3 = yield 4.
                    return "can't delete " + e.NodeName;
                }
            }
            return null;
        }

        internal override string CheckAugmentedAssign() => "illegal expression for augmented assignment";

        public override string NodeName => "sequence";

        private static bool IsComplexAssignment(Expression expr) => !(expr is NameExpression);
    }
}

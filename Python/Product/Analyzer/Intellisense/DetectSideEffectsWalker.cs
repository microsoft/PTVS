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
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Walks the AST, and indicates if execution of the node has side effects
    /// </summary>
    internal class DetectSideEffectsWalker : PythonWalker {
        public bool HasSideEffects { get; private set; }

        public override bool Walk(AwaitExpression node) {
            HasSideEffects = true;
            return false;
        }

        public override bool Walk(CallExpression node) {
            HasSideEffects = true;
            return false;
        }

        public override bool Walk(BackQuoteExpression node) {
            HasSideEffects = true;
            return false;
        }

        public override bool Walk(ErrorExpression node) {
            HasSideEffects = true;
            return false;
        }

        public override bool Walk(YieldExpression node) {
            HasSideEffects = true;
            return false;
        }

        public override bool Walk(YieldFromExpression node) {
            HasSideEffects = true;
            return false;
        }

        private static readonly HashSet<string> allowedCalls = new HashSet<string> {
                "abs", "bool", "callable", "chr", "cmp", "complex", "divmod", "float", "format",
                "getattr", "hasattr", "hash", "hex", "id", "int", "isinstance", "issubclass",
                "len", "max", "min", "oct", "ord", "pow", "repr", "round", "str", "tuple", "type"
            };

        public static bool IsSideEffectFreeCall(string name) {
            return allowedCalls.Contains(name);
        }
    }
}

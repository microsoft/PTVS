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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Computes the fully qualified function name, including name of the enclosing class for methods,
    /// and, recursively, names of any outer functions.
    /// </summary>
    /// <example>
    /// Given this code:
    /// <code>
    /// class A:
    ///   def b(self):
    ///     def c():
    ///       class D:
    ///         def e(self):
    ///           pass
    /// </code>
    /// And with the current statement being <c>pass</c>, the qualified name is "D.e in c in A.b".
    /// </example>
    class QualifiedFunctionNameWalker : PythonWalker {
        private readonly PythonAst _ast;
        private readonly int _lineNumber;
        private readonly List<string> _names = new List<string>();
        private readonly string _expectedFuncName;

        public QualifiedFunctionNameWalker(PythonAst ast, int lineNumber, string expectedFuncName) {
            _ast = ast;
            _lineNumber = lineNumber;
            _expectedFuncName = expectedFuncName;
        }

        public IEnumerable<string> Name => _names.AsEnumerable().Reverse();

        public static string GetDisplayName(int lineNo, string functionName, PythonAst ast, Func<string, string, string> nameAggregator) {
            var walker = new QualifiedFunctionNameWalker(ast, lineNo, functionName);
            try {
                ast.Walk(walker);
            } catch (InvalidDataException) {
                // Walker ran into a mismatch between expected function name and AST, so we cannot
                // rely on AST to construct an accurate qualified name. Just return what we have.
                return functionName;
            }

            var names = walker.Name;
            if (names.Any()) {
                string qualName = names.Aggregate(nameAggregator);
                if (!string.IsNullOrEmpty(qualName)) {
                    return qualName;
                }
            }

            return functionName;
        }

        public override void PostWalk(FunctionDefinition node) {
            int start = node.GetStart(_ast).Line;
            int end = node.Body.GetEnd(_ast).Line + 1;
            if (_lineNumber < start || _lineNumber >= end) {
                return;
            }

            string funcName = node.Name;
            if (_names.Count == 0 && funcName != _expectedFuncName) {
                // The innermost function name must match the one that we've got from the code object.
                // If it doesn't, the source code that we're parsing is out of sync with the running program,
                // and cannot be used to compute the fully qualified name.
                throw new InvalidDataException();
            }

            for (var classDef = node.Parent as ClassDefinition; classDef != null; classDef = classDef.Parent as ClassDefinition) {
                funcName = classDef.Name + "." + funcName;
            }

            _names.Add(funcName);
        }
    }
}

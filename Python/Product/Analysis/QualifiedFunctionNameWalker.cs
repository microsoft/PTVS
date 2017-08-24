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

using System.IO;
using System.Text;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    public class QualifiedFunctionNameWalker : PythonWalker {
        private readonly PythonAst _ast;
        private readonly int _lineNumber;
        private readonly StringBuilder _name = new StringBuilder();
        private readonly string _expectedFuncName;

        public QualifiedFunctionNameWalker(PythonAst ast, int lineNumber, string expectedFuncName) {
            _ast = ast;
            _lineNumber = lineNumber;
            _expectedFuncName = expectedFuncName;
        }

        public string Name {
            get { return _name.ToString(); }
        }

        public override void PostWalk(FunctionDefinition node) {
            int start = node.GetStart(_ast).Line;
            int end = node.Body.GetEnd(_ast).Line + 1;
            if (_lineNumber < start || _lineNumber >= end) {
                return;
            }

            string funcName = node.Name;
            if (_name.Length == 0 && funcName != _expectedFuncName) {
                // The innermost function name must match the one that we've got from the code object.
                // If it doesn't, the source code that we're parsing is out of sync with the running program,
                // and cannot be used to compute the fully qualified name.
                throw new InvalidDataException();
            }

            for (var classDef = node.Parent as ClassDefinition; classDef != null; classDef = classDef.Parent as ClassDefinition) {
                funcName = classDef.Name + "." + funcName;
            }

            if (_name.Length != 0) {
                var inner = _name.ToString();
                _name.Clear();
                _name.Append(Strings.DebugStackFrameNameInName.FormatUI(inner, funcName));
            } else {
                _name.Append(funcName);
            }
        }
    }
}

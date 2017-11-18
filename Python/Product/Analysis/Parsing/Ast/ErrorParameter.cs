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

using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {
    class ErrorParameter : Parameter {
        private readonly Expression _error;
        
        public ErrorParameter(Expression errorValue, ParameterKind kind)
            : base(null, kind) {
                _error = errorValue;
        }

        public Expression Error => _error;

        internal override void AppendParameterName(StringBuilder res, PythonAst ast, CodeFormattingOptions format, string leadingWhiteSpace) {
            _error?.AppendCodeString(res, ast, format, leadingWhiteSpace);
        }
    }
}

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
    public class ErrorExpression : Expression {
        private readonly Expression _preceeding;
        private readonly string _verbatimImage;

        public ErrorExpression(string verbatimImage, Expression preceeding) {
            _preceeding = preceeding;
            _verbatimImage = verbatimImage;
        }

        public string VerbatimImage {
            get {
                return _verbatimImage;
            }
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            if (_preceeding != null) {
                _preceeding.AppendCodeString(res, ast, format);
            }
            res.Append(_verbatimImage ?? "<error>");
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_preceeding != null) {
                    _preceeding.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }
    }
}

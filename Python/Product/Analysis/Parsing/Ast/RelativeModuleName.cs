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
    public class RelativeModuleName : ModuleName {
        private readonly int _dotCount;

        public RelativeModuleName(NameExpression[] names, int dotCount)
            : base(names) {
            _dotCount = dotCount;
        }

        public override string MakeString() {
            return new string('.', DotCount) + base.MakeString();
        }

        public int DotCount {
            get {
                return _dotCount;
            }
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            var whitespace = this.GetListWhiteSpace(ast);
            for (int i = 0; i < _dotCount; i++) {
                if (whitespace != null) {
                    res.Append(whitespace[i]);
                }
                res.Append('.');
            }
            base.AppendCodeString(res, ast, format);
        }

        public override string GetLeadingWhiteSpace(PythonAst ast) {
            var whitespace = this.GetListWhiteSpace(ast);
            if (whitespace != null && whitespace.Length > 0) {
                return whitespace[0];
            }
            return null;
        }

        public override void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace) {
            var list = this.GetListWhiteSpace(ast);
            if (list != null && list.Length > 0) {
                list[0] = whiteSpace;
            }
        }
    }
}

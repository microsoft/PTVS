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

using System.Text;
using Microsoft.PythonTools.Common.Core.Collections;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public class RelativeModuleName : ModuleName {
        public RelativeModuleName(ImmutableArray<NameExpression> names, int dotCount)
            : base(names) {
            DotCount = dotCount;
        }

        public override string MakeString() => new string('.', DotCount) + base.MakeString();

        public int DotCount { get; }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            var whitespace = this.GetListWhiteSpace(ast);
            for (var i = 0; i < DotCount; i++) {
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

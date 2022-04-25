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
using System.Text;
using Microsoft.PythonTools.Common.Core;
using Microsoft.PythonTools.Common.Core.Extensions;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public class SublistParameter : Parameter {
        private readonly int _position;

        public SublistParameter(int position, TupleExpression tuple)
            : base(null, ParameterKind.Normal) {
            _position = position;
            Tuple = tuple;
        }

        public override string Name => ".{0}".FormatUI(_position);

        public TupleExpression Tuple { get; }

        public override IEnumerable<Node> GetChildNodes() {
            if (Tuple != null) yield return Tuple;
            if (DefaultValue != null) yield return DefaultValue;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Tuple?.Walk(walker);
                DefaultValue?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format, string leadingWhiteSpace) {
            var kwOnlyText = this.GetExtraVerbatimText(ast);
            if (kwOnlyText != null) {
                if (leadingWhiteSpace != null) {
                    res.Append(leadingWhiteSpace);
                    res.Append(kwOnlyText.TrimStart());
                    leadingWhiteSpace = null;
                } else {
                    res.Append(kwOnlyText);
                }
            }
            Tuple.AppendCodeString(res, ast, format, leadingWhiteSpace);
            if (DefaultValue != null) {
                format.Append(
                    res,
                    format.SpaceAroundDefaultValueEquals,
                    " ",
                    "",
                    NodeAttributes.GetWhiteSpace(this, ast, WhitespacePrecedingAssign)
                );
                res.Append('=');
                if (format.SpaceAroundDefaultValueEquals != null) {
                    DefaultValue.AppendCodeString(res, ast, format, format.SpaceAroundDefaultValueEquals.Value ? " " : "");
                } else {
                    DefaultValue.AppendCodeString(res, ast, format);
                }
            }
        }
    }
}

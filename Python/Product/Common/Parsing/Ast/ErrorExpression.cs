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
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public class ErrorExpression : Expression {
        private readonly Expression _preceding;
        private readonly string _verbatimImage;
        private readonly ErrorExpression _nested;

        private ErrorExpression(string verbatimImage, Expression preceding, ErrorExpression nested) {
            _preceding = preceding;
            _verbatimImage = verbatimImage;
            _nested = nested;
        }

        public ErrorExpression(string verbatimImage, Expression preceding) : this(verbatimImage, preceding, null) { }

        public override string NodeName => "error expression";

        public ErrorExpression AddPrefix(string verbatimImage, Expression preceding) => new ErrorExpression(verbatimImage, preceding, this);

        public string VerbatimImage => _verbatimImage;

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            _preceding?.AppendCodeString(res, ast, format);
            res.Append(_verbatimImage ?? "<error>");
            _nested?.AppendCodeString(res, ast, format);
        }

        public override IEnumerable<Node> GetChildNodes() {
            if (_preceding != null) yield return _preceding;
            if (_nested != null) yield return _nested;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                _preceding?.Walk(walker);
                _nested?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                if (_preceding != null) {
                    await _preceding.WalkAsync(walker, cancellationToken);
                }
                if (_nested != null) {
                    await _nested.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }
    }
}

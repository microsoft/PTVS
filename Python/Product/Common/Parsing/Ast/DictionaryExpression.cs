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
using Microsoft.PythonTools.Common.Core.Collections;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public class DictionaryExpression : Expression {
        public DictionaryExpression(ImmutableArray<SliceExpression> items) {
            Items = items;
        }

        public ImmutableArray<SliceExpression> Items { get; }

        public override string NodeName => "dictionary display";

        public override IEnumerable<Node> GetChildNodes() => Items;

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                foreach (var s in Items) {
                    s.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                foreach (var s in Items) {
                    await s.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) => ListExpression.AppendItems(res, ast, format, "{", this.IsMissingCloseGrouping(ast) ? "" : "}", this, Items);
    }

    /// <summary>
    /// Subclass of SliceExpression for an entry in a dict that only has a key.
    /// These are typically parser errors.
    /// </summary>
    public class DictKeyOnlyExpression : SliceExpression {
        public DictKeyOnlyExpression(Expression expr) : base(expr, null, null, false) { }

        public Expression Key => SliceStart;

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) => Key?.AppendCodeString(res, ast, format);
    }

    /// <summary>
    /// Subclass of SliceExpression for an entry in a dict that only has a value.
    /// These are typically StarExpressions for unpacking.
    /// </summary>
    public class DictValueOnlyExpression : SliceExpression {
        public DictValueOnlyExpression(Expression expr) : base(null, expr, null, false) { }

        public Expression Value => SliceStop;

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) => Value?.AppendCodeString(res, ast, format);
    }
}

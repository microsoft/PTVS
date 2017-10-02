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
using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {

    public class DictionaryExpression : Expression {
        private readonly SliceExpression[] _items;

        public DictionaryExpression(params SliceExpression[] items) {
            _items = items;
        }

        public IList<SliceExpression> Items {
            get { return _items; }
        }

        public override string NodeName {
            get {
                return "dictionary display";
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_items != null) {
                    foreach (SliceExpression s in _items) {
                        s.Walk(walker);
                    }
                }
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            ListExpression.AppendItems(res, ast, format, "{", this.IsMissingCloseGrouping(ast) ? "" : "}", this, Items);
        }
    }

    /// <summary>
    /// Subclass of SliceExpression for an entry in a dict that only has a key.
    /// These are typically parser errors.
    /// </summary>
    public class DictKeyOnlyExpression : SliceExpression {
        public DictKeyOnlyExpression(Expression expr) : base(expr, null, null, false) { }

        public Expression Key => SliceStart;

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            Key?.AppendCodeString(res, ast, format);
        }
    }

    /// <summary>
    /// Subclass of SliceExpression for an entry in a dict that only has a value.
    /// These are typically StarExpressions for unpacking.
    /// </summary>
    public class DictValueOnlyExpression : SliceExpression {
        public DictValueOnlyExpression(Expression expr) : base(null, expr, null, false) { }

        public Expression Value => SliceStop;

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            Value?.AppendCodeString(res, ast, format);
        }
    }
}

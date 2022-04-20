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

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Common.Core.Collections;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public class WithStatement : Statement, IMaybeAsyncStatement {
        private int? _keywordEndIndex;

        public WithStatement(ImmutableArray<WithItem> items, Statement body) {
            Items = items;
            Body = body;
        }

        public WithStatement(ImmutableArray<WithItem> items, Statement body, bool isAsync) : this(items, body) {
            IsAsync = isAsync;
        }

        public ImmutableArray<WithItem> Items { get; }

        public int HeaderIndex { get; set; }
        internal void SetKeywordEndIndex(int index) => _keywordEndIndex = index;
        public override int KeywordEndIndex => _keywordEndIndex ?? StartIndex + (IsAsync ? 10 : 4);
        public override int KeywordLength => KeywordEndIndex - StartIndex;

        public Statement Body { get; }
        public bool IsAsync { get; }

        public override IEnumerable<Node> GetChildNodes() {
            foreach (var item in Items) {
                yield return item;
            }
            if (Body != null) yield return Body;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                foreach (var item in Items) {
                    item.Walk(walker);
                }

                if (Body != null) {
                    Body.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                foreach (var item in Items) {
                    await item.WalkAsync(walker, cancellationToken);
                }
                if (Body != null) {
                    await Body.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        public int GetIndexOfWith(PythonAst ast) {
            if (!IsAsync) {
                return StartIndex;
            }
            return StartIndex + this.GetSecondWhiteSpace(ast).Length + 5;
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            format.ReflowComment(res, this.GetPreceedingWhiteSpace(ast));
            if (IsAsync) {
                res.Append("async");
                res.Append(this.GetSecondWhiteSpace(ast));
            }
            res.Append("with");
            var itemWhiteSpace = this.GetListWhiteSpace(ast);
            var whiteSpaceIndex = 0;
            for (var i = 0; i < Items.Count; i++) {
                var item = Items[i];
                if (i != 0) {
                    if (itemWhiteSpace != null) {
                        res.Append(itemWhiteSpace[whiteSpaceIndex++]);
                    }
                    res.Append(',');
                }

                item.ContextManager.AppendCodeString(res, ast, format);
                if (item.Variable != null) {
                    if (itemWhiteSpace != null) {
                        res.Append(itemWhiteSpace[whiteSpaceIndex++]);
                    } else {
                        res.Append(' ');
                    }
                    res.Append("as");
                    item.Variable.AppendCodeString(res, ast, format);
                }
            }

            Body.AppendCodeString(res, ast, format);
        }
    }

    public sealed class WithItem : Node {
        public WithItem(Expression contextManager, Expression variable, int asIndex) {
            ContextManager = contextManager;
            Variable = variable;
            AsIndex = asIndex;
        }

        public Expression ContextManager { get; }
        public Expression Variable { get; }
        public int AsIndex { get; }

        public override IEnumerable<Node> GetChildNodes() {
            if (ContextManager != null) yield return ContextManager;
            if (Variable != null) yield return Variable;
        }

        public override void Walk(PythonWalker walker) {
            ContextManager?.Walk(walker);
            Variable?.Walk(walker);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                if (ContextManager != null) {
                    await ContextManager.WalkAsync(walker, cancellationToken);
                }
                if (Variable != null) {
                    await Variable.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) =>
            // WithStatement expands us 
            throw new InvalidOperationException();
    }
}

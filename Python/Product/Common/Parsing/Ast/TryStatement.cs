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
using Microsoft.PythonTools.Common.Core;
using Microsoft.PythonTools.Common.Core.Extensions;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public class TryStatement : Statement {
        private readonly TryStatementHandler[] _handlers;

        public TryStatement(Statement body, TryStatementHandler[] handlers, Statement else_, Statement finally_) {
            Body = body;
            _handlers = handlers;
            Else = else_;
            Finally = finally_;
        }

        public int HeaderIndex { get; set; }
        public int ElseIndex { get; set; }
        public int FinallyIndex { get; set; }
        public override int KeywordLength => 3;

        /// <summary>
        /// The statements under the try-block.
        /// </summary>
        public Statement Body { get; }

        /// <summary>
        /// The body of the optional Else block for this try. NULL if there is no Else block.
        /// </summary>
        public Statement Else { get; }

        /// <summary>
        /// The body of the optional finally associated with this try. NULL if there is no finally block.
        /// </summary>
        public Statement Finally { get; }

        /// <summary>
        /// Array of except (catch) blocks associated with this try. NULL if there are no except blocks.
        /// </summary>
        public IList<TryStatementHandler> Handlers => _handlers;

        public override IEnumerable<Node> GetChildNodes() {
            if (Body != null) yield return Body;
            if (_handlers != null) {
                foreach (var handler in _handlers) {
                    yield return handler;
                }
            }

            if (Else != null) yield return Else;
            if (Finally != null) yield return Finally;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Body?.Walk(walker);
                foreach (var handler in _handlers.MaybeEnumerate()) {
                    handler.Walk(walker);
                }
                Else?.Walk(walker);
                Finally?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                if (Body != null) {
                    await Body.WalkAsync(walker, cancellationToken);
                }
                foreach (var handler in _handlers.MaybeEnumerate()) {
                    await handler.WalkAsync(walker, cancellationToken);
                }
                if (Else != null) {
                    await Else.WalkAsync(walker, cancellationToken);
                }
                if (Finally != null) {
                    await Finally.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            format.ReflowComment(res, this.GetPreceedingWhiteSpace(ast));
            res.Append("try");
            Body.AppendCodeString(res, ast, format);

            foreach (var h in _handlers.MaybeEnumerate()) {
                h.AppendCodeString(res, ast, format);
            }

            if (Else != null) {
                format.ReflowComment(res, this.GetSecondWhiteSpace(ast));
                res.Append("else");
                Else.AppendCodeString(res, ast, format);
            }

            if (Finally != null) {
                format.ReflowComment(res, this.GetThirdWhiteSpace(ast));
                res.Append("finally");
                Finally.AppendCodeString(res, ast, format);
            }
        }
    }

    // A handler corresponds to the except block.
    public class TryStatementHandler : Node {
        public TryStatementHandler(Expression test, Expression target, Statement body) {
            Test = test;
            Target = target;
            Body = body;
        }

        public int HeaderIndex { get; set; }
        public int KeywordLength => 6;
        public int KeywordEndIndex { get; set; }

        public Expression Test { get; }
        public Expression Target { get; }
        public Statement Body { get; }

        public override IEnumerable<Node> GetChildNodes() {
            if (Test != null) yield return Test;
            if (Target != null) yield return Target;
            if (Body != null) yield return Body;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Test?.Walk(walker);
                Target?.Walk(walker);
                Body?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                if (Test != null) {
                    await Test.WalkAsync(walker, cancellationToken);
                }
                if (Target != null) {
                    await Target.WalkAsync(walker, cancellationToken);
                }
                if (Body != null) {
                    await Body.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            format.ReflowComment(res, this.GetPreceedingWhiteSpace(ast));
            res.Append("except");
            if (Test != null) {
                Test.AppendCodeString(res, ast, format);
                if (Target != null) {
                    res.Append(this.GetSecondWhiteSpace(ast));
                    if (this.IsAltForm(ast)) {
                        res.Append("as");
                    } else {
                        res.Append(",");
                    }

                    Target.AppendCodeString(res, ast, format);
                }
            }

            Body.AppendCodeString(res, ast, format);
        }
    }
}

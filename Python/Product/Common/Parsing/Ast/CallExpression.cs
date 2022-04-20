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
    public class CallExpression : Expression {
        public CallExpression(Expression target, ImmutableArray<Arg> args) {
            Target = target;
            Args = args;
        }

        public Expression Target { get; }
        public ImmutableArray<Arg> Args { get; }

        public bool NeedsLocalsDictionary() {
            if (!(Target is NameExpression nameExpr)) {
                return false;
            }

            if (Args.Count == 0) {
                switch (nameExpr.Name) {
                    case "locals":
                    case "vars":
                    case "dir":
                        return true;
                    default:
                        return false;
                }
            }

            if (Args.Count == 1 && (nameExpr.Name == "dir" || nameExpr.Name == "vars")) {
                if (Args[0].Name == "*" || Args[0].Name == "**") {
                    // could be splatting empty list or dict resulting in 0-param call which needs context
                    return true;
                }
            } else if (Args.Count == 2 && (nameExpr.Name == "dir" || nameExpr.Name == "vars")) {
                if (Args[0].Name == "*" && Args[1].Name == "**") {
                    // could be splatting empty list and dict resulting in 0-param call which needs context
                    return true;
                }
            } else {
                switch (nameExpr.Name) {
                    case "eval":
                    case "execfile":
                        return true;
                }
            }
            return false;
        }

        internal override string CheckAssign() => "can't assign to function call";

        internal override string CheckDelete() => "can't delete function call";

        public override string NodeName => "function call";

        public override IEnumerable<Node> GetChildNodes() {
            if (Target != null) yield return Target;
            foreach (var arg in Args) {
                yield return arg;
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Target?.Walk(walker);
                foreach (var arg in Args) {
                    arg.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }


        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                if (Target != null) {
                    await Target.WalkAsync(walker, cancellationToken);
                }
                foreach (var arg in Args) {
                    await arg.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            Target.AppendCodeString(res, ast, format);
            format.Append(
                res,
                format.SpaceBeforeCallParen,
                " ",
                string.Empty,
                this.GetPreceedingWhiteSpaceDefaultNull(ast)
            );

            res.Append('(');

            if (Args.Count == 0) {
                if (format.SpaceWithinEmptyCallArgumentList != null && format.SpaceWithinEmptyCallArgumentList.Value) {
                    res.Append(' ');
                }
            } else {
                var listWhiteSpace = format.SpaceBeforeComma == null ? this.GetListWhiteSpace(ast) : null;
                var spaceAfterComma = format.SpaceAfterComma.HasValue ? (format.SpaceAfterComma.Value ? " " : string.Empty) : (string)null;
                for (var i = 0; i < Args.Count; i++) {
                    if (i > 0) {
                        if (format.SpaceBeforeComma == true) {
                            res.Append(' ');
                        } else if (listWhiteSpace != null) {
                            res.Append(listWhiteSpace[i - 1]);
                        }
                        res.Append(',');
                    } else if (format.SpaceWithinCallParens != null) {
                        Args[i].AppendCodeString(res, ast, format, format.SpaceWithinCallParens.Value ? " " : string.Empty);
                        continue;
                    }

                    Args[i].AppendCodeString(res, ast, format, spaceAfterComma);
                }

                if (listWhiteSpace != null && listWhiteSpace.Length == Args.Count) {
                    // trailing comma
                    res.Append(listWhiteSpace[listWhiteSpace.Length - 1]);
                    res.Append(",");
                }
            }

            if (!this.IsMissingCloseGrouping(ast)) {
                if (Args.Count != 0 ||
                    format.SpaceWithinEmptyCallArgumentList == null ||
                    !string.IsNullOrWhiteSpace(this.GetSecondWhiteSpaceDefaultNull(ast))) {
                    format.Append(
                        res,
                        format.SpaceWithinCallParens,
                        " ",
                        string.Empty,
                        this.GetSecondWhiteSpaceDefaultNull(ast)
                    );
                }
                res.Append(')');
            }
        }

        /// <summary>
        /// Returns the index of the argument in Args at a given index.
        /// </summary>
        /// <returns>False if not within the call. -1 if it is in
        /// an argument not in Args.</returns>
        public bool GetArgumentAtIndex(PythonAst ast, int index, out int argIndex) {
            argIndex = -1;
            if (index <= Target.EndIndex || index > EndIndex) {
                return false;
            }
            if (Args == null) {
                return true;
            }

            for (var i = 0; i < Args.Count; ++i) {
                var a = Args[i];
                if (index <= a.EndIndexIncludingWhitespace) {
                    argIndex = i;
                    return true;
                }
            }

            if (index < EndIndex) {
                argIndex = -1;
                return true;
            }

            return false;
        }

        public override string GetLeadingWhiteSpace(PythonAst ast) => Target.GetLeadingWhiteSpace(ast);

        public override void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace) => Target.SetLeadingWhiteSpace(ast, whiteSpace);
    }
}

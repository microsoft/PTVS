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
    public class SliceExpression : Expression {
        public SliceExpression(Expression start, Expression stop, Expression step, bool stepProvided) {
            SliceStart = start;
            SliceStop = stop;
            SliceStep = step;
            StepProvided = stepProvided;
        }

        public Expression SliceStart { get; }

        public Expression SliceStop { get; }

        public Expression SliceStep { get; }

        /// <summary>
        /// True if the user provided a step parameter (either providing an explicit parameter
        /// or providing an empty step parameter) false if only start and stop were provided.
        /// </summary>
        public bool StepProvided { get; }

        public override string NodeName => "slice";

        public override IEnumerable<Node> GetChildNodes() {
            if (SliceStart != null) yield return SliceStart;
            if (SliceStop != null) yield return SliceStop;
            if (SliceStep != null) yield return SliceStep;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                SliceStart?.Walk(walker);
                SliceStop?.Walk(walker);
                SliceStep?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                if (SliceStart != null) {
                    await SliceStart.WalkAsync(walker, cancellationToken);
                }
                if (SliceStop != null) {
                    await SliceStop.WalkAsync(walker, cancellationToken);
                }
                if (SliceStep != null) {
                    await SliceStep.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            SliceStart?.AppendCodeString(res, ast, format);
            if (!this.IsIncompleteNode(ast)) {
                format.Append(res, format.SpaceBeforeSliceColon, " ", string.Empty, this.GetPreceedingWhiteSpaceDefaultNull(ast) ?? "");
                res.Append(':');
                if (SliceStop != null) {
                    string ws = null;
                    if (format.SpaceAfterSliceColon.HasValue) {
                        ws = string.Empty;
                        format.Append(res, format.SpaceAfterSliceColon, " ", string.Empty, string.Empty);
                    }
                    SliceStop.AppendCodeString(res, ast, format, ws);
                }
                if (StepProvided) {
                    format.Append(res, format.SpaceBeforeSliceColon, " ", string.Empty, this.GetSecondWhiteSpaceDefaultNull(ast) ?? "");
                    res.Append(':');
                    if (SliceStep != null) {
                        string ws = null;
                        if (format.SpaceAfterSliceColon.HasValue) {
                            ws = string.Empty;
                            format.Append(res, format.SpaceAfterSliceColon, " ", string.Empty, string.Empty);
                        }
                        SliceStep.AppendCodeString(res, ast, format, ws);
                    }
                }
            }
        }


        public override string GetLeadingWhiteSpace(PythonAst ast) 
            => SliceStart != null ? SliceStart.GetLeadingWhiteSpace(ast) : this.GetPreceedingWhiteSpace(ast);

        public override void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace) {
            if (SliceStart != null) {
                SliceStart.SetLeadingWhiteSpace(ast, whiteSpace);
            } else {
                base.SetLeadingWhiteSpace(ast, whiteSpace);
            }
        }
    }
}

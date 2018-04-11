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
    public class SliceExpression : Expression {
        private readonly Expression _sliceStart;
        private readonly Expression _sliceStop;
        private readonly Expression _sliceStep;
        private readonly bool _stepProvided;

        public SliceExpression(Expression start, Expression stop, Expression step, bool stepProvided) {
            _sliceStart = start;
            _sliceStop = stop;
            _sliceStep = step;
            _stepProvided = stepProvided;
        }

        public Expression SliceStart {
            get { return _sliceStart; }
        }

        public Expression SliceStop {
            get { return _sliceStop; }
        }

        public Expression SliceStep {
            get { return _sliceStep; }
        }

        /// <summary>
        /// True if the user provided a step parameter (either providing an explicit parameter
        /// or providing an empty step parameter) false if only start and stop were provided.
        /// </summary>
        public bool StepProvided {
            get {
                return _stepProvided;
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_sliceStart != null) {
                    _sliceStart.Walk(walker);
                }
                if (_sliceStop != null) {
                    _sliceStop.Walk(walker);
                }
                if (_sliceStep != null) {
                    _sliceStep.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            if (_sliceStart != null) {
                _sliceStart.AppendCodeString(res, ast, format);
            }
            if (!this.IsIncompleteNode(ast)) {
                format.Append(res, format.SpaceBeforeSliceColon, " ", "", this.GetPreceedingWhiteSpaceDefaultNull(ast) ?? "");
                res.Append(':');
                if (_sliceStop != null) {
                    string ws = null;
                    if (format.SpaceAfterSliceColon.HasValue) {
                        ws = "";
                        format.Append(res, format.SpaceAfterSliceColon, " ", "", "");
                    }
                    _sliceStop.AppendCodeString(res, ast, format, ws);
                }
                if (_stepProvided) {
                    format.Append(res, format.SpaceBeforeSliceColon, " ", "", this.GetSecondWhiteSpaceDefaultNull(ast) ?? "");
                    res.Append(':');
                    if (_sliceStep != null) {
                        string ws = null;
                        if (format.SpaceAfterSliceColon.HasValue) {
                            ws = "";
                            format.Append(res, format.SpaceAfterSliceColon, " ", "", "");
                        }
                        _sliceStep.AppendCodeString(res, ast, format, ws);
                    }
                }
            }
        }


        public override string GetLeadingWhiteSpace(PythonAst ast) {
            if (_sliceStart != null) {
                return _sliceStart.GetLeadingWhiteSpace(ast);
            }
            return this.GetPreceedingWhiteSpace(ast);
        }

        public override void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace) {
            if (_sliceStart != null) {
                _sliceStart.SetLeadingWhiteSpace(ast, whiteSpace);
            } else {
                base.SetLeadingWhiteSpace(ast, whiteSpace);
            }
        }

    }
}

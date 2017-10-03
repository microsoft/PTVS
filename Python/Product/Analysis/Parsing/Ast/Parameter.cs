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

using System;
using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {
    /// <summary>
    /// Parameter base class
    /// </summary>
    public class Parameter : Node {
        /// <summary>
        /// Position of the parameter: 0-based index
        /// </summary>
        private readonly string/*!*/ _name;
        internal readonly ParameterKind _kind;
        internal Expression _defaultValue, _annotation;

        internal static readonly object WhitespacePrecedingAssign = new object();

        public Parameter(string name, ParameterKind kind) {
            _name = name ?? "";
            _kind = kind;
        }

        public override string NodeName {
            get {
                return "parameter name";
            }
        }

        /// <summary>
        /// Parameter name
        /// </summary>
        public string/*!*/ Name {
            get { return _name; }
        }

        public Expression DefaultValue {
            get { return _defaultValue; }
            set { _defaultValue = value; }
        }

        public Expression Annotation {
            get {
                return _annotation;
            }
            set {
                _annotation = value;
            }
        }

        public bool IsList {
            get {
                return _kind == ParameterKind.List;
            }
        }

        public bool IsDictionary {
            get {
                return _kind == ParameterKind.Dictionary;
            }
        }

        public bool IsKeywordOnly {
            get {
                return _kind == ParameterKind.KeywordOnly;
            }
        }

        internal ParameterKind Kind {
            get {
                return _kind;
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_annotation != null) {
                    _annotation.Walk(walker);
                }
                if (_defaultValue != null) {
                    _defaultValue.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        public PythonVariable GetVariable(PythonAst ast) {
            object reference;
            if (ast.TryGetAttribute(this, NodeAttributes.Variable, out reference)) {
                return (PythonVariable)reference;
            }
            return null;
        }

        public void AddPreceedingWhiteSpace(PythonAst ast, string whiteSpace) {
            ast.SetAttribute(this, NodeAttributes.PreceedingWhiteSpace, whiteSpace);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            AppendCodeString(res, ast, format, null);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format, string leadingWhiteSpace) {
            string kwOnlyText = this.GetExtraVerbatimText(ast);
            if (kwOnlyText != null) {
                if (leadingWhiteSpace != null) {
                    res.Append(leadingWhiteSpace);
                    res.Append(kwOnlyText.TrimStart());
                    leadingWhiteSpace = null;
                } else {
                    res.Append(kwOnlyText);
                }
            }
            switch (Kind) {
                case ParameterKind.Dictionary:
                    res.Append(leadingWhiteSpace ?? this.GetPreceedingWhiteSpace(ast));
                    res.Append("**");
                    res.Append(this.GetSecondWhiteSpace(ast));
                    res.Append(this.GetVerbatimImage(ast) ?? _name);
                    AppendAnnotation(res, ast, format);
                    break;
                case ParameterKind.List:
                    res.Append(leadingWhiteSpace ?? this.GetPreceedingWhiteSpace(ast));
                    res.Append('*');
                    res.Append(this.GetSecondWhiteSpace(ast));
                    res.Append(this.GetVerbatimImage(ast) ?? _name);
                    AppendAnnotation(res, ast, format);
                    break;
                case ParameterKind.Normal:
                    if (this.IsAltForm(ast)) {
                        res.Append(leadingWhiteSpace ?? this.GetPreceedingWhiteSpace(ast));
                        res.Append('(');
                        res.Append(this.GetThirdWhiteSpace(ast));
                        res.Append(this.GetVerbatimImage(ast) ?? _name);
                        if (!this.IsMissingCloseGrouping(ast)) {
                            res.Append(this.GetSecondWhiteSpace(ast));
                            res.Append(')');
                        }
                    } else {
                        res.Append(leadingWhiteSpace ?? this.GetPreceedingWhiteSpaceDefaultNull(ast));
                        res.Append(this.GetVerbatimImage(ast) ?? _name);
                        AppendAnnotation(res, ast, format);
                    }
                    break;
                case ParameterKind.KeywordOnly:
                    res.Append(leadingWhiteSpace ?? this.GetPreceedingWhiteSpace(ast));
                    res.Append(this.GetVerbatimImage(ast) ?? _name);
                    AppendAnnotation(res, ast, format);
                    break;
                default: throw new InvalidOperationException();
            }

            if (_defaultValue != null) {
                format.Append(
                    res,
                    format.SpaceAroundDefaultValueEquals,
                    " ",
                    "",
                    NodeAttributes.GetWhiteSpace(this, ast, WhitespacePrecedingAssign)
                );

                res.Append('=');
                if (format.SpaceAroundDefaultValueEquals != null) {
                    _defaultValue.AppendCodeString(res, ast, format, format.SpaceAroundDefaultValueEquals.Value ? " " : "");
                } else {
                    _defaultValue.AppendCodeString(res, ast, format);
                }
            }
        }

        private void AppendAnnotation(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            if (_annotation != null) {
                res.Append(this.GetThirdWhiteSpace(ast));
                res.Append(':');
                _annotation.AppendCodeString(res, ast, format);
            }
        }
    }

}

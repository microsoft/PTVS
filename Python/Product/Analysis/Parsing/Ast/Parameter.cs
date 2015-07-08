/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

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
                    res.Append(leadingWhiteSpace ?? this.GetProceedingWhiteSpace(ast));
                    res.Append("**");
                    res.Append(this.GetSecondWhiteSpace(ast));
                    res.Append(this.GetVerbatimImage(ast) ?? _name);
                    AppendAnnotation(res, ast, format);
                    break;
                case ParameterKind.List:
                    res.Append(leadingWhiteSpace ?? this.GetProceedingWhiteSpace(ast));
                    res.Append('*');
                    res.Append(this.GetSecondWhiteSpace(ast));
                    res.Append(this.GetVerbatimImage(ast) ?? _name);
                    AppendAnnotation(res, ast, format);
                    break;
                case ParameterKind.Normal:
                    if (this.IsAltForm(ast)) {
                        res.Append(leadingWhiteSpace ?? this.GetProceedingWhiteSpace(ast));
                        res.Append('(');
                        res.Append(this.GetThirdWhiteSpace(ast));
                        res.Append(this.GetVerbatimImage(ast) ?? _name);
                        if (!this.IsMissingCloseGrouping(ast)) {
                            res.Append(this.GetSecondWhiteSpace(ast));
                            res.Append(')');
                        }
                    } else {
                        res.Append(leadingWhiteSpace ?? this.GetProceedingWhiteSpaceDefaultNull(ast));
                        res.Append(this.GetVerbatimImage(ast) ?? _name);
                        AppendAnnotation(res, ast, format);
                    }
                    break;
                case ParameterKind.KeywordOnly:
                    res.Append(leadingWhiteSpace ?? this.GetProceedingWhiteSpace(ast));
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
                    this.GetSecondWhiteSpace(ast)
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

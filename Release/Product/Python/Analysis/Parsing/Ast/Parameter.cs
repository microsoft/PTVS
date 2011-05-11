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
#if NAME_BINDING
        private PythonVariable _variable;
#endif
        public Parameter(string name, ParameterKind kind) {
            _name = name ?? "";
            _kind = kind;
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

#if NAME_BINDING
        internal PythonVariable PythonVariable {
            get { return _variable; }
            set { _variable = value; }
        }
#endif

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_defaultValue != null) {
                    _defaultValue.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast) {
            switch (Kind) {
                case ParameterKind.Dictionary:
                    res.Append(this.GetProceedingWhiteSpace(ast));
                    res.Append("**");
                    res.Append(this.GetSecondWhiteSpace(ast));
                    res.Append(_name);
                    AppendAnnotation(res, ast);
                    break;
                case ParameterKind.List:
                    res.Append(this.GetProceedingWhiteSpace(ast));
                    res.Append('*');
                    res.Append(this.GetSecondWhiteSpace(ast));
                    res.Append(_name);
                    AppendAnnotation(res, ast);
                    break;
                case ParameterKind.Normal:
                    if (this.IsAltForm(ast)) {
                        res.Append(this.GetProceedingWhiteSpace(ast));
                        res.Append('(');
                        res.Append(this.GetThirdWhiteSpace(ast));
                        res.Append(_name);
                        res.Append(this.GetSecondWhiteSpace(ast));
                        res.Append(')');
                    } else {
                        res.Append(this.GetProceedingWhiteSpace(ast));
                        res.Append(_name);
                        AppendAnnotation(res, ast);
                    }
                    break;
                case ParameterKind.KeywordOnly:
                    res.Append(this.GetExtraVerbatimText(ast));
                    res.Append(this.GetProceedingWhiteSpace(ast));
                    res.Append(_name);
                    AppendAnnotation(res, ast);
                    break;
                default: throw new InvalidOperationException();
            }

            if (_defaultValue != null) {
                res.Append(this.GetSecondWhiteSpace(ast));
                res.Append('=');
                _defaultValue.AppendCodeString(res, ast);
            }
        }

        private void AppendAnnotation(StringBuilder res, PythonAst ast) {
            if (_annotation != null) {
                res.Append(this.GetThirdWhiteSpace(ast));
                res.Append(':');
                _annotation.AppendCodeString(res, ast);
            }
        }
    }

}

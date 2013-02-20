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

using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {
    class ErrorParameter : Parameter {
        private readonly ErrorExpression _error;
        
        public ErrorParameter(ErrorExpression errorValue)
            : base("", ParameterKind.Normal) {
                _error = errorValue;
        }

        public ErrorExpression Error {
            get {
                return _error;
            }
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
            bool isAltForm = this.IsAltForm(ast);
            if (isAltForm) {
                res.Append(leadingWhiteSpace ?? this.GetProceedingWhiteSpace(ast));
                res.Append('(');
                leadingWhiteSpace = null;
            }
            _error.AppendCodeString(res, ast, format, leadingWhiteSpace);
            if (this.DefaultValue != null) {
                res.Append(this.GetSecondWhiteSpace(ast));
                res.Append('=');
                this.DefaultValue.AppendCodeString(res, ast, format);
            }
            if (isAltForm && !this.IsMissingCloseGrouping(ast)) {
                res.Append(this.GetSecondWhiteSpace(ast));
                res.Append(')');
            }
        }
    }
}

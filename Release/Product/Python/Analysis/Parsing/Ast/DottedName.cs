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
using System.Collections.Generic;
using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {
    public class DottedName : Node {
        private readonly NameExpression[] _names;

        public DottedName(NameExpression[] names) {
            _names = names;
        }

        public IList<NameExpression> Names {
            get { return _names; }
        }

        public virtual string MakeString() {
            if (_names.Length == 0) return String.Empty;

            StringBuilder ret = new StringBuilder(_names[0].Name);
            for (int i = 1; i < _names.Length; i++) {
                ret.Append('.');
                ret.Append(_names[i].Name);
            }
            return ret.ToString();
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                ;
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            var whitespace = this.GetNamesWhiteSpace(ast);
            
            for (int i = 0, whitespaceIndex = 0; i < _names.Length; i++) {
                if (whitespace != null) {
                    res.Append(whitespace[whitespaceIndex++]);
                }
                if (i != 0) {
                    res.Append('.');
                    if (whitespace != null) {
                        res.Append(whitespace[whitespaceIndex++]);
                    }
                }
                _names[i].AppendCodeString(res, ast, format);
            }
        }


        public override string GetLeadingWhiteSpace(PythonAst ast) {
            var whitespace = this.GetNamesWhiteSpace(ast);
            if (whitespace != null && whitespace.Length > 0) {
                return whitespace[0];
            }
            return null;
        }

    }
}

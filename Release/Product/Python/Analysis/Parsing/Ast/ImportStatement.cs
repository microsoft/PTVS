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

using System.Collections.Generic;
using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {

    public class ImportStatement : Statement {
        private readonly ModuleName[] _names;
        private readonly string[] _asNames;
        private readonly bool _forceAbsolute;

        private PythonVariable[] _variables;

        public ImportStatement(ModuleName[] names, string[] asNames, bool forceAbsolute) {
            _names = names;
            _asNames = asNames;
            _forceAbsolute = forceAbsolute;
        }

        public PythonVariable[] Variables {
            get { return _variables; }
            set { _variables = value; }
        }

        public PythonReference[] GetReferences(PythonAst ast) {
            return GetVariableReferences(this, ast);
        }

        public IList<DottedName> Names {
            get { return _names; }
        }

        public IList<string> AsNames {
            get { return _asNames; }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast) {
            res.Append(this.GetProceedingWhiteSpace(ast));
            res.Append("import");

            var itemWhiteSpace = this.GetListWhiteSpace(ast);
            var asNameWhiteSpace = this.GetNamesWhiteSpace(ast);
            var verbatimNames = this.GetVerbatimNames(ast);
            for (int i = 0, asIndex = 0; i < _names.Length; i++) {                
                if (i > 0 && itemWhiteSpace != null) {
                    res.Append(itemWhiteSpace[i - 1]);
                    res.Append(',');
                }

                _names[i].AppendCodeString(res, ast);
                if (AsNames[i] != null) {
                    if (asNameWhiteSpace != null) {
                        res.Append(asNameWhiteSpace[asIndex++]);
                    }
                    res.Append("as");
                    if (AsNames[i].Length != 0) {
                        if (asNameWhiteSpace != null) {
                            res.Append(asNameWhiteSpace[asIndex++]);
                        }

                        res.Append(verbatimNames != null ? (verbatimNames[i] ?? _asNames[i]) : _asNames[i]);
                    }
                }
            }

        }
    }
}

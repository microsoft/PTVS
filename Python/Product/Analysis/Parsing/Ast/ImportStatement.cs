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

    public class ImportStatement : Statement {
        private readonly ModuleName[] _names;
        private readonly NameExpression[] _asNames;
        private readonly bool _forceAbsolute;

        private PythonVariable[] _variables;

        public ImportStatement(ModuleName[] names, NameExpression[] asNames, bool forceAbsolute) {
            _names = names;
            _asNames = asNames;
            _forceAbsolute = forceAbsolute;
        }

        public bool ForceAbsolute {
            get {
                return _forceAbsolute;
            }
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

        public IList<NameExpression> AsNames {
            get { return _asNames; }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
            }
            walker.PostWalk(this);
        }

        /// <summary>
        /// Removes the import at the specified index (which must be in the range of
        /// the Names property) and returns a new ImportStatement which is the same
        /// as this one minus the imported name.  Preserves all round-tripping metadata
        /// in the process.
        /// 
        /// New in 1.1.
        /// </summary>
        public ImportStatement RemoveImport(PythonAst ast, int index) {
            if (index < 0 || index >= _names.Length) {
                throw new ArgumentOutOfRangeException("index");
            }
            if (ast == null) {
                throw new ArgumentNullException("ast");
            }

            ModuleName[] names = new ModuleName[_names.Length - 1];
            NameExpression[] asNames = _asNames == null ? null : new NameExpression[_asNames.Length - 1];
            var asNameWhiteSpace = this.GetNamesWhiteSpace(ast);
            var itemWhiteSpace = this.GetListWhiteSpace(ast);
            List<string> newAsNameWhiteSpace = new List<string>();
            List<string> newListWhiteSpace = new List<string>();
            int asIndex = 0;
            for (int i = 0, write = 0; i < _names.Length; i++) {
                bool includingCurrentName = i != index;

                // track the white space, this needs to be kept in sync w/ ToCodeString and how the
                // parser creates the white space.
                if (i > 0 && itemWhiteSpace != null) {
                    if (includingCurrentName) {
                        newListWhiteSpace.Add(itemWhiteSpace[i - 1]);
                    }
                }

                if (includingCurrentName) {
                    names[write] = _names[i];

                    if (_asNames != null) {
                        asNames[write] = _asNames[i];
                    }

                    write++;
                }

                if (AsNames[i] != null && includingCurrentName) {
                    if (asNameWhiteSpace != null) {
                        newAsNameWhiteSpace.Add(asNameWhiteSpace[asIndex++]);
                    }

                    if (_asNames[i].Name.Length != 0) {
                        if (asNameWhiteSpace != null) {
                            newAsNameWhiteSpace.Add(asNameWhiteSpace[asIndex++]);
                        }
                    }
                }
            }

            var res = new ImportStatement(names, asNames, _forceAbsolute);
            ast.CopyAttributes(this, res);
            ast.SetAttribute(res, NodeAttributes.NamesWhiteSpace, newAsNameWhiteSpace.ToArray());
            ast.SetAttribute(res, NodeAttributes.ListWhiteSpace, newListWhiteSpace.ToArray());

            return res;
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            var asNameWhiteSpace = this.GetNamesWhiteSpace(ast);
            if (format.ReplaceMultipleImportsWithMultipleStatements) {
                var proceeding = this.GetProceedingWhiteSpace(ast);
                var additionalProceeding = format.GetNextLineProceedingText(proceeding);
                
                for (int i = 0, asIndex = 0; i < _names.Length; i++) {
                    if (i == 0) {
                        format.ReflowComment(res, proceeding) ;
                    } else {
                        res.Append(additionalProceeding);
                    }
                    res.Append("import");

                    _names[i].AppendCodeString(res, ast, format);
                    AppendAs(res, ast, format, asNameWhiteSpace, i, ref asIndex);
                }
                return;
            } else {
                format.ReflowComment(res, this.GetProceedingWhiteSpace(ast));
                res.Append("import");

                var itemWhiteSpace = this.GetListWhiteSpace(ast);
                for (int i = 0, asIndex = 0; i < _names.Length; i++) {
                    if (i > 0 && itemWhiteSpace != null) {
                        res.Append(itemWhiteSpace[i - 1]);
                        res.Append(',');
                    }

                    _names[i].AppendCodeString(res, ast, format);
                    AppendAs(res, ast, format, asNameWhiteSpace, i, ref asIndex);
                }
            }
        }

        private void AppendAs(StringBuilder res, PythonAst ast, CodeFormattingOptions format, string[] asNameWhiteSpace, int i, ref int asIndex) {
            if (AsNames[i] != null) {
                if (asNameWhiteSpace != null) {
                    res.Append(asNameWhiteSpace[asIndex++]);
                }
                res.Append("as");

                if (_asNames[i].Name.Length != 0) {
                    if (asNameWhiteSpace != null) {
                        res.Append(asNameWhiteSpace[asIndex++]);
                    }

                    _asNames[i].AppendCodeString(res, ast, format);
                }
            }
        }
    }
}

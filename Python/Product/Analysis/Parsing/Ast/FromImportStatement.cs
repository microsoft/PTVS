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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {

    public class FromImportStatement : Statement {
        private static readonly string[] _star = new[] { "*" };
        private PythonVariable[] _variables;

        public FromImportStatement(ModuleName root, NameExpression/*!*/[] names, NameExpression[] asNames, bool fromFuture, bool forceAbsolute, int importIndex) {
            Root = root;
            Names = names;
            AsNames = asNames;
            IsFromFuture = fromFuture;
            ForceAbsolute = forceAbsolute;
            ImportIndex = importIndex;
        }

        public ModuleName Root { get; }
        public bool IsFromFuture { get; }
        public bool ForceAbsolute { get; }
        public IList<NameExpression/*!*/> Names { get; }
        public IList<NameExpression> AsNames { get; }
        public int ImportIndex { get; }

        public override int KeywordLength => 4;

        /// <summary>
        /// Gets the variables associated with each imported name.
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays",
            Justification = "breaking change")]
        public PythonVariable[] Variables {
            get { return _variables; }
            set { _variables = value; }
        }

        public PythonReference[] GetReferences(PythonAst ast) {
            return GetVariableReferences(this, ast);
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
            }
            walker.PostWalk(this);
        }

        /// <summary>
        /// Returns a new FromImport statement that is identical to this one but has
        /// removed the specified import statement.  Otherwise preserves any attributes
        /// for the statement.
        /// 
        /// New in 1.1.
        /// <param name="ast">The parent AST whose attributes should be updated for the new node.</param>
        /// <param name="index">The index in Names of the import to be removed.</param>
        /// </summary>
        public FromImportStatement RemoveImport(PythonAst ast, int index) {
            if (index < 0 || index >= Names.Count) {
                throw new ArgumentOutOfRangeException("index");
            }
            if (ast == null) {
                throw new ArgumentNullException("ast");
            }

            NameExpression[] names = new NameExpression[Names.Count - 1];
            NameExpression[] asNames = AsNames == null ? null : new NameExpression[AsNames.Count - 1];
            var asNameWhiteSpace = this.GetNamesWhiteSpace(ast);
            List<string> newAsNameWhiteSpace = new List<string>();
            int importIndex = ImportIndex;
            int asIndex = 0;
            for (int i = 0, write = 0; i < Names.Count; i++) {
                bool includingCurrentName = i != index;

                // track the white space, this needs to be kept in sync w/ ToCodeString and how the
                // parser creates the white space.

                if (asNameWhiteSpace != null && asIndex < asNameWhiteSpace.Length) {
                    if (write > 0) {
                        if (includingCurrentName) {
                            newAsNameWhiteSpace.Add(asNameWhiteSpace[asIndex++]);
                        } else {
                            asIndex++;
                        }
                    } else if (i > 0) {
                        asIndex++;
                    }
                }

                if (asNameWhiteSpace != null && asIndex < asNameWhiteSpace.Length) {
                    if (includingCurrentName) {
                        if (newAsNameWhiteSpace.Count == 0) {
                            // no matter what we want the 1st entry to have the whitespace after the import keyword
                            newAsNameWhiteSpace.Add(asNameWhiteSpace[0]);
                            asIndex++;
                        } else {
                            newAsNameWhiteSpace.Add(asNameWhiteSpace[asIndex++]);
                        }
                    } else {
                        asIndex++;
                    }
                }

                if (includingCurrentName) {
                    names[write] = Names[i];

                    if (AsNames != null) {
                        asNames[write] = AsNames[i];
                    }

                    write++;
                }

                if (AsNames != null && AsNames[i] != null) {
                    if (asNameWhiteSpace != null && asIndex < asNameWhiteSpace.Length) {
                        if (i != index) {
                            newAsNameWhiteSpace.Add(asNameWhiteSpace[asIndex++]);
                        } else {
                            asIndex++;
                        }
                    }

                    if (AsNames[i].Name.Length != 0) {
                        if (asNameWhiteSpace != null && asIndex < asNameWhiteSpace.Length) {
                            if (i != index) {
                                newAsNameWhiteSpace.Add(asNameWhiteSpace[asIndex++]);
                            } else {
                                asIndex++;
                            }
                        }
                    } else {
                        asIndex++;
                    }
                }
            }

            if (asNameWhiteSpace != null && asIndex < asNameWhiteSpace.Length) {
                // trailing comma
                newAsNameWhiteSpace.Add(asNameWhiteSpace[asNameWhiteSpace.Length - 1]);
            }

            var res = new FromImportStatement(Root, names, asNames, IsFromFuture, ForceAbsolute, importIndex);
            ast.CopyAttributes(this, res);
            ast.SetAttribute(res, NodeAttributes.NamesWhiteSpace, newAsNameWhiteSpace.ToArray());

            return res;
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            format.ReflowComment(res, this.GetPreceedingWhiteSpace(ast));
            res.Append("from");
            Root.AppendCodeString(res, ast, format);

            if (!this.IsIncompleteNode(ast)) {
                res.Append(this.GetSecondWhiteSpace(ast));
                res.Append("import");
                if (!this.IsAltForm(ast)) {
                    res.Append(this.GetThirdWhiteSpace(ast));
                    res.Append('(');
                }

                var asNameWhiteSpace = this.GetNamesWhiteSpace(ast);
                int asIndex = 0;
                for (int i = 0; i < Names.Count; i++) {
                    if (i > 0) {
                        if (asNameWhiteSpace != null && asIndex < asNameWhiteSpace.Length) {
                            res.Append(asNameWhiteSpace[asIndex++]);
                        }
                        res.Append(',');
                    }

                    if (asNameWhiteSpace != null && asIndex < asNameWhiteSpace.Length) {
                        res.Append(asNameWhiteSpace[asIndex++]);
                    } else {
                        res.Append(' ');
                    }

                    Names[i].AppendCodeString(res, ast, format);
                    if (AsNames != null && AsNames[i] != null) {
                        if (asNameWhiteSpace != null && asIndex < asNameWhiteSpace.Length) {
                            res.Append(asNameWhiteSpace[asIndex++]);
                        }
                        res.Append("as");
                        if (AsNames[i].Name.Length != 0) {
                            if (asNameWhiteSpace != null && asIndex < asNameWhiteSpace.Length) {
                                res.Append(asNameWhiteSpace[asIndex++]);
                            }
                            AsNames[i].AppendCodeString(res, ast, format);
                        } else {
                            asIndex++;
                        }
                    }
                }

                if (asNameWhiteSpace != null && asIndex < asNameWhiteSpace.Length) {
                    // trailing comma
                    res.Append(asNameWhiteSpace[asNameWhiteSpace.Length - 1]);
                    res.Append(",");
                }

                if (!this.IsAltForm(ast) && !this.IsMissingCloseGrouping(ast)) {
                    res.Append(this.GetFourthWhiteSpace(ast));
                    res.Append(')');
                }
            }
        }
    }
}

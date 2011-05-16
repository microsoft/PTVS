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
    public class ClassDefinition : ScopeStatement {
        private int _headerIndex;
        private readonly string/*!*/ _name;
        private Statement _body;
        private readonly Arg[] _bases;
        private DecoratorStatement _decorators;

        private PythonVariable _variable;           // Variable corresponding to the class name
        private PythonVariable _modVariable;        // Variable for the the __module__ (module name)
        private PythonVariable _docVariable;        // Variable for the __doc__ attribute
        private PythonVariable _modNameVariable;    // Variable for the module's __name__

        public ClassDefinition(string name, Arg[] bases, Statement body) {           
            _name = name ?? "";
            _bases = bases;
            _body = body;
        }

        public int HeaderIndex {
            get { return _headerIndex; }
            set { _headerIndex = value; }
        }

        public override string/*!*/ Name {
            get { return _name; }
        }

        public IList<Arg> Bases {
            get { return _bases; }
        }

        public Statement Body {
            get { return _body; }
        }

        public DecoratorStatement Decorators {
            get {
                return _decorators;
            }
            internal set {
                _decorators = value;
            }
        }

        internal PythonVariable PythonVariable {
            get { return _variable; }
            set { _variable = value; }
        }

        internal PythonVariable ModVariable {
            get { return _modVariable; }
            set { _modVariable = value; }
        }

        internal PythonVariable DocVariable {
            get { return _docVariable; }
            set { _docVariable = value; }
        }

        internal PythonVariable ModuleNameVariable {
            get { return _modNameVariable; }
            set { _modNameVariable = value; }
        }

        internal override bool HasLateBoundVariableSets {
            get {
                return base.HasLateBoundVariableSets || NeedsLocalsDictionary;
            }
            set {
                base.HasLateBoundVariableSets = value;
            }
        }
        
        internal override bool NeedsLocalContext {
            get {
                return true;
            }
        }

        internal override bool ExposesLocalVariable(PythonVariable variable) {
            return true;
        }

        internal override PythonVariable BindReference(PythonNameBinder binder, PythonReference reference) {
            PythonVariable variable;

            // Python semantics: The variables bound local in the class
            // scope are accessed by name - the dictionary behavior of classes
            if (TryGetVariable(reference.Name, out variable)) {
                // TODO: This results in doing a dictionary lookup to get/set the local,
                // when it should probably be an uninitialized check / global lookup for gets
                // and a direct set
                if (variable.Kind == VariableKind.Global) {
                    AddReferencedGlobal(reference.Name);
                } else if (variable.Kind == VariableKind.Local) {
                    return null;
                }

                return variable;
            }

            // Try to bind in outer scopes, if we have an unqualified exec we need to leave the
            // variables as free for the same reason that locals are accessed by name.
            for (ScopeStatement parent = Parent; parent != null; parent = parent.Parent) {
                if (parent.TryBindOuter(this, reference.Name, true, out variable)) {
                    return variable;
                }
            }

            return null;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_decorators != null) {
                    _decorators.Walk(walker);
                }
                if (_bases != null) {
                    foreach (var b in _bases) {
                        b.Walk(walker);
                    }
                }
                if (_body != null) {
                    _body.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        public SourceLocation Header {
            get { return GlobalParent.IndexToLocation(_headerIndex); }
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast) {
            if (Decorators != null) {
                Decorators.AppendCodeString(res, ast);
            }

            res.Append(this.GetProceedingWhiteSpace(ast));
            res.Append("class");
            res.Append(this.GetSecondWhiteSpace(ast));
            res.Append(this.GetVerbatimImage(ast) ?? Name);

            if (!this.IsAltForm(ast)) {
                res.Append(this.GetThirdWhiteSpace(ast));
                res.Append('(');
            }

            ListExpression.AppendItems(
                res,
                ast,
                "",
                "",
                this,
                this.Bases.Count,
                (i, sb) => this.Bases[i].AppendCodeString(sb, ast)
            );
            
            if (!this.IsAltForm(ast) && !this.IsMissingCloseGrouping(ast)) {
                res.Append(this.GetFourthWhiteSpace(ast));
                res.Append(')');
            }

            _body.AppendCodeString(res, ast);
        }
    }
}

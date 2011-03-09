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

namespace Microsoft.PythonTools.Parsing.Ast {
    public class ClassDefinition : ScopeStatement {
        private int _headerIndex;
        private readonly string _name;
        private Statement _body;
        private readonly Expression[] _bases;
        private readonly Dictionary<string, Expression> _kwArgs;
        private IList<Expression> _decorators;

        private PythonVariable _variable;           // Variable corresponding to the class name
        private PythonVariable _modVariable;        // Variable for the the __module__ (module name)
        private PythonVariable _docVariable;        // Variable for the __doc__ attribute
        private PythonVariable _modNameVariable;    // Variable for the module's __name__

        public ClassDefinition(string name, Expression[] bases, Statement body, Dictionary<string, Expression> kwArgs) {           
            _name = name;
            _bases = bases;
            _body = body;
            _kwArgs = kwArgs;
        }

        public int HeaderIndex {
            get { return _headerIndex; }
            set { _headerIndex = value; }
        }

        public override string Name {
            get { return _name; }
        }

        public Dictionary<string, Expression> KeywordArgs {
            get {
                return _kwArgs;
            }
        }

        public IList<Expression> Bases {
            get { return _bases; }
        }

        public Statement Body {
            get { return _body; }
        }

        public IList<Expression> Decorators {
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

        internal override string ScopeDocumentation {
            get {
                return GetDocumentation(_body);
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_decorators != null) {
                    foreach (Expression decorator in _decorators) {
                        decorator.Walk(walker);
                    }
                }
                if (_bases != null) {
                    foreach (Expression b in _bases) {
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
    }
}

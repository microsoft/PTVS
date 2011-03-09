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

namespace Microsoft.PythonTools.Parsing.Ast {

    /// <summary>
    /// Top-level ast for all Python code.  Holds onto the body and the line mapping information.
    /// </summary>
    public sealed class PythonAst : ScopeStatement {
        private Statement _body;
        internal int[] _lineLocations;

        /// <summary>
        /// Creates a new PythonAst without a body.  ParsingFinished should be called afterwards to set
        /// the body.
        /// </summary>
        internal PythonAst() {
        }

        public PythonAst(Statement body, int[] lineLocations) {
            ParsingFinished(lineLocations, body);
        }

        /// <summary>
        /// Called when parsing is complete, the body is built, the line mapping and language features are known.
        /// 
        /// This is used in conjunction with the constructor which does not take a body.  It enables creating
        /// the outer most PythonAst first so that nodes can always have a global parent.  This lets an un-bound
        /// tree to still provide it's line information immediately after parsing.  When we set the location
        /// of each node during construction we also set the global parent.  When we name bind the global 
        /// parent gets replaced with the real parent ScopeStatement.
        /// </summary>
        /// <param name="lineLocations">a mapping of where each line begins</param>
        /// <param name="body">The body of code</param>
        /// <param name="languageFeatures">The language features which were set during parsing.</param>
        internal void ParsingFinished(int[] lineLocations, Statement body) {
            if (_body != null) {
                throw new InvalidOperationException("cannot set body twice");
            }

            _body = body;
            _lineLocations = lineLocations;
        }

        public override string Name {
            get {
                return "<module>";
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_body != null) {
                    _body.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        public Statement Body {
            get { return _body; }
        }

        internal SourceLocation IndexToLocation(int index) {
            if (index == -1) {
                return SourceLocation.Invalid;
            }

            var locs = GlobalParent._lineLocations;
            int match = Array.BinarySearch(locs, index);
            if (match < 0) {
                // If our index = -1, it means we're on the first line.
                if (match == -1) {
                    return new SourceLocation(index, 1, index + 1);
                }

                // If we couldn't find an exact match for this line number, get the nearest
                // matching line number less than this one
                match = ~match - 1;
            }
            return new SourceLocation(index, match + 2, index - locs[match] + 1);
        }

        #region Name Binding Support

        internal override bool ExposesLocalVariable(PythonVariable variable) {
            return true;
        }

        internal override void FinishBind(PythonNameBinder binder) {
        }

        internal override PythonVariable BindReference(PythonNameBinder binder, PythonReference reference) {
            return EnsureVariable(reference.Name);
        }

        internal override bool TryBindOuter(ScopeStatement from, string name, bool allowGlobals, out PythonVariable variable) {
            if (allowGlobals) {
                // Unbound variable
                from.AddReferencedGlobal(name);

                if (from.HasLateBoundVariableSets) {
                    // If the context contains unqualified exec, new locals can be introduced
                    // Therefore we need to turn this into a fully late-bound lookup which
                    // happens when we don't have a PythonVariable.
                    variable = null;
                    return false;
                } else {
                    // Create a global variable to bind to.
                    variable = EnsureGlobalVariable(name);
                    return true;
                }
            }
            variable = null;
            return false;
        }

        internal override bool IsGlobal {
            get { return true; }
        }

        /// <summary>
        /// Creates a variable at the global level.  Called for known globals (e.g. __name__),
        /// for variables explicitly declared global by the user, and names accessed
        /// but not defined in the lexical scope.
        /// </summary>
        internal PythonVariable/*!*/ EnsureGlobalVariable(string name) {
            PythonVariable variable;
            if (!TryGetVariable(name, out variable)) {
                variable = CreateVariable(name, VariableKind.Global);
            }

            return variable;
        }


        internal PythonVariable/*!*/ EnsureNonlocalVariable(string name) {
            PythonVariable variable;
            if (!TryGetVariable(name, out variable)) {
                variable = CreateVariable(name, VariableKind.Nonlocal);
            }

            return variable;
        }

        #endregion
    }
}



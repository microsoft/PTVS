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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Common.Core.Collections;
using Microsoft.PythonTools.Common.Core.Text;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public class ClassDefinition : ScopeStatement {
        private readonly NameExpression/*!*/ _name;
        private readonly Statement _body;
        private DecoratorStatement _decorators;

        public ClassDefinition(NameExpression/*!*/ name, ImmutableArray<Arg> bases, Statement body) {
            _name = name;
            Bases = bases;
            _body = body;
        }

        public override int KeywordLength => 5;

        public int HeaderIndex { get; set; }

        public override string/*!*/ Name => _name.Name ?? "";

        public NameExpression/*!*/ NameExpression => _name;

        public ImmutableArray<Arg> Bases { get; }

        public override Statement Body => _body;

        public DecoratorStatement Decorators {
            get => _decorators;
            internal set => _decorators = value;
        }

        /// <summary>
        /// Gets the variable that this class definition was assigned to.
        /// </summary>
        public PythonVariable Variable { get; set; }

        /// <summary>
        /// Gets the variable reference for the specific assignment to the variable for this class definition.
        /// </summary>
        public PythonReference GetVariableReference(PythonAst ast) => GetVariableReference(this, ast);

        /// <summary>
        /// Variable for the classes __class__ cell var on 3.x
        /// </summary>
        internal PythonVariable ClassVariable { get; set; }

        /// <summary>
        /// Variable for the the __module__ (module name)
        /// </summary>
        internal PythonVariable ModVariable { get; set; }

        /// <summary>
        /// Variable for the __doc__ attribute
        /// </summary>
        internal PythonVariable DocVariable { get; set; }

        /// <summary>
        /// Variable for the module's __name__
        /// </summary>
        internal PythonVariable ModuleNameVariable { get; set; }

        internal override bool HasLateBoundVariableSets {
            get => base.HasLateBoundVariableSets || NeedsLocalsDictionary;
            set => base.HasLateBoundVariableSets = value;
        }

        internal override bool ExposesLocalVariable(PythonVariable variable) => true;

        internal override bool TryBindOuter(ScopeStatement from, string name, bool allowGlobals, out PythonVariable variable) {
            if (name == "__class__" && ClassVariable != null) {
                // 3.x has a cell var called __class__ which can be bound by inner scopes
                variable = ClassVariable;
                return true;
            }

            return base.TryBindOuter(from, name, allowGlobals, out variable);
        }

        internal override PythonVariable BindReference(PythonNameBinder binder, string name) {

            // Python semantics: The variables bound local in the class
            // scope are accessed by name - the dictionary behavior of classes
            if (TryGetVariable(name, out var variable)) {
                // TODO: This results in doing a dictionary lookup to get/set the local,
                // when it should probably be an uninitialized check / global lookup for gets
                // and a direct set
                if (variable.Kind == VariableKind.Global) {
                    AddReferencedGlobal(name);
                } else if (variable.Kind == VariableKind.Local) {
                    return null;
                }

                return variable;
            }

            // Try to bind in outer scopes, if we have an unqualified exec we need to leave the
            // variables as free for the same reason that locals are accessed by name.
            for (var parent = Parent; parent != null; parent = parent.Parent) {
                if (parent.TryBindOuter(this, name, true, out variable)) {
                    return variable;
                }
            }

            return null;
        }

        public override IEnumerable<Node> GetChildNodes() {
            if (_name != null) yield return _name;
            if (_decorators != null) yield return _decorators;
            foreach (var b in Bases) {
                yield return b;
            }
            if (_body != null) yield return _body;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                _name?.Walk(walker);
                _decorators?.Walk(walker);
                foreach (var b in Bases) {
                    b.Walk(walker);
                }
                _body?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                if (_name != null) {
                    await _name.WalkAsync(walker, cancellationToken);
                }

                if (_decorators != null) {
                    await _decorators.WalkAsync(walker, cancellationToken);
                }
                foreach (var b in Bases) {
                    await b.WalkAsync(walker, cancellationToken);
                }
                if (_body != null) {
                    await _body.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        public SourceLocation Header => GlobalParent.IndexToLocation(HeaderIndex);

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            if (Decorators != null) {
                Decorators.AppendCodeString(res, ast, format);
            }

            format.ReflowComment(res, this.GetPreceedingWhiteSpace(ast));
            res.Append("class");
            res.Append(this.GetSecondWhiteSpace(ast));
            res.Append(this.GetVerbatimImage(ast) ?? Name);

            if (!this.IsAltForm(ast)) {
                format.Append(
                    res,
                    format.SpaceBeforeClassDeclarationParen,
                    " ",
                    "",
                    this.GetThirdWhiteSpace(ast)
                );

                res.Append('(');
            }

            if (Bases.Count != 0) {
                ListExpression.AppendItems(
                    res,
                    ast,
                    format,
                    "",
                    "",
                    this,
                    Bases.Count,
                    (i, sb) => {
                        if (format.SpaceWithinClassDeclarationParens != null && i == 0) {
                            // need to remove any leading whitespace which was preserved for
                            // the 1st param, and then force the correct whitespace.
                            Bases[i].AppendCodeString(sb, ast, format, format.SpaceWithinClassDeclarationParens.Value ? " " : "");
                        } else {
                            Bases[i].AppendCodeString(sb, ast, format);
                        }
                    }
                );
            } else if (!this.IsAltForm(ast)) {
                if (format.SpaceWithinEmptyBaseClassList != null && format.SpaceWithinEmptyBaseClassList.Value) {
                    res.Append(' ');
                }
            }

            if (!this.IsAltForm(ast) && !this.IsMissingCloseGrouping(ast)) {
                if (Bases.Count != 0 ||
                    format.SpaceWithinEmptyBaseClassList == null ||
                    !string.IsNullOrWhiteSpace(this.GetFourthWhiteSpace(ast))) {
                    format.Append(
                        res,
                        format.SpaceWithinClassDeclarationParens,
                        " ",
                        "",
                        this.GetFourthWhiteSpace(ast)
                    );
                }

                res.Append(')');
            }

            _body.AppendCodeString(res, ast, format);
        }
    }
}

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
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Parsing.Ast {

    /// <summary>
    /// Top-level ast for all Python code.  Holds onto the body and the line mapping information.
    /// </summary>
    public sealed class PythonAst : ScopeStatement {
        private readonly PythonLanguageVersion _langVersion;
        private readonly Statement _body;
        internal readonly NewLineLocation[] _lineLocations;
        internal readonly SourceLocation[] _commentLocations;
        private readonly Dictionary<Node, Dictionary<object, object>> _attributes = new Dictionary<Node, Dictionary<object, object>>();
        private string _privatePrefix;

        internal PythonAst(Statement body, NewLineLocation[] lineLocations, PythonLanguageVersion langVersion, SourceLocation[] commentLocations) {
            if (body == null) {
                throw new ArgumentNullException(nameof(body));
            }
            _langVersion = langVersion;
            _body = body;
            _lineLocations = lineLocations;
            _commentLocations = commentLocations;
        }

        internal PythonAst(IEnumerable<PythonAst> existingAst) {
            var asts = existingAst.ToArray();
            _body = new SuiteStatement(asts.Select(a => a.Body).ToArray());
            _langVersion = asts.Select(a => a._langVersion).Distinct().Single();
            var locs = new List<NewLineLocation>();
            var comments = new List<SourceLocation>();
            int offset = 0;
            foreach (var a in asts) {
                locs.AddRange(a._lineLocations.Select(ll => new NewLineLocation(ll.EndIndex + offset, ll.Kind)));
                offset = locs.LastOrDefault().EndIndex;
            }
            _lineLocations = locs.ToArray();
            offset = 0;
            foreach (var a in asts) {
                comments.AddRange(a._commentLocations.Select(cl => new SourceLocation(cl.Line + offset, cl.Column)));
                offset += a._lineLocations.Length + 1;
            }
            _commentLocations = comments.ToArray();
        }

        public override string Name {
            get {
                return "<module>";
            }
        }

        /// <summary>
        /// Gets the class name which this AST was parsed under.  The class name is appended to any member
        /// accesses that occur.
        /// </summary>
        public string PrivatePrefix {
            get {
                return _privatePrefix;
            }
            internal set {
                _privatePrefix = value;
            }
        }

        /// <summary>
        /// True if the AST was created with verbatim strings.
        /// </summary>
        public bool HasVerbatim { get; internal set; }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                _body.Walk(walker);
            }
            walker.PostWalk(this);
        }

        public override Statement Body {
            get { return _body; }
        }

        public PythonLanguageVersion LanguageVersion {
            get { return _langVersion; }
        }

        internal bool TryGetAttribute(Node node, object key, out object value) {
            if (_attributes.TryGetValue(node, out var nodeAttrs)) {
                return nodeAttrs.TryGetValue(key, out value);
            }
            value = null;
            return false;
        }

        internal void SetAttribute(Node node, object key, object value) {
            if (!_attributes.TryGetValue(node, out var nodeAttrs)) {
                nodeAttrs = _attributes[node] = new Dictionary<object, object>();
            }
            nodeAttrs[key] = value;
        }

        /// <summary>
        /// Copies attributes that apply to one node and makes them available for the other node.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        public void CopyAttributes(Node from, Node to) {
            if (_attributes.TryGetValue(from, out var fromAttrs)) {
                var toAttrs = new Dictionary<object, object>(fromAttrs.Count);
                foreach (var nodeAttr in fromAttrs) {
                    toAttrs[nodeAttr.Key] = nodeAttr.Value;
                }
                _attributes[to] = toAttrs;
            }
        }

        internal void SetAttributes(Dictionary<Node, Dictionary<object, object>> attributes) {
            foreach (var nodeAttributes in attributes) {
                var node = nodeAttributes.Key;
                if (!_attributes.TryGetValue(node, out var existingNodeAttributes)) {
                    existingNodeAttributes = _attributes[node] = new Dictionary<object, object>(nodeAttributes.Value.Count);
                }

                foreach (var nodeAttr in nodeAttributes.Value) {
                    existingNodeAttributes[nodeAttr.Key] = nodeAttr.Value;
                }
            }
        }

        public SourceLocation IndexToLocation(int index) {
            return NewLineLocation.IndexToLocation(_lineLocations, index);
        }

        public int LocationToIndex(SourceLocation location) {
            return NewLineLocation.LocationToIndex(_lineLocations, location, EndIndex);
        }

        /// <summary>
        /// Length of the span (number of characters inside the span).
        /// </summary>
        public int GetSpanLength(SourceSpan span) {
            return LocationToIndex(span.End) - LocationToIndex(span.Start);
        }


        internal int GetLineEndFromPosition(int index) {
            var loc = IndexToLocation(index);
            if (loc.Line >= _lineLocations.Length) {
                return index;
            }
            var res = _lineLocations[loc.Line - 1];
            switch (res.Kind) {
                case NewLineKind.LineFeed:
                case NewLineKind.CarriageReturn: return res.EndIndex - 1;
                case NewLineKind.CarriageReturnLineFeed: return res.EndIndex - 2;
                default:
                    throw new InvalidOperationException("Bad line ending info");
            }
        }

        #region Name Binding Support

        internal override bool ExposesLocalVariable(PythonVariable variable) {
            return true;
        }

        internal override void FinishBind(PythonNameBinder binder) {
        }

        internal override PythonVariable BindReference(PythonNameBinder binder, string name) {
            return EnsureVariable(name);
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

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            _body.AppendCodeString(res, ast, format);
            res.Append(this.GetExtraVerbatimText(ast));
        }
    }
}



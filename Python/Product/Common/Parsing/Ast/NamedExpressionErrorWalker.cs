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

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Common.Core.Collections;
using Microsoft.PythonTools.Common.Core.Disposables;
using Microsoft.PythonTools.Common.Core.Extensions;
using Microsoft.PythonTools.Common.Parsing.Extensions;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    internal class NamedExpressionErrorWalker : PythonWalker {
        private readonly Action<int, int, string> _reportError;

        private NameScope _scope = NameScope.Root;
        private bool _insideForList = false;

        internal static void Check(PythonAst ast, PythonLanguageVersion langVersion, Action<int, int, string> reportError) {
            if (langVersion < PythonLanguageVersion.V38) {
                return;
            }

            ast.Walk(new NamedExpressionErrorWalker(reportError));
        }

        private NamedExpressionErrorWalker(Action<int, int, string> reportError) {
            _reportError = reportError;
        }

        public override bool Walk(ClassDefinition node) {
            _scope = new NameScope(_scope, true);
            return base.Walk(node);
        }

        public override void PostWalk(ClassDefinition node) {
            base.PostWalk(node);
            _scope = _scope.Prev;
        }

        public override bool Walk(FunctionDefinition node) {
            // LambdaExpression wraps FunctionDefinition, so this handles both functions and lambdas.
            var parameters = node.Parameters.Select(p => p.Name).Where(IsNotIgnoredName);
            _scope = new NameScope(_scope, parameters);
            return base.Walk(node);
        }

        public override void PostWalk(FunctionDefinition node) {
            base.PostWalk(node);
            _scope = _scope.Prev;
        }

        public override bool Walk(GeneratorExpression node) {
            using (ComprehensionScope()) {
                foreach (var ci in node.Iterators) {
                    ci.Walk(this);
                }
                node.Item?.Walk(this);
            }
            return false;
        }

        public override bool Walk(DictionaryComprehension node) {
            using (ComprehensionScope()) {
                foreach (var ci in node.Iterators) {
                    ci.Walk(this);
                }
                node.Key?.Walk(this);
                node.Value?.Walk(this);
            }
            return false;
        }

        public override bool Walk(ListComprehension node) {
            using (ComprehensionScope()) {
                foreach (var ci in node.Iterators) {
                    ci.Walk(this);
                }
                node.Item?.Walk(this);
            }
            return false;
        }

        public override bool Walk(SetComprehension node) {
            using (ComprehensionScope()) {
                foreach (var ci in node.Iterators) {
                    ci.Walk(this);
                }
                node.Item?.Walk(this);
            }
            return false;
        }

        public override bool Walk(ComprehensionFor node) {
            var names = node.Left?.ChildNodesBreadthFirst().OfType<NameExpression>().Where(IsNotIgnoredName).MaybeEnumerate();
            _scope.AddIterators(names.Select(name => name.Name).ToImmutableArray());

            // Collect this ahead of time, so that walking the list does not modify the list of names.
            var bad = names.Where(name => _scope.IsNamed(name.Name)).ToImmutableArray();

            var old = _insideForList;
            _insideForList = true;
            node.List?.Walk(this);
            _insideForList = old;

            foreach (var name in bad) {
                ReportSyntaxError(name, Strings.NamedExpressionIteratorRebindsNamedErrorMsg.FormatInvariant(name.Name));
            }

            return false;
        }

        public override bool Walk(NamedExpression node) {
            var names = node.Target.ChildNodesBreadthFirst().OfType<NameExpression>().Where(IsNotIgnoredName).MaybeEnumerate();
            _scope.AddNamed(names.Select(name => name.Name).ToImmutableArray());

            if (_insideForList) {
                ReportSyntaxError(node, Strings.NamedExpressionInComprehensionIteratorErrorMsg);
                return false;
            }

            if (_scope.IsClassTarget) {
                ReportSyntaxError(node, Strings.NamedExpressionInClassBodyErrorMsg);
                return false;
            }

            foreach (var name in names) {
                if (_scope.IsIterator(name.Name)) {
                    ReportSyntaxError(name, Strings.NamedExpressionRebindIteratorErrorMsg.FormatInvariant(name.Name));
                }
            }

            node.Value?.Walk(this);
            return false;
        }

        private void ReportSyntaxError(Node node, string message) => _reportError(node.StartIndex, node.EndIndex, message);

        private static bool IsNotIgnoredName(NameExpression name) => IsNotIgnoredName(name.Name);

        private static bool IsNotIgnoredName(string name) => !string.IsNullOrWhiteSpace(name) && name != "_";

        private IDisposable ComprehensionScope() {
            _scope = new NameScope(_scope);
            return Disposable.Create(() => _scope = _scope.Prev);
        }

        private class NameScope {
            public static NameScope Root = new NameScope();

            private readonly bool _isRoot;
            private readonly bool? _isClassTarget;
            private readonly ImmutableArray<string> _funcParams = ImmutableArray<string>.Empty;

            private ImmutableArray<string> _named = ImmutableArray<string>.Empty;
            private ImmutableArray<string> _iterators = ImmutableArray<string>.Empty;

            private NameScope() {
                _isRoot = true;
            }

            public NameScope(NameScope prev, bool? isClassTarget = null) {
                Prev = prev;
                _isClassTarget = isClassTarget;
            }

            public NameScope(NameScope prev, IEnumerable<string> funcParams) : this(prev, false) {
                _funcParams = funcParams.ToImmutableArray();
            }

            public NameScope Prev { get; } = null;

            public bool IsClassTarget => _isClassTarget ?? Prev?.IsClassTarget ?? false;

            public void AddIterators(ImmutableArray<string> names) {
                if (!_isRoot) {
                    _iterators = _iterators.AddRange(names);
                }
            }

            public void AddNamed(ImmutableArray<string> names) {
                if (!_isRoot) {
                    _named = _named.AddRange(names);
                }
            }

            public bool IsIterator(string name) {
                if (_isRoot || _funcParams.Contains(name)) {
                    return false;
                }

                return _iterators.Contains(name) || (Prev?.IsIterator(name) ?? false);
            }

            public bool IsNamed(string name) {
                if (_isRoot || _funcParams.Contains(name)) {
                    return false;
                }

                return _named.Contains(name) || (Prev?.IsNamed(name) ?? false);
            }
        }
    }
}

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
using Microsoft.PythonTools.Common.Core;
using Microsoft.PythonTools.Common.Core.Collections;
using Microsoft.PythonTools.Common.Core.Extensions;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public abstract class ComprehensionIterator : Node {
    }

    public abstract class Comprehension : Expression {
        public abstract ImmutableArray<ComprehensionIterator> Iterators { get; }
        public abstract override string NodeName { get; }

        internal void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format, string start, string end, Expression item) {
            if (!string.IsNullOrEmpty(start)) {
                format.ReflowComment(res, this.GetPreceedingWhiteSpace(ast));
                res.Append(start);
            }

            item.AppendCodeString(res, ast, format);

            for (var i = 0; i < Iterators.Count; i++) {
                Iterators[i].AppendCodeString(res, ast, format);
            }

            if (!string.IsNullOrEmpty(end)) {
                res.Append(this.GetSecondWhiteSpace(ast));
                res.Append(end);
            }
        }
    }

    public sealed class ListComprehension : Comprehension {
        public ListComprehension(Expression item, ImmutableArray<ComprehensionIterator> iterators) {
            Item = item;
            Iterators = iterators;
        }

        public Expression Item { get; }

        public override ImmutableArray<ComprehensionIterator> Iterators { get; }

        public override string NodeName => "list comprehension";

        public override IEnumerable<Node> GetChildNodes() {
            if (Item != null) yield return Item;
            foreach (var iterator in Iterators) {
                yield return iterator;
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Item?.Walk(walker);
                foreach (var ci in Iterators) {
                    ci.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                if (Item != null) {
                    await Item.WalkAsync(walker, cancellationToken);
                }
                foreach (var ci in Iterators) {
                    await ci.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) => AppendCodeString(res, ast, format, "[", this.IsMissingCloseGrouping(ast) ? "" : "]", Item);
    }

    public sealed class SetComprehension : Comprehension {
        public SetComprehension(Expression item, ImmutableArray<ComprehensionIterator> iterators) {
            Item = item;
            Iterators = iterators;
        }

        public Expression Item { get; }

        public override ImmutableArray<ComprehensionIterator> Iterators { get; }

        public override string NodeName => "set comprehension";

        public override IEnumerable<Node> GetChildNodes() {
            if (Item != null) yield return Item;
            foreach (var iterator in Iterators) {
                yield return iterator;
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Item?.Walk(walker);
                foreach (var ci in Iterators.MaybeEnumerate()) {
                    ci.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                if (Item != null) {
                    await Item.WalkAsync(walker, cancellationToken);
                }
                foreach (var ci in Iterators.MaybeEnumerate()) {
                    await ci.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) => AppendCodeString(res, ast, format, "{", this.IsMissingCloseGrouping(ast) ? "" : "}", Item);
    }

    public sealed class DictionaryComprehension : Comprehension {
        private readonly SliceExpression _value;

        public DictionaryComprehension(SliceExpression value, ImmutableArray<ComprehensionIterator> iterators) {
            _value = value;
            Iterators = iterators;
        }

        public Expression Key => _value.SliceStart;

        public Expression Value => _value.SliceStop;

        public override ImmutableArray<ComprehensionIterator> Iterators { get; }

        public override string NodeName => "dict comprehension";

        public override IEnumerable<Node> GetChildNodes() {
            if (_value != null) yield return _value;
            foreach (var iterator in Iterators) {
                yield return iterator;
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                _value?.Walk(walker);
                foreach (var ci in Iterators.MaybeEnumerate()) {
                    ci.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                if (_value != null) {
                    await _value.WalkAsync(walker, cancellationToken);
                }
                foreach (var ci in Iterators.MaybeEnumerate()) {
                    await ci.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format)
            => AppendCodeString(res, ast, format, "{", this.IsMissingCloseGrouping(ast) ? "" : "}", _value);
    }
}

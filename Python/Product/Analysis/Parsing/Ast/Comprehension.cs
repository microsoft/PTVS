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
using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {
    public abstract class ComprehensionIterator : Node {
    }

    public abstract class Comprehension : Expression {
        public abstract IList<ComprehensionIterator> Iterators { get; }
        public abstract override string NodeName { get; }

        public abstract override void Walk(PythonWalker walker);

        internal void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format, string start, string end, Expression item) {
            if (!String.IsNullOrEmpty(start)) {
                format.ReflowComment(res, this.GetPreceedingWhiteSpace(ast));
                res.Append(start);
            }

            item.AppendCodeString(res, ast, format);

            for (int i = 0; i < Iterators.Count; i++) {
                Iterators[i].AppendCodeString(res, ast, format);
            }

            if (!String.IsNullOrEmpty(end)) {
                res.Append(this.GetSecondWhiteSpace(ast));
                res.Append(end);
            }
        }
    }

    public sealed class ListComprehension : Comprehension {
        private readonly ComprehensionIterator[] _iterators;
        private readonly Expression _item;

        public ListComprehension(Expression item, ComprehensionIterator[] iterators) {
            _item = item;
            _iterators = iterators;
        }

        public Expression Item {
            get { return _item; }
        }

        public override IList<ComprehensionIterator> Iterators {
            get { return _iterators; }
        }

        public override string NodeName {
            get {
                return "list comprehension";
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_item != null) {
                    _item.Walk(walker);
                }
                if (_iterators != null) {
                    foreach (ComprehensionIterator ci in _iterators) {
                        ci.Walk(walker);
                    }
                }
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            AppendCodeString(res, ast, format, "[", this.IsMissingCloseGrouping(ast) ? "" : "]", _item);
        }
    }

    public sealed class SetComprehension : Comprehension {
        private readonly ComprehensionIterator[] _iterators;
        private readonly Expression _item;

        public SetComprehension(Expression item, ComprehensionIterator[] iterators) {
            _item = item;
            _iterators = iterators;
        }

        public Expression Item {
            get { return _item; }
        }

        public override IList<ComprehensionIterator> Iterators {
            get { return _iterators; }
        }

        public override string NodeName {
            get {
                return "set comprehension";
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_item != null) {
                    _item.Walk(walker);
                }
                if (_iterators != null) {
                    foreach (ComprehensionIterator ci in _iterators) {
                        ci.Walk(walker);
                    }
                }
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            AppendCodeString(res, ast, format, "{", this.IsMissingCloseGrouping(ast) ? "" : "}", _item);
        }
    }

    public sealed class DictionaryComprehension : Comprehension {
        private readonly ComprehensionIterator[] _iterators;
        private readonly SliceExpression _value;

        public DictionaryComprehension(SliceExpression value, ComprehensionIterator[] iterators) {
            _value = value;
            _iterators = iterators;
        }

        public Expression Key {
            get { return _value.SliceStart; }
        }

        public Expression Value {
            get { return _value.SliceStop; }
        }

        public override IList<ComprehensionIterator> Iterators {
            get { return _iterators; }
        }

        public override string NodeName {
            get {
                return "dict comprehension";
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_value != null) {
                    _value.Walk(walker);
                }

                if (_iterators != null) {
                    foreach (ComprehensionIterator ci in _iterators) {
                        ci.Walk(walker);
                    }
                }
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            AppendCodeString(res, ast, format, "{", this.IsMissingCloseGrouping(ast) ? "" : "}", _value);
        }
    }
}

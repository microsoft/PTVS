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
    public abstract class ComprehensionIterator : Node {
    }

    public abstract class Comprehension : Expression {
        public abstract IList<ComprehensionIterator> Iterators { get; }
        public abstract override string NodeName { get; }

        public abstract override void Walk(PythonWalker walker);
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
    }

    public sealed class DictionaryComprehension : Comprehension {
        private readonly ComprehensionIterator[] _iterators;
        private readonly Expression _key, _value;

        public DictionaryComprehension(Expression key, Expression value, ComprehensionIterator[] iterators) {
            _key = key;
            _value = value;
            _iterators = iterators;
        }

        public Expression Key {
            get { return _key; }
        }

        public Expression Value {
            get { return _value; }
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
                if (_key != null) {
                    _key.Walk(walker);
                }
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
    }
}

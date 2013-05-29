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
using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal struct CallChain : IEquatable<CallChain>, IEnumerable<Node> {
        private readonly object _chain;

        public CallChain(Node call) {
            _chain = call;
        }

        public CallChain(Node call, CallChain preceding, int limit) {
            if (limit == 1) {
                _chain = call;
            } else {
                _chain = Enumerable.Repeat(call, 1).Concat(preceding).Take(limit).ToArray();
            }
        }

        internal CallChain(Node call, AnalysisUnit unit, int limit) {
            var fau = unit as FunctionAnalysisUnit;
            if (fau == null || limit == 1) {
                _chain = call;
            } else {
                _chain = Enumerable.Repeat(call, 1).Concat(fau.CallChain).Take(limit).ToArray();
            }
        }

        public Node this[int index] {
            get {
                if (index == 0 && _chain != null) {
                    return (_chain as Node) ?? ((Node[])_chain)[0];
                }
                var arr = _chain as Node[];
                if (arr != null) {
                    return arr[index];
                }
                throw new IndexOutOfRangeException();
            }
        }

        public int Count {
            get {
                if (_chain == null) {
                    return 0;
                } else if (_chain is Node) {
                    return 1;
                } else {
                    return ((Node[])_chain).Length;
                }
            }
        }

        public IEnumerator<Node> GetEnumerator() {
            var single = _chain as Node;
            if (single != null) {
                return new SetOfOneEnumerator<Node>(single);
            }
            var arr = _chain as IEnumerable<Node>;
            if (arr != null) {
                return arr.GetEnumerator();
            }
            return Enumerable.Empty<Node>().GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public override bool Equals(object obj) {
            if (obj == null) {
                return false;
            }
            return Equals((CallChain)obj);
        }

        public override int GetHashCode() {
            return this.Aggregate(13187, (hc, n) => hc ^ n.GetHashCode());
        }

        public bool Equals(CallChain other) {
            for (int x = 0, y = 0; x < Count && y < other.Count; ++x, ++y) {
                if (!this[x].Equals(other[y])) {
                    return false;
                }
            }
            // Unequal length is okay as long as the prefix matches.
            return true;
        }
    }
}

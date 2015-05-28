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
    internal struct CallChain : IEnumerable<Node> {
        private readonly object _chain;

        private static Node EmptyNode = new NameExpression(null);
        private static IEnumerable<Node> EmptyNodes {
            get {
                while (true) {
                    yield return EmptyNode;
                }
            }
        }

        private CallChain(object chain) {
            _chain = chain;
        }

        public CallChain(Node call, CallChain preceding, int limit) {
            if (limit == 1) {
                _chain = call;
            } else {
                _chain = Enumerable.Repeat(call, 1).Concat(preceding).Concat(EmptyNodes).Take(limit).ToArray();
            }
        }

        public CallChain(Node call, AnalysisUnit unit, int limit) {
            var fau = unit as FunctionAnalysisUnit;
            if (fau == null || limit == 1) {
                _chain = call;
            } else {
                _chain = Enumerable.Repeat(call, 1).Concat(fau.CallChain).Concat(EmptyNodes).Take(limit).ToArray();
            }
        }

        public CallChain Trim(int limit) {
            object newChain;
            if (_chain == null || limit == 0) {
                newChain = null;
            } else if (_chain is Node) {
                newChain = _chain;
            } else if (limit == 1) {
                newChain = ((Node[])_chain)[0];
            } else {
                newChain = ((Node[])_chain).Take(limit).ToArray();
            }
            return new CallChain(newChain);
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

        public override int GetHashCode() {
            return this.Aggregate(13187, (hc, n) => n.GetHashCode() ^ hc);
        }

        public override bool Equals(object obj) {
            if (obj == null) {
                return false;
            }
            var other = (CallChain)obj;
            return this.SequenceEqual(other);
        }

        public bool PrefixMatches(CallChain other, int limit) {
            return this.Take(limit).SequenceEqual(other.Take(limit));
        }
    }

    internal class CallChainSet<T> {
        private readonly Dictionary<IPythonProjectEntry, KeyValuePair<int, Dictionary<CallChain, T>>> _data;

        public CallChainSet() {
            _data = new Dictionary<IPythonProjectEntry, KeyValuePair<int, Dictionary<CallChain, T>>>();
        }

        public bool TryGetValue(IPythonProjectEntry entry, CallChain chain, int prefixLength, out T value) {
            value = default(T);

            KeyValuePair<int, Dictionary<CallChain, T>> entryData;
            lock (_data) {
                if (!_data.TryGetValue(entry, out entryData)) {
                    return false;
                }
                if (entryData.Key != entry.AnalysisVersion) {
                    _data.Remove(entry);
                    return false;
                }
            }
            lock (entryData.Value) {
                return entryData.Value.TryGetValue(chain, out value);
            }
        }

        public void Clear() {
            lock (_data) {
                _data.Clear();
            }
        }

        public int Count {
            get {
                var data = _data;
                if (data == null) {
                    return 0;
                }
                lock (data) {
                    return data.Values.Sum(v => v.Value.Count);
                }
            }
        }

        public void Add(IPythonProjectEntry entry, CallChain chain, T value) {
            KeyValuePair<int, Dictionary<CallChain, T>> entryData;
            lock (_data) {
                if (!_data.TryGetValue(entry, out entryData) || entryData.Key != entry.AnalysisVersion) {
                    _data[entry] = entryData = new KeyValuePair<int, Dictionary<CallChain, T>>(
                        entry.AnalysisVersion,
                        new Dictionary<CallChain, T>()
                    );
                }
            }

            lock (entryData.Value) {
                entryData.Value[chain] = value;
            }
        }

        public IEnumerable<T> Values {
            get {
                if (_data == null) {
                    return Enumerable.Empty<T>();
                }

                return _data.AsLockedEnumerable()
                    .SelectMany(v => v.Value.Value.Values.AsLockedEnumerable(v.Value.Value));
            }
        }
    }
}

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
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal struct CallChain : IEnumerable<Node>, IEquatable<CallChain> {
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
            var fau = unit as FunctionClosureAnalysisUnit;
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

        public bool Equals(CallChain other) {
            return this.SequenceEqual(other);
        }

        public override string ToString() {
            return "<CallChain {0:X8}>".FormatInvariant(GetHashCode());
        }
    }

    /// <summary>
    /// Maintains a mapping from the CallChain to the FunctionAnalysisUnit's used
    /// for analyzing each unique call.
    /// 
    /// Entries are stored keyed off the ProjectEntry and get thrown away when
    /// a new version of the project entry shows up.
    /// </summary>
    internal class CallChainSet {
        private readonly Dictionary<IVersioned, CallChainEntry> _data;

        public CallChainSet() {
            _data = new Dictionary<IVersioned, CallChainEntry>();
        }

        public bool TryGetValue(IVersioned entry, CallChain chain, int prefixLength, out FunctionAnalysisUnit value) {
            value = null;

            CallChainEntry entryData;
            lock (_data) {
                if (!_data.TryGetValue(entry, out entryData)) {
                    return false;
                }
                if (entryData.AnalysisVersion != entry.AnalysisVersion) {
                    _data.Remove(entry);
                    return false;
                }
            }
            lock (entryData.Calls) {
                return entryData.Calls.TryGetValue(chain, out value);
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
                    return data.Values.Sum(v => v.Calls.Count);
                }
            }
        }

        public void Add(IVersioned entry, CallChain chain, FunctionAnalysisUnit value) {
            CallChainEntry entryData;
            lock (_data) {
                if (!_data.TryGetValue(entry, out entryData) || entryData.AnalysisVersion != entry.AnalysisVersion) {
                    _data[entry] = entryData = new CallChainEntry(
                        entry.AnalysisVersion,
                        new Dictionary<CallChain, FunctionAnalysisUnit>()
                    );
                }
            }

            lock (entryData.Calls) {
                entryData.Calls[chain] = value;
            }
        }

        public IEnumerable<FunctionAnalysisUnit> Values {
            get {
                if (_data == null) {
                    return Enumerable.Empty<FunctionAnalysisUnit>();
                }

                return _data.AsLockedEnumerable()
                    .SelectMany(v => v.Value.Calls.Values.AsLockedEnumerable(v.Value.Calls));
            }
        }

        struct CallChainEntry {
            public readonly int AnalysisVersion;
            public readonly Dictionary<CallChain, FunctionAnalysisUnit> Calls;

            public CallChainEntry(int version, Dictionary<CallChain, FunctionAnalysisUnit> calls) {
                AnalysisVersion = version;
                Calls = calls;
            }
        }
    }
}

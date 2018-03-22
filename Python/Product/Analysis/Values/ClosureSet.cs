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

using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools.Analysis.Values {
    sealed class ClosureSetDefinition {
        private readonly IReadOnlyList<int> _keys;

        public ClosureSetDefinition(IEnumerable<int> argumentIndices) {
            _keys = argumentIndices.Where(i => i >= 0).ToArray();
        }

        public ClosureSet Get(ArgumentSet callArgs) {
            return new ClosureSet(
                this,
                _keys.Select(i => i < callArgs.Args.Length ? callArgs.Args[i] : null).ToArray(),
                ObjectComparer.Instance
            );
        }
    }

    sealed class ClosureSet {
        private readonly ClosureSetDefinition _owner;
        private readonly IReadOnlyList<IAnalysisSet> _values;
        private readonly IEqualityComparer<IAnalysisSet> _comparer;
        private int _hashCode;

        public ClosureSet(ClosureSetDefinition owner, IReadOnlyList<IAnalysisSet> values, IEqualityComparer<IAnalysisSet> comparer) {
            _owner = owner;
            _values = values;
            _comparer = comparer;
            var hc = _owner.GetHashCode();
            unchecked {
                foreach (var v in _values) {
                    hc += 17 * comparer.GetHashCode(v);
                }
            }
            _hashCode = hc;
        }

        public override string ToString() {
            var parts = new List<string>();
            parts.Add("<");
            foreach (var v in _values) {
                parts.AddRange(v.GetRichDescriptions(", ", unionPrefix: "{", unionSuffix: "}").Select(kv => kv.Value));
            }
            if (parts.Count > 2) {
                parts.RemoveAt(1);
                parts.Add(":");
            }
            parts.Add("{0:X8}".FormatInvariant(GetHashCode()));
            parts.Add(">");
            return string.Join("", parts);
        }

        public override bool Equals(object obj) {
            var other = obj as ClosureSet;
            if (other == null) {
                return false;
            }
            if (_owner != other._owner) {
                return false;
            }
            if (_values.Zip(other._values, (s1, s2) => _comparer.Equals(s1, s2)).All(b => b)) {
                return true;
            }
            return false;
        }

        public override int GetHashCode() => _hashCode;
    }
}

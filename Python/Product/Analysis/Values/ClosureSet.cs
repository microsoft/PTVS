using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    sealed class ClosureSetDefinition {
        private readonly IReadOnlyList<string> _keys;

        public ClosureSetDefinition(IEnumerable<KeyValuePair<string, PythonVariable>> variables) {
            _keys = variables.Where(kv => kv.Value.AccessedInNestedScope).Select(kv => kv.Key).OrderBy(k => k).ToArray();
        }

        public ClosureSet Get(Node node, AnalysisUnit unit) {
            var eval = new ExpressionEvaluator(unit);
            return new ClosureSet(
                this,
                _keys.Select(k => eval.LookupAnalysisSetByName(node, k, addRef: false)).ToArray(),
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

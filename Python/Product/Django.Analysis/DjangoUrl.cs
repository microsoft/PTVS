using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Microsoft.PythonTools.Django.Analysis {
    class DjangoUrl : IComparable<DjangoUrl> {
        public string FullUrlName;
        private readonly string _urlRegex;
        private static readonly Regex _regexGroupMatchingRegex = new Regex(@"\(.*?\)");
        public IList<DjangoUrlParameter> _parameters = new List<DjangoUrlParameter>();

        public IEnumerable<DjangoUrlParameter> NamedParameters {
            get {
                return _parameters.Where(p => p.IsNamed);
            }
        }

        public DjangoUrl() { }

        public DjangoUrl(string urlName, string urlRegex) {
            FullUrlName = urlName;
            _urlRegex = urlRegex;

            ParseUrlRegex();
        }

        private void ParseUrlRegex() {
            MatchCollection matches = _regexGroupMatchingRegex.Matches(_urlRegex);

            foreach (Match m in matches) {
                foreach (Group grp in m.Groups) {
                    _parameters.Add(new DjangoUrlParameter(grp.Value));
                }
            }
        }

        #region IComparable implementation

        public int CompareTo(DjangoUrl other) {
            return FullUrlName.CompareTo(other.FullUrlName);
        }

        #endregion
    }

    class DjangoUrlParameter {
        private static readonly Regex _namedParameterRegex = new Regex(@"\?P<(.*)>");

        public readonly string RegexValue;
        public readonly string Name;
        public readonly bool IsNamed;

        public DjangoUrlParameter() { }

        public DjangoUrlParameter(string parameterRegex) {
            Name = parameterRegex;
            RegexValue = parameterRegex.TrimStart('(').TrimEnd(')');

            Match m = _namedParameterRegex.Match(RegexValue);
            IsNamed = m.Success && m.Groups.Count == 2;
            if (IsNamed) {
                Name = m.Groups[1].Value;
            }
        }
    }
}

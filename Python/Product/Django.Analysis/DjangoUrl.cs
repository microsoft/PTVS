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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace Microsoft.PythonTools.Django.Analysis
{
    class DjangoUrl
    {
        public readonly string Name;
        public string FullName
        {
            get
            {
                return Name;
            }
        }
        private readonly string _urlRegex;
        private static readonly Regex _regexGroupMatchingRegex = new Regex(@"\(.*?\)");
        public IList<DjangoUrlParameter> Parameters = new List<DjangoUrlParameter>();

        public IEnumerable<DjangoUrlParameter> NamedParameters
        {
            get
            {
                return Parameters.Where(p => p.IsNamed);
            }
        }

        public DjangoUrl() { }

        public DjangoUrl(string urlName, string urlRegex)
        {
            Name = urlName ?? throw new ArgumentNullException(nameof(urlName));
            _urlRegex = urlRegex ?? throw new ArgumentNullException(nameof(urlRegex));

            ParseUrlRegex();
        }

        private void ParseUrlRegex()
        {
            MatchCollection matches = _regexGroupMatchingRegex.Matches(_urlRegex);

            foreach (Match m in matches)
            {
                foreach (Group grp in m.Groups)
                {
                    Parameters.Add(new DjangoUrlParameter(grp.Value));
                }
            }
        }
    }

    class DjangoUrlParameter
    {
        private static readonly Regex _namedParameterRegex = new Regex(@"\?P<(.*)>");

        public readonly string RegexValue;
        public readonly string Name;
        public readonly bool IsNamed;

        public DjangoUrlParameter() { }

        public DjangoUrlParameter(string parameterRegex)
        {
            Name = parameterRegex;
            RegexValue = parameterRegex.TrimStart('(').TrimEnd(')');

            Match m = _namedParameterRegex.Match(RegexValue);
            IsNamed = m.Success && m.Groups.Count == 2;
            if (IsNamed)
            {
                Name = m.Groups[1].Value;
            }
        }
    }
}

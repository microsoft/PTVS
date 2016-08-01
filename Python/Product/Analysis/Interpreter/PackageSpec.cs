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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Interpreter {
    public struct PackageSpec {
        private static readonly Regex FindRequirementRegex = new Regex(@"
            (?<!\#.*)       # ensure we are not in a comment
            (?<=\s|\A)      # ensure we are preceded by a space/start of the line
            (?<spec>        # <spec> includes name, version and whitespace
                (?<name>[^\s\#<>=!\-][^\s\#<>=!]*)  # just the name, no whitespace
                \s*
                (?<constraint>(==\s*(?<exact_ver>[^\s\#]+))?[^\#]*)
            )", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace
        );

        private readonly Lazy<Match> _match;

        public string Spec { get; }

        public PackageSpec(string fullSpec) {
            Spec = fullSpec;
            _match = new Lazy<Match>(() => FindRequirementRegex.Match(fullSpec));
        }

        public bool IsValid => _match?.Value.Success ?? false;
        public string Name => _match?.Value.Groups["name"].Value;
        public string Constraint => _match?.Value.Groups["constraint"].Value;
        public string Version => _match?.Value.Groups["exact_ver"].Value;
    }
}

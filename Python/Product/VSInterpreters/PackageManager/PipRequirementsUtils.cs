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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.PythonTools.Interpreter {
    static class PipRequirementsUtils {
        internal static readonly Regex FindRequirementRegex = new Regex(@"
            (?<!\#.*)       # ensure we are not in a comment
            (?<=\s|\A)      # ensure we are preceded by a space/start of the line
            (?<spec>        # <spec> includes name, version and whitespace
                (?<name>[^\s\#<>=!\-][^\s\#<>=!]*)  # just the name, no whitespace
                (\s*(?<cmp><=|>=|<|>|!=|==)\s*
                    (?<ver>[^\#]+)
                )?          # cmp and ver are optional
            )", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace
        );

        internal static IEnumerable<string> MergeRequirements(
            IEnumerable<string> original,
            IEnumerable<PackageSpec> updates,
            bool addNew
        ) {
            if (original == null) {
                foreach (var req in updates.OrderBy(r => r.FullSpec)) {
                    yield return req.FullSpec;
                }
                yield break;
            }

            var existing = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var p in updates) {
                existing[p.Name] = p.FullSpec;
            }

            var seen = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var _line in original) {
                var line = _line;
                foreach (var m in FindRequirementRegex.Matches(line).Cast<Match>()) {
                    string newReq;
                    var name = m.Groups["name"].Value;
                    if (existing.TryGetValue(name, out newReq)) {
                        line = FindRequirementRegex.Replace(line, m2 =>
                            name.Equals(m2.Groups["name"].Value, StringComparison.InvariantCultureIgnoreCase) ?
                                newReq :
                                m2.Value
                        );
                        seen.Add(name);
                    }
                }
                yield return line;
            }

            if (addNew) {
                foreach (var req in existing
                    .Where(kv => !seen.Contains(kv.Key))
                    .Select(kv => kv.Value)
                    .OrderBy(v => v)
                ) {
                    yield return req;
                }
            }
        }

        internal static bool AnyPackageMissing(
            IEnumerable<string> original,
            IEnumerable<PackageSpec> installed
        ) {
            foreach (var _line in original) {
                var line = _line;
                foreach (var m in FindRequirementRegex.Matches(line).Cast<Match>()) {
                    var name = m.Groups["name"].Value;
                    if (installed.FirstOrDefault(pkg => string.Compare(pkg.Name, name, StringComparison.OrdinalIgnoreCase) == 0) == null) {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}

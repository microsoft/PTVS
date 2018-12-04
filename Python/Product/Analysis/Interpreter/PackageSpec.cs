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

using System.Text.RegularExpressions;

namespace Microsoft.PythonTools.Interpreter {
    public class PackageSpec {
        private static readonly Regex FindRequirementRegex = new Regex(@"
            (?<!\#.*)       # ensure we are not in a comment
            (?<=\s|\A)      # ensure we are preceded by a space/start of the line
            (?<spec>        # <spec> includes name, version and whitespace
                (?<name>[^\s\#<>=!\-][^\s\#<>=!]*)  # just the name, no whitespace
                \s*
                (?<constraint>(==\s*(?<exact_ver>[^\s\#]+))?[^\#]*)
            )$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace
        );

        private static readonly Regex PipListRegex = new Regex(@"
            (?<!\#.*)       # ensure we are not in a comment
            (?<=\s|\A)      # ensure we are preceded by a space/start of the line
            (?<spec>        # <spec> includes name, version and whitespace
                (?<name>[^\s\#<>=!\-][^\s\#<>=!]*)  # just the name, no whitespace
                \s*
                (\((?<exact_ver>[^\s\#]+)\))?
                [^\#]*
            )$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace
        );

        private string _fullSpec;
        private string _constraint;

        public string FullSpec {
            get {
                if (!string.IsNullOrEmpty(_fullSpec)) {
                    return _fullSpec;
                }
                return Name + Constraint;
            }
        }
        public string Name { get; set; }

        public string Constraint {
            get {
                if (!string.IsNullOrEmpty(_constraint)) {
                    return _constraint;
                }
                if (ExactVersion.IsEmpty) {
                    return "";
                }
                return "==" + ExactVersion;
            }
            set { _constraint = value; }
        }

        public PackageVersion ExactVersion { get; set; }
        public string Description { get; set; }
        public bool IsValid => !string.IsNullOrEmpty(Name);

        public PackageSpec() { }

        public PackageSpec(string name, string exactVersion, string constraint = null, string fullSpec = null)
            : this(name, PackageVersion.TryParse(exactVersion), constraint, fullSpec) { }

        public PackageSpec(string name, PackageVersion? exactVersion = null, string constraint = null, string fullSpec = null) {
            _fullSpec = fullSpec ?? "";
            Name = name ?? "";
            ExactVersion = exactVersion ?? PackageVersion.Empty;
            Constraint = constraint;
        }

        public PackageSpec Clone() {
            return (PackageSpec)MemberwiseClone();
        }

        public static PackageSpec FromRequirement(string fullSpec) {
            var match = FindRequirementRegex.Match(fullSpec);
            if (!match.Success) {
                return new PackageSpec();
            }
            return new PackageSpec(
                match.Groups["name"].Value,
                match.Groups["exact_ver"].Value,
                match.Groups["constraint"].Value,
                match.Groups["spec"].Value
            );
        }

        public static PackageSpec FromPipList(string line) {
            var match = PipListRegex.Match(line);
            if (!match.Success) {
                return new PackageSpec();
            }
            return new PackageSpec(match.Groups["name"].Value, match.Groups["exact_ver"].Value);
        }

        /// <summary>
        /// Returns a package spec containing the specified string as the 
        /// <see cref="FullSpec"/>. However, <see cref="IsValid"/> may be false.
        /// </summary>
        public static PackageSpec FromArguments(string arguments) {
            return new PackageSpec(null, fullSpec: arguments);
        }
    }
}

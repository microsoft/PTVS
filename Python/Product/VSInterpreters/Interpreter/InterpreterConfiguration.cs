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
using System.Globalization;
using System.Linq;
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools.Interpreter {
    // Note: this is copied from language server because the one there is sealed
    public class InterpreterConfiguration : IEquatable<InterpreterConfiguration> {
        private readonly string _description;
        private string _fullDescription;

        /// <summary>
        /// Constructs a new interpreter configuration based on the provided values.
        /// </summary>
        public InterpreterConfiguration(
            string id,
            string description,
            string pythonExePath = null,
            string pathVar = "",
            string libPath = null,
            string sitePackagesPath = null,
            InterpreterArchitecture architecture = default(InterpreterArchitecture),
            Version version = null
        ) {
            Id = id;
            _description = description ?? string.Empty;
            InterpreterPath = pythonExePath;
            PathEnvironmentVariable = pathVar;
            Architecture = architecture ?? InterpreterArchitecture.Unknown;
            Version = version ?? new Version();
            LibraryPath = libPath ?? string.Empty;
            SitePackagesPath = sitePackagesPath ?? string.Empty;
        }

        private static string Read(Dictionary<string, object> d, string k)
            => d.TryGetValue(k, out var o) ? o as string : null;

        private InterpreterConfiguration(Dictionary<string, object> properties) {
            Id = Read(properties, nameof(Id));
            _description = Read(properties, nameof(Description)) ?? "";
            InterpreterPath = Read(properties, nameof(InterpreterPath));
            PathEnvironmentVariable = Read(properties, nameof(PathEnvironmentVariable));
            LibraryPath = Read(properties, nameof(LibraryPath));
            Architecture = InterpreterArchitecture.TryParse(Read(properties, nameof(Architecture)));
            try {
                Version = Version.Parse(Read(properties, nameof(Version)));
            } catch (Exception ex) when (ex is ArgumentException || ex is FormatException) {
                Version = new Version();
            }
            if (properties.TryGetValue(nameof(SearchPaths), out object o)) {
                SearchPaths.Clear();
                if (o is string s) {
                    SearchPaths.AddRange(s.Split(';'));
                } else if (o is IEnumerable<string> ss) {
                    SearchPaths.AddRange(ss);
                }
            }
        }

        internal void WriteToDictionary(Dictionary<string, object> properties) {
            properties[nameof(Id)] = Id;
            properties[nameof(Description)] = _description;
            properties[nameof(InterpreterPath)] = InterpreterPath;
            properties[nameof(PathEnvironmentVariable)] = PathEnvironmentVariable;
            properties[nameof(LibraryPath)] = LibraryPath;
            properties[nameof(Architecture)] = Architecture.ToString();
            if (Version != null) {
                properties[nameof(Version)] = Version.ToString();
            }
            properties[nameof(SearchPaths)] = SearchPaths.ToArray();
        }

        /// <summary>
        /// Serializes an interpreter configuration to a dictionary.
        /// </summary>
        public Dictionary<string, object> ToDictionary() {
            var d = new Dictionary<string, object>();
            WriteToDictionary(d);
            return d;
        }

        /// <summary>
        /// Gets a unique and stable identifier for this interpreter.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets a friendly description of the interpreter
        /// </summary>
        public string Description => _fullDescription ?? _description;

        /// <summary>
        /// Changes the description to be less likely to be
        /// ambiguous with other interpreters.
        /// </summary>
        public void SwitchToFullDescription() {
            var hasVersion = _description.Contains(Version.ToString());
            var hasArch = _description.IndexOf(Architecture.ToString(null, CultureInfo.CurrentCulture), StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                _description.IndexOf(Architecture.ToString("x", CultureInfo.CurrentCulture), StringComparison.CurrentCultureIgnoreCase) >= 0;

            if (hasVersion && hasArch) {
                // Regular description is sufficient
                _fullDescription = null;
            } else if (hasVersion) {
                _fullDescription = "{0} ({1})".FormatUI(_description, Architecture);
            } else if (hasArch) {
                _fullDescription = "{0} ({1})".FormatUI(_description, Version);
            } else {
                _fullDescription = "{0} ({1}, {2})".FormatUI(_description, Version, Architecture);
            }
        }

        /// <summary>
        /// Returns the path to the interpreter executable for launching Python
        /// applications.
        /// </summary>
        public string InterpreterPath { get; }
        
        /// <summary>
        /// Gets the environment variable which should be used to set sys.path.
        /// </summary>
        public string PathEnvironmentVariable { get; }

        /// <summary>
        /// The architecture of the interpreter executable.
        /// </summary>
        public InterpreterArchitecture Architecture { get; }

        public string ArchitectureString => Architecture.ToString();

        /// <summary>
        /// The language version of the interpreter (e.g. 2.7).
        /// </summary>
        public Version Version { get; }

        /// <summary>
        /// Returns path to Python standard libraries.
        /// </summary>
        public string LibraryPath {get; }

        /// <summary>
        /// Returns path to Python site packages from 'import site; print(site.getsitepackages())'
        /// </summary>
        public string SitePackagesPath { get; }

        /// <summary>
        /// The fixed search paths of the interpreter.
        /// </summary>
        public List<string> SearchPaths { get; } = new List<string>();

        public static bool operator ==(InterpreterConfiguration x, InterpreterConfiguration y)
            => x?.Equals(y) ?? object.ReferenceEquals(y, null);
        public static bool operator !=(InterpreterConfiguration x, InterpreterConfiguration y)
            => !(x?.Equals(y) ?? object.ReferenceEquals(y, null));

        public override bool Equals(object obj) => Equals(obj as InterpreterConfiguration);

        public bool Equals(InterpreterConfiguration other) {
            if (other == null) {
                return false;
            }

            var cmp = StringComparer.OrdinalIgnoreCase;
            return string.Equals(Id, other.Id) &&
                cmp.Equals(Description, other.Description) &&
                cmp.Equals(InterpreterPath, other.InterpreterPath) &&
                cmp.Equals(PathEnvironmentVariable, other.PathEnvironmentVariable) &&
                Architecture == other.Architecture &&
                Version == other.Version;
        }

        public override int GetHashCode() {
            var cmp = StringComparer.OrdinalIgnoreCase;
            return Id.GetHashCode() ^
                cmp.GetHashCode(Description) ^
                cmp.GetHashCode(InterpreterPath ?? "") ^
                cmp.GetHashCode(PathEnvironmentVariable ?? "") ^
                Architecture.GetHashCode() ^
                Version.GetHashCode();
        }

        public override string ToString() => Description;

        /// <summary>
        /// Attempts to update descriptions to be unique within the
        /// provided sequence by adding information about the
        /// interpreter that is missing from the default description.
        /// </summary>
        public static void DisambiguateDescriptions(IReadOnlyList<InterpreterConfiguration> configs) {
            foreach (var c in configs) {
                c._fullDescription = null;
            }
            foreach (var c in configs.GroupBy(i => i._description ?? "").Where(g => g.Count() > 1).SelectMany(g => g)) {
                c.SwitchToFullDescription();
            }
        }
    }
}

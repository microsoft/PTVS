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

namespace Microsoft.CookiecutterTools.Interpreters {
    public sealed class InterpreterConfiguration {
        /// <summary>
        /// <para>Constructs a new interpreter configuration based on the
        /// provided values.</para>
        /// <para>No validation is performed on the parameters.</para>
        /// <para>If winPath is null or empty,
        /// <see cref="WindowsInterpreterPath"/> will be set to path.</para>
        /// </summary>
        public InterpreterConfiguration(
            string id,
            string description,
            string prefixPath = null,
            string path = null,
            string winPath = "",
            string pathVar = "",
            InterpreterArchitecture arch = default(InterpreterArchitecture),
            Version version = null
        ) {
            Id = id;
            Description = description;
            PrefixPath = prefixPath;
            InterpreterPath = path;
            WindowsInterpreterPath = string.IsNullOrEmpty(winPath) ? path : winPath;
            PathEnvironmentVariable = pathVar;
            Architecture = arch ?? InterpreterArchitecture.Unknown;
            Version = version ?? new Version();
        }

        /// <summary>
        /// Gets a unique and stable identifier for this interpreter.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets a friendly description of the interpreter
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Returns the prefix path of the Python installation. All files
        /// related to the installation should be underneath this path.
        /// </summary>
        public string PrefixPath { get; }

        /// <summary>
        /// Returns the path to the interpreter executable for launching Python
        /// applications.
        /// </summary>
        public string InterpreterPath { get; }

        /// <summary>
        /// Returns the path to the interpreter executable for launching Python
        /// applications which are windows applications (pythonw.exe, ipyw.exe).
        /// </summary>
        public string WindowsInterpreterPath { get; }

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

        public override bool Equals(object obj) {
            var other = obj as InterpreterConfiguration;
            if (other == null) {
                return false;
            }

            var cmp = StringComparer.OrdinalIgnoreCase;
            return cmp.Equals(PrefixPath, other.PrefixPath) &&
                cmp.Equals(Id, other.Id) &&
                cmp.Equals(InterpreterPath, other.InterpreterPath) &&
                cmp.Equals(WindowsInterpreterPath, other.WindowsInterpreterPath) &&
                cmp.Equals(PathEnvironmentVariable, other.PathEnvironmentVariable) &&
                Architecture == other.Architecture &&
                Version == other.Version;
        }

        public override int GetHashCode() {
            var cmp = StringComparer.OrdinalIgnoreCase;
            return cmp.GetHashCode(PrefixPath ?? "") ^
                Id.GetHashCode() ^
                cmp.GetHashCode(InterpreterPath ?? "") ^
                cmp.GetHashCode(WindowsInterpreterPath ?? "") ^
                cmp.GetHashCode(PathEnvironmentVariable ?? "") ^
                Architecture.GetHashCode() ^
                Version.GetHashCode();
        }

        public override string ToString() {
            return Description;
        }
    }
}

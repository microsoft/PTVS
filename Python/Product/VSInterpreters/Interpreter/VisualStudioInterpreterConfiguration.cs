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

namespace Microsoft.PythonTools.Interpreter {
    public class VisualStudioInterpreterConfiguration : InterpreterConfiguration, IEquatable<VisualStudioInterpreterConfiguration> {
        public string PrefixPath { get; }

        /// <summary>
        /// Returns the path to the interpreter executable for launching Python
        /// applications which are windows applications (pythonw.exe, ipyw.exe).
        /// </summary>
        public string WindowsInterpreterPath { get; }

        /// <summary>
        /// The UI behavior of the interpreter.
        /// </summary>
        public InterpreterUIMode UIMode { get; }


        public VisualStudioInterpreterConfiguration(
            string id,
            string description,
            string prefixPath = null,
            string pythonExePath = null,
            string winPath = "",
            string pathVar = "",
            InterpreterArchitecture architecture = default(InterpreterArchitecture),
            Version version = null,
            InterpreterUIMode uiMode = InterpreterUIMode.Normal
        ) : base(id, description, pythonExePath, pathVar, string.Empty, string.Empty, architecture, version) {
            PrefixPath = prefixPath;
            WindowsInterpreterPath = string.IsNullOrEmpty(winPath) ? pythonExePath : winPath;
            UIMode = uiMode;
        }

        public bool Equals(VisualStudioInterpreterConfiguration other) {
            if (other == null) {
                return false;
            }

            var cmp = StringComparer.OrdinalIgnoreCase;
            return cmp.Equals(PrefixPath, other.PrefixPath) &&
                   string.Equals(Id, other.Id) &&
                   cmp.Equals(Description, other.Description) &&
                   cmp.Equals(InterpreterPath, other.InterpreterPath) &&
                   cmp.Equals(WindowsInterpreterPath, other.WindowsInterpreterPath) &&
                   cmp.Equals(PathEnvironmentVariable, other.PathEnvironmentVariable) &&
                   Architecture == other.Architecture &&
                   Version == other.Version &&
                   UIMode == other.UIMode;
        }

        public override int GetHashCode() {
            var cmp = StringComparer.OrdinalIgnoreCase;
            return cmp.GetHashCode(PrefixPath ?? "") ^
                   Id.GetHashCode() ^
                   cmp.GetHashCode(Description) ^
                   cmp.GetHashCode(InterpreterPath ?? "") ^
                   cmp.GetHashCode(WindowsInterpreterPath ?? "") ^
                   cmp.GetHashCode(PathEnvironmentVariable ?? "") ^
                   Architecture.GetHashCode() ^
                   Version.GetHashCode() ^
                   UIMode.GetHashCode();
        }
    }
}

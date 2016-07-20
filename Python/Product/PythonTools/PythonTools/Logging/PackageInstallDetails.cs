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

namespace Microsoft.PythonTools.Logging {
    public sealed class PackageInstallDetails {

        private readonly string _name;
        private readonly string _version;
        private readonly string _interpreterName;
        private readonly string _interpreterVersion;
        private readonly string _interpreterArchitecture;
        private readonly string _installer;
        private readonly bool _elevated;
        private readonly int _installResult;

        public PackageInstallDetails(
                        string packageName,
                        string packageVersion,
                        string interpreterName,
                        string interpreterVersion,
                        string interpreterArchictecture,
                        string installer,
                        bool elevated,
                        int installResult) {
            _name = packageName != null ? packageName : String.Empty;
            _version = packageVersion != null ? packageVersion : String.Empty;
            _interpreterName = interpreterName != null ? interpreterName : String.Empty;
            _interpreterVersion = interpreterVersion != null ? interpreterVersion : String.Empty;
            _interpreterArchitecture = interpreterArchictecture != null ? interpreterArchictecture : String.Empty;
            _installer = installer != null ? installer : String.Empty;
            _elevated = elevated;
            _installResult = installResult;
        }

        private const string _header = "Name,Version,InterpreterName,InterpreterVersion,InterpreterArchitecture,Installer,Elevated,InstallResult";
        public static string Header() { return _header; }

        public override string ToString() {
            return Name + "," + 
                Version + "," + 
                InterpreterName + "," +
                InterpreterVersion + "," +
                InterpreterArchitecture + "," +
                Installer + "," +
                Elevated + "," +
                InstallResult;
        }

        /// <summary>
        /// Name of the package installed
        /// </summary>
        public string Name { get { return _name; } }
        /// <summary>
        /// Version of the package installed
        /// </summary>
        public string Version { get { return _version; } }
        public string InterpreterName { get { return _interpreterName; } }
        public string InterpreterVersion { get { return _interpreterVersion; } }
        public string InterpreterArchitecture { get { return _interpreterArchitecture; } }
        public int InstallResult { get { return _installResult; } }
        public bool Elevated { get { return _elevated; } }
        /// <summary>
        /// Represents the technology used for installation
        ///   ex: pip, easy_install, conda
        /// </summary>
        public string Installer { get { return _installer; } }
    }
}
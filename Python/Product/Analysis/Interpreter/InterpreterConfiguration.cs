/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Microsoft.PythonTools.Interpreter {
    public sealed class InterpreterConfiguration {
        readonly string _prefixPath;
        readonly string _interpreterPath;
        readonly string _windowsInterpreterPath;
        readonly string _libraryPath;
        readonly string _pathEnvironmentVariable;
        readonly ProcessorArchitecture _architecture;
        readonly Version _version;

        internal InterpreterConfiguration(Version version) {
            _version = version;
        }

        public InterpreterConfiguration(string prefixPath,
                                        string path,
                                        string winPath,
                                        string libraryPath,
                                        string pathVar,
                                        ProcessorArchitecture arch,
                                        Version version) {
            _prefixPath = prefixPath;
            _interpreterPath = path;
            _windowsInterpreterPath = winPath ?? path;
            _libraryPath = libraryPath;
            if (string.IsNullOrEmpty(_libraryPath)) {
                _libraryPath = Path.Combine(_prefixPath, "Lib");
            }
            _pathEnvironmentVariable = pathVar;
            _architecture = arch;
            _version = version;
            Debug.Assert(string.IsNullOrEmpty(_interpreterPath) || !string.IsNullOrEmpty(_prefixPath),
                "Anyone providing an interpreter should also specify the prefix path");
        }

        /// <summary>
        /// Returns the prefix path of the Python installation.
        /// </summary>
        public string PrefixPath {
            get { return _prefixPath; }
        }

        /// <summary>
        /// Returns the path to the interpreter executable for launching Python applications.
        /// </summary>
        public string InterpreterPath {
            get { return _interpreterPath; }
        }

        /// <summary>
        /// Returns the path to the interpreter executable for launching Python applications
        /// which are windows applications (pythonw.exe, ipyw.exe)
        /// </summary>
        public string WindowsInterpreterPath {
            get { return _windowsInterpreterPath; }
        }

        /// <summary>
        /// The path to the standard library associated with this interpreter.
        /// This may be null if the interpreter does not support standard
        /// library analysis.
        /// </summary>
        public string LibraryPath {
            get { return _libraryPath; }
        }

        /// <summary>
        /// Gets the environment variable which should be used to set sys.path.
        /// </summary>
        public string PathEnvironmentVariable {
            get { return _pathEnvironmentVariable; }
        }

        /// <summary>
        /// The architecture of the interpreter executable.
        /// </summary>
        public ProcessorArchitecture Architecture {
            get { return _architecture; }
        }

        /// <summary>
        /// The language version of the interpreter (e.g. 2.7).
        /// </summary>
        public Version Version {
            get { return _version; }
        }

        public override bool Equals(object obj) {
            var other = obj as InterpreterConfiguration;
            if (other == null) {
                return false;
            }

            var cmp = StringComparer.OrdinalIgnoreCase;
            return cmp.Equals(PrefixPath, other.PrefixPath) &&
                cmp.Equals(InterpreterPath, other.InterpreterPath) &&
                cmp.Equals(WindowsInterpreterPath, other.WindowsInterpreterPath) &&
                cmp.Equals(LibraryPath, other.LibraryPath) &&
                cmp.Equals(PathEnvironmentVariable, other.PathEnvironmentVariable) &&
                Architecture == other.Architecture &&
                Version == other.Version;
        }

        public override int GetHashCode() {
            var cmp = StringComparer.OrdinalIgnoreCase;
            return cmp.GetHashCode(PrefixPath) ^
                cmp.GetHashCode(InterpreterPath) ^
                cmp.GetHashCode(WindowsInterpreterPath) ^
                cmp.GetHashCode(LibraryPath) ^
                cmp.GetHashCode(PathEnvironmentVariable) ^
                Architecture.GetHashCode() ^
                Version.GetHashCode();
        }
    }
}

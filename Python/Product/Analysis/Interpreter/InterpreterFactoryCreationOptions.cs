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

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Specifies the options for an executable-based interpreter factory.
    /// </summary>
    public sealed class InterpreterFactoryCreationOptions {
        public InterpreterFactoryCreationOptions() {
            Architecture = InterpreterArchitecture.Unknown;
        }

        public InterpreterFactoryCreationOptions Clone() {
            return (InterpreterFactoryCreationOptions)MemberwiseClone();
        }

        /// <summary>
        /// Specifies the version of the language which this interpreter uses.
        /// </summary>
        public Version LanguageVersion { get; set; }

        /// <summary>
        /// Specifies the version of the language this interpreter uses as a
        /// string.
        /// </summary>
        /// <exception cref="FormatException">
        /// The string cannot be parsed.
        /// </exception>
        public string LanguageVersionString {
            get {
                return LanguageVersion != null ? LanguageVersion.ToString() : string.Empty;
            }
            set {
                LanguageVersion = string.IsNullOrEmpty(value) ? null : Version.Parse(value);
            }
        }
        
        public string Id {
            get;
            set;
        }

        /// <summary>
        /// Provides a description of the interpreter. Value must be a string.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Specifies the path for the Python executable. Value must be a path
        /// to an executable file.
        /// </summary>
        public string InterpreterPath { get; set; }

        /// <summary>
        /// Specifies the path for the Python executable for starting GUI
        /// applications. Value must be a path to an executable file or an empty
        /// string.
        /// </summary>
        public string WindowInterpreterPath { get; set; }

        /// <summary>
        /// Specifies the path containing the Python standard library. Value
        /// must be a path to a directory.
        /// </summary>
        public string LibraryPath { get; set; }

        /// <summary>
        /// Specifies the root path of this interpreter. Value must be a path to
        /// a directory.
        /// </summary>
        public string PrefixPath { get; set; }

        /// <summary>
        /// Specifies the environment variable used for setting sys.path. Value
        /// must be a string; default is "PYTHONPATH".
        /// </summary>
        public string PathEnvironmentVariableName { get; set; }

        /// <summary>
        /// Specifies the processor architecture of the Python interpreter.
        /// The default is X86.
        /// </summary>
        public InterpreterArchitecture Architecture { get; set; }

        /// <summary>
        /// Gets or sets the architecture as either "x86" or "x64".
        /// </summary>
        public string ArchitectureString {
            get {
                return Architecture.ToString("x");
            }
            set {
                Architecture = InterpreterArchitecture.TryParse(value);
            }
        }
    }
}

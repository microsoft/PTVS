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
using System.Reflection;

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Specifies the options for an executable-based interpreter factory.
    /// </summary>
    public sealed class InterpreterFactoryCreationOptions {
        public InterpreterFactoryCreationOptions() {
            Architecture = ProcessorArchitecture.X86;
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
                return LanguageVersion.ToString();
            }
            set {
                LanguageVersion = Version.Parse(value);
            }
        }
        
        /// <summary>
        /// Specifies a unique identifier for the interpreter. If one is not
        /// provided a new Guid will be created.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Specifies the Id of the language as a string.
        /// </summary>
        /// <exception cref="FormatException">
        /// The string cannot be parsed.
        /// </exception>
        public string IdString {
            get {
                return Id.ToString("B");
            }
            set {
                if (string.IsNullOrEmpty(value)) {
                    Id = Guid.Empty;
                } else {
                    Id = Guid.Parse(value);
                }
            }
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
        /// If true, a file system watcher is used to monitor the library path
        /// for changes. These may affect the IsCurrent property of the created
        /// interpreter.
        /// </summary>
        public bool WatchLibraryForNewModules { get; set; }

        /// <summary>
        /// Specifies the environment variable used for setting sys.path. Value
        /// must be a string; default is "PYTHONPATH".
        /// </summary>
        public string PathEnvironmentVariableName { get; set; }

        /// <summary>
        /// Specifies the processor architecture of the Python interpreter.
        /// The default is X86.
        /// </summary>
        public ProcessorArchitecture Architecture { get; set; }

        /// <summary>
        /// Gets or sets the architecture as either "x86" or "x64".
        /// </summary>
        public string ArchitectureString {
            get {
                return Architecture == ProcessorArchitecture.Amd64 ? "x64" : "x86";
            }
            set {
                if (!string.IsNullOrEmpty(value) && value.Equals("x64", StringComparison.InvariantCultureIgnoreCase)) {
                    Architecture = ProcessorArchitecture.Amd64;
                } else {
                    Architecture = ProcessorArchitecture.X86;
                }
            }
        }
    }
}

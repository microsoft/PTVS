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
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Options {
    /// <summary>
    /// Captures all of the options for an interpreter.  We can mutate this instance and then only when the user
    /// commits the changes do we propagate these back to an interpreter.
    /// </summary>
    class InterpreterOptions {
        private readonly PythonToolsService _pyService;
        private readonly IPythonInterpreterFactory _interpreter;

        public string Display;
        public Guid Id;
        public string InterpreterPath;
        public string WindowsInterpreterPath;
        public string LibraryPath;
        public string Architecture;
        public string Version;
        public string PathEnvironmentVariable;
        public bool Removed;
        public bool Added;
        public bool IsConfigurable;
        public bool SupportsCompletionDb;
        public IPythonInterpreterFactory Factory;
        public PythonInteractiveOptions InteractiveOptions;

        public InterpreterOptions(PythonToolsService pyService, IPythonInterpreterFactory interpreter) {
            _pyService = pyService;
            _interpreter = interpreter;
        }

        public void Load() {
            var configurable = _pyService._interpreterOptionsService;
            Debug.Assert(configurable != null);

            Display = _interpreter.Description;
            Id = _interpreter.Id;
            InterpreterPath = _interpreter.Configuration.InterpreterPath;
            WindowsInterpreterPath = _interpreter.Configuration.WindowsInterpreterPath;
            LibraryPath = _interpreter.Configuration.LibraryPath;
            Version = _interpreter.Configuration.Version.ToString();
            Architecture = FormatArchitecture(_interpreter.Configuration.Architecture);
            PathEnvironmentVariable = _interpreter.Configuration.PathEnvironmentVariable;
            IsConfigurable = configurable != null && configurable.IsConfigurable(_interpreter.Configuration.Id);
            SupportsCompletionDb = _interpreter is IPythonInterpreterFactoryWithDatabase;
            Factory = _interpreter;
        }

        private static string FormatArchitecture(ProcessorArchitecture arch) {
            switch (arch) {
                case ProcessorArchitecture.Amd64: return "x64";
                case ProcessorArchitecture.X86: return "x86";
                default: return "Unknown";
            }
        }
    }
}

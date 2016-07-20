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
        internal readonly InterpreterConfiguration _config;

        public string Display;
        public string Description;
        public string Id;
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

        public InterpreterOptions(PythonToolsService pyService, InterpreterConfiguration config) {
            _pyService = pyService;
            _config = config;
        }

        public void Load() {
            var configurable = _pyService._interpreterOptionsService;
            Debug.Assert(configurable != null);

            Display = _config.FullDescription;
            Description = _config.Description;
            Id = _config.Id;
            InterpreterPath = _config.InterpreterPath;
            WindowsInterpreterPath = _config.WindowsInterpreterPath;
            LibraryPath = _config.LibraryPath;
            Version = _config.Version.ToString();
            Architecture = FormatArchitecture(_config.Architecture);
            PathEnvironmentVariable = _config.PathEnvironmentVariable;
            IsConfigurable = configurable != null && configurable.IsConfigurable(_config.Id);
            SupportsCompletionDb = _config.UIMode.HasFlag(InterpreterUIMode.SupportsDatabase);
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

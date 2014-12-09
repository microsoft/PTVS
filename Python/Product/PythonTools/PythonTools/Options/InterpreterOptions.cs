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
            var configurable = _pyService._interpreterOptionsService.KnownProviders.OfType<ConfigurablePythonInterpreterFactoryProvider>().FirstOrDefault();
            Debug.Assert(configurable != null);

            Display = _interpreter.Description;
            Id = _interpreter.Id;
            InterpreterPath = _interpreter.Configuration.InterpreterPath;
            WindowsInterpreterPath = _interpreter.Configuration.WindowsInterpreterPath;
            LibraryPath = _interpreter.Configuration.LibraryPath;
            Version = _interpreter.Configuration.Version.ToString();
            Architecture = FormatArchitecture(_interpreter.Configuration.Architecture);
            PathEnvironmentVariable = _interpreter.Configuration.PathEnvironmentVariable;
            IsConfigurable = configurable != null && configurable.IsConfigurable(_interpreter);
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

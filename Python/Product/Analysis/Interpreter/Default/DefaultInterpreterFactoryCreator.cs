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
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Reflection;

namespace Microsoft.PythonTools.Interpreter.Default {
    [Export(typeof(IDefaultInterpreterFactoryCreator))]
    class DefaultInterpreterFactoryCreator : IDefaultInterpreterFactoryCreator {
        public IPythonInterpreterFactory CreateInterpreterFactory(Dictionary<InterpreterFactoryOptions, object> options) {
            object value;
            Version version = new Version(2, 7);
            if (options.TryGetValue(InterpreterFactoryOptions.Version, out value)) {
                if (value is string && Version.TryParse((string)value, out version)) {
                } else if (value is Version) {
                    version = (Version)value;
                } else {
                    throw new InvalidOperationException(String.Format("Bad version: {0}", value));
                }
            }

            Guid id;
            if (options.TryGetValue(InterpreterFactoryOptions.Guid, out value)) {
                if (value is string && Guid.TryParse((string)value, out id)) {
                } else if (value is Guid) {
                    id = (Guid)value;
                } else {
                    throw new InvalidOperationException(String.Format("Bad guid: {0}", value));
                }
            } else {
                id = Guid.NewGuid();
            }

            string description = "";
            if (options.TryGetValue(InterpreterFactoryOptions.Description, out value)) {
                if (value is string) {
                    description = (string)value;
                } else {
                    throw new InvalidOperationException(String.Format("Bad description: {0}", value));
                }
            }

            string pyPath = "";
            if (options.TryGetValue(InterpreterFactoryOptions.PythonPath, out value)) {
                if (value is string) {
                    pyPath = (string)value;
                } else {
                    throw new InvalidOperationException(String.Format("Bad Python path: {0}", value));
                }
            }

            string pyWindowsPath = "";
            if (options.TryGetValue(InterpreterFactoryOptions.PythonWindowsPath, out value)) {
                if (value is string) {
                    pyWindowsPath = (string)value;
                } else {
                    throw new InvalidOperationException(String.Format("Bad Python Windows path: {0}", value));
                }
            }

            string pyPathEnvVar = "PYTHONPATH";
            if (options.TryGetValue(InterpreterFactoryOptions.PathEnvVar, out value)) {
                if (value is string) {
                    pyPathEnvVar = (string)value;
                } else {
                    throw new InvalidOperationException(String.Format("Bad Python Windows path env var: {0}", value));
                }
            }

            ProcessorArchitecture arch = ProcessorArchitecture.X86;
            if (options.TryGetValue(InterpreterFactoryOptions.ProcessorArchitecture, out value)) {
                if (value is string && Enum.TryParse<ProcessorArchitecture>((string)value, true, out arch)) {
                } else if (value is ProcessorArchitecture) {
                    arch = (ProcessorArchitecture)value;
                } else {
                    throw new InvalidOperationException(String.Format("Bad processor architecture value: {0}", value));
                }
            }

            return new CPythonInterpreterFactory(
                version,
                id,
                description,
                pyPath,
                pyWindowsPath,
                pyPathEnvVar,
                arch
            );
        }
    }
}

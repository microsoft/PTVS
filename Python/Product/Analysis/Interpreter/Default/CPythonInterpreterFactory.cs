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
using System.IO;
using System.Reflection;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Interpreter.Default {
    class CPythonInterpreterFactory : PythonInterpreterFactoryWithDatabase {
        public CPythonInterpreterFactory(
            Version version,
            Guid id,
            string description,
            string prefixPath,
            string pythonPath,
            string pythonwPath,
            string libPath,
            string pathEnvVar,
            ProcessorArchitecture arch,
            bool watchForNewModules)
            : base(
                id,
                description,
                new InterpreterConfiguration(
                    prefixPath,
                    pythonPath,
                    pythonwPath,
                    libPath,
                    pathEnvVar,
                    arch,
                    version),
                watchForNewModules) { }

        public override bool AssumeSimpleLibraryLayout {
            get {
                return false;
            }
        }
    }
}

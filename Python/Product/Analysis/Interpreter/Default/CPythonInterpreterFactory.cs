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

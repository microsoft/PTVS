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

namespace Microsoft.PythonTools.Interpreter.Default {
    class CPythonInterpreterConfiguration : InterpreterConfiguration {
        private readonly string _pythonPath, _pythonwPath, _pathEnvVar;
        private readonly ProcessorArchitecture _arch;
        private readonly Version _version;
        
        public CPythonInterpreterConfiguration(string pythonPath, string pythonwPath, string pathEnvVar, ProcessorArchitecture arch, Version version) {
            _pythonPath = pythonPath;
            _pythonwPath = pythonwPath;
            _arch = arch;
            _version = version;
            _pathEnvVar = pathEnvVar;
        }

        public override string InterpreterPath {
            get { return _pythonPath; }
        }

        public override string WindowsInterpreterPath {
            get { return _pythonwPath; }
        }

        public override string PathEnvironmentVariable {
            get { return _pathEnvVar; }
        }

        public override ProcessorArchitecture Architecture {
            get { return _arch; }
        }

        public override Version Version {
            get { return _version; }
        }
    }
}

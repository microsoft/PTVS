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
using Microsoft.PythonTools.Interpreter;

namespace TestUtilities.Python {
    public class MockInterpreterConfiguration : InterpreterConfiguration {
        public MockInterpreterConfiguration(Version version) {
            _version = version;
        }

        public MockInterpreterConfiguration(string path, string winPath = null, string pathVar = "PYTHONPATH", ProcessorArchitecture arch = ProcessorArchitecture.X86, string version = "2.7") {
            _interpreterPath = path;
            _windowsInterpreterPath = winPath ?? path;
            _pathEnvironmentVariable = pathVar;
            _architecture = arch;
            _version = Version.Parse(version);
        }

        readonly string _interpreterPath;
        readonly string _windowsInterpreterPath;
        readonly string _pathEnvironmentVariable;
        readonly ProcessorArchitecture _architecture;
        readonly Version _version;

        public override string InterpreterPath {
            get { return _interpreterPath; }
        }

        public override string WindowsInterpreterPath {
            get { return _windowsInterpreterPath; }
        }

        public override string PathEnvironmentVariable {
            get { return _pathEnvironmentVariable; }
        }

        public override ProcessorArchitecture Architecture {
            get { return _architecture; }
        }

        public override Version Version {
            get { return _version; }
        }
    }
}

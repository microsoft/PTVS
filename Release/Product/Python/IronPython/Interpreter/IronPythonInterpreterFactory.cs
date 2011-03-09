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
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonInterpreterFactory : IPythonInterpreterFactory {
        private static readonly Guid _ipyInterpreterGuid = new Guid("{80659AB7-4D53-4E0C-8588-A766116CBD46}");
        private readonly InterpreterConfiguration _config = new IronPythonInterpreterConfiguration();

        public InterpreterConfiguration Configuration {
            get {
                return _config;
            }
        }

        public string Description {
            get { return "IronPython"; }
        }
        
        public Guid Id {
            get { return _ipyInterpreterGuid; }
        }

        public IPythonInterpreter CreateInterpreter() {
            return new IronPythonInterpreter();
        }

        void GenerateCompletionDatabase(GenerateDatabaseOptions options) {
        }

        class IronPythonInterpreterConfiguration : InterpreterConfiguration {
            public override string InterpreterPath {
                get { return Path.Combine(IronPythonInterpreter.GetPythonInstallDir(), "ipy.exe"); }
            }

            public override string WindowsInterpreterPath {
                get { return Path.Combine(IronPythonInterpreter.GetPythonInstallDir(), "ipyw.exe"); }
            }

            public override string PathEnvironmentVariable {
                get { return "IRONPYTHONPATH"; }
            }

            public override ProcessorArchitecture Architecture {
                get { return ProcessorArchitecture.MSIL; }
            }

            public override Version Version {
                get {
                    return new Version(2, 7, 0, 0);
                }
            }
        }
    }
}

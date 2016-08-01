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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonInterpreterFactory : PythonInterpreterFactoryWithDatabase {
        public IronPythonInterpreterFactory(InterpreterArchitecture arch)
            : base(GetConfiguration(arch), true) { }

        private static string GetInterpreterId(InterpreterArchitecture arch) {
            if (arch == InterpreterArchitecture.x64) {
                return "IronPython|2.7-64";
            } else {
                return "IronPython|2.7-32";
            }
        }

        internal static InterpreterConfiguration GetConfiguration(InterpreterArchitecture arch) {
            var prefixPath = IronPythonResolver.GetPythonInstallDir();
            return new InterpreterConfiguration(
                GetInterpreterId(arch),
                string.Format("IronPython 2.7{0: ()}", arch),
                prefixPath,
                Path.Combine(prefixPath, arch == InterpreterArchitecture.x64 ? "ipy64.exe" : "ipy.exe"),
                Path.Combine(prefixPath, arch == InterpreterArchitecture.x64 ? "ipyw64.exe" : "ipyw.exe"),
                "IRONPYTHONPATH",
                arch,
                new Version(2, 7),
                InterpreterUIMode.SupportsDatabase
            );
        }

        public override IPythonInterpreter MakeInterpreter(PythonInterpreterFactoryWithDatabase factory) {
            return new IronPythonInterpreter(factory);
        }
    }
}

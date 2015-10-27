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
        private static readonly Guid _ipyInterpreterGuid = new Guid("{80659AB7-4D53-4E0C-8588-A766116CBD46}");
        private static readonly Guid _ipy64InterpreterGuid = new Guid("{FCC291AA-427C-498C-A4D7-4502D6449B8C}");

        public IronPythonInterpreterFactory(ProcessorArchitecture arch = ProcessorArchitecture.X86)
            : base(
                arch == ProcessorArchitecture.Amd64 ? _ipy64InterpreterGuid : _ipyInterpreterGuid,
                arch == ProcessorArchitecture.Amd64 ? "IronPython 64-bit 2.7" : "IronPython 2.7",
                GetConfiguration(arch),
                true) { }

        private static InterpreterConfiguration GetConfiguration(ProcessorArchitecture arch) {
            var prefixPath = IronPythonResolver.GetPythonInstallDir();
            return new InterpreterConfiguration(
                prefixPath,
                Path.Combine(prefixPath, arch == ProcessorArchitecture.Amd64 ? "ipy64.exe" : "ipy.exe"),
                Path.Combine(prefixPath, arch == ProcessorArchitecture.Amd64 ? "ipyw64.exe" : "ipyw.exe"),
                Path.Combine(prefixPath, "Lib"),
                "IRONPYTHONPATH",
                arch,
                new Version(2, 7));
        }

        public override IPythonInterpreter MakeInterpreter(PythonInterpreterFactoryWithDatabase factory) {
            return new IronPythonInterpreter(factory);
        }

        public override bool AssumeSimpleLibraryLayout {
            get {
                return false;
            }
        }
    }
}

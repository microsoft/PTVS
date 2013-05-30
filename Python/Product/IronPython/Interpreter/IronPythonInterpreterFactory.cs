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
                arch == ProcessorArchitecture.Amd64 ? "IronPython 64-bit" : "IronPython",
                new InterpreterConfiguration(
                    Path.Combine(IronPythonResolver.GetPythonInstallDir(), arch == ProcessorArchitecture.Amd64 ? "ipy64.exe" : "ipy.exe"),
                    Path.Combine(IronPythonResolver.GetPythonInstallDir(), arch == ProcessorArchitecture.Amd64 ? "ipyw64.exe" : "ipyw.exe"),
                    Path.Combine(IronPythonResolver.GetPythonInstallDir(), "Lib"),
                    "IRONPYTHONPATH",
                    arch,
                    new Version(2, 7)),
                true) { }

        protected override IPythonInterpreter MakeInterpreter(PythonTypeDatabase typeDb) {
            return new IronPythonInterpreter(this);
        }
    }
}

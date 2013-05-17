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
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Debugger.Interop;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using System.IO;
using System.Net.Security;
using System.Text.RegularExpressions;

namespace Microsoft.PythonTools.Debugger.Remote {
    internal class PythonRemoteEnumDebugPrograms : PythonRemoteEnumDebug<IDebugProgram2>, IEnumDebugPrograms2 {

        public readonly PythonRemoteDebugProcess _process;

        public PythonRemoteEnumDebugPrograms(PythonRemoteDebugProcess process)
            : base(new PythonRemoteDebugProgram(process)) {
            this._process = process;
        }

        public int Clone(out IEnumDebugPrograms2 ppEnum) {
            ppEnum = new PythonRemoteEnumDebugPrograms(_process);
            return 0;
        }
    }
}

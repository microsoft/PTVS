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
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;

namespace Microsoft.PythonTools.Debugger.Remote {
    internal class PythonRemoteDebugPort : IDebugPort2 {
        private readonly PythonRemoteDebugPortSupplier _supplier;
        private readonly Guid _guid = Guid.NewGuid();
        private readonly string _hostName;
        private readonly ushort _portNumber;
        private readonly string _secret;
        private readonly bool _useSsl;

        public PythonRemoteDebugPort(PythonRemoteDebugPortSupplier supplier, string hostName, ushort portNumber, string secret, bool useSsl) {
            this._supplier = supplier;
            this._hostName = hostName;
            this._portNumber = portNumber;
            this._secret = secret;
            this._useSsl = useSsl;
        }

        public string HostName {
            get { return _hostName; }
        }

        public ushort PortNumber {
            get { return _portNumber; }
        }

        public string Secret {
            get { return _secret; }
        }

        public bool UseSsl {
            get { return _useSsl; }
        }

        public int EnumProcesses(out IEnumDebugProcesses2 ppEnum) {
            var process = PythonRemoteDebugProcess.Connect(this);
            if (process == null) {
                ppEnum = null;
                return VSConstants.E_FAIL;
            } else {
                ppEnum = new PythonRemoteEnumDebugProcesses(process);
                return VSConstants.S_OK;
            }
        }

        public int GetPortId(out Guid pguidPort) {
            pguidPort = _guid;
            return 0;
        }

        public int GetPortName(out string pbstrName) {
            pbstrName = _hostName + ":" + _portNumber;
            if (_secret != "") {
                pbstrName = _secret + "@" + pbstrName;
            }
            return VSConstants.S_OK;
        }

        public int GetPortRequest(out IDebugPortRequest2 ppRequest) {
            throw new NotImplementedException();
        }

        public int GetPortSupplier(out IDebugPortSupplier2 ppSupplier) {
            throw new NotImplementedException();
        }

        public int GetProcess(AD_PROCESS_ID ProcessId, out IDebugProcess2 ppProcess) {
            throw new NotImplementedException();
        }
    }
}

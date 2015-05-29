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
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.PythonTools.Debugger.Transports;
using Microsoft.PythonTools.DkmDebugger;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;

namespace Microsoft.PythonTools.Debugger.Remote {

    [ComVisible(true)]
    [Guid(Guids.RemoteDebugPortSupplierCLSID)]
    public class PythonRemoteDebugPortSupplier : IDebugPortSupplier2, IDebugPortSupplierDescription2 {
        public const string PortSupplierId = "{FEB76325-D127-4E02-B59D-B16D93D46CF5}";
        public static readonly Guid PortSupplierGuid = new Guid(PortSupplierId);

        // Qualifier for our transport has one of the following formats:
        //
        //   tcp[s]://[secret@]hostname[:port]
        //   ws[s]://[secret@]hostname[:port][/path]
        //   [secret@]hostname[:port]
        //
        // 'tcp' and 'tcps' are for connecting directly to ptvsd; 'ws' and 'wss' are for connecting through WebSocketProxy.
        // The versions ending with '...s' use SSL to secure the connection. If no scheme is specified, 'tcp' is the default.
        // If port is not specified, it defaults to 5678 for 'tcp' and 'tcps', 80 for 'ws' and 443 for 'wss'.
        public int AddPort(IDebugPortRequest2 pRequest, out IDebugPort2 ppPort) {
            ppPort = null;

            string name;
            pRequest.GetPortName(out name);

            // Support old-style 'hostname:port' format, as well.
            if (!name.Contains("://")) {
                name = "tcp://" + name;
            }

            var uri = new Uri(name, UriKind.Absolute);
            var transport = DebuggerTransportFactory.Get(uri);
            if (transport == null) {
                MessageBox.Show(string.Format("Unrecognized remote debugging transport '{0}'.", uri.Scheme), null, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return VSConstants.E_FAIL;
            }

            var validationError = transport.Validate(uri);
            if (validationError != null) {
                return validationError.HResult;
            }

            var port = new PythonRemoteDebugPort(this, pRequest, uri);

            // Validate connection early. Debugger automation (DTE) objects are not consistent in error checking from this
            // point on, so errors reported from EnumProcesses and further calls may be ignored and treated as successes
            // (with empty result). Reporting an error here works around that.
            IEnumDebugProcesses2 processes;
            int hr = port.EnumProcesses(out processes);
            if (hr < 0) {
                return hr;
            }

            ppPort = port;
            return VSConstants.S_OK;
        }

        public int CanAddPort() {
            return VSConstants.S_OK; // S_OK = true, S_FALSE = false
        }

        public int EnumPorts(out IEnumDebugPorts2 ppEnum) {
            ppEnum = null;
            return VSConstants.S_OK;
        }

        public int GetPort(ref Guid guidPort, out IDebugPort2 ppPort) {
            ppPort = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetPortSupplierId(out Guid pguidPortSupplier) {
            pguidPortSupplier = PortSupplierGuid;
            return VSConstants.S_OK;
        }

        public int GetPortSupplierName(out string pbstrName) {
            pbstrName = "Python remote (ptvsd)";
            return VSConstants.S_OK;
        }

        public int RemovePort(IDebugPort2 pPort) {
            return VSConstants.S_OK;
        }

        public int GetDescription(enum_PORT_SUPPLIER_DESCRIPTION_FLAGS[] pdwFlags, out string pbstrText) {
            pbstrText =
                "Allows debugging a Python process on a remote machine running any OS, if it can be connected to via TCP, " +
                "and remote debugging has been enabled by using the 'ptvsd' module. " +
                "Specify the secret, hostname and port to connect to in the 'Qualifier' textbox, e.g. 'tcp://secret@localhost:5678'. ";
            return VSConstants.S_OK;
        }
    }
}

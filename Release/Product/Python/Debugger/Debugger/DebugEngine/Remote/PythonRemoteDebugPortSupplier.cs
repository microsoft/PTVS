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
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Debugger.Interop;

namespace Microsoft.PythonTools.Debugger.Remote {
    public abstract class PythonRemoteDebugPortSupplier : IDebugPortSupplier2, IDebugPortSupplierDescription2 {
        // Qualifier for our transport is parsed as 'secret@hostname:port', where 'secret@' and ':port' are both optional.
        private static readonly Regex _portNameRegex = new Regex(@"^((?<secret>.+?)@)?(?<hostName>.+?)(:(?<portNum>\d+))?$", RegexOptions.ExplicitCapture);

        private readonly Guid _guid;
        private readonly ushort _defaultPort;
        private readonly bool _useSsl;

        protected PythonRemoteDebugPortSupplier(Guid guid, ushort defaultPort, bool useSsl) {
            _guid = guid;
            _defaultPort = defaultPort;
            _useSsl = useSsl;
        }

        public int AddPort(IDebugPortRequest2 pRequest, out IDebugPort2 ppPort) {
            ppPort = null;

            string name;
            pRequest.GetPortName(out name);

            Match m = _portNameRegex.Match(name);
            if (!m.Success) {
                return new FormatException().HResult;
            }

            string secret = m.Groups["secret"].Value;
            string hostName = m.Groups["hostName"].Value;

            ushort portNum = _defaultPort;
            if (m.Groups["portNum"].Success) {
                if (!ushort.TryParse(m.Groups["portNum"].Value, out portNum)) {
                    return new FormatException().HResult;
                }
            }

            var port = new PythonRemoteDebugPort(this, hostName, portNum, secret, _useSsl);
            ppPort = port;
            return 0;
        }

        public int CanAddPort() {
            return 0; // S_OK = true, S_FALSE = false
        }

        public int EnumPorts(out IEnumDebugPorts2 ppEnum) {
            ppEnum = null;
            return 0;
        }

        public int GetPort(ref Guid guidPort, out IDebugPort2 ppPort) {
            throw new NotImplementedException();
        }

        public int GetPortSupplierId(out Guid pguidPortSupplier) {
            pguidPortSupplier = _guid;
            return 0;
        }

        public int GetPortSupplierName(out string pbstrName) {
            pbstrName = "Python remote debugging " + (_useSsl ? "(SSL)" : "(unsecured)");
            return 0;
        }

        public int RemovePort(IDebugPort2 pPort) {
            return 0;
        }

        public int GetDescription(enum_PORT_SUPPLIER_DESCRIPTION_FLAGS[] pdwFlags, out string pbstrText) {
            pbstrText =
                "Allows debugging a Python process on a remote machine running any OS, if it can be connected to via TCP, " +
                "and the process has enabled remote debugging by importing 'ptvsd' module and invoking 'ptvsd.enable_attach()'. " +
                "Specify the secret, hostname and port to connect to in the 'Qualifier' textbox, e.g. 'secret@localhost:5678'. ";
            if (!_useSsl) {
                pbstrText += "This transport is not secure, and should not be used on a network that might have hostile traffic.";
            }
            return 0;
        }
    }


    [ComVisible(true)]
    [Guid("B8CBA3DE-4A20-4DD7-8709-EC66A6A256D3")]
    public class PythonRemoteDebugPortSupplierUnsecured : PythonRemoteDebugPortSupplier {
        public const string PortSupplierId = "{FEB76325-D127-4E02-B59D-B16D93D46CF5}";
        public static readonly Guid PortSupplierGuid = new Guid(PortSupplierId);
        public const ushort DefaultPort = 5678;

        public PythonRemoteDebugPortSupplierUnsecured()
            : base(PortSupplierGuid, DefaultPort, useSsl: false) {
        }
    }


    [ComVisible(true)]
    [Guid("994FA2E5-CA7B-4FF5-80F8-331766A2C663")]
    public class PythonRemoteDebugPortSupplierSsl : PythonRemoteDebugPortSupplier {
        public const string PortSupplierId = "{9110921B-1371-4C33-8844-C5601F503390}";
        public static readonly Guid PortSupplierGuid = new Guid(PortSupplierId);
        public const ushort DefaultPort = 5678;

        public PythonRemoteDebugPortSupplierSsl()
            : base(PortSupplierGuid, DefaultPort, useSsl: true) {
        }
    }
}

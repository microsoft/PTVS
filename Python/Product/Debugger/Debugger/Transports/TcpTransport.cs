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
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Microsoft.PythonTools.Debugger.Transports {
    internal class TcpTransport : IDebuggerTransport {
        public const ushort DefaultPort = 5678;

        public Exception Validate(Uri uri) {
            if (uri.AbsolutePath != "/") {
                return new FormatException("tcp:// URI cannot contain a path");
            }
            return null;
        }

        public virtual Stream Connect(Uri uri, bool requireAuthentication) {
            if (uri.Port < 0) {
                uri = new UriBuilder(uri) { Port = DefaultPort }.Uri;
            }

            // PTVSD is using AF_INET by default, so lets make sure to try the IPv4 address in lieu of IPv6 address
            var tcpClient = new TcpClient(AddressFamily.InterNetwork);
            
            try {
                tcpClient.Connect(uri.Host, uri.Port);
                var stream = tcpClient.GetStream();
                tcpClient = null;
                return stream;
            } catch (IOException ex) {
                throw new ConnectionException(ConnErrorMessages.RemoteNetworkError, ex);
            } finally {
                if (tcpClient != null) {
                    tcpClient.Close();
                }
            }
        }
    }
}

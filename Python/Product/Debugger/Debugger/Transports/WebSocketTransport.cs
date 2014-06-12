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
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Debugger.Transports {
    internal class WebSocketTransport : IDebuggerTransport {
        public Exception Validate(Uri uri) {
            return null;
        }

        public virtual Stream Connect(Uri uri, bool requireAuthentication) {
            // IIS starts python.exe processes lazily on the first incoming request, and will terminate them after a period
            // of inactivity, making it impossible to attach. So before trying to connect to the debugger, "ping" the website
            // via HTTP to ensure that we have something to connect to.
            try {
                var httpRequest = WebRequest.Create(new UriBuilder(uri) { Scheme = "http", Port = -1, Path = "/" }.Uri);
                httpRequest.Method = WebRequestMethods.Http.Head;
                httpRequest.Timeout = 5000;
                httpRequest.GetResponse().Dispose();
            } catch (WebException) {
                // If it fails or times out, just go ahead and try to connect anyway, and rely on normal error reporting path.
            }

            var webSocket = new ClientWebSocket();
            try {
                webSocket.ConnectAsync(uri, CancellationToken.None).GetAwaiter().GetResult();
                var stream = new WebSocketStream(webSocket, ownsSocket: true);
                webSocket = null;
                return stream;
            } catch (WebSocketException ex) {
                throw new ConnectionException(ConnErrorMessages.RemoteNetworkError, ex);
            } catch (IOException ex) {
                throw new ConnectionException(ConnErrorMessages.RemoteNetworkError, ex);
            } catch (PlatformNotSupportedException ex) {
                throw new ConnectionException(ConnErrorMessages.RemoteUnsupportedTransport, ex);
            } finally {
                if (webSocket != null) {
                    webSocket.Dispose();
                }
            }
        }
    }
}

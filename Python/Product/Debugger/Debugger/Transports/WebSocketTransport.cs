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

#if DEV11_OR_LATER

using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Debugger.Transports {
    internal class WebSocketTransport : IDebuggerTransport {
        public Exception Validate(Uri uri) {
            return null;
        }

        public virtual Stream Connect(Uri uri, bool requireAuthentication) {
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
            } finally {
                if (webSocket != null) {
                    webSocket.Dispose();
                }
            }
        }
    }
}

#endif
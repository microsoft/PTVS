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
using System.Security.Authentication;

namespace Microsoft.PythonTools.Debugger.Transports {
    internal interface IDebuggerTransport {
        /// <summary>
        /// Validates the remote debugging endpoint URI for correctness.
        /// </summary>
        /// <returns><c>null</c> if <paramref name="uri"/> is valid, otherwise an exception object representing the validation errors.</returns>
        Exception Validate(Uri uri);

        /// <summary>
        /// Establishes a connection to a remote debugging endpoint and returns a stream over which debugging commands can be issued.
        /// </summary>
        /// <param name="requireAuthentication">
        /// If <c>true</c>, the remote endpoint must be properly authenticated if the protocol permits it (e.g. for SSL, it must present
        /// a valid trusted certificate). If <c>false</c>, any server authentication errors are ignored.
        /// </param>
        /// <exception cref="AuthenticationException">
        /// Thrown if the remote endpoint could not be authenticated, and <paramref name="requireAuthentication"/> is <c>true</c>.
        /// </exception>
        /// <exception cref="ConnectionException">
        /// Thrown for all connection issues that have an associated <see cref="ConnErrorMessages"/> code.
        /// </exception>
        /// <remarks>
        /// If the transport does not support authentication, <paramref name="requireAuthentication"/> is ignored.
        /// </remarks>
        Stream Connect(Uri uri, bool requireAuthentication);
    }

    internal static class DebuggerTransportFactory {
        private static readonly Dictionary<string, Func<IDebuggerTransport>> _factories = new Dictionary<string, Func<IDebuggerTransport>> {
            { "tcp", () => new TcpTransport() },
            { "tcps", () => new TcpsTransport() },
#if DEV11_OR_LATER
            { "ws", () => new WebSocketTransport() },
            { "wss", () => new WebSocketTransport() },
#endif
        };
                                                                                            

        /// <returns>
        /// An <see cref="IDebuggerTransport"/> that can validate and connect to <paramref name="uri"/>, or <c>null</c> if there is no such transport.
        /// </returns>
        public static IDebuggerTransport Get(Uri uri) {
            Func<IDebuggerTransport> factory;
            if (_factories.TryGetValue(uri.Scheme, out factory)) {
                return factory();
            } else {
                return null;
            }
        }
    }
}

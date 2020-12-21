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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

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
            { "ws", () => new WebSocketTransport() },
            { "wss", () => new WebSocketTransport() },
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

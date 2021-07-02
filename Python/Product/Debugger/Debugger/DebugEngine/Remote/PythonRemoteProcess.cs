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
using System.IO;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.PythonTools.Debugger.Transports;
using Microsoft.PythonTools.Parsing;
using LDP = Microsoft.PythonTools.Debugger.LegacyDebuggerProtocol;

namespace Microsoft.PythonTools.Debugger.Remote {
    internal class PythonRemoteProcess : PythonProcess {
        public const byte DebuggerProtocolVersion = 8; // must be kept in sync with PTVSDBG_VER in attach_server.py
        public const string DebuggerSignature = "PTVSDBG";
        private const int ConnectTimeoutMs = 5000;

        private PythonRemoteProcess(int pid, Uri uri, PythonLanguageVersion langVer, TextWriter debugLog)
            : base(pid, langVer, debugLog) {
            Uri = uri;
            ParseQueryString();
        }

        public Uri Uri { get; private set; }

        internal string TargetHostType { get; private set; }

        private void ParseQueryString() {
            if (Uri != null && Uri.Query != null) {
                var queryParts = HttpUtility.ParseQueryString(Uri.Query);

                TargetHostType = queryParts[AD7Engine.TargetHostType];

                var sourceDir = queryParts[AD7Engine.SourceDirectoryKey];
                var targetDir = queryParts[AD7Engine.TargetDirectoryKey];
                if (!string.IsNullOrWhiteSpace(sourceDir) && !string.IsNullOrWhiteSpace(targetDir)) {
                    AddDirMapping(new string[] { sourceDir, targetDir });
                }
            }
        }

        /// <summary>
        /// Connects to and performs the initial handshake with the remote debugging server, verifying protocol signature and version number,
        /// and returns the opened stream in a state ready to receive further ptvsd commands (e.g. attach).
        /// </summary>
        public static async Task<DebugConnection> ConnectAsync(Uri uri, bool warnAboutAuthenticationErrors, TextWriter debugLog, CancellationToken ct) {
            var transport = DebuggerTransportFactory.Get(uri);
            if (transport == null) {
                throw new ConnectionException(ConnErrorMessages.RemoteInvalidUri);
            }

            Stream stream = null;
            do {
                try {
                    stream = transport.Connect(uri, warnAboutAuthenticationErrors);
                } catch (AuthenticationException ex) {
                    if (!warnAboutAuthenticationErrors) {
                        // This should never happen, but if it does, we don't want to keep trying.
                        throw new ConnectionException(ConnErrorMessages.RemoteSslError, ex);
                    }

                    string errText = Strings.RemoteProcessAuthenticationErrorWarning.FormatUI(ex.Message);
                    var dlgRes = MessageBox.Show(errText, Strings.ProductTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (dlgRes == DialogResult.Yes) {
                        warnAboutAuthenticationErrors = false;
                    } else {
                        throw new ConnectionException(ConnErrorMessages.RemoteSslError, ex);
                    }
                }
            } while (stream == null);

            var debugConn = new DebugConnection(stream, debugLog);
            bool connected = false;
            try {
                string serverDebuggerName = string.Empty;
                int serverDebuggerProtocolVersion = 0;
                using (var connectedEvent = new AutoResetEvent(false)) {
                    EventHandler<LDP.RemoteConnectedEvent> eventReceived = (object sender, LDP.RemoteConnectedEvent ea) => {
                        serverDebuggerName = ea.debuggerName;
                        serverDebuggerProtocolVersion = ea.debuggerProtocolVersion;
                        try {
                            connectedEvent.Set();
                        } catch (ObjectDisposedException) {
                        }
                        debugConn.Authenticated();
                    };

                    // When the server accepts a connection, it sends an event and then waits for a request
                    debugConn.LegacyRemoteConnected += eventReceived;
                    try {
                        debugConn.StartListening();
                        bool received = connectedEvent.WaitOne(ConnectTimeoutMs);
                        if (!received) {
                            throw new ConnectionException(ConnErrorMessages.TimeOut);
                        }
                    } finally {
                        debugConn.LegacyRemoteConnected -= eventReceived;
                    }
                }

                if (serverDebuggerName != DebuggerSignature) {
                    throw new ConnectionException(ConnErrorMessages.RemoteUnsupportedServer);
                }

                // If we are talking the same protocol but different version, reply with signature + version before bailing out
                // so that ptvsd has a chance to gracefully close the socket on its side. 
                var response = await debugConn.SendRequestAsync(new LDP.RemoteDebuggerAuthenticateRequest() {
                    clientSecret = uri.UserInfo,
                    debuggerName = DebuggerSignature,
                    debuggerProtocolVersion = DebuggerProtocolVersion,
                }, ct);

                if (serverDebuggerProtocolVersion != DebuggerProtocolVersion) {
                    throw new ConnectionException(ConnErrorMessages.RemoteUnsupportedServer);
                }

                if (!response.accepted) {
                    throw new ConnectionException(ConnErrorMessages.RemoteSecretMismatch);
                }

                connected = true;
                return debugConn;
            } catch (IOException ex) {
                throw new ConnectionException(ConnErrorMessages.RemoteNetworkError, ex);
            } catch (SocketException ex) {
                throw new ConnectionException(ConnErrorMessages.RemoteNetworkError, ex);
            } finally {
                if (!connected) {
                    debugConn?.Dispose();
                }
            }
        }

        /// <summary>
        /// Same as the static version of this method, but uses the same <c>uri</c>
        /// that was originally used to create this instance of <see cref="PythonRemoteProcess"/>.
        /// </summary>
        public async Task<DebugConnection> ConnectAsync(bool warnAboutAuthenticationErrors, TextWriter debugLog, CancellationToken ct) {
            return await ConnectAsync(Uri, warnAboutAuthenticationErrors, debugLog, ct);
        }

        public static async Task<PythonProcess> AttachAsync(Uri uri, bool warnAboutAuthenticationErrors, TextWriter debugLog, CancellationToken ct) {
            string debugOptions = "";
            if (uri.Query != null) {
                // It is possible to have more than one opt=... in the URI - the debug engine will always supply one based on
                // the options that it gets from VS, but the user can also explicitly specify it directly in the URI when doing
                // ptvsd attach (this is currently the only way to enable some flags when attaching, e.g. Django debugging).
                // ParseQueryString will automatically concat them all into a single value using commas.
                var queryParts = HttpUtility.ParseQueryString(uri.Query);
                debugOptions = queryParts[AD7Engine.DebugOptionsKey] ?? "";
            }

            var debugConn = await ConnectAsync(uri, warnAboutAuthenticationErrors, debugLog, ct);
            try {
                var response = await debugConn.SendRequestAsync(new LDP.RemoteDebuggerAttachRequest() {
                    debugOptions = debugOptions,
                });

                if (!response.accepted) {
                    throw new ConnectionException(ConnErrorMessages.RemoteAttachRejected);
                }

                var langVer = (PythonLanguageVersion)((response.pythonMajor << 8) | response.pythonMinor);
                if (!Enum.IsDefined(typeof(PythonLanguageVersion), langVer)) {
                    langVer = PythonLanguageVersion.None;
                }

                var process = new PythonRemoteProcess(response.processId, uri, langVer, debugLog);
                process.Connect(debugConn);
                debugConn = null;
                return process;
            } catch (IOException ex) {
                throw new ConnectionException(ConnErrorMessages.RemoteNetworkError, ex);
            } finally {
                if (debugConn != null) {
                    debugConn.Dispose();
                }
            }
        }
    }
}

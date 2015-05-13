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
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Web;
using System.Windows.Forms;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.PythonTools.Debugger.Transports;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Debugger.Remote {
    internal class PythonRemoteProcess : PythonProcess {
        public const byte DebuggerProtocolVersion = 5; // must be kept in sync with PTVSDBG_VER in attach_server.py
        public const string DebuggerSignature = "PTVSDBG";
        public const string Accepted = "ACPT";
        public const string Rejected = "RJCT";
        public static readonly byte[] DebuggerSignatureBytes = Encoding.ASCII.GetBytes(DebuggerSignature);
        public static readonly byte[] InfoCommandBytes = Encoding.ASCII.GetBytes("INFO");
        public static readonly byte[] AttachCommandBytes = Encoding.ASCII.GetBytes("ATCH");
        public static readonly byte[] ReplCommandBytes = Encoding.ASCII.GetBytes("REPL");

        private PythonRemoteProcess(int pid, Uri uri, PythonLanguageVersion langVer)
            : base(pid, langVer) {
            Uri = uri;
            ParseQueryString();
        }

        public Uri Uri { get; private set; }

        internal string TargetHostType { get; private set; }

        internal void ParseQueryString() {
            if (Uri != null && Uri.Query != null) {
                var queryParts = HttpUtility.ParseQueryString(Uri.Query);

                var sourceDir = queryParts[AD7Engine.SourceDirectoryKey];
                var targetDir = queryParts[AD7Engine.TargetDirectoryKey];

                TargetHostType = queryParts[AD7Engine.TargetHostType];

                if (!string.IsNullOrWhiteSpace(sourceDir) && !string.IsNullOrWhiteSpace(targetDir))
                    AddDirMapping(new string[] { sourceDir, targetDir });
            }
        }

        /// <summary>
        /// Connects to and performs the initial handshake with the remote debugging server, verifying protocol signature and version number,
        /// and returns the opened stream in a state ready to receive further ptvsd commands (e.g. attach).
        /// </summary>
        public static Stream Connect(Uri uri, bool warnAboutAuthenticationErrors) {
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

                    string errText = ex.Message + "\nConnect anyway?";
                    var dlgRes = MessageBox.Show(errText, null, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (dlgRes == DialogResult.Yes) {
                        warnAboutAuthenticationErrors = false;
                    } else {
                        throw new ConnectionException(ConnErrorMessages.RemoteSslError, ex);
                    }
                }
            } while (stream == null);

            bool connected = false;
            try {
                string sig = stream.ReadAsciiString(DebuggerSignature.Length);
                if (sig != DebuggerSignature) {
                    throw new ConnectionException(ConnErrorMessages.RemoteUnsupportedServer);
                }

                long ver = stream.ReadInt64();

                // If we are talking the same protocol but different version, reply with signature + version before bailing out
                // so that ptvsd has a chance to gracefully close the socket on its side. 
                stream.Write(DebuggerSignatureBytes);
                stream.WriteInt64(DebuggerProtocolVersion);

                if (ver != DebuggerProtocolVersion) {
                    throw new ConnectionException(ConnErrorMessages.RemoteUnsupportedServer);
                }

                stream.WriteString(uri.UserInfo);
                string secretResp = stream.ReadAsciiString(Accepted.Length);
                if (secretResp != Accepted) {
                    throw new ConnectionException(ConnErrorMessages.RemoteSecretMismatch);
                }

                connected = true;
                return stream;
            } catch (IOException ex) {
                throw new ConnectionException(ConnErrorMessages.RemoteNetworkError, ex);
            } catch (SocketException ex) {
                throw new ConnectionException(ConnErrorMessages.RemoteNetworkError, ex);
            } finally {
                if (!connected) {
                    if (stream != null) {
                        stream.Close();
                    }
                }
            }
        }

        /// <summary>
        /// Same as the static version of this method, but uses the same <c>uri</c>
        /// that was originally used to create this instance of <see cref="PythonRemoteProcess"/>.
        /// </summary>
        public Stream Connect(bool warnAboutAuthenticationErrors) {
            return Connect(Uri, warnAboutAuthenticationErrors);
        }

        public static PythonProcess Attach(Uri uri, bool warnAboutAuthenticationErrors) {
            var stream = Connect(uri, warnAboutAuthenticationErrors);
            try {
                stream.Write(AttachCommandBytes);

                string attachResp = stream.ReadAsciiString(Accepted.Length);
                if (attachResp != Accepted) {
                    throw new ConnectionException(ConnErrorMessages.RemoteAttachRejected);
                }

                int pid = stream.ReadInt32();
                int langMajor = stream.ReadInt32();
                int langMinor = stream.ReadInt32();
                int langMicro = stream.ReadInt32();
                var langVer = (PythonLanguageVersion)((langMajor << 8) | langMinor);
                if (!Enum.IsDefined(typeof(PythonLanguageVersion), langVer)) {
                    langVer = PythonLanguageVersion.None;
                }

                var process = new PythonRemoteProcess(pid, uri, langVer);
                process.Connect(stream);
                stream = null;
                return process;
            } catch (IOException ex) {
                throw new ConnectionException(ConnErrorMessages.RemoteNetworkError, ex);
            } finally {
                if (stream != null) {
                    stream.Close();
                }
            }
        }
    }
}


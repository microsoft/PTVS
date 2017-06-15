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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using LDP = Microsoft.PythonTools.Debugger.LegacyDebuggerProtocol;

namespace Microsoft.PythonTools.Debugger {
    /// <summary>
    /// Handles connections from all debuggers.
    /// </summary>
    static class DebugConnectionListener {
        private static int _listenerPort = -1;
        private static readonly Dictionary<Guid, WeakReference> _targets = new Dictionary<Guid, WeakReference>();
        private const int ConnectTimeoutMs = 5000;

        public static void RegisterProcess(Guid id, PythonProcess process) {
            lock (_targets) {
                EnsureListenerSocket();

                _targets[id] = new WeakReference(process);
            }
        }

        public static int ListenerPort {
            get {
                lock (_targets) {
                    EnsureListenerSocket();
                }

                return _listenerPort;
            }
        }

        private static void EnsureListenerSocket() {
            if (_listenerPort < 0) {
                var socketSource = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                socketSource.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                socketSource.Listen(0);
                _listenerPort = ((IPEndPoint)socketSource.LocalEndPoint).Port;
                Debug.WriteLine("Listening for debug connections on port {0}", _listenerPort);
                socketSource.BeginAccept(AcceptConnection, socketSource);
            }
        }

        public static void UnregisterProcess(Guid id) {
            lock (_targets) {
                _targets.Remove(id);
            }
        }

        public static PythonProcess GetProcess(Guid id) {
            WeakReference value;
            lock (_targets) {
                if (_targets.TryGetValue(id, out value)) {
                    return (PythonProcess)value.Target;
                }
            }
            return null;
        }

        private static void AcceptConnection(IAsyncResult iar) {
            Socket socket = null;
            var socketSource = ((Socket)iar.AsyncState);
            try {
                socket = socketSource.EndAccept(iar);
            } catch (SocketException ex) {
                Debug.WriteLine("DebugConnectionListener socket failed");
                Debug.WriteLine(ex);
            } catch (ObjectDisposedException) {
                Debug.WriteLine("DebugConnectionListener socket closed");
            }

            if (socket != null) {
                var stream = new NetworkStream(socket, ownsSocket: true);
                try {
                    socket.Blocking = true;

                var debugConn = new DebugConnection(stream);
                try {
                    string debugId = string.Empty;
                    var result = ConnErrorMessages.None;
                    using (var connectedEvent = new AutoResetEvent(false)) {
                        EventHandler<LDP.LocalConnectedEvent> eventReceived = (object sender, LDP.LocalConnectedEvent ea) => {
                            result = (ConnErrorMessages)ea.result;
                            debugId = ea.processGuid;
                            debugConn.Authenticated();
                            try {
                                connectedEvent.Set();
                            } catch (ObjectDisposedException) {
                            }
                        };

                            debugConn.LegacyLocalConnected += eventReceived;
                            try {
                                debugConn.StartListening();
                                bool received = connectedEvent.WaitOne(ConnectTimeoutMs);
                                if (!received) {
                                    throw new TimeoutException();
                                }
                            } finally {
                                debugConn.LegacyLocalConnected -= eventReceived;
                            }
                        }

                        lock (_targets) {
                            Guid debugGuid;
                            WeakReference weakProcess;
                            PythonProcess targetProcess;

                            if (Guid.TryParse(debugId, out debugGuid) &&
                                _targets.TryGetValue(debugGuid, out weakProcess) &&
                                (targetProcess = weakProcess.Target as PythonProcess) != null) {

                                if (result == ConnErrorMessages.None) {
                                    targetProcess.Connect(debugConn);
                                    stream = null;
                                    socket = null;
                                    debugConn = null;
                                } else {
                                    WriteErrorToOutputWindow(result, targetProcess);
                                    targetProcess.Unregister();
                                }
                            } else {
                                Debug.WriteLine("Unknown debug target: {0}", debugId);
                            }
                        }
                    } finally {
                        debugConn?.Dispose();
                    }
                } catch (IOException) {
                } catch (SocketException) {
                } catch (TimeoutException) {
                } catch (Exception ex) {
                    Environment.FailFast("DebugConnectionListener.AcceptConnection", ex);
                } finally {
                    stream?.Dispose();
                    socket?.Dispose();
                }
            }

            socketSource.BeginAccept(AcceptConnection, socketSource);
        }

        private static void WriteErrorToOutputWindow(ConnErrorMessages result, PythonProcess targetProcess) {
            var outWin = (IVsOutputWindow)Package.GetGlobalService(typeof(IVsOutputWindow));

            IVsOutputWindowPane pane;
            if (outWin != null && ErrorHandler.Succeeded(outWin.GetPane(VSConstants.GUID_OutWindowDebugPane, out pane))) {
                pane.Activate();
                string moduleName;
                try {
                    moduleName = Process.GetProcessById(targetProcess.Id).MainModule.ModuleName;
                } catch {
                    // either the process is no longer around, or it's a 64-bit process
                    // and we can't get the EXE name.
                    moduleName = null;
                }

                if (moduleName != null) {
                    pane.OutputString(Strings.DebugConnectionFailedToConnectToProcessWithModule.FormatUI(
                        targetProcess.Id,
                        moduleName,
                        result.GetErrorMessage())
                    );
                } else {
                    pane.OutputString(Strings.DebugConnectionFailedToConnectToProcessWithoutModule.FormatUI(
                        targetProcess.Id,
                        result.GetErrorMessage())
                    );
                }
            }
        }
    }
}

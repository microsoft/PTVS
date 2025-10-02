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
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Logging;
using Microsoft.VisualStudio.Debugger.DebugAdapterHost.Interfaces;
using System.Text;

namespace Microsoft.PythonTools.Debugger {
    internal sealed class DebugAdapterRemoteProcess : ITargetHostProcess, IDisposable {
        private const int _debuggerConnectionTimeout = 45000; // extended to 45 seconds to allow loader retries
        private DebugRemoteAdapterProcessStream _stream;
        private bool _debuggerConnected = false;

        private DebugAdapterRemoteProcess() { }

        public static ITargetHostProcess Attach(DebugTcpAttachInfo debugAttachInfo) {
            if (debugAttachInfo == null) {
                Debug.WriteLine("[PTVS][DebugAdapterRemoteProcess] Attach called with null DebugTcpAttachInfo");
                return null;
            }

            // If RemoteUri was not populated by the caller, construct it from Host/Port.
            if (debugAttachInfo.RemoteUri == null) {
                if (!string.IsNullOrEmpty(debugAttachInfo.Host) && debugAttachInfo.Port > 0) {
                    try {
                        // Use a synthetic tcp scheme; only Host/Port/IsLoopback are consumed.
                        debugAttachInfo.RemoteUri = new Uri($"tcp://{debugAttachInfo.Host}:{debugAttachInfo.Port}");
                    } catch (Exception ex) {
                        Debug.WriteLine($"[PTVS][DebugAdapterRemoteProcess] Failed to build RemoteUri: {ex.Message}");
                        return null;
                    }
                } else {
                    Debug.WriteLine("[PTVS][DebugAdapterRemoteProcess] Insufficient host/port to build RemoteUri");
                    return null;
                }
            }

            var attachedProcess = new DebugAdapterRemoteProcess();
            return attachedProcess.ConnectSocket(debugAttachInfo.RemoteUri) ? attachedProcess : null;
        }

        private bool ConnectSocket(Uri uri) {
            if (uri == null) {
                Debug.WriteLine("[PTVS][DebugAdapterRemoteProcess] ConnectSocket received null Uri");
                return false;
            }
            _debuggerConnected = false;
            var logger = (IPythonToolsLogger)VisualStudio.Shell.ServiceProvider.GlobalProvider.GetService(typeof(IPythonToolsLogger));

            string portFilePath = Environment.GetEnvironmentVariable("PTVS_DEBUG_PORT_FILE");
            int originalPort = uri.Port;

            EndPoint BuildEndpoint(int port) {
                if (uri.IsLoopback) return new IPEndPoint(IPAddress.Loopback, port);
                return new DnsEndPoint(uri.Host, port);
            }

            int currentPort = uri.Port;
            var endpoint = BuildEndpoint(currentPort);
            Debug.WriteLine($"[PTVS][DebugAdapterRemoteProcess] Begin connect host={uri.Host} initialPort={currentPort} timeout={_debuggerConnectionTimeout}ms portFile={(string.IsNullOrEmpty(portFilePath)?"<none>":portFilePath)}");

            var sw = Stopwatch.StartNew();
            int attempt = 0;
            Socket socket = null;
            const int backoffInitialMs = 80;
            const int backoffMaxMs = 600; // slight increase due to longer overall timeout
            // Initial delay to allow target loader to import and start listener.
            Debug.WriteLine("[PTVS][DebugAdapterRemoteProcess] Initial wait 300ms before first connect attempt");
            System.Threading.Thread.Sleep(300);
            while (sw.ElapsedMilliseconds < _debuggerConnectionTimeout) {
                attempt++;
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try {
                    Debug.WriteLine($"[PTVS][DebugAdapterRemoteProcess] Attempt {attempt} connecting to {uri.Host}:{currentPort} elapsed={sw.ElapsedMilliseconds}ms");
                    socket.Connect(endpoint);
                    if (socket.Connected) {
                        Debug.WriteLine($"[PTVS][DebugAdapterRemoteProcess] SUCCESS attempt={attempt} elapsed={sw.ElapsedMilliseconds}ms port={currentPort}");
                        _debuggerConnected = true;
                        _stream = new DebugRemoteAdapterProcessStream(new NetworkStream(socket, ownsSocket: true));
                        _stream.Disconnected += OnDisconnected;
                        _stream.Initialized += OnInitialized;
                        break;
                    }
                    Debug.WriteLine($"[PTVS][DebugAdapterRemoteProcess] Unexpected: socket.Connect returned but not Connected attempt={attempt}");
                } catch (SocketException sex) {
                    Debug.WriteLine($"[PTVS][DebugAdapterRemoteProcess] Attempt {attempt} SocketException code={sex.SocketErrorCode} elapsed={sw.ElapsedMilliseconds}ms");
                    if (sex.SocketErrorCode != SocketError.ConnectionRefused && sex.SocketErrorCode != SocketError.TimedOut) {
                        Debug.WriteLine($"[PTVS][DebugAdapterRemoteProcess] Non-retriable socket error, aborting connection loop");
                        break;
                    }
                    // If dynamic attach (port was 0) and we have a port file, see if port changed.
                    if (!string.IsNullOrEmpty(portFilePath) && File.Exists(portFilePath)) {
                        try {
                            var fileInfo = new FileInfo(portFilePath);
                            if (fileInfo.Length > 0) {
                                string txt = File.ReadAllText(portFilePath).Trim();
                                Debug.WriteLine($"[PTVS][DebugAdapterRemoteProcess] Read port file '{portFilePath}' value='{txt}' lastWrite={fileInfo.LastWriteTime:O}");
                                if (int.TryParse(txt, out var filePort) && filePort > 0 && filePort != currentPort) {
                                    Debug.WriteLine($"[PTVS][DebugAdapterRemoteProcess] Port update detected via file: {currentPort} -> {filePort}");
                                    currentPort = filePort;
                                    endpoint = BuildEndpoint(currentPort);
                                    // small pause after port change
                                    System.Threading.Thread.Sleep(120);
                                }
                            } else {
                                Debug.WriteLine($"[PTVS][DebugAdapterRemoteProcess] Port file exists but empty (len=0)");
                            }
                        } catch (Exception exRead) {
                            Debug.WriteLine($"[PTVS][DebugAdapterRemoteProcess] Port file read exception: {exRead.Message}");
                        }
                    }
                } catch (Exception ex) {
                    Debug.WriteLine($"[PTVS][DebugAdapterRemoteProcess] Attempt {attempt} general exception: {ex.Message}");
                }
                if (_debuggerConnected) break;
                socket.Dispose();
                int delay = Math.Min(backoffInitialMs * (int)Math.Pow(2, attempt - 1), backoffMaxMs);
                Debug.WriteLine($"[PTVS][DebugAdapterRemoteProcess] Waiting {delay}ms before next attempt (elapsed={sw.ElapsedMilliseconds}ms)");
                System.Threading.Thread.Sleep(delay);
            }

            if (!_debuggerConnected) {
                Debug.WriteLine($"[PTVS][DebugAdapterRemoteProcess] TIMEOUT elapsed={sw.ElapsedMilliseconds}ms finalPort={currentPort} originalPort={originalPort} attempts={attempt}");
                logger?.LogEvent(PythonLogEvent.DebugAdapterConnectionTimeout, "Attach");
            }
            return _debuggerConnected;
        }

        private void OnDisconnected(object sender, EventArgs e) {
            Debug.WriteLine("[PTVS][DebugAdapterRemoteProcess] OnDisconnected invoked");
            if (_stream != null) {
                _stream.Disconnected -= OnDisconnected;
                _stream.Initialized -= OnInitialized;
                _stream.Dispose();
            }
            Exited?.Invoke(this, null);
        }

        private void OnInitialized(object sender, EventArgs e) {
            Debug.WriteLine("[PTVS][DebugAdapterRemoteProcess] OnInitialized -> requesting debugpy version");
            CustomDebugAdapterProtocolExtension.SendRequest(
                new DebugPyVersionRequest(),
                DebugPyVersionHelper.VerifyDebugPyVersion,
                DebugPyVersionHelper.ShowDebugPyVersionError);
        }

        public IntPtr Handle => IntPtr.Zero;

        public Stream StandardInput => _stream;

        public Stream StandardOutput => _stream;

        public bool HasExited => _debuggerConnected;

        public event EventHandler Exited;
        public event DataReceivedEventHandler ErrorDataReceived;

        public void Dispose() {
            Debug.WriteLine("[PTVS][DebugAdapterRemoteProcess] Dispose called");
            if (_stream != null) {
                _stream.Dispose();
            }
        }

        public void Terminate() {
            Debug.WriteLine("[PTVS][DebugAdapterRemoteProcess] Terminate called");
            if (_stream != null) {
                _stream.Dispose();
            }
            ErrorDataReceived?.Invoke(this, null);
        }
    }
}

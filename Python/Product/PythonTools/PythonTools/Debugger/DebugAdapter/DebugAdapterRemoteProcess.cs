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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Logging;
using Microsoft.VisualStudio.Debugger.DebugAdapterHost.Interfaces;

namespace Microsoft.PythonTools.Debugger {
    internal sealed class DebugAdapterRemoteProcess : ITargetHostProcess, IDisposable {
        private const int _debuggerConnectionTimeout = 20000; // 20 seconds
        private DebugRemoteAdapterProcessStream _stream;
        private int _hasExited;

        private DebugAdapterRemoteProcess() { }

        public static ITargetHostProcess Attach(DebugTcpAttachInfo debugAttachInfo) {
            var logger = (IPythonToolsLogger)VisualStudio.Shell.ServiceProvider.GlobalProvider.GetService(typeof(IPythonToolsLogger));
            return Attach(
                debugAttachInfo.RemoteUri,
                _debuggerConnectionTimeout,
                ConnectSocketAsync,
                WaitForConnectionOnUIThread,
                logger
            );
        }

        internal static DebugAdapterRemoteProcess Attach(Uri uri, int connectionTimeout) {
            return Attach(uri, connectionTimeout, ConnectSocketAsync, WaitForConnectionSynchronously, null);
        }

        internal static DebugAdapterRemoteProcess Attach(
            Uri uri,
            int connectionTimeout,
            Func<Socket, EndPoint, Task> connectAsync
        ) {
            return Attach(uri, connectionTimeout, connectAsync, WaitForConnectionSynchronously, null);
        }

        private static DebugAdapterRemoteProcess Attach(
            Uri uri,
            int connectionTimeout,
            Func<Socket, EndPoint, Task> connectAsync,
            Func<Task, int, bool> waitForConnection,
            IPythonToolsLogger logger
        ) {
            var attachedProcess = new DebugAdapterRemoteProcess();
            return attachedProcess.ConnectSocket(
                uri,
                connectionTimeout,
                connectAsync,
                waitForConnection,
                logger
            ) ? attachedProcess : null;
        }

        private static Task ConnectSocketAsync(Socket socket, EndPoint endpoint) {
            return Task.Factory.FromAsync(socket.BeginConnect, socket.EndConnect, endpoint, null);
        }

        private static bool WaitForConnectionOnUIThread(Task connectTask, int connectionTimeout) {
            return VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.Run(async () => {
                var completedTask = await Task.WhenAny(connectTask, Task.Delay(connectionTimeout));
                if (!ReferenceEquals(completedTask, connectTask)) {
                    return false;
                }

                await connectTask;
                return true;
            });
        }

        private static bool WaitForConnectionSynchronously(Task connectTask, int connectionTimeout) {
            var completedTask = Task.WhenAny(connectTask, Task.Delay(connectionTimeout)).GetAwaiter().GetResult();
            if (!ReferenceEquals(completedTask, connectTask)) {
                return false;
            }

            connectTask.GetAwaiter().GetResult();
            return true;
        }

        private bool ConnectSocket(
            Uri uri,
            int connectionTimeout,
            Func<Socket, EndPoint, Task> connectAsync,
            Func<Task, int, bool> waitForConnection,
            IPythonToolsLogger logger
        ) {
            Socket socket = null;
            try {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                EndPoint endpoint;
                if (uri.IsLoopback) {
                    endpoint = new IPEndPoint(IPAddress.Loopback, uri.Port);
                } else {
                    endpoint = new DnsEndPoint(uri.Host, uri.Port);
                }

                Debug.WriteLine(
                    "Connecting to remote debugger at {0}".FormatInvariant(uri),
                    nameof(DebugAdapterRemoteProcess)
                );
                var connectTask = connectAsync(socket, endpoint);
                if (!waitForConnection(connectTask, connectionTimeout)) {
                    Debug.WriteLine("Timed out waiting for debugger to connect.", nameof(DebugAdapterRemoteProcess));
                    logger?.LogEvent(PythonLogEvent.DebugAdapterConnectionTimeout, "Attach");
                    ObserveFault(connectTask);
                    return false;
                }

                if (!socket.Connected) {
                    Debug.WriteLine("The remote debugger connection did not complete.", nameof(DebugAdapterRemoteProcess));
                    return false;
                }

                var stream = new DebugRemoteAdapterProcessStream(new NetworkStream(socket, ownsSocket: true));
                stream.Disconnected += OnDisconnected;
                stream.Initialized += OnInitialized;
                _stream = stream;
                socket = null;
                return true;
            } catch (Exception ex) when (IsConnectionException(ex)) {
                Debug.WriteLine(
                    "Error waiting for debugger to connect {0}".FormatInvariant(ex.GetBaseException()),
                    nameof(DebugAdapterRemoteProcess)
                );
                return false;
            } finally {
                socket?.Dispose();
            }
        }

        private static bool IsConnectionException(Exception ex) {
            var baseException = ex.GetBaseException();
            return baseException is SocketException || baseException is ObjectDisposedException;
        }

        private static void ObserveFault(Task task) {
            task.ContinueWith(
                faultedTask => {
                    var ignored = faultedTask.Exception;
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default
            );
        }

        private void OnDisconnected(object sender, EventArgs e) {
            Exit();
        }

        private void OnInitialized(object sender, EventArgs e) {
            if (!HasExited) {
                CustomDebugAdapterProtocolExtension.SendRequest(
                    new DebugPyVersionRequest(),
                    DebugPyVersionHelper.VerifyDebugPyVersion,
                    DebugPyVersionHelper.ShowDebugPyVersionError
                );
            }
        }

        private void Exit() {
            if (Interlocked.Exchange(ref _hasExited, 1) != 0) {
                return;
            }

            var stream = _stream;
            if (stream != null) {
                stream.Disconnected -= OnDisconnected;
                stream.Initialized -= OnInitialized;
            }

            try {
                stream?.Dispose();
            } finally {
                Exited?.Invoke(this, EventArgs.Empty);
            }
        }

        public IntPtr Handle => IntPtr.Zero;

        public Stream StandardInput => _stream;

        public Stream StandardOutput => _stream;

        public bool HasExited => Volatile.Read(ref _hasExited) != 0;

        public event EventHandler Exited;
        public event DataReceivedEventHandler ErrorDataReceived { add { } remove { } }

        public void Dispose() {
            Exit();
            GC.SuppressFinalize(this);
        }

        public void Terminate() {
            Exit();
        }
    }
}

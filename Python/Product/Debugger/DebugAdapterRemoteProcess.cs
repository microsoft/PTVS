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
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Debugger.DebugAdapterHost.Interfaces;
using Microsoft.PythonTools.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Microsoft.PythonTools.Debugger {
    sealed class DebugAdapterRemoteProcess : ITargetHostProcess, IDisposable {
        private const int _debuggerConnectionTimeout = 5000; // 5000 ms
        private Stream _stream;
        private bool _debuggerConnected = false;

        private DebugAdapterRemoteProcess() {}

        public static ITargetHostProcess Attach(string attachJson) {
            var process = new DebugAdapterRemoteProcess();
            process.AttachProcess(attachJson);
            return process;
        }

        private void AttachProcess(string attachJson) {
            var json = JObject.Parse(attachJson);
            var uri = new Uri(json["remote"].Value<string>());
            ConnectSocket(uri);
        }

        private void ConnectSocket(Uri uri) {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            EndPoint endpoint;
            if (uri.IsLoopback) {
                endpoint = new IPEndPoint(IPAddress.Loopback, uri.Port);
            } else {
                endpoint = new DnsEndPoint(uri.Host, uri.Port);
            }
            Debug.WriteLine("Connecting to remote debugger at {0}", uri.ToString());
            var connection = Task.Factory.FromAsync(
                (AsyncCallback callback, object state) => socket.BeginConnect(endpoint, callback, state),
                socket.EndConnect,
                null);

            try {
                if (connection.Wait(_debuggerConnectionTimeout)) {
                    _debuggerConnected = true;
                    _stream = new DebugAdapterProcessStream(new NetworkStream(socket, ownsSocket: true));
                } else {
                    Debug.WriteLine("Timed out waiting for debuger to connect.", nameof(DebugAdapterRemoteProcess));
                }
            } catch (AggregateException ex) {
                Debug.WriteLine("Error waiting for debuger to connect {0}".FormatInvariant(ex.InnerException ?? ex), nameof(DebugAdapterRemoteProcess));
            }
        }

        public IntPtr Handle => IntPtr.Zero;

        public Stream StandardInput => _stream;

        public Stream StandardOutput => _stream;

        public bool HasExited => _debuggerConnected;

        public event EventHandler Exited;
        public event DataReceivedEventHandler ErrorDataReceived;

        public void Dispose() {
            if(_stream != null) {
                _stream.Dispose();
            }
            Exited?.Invoke(this, null);
            ErrorDataReceived?.Invoke(this, null);
        }

        public void Terminate() {}
    }
}

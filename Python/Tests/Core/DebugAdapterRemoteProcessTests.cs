// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://schemas.microsoft.com/fwlink/?LinkID=322387
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for the specific language governing
// permissions and limitations under the License.

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Debugger;
using Microsoft.VisualStudio.Debugger.DebugAdapterHost.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace PythonToolsTests {
    [TestClass]
    public class DebugAdapterRemoteProcessTests {
        [TestMethod, Priority(UnitTestPriority.P0)]
        public void ConnectedRemoteProcessHasNotExited() {
            using (var connection = LoopbackConnection.Create()) {
                ITargetHostProcess process = connection.TargetProcess;

                Assert.IsFalse(process.HasExited);
                Assert.AreSame(process.StandardInput, process.StandardOutput);
                Assert.IsNotNull(process.StandardInput);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void RemoteCloseRaisesExitedOnce() {
            using (var connection = LoopbackConnection.Create()) {
                var exitCount = 0;
                using (var exited = new ManualResetEventSlim()) {
                    connection.TargetProcess.Exited += (sender, args) => {
                        Interlocked.Increment(ref exitCount);
                        exited.Set();
                    };

                    var readTask = ReadAsync(connection.TargetProcess.StandardOutput);
                    connection.Server.Dispose();

                    Assert.IsTrue(readTask.Wait(TimeSpan.FromSeconds(5)), "The adapter stream read did not complete.");
                    Assert.AreEqual(0, readTask.Result);
                    Assert.IsTrue(exited.Wait(TimeSpan.FromSeconds(5)), "The process exit event was not raised.");
                    Assert.IsTrue(connection.TargetProcess.HasExited);

                    connection.TargetProcess.Terminate();
                    connection.TargetProcess.Dispose();
                    Assert.AreEqual(1, Volatile.Read(ref exitCount));
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void FragmentedDisconnectResponseRaisesExitedOnce() {
            using (var connection = LoopbackConnection.Create()) {
                var exitCount = 0;
                using (var exited = new ManualResetEventSlim()) {
                    connection.TargetProcess.Exited += (sender, args) => {
                        Interlocked.Increment(ref exitCount);
                        exited.Set();
                    };

                    var primingMessage = CreateResponse("threads");
                    var primingRead = ReadExactAsync(
                        connection.TargetProcess.StandardOutput,
                        primingMessage.Length
                    );
                    Write(connection.Server.GetStream(), primingMessage, 0, primingMessage.Length);
                    Assert.IsTrue(primingRead.Wait(TimeSpan.FromSeconds(5)), "The priming adapter stream read did not complete.");
                    Assert.AreEqual(primingMessage.Length, primingRead.Result);
                    Assert.IsFalse(connection.TargetProcess.HasExited);

                    var message = CreateDisconnectResponse();
                    var firstSplit = 10;
                    var secondSplit = message.Length / 2;

                    var firstRead = ReadExactAsync(connection.TargetProcess.StandardOutput, firstSplit);
                    Write(connection.Server.GetStream(), message, 0, firstSplit);
                    Assert.IsTrue(firstRead.Wait(TimeSpan.FromSeconds(5)), "The first adapter stream read did not complete.");
                    Assert.IsFalse(connection.TargetProcess.HasExited);

                    var secondRead = ReadExactAsync(
                        connection.TargetProcess.StandardOutput,
                        secondSplit - firstSplit
                    );
                    Write(connection.Server.GetStream(), message, firstSplit, secondSplit - firstSplit);
                    Assert.IsTrue(secondRead.Wait(TimeSpan.FromSeconds(5)), "The second adapter stream read did not complete.");
                    Assert.IsFalse(connection.TargetProcess.HasExited);

                    var finalRead = ReadUntilExitedAsync(connection.TargetProcess);
                    Write(connection.Server.GetStream(), message, secondSplit, message.Length - secondSplit);
                    Assert.IsTrue(finalRead.Wait(TimeSpan.FromSeconds(5)), "The final adapter stream read did not complete.");
                    Assert.IsTrue(finalRead.Result > 0);
                    Assert.IsTrue(exited.Wait(TimeSpan.FromSeconds(5)), "The disconnect response did not end the process.");
                    Assert.IsTrue(connection.TargetProcess.HasExited);

                    connection.TargetProcess.Terminate();
                    connection.TargetProcess.Dispose();
                    Assert.AreEqual(1, Volatile.Read(ref exitCount));
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void TerminateRaisesExitedAndDisposesStream() {
            VerifyLocalExit(process => process.Terminate());
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void DisposeRaisesExitedAndDisposesStream() {
            VerifyLocalExit(process => process.Dispose());
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void ConcurrentDisconnectAndDisposalRaiseExitedOnce() {
            using (var connection = LoopbackConnection.Create()) {
                var exitCount = 0;
                using (var exited = new ManualResetEventSlim())
                using (var start = new ManualResetEventSlim()) {
                    connection.TargetProcess.Exited += (sender, args) => {
                        Interlocked.Increment(ref exitCount);
                        exited.Set();
                    };

                    var readTask = ReadAsync(connection.TargetProcess.StandardOutput);
                    var actions = Enumerable.Range(0, 12)
                        .Select(index => Task.Run(() => {
                            start.Wait();
                            switch (index % 4) {
                                case 0:
                                    connection.TargetProcess.Terminate();
                                    break;
                                case 1:
                                    connection.TargetProcess.Dispose();
                                    break;
                                case 2:
                                    connection.TargetProcess.StandardOutput.Dispose();
                                    break;
                                default:
                                    connection.Server.Dispose();
                                    break;
                            }
                        }))
                        .ToArray();

                    start.Set();

                    Assert.IsTrue(Task.WaitAll(actions, TimeSpan.FromSeconds(5)), "Concurrent disposal did not complete.");
                    Assert.IsTrue(readTask.Wait(TimeSpan.FromSeconds(5)), "The pending adapter read was not released.");
                    Assert.AreEqual(0, readTask.Result);
                    Assert.IsTrue(exited.Wait(TimeSpan.FromSeconds(5)), "The process exit event was not raised.");
                    Assert.IsTrue(connection.TargetProcess.HasExited);
                    Assert.AreEqual(1, Volatile.Read(ref exitCount));
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void ConnectionTimeoutDisposesSocket() {
            Socket socket = null;
            var pendingConnection = new TaskCompletionSource<object>();

            var process = DebugAdapterRemoteProcess.Attach(
                new Uri("tcp://127.0.0.1:1"),
                100,
                (createdSocket, endpoint) => {
                    socket = createdSocket;
                    return pendingConnection.Task;
                }
            );

            Assert.IsNull(process);
            AssertSocketDisposed(socket);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void ConnectionFailureDisposesSocket() {
            Socket socket = null;
            var failedConnection = new TaskCompletionSource<object>();
            failedConnection.SetException(new SocketException((int)SocketError.ConnectionRefused));

            var process = DebugAdapterRemoteProcess.Attach(
                new Uri("tcp://127.0.0.1:1"),
                5000,
                (createdSocket, endpoint) => {
                    socket = createdSocket;
                    return failedConnection.Task;
                }
            );

            Assert.IsNull(process);
            AssertSocketDisposed(socket);
        }

        private static void VerifyLocalExit(Action<DebugAdapterRemoteProcess> exit) {
            using (var connection = LoopbackConnection.Create()) {
                var exitCount = 0;
                var exitedWithDisposedStream = 0;
                var stream = connection.TargetProcess.StandardOutput;
                using (var exited = new ManualResetEventSlim()) {
                    connection.TargetProcess.Exited += (sender, args) => {
                        Interlocked.Increment(ref exitCount);
                        if (connection.TargetProcess.HasExited && !stream.CanRead && !stream.CanWrite) {
                            Volatile.Write(ref exitedWithDisposedStream, 1);
                        }
                        exited.Set();
                    };

                    exit(connection.TargetProcess);

                    Assert.IsTrue(exited.Wait(TimeSpan.FromSeconds(5)), "The process exit event was not raised.");
                    Assert.IsTrue(connection.TargetProcess.HasExited);
                    Assert.AreEqual(1, Volatile.Read(ref exitCount));
                    Assert.AreEqual(1, Volatile.Read(ref exitedWithDisposedStream));
                    AssertStreamDisposed(stream);

                    exit(connection.TargetProcess);
                    Assert.AreEqual(1, Volatile.Read(ref exitCount));
                }
            }
        }

        private static Task<int> ReadAsync(Stream stream) {
            return Task.Run(() => {
                var buffer = new byte[4096];
                return stream.Read(buffer, 0, buffer.Length);
            });
        }

        private static Task<int> ReadExactAsync(Stream stream, int expectedBytes) {
            return Task.Run(() => {
                var buffer = new byte[4096];
                var bytesRead = 0;
                while (bytesRead < expectedBytes) {
                    var read = stream.Read(
                        buffer,
                        0,
                        Math.Min(buffer.Length, expectedBytes - bytesRead)
                    );
                    if (read == 0) {
                        break;
                    }
                    bytesRead += read;
                }
                return bytesRead;
            });
        }

        private static Task<int> ReadUntilExitedAsync(DebugAdapterRemoteProcess process) {
            return Task.Run(() => {
                var buffer = new byte[4096];
                var bytesRead = 0;
                while (!process.HasExited) {
                    bytesRead += process.StandardOutput.Read(buffer, 0, buffer.Length);
                }
                return bytesRead;
            });
        }

        private static byte[] CreateDisconnectResponse() {
            return CreateResponse("disconnect");
        }

        private static byte[] CreateResponse(string command) {
            var body = Encoding.UTF8.GetBytes(
                $"{{\"seq\":2,\"type\":\"response\",\"request_seq\":1,\"success\":true,\"command\":\"{command}\"}}"
            );
            var header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
            var message = new byte[header.Length + body.Length];
            Buffer.BlockCopy(header, 0, message, 0, header.Length);
            Buffer.BlockCopy(body, 0, message, header.Length, body.Length);
            return message;
        }

        private static void Write(NetworkStream stream, byte[] buffer, int offset, int count) {
            stream.Write(buffer, offset, count);
            stream.Flush();
        }

        private static void AssertSocketDisposed(Socket socket) {
            Assert.IsNotNull(socket);
            try {
                var ignored = socket.Available;
                Assert.Fail("The failed connection socket was not disposed.");
            } catch (ObjectDisposedException) {
            }
        }

        private static void AssertStreamDisposed(Stream stream) {
            Assert.IsFalse(stream.CanRead);
            Assert.IsFalse(stream.CanWrite);
        }

        private sealed class LoopbackConnection : IDisposable {
            private readonly TcpListener _listener;

            private LoopbackConnection(TcpListener listener, TcpClient server, DebugAdapterRemoteProcess targetProcess) {
                _listener = listener;
                Server = server;
                TargetProcess = targetProcess;
            }

            public TcpClient Server { get; }

            public DebugAdapterRemoteProcess TargetProcess { get; }

            public static LoopbackConnection Create() {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();

                try {
                    var acceptTask = listener.AcceptTcpClientAsync();
                    var endpoint = (IPEndPoint)listener.LocalEndpoint;
                    var targetProcess = DebugAdapterRemoteProcess.Attach(
                        new Uri($"tcp://127.0.0.1:{endpoint.Port}"),
                        5000
                    );

                    Assert.IsNotNull(targetProcess, "The loopback adapter connection failed.");
                    Assert.IsTrue(acceptTask.Wait(TimeSpan.FromSeconds(5)), "The loopback connection was not accepted.");
                    return new LoopbackConnection(listener, acceptTask.Result, targetProcess);
                } catch {
                    listener.Stop();
                    throw;
                }
            }

            public void Dispose() {
                TargetProcess.Dispose();
                Server.Dispose();
                _listener.Stop();
            }
        }
    }
}

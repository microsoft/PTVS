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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Ipc.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace IpcJsonTests {
    [TestClass]
    public class IpcJsonConnectionTests {
        private Connection _client;
        private Connection _server;
        private readonly AutoResetEvent _connected = new AutoResetEvent(false);

        private static string PythonSocketSendEventPath => Path.Combine(TestData.GetPath("TestData"), "Ipc.Json", "socket_send_event.py");
        private static string PythonSocketHandleRequest => Path.Combine(TestData.GetPath("TestData"), "Ipc.Json", "socket_handle_request.py");

        // Change this to true if you want to have an easier time debugging the python script
        // You'll be expected to start it manually, which you can do by opening
        // IpcJsonServers.sln, adjusting the debug script arguments to have the correct port number and pressing F5
        private static readonly bool StartPythonProcessManually = false;

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        [TestMethod, Priority(0)]
        public async Task DotNetHandleRequest() {
            InitConnection(async (requestArgs, done) => {
                switch (requestArgs.Command) {
                    case TestDataProtocol.TestRequest.Command: {
                            var testRequest = requestArgs.Request as TestDataProtocol.TestRequest;
                            await done(new TestDataProtocol.TestResponse() {
                                requestText = testRequest.dataText,
                                responseText = "test response text",
                            });
                            break;
                        }
                    default:
                        throw new InvalidOperationException();
                }
            });

            _client.EventReceived += (object sender, EventReceivedEventArgs e) => {
                Assert.Fail();
            };

            var response = await _client.SendRequestAsync(new TestDataProtocol.TestRequest() {
                dataText = "request data text",
                dataTextList = new string[] { "value 1", "value 2" },
            }, CancellationTokens.After5s);

            Assert.AreEqual("request data text", response.requestText);
            Assert.AreEqual("test response text", response.responseText);
        }

        [TestMethod, Priority(0)]
        public async Task DotNetHandleRequestUnicode() {
            InitConnection(async (requestArgs, done) => {
                switch (requestArgs.Command) {
                    case TestDataProtocol.TestRequest.Command: {
                            var testRequest = requestArgs.Request as TestDataProtocol.TestRequest;
                            await done(new TestDataProtocol.TestResponse() {
                                requestText = testRequest.dataText,
                                responseText = "この文は、テストです。私はこれがうまく願っています。",
                            });
                            break;
                        }
                    default:
                        throw new InvalidOperationException();
                }
            });

            _client.EventReceived += (object sender, EventReceivedEventArgs e) => {
                Assert.Fail();
            };

            var response = await _client.SendRequestAsync(new TestDataProtocol.TestRequest() {
                dataText = "データテキストを要求する",
                dataTextList = new string[] { "value 1", "value 2" },
            }, CancellationTokens.After5s);

            Assert.AreEqual("データテキストを要求する", response.requestText);
            Assert.AreEqual("この文は、テストです。私はこれがうまく願っています。", response.responseText);
        }

        [TestMethod, Priority(0)]
        public async Task PythonHandleRequest() {
            using (InitConnection(PythonSocketHandleRequest)) {
                _client.EventReceived += (object sender, EventReceivedEventArgs e) => {
                    Assert.Fail();
                };

                var response = await _client.SendRequestAsync(new TestDataProtocol.TestRequest() {
                    dataText = "request data text",
                    dataTextList = new string[] { "value 1", "value 2" },
                }, StartPythonProcessManually ? CancellationTokens.After60s : CancellationTokens.After5s);

                await _client.SendRequestAsync(
                    new TestDataProtocol.DisconnectRequest(),
                    StartPythonProcessManually ? CancellationTokens.After60s : CancellationTokens.After5s
                );

                Assert.AreEqual("request data text", response.requestText);
                Assert.AreEqual("test response text", response.responseText);
            }
        }

        [TestMethod, Priority(0)]
        public async Task PythonHandleRequestUnicode() {
            using (InitConnection(PythonSocketHandleRequest)) {
                _client.EventReceived += (object sender, EventReceivedEventArgs e) => {
                    Assert.Fail();
                };

                var response = await _client.SendRequestAsync(new TestDataProtocol.TestRequest() {
                    dataText = "データテキストを要求する 请输入",
                    dataTextList = new string[] { "value 1", "value 2" },
                }, StartPythonProcessManually ? CancellationTokens.After60s : CancellationTokens.After5s);

                await _client.SendRequestAsync(
                    new TestDataProtocol.DisconnectRequest(),
                    StartPythonProcessManually ? CancellationTokens.After60s : CancellationTokens.After5s
                );

                Assert.AreEqual("データテキストを要求する 请输入", response.requestText);
                Assert.AreEqual("test response text", response.responseText);
            }
        }

        [TestMethod, Priority(0)]
        public async Task DotNetSendEvent() {
            InitConnection((requestArgs, done) => {
                Assert.Fail();
                return Task.CompletedTask;
            });

            var eventReceived = new AutoResetEvent(false);
            var eventsReceived = new List<EventReceivedEventArgs>();
            _client.EventReceived += (object sender, EventReceivedEventArgs e) => {
                eventsReceived.Add(e);
                if (eventsReceived.Count == 1) {
                    eventReceived.Set();
                }
            };

            await _server.SendEventAsync(new TestDataProtocol.TestEvent() {
                dataText = "event data text"
            });

            eventReceived.WaitOne(2000);

            Assert.AreEqual(1, eventsReceived.Count);
            Assert.AreEqual(TestDataProtocol.TestEvent.Name, eventsReceived[0].Name);
            Assert.AreEqual("event data text", ((TestDataProtocol.TestEvent)eventsReceived[0].Event).dataText);
        }

        [TestMethod, Priority(0)]
        public async Task PythonSendEvent() {
            using (InitConnection(PythonSocketSendEventPath)) {
                var eventReceived = new AutoResetEvent(false);
                var eventsReceived = new List<EventReceivedEventArgs>();
                _client.EventReceived += (object sender, EventReceivedEventArgs e) => {
                    eventsReceived.Add(e);
                    if (eventsReceived.Count == 1) {
                        eventReceived.Set();
                    }
                };

                eventReceived.WaitOne(2000);

                Assert.AreEqual(1, eventsReceived.Count);
                Assert.AreEqual(TestDataProtocol.TestEvent.Name, eventsReceived[0].Name);
                Assert.AreEqual("python event data text", ((TestDataProtocol.TestEvent)eventsReceived[0].Event).dataText);
                Assert.AreEqual(76, ((TestDataProtocol.TestEvent)eventsReceived[0].Event).dataInt32);
            }
        }

        private void InitConnection(Func<RequestArgs, Func<Response, Task>, Task> serverRequestHandler) {
            // Client sends requests, receives responses and events
            // Server receives requests, sends back responses and events
            // Client creates the socket on an available port,
            // passes the port number to the server which connects back to it.
            var portNum = StartClient();

            StartServer(portNum, serverRequestHandler);

            _connected.WaitOne();
        }

        private sealed class KillAndDisposeProcess : IDisposable {
            public KillAndDisposeProcess(ProcessOutput process) {
                Process = process;
            }

            public void Dispose() {
                Process.Kill();
                Process.Dispose();
            }

            public ProcessOutput Process { get; }
        }

        private IDisposable InitConnection(string serverScriptPath) {
            // Client sends requests, receives responses and events
            // Server receives requests, sends back responses and events
            // Client creates the socket on an available port,
            // passes the port number to the server which connects back to it.
            var portNum = StartClient();

            if (!StartPythonProcessManually) {
                Assert.IsTrue(File.Exists(serverScriptPath), "Python test data script '{0}' was not found.".FormatUI(serverScriptPath));

                var workingDir = Path.GetDirectoryName(serverScriptPath);

                var searchPaths = new HashSet<string>();
                searchPaths.Add(PtvsdSearchPath);
                searchPaths.Add(workingDir);

                var env = new List<KeyValuePair<string, string>>();
                env.Add(new KeyValuePair<string, string>("PYTHONPATH", string.Join(";", searchPaths)));

                var arguments = new List<string>();
                arguments.Add(serverScriptPath);
                arguments.Add("-r");
                arguments.Add(portNum.ToString());
                var proc = ProcessOutput.Run(
                    (PythonPaths.Python27 ?? PythonPaths.Python27_x64).InterpreterPath,
                    arguments,
                    workingDir,
                    env,
                    false,
                    null
                );
                try {
                    if (proc.ExitCode.HasValue) {
                        // Process has already exited
                        proc.Wait();
                        if (proc.StandardErrorLines.Any()) {
                            Assert.Fail(String.Join(Environment.NewLine, proc.StandardErrorLines));
                        }
                        return null;
                    } else {
                        _connected.WaitOne();
                        var p = proc;
                        proc = null;
                        return new KillAndDisposeProcess(p);
                    }
                } finally {
                    if (proc != null) {
                        proc.Dispose();
                    }
                }
            } else {
                // Check the port number variable assigned above if you want to
                // start the python process manually
                Debugger.Break();
                return null;
            }
        }

        internal static string PtvsdSearchPath {
            get {
                return Path.GetDirectoryName(Path.GetDirectoryName(PythonToolsInstallPath.GetFile("ptvsd\\__init__.py", typeof(Connection).Assembly)));
            }
        }

        private void StartServer(int portNum, Func<RequestArgs, Func<Response, Task>, Task> requestHandler) {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, portNum);

            var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.IP);
            socket.Connect(endPoint);

            var stream = new NetworkStream(socket, ownsSocket: true);

            _server = new Connection(
                stream,
                true,
                stream,
                true,
                requestHandler,
                TestDataProtocol.RegisteredTypes
            );
            Task.Run(_server.ProcessMessages).DoNotWait();
        }

        private int StartClient() {
            new SocketPermission(NetworkAccess.Accept, TransportType.All, "", SocketPermission.AllPorts).Demand();

            // Use an available port
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, 0);

            var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.IP);
            socket.Bind(endPoint);
            socket.Listen(10);

            // Find out which port is being used
            var portNum = ((IPEndPoint)socket.LocalEndPoint).Port;

            Task.Run(() => { socket.BeginAccept(StartClientAcceptCallback, socket); });
            return portNum;
        }

        private void StartClientAcceptCallback(IAsyncResult ar) {
            var socket = ((Socket)ar.AsyncState).EndAccept(ar);
            var stream = new NetworkStream(socket, ownsSocket: true);
            _client = new Connection(stream, true, stream, true, null, TestDataProtocol.RegisteredTypes);
            Task.Run(_client.ProcessMessages).DoNotWait();
            _connected.Set();
        }
    }

    static class TestDataProtocol {
        public static readonly Dictionary<string, Type> RegisteredTypes = CollectCommands();

        private static Dictionary<string, Type> CollectCommands() {
            Dictionary<string, Type> all = new Dictionary<string, Type>();
            foreach (var type in typeof(TestDataProtocol).GetNestedTypes()) {
                if (type.IsSubclassOf(typeof(Request))) {
                    var command = type.GetField("Command");
                    if (command != null) {
                        all["request." + (string)command.GetRawConstantValue()] = type;
                    }
                } else if (type.IsSubclassOf(typeof(Event))) {
                    var name = type.GetField("Name");
                    if (name != null) {
                        all["event." + (string)name.GetRawConstantValue()] = type;
                    }
                }
            }
            return all;
        }

#pragma warning disable 0649 // Field 'field' is never assigned to, and will always have its default value 'value'

        public sealed class TestRequest : Request<TestResponse> {
            public const string Command = "testRequest";

            public override string command => Command;

            public string dataText;
            public string[] dataTextList;

        }

        public sealed class TestResponse : Response {
            public string requestText;
            public string responseText;
            public string[] responseTextList;
        }

        public sealed class DisconnectRequest : GenericRequest {
            public const string Command = "disconnect";

            public override string command => Command;
        }

        public class TestEvent : Event {
            public const string Name = "testEvent";
            public string dataText;
            public int dataInt32;
            public long dataInt64;
            public float dataFloat32;
            public double dataFloat64;

            public override string name => Name;
        }

#pragma warning restore 0649

    }
}

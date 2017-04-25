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
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;

namespace IpcJsonTests {
    [TestClass]
    public class PacketReadPythonTests {
        private Stream _clientStream;
        private readonly AutoResetEvent _connected = new AutoResetEvent(false);

        private static string PythonParsingTestPath => Path.Combine(TestData.GetPath("TestData"), "Ipc.Json", "parsing_test.py");

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        private static string validJson1 = @"{'request_seq':1,'success':true,'command':'initialize','message':null,'body':{'failedLoads':[],'error':null},'type':'response','seq':2}".Replace('\'', '"');
        private static string validJson2 = @"{'event':'childFileAnalyzed','body':{'filename':'C:\\Projects\\RemoteDebugApp\\RemoteDebugApp.py','fileId':1,'isTemporaryFile':false,'suppressErrorList':false},'type':'event','seq':5}".Replace('\'', '"');
        private static string validUtf8Json1 = @"{'event':'childFileAnalyzed','body':{'filename':'C:\\Projects\\RemoteDebugApp\\この文は、テストです。私はこれがうまく願っています。.py','fileId':1,'isTemporaryFile':false,'suppressErrorList':false},'type':'event','seq':5}".Replace('\'', '"');
        private static string validUtf8Json2 = @"{'event':'childFileAnalyzed','body':{'filename':'C:\\Projects\\RemoteDebugApp\\é.py','fileId':1,'isTemporaryFile':false,'suppressErrorList':false},'type':'event','seq':5}".Replace('\'', '"');

        [TestMethod, Priority(1)]
        public async Task ValidPackets() {
            await TestValidPacketAsync(PacketProvider.GetValidPacket1());
            await TestValidPacketAsync(PacketProvider.GetValidPacket2());
        }

        [TestMethod, Priority(1)]
        public async Task ValidUnicodePackets() {
            await TestValidPacketAsync(PacketProvider.GetValidUnicodePacket1());
            await TestValidPacketAsync(PacketProvider.GetValidUnicodePacket2());
        }

        [TestMethod, Priority(1)]
        public async Task TruncatedJson() {
            foreach (var packet in PacketProvider.GetTruncatedJsonPackets()) {
                await TestInvalidPacketAsync(packet, "visualstudio_py_ipcjson.InvalidContentError");
            }
        }

        [TestMethod, Priority(1)]
        public async Task IncorrectContentLengthUnderread() {
            foreach (var packet in PacketProvider.GetIncorrectContentLengthUnderreadPackets()) {
                await TestInvalidPacketAsync(packet, "visualstudio_py_ipcjson.InvalidContentError");
            }
        }

        [TestMethod, Priority(1)]
        public async Task IncorrectContentLengthOverread() {
            foreach (var packet in PacketProvider.GetIncorrectContentLengthOverreadPackets()) {
                await TestInvalidPacketAsync(packet, "visualstudio_py_ipcjson.InvalidContentError");
            }
        }

        [TestMethod, Priority(1)]
        public async Task IncorrectContentLengthOverreadEndOfStream() {
            foreach (var packet in PacketProvider.GetIncorrectContentLengthOverreadEndOfStreamPackets()) {
                await TestInvalidPacketAsync(packet, "visualstudio_py_ipcjson.InvalidContentError");
            }
        }

        [TestMethod, Priority(1)]
        public async Task InvalidContentLengthType() {
            await TestInvalidPacketAsync(PacketProvider.GetInvalidContentLengthIntegerTooLargePacket(), "visualstudio_py_ipcjson.InvalidHeaderError");
            await TestInvalidPacketAsync(PacketProvider.GetInvalidContentLengthNegativeIntegerPacket(), "visualstudio_py_ipcjson.InvalidHeaderError");
            await TestInvalidPacketAsync(PacketProvider.GetInvalidContentLengthNotIntegerPacket(), "visualstudio_py_ipcjson.InvalidHeaderError");
        }

        [TestMethod, Priority(1)]
        public async Task MissingContentLength() {
            await TestInvalidPacketAsync(PacketProvider.GetMissingContentLengthPacket(), "visualstudio_py_ipcjson.InvalidHeaderError");
        }

        [TestMethod, Priority(1)]
        public async Task MalformedHeader() {
            await TestInvalidPacketAsync(PacketProvider.GetMalformedHeaderPacket(), "visualstudio_py_ipcjson.InvalidHeaderError");
        }

        [TestMethod, Priority(1)]
        public async Task AdditionalHeaders() {
            await TestValidPacketAsync(PacketProvider.GetAdditionalHeadersPacket());
        }

        [TestMethod, Priority(1)]
        public async Task EmptyStream() {
            await TestNoPacketAsync(PacketProvider.GetNoPacket(), "");
        }

        [TestMethod, Priority(1)]
        public async Task UnterminatedHeader() {
            await TestNoPacketAsync(PacketProvider.GetUnterminatedPacket(), "visualstudio_py_ipcjson.InvalidHeaderError");
            await TestNoPacketAsync(PacketProvider.GetIncorrectlyTerminatedPacket(), "visualstudio_py_ipcjson.InvalidHeaderError");
        }

        private Task TestValidPacketAsync(Packet packet) {
            return TestPacketAsync(packet);
        }

        private Task TestInvalidPacketAsync(Packet packet, string expectedError) {
            return TestPacketAsync(packet, expectedError, closeStream: false);
        }

        private Task TestNoPacketAsync(Packet packet, string expectedError) {
            return TestPacketAsync(packet, expectedError, closeStream: true);
        }

        private async Task TestPacketAsync(Packet packet, string expectedError = null, bool closeStream = false) {
            using (var proc = InitConnection(PythonParsingTestPath)) {
                await WritePacketAsync(packet);
                if (closeStream) {
                    // We expect the process to be busy reading headers
                    var closed = proc.Wait(TimeSpan.FromMilliseconds(500));
                    Assert.IsFalse(closed);

                    // Close the stream so it gets unblocked
                    _clientStream.Close();
                }

                if (!proc.Wait(TimeSpan.FromSeconds(2))) {
                    proc.Kill();
                    Assert.Fail("Python process did not exit");
                }

                CheckProcessResult(proc, expectedError);
            }
        }

        private async Task WritePacketAsync(Packet packet) {
            var bytes = packet.AsBytes();
            await _clientStream.WriteAsync(bytes, 0, bytes.Length);
        }

        private ProcessOutput InitConnection(string serverScriptPath) {
            var portNum = StartClient();

            Assert.IsTrue(File.Exists(serverScriptPath), "Python test data script '{0}' was not found.".FormatUI(serverScriptPath));

            var workingDir = Path.GetDirectoryName(serverScriptPath);

            var searchPaths = new HashSet<string>();
            searchPaths.Add(IpcJsonTests.PtvsdSearchPath);
            searchPaths.Add(workingDir);

            var env = new List<KeyValuePair<string, string>>();
            env.Add(new KeyValuePair<string, string>("PYTHONPATH", string.Join(";", searchPaths)));

            var arguments = new List<string>();
            arguments.Add(serverScriptPath);
            arguments.Add("-r");
            arguments.Add(portNum.ToString());
            var proc = ProcessOutput.Run(
                PythonPaths.Python27.InterpreterPath,
                arguments,
                workingDir,
                env,
                false,
                null
            );

            if (proc.ExitCode.HasValue) {
                // Process has already exited
                proc.Wait();
                CheckProcessResult(proc);
            }

            _connected.WaitOne();

            return proc;
        }

        private void CheckProcessResult(ProcessOutput proc, string expectedError = null) {
            Console.WriteLine(String.Join(Environment.NewLine, proc.StandardOutputLines));
            Debug.WriteLine(String.Join(Environment.NewLine, proc.StandardErrorLines));
            if (!string.IsNullOrEmpty(expectedError)) {
                var matches = proc.StandardErrorLines.Where(err => err.Contains(expectedError));
                if (matches.Count() == 0) {
                    Assert.Fail(String.Join(Environment.NewLine, proc.StandardErrorLines));
                }
            } else {
                if (proc.StandardErrorLines.Any()) {
                    Assert.Fail(String.Join(Environment.NewLine, proc.StandardErrorLines));
                }
                Assert.AreEqual(0, proc.ExitCode);
            }
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
            _clientStream = new NetworkStream(socket, ownsSocket: true);
            _connected.Set();
        }
    }
}

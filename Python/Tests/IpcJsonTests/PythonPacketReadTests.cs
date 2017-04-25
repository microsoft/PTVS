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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;

namespace IpcJsonTests {
    [TestClass]
    public class PythonPacketReadTests {
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
            await TestValidPacketAsync(MakePacketFromJson(validJson1));
            await TestValidPacketAsync(MakePacketFromJson(validJson2));
        }

        [TestMethod, Priority(1)]
        public async Task ValidUnicodePackets() {
            await TestValidPacketAsync(MakePacketFromJson(validUtf8Json1));
            await TestValidPacketAsync(MakePacketFromJson(validUtf8Json2));
        }

        [TestMethod, Priority(1)]
        public async Task TruncatedJson() {
            // Valid packet, but the json is invalid because it's truncated
            for (int i = 1; i < validJson1.Length; i++) {
                await TestInvalidPacketAsync(
                    MakePacketFromJson(validJson1.Substring(0, validJson1.Length - i)),
                    "visualstudio_py_ipcjson.InvalidContentError"
                );
            }
        }

        [TestMethod, Priority(1)]
        public async Task IncorrectContentLengthUnderread() {
            // Full json is in the stream, but the header was corrupted and the
            // Content-Length value is SMALLER than it should be, so the packet body
            // will miss parts of the json at the end.
            for (int i = 1; i < validJson1.Length; i++) {
                var json = MakeBody(validJson1);
                var headers = MakeHeaders(i);
                await TestInvalidPacketAsync(
                    MakePacket(headers, json),
                    "visualstudio_py_ipcjson.InvalidContentError"
                );
            }
        }

        [TestMethod, Priority(1)]
        public async Task IncorrectContentLengthOverread() {
            // Full json is in the stream, but the header was corrupted and the
            // Content-Length value is LARGER than it should be, so the packet body
            // will include junk at the end from the next message.
            var endJunk = MakeHeaders(5);
            for (int i = 1; i < endJunk.Length; i++) {
                var json = MakeBody(validJson1);
                var headers = MakeHeaders(json.Length + i);
                await TestInvalidPacketAsync(
                    MakePacket(headers, json, endJunk),
                    "visualstudio_py_ipcjson.InvalidContentError"
                );
            }
        }

        [TestMethod, Priority(1)]
        public async Task IncorrectContentLengthOverreadEndOfStream() {
            // Full json is in the stream, but the header was corrupted and the
            // Content-Length value is LARGER than it should be, and there's no
            // more data in the stream after this.
            for (int i = 1; i < 5; i++) {
                var json = MakeBody(validJson1);
                var headers = MakeHeaders(json.Length + i);
                await TestInvalidPacketAsync(
                    MakePacket(headers, json),
                    "visualstudio_py_ipcjson.InvalidContentError"
                );
            }
        }

        [TestMethod, Priority(1)]
        public async Task InvalidContentLengthType() {
            var body = MakeBody(validJson1);
            await TestInvalidPacketAsync(
                MakePacket(Encoding.ASCII.GetBytes("Content-Length: 2147483649\r\n\r\n"), body),
                "visualstudio_py_ipcjson.InvalidHeaderError"
            );
            await TestInvalidPacketAsync(
                MakePacket(Encoding.ASCII.GetBytes("Content-Length: -1\r\n\r\n"), body),
                "visualstudio_py_ipcjson.InvalidHeaderError"
            );
            await TestInvalidPacketAsync(
                MakePacket(Encoding.ASCII.GetBytes("Content-Length: BAD\r\n\r\n"), body),
                "visualstudio_py_ipcjson.InvalidHeaderError"
            );
        }

        [TestMethod, Priority(1)]
        public async Task MissingContentLength() {
            var body = MakeBody(validJson1);
            await TestInvalidPacketAsync(
                MakePacket(Encoding.ASCII.GetBytes("From: Test\r\n\r\n"), body),
                "visualstudio_py_ipcjson.InvalidHeaderError"
            );
        }

        [TestMethod, Priority(1)]
        public async Task MalformedHeader() {
            var body = MakeBody(validJson1);
            await TestInvalidPacketAsync(MakePacket(Encoding.ASCII.GetBytes("Content-Length\r\n\r\n"), body),
                "visualstudio_py_ipcjson.InvalidHeaderError"
            );
        }

        [TestMethod, Priority(1)]
        public async Task AdditionalHeaders() {
            // Other headers are fine, we only care that Content-Length is there and valid
            var body = MakeBody(validJson1);
            await TestValidPacketAsync(
                MakePacket(Encoding.ASCII.GetBytes(string.Format("From: Test\r\nContent-Length:{0}\r\nTo: You\r\n\r\n", body.Length)), body)
            );
        }

        [TestMethod, Priority(1)]
        public async Task EmptyStream() {
            await TestUnterminatedHeaderPacketAsync(MakePacket(new byte[0], new byte[0]), "");
        }

        [TestMethod, Priority(1)]
        public async Task UnterminatedHeader() {
            await TestUnterminatedHeaderPacketAsync(
                MakePacket(Encoding.ASCII.GetBytes("NoTerminator"), new byte[0]),
                "visualstudio_py_ipcjson.InvalidHeaderError"
            );

            var body = MakeBody(validJson1);
            await TestUnterminatedHeaderPacketAsync(
                MakePacket(Encoding.ASCII.GetBytes(string.Format("Content-Length:{0}\n\n", body.Length)), body),
                "visualstudio_py_ipcjson.InvalidHeaderError"
            );
        }

        private Task TestValidPacketAsync(byte[] packet) {
            return TestPacketAsync(packet);
        }

        private Task TestInvalidPacketAsync(byte[] packet, string expectedError) {
            return TestPacketAsync(packet, expectedError, closeStream: false);
        }

        private Task TestUnterminatedHeaderPacketAsync(byte[] packet, string expectedError) {
            return TestPacketAsync(packet, expectedError, closeStream: true);
        }

        private async Task TestPacketAsync(byte[] packet, string expectedError = null, bool closeStream = false) {
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

        private async Task WritePacketAsync(byte[] packet) {
            await _clientStream.WriteAsync(packet, 0, packet.Length);
        }

        private static byte[] MakePacketFromJson(string json) {
            var encoded = MakeBody(json);
            var headers = MakeHeaders(encoded.Length);

            return MakePacket(headers, encoded);
        }

        private static byte[] MakePacket(byte[] headers, byte[] encoded, byte[] endJunk = null) {
            var stream = new MemoryStream();
            stream.Write(headers, 0, headers.Length);
            stream.Write(encoded, 0, encoded.Length);
            if (endJunk != null) {
                stream.Write(endJunk, 0, endJunk.Length);
            }
            stream.Flush();
            stream.Seek(0, SeekOrigin.Begin);

            return stream.ToArray();
        }

        private static byte[] MakeBody(string json) {
            return Encoding.UTF8.GetBytes(json);
        }

        private static byte[] MakeHeaders(int contentLength) {
            return Encoding.ASCII.GetBytes(string.Format("Content-Length: {0}\r\n\r\n", contentLength));
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

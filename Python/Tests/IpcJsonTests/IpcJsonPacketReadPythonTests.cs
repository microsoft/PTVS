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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;

namespace IpcJsonTests {
    [TestClass]
    public class IpcJsonPacketReadPythonTests {
        private Stream _clientStream;
        private readonly AutoResetEvent _connected = new AutoResetEvent(false);

        private static string PythonParsingTestPath => Path.Combine(TestData.GetPath("TestData"), "Ipc.Json", "parsing_test.py");

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        [TestInitialize]
        public void TestInit() {
            Version.AssertInstalled();
        }

        internal virtual PythonVersion Version {
            get {
                return PythonPaths.Python26 ?? PythonPaths.Python26_x64;
            }
        }

        [TestMethod, Priority(0)]
        public async Task ValidPackets() {
            await TestValidPacketAsync(PacketProvider.GetValidPacket1());
            await TestValidPacketAsync(PacketProvider.GetValidPacket2());
        }

        [TestMethod, Priority(0)]
        public async Task ValidUnicodePackets() {
            await TestValidPacketAsync(PacketProvider.GetValidUnicodePacket1());
            await TestValidPacketAsync(PacketProvider.GetValidUnicodePacket2());
        }

        [TestMethod, Priority(0)]
        public async Task TruncatedJson() {
            foreach (var packet in PacketProvider.GetTruncatedJsonPackets()) {
                await TestInvalidPacketAsync(packet);
            }
        }

        [TestMethod, Priority(0)]
        public async Task IncorrectContentLengthUnderread() {
            foreach (var packet in PacketProvider.GetIncorrectContentLengthUnderreadPackets()) {
                await TestInvalidPacketAsync(packet);
            }
        }

        [TestMethod, Priority(0)]
        public async Task IncorrectContentLengthOverread() {
            foreach (var packet in PacketProvider.GetIncorrectContentLengthOverreadPackets()) {
                await TestInvalidPacketAsync(packet);
            }
        }

        [TestMethod, Priority(0)]
        public async Task IncorrectContentLengthOverreadEndOfStream() {
            foreach (var packet in PacketProvider.GetIncorrectContentLengthOverreadEndOfStreamPackets()) {
                await TestInvalidPacketAsync(packet);
            }
        }

        [TestMethod, Priority(0)]
        public async Task InvalidContentLengthType() {
            await TestInvalidPacketAsync(PacketProvider.GetInvalidContentLengthIntegerTooLargePacket());
            await TestInvalidPacketAsync(PacketProvider.GetInvalidContentLengthNegativeIntegerPacket());
            await TestInvalidPacketAsync(PacketProvider.GetInvalidContentLengthNotIntegerPacket());
        }

        [TestMethod, Priority(0)]
        public async Task MissingContentLength() {
            await TestInvalidPacketAsync(PacketProvider.GetMissingContentLengthPacket());
        }

        [TestMethod, Priority(0)]
        public async Task MalformedHeader() {
            await TestInvalidPacketAsync(PacketProvider.GetMalformedHeaderPacket());
        }

        [TestMethod, Priority(0)]
        public async Task AdditionalHeaders() {
            await TestValidPacketAsync(PacketProvider.GetAdditionalHeadersPacket());
        }

        [TestMethod, Priority(0)]
        public async Task EmptyStream() {
            await TestNoPacketAsync(PacketProvider.GetNoPacket());
        }

        [TestMethod, Priority(0)]
        public async Task UnterminatedHeader() {
            await TestNoPacketAsync(PacketProvider.GetUnterminatedPacket());
            await TestNoPacketAsync(PacketProvider.GetIncorrectlyTerminatedPacket());
        }

        private Task TestValidPacketAsync(Packet packet) {
            Assert.IsFalse(packet.BadHeaders || packet.BadContent);
            return TestPacketAsync(packet);
        }

        private Task TestInvalidPacketAsync(Packet packet) {
            Assert.IsTrue(packet.BadHeaders || packet.BadContent);
            return TestPacketAsync(packet,
                packet.BadHeaders ? "ptvsd.ipcjson.InvalidHeaderError" : "ptvsd.ipcjson.InvalidContentError",
                closeStream: packet.ReadPastEndOfStream
            );
        }

        private Task TestNoPacketAsync(Packet packet) {
            string expectedError = null;
            if (packet.BadHeaders) {
                expectedError = "ptvsd.ipcjson.InvalidHeaderError";
            } else if (packet.BadContent) {
                expectedError = "ptvsd.ipcjson.InvalidContentError";
            }
            return TestPacketAsync(packet, expectedError, closeStream: true);
        }

        private async Task TestPacketAsync(Packet packet, string expectedError = null, bool closeStream = false) {
            using (var proc = InitConnection(PythonParsingTestPath)) {
                var bytes = packet.AsBytes();
                await _clientStream.WriteAsync(bytes, 0, bytes.Length);
                if (closeStream) {
                    // We expect the process to be busy reading headers and not exit
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

        private ProcessOutput InitConnection(string serverScriptPath) {
            var portNum = StartClient();

            Assert.IsTrue(File.Exists(serverScriptPath), "Python test data script '{0}' was not found.".FormatUI(serverScriptPath));

            var workingDir = Path.GetDirectoryName(serverScriptPath);

            var searchPaths = new HashSet<string>();
            searchPaths.Add(IpcJsonConnectionTests.PtvsdSearchPath);
            searchPaths.Add(workingDir);

            var env = new List<KeyValuePair<string, string>>();
            env.Add(new KeyValuePair<string, string>(Version.Configuration.PathEnvironmentVariable, string.Join(";", searchPaths)));

            var arguments = new List<string>();
            arguments.Add(serverScriptPath);
            arguments.Add("-r");
            arguments.Add(portNum.ToString());
            var proc = ProcessOutput.Run(
                Version.InterpreterPath,
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

            _connected.WaitOne(10000);

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

    [TestClass]
    public class IpcJsonPacketReadPythonTestsIpy : IpcJsonPacketReadPythonTests {
        [ClassInitialize]
        public static new void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        internal override PythonVersion Version {
            get {
                return PythonPaths.IronPython27 ?? PythonPaths.IronPython27_x64;
            }
        }
    }

    [TestClass]
    public class IpcJsonPacketReadPythonTests27 : IpcJsonPacketReadPythonTests {
        [ClassInitialize]
        public static new void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        internal override PythonVersion Version {
            get {
                return PythonPaths.Python27 ?? PythonPaths.Python27_x64;
            }
        }
    }

    [TestClass]
    public class IpcJsonPacketReadPythonTests31 : IpcJsonPacketReadPythonTests {
        [ClassInitialize]
        public static new void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        internal override PythonVersion Version {
            get {
                return PythonPaths.Python31 ?? PythonPaths.Python31_x64;
            }
        }
    }

    [TestClass]
    public class IpcJsonPacketReadPythonTests32 : IpcJsonPacketReadPythonTests {
        [ClassInitialize]
        public static new void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        internal override PythonVersion Version {
            get {
                return PythonPaths.Python32 ?? PythonPaths.Python32_x64;
            }
        }
    }

    [TestClass]
    public class IpcJsonPacketReadPythonTests33 : IpcJsonPacketReadPythonTests {
        [ClassInitialize]
        public static new void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        internal override PythonVersion Version {
            get {
                return PythonPaths.Python33 ?? PythonPaths.Python33_x64;
            }
        }
    }

    [TestClass]
    public class IpcJsonPacketReadPythonTests34 : IpcJsonPacketReadPythonTests {
        [ClassInitialize]
        public static new void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        internal override PythonVersion Version {
            get {
                return PythonPaths.Python34 ?? PythonPaths.Python34_x64;
            }
        }
    }

    [TestClass]
    public class IpcJsonPacketReadPythonTests35 : IpcJsonPacketReadPythonTests {
        [ClassInitialize]
        public static new void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        internal override PythonVersion Version {
            get {
                return PythonPaths.Python35 ?? PythonPaths.Python35_x64;
            }
        }
    }

    [TestClass]
    public class IpcJsonPacketReadPythonTests36 : IpcJsonPacketReadPythonTests {
        [ClassInitialize]
        public static new void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        internal override PythonVersion Version {
            get {
                return PythonPaths.Python36 ?? PythonPaths.Python36_x64;
            }
        }
    }
}

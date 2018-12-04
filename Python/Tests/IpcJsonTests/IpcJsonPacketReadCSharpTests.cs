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

using System.IO;
using System.Threading.Tasks;
using Microsoft.PythonTools.Ipc.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IpcJsonTests {
    [TestClass]
    public class IpcJsonPacketReadCSharpTests {
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

        private static async Task TestValidPacketAsync(Packet packet) {
            Assert.IsFalse(packet.BadHeaders || packet.BadContent);
            var reader = new ProtocolReader(packet.AsStream());
            var obj = await Connection.ReadPacketAsJObject(reader);
            Assert.IsNotNull(obj);
        }

        private static async Task TestNoPacketAsync(Packet packet) {
            var reader = new ProtocolReader(packet.AsStream());
            var obj = await Connection.ReadPacketAsJObject(reader);
            Assert.IsNull(obj);
        }

        private static async Task TestInvalidPacketAsync(Packet packet) {
            Assert.IsTrue(packet.BadHeaders || packet.BadContent);
            var reader = new ProtocolReader(packet.AsStream());
            try {
                var obj = await Connection.ReadPacketAsJObject(reader);
                Assert.IsNotNull(obj);
                Assert.Fail("Failed to raise InvalidDataException.");
            } catch (InvalidDataException) {
            }
        }
    }
}

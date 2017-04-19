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
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Ipc.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IpcJsonTests {
    [TestClass]
    public class PacketReadTests {
        private const string validJson1 = @"{'request_seq':1,'success':true,'command':'initialize','message':null,'body':{'failedLoads':[],'error':null},'type':'response','seq':2}";
        private const string validJson2 = @"{'event':'childFileAnalyzed','body':{'filename':'C:\\Projects\\RemoteDebugApp\\RemoteDebugApp.py','fileId':1,'isTemporaryFile':false,'suppressErrorList':false},'type':'event','seq':5}";
        private const string validUtf8Json1 = @"{'event':'childFileAnalyzed','body':{'filename':'C:\\Projects\\RemoteDebugApp\\この文は、テストです。私はこれがうまく願っています。.py','fileId':1,'isTemporaryFile':false,'suppressErrorList':false},'type':'event','seq':5}";
        private const string validUtf8Json2 = @"{'event':'childFileAnalyzed','body':{'filename':'C:\\Projects\\RemoteDebugApp\\é.py','fileId':1,'isTemporaryFile':false,'suppressErrorList':false},'type':'event','seq':5}";

        [TestMethod, Priority(1)]
        public async Task ValidPackets() {
            await ReadValidPacket(MakePacketFromJson(validJson1));
            await ReadValidPacket(MakePacketFromJson(validJson2));
        }

        [TestMethod, Priority(1)]
        public async Task ValidUnicodePackets() {
            await ReadValidPacket(MakePacketFromJson(validUtf8Json1));
            await ReadValidPacket(MakePacketFromJson(validUtf8Json2));
        }

        [TestMethod, Priority(1)]
        public async Task TruncatedJson() {
            // Valid packet, but the json is invalid because it's truncated
            for (int i = 1; i < validJson1.Length; i++) {
                await ReadInvalidPacket(MakePacketFromJson(validJson1.Substring(0, validJson1.Length - i)));
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
                await ReadInvalidPacket(MakePacket(headers, json));
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
                await ReadInvalidPacket(MakePacket(headers, json, endJunk));
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
                await ReadInvalidPacket(MakePacket(headers, json));
            }
        }

        [TestMethod, Priority(1)]
        public async Task InvalidContentLengthType() {
            var body = MakeBody(validJson1);
            await ReadInvalidPacket(MakePacket(Encoding.UTF8.GetBytes("Content-Length: 2147483649\r\n\r\n"), body));
            await ReadInvalidPacket(MakePacket(Encoding.UTF8.GetBytes("Content-Length: -1\r\n\r\n"), body));
            await ReadInvalidPacket(MakePacket(Encoding.UTF8.GetBytes("Content-Length: BAD\r\n\r\n"), body));
        }

        [TestMethod, Priority(1)]
        public async Task MissingContentLength() {
            var body = MakeBody(validJson1);
            await ReadInvalidPacket(MakePacket(Encoding.UTF8.GetBytes("From: Test\r\n\r\n"), body));
        }

        [TestMethod, Priority(1)]
        public async Task MalformedHeader() {
            var body = MakeBody(validJson1);
            await ReadInvalidPacket(MakePacket(Encoding.UTF8.GetBytes("Content-Length\r\n\r\n"), body));
        }

        [TestMethod, Priority(1)]
        public async Task AdditionalHeaders() {
            // Other headers are fine, we only care that Content-Length is there and valid
            var body = MakeBody(validJson1);
            await ReadValidPacket(MakePacket(Encoding.UTF8.GetBytes(string.Format("From: Test\r\nContent-Length:{0}\r\nTo: You\r\n\r\n", body.Length)), body));
        }

        [TestMethod, Priority(1)]
        public async Task EmptyStream() {
            await ReadNullPacket(MakePacket(new byte[0], new byte[0]));
        }

        [TestMethod, Priority(1)]
        public async Task UnterminatedHeader() {
            await ReadNullPacket(MakePacket(Encoding.ASCII.GetBytes("NoTerminator"), new byte[0]));

            var body = MakeBody(validJson1);
            await ReadNullPacket(MakePacket(Encoding.UTF8.GetBytes(string.Format("Content-Length:{0}\n\n", body.Length)), body));
        }

        private static async Task ReadValidPacket(Stream stream) {
            var reader = new ProtocolReader(stream);
            var packet = await Connection.ReadPacketAsJObject(reader);
            Assert.IsNotNull(packet);
        }

        private static async Task ReadNullPacket(Stream stream) {
            var reader = new ProtocolReader(stream);
            var packet = await Connection.ReadPacketAsJObject(reader);
            Assert.IsNull(packet);
        }

        private static async Task ReadInvalidPacket(Stream stream) {
            var reader = new ProtocolReader(stream);
            try {
                var packet = await Connection.ReadPacketAsJObject(reader);
                Assert.IsNotNull(packet);
                Assert.Fail("Failed to raise InvalidDataException.");
            } catch (InvalidDataException) {
            }
        }

        private static Stream MakePacketFromJson(string json) {
            var encoded = MakeBody(json);
            var headers = MakeHeaders(encoded.Length);

            return MakePacket(headers, encoded);
        }

        private static Stream MakePacket(byte[] headers, byte[] encoded, byte[] endJunk = null) {
            var stream = new MemoryStream();
            stream.Write(headers, 0, headers.Length);
            stream.Write(encoded, 0, encoded.Length);
            if (endJunk != null) {
                stream.Write(endJunk, 0, endJunk.Length);
            }
            stream.Flush();
            stream.Seek(0, SeekOrigin.Begin);

            return stream;
        }

        private static byte[] MakeBody(string json) {
            return Encoding.UTF8.GetBytes(json);
        }

        private static byte[] MakeHeaders(int contentLength) {
            return Encoding.ASCII.GetBytes(string.Format("Content-Length: {0}\r\n\r\n", contentLength));
        }
    }
}

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

using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IpcJsonTests {
    class PacketProvider {
        private static string validJson1 = @"{'request_seq':1,'success':true,'command':'initialize','message':null,'body':{'failedLoads':[],'error':null},'type':'response','seq':2}".Replace('\'', '"');
        private static string validJson2 = @"{'event':'childFileAnalyzed','body':{'filename':'C:\\Projects\\RemoteDebugApp\\RemoteDebugApp.py','fileId':1,'isTemporaryFile':false,'suppressErrorList':false},'type':'event','seq':5}".Replace('\'', '"');
        private static string validUtf8Json1 = @"{'event':'childFileAnalyzed','body':{'filename':'C:\\Projects\\RemoteDebugApp\\この文は、テストです。私はこれがうまく願っています。.py','fileId':1,'isTemporaryFile':false,'suppressErrorList':false},'type':'event','seq':5}".Replace('\'', '"');
        private static string validUtf8Json2 = @"{'event':'childFileAnalyzed','body':{'filename':'C:\\Projects\\RemoteDebugApp\\é.py','fileId':1,'isTemporaryFile':false,'suppressErrorList':false},'type':'event','seq':5}".Replace('\'', '"');

        public static Packet GetValidPacket1() => MakePacketFromJson(validJson1);

        public static Packet GetValidPacket2() => MakePacketFromJson(validJson2);

        public static Packet GetValidUnicodePacket1() => MakePacketFromJson(validUtf8Json1);

        public static Packet GetValidUnicodePacket2() => MakePacketFromJson(validUtf8Json2);

        public static Packet GetNoPacket() => MakePacket(new byte[0], new byte[0]);

        public static Packet GetUnterminatedPacket() => MakePacket(Encoding.ASCII.GetBytes("NoTerminator"), new byte[0], badHeaders: true);

        public static Packet GetInvalidContentLengthIntegerTooLargePacket() {
            return MakePacket(Encoding.ASCII.GetBytes("Content-Length: 2147483649\r\n\r\n"), MakeBody(validJson1), badHeaders: true);
        }

        public static Packet GetInvalidContentLengthNegativeIntegerPacket() {
            return MakePacket(Encoding.ASCII.GetBytes("Content-Length: -1\r\n\r\n"), MakeBody(validJson1), badHeaders: true);
        }

        public static Packet GetInvalidContentLengthNotIntegerPacket() {
            return MakePacket(Encoding.ASCII.GetBytes("Content-Length: BAD\r\n\r\n"), MakeBody(validJson1), badHeaders: true);
        }

        public static Packet GetMissingContentLengthPacket() {
            return MakePacket(Encoding.ASCII.GetBytes("From: Test\r\n\r\n"), MakeBody(validJson1), badHeaders: true);
        }

        public static Packet GetMalformedHeaderPacket() {
            return MakePacket(Encoding.ASCII.GetBytes("Content-Length\r\n\r\n"), MakeBody(validJson1), badHeaders: true);
        }

        public static Packet GetAdditionalHeadersPacket() {
            // Other headers are fine, we only care that Content-Length is there and valid
            var body = MakeBody(validJson1);
            return MakePacket(Encoding.ASCII.GetBytes(string.Format("From: Test\r\nContent-Length:{0}\r\nTo: You\r\n\r\n", body.Length)), body);
        }

        public static Packet GetIncorrectlyTerminatedPacket() {
            var body = MakeBody(validJson1);
            return MakePacket(Encoding.ASCII.GetBytes(string.Format("Content-Length:{0}\n\n", body.Length)), body, badHeaders: true);
        }

        public static IEnumerable<Packet> GetTruncatedJsonPackets() {
            // Valid packet, but the json is invalid because it's truncated
            for (int i = 1; i < validJson1.Length; i += 3) {
                yield return MakePacketFromJson(validJson1.Substring(0, validJson1.Length - i), badContent: true);
            }
        }

        public static IEnumerable<Packet> GetIncorrectContentLengthUnderreadPackets() {
            // Full json is in the stream, but the header was corrupted and the
            // Content-Length value is SMALLER than it should be, so the packet body
            // will miss parts of the json at the end.
            for (int i = 1; i < validJson1.Length; i += 3) {
                var json = MakeBody(validJson1);
                var headers = MakeHeaders(i);
                yield return MakePacket(headers, json, badContent: true);
            }
        }

        public static IEnumerable<Packet> GetIncorrectContentLengthOverreadPackets() {
            // Full json is in the stream, but the header was corrupted and the
            // Content-Length value is LARGER than it should be, so the packet body
            // will include junk at the end from the next message.
            var endJunk = MakeHeaders(5);
            for (int i = 1; i < endJunk.Length; i++) {
                var json = MakeBody(validJson1);
                var headers = MakeHeaders(json.Length + i);
                yield return MakePacket(headers, json, endJunk, badContent: true);
            }
        }

        public static IEnumerable<Packet> GetIncorrectContentLengthOverreadEndOfStreamPackets() {
            // Full json is in the stream, but the header was corrupted and the
            // Content-Length value is LARGER than it should be, and there's no
            // more data in the stream after this.
            for (int i = 1; i < 5; i++) {
                var json = MakeBody(validJson1);
                var headers = MakeHeaders(json.Length + i);
                yield return MakePacket(headers, json, badContent: true, blocked: true);
            }
        }

        private static Packet MakePacketFromJson(string json, bool badContent = false) {
            var encoded = MakeBody(json);
            var headers = MakeHeaders(encoded.Length);

            return MakePacket(headers, encoded, badContent: badContent);
        }

        private static Packet MakePacket(byte[] headers, byte[] encoded, byte[] endJunk = null, bool badHeaders = false, bool badContent = false, bool blocked = false) {
            return new Packet(headers, encoded, endJunk, badHeaders, badContent, blocked);
        }

        private static byte[] MakeBody(string json) {
            return Encoding.UTF8.GetBytes(json);
        }

        private static byte[] MakeHeaders(int contentLength) {
            return Encoding.ASCII.GetBytes(string.Format("Content-Length: {0}\r\n\r\n", contentLength));
        }
    }

    class Packet {
        private List<byte> _data = new List<byte>();
        public bool BadHeaders { get; }
        public bool BadContent { get; }
        public bool ReadPastEndOfStream { get; }

        public Packet(byte[] headers, byte[] content, byte[] endJunk = null, bool badHeaders = false, bool badContent = false, bool readPastEndOfStream = false) {
            _data.AddRange(headers);
            _data.AddRange(content);
            if (endJunk != null) {
                _data.AddRange(endJunk);
            }
            BadHeaders = badHeaders;
            BadContent = badContent;
            ReadPastEndOfStream = readPastEndOfStream;
        }

        public Stream AsStream() {
            var stream = new MemoryStream();
            var data = AsBytes();
            stream.Write(data, 0, data.Length);
            stream.Flush();
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        public byte[] AsBytes() {
            return _data.ToArray();
        }
    }
}

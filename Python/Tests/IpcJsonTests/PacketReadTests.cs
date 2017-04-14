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
        [TestMethod, Priority(1)]
        public async Task ReadPacket() {
            string data = @"Content-Length: 135

{'request_seq':1,'success':true,'command':'initialize','message':null,'body':{'failedLoads':[],'error':null},'type':'response','seq':2}";
            using (var stream = new MemoryStream(UTF8Encoding.Default.GetBytes(data)))
            using (var reader = new StreamReader(stream)) {
                var packet = await Connection.ReadPacketAsJObject(reader);
            }
        }

        //{"request_seq":1,"success":true,"command":"initialize","message":null,"body":{"failedLoads":[],"error":null},"type":"response","seq":2}
        //{"event":"childFileAnalyzed","body":{"filename":"C:\\Users\\huvalo\\RemoteDebugApp\\RemoteDebugApp.py","fileId":1,"isTemporaryFile":false,"suppressErrorList":false},"type":"event","seq":5}

        private static byte[] MakeValidPacket(string json) {
            var encoded = UTF8Encoding.Default.GetBytes(json);
            var header = UTF8Encoding.Default.GetBytes(string.Format("Content-Length: {0}", encoded.Length));
            var packet = new byte[header.Length + encoded.Length];
            Array.Copy(header, packet, header.Length);
            Array.Copy(encoded, 0, packet, header.Length, encoded.Length);
            return packet;
        }
    }
}

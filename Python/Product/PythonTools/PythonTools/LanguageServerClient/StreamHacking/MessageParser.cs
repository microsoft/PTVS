using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.PythonTools.LanguageServerClient.StreamHacking {
    static class MessageParser {
        public static JObject Deserialize(StreamData data) {
            // Go through the byte stream until we get past the http header
            var httpHeaderOffset = -1;
            for (int i = data.offset + 4; i < data.bytes.Length; i++) {
                if (data.bytes[i - 3] == '\r' && data.bytes[i - 2] == '\n' && data.bytes[i - 1] == '\r' && data.bytes[i] == '\n') {
                    // Two line feeds in a row. Means an empty line
                    httpHeaderOffset = i + 1;
                    break;
                }
            }

            if (httpHeaderOffset > 0) {
                try {
                    var messageJson = System.Text.Encoding.UTF8.GetString(data.bytes, httpHeaderOffset, data.count - (httpHeaderOffset - data.offset));
                    var messageJsonLength = System.Text.Encoding.UTF8.GetBytes(messageJson).Length;
                    return JObject.Parse(messageJson);
                } catch {

                }

            }
            return null;
        }

        public static StreamData Serialize(JObject message) {
            var newJson = message.ToString(Newtonsoft.Json.Formatting.None);
            var newJsonLength = System.Text.Encoding.UTF8.GetBytes(newJson).Length;

            // Http header is just 'Content-Length: number'
            var fullText = $"Content-Length: {newJsonLength}\r\n\r\n{newJson}";
            var newBuffer = System.Text.Encoding.UTF8.GetBytes(fullText);
            return new StreamData { bytes = newBuffer, offset = 0, count = newBuffer.Length };
        }
    }
}

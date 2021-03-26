using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.PythonTools.LanguageServerClient.StreamHacking {
    static class MessageParser {
        private static string ContentLengthHeader = "Content-Length: ";

        public static JObject Deserialize(StreamData data) {
            // Go through the byte stream until we get past the http header
            var contentLength = data.count;
            var httpHeaderOffset = -1;
            for (int i = data.offset + 4; i < data.offset+data.count && i < data.bytes.Length; i++) {
                if (data.bytes[i - 3] == '\r' && data.bytes[i - 2] == '\n' && data.bytes[i - 1] == '\r' && data.bytes[i] == '\n') {
                    // Two line feeds in a row. Means an empty line. There should be a number before this which is the content length
                    httpHeaderOffset = i + 1;

                    // Go backwards until we find 'Content-Length: '
                    var headerEnd = i - 3;
                    var headerStart = i - ContentLengthHeader.Length;
                    while (headerStart >= 0 && System.Text.Encoding.UTF8.GetString(data.bytes, headerStart, ContentLengthHeader.Length) != ContentLengthHeader) {
                        headerStart--;
                    }

                    // Pull off just the 'number' next to it.
                    if (headerStart >= 0) {
                        var contentLengthNumberStrLength = headerEnd - (headerStart + ContentLengthHeader.Length);
                        var contentLengthStr = System.Text.Encoding.UTF8.GetString(data.bytes, headerStart + ContentLengthHeader.Length, contentLengthNumberStrLength);
                        Int32.TryParse(contentLengthStr, out contentLength);
                    }
                    break;
                }
            }

            if (httpHeaderOffset > 0) {
                try {
                    contentLength = Math.Min(contentLength, data.bytes.Length - httpHeaderOffset);
                    var messageJson = System.Text.Encoding.UTF8.GetString(data.bytes, httpHeaderOffset, contentLength);
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
            var fullText = $"{ContentLengthHeader}{newJsonLength}\r\n\r\n{newJson}";
            var newBuffer = System.Text.Encoding.UTF8.GetBytes(fullText);
            return new StreamData { bytes = newBuffer, offset = 0, count = newBuffer.Length };
        }
    }
}

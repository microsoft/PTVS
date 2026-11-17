using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.PythonTools.LanguageServerClient {
    internal static class ProtocolJsonSerialization {
        internal static JsonSerializerOptions Options { get; } = CreateOptions();

        private static JsonSerializerOptions CreateOptions() {
            var options = new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
            ProtocolJsonSerializerOptions.Configure(options);
            return options;
        }

        internal sealed class DocumentUriConverter : JsonConverter<Uri> {
            public override Uri Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
                new Uri(reader.GetString(), UriKind.Absolute);

            public override void Write(Utf8JsonWriter writer, Uri value, JsonSerializerOptions options) =>
                writer.WriteStringValue(value.AbsoluteUri);
        }
    }
}

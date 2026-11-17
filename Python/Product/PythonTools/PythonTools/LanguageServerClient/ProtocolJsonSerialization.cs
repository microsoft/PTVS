using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using LegacyJson = Newtonsoft.Json;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.PythonTools.LanguageServerClient {
    internal static class ProtocolJsonSerialization {
        internal static JsonSerializerOptions Options { get; } = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver {
                Modifiers = { ConfigureProtocolContract }
            },
            Converters = {
                new DocumentUriConverter(),
                new MarkupKindConverter(),
                new SumTypeConverter<string, LSP.MarkupContent>(value => value.ValueKind == JsonValueKind.String),
                new SumTypeConverter<LSP.TextEdit, LSP.InsertReplaceEdit>(value => value.TryGetProperty("range", out _)),
                new SumTypeConverter<LSP.Range, LSP.InsertReplaceRange>(value => value.TryGetProperty("start", out _))
            }
        };

        private static void ConfigureProtocolContract(JsonTypeInfo typeInfo) {
            if (typeInfo.Type.Assembly != typeof(LSP.Position).Assembly || typeInfo.Kind != JsonTypeInfoKind.Object) {
                return;
            }

            // The feed's protocol types describe their wire contracts with DataMember and Newtonsoft attributes.
            for (var i = typeInfo.Properties.Count - 1; i >= 0; i--) {
                var property = typeInfo.Properties[i];
                var member = property.AttributeProvider as MemberInfo;
                var dataMember = member?.GetCustomAttribute<DataMemberAttribute>();
                if (dataMember == null) {
                    typeInfo.Properties.RemoveAt(i);
                    continue;
                }

                if (dataMember.Name != null) {
                    property.Name = dataMember.Name;
                }
                property.IsRequired = dataMember.IsRequired;

                var legacyProperty = member.GetCustomAttribute<LegacyJson.JsonPropertyAttribute>();
                var ignoreNull = legacyProperty?.NullValueHandling == LegacyJson.NullValueHandling.Ignore;
                var ignoreDefault = legacyProperty != null
                    && (legacyProperty.DefaultValueHandling & LegacyJson.DefaultValueHandling.Ignore) != 0;
                if (ignoreNull || ignoreDefault) {
                    var defaultValue = member.GetCustomAttribute<DefaultValueAttribute>()?.Value
                        ?? (property.PropertyType.IsValueType ? Activator.CreateInstance(property.PropertyType) : null);
                    property.ShouldSerialize = (_, value) =>
                        (!ignoreNull || value != null) && (!ignoreDefault || !Equals(value, defaultValue));
                }
            }
        }

        internal sealed class DocumentUriConverter : JsonConverter<Uri> {
            public override Uri Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
                new Uri(reader.GetString(), UriKind.Absolute);

            public override void Write(Utf8JsonWriter writer, Uri value, JsonSerializerOptions options) =>
                writer.WriteStringValue(value.AbsoluteUri);
        }

        private sealed class MarkupKindConverter : JsonConverter<LSP.MarkupKind> {
            public override LSP.MarkupKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
                new LSP.MarkupKind(reader.GetString());

            public override void Write(Utf8JsonWriter writer, LSP.MarkupKind value, JsonSerializerOptions options) =>
                writer.WriteStringValue(value.Value);
        }

        private sealed class SumTypeConverter<TFirst, TSecond>(Func<JsonElement, bool> isFirst) : JsonConverter<LSP.SumType<TFirst, TSecond>> {
            public override LSP.SumType<TFirst, TSecond> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
                using var document = JsonDocument.ParseValue(ref reader);
                var value = document.RootElement;
                return isFirst(value)
                    ? new LSP.SumType<TFirst, TSecond>(value.Deserialize<TFirst>(options))
                    : new LSP.SumType<TFirst, TSecond>(value.Deserialize<TSecond>(options));
            }

            public override void Write(Utf8JsonWriter writer, LSP.SumType<TFirst, TSecond> value, JsonSerializerOptions options) =>
                JsonSerializer.Serialize(writer, value.Value, options);
        }
    }
}

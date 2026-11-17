using System;
using System.Linq;
using System.Text.Json;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.PythonTools.LanguageServerClient {
    /// <summary>
    /// Class used for converting LSP results from JSON structures to LSP objects
    /// </summary>
    internal static class ResultConverter {
        internal static object[] ConvertResult(object result) {
            if (result == null) {
                return Array.Empty<object>();
            }

            if (result is not JsonElement json) {
                throw new ArgumentException("Expected a JSON result.", nameof(result));
            }

            if (json.ValueKind == JsonValueKind.Array) {
                var first = json.EnumerateArray().FirstOrDefault();
                if (HasProperty(first, "range") && HasProperty(first, "kind")) {
                    return json.Deserialize<LSP.DocumentSymbol[]>(ProtocolJsonSerialization.Options);
                } else if (HasProperty(first, "location") && HasProperty(first, "kind")) {
                    return json.Deserialize<LSP.SymbolInformation[]>(ProtocolJsonSerialization.Options);
                } else if (HasProperty(first, "uri")) {
                    return json.Deserialize<LSP.Location[]>(ProtocolJsonSerialization.Options);
                } else if (HasProperty(first, "label")) {
                    return new LSP.CompletionList[] { new LSP.CompletionList { Items = json.Deserialize<LSP.CompletionItem[]>(ProtocolJsonSerialization.Options) } };
                }
            } else if (json.ValueKind == JsonValueKind.Object) {
                if (HasProperty(json, "range") && HasProperty(json, "kind")) {
                    return new LSP.DocumentSymbol[] { json.Deserialize<LSP.DocumentSymbol>(ProtocolJsonSerialization.Options) };
                } else if (HasProperty(json, "location") && HasProperty(json, "kind")) {
                    return new LSP.SymbolInformation[] { json.Deserialize<LSP.SymbolInformation>(ProtocolJsonSerialization.Options) };
                } else if (HasProperty(json, "uri")) {
                    return new LSP.Location[] { json.Deserialize<LSP.Location>(ProtocolJsonSerialization.Options) };
                } else if (HasProperty(json, "isIncomplete")) {
                    return new LSP.CompletionList[] { json.Deserialize<LSP.CompletionList>(ProtocolJsonSerialization.Options) };
                }
            }

            return Array.Empty<object>();
        }

        private static bool HasProperty(JsonElement json, string name) =>
            json.ValueKind == JsonValueKind.Object && json.TryGetProperty(name, out _);
    }
}

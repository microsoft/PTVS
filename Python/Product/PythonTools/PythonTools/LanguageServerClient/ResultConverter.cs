using System;
using System.Linq;
#if DEV18_OR_LATER
using System.Text.Json;
#endif
using Newtonsoft.Json.Linq;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.PythonTools.LanguageServerClient {
    /// <summary>
    /// Class used for converting LSP results from JSON structures to LSP objects
    /// </summary>
    internal static class ResultConverter {
        internal static object[] ConvertResult(object result) {
            var jToken = ConvertToJToken(result);
            if (jToken != null) {
                var jsonSerializer = new Newtonsoft.Json.JsonSerializer();

                if (jToken.Type == JTokenType.Array) {
                    var jArray = (JArray)jToken;
                    if (jArray.Any()) {
                        if (IsDocumentSymbol(jArray.First)) {
                            return jArray.ToObject<LSP.DocumentSymbol[]>(jsonSerializer);
                        } else if (IsSymbolInformation(jArray.First)) {
                            return jArray.ToObject<LSP.SymbolInformation[]>(jsonSerializer);
                        } else if (IsLocation(jArray.First)) {
                            return jArray.ToObject<LSP.Location[]>(jsonSerializer);
                        }
                    }
                } else if (jToken.Type == JTokenType.Object) {
                    var jObject = (JObject)jToken;
                    if (IsDocumentSymbol(jObject)) {
                        return new LSP.DocumentSymbol[] { jObject.ToObject<LSP.DocumentSymbol>(jsonSerializer) };
                    } else if (IsSymbolInformation(jObject)) {
                        return new LSP.SymbolInformation[] { jObject.ToObject<LSP.SymbolInformation>(jsonSerializer) };
                    } else if (IsLocation(jObject)) {
                        return new LSP.Location[] { jObject.ToObject<LSP.Location>(jsonSerializer) };
                    } else if (IsCompletionList(jObject)) {
                        return new LSP.CompletionList[] { jObject.ToObject<LSP.CompletionList>(jsonSerializer) };
                    }
                }
            }

            return Array.Empty<object>();
        }

        private static JToken ConvertToJToken(object result) {
            if (result is JToken jToken) {
                return jToken;
            }

#if DEV18_OR_LATER
            if (result is JsonDocument jsonDocument) {
                return ConvertToJToken(jsonDocument.RootElement);
            }

            if (result is JsonElement jsonElement &&
                jsonElement.ValueKind != JsonValueKind.Null &&
                jsonElement.ValueKind != JsonValueKind.Undefined) {
                return JToken.Parse(jsonElement.GetRawText());
            }
#endif

            return null;
        }

        private static bool IsDocumentSymbol(JToken jobject) {
            return (jobject != null && jobject["range"] != null && jobject["kind"] != null);
        }
        private static bool IsSymbolInformation(JToken jobject) {
            return (jobject != null && jobject["location"] != null && jobject["kind"] != null);
        }
        private static bool IsLocation(JToken jobject) {
            return (jobject != null && jobject["uri"] != null);
        }

        private static bool IsCompletionList(JToken jobject) {
            return (jobject != null && jobject["isIncomplete"] != null);
        }
    }
}

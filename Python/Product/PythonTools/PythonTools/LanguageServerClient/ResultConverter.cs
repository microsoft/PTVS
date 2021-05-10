using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.PythonTools.LanguageServerClient {
    /// <summary>
    /// Class used for converting LSP results from JSON structures to LSP objects
    /// </summary>
    internal static class ResultConverter {
        internal static object[] ConvertResult(JToken jToken) {
            if (jToken != null) {
                var jsonSerializer = new JsonSerializer();

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

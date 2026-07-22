using System;
#if DEV18_OR_LATER
using System.Text.Json;
#else
using System.Globalization;
#endif
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Editor;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.Protocol;
#if !DEV18_OR_LATER
using Newtonsoft.Json.Linq;
#endif

namespace Microsoft.PythonTools.LanguageServerClient {
#if !DEV18_OR_LATER
#pragma warning disable CS0618
#endif
    internal sealed class PythonSignatureHelpMiddleLayer :
#if DEV18_OR_LATER
        ILanguageClientMiddleLayer2<JsonDocument> {
#else
        ILanguageClientMiddleLayer {
#endif
        private LanguagePreferences _languagePreferences;

        internal void Initialize(LanguagePreferences languagePreferences) {
            Volatile.Write(ref _languagePreferences, languagePreferences ?? throw new ArgumentNullException(nameof(languagePreferences)));
        }

        public bool CanHandle(string methodName) =>
            string.Equals(methodName, Methods.TextDocumentSignatureHelpName, StringComparison.Ordinal);

#if DEV18_OR_LATER
        public Task<JsonDocument> HandleRequestAsync(string methodName, JsonDocument methodParam, Func<JsonDocument, Task<JsonDocument>> sendRequest) {
            if (ShouldSuppressAutomaticSignatureHelp(methodParam)) {
                return Task.FromResult<JsonDocument>(null);
            }

            return sendRequest(methodParam);
        }

        public Task HandleNotificationAsync(string methodName, JsonDocument methodParam, Func<JsonDocument, Task> sendNotification) =>
            sendNotification(methodParam);

        private bool ShouldSuppressAutomaticSignatureHelp(JsonDocument methodParam) {
            var languagePreferences = Volatile.Read(ref _languagePreferences);
            if (languagePreferences == null || languagePreferences.AutoListParams || methodParam == null) {
                return false;
            }

            var request = methodParam.RootElement;
            if (request.ValueKind != JsonValueKind.Object ||
                !request.TryGetProperty("context", out var context) ||
                context.ValueKind != JsonValueKind.Object ||
                !context.TryGetProperty("triggerKind", out var triggerKindElement) ||
                triggerKindElement.ValueKind != JsonValueKind.Number ||
                !context.TryGetProperty("isRetrigger", out var isRetrigger) ||
                (isRetrigger.ValueKind != JsonValueKind.True && isRetrigger.ValueKind != JsonValueKind.False) ||
                isRetrigger.GetBoolean() ||
                (context.TryGetProperty("activeSignatureHelp", out var activeSignatureHelp) &&
                    activeSignatureHelp.ValueKind != JsonValueKind.Null) ||
                !triggerKindElement.TryGetInt32(out var triggerKind)) {
                return false;
            }

            return triggerKind == (int)SignatureHelpTriggerKind.TriggerCharacter ||
                triggerKind == (int)SignatureHelpTriggerKind.ContentChange;
        }
#else
        public Task<JToken> HandleRequestAsync(string methodName, JToken methodParam, Func<JToken, Task<JToken>> sendRequest) {
            if (ShouldSuppressAutomaticSignatureHelp(methodParam)) {
                return Task.FromResult<JToken>(JValue.CreateNull());
            }

            return sendRequest(methodParam);
        }

        public Task HandleNotificationAsync(string methodName, JToken methodParam, Func<JToken, Task> sendNotification) =>
            sendNotification(methodParam);

        private bool ShouldSuppressAutomaticSignatureHelp(JToken methodParam) {
            var languagePreferences = Volatile.Read(ref _languagePreferences);
            if (languagePreferences == null || languagePreferences.AutoListParams) {
                return false;
            }

            if (!(methodParam is JObject request) ||
                !(request["context"] is JObject context) ||
                context["triggerKind"]?.Type != JTokenType.Integer ||
                context["isRetrigger"]?.Type != JTokenType.Boolean ||
                context["isRetrigger"].Value<bool>() ||
                (context["activeSignatureHelp"] != null && context["activeSignatureHelp"].Type != JTokenType.Null)) {
                return false;
            }

            if (!int.TryParse(
                context["triggerKind"].ToString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var triggerKind)) {
                return false;
            }

            return triggerKind == (int)SignatureHelpTriggerKind.TriggerCharacter ||
                triggerKind == (int)SignatureHelpTriggerKind.ContentChange;
        }
#endif
    }
#if !DEV18_OR_LATER
#pragma warning restore CS0618
#endif
}

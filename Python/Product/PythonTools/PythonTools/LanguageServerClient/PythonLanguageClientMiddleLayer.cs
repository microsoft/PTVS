using System;
using System.Collections.Generic;
using System.IO;
#if DEV18_OR_LATER
using System.Text.Json;
using System.Text.Json.Nodes;
#else
using System.Globalization;
using Newtonsoft.Json.Linq;
using JsonObject = Newtonsoft.Json.Linq.JObject;
using JsonArray = Newtonsoft.Json.Linq.JArray;
#endif
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Editor;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.LanguageServerClient {
#if !DEV18_OR_LATER
#pragma warning disable CS0618
#endif
    internal sealed class PythonLanguageClientMiddleLayer :
#if DEV18_OR_LATER
        ILanguageClientMiddleLayer2<JsonDocument>,
#else
        ILanguageClientMiddleLayer,
#endif
        IPropertyOwner {
        private const string SignatureHelpMethod = "textDocument/signatureHelp";
        private const string DidOpenMethod = "textDocument/didOpen";
        private const string DidChangeMethod = "textDocument/didChange";
        private const string DidCloseMethod = "textDocument/didClose";
        private const string PublishDiagnosticsMethod = "textDocument/publishDiagnostics";
        private const string WorkspaceApplyEditMethod = "workspace/applyEdit";

        private readonly object _documentsLock = new object();
        private readonly Dictionary<string, DocumentState> _documents = new Dictionary<string, DocumentState>(StringComparer.OrdinalIgnoreCase);
        private LanguagePreferences _languagePreferences;

        public PropertyCollection Properties { get; } = new PropertyCollection();

        internal void Initialize(LanguagePreferences languagePreferences) {
            Volatile.Write(ref _languagePreferences, languagePreferences ?? throw new ArgumentNullException(nameof(languagePreferences)));
        }

        internal void TrackDocumentMessage(JsonObject message) {
#if DEV18_OR_LATER
            var methodName = message?["method"]?.GetValue<string>();
            var textDocument = message?["params"]?["textDocument"];
            var uri = textDocument?["uri"]?.GetValue<string>();
            var version = textDocument?["version"]?.GetValue<int?>();
#else
            var methodName = message?.Value<string>("method");
            var textDocument = message?["params"]?["textDocument"];
            var uri = textDocument?["uri"]?.Value<string>();
            var version = textDocument?["version"]?.Value<int?>();
#endif
            UpdateDocumentState(methodName, uri, version, null);
        }

        public bool CanHandle(string methodName) =>
            string.Equals(methodName, SignatureHelpMethod, StringComparison.Ordinal) ||
            string.Equals(methodName, PublishDiagnosticsMethod, StringComparison.Ordinal) ||
            string.Equals(methodName, WorkspaceApplyEditMethod, StringComparison.Ordinal) ||
            IsDocumentLifecycleMethod(methodName);

#if DEV18_OR_LATER
        public Task<JsonDocument> HandleRequestAsync(string methodName, JsonDocument methodParam, Func<JsonDocument, Task<JsonDocument>> sendRequest) {
            if (string.Equals(methodName, SignatureHelpMethod, StringComparison.Ordinal) &&
                ShouldSuppressAutomaticSignatureHelp(methodParam)) {
                return Task.FromResult<JsonDocument>(null);
            }

            return sendRequest(methodParam);
        }

        public async Task HandleNotificationAsync(string methodName, JsonDocument methodParam, Func<JsonDocument, Task> sendNotification) {
            if (IsDocumentLifecycleMethod(methodName)) {
                TrackDocumentSnapshot(methodName, methodParam);
                await sendNotification(methodParam).ConfigureAwait(false);
                return;
            }

            if (string.Equals(methodName, WorkspaceApplyEditMethod, StringComparison.Ordinal)) {
                using var normalized = NormalizeWorkspaceEdit(methodParam);
                if (normalized != null) {
                    await sendNotification(normalized).ConfigureAwait(false);
                    return;
                }
            }

            if (string.Equals(methodName, PublishDiagnosticsMethod, StringComparison.Ordinal) &&
                IsStaleDiagnostics(methodParam)) {
                return;
            }

            await sendNotification(methodParam).ConfigureAwait(false);
        }

        private void TrackDocumentSnapshot(string methodName, JsonDocument methodParam) {
            if (methodParam == null || methodParam.RootElement.ValueKind != JsonValueKind.Object) {
                return;
            }

            var request = methodParam.RootElement;
            if (!request.TryGetProperty("textDocument", out var textDocument) ||
                textDocument.ValueKind != JsonValueKind.Object ||
                !textDocument.TryGetProperty("uri", out var uriElement) ||
                uriElement.ValueKind != JsonValueKind.String) {
                return;
            }

            int? version = null;
            if (textDocument.TryGetProperty("version", out var versionElement) &&
                versionElement.ValueKind == JsonValueKind.Number &&
                versionElement.TryGetInt32(out var parsedVersion)) {
                version = parsedVersion;
            }

            UpdateDocumentState(methodName, uriElement.GetString(), version, GetCurrentTextBuffer());
        }

        private bool IsStaleDiagnostics(JsonDocument methodParam) {
            if (methodParam == null ||
                methodParam.RootElement.ValueKind != JsonValueKind.Object ||
                !methodParam.RootElement.TryGetProperty("uri", out var uriElement) ||
                uriElement.ValueKind != JsonValueKind.String ||
                !methodParam.RootElement.TryGetProperty("version", out var versionElement) ||
                versionElement.ValueKind != JsonValueKind.Number ||
                !versionElement.TryGetInt32(out var version)) {
                return false;
            }

            return IsStaleDiagnostics(uriElement.GetString(), version);
        }

        private JsonDocument NormalizeWorkspaceEdit(JsonDocument methodParam) {
            if (methodParam == null || methodParam.RootElement.ValueKind != JsonValueKind.Object) {
                return null;
            }

            var request = JsonNode.Parse(methodParam.RootElement.GetRawText()).AsObject();
            return NormalizeWorkspaceEdit(request) ? JsonSerializer.SerializeToDocument(request) : null;
        }

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
            if (string.Equals(methodName, SignatureHelpMethod, StringComparison.Ordinal) &&
                ShouldSuppressAutomaticSignatureHelp(methodParam)) {
                return Task.FromResult<JToken>(JValue.CreateNull());
            }

            return sendRequest(methodParam);
        }

        public Task HandleNotificationAsync(string methodName, JToken methodParam, Func<JToken, Task> sendNotification) {
            if (IsDocumentLifecycleMethod(methodName)) {
                TrackDocumentSnapshot(methodName, methodParam);
                return sendNotification(methodParam);
            }

            if (string.Equals(methodName, WorkspaceApplyEditMethod, StringComparison.Ordinal) &&
                methodParam is JObject request) {
                var normalized = (JObject)request.DeepClone();
                if (NormalizeWorkspaceEdit(normalized)) {
                    return sendNotification(normalized);
                }
            }

            if (string.Equals(methodName, PublishDiagnosticsMethod, StringComparison.Ordinal) &&
                methodParam is JObject diagnostics &&
                diagnostics["uri"]?.Type == JTokenType.String &&
                diagnostics["version"]?.Type == JTokenType.Integer &&
                IsStaleDiagnostics(diagnostics["uri"].Value<string>(), diagnostics["version"].Value<int>())) {
                return Task.CompletedTask;
            }

            return sendNotification(methodParam);
        }

        private void TrackDocumentSnapshot(string methodName, JToken methodParam) {
            if (!(methodParam is JObject request) ||
                !(request["textDocument"] is JObject textDocument) ||
                textDocument["uri"]?.Type != JTokenType.String) {
                return;
            }

            var version = textDocument["version"]?.Type == JTokenType.Integer
                ? textDocument["version"].Value<int?>()
                : null;
            UpdateDocumentState(methodName, textDocument["uri"].Value<string>(), version, GetCurrentTextBuffer());
        }

        private bool ShouldSuppressAutomaticSignatureHelp(JToken methodParam) {
            var languagePreferences = Volatile.Read(ref _languagePreferences);
            if (languagePreferences == null || languagePreferences.AutoListParams ||
                !(methodParam is JObject request) ||
                !(request["context"] is JObject context) ||
                context["triggerKind"]?.Type != JTokenType.Integer ||
                context["isRetrigger"]?.Type != JTokenType.Boolean ||
                context["isRetrigger"].Value<bool>() ||
                (context["activeSignatureHelp"] != null && context["activeSignatureHelp"].Type != JTokenType.Null) ||
                !int.TryParse(context["triggerKind"].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var triggerKind)) {
                return false;
            }

            return triggerKind == (int)SignatureHelpTriggerKind.TriggerCharacter ||
                triggerKind == (int)SignatureHelpTriggerKind.ContentChange;
        }
#endif

        private static bool IsDocumentLifecycleMethod(string methodName) =>
            string.Equals(methodName, DidOpenMethod, StringComparison.Ordinal) ||
            string.Equals(methodName, DidChangeMethod, StringComparison.Ordinal) ||
            string.Equals(methodName, DidCloseMethod, StringComparison.Ordinal);

        private ITextBuffer GetCurrentTextBuffer() {
            return Properties.TryGetProperty(typeof(ITextSnapshot), out ITextSnapshot snapshot)
                ? snapshot?.TextBuffer
                : null;
        }

        private void UpdateDocumentState(string methodName, string uri, int? version, ITextBuffer textBuffer) {
            if (string.IsNullOrEmpty(uri) || !IsDocumentLifecycleMethod(methodName)) {
                return;
            }

            var key = GetDocumentKey(uri);
            lock (_documentsLock) {
                if (string.Equals(methodName, DidCloseMethod, StringComparison.Ordinal)) {
                    _documents.Remove(key);
                    return;
                }

                if (!_documents.TryGetValue(key, out var document) ||
                    (string.Equals(methodName, DidOpenMethod, StringComparison.Ordinal) && textBuffer != null)) {
                    document = new DocumentState(version ?? -1);
                    _documents[key] = document;
                } else if (version.HasValue) {
                    document.Version = string.Equals(methodName, DidOpenMethod, StringComparison.Ordinal)
                        ? version.Value
                        : Math.Max(document.Version, version.Value);
                }

                if (textBuffer != null) {
                    document.TextBuffer = new WeakReference<ITextBuffer>(textBuffer);
                }
            }
        }

        private bool IsStaleDiagnostics(string uri, int version) {
            if (string.IsNullOrEmpty(uri)) {
                return false;
            }

            lock (_documentsLock) {
                return _documents.TryGetValue(GetDocumentKey(uri), out var document) &&
                    version < document.Version;
            }
        }

        private bool NormalizeWorkspaceEdit(JsonObject request) {
            // Versioned documentChanges are resolved by the VS broker against their matching snapshot.
            if (!(request["edit"]?["changes"] is JsonObject changes)) {
                return false;
            }

            var changed = false;
#if DEV18_OR_LATER
            foreach (var documentChanges in changes) {
                if (documentChanges.Value is JsonArray edits &&
                    TryGetDocumentSnapshot(documentChanges.Key, out var snapshot)) {
#else
            foreach (var documentChanges in changes.Properties()) {
                if (documentChanges.Value is JsonArray edits &&
                    TryGetDocumentSnapshot(documentChanges.Name, out var snapshot)) {
#endif
                    changed |= NormalizeTextEdits(edits, snapshot);
                }
            }

            return changed;
        }

        private bool TryGetDocumentSnapshot(string uri, out ITextSnapshot snapshot) {
            snapshot = null;
            lock (_documentsLock) {
                if (!_documents.TryGetValue(GetDocumentKey(uri), out var document) ||
                    document.TextBuffer == null ||
                    !document.TextBuffer.TryGetTarget(out var textBuffer)) {
                    return false;
                }

                snapshot = textBuffer.CurrentSnapshot;
                return true;
            }
        }

        private static bool NormalizeTextEdits(JsonArray edits, ITextSnapshot snapshot) {
            var changed = false;
            foreach (var edit in edits) {
                if (edit is not JsonObject editObject || editObject["range"] is not JsonObject range) {
                    continue;
                }

                changed |= NormalizePosition(range["start"] as JsonObject, snapshot);
                changed |= NormalizePosition(range["end"] as JsonObject, snapshot);
            }

            return changed;
        }

        private static bool NormalizePosition(JsonObject position, ITextSnapshot snapshot) {
#if DEV18_OR_LATER
            if (position?["line"] is not JsonValue line || !line.TryGetValue<int>(out var lineNumber) ||
                position["character"] is not JsonValue characterValue || !characterValue.TryGetValue<int>(out var character)) {
                return false;
            }
#else
            if (position?["line"]?.Type != JTokenType.Integer || position["character"]?.Type != JTokenType.Integer) {
                return false;
            }
            var lineNumber = position["line"].Value<int>();
            var character = position["character"].Value<int>();
#endif

            if (lineNumber < 0 || lineNumber >= snapshot.LineCount) {
                return false;
            }

            var lineLength = snapshot.GetLineFromLineNumber(lineNumber).Length;
            if (character < 0 || character <= lineLength) {
                return false;
            }

            // LSP positions cannot point into a line break.
            position["character"] = lineLength;
            return true;
        }

        private static string GetDocumentKey(string uri) {
            if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri)) {
                return uri;
            }

            if (!parsedUri.IsFile) {
                return parsedUri.AbsoluteUri;
            }

            var path = parsedUri.GetComponents(UriComponents.Path, UriFormat.Unescaped)
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            if (!string.IsNullOrEmpty(parsedUri.Host) &&
                !string.Equals(parsedUri.Host, "localhost", StringComparison.OrdinalIgnoreCase)) {
                path = string.Concat(
                    new string(Path.DirectorySeparatorChar, 2),
                    parsedUri.Host,
                    Path.DirectorySeparatorChar,
                    path
                );
            }

            try {
                return Path.GetFullPath(path);
            } catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException) {
                return path;
            }
        }

        private sealed class DocumentState {
            public DocumentState(int version) {
                Version = version;
            }

            public int Version { get; set; }
            public WeakReference<ITextBuffer> TextBuffer { get; set; }
        }
    }
#if !DEV18_OR_LATER
#pragma warning restore CS0618
#endif
}

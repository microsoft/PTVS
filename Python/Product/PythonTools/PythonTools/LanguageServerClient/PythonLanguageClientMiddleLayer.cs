using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using Newtonsoft.Json.Linq;

namespace Microsoft.PythonTools.LanguageServerClient {
#if !DEV18_OR_LATER
#pragma warning disable CS0618
#endif
    internal sealed class PythonLanguageClientMiddleLayer :
#if DEV18_OR_LATER
        ILanguageClientMiddleLayer2<JsonDocument> {
#else
        ILanguageClientMiddleLayer {
#endif
        private const string DidOpenMethod = "textDocument/didOpen";
        private const string DidChangeMethod = "textDocument/didChange";
        private const string DidCloseMethod = "textDocument/didClose";
        private const string PublishDiagnosticsMethod = "textDocument/publishDiagnostics";
        private const string WorkspaceApplyEditMethod = "workspace/applyEdit";

        private readonly object _documentsLock = new object();
        private readonly Dictionary<string, DocumentState> _documents = new Dictionary<string, DocumentState>(StringComparer.OrdinalIgnoreCase);
        private LanguagePreferences _languagePreferences;

        internal void Initialize(LanguagePreferences languagePreferences) {
            Volatile.Write(ref _languagePreferences, languagePreferences ?? throw new ArgumentNullException(nameof(languagePreferences)));
        }

        internal void TrackDocumentMessage(JObject message) {
            var methodName = message?.Value<string>("method");
            var textDocument = message?["params"]?["textDocument"];
            var uri = textDocument?["uri"]?.Value<string>();
            if (string.IsNullOrEmpty(uri)) {
                return;
            }

            var key = GetDocumentKey(uri);
            lock (_documentsLock) {
                if (string.Equals(methodName, DidCloseMethod, StringComparison.Ordinal)) {
                    _documents.Remove(key);
                } else if (string.Equals(methodName, DidOpenMethod, StringComparison.Ordinal)) {
                    var version = textDocument["version"]?.Value<int?>();
                    var text = textDocument["text"]?.Value<string>();
                    if (version.HasValue && text != null) {
                        _documents[key] = new DocumentState(version.Value, text);
                    }
                } else if (string.Equals(methodName, DidChangeMethod, StringComparison.Ordinal)) {
                    var version = textDocument["version"]?.Value<int?>();
                    if (!version.HasValue) {
                        return;
                    }

                    if (!_documents.TryGetValue(key, out var state)) {
                        state = new DocumentState(version.Value, null);
                        _documents[key] = state;
                    }

                    state.Version = Math.Max(state.Version, version.Value);
                    if (state.Text != null &&
                        message["params"]?["contentChanges"] is JArray changes &&
                        TryApplyContentChanges(state.Text, changes, out var updatedText)) {
                        state.Text = updatedText;
                    } else {
                        state.Text = null;
                    }
                }
            }
        }

        public bool CanHandle(string methodName) =>
            string.Equals(methodName, Methods.TextDocumentSignatureHelpName, StringComparison.Ordinal) ||
            string.Equals(methodName, PublishDiagnosticsMethod, StringComparison.Ordinal) ||
            string.Equals(methodName, WorkspaceApplyEditMethod, StringComparison.Ordinal);

#if DEV18_OR_LATER
        public Task<JsonDocument> HandleRequestAsync(string methodName, JsonDocument methodParam, Func<JsonDocument, Task<JsonDocument>> sendRequest) {
            if (string.Equals(methodName, Methods.TextDocumentSignatureHelpName, StringComparison.Ordinal) &&
                ShouldSuppressAutomaticSignatureHelp(methodParam)) {
                return Task.FromResult<JsonDocument>(null);
            }

            return sendRequest(methodParam);
        }

        public Task HandleNotificationAsync(string methodName, JsonDocument methodParam, Func<JsonDocument, Task> sendNotification) {
            if (string.Equals(methodName, WorkspaceApplyEditMethod, StringComparison.Ordinal)) {
                var normalized = NormalizeWorkspaceEdit(methodParam);
                if (normalized != null) {
                    return sendNotification(normalized);
                }
            }

            if (string.Equals(methodName, PublishDiagnosticsMethod, StringComparison.Ordinal) &&
                IsStaleDiagnostics(methodParam)) {
                return Task.CompletedTask;
            }

            return sendNotification(methodParam);
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

            try {
                var request = JObject.Parse(methodParam.RootElement.GetRawText());
                return NormalizeWorkspaceEdit(request)
                    ? JsonDocument.Parse(request.ToString(Newtonsoft.Json.Formatting.None))
                    : null;
            } catch (Newtonsoft.Json.JsonException) {
                return null;
            }
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
            if (string.Equals(methodName, Methods.TextDocumentSignatureHelpName, StringComparison.Ordinal) &&
                ShouldSuppressAutomaticSignatureHelp(methodParam)) {
                return Task.FromResult<JToken>(JValue.CreateNull());
            }

            return sendRequest(methodParam);
        }

        public Task HandleNotificationAsync(string methodName, JToken methodParam, Func<JToken, Task> sendNotification) {
            if (string.Equals(methodName, WorkspaceApplyEditMethod, StringComparison.Ordinal) &&
                methodParam is JObject request) {
                var normalized = (JObject)request.DeepClone();
                if (NormalizeWorkspaceEdit(normalized)) {
                    return sendNotification(normalized);
                }
            }

            if (string.Equals(methodName, PublishDiagnosticsMethod, StringComparison.Ordinal) &&
                IsStaleDiagnostics(methodParam)) {
                return Task.CompletedTask;
            }

            return sendNotification(methodParam);
        }

        private bool IsStaleDiagnostics(JToken methodParam) {
            if (!(methodParam is JObject diagnostics) ||
                diagnostics["uri"]?.Type != JTokenType.String ||
                diagnostics["version"]?.Type != JTokenType.Integer) {
                return false;
            }

            return IsStaleDiagnostics(
                diagnostics["uri"].Value<string>(),
                diagnostics["version"].Value<int>()
            );
        }

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

        private bool IsStaleDiagnostics(string uri, int version) {
            if (string.IsNullOrEmpty(uri)) {
                return false;
            }

            lock (_documentsLock) {
                return _documents.TryGetValue(GetDocumentKey(uri), out var document) &&
                    version < document.Version;
            }
        }

        private bool NormalizeWorkspaceEdit(JObject request) {
            if (!(request["edit"] is JObject workspaceEdit)) {
                return false;
            }

            var changed = false;
            if (workspaceEdit["changes"] is JObject changes) {
                foreach (var documentChanges in changes.Properties()) {
                    if (documentChanges.Value is JArray edits &&
                        TryGetDocumentText(documentChanges.Name, out var text)) {
                        changed |= NormalizeTextEdits(edits, text);
                    }
                }
            }

            return changed;
        }

        private bool TryGetDocumentText(string uri, out string text) {
            lock (_documentsLock) {
                if (_documents.TryGetValue(GetDocumentKey(uri), out var document) &&
                    document.Text != null) {
                    text = document.Text;
                    return true;
                }
            }

            text = null;
            return false;
        }

        private static bool NormalizeTextEdits(JArray edits, string text) {
            var changed = false;
            foreach (var edit in edits.OfType<JObject>()) {
                if (!(edit["range"] is JObject range)) {
                    continue;
                }

                changed |= NormalizePosition(range["start"] as JObject, text);
                changed |= NormalizePosition(range["end"] as JObject, text);
            }

            return changed;
        }

        private static bool NormalizePosition(JObject position, string text) {
            if (position?["line"]?.Type != JTokenType.Integer ||
                position["character"]?.Type != JTokenType.Integer ||
                !TryGetLineBounds(text, position["line"].Value<int>(), out var lineStart, out var contentEnd, out _)) {
                return false;
            }

            var character = position["character"].Value<int>();
            var lineLength = contentEnd - lineStart;
            if (character < 0 || character <= lineLength) {
                return false;
            }

            // LSP positions cannot point into a line break.
            position["character"] = lineLength;
            return true;
        }

        private static bool TryApplyContentChanges(string text, JArray changes, out string updatedText) {
            var current = text;
            foreach (var change in changes.OfType<JObject>()) {
                var newText = change["text"]?.Value<string>();
                if (newText == null) {
                    updatedText = null;
                    return false;
                }

                if (!(change["range"] is JObject range)) {
                    current = newText;
                    continue;
                }

                if (!TryGetPosition(current, range["start"] as JObject, out var start) ||
                    !TryGetPosition(current, range["end"] as JObject, out var end) ||
                    end < start) {
                    updatedText = null;
                    return false;
                }

                current = string.Concat(
                    current.Substring(0, start),
                    newText,
                    current.Substring(end)
                );
            }

            updatedText = current;
            return true;
        }

        private static bool TryGetPosition(string text, JObject position, out int offset) {
            offset = 0;
            if (position?["line"]?.Type != JTokenType.Integer ||
                position["character"]?.Type != JTokenType.Integer ||
                !TryGetLineBounds(text, position["line"].Value<int>(), out var lineStart, out _, out var nextLineStart)) {
                return false;
            }

            var character = position["character"].Value<int>();
            if (character < 0 || character > nextLineStart - lineStart) {
                return false;
            }

            offset = lineStart + character;
            return true;
        }

        private static bool TryGetLineBounds(string text, int targetLine, out int lineStart, out int contentEnd, out int nextLineStart) {
            lineStart = 0;
            contentEnd = 0;
            nextLineStart = 0;
            if (targetLine < 0) {
                return false;
            }

            var line = 0;
            var start = 0;
            while (true) {
                var end = start;
                while (end < text.Length && text[end] != '\r' && text[end] != '\n') {
                    end++;
                }

                var next = end;
                if (next < text.Length) {
                    next += text[next] == '\r' && next + 1 < text.Length && text[next + 1] == '\n'
                        ? 2
                        : 1;
                }

                if (line == targetLine) {
                    lineStart = start;
                    contentEnd = end;
                    nextLineStart = next;
                    return true;
                }

                if (end == text.Length) {
                    return false;
                }

                line++;
                start = next;
            }
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
            public DocumentState(int version, string text) {
                Version = version;
                Text = text;
            }

            public int Version { get; set; }
            public string Text { get; set; }
        }
    }
#if !DEV18_OR_LATER
#pragma warning restore CS0618
#endif
}

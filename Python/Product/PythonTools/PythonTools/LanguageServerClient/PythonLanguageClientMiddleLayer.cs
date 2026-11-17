using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Editor;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.LanguageServerClient {
    internal sealed class PythonLanguageClientMiddleLayer :
        ILanguageClientMiddleLayer2<JsonDocument>,
        IPropertyOwner {
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
            var methodName = message?["method"]?.GetValue<string>();
            var textDocument = message?["params"]?["textDocument"];
            var uri = textDocument?["uri"]?.GetValue<string>();
            var version = textDocument?["version"]?.GetValue<int?>();
            UpdateDocumentState(methodName, uri, version, null);
        }

        public bool CanHandle(string methodName) =>
            string.Equals(methodName, Requests.TextDocumentSignatureHelpMethod, StringComparison.Ordinal) ||
            string.Equals(methodName, PublishDiagnosticsMethod, StringComparison.Ordinal) ||
            string.Equals(methodName, WorkspaceApplyEditMethod, StringComparison.Ordinal) ||
            IsDocumentLifecycleMethod(methodName);

        public Task<JsonDocument> HandleRequestAsync(string methodName, JsonDocument methodParam, Func<JsonDocument, Task<JsonDocument>> sendRequest) {
            if (string.Equals(methodName, Requests.TextDocumentSignatureHelpMethod, StringComparison.Ordinal) &&
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
            foreach (var documentChanges in changes) {
                if (documentChanges.Value is JsonArray edits &&
                    TryGetDocumentSnapshot(documentChanges.Key, out var snapshot)) {
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
            if (position?["line"] is not JsonValue line || !line.TryGetValue<int>(out var lineNumber) ||
                position["character"] is not JsonValue characterValue || !characterValue.TryGetValue<int>(out var character)) {
                return false;
            }

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
}

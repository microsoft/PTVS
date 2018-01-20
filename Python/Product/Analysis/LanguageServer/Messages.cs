// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    public struct InitializeParams {
        public int? processId;
        public string rootPath;
        public Uri rootUri;
        public PythonInitializationOptions initializationOptions;
        public ClientCapabilities? capabilities;
        public TraceLevel trace;
    }

    [Serializable]
    public class LanguageServerException : Exception {
        public const int UnknownDocument = 1;
        public const int UnsupportedDocumentType = 2;
        public const int MismatchedVersion = 3;

        public int Code => (int)Data["Code"];

        public sealed override System.Collections.IDictionary Data => base.Data;

        public LanguageServerException(int code, string message) : base(message) {
            Data["Code"] = code;
        }

        public LanguageServerException(int code, string message, Exception innerException) : base(message, innerException) {
            Data["Code"] = code;
        }
    }

    public struct InitializeResult {
        public ServerCapabilities? capabilities;
    }

    public struct InitializedParams { }

    public sealed class ShowMessageEventArgs : EventArgs {
        public MessageType type { get; set; }
        public string message { get; set; }
    }

    public struct ShowMessageRequestParams {
        public MessageType type;
        public string message;
        public MessageActionItem[] actions;
    }

    public sealed class LogMessageEventArgs : EventArgs {
        public MessageType type { get; set; }
        public string message { get; set; }
    }

    public sealed class TelemetryEventArgs : EventArgs {
        public object value { get; set; }
    }

    public struct RegistrationParams {
        public Registration[] registrations;
    }

    [ComVisible(false)]
    public sealed class RegisterCapabilityEventArgs : CallbackEventArgs<RegistrationParams> {
        internal RegisterCapabilityEventArgs(TaskCompletionSource<object> task) : base(task) { }
    }

    public struct UnregistrationParams {
        public Unregistration[] unregistrations;
    }

    [ComVisible(false)]
    public sealed class UnregisterCapabilityEventArgs : CallbackEventArgs<UnregistrationParams> {
        internal UnregisterCapabilityEventArgs(TaskCompletionSource<object> task) : base(task) { }
    }

    public struct DidChangeConfigurationParams {
        public object settings;
    }

    public struct DidChangeWatchedFilesParams {
        public FileEvent[] changes;
    }

    public struct WorkplaceSymbolParams {
        public string query;
    }

    public struct ExecuteCommandParams {
        public string command;
        public object[] arguments;
    }

    public struct ApplyWorkspaceEditParams {
        /// <summary>
        /// An optional label of the workspace edit.This label is
        /// presented in the user interface for example on an undo
        /// stack to undo the workspace edit.
        /// </summary>
        public string label;
        public WorkspaceEdit edit;
    }

    public struct ApplyWorkspaceEditResponse {
        public bool applied;
    }

    [ComVisible(false)]
    public sealed class ApplyWorkspaceEditEventArgs : CallbackEventArgs<ApplyWorkspaceEditParams, ApplyWorkspaceEditResponse> {
        internal ApplyWorkspaceEditEventArgs(TaskCompletionSource<ApplyWorkspaceEditResponse?> task) : base(task) { }
    }

    public struct DidOpenTextDocumentParams {
        public TextDocumentItem textDocument;
    }

    public struct DidChangeTextDocumentParams {
        public VersionedTextDocumentIdentifier textDocument;
        public TextDocumentContentChangedEvent[] contentChanges;
    }

    public struct WillSaveTextDocumentParams {
        public TextDocumentIdentifier textDocument;
        public TextDocumentSaveReason reason;
    }

    public struct DidSaveTextDocumentParams {
        public TextDocumentIdentifier textDocument;
        public string content;
    }

    public struct DidCloseTextDocumentParams {
        public TextDocumentIdentifier textDocument;
    }

    public sealed class PublishDiagnosticsEventArgs : EventArgs {
        public Uri uri { get; set; }
        public IReadOnlyList<Diagnostic> diagnostics { get; set; }

        /// <summary>
        /// The version the ranges in the diagnostics apply to.
        /// </summary>
        public int? _version { get; set; }
    }

    public struct TextDocumentPositionParams {
        public TextDocumentIdentifier textDocument;
        public Position position;

        /// <summary>
        /// The intended version that position applies to. The request may fail if
        /// the server cannot map correctly.
        /// </summary>
        public int? _version;
        /// <summary>
        /// Override the expression to evaluate. If omitted, uses the context at the
        /// specified position.
        /// </summary>
        public string _expr;
    }

    public struct CompletionParams {
        public TextDocumentIdentifier textDocument;
        public Position position;
        public CompletionContext? context;

        /// <summary>
        /// The intended version that position applies to. The request may fail if
        /// the server cannot map correctly.
        /// </summary>
        public int? _version;
        /// <summary>
        /// Override the expression to evaluate. If omitted, uses the context at the
        /// specified position.
        /// </summary>
        public string _expr;
    }

    public struct CompletionContext {
        public CompletionTriggerKind triggerKind;
        public string triggerCharacter;

        public bool _intersection;
        public bool? _statementKeywords;
        public bool? _expressionKeywords;
        public bool? _includeAllModules;
        public bool? _includeArgumentNames;
        public CompletionItemKind? _filterKind;
    }

    public struct ReferencesParams {
        public TextDocumentIdentifier textDocument;
        public Position position;
        public ReferenceContext? context;

        /// <summary>
        /// The intended version that range applies to. The request may fail if
        /// the server cannot map correctly.
        /// </summary>
        public int? _version;
    }

    public struct ReferenceContext {
        public bool includeDeclaration;
    }

    public struct DocumentSymbolParams {
        public TextDocumentIdentifier textDocument;
    }

    public struct CodeActionParams {
        public TextDocumentIdentifier textDocument;
        public Range range;
        public CodeActionContext? context;

        /// <summary>
        /// The intended version that range applies to. The request may fail if
        /// the server cannot map correctly.
        /// </summary>
        public int? _version;
    }

    public struct CodeActionContext {
        public Diagnostic[] diagnostics;

        /// <summary>
        /// The intended version that diagnostic locations apply to. The request may
        /// fail if the server cannot map correctly.
        /// </summary>
        public int? _version;
    }

    public struct DocumentLinkParams {
        public TextDocumentIdentifier textDocument;
    }

    public struct DocumentFormattingParams {
        public TextDocumentIdentifier textDocument;
        public FormattingOptions options;
    }

    public struct DocumentRangeFormattingParams {
        public TextDocumentIdentifier textDocument;
        public Range range;
        public FormattingOptions options;

        /// <summary>
        /// The intended version that range applies to. The request may fail if
        /// the server cannot map correctly.
        /// </summary>
        public int? _version;
    }

    public struct DocumentOnTypeFormattingParams {
        public TextDocumentIdentifier textDocument;
        public Position position;
        public string ch;
        public FormattingOptions options;

        /// <summary>
        /// The intended version that range applies to. The request may fail if
        /// the server cannot map correctly.
        /// </summary>
        public int? _version;
    }

    public struct RenameParams {
        public TextDocumentIdentifier textDocument;
        public Position position;
        public string newName;

        /// <summary>
        /// The intended version that position applies to. The request may fail if
        /// the server cannot map correctly.
        /// </summary>
        public int? _version;
    }

    public sealed class FileFoundEventArgs : EventArgs {
        public Uri uri { get; set; }
    }

    public sealed class ParseCompleteEventArgs : EventArgs {
        public Uri uri { get; set; }
        public int version { get; set; }
    }

    public sealed class AnalysisCompleteEventArgs : EventArgs {
        public Uri uri { get; set; }
        public int version { get; set; }
    }

}

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

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    public struct ResponseError {
        public int code;
        public string message;
    }

    public struct ResponseError<T> {
        public int code;
        public string message;
        public T data;
    }

    public struct Position {
        /// <summary>
        /// Line position in a document (zero-based).
        /// </summary>
        public int line;

        /// <summary>
        /// Character offset on a line in a document (zero-based). Assuming that the line is
        /// represented as a string, the `character` value represents the gap between the
        /// `character` and `character + 1`.
        /// 
        /// If the character value is greater than the line length it defaults back to the
        /// line length.
        /// </summary>
        public int character;

        public static implicit operator SourceLocation(Position p) => new SourceLocation(p.line + 1, p.character + 1);
        public static implicit operator Position(SourceLocation loc) => new Position { line = loc.Line - 1, character = loc.Column - 1 };

        public override string ToString() => ((SourceLocation)this).ToString();
    }

    public struct Range {
        public Position start, end;

        public static implicit operator SourceSpan(Range r) => new SourceSpan(r.start, r.end);
        public static implicit operator Range(SourceSpan span) => new Range { start = span.Start, end = span.End };

        public override string ToString() => ((SourceSpan)this).ToString();
    }

    public struct Location {
        public Uri uri;
        public Range range;
    }

    public struct Diagnostic {
        /// <summary>
        /// The range at which the message applies.
        /// </summary>
        public Range range;

        /// <summary>
        /// The diagnostic's severity. Can be omitted. If omitted it is up to the
        /// client to interpret diagnostics as error, warning, info or hint.
        /// </summary>
        public DiagnosticSeverity severity;

        /// <summary>
        /// The diagnostic's code (number or string). Can be omitted.
        /// </summary>
        public object code;

        /// <summary>
        /// A human-readable string describing the source of this
        /// diagnostic, e.g. 'typescript' or 'super lint'.
        /// </summary>
        public string source;

        /// <summary>
        /// The diagnostic's message.
        /// </summary>
        public string message;
    }

    public enum DiagnosticSeverity : int {
        Unspecified = 0,
        Error = 1,
        Warning = 2,
        Information = 3,
        Hint = 4
    }

    public struct Command {
        /// <summary>
        /// Title of the command, like `save`.
        /// </summary>
        public string title;

        /// <summary>
        /// The identifier of the actual command handler.
        /// </summary>
        public string command;

        /// <summary>
        /// Arguments that the command handler should be invoked with.
        /// </summary>
        public object[] arguments;
    }

    public struct TextEdit {
        /// <summary>
        /// The range of the text document to be manipulated. To insert
        /// text into a document create a range where start === end.
        /// </summary>
        public Range range;

        /// <summary>
        /// The string to be inserted. For delete operations use an
        /// empty string.
        /// </summary>
        public string newText;

        /// <summary>
        /// Extended version information specifying the source version
        /// that range applies to. Should be used by the client to
        /// adjust range before applying the edit.
        /// </summary>
        public int? _version;
    }

    public struct TextDocumentEdit {
        public VersionedTextDocumentIdentifier textDocument;
        public TextEdit[] edits;
    }

    public struct WorkspaceEdit {
        public Dictionary<Uri, TextEdit[]> changes;
        public TextDocumentEdit[] documentChanges;
    }

    public struct TextDocumentIdentifier {
        public Uri uri;

        public static implicit operator TextDocumentIdentifier(Uri uri) => new TextDocumentIdentifier { uri = uri };
    }

    public struct TextDocumentItem {
        public Uri uri;
        public string languageId;
        public int version;
        public string text;
    }

    public struct VersionedTextDocumentIdentifier {
        public Uri uri;
        public int? version;
    }

    public struct DocumentFilter {
        /// <summary>
        /// A language id, like `typescript`.
        /// </summary>
        public string language;

        /// <summary>
        /// A Uri [scheme](#Uri.scheme), like `file` or `untitled`.
        /// </summary>
        public string scheme;

        /// <summary>
        /// A glob pattern, like `*.{ts,js}`.
        /// </summary>
        public string pattern;
    }

    public struct MarkupContent {
        public MarkupKind kind;
        public string value;
    }


    /// <summary>
    /// Required layout for the initializationOptions member of initializeParams
    /// </summary>
    public struct PythonInitializationOptions {
        public struct Interpreter {
            /// <summary>
            /// The serialized info required to restore an interpreter factory
            /// </summary>
            public string assembly;
            public string typeName;
            public Dictionary<string, object> properties;

            /// <summary>
            /// The x.y language version of the interpreter in case the factory
            /// cannot be restored.
            /// </summary>
            public string version;
        }
        public Interpreter interpreter;

        public string[] searchPaths;
    }


    public struct WorkspaceClientCapabilities {
        public bool applyEdit;
        public struct WorkspaceEditCapabilities { public bool documentChanges; }
        public WorkspaceEditCapabilities? documentChanges;

        public struct DidConfigurationChangeCapabilities { public bool dynamicRegistration; }
        public DidConfigurationChangeCapabilities? didConfigurationChange;
        public struct DidChangeWatchedFilesCapabilities { public bool dynamicRegistration; }
        public DidChangeWatchedFilesCapabilities? didChangeWatchedFiles;

        public struct SymbolCapabilities {
            public bool dynamicRegistration;

            public struct SymbolKindCapabilities {
                /// <summary>
                /// The symbol kind values the client supports. When this
                /// property exists the client also guarantees that it will
                /// handle values outside its set gracefully and falls back
                /// to a default value when unknown.
                /// 
                /// If this property is not present the client only supports
                /// the symbol kinds from `File` to `Array` as defined in
                /// the initial version of the protocol.
                /// </summary>
                public SymbolKind[] valueSet;
            }
            public SymbolKindCapabilities? symbolKind;
        }
        public SymbolCapabilities? symbol;

        public struct ExecuteCommandCapabilities { public bool dynamicRegistration; }
        public ExecuteCommandCapabilities? executeCommand;
    }

    public struct TextDocumentClientCapabilities {
        public struct SynchronizationCapabilities {
            public bool dynamicRegistration;
            public bool willSave;
            /// <summary>
            /// The client supports sending a will save request and
            /// waits for a response providing text edits which will
            /// be applied to the document before it is saved.
            /// </summary>
            public bool willSaveWaitUntil;
            public bool didSave;
        }
        public SynchronizationCapabilities? synchronization;

        public struct CompletionCapabilities {
            public bool dynamicRegistration;

            public struct CompletionItemCapabilities {
                /// <summary>
                /// Client supports snippets as insert text.
                /// 
                /// A snippet can define tab stops and placeholders with `$1`, `$2`
                /// and `${3:foo}`. `$0` defines the final tab stop, it defaults to
                /// the end of the snippet. Placeholders with equal identifiers are linked,
                /// that is typing in one will update others too.
                /// </summary>
                public bool snippetSupport;

                public bool commitCharactersSupport;

                public MarkupKind[] documentationFormat;
            }
            public CompletionItemCapabilities? completionItem;

            public struct CompletionItemKindCapabilities {
                /// <summary>
                /// The completion item kind values the client supports. When this
                /// property exists the client also guarantees that it will
                /// handle values outside its set gracefully and falls back
                /// to a default value when unknown.
                /// 
                /// If this property is not present the client only supports
                /// the completion items kinds from `Text` to `Reference` as defined in
                /// the initial version of the protocol.
                /// </summary>
                public SymbolKind[] valueSet;
            }
            public CompletionItemKindCapabilities? completionItemKind;

            /// <summary>
            /// The client supports to send additional context information for a
            /// `textDocument/completion` request.
            /// </summary>
            public bool contextSupport;
        }
        public CompletionCapabilities? completion;

        public struct HoverCapabilities {
            public bool dynamicRegistration;
            /// <summary>
            /// Client supports the follow content formats for the content
            /// property.The order describes the preferred format of the client.
            /// </summary>
            public MarkupKind[] contentFormat;
        }
        public HoverCapabilities? hover;

        public struct SignatureHelpCapabilities {
            public bool dynamicRegistration;

            public struct SignatureInformationCapabilities {
                /// <summary>
                ///  Client supports the follow content formats for the documentation
                /// property.The order describes the preferred format of the client.
                /// </summary>
                public MarkupKind[] documentationFormat;
            }
            public SignatureInformationCapabilities? signatureInformation;
        }
        public SignatureHelpCapabilities? signatureHelp;

        public struct ReferencesCapabilities { public bool dynamicRegistration; }
        public ReferencesCapabilities? references;

        public struct DocumentHighlightCapabilities { public bool dynamicRegistration; }
        public DocumentHighlightCapabilities? documentHighlight;

        public struct DocumentSymbolCapabilities {
            public bool dynamicRegistration;
            public struct SymbolKindCapabilities {
                /// <summary>
                /// The symbol kind values the client supports. When this
                /// property exists the client also guarantees that it will
                /// handle values outside its set gracefully and falls back
                /// to a default value when unknown.
                /// 
                /// If this property is not present the client only supports
                /// the symbol kinds from `File` to `Array` as defined in
                /// the initial version of the protocol.
                /// </summary>
                public SymbolKind[] valueSet;
            }
            public SymbolKindCapabilities? symbolKind;
        }
        public DocumentSymbolCapabilities? documentSymbol;

        public struct FormattingCapabilities { public bool dynamicRegistration; }
        public FormattingCapabilities? formatting;

        public struct RangeFormattingCapabilities { public bool dynamicRegistration; }
        public RangeFormattingCapabilities? rangeFormatting;

        public struct OnTypeFormattingCapabilities { public bool dynamicRegistration; }
        public OnTypeFormattingCapabilities? onTypeFormatting;

        public struct DefinitionCapabilities { public bool dynamicRegistration; }
        public DefinitionCapabilities? definition;

        public struct CodeActionCapabilities { public bool dynamicRegistration; }
        public CodeActionCapabilities? codeAction;

        public struct CodeLensCapabilities { public bool dynamicRegistration; }
        public CodeLensCapabilities? codeLens;

        public struct DocumentLinkCapabilities { public bool dynamicRegistration; }
        public DocumentLinkCapabilities? documentLink;

        public struct RenameCapabilities { public bool dynamicRegistration; }
        public RenameCapabilities? rename;
    }

    /// <summary>
    /// This struct is for Python-specific extensions. It is included with
    /// client capabilities following the specification for extra settings.
    /// </summary>
    public struct PythonClientCapabilities {
        /// <summary>
        /// Client expects analysis progress updates, including notifications
        /// when analysis is complete for a particular document version.
        /// </summary>
        public bool? analysisUpdates;

        /// <summary>
        /// Number of milliseconds of synchronous wait to allow during request
        /// for completions.
        /// </summary>
        public int? completionsTimeout;

        /// <summary>
        /// Enables an even higher level of logging via the logMessage event.
        /// This will likely have a performance impact.
        /// </summary>
        public bool? traceLogging;

        /// <summary>
        /// Disables automatic analysis of all files under the root URI.
        /// </summary>
        public bool? manualFileLoad;
    }

    public struct ClientCapabilities {
        public WorkspaceClientCapabilities? workspace;
        public TextDocumentClientCapabilities? textDocument;
        public object experimental;
        public PythonClientCapabilities? python;
    }


    public struct CompletionOptions {
        /// <summary>
        /// The server provides support to resolve additional
        /// information for a completion item.
        /// </summary>
        public bool resolveProvider;
        /// <summary>
        /// The characters that trigger completion automatically.
        /// </summary>
        public string[] triggerCharacters;
    }

    public struct SignatureHelpOptions {
        /// <summary>
        /// The characters that trigger signature help
        /// automatically.
        /// </summary>
        public string[] triggerCharacters;
    }

    public struct CodeLensOptions {
        public bool resolveProvider;
    }

    public struct DocumentOnTypeFormattingOptions {
        public string firstTriggerCharacter;
        public string[] moreTriggerCharacter;
    }

    public struct DocumentLinkOptions {
        public bool resolveProvider;
    }

    public struct ExecuteCommandOptions {
        public string[] commands;
    }

    public struct SaveOptions {
        public bool includeText;
    }

    public struct TextDocumentSyncOptions {
        /// <summary>
        /// Open and close notifications are sent to the server.
        /// </summary>
        public bool openClose;
        public TextDocumentSyncKind change;
        public bool willSave;
        public bool willSaveWaitUntil;
        public SaveOptions? save;
    }

    public struct ServerCapabilities {
        public TextDocumentSyncOptions? textDocumentSync;
        public bool hoverProvider;
        public CompletionOptions? completionProvider;
        public SignatureHelpOptions? signatureHelpProvider;
        public bool definitionProvider;
        public bool referencesProvider;
        public bool documentHighlightProvider;
        public bool documentSymbolProvider;
        public bool workspaceSymbolProvider;
        public bool codeActionProvider;
        public CodeLensOptions? codeLensProvider;
        public bool documentFormattingProvider;
        public bool documentRangeFormattingProvider;
        public DocumentOnTypeFormattingOptions? documentOnTypeFormattingProvider;
        public bool renameProvider;
        public DocumentLinkOptions? documentLinkProvider;
        public ExecuteCommandOptions? executeCommandProvider;
        public object experimental;
    }

    public struct MessageActionItem {
        public string title;
    }

    public struct Registration {
        public string id;
        public string method;
        public IRegistrationOptions registerOptions;
    }

    public interface IRegistrationOptions { }

    public struct TextDocumentRegistrationOptions : IRegistrationOptions {
        public DocumentFilter? documentSelector;
    }

    public struct Unregistration {
        public string id;
        public string method;
    }

    public struct FileEvent {
        public Uri uri;
        public FileChangeType type;
    }

    public struct DidChangeWatchedFilesRegistrationOptions : IRegistrationOptions {
        public FileSystemWatcher[] watchers;
    }

    public struct FileSystemWatcher {
        public string globPattern;
        public WatchKind? type;
    }

    public struct ExecuteCommandRegistrationOptions : IRegistrationOptions {
        public string[] commands;
    }

    public struct TextDocumentContentChangedEvent {
        public Range? range;
        public int? rangeLength;
        public string text;
    }

    public struct TextDocumentChangeRegistrationOptions : IRegistrationOptions {
        public DocumentFilter? documentSelector;
        public TextDocumentSyncKind syncKind;
    }

    public struct TextDocumentSaveRegistrationOptions : IRegistrationOptions {
        public DocumentFilter? documentSelector;
        public bool includeText;
    }

    public struct CompletionList {
        /// <summary>
        /// This list is not complete. Further typing should result in recomputing
        /// this list.
        /// </summary>
        public bool isIncomplete;
        public CompletionItem[] items;
    }

    public struct CompletionItem {
        public string label;
        public CompletionItemKind kind;
        public string detail;
        public string documentation;
        public string sortText;
        public string filterText;
        public string insertText;
        public InsertTextFormat insertTextFormat;
        public TextEdit? textEdit;
        public TextEdit[] additionalTextEdits;
        public string[] commitCharacters;
        public Command? command;
        public object data;

        public string _kind;
        public CompletionItemValue[] _values;
    }

    // Not in LSP spec
    public struct CompletionItemValue {
        public string description;
        public string documentation;
        public Reference[] references;
    }

    public struct CompletionRegistrationOptions : IRegistrationOptions {
        public DocumentFilter? documentSelector;
        public string[] triggerCharacters;
        public bool resolveProvider;
    }

    public struct Hover {
        public MarkupContent contents;
        public Range? range;

        /// <summary>
        /// The document version that range applies to.
        /// </summary>
        public int? _version;
    }

    public struct SignatureHelp {
        public SignatureInformation[] signatures;
        public int activeSignature;
        public int activeParameter;
    }

    public struct SignatureInformation {
        public string label;
        public MarkupContent? documentation;
        public ParameterInformation[] parameters;
    }

    public struct ParameterInformation {
        public string label;
        public MarkupContent? documentation;

        public string _type;
        public string _defaultValue;
        public bool? _isOptional;
    }

    /// <summary>
    /// Used instead of Position when returning references so we can include
    /// the kind.
    /// </summary>
    public struct Reference {
        public Uri uri;
        public Range range;

        /// <summary>
        /// The kind of reference
        /// </summary>
        public ReferenceKind? _kind;
        /// <summary>
        /// The document version that range applies to
        /// </summary>
        public int? _version;
    }

    public struct DocumentHighlight {
        public Range range;
        public DocumentHighlightKind kind;

        /// <summary>
        /// The document version that range applies to
        /// </summary>
        public int? _version;
    }

    public struct SymbolInformation {
        public string name;
        public SymbolKind kind;
        public Location location;
        /// <summary>
        /// The name of the symbol containing this symbol. This information is for
        /// user interface purposes (e.g.to render a qualifier in the user interface
        /// if necessary). It can't be used to re-infer a hierarchy for the document
        /// symbols.
        /// </summary>
        public string containerName;

        /// <summary>
        /// The document version that location applies to
        /// </summary>
        public int? _version;
        public string _kind;
    }

    public struct CodeLens {
        public Range range;
        public Command? command;
        public object data;

        /// <summary>
        /// The document version that range applies to
        /// </summary>
        public int? _version;
    }

    public struct DocumentLink {
        public Range range;
        public Uri target;

        /// <summary>
        /// The document version tha range applies to
        /// </summary>
        public int? _version;
    }

    public struct DocumentLinkRegistrationOptions : IRegistrationOptions {
        public DocumentFilter? documentSelector;
        public bool resolveProvider;
    }

    public struct FormattingOptions {
        public int tabSize;
        public bool insertSpaces;

    }

    public struct DocumentOnTypeFormattingRegistrationOptions : IRegistrationOptions {
        public DocumentFilter? documentSelector;
        public string firstTriggerCharacter;
        public string[] moreTriggerCharacters;
    }

}


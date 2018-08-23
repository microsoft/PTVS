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
    [Serializable]
    public struct ResponseError {
        public int code;
        public string message;
    }

    public struct ResponseError<T> {
        public int code;
        public string message;
        public T data;
    }

    [Serializable]
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

        public static bool operator >(Position p1, Position p2) => p1.line > p2.line || p1.line == p2.line && p1.character > p2.character;
        public static bool operator <(Position p1, Position p2) => p1.line < p2.line || p1.line == p2.line && p1.character < p2.character;

        public override string ToString() => ((SourceLocation)this).ToString();
    }

    [Serializable]
    public struct Range {
        public Position start, end;

        public static implicit operator SourceSpan(Range r) => new SourceSpan(r.start, r.end);
        public static implicit operator Range(SourceSpan span) => new Range { start = span.Start, end = span.End };

        public override string ToString() => ((SourceSpan)this).ToString();
    }

    [Serializable]
    public struct Location {
        public Uri uri;
        public Range range;
    }

    [Serializable]
    public class Diagnostic {
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
        /// The diagnostic's code (string, such as 'unresolved-import'). Can be omitted.
        /// <seealso cref="Analyzer.ErrorMessages"/>
        /// </summary>
        public string code;

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

    [Serializable]
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

    [Serializable]
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

    [Serializable]
    public struct TextDocumentEdit {
        public VersionedTextDocumentIdentifier textDocument;
        public TextEdit[] edits;
    }

    [Serializable]
    public struct WorkspaceEdit {
        public Dictionary<Uri, TextEdit[]> changes;
        public TextDocumentEdit[] documentChanges;
    }

    [Serializable]
    public struct TextDocumentIdentifier {
        public Uri uri;

        public static implicit operator TextDocumentIdentifier(Uri uri) => new TextDocumentIdentifier { uri = uri };
    }

    [Serializable]
    public struct TextDocumentItem {
        public Uri uri;
        public string languageId;
        public int version;
        public string text;
    }

    [Serializable]
    public struct VersionedTextDocumentIdentifier {
        public Uri uri;
        public int? version;
        public int? _fromVersion;
    }

    [Serializable]
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

    [Serializable]
    public class MarkupContent {
        public string kind;
        public string value;

        public static implicit operator MarkupContent(string text) => new MarkupContent { kind = MarkupKind.PlainText, value = text };
    }

    public class InformationDisplayOptions {
        public string preferredFormat;
        public bool trimDocumentationLines;
        public int maxDocumentationLineLength;
        public bool trimDocumentationText;
        public int maxDocumentationTextLength;
        public int maxDocumentationLines;
    }

    /// <summary>
    /// Required layout for the initializationOptions member of initializeParams
    /// </summary>
    [Serializable]
    public class PythonInitializationOptions {
        [Serializable]
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

        /// <summary>
        /// Paths to search when attempting to resolve module imports.
        /// </summary>
        public string[] searchPaths = Array.Empty<string>();

        /// <summary>
        /// Secondary paths to search when resolving modules. Not supported by all
        /// factories. In generaly, only source files will be discovered, and their
        /// contents will be merged with the initial module.
        /// </summary>
        public string[] typeStubSearchPaths = Array.Empty<string>();

        /// <summary>
        /// Indicates that analysis engine is running in a test environment.
        /// Causes initialization and analysis sequences to fully
        /// complete before information requests such as hover or
        /// completion can be processed.
        /// </summary>
        public bool testEnvironment;

        /// <summary>
        /// Controls tooltip display appearance. Different between VS and VS Code.
        /// </summary>
        public InformationDisplayOptions displayOptions = new InformationDisplayOptions();

        /// <summary>
        /// Glob pattern of files and folders to exclude from loading
        /// into the Python analysis engine.
        /// </summary>
        public string[] excludeFiles = Array.Empty<string>();

        /// <summary>
        /// Glob pattern of files and folders under the root folder that
        /// should be loaded into the Python analysis engine.
        /// </summary>
        public string[] includeFiles = Array.Empty<string>();

        /// <summary>
        /// Client expects analysis progress updates, including notifications
        /// when analysis is complete for a particular document version.
        /// </summary>
        public bool analysisUpdates;

        /// <summary>
        /// Enables an even higher level of logging via the logMessage event.
        /// This will likely have a performance impact.
        /// </summary>
        public bool traceLogging;

        /// <summary> 
        /// If true, analyzer will be created asynchronously. Used in VS Code. 
        /// </summary> 
        public bool asyncStartup;
    }

    [Serializable]
    public class WorkspaceClientCapabilities {
        public bool applyEdit;

        public struct WorkspaceEditCapabilities { public bool documentChanges; }
        public WorkspaceEditCapabilities? documentChanges;

        public struct DidConfigurationChangeCapabilities { public bool dynamicRegistration; }
        public DidConfigurationChangeCapabilities? didConfigurationChange;

        public struct DidChangeWatchedFilesCapabilities { public bool dynamicRegistration; }
        public DidChangeWatchedFilesCapabilities? didChangeWatchedFiles;

        [Serializable]
        public struct SymbolCapabilities {
            public bool dynamicRegistration;

            [Serializable]
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

    [Serializable]
    public class TextDocumentClientCapabilities {
        [Serializable]
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

        [Serializable]
        public struct CompletionCapabilities {
            public bool dynamicRegistration;

            [Serializable]
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

                public string[] documentationFormat;
            }
            public CompletionItemCapabilities? completionItem;

            [Serializable]
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

        [Serializable]
        public struct HoverCapabilities {
            public bool dynamicRegistration;
            /// <summary>
            /// Client supports the follow content formats for the content
            /// property.The order describes the preferred format of the client.
            /// </summary>
            public string[] contentFormat;
        }
        public HoverCapabilities? hover;

        [Serializable]
        public struct SignatureHelpCapabilities {
            public bool dynamicRegistration;

            public struct SignatureInformationCapabilities {
                /// <summary>
                ///  Client supports the follow content formats for the documentation
                /// property.The order describes the preferred format of the client.
                /// </summary>
                public string[] documentationFormat;

                /// <summary>
                /// When true, the label in the returned signature information will
                /// only contain the function name. Otherwise, the label will contain
                /// the full signature.
                /// </summary>
                public bool? _shortLabel;
            }
            public SignatureInformationCapabilities? signatureInformation;
        }
        public SignatureHelpCapabilities? signatureHelp;

        [Serializable]
        public struct ReferencesCapabilities { public bool dynamicRegistration; }
        public ReferencesCapabilities? references;

        [Serializable]
        public struct DocumentHighlightCapabilities { public bool dynamicRegistration; }
        public DocumentHighlightCapabilities? documentHighlight;

        [Serializable]
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

            /// <summary>
            /// The client support hierarchical document symbols.
            /// </summary>
            public bool? hierarchicalDocumentSymbolSupport;
        }
        public DocumentSymbolCapabilities? documentSymbol;

        [Serializable]
        public struct FormattingCapabilities { public bool dynamicRegistration; }
        public FormattingCapabilities? formatting;

        [Serializable]
        public struct RangeFormattingCapabilities { public bool dynamicRegistration; }
        public RangeFormattingCapabilities? rangeFormatting;

        [Serializable]
        public struct OnTypeFormattingCapabilities { public bool dynamicRegistration; }
        public OnTypeFormattingCapabilities? onTypeFormatting;

        public struct DefinitionCapabilities { public bool dynamicRegistration; }
        public DefinitionCapabilities? definition;

        [Serializable]
        public struct CodeActionCapabilities { public bool dynamicRegistration; }
        public CodeActionCapabilities? codeAction;

        [Serializable]
        public struct CodeLensCapabilities { public bool dynamicRegistration; }
        public CodeLensCapabilities? codeLens;

        [Serializable]
        public struct DocumentLinkCapabilities { public bool dynamicRegistration; }
        public DocumentLinkCapabilities? documentLink;

        [Serializable]
        public struct RenameCapabilities { public bool dynamicRegistration; }
        public RenameCapabilities? rename;
    }

    /// <summary>
    /// This struct is for Python-specific extensions. It is included with
    /// client capabilities following the specification for extra settings.
    /// </summary>
    [Serializable]
    public class PythonClientCapabilities {
        /// <summary>
        /// Disables automatic analysis of all files under the root URI.
        /// </summary>
        public bool? manualFileLoad;

        /// <summary>
        /// Enables rich diagnostics from code analysis.
        /// </summary>
        public bool? liveLinting;
    }

    [Serializable]
    public class ClientCapabilities {
        public WorkspaceClientCapabilities workspace;
        public TextDocumentClientCapabilities textDocument;
        public object experimental;
        public PythonClientCapabilities python;
    }

    [Serializable]
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

    [Serializable]
    public struct SignatureHelpOptions {
        /// <summary>
        /// The characters that trigger signature help
        /// automatically.
        /// </summary>
        public string[] triggerCharacters;
    }

    [Serializable]
    public struct CodeLensOptions {
        public bool resolveProvider;
    }

    [Serializable]
    public struct DocumentOnTypeFormattingOptions {
        public string firstTriggerCharacter;
        public string[] moreTriggerCharacter;
    }

    [Serializable]
    public struct DocumentLinkOptions {
        public bool resolveProvider;
    }

    [Serializable]
    public struct ExecuteCommandOptions {
        public string[] commands;
    }

    [Serializable]
    public class SaveOptions {
        public bool includeText;
    }

    [Serializable]
    public class TextDocumentSyncOptions {
        /// <summary>
        /// Open and close notifications are sent to the server.
        /// </summary>
        public bool openClose;
        public TextDocumentSyncKind change;
        public bool willSave;
        public bool willSaveWaitUntil;
        public SaveOptions save;
    }

    [Serializable]
    public struct ServerCapabilities {
        public TextDocumentSyncOptions textDocumentSync;
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

    [Serializable]
    public struct MessageActionItem {
        public string title;
    }

    [Serializable]
    public struct Registration {
        public string id;
        public string method;
        public IRegistrationOptions registerOptions;
    }

    public interface IRegistrationOptions { }

    [Serializable]
    public struct TextDocumentRegistrationOptions : IRegistrationOptions {
        public DocumentFilter? documentSelector;
    }

    [Serializable]
    public struct Unregistration {
        public string id;
        public string method;
    }

    [Serializable]
    public struct FileEvent {
        public Uri uri;
        public FileChangeType type;
    }

    [Serializable]
    public struct DidChangeWatchedFilesRegistrationOptions : IRegistrationOptions {
        public FileSystemWatcher[] watchers;
    }

    [Serializable]
    public struct FileSystemWatcher {
        public string globPattern;
        public WatchKind? type;
    }

    [Serializable]
    public struct ExecuteCommandRegistrationOptions : IRegistrationOptions {
        public string[] commands;
    }

    [Serializable]
    public struct TextDocumentContentChangedEvent {
        public Range? range;
        public int? rangeLength;
        public string text;
    }

    public struct TextDocumentChangeRegistrationOptions : IRegistrationOptions {
        public DocumentFilter? documentSelector;
        public TextDocumentSyncKind syncKind;
    }

    [Serializable]
    public struct TextDocumentSaveRegistrationOptions : IRegistrationOptions {
        public DocumentFilter? documentSelector;
        public bool includeText;
    }

    [Serializable]
    public class CompletionList {
        /// <summary>
        /// This list is not complete. Further typing should result in recomputing
        /// this list.
        /// </summary>
        public bool isIncomplete;
        public CompletionItem[] items;

        /// <summary>
        /// The range that should be replaced when committing a completion from this
        /// list. Where <c>textEdit</c> is set on a completion, prefer that.
        /// </summary>
        public Range? _applicableSpan;
        /// <summary>
        /// When true, snippets are allowed in this context.
        /// </summary>
        public bool? _allowSnippet;
        /// <summary>
        /// The expression that members are being displayed for.
        /// </summary>
        public string _expr;
        /// <summary>
        /// When true, completions should commit by default. When false, completions
        /// should not commit. If unspecified the client may decide.
        /// </summary>
        public bool? _commitByDefault;
    }

    [Serializable]
    public class CompletionItem {
        public string label;
        public CompletionItemKind kind;
        public string detail;
        public MarkupContent documentation;
        public string sortText;
        public string filterText;
        public bool? preselect; // VS Code 1.25+
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
    [Serializable]
    public struct CompletionItemValue {
        public string description;
        public string documentation;
        public Reference[] references;
    }

    [Serializable]
    public struct CompletionRegistrationOptions : IRegistrationOptions {
        public DocumentFilter? documentSelector;
        public string[] triggerCharacters;
        public bool resolveProvider;
    }

    [Serializable]
    public class Hover {
        public MarkupContent contents;
        public Range? range;

        /// <summary>
        /// The document version that range applies to.
        /// </summary>
        public int? _version;
        /// <summary>
        /// List of fully qualified type names for the expression
        /// </summary>
        public string[] _typeNames;
    }

    [Serializable]
    public class SignatureHelp {
        public SignatureInformation[] signatures;
        public int activeSignature;
        public int activeParameter;
    }

    [Serializable]
    public class SignatureInformation {
        public string label;
        public MarkupContent documentation;
        public ParameterInformation[] parameters;

        public string[] _returnTypes;
    }

    [Serializable]
    public class ParameterInformation {
        public string label;
        public MarkupContent documentation;

        [NonSerialized]
        public string _type;
        [NonSerialized]
        public string _defaultValue;
        [NonSerialized]
        public bool? _isOptional;
    }

    /// <summary>
    /// Used instead of Position when returning references so we can include
    /// the kind.
    /// </summary>
    [Serializable]
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

    [Serializable]
    public struct DocumentHighlight {
        public Range range;
        public DocumentHighlightKind kind;

        /// <summary>
        /// The document version that range applies to
        /// </summary>
        public int? _version;
    }

    [Serializable]
    public struct DocumentSymbol {
        /// <summary>
        /// The name of this symbol.
        /// </summary>
        public string name;

        /// <summary>
        /// More detail for this symbol, e.g the signature of a function. If not provided the
        /// name is used.
        /// </summary>
        public string detail;

        /// <summary>
        /// The kind of this symbol.
        /// </summary>
        public SymbolKind kind;

        /// <summary>
        /// Indicates if this symbol is deprecated.
        /// </summary>
        public bool deprecated;

        /// <summary>
        /// The range enclosing this symbol not including leading/trailing whitespace but everything else
        /// like comments.This information is typically used to determine if the clients cursor is
        /// inside the symbol to reveal in the symbol in the UI.
        /// </summary>
        public Range range;

        /// <summary>
        /// The range that should be selected and revealed when this symbol is being picked, 
        /// e.g the name of a function. Must be contained by the `range`.
        /// </summary>
        public Range selectionRange;

        /// <summary>
        /// Children of this symbol, e.g. properties of a class.
        /// </summary>
        public DocumentSymbol[] children;

        /// <summary>
        /// Custom field provides more information on the function or method such as 
        /// 'classmethod' or 'property' that are not part of the <see cref="SymbolKind"/>.
        /// </summary>
        public string _functionKind;
    }

    [Serializable]
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

    [Serializable]
    public struct CodeLens {
        public Range range;
        public Command? command;
        public object data;

        /// <summary>
        /// The document version that range applies to
        /// </summary>
        public int? _version;
    }

    [Serializable]
    public struct DocumentLink {
        public Range range;
        public Uri target;

        /// <summary>
        /// The document version tha range applies to
        /// </summary>
        public int? _version;
    }

    [Serializable]
    public struct DocumentLinkRegistrationOptions : IRegistrationOptions {
        public DocumentFilter? documentSelector;
        public bool resolveProvider;
    }

    [Serializable]
    public struct FormattingOptions {
        public int tabSize;
        public bool insertSpaces;

    }

    [Serializable]
    public struct DocumentOnTypeFormattingRegistrationOptions : IRegistrationOptions {
        public DocumentFilter? documentSelector;
        public string firstTriggerCharacter;
        public string[] moreTriggerCharacters;
    }
    internal static class MarkupKind {
        public const string PlainText = "plaintext";
        public const string Markdown = "markdown";
    }
}

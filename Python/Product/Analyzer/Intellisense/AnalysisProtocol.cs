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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Ipc.Json;
using Microsoft.PythonTools.Parsing;
using Newtonsoft.Json;
using LS = Microsoft.PythonTools.Analysis.LanguageServer;

namespace Microsoft.PythonTools.Intellisense {
    public static class AnalysisProtocol {
        public static readonly Dictionary<string, Type> RegisteredTypes = CollectCommands();

        private static Dictionary<string, Type> CollectCommands() {
            Dictionary<string, Type> all = new Dictionary<string, Type>();
            foreach (var type in typeof(AnalysisProtocol).GetNestedTypes()) {
                if (type.IsSubclassOf(typeof(Request))) {
                    var command = type.GetField("Command");
                    if (command != null) {
                        all["request." + (string)command.GetRawConstantValue()] = type;
                    }
                } else if (type.IsSubclassOf(typeof(Event))) {
                    var name = type.GetField("Name");
                    if (name != null) {
                        all["event." + (string)name.GetRawConstantValue()] = type;
                    }
                }
            }
            return all;
        }

        public sealed class InitializeRequest : Request<InitializeResponse> {
            public const string Command = "initialize";

            public override string command => Command;

            public InterpreterInfo interpreter;

            [JsonConverter(typeof(UriJsonConverter))]
            public Uri rootUri;
            public bool analyzeAllFiles;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public bool traceLogging;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public bool liveLinting;
        }

        public sealed class InterpreterInfo {
            public string assembly, typeName;
            public Dictionary<string, object> properties;
        }

        public sealed class InitializeResponse : Response {
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            public string error;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            public string fullError;
        }

        public sealed class ExitRequest : GenericRequest {
            public const string Command = "exit";

            public override string command => Command;
        }

        public sealed class GetReferencesResponse : Response {
            public ProjectReference[] references;
        }

        public sealed class ProjectReference {
            public string name, kind, assemblyName;

            public static ProjectReference Convert(Microsoft.PythonTools.Interpreter.ProjectReference reference) {
                return new ProjectReference() {
                    name = reference.Name,
                    kind = GetReferenceKind(reference.Kind),
                    assemblyName = GetReferenceAssembly(reference)
                };
            }

            public static Microsoft.PythonTools.Interpreter.ProjectReference Convert(ProjectReference reference) {
                switch (reference.kind) {
                    case "extension":
                        return new Microsoft.PythonTools.Interpreter.ProjectReference(
                            reference.name,
                            ProjectReferenceKind.ExtensionModule
                        );
                    case "assembly":
                        return new ProjectAssemblyReference(
                            new AssemblyName(reference.assemblyName),
                            reference.name
                        );
                    default:
                        throw new InvalidOperationException("Unsupported reference type " + reference.kind);
                }
            }

            private static string GetReferenceAssembly(Microsoft.PythonTools.Interpreter.ProjectReference reference) {
                switch (reference.Kind) {
                    case ProjectReferenceKind.Assembly:
                        return ((ProjectAssemblyReference)reference).AssemblyName.FullName;
                    default: return null;
                }
            }

            public static string GetReferenceKind(ProjectReferenceKind kind) {
                switch (kind) {
                    case ProjectReferenceKind.Assembly: return "assembly";
                    case ProjectReferenceKind.ExtensionModule: return "extension";
                    default: return null;
                }
            }

        }

        public sealed class SetAnalysisLimitsRequest : Request {
            public const string Command = "setAnalysisLimits";

            public override string command => Command;

        }

        public sealed class ValueDescriptionRequest : Request<ValueDescriptionResponse> {
            public const string Command = "valueDescriptions";

            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;
            public string expr;
            public int line, column;

            public override string command => Command;
        }

        public sealed class ValueDescriptionResponse : Response {
            public string[] descriptions;
        }

        public sealed class AddReferenceRequest : Request<AddReferenceResponse> {
            public const string Command = "addReference";
            public ProjectReference reference;

            public override string command => Command;
        }

        public sealed class AddReferenceResponse : Response {
        }

        public sealed class RemoveReferenceRequest : Request<RemoveReferenceResponse> {
            public const string Command = "removeReference";
            public ProjectReference reference;

            public override string command => Command;
        }

        public sealed class RemoveReferenceResponse : Response {
        }

        public sealed class AnalysisClassificationsRequest : Request<AnalysisClassificationsResponse> {
            public const string Command = "analysisClassify";

            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;
            public bool colorNames;

            public override string command => Command;
        }

        /// <summary>
        /// Gets a location where a method can safely be inserted into a top level class
        /// </summary>
        public sealed class MethodInsertionLocationRequest : Request<MethodInsertionLocationResponse> {
            public const string Command = "methodInsertion";

            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;
            public string className;

            public override string command => Command;
        }

        public sealed class MethodInsertionLocationResponse : Response {
            public int line, column;
            public int version;
        }

        public sealed class MethodInfoRequest : Request<MethodInfoResponse> {
            public const string Command = "methodInfo";

            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;
            public string className;
            public string methodName;

            public override string command => Command;
        }

        public sealed class MethodInfoResponse : Response {
            public int start, end;
            public int version;
            public bool found;
        }

        public sealed class FindMethodsRequest : Request<FindMethodsResponse> {
            public const string Command = "findMethods";

            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;
            public string className;

            /// <summary>
            /// Optional filter of the number of parameters
            /// </summary>
            public int? paramCount;

            public override string command => Command;
        }

        public sealed class FindMethodsResponse : Response {
            public string[] names;
        }

        public sealed class AnalysisClassificationsResponse : Response {
            public AnalysisClassification[] classifications;

            public int version;
        }

        public sealed class AnalysisClassification {
            public int startLine, startColumn;
            public int endLine, endColumn;
            public string type;
        }

        public class QuickInfoRequest : Request<QuickInfoResponse> {
            public const string Command = "quickInfo";

            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;
            public string expr;
            public int line, column;

            public override string command => Command;
        }

        public class QuickInfoResponse : Response {
            public string text;
        }

        public class FileParsedEvent : Event {
            public const string Name = "fileParsed";

            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;
            public int version;

            public override string name => Name;
        }

        public class DiagnosticsEvent : Event {
            public const string Name = "diagnostics";

            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;
            public int version;
            public LS.Diagnostic[] diagnostics;

            public bool ShouldSerializediagnostics() => (diagnostics?.Length ?? 0) > 0;

            public override string name => Name;
        }

        public sealed class FormatCodeRequest : Request<FormatCodeResponse> {
            public const string Command = "formatCode";

            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;
            public int startLine, startColumn;
            public int endLine, endColumn;
            public string newLine;
            public CodeFormattingOptions options;

            public override string command => Command;
        }

        public sealed class FormatCodeResponse : Response {
            public ChangeInfo[] changes;
            public int version;
        }

        public struct CodeSpan {
            public int start, length;
        }

        public class AddFileRequest : Request<AddFileResponse> {
            public const string Command = "addFile";

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string path;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            public string addingFromDir;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            public bool isTemporaryFile;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            public bool suppressErrorLists;

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            [JsonConverter(typeof(UriJsonConverter))]
            public Uri uri;

            public override string command => Command;
        }

        public class AddFileResponse : Response {
            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;
        }

        public class AddBulkFileRequest : Request<AddBulkFileResponse> {
            public const string Command = "addBulkFile";

            public string[] path;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string addingFromDir;

            public override string command => Command;
        }

        public class AddBulkFileResponse : Response {
            [JsonProperty(ItemConverterType = typeof(UriJsonConverter))]
            public Uri[] documentUri;
        }

        public sealed class SetSearchPathRequest : Request<Response> {
            public const string Command = "setSearchPath";

            public string[] dir;
            public override string command => Command;
        }

        public sealed class UnloadFileRequest : Request<Response> {
            public const string Command = "unloadFile";

            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;
            public override string command => Command;

            public override string ToString() => "{0}:{1}".FormatUI(command, documentUri);
        }


        public sealed class DirectoryFileAddedEvent : Event {
            public const string Name = "directoryFileAdded";

            public string filename;
            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;

            public override string name => Name;
        }

        public sealed class FileUpdateRequest : Request<FileUpdateResponse> {
            public const string Command = "fileUpdate";

            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;
            public FileUpdate[] updates;

            public override string command => Command;

            public override string ToString() => "{0}:{1} ({2} updates)".FormatUI(command, documentUri, updates.Length);
        }

        public enum FileUpdateKind {
            none,
            /// <summary>
            /// Reset the content to the specified content string
            /// </summary>
            reset,
            /// <summary>
            /// Apply the list of changes to the content
            /// </summary>
            changes
        }

        public sealed class AddImportRequest : Request<AddImportResponse> {
            public const string Command = "addImport";

            public string fromModule, name, newLine;
            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;

            public override string command => Command;
        }

        public sealed class AddImportResponse : Response {
            public ChangeInfo[] changes;
            public int version = -1;
        }

        public sealed class IsMissingImportRequest : Request<IsMissingImportResponse> {
            public const string Command = "isMissingImport";

            public string text;
            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;
            public int line, column;

            public override string command => Command;
        }

        public sealed class IsMissingImportResponse : Response {
            public bool isMissing;
        }

        public sealed class AvailableImportsRequest : Request<AvailableImportsResponse> {
            public const string Command = "availableImports";

            public string name;

            public override string command => Command;
        }

        public sealed class AvailableImportsResponse : Response {
            public ImportInfo[] imports;
        }

        public sealed class ImportInfo {
            public string fromName, importName;


            // Provide Equals so we can easily uniquify sequences of ImportInfo

            public override bool Equals(object obj) {
                if (obj is ImportInfo ii) {
                    return fromName == ii.fromName && importName == ii.importName;
                }
                return false;
            }

            public override int GetHashCode() {
                return ((fromName ?? "") + "." + (importName ?? "")).GetHashCode();
            }
        }

        public sealed class FileUpdate {
            public FileUpdateKind kind;

            // Unlike most version numbers, this is what the version will be
            // _after_ applying the update, not before. The target file is
            // assumed to be at version-1 when applying this change.
            public int version;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public ChangeInfo[] changes;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string content;
        }

        public sealed class FileUpdateResponse : Response {
            public int version;
#if DEBUG
            public string newCode;
#endif
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public bool? failed;
        }

        public sealed class UnresolvedImport {
            public string name;
            public int startLine, endLine, startColumn, endColumn;
        }

        public sealed class ChangeInfo {
            public string newText;
            public int startLine, startColumn;
            public int endLine, endColumn;
#if DEBUG
            public int _startIndex, _endIndex;
#endif

            public static ChangeInfo FromDocumentChange(DocumentChange c) {
                return new ChangeInfo {
                    startLine = c.ReplacedSpan.Start.Line,
                    startColumn = c.ReplacedSpan.Start.Column,
                    endLine = c.ReplacedSpan.End.Line,
                    endColumn = c.ReplacedSpan.End.Column,
                    newText = c.InsertedText
                };
            }

            public DocumentChange ToDocumentChange() {
                return new DocumentChange {
                    InsertedText = newText,
                    ReplacedSpan = new SourceSpan(new SourceLocation(startLine, startColumn), new SourceLocation(endLine, endColumn))
                };
            }
        }

        public sealed class LocationNameRequest : Request<LocationNameResponse> {
            public const string Command = "locationName";

            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;
            public int line, column;

            public override string command => Command;
        }

        public sealed class LocationNameResponse : Response {
            public string name;
            public int lineOffset;
        }


        public sealed class ProximityExpressionsRequest : Request<ProximityExpressionsResponse> {
            public const string Command = "proximityExpressions";

            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;
            public int line, column, lineCount;

            public override string command => Command;
        }

        public sealed class ProximityExpressionsResponse : Response {
            public string[] names;
        }

        public sealed class RemoveImportsRequest : Request<RemoveImportsResponse> {
            public const string Command = "removeImports";

            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;
            public int version;
            public int line, column;
            public bool allScopes;

            public override string command => Command;
        }

        public sealed class RemoveImportsResponse : Response {
            public ChangeInfo[] changes;
            public int version = -1;
        }

        public sealed class ExtractMethodRequest : Request<ExtractMethodResponse> {
            public const string Command = "extractMethod";

            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;
            public int startIndex, endIndex;
            public int indentSize;
            public string name;
            public string[] parameters;
            public bool convertTabsToSpaces;
            public string newLine;
            public bool shouldExpandSelection;
            public int? scope;

            public override string command => Command;
        }

        public sealed class ExtractMethodResponse : Response {
            public CannotExtractReason cannotExtractReason;
            public ChangeInfo[] changes;
            /// <summary>
            /// available scopes the user can retarget to
            /// </summary>
            public ScopeInfo[] scopes;
            public bool wasExpanded;
            public int startLine, startCol;
            public int endLine, endCol;
            public int version;
            public string methodBody;
            public string[] variables;
        }
        
        public enum CannotExtractReason {
            None = 0,
            InvalidTargetSelected = 1,
            InvalidExpressionSelected = 2,
            MethodAssignsVariablesAndReturns = 3,
            StatementsFromClassDefinition = 4,
            SelectionContainsBreakButNotEnclosingLoop = 5,
            SelectionContainsContinueButNotEnclosingLoop = 6,
            ContainsYieldExpression = 7,
            ContainsFromImportStar = 8,
            SelectionContainsReturn = 9
        }

        public class ScopeInfo {
            public string type, name;
            public int id;
            public string[] variables;
        }

        public sealed class ModuleImportsRequest : Request<ModuleImportsResponse> {
            public const string Command = "moduleImports";

            public string moduleName;
            public bool includeUnresolved;

            public override string command => Command;
        }

        public sealed class ModuleImportsResponse : Response {
            public ModuleInfo[] modules;
        }

        public sealed class ModuleInfo {
            public string moduleName;
            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;
            public string filename;
        }

        public class EnqueueFileResponse : Response {
            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;
        }

        public class GetModulesRequest : Request<CompletionsResponse> {
            public const string Command = "getModules";

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;
            public string[] package;

            public override string command => Command;

            public bool ShouldSerializepackage() => (package?.Length ?? 0) > 0;
        }

        public class GetAllMembersRequest : Request<CompletionsResponse> {
            public const string Command = "getAllMembers";

            public string prefix;
            public GetMemberOptions options;

            public override string command => Command;
        }

        public class CompletionsRequest : Request<CompletionsResponse> {
            public const string Command = "completions";

            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;
            public string text;
            public int line, column;
            public GetMemberOptions options;
            public bool forceCompletions;

            public override string command => Command;
        }

        public class SignaturesRequest : Request<SignaturesResponse> {
            public const string Command = "sigs";

            public override string command => Command;

            public string text;
            public int line, column;
            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;
        }

        public sealed class ModulesChangedEvent : Event {
            public const string Name = "modulesChanged";

            public override string name => Name;
        }

        public struct FileEvent {
            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;
            public LS.FileChangeType kind;
        }

        public sealed class FileChangedEvent : Event {
            public const string Name = "fileChanged";

            public FileEvent[] changes;

            public override string name => Name;
        }

        public sealed class SignaturesResponse : Response {
            public Signature[] sigs;
        }

        public class Signature {
            public string name;
            public string doc;
            public Parameter[] parameters;
        }

        public class Parameter {
            public string name, defaultValue, doc, type;
            public bool optional;
        }

        public class FileAnalysisCompleteEvent : Event {
            public const string Name = "fileAnalysisComplete";
            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;

            public int version;

            public override string name => Name;
            public override string ToString() => "{0}:{1} ({2})".FormatUI(name, documentUri, version);
        }

        public sealed class LoadExtensionRequest : Request<LoadExtensionResponse> {
            public const string Command = "loadExtensionRequest";

            public override string command => Command;

            public string extension;
            public string assembly;
            public string typeName;
        }

        public sealed class LoadExtensionResponse : Response {
            public string error;
            public string fullError;
        }

        public sealed class ExtensionRequest : Request<ExtensionResponse> {
            public const string Command = "extensionRequest";

            public override string command => Command;

            public string extension;
            public string commandId;
            public string body;
        }

        public sealed class ExtensionResponse : Response {
            public string response;
            public string error;
        }

        /// <summary>
        /// Signals all files are analyzed
        /// </summary>
        public class AnalysisCompleteEvent : Event {
            public const string Name = "analysisComplete";

            public override string name => Name;
        }

        public sealed class AnalysisStatusRequest : Request<AnalysisStatusResponse> {
            public const string Command = "analysisStatus";

            public override string command => Command;
        }

        public sealed class AnalysisStatusResponse : Response {
            public int itemsLeft;
        }


        public class CompletionsResponse : Response {
            public Completion[] completions;
        }

        public class Completion {
            public string name;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            public string completion;   // when null, use "name"
            public string doc;
            public PythonMemberType memberType;

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public CompletionValue[] detailedValues;

            public bool ShouldSerializedetailedValues() => (detailedValues?.Length ?? 0) > 0;
        }

        public sealed class CompletionValue {
            public DescriptionComponent[] description;
            public string doc;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public AnalysisReference[] locations;

            public bool ShouldSerializelocations() => (locations?.Length ?? 0) > 0;
        }

        public sealed class DescriptionComponent {
            public string text, kind;

            public DescriptionComponent() {
            }

            public DescriptionComponent(string text, string kind) {
                this.text = text;
                this.kind = kind;
            }
        }

        public sealed class SetAnalysisOptionsRequest : Request<Response> {
            public const string Command = "setAnalysisOptions";

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public AnalysisOptions options;

            public override string command => Command;
        }

        public sealed class AnalysisOptions {
            public Severity indentationInconsistencySeverity;
            public Dictionary<string, LS.DiagnosticSeverity> commentTokens;
            public Dictionary<string, int> analysisLimits;
            public LS.MessageType? traceLevel;
            public string[] typeStubPaths;
        }



        public class AnalysisReference {
            public string kind; // definition, reference, value
            public string expr;
            public string file;
            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;
            public int startLine, startColumn, endLine, endColumn;
            public int? version;
        }

        public sealed class AnalyzeExpressionRequest : Request<AnalyzeExpressionResponse> {
            public const string Command = "findDefs";

            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;
            public string expr;
            public int line, column;

            public override string command => Command;
        }

        public sealed class AnalyzeExpressionResponse : Response {
            public AnalysisReference[] variables;
            /// <summary>
            /// The private prefix for the member if defined inside a class with name mangling.
            /// </summary>
            public string privatePrefix;
        }

        public sealed class OutliningRegionsRequest : Request<OutliningRegionsResponse> {
            public const string Command = "outliningRegions";
            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;

            public override string command => Command;
        }

        public sealed class OutliningRegionsResponse : Response {
            public int version = -1;
            public OutliningTag[] tags;
        }

        public sealed class OutliningTag {
            public int startLine, startCol;
            public int endLine, endCol;
        }

        public sealed class NavigationRequest : Request<NavigationResponse> {
            public const string Command = "navigation";
            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;

            public override string command => Command;
        }

        public sealed class NavigationResponse : Response {
            public int version;
            public Navigation[] navigations;
        }

        public sealed class Navigation {
            public string name, type;
            public int startLine, startColumn;
            public int endLine, endColumn;
            public Navigation[] children;
        }

        public class AnalyzerWarningEvent : Event {
            public string message;
            public const string Name = "analyzerWarning";

            public override string name => Name;
        }

        public class UnhandledExceptionEvent : Event {
            public string message;
            public const string Name = "unhandledException";

            public UnhandledExceptionEvent(Exception ex) {
                message = ex.ToString();
            }

            public UnhandledExceptionEvent(string message) {
                this.message = message;
            }

            public override string name => Name;
        }

        public enum ExpressionAtPointPurpose : int {
            Hover = 1,
            Evaluate = 2,
            EvaluateMembers = 3,
            FindDefinition = 4,
            Rename = 5
        }

        public sealed class ExpressionAtPointRequest : Request<ExpressionAtPointResponse> {
            public const string Command = "exprAtPoint";

            [JsonConverter(typeof(UriJsonConverter))]
            public Uri documentUri;
            public int line, column;
            public ExpressionAtPointPurpose purpose;

            public override string command => Command;
        }

        public sealed class ExpressionAtPointResponse : Response {
            public string expression;
            public string type;
            public int bufferVersion;
            public int startLine, startColumn;
            public int endLine, endColumn;
        }

        public class LanguageServerRequest : Request<LanguageServerResponse> {
            public const string Command = "languageServer";

            public string name;
            public object body;

            public override string command => Command;
        }

        public class LanguageServerResponse : Response {
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public object body;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string error;
        }
    }
}

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
using System.Reflection;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Ipc.Json;
using Microsoft.PythonTools.Parsing;
using Newtonsoft.Json;

namespace Microsoft.PythonTools.Intellisense {
    internal static class AnalysisProtocol {
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

            public string[] mefExtensions;
            public string interpreterId, projectFile, projectHome;

            public DerivedInterpreter[] derivedInterpreters;

        }

        public sealed class DerivedInterpreter {
            public string name, id, description, version, baseInterpreter, path, windowsPath, libPath, pathEnvVar, arch;
        }

        public sealed class InitializeResponse : Response {
            public string[] failedLoads;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            public string error;

            public bool ShouldSerializefailedLoads() => (failedLoads?.Length ?? 0) > 0;
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

        public sealed class ValueDescriptionRequest : Request<ValueDescriptionResponse> {
            public const string Command = "valueDescriptions";

            public int fileId;
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

            public int fileId, bufferId;
            public bool colorNames;

            public override string command => Command;
        }

        /// <summary>
        /// Gets a location where a method can safely be inserted into a top level class
        /// </summary>
        public sealed class MethodInsertionLocationRequest : Request<MethodInsertionLocationResponse> {
            public const string Command = "methodInsertion";

            public int fileId, bufferId;
            public string className;

            public override string command => Command;
        }

        public sealed class MethodInsertionLocationResponse : Response {
            public int location, indentation;
            public int version;
        }

        public sealed class MethodInfoRequest : Request<MethodInfoResponse> {
            public const string Command = "methodInfo";

            public int fileId, bufferId;
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

            public int fileId, bufferId;
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
            public int start, length;
            public string type;
        }

        public class QuickInfoRequest : Request<QuickInfoResponse> {
            public const string Command = "quickInfo";

            public int fileId;
            public string expr;
            public int line, column;

            public override string command => Command;
        }

        public class QuickInfoResponse : Response {
            public string text;
        }

        public class FileParsedEvent : Event {
            public const string Name = "fileParsed";

            public int fileId;
            public BufferParseInfo[] buffers;

            public override string name => Name;
        }

        public class BufferParseInfo {
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            public int bufferId;
            public int version;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            public bool hasErrors;
            public Error[] errors;
            public Error[] warnings;
            public TaskItem[] tasks;

            public bool ShouldSerializeerrors() => (errors?.Length ?? 0) > 0;
            public bool ShouldSerializewarnings() => (warnings?.Length ?? 0) > 0;
            public bool ShouldSerializetasks() => (tasks?.Length ?? 0) > 0;
        }

        public sealed class FormatCodeRequest : Request<FormatCodeResponse> {
            public const string Command = "formatCode";

            public int fileId, bufferId, startIndex, endIndex;
            public string newLine;
            public CodeFormattingOptions options;

            public override string command => Command;
        }

        public sealed class FormatCodeResponse : Response {
            public ChangeInfo[] changes;

            public int startIndex, endIndex;
            public int version = -1;
        }

        public struct CodeSpan {
            public int start, length;
        }

        public class Error {
            public string message;
            public int startLine, startColumn;
            public int endLine, endColumn, length;
        }

        public class AddFileRequest : Request<AddFileResponse> {
            public const string Command = "addFile";

            public string path;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            public string addingFromDir;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            public bool isTemporaryFile;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            public bool suppressErrorLists;

            public override string command => Command;
        }

        public class AddFileResponse : Response {
            public int fileId;
        }

        public class AddBulkFileRequest : Request<AddBulkFileResponse> {
            public const string Command = "addBulkFile";

            public string[] path;
            public string addingFromDir;

            public override string command => Command;
        }

        public class AddBulkFileResponse : Response {
            public int[] fileId;
        }

        public sealed class SetSearchPathRequest : Request<Response> {
            public const string Command = "setSearchPath";

            public string[] dir;
            public override string command => Command;
        }

        public sealed class UnloadFileRequest : Request<Response> {
            public const string Command = "unloadFile";

            public int fileId;
            public override string command => Command;

            public override string ToString() => "{0}:{1}".FormatUI(command, fileId);
        }


        public sealed class DirectoryFileAddedEvent : Event {
            public const string Name = "directoryFileAdded";

            public string filename;
            public int fileId;

            public override string name => Name;
        }

        public sealed class FileUpdateRequest : Request<FileUpdateResponse> {
            public const string Command = "fileUpdate";

            public int fileId;
            public FileUpdate[] updates;

            public override string command => Command;

            public override string ToString() => "{0}:{1} ({2} updates)".FormatUI(command, fileId, updates.Length);
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
            public int fileId, bufferId;

            public override string command => Command;
        }

        public sealed class AddImportResponse : Response {
            public ChangeInfo[] changes;
            public int version = -1;
        }

        public sealed class IsMissingImportRequest : Request<IsMissingImportResponse> {
            public const string Command = "isMissingImport";

            public string text;
            public int line, column, fileId, bufferId;

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
            public int bufferId, version;
            public VersionChanges[] versions;
            public string content;
        }

        public sealed class FileUpdateResponse : Response {
#if DEBUG
            public Dictionary<int, string> newCode;
#endif
            public bool? failed;
        }

        public sealed class UnresolvedImportsRequest : Request<UnresolvedImportsResponse> {
            public const string Command = "unresolvedImports";

            public override string command => Command;

            public int fileId, bufferId;
        }

        public sealed class UnresolvedImportsResponse : Response {
            public int version = -1;
            public UnresolvedImport[] unresolved;
        }

        public sealed class UnresolvedImport {
            public string name;
            public int startLine, endLine, startColumn, endColumn;
        }

        public sealed class FileChangedResponse : Response {
#if DEBUG
            public string newCode;
#endif
        }

        public sealed class VersionChanges {
            public ChangeInfo[] changes;
        }

        public sealed class ChangeInfo {
            public string newText;
            public int start;
            public int length;

            public static ChangeInfo FromBounds(string text, int start, int end) {
                return new ChangeInfo() {
                    newText = text,
                    start = start,
                    length = end - start
                };
            }
        }

        public sealed class LocationNameRequest : Request<LocationNameResponse> {
            public const string Command = "locationName";

            public int fileId, bufferId, line, column;

            public override string command => Command;
        }

        public sealed class LocationNameResponse : Response {
            public string name;
            public int lineOffset;
        }


        public sealed class ProximityExpressionsRequest : Request<ProximityExpressionsResponse> {
            public const string Command = "proximityExpressions";

            public int fileId, bufferId, line, column, lineCount;

            public override string command => Command;
        }

        public sealed class ProximityExpressionsResponse : Response {
            public string[] names;
        }

        public sealed class OverridesCompletionRequest : Request<OverridesCompletionResponse> {
            public const string Command = "overrides";

            public int fileId, bufferId;
            public int line, column;
            public string indentation;

            public override string command => Command;
        }

        public sealed class OverridesCompletionResponse : Response {
            public Override[] overrides;
        }

        public sealed class Override {
            public string name, doc, completion;
        }

        public sealed class RemoveImportsRequest : Request<RemoveImportsResponse> {
            public const string Command = "removeImports";

            public int fileId, bufferId, index;
            public bool allScopes;

            public override string command => Command;
        }

        public sealed class RemoveImportsResponse : Response {
            public ChangeInfo[] changes;
            public int version = -1;
        }

        public sealed class ExtractMethodRequest : Request<ExtractMethodResponse> {
            public const string Command = "extractMethod";

            public int fileId, bufferId, startIndex, endIndex;
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
            public string cannotExtractMsg;
            public ChangeInfo[] changes;
            /// <summary>
            /// available scopes the user can retarget to
            /// </summary>
            public ScopeInfo[] scopes;
            public bool wasExpanded;
            public int? startIndex, endIndex;
            public int version;
            public string methodBody;
            public string[] variables;
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
            public int fileId;
            public string filename;
        }

        public class EnqueueFileResponse : Response {
            public int fileId;
        }

        public sealed class ChildFileAnalyzed : Event {
            public const string Name = "childFileAnalyzed";

            /// <summary>
            /// The filename which got added
            /// </summary>
            public string filename;

            public int fileId;

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            public bool isTemporaryFile, suppressErrorList;

            public override string name => Name;
        }

        public class TopLevelCompletionsRequest : Request<CompletionsResponse> {
            public const string Command = "topCompletions";

            public int fileId;
            public int line, column;
            public GetMemberOptions options;

            public override string command => Command;
        }

        public class GetModulesRequest : Request<CompletionsResponse> {
            public const string Command = "getModules";

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            public int fileId;
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

            public int fileId, bufferId;
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
            public int fileId;
        }

        public sealed class ModulesChangedEvent : Event {
            public const string Name = "modulesChanged";

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
            public AnalysisReference[] variables;
        }

        public class FileAnalysisCompleteEvent : Event {
            public const string Name = "fileAnalysisComplete";
            public int fileId;

            public BufferVersion[] versions;

            public override string name => Name;
        }

        public sealed class BufferVersion {
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            public int bufferId;
            public int version;
        }

        public sealed class ExtensionAddedEvent : Event {
            public const string Name = "extensionAdded";

            public string path;

            public override string name => Name;
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
            public string completion;
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

        public sealed class OptionsChangedEvent : Event {
            public const string Name = "optionsChanged";

            public Severity indentation_inconsistency_severity;
            public int? crossModuleAnalysisLimit;

            public override string name => Name;
        }

        public sealed class SetCommentTaskTokens : Event {
            public const string Name = "setCommentTaskTokens";

            public Dictionary<string, TaskPriority> tokens;

            public override string name => Name;
        }

        public sealed class TaskItem : Error {
            public TaskPriority priority;
            public TaskCategory category;
            public bool squiggle;
        }

        public enum TaskPriority {
            high,
            normal,
            low
        }

        public enum TaskCategory {
            buildCompile,
            comments
        }



        public class AnalysisReference {
            public string kind; // definition, reference, value
            public string expr;
            public string file;
            public int line, column;
            // the span of the whole definition, when applicable
            public int? definitionStartLine, definitionStartColumn;
            public int? definitionEndLine, definitionEndColumn;
        }

        public sealed class AnalyzeExpressionRequest : Request<AnalyzeExpressionResponse> {
            public const string Command = "findDefs";

            public int fileId;
            public string expr;
            public int line, column;
            [Obsolete("only use line and column")]
            public int index;

            public override string command => Command;
        }

        public sealed class AnalyzeExpressionResponse : Response {
            public AnalysisReference[] variables;
            /// <summary>
            /// The private prefix for the member if defined inside a class with name mangling.
            /// </summary>
            public string privatePrefix;
            /// <summary>
            /// The plain member name, e.g. "member" in a statement like "container.member"
            /// </summary>
            public string memberName;
        }

        public sealed class OutliningRegionsRequest : Request<OutliningRegionsResponse> {
            public const string Command = "outliningRegions";
            public int fileId, bufferId;

            public override string command => Command;
        }

        public sealed class OutliningRegionsResponse : Response {
            public int version = -1;
            public OutliningTag[] tags;
        }

        public sealed class OutliningTag {
            public int headerIndex, startIndex, endIndex;
        }

        public sealed class NavigationRequest : Request<NavigationResponse> {
            public const string Command = "navigation";
            public int fileId;
            public int bufferId;

            public override string command => Command;
        }

        public sealed class NavigationResponse : Response {
            public int version;
            public Navigation[] navigations;
        }

        public sealed class Navigation {
            public string name, type;
            public int startIndex, endIndex, bufferId;
            public Navigation[] children;
        }

        internal class AnalyzerWarningEvent : Event {
            public string message;
            public const string Name = "analyzerWarning";

            public AnalyzerWarningEvent(string message) {
                this.message = message;
            }

            public override string name => Name;
        }

        internal class UnhandledExceptionEvent : Event {
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

            public int fileId, bufferId;
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
    }
}

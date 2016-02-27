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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Communication;
using Microsoft.PythonTools.Cdp;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;


namespace Microsoft.PythonTools.Intellisense {
    using Analysis.Project;
    using static AnalysisProtocol;
    using AP = AnalysisProtocol;

    /// <summary>
    /// Performs centralized parsing and analysis of Python source code within Visual Studio.
    /// 
    /// This class is responsible for maintaining the up-to-date analysis of the active files being worked
    /// on inside of a Visual Studio project.  
    /// 
    /// This class is built upon the core PythonAnalyzer class which provides basic analysis services.  This class
    /// maintains the thread safety invarients of working with that class, handles parsing of files as they're
    /// updated via interfacing w/ the Visual Studio editor APIs, and supports adding additional files to the 
    /// analysis.
    /// 
    /// New in 1.5.
    /// </summary>
    sealed class OutOfProcProjectAnalyzer : IDisposable {
        // For entries that were loaded from a .zip file, IProjectEntry.Properties[_zipFileName] contains the full path to that archive.
        private static readonly object _zipFileName = new { Name = "ZipFileName" };

        // For entries that were loaded from a .zip file, IProjectEntry.Properties[_pathInZipFile] contains the path of the item inside the archive.
        private static readonly object _pathInZipFile = new { Name = "PathInZipFile" };

        private readonly AnalysisQueue _analysisQueue;
        private readonly IPythonInterpreterFactory _interpreterFactory;
        //private readonly Dictionary<BufferParser, IProjectEntry> _openFiles = new Dictionary<BufferParser, IProjectEntry>();
        private readonly ProjectEntryMap _projectFiles;
        private readonly PythonAnalyzer _pyAnalyzer;
        private readonly AutoResetEvent _queueActivityEvent = new AutoResetEvent(false);
        private readonly IPythonInterpreterFactory[] _allFactories;
        private volatile Dictionary<string, AP.TaskPriority> _commentPriorityMap = new Dictionary<string, AP.TaskPriority>() {
            { "TODO", AP.TaskPriority.normal },
            { "HACK", AP.TaskPriority.high },
        };
        private AP.OptionsChangedEvent _options;
        internal int _analysisPending;

        // Moniker strings allow the task provider to distinguish between
        // different sources of items for the same file.
        private const string ParserTaskMoniker = "Parser";
        internal const string UnresolvedImportMoniker = "UnresolvedImport";

#if PORT
        private readonly UnresolvedImportSquiggleProvider _unresolvedSquiggles;
#endif

        private readonly Connection _connection;
        internal Task ReloadTask;

        internal OutOfProcProjectAnalyzer(
            Stream writer, Stream reader,
            IPythonInterpreterFactory factory,
            IPythonInterpreterFactory[] allFactories)
            : this(writer, reader, factory.CreateInterpreter(), factory, allFactories) {
        }

        internal OutOfProcProjectAnalyzer(
            Stream writer, Stream reader,
            IPythonInterpreter interpreter,
            IPythonInterpreterFactory factory,
            IPythonInterpreterFactory[] allFactories
        ) {
#if PORT
            _errorProvider = (ErrorTaskProvider)serviceProvider.GetService(typeof(ErrorTaskProvider));
            _commentTaskProvider = (CommentTaskProvider)serviceProvider.GetService(typeof(CommentTaskProvider));
            _unresolvedSquiggles = new UnresolvedImportSquiggleProvider(serviceProvider, _errorProvider);
#endif

            _analysisQueue = new AnalysisQueue(this);
            _analysisQueue.AnalysisStarted += AnalysisQueue_AnalysisStarted;
            _allFactories = allFactories;
            _options = new AP.OptionsChangedEvent() {
                implicitProject = false,
                indentation_inconsistency_severity = Severity.Ignore
            };
            _interpreterFactory = factory;

            if (interpreter != null) {
                _pyAnalyzer = PythonAnalyzer.Create(factory, interpreter);
                ReloadTask = _pyAnalyzer.ReloadModulesAsync()/*.HandleAllExceptions(_serviceProvider, GetType())*/;
                ReloadTask.ContinueWith(_ => ReloadTask = null);
                interpreter.ModuleNamesChanged += OnModulesChanged;
            }

            _projectFiles = new ProjectEntryMap();
            _connection = new Connection(writer, reader, RequestHandler, AnalysisProtocol.RegisteredTypes);
            _connection.EventReceived += ConectionReceivedEvent;
        }

        private void ConectionReceivedEvent(object sender, EventReceivedEventArgs e) {
            switch (e.Event.name) {
                case AP.ModulesChangedEvent.Name: OnModulesChanged(this, EventArgs.Empty); break;
                case AP.OptionsChangedEvent.Name: SetOptions((AP.OptionsChangedEvent)e.Event); break;
                case AP.SetCommentTaskTokens.Name: _commentPriorityMap = ((AP.SetCommentTaskTokens)e.Event).tokens; break;
            }
        }

        private void SetOptions(AP.OptionsChangedEvent options) {
            _pyAnalyzer.Limits.CrossModule = options.crossModuleAnalysisLimit;
            _options = options;
        }

        private async Task<Response> RequestHandler(RequestArgs requestArgs) {
            await Task.FromResult((object)null);
            var command = requestArgs.Command;
            var request = requestArgs.Request;

            switch (command) {
                case AP.UnloadFileRequest.Command: return UnloadFile((AP.UnloadFileRequest)request);
                case AP.AddFileRequest.Command: return AnalyzeFile((AP.AddFileRequest)request);

                case AP.TopLevelCompletionsRequest.Command: return GetTopLevelCompletions(request);
                case AP.CompletionsRequest.Command: return GetCompletions(request);
                case AP.GetModulesRequest.Command: return GetModules(request);
                case AP.GetModuleMembers.Command: return GeModuleMembers(request);
                case AP.SignaturesRequest.Command: return GetSignatures((AP.SignaturesRequest)request);
                case AP.QuickInfoRequest.Command: return GetQuickInfo((AP.QuickInfoRequest)request);
                case AP.AnalyzeExpressionRequest.Command: return AnalyzeExpression((AP.AnalyzeExpressionRequest)request);
                case AP.OutlingRegionsRequest.Command: return GetOutliningRegions((AP.OutlingRegionsRequest)request);
                case AP.NavigationRequest.Command: return GetNavigations((AP.NavigationRequest)request);
                case AP.FileUpdateRequest.Command: return UpdateContent((AP.FileUpdateRequest)request);
                case AP.UnresolvedImportsRequest.Command: return GetUnresolvedImports((AP.UnresolvedImportsRequest)request);
                default:
                    throw new InvalidOperationException("Unknown command");
            }

        }
        private Response GetUnresolvedImports(AP.UnresolvedImportsRequest request) {
            var entry = _projectFiles[request.fileId] as IPythonProjectEntry;
            if (entry == null ||
                string.IsNullOrEmpty(entry.ModuleName) ||
                string.IsNullOrEmpty(entry.FilePath)
            ) {
                return new AP.UnresolvedImportsResponse();
            }

            var analysis = entry.Analysis;
            var analyzer = analysis != null ? analysis.ProjectState : null;
            if (analyzer == null) {
                return new AP.UnresolvedImportsResponse();
            }

            PythonAst ast;
            IAnalysisCookie cookie;
            entry.GetTreeAndCookie(out ast, out cookie);

            var versions = cookie as VersionCookie;
            var imports = new List<AP.BufferUnresolvedImports>();
            if (versions != null) {
                foreach (var version in versions.Versions) {
                    if (version.Value.Ast != null) {
                        var walker = new ImportStatementWalker(
                            version.Value.Ast, 
                            entry, 
                            analyzer
                        );

                        version.Value.Ast.Walk(walker);
                        imports.Add(
                            new AP.BufferUnresolvedImports() {
                                bufferId = version.Key,
                                version = version.Value.Version,
                                unresolved = walker.Imports.ToArray()
                            }
                        );
                    }
                }
            }

            return new AP.UnresolvedImportsResponse() {
                buffers = imports.ToArray()
            };
        }

        class ImportStatementWalker : PythonWalker {
            public readonly List<UnresolvedImport> Imports = new List<UnresolvedImport>();

            readonly IPythonProjectEntry _entry;
            readonly PythonAnalyzer _analyzer;
            private readonly PythonAst _ast;

            public ImportStatementWalker(PythonAst ast, IPythonProjectEntry entry, PythonAnalyzer analyzer) {
                _ast = ast;
                _entry = entry;
                _analyzer = analyzer;
            }

            public override bool Walk(FromImportStatement node) {
                var name = node.Root.MakeString();
                if (!_analyzer.IsModuleResolved(_entry, name, node.ForceAbsolute)) {
                    Imports.Add(MakeUnresolvedImport(name, node.Root));
                }
                return base.Walk(node);
            }

            private UnresolvedImport MakeUnresolvedImport(string name, Node spanNode) {
                var span = spanNode.GetSpan(_ast);
                return new UnresolvedImport() {
                    name = name,
                    startLine = span.Start.Line,
                    startColumn = span.Start.Column,
                    startIndex = span.Start.Index,
                    endLine = span.End.Line,
                    endColumn = span.End.Column,
                    endIndex = span.End.Index
                };
            }

            public override bool Walk(ImportStatement node) {
                foreach (var nameNode in node.Names) {
                    var name = nameNode.MakeString();
                    if (!_analyzer.IsModuleResolved(_entry, name, node.ForceAbsolute)) {
                        Imports.Add(MakeUnresolvedImport(name, nameNode));
                    }
                }
                return base.Walk(node);
            }

            private static bool IsImportError(Expression expr) {
                var name = expr as NameExpression;
                if (name != null) {
                    return name.Name == "Exception" || name.Name == "BaseException" || name.Name == "ImportError";
                }

                var tuple = expr as TupleExpression;
                if (tuple != null) {
                    return tuple.Items.Any(IsImportError);
                }

                return false;
            }

            private static bool ShouldWalkNormally(TryStatement node) {
                if (node.Handlers == null) {
                    return true;
                }

                foreach (var handler in node.Handlers) {
                    if (handler.Test == null || IsImportError(handler.Test)) {
                        return false;
                    }
                }

                return true;
            }

            public override bool Walk(TryStatement node) {
                if (ShouldWalkNormally(node)) {
                    return base.Walk(node);
                }

                // Don't walk 'try' body, but walk everything else
                if (node.Handlers != null) {
                    foreach (var handler in node.Handlers) {
                        handler.Walk(this);
                    }
                }
                if (node.Else != null) {
                    node.Else.Walk(this);
                }
                if (node.Finally != null) {
                    node.Finally.Walk(this);
                }

                return false;
            }
        }

        private Response GetOutliningRegions(AP.OutlingRegionsRequest request) {
            var pyEntry = _projectFiles[request.fileId] as IPythonProjectEntry;
            PythonAst tree;
            IAnalysisCookie cookie;
            pyEntry.GetTreeAndCookie(out tree, out cookie);
            var versions = cookie as VersionCookie;
            List<AP.BufferOutliningTags> buffers = new List<AP.BufferOutliningTags>();
            if (versions != null) {

                foreach (var version in versions.Versions) {
                    if (version.Value.Ast != null) {
                        var walker = new OutliningWalker(version.Value.Ast);

                        version.Value.Ast.Walk(walker);

                        var tags = walker.TagSpans
                            .GroupBy(s => version.Value.Ast.IndexToLocation(s.startIndex).Line)
                            .Select(ss => ss.OrderBy(s => version.Value.Ast.IndexToLocation(s.endIndex).Line - ss.Key).Last())
                            .ToArray();

                        buffers.Add(
                            new AP.BufferOutliningTags() {
                                bufferId = version.Key,
                                version = version.Value.Version,
                                tags = tags
                            }
                        );
                    }
                }
            }

            return new AP.OutliningRegionsResponse() { buffers = buffers.ToArray() };
        }

        private Response GetNavigations(AP.NavigationRequest request) {
            var pyEntry = _projectFiles[request.fileId] as IPythonProjectEntry;

            List<AP.BufferNavigations> buffers = new List<AP.BufferNavigations>();
            IAnalysisCookie cookie;
            PythonAst tree;
            pyEntry.GetTreeAndCookie(out tree, out cookie);

            var versions = cookie as VersionCookie;
            if (versions != null) {
                foreach (var version in versions.Versions) {
                    if (version.Value.Ast == null) {
                        continue;
                    }

                    var navs = new List<AP.Navigation>();

                    var suite = version.Value.Ast.Body as SuiteStatement;
                    if (suite == null) {
                        continue;
                    }

                    foreach (var stmt in suite.Statements) {
                        var classDef = stmt as ClassDefinition;
                        if (classDef != null) {
                            // classes have nested defs
                            var classSuite = classDef.Body as SuiteStatement;
                            var nestedNavs = new List<AP.Navigation>();
                            if (classSuite != null) {
                                foreach (var child in classSuite.Statements) {
                                    if (child is ClassDefinition || child is FunctionDefinition) {
                                        nestedNavs.Add(GetNavigation(child));
                                    }
                                }
                            }

                            navs.Add(new AP.Navigation() {
                                type = "class",
                                name = classDef.Name,
                                startIndex = classDef.StartIndex,
                                endIndex = classDef.EndIndex,
                                children = nestedNavs.ToArray()
                            });

                        } else if (stmt is FunctionDefinition) {
                            navs.Add(GetNavigation(stmt));
                        }
                    }

                    buffers.Add(new AP.BufferNavigations() {
                        bufferId = version.Key,
                        version = version.Value.Version,
                        navigations = navs.ToArray()
                    });
                }
            }

            return new AP.NavigationResponse() { buffers = buffers.ToArray() };
        }

        private static AP.Navigation GetNavigation(Statement stmt) {
            string type, name;
            FunctionDefinition funcDef = stmt as FunctionDefinition;
            if (funcDef != null) {
                name = funcDef.Name;
                type = "function";
                if (funcDef.Decorators != null && funcDef.Decorators.Decorators.Count == 1) {
                    foreach (var decorator in funcDef.Decorators.Decorators) {
                        NameExpression nameExpr = decorator as NameExpression;
                        if (nameExpr != null) {
                            if (nameExpr.Name == "property") {
                                type = "property";
                                break;
                            } else if (nameExpr.Name == "staticmethod") {
                                type = "staticmethod";
                                break;
                            } else if (nameExpr.Name == "classmethod") {
                                type = "classmethod";
                                break;
                            }
                        }
                    }
                }
            } else {
                name = ((ClassDefinition)stmt).Name;
                type = "class";
            }
            return new AP.Navigation() {
                type = type,
                name = name,
                startIndex = stmt.StartIndex,
                endIndex = stmt.EndIndex
            };
        }

        private Response AnalyzeExpression(AP.AnalyzeExpressionRequest request) {
            var pyEntry = _projectFiles[request.fileId] as IPythonProjectEntry;
            AP.Reference[] references;
            string privatePrefix = null;
            string memberName = null;
            if (pyEntry.Analysis != null) {
                var variables = pyEntry.Analysis.GetVariables(
                    request.expr,
                    new SourceLocation(
                        request.index,
                        request.line,
                        request.column
                    )
                );

                var ast = variables.Ast;
                var expr = Statement.GetExpression(ast.Body);

                NameExpression ne = expr as NameExpression;
                MemberExpression me;
                if (ne != null) {
                    memberName = ne.Name;
                } else if ((me = expr as MemberExpression) != null) {
                    memberName = me.Name;
                }

                privatePrefix = variables.Ast.PrivatePrefix;
                references = variables.Select(MakeReference).ToArray();
            } else {
                references = new AP.Reference[0];
            }

            return new AP.AnalyzeExpressionResponse() {
                variables = references,
                privatePrefix = privatePrefix,
                memberName = memberName
            };
        }

        private AP.Reference MakeReference(IAnalysisVariable arg) {
            return new AP.Reference() {
                column = arg.Location.Column,
                line = arg.Location.Line,
                kind = GetVariableType(arg.Type),
                file = GetFile(arg.Location.ProjectEntry)
            };
        }

        private string GetFile(IProjectEntry projectEntry) {
            if (projectEntry != null) {
                return projectEntry.FilePath;
            }
            return null;
        }

        private static string GetVariableType(VariableType type) {
            switch (type) {
                case VariableType.Definition: return "definition";
                case VariableType.Reference: return "AP.Reference";
                case VariableType.Value: return "value";
            }
            return null;
        }

        private Response GetQuickInfo(AP.QuickInfoRequest request) {
            var pyEntry = _projectFiles[request.fileId] as IPythonProjectEntry;
            string text = null;
            if (pyEntry.Analysis != null) {
                var values = pyEntry.Analysis.GetValues(
                    request.expr,
                    new SourceLocation(
                        request.index,
                        request.line,
                        request.column
                    )
                );

                bool first = true;
                var result = new StringBuilder();
                int count = 0;
                List<AnalysisValue> listVars = new List<AnalysisValue>(values);
                HashSet<string> descriptions = new HashSet<string>();
                bool multiline = false;
                foreach (var v in listVars) {
                    string description = null;
                    if (listVars.Count == 1) {
                        if (!String.IsNullOrWhiteSpace(v.Description)) {
                            description = v.Description;
                        }
                    } else {
                        if (!String.IsNullOrWhiteSpace(v.ShortDescription)) {
                            description = v.ShortDescription;
                        }
                    }

                    description = LimitLines(description);

                    if (description != null && descriptions.Add(description)) {
                        if (first) {
                            first = false;
                        } else {
                            if (result.Length == 0 || result[result.Length - 1] != '\n') {
                                result.Append(", ");
                            } else {
                                multiline = true;
                            }
                        }
                        result.Append(description);
                        count++;
                    }
                }

                string expr = request.expr;
                if (expr.Length > 4096) {
                    expr = expr.Substring(0, 4093) + "...";
                }
                if (multiline) {
                    result.Insert(0, expr + ": " + Environment.NewLine);
                } else if (result.Length > 0) {
                    result.Insert(0, expr + ": ");
                } else {
                    result.Append(expr);
                    result.Append(": ");
                    result.Append("<unknown type>");
                }

                text = result.ToString();
            }

            return new AP.QuickInfoResponse() {
                text = text
            };
        }


        internal static string LimitLines(
            string str,
            int maxLines = 30,
            int charsPerLine = 200,
            bool ellipsisAtEnd = true,
            bool stopAtFirstBlankLine = false
        ) {
            if (string.IsNullOrEmpty(str)) {
                return str;
            }

            int lineCount = 0;
            var prettyPrinted = new StringBuilder();
            bool wasEmpty = true;

            using (var reader = new StringReader(str)) {
                for (var line = reader.ReadLine(); line != null && lineCount < maxLines; line = reader.ReadLine()) {
                    if (string.IsNullOrWhiteSpace(line)) {
                        if (wasEmpty) {
                            continue;
                        }
                        wasEmpty = true;
                        if (stopAtFirstBlankLine) {
                            lineCount = maxLines;
                            break;
                        }
                        lineCount += 1;
                        prettyPrinted.AppendLine();
                    } else {
                        wasEmpty = false;
                        lineCount += (line.Length / charsPerLine) + 1;
                        prettyPrinted.AppendLine(line);
                    }
                }
            }
            if (ellipsisAtEnd && lineCount >= maxLines) {
                prettyPrinted.AppendLine("...");
            }
            return prettyPrinted.ToString().Trim();
        }

        private Response GetSignatures(AP.SignaturesRequest request) {
            var pyEntry = _projectFiles[request.fileId] as IPythonProjectEntry;
            IEnumerable<IOverloadResult> sigs;
            if (pyEntry.Analysis != null) {
                sigs = pyEntry.Analysis.GetSignaturesByIndex(
                    request.text,
                    request.location
                );
            } else {
                sigs = Enumerable.Empty<IOverloadResult>();
            }

            return new AP.SignaturesResponse() {
                sigs = ToSignatures(sigs)
            };

        }

        private Response GetTopLevelCompletions(Request request) {
            var topLevelCompletions = (AP.TopLevelCompletionsRequest)request;

            var pyEntry = _projectFiles[topLevelCompletions.fileId] as IPythonProjectEntry;
            IEnumerable<MemberResult> members;
            if (pyEntry.Analysis != null) {

                members = pyEntry.Analysis.GetAllAvailableMembers(
                    new SourceLocation(topLevelCompletions.location, 1, topLevelCompletions.column),
                    topLevelCompletions.options
                );
            } else {
                members = Enumerable.Empty<MemberResult>();
            }

            return new AP.CompletionsResponse() {
                completions = ToCompletions(members.ToArray())
            };
        }

        private Response GeModuleMembers(Request request) {
            var getModuleMembers = (AP.GetModuleMembers)request;

            return new AP.CompletionsResponse() {
                completions = ToCompletions(_pyAnalyzer.GetModuleMembers(
                    null, // TODO: ModuleContext
                    getModuleMembers.package,
                    getModuleMembers.includeMembers
                ))
            };
        }

        private Response GetModules(Request request) {
            var getModules = (AP.GetModulesRequest)request;

            return new AP.CompletionsResponse() {
                completions = ToCompletions(_pyAnalyzer.GetModules(
                    getModules.topLevelOnly
                ))
            };
        }

        private Response GetCompletions(Request request) {
            var completions = (AP.CompletionsRequest)request;

            var pyEntry = _projectFiles[completions.fileId] as IPythonProjectEntry;
            IEnumerable<MemberResult> members;
            if (pyEntry.Analysis != null) {

                members = pyEntry.Analysis.GetMembersByIndex(
                    completions.text,
                    completions.location,
                    completions.options
                );
            } else {
                members = Enumerable.Empty<MemberResult>();
            }

            return new AP.CompletionsResponse() {
                completions = ToCompletions(members.ToArray())
            };
        }

        private AP.Signature[] ToSignatures(IEnumerable<IOverloadResult> sigs) {
            return sigs.Select(
                sig => new AP.Signature() {
                    name = sig.Name,
                    doc = sig.Documentation,
                    parameters = sig.Parameters.Select(
                        param => new AP.Parameter() {
                            name = param.Name,
                            defaultValue = param.DefaultValue,
                            optional = param.IsOptional,
                            doc = param.Documentation,
                            type = param.Type,
                            variables = param.Variables != null ? param.Variables.Select(MakeReference).ToArray() : null
                        }
                    ).ToArray()
                }
            ).ToArray();
        }

        private AP.Completion[] ToCompletions(MemberResult[] memberResult) {
            AP.Completion[] res = new AP.Completion[memberResult.Length];
            for (int i = 0; i < memberResult.Length; i++) {
                res[i] = new AP.Completion() {
                    name = memberResult[i].Name,
                    completion = memberResult[i].Completion,
                    memberType = memberResult[i].MemberType
                };
            }
            return res;
        }

        private Response AnalyzeFile(AP.AddFileRequest request) {
            var entry = AnalyzeFile(request.path, request.addingFromDir);

            if (entry != null) {
                return new AP.AddFileResponse() {
                    fileId = ProjectEntryMap.GetId(entry)
                };
            }

            throw new InvalidOperationException("Failed to add item");
        }

        private Response UnloadFile(AP.UnloadFileRequest command) {
            var entry = _projectFiles[command.fileId];
            if (entry == null) {
                throw new InvalidOperationException("Unknown project entry");
            }

            UnloadFile(entry);
            return new Response();
        }

        abstract class CodeInfo {
            public readonly int Version;
            public object Code; // Stream or TextReader

            public CodeInfo(int version) {
                Version = version;
            }

            public abstract Parser CreateParser(PythonLanguageVersion version, ParserOptions options);

            public abstract TextReader GetReader();
        }

        class StreamCodeInfo : CodeInfo {
            private readonly Stream _stream;

            public StreamCodeInfo(int version, Stream stream) : base(version) {
                _stream = stream;
            }

            public override Parser CreateParser(PythonLanguageVersion version, ParserOptions options) {
                return Parser.CreateParser(_stream, version, options);
            }

            public override TextReader GetReader() {
                return new StreamReader(_stream);
            }
        }

        class TextCodeInfo : CodeInfo {
            private readonly TextReader _text;
            public TextCodeInfo(int version, TextReader text) : base(version) {
                _text = text;
            }

            public override Parser CreateParser(PythonLanguageVersion version, ParserOptions options) {
                return Parser.CreateParser(_text, version, options);
            }

            public override TextReader GetReader() {
                return _text;
            }
        }

        private Response UpdateContent(FileUpdateRequest request) {
            var entry = _projectFiles[request.fileId];

            SortedDictionary<int, CodeInfo> codeByBuffer = new SortedDictionary<int, CodeInfo>();
#if DEBUG
            Dictionary<int, string> newCode = new Dictionary<int, string>();
#endif
            foreach (var update in request.updates) {
                switch (update.kind) {
                    case AP.FileUpdateKind.changes:
                        if (entry != null) {
                            var curCode = entry.GetCurrentCode(update.bufferId);
                            if (curCode == null) {
                                entry.SetCurrentCode(curCode = new StringBuilder(), update.bufferId);
                            }

                            foreach (var change in update.changes) {
                                curCode.Remove(change.start, change.length);
                                curCode.Insert(change.start, change.newText);
                            }

                            var newCodeStr = curCode.ToString();
                            codeByBuffer[update.bufferId] = new TextCodeInfo(
                                update.version,
                                new StringReader(newCodeStr)
                            );
#if DEBUG
                            newCode[update.bufferId] = newCodeStr;
#endif
                        }
                        break;
                    case AP.FileUpdateKind.reset:
                        entry.SetCurrentCode(new StringBuilder(update.content), update.bufferId);
                        codeByBuffer[update.bufferId] = new TextCodeInfo(
                            update.version,
                            new StringReader(update.content)
                        );
#if DEBUG
                        newCode[update.bufferId] = update.content;
#endif                        
                        break;
                    default:
                        throw new InvalidOperationException("unsupported update kind");
                }
            }

            EnqueWorker(() => ParseFile(entry, codeByBuffer));

            return new AP.FileUpdateResponse() {
#if DEBUG
                newCode = newCode
#endif
            };
        }

        /// <summary>
        /// Gets a CompletionList providing a list of possible members the user can dot through.
        /// </summary>
        internal AP.CompletionsResponse GetCompletions(AP.CompletionsRequest request) {
            var file = _projectFiles[request.fileId];
            if (file == null) {
                throw new InvalidOperationException("Unknown project entry");
            }

            return //TrySpecialCompletions(serviceProvider, snapshot, span, point, options) ??
                GetNormalCompletions(file, request);
            //return 
            //         GetNormalCompletionContext(request);
        }

        internal Task ProcessMessages() {
            return _connection.ProcessMessages();
        }

        public AP.OptionsChangedEvent Options {
            get {
                return _options;
            }
        }

        private void AnalysisQueue_AnalysisStarted(object sender, EventArgs e) {
            var evt = AnalysisStarted;
            if (evt != null) {
                evt(this, e);
            }
        }

        internal static string GetZipFileName(IProjectEntry entry) {
            object result;
            entry.Properties.TryGetValue(_zipFileName, out result);
            return (string)result;
        }

        private static void SetZipFileName(IProjectEntry entry, string value) {
            entry.Properties[_zipFileName] = value;
        }

        internal static string GetPathInZipFile(IProjectEntry entry) {
            object result;
            entry.Properties.TryGetValue(_pathInZipFile, out result);
            return (string)result;
        }

        private static void SetPathInZipFile(IProjectEntry entry, string value) {
            entry.Properties[_pathInZipFile] = value;
        }

        private async void OnModulesChanged(object sender, EventArgs args) {
            Debug.Assert(_pyAnalyzer != null, "Should not have null _pyAnalyzer here");
            if (_pyAnalyzer == null) {
                return;
            }

            await _pyAnalyzer.ReloadModulesAsync();

            // re-analyze all of the modules when we get a new set of modules loaded...
            foreach (var nameAndEntry in _projectFiles) {
                EnqueueFile(nameAndEntry.Value, nameAndEntry.Key);
            }
        }


#if PORT
        private IProjectEntry CreateProjectEntry(ITextBuffer buffer, IAnalysisCookie analysisCookie) {
            if (_pyAnalyzer == null) {
                // We aren't able to analyze code, so don't create an entry.
                return null;
            }

            var replEval = buffer.GetReplEvaluator();
            if (replEval != null) {
                // We have a repl window, create an untracked module.
                return _pyAnalyzer.AddModule(null, null, analysisCookie);
            }

            string path = buffer.GetFilePath();
            if (path == null) {
                return null;
            }

            IProjectEntry entry;
            if (!_projectFiles.TryGetValue(path, out entry)) {
                if (buffer.ContentType.IsOfType(PythonCoreConstants.ContentType)) {
                    string modName;
                    try {
                        modName = ModulePath.FromFullPath(path).ModuleName;
                    } catch (ArgumentException) {
                        modName = null;
                    }

                    IPythonProjectEntry[] reanalyzeEntries = null;
                    if (!string.IsNullOrEmpty(modName)) {
                        reanalyzeEntries = Project.GetEntriesThatImportModule(modName, true).ToArray();
                    }

                    entry = _pyAnalyzer.AddModule(
                        modName,
                        buffer.GetFilePath(),
                        analysisCookie
                    );

                    if (reanalyzeEntries != null) {
                        foreach (var entryRef in reanalyzeEntries) {
                            _analysisQueue.Enqueue(entryRef, AnalysisPriority.Low);
                        }
                    }
                } else if (buffer.ContentType.IsOfType("XAML")) {
                    entry = _pyAnalyzer.AddXamlFile(buffer.GetFilePath());
                } else {
                    return null;
                }

                _projectFiles[path] = entry;

                if (ImplicitProject && ShouldAnalyzePath(path)) { // don't analyze std lib
                    QueueDirectoryAnalysis(path);
                }
            }

            return entry;
        }
#endif
        private void QueueDirectoryAnalysis(string path) {
            ThreadPool.QueueUserWorkItem(x => {
                AnalyzeDirectory(PathUtils.NormalizeDirectoryPath(Path.GetDirectoryName(path)));
            });
        }

        private bool ShouldAnalyzePath(string path) {
            foreach (var fact in _allFactories) {
                if (PathUtils.IsValidPath(fact.Configuration.InterpreterPath) &&
                    PathUtils.IsSubpathOf(Path.GetDirectoryName(fact.Configuration.InterpreterPath), path)) {
                    return false;
                }
            }
            return true;
        }

        internal IProjectEntry AnalyzeFile(string path, string addingFromDirectory = null) {
            if (_pyAnalyzer == null) {
                // We aren't able to analyze code, so don't create an entry.
                return null;
            }

            IProjectEntry item;
            if (!_projectFiles.TryGetValue(path, out item)) {
                if (ModulePath.IsPythonSourceFile(path)) {
                    string modName;
                    try {
                        modName = ModulePath.FromFullPath(path, addingFromDirectory).ModuleName;
                    } catch (ArgumentException) {
                        // File is not a valid module, but we can still add an
                        // entry for it.
                        modName = null;
                    }

                    IPythonProjectEntry[] reanalyzeEntries = null;
                    if (!string.IsNullOrEmpty(modName)) {
                        reanalyzeEntries = Project.GetEntriesThatImportModule(modName, true).ToArray();
                    }

                    var pyEntry = _pyAnalyzer.AddModule(
                        modName,
                        path,
                        null
                    );
                    pyEntry.OnNewAnalysis += OnNewAnalysis;

                    pyEntry.BeginParsingTree();

                    if (reanalyzeEntries != null) {
                        foreach (var entryRef in reanalyzeEntries) {
                            _analysisQueue.Enqueue(entryRef, AnalysisPriority.Low);
                        }
                    }

                    item = pyEntry;
                } else if (path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)) {
                    item = _pyAnalyzer.AddXamlFile(path, null);
                }

                if (item != null) {
                    _projectFiles.Add(path, item);
                    EnqueueFile(item, path);
                }
            } else if (addingFromDirectory != null) {
                var module = item as IPythonProjectEntry;
                if (module != null && ModulePath.IsPythonSourceFile(path)) {
                    string modName = null;
                    try {
                        modName = ModulePath.FromFullPath(path, addingFromDirectory).ModuleName;
                    } catch (ArgumentException) {
                        // Module does not have a valid name, so we can't make
                        // an alias for it.
                    }

                    if (modName != null && module.ModuleName != modName) {
                        _pyAnalyzer.AddModuleAlias(module.ModuleName, modName);

                        var reanalyzeEntries = Project.GetEntriesThatImportModule(modName, true).ToArray();
                        foreach (var entryRef in reanalyzeEntries) {
                            _analysisQueue.Enqueue(entryRef, AnalysisPriority.Low);
                        }
                    }
                }
            }

            return item;
        }

        private void OnNewAnalysis(object sender, EventArgs e) {
            var projEntry = sender as IProjectEntry;
            if (projEntry != null) {
                var fileId = ProjectEntryMap.GetId(projEntry);

                _connection.SendEventAsync(new AnalysisCompleteEvent() { fileId = fileId });
            }
        }

        internal IEnumerable<KeyValuePair<string, IProjectEntry>> LoadedFiles {
            get {
                return _projectFiles;
            }
        }

        internal IProjectEntry GetEntryFromFile(string path) {
            IProjectEntry res;
            if (_projectFiles.TryGetValue(path, out res)) {
                return res;
            }
            return null;
        }

#if PORT
        internal static MissingImportAnalysis GetMissingImports(IServiceProvider serviceProvider, ITextSnapshot snapshot, ITrackingSpan span) {
            ReverseExpressionParser parser = new ReverseExpressionParser(snapshot, snapshot.TextBuffer, span);
            var loc = span.GetSpan(snapshot.Version);
            int dummy;
            SnapshotPoint? dummyPoint;
            string lastKeywordArg;
            bool isParameterName;
            var exprRange = parser.GetExpressionRange(0, out dummy, out dummyPoint, out lastKeywordArg, out isParameterName);
            if (exprRange == null || isParameterName) {
                return MissingImportAnalysis.Empty;
            }

            IPythonProjectEntry entry;
            ModuleAnalysis analysis;
            if (!snapshot.TextBuffer.TryGetPythonProjectEntry(out entry) ||
                entry == null ||
                (analysis = entry.Analysis) == null) {
                return MissingImportAnalysis.Empty;
            }

            var text = exprRange.Value.GetText();
            if (string.IsNullOrEmpty(text)) {
                return MissingImportAnalysis.Empty;
            }

            var analyzer = analysis.ProjectState;
            var index = (parser.GetStatementRange() ?? span.GetSpan(snapshot)).Start.Position;

            var location = TranslateIndex(
                index,
                snapshot,
                analysis
            );
            var nameExpr = GetFirstNameExpression(analysis.GetAstFromText(text, location).Body);

            if (nameExpr != null && !IsImplicitlyDefinedName(nameExpr)) {
                var name = nameExpr.Name;
                lock (snapshot.TextBuffer.GetAnalyzer(serviceProvider)) {
                    var hasVariables = analysis.GetVariables(name, location).Any(IsDefinition);
                    var hasValues = analysis.GetValues(name, location).Any();

                    // if we have type information or an assignment to the variable we won't offer 
                    // an import smart tag.
                    if (!hasValues && !hasVariables) {
                        var applicableSpan = parser.Snapshot.CreateTrackingSpan(
                            exprRange.Value.Span,
                            SpanTrackingMode.EdgeExclusive
                        );
                        return new MissingImportAnalysis(name, analysis.ProjectState, applicableSpan);
                    }
                }
            }

            // if we have type information don't offer to add imports
            return MissingImportAnalysis.Empty;
        }
#endif
        private static NameExpression GetFirstNameExpression(Statement stmt) {
            return GetFirstNameExpression(Statement.GetExpression(stmt));
        }

        private static NameExpression GetFirstNameExpression(Expression expr) {
            NameExpression nameExpr;
            CallExpression callExpr;
            MemberExpression membExpr;

            if ((nameExpr = expr as NameExpression) != null) {
                return nameExpr;
            }
            if ((callExpr = expr as CallExpression) != null) {
                return GetFirstNameExpression(callExpr.Target);
            }
            if ((membExpr = expr as MemberExpression) != null) {
                return GetFirstNameExpression(membExpr.Target);
            }

            return null;
        }

        private static bool IsDefinition(IAnalysisVariable variable) {
            return variable.Type == VariableType.Definition;
        }

        private static bool IsImplicitlyDefinedName(NameExpression nameExpr) {
            return nameExpr.Name == "__all__" ||
                nameExpr.Name == "__file__" ||
                nameExpr.Name == "__doc__" ||
                nameExpr.Name == "__name__";
        }

        internal bool IsAnalyzing {
            get {
                return IsParsing || _analysisQueue.IsAnalyzing;
            }
        }

        internal event EventHandler AnalysisStarted;

        internal void WaitForCompleteAnalysis(Func<int, bool> itemsLeftUpdated) {
            if (IsAnalyzing) {
                while (IsAnalyzing) {
                    QueueActivityEvent.WaitOne(100);

                    int itemsLeft = ParsePending + _analysisQueue.AnalysisPending;

                    if (!itemsLeftUpdated(itemsLeft)) {
                        break;
                    }
                }
            } else {
                itemsLeftUpdated(0);
            }
        }

        internal AutoResetEvent QueueActivityEvent {
            get {
                return _queueActivityEvent;
            }
        }

        /// <summary>
        /// True if the project is an implicit project and it should model files on disk in addition
        /// to files which are explicitly added.
        /// </summary>
        internal bool ImplicitProject {
            get {
                return _options.implicitProject;
            }
        }

        internal IPythonInterpreterFactory InterpreterFactory {
            get {
                return _interpreterFactory;
            }
        }

        internal IPythonInterpreter Interpreter {
            get {
                return _pyAnalyzer != null ? _pyAnalyzer.Interpreter : null;
            }
        }

        public PythonAnalyzer Project {
            get {
                return _pyAnalyzer;
            }
        }

        class ParseResult {
            public readonly PythonAst Ast; 
            public readonly CollectingErrorSink Errors;
            public readonly List<AP.TaskItem> Tasks;
            public readonly int Version;

            public ParseResult(PythonAst ast, CollectingErrorSink errors, List<AP.TaskItem> tasks, int version) {
                Ast = ast;
                Errors = errors;
                Tasks = tasks;
                Version = version;
            }
        }


        private void ParseFile(IProjectEntry entry, IDictionary<int, CodeInfo> buffers) {
            IPythonProjectEntry pyEntry;
            IExternalProjectEntry externalEntry;

            string zipFileName = GetZipFileName(entry);
            string pathInZipFile = GetPathInZipFile(entry);

            SortedDictionary<int, ParseResult> parseResults = new SortedDictionary<int, ParseResult>();

            if ((pyEntry = entry as IPythonProjectEntry) != null) {
                foreach (var buffer in buffers) {
                    var errorSink = new CollectingErrorSink();
                    var tasks = new List<AP.TaskItem>();
                    ParserOptions options = MakeParserOptions(errorSink, tasks);

                    using (var parser = buffer.Value.CreateParser(Project.LanguageVersion, options)) {
                        var ast = ParseOneFile(parser);
                        parseResults[buffer.Key] = new ParseResult(
                            ast, 
                            errorSink, 
                            tasks, 
                            buffer.Value.Version
                        );
                    }
                }

                // Save the single or combined tree into the project entry
                UpdateAnalysisTree(pyEntry, parseResults);

                // update squiggles for the buffer. snapshot may be null if we
                // are analyzing a file that is not open
                SendParseComplete(pyEntry, parseResults);

                // enqueue analysis of the file
                if (parseResults.Where(x => x.Value.Ast != null).Any()) {
                    _analysisQueue.Enqueue(pyEntry, AnalysisPriority.Normal);
                }
            } else if ((externalEntry = entry as IExternalProjectEntry) != null) {
                foreach(var keyValue in buffers) { 
                    externalEntry.ParseContent(keyValue.Value.GetReader(), null);
                    _analysisQueue.Enqueue(entry, AnalysisPriority.Normal);
                }
            }
        }

        private static void UpdateAnalysisTree(IPythonProjectEntry pyEntry, SortedDictionary<int, ParseResult> parseResults) {
            IAnalysisCookie cookie = new VersionCookie(
                parseResults.ToDictionary(
                    x => x.Key,
                    x => new VersionInfo(x.Value.Version, x.Value.Ast)
                )
            );

            var asts = parseResults.Where(x => x.Value.Ast != null).Select(x => x.Value.Ast).ToArray();
            PythonAst finalAst;
            if (asts.Length == 1) {
                finalAst = asts[0];
            } else if (asts.Length > 0) {
                // multiple ASTs, merge them together
                finalAst = new PythonAst(
                    new SuiteStatement(
                        asts.Select(ast => ast.Body).ToArray()
                    ),
                    new int[0],
                    asts[0].LanguageVersion
                );
            } else {
                // we failed to get any sort of AST out, so we can't analyze...
                // But we need to balance the UpdateTree call, so just fetch the
                // last valid ast and cookie.
                pyEntry.GetTreeAndCookie(out finalAst, out cookie);
            }

            pyEntry.UpdateTree(finalAst, cookie);
        }

        private async void SendParseComplete(IPythonProjectEntry entry, SortedDictionary<int, ParseResult> parseResults) {
            await _connection.SendEventAsync(
                new AP.FileParsedEvent() {
                    fileId = ProjectEntryMap.GetId(entry),
                    buffers = parseResults.Select(
                        x => new BufferParseInfo() {
                            bufferId = x.Key,
                            version = x.Value.Version,
                            errors = x.Value.Errors.Errors.Select(MakeError).ToArray(),
                            warnings = x.Value.Errors.Warnings.Select(MakeError).ToArray(),
                            hasErrors = x.Value.Errors.Errors.Any(),
                            tasks = x.Value.Tasks.ToArray()
                        }
                    ).ToArray()
                }
            );
        }

        private static AP.Error MakeError(ErrorResult error) {
            return new AP.Error() {
                message = error.Message,
                startLine = error.Span.Start.Line,
                startColumn = error.Span.Start.Column,
                startIndex = error.Span.Start.Index,
                endLine = error.Span.End.Line,
                endColumn = error.Span.End.Column,
                length = error.Span.Length
            };
        }

        private ParserOptions MakeParserOptions(CollectingErrorSink errorSink, List<AP.TaskItem> tasks) {
            var options = new ParserOptions {
                ErrorSink = errorSink,
                IndentationInconsistencySeverity = _options.indentation_inconsistency_severity,
                BindReferences = true
            };
            options.ProcessComment += (sender, e) => ProcessComment(tasks, e.Span, e.Text);
            return options;
        }

        private static PythonAst ParseOneFile(Parser parser) {
            if (parser != null) {
                try {
                    return parser.ParseFile();
                } catch (BadSourceException) {
                } catch (Exception e) {
                    if (e.IsCriticalException()) {
                        throw;
                    }
                    Debug.Assert(false, String.Format("Failure in Python parser: {0}", e.ToString()));
                }

            }
            return null;
        }

        // Tokenizer callback. Extracts comment tasks (like "TODO" or "HACK") from comments.
        private void ProcessComment(List<AP.TaskItem> commentTasks, SourceSpan span, string text) {
            if (text.Length > 0) {
                var tokens = _commentPriorityMap;
                if (tokens != null) {
                    foreach (var kv in tokens) {
                        if (text.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0) {
                            commentTasks.Add(
                                new AP.TaskItem() {
                                    message = text.Substring(1).Trim(),
                                    startLine = span.Start.Line,
                                    startColumn = span.Start.Column,
                                    startIndex = span.Start.Index,
                                    endLine = span.End.Line,
                                    endColumn = span.End.Column,
                                    length = span.Length,
                                    priority = kv.Value,
                                    category = AP.TaskCategory.comments,
                                    squiggle = false
                                }
                            );
                        }
                    }
                }
            }
        }

        #region Implementation Details

        private static Stopwatch _stopwatch = MakeStopWatch();

        internal static Stopwatch Stopwatch {
            get {
                return _stopwatch;
            }
        }
#if PORT
        private static SignatureAnalysis TryGetLiveSignatures(ITextSnapshot snapshot, int paramIndex, string text, ITrackingSpan applicableSpan, string lastKeywordArg) {
            IInteractiveEvaluator eval;
            IPythonReplIntellisense dlrEval;
            if (snapshot.TextBuffer.Properties.TryGetProperty<IInteractiveEvaluator>(typeof(IInteractiveEvaluator), out eval) &&
                (dlrEval = eval as IPythonReplIntellisense) != null) {
                if (text.EndsWith("(")) {
                    text = text.Substring(0, text.Length - 1);
                }
                var liveSigs = dlrEval.GetSignatureDocumentation(text);

                if (liveSigs != null && liveSigs.Length > 0) {
                    return new SignatureAnalysis(text, paramIndex, GetLiveSignatures(text, liveSigs, paramIndex, applicableSpan, lastKeywordArg), lastKeywordArg);
                }
            }
            return null;
        }

        private static ISignature[] GetLiveSignatures(string text, ICollection<OverloadDoc> liveSigs, int paramIndex, ITrackingSpan span, string lastKeywordArg) {
            ISignature[] res = new ISignature[liveSigs.Count];
            int i = 0;
            foreach (var sig in liveSigs) {
                res[i++] = new PythonSignature(
                    span,
                    new LiveOverloadResult(text, sig.Documentation, sig.Parameters),
                    paramIndex,
                    lastKeywordArg
                );
            }
            return res;
        }

        class LiveOverloadResult : IOverloadResult {
            private readonly string _name, _doc;
            private readonly ParameterResult[] _parameters;

            public LiveOverloadResult(string name, string documentation, ParameterResult[] parameters) {
                _name = name;
                _doc = documentation;
                _parameters = parameters;
            }

        #region IOverloadResult Members

            public string Name {
                get { return _name; }
            }

            public string Documentation {
                get { return _doc; }
            }

            public ParameterResult[] Parameters {
                get { return _parameters; }
            }

        #endregion
        }

        internal bool ShouldEvaluateForCompletion(string source) {
            switch (_pyService.GetInteractiveOptions(_interpreterFactory).ReplIntellisenseMode) {
                case ReplIntellisenseMode.AlwaysEvaluate: return true;
                case ReplIntellisenseMode.NeverEvaluate: return false;
                case ReplIntellisenseMode.DontEvaluateCalls:
                    var parser = Parser.CreateParser(new StringReader(source), _interpreterFactory.GetLanguageVersion());

                    var stmt = parser.ParseSingleStatement();
                    var exprWalker = new ExprWalker();

                    stmt.Walk(exprWalker);
                    return exprWalker.ShouldExecute;
                default: throw new InvalidOperationException();
            }
        }

        class ExprWalker : PythonWalker {
            public bool ShouldExecute = true;

            public override bool Walk(CallExpression node) {
                ShouldExecute = false;
                return base.Walk(node);
            }
        }

        private static CompletionAnalysis TrySpecialCompletions(AP.CompletionsRequest request) {
            var snapSpan = span.GetSpan(snapshot);
            var buffer = snapshot.TextBuffer;
            var classifier = buffer.GetPythonClassifier();
            if (classifier == null) {
                return null;
            }

            var parser = new ReverseExpressionParser(snapshot, buffer, span);
            var statementRange = parser.GetStatementRange();
            if (!statementRange.HasValue) {
                statementRange = snapSpan.Start.GetContainingLine().Extent;
            }
            if (snapSpan.Start < statementRange.Value.Start) {
                return null;
            }

            var tokens = classifier.GetClassificationSpans(new SnapshotSpan(statementRange.Value.Start, snapSpan.Start));
            if (tokens.Count > 0) {
                // Check for context-sensitive intellisense
                var lastClass = tokens[tokens.Count - 1];

                if (lastClass.ClassificationType == classifier.Provider.Comment) {
                    // No completions in comments
                    return CompletionAnalysis.EmptyCompletionContext;
                } else if (lastClass.ClassificationType == classifier.Provider.StringLiteral) {
                    // String completion
                    if (lastClass.Span.Start.GetContainingLine().LineNumber == lastClass.Span.End.GetContainingLine().LineNumber) {
                        return new StringLiteralCompletionList(span, buffer, options);
                    } else {
                        // multi-line string, no string completions.
                        return CompletionAnalysis.EmptyCompletionContext;
                    }
                } else if (lastClass.ClassificationType == classifier.Provider.Operator &&
                    lastClass.Span.GetText() == "@") {

                    if (tokens.Count == 1) {
                        return new DecoratorCompletionAnalysis(span, buffer, options);
                    }
                    // TODO: Handle completions automatically popping up
                    // after '@' when it is used as a binary operator.
                } else if (CompletionAnalysis.IsKeyword(lastClass, "def")) {
                    return new OverrideCompletionAnalysis(span, buffer, options);
                }

                // Import completions
                var first = tokens[0];
                if (CompletionAnalysis.IsKeyword(first, "import")) {
                    return ImportCompletionAnalysis.Make(tokens, span, buffer, options);
                } else if (CompletionAnalysis.IsKeyword(first, "from")) {
                    return FromImportCompletionAnalysis.Make(tokens, span, buffer, options);
                } else if (CompletionAnalysis.IsKeyword(first, "raise") || CompletionAnalysis.IsKeyword(first, "except")) {
                    if (tokens.Count == 1 ||
                        lastClass.ClassificationType.IsOfType(PythonPredefinedClassificationTypeNames.Comma) ||
                        (lastClass.IsOpenGrouping() && tokens.Count < 3)) {
                        return new ExceptionCompletionAnalysis(span, buffer, options);
                    }
                }
                return null;
            } else if ((tokens = classifier.GetClassificationSpans(snapSpan.Start.GetContainingLine().ExtentIncludingLineBreak)).Count > 0 &&
               tokens[0].ClassificationType == classifier.Provider.StringLiteral) {
                // multi-line string, no string completions.
                return CompletionAnalysis.EmptyCompletionContext;
            } else if (snapshot.IsReplBufferWithCommand()) {
                return CompletionAnalysis.EmptyCompletionContext;
            }

            return null;
        }
#endif
        private AP.CompletionsResponse GetNormalCompletions(IProjectEntry projectEntry, AP.CompletionsRequest request /*IServiceProvider serviceProvider, ITextSnapshot snapshot, ITrackingSpan applicableSpan, ITrackingPoint point, CompletionOptions options*/) {
            var code = projectEntry.GetCurrentCode();

            if (IsSpaceCompletion(code, request.location) && !request.forceCompletions) {
                return new AP.CompletionsResponse() {
                    completions = new AP.Completion[0]
                };
            }

            var analysis = ((IPythonProjectEntry)projectEntry).Analysis;
            var members = analysis.GetMembers(
                request.text,
                new SourceLocation(
                    request.location,
                    1,
                    request.column
                ),
                request.options
            );

            return new AP.CompletionsResponse() {
                completions = ToCompletions(members.ToArray())
            };
        }

        private bool IsSpaceCompletion(StringBuilder text, int location) {
            if (location > 0 && location < text.Length - 1) {
                return text[location - 1] == ' ';
            }
            return false;
        }

        private static Stopwatch MakeStopWatch() {
            var res = new Stopwatch();
            res.Start();
            return res;
        }

        /// <summary>
        /// Analyzes a complete directory including all of the contained files and packages.
        /// </summary>
        /// <param name="dir">Directory to analyze.</param>
        /// <param name="onFileAnalyzed">If specified, this callback is invoked for every <see cref="IProjectEntry"/>
        /// that is analyzed while analyzing this directory.</param>
        /// <remarks>The callback may be invoked on a thread different from the one that this function was originally invoked on.</remarks>
        public void AnalyzeDirectory(string dir, Action<IProjectEntry> onFileAnalyzed = null) {
            _analysisQueue.Enqueue(new AddDirectoryAnalysis(dir, onFileAnalyzed, this), AnalysisPriority.High);
        }

        class AddDirectoryAnalysis : IAnalyzable {
            private readonly string _dir;
            private readonly Action<IProjectEntry> _onFileAnalyzed;
            private readonly OutOfProcProjectAnalyzer _analyzer;

            public AddDirectoryAnalysis(string dir, Action<IProjectEntry> onFileAnalyzed, OutOfProcProjectAnalyzer analyzer) {
                _dir = dir;
                _onFileAnalyzed = onFileAnalyzed;
                _analyzer = analyzer;
            }

            #region IAnalyzable Members

            public void Analyze(CancellationToken cancel) {
                if (cancel.IsCancellationRequested) {
                    return;
                }

                AnalyzeDirectoryWorker(_dir, true, _onFileAnalyzed, cancel);
            }

            #endregion

            private void AnalyzeDirectoryWorker(string dir, bool addDir, Action<IProjectEntry> onFileAnalyzed, CancellationToken cancel) {
                if (_analyzer._pyAnalyzer == null) {
                    // We aren't able to analyze code.
                    return;
                }

                if (string.IsNullOrEmpty(dir)) {
                    Debug.Assert(false, "Unexpected empty dir");
                    return;
                }

                if (addDir) {
                    _analyzer._pyAnalyzer.AddAnalysisDirectory(dir);
                }

                try {
                    var filenames = Directory.GetFiles(dir, "*.py").Concat(Directory.GetFiles(dir, "*.pyw"));
                    foreach (string filename in filenames) {
                        if (cancel.IsCancellationRequested) {
                            break;
                        }
                        IProjectEntry entry = _analyzer.AnalyzeFile(filename, _dir);
                        if (onFileAnalyzed != null) {
                            onFileAnalyzed(entry);
                        }
                    }
                } catch (IOException) {
                    // We want to handle DirectoryNotFound, DriveNotFound, PathTooLong
                } catch (UnauthorizedAccessException) {
                }

                try {
                    foreach (string innerDir in Directory.GetDirectories(dir)) {
                        if (cancel.IsCancellationRequested) {
                            break;
                        }
                        if (File.Exists(PathUtils.GetAbsoluteFilePath(innerDir, "__init__.py"))) {
                            AnalyzeDirectoryWorker(innerDir, false, onFileAnalyzed, cancel);
                        }
                    }
                } catch (IOException) {
                    // We want to handle DirectoryNotFound, DriveNotFound, PathTooLong
                } catch (UnauthorizedAccessException) {
                }
            }
        }

        /// <summary>
        /// Analyzes a .zip file including all of the contained files and packages.
        /// </summary>
        /// <param name="dir">.zip file to analyze.</param>
        /// <param name="onFileAnalyzed">If specified, this callback is invoked for every <see cref="IProjectEntry"/>
        /// that is analyzed while analyzing this directory.</param>
        /// <remarks>The callback may be invoked on a thread different from the one that this function was originally invoked on.</remarks>
        public void AnalyzeZipArchive(string zipFileName, Action<IProjectEntry> onFileAnalyzed = null) {
            _analysisQueue.Enqueue(new AddZipArchiveAnalysis(zipFileName, onFileAnalyzed, this), AnalysisPriority.High);
        }

        private class AddZipArchiveAnalysis : IAnalyzable {
            private readonly string _zipFileName;
            private readonly Action<IProjectEntry> _onFileAnalyzed;
            private readonly OutOfProcProjectAnalyzer _analyzer;

            public AddZipArchiveAnalysis(string zipFileName, Action<IProjectEntry> onFileAnalyzed, OutOfProcProjectAnalyzer analyzer) {
                _zipFileName = zipFileName;
                _onFileAnalyzed = onFileAnalyzed;
                _analyzer = analyzer;
            }

            #region IAnalyzable Members

            public void Analyze(CancellationToken cancel) {
                if (cancel.IsCancellationRequested) {
                    return;
                }

                _analyzer.AnalyzeZipArchiveWorker(_zipFileName, _onFileAnalyzed, cancel);
            }

            #endregion
        }


        private void AnalyzeZipArchiveWorker(string zipFileName, Action<IProjectEntry> onFileAnalyzed, CancellationToken cancel) {
            if (_pyAnalyzer == null) {
                // We aren't able to analyze code.
                return;
            }

            _pyAnalyzer.AddAnalysisDirectory(zipFileName);

            ZipArchive archive = null;
            Queue<ZipArchiveEntry> entryQueue = null;
            try {
                archive = ZipFile.Open(zipFileName, ZipArchiveMode.Read);

                // We only want to scan files in directories that are packages - i.e. contain __init__.py. So enumerate
                // entries in the archive, and build a list of such directories, so that later on we can compare file
                // paths against that to see if we should scan them.
                var packageDirs = new HashSet<string>(
                    from entry in archive.Entries
                    where entry.Name == "__init__.py"
                    select Path.GetDirectoryName(entry.FullName));
                packageDirs.Add(""); // we always want to scan files on the top level of the archive

                entryQueue = new Queue<ZipArchiveEntry>(
                    from entry in archive.Entries
                    let ext = Path.GetExtension(entry.Name)
                    where ext == ".py" || ext == ".pyw"
                    let path = Path.GetDirectoryName(entry.FullName)
                    where packageDirs.Contains(path)
                    select entry);
            } catch (InvalidDataException ex) {
                Debug.Fail(ex.Message);
                return;
            } catch (IOException ex) {
                Debug.Fail(ex.Message);
                return;
            } catch (UnauthorizedAccessException ex) {
                Debug.Fail(ex.Message);
                return;
            } finally {
                if (archive != null && entryQueue == null) {
                    archive.Dispose();
                }
            }

            // ZipArchive is not thread safe, and so we cannot analyze entries in parallel. Instead, use completion
            // callbacks to queue the next one for analysis only after the preceding one is fully analyzed.
            Action analyzeNextEntry = null;
            analyzeNextEntry = () => {
                try {
                    if (entryQueue.Count == 0 || cancel.IsCancellationRequested) {
                        archive.Dispose();
                        return;
                    }

                    ZipArchiveEntry zipEntry = entryQueue.Dequeue();
                    IProjectEntry projEntry = AnalyzeZipArchiveEntry(zipFileName, zipEntry, analyzeNextEntry);
                    if (onFileAnalyzed != null) {
                        onFileAnalyzed(projEntry);
                    }
                } catch (InvalidDataException ex) {
                    Debug.Fail(ex.Message);
                } catch (IOException ex) {
                    Debug.Fail(ex.Message);
                } catch (UnauthorizedAccessException ex) {
                    Debug.Fail(ex.Message);
                }
            };
            analyzeNextEntry();
        }

        private IProjectEntry AnalyzeZipArchiveEntry(string zipFileName, ZipArchiveEntry entry, Action onComplete) {
            if (_pyAnalyzer == null) {
                // We aren't able to analyze code, so don't create an entry.
                return null;
            }
            try {
                string pathInZip = entry.FullName.Replace('/', '\\');
                string path;
                try {
                    path = PathUtils.GetAbsoluteFilePath(zipFileName, pathInZip);
                } catch (ArgumentException) {
                    return null;
                }

                IProjectEntry item;
                if (_projectFiles.TryGetValue(path, out item)) {
                    return item;
                }

                if (ModulePath.IsPythonSourceFile(path)) {
                    // Use the entry path relative to the root of the archive to determine module name - this boundary
                    // should never be crossed, even if the parent directory of the zip is itself a package.
                    string modName;
                    try {
                        modName = ModulePath.FromFullPath(
                            pathInZip,
                            isPackage: dir => entry.Archive.GetEntry(
                                (PathUtils.EnsureEndSeparator(dir) + "__init__.py").Replace('\\', '/')
                            ) != null).ModuleName;
                    } catch (ArgumentException) {
                        return null;
                    }
                    item = _pyAnalyzer.AddModule(modName, path, null);
                }
                if (item == null) {
                    return null;
                }

                SetZipFileName(item, zipFileName);
                SetPathInZipFile(item, pathInZip);
                _projectFiles.Add(path, item);
                IPythonProjectEntry pyEntry = item as IPythonProjectEntry;
                if (pyEntry != null) {
                    pyEntry.BeginParsingTree();
                }

                EnqueueZipArchiveEntry(item, zipFileName, entry, onComplete);
                onComplete = null;
                return item;
            } finally {
                if (onComplete != null) {
                    onComplete();
                }
            }
        }

        internal void StopAnalyzingDirectory(string directory) {
            if (_pyAnalyzer == null) {
                // We aren't able to analyze code.
                return;
            }

            _pyAnalyzer.RemoveAnalysisDirectory(directory);
        }

        internal void Cancel() {
            _analysisQueue.Stop();
        }

        internal void UnloadFile(IProjectEntry entry) {
            if (_pyAnalyzer == null) {
                // We aren't able to analyze code.
                return;
            }

            if (entry != null) {
                // If we remove a Python module, reanalyze any other modules
                // that referenced it.
                IPythonProjectEntry[] reanalyzeEntries = null;
                var pyEntry = entry as IPythonProjectEntry;
                if (pyEntry != null && !string.IsNullOrEmpty(pyEntry.ModuleName)) {
                    reanalyzeEntries = _pyAnalyzer.GetEntriesThatImportModule(pyEntry.ModuleName, false).ToArray();
                }

                _pyAnalyzer.RemoveModule(entry);
                _projectFiles.Remove(entry);

                if (reanalyzeEntries != null) {
                    foreach (var existing in reanalyzeEntries) {
                        _analysisQueue.Enqueue(existing, AnalysisPriority.Normal);
                    }
                }
            }
        }

        /// <summary>
        /// Parses the specified file on disk.
        /// </summary>
        /// <param name="filename"></param>
        public void EnqueueFile(IProjectEntry projEntry, string filename) {
            EnqueWorker(() => {
                for (int i = 0; i < 10; i++) {
                    try {
                        if (!File.Exists(filename)) {
                            break;
                        }
                        using (var reader = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                            ParseFile(
                                projEntry, 
                                new Dictionary<int, CodeInfo> {
                                    { 0, new StreamCodeInfo(0, reader) }
                                }
                            );
                            return;
                        }
                    } catch (IOException) {
                        // file being copied, try again...
                        Thread.Sleep(100);
                    } catch (UnauthorizedAccessException) {
                        // file is inaccessible, try again...
                        Thread.Sleep(100);
                    }
                }

                IPythonProjectEntry pyEntry = projEntry as IPythonProjectEntry;
                if (pyEntry != null) {
                    // failed to parse, keep the UpdateTree calls balanced
                    pyEntry.UpdateTree(null, null);
                }
            });
        }

        public void EnqueueZipArchiveEntry(IProjectEntry projEntry, string zipFileName, ZipArchiveEntry entry, Action onComplete) {
            var pathInArchive = entry.FullName.Replace('/', '\\');
            var fileName = Path.Combine(zipFileName, pathInArchive);
            EnqueWorker(() => {
                try {
                    using (var stream = entry.Open()) {
                        ParseFile(
                            projEntry,
                            new Dictionary<int, CodeInfo> {
                                { 0, new StreamCodeInfo(0, stream) }
                            }
                        );
                        return;
                    }
                } catch (IOException ex) {
                    Debug.Fail(ex.Message);
                } catch (InvalidDataException ex) {
                    Debug.Fail(ex.Message);
                } finally {
                    onComplete();
                }

                IPythonProjectEntry pyEntry = projEntry as IPythonProjectEntry;
                if (pyEntry != null) {
                    // failed to parse, keep the UpdateTree calls balanced
                    pyEntry.UpdateTree(null, null);
                }
            });
        }

        private void EnqueWorker(Action parser) {
            Interlocked.Increment(ref _analysisPending);

            ThreadPool.QueueUserWorkItem(
                dummy => {
                    try {
                        parser();
                    } finally {
                        Interlocked.Decrement(ref _analysisPending);
                    }
                }
            );
        }

        public bool IsParsing {
            get {
                return _analysisPending > 0;
            }
        }

        public int ParsePending {
            get {
                return _analysisPending;
            }
        }

        #endregion

        #region IDisposable Members

        public void Dispose() {
            foreach (var pathAndEntry in _projectFiles) {
#if PORT
                _errorProvider.Clear(pathAndEntry.Value, ParserTaskMoniker);
                _errorProvider.Clear(pathAndEntry.Value, UnresolvedImportMoniker);
                _commentTaskProvider.Clear(pathAndEntry.Value, ParserTaskMoniker);
#endif
            }

            _analysisQueue.AnalysisStarted -= AnalysisQueue_AnalysisStarted;
            _analysisQueue.Dispose();
            if (_pyAnalyzer != null) {
                _pyAnalyzer.Interpreter.ModuleNamesChanged -= OnModulesChanged;
                _pyAnalyzer.Dispose();
            }

            _queueActivityEvent.Dispose();
        }

        #endregion

        internal void RemoveReference(ProjectAssemblyReference reference) {
            var interp = Interpreter as IPythonInterpreterWithProjectReferences;
            if (interp != null) {
                interp.RemoveReference(reference);
            }
        }

        class OutliningWalker : PythonWalker {
            public readonly List<AP.OutliningTag> TagSpans = new List<AP.OutliningTag>();
            readonly PythonAst _ast;

            public OutliningWalker(PythonAst ast) {
                _ast = ast;
            }

            // Compound Statements: if, while, for, try, with, func, class, decorated
            public override bool Walk(IfStatement node) {
                if (node.ElseStatement != null) {
                    AddTagIfNecessaryShowLineAbove(node, node.ElseStatement);
                }

                return base.Walk(node);
            }

            public override bool Walk(IfStatementTest node) {
                if (node.Test != null && node.Body != null) {
                    AddTagIfNecessary(node.Test.StartIndex, node.Body.EndIndex);
                    // Don't walk test condition.
                    node.Body.Walk(this);
                }
                return false;
            }

            public override bool Walk(WhileStatement node) {
                // Walk while statements manually so we don't traverse the test.
                // This prevents the test from being collapsed ever.
                if (node.Body != null) {
                    AddTagIfNecessary(
                        node.StartIndex,
                        node.Body.EndIndex,
                        node.HeaderIndex
                    );
                    node.Body.Walk(this);
                }
                if (node.ElseStatement != null) {
                    AddTagIfNecessaryShowLineAbove(node, node.ElseStatement);
                    node.ElseStatement.Walk(this);
                }
                return false;
            }

            public override bool Walk(ForStatement node) {
                // Walk for statements manually so we don't traverse the list.  
                // This prevents the list and/or left from being collapsed ever.
                if (node.Body != null) {
                    AddTagIfNecessary(
                        node.StartIndex,
                        node.Body.EndIndex,
                        node.HeaderIndex
                    );
                    node.Body.Walk(this);
                }
                if (node.Else != null) {
                    AddTagIfNecessaryShowLineAbove(node, node.Else);
                    node.Else.Walk(this);
                }
                return false;
            }

            public override bool Walk(TryStatement node) {
                if (node.Body != null) {
                    AddTagIfNecessaryShowLineAbove(node, node.Body);
                }
                if (node.Handlers != null) {
                    foreach (var h in node.Handlers) {
                        AddTagIfNecessaryShowLineAbove(node, h);
                    }
                }
                if (node.Finally != null) {
                    AddTagIfNecessaryShowLineAbove(node, node.Finally);
                }
                if (node.Else != null) {
                    AddTagIfNecessaryShowLineAbove(node, node.Else);
                }

                return base.Walk(node);
            }

            public override bool Walk(WithStatement node) {
                AddTagIfNecessary(node);
                return base.Walk(node);
            }

            public override bool Walk(FunctionDefinition node) {
                // Walk manually so collapsing is not enabled for params.
                if (node.Body != null) {
                    AddTagIfNecessary(
                        node.StartIndex,
                        node.Body.EndIndex,
                        decorator: node.Decorators);
                    node.Body.Walk(this);
                }

                return false;
            }

            public override bool Walk(ClassDefinition node) {
                AddTagIfNecessary(node, node.HeaderIndex + 1, node.Decorators);

                return base.Walk(node);
            }

            // Not-Compound Statements
            public override bool Walk(CallExpression node) {
                AddTagIfNecessary(node);
                return base.Walk(node);
            }

            public override bool Walk(FromImportStatement node) {
                AddTagIfNecessary(node);
                return base.Walk(node);
            }

            public override bool Walk(ListExpression node) {
                AddTagIfNecessary(node);
                return base.Walk(node);
            }

            public override bool Walk(TupleExpression node) {
                AddTagIfNecessary(node);
                return base.Walk(node);
            }

            public override bool Walk(DictionaryExpression node) {
                AddTagIfNecessary(node);
                return base.Walk(node);
            }

            public override bool Walk(SetExpression node) {
                AddTagIfNecessary(node);
                return base.Walk(node);
            }

            public override bool Walk(ParenthesisExpression node) {
                AddTagIfNecessary(node);
                return base.Walk(node);
            }

            public override bool Walk(ConstantExpression node) {
                AddTagIfNecessary(node);
                return base.Walk(node);
            }

            private void AddTagIfNecessary(Node node, int headerIndex = -1, DecoratorStatement decorator = null) {
                AddTagIfNecessary(node.StartIndex, node.EndIndex, headerIndex, decorator);
            }

            private void AddTagIfNecessary(int startIndex, int endIndex, int headerIndex = -1, DecoratorStatement decorator = null, int minLinesToCollapse = 3) {
                var startLine = _ast.IndexToLocation(startIndex).Line;
                var endLine = _ast.IndexToLocation(endIndex).Line;
                var lines = endLine - startLine + 1;

                // Collapse if more than 3 lines.
                if (lines >= minLinesToCollapse) {
                    if (decorator != null) {
                        // we don't want to collapse the decorators, we like them visible, so
                        // we base our starting position on where the decorators end.
                        startIndex = decorator.EndIndex + 1;
                    }

                    var tagSpan = new AP.OutliningTag() {
                        startIndex = startIndex,
                        endIndex = endIndex,
                        headerIndex = headerIndex
                    };
                    TagSpans.Add(tagSpan);
                }
            }

            private void AddTagIfNecessaryShowLineAbove(Node parent, Node node) {
                var parentLocation = _ast.IndexToLocation(parent.EndIndex);
                var childLocation = _ast.IndexToLocation(node.StartIndex);

                if (parentLocation.Line == childLocation.Line) {
                    AddTagIfNecessary(node.StartIndex, node.EndIndex);
                } else {
                    AddTagIfNecessary(parent.StartIndex, node.EndIndex);
                }
            }
        }
    }
}

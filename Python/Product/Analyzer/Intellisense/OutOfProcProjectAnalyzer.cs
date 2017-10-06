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
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Ipc.Json;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.PythonTools.Projects;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Intellisense {
    using AP = AnalysisProtocol;

    /// <summary>
    /// Performs centralized parsing and analysis of Python source code for a remotely running process.
    /// 
    /// This class is responsible for maintaining the up-to-date analysis of the active files being worked
    /// on inside of a single proejct.  
    /// 
    /// This class is built upon the core PythonAnalyzer class which provides basic analysis services.  This class
    /// maintains the thread safety invarients of working with that class, handles parsing of files as they're
    /// updated via interfacing w/ the remote process editor APIs, and supports adding additional files to the 
    /// analysis.
    /// </summary>
    sealed class OutOfProcProjectAnalyzer : IDisposable {
        private readonly AnalysisQueue _analysisQueue;
        private IPythonInterpreterFactory _interpreterFactory;
        private readonly ProjectEntryMap _projectFiles;
        private PythonAnalyzer _pyAnalyzer;
        private readonly AutoResetEvent _queueActivityEvent = new AutoResetEvent(false);
        private InterpreterConfiguration[] _allConfigs;
        private volatile Dictionary<string, AP.TaskPriority> _commentPriorityMap = new Dictionary<string, AP.TaskPriority>() {
            { "TODO", AP.TaskPriority.normal },
            { "HACK", AP.TaskPriority.high },
        };
        private AP.OptionsChangedEvent _options;
        private readonly AggregateCatalog _catalog;
        private readonly CompositionContainer _container;
        internal int _analysisPending;

        private bool _isDisposed;

        // Moniker strings allow the task provider to distinguish between
        // different sources of items for the same file.
        private const string ParserTaskMoniker = "Parser";
        internal const string UnresolvedImportMoniker = "UnresolvedImport";
        private readonly HashSet<IAnalysisExtension> _registeredExtensions = new HashSet<IAnalysisExtension>();
        private readonly Dictionary<string, IAnalysisExtension> _extensionsByName = new Dictionary<string, IAnalysisExtension>();

        private readonly Connection _connection;

        internal OutOfProcProjectAnalyzer(Stream writer, Stream reader) {
            _analysisQueue = new AnalysisQueue(this);
            _analysisQueue.AnalysisComplete += AnalysisQueue_Complete;
            _analysisQueue.AnalysisAborted += AnalysisQueue_Aborted;
            _options = new AP.OptionsChangedEvent() {
                indentation_inconsistency_severity = Severity.Ignore
            };

            _projectFiles = new ProjectEntryMap();
            _connection = new Connection(
                writer,
                true,
                reader,
                true,
                RequestHandler,
                AP.RegisteredTypes,
                "Analyzer"
            );
            _connection.EventReceived += ConectionReceivedEvent;

            GlobalInterpreterOptions.SuppressFileSystemWatchers = true;
            GlobalInterpreterOptions.SuppressPackageManagers = true;

            _catalog = new AggregateCatalog();
            _container = new CompositionContainer(_catalog);
            _container.ExportsChanged += ContainerExportsChanged;
        }

        private void AnalysisQueue_Aborted(object sender, EventArgs e) {
            _connection.Dispose();
        }

        private void ContainerExportsChanged(object sender, ExportsChangeEventArgs e) {
            // figure out the new extensions...
            var extensions = _container.GetExports<IAnalysisExtension, IDictionary<string, object>>();
            HashSet<IAnalysisExtension> newExtensions = new HashSet<IAnalysisExtension>();
            lock (_registeredExtensions) {
                foreach (var extension in extensions) {

                    if (!_registeredExtensions.Contains(extension.Value)) {

                        newExtensions.Add(extension.Value);
                        if (extension.Metadata.ContainsKey("Name") && extension.Metadata["Name"] is string) {
                            _extensionsByName[(string)extension.Metadata["Name"]] = extension.Value;
                        } else {
                            Console.Error.WriteLine("Extension {0} has no name, will not respond to commands", extension.Value);
                        }
                    }
                }

                _registeredExtensions.UnionWith(newExtensions);
            }

            if (_pyAnalyzer != null) {
                // inform them of the analyzer...
                foreach (var extension in newExtensions) {
                    extension.Register(_pyAnalyzer);
                }
            }
        }

        private void ConectionReceivedEvent(object sender, EventReceivedEventArgs e) {
            switch (e.Event.name) {
                case AP.ModulesChangedEvent.Name: OnModulesChanged(this, EventArgs.Empty); break;
                case AP.OptionsChangedEvent.Name: SetOptions((AP.OptionsChangedEvent)e.Event); break;
                case AP.SetCommentTaskTokens.Name: _commentPriorityMap = ((AP.SetCommentTaskTokens)e.Event).tokens; break;
                case AP.ExtensionAddedEvent.Name: AddExtension((AP.ExtensionAddedEvent)e.Event); break;
            }
        }

        private void AddExtension(AP.ExtensionAddedEvent extensionAdded) {
            _catalog.Catalogs.Add(new AssemblyCatalog(extensionAdded.path));
        }

        private void SetOptions(AP.OptionsChangedEvent options) {
            if (_pyAnalyzer != null) {
                _pyAnalyzer.Limits.CrossModule = options.crossModuleAnalysisLimit;
            }
            _options = options;
        }

        private async Task RequestHandler(RequestArgs requestArgs, Func<Response, Task> done) {
            Response response;
            var command = requestArgs.Command;
            var request = requestArgs.Request;

            // These commands send their own responses, and then we return.
            switch (command) {
                case AP.AddFileRequest.Command: await AnalyzeFileAsync((AP.AddFileRequest)request, done); return;
                case AP.AddBulkFileRequest.Command: await AnalyzeFileAsync((AP.AddBulkFileRequest)request, done); return;
            }

            // These commands return a response, which we then send.
            switch (command) {
                case AP.UnloadFileRequest.Command: response = UnloadFile((AP.UnloadFileRequest)request); break;
                case AP.TopLevelCompletionsRequest.Command: response = GetTopLevelCompletions(request); break;
                case AP.CompletionsRequest.Command: response = GetCompletions(request); break;
                case AP.GetAllMembersRequest.Command: response = GetAllMembers(request); break;
                case AP.GetModulesRequest.Command: response = GetModules(request); break;
                case AP.SignaturesRequest.Command: response = GetSignatures((AP.SignaturesRequest)request); break;
                case AP.QuickInfoRequest.Command: response = GetQuickInfo((AP.QuickInfoRequest)request); break;
                case AP.AnalyzeExpressionRequest.Command: response = AnalyzeExpression((AP.AnalyzeExpressionRequest)request); break;
                case AP.OutliningRegionsRequest.Command: response = GetOutliningRegions((AP.OutliningRegionsRequest)request); break;
                case AP.NavigationRequest.Command: response = GetNavigations((AP.NavigationRequest)request); break;
                case AP.FileUpdateRequest.Command: response = UpdateContent((AP.FileUpdateRequest)request); break;
                case AP.UnresolvedImportsRequest.Command: response = GetUnresolvedImports((AP.UnresolvedImportsRequest)request); break;
                case AP.AddImportRequest.Command: response = AddImportRequest((AP.AddImportRequest)request); break;
                case AP.IsMissingImportRequest.Command: response = IsMissingImport((AP.IsMissingImportRequest)request); break;
                case AP.AvailableImportsRequest.Command: response = AvailableImports((AP.AvailableImportsRequest)request); break;
                case AP.FormatCodeRequest.Command: response = FormatCode((AP.FormatCodeRequest)request); break;
                case AP.RemoveImportsRequest.Command: response = RemoveImports((AP.RemoveImportsRequest)request); break;
                case AP.ExtractMethodRequest.Command: response = ExtractMethod((AP.ExtractMethodRequest)request); break;
                case AP.AnalysisStatusRequest.Command: response = AnalysisStatus(); break;
                case AP.OverridesCompletionRequest.Command: response = GetOverrides((AP.OverridesCompletionRequest)request); break;
                case AP.LocationNameRequest.Command: response = GetLocationName((AP.LocationNameRequest)request); break;
                case AP.ProximityExpressionsRequest.Command: response = GetProximityExpressions((AP.ProximityExpressionsRequest)request); break;
                case AP.AnalysisClassificationsRequest.Command: response = GetAnalysisClassifications((AP.AnalysisClassificationsRequest)request); break;
                case AP.MethodInsertionLocationRequest.Command: response = GetMethodInsertionLocation((AP.MethodInsertionLocationRequest)request); break;
                case AP.MethodInfoRequest.Command: response = GetMethodInfo((AP.MethodInfoRequest)request); break;
                case AP.FindMethodsRequest.Command: response = FindMethods((AP.FindMethodsRequest)request); break;
                case AP.AddReferenceRequest.Command: response = AddReference((AP.AddReferenceRequest)request); break;
                case AP.RemoveReferenceRequest.Command: response = RemoveReference((AP.RemoveReferenceRequest)request); break;
                case AP.SetSearchPathRequest.Command: response = SetSearchPath((AP.SetSearchPathRequest)request); break;
                case AP.ModuleImportsRequest.Command: response = GetModuleImports((AP.ModuleImportsRequest)request); break;
                case AP.ValueDescriptionRequest.Command: response = GetValueDescriptions((AP.ValueDescriptionRequest)request); break;
                case AP.ExtensionRequest.Command: response = ExtensionRequest((AP.ExtensionRequest)request); break;
                case AP.InitializeRequest.Command: response = await Initialize((AP.InitializeRequest)request); break;
                case AP.ExpressionForDataTipRequest.Command: response = ExpressionForDataTip((AP.ExpressionForDataTipRequest)request); break;
                case AP.ExitRequest.Command: throw new OperationCanceledException();
                default:
                    throw new InvalidOperationException("Unknown command");
            }

            await done(response);
        }

        internal void ReportUnhandledException(Exception ex) {
            SendUnhandledException(ex);
            // Allow some time for the other threads to write the event before
            // we (probably) come crashing down.
            Thread.Sleep(100);
        }

        private async void SendUnhandledException(Exception ex) {
            try {
                Debug.Fail(ex.ToString());
                await _connection.SendEventAsync(
                    new AP.UnhandledExceptionEvent(ex)
                ).ConfigureAwait(false);
            } catch (Exception) {
                // We're in pretty bad state, but nothing useful we can do about
                // it.
                Debug.Fail("Unhandled exception reporting unhandled exception");
            }
        }

        private async Task<Response> Initialize(AP.InitializeRequest request) {
            List<AssemblyCatalog> catalogs = new List<AssemblyCatalog>();

            HashSet<string> assemblies = new HashSet<string>(request.mefExtensions);
            assemblies.Add(typeof(IInterpreterRegistryService).Assembly.Location);
            assemblies.Add(GetType().Assembly.Location);

            List<string> failures = new List<string>();
            string error = null;
            foreach (var asm in assemblies) {
                try {
                    var asmCatalog = new AssemblyCatalog(asm);
                    _catalog.Catalogs.Add(asmCatalog);
                } catch (Exception e) {
                    failures.Add(String.Format("Failed to load {0}: {1}", asm, e));
                }
            }

            if (request.projectFile != null) {
                var projectContextProvider = _container.GetExportedValue<OutOfProcProjectContextProvider>();
                projectContextProvider.AddContext(
                    new InMemoryProject(
                        request.projectFile,
                        new Dictionary<string, object>() {
                            { "InterpreterId", request.interpreterId },
                            { "ProjectHome", request.projectHome },
                            {  "Interpreters",
                                request.derivedInterpreters.Select(
                                    interp => new Dictionary<string, string>() {
                                        { "EvaluatedInclude", interp.name },
                                        { MSBuildConstants.IdKey,              interp.id },
                                        { MSBuildConstants.VersionKey,         interp.version },
                                        { MSBuildConstants.DescriptionKey,     interp.description },
                                        { MSBuildConstants.BaseInterpreterKey, interp.baseInterpreter },
                                        { MSBuildConstants.InterpreterPathKey, interp.path },
                                        { MSBuildConstants.WindowsPathKey,     interp.windowsPath },
                                        { MSBuildConstants.LibraryPathKey,     interp.libPath },
                                        { MSBuildConstants.PathEnvVarKey,      interp.pathEnvVar },
                                        { MSBuildConstants.ArchitectureKey,    interp.arch }
                                    }
                                ).ToArray()
                            }
                        }
                    )
                );
            }

            var registry = _container.GetExportedValue<IInterpreterRegistryService>();
            IPythonInterpreterFactory factory = null;
            Version analysisVersion;
            if (request.interpreterId.StartsWith("AnalysisOnly|")) {
                int versionStart = request.interpreterId.IndexOf('|') + 1;
                int versionEnd = request.interpreterId.IndexOf('|', versionStart);

                if (Version.TryParse(request.interpreterId.Substring(versionStart, versionEnd - versionStart), out analysisVersion)) {
                    string[] dbDirs;
                    if (versionEnd + 1 != request.interpreterId.Length) {
                        dbDirs = request.interpreterId.Substring(versionEnd + 1).Split('|');
                    } else {
                        dbDirs = null;
                    }
                    factory = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(analysisVersion, null, dbDirs);
                }
            } else {
                factory = registry.FindInterpreter(request.interpreterId);
            }

            if (factory == null) {
                if (_connection != null) {
                    await _connection.SendEventAsync(
                        new AP.AnalyzerWarningEvent(string.Format("No active interpreter found for interpreter ID: {0}", request.interpreterId))
                    );
                }
                var db = PythonTypeDatabase.CreateDefaultTypeDatabase();
                factory = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(db.LanguageVersion, db);
            }

            _interpreterFactory = factory;
            _allConfigs = registry.Configurations.ToArray();

            try {
                var interpreter = factory.CreateInterpreter();
                if (interpreter != null) {
                    _pyAnalyzer = PythonAnalyzer.Create(factory, interpreter);
                    await _pyAnalyzer.ReloadModulesAsync();
                    interpreter.ModuleNamesChanged += OnModulesChanged;
                }
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                error = ex.ToString();
            }

            return new AP.InitializeResponse() {
                failedLoads = failures.ToArray(),
                error = error
            };
        }

        private Response ExtensionRequest(AP.ExtensionRequest request) {
            IAnalysisExtension extension;
            lock (_registeredExtensions) {
                if (!_extensionsByName.TryGetValue(request.extension, out extension)) {
                    throw new InvalidOperationException("Unknown extension: " + request.extension);
                }
            }

            return new AP.ExtensionResponse() {
                response = extension.HandleCommand(request.commandId, request.body)
            };
        }

        private Response GetValueDescriptions(AP.ValueDescriptionRequest request) {
            var pyEntry = _projectFiles[request.fileId] as IPythonProjectEntry;
            string[] descriptions = Array.Empty<string>();
            if (pyEntry.Analysis != null) {
                var values = pyEntry.Analysis.GetValues(
                    request.expr,
                    new SourceLocation(
                        request.index,
                        request.line,
                        request.column
                    )
                );

                descriptions = values.Select(x => x.Description).ToArray();
            }

            return new AP.ValueDescriptionResponse() {
                descriptions = descriptions
            };
        }

        private Response GetModuleImports(AP.ModuleImportsRequest request) {
            var res = Analyzer.GetEntriesThatImportModule(
                request.moduleName,
                request.includeUnresolved
            );

            return new AP.ModuleImportsResponse() {
                modules = res.Select(entry => new AP.ModuleInfo() {
                    filename = entry.FilePath,
                    moduleName = entry.ModuleName,
                    fileId = ProjectEntryMap.GetId(entry)
                }).ToArray()
            };
        }

        private Response SetSearchPath(AP.SetSearchPathRequest request) {
            Analyzer.SetSearchPaths(request.dir);

            return new Response();
        }

        private Response RemoveReference(AP.RemoveReferenceRequest request) {
            var interp = Interpreter as IPythonInterpreterWithProjectReferences;
            if (interp != null) {
                interp.RemoveReference(AP.ProjectReference.Convert(request.reference));
            }
            return new AP.RemoveReferenceResponse();
        }

        private Response AddReference(AP.AddReferenceRequest request) {
            var interp = Interpreter as IPythonInterpreterWithProjectReferences;
            if (interp != null) {
                interp.AddReferenceAsync(AP.ProjectReference.Convert(request.reference)).Wait();
            }
            return new AP.AddReferenceResponse();
        }

        private static ProjectReference GetAssemblyReference(string kind, string name, string assemblyName) {
            ProjectReference reference;
            switch (kind) {
                case "assembly":
                    reference = new ProjectAssemblyReference(
                        new AssemblyName(assemblyName),
                        name
                    );
                    break;
                case "extension":
                    reference = new ProjectReference(name, ProjectReferenceKind.ExtensionModule);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported reference type: " + kind);
            }

            return reference;
        }

        private Response FindMethods(AP.FindMethodsRequest request) {
            var analysis = _projectFiles[request.fileId] as IPythonProjectEntry;

            List<string> names = new List<string>();
            if (analysis != null) {
                int version;
                string code;
                var ast = analysis.GetVerbatimAstAndCode(
                    Analyzer.LanguageVersion,
                    request.bufferId,
                    out version,
                    out code
                );

                if (ast != null) {
                    foreach (var classDef in FindClassDef(request.className, ast)) {
                        SuiteStatement suite = classDef.Body as SuiteStatement;
                        if (suite != null) {
                            foreach (var methodCandidate in suite.Statements) {
                                FunctionDefinition funcDef = methodCandidate as FunctionDefinition;
                                if (funcDef != null) {
                                    if (request.paramCount != null && request.paramCount != funcDef.Parameters.Count) {
                                        continue;
                                    }

                                    names.Add(funcDef.Name);
                                }
                            }
                        }
                    }
                }
            }

            return new AP.FindMethodsResponse() {
                names = names.ToArray()
            };
        }

        private Response GetMethodInsertionLocation(AP.MethodInsertionLocationRequest request) {
            var analysis = _projectFiles[request.fileId] as IPythonProjectEntry;

            if (analysis != null) {
                int version;
                string code;
                var ast = analysis.GetVerbatimAstAndCode(
                    Analyzer.LanguageVersion,
                    request.bufferId,
                    out version,
                    out code
                );

                if (ast != null) {
                    foreach (var classDef in FindClassDef(request.className, ast)) {
                        int end = classDef.Body.EndIndex;
                        // insert after the newline at the end of the last statement of the class def
                        if (code[end] == '\r') {
                            if (end + 1 < code.Length &&
                                code[end + 1] == '\n') {
                                end += 2;
                            } else {
                                end++;
                            }
                        } else if (code[end] == '\n') {
                            end++;
                        }

                        return new AP.MethodInsertionLocationResponse() {
                            indentation = classDef.Body.GetStart(ast).Column - 1,
                            location = end,
                            version = version
                        };
                    }

                    throw new InvalidOperationException("Failed to find class definition");
                }
            }

            throw new InvalidOperationException("Analysis not available");
        }

        private static IEnumerable<ClassDefinition> FindClassDef(string name, PythonAst ast) {
            var suiteStmt = ast.Body as SuiteStatement;
            foreach (var stmt in suiteStmt.Statements) {
                var classDef = stmt as ClassDefinition;
                if (classDef != null &&
                    (classDef.Name == name || name == null)) {
                    yield return classDef;
                }
            }
        }

        private Response GetMethodInfo(AP.MethodInfoRequest request) {
            var analysis = _projectFiles[request.fileId] as IPythonProjectEntry;

            if (analysis != null) {
                int version;
                string code;
                var ast = analysis.GetVerbatimAstAndCode(
                    Project.LanguageVersion,
                    request.bufferId,
                    out version,
                    out code
                );

                if (ast != null) {
                    foreach (var classDef in FindClassDef(request.className, ast)) {
                        SuiteStatement suite = classDef.Body as SuiteStatement;

                        if (suite != null) {
                            foreach (var methodCandidate in suite.Statements) {
                                FunctionDefinition funcDef = methodCandidate as FunctionDefinition;
                                if (funcDef != null) {
                                    if (funcDef.Name == request.methodName) {
                                        return new AP.MethodInfoResponse() {
                                            start = funcDef.StartIndex,
                                            end = funcDef.EndIndex,
                                            version = version,
                                            found = true
                                        };
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return new AP.MethodInfoResponse() {
                found = false
            };
        }

        private Response GetAnalysisClassifications(AP.AnalysisClassificationsRequest request) {
            var projEntry = _projectFiles[request.fileId] as IPythonProjectEntry;

            AP.AnalysisClassification[] classifications = Array.Empty<AP.AnalysisClassification>();
            var bufferVersion = GetBufferVersion(request.fileId, request.bufferId);
            if (bufferVersion != null && bufferVersion.Ast != null) {
                var moduleAnalysis = request.colorNames ? projEntry.Analysis : null;

                var walker = new ClassifierWalker(bufferVersion.Ast, moduleAnalysis);
                bufferVersion.Ast.Walk(walker);

                classifications = walker.Spans.ToArray();
            }

            return new AP.AnalysisClassificationsResponse() {
                version = bufferVersion?.Version ?? -1,
                classifications = classifications
            };
        }

        private Response GetProximityExpressions(AP.ProximityExpressionsRequest request) {
            var projEntry = _projectFiles[request.fileId] as IPythonProjectEntry;

            string[] res = Array.Empty<string>();
            if (projEntry != null) {
                PythonAst tree = projEntry.Tree;
                if (tree != null) {
                    int startLine = Math.Max(request.line - request.lineCount + 1, 0);
                    if (startLine <= request.line) {
                        var walker = new ProximityExpressionWalker(tree, startLine, request.line);
                        tree.Walk(walker);
                        res = walker.GetExpressions().ToArray();
                    }
                }
            }
            return new AP.ProximityExpressionsResponse() {
                names = res
            };
        }

        private Response GetLocationName(AP.LocationNameRequest request) {
            var projEntry = _projectFiles[request.fileId] as IPythonProjectEntry;

            string name = "";
            int column = 0;
            if (projEntry != null) {
                PythonAst tree = projEntry.Tree;
                if (tree != null) {
                    string foundName = FindNodeInTree(tree, tree.Body as SuiteStatement, request.line);
                    if (foundName != null) {
                        name = projEntry.ModuleName + "." + foundName;
                        column = request.column;
                    } else {
                        name = projEntry.ModuleName;
                        column = request.column;
                    }
                }
            }

            return new AP.LocationNameResponse() {
                name = name,
                lineOffset = column
            };
        }

        private static string FindNodeInTree(PythonAst tree, SuiteStatement statement, int line) {
            if (statement != null) {
                foreach (var node in statement.Statements) {
                    FunctionDefinition funcDef = node as FunctionDefinition;
                    if (funcDef != null) {
                        var span = funcDef.GetSpan(tree);
                        if (span.Start.Line <= line && line <= span.End.Line) {
                            var res = FindNodeInTree(tree, funcDef.Body as SuiteStatement, line);
                            if (res != null) {
                                return funcDef.Name + "." + res;
                            }
                            return funcDef.Name;
                        }
                        continue;
                    }

                    ClassDefinition classDef = node as ClassDefinition;
                    if (classDef != null) {
                        var span = classDef.GetSpan(tree);
                        if (span.Start.Line <= line && line <= span.End.Line) {
                            var res = FindNodeInTree(tree, classDef.Body as SuiteStatement, line);
                            if (res != null) {
                                return classDef.Name + "." + res;
                            }
                            return classDef.Name;
                        }
                    }
                }
            }
            return null;
        }


        private Response AnalysisStatus() {
            QueueActivityEvent.WaitOne(100);

            return new AP.AnalysisStatusResponse() {
                itemsLeft = ParsePending + _analysisQueue.AnalysisPending
            };
        }

        private Response ExtractMethod(AP.ExtractMethodRequest request) {
            var projectFile = _projectFiles[request.fileId] as IPythonProjectEntry;
            int version;
            string code;
            var ast = projectFile.GetVerbatimAstAndCode(
                Project.LanguageVersion,
                request.bufferId,
                out version,
                out code
            );

            return new OutOfProcMethodExtractor(
                ast,
                code
            ).ExtractMethod(request, version);
        }

        private Response RemoveImports(AP.RemoveImportsRequest request) {
            var projectFile = _projectFiles[request.fileId] as IPythonProjectEntry;
            int version;
            string code;
            var ast = projectFile.GetVerbatimAstAndCode(
                Project.LanguageVersion,
                request.bufferId,
                out version,
                out code
            );
            if (ast != null) {
                var remover = new ImportRemover(ast, code, request.allScopes, request.index);

                return new AP.RemoveImportsResponse() {
                    changes = remover.RemoveImports(),
                    version = version
                };
            }

            return new AP.RemoveImportsResponse() {
                changes = Array.Empty<AP.ChangeInfo>(),
                version = -1
            };
        }

        private Response FormatCode(AP.FormatCodeRequest request) {
            var projectFile = _projectFiles[request.fileId] as IPythonProjectEntry;

            int version;
            string code;
            var ast = projectFile.GetVerbatimAstAndCode(
                Project.LanguageVersion,
                request.bufferId,
                out version,
                out code
            );
            if (ast != null) {
                var walker = new EnclosingNodeWalker(ast, request.startIndex, request.endIndex);
                ast.Walk(walker);

                if (walker.Target == null || !walker.Target.IsValidSelection) {
                    return new AP.FormatCodeResponse() {
                        changes = new AnalysisProtocol.ChangeInfo[0]
                    };
                }

                var body = walker.Target.GetNode(ast);


                int whitspaceStart = walker.Target.StartIncludingIndentation;

                int start;
                if (request.startIndex <= walker.Target.StartIncludingLeadingWhiteSpace) {
                    // we've selected the leading comments, format them too...
                    start = walker.Target.StartIncludingLeadingWhiteSpace;
                } else {
                    // the user didn't have any comments selected, don't reformat them
                    body.SetLeadingWhiteSpace(ast, body.GetIndentationLevel(ast));

                    start = walker.Target.StartIncludingIndentation;
                }

                int length = walker.Target.End - start;
                if (walker.Target.End < code.Length) {
                    if (code[walker.Target.End] == '\r') {
                        length++;
                        if (walker.Target.End + 1 < code.Length &&
                            code[walker.Target.End + 1] == '\n') {
                            length++;
                        }
                    } else if (code[walker.Target.End] == '\n') {
                        length++;
                    }
                }

                var selectedCode = code.Substring(start, length);

                return new AP.FormatCodeResponse() {
                    startIndex = start,
                    endIndex = walker.Target.End,
                    version = version,
                    changes = selectedCode.ReplaceByLines(
                        body.ToCodeString(ast, request.options),
                        request.newLine
                    ).Select(
                        x => new AP.ChangeInfo() {
                            start = x.start + start,
                            length = x.length,
                            newText = x.newText
                        }
                    ).ToArray()
                };
            }

            return new AP.FormatCodeResponse() {
                startIndex = 0,
                endIndex = 0,
                version = -1,
                changes = Array.Empty<AP.ChangeInfo>()
            };
        }

        private Response AvailableImports(AP.AvailableImportsRequest request) {
            return new AP.AvailableImportsResponse() {
                imports = Analyzer.FindNameInAllModules(request.name)
                    .Select(
                        x => new AP.ImportInfo() {
                            importName = x.ImportName,
                            fromName = x.FromName
                        }
                    )
                    .ToArray()
            };
        }

        private Response IsMissingImport(AP.IsMissingImportRequest request) {
            var entry = _projectFiles[request.fileId] as IPythonProjectEntry;
            var analysis = entry.Analysis;
            if (analysis != null) {
                var location = new SourceLocation(request.index, request.line, request.column);
                var nameExpr = GetFirstNameExpression(
                    analysis.GetAstFromText(
                        request.text,
                        location
                    ).Body
                );

                if (nameExpr != null && !IsImplicitlyDefinedName(nameExpr)) {
                    var name = nameExpr.Name;
                    var hasVariables = analysis.GetVariables(name, location).Any(IsDefinition);
                    var hasValues = analysis.GetValues(name, location).Any();

                    // if we have type information or an assignment to the variable we won't offer 
                    // an import smart tag.
                    if (!hasValues && !hasVariables) {
                        return new AP.IsMissingImportResponse() {
                            isMissing = true
                        };
                    }
                }
            }

            return new AP.IsMissingImportResponse() {
                isMissing = false
            };
        }

        private Response AddImportRequest(AP.AddImportRequest request) {
            var projectFile = _projectFiles[request.fileId] as IPythonProjectEntry;
            string name = request.name;
            string fromModule = request.fromModule;

            int version;
            PythonAst curAst = projectFile.GetVerbatimAst(Project.LanguageVersion, request.bufferId, out version);
            if (curAst == null) {
                return new AP.AddImportResponse() {
                    changes = Array.Empty<AP.ChangeInfo>(),
                    version = -1
                };
            }

            var suiteBody = curAst.Body as SuiteStatement;
            int start = 0;
            if (suiteBody != null) {
                foreach (var statement in suiteBody.Statements) {
                    if (IsDocString(statement as ExpressionStatement)) {
                        // doc string, import comes after this...
                        start = statement.EndIndex;
                        continue;
                    }

                    FromImportStatement fromImport;

                    if (statement is ImportStatement) {
                        if (fromModule == "__future__") {
                            // we need to insert before normal imports
                            break;
                        }

                        // we insert after this
                        start = statement.EndIndex;
                    } else if ((fromImport = (statement as FromImportStatement)) != null) {
                        // we might update this, we might insert after
                        if (fromModule != "__future__" && fromImport.Root.MakeString() == fromModule) {
                            // update the existing from ... import statement to include the new name.
                            return new AP.AddImportResponse() {
                                changes = new[] { UpdateFromImport(curAst, fromImport, name) }
                            };
                        }

                        start = statement.EndIndex;
                    }

                    break;
                }
            }

            string newText = MakeImportCode(fromModule, name);
            if (start == 0) {
                // we're adding it at the beginning of the file, we need a new line
                // after the import statement
                newText += request.newLine;
            } else {
                // we're adding it after the end of a statement, we need a newline after 
                // the statement we're appending after.
                newText = request.newLine + newText;
            }

            return new AP.AddImportResponse() {
                changes = new[] {
                    new AP.ChangeInfo() {
                        start = start,
                        length = 0,
                        newText = newText
                    }
                },
                version = version
            };
        }

        public static string MakeImportCode(string fromModule, string name) {
            if (string.IsNullOrEmpty(fromModule)) {
                return string.Format("import {0}", name);
            } else {
                return string.Format("from {0} import {1}", fromModule, name);
            }
        }

        private static AP.ChangeInfo UpdateFromImport(
            PythonAst curAst,
            FromImportStatement fromImport,
            string name
        ) {
            NameExpression[] names = new NameExpression[fromImport.Names.Count + 1];
            NameExpression[] asNames = fromImport.AsNames == null ? null : new NameExpression[fromImport.AsNames.Count + 1];
            NameExpression newName = new NameExpression(name);
            for (int i = 0; i < fromImport.Names.Count; i++) {
                names[i] = fromImport.Names[i];
            }
            names[fromImport.Names.Count] = newName;

            if (asNames != null) {
                for (int i = 0; i < fromImport.AsNames.Count; i++) {
                    asNames[i] = fromImport.AsNames[i];
                }
            }

            var newImport = new FromImportStatement((ModuleName)fromImport.Root, names, asNames, fromImport.IsFromFuture, fromImport.ForceAbsolute);
            curAst.CopyAttributes(fromImport, newImport);

            var newCode = newImport.ToCodeString(curAst);

            var span = fromImport.GetSpan(curAst);
            int leadingWhiteSpaceLength = (fromImport.GetLeadingWhiteSpace(curAst) ?? "").Length;
            return new AP.ChangeInfo() {
                start = span.Start.Index - leadingWhiteSpaceLength,
                length = span.Length + leadingWhiteSpaceLength,
                newText = newCode
            };
        }

        private static bool IsDocString(ExpressionStatement exprStmt) {
            ConstantExpression constExpr;
            return exprStmt != null &&
                    (constExpr = exprStmt.Expression as ConstantExpression) != null &&
                    (constExpr.Value is string || constExpr.Value is AsciiString);
        }

        private Response GetUnresolvedImports(AP.UnresolvedImportsRequest request) {
            var bufferVersion = GetBufferVersion(request.fileId, request.bufferId);

            AP.UnresolvedImport[] unresolved = Array.Empty<AP.UnresolvedImport>();
            if (bufferVersion != null && bufferVersion.Ast != null) {
                var entry = _projectFiles[request.fileId] as IPythonProjectEntry;

                var walker = new ImportStatementWalker(
                    bufferVersion.Ast,
                    entry,
                    Analyzer
                );

                bufferVersion.Ast.Walk(walker);
                unresolved = walker.Imports.ToArray();
            }

            return new AP.UnresolvedImportsResponse() {
                unresolved = unresolved,
                version = bufferVersion?.Version ?? -1
            };
        }

        private BufferVersion GetBufferVersion(int fileId, int bufferId) {
            var entry = _projectFiles[fileId] as IPythonProjectEntry;
            if (entry != null) {
                PythonAst ast;
                IAnalysisCookie cookie;
                entry.GetTreeAndCookie(out ast, out cookie);

                var versions = cookie as VersionCookie;
                BufferVersion versionInfo;
                if (versions != null && versions.Buffers.TryGetValue(bufferId, out versionInfo)) {
                    return versionInfo;
                }
            }
            return null;
        }

        class ImportStatementWalker : PythonWalker {
            public readonly List<AP.UnresolvedImport> Imports = new List<AP.UnresolvedImport>();

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

            private AP.UnresolvedImport MakeUnresolvedImport(string name, Node spanNode) {
                var span = spanNode.GetSpan(_ast);
                return new AP.UnresolvedImport() {
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

        private Response GetOutliningRegions(AP.OutliningRegionsRequest request) {
            AP.OutliningTag[] tags = Array.Empty<AP.OutliningTag>();
            var bufferVersion = GetBufferVersion(request.fileId, request.bufferId);
            if (bufferVersion != null && bufferVersion.Ast != null) {
                var walker = new OutliningWalker(bufferVersion.Ast);

                bufferVersion.Ast.Walk(walker);

                tags = walker.GetTags();
            }

            return new AP.OutliningRegionsResponse() {
                tags = tags.ToArray(),
                version = bufferVersion?.Version ?? -1
            };
        }

        private Response GetNavigations(AP.NavigationRequest request) {
            List<AP.Navigation> navs = new List<AP.Navigation>();
            var bufferVersion = GetBufferVersion(request.fileId, request.bufferId);

            if (bufferVersion != null && bufferVersion.Ast != null) {
                var suite = bufferVersion.Ast.Body as SuiteStatement;
                if (suite != null) {
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
                }
            }


            return new AP.NavigationResponse() {
                version = bufferVersion?.Version ?? -1,
                navigations = navs.ToArray()
            };
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

        private Response ExpressionForDataTip(AP.ExpressionForDataTipRequest request) {
            var pyEntry = _projectFiles[request.fileId] as IPythonProjectEntry;

            string dataTipExpression = null;
            var options = new CodeFormattingOptions() {
                UseVerbatimImage = false
            };

            if (pyEntry != null && pyEntry.Analysis != null) {
                var ast = pyEntry.Analysis.GetAstFromText(request.expr, new SourceLocation(request.index, request.line, request.column));
                var expr = Statement.GetExpression(ast.Body);

                if (ast != null) {
                    var walker = new DetectSideEffectsWalker();
                    ast.Walk(walker);
                    if (!walker.HasSideEffects) {
                        dataTipExpression = expr?.ToCodeString(ast, new CodeFormattingOptions() { UseVerbatimImage = false });
                    }
                }
            }

            return new AP.ExpressionForDataTipResponse() {
                expression = dataTipExpression
            };
        }

        private Response AnalyzeExpression(AP.AnalyzeExpressionRequest request) {
            var pyEntry = _projectFiles[request.fileId] as IPythonProjectEntry;
            AP.AnalysisReference[] references;
            string privatePrefix = null;
            string memberName = null;

            if (pyEntry.Tree != null) {
                var w = new ImportedModuleNameWalker(pyEntry.ModuleName, request.index);
                pyEntry.Tree.Walk(w);
                ModuleReference modRef;
                if (!string.IsNullOrEmpty(w.ImportedName) &&
                    _pyAnalyzer.Modules.TryImport(w.ImportedName, out modRef)) {
                    // Return a module reference
                    return new AP.AnalyzeExpressionResponse {
                        variables = modRef.AnalysisModule.Locations
                            .Select(l => MakeReference(l, VariableType.Definition))
                            .ToArray(),
                        memberName = w.ImportedName
                    };
                }
            }

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
                references = new AP.AnalysisReference[0];
            }

            return new AP.AnalyzeExpressionResponse() {
                variables = references,
                privatePrefix = privatePrefix,
                memberName = memberName
            };
        }

        private AP.AnalysisReference MakeReference(IAnalysisVariable arg) {
            var reference = MakeReference(arg.Location, arg.Type);
            reference.definitionStartLine = arg.DefinitionLocation.StartLine;
            reference.definitionStartColumn = arg.DefinitionLocation.StartColumn;
            if (arg.DefinitionLocation.EndLine.HasValue) {
                reference.definitionEndLine = arg.DefinitionLocation.EndLine.Value;
            } else {
                reference.definitionEndLine = arg.DefinitionLocation.StartLine;
            }
            if (arg.DefinitionLocation.EndColumn.HasValue) {
                reference.definitionEndColumn = arg.DefinitionLocation.EndColumn.Value;
            } else {
                reference.definitionEndColumn = arg.DefinitionLocation.StartColumn;
            }
            return reference;
        }

        private AP.AnalysisReference MakeReference(LocationInfo location, VariableType type) {
            return new AP.AnalysisReference() {
                column = location.StartColumn,
                line = location.StartLine,
                kind = GetVariableType(type),
                file = location?.FilePath
            };
        }

        private static string GetVariableType(VariableType type) {
            switch (type) {
                case VariableType.Definition: return "definition";
                case VariableType.Reference: return "reference";
                case VariableType.Value: return "value";
            }
            return null;
        }

        private Response GetQuickInfo(AP.QuickInfoRequest request) {
            var pyEntry = _projectFiles[request.fileId] as IPythonProjectEntry;
            string text = null;

            if (pyEntry.Tree != null) {
                var w = new ImportedModuleNameWalker(pyEntry.ModuleName, request.index);
                pyEntry.Tree.Walk(w);
                if (!string.IsNullOrEmpty(w.ImportedName)) {
                    return new AP.QuickInfoResponse {
                        text = w.ImportedName + ": module"
                    };
                }
            }

            if (pyEntry.Analysis != null) {
                bool first = true;
                var result = new StringBuilder();
                int count = 0;
                var descriptions = new HashSet<string>();
                bool multiline = false;
                bool includeExpression = true;

                var loc = new SourceLocation(request.index, request.line, request.column);
                var exprAst = ModuleAnalysis.GetExpression(pyEntry.Analysis.GetAstFromText(request.expr, loc)?.Body);
                if (exprAst is ConstantExpression || exprAst is ErrorExpression) {
                    includeExpression = false;
                }

                var values = pyEntry.Analysis.GetValues(request.expr, loc);
                var listVars = new List<AnalysisValue>(values);
                
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

                if (includeExpression) {
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
                using (new DebugTimer("GetSignaturesByIndex")) {
                    sigs = pyEntry.Analysis.GetSignaturesByIndex(
                        request.text,
                        request.location
                    );
                }
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
                completions = ToCompletions(members.ToArray(), topLevelCompletions.options)
            };
        }

        private Response GetModules(Request request) {
            var getModules = (AP.GetModulesRequest)request;
            var prefix = getModules.package == null ? null : string.Join(".", getModules.package);

            return new AP.CompletionsResponse() {
                completions = ToCompletions(
                    Analyzer.GetModules(prefix),
                    GetMemberOptions.None
                )
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
                ).MaybeEnumerate();
            } else {
                members = Enumerable.Empty<MemberResult>();
            }

            return new AP.CompletionsResponse() {
                completions = ToCompletions(members.ToArray(), completions.options)
            };
        }

        private static IEnumerable<MemberResult> GetModuleVariables(
            IPythonProjectEntry entry,
            GetMemberOptions opts,
            string prefix
        ) {
            var analysis = entry?.Analysis;
            if (analysis == null) {
                yield break;
            }

            foreach (var m in analysis.GetAllAvailableMembers(SourceLocation.None, opts)) {
                if (m.Values.Any(v => v.DeclaringModule == entry)) {
                    if (string.IsNullOrEmpty(prefix) || m.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
                        yield return m;
                    }
                }
            }
        }

        private Response GetAllMembers(Request request) {
            var req = (AP.GetAllMembersRequest)request;

            var members = Enumerable.Empty<MemberResult>();
            var opts = GetMemberOptions.ExcludeBuiltins | GetMemberOptions.DeclaredOnly | req.options;

            foreach (var entry in _projectFiles) {
                members = members.Concat(
                    GetModuleVariables(entry.Value as IPythonProjectEntry, opts, req.prefix)
                );
            }

            members = members.GroupBy(mr => mr.Name).Select(g => g.First());

            return new AP.CompletionsResponse() {
                completions = ToCompletions(members.ToArray(), opts)
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

        private AP.Completion[] ToCompletions(MemberResult[] memberResult, GetMemberOptions options) {
            AP.Completion[] res = new AP.Completion[memberResult.Length];
            for (int i = 0; i < memberResult.Length; i++) {
                var member = memberResult[i];

                res[i] = new AP.Completion() {
                    name = member.Name,
                    completion = member.Completion,
                    doc = member.Documentation,
                    memberType = member.MemberType
                };

                if (options.HasFlag(GetMemberOptions.DetailedInformation)) {
                    List<AP.CompletionValue> values = new List<AnalysisProtocol.CompletionValue>();

                    foreach (var value in member.Values) {
                        var descComps = Array.Empty<AP.DescriptionComponent>();
                        var hasDesc = value as IHasRichDescription;
                        if (hasDesc != null) {
                            descComps = hasDesc
                                .GetRichDescription()
                                .Select(kv => new AP.DescriptionComponent(kv.Value, kv.Key))
                                .ToArray();
                        }
                        values.Add(
                            new AP.CompletionValue() {
                                description = descComps,
                                doc = value.Documentation,
                                locations = value.Locations.Select(x => MakeReference(x, VariableType.Definition)).ToArray()
                            }
                        );
                    }
                    res[i].detailedValues = values.ToArray();
                }
            }
            return res;
        }

        private async Task AnalyzeFileAsync(AP.AddFileRequest request, Func<Response, Task> done) {
            int fileId;
            var entry = AddNewFile(request.path, request.addingFromDir, out fileId);

            await done(new AP.AddFileResponse { fileId = fileId });

            if (entry != null) {
                await BeginAnalyzingFileAsync(entry, fileId, request.isTemporaryFile, request.suppressErrorLists);
            }
        }

        private async Task AnalyzeFileAsync(AP.AddBulkFileRequest request, Func<Response, Task> done) {
            var entries = new IProjectEntry[request.path.Length];
            var response = new AP.AddBulkFileResponse {
                fileId = Enumerable.Repeat(-1, request.path.Length).ToArray()
            };

            for(int i = 0; i < request.path.Length; ++i) {
                if (!string.IsNullOrEmpty(request.path[i])) {
                    entries[i] = AddNewFile(request.path[i], request.addingFromDir, out response.fileId[i]);
                }
            }

            await done(response);

            for (int i = 0; i < entries.Length; ++i) {
                if (entries[i] != null) {
                    await BeginAnalyzingFileAsync(entries[i], response.fileId[i], false, false);
                }
            }
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

        private Response GetOverrides(AP.OverridesCompletionRequest request) {
            var projectFile = _projectFiles[request.fileId] as IPythonProjectEntry;
            var analysis = projectFile.Analysis;
            if (analysis != null) {

                var location = new SourceLocation(request.index, request.line, request.column);

                var cls = analysis.GetDefinitionTree(location).LastOrDefault(member => member.MemberType == PythonMemberType.Class);
                var members = analysis.GetOverrideable(location).ToArray();

                return new AP.OverridesCompletionResponse() {
                    overrides = members
                        .Select(member => new AP.Override() {
                            name = member.Name,
                            doc = member.Documentation,
                            completion = MakeCompletionString(request, member, cls.Name)
                        }).ToArray()
                };
            }
            return new AP.OverridesCompletionResponse() {
                overrides = Array.Empty<AP.Override>()
            };
        }

        private static readonly Regex ValidParameterName = new Regex(@"^(\*|\*\*)?[a-z_][a-z0-9_]*", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static string GetSafeParameterName(ParameterResult result, int index) {
            if (!string.IsNullOrEmpty(result.DefaultValue)) {
                return GetSafeArgumentName(result, index) + " = " + result.DefaultValue;
            }
            return GetSafeArgumentName(result, index);
        }

        private static string GetSafeArgumentName(ParameterResult result, int index) {
            var match = ValidParameterName.Match(result.Name);

            if (match.Success) {
                return match.Value;
            } else if (result.Name.StartsWith("**")) {
                return "**kwargs";
            } else if (result.Name.StartsWith("*")) {
                return "*args";
            } else {
                return "arg" + index.ToString();
            }
        }

        private string MakeCompletionString(AP.OverridesCompletionRequest request, IOverloadResult result, string className) {
            var sb = new StringBuilder();

            sb.AppendLine(result.Name + "(" + string.Join(", ", result.Parameters.Select((p, i) => GetSafeParameterName(p, i))) + "):");

            sb.Append(request.indentation);

            if (result.Parameters.Length > 0) {
                var parameterString = string.Join(", ", result.Parameters.Skip(1).Select((p, i) => GetSafeArgumentName(p, i + 1)));

                if (InterpreterFactory.GetLanguageVersion().Is3x()) {
                    sb.AppendFormat("return super().{0}({1})",
                        result.Name,
                        parameterString);
                } else if (!string.IsNullOrEmpty(className)) {
                    sb.AppendFormat("return super({0}, {1}).{2}({3})",
                        className,
                        result.Parameters.First().Name,
                        result.Name,
                        parameterString);
                } else {
                    sb.Append("pass");
                }
            } else {
                sb.Append("pass");
            }

            return sb.ToString();
        }

        private Response UpdateContent(AP.FileUpdateRequest request) {
            var entry = _projectFiles[request.fileId];
            if (entry == null) {
                return new AP.FileUpdateResponse() {
                    failed = true
                };
            }

            SortedDictionary<int, CodeInfo> codeByBuffer = new SortedDictionary<int, CodeInfo>();
#if DEBUG
            Dictionary<int, string> newCode = new Dictionary<int, string>();
#endif
            foreach (var update in request.updates) {
                switch (update.kind) {
                    case AP.FileUpdateKind.changes:
                        if (entry != null) {
                            var newCodeStr = entry.UpdateCode(
                                update.versions,
                                update.bufferId,
                                update.version
                            );

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
                        entry.SetCurrentCode(update.content, update.bufferId, update.version);
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

            using (new DebugTimer("GetCompletions")) {
                return GetNormalCompletions(file, request);
            }
        }

        internal Task ProcessMessages() {
            return _connection.ProcessMessages();
        }

        public AP.OptionsChangedEvent Options {
            get {
                return _options;
            }
        }

        private async void AnalysisQueue_Complete(object sender, EventArgs e) {
            if (_connection == null) {
                return;
            }
            await _connection.SendEventAsync(new AP.AnalysisCompleteEvent()).ConfigureAwait(false);
        }

        private async void OnModulesChanged(object sender, EventArgs args) {
            if (_pyAnalyzer == null) {
                Debug.Fail("Should not have null _pyAnalyzer here");
                return;
            }

            await _pyAnalyzer.ReloadModulesAsync();

            // re-analyze all of the modules when we get a new set of modules loaded...
            foreach (var entry in _pyAnalyzer.ModulesByFilename) {
                _analysisQueue.Enqueue(entry.Value.ProjectEntry, AnalysisPriority.Normal);
            }
        }

        private bool ShouldAnalyzePath(string path) {
            foreach (var config in _allConfigs) {
                if (PathUtils.IsValidPath(config.InterpreterPath) &&
                    PathUtils.IsSubpathOf(Path.GetDirectoryName(config.InterpreterPath), path)) {
                    return false;
                }
            }
            return true;
        }

        private IProjectEntry AddNewFile(string path, string addingFromDirectory, out int fileId) {
            if (_pyAnalyzer == null) {
                Debug.Fail("AnalyzeNewFile should only be called when _pyAnalyzer exists");
                fileId = -1;
                return null;
            }

            IProjectEntry item = null;
            IPythonProjectEntry pyItem = null;
            if (_projectFiles.TryGetValue(path, out item)) {
                // If the module exists, we may be adding an alias to it.
                if (addingFromDirectory != null &&
                    (pyItem = item as IPythonProjectEntry) != null &&
                    ModulePath.IsPythonSourceFile(path)) {
                    string modName = null;
                    try {
                        modName = ModulePath.FromFullPath(path, addingFromDirectory).ModuleName;
                    } catch (ArgumentException) {
                        // Module does not have a valid name, so we can't make
                        // an alias for it.
                        fileId = -1;
                        return null;
                    }

                    if (modName != null && pyItem.ModuleName != modName) {
                        _pyAnalyzer.AddModuleAlias(pyItem.ModuleName, modName);

                        var reanalyzeEntries = _pyAnalyzer.GetEntriesThatImportModule(modName, true).ToArray();
                        foreach (var entryRef in reanalyzeEntries) {
                            _analysisQueue.Enqueue(entryRef, AnalysisPriority.Low);
                        }
                    }
                }

                fileId = ProjectEntryMap.GetId(item);
                return item;
            }

            if (Path.GetExtension(path).Equals(".xaml", StringComparison.OrdinalIgnoreCase)) {
                item = _pyAnalyzer.AddXamlFile(path, null);

            } else {
                // assume it's a Python file...
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
                    reanalyzeEntries = _pyAnalyzer.GetEntriesThatImportModule(modName, true).ToArray();
                }

                pyItem = _pyAnalyzer.AddModule(modName, path, null);
                pyItem.OnNewAnalysis += OnNewAnalysis;

                pyItem.BeginParsingTree();

                if (reanalyzeEntries != null) {
                    foreach (var entryRef in reanalyzeEntries) {
                        _analysisQueue.Enqueue(entryRef, AnalysisPriority.Low);
                    }
                }

                item = pyItem;
            }

            if (item != null) {
                fileId = _projectFiles.Add(path, item);
            } else {
                fileId = -1;
            }

            return item;
        }

        internal async Task BeginAnalyzingFileAsync(IProjectEntry item, int fileId, bool isTemporaryFile, bool suppressErrorList) {
            if (_pyAnalyzer == null) {
                // We aren't able to analyze code, so don't create an entry.
                return;
            }

            // Send a notification for this file before starting analysis
            // An AnalyzeFile event will send the same details in its
            // response.
            await _connection.SendEventAsync(new AP.ChildFileAnalyzed() {
                fileId = fileId >= 0 ? fileId : ProjectEntryMap.GetId(item),
                filename = item.FilePath,
                isTemporaryFile = isTemporaryFile,
                suppressErrorList = suppressErrorList
            });

            EnqueueFile(item, item.FilePath);
        }

        private async void OnNewAnalysis(object sender, EventArgs e) {
            var projEntry = sender as IPythonProjectEntry;
            if (projEntry != null) {
                var fileId = ProjectEntryMap.GetId(projEntry);
                PythonAst dummy;
                IAnalysisCookie cookieTmp;
                projEntry.GetTreeAndCookie(out dummy, out cookieTmp);
                var cookie = (VersionCookie)cookieTmp;

                var versions = cookie.Buffers.Select(x => new AP.BufferVersion() {
                    bufferId = x.Key,
                    version = x.Value.Version
                }).ToArray();

                await _connection.SendEventAsync(
                    new AP.FileAnalysisCompleteEvent() {
                        fileId = fileId,
                        versions = versions
                    }
                );
            }
        }

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

        internal IPythonInterpreterFactory InterpreterFactory {
            get {
                return _interpreterFactory;
            }
        }

        internal IPythonInterpreter Interpreter {
            get {
                return _pyAnalyzer?.Interpreter;
            }
        }

        // Returns the current analyzer or throws InvalidOperationException.
        // This should be used in request handlers that should fail when
        // analysis is impossible. Callers that explicitly check for null before
        // use should use _pyAnalyzer directly.
        private PythonAnalyzer Analyzer {
            get {
                if (_pyAnalyzer == null) {
                    throw new InvalidOperationException("Unable to analyze code");
                }

                return _pyAnalyzer;
            }
        }

        /// <summary>
        /// Returns the current analyzer or null if unable to analyze code.
        /// </summary>
        /// <remarks>
        /// This is for public consumption only and should not be used within
        /// <see cref="OutOfProcProjectAnalyzer"/>.
        /// </remarks>
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
                foreach (var keyValue in buffers) {
                    externalEntry.ParseContent(keyValue.Value.GetReader(), null);
                    _analysisQueue.Enqueue(entry, AnalysisPriority.Normal);
                }
            }
        }

        private static void UpdateAnalysisTree(IPythonProjectEntry pyEntry, SortedDictionary<int, ParseResult> parseResults) {
            IAnalysisCookie cookie = new VersionCookie(
                parseResults.ToDictionary(
                    x => x.Key,
                    x => new BufferVersion(x.Value.Version, x.Value.Ast)
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
                    new NewLineLocation[0],
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
                        x => new AP.BufferParseInfo() {
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


        private AP.CompletionsResponse GetNormalCompletions(IProjectEntry projectEntry, AP.CompletionsRequest request) {
            int version;
            var code = projectEntry.GetCurrentCode(request.bufferId, out version);

            if (IsSpaceCompletion(code, request.location) && !request.forceCompletions) {
                return new AP.CompletionsResponse() {
                    completions = new AP.Completion[0]
                };
            }

            var analysis = ((IPythonProjectEntry)projectEntry).Analysis;
            if (analysis != null) {
                var members = analysis.GetMembers(
                    request.text,
                    new SourceLocation(
                        request.location,
                        1,
                        request.column
                    ),
                    request.options
                ).MaybeEnumerate();

                return new AP.CompletionsResponse() {
                    completions = ToCompletions(members.ToArray(), request.options)
                };
            }
            return new AP.CompletionsResponse() {
                completions = Array.Empty<AP.Completion>()
            };
        }

        private bool IsSpaceCompletion(string text, int location) {
            if (location > 0 && location < text.Length - 1) {
                return text[location - 1] == ' ';
            }
            return false;
        }

        internal void Cancel() {
            _analysisQueue.Stop();
        }

        internal void UnloadFile(IProjectEntry entry) {
            if (entry != null) {
                // If we remove a Python module, reanalyze any other modules
                // that referenced it.
                IPythonProjectEntry[] reanalyzeEntries = null;
                var pyEntry = entry as IPythonProjectEntry;
                if (pyEntry != null && !string.IsNullOrEmpty(pyEntry.ModuleName)) {
                    reanalyzeEntries = Analyzer.GetEntriesThatImportModule(pyEntry.ModuleName, false).ToArray();
                }

                Analyzer.RemoveModule(entry);
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
                    SendParseComplete(pyEntry, new SortedDictionary<int, ParseResult>());
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
            if (_isDisposed) {
                return;
            }

            _isDisposed = true;
            _analysisQueue.AnalysisComplete -= AnalysisQueue_Complete;
            _analysisQueue.Dispose();
            if (_pyAnalyzer != null) {
                _pyAnalyzer.Interpreter.ModuleNamesChanged -= OnModulesChanged;
                _pyAnalyzer.Dispose();
            }

            _queueActivityEvent.Dispose();
            _connection.Dispose();
            _container.Dispose();
            _catalog.Dispose();
        }

        #endregion

        internal void RemoveReference(ProjectAssemblyReference reference) {
            var interp = Interpreter as IPythonInterpreterWithProjectReferences;
            if (interp != null) {
                interp.RemoveReference(reference);
            }
        }

    }
}

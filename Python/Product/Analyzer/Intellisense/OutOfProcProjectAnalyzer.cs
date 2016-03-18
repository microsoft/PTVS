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
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Ipc.Json;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

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
        // For entries that were loaded from a .zip file, IProjectEntry.Properties[_zipFileName] contains the full path to that archive.
        private static readonly object _zipFileName = new { Name = "ZipFileName" };

        // For entries that were loaded from a .zip file, IProjectEntry.Properties[_pathInZipFile] contains the path of the item inside the archive.
        private static readonly object _pathInZipFile = new { Name = "PathInZipFile" };

        private readonly AnalysisQueue _analysisQueue;
        private IPythonInterpreterFactory _interpreterFactory;
        //private readonly Dictionary<BufferParser, IProjectEntry> _openFiles = new Dictionary<BufferParser, IProjectEntry>();
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

        // Moniker strings allow the task provider to distinguish between
        // different sources of items for the same file.
        private const string ParserTaskMoniker = "Parser";
        internal const string UnresolvedImportMoniker = "UnresolvedImport";
        private readonly HashSet<IAnalysisExtension> _registeredExtensions = new HashSet<IAnalysisExtension>();
        private readonly Dictionary<string, IAnalysisExtension> _extensionsByName = new Dictionary<string, IAnalysisExtension>();

        private readonly Connection _connection;
        internal Task ReloadTask;

        internal OutOfProcProjectAnalyzer(Stream writer, Stream reader) {
            _analysisQueue = new AnalysisQueue(this);
            _analysisQueue.AnalysisComplete += AnalysisQueue_Complete;
            _options = new AP.OptionsChangedEvent() {
                indentation_inconsistency_severity = Severity.Ignore
            };

            _projectFiles = new ProjectEntryMap();
            _connection = new Connection(writer, reader, RequestHandler, AP.RegisteredTypes);
            _connection.EventReceived += ConectionReceivedEvent;

            _catalog = new AggregateCatalog();
            _container = new CompositionContainer(_catalog);
            _container.ExportsChanged += ContainerExportsChanged;
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

            // inform them of the analyzer...
            foreach (var extension in newExtensions) {
                extension.Register(_pyAnalyzer);
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

            switch (command) {
                case AP.UnloadFileRequest.Command: response = UnloadFile((AP.UnloadFileRequest)request); break;
                case AP.AddFileRequest.Command: response = AnalyzeFile((AP.AddFileRequest)request); break;
                case AP.TopLevelCompletionsRequest.Command: response = GetTopLevelCompletions(request); break;
                case AP.CompletionsRequest.Command: response = GetCompletions(request); break;
                case AP.GetModulesRequest.Command: response = GetModules(request); break;
                case AP.GetModuleMembers.Command: response = GeModuleMembers(request); break;
                case AP.SignaturesRequest.Command: response = GetSignatures((AP.SignaturesRequest)request); break;
                case AP.QuickInfoRequest.Command: response = GetQuickInfo((AP.QuickInfoRequest)request); break;
                case AP.AnalyzeExpressionRequest.Command: response = AnalyzeExpression((AP.AnalyzeExpressionRequest)request); break;
                case AP.OutlingRegionsRequest.Command: response = GetOutliningRegions((AP.OutlingRegionsRequest)request); break;
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
                case AP.GetReferencesRequest.Command: response = GetReferences((AP.GetReferencesRequest)request); break;
                case AP.AddZipArchiveRequest.Command: response = AddZipArchive((AP.AddZipArchiveRequest)request); break;
                case AP.AddDirectoryRequest.Command: response = AddDirectory((AP.AddDirectoryRequest)request); break;
                case AP.ModuleImportsRequest.Command: response = GetModuleImports((AP.ModuleImportsRequest)request); break;
                case AP.ValueDescriptionRequest.Command: response = GetValueDescriptions((AP.ValueDescriptionRequest)request); break;
                case AP.ExtensionRequest.Command: response = ExtensionRequest((AP.ExtensionRequest)request); break;
                case AP.InitializeRequest.Command: response = Initialize((AP.InitializeRequest)request); break;
                default:
                    throw new InvalidOperationException("Unknown command");
            }

            await done(response);
        }

        private Response Initialize(AP.InitializeRequest request) {
            List<AssemblyCatalog> catalogs = new List<AssemblyCatalog>();

            List<string> failures = new List<string>();
            string error = null;
            foreach (var asm in request.mefExtensions) {
                try {
                    var asmCatalog = new AssemblyCatalog(asm);
                    _catalog.Catalogs.Add(asmCatalog);
                } catch (Exception e) {
                    failures.Add(String.Format("Failed to load {0}: {1}", asm, e));
                }
            }

            _catalog.Catalogs.Add(new AssemblyCatalog(GetType().Assembly));

            if (request.projectFile != null) {
                var projectContextProvider = _container.GetExportedValue<OutOfProcProjectContextProvider>();
                projectContextProvider.AddContext(request.projectFile);
            }

            IPythonInterpreterFactory factory = null;
            Version analysisVersion;
            if (request.interpreterId.StartsWith("AnalysisOnly;")) {
                int versionStart = request.interpreterId.IndexOf(';') + 1;
                int versionEnd = request.interpreterId.IndexOf(';', versionStart);

                if (Version.TryParse(request.interpreterId.Substring(versionStart, versionEnd - versionStart), out analysisVersion)) {
                    string[] dbDirs;
                    if (versionEnd + 1 != request.interpreterId.Length) {
                        dbDirs = request.interpreterId.Substring(versionEnd + 1).Split(';');
                    } else {
                        dbDirs = null;
                    }
                    factory = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(analysisVersion, null, dbDirs);
                }
            } else {
                factory = _container.GetInterpreterFactory(request.interpreterId);
            }

            if (factory == null) {
                error = String.Format("No active interpreter found for interpreter ID: {0}", request.interpreterId);
                return new AP.InitializeResponse() {
                    failedLoads = failures.ToArray(),
                    error = error
                };
            }

            _interpreterFactory = factory;
            _allConfigs = _container.GetConfigurations().Values.ToArray();

            var interpreter = factory.CreateInterpreter();
            if (interpreter != null) {
                _pyAnalyzer = PythonAnalyzer.Create(factory, interpreter);
                ReloadTask = _pyAnalyzer.ReloadModulesAsync()/*.HandleAllExceptions(_serviceProvider, GetType())*/;
                ReloadTask.ContinueWith(_ => ReloadTask = null);
                interpreter.ModuleNamesChanged += OnModulesChanged;
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
            var res = Project.GetEntriesThatImportModule(
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

        private Response AddDirectory(AP.AddDirectoryRequest request) {
            AnalyzeDirectory(
                request.dir,
                entry => {
                    _connection.SendEventAsync(
                        new AP.ChildFileAnalyzed() {
                            parent = request.dir,
                            filename = entry.FilePath
                        }
                    ).Wait();
                }
            );

            return new Response();
        }

        private Response AddZipArchive(AP.AddZipArchiveRequest request) {
            AnalyzeZipArchive(
                request.archive,
                entry => {
                    _connection.SendEventAsync(
                        new AP.ChildFileAnalyzed() {
                            parent = request.archive,
                            filename = entry.FilePath
                        }
                    ).Wait();
                }
            );

            return new Response();
        }

        private Response GetReferences(AP.GetReferencesRequest request) {
            var interp = Interpreter as IPythonInterpreterWithProjectReferences2;
            AP.ProjectReference[] references = Array.Empty<AP.ProjectReference>();
            if (interp != null) {
                references = interp.GetReferences().Select(AP.ProjectReference.Convert).ToArray();
            }
            return new AP.GetReferencesResponse() {
                references = references
            };
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
                    Project.LanguageVersion,
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

                // remove any leading comments before round tripping, not selecting them
                // gives a nicer overall experience, otherwise we have a selection to the
                // previous line which only covers white space.
                body.SetLeadingWhiteSpace(ast, body.GetIndentationLevel(ast));

                var selectedCode = code.Substring(
                    walker.Target.StartIncludingIndentation,
                    walker.Target.End - walker.Target.StartIncludingIndentation
                );

                return new AP.FormatCodeResponse() {
                    startIndex = walker.Target.StartIncludingIndentation,
                    endIndex = walker.Target.End,
                    version = version,
                    changes = selectedCode.ReplaceByLines(
                        body.ToCodeString(ast, request.options),
                        request.newLine
                    )
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
                imports = _pyAnalyzer.FindNameInAllModules(request.name)
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
                    _pyAnalyzer
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

            PythonAst ast;
            IAnalysisCookie cookie;
            entry.GetTreeAndCookie(out ast, out cookie);

            var versions = cookie as VersionCookie;
            BufferVersion versionInfo;
            if (versions != null && versions.Buffers.TryGetValue(bufferId, out versionInfo)) {
                return versionInfo;
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

        private Response GetOutliningRegions(AP.OutlingRegionsRequest request) {
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

        private Response AnalyzeExpression(AP.AnalyzeExpressionRequest request) {
            var pyEntry = _projectFiles[request.fileId] as IPythonProjectEntry;
            AP.AnalysisReference[] references;
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
                references = new AP.AnalysisReference[0];
            }

            return new AP.AnalyzeExpressionResponse() {
                variables = references,
                privatePrefix = privatePrefix,
                memberName = memberName
            };
        }

        private AP.AnalysisReference MakeReference(IAnalysisVariable arg) {
            return MakeReference(arg.Location, arg.Type);
        }

        private AP.AnalysisReference MakeReference(LocationInfo location, VariableType type) {
            return new AP.AnalysisReference() {
                column = location.Column,
                line = location.Line,
                kind = GetVariableType(type),
                file = GetFile(location.ProjectEntry)
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
                case VariableType.Reference: return "reference";
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
                completions = ToCompletions(members.ToArray(), topLevelCompletions.options)
            };
        }

        private Response GeModuleMembers(Request request) {
            var getModuleMembers = (AP.GetModuleMembers)request;

            return new AP.CompletionsResponse() {
                completions = ToCompletions(
                    _pyAnalyzer.GetModuleMembers(
                        _projectFiles[getModuleMembers.fileId].AnalysisContext,
                        getModuleMembers.package,
                        getModuleMembers.includeMembers
                    ),
                    GetMemberOptions.None
                )
            };
        }

        private Response GetModules(Request request) {
            var getModules = (AP.GetModulesRequest)request;

            return new AP.CompletionsResponse() {
                completions = ToCompletions(
                    _pyAnalyzer.GetModules(getModules.topLevelOnly),
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
                );
            } else {
                members = Enumerable.Empty<MemberResult>();
            }

            return new AP.CompletionsResponse() {
                completions = ToCompletions(members.ToArray(), completions.options)
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
                        var descComps = new List<AP.DescriptionComponent>();
                        if (value is FunctionInfo) {
                            var def = ((FunctionInfo)value).FunctionDefinition;
                            ((FunctionInfo)value).GetDescription((text, kind) => {
                                descComps.Add(new AP.DescriptionComponent(text, kind));
                            });
                        } else if (value is ClassInfo) {
                            FillClassDescription(descComps, ((ClassInfo)value).ClassDefinition);
                        }
                        values.Add(
                            new AP.CompletionValue() {
                                description = descComps.ToArray(),
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

        private static void FillClassDescription(List<AP.DescriptionComponent> description, ClassDefinition classDef) {
            description.Add(new AP.DescriptionComponent("class ", "misc"));
            description.Add(new AP.DescriptionComponent(classDef.Name, "name"));
            if (classDef.Bases.Count > 0) {
                description.Add(new AP.DescriptionComponent("(", "misc"));
                bool comma = false;
                foreach (var baseClass in classDef.Bases) {
                    if (comma) {
                        description.Add(new AP.DescriptionComponent(", ", "misc"));
                    }

                    string baseStr = FormatExpression(baseClass.Expression);
                    if (baseStr != null) {
                        description.Add(new AP.DescriptionComponent(baseStr, "type"));
                    }

                    comma = true;
                }
                description.Add(new AP.DescriptionComponent(")", "misc"));
            }

            description.Add(new AP.DescriptionComponent("\n", "misc"));
            description.Add(new AP.DescriptionComponent(null, "enddecl"));

            if (!String.IsNullOrWhiteSpace(classDef.Body.Documentation)) {
                description.Add(new AP.DescriptionComponent("    " + classDef.Body.Documentation, "misc"));
            }
        }

        private static string FormatExpression(Expression baseClass) {
            NameExpression ne = baseClass as NameExpression;
            if (ne != null) {
                return ne.Name;
            }

            MemberExpression me = baseClass as MemberExpression;
            if (me != null) {
                string expr = FormatExpression(me.Target);
                if (expr != null) {
                    return expr + "." + me.Name ?? string.Empty;
                }
            }

            return null;
        }

        private Response AnalyzeFile(AP.AddFileRequest request) {
            var entry = AnalyzeFile(request.path, request.addingFromDir);

            if (entry != null) {
                return new AP.AddFileResponse() {
                    fileId = ProjectEntryMap.GetId(entry)
                };
            }

            return new AP.AddFileResponse() {
                fileId = -1
            };
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

            return GetNormalCompletions(file, request);
        }

        internal Task ProcessMessages() {
            return _connection.ProcessMessages();
        }

        public AP.OptionsChangedEvent Options {
            get {
                return _options;
            }
        }

        private void AnalysisQueue_Complete(object sender, EventArgs e) {
            _connection?.SendEventAsync(new AP.AnalysisCompleteEvent());
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
                );

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
                    SendParseComplete(pyEntry, new SortedDictionary<int, ParseResult>());
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
            _analysisQueue.AnalysisComplete -= AnalysisQueue_Complete;
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

    }
}

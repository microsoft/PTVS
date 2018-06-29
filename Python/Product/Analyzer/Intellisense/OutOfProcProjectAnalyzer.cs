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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Ipc.Json;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.PythonTools.Projects;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Intellisense {
    using AP = AnalysisProtocol;
    using LS = Microsoft.PythonTools.Analysis.LanguageServer;

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
        private readonly LS.Server _server;
        private readonly Dictionary<string, IAnalysisExtension> _extensions;
        private readonly Action<string> _log;

        private bool _isDisposed;

        // Moniker strings allow the task provider to distinguish between
        // different sources of items for the same file.
        private const string ParserTaskMoniker = "Parser";
        internal const string UnresolvedImportMoniker = "UnresolvedImport";

        private readonly Connection _connection;

        internal OutOfProcProjectAnalyzer(Stream writer, Stream reader, Action<string> log) {
            _server = new LS.Server();
            _server.OnParseComplete += OnParseComplete;
            _server.OnAnalysisComplete += OnAnalysisComplete;
            _server.OnLogMessage += Server_OnLogMessage;
            _server.OnPublishDiagnostics += OnPublishDiagnostics;
            _server._queue.AnalysisComplete += AnalysisQueue_Complete;
            _server._queue.AnalysisAborted += AnalysisQueue_Aborted;

            _log = log;
            Options = new AP.AnalysisOptions();

            _extensions = new Dictionary<string, IAnalysisExtension>();

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
            if (!string.IsNullOrEmpty(_connection.LogFilename)) {
                _log?.Invoke($"Connection log: {_connection.LogFilename}");
            }
        }

        private void Server_OnLogMessage(object sender, LS.LogMessageEventArgs e) {
            if (_log != null && Options.traceLevel.HasValue && e.type <= Options.traceLevel.Value) {
                _log(e.message);
                _connection?.SendEventAsync(new AP.AnalyzerWarningEvent { message = e.message }).DoNotWait();
            }
        }

        private void AnalysisQueue_Aborted(object sender, EventArgs e) {
            _connection.Dispose();
        }

        private void ConectionReceivedEvent(object sender, EventReceivedEventArgs e) {
            switch (e.Event.name) {
                case AP.ModulesChangedEvent.Name: OnModulesChanged(this, EventArgs.Empty); break;
                case AP.FileChangedEvent.Name: OnFileChanged((AP.FileChangedEvent)e.Event); break;
            }
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
                case AP.UnloadFileRequest.Command: response = await UnloadFile((AP.UnloadFileRequest)request); break;
                case AP.CompletionsRequest.Command: response = await GetCompletions(request); break;
                case AP.GetAllMembersRequest.Command: response = await GetAllMembers(request); break;
                case AP.GetModulesRequest.Command: response = await GetModules(request); break;
                case AP.SignaturesRequest.Command: response = await GetSignatures((AP.SignaturesRequest)request); break;
                case AP.QuickInfoRequest.Command: response = await GetQuickInfo((AP.QuickInfoRequest)request); break;
                case AP.AnalyzeExpressionRequest.Command: response = await AnalyzeExpression((AP.AnalyzeExpressionRequest)request); break;
                case AP.OutliningRegionsRequest.Command: response = GetOutliningRegions((AP.OutliningRegionsRequest)request); break;
                case AP.NavigationRequest.Command: response = GetNavigations((AP.NavigationRequest)request); break;
                case AP.FileUpdateRequest.Command: response = await UpdateContent((AP.FileUpdateRequest)request); break;
                case AP.AddImportRequest.Command: response = AddImportRequest((AP.AddImportRequest)request); break;
                case AP.IsMissingImportRequest.Command: response = IsMissingImport((AP.IsMissingImportRequest)request); break;
                case AP.AvailableImportsRequest.Command: response = AvailableImports((AP.AvailableImportsRequest)request); break;
                case AP.FormatCodeRequest.Command: response = FormatCode((AP.FormatCodeRequest)request); break;
                case AP.RemoveImportsRequest.Command: response = RemoveImports((AP.RemoveImportsRequest)request); break;
                case AP.ExtractMethodRequest.Command: response = ExtractMethod((AP.ExtractMethodRequest)request); break;
                case AP.AnalysisStatusRequest.Command: response = AnalysisStatus(); break;
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
                case AP.LoadExtensionRequest.Command: response = LoadExtensionRequest((AP.LoadExtensionRequest)request); break;
                case AP.ExtensionRequest.Command: response = ExtensionRequest((AP.ExtensionRequest)request); break;
                case AP.ExpressionAtPointRequest.Command: response = ExpressionAtPoint((AP.ExpressionAtPointRequest)request); break;
                case AP.InitializeRequest.Command: response = await Initialize((AP.InitializeRequest)request); break;
                case AP.SetAnalysisOptionsRequest.Command: response = SetAnalysisOptions((AP.SetAnalysisOptionsRequest)request); break;
                case AP.LanguageServerRequest.Command: response = await ProcessLanguageServerRequest((AP.LanguageServerRequest)request); break;
                case AP.ExitRequest.Command: throw new OperationCanceledException();
                default:
                    throw new InvalidOperationException("Unknown command");
            }

            await done(response);
        }

        private async Task<Response> ProcessLanguageServerRequest(AP.LanguageServerRequest request) {
            try {
                var body = (Newtonsoft.Json.Linq.JObject)request.body;

                switch (request.name) {
                    case "textDocument/completion": return new AP.LanguageServerResponse { body = await _server.Completion(body.ToObject<LS.CompletionParams>()) };
                }

                return new AP.LanguageServerResponse { error = "Unknown command: " + request.name };
            } catch (Exception ex) {
                return new AP.LanguageServerResponse { error = ex.ToString() };
            }
        }

        internal void ReportUnhandledException(Exception ex) {
            SendUnhandledExceptionAsync(ex).DoNotWait();
            // Allow some time for the other threads to write the event before
            // we (probably) come crashing down.
            Thread.Sleep(100);
        }

        private async Task SendUnhandledExceptionAsync(Exception ex) {
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

        private Response IncorrectFileType() {
            throw new InvalidOperationException("File was not correct type");
        }

        private Response IncorrectBufferId(Uri documentUri) {
            throw new InvalidOperationException($"Buffer was not valid in file {documentUri?.AbsoluteUri ?? "(null)"}");
        }

        private IPythonInterpreterFactory LoadInterpreterFactory(AP.InterpreterInfo info) {
            if (string.IsNullOrEmpty(info?.assembly) || string.IsNullOrEmpty(info?.typeName)) {
                return null;
            }

            var assembly = File.Exists(info.assembly) ? AssemblyName.GetAssemblyName(info.assembly) : new AssemblyName(info.assembly);
            var type = Assembly.Load(assembly).GetType(info.typeName, true);

            return (IPythonInterpreterFactory)Activator.CreateInstance(
                type,
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance,
                null,
                new object[] { info.properties },
                CultureInfo.CurrentCulture
            );
        }

        private IAnalysisExtension LoadAnalysisExtension(AP.LoadExtensionRequest info) {
            if (string.IsNullOrEmpty(info?.assembly) || string.IsNullOrEmpty(info?.typeName)) {
                return null;
            }

            var assembly = File.Exists(info.assembly) ? AssemblyName.GetAssemblyName(info.assembly) : new AssemblyName(info.assembly);
            var type = Assembly.Load(assembly).GetType(info.typeName, true);

            return (IAnalysisExtension)Activator.CreateInstance(
                type,
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance,
                null,
                new object[0],
                CultureInfo.CurrentCulture
            );
        }

        private async Task<Response> Initialize(AP.InitializeRequest request) {
            try {
                await _server.Initialize(new LS.InitializeParams {
                    rootUri = request.rootUri,
                    initializationOptions = new LS.PythonInitializationOptions {
                        interpreter = new LS.PythonInitializationOptions.Interpreter {
                            assembly = request.interpreter?.assembly,
                            typeName = request.interpreter?.typeName,
                            properties = request.interpreter?.properties
                        },
                        displayOptions = new LS.InformationDisplayOptions {
                            maxDocumentationLineLength = 30,
                            trimDocumentationLines = true,
                            maxDocumentationTextLength = 1024,
                            trimDocumentationText = true,
                            maxDocumentationLines = 100
                        }
                    },
                    capabilities = new LS.ClientCapabilities {
                        python = new LS.PythonClientCapabilities {
                            analysisUpdates = true,
                            completionsTimeout = 5000,
                            manualFileLoad = !request.analyzeAllFiles,
                            traceLogging = request.traceLogging,
                            liveLinting = request.liveLinting
                        },
                        textDocument = new LS.TextDocumentClientCapabilities {
                            completion = new LS.TextDocumentClientCapabilities.CompletionCapabilities {
                                completionItem = new LS.TextDocumentClientCapabilities.CompletionCapabilities.CompletionItemCapabilities {
                                    documentationFormat = new[] { LS.MarkupKind.PlainText },
                                    snippetSupport = false
                                }
                            },
                            signatureHelp = new LS.TextDocumentClientCapabilities.SignatureHelpCapabilities {
                                signatureInformation = new LS.TextDocumentClientCapabilities.SignatureHelpCapabilities.SignatureInformationCapabilities {
                                    documentationFormat = new[] { LS.MarkupKind.PlainText },
                                    _shortLabel = true
                                }
                            },
                            hover = new LS.TextDocumentClientCapabilities.HoverCapabilities {
                                contentFormat = new[] { LS.MarkupKind.PlainText }
                            }
                        }
                    }
                });
            } catch (Exception ex) {
                return new AP.InitializeResponse {
                    error = ex.Message,
                    fullError = ex.ToString()
                };
            }

            return new AP.InitializeResponse();
        }

        private Response LoadExtensionRequest(AP.LoadExtensionRequest request) {
            IAnalysisExtension extension, oldExtension;

            if (Project == null) {
                return new AP.LoadExtensionResponse {
                    error = "Uninitialized analyzer",
                    fullError = $"Uninitialized analyzer{Environment.NewLine}{new StackTrace()}"
                };
            }

            try {
                extension = LoadAnalysisExtension(request);
                extension.Register(Project);
            } catch (Exception ex) {
                return new AP.LoadExtensionResponse {
                    error = ex.Message,
                    fullError = ex.ToString()
                };
            }

            lock (_extensions) {
                _extensions.TryGetValue(request.extension, out oldExtension);
                _extensions[request.extension] = extension;
            }
            (oldExtension as IDisposable)?.Dispose();

            return new AP.LoadExtensionResponse();
        }

        private Response ExtensionRequest(AP.ExtensionRequest request) {
            IAnalysisExtension extension;
            lock (_extensions) {
                if (!_extensions.TryGetValue(request.extension, out extension)) {
                    return new AP.ExtensionResponse {
                        error = $"Unknown extension: {request.extension}"
                    };
                }
            }

            try {
                return new AP.ExtensionResponse {
                    response = extension.HandleCommand(request.commandId, request.body)
                };
            } catch (Exception ex) {
                return new AP.ExtensionResponse {
                    error = ex.ToString()
                };
            }
        }

        private Response GetValueDescriptions(AP.ValueDescriptionRequest request) {
            var entry = GetPythonEntry(request.documentUri);
            if (entry == null) {
                return IncorrectFileType();
            }
            string[] descriptions = Array.Empty<string>();
            if (entry.Analysis != null) {
                var values = entry.Analysis.GetValues(
                    request.expr,
                    new SourceLocation(
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
                    documentUri = entry.DocumentUri
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

        private Response FindMethods(AP.FindMethodsRequest request) {
            var analysis = GetPythonEntry(request.documentUri);

            List<string> names = new List<string>();
            if (analysis != null) {
                int version;
                string code;
                var ast = analysis.GetVerbatimAstAndCode(
                    Analyzer.LanguageVersion,
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
                                    if (request.paramCount != null && request.paramCount != funcDef.ParametersInternal.Length) {
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
            var analysis = GetPythonEntry(request.documentUri);
            if (analysis == null) {
                return IncorrectFileType();
            }

            int version;
            string code;
            var ast = analysis.GetVerbatimAstAndCode(
                Analyzer.LanguageVersion,
                out version,
                out code
            );

            if (ast == null) {
                return new AP.MethodInsertionLocationResponse();
            }

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
                    line = ast.IndexToLocation(end).Line,
                    column = classDef.Body.GetStart(ast).Column,
                    version = version
                };
            }

            throw new InvalidOperationException("Failed to find class definition");
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
            var analysis = GetPythonEntry(request.documentUri);

            if (analysis != null) {
                int version;
                string code;
                var ast = analysis.GetVerbatimAstAndCode(
                    Project.LanguageVersion,
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
            var projEntry = GetPythonEntry(request.documentUri);

            if (projEntry == null) {
                return IncorrectFileType();
            }

            var bufferVersion = GetPythonBuffer(request.documentUri);
            if (bufferVersion.Ast == null) {
                return new AP.AnalysisClassificationsResponse();
            }

            var moduleAnalysis = request.colorNames ? projEntry.Analysis : null;

            var walker = new ClassifierWalker(bufferVersion.Ast, moduleAnalysis);
            bufferVersion.Ast.Walk(walker);

            return new AP.AnalysisClassificationsResponse() {
                version = bufferVersion.Version,
                classifications = walker.Spans.Select(s => new AP.AnalysisClassification {
                    startLine = s.Span.Start.Line,
                    startColumn = s.Span.Start.Column,
                    endLine = s.Span.End.Line,
                    endColumn = s.Span.End.Column,
                    type = s.Tag
                }).ToArray()
            };
        }

        private Response GetProximityExpressions(AP.ProximityExpressionsRequest request) {
            var projEntry = GetPythonEntry(request.documentUri);

            var res = new AP.ProximityExpressionsResponse();

            var tree = projEntry?.Tree;
            if (tree == null) {
                return res;
            }

            int startLine = Math.Max(request.line - request.lineCount + 1, 0);
            if (startLine <= request.line) {
                var walker = new ProximityExpressionWalker(tree, startLine, request.line);
                tree.Walk(walker);
                res.names = walker.GetExpressions().ToArray();
            }

            return res;
        }

        private Response GetLocationName(AP.LocationNameRequest request) {
            var projEntry = GetPythonEntry(request.documentUri);

            var res = new AP.LocationNameResponse();

            var tree = projEntry?.Tree;
            if (tree == null) {
                return res;
            }

            string foundName = FindNodeInTree(tree, tree.Body as SuiteStatement, request.line);
            if (foundName != null) {
                res.name = projEntry.ModuleName + "." + foundName;
                res.lineOffset = request.column;
            } else {
                res.name = projEntry.ModuleName;
                res.lineOffset = request.column;
            }

            return res;
        }

        private static string FindNodeInTree(PythonAst tree, SuiteStatement statement, int line) {
            if (statement == null) {
                return null;
            }

            foreach (var node in statement.Statements) {
                if (node is FunctionDefinition funcDef) {
                    var span = funcDef.GetSpan(tree);
                    if (span.Start.Line <= line && line <= span.End.Line) {
                        var res = FindNodeInTree(tree, funcDef.Body as SuiteStatement, line);
                        if (res != null) {
                            return funcDef.Name + "." + res;
                        }
                        return funcDef.Name;
                    }
                } else if (node is ClassDefinition classDef) {
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
            return null;
        }


        private Response AnalysisStatus() {
            return new AP.AnalysisStatusResponse() {
                itemsLeft = _server.EstimateRemainingWork()
            };
        }

        private Response ExtractMethod(AP.ExtractMethodRequest request) {
            var projectFile = GetPythonEntry(request.documentUri);
            if (projectFile == null) {
                return IncorrectFileType();
            }

            int version;
            string code;
            var ast = projectFile.GetVerbatimAstAndCode(
                Project.LanguageVersion,
                out version,
                out code
            );

            return new OutOfProcMethodExtractor(
                ast,
                code
            ).ExtractMethod(request, version);
        }

        private Response RemoveImports(AP.RemoveImportsRequest request) {
            var projectFile = GetPythonEntry(request.documentUri);
            if (projectFile == null) {
                return IncorrectFileType();
            }

            int version;
            string code;
            var ast = projectFile.GetVerbatimAstAndCode(
                Project.LanguageVersion,
                out version,
                out code
            );
            if (ast == null) {
                return new AP.RemoveImportsResponse();
            }
            var remover = new ImportRemover(ast, code, request.allScopes, ast.LocationToIndex(new SourceLocation(request.line, request.column)));

            return new AP.RemoveImportsResponse() {
                changes = remover.RemoveImports().Select(AP.ChangeInfo.FromDocumentChange).ToArray(),
                version = version
            };
        }

        private Response FormatCode(AP.FormatCodeRequest request) {
            var projectFile = GetPythonEntry(request.documentUri);
            if (projectFile == null) {
                return IncorrectFileType();
            }

            int version;
            string code;
            var ast = projectFile.GetVerbatimAstAndCode(
                Project.LanguageVersion,
                out version,
                out code
            );
            if (ast == null) {
                return new AP.FormatCodeResponse();
            }

            int startIndex = ast.LocationToIndex(new SourceLocation(request.startLine, request.startColumn));
            int endIndex = ast.LocationToIndex(new SourceLocation(request.endLine, request.endColumn));

            var walker = new EnclosingNodeWalker(ast, startIndex, endIndex);
            ast.Walk(walker);

            if (walker.Target == null || !walker.Target.IsValidSelection) {
                return new AP.FormatCodeResponse();
            }

            var body = walker.Target.GetNode();


            var whitspaceStart = walker.Target.StartIncludingIndentation;

            int start = ast.LocationToIndex(walker.Target.StartIncludingLeadingWhiteSpace);
            int end = ast.LocationToIndex(walker.Target.End);
            if (startIndex > start) {
                // the user didn't have any comments selected, don't reformat them
                body.SetLeadingWhiteSpace(ast, body.GetIndentationLevel(ast));

                start = ast.LocationToIndex(walker.Target.StartIncludingIndentation);
            }

            int length = end - start;
            if (end < code.Length) {
                if (code[end] == '\r') {
                    end++;
                    length++;
                    if (end < code.Length &&
                        code[end] == '\n') {
                        end++;
                        length++;
                    }
                } else if (code[end] == '\n') {
                    length++;
                }
            }

            var selectedCode = code.Substring(start, length);

            return new AP.FormatCodeResponse() {
                version = version,
                changes = selectedCode.ReplaceByLines(
                    walker.Target.StartIncludingLeadingWhiteSpace.Line,
                    body.ToCodeString(ast, request.options),
                    request.newLine
                ).Select(AP.ChangeInfo.FromDocumentChange).ToArray()
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
                    .Distinct()
                    .ToArray()
            };
        }

        private Response IsMissingImport(AP.IsMissingImportRequest request) {
            var entry = GetPythonEntry(request.documentUri);
            var analysis = entry?.Analysis;
            if (analysis == null) {
                return new AP.IsMissingImportResponse();
            }

            var location = new SourceLocation(request.line, request.column);
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

            return new AP.IsMissingImportResponse();
        }

        private Response AddImportRequest(AP.AddImportRequest request) {
            var projectFile = GetPythonEntry(request.documentUri);
            if (projectFile == null) {
                return IncorrectFileType();
            }

            string name = request.name;
            string fromModule = request.fromModule;

            int version;
            var curAst = projectFile.GetVerbatimAst(Project.LanguageVersion, out version);
            if (curAst == null) {
                return new AP.AddImportResponse();
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
                                changes = new[] { UpdateFromImport(curAst, fromImport, name) },
                                version = version
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
                    AP.ChangeInfo.FromDocumentChange(DocumentChange.Insert(newText, curAst.IndexToLocation(start)))
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

            var newImport = new FromImportStatement((ModuleName)fromImport.Root, names, asNames, fromImport.IsFromFuture, fromImport.ForceAbsolute, -1);
            curAst.CopyAttributes(fromImport, newImport);

            var newCode = newImport.ToCodeString(curAst);

            var span = fromImport.GetSpan(curAst);
            int leadingWhiteSpaceLength = (fromImport.GetLeadingWhiteSpace(curAst) ?? "").Length;
            return AP.ChangeInfo.FromDocumentChange(DocumentChange.Replace(
                new SourceSpan(
                    span.Start.AddColumns(-leadingWhiteSpaceLength),
                    span.End
                ), newCode
            ));
        }

        private static bool IsDocString(ExpressionStatement exprStmt) {
            ConstantExpression constExpr;
            return exprStmt != null &&
                    (constExpr = exprStmt.Expression as ConstantExpression) != null &&
                    (constExpr.Value is string || constExpr.Value is AsciiString);
        }

        private IPythonProjectEntry GetPythonEntry(Uri documentUri) {
            if (documentUri == null) {
                return null;
            }
            return _server.GetEntry(documentUri) as IPythonProjectEntry;
        }

        private VersionedAst GetPythonBuffer(Uri documentUri) {
            var entry = GetPythonEntry(documentUri);
            if (entry == null) {
                return default(VersionedAst);
            }

            var parse = entry.GetCurrentParse();
            var ast = parse?.Tree;
            var cookie = parse?.Cookie;

            if (cookie is VersionCookie vc) {
                int i = _server.GetPart(documentUri);
                if (vc.Versions.TryGetValue(i, out var bv)) {
                    return new VersionedAst { Ast = bv.Ast, Version = bv.Version };
                }
            }
            return new VersionedAst { Ast = ast, Version = 0 };
        }

        private struct VersionedAst {
            public PythonAst Ast;
            public int Version;
        }

        private Response GetOutliningRegions(AP.OutliningRegionsRequest request) {
            var bufferVersion = GetPythonBuffer(request.documentUri);
            if (bufferVersion.Ast == null) {
                return IncorrectBufferId(request.documentUri);
            }

            var walker = new OutliningWalker(bufferVersion.Ast);
            bufferVersion.Ast.Walk(walker);

            return new AP.OutliningRegionsResponse() {
                tags = walker.GetTags().Select(t => new AP.OutliningTag {
                    startLine = t.Span.Start.Line,
                    startCol = t.Span.Start.Column,
                    endLine = t.Span.End.Line,
                    endCol = t.Span.End.Column
                }).ToArray(),
                version = bufferVersion.Version
            };
        }

        private Response GetNavigations(AP.NavigationRequest request) {
            List<AP.Navigation> navs = new List<AP.Navigation>();
            var bufferVersion = GetPythonBuffer(request.documentUri);

            var suite = bufferVersion.Ast?.Body as SuiteStatement;
            if (suite == null) {
                return IncorrectBufferId(request.documentUri);
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
                                nestedNavs.Add(GetNavigation(bufferVersion.Ast, child));
                            }
                        }
                    }

                    var startLoc = classDef.GetStart(bufferVersion.Ast);
                    var endLoc = classDef.GetEnd(bufferVersion.Ast);
                    if (startLoc >= endLoc) {
                        Debug.Fail($"Invalid span on AST node {classDef}");
                        endLoc = bufferVersion.Ast.IndexToLocation(classDef.StartIndex + 1);
                    }

                    navs.Add(new AP.Navigation() {
                        type = "class",
                        name = classDef.Name,
                        startLine = startLoc.Line,
                        startColumn = startLoc.Column,
                        endLine = endLoc.Line,
                        endColumn = endLoc.Column,
                        children = nestedNavs.ToArray()
                    });

                } else if (stmt is FunctionDefinition) {
                    navs.Add(GetNavigation(bufferVersion.Ast, stmt));
                }
            }


            return new AP.NavigationResponse() {
                version = bufferVersion.Version,
                navigations = navs.ToArray()
            };
        }

        private static AP.Navigation GetNavigation(PythonAst ast, Statement stmt) {
            string type, name;
            if (stmt is FunctionDefinition funcDef) {
                name = funcDef.Name;
                type = "function";
                if (funcDef.Decorators != null && funcDef.Decorators.DecoratorsInternal.Length == 1) {
                    foreach (var decorator in funcDef.Decorators.DecoratorsInternal) {
                        if (decorator is NameExpression nameExpr) {
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
            var startLoc = stmt.GetStart(ast);
            var endLoc = stmt.GetEnd(ast);
            if (startLoc >= endLoc) {
                Debug.Fail($"Invalid span on AST node {stmt}");
                endLoc = ast.IndexToLocation(stmt.StartIndex + 1);
            }

            return new AP.Navigation {
                type = type,
                name = name,
                startLine = startLoc.Line,
                startColumn = startLoc.Column,
                endLine = endLoc.Line,
                endColumn = endLoc.Column
            };
        }

        private Response ExpressionAtPoint(AP.ExpressionAtPointRequest request) {
            var buffer = GetPythonBuffer(request.documentUri);
            if (buffer.Ast == null) {
                return null;
            }

            var res = new AP.ExpressionAtPointResponse();
            if (!GetExpressionAtPoint(buffer.Ast, request.line, request.column, request.purpose, out SourceSpan span, out res.type)) {
                return null;
            }
            res.startLine = span.Start.Line;
            res.startColumn = span.Start.Column;
            res.endLine = span.End.Line;
            res.endColumn = span.End.Column;
            res.bufferVersion = buffer.Version;
            return res;
        }

        private bool GetExpressionAtPoint(PythonAst ast, int line, int column, AP.ExpressionAtPointPurpose purpose, out SourceSpan span, out string type) {
            span = default(SourceSpan);
            type = null;

            if (ast == null) {
                return false;
            }

            GetExpressionOptions options;
            switch (purpose) {
                case AP.ExpressionAtPointPurpose.Evaluate:
                    options = GetExpressionOptions.Evaluate;
                    break;
                case AP.ExpressionAtPointPurpose.EvaluateMembers:
                    options = GetExpressionOptions.EvaluateMembers;
                    break;
                case AP.ExpressionAtPointPurpose.Hover:
                    options = GetExpressionOptions.Hover;
                    break;
                case AP.ExpressionAtPointPurpose.FindDefinition:
                    options = GetExpressionOptions.FindDefinition;
                    break;
                case AP.ExpressionAtPointPurpose.Rename:
                    options = GetExpressionOptions.Rename;
                    break;
                default:
                    options = new GetExpressionOptions();
                    break;
            }

            var exprFinder = new ExpressionFinder(ast, options);
            var expr = exprFinder.GetExpression(new SourceLocation(line, column));
            if (expr == null) {
                return false;
            }

            span = expr.GetSpan(ast);
            type = expr.NodeName;
            return true;
        }

        private async Task<Response> AnalyzeExpression(AP.AnalyzeExpressionRequest request) {
            var entry = GetPythonEntry(request.documentUri);
            if (entry == null) {
                return IncorrectFileType();
            }

            var references = await _server.FindReferences(new LS.ReferencesParams {
                textDocument = request.documentUri,
                position = new SourceLocation(request.line, request.column),
                context = new LS.ReferenceContext {
                    includeDeclaration = true,
                    _includeValues = true
                }
            });

            var privatePrefix = entry.Analysis.GetPrivatePrefix(new SourceLocation(request.line, request.column));

            return new AP.AnalyzeExpressionResponse {
                variables = references.Select(MakeReference).ToArray(),
                privatePrefix = privatePrefix
            };
        }

        private AP.AnalysisReference MakeReference(LS.Reference r) {
            var range = (SourceSpan)r.range;

            return new AP.AnalysisReference {
                documentUri = r.uri,
                file = (_server.GetEntry(r.uri, throwIfMissing: false)?.FilePath) ?? r.uri?.LocalPath,
                startLine = range.Start.Line,
                startColumn = range.Start.Column,
                endLine = range.End.Line,
                endColumn = range.End.Column,
                kind = GetVariableType(r._kind),
                version = r._version
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

        private static string GetVariableType(LS.ReferenceKind? type) {
            if (!type.HasValue) {
                return null;
            }
            switch (type.Value) {
                case LS.ReferenceKind.Definition: return "definition";
                case LS.ReferenceKind.Reference: return "reference";
                case LS.ReferenceKind.Value: return "value";
            }
            return null;
        }

        private async Task<Response> GetQuickInfo(AP.QuickInfoRequest request) {
            LS.Hover hover;
            using (new DebugTimer("QuickInfo")) {
                hover = await _server.Hover(new LS.TextDocumentPositionParams {
                    textDocument = request.documentUri,
                    position = new SourceLocation(request.line, request.column),
                    _expr = request.expr,
                });
            }

            return new AP.QuickInfoResponse {
                text = hover.contents.value
            };
        }

        private async Task<Response> GetSignatures(AP.SignaturesRequest request) {
            LS.SignatureHelp sigs;

            using (new DebugTimer("SignatureHelp")) {
                sigs = await _server.SignatureHelp(new LS.TextDocumentPositionParams {
                    textDocument = request.documentUri,
                    position = new SourceLocation(request.line, request.column),
                    _expr = request.text
                });
            }

            return new AP.SignaturesResponse {
                sigs = sigs?.signatures?.Select(
                    s => new AP.Signature {
                        name = s.label,
                        doc = s.documentation?.value,
                        parameters = s.parameters.MaybeEnumerate().Select(
                            p => new AP.Parameter {
                                name = p.label,
                                defaultValue = p._defaultValue,
                                optional = p._isOptional ?? false,
                                doc = p.documentation?.value,
                                type = p._type
                            }
                        ).ToArray()
                    }
                ).ToArray()
            };
        }

        private async Task<Response> GetModules(Request request) {
            var getModules = (AP.GetModulesRequest)request;
            var prefix = getModules.package == null ? null : (string.Join(".", getModules.package));

            var modules = await _server.Completion(new LS.CompletionParams {
                textDocument = getModules.documentUri,
                _expr = prefix,
                context = new LS.CompletionContext {
                    triggerKind = LS.CompletionTriggerKind.Invoked,
                    _filterKind = LS.CompletionItemKind.Module,
                    //_includeAllModules = getModules.package == null
                }
            });

            return new AP.CompletionsResponse {
                completions = await ToCompletions(modules.items, GetMemberOptions.None)
            };
        }

        private async Task<Response> GetCompletions(Request request) {
            var req = (AP.CompletionsRequest)request;

            var members = await _server.Completion(new LS.CompletionParams {
                position = new LS.Position { line = req.line - 1, character = req.column - 1 },
                textDocument = req.documentUri,
                context = new LS.CompletionContext {
                    _intersection = req.options.HasFlag(GetMemberOptions.IntersectMultipleResults),
                    //_statementKeywords = req.options.HasFlag(GetMemberOptions.IncludeStatementKeywords),
                    //_expressionKeywords = req.options.HasFlag(GetMemberOptions.IncludeExpressionKeywords),
                    //_includeArgumentNames = true
                },
                _expr = req.text
            });

            return new AP.CompletionsResponse() {
                completions = await ToCompletions(members.items, req.options)
            };
        }

        private async Task<Response> GetAllMembers(Request request) {
            var req = (AP.GetAllMembersRequest)request;

            var members = await _server.WorkspaceSymbols(new LS.WorkspaceSymbolParams {
                query = req.prefix
            }).ConfigureAwait(false);

            return new AP.CompletionsResponse() {
                completions = await ToCompletions(members)
            };
        }

        private async Task<AP.Completion[]> ToCompletions(IEnumerable<LS.SymbolInformation> symbols) {
            if (symbols == null) {
                return null;
            }

            var res = new List<AP.Completion>();
            foreach (var s in symbols) {
                var m = new AP.Completion {
                    name = s.name,
                    memberType = ToMemberType(s._kind, s.kind)
                };

                if (s.location.uri != null) {
                    m.detailedValues = new[] {
                        new AP.CompletionValue {
                            locations = new [] {
                                new AP.AnalysisReference {
                                    file = s.location.uri.AbsolutePath,
                                    documentUri = s.location.uri,
                                    startLine = s.location.range.start.line + 1,
                                    startColumn = s.location.range.start.character + 1,
                                    endLine = s.location.range.end.line + 1,
                                    endColumn = s.location.range.end.character + 1,
                                    kind = "definition",
                                }
                            }
                        }
                    };
                }

                res.Add(m);
            }

            return res.ToArray();
        }


        private async Task<AP.Completion[]> ToCompletions(IEnumerable<LS.CompletionItem> completions, GetMemberOptions options) {
            if (completions == null) {
                return null;
            }

            var res = new List<AP.Completion>();
            foreach (var c in completions) {
                var m = new AP.Completion {
                    name = c.label,
                    completion = (c.label == c.insertText) ? null : c.insertText,
                    doc = c.documentation?.value,
                    memberType = ToMemberType(c._kind, c.kind)
                };

                if (options.HasFlag(GetMemberOptions.DetailedInformation)) {
                    var c2 = await _server.CompletionItemResolve(c);
                    var vars = new List<AP.CompletionValue>();
                    foreach (var v in c2._values.MaybeEnumerate()) {
                        vars.Add(new AP.CompletionValue {
                            description = new[] { new AP.DescriptionComponent { kind = null, text = v.description } },
                            doc = v.documentation,
                            locations = v.references?.Where(r => r.uri.IsFile).Select(r => new AP.AnalysisReference {
                                file = r.uri.AbsolutePath,
                                documentUri = r.uri,
                                startLine = r.range.start.line + 1,
                                startColumn = r.range.start.character + 1,
                                endLine = r.range.end.line + 1,
                                endColumn = r.range.end.character + 1,
                                kind = GetVariableType(r._kind)
                            }).ToArray()
                        });
                    }
                }

                res.Add(m);
            }

            return res.ToArray();
        }

        private PythonMemberType ToMemberType(string originalKind, LS.CompletionItemKind kind) {
            PythonMemberType res;
            if (!string.IsNullOrEmpty(originalKind) && Enum.TryParse(originalKind, true, out res)) {
                return res;
            }

            switch (kind) {
                case LS.CompletionItemKind.None: return PythonMemberType.Unknown;
                case LS.CompletionItemKind.Text: return PythonMemberType.Constant;
                case LS.CompletionItemKind.Method: return PythonMemberType.Method;
                case LS.CompletionItemKind.Function: return PythonMemberType.Function;
                case LS.CompletionItemKind.Constructor: return PythonMemberType.Function;
                case LS.CompletionItemKind.Field: return PythonMemberType.Field;
                case LS.CompletionItemKind.Variable: return PythonMemberType.Instance;
                case LS.CompletionItemKind.Class: return PythonMemberType.Class;
                case LS.CompletionItemKind.Interface: return PythonMemberType.Class;
                case LS.CompletionItemKind.Module: return PythonMemberType.Module;
                case LS.CompletionItemKind.Property: return PythonMemberType.Property;
                case LS.CompletionItemKind.Unit: return PythonMemberType.Unknown;
                case LS.CompletionItemKind.Value: return PythonMemberType.Instance;
                case LS.CompletionItemKind.Enum: return PythonMemberType.Enum;
                case LS.CompletionItemKind.Keyword: return PythonMemberType.Keyword;
                case LS.CompletionItemKind.Snippet: return PythonMemberType.CodeSnippet;
                case LS.CompletionItemKind.Color: return PythonMemberType.Instance;
                case LS.CompletionItemKind.File: return PythonMemberType.Module;
                case LS.CompletionItemKind.Reference: return PythonMemberType.Unknown;
                case LS.CompletionItemKind.Folder: return PythonMemberType.Module;
                case LS.CompletionItemKind.EnumMember: return PythonMemberType.EnumInstance;
                case LS.CompletionItemKind.Constant: return PythonMemberType.Constant;
                case LS.CompletionItemKind.Struct: return PythonMemberType.Class;
                case LS.CompletionItemKind.Event: return PythonMemberType.Delegate;
                case LS.CompletionItemKind.Operator: return PythonMemberType.Unknown;
                case LS.CompletionItemKind.TypeParameter: return PythonMemberType.Class;
                default: return PythonMemberType.Unknown;
            }
        }

        private PythonMemberType ToMemberType(string originalKind, LS.SymbolKind kind) {
            PythonMemberType res;
            if (!string.IsNullOrEmpty(originalKind) && Enum.TryParse(originalKind, true, out res)) {
                return res;
            }

            switch (kind) {
                case LS.SymbolKind.None: return PythonMemberType.Unknown;
                case LS.SymbolKind.File: return PythonMemberType.Module;
                case LS.SymbolKind.Module: return PythonMemberType.Module;
                case LS.SymbolKind.Namespace: return PythonMemberType.Namespace;
                case LS.SymbolKind.Package: return PythonMemberType.Module;
                case LS.SymbolKind.Class: return PythonMemberType.Class;
                case LS.SymbolKind.Method: return PythonMemberType.Method;
                case LS.SymbolKind.Property: return PythonMemberType.Property;
                case LS.SymbolKind.Field: return PythonMemberType.Field;
                case LS.SymbolKind.Constructor: return PythonMemberType.Method;
                case LS.SymbolKind.Enum: return PythonMemberType.Enum;
                case LS.SymbolKind.Interface: return PythonMemberType.Class;
                case LS.SymbolKind.Function: return PythonMemberType.Function;
                case LS.SymbolKind.Variable: return PythonMemberType.Field;
                case LS.SymbolKind.Constant: return PythonMemberType.Constant;
                case LS.SymbolKind.String: return PythonMemberType.Constant;
                case LS.SymbolKind.Number: return PythonMemberType.Constant;
                case LS.SymbolKind.Boolean: return PythonMemberType.Constant;
                case LS.SymbolKind.Array: return PythonMemberType.Instance;
                case LS.SymbolKind.Object: return PythonMemberType.Instance;
                case LS.SymbolKind.Key: return PythonMemberType.Unknown;
                case LS.SymbolKind.Null: return PythonMemberType.Unknown;
                case LS.SymbolKind.EnumMember: return PythonMemberType.EnumInstance;
                case LS.SymbolKind.Struct: return PythonMemberType.Class;
                case LS.SymbolKind.Event: return PythonMemberType.Event;
                case LS.SymbolKind.Operator: return PythonMemberType.Method;
                case LS.SymbolKind.TypeParameter: return PythonMemberType.NamedArgument;
                default: return PythonMemberType.Unknown;
            }
        }

        private async Task AnalyzeFileAsync(AP.AddFileRequest request, Func<Response, Task> done) {
            var uri = request.uri ?? ProjectEntry.MakeDocumentUri(request.path);
            var entry = await AddNewFile(uri, request.path, request.addingFromDir);

            await done(new AP.AddFileResponse { documentUri = uri });
        }

        private async Task AnalyzeFileAsync(AP.AddBulkFileRequest request, Func<Response, Task> done) {
            var entries = new IProjectEntry[request.path.Length];
            var response = new AP.AddBulkFileResponse {
                documentUri = new Uri[request.path.Length]
            };

            for(int i = 0; i < request.path.Length; ++i) {
                if (!string.IsNullOrEmpty(request.path[i])) {
                    var documentUri = ProjectEntry.MakeDocumentUri(request.path[i]);
                    entries[i] = await AddNewFile(documentUri, request.path[i], request.addingFromDir);
                    response.documentUri[i] = documentUri;
                }
            }

            await done(response);
        }

        private async Task<IProjectEntry> AddNewFile(Uri documentUri, string path, string addingFromDir) {
            if (documentUri.IsFile && Path.GetExtension(documentUri.LocalPath).Equals(".xaml", StringComparison.OrdinalIgnoreCase)) {
                return Project.AddXamlFile(path, null);
            }

            return await _server.LoadFileAsync(
                documentUri,
                string.IsNullOrEmpty(addingFromDir) ? null : new Uri(PathUtils.NormalizePath(addingFromDir))
            ).ConfigureAwait(false);
        }

        private async Task<Response> UnloadFile(AP.UnloadFileRequest command) {
            await _server.UnloadFileAsync(command.documentUri);
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
            private readonly bool _isStubFile;

            public StreamCodeInfo(int version, Stream stream, bool isStubFile) : base(version) {
                _stream = stream;
                _isStubFile = isStubFile;
            }

            public override Parser CreateParser(PythonLanguageVersion version, ParserOptions options) {
                if (_isStubFile) {
                    options = options?.Clone() ?? new ParserOptions();
                    options.StubFile = true;
                }
                return Parser.CreateParser(_stream, version, options);
            }

            public override TextReader GetReader() {
                return new StreamReader(_stream);
            }
        }

        class TextCodeInfo : CodeInfo {
            private readonly TextReader _text;
            private readonly bool _isStubFile;

            public TextCodeInfo(int version, TextReader text, bool isStubFile) : base(version) {
                _text = text;
                _isStubFile = isStubFile;
            }

            public override Parser CreateParser(PythonLanguageVersion version, ParserOptions options) {
                if (_isStubFile) {
                    options = options?.Clone() ?? new ParserOptions();
                    options.StubFile = true;
                }
                return Parser.CreateParser(_text, version, options);
            }

            public override TextReader GetReader() {
                return _text;
            }
        }

        private async Task<Response> UpdateContent(AP.FileUpdateRequest request) {
            int version = -1;
            foreach (var fileChange in request.updates) {
                var changes = new List<LS.TextDocumentContentChangedEvent>();
                if (fileChange.kind == AP.FileUpdateKind.reset) {
                    changes.Add(new LS.TextDocumentContentChangedEvent {
                        text = fileChange.content
                    });
                    version = fileChange.version;
                } else if (fileChange.kind == AP.FileUpdateKind.changes) {
                    changes.AddRange(fileChange.changes.Select(c => new LS.TextDocumentContentChangedEvent {
                        range = new SourceSpan(
                            new SourceLocation(c.startLine, c.startColumn),
                            new SourceLocation(c.endLine, c.endColumn)
                        ),
                        text = c.newText
                    }));
                    version = fileChange.version;
                } else {
                    continue;
                }
                _server.DidChangeTextDocument(new LS.DidChangeTextDocumentParams {
                    textDocument = new LS.VersionedTextDocumentIdentifier {
                        uri = request.documentUri,
                        version = version
                    },
                    contentChanges = changes.ToArray()
                });
            }

#if DEBUG
            var entry = _server.GetEntry(request.documentUri);
            int part = _server.GetPart(request.documentUri);
            return new AP.FileUpdateResponse {
                version = version,
                newCode = (entry as IDocument)?.ReadDocument(part, out _)?.ReadToEnd()
            };
#else
            return new AP.FileUpdateResponse {
                version = version
            };
#endif
        }

        internal Task ProcessMessages() {
            return _connection.ProcessMessages();
        }

        private Response SetAnalysisOptions(AP.SetAnalysisOptionsRequest request) {
            Options = request.options ?? new AP.AnalysisOptions();

            Project.Limits = new AnalysisLimits(Options.analysisLimits);
            _server._parseQueue.InconsistentIndentation = LS.DiagnosticsErrorSink.GetSeverity(Options.indentationInconsistencySeverity);
            _server._parseQueue.TaskCommentMap = Options.commentTokens;
            _server._analyzer.SetTypeStubPaths(Options.typeStubPaths);

            return new Response();
        }


        public AP.AnalysisOptions Options { get; set; }

        private void AnalysisQueue_Complete(object sender, EventArgs e) {
            _connection?.SendEventAsync(new AP.AnalysisCompleteEvent()).DoNotWait();
        }

        private void OnModulesChanged(object sender, EventArgs args) {
            _server.DidChangeConfiguration(new LS.DidChangeConfigurationParams()).DoNotWait();
        }

        private void OnFileChanged(AP.FileChangedEvent e) {
            _server.DidChangeWatchedFiles(new LS.DidChangeWatchedFilesParams {
                changes = e.changes.MaybeEnumerate().Select(c => new LS.FileEvent { uri = c.documentUri, type = c.kind }).ToArray()
            }).DoNotWait();
        }

        private void OnAnalysisComplete(object sender, LS.AnalysisCompleteEventArgs e) {
            _connection.SendEventAsync(
                new AP.FileAnalysisCompleteEvent {
                    documentUri = e.uri,
                    version = e.version
                }
            ).DoNotWait();
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

        internal Task WaitForCompleteAnalysis() => _server.WaitForCompleteAnalysisAsync();

        internal IPythonInterpreterFactory InterpreterFactory => Project?.InterpreterFactory;

        internal IPythonInterpreter Interpreter => Project?.Interpreter;

        // Returns the current analyzer or throws InvalidOperationException.
        // This should be used in request handlers that should fail when
        // analysis is impossible. Callers that explicitly check for null before
        // use should use _pyAnalyzer directly.
        private PythonAnalyzer Analyzer {
            get {
                if (Project == null) {
                    throw new InvalidOperationException("Unable to analyze code");
                }

                return Project;
            }
        }

        /// <summary>
        /// Returns the current analyzer or null if unable to analyze code.
        /// </summary>
        /// <remarks>
        /// This is for public consumption only and should not be used within
        /// <see cref="OutOfProcProjectAnalyzer"/>.
        /// </remarks>
        public PythonAnalyzer Project => _server._analyzer;

        private void OnPublishDiagnostics(object sender, LS.PublishDiagnosticsEventArgs e) {
            _connection.SendEventAsync(
                new AP.DiagnosticsEvent {
                    documentUri = e.uri,
                    version = e._version ?? -1,
                    diagnostics = e.diagnostics?.ToArray()
                }
            ).DoNotWait();
        }

        private void OnParseComplete(object sender, LS.ParseCompleteEventArgs e) {
            _connection.SendEventAsync(
                new AP.FileParsedEvent {
                    documentUri = e.uri,
                    version = e.version
                }
            ).DoNotWait();
        }


        #region IDisposable Members

        public void Dispose() {
            if (_isDisposed) {
                return;
            }

            _isDisposed = true;
            _server._queue.AnalysisComplete -= AnalysisQueue_Complete;
            if (Project != null) {
                Project.Interpreter.ModuleNamesChanged -= OnModulesChanged;
                Project.Dispose();
            }

            lock (_extensions) {
                foreach (var extension in _extensions.Values) {
                    (extension as IDisposable)?.Dispose();
                }
            }

            _server.Dispose();

            _connection.Dispose();
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

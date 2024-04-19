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
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Common.Core.Disposables;
using Microsoft.PythonTools.Common.Infrastructure;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.LanguageServerClient.FileWatcher;
using Microsoft.PythonTools.LanguageServerClient.StreamHacking;
using Microsoft.PythonTools.LanguageServerClient.WorkspaceFolderChanged;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Utility;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Workspace.VSIntegration.Contracts;
using Microsoft.VisualStudioTools;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.LanguageServerClient {
    /// <summary>
    /// Implementation of the language server client.
    /// </summary>
    /// <remarks>
    /// See documentation at https://docs.microsoft.com/en-us/visualstudio/extensibility/adding-an-lsp-extension?view=vs-2019
    /// </remarks>
    [Export(typeof(ILanguageClient))]
    [ContentType(PythonCoreConstants.ContentType)]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class PythonLanguageClient : ILanguageClient, ILanguageClientCustomMessage2, IDisposable {
        [Import(typeof(SVsServiceProvider))]
        public IServiceProvider Site;

        [Import]
        public IContentTypeRegistryService ContentTypeRegistryService;

        [Import]
        public IPythonWorkspaceContextProvider PythonWorkspaceContextProvider;

        [Import]
        public VsProjectContextProvider ProjectContextProvider;

        [Import]
        public IInterpreterOptionsService OptionsService;

        [Import]
        public JoinableTaskContext JoinableTaskContext;

        [Import]
        public IVsFolderWorkspaceService WorkspaceService;

        [Import]
        public ITextDocumentFactoryService TextDocumentFactoryService;

        /// <summary>
        /// Used for testing. Waits for the language client to be up and connected
        /// </summary>
        public static Task ReadyTask => _readyTcs.Task;

        private readonly Common.Core.Disposables.DisposableBag _disposables;
        private List<IPythonLanguageClientContext> _clientContexts = new List<IPythonLanguageClientContext>();
        private PythonAnalysisOptions _analysisOptions;
        private PythonAdvancedEditorOptions _advancedEditorOptions;
        private ITaskList _taskListService; 
        private LanguageServer _server;
        private JsonRpc _rpc;
        private bool _workspaceFoldersSupported = false;
        private bool _isDebugging = LanguageServer.IsDebugging();
        private bool _sentInitialWorkspaceFolders = false;
        private FileWatcher.Listener _fileListener;
        private static TaskCompletionSource<int> _readyTcs = new System.Threading.Tasks.TaskCompletionSource<int>();
        private bool _loaded = false;
        private Timer _deferredSettingsChangedTimer;
        private const int _defaultSettingsDelayMS = 2000;

        public PythonLanguageClient() {
            _disposables = new Common.Core.Disposables.DisposableBag(GetType().Name);
        }

        public string ContentTypeName => PythonCoreConstants.ContentType;

        public string Name => @"Pylance";

        // Called by Microsoft.VisualStudio.LanguageServer.Client.RemoteLanguageServiceBroker.UpdateClientsWithConfigurationSettingsAsync
        // Used to send LS WorkspaceDidChangeConfiguration notification
        public IEnumerable<string> ConfigurationSections => Enumerable.Repeat("python", 1);
        public object InitializationOptions { get; private set; }

        public IEnumerable<string> FilesToWatch => null;
        public object MiddleLayer => null;
        public object CustomMessageTarget { get; private set; }
        public bool IsInitialized { get; private set; }
        public bool Loaded => this._loaded;

        public bool ShowNotificationOnInitializeFailed => true;

        public event AsyncEventHandler<EventArgs> StartAsync;
#pragma warning disable CS0067
        public event AsyncEventHandler<EventArgs> StopAsync;
#pragma warning restore CS0067

        public async Task<Connection> ActivateAsync(CancellationToken token) {
            if (_server == null) {
                Debug.Fail("Should not have called StartAsync when _server is null.");
                return null;
            }
            await JoinableTaskContext.Factory.SwitchToMainThreadAsync();
            CreateClientContexts();

            _deferredSettingsChangedTimer = new Timer(OnDeferredSettingsChanged, state: null, Timeout.Infinite, Timeout.Infinite);
            _analysisOptions = Site.GetPythonToolsService().AnalysisOptions;
            _advancedEditorOptions = Site.GetPythonToolsService().AdvancedEditorOptions;
            _analysisOptions.Changed += OnSettingsChanged;
            _advancedEditorOptions.Changed += OnSettingsChanged;
            _taskListService = Site.GetService<SVsTaskList, ITaskList>();
            _taskListService.PropertyChanged += OnSettingsChanged;
            var dte = (EnvDTE80.DTE2)Site.GetService(typeof(EnvDTE.DTE));
            var solutionEvents = dte.Events.SolutionEvents;
            solutionEvents.Opened += OnSolutionOpened;
            solutionEvents.BeforeClosing += OnSolutionClosing;
            solutionEvents.ProjectAdded += OnProjectAdded;
            solutionEvents.ProjectRemoved += OnProjectRemoved;
            WorkspaceService.OnActiveWorkspaceChanged += OnWorkspaceOpening;

            _disposables.Add(() => {
                _clientContexts.ForEach(c => {
                    c.InterpreterChanged -= OnSettingsChanged;
                    c.SearchPathsChanged -= OnSettingsChanged;
                    c.ReanalyzeChanged -= OnReanalyzeChanged;
                    });
                _analysisOptions.Changed -= OnSettingsChanged;
                _advancedEditorOptions.Changed -= OnSettingsChanged;

                try {
                    var taskListService = Site.GetService<SVsTaskList, ITaskList>();
                    taskListService.PropertyChanged -= OnSettingsChanged;
                } catch (ServiceUnavailableException) {
                }
                
                _clientContexts.ForEach(c => c.Dispose());
                _clientContexts.Clear();
                solutionEvents.Opened -= OnSolutionOpened;
                solutionEvents.ProjectAdded -= OnProjectAdded;
                solutionEvents.ProjectRemoved -= OnProjectRemoved;
                solutionEvents.BeforeClosing -= OnSolutionClosing;
                WorkspaceService.OnActiveWorkspaceChanged -= OnWorkspaceOpening;
                _deferredSettingsChangedTimer.Dispose();
            });

            return await _server.ActivateAsync();
        }

        public async Task OnLoadedAsync() {
            await JoinableTaskContext.Factory.SwitchToMainThreadAsync();
            // Force the package to load, since this is a MEF component,
            // there is no guarantee it has been loaded.
            Site.GetPythonToolsService();

            // Indicate to python tools service we've loaded.
            _loaded = true;

            // Client context cannot be created here since the is no workspace yet
            // and hence we don't know if this is workspace or a loose files case.
            _server = new LanguageServer(Site, JoinableTaskContext, this.OnSendToServer);
            InitializationOptions = null;

            var customTarget = new PythonLanguageClientCustomTarget(Site, JoinableTaskContext);
            CustomMessageTarget = customTarget;
            customTarget.WatchedFilesRegistered += WatchedFilesRegistered;
            customTarget.WorkspaceFolderChangeRegistered += OnWorkspaceFolderWatched;
            customTarget.AnalysisComplete += OnAnalysisComplete;
            customTarget.WorkspaceConfiguration += OnWorkspaceConfiguration;
            await StartAsync.InvokeAsync(this, EventArgs.Empty);
        }

        private async Task OnWorkspaceConfiguration(object sender, WorkspaceConfiguration.ConfigurationArgs args) {
            // This is where we send the settings for each client context.
            // Pylance will send a workspace/configuration request for each workspace
            // We return the language server settings as a response
            List<object> result = new List<object>();

            await JoinableTaskContext.Factory.SwitchToMainThreadAsync();

            // Return the matching results
            foreach (var item in args.requestParams.items) {

                var pythonSetting = GetSettings(item.scopeUri);

                if(pythonSetting == null) {
                    continue;
                }

                // Add to our results based on the section asked for
                if (item.section == "python") {
                    result.Add(pythonSetting);
                } else if (item.section == "python.analysis") {
                    result.Add(pythonSetting.analysis);
                }
            }

            // Convert into an array for serialization
            args.requestResult = result.ToArray();
        }

        public async Task OnServerInitializedAsync() {
            IsInitialized = true;
            // Set _workspaceFoldersSupported to true and send to either workspace open or solution open
            OnWorkspaceFolderWatched(this, EventArgs.Empty);
        }

        public Task OnServerInitializeFailedAsync(Exception e) {
            MessageBox.ShowErrorMessage(Site, Strings.LanguageClientInitializeFailed.FormatUI(e));
            return Task.CompletedTask;
        }

        public Task AttachForCustomMessageAsync(JsonRpc rpc) {
     
            _rpc = rpc;

            // This is a workaround until we have proper API from ILanguageClient for now.
            _rpc.AllowModificationWhileListening = true;
            _rpc.CancellationStrategy = new CustomCancellationStrategy(_server.CancellationFolderName, _rpc);
            _rpc.AllowModificationWhileListening = false;

            // Create our listener for file events
            _fileListener?.Dispose();
            _fileListener = null;
            _fileListener = new FileWatcher.Listener(_rpc, WorkspaceService, Site);
            _disposables.Add(_fileListener);

            // We also need to switch the order on handlers for all of the rpc targets. Until
            // the VSSDK gives us a way to do this, use reflection.
            try {
                var fi = rpc.GetType().GetField("rpcTargetInfo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (fi != null) {
                    var rpcTargetInfo = fi.GetValue(rpc);
                    if (rpcTargetInfo != null) {
                        fi = rpcTargetInfo.GetType().GetField("targetRequestMethodToClrMethodMap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (fi != null) {
                            // Have to use reflection all the way down as the type of the entry is private
                            var dictionary = fi.GetValue(rpcTargetInfo);
                            var method = dictionary?.GetType().GetMethod("get_Keys");
                            var keys = method?.Invoke(dictionary, new object[0]) as IEnumerable<string>;
                            foreach (var key in keys) {
                                method = dictionary?.GetType().GetMethod("get_Item");
                                var list = method?.Invoke(dictionary, new object[1] { key });
                                var reverse = list?.GetType().GetMethod(
                                    "Reverse",
                                    BindingFlags.Public | BindingFlags.Instance,
                                    null,
                                    CallingConventions.Any,
                                    new Type[] { },
                                    null);
                                reverse?.Invoke(list, new object[0]);
                            }
                        }
                    }
                }
            } catch {
                // Any exceptions, just skip this part
            }
            
            return Task.CompletedTask;
        }

        public void Dispose() => _disposables.TryDispose();

        public void AddClientContext(IPythonLanguageClientContext context) {
            _clientContexts.Add(context);
            context.InterpreterChanged += OnSettingsChanged;
            context.SearchPathsChanged += OnSettingsChanged;
            context.ReanalyzeChanged += OnReanalyzeChanged;
        }

        public Task InvokeTextDocumentDidOpenAsync(LSP.DidOpenTextDocumentParams request)
            => NotifyWithParametersAsync("textDocument/didOpen", request);

        public Task InvokeTextDocumentDidChangeAsync(LSP.DidChangeTextDocumentParams request)
            => NotifyWithParametersAsync("textDocument/didChange", request);

        public Task InvokeDidChangeConfigurationAsync(LSP.DidChangeConfigurationParams request) {

            return _rpc.NotifyWithParameterObjectAsync("workspace/didChangeConfiguration", request);
        }

        public async Task InvokeDidChangeWorkspaceFoldersAsync(WorkspaceFolder[] added, WorkspaceFolder[] removed) {

            await _rpc.NotifyWithParameterObjectAsync("workspace/didChangeWorkspaceFolders",
                new DidChangeWorkspaceFoldersParams {
                    changeEvent = new WorkspaceFoldersChangeEvent {
                        added = added,
                        removed = removed
                    }
                });
                   
            // If we send workspace folder updates, we have to resend document opens
            // await SendDocumentOpensAsync();
        }

        public Task<object> InvokeTextDocumentCompletionAsync(LSP.CompletionParams request, CancellationToken cancellationToken = default)
            => InvokeWithParametersAsync<object>("textDocument/completion", request, cancellationToken);

        public Task<object> InvokeTextDocumentSymbolsAsync(LSP.DocumentSymbolParams request, CancellationToken cancellationToken)
            => InvokeWithParametersAsync<object>("textDocument/documentSymbol", request, cancellationToken);

        public Task<object> InvokeTextDocumentDefinitionAsync(LSP.TextDocumentPositionParams request, CancellationToken cancellationToken)
            => InvokeWithParametersAsync<object>("textDocument/definition", request, cancellationToken);

        public Task<LSP.Location[]> InvokeReferencesAsync(LSP.ReferenceParams request, CancellationToken cancellationToken)
            => InvokeWithParametersAsync<LSP.Location[]>("textDocument/references", request, cancellationToken);

        public Task<LSP.CompletionItem> InvokeResolveAsync(LSP.CompletionItem request, CancellationToken cancellationToken)
            => InvokeWithParametersAsync<LSP.CompletionItem>("completionItem/resolve", request, cancellationToken);

        public Task<object> InvokeCommandAsync(LSP.ExecuteCommandParams request, CancellationToken cancellationToken)
            => InvokeWithParametersAsync<object>("workspace/executeCommand", request, cancellationToken);


        private async Task<R> InvokeWithParametersAsync<R>(string request, object parameters, CancellationToken t) where R : class {
            await _readyTcs.Task.ConfigureAwait(false);

            return await _rpc.InvokeWithParameterObjectAsync<R>(request, parameters, t).ConfigureAwait(false);
        }

        private async Task NotifyWithParametersAsync(string request, object parameters) {
            await _readyTcs.Task.ConfigureAwait(false);

            await _rpc.NotifyWithParameterObjectAsync(request, parameters).ConfigureAwait(false);
        }

        private LanguageServerSettings.PythonSettings GetSettings(Uri scopeUri = null)
        {
            IPythonLanguageClientContext context = null;
            if (scopeUri == null) {
                // REPL context has null RootPath
                context = _clientContexts.Find(c => c.RootPath == null);
                if (context == null) {                  
                    return null;
                }
            }else {
                var pathFromScopeUri = CommonUtils.NormalizeDirectoryPath(scopeUri.LocalPath).ToLower().TrimStart('\\');
                // Find the matching context for the item
                context = _clientContexts.Find(c => scopeUri != null && PathUtils.IsSamePath(c.RootPath.ToLower(), pathFromScopeUri));
            }

            if (context == null) {
                Debug.WriteLine(String.Format("GetSettings() scopeUri not found: {0}", scopeUri));
                return null;
            }

            Debug.Assert(_analysisOptions != null);

            var extraPaths = UserSettings.GetStringSetting(
                PythonConstants.ExtraPathsSetting, null, Site, PythonWorkspaceContextProvider.Workspace, out _)?.Split(';')
                ?? _analysisOptions.ExtraPaths;

            // Add search paths to extraPaths for pylance to look through
            var searchPaths = context.SearchPaths.ToArray();
            extraPaths = extraPaths == null ? searchPaths : extraPaths.Concat(searchPaths).ToArray();

            var stubPath = UserSettings.GetStringSetting(
                PythonConstants.StubPathSetting, null, Site, PythonWorkspaceContextProvider.Workspace, out _)
                ?? _analysisOptions.StubPath;

            var typeCheckingMode = UserSettings.GetStringSetting(
                PythonConstants.TypeCheckingModeSetting, null, Site, PythonWorkspaceContextProvider.Workspace, out _)
                ?? _analysisOptions.TypeCheckingMode;

            // get task list tokens from options
            var taskListTokens = new List<LanguageServerSettings.PythonSettings.PythonAnalysisSettings.TaskListToken>();
            var taskListService = Site.GetService<SVsTaskList, ITaskList>();
            if (taskListService != null)
            {
                foreach (var commentToken in taskListService.CommentTokens)
                {
                    taskListTokens.Add(new LanguageServerSettings.PythonSettings.PythonAnalysisSettings.TaskListToken()
                    {
                        text = commentToken.Text,
                        priority = commentToken.Priority.ToString()
                    });
                }
            }

            var settings = new LanguageServerSettings.PythonSettings {
                pythonPath = context.InterpreterConfiguration.InterpreterPath,
                venvPath = string.Empty,
                analysis = new LanguageServerSettings.PythonSettings.PythonAnalysisSettings {
                    logLevel = _analysisOptions.LogLevel,
                    autoSearchPaths = _analysisOptions.AutoSearchPaths,
                    diagnosticMode = _analysisOptions.DiagnosticMode,
                    extraPaths = extraPaths,
                    stubPath = stubPath,
                    typeshedPaths = _analysisOptions.TypeshedPaths,
                    typeCheckingMode = typeCheckingMode,
                    useLibraryCodeForTypes = true,
                    completeFunctionParens = _advancedEditorOptions.CompleteFunctionParens,
                    autoImportCompletions = _advancedEditorOptions.AutoImportCompletions,
                    indexing = _analysisOptions.Indexing,
                    extraCommitChars = false,
                    importFormat = _analysisOptions.ImportFormat,
                    inlayHints = new LanguageServerSettings.PythonSettings.PythonAnalysisSettings.PythonAnalysisInlayHintsSettings {
                        variableTypes = false,
                        functionReturnTypes = false
                    },
                    taskListTokens = taskListTokens.ToArray()
                }

            };

            return settings;

        }

        private void OnSettingsChanged(object sender, EventArgs e) {
            try {
                System.Diagnostics.Debug.WriteLine("Settings Changed");
                _deferredSettingsChangedTimer.Change(_defaultSettingsDelayMS, Timeout.Infinite);
            } catch (ObjectDisposedException) {
            }
        }

        private void OnDeferredSettingsChanged(object state) {
            Debug.WriteLine("deferred Settings Changed");
            InvokeDidChangeConfigurationAsync(new LSP.DidChangeConfigurationParams() {
                // If we pass null settings and workspace.configuration is supported, Pylance will ask
                // us for per workspace configuration settings. Otherwise we can send
                // global settings here.
                Settings = null
            }).DoNotWait();

        }

        private void OnAnalysisComplete(object sender, EventArgs e) {
            // Used by test code to know when it's okay to try and use intellisense
            _readyTcs.TrySetResult(0);
        }

        private void OnReanalyzeChanged(object sender, EventArgs e) {
            try {
                Debug.WriteLine("Reanalyze Changed");
                _deferredSettingsChangedTimer.Change(_defaultSettingsDelayMS, Timeout.Infinite);
            } catch (ObjectDisposedException) {
            }
        }
    
        private bool TryGetOpenedDocumentData(RunningDocumentInfo info, out ITextBuffer textBuffer, out string filePath) {
            textBuffer = null;
            filePath = string.Empty;

            if (!info.IsDocumentInitialized) {
                return false;
            }

            IVsUserData vsUserData = info.DocData as IVsUserData;
            if (vsUserData == null) {
                return false;
            }

            // Acquire the text buffer and snapshot from the document
            vsUserData.GetData(Microsoft.VisualStudio.Editor.DefGuidList.guidDocumentTextSnapshot, out object snapshot);
            textBuffer = (snapshot as ITextSnapshot)?.TextBuffer;
            if (textBuffer == null) {
                return false;
            }

            if (!TextDocumentFactoryService.TryGetTextDocument(textBuffer, out ITextDocument textDocument)) {
                return false;
            }

            filePath = textDocument.FilePath;

            return true;
        }

        private void OnWorkspaceOrSolutionOpened() {
            if (WorkspaceService.CurrentWorkspace != null) {
                OnWorkspaceOpening(this, EventArgs.Empty).DoNotWait();
            } else {
                OnSolutionOpened();
            }
        }

        private void OnWorkspaceFolderWatched(object sender, EventArgs e) {
            _workspaceFoldersSupported = true;
            OnWorkspaceOrSolutionOpened();
        }

        private void WatchedFilesRegistered(object sender, DidChangeWatchedFilesRegistrationOptions e) {
            // Add the file globs to our listener. It will listen to the globs
            _fileListener?.AddPatterns(e.Watchers);
        }

        private void CreateClientContexts() {
            if (PythonWorkspaceContextProvider.Workspace != null) {
                AddClientContext(new PythonLanguageClientContextWorkspace(PythonWorkspaceContextProvider.Workspace));
            } else {
                var nodes = from n in ProjectContextProvider.ProjectNodes
                            select new PythonLanguageClientContextProject(n);
                foreach (var n in nodes) {
                    AddClientContext(n);
                }
            }
        }

        // This is all a hack until VSSDK LanguageServer can handle workspace folders and dynamic registration
        private Tuple<StreamData, bool> OnSendToServer(StreamData data) {
            var message = MessageParser.Deserialize(data);
            if (message != null) {
                if (_isDebugging) {
                    System.Diagnostics.Debug.WriteLine($"*** Sending pylance: {message.ToString()}");
                }
                try {
                    // If this is the initialize method, add the workspace folders capability
                    if (message.Value<string>("method") == "initialize") {
                        if (message.TryGetValue("params", out JToken messageParams)) {
                            var capabilities = messageParams["capabilities"];
                            if (capabilities != null) {
                                if (capabilities["workspace"] == null) {
                                    capabilities["workspace"] = JToken.FromObject(new { });
                                }
                                capabilities["workspace"]["workspaceFolders"] = true;
                                capabilities["workspace"]["didChangeWatchedFiles"]["dynamicRegistration"] = true;
                                capabilities["workspace"]["configuration"] = true;

                                // Root path and root URI should not be sent. They're deprecated and will
                                // just confuse pylance with respect to what is the root folder. 
                                // https://microsoft.github.io/language-server-protocol/specifications/specification-current/#initialize
                                // Setting them to empty will make pylance think they're gone.
                                messageParams["rootPath"] = "";
                                messageParams["rootUri"] = "";

                                // Need to rewrite the message now. 
                                return Tuple.Create(MessageParser.Serialize(message), true);
                            }
                        }
                    } else if (message.Value<string>("method") == "textDocument/codeAction") {
                        if (message.TryGetValue("params", out JToken messageParams)) {
                            if (messageParams != null && messageParams["range"] != null 
                                && messageParams["context"] != null 
                                && messageParams["context"]["_vs_selectionRange"] != null) {
                                var selectionRange = messageParams["context"]["_vs_selectionRange"];
                                messageParams["range"]["start"] = selectionRange["start"];
                                messageParams["range"]["end"] = selectionRange["end"];
                                return Tuple.Create(MessageParser.Serialize(message), true);
                            }
                                
                        }
                    }
                } catch {
                    // Don't care if this happens. Just skip the message
                }
            }

            // Return the tuple that indicates if we need to keep listening or not
            // For now we will keep listening since code action requests can be sent multiple times
            return Tuple.Create(data, true);
        }

        private async Task OnWorkspaceOpening(object sende, EventArgs e) {
            if (_workspaceFoldersSupported && IsInitialized && !_sentInitialWorkspaceFolders && WorkspaceService.CurrentWorkspace != null) {
                _sentInitialWorkspaceFolders = true;
                // Send just this workspace folder. Assumption here is that the language client will be destroyed/recreated on
                // each workspace open
                var folder = new WorkspaceFolder { uri = new System.Uri(WorkspaceService.CurrentWorkspace.Location), name = WorkspaceService.CurrentWorkspace.GetName() };
                await InvokeDidChangeWorkspaceFoldersAsync(new WorkspaceFolder[] { folder }, new WorkspaceFolder[0]);
            }
        }

        private void OnSolutionClosing() {
            this._clientContexts.Clear();
            if (_workspaceFoldersSupported && IsInitialized && _sentInitialWorkspaceFolders) {
                JoinableTaskContext.Factory.RunAsync(async () => {
                    // If workspace folders are supported, then send our workspace folders
                    var folders = from n in this.ProjectContextProvider.ProjectNodes
                                  select new WorkspaceFolder { uri = new System.Uri(n.BaseURI.Directory), name = n.Name };
                    if (folders.Any()) {
                        await InvokeDidChangeWorkspaceFoldersAsync(new WorkspaceFolder[0], folders.ToArray());
                    }
                });
                _workspaceFoldersSupported = false;
                IsInitialized = false;
                _sentInitialWorkspaceFolders = false;
            }
        }

        private void OnSolutionOpened() {
            if (_workspaceFoldersSupported && IsInitialized && !_sentInitialWorkspaceFolders) {
                _sentInitialWorkspaceFolders = true;
                JoinableTaskContext.Factory.RunAsync(async () => {
                    // If workspace folders are supported, then send our workspace folders
                    var folders = from n in this.ProjectContextProvider.ProjectNodes
                                  select new WorkspaceFolder { uri = new System.Uri(n.BaseURI.Directory), name = n.Name };
                    if (folders.Any()) {
                        await InvokeDidChangeWorkspaceFoldersAsync(folders.ToArray(), new WorkspaceFolder[0]);
                    }
                });
            }
        }

        private void OnProjectAdded(EnvDTE.Project project) {
            if (_workspaceFoldersSupported) {
                JoinableTaskContext.Factory.RunAsync(async () => {
                    var pythonProject = project as PythonProjectNode;
                    if (pythonProject != null) {
                        var folder = new WorkspaceFolder { uri = new System.Uri(pythonProject.BaseURI.Directory), name = project.Name };
                        await InvokeDidChangeWorkspaceFoldersAsync(new WorkspaceFolder[] { folder }, new WorkspaceFolder[0]);
                    }
                });
            }
        }

        private void OnProjectRemoved(EnvDTE.Project project) {
            if (_workspaceFoldersSupported) {
                JoinableTaskContext.Factory.RunAsync(async () => {
                    var pythonProject = project as PythonProjectNode;
                    if (pythonProject != null) {
                        var folder = new WorkspaceFolder { uri = new System.Uri(pythonProject.BaseURI.Directory), name = project.Name };
                        await InvokeDidChangeWorkspaceFoldersAsync(new WorkspaceFolder[0], new WorkspaceFolder[] { folder });
                    }
                });
            }
        }

        public async Task<InitializationFailureContext> OnServerInitializeFailedAsync(ILanguageClientInitializationInfo initializationState) {
            var results = new InitializationFailureContext();
            results.FailureMessage = initializationState.InitializationException.Message;
            return results;
        }
    }
}

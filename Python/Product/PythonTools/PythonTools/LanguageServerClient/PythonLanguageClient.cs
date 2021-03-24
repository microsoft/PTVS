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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core.Disposables;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.LanguageServerClient.StreamHacking;
using Microsoft.PythonTools.LanguageServerClient.WorkspaceFolderChanged;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Utility;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
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

        private readonly DisposableBag _disposables;
        private IPythonLanguageClientContext _clientContext;
        private PythonAnalysisOptions _analysisOptions;
        private PythonAdvancedEditorOptions _advancedEditorOptions;
        private PylanceLanguageServer _server;
        private JsonRpc _rpc;
        private bool _workspaceFoldersSupported;

        public PythonLanguageClient() {
            _disposables = new DisposableBag(GetType().Name);
        }

        public string ContentTypeName => PythonCoreConstants.ContentType;

        public string Name => @"Pylance";

        // Called by Microsoft.VisualStudio.LanguageServer.Client.RemoteLanguageServiceBroker.UpdateClientsWithConfigurationSettingsAsync
        // Used to send LS WorkspaceDidChangeConfiguration notification
        public IEnumerable<string> ConfigurationSections => Enumerable.Repeat("python", 1);
        public object InitializationOptions { get; private set; }

        // TODO: investigate how this can be used, VS does not allow currently
        // for the server to dynamically register file watching.
        public IEnumerable<string> FilesToWatch => null;
        public object MiddleLayer => null;
        public object CustomMessageTarget { get; private set; }
        public bool IsInitialized { get; private set; }

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

            _clientContext = CreateClientContext();

            _analysisOptions = Site.GetPythonToolsService().AnalysisOptions;
            _advancedEditorOptions = Site.GetPythonToolsService().AdvancedEditorOptions;

            _clientContext.InterpreterChanged += OnSettingsChanged;
            _analysisOptions.Changed += OnSettingsChanged;
            _advancedEditorOptions.Changed += OnSettingsChanged;
            var dte = (EnvDTE80.DTE2)Site.GetService(typeof(EnvDTE.DTE));
            var solutionEvents = dte.Events.SolutionEvents;
            solutionEvents.Opened += OnSolutionOpened;
            solutionEvents.ProjectAdded += OnProjectAddedOrRemoved;
            solutionEvents.ProjectRemoved += OnProjectAddedOrRemoved;

            _disposables.Add(() => {
                _clientContext.InterpreterChanged -= OnSettingsChanged;
                _analysisOptions.Changed -= OnSettingsChanged;
                _advancedEditorOptions.Changed -= OnSettingsChanged;
                _clientContext.Dispose();
                solutionEvents.ProjectAdded -= OnProjectAddedOrRemoved;
                solutionEvents.ProjectRemoved -= OnProjectAddedOrRemoved;
            });

            return await _server.ActivateAsync();
        }

        public async Task OnLoadedAsync() {
            await JoinableTaskContext.Factory.SwitchToMainThreadAsync();
            // Force the package to load, since this is a MEF component,
            // there is no guarantee it has been loaded.
            Site.GetPythonToolsService();

            // Client context cannot be created here since the is no workspace yet
            // and hence we don't know if this is workspace or a loose files case.
            _server = new PylanceLanguageServer(Site, JoinableTaskContext, this.OnSendToServer, this.OnReceiveFromServer);
            InitializationOptions = null;
            CustomMessageTarget = new PythonLanguageClientCustomTarget(Site, JoinableTaskContext);
            await StartAsync.InvokeAsync(this, EventArgs.Empty);
        }

        public async Task OnServerInitializedAsync() {
            await SendDidChangeConfiguration();
            IsInitialized = true;
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

            return Task.CompletedTask;
        }

        public void Dispose() => _disposables.TryDispose();

        public Task InvokeTextDocumentDidOpenAsync(LSP.DidOpenTextDocumentParams request)
            => _rpc == null ? Task.CompletedTask : _rpc.NotifyWithParameterObjectAsync("textDocument/didOpen", request);

        public Task InvokeTextDocumentDidChangeAsync(LSP.DidChangeTextDocumentParams request)
            => _rpc == null ? Task.CompletedTask : _rpc.NotifyWithParameterObjectAsync("textDocument/didChange", request);

        public Task InvokeDidChangeConfigurationAsync(LSP.DidChangeConfigurationParams request)
            => _rpc == null ? Task.CompletedTask : _rpc.NotifyWithParameterObjectAsync("workspace/didChangeConfiguration", request);

        public Task InvokeDidChangeWorkspaceFoldersAsync(DidChangeWorkspaceFoldersParams request)
            => _rpc == null ? Task.CompletedTask : _rpc.NotifyWithParameterObjectAsync("workspace/didChangeWorkspaceFolders", request);


        public Task<LSP.CompletionList> InvokeTextDocumentCompletionAsync(LSP.CompletionParams request, CancellationToken cancellationToken = default)
            => _rpc == null ? Task.FromResult(new LSP.CompletionList()) : _rpc.InvokeWithParameterObjectAsync<LSP.CompletionList>("textDocument/completion", request, cancellationToken);

        public Task<TResult> InvokeWithParameterObjectAsync<TResult>(string targetName, object argument = null, CancellationToken cancellationToken = default)
            => _rpc == null ? Task.FromResult(default(TResult)) : _rpc.InvokeWithParameterObjectAsync<TResult>(targetName, argument, cancellationToken);

        private void OnSettingsChanged(object sender, EventArgs e) => SendDidChangeConfiguration().DoNotWait();

        private void OnProjectAddedOrRemoved(EnvDTE.Project Project) => SendDidChangeConfiguration().DoNotWait();

        private async Task SendDidChangeConfiguration() {
            if (_clientContext is PythonLanguageClientContextProject) {
                // Project interactions are main thread only.
                await JoinableTaskContext.Factory.SwitchToMainThreadAsync();
            }

            Debug.Assert(_clientContext != null);
            Debug.Assert(_analysisOptions != null);

            var extraPaths = UserSettings.GetStringSetting(
                PythonConstants.ExtraPathsSetting, null, Site, PythonWorkspaceContextProvider.Workspace, out _)?.Split(';')
                ?? _analysisOptions.ExtraPaths;

            var stubPath = UserSettings.GetStringSetting(
                PythonConstants.StubPathSetting, null, Site, PythonWorkspaceContextProvider.Workspace, out _)
                ?? _analysisOptions.StubPath;

            var typeCheckingMode = UserSettings.GetStringSetting(
                PythonConstants.TypeCheckingModeSetting, null, Site, PythonWorkspaceContextProvider.Workspace, out _)
                ?? _analysisOptions.TypeCheckingMode;

            var ver3 = new Version(3, 0);
            if (_clientContext.InterpreterConfiguration.Version < ver3) {
                MessageBox.ShowWarningMessage(Site, Strings.WarningPython2NotSupported);
            }

            var settings = new PylanceSettings {
                python = new PylanceSettings.PythonSettings {
                    pythonPath = _clientContext.InterpreterConfiguration.InterpreterPath,
                    venvPath = string.Empty,
                    analysis = new PylanceSettings.PythonSettings.PythonAnalysisSettings {
                        logLevel = _analysisOptions.LogLevel,
                        autoSearchPaths = _analysisOptions.AutoSearchPaths,
                        diagnosticMode = _analysisOptions.DiagnosticMode,
                        extraPaths = extraPaths,
                        stubPath = stubPath,
                        typeshedPaths = _analysisOptions.TypeshedPaths,
                        typeCheckingMode = typeCheckingMode,
                        useLibraryCodeForTypes = true,
                        completeFunctionParens = _advancedEditorOptions.CompleteFunctionParens,
                        autoImportCompletions = _advancedEditorOptions.AutoImportCompletions
                    }
                }
            };

            var config = new LSP.DidChangeConfigurationParams() {
                Settings = settings
            };
            await InvokeDidChangeConfigurationAsync(config);
        }

        private IPythonLanguageClientContext CreateClientContext() {
            if (PythonWorkspaceContextProvider.Workspace != null) {
                return new PythonLanguageClientContextWorkspace(PythonWorkspaceContextProvider.Workspace);
            }

            if (ProjectContextProvider.ProjectNodes.MaybeEnumerate().Any()) {
                // TODO: support multiple projects in solution.
                var node = ProjectContextProvider.ProjectNodes.First();
                if (node != null) {
                    return new PythonLanguageClientContextProject(node);
                }
            }

            return new PythonLanguageClientContextGlobal(OptionsService);
        }

        // This is all a hack until VSSDK LanguageServer can handle workspace folders
        private StreamData OnSendToServer(StreamData data) {
            System.Diagnostics.Debug.WriteLine($"Sending:\r\n{System.Text.Encoding.UTF8.GetString(data.bytes, data.offset, data.count)}\r\n");
            var message = MessageParser.Deserialize(data);
            if (message != null) {
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

                                // Need to rewrite the message now
                                return MessageParser.Serialize(message);
                            }
                        }
                    }
                } catch {
                    // Don't care if this happens. Just skip the message
                }
            }
            return data;
        }

        private void OnReceiveFromServer(StreamData data) {
            System.Diagnostics.Debug.WriteLine($"Receiving:\r\n{System.Text.Encoding.UTF8.GetString(data.bytes, data.offset, data.count)}\r\n");

            // Some messages are being swallowed by the VSSDK RemoteLanguageClientInstance. Handle them ourselves here
            var message = MessageParser.Deserialize(data);
            if (message != null && message["method"].ToString() == "client/registerCapability") {
                // See if workspace folders are supported
                _workspaceFoldersSupported = message["params"]["registrations"].Values().Any((t) => t["method"].ToString() == "workspace/didChangeWorkspaceFolders");

                // If supported, act like a solution just opened
                if (_workspaceFoldersSupported) {
                    OnSolutionOpened();
                }
            }
        }

        private void OnSolutionOpened() {
            // If workspace folders are supported, then send our workspace folders
            var folders = from n in this.ProjectContextProvider.ProjectNodes
                          select new WorkspaceFolder { uri = new System.Uri(n.BaseURI.Directory) };
            if (folders.Any()) {
                InvokeDidChangeWorkspaceFoldersAsync(new DidChangeWorkspaceFoldersParams { changeEvent = new WorkspaceFoldersChangeEvent { added = folders.ToArray(), removed = [] } });
            }
        }

    }
}

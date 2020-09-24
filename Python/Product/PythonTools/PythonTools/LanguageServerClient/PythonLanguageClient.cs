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
using System.Windows;
using Microsoft.Python.Core.Disposables;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
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
    public class PythonLanguageClient : ILanguageClient, ILanguageClientCustomMessage2, IDisposable {
        [Import(typeof(SVsServiceProvider))]
        public IServiceProvider Site;

        [Import]
        public IContentTypeRegistryService ContentTypeRegistryService;

        [Import]
        public IPythonWorkspaceContextProvider PythonWorkspaceContextProvider;

        [Import]
        public IInterpreterOptionsService OptionsService;

        [Import]
        public JoinableTaskContext JoinableTaskContext;

        private IPythonLanguageClientContext _clientContext;
        private readonly DisposableBag _disposables;
        private PythonLanguageServer _server;
        private JsonRpc _rpc;

        public PythonLanguageClient() {
            _disposables = new DisposableBag(GetType().Name);
        }

        public string ContentTypeName => PythonCoreConstants.ContentType;

        public string Name => "Pylance";

        public IEnumerable<string> ConfigurationSections {
            get {
                // Called by Microsoft.VisualStudio.LanguageServer.Client.RemoteLanguageServiceBroker.UpdateClientsWithConfigurationSettingsAsync
                // Used to send LS WorkspaceDidChangeConfiguration notification
                yield return "python";
            }
        }

        public object InitializationOptions { get; private set; }

        // TODO: investigate how this can be used, VS does not allow currently
        // for the server to dynamically register file watching.
        public IEnumerable<string> FilesToWatch => null;

        public object MiddleLayer { get; private set; }

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

            if (PythonWorkspaceContextProvider.Workspace != null) {
                _clientContext = new PythonLanguageClientContextWorkspace(PythonWorkspaceContextProvider.Workspace, PythonCoreConstants.ContentType);
            } else {
                _clientContext = new PythonLanguageClientContextGlobal(OptionsService, PythonCoreConstants.ContentType);
            }

            return await _server.ActivateAsync();
        }

        public async Task OnLoadedAsync() {
            await JoinableTaskContext.Factory.SwitchToMainThreadAsync();

            // Force the package to load, since this is a MEF component,
            // there is no guarantee it has been loaded.
            Site.GetPythonToolsService();

            // Client context cannot be created here since the is no workspace yet
            // and hence we don't know if this is workspace or a loose files case.
            _server = PythonLanguageServer.Create(Site, JoinableTaskContext);
            if (_server == null) {
                return;
            }

            InitializationOptions = null;
            CustomMessageTarget = new PythonLanguageClientCustomTarget(Site, JoinableTaskContext);

            await StartAsync.InvokeAsync(this, EventArgs.Empty);
        }

        public async Task OnServerInitializedAsync() {
            await SendDidChangeConfiguration();
            IsInitialized = true;
        }

        public Task OnServerInitializeFailedAsync(Exception e) {
            MessageBox.Show(Strings.LanguageClientInitializeFailed.FormatUI(e), Strings.ProductTitle);
            return Task.CompletedTask;
        }

        public Task AttachForCustomMessageAsync(JsonRpc rpc) {
            _rpc = rpc;
            return Task.CompletedTask;
        }

        public void Dispose() {
            _disposables.TryDispose();
        }

        public Task InvokeTextDocumentDidOpenAsync(LSP.DidOpenTextDocumentParams request) {
            if (_rpc == null) {
                return Task.CompletedTask;
            }

            return _rpc.NotifyWithParameterObjectAsync("textDocument/didOpen", request);
        }

        public Task InvokeTextDocumentDidChangeAsync(LSP.DidChangeTextDocumentParams request) {
            if (_rpc == null) {
                return Task.CompletedTask;
            }

            return _rpc.NotifyWithParameterObjectAsync("textDocument/didChange", request);
        }

        public Task InvokeDidChangeConfigurationAsync(
            LSP.DidChangeConfigurationParams request,
            CancellationToken cancellationToken = default(CancellationToken)
        ) {
            if (_rpc == null) {
                return Task.CompletedTask;
            }

            return _rpc.NotifyWithParameterObjectAsync("workspace/didChangeConfiguration", request);
        }

        public Task<LSP.CompletionList> InvokeTextDocumentCompletionAsync(
            LSP.CompletionParams request,
            CancellationToken cancellationToken = default(CancellationToken)
        ) {
            if (_rpc == null) {
                return Task.FromResult(new LSP.CompletionList());
            }

            return _rpc.InvokeWithParameterObjectAsync<LSP.CompletionList>("textDocument/completion", request);
        }

        public Task<TResult> InvokeWithParameterObjectAsync<TResult>(string targetName, object argument = null, CancellationToken cancellationToken = default) {
            if (_rpc == null) {
                return Task.FromResult(default(TResult));
            }

            return _rpc.InvokeWithParameterObjectAsync<TResult>(targetName, argument, cancellationToken);
        }

        private void OnInterpreterChanged(object sender, EventArgs e) {
            SendDidChangeConfiguration().DoNotWait();
        }

        private void OnSearchPathsChanged(object sender, EventArgs e) {
            SendDidChangeConfiguration().DoNotWait();
        }

        private async Task SendDidChangeConfiguration() {
            Debug.Assert(_clientContext != null);
            // TODO: client context needs to also provide the settings that are currently hard coded here
            // so that workspace can get it from PythonSettings.json and projects from their .pyproj, etc.
            var settings = new Settings {
                python = new Settings.PythonSettings {
                    pythonPath = _clientContext.InterpreterConfiguration.InterpreterPath,
                    venvPath = string.Empty,
                    analysis = new Settings.PythonSettings.PythonAnalysisSettings {
                        logLevel = "log",
                        autoSearchPaths = false,
                        diagnosticMode = "openFilesOnly",
                        diagnosticSeverityOverrides = new Dictionary<string, string>(),
                        extraPaths = _clientContext.SearchPaths.ToArray(),
                        stubPath = string.Empty,
                        typeCheckingMode = "basic",
                        useLibraryCodeForTypes = true
                    }
                }
            };

            var config = new LSP.DidChangeConfigurationParams() {
                Settings = settings
            };
            await InvokeDidChangeConfigurationAsync(config);
        }

        [Serializable]
        public sealed class Settings {
            [Serializable]
            public class PythonSettings {
                [Serializable]
                public class PythonAnalysisSettings {
                    public string[] typeshedPaths;
                    public string stubPath;
                    public Dictionary<string, string> diagnosticSeverityOverrides;
                    public string diagnosticMode; // TODO: make this an enum
                    public string logLevel; // TODO: make this an enum
                    public bool? autoSearchPaths;
                    public string[] extraPaths;
                    public string typeCheckingMode; // TODO: make this an enum
                    public bool? useLibraryCodeForTypes;
                }
                public PythonAnalysisSettings analysis;
                public string pythonPath;
                public string venvPath;
            }
            public PythonSettings python;
        }
    }
}

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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Python.Core.Disposables;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Utility;
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

        private readonly DisposableBag _disposables;
        private IPythonLanguageClientContext _clientContext;
        private PythonAnalysisOptions _analysisOptions;
        private PythonAdvancedEditorOptions _advancedEditorOptions;
        private PythonLanguageServer _server;
        private JsonRpc _rpc;

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

            _clientContext = PythonWorkspaceContextProvider.Workspace != null
                ? (IPythonLanguageClientContext)new PythonLanguageClientContextWorkspace(PythonWorkspaceContextProvider.Workspace)
                : new PythonLanguageClientContextGlobal(OptionsService);
            _analysisOptions = Site.GetPythonToolsService().AnalysisOptions;
            _advancedEditorOptions = Site.GetPythonToolsService().AdvancedEditorOptions;

            _clientContext.InterpreterChanged += OnSettingsChanged;
            _analysisOptions.Changed += OnSettingsChanged;
            _advancedEditorOptions.Changed += OnSettingsChanged;

            _disposables.Add(() => {
                _clientContext.InterpreterChanged -= OnSettingsChanged;
                _analysisOptions.Changed -= OnSettingsChanged;
                _advancedEditorOptions.Changed -= OnSettingsChanged;
                _clientContext.Dispose();
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
            _server = PythonLanguageServer.Create(Site, JoinableTaskContext);
            if (_server != null) {
                InitializationOptions = null;
                CustomMessageTarget = new PythonLanguageClientCustomTarget(Site, JoinableTaskContext);
                await StartAsync.InvokeAsync(this, EventArgs.Empty);
            }
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

            // This is a workaround until we have proper API from ILanguageClient for now.
            _rpc.AllowModificationWhileListening = true;
            _rpc.CancellationStrategy = new CustomCancellationStrategy(this._server.CancellationFolderName, _rpc);
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

        public Task<LSP.CompletionList> InvokeTextDocumentCompletionAsync(LSP.CompletionParams request, CancellationToken cancellationToken = default)
            => _rpc == null ? Task.FromResult(new LSP.CompletionList()) : _rpc.InvokeWithParameterObjectAsync<LSP.CompletionList>("textDocument/completion", request, cancellationToken);

        public Task<TResult> InvokeWithParameterObjectAsync<TResult>(string targetName, object argument = null, CancellationToken cancellationToken = default)
            => _rpc == null ? Task.FromResult(default(TResult)) : _rpc.InvokeWithParameterObjectAsync<TResult>(targetName, argument, cancellationToken);

        private void OnSettingsChanged(object sender, EventArgs e) => SendDidChangeConfiguration().DoNotWait();

        private async Task SendDidChangeConfiguration() {
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

            var settings = new Settings {
                python = new Settings.PythonSettings {
                    pythonPath = _clientContext.InterpreterConfiguration.InterpreterPath,
                    venvPath = string.Empty,
                    analysis = new Settings.PythonSettings.PythonAnalysisSettings {
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

        public static class DiagnosticMode {
            public const string OpenFilesOnly = "openFilesOnly";
            public const string Workspace = "workspace";
        }

        public static class LogLevel {
            public const string Error = "Error";
            public const string Warning = "Warning";
            public const string Information = "Information";
            public const string Trace = "Trace";
        }

        public static class TypeCheckingMode {
            public const string Off = "off";
            public const string Basic = "basic";
            public const string Strict = "strict";
        }

        [Serializable]
        public sealed class Settings {
            [Serializable]
            public class PythonSettings {
                /// <summary>
                /// Python settings. Match [python] section in Pylance.
                /// </summary>
                [Serializable]
                public class PythonAnalysisSettings {
                    /// <summary>
                    /// Paths to look for typeshed modules.
                    /// </summary>
                    public string[] typeshedPaths;

                    /// <summary>
                    /// Path to directory containing custom type stub files.
                    /// </summary>
                    public string stubPath;

                    /// <summary>
                    /// Allows a user to override the severity levels for individual diagnostics.
                    /// Typically specified in mspythonconfig.json.
                    /// </summary>
                    public Dictionary<string, string> diagnosticSeverityOverrides;

                    /// <summary>
                    /// Analyzes and reports errors on only open files or the entire workspace.
                    /// "enum": ["openFilesOnly", "workspace"]
                    /// </summary>
                    public string diagnosticMode;
                    
                    /// <summary>
                    /// Specifies the level of logging for the Output panel.
                    /// "enum": ["Error", "Warning", "Information", "Trace"]
                    /// </summary>
                    public string logLevel;
                    
                    /// <summary>
                    /// Automatically add common search paths like 'src'.
                    /// </summary>
                    public bool? autoSearchPaths;
                    
                    /// <summary>
                    /// Defines the default rule set for type checking.
                    /// </summary>
                    public string typeCheckingMode;
                    
                    /// <summary>
                    /// Use library implementations to extract type information when type stub is not present.
                    /// </summary>
                    public bool? useLibraryCodeForTypes;

                    /// <summary>
                    /// Additional import search resolution paths.
                    /// </summary>
                    public string[] extraPaths;

                    /// <summary>
                    /// Automatically add brackets for functions.
                    /// </summary>
                    public bool completeFunctionParens;

                    /// <summary>
                    /// Offer auto-import completions.
                    /// </summary>
                    public bool autoImportCompletions;
                }
                /// <summary>
                /// Analysis settings.
                /// </summary>
                public PythonAnalysisSettings analysis;
                
                /// <summary>
                /// Path to Python, you can use a custom version of Python.
                /// </summary>
                public string pythonPath;
                
                /// <summary>
                /// Path to folder with a list of Virtual Environments.
                /// </summary>
                public string venvPath;
            }
            /// <summary>
            /// Python section.
            /// </summary>
            public PythonSettings python;
        }

        private sealed class CustomCancellationStrategy : ICancellationStrategy {
            private readonly string _folderName;
            private readonly JsonRpc _jsonRpc;
            private readonly ICancellationStrategy _cancellationStrategy;

            private readonly string _cancellationFolderPath;

            public CustomCancellationStrategy(string folderName, JsonRpc jsonRpc) {
                this._folderName = folderName ?? throw new ArgumentNullException(nameof(folderName));
                this._jsonRpc = jsonRpc ?? throw new ArgumentNullException(nameof(jsonRpc));

                this._cancellationFolderPath = Path.Combine(Path.GetTempPath(), "python-languageserver-cancellation", this._folderName);

                this._cancellationStrategy = this._jsonRpc.CancellationStrategy;
                this._jsonRpc.Disconnected += OnDisconnected;

                try {
                    Directory.CreateDirectory(this._cancellationFolderPath);
                } catch (Exception e) when (!e.IsCriticalException()){ 
                    // not much we can do about it.
                }
            }

            public void IncomingRequestStarted(RequestId requestId, CancellationTokenSource cancellationTokenSource) => this._cancellationStrategy.IncomingRequestStarted(requestId, cancellationTokenSource);
            public void IncomingRequestEnded(RequestId requestId) => this._cancellationStrategy.IncomingRequestEnded(requestId);

            public void CancelOutboundRequest(RequestId requestId) {
                try {
                    using (File.OpenWrite(getCancellationFilePath(requestId))) { }
                } catch (Exception e) when (!e.IsCriticalException()) {
                    // simply ignore. not that big deal.
                }
            }

            public void OutboundRequestEnded(RequestId requestId) {
                try {
                    File.Delete(getCancellationFilePath(requestId));
                } catch (Exception e) when (!e.IsCriticalException()) {
                    // simply ignore. not that big deal.
                }
            }

            private string getCancellationFilePath(RequestId requestId) {
                var id = requestId.Number?.ToString() ?? requestId.String ?? "noid";
                return Path.Combine(this._cancellationFolderPath, $"cancellation-{id}.tmp");
            }

            private void OnDisconnected(object sender, JsonRpcDisconnectedEventArgs _) {
                // clean up cancellation folder
                try {
                    Directory.Delete(this._cancellationFolderPath, recursive: true);
                } catch (Exception e) when (!e.IsCriticalException()) {
                    // not much we can do. ignore it.
                }
            }
        }
    }
}

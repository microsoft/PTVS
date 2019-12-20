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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Python.Core.Disposables;
using Microsoft.Python.Parsing;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudioTools;
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
    internal class PythonLanguageClient : ILanguageClient, ILanguageClientCustomMessage2, IDisposable {
        private readonly IServiceProvider _site;
        private readonly JoinableTaskContext _joinableTaskContext;
        private readonly IPythonLanguageClientContext _clientContext;
        private readonly DisposableBag _disposables;
        private PythonLanguageServer _server;
        private JsonRpc _rpc;

        private static readonly List<PythonLanguageClient> _languageClients = new List<PythonLanguageClient>();

        private PythonLanguageClient(
            IServiceProvider site,
            JoinableTaskContext joinableTaskContext,
            IPythonLanguageClientContext clientContext
        ) {
            _site = site ?? throw new ArgumentNullException(nameof(site));
            _joinableTaskContext = joinableTaskContext ?? throw new ArgumentNullException(nameof(joinableTaskContext));
            _clientContext = clientContext ?? throw new ArgumentNullException(nameof(clientContext));
            _disposables = new DisposableBag(GetType().Name);

            _clientContext.InterpreterChanged += OnInterpreterChanged;
            _clientContext.SearchPathsChanged += OnSearchPathsChanged;
            _clientContext.Closed += OnClosed;

            _disposables.Add(() => {
                _clientContext.InterpreterChanged -= OnInterpreterChanged;
                _clientContext.SearchPathsChanged -= OnSearchPathsChanged;
                _clientContext.Closed -= OnClosed;
            });

            if (clientContext is IDisposable disposable) {
                _disposables.Add(disposable);
            }

            CustomMessageTarget = new PythonLanguageClientCustomTarget(site);
        }

        public static async Task EnsureLanguageClientAsync(
            IServiceProvider site,
            JoinableTaskContext joinableTaskContext,
            IPythonLanguageClientContext clientContext
        ) {
            if (site == null) {
                throw new ArgumentNullException(nameof(site));
            }

            if (joinableTaskContext == null) {
                throw new ArgumentNullException(nameof(joinableTaskContext));
            }

            if (clientContext == null) {
                throw new ArgumentNullException(nameof(clientContext));
            }

            await joinableTaskContext.Factory.SwitchToMainThreadAsync();

            var broker = site.GetComponentModel().GetService<ILanguageClientBroker>();

            await EnsureLanguageClientAsync(site, joinableTaskContext, clientContext, broker);
        }

        internal static async Task EnsureLanguageClientAsync(
            IServiceProvider site,
            JoinableTaskContext joinableTaskContext,
            IPythonLanguageClientContext clientContext,
            ILanguageClientBroker broker
        ) {
            if (site == null) {
                throw new ArgumentNullException(nameof(site));
            }

            if (clientContext == null) {
                throw new ArgumentNullException(nameof(clientContext));
            }

            if (broker == null) {
                throw new ArgumentNullException(nameof(broker));
            }

            PythonLanguageClient client = null;
            lock (_languageClients) {
                if (!_languageClients.Any(lc => lc.ContentTypeName == clientContext.ContentTypeName)) {
                    client = new PythonLanguageClient(site, joinableTaskContext, clientContext);
                    _languageClients.Add(client);
                }
            }

            if (client != null) {
                await broker.LoadAsync(new PythonLanguageClientMetadata(null, clientContext.ContentTypeName), client);
            }
        }

        public static PythonLanguageClient FindLanguageClient(string contentTypeName) {
            if (contentTypeName == null) {
                throw new ArgumentNullException(nameof(contentTypeName));
            }

            lock (_languageClients) {
                return _languageClients.SingleOrDefault(lc => lc.ContentTypeName == contentTypeName);
            }
        }

        public static PythonLanguageClient FindLanguageClient(ITextBuffer textBuffer) {
            if (textBuffer == null) {
                throw new ArgumentNullException(nameof(textBuffer));
            }

            return FindLanguageClient(textBuffer.ContentType.TypeName);
        }

        public static void DisposeLanguageClient(string contentTypeName) {
            if (contentTypeName == null) {
                throw new ArgumentNullException(nameof(contentTypeName));
            }

            PythonLanguageClient client = null;
            lock (_languageClients) {
                client = _languageClients.SingleOrDefault(lc => lc.ContentTypeName == contentTypeName);
                if (client != null) {
                    _languageClients.Remove(client);
                    client.Stop();
                    client.Dispose();
                }
            }
        }

        public string ContentTypeName => _clientContext.ContentTypeName;

        public InterpreterConfiguration Configuration => _clientContext.InterpreterConfiguration;

        public string Name => "Python Language Extension";

        public IEnumerable<string> ConfigurationSections {
            get {
                // Called by Microsoft.VisualStudio.LanguageServer.Client.RemoteLanguageServiceBroker.UpdateClientsWithConfigurationSettingsAsync
                // Used to send LS WorkspaceDidChangeConfiguration notification
                yield return "python";
            }
        }

        // called from Microsoft.VisualStudio.LanguageServer.Client.RemoteLanguageClientInstance.InitializeAsync
        // which sets Capabilities, RootPath, ProcessId, and InitializationOptions (to this property value)
        // initParam.Capabilities.TextDocument.Rename = new DynamicRegistrationSetting(false); ??
        // 
        // in vscode, the equivalent is in src/client/activation/languageserver/analysisoptions
        public object InitializationOptions { get; private set; }

        // TODO: what do we do with this?
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

            return await _server.ActivateAsync();
        }

        public async Task OnLoadedAsync() {
            await _joinableTaskContext.Factory.SwitchToMainThreadAsync();

            var interpreterPath = _clientContext.InterpreterConfiguration?.InterpreterPath;
            var version = _clientContext.InterpreterConfiguration?.Version;
            var searchPaths = _clientContext.SearchPaths.ToList();
            string rootPath = _clientContext.RootPath;

            if (string.IsNullOrEmpty(interpreterPath) || version == null) {
                return;
            }

            PythonLanguageVersion langVersion;
            try {
                langVersion = version.ToLanguageVersion();
            } catch (InvalidOperationException) {
                langVersion = PythonLanguageVersion.None;
            }

            _server = PythonLanguageServer.Create(_site, _joinableTaskContext, langVersion);
            if (_server == null) {
                return;
            }

            InitializationOptions = _server.CreateInitializationOptions(
                interpreterPath,
                version.ToString(),
                rootPath,
                searchPaths
            );

            await StartAsync.InvokeAsync(this, EventArgs.Empty);
        }

        public async Task OnServerInitializedAsync() {
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

        private void Stop() {
            _site.GetUIThread().InvokeTaskSync(async () => {
                await StopAsync?.Invoke(this, EventArgs.Empty);
            }, CancellationToken.None);
        }

        private void Restart() {
            _site.GetUIThread().InvokeTaskSync(RestartAsync, CancellationToken.None);
        }

        private async Task RestartAsync() {
            // Disposing of the current language client will dispose of the context
            // so we first make a copy of it to use when initializing the new client.
            var site = _site;
            var context = (IPythonLanguageClientContext)_clientContext.Clone();

            DisposeLanguageClient(ContentTypeName);

            await EnsureLanguageClientAsync(
                site,
                _joinableTaskContext,
                context
            );
        }

        private void OnInterpreterChanged(object sender, EventArgs e) {
            Restart();
        }

        private void OnSearchPathsChanged(object sender, EventArgs e) {
            Restart();
        }

        private void OnClosed(object sender, EventArgs e) {
            PythonLanguageClient.DisposeLanguageClient(ContentTypeName);
        }
    }
}

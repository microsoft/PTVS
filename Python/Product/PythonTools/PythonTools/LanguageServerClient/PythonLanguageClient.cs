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
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core.Disposables;
using Microsoft.Python.Parsing;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Workspace.VSIntegration.Contracts;
using Microsoft.VisualStudioTools;
using StreamJsonRpc;
using Task = System.Threading.Tasks.Task;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.PythonTools.LanguageServerClient {
    /// <summary>
    /// Implementation of the language server client.
    /// </summary>
    /// <remarks>
    /// See documentation at https://docs.microsoft.com/en-us/visualstudio/extensibility/adding-an-lsp-extension?view=vs-2019
    /// </remarks>
    internal class PythonLanguageClient : ILanguageClient, ILanguageClientCustomMessage2, IDisposable {
        private readonly IServiceProvider _site;
        private readonly IVsFolderWorkspaceService _workspaceService;
        private readonly IInterpreterOptionsService _optionsService;
        private readonly IInterpreterRegistryService _registryService;
        private readonly ILanguageClientBroker _broker;
        private readonly PythonProjectNode _project;
        private readonly IInteractiveWindow _replWindow;
        private string _serverFolderPath;
        private JsonRpc _rpc;
        private DisposableBag _disposables;

        private static readonly List<PythonLanguageClient> _languageClients = new List<PythonLanguageClient>();

        public PythonLanguageClient(
            IServiceProvider site,
            string contentTypeName,
            IVsFolderWorkspaceService workspaceService,
            IInterpreterOptionsService optionsService,
            IInterpreterRegistryService registryService,
            ILanguageClientBroker broker,
            PythonProjectNode project,
            IInteractiveWindow replWindow
        ) {
            _site = site ?? throw new ArgumentNullException(nameof(site));
            ContentTypeName = contentTypeName ?? throw new ArgumentNullException(nameof(contentTypeName));
            _workspaceService = workspaceService ?? throw new ArgumentNullException(nameof(workspaceService));
            _optionsService = optionsService ?? throw new ArgumentNullException(nameof(optionsService));
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _broker = broker ?? throw new ArgumentNullException(nameof(broker));
            _project = project;
            _replWindow = replWindow;
            _disposables = new DisposableBag(GetType().Name);

            if (replWindow != null) {
                // TODO: need to hook into Reset REPL to restart language server, environment settings may have changed
                ReplDocument = new ReplDocument(site, replWindow, this);
                _disposables.Add(ReplDocument);
            }

            var pythonWorkspaceProvider = site.GetComponentModel().GetService<IPythonWorkspaceContextProvider>();
            var pythonWorkspace = pythonWorkspaceProvider?.Workspace;
            if (pythonWorkspace != null) {
                pythonWorkspace.ActiveInterpreterChanged += OnWorkspaceChanged;
                pythonWorkspace.SearchPathsSettingChanged += OnWorkspaceChanged;
                pythonWorkspace.AddActionOnClose(this, OnWorkspaceClosed);
            }

            if (project != null) {
                project.LanguageServerRestart += OnProjectChanged;
                project.AddActionOnClose(this, OnProjectClosed);
            }

            _optionsService.DefaultInterpreterChanged += OnDefaultInterpreterChanged;
            _disposables.Add(() => {
                _optionsService.DefaultInterpreterChanged -= OnDefaultInterpreterChanged;

                if (project != null) {
                    project.LanguageServerRestart -= OnProjectChanged;
                }

                if (pythonWorkspace != null) {
                    pythonWorkspace.ActiveInterpreterChanged -= OnWorkspaceChanged;
                    pythonWorkspace.SearchPathsSettingChanged -= OnWorkspaceChanged;
                }
            });

            MiddleLayer = new PythonLanguageClientMiddleLayer(site.GetPythonToolsService(), site.GetComponentModel().GetService<PythonSnippetManager>(), null);
            CustomMessageTarget = new PythonLanguageClientCustomTarget(site);
        }

        public static async Task EnsureLanguageClientAsync(
            IServiceProvider serviceProvider,
            IInteractiveWindow replWindow,
            string contentTypeName
        ) {
            if (serviceProvider == null) {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (contentTypeName == null) {
                throw new ArgumentNullException(nameof(contentTypeName));
            }

            var componentModel = serviceProvider.GetComponentModel();
            var workspaceService = componentModel.GetService<IVsFolderWorkspaceService>();
            var optionsService = componentModel.GetService<IInterpreterOptionsService>();
            var registryService = componentModel.GetService<IInterpreterRegistryService>();
            var broker = componentModel.GetService<ILanguageClientBroker>();

            await EnsureLanguageClientAsync(
                serviceProvider,
                workspaceService,
                optionsService,
                registryService,
                broker,
                contentTypeName,
                null,
                replWindow
            );
        }

        private static async Task EnsureLanguageClientAsync(
            IServiceProvider serviceProvider,
            PythonProjectNode project,
            string contentTypeName
        ) {
            if (serviceProvider == null) {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (contentTypeName == null) {
                throw new ArgumentNullException(nameof(contentTypeName));
            }

            var componentModel = serviceProvider.GetComponentModel();
            var workspaceService = componentModel.GetService<IVsFolderWorkspaceService>();
            var optionsService = componentModel.GetService<IInterpreterOptionsService>();
            var registryService = componentModel.GetService<IInterpreterRegistryService>();
            var broker = componentModel.GetService<ILanguageClientBroker>();

            await EnsureLanguageClientAsync(
                serviceProvider,
                workspaceService,
                optionsService,
                registryService,
                broker,
                contentTypeName,
                project,
                null
            );
        }

        private static async Task EnsureLanguageClientAsync(
            IServiceProvider serviceProvider,
            string contentTypeName
        ) {
            if (serviceProvider == null) {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (contentTypeName == null) {
                throw new ArgumentNullException(nameof(contentTypeName));
            }

            var componentModel = serviceProvider.GetComponentModel();
            var workspaceService = componentModel.GetService<IVsFolderWorkspaceService>();
            var optionsService = componentModel.GetService<IInterpreterOptionsService>();
            var registryService = componentModel.GetService<IInterpreterRegistryService>();
            var broker = componentModel.GetService<ILanguageClientBroker>();

            await EnsureLanguageClientAsync(
                serviceProvider,
                workspaceService,
                optionsService,
                registryService,
                broker,
                contentTypeName,
                null,
                null
            );
        }

        public static async Task EnsureLanguageClientAsync(
            IServiceProvider site,
            IVsFolderWorkspaceService workspaceService,
            IInterpreterOptionsService optionsService,
            IInterpreterRegistryService registryService,
            ILanguageClientBroker broker,
            string contentTypeName,
            PythonProjectNode project,
            IInteractiveWindow replWindow
        ) {
            if (contentTypeName == null) {
                throw new ArgumentNullException(nameof(contentTypeName));
            }

            PythonLanguageClient client = null;
            lock (_languageClients) {
                if (!_languageClients.Any(lc => lc.ContentTypeName == contentTypeName)) {
                    client = new PythonLanguageClient(site, contentTypeName, workspaceService, optionsService, registryService, broker, project, replWindow);
                    _languageClients.Add(client);
                }
            }

            if (client != null) {
                await broker.LoadAsync(new PythonLanguageClientMetadata(null, contentTypeName), client);
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

        public ReplDocument ReplDocument { get; }

        public string ContentTypeName { get; }

        public IPythonInterpreterFactory Factory { get; private set; }

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
            await Task.Yield();

            var info = PythonLanguageClientStartInfo.Create(_serverFolderPath);

            var process = new Process {
                StartInfo = info
            };

            if (process.Start()) {
                return new Connection(process.StandardOutput.BaseStream, process.StandardInput.BaseStream);
            }

            return null;
        }

        public async Task OnLoadedAsync() {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var workspace = _workspaceService.CurrentWorkspace;

            // Force initialization of python tools service by requesting it
            _site.GetPythonToolsService();

            var shell = _site.GetService<SVsShell, IVsShell>();
            shell.GetProperty((int)__VSSPROPID4.VSSPROPID_LocalAppDataDir, out object localAppDataDir);
            _serverFolderPath = PythonLanguageClientStartInfo.EnsureLanguageServer((string)localAppDataDir);

            var interpreterPath = string.Empty;
            var interpreterVersion = string.Empty;
            var searchPaths = new List<string>();
            string rootPath = null;

            if (_replWindow != null) {
                // TODO: someone needs to unsubscribe
                //_replWindow.SubmissionBufferAdded += OnReplInputBufferCreated;

                var evaluator = _replWindow.Evaluator as PythonCommonInteractiveEvaluator;
                if (_replWindow.Evaluator is SelectableReplEvaluator selEvaluator) {
                    evaluator = selEvaluator.Evaluator as PythonCommonInteractiveEvaluator;
                }

                if (evaluator != null) {
                    interpreterPath = evaluator.Configuration.Interpreter.InterpreterPath;
                    interpreterVersion = evaluator.LanguageVersion.ToVersion().ToString();
                    searchPaths.AddRange(evaluator.Configuration.SearchPaths);
                }
            } else if (_project != null) {
                Factory = _project.ActiveInterpreter;
                if (Factory != null) {
                    interpreterPath = Factory.Configuration.InterpreterPath;
                    interpreterVersion = Factory.Configuration.Version.ToString();
                    searchPaths.AddRange(_project._searchPaths.GetAbsoluteSearchPaths());
                    rootPath = _project.ProjectHome;
                }
            } else if (workspace != null) {
                Factory = workspace.GetInterpreterFactory(_registryService, _optionsService);
                if (Factory != null) {
                    interpreterPath = Factory.Configuration.InterpreterPath;
                    interpreterVersion = Factory.Configuration.Version.ToString();
                    // VSCode captures the python.exe env variables, uses PYTHONPATH to build this list
                    searchPaths.AddRange(workspace.GetAbsoluteSearchPaths());
                    rootPath = workspace.Location;
                }
            } else {
                Factory = _optionsService.DefaultInterpreter;
                if (Factory != null) {
                    interpreterPath = Factory.Configuration.InterpreterPath;
                    interpreterVersion = Factory.Configuration.Version.ToString();
                }
            }

            if (string.IsNullOrEmpty(interpreterPath) || string.IsNullOrEmpty(interpreterVersion)) {
                return;
            }

            InitializationOptions = new PythonInitializationOptions {
                // we need to read from the workspace settings in order to populate this correctly
                // (or from the project)
                interpreter = new PythonInitializationOptions.Interpreter {
                    properties = new PythonInitializationOptions.Interpreter.InterpreterProperties {
                        InterpreterPath = interpreterPath,
                        Version = interpreterVersion,
                        DatabasePath = _serverFolderPath,
                    }
                },
                searchPaths = searchPaths.ToArray(),
                typeStubSearchPaths = new[] {
                    Path.Combine(_serverFolderPath, "Typeshed")
                },
                excludeFiles = new[] {
                    "**/Lib/**",
                    "**/site-packages/**",
                    "**/node_modules",
                    "**/bower_components",
                    "**/.git",
                    "**/.svn",
                    "**/.hg",
                    "**/CVS",
                    "**/.DS_Store",
                    "**/.git/objects/**",
                    "**/.git/subtree-cache/**",
                    "**/node_modules/*/**",
                    ".vscode/*.py",
                    "**/site-packages/**/*.py"
                },
                rootPathOverride = rootPath
            };

            await StartAsync.InvokeAsync(this, EventArgs.Empty);
        }

        public async Task OnServerInitializedAsync() {
            if (ReplDocument != null) {
                await ReplDocument.InitializeAsync();
            }

            IsInitialized = true;
        }

        public Task OnServerInitializeFailedAsync(Exception e) {
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
            return _rpc.NotifyWithParameterObjectAsync("textDocument/didOpen", request);
        }

        public Task InvokeTextDocumentDidChangeAsync(LSP.DidChangeTextDocumentParams request) {
            return _rpc.NotifyWithParameterObjectAsync("textDocument/didChange", request);
        }

        public Task<LSP.CompletionList> InvokeTextDocumentCompletionAsync(
            LSP.CompletionParams request,
            CancellationToken cancellationToken = default(CancellationToken)
        ) {
            return _rpc.InvokeWithParameterObjectAsync<LSP.CompletionList>("textDocument/completion", request);
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
            var site = _site;
            var project = _project;
            var contentTypeName = ContentTypeName;

            DisposeLanguageClient(ContentTypeName);
            await EnsureLanguageClientAsync(site, project, contentTypeName);

            //await StopAsync?.Invoke(this, EventArgs.Empty);
            //await _broker.LoadAsync(new PythonLanguageClientMetadata(null, ContentTypeName), this);
        }

        private void OnProjectChanged(object sender, EventArgs e) {
            Restart();
        }

        private void OnProjectClosed(object key) {
            PythonLanguageClient.DisposeLanguageClient(ContentTypeName);
        }

        private void OnWorkspaceChanged(object sender, EventArgs e) {
            Restart();
        }

        private void OnWorkspaceClosed(object key) {
            PythonLanguageClient.DisposeLanguageClient(ContentTypeName);
        }

        private void OnDefaultInterpreterChanged(object sender, EventArgs e) {
            if (_optionsService.DefaultInterpreter == Factory) {
                return;
            }

            if (_project != null || _workspaceService.CurrentWorkspace != null) {
                // This event happens while loading the project or workspace.
                // We rely on custom events from those to handle restarts.
                return;
            }

            DisposeLanguageClient(ContentTypeName);
            EnsureLanguageClientAsync(
                _site,
                _workspaceService,
                _optionsService,
                _registryService,
                _broker,
                ContentTypeName,
                _project,
                _replWindow
            ).HandleAllExceptions(_site, GetType()).DoNotWait();
        }
    }
}

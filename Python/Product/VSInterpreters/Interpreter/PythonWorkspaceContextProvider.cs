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
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Workspace.VSIntegration.Contracts;

namespace Microsoft.PythonTools.Interpreter {
    [Export(typeof(IPythonWorkspaceContextProvider))]
    [Export(typeof(PythonWorkspaceContextProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    class PythonWorkspaceContextProvider : IPythonWorkspaceContextProvider, IDisposable {
        private readonly IVsFolderWorkspaceService _workspaceService;
        private readonly Lazy<IInterpreterOptionsService> _optionsService;
        private readonly Lazy<IInterpreterRegistryService> _registryService;
        private readonly JoinableTaskContext _joinableTaskContext;
        private readonly object _currentContextLock = new object();
        private IPythonWorkspaceContext _currentContext;
        private bool _initialized;

        [ImportingConstructor]
        public PythonWorkspaceContextProvider(
            [Import] IVsFolderWorkspaceService workspaceService,
            [Import] Lazy<IInterpreterOptionsService> optionsService,
            [Import] Lazy<IInterpreterRegistryService> registryService,
            [Import] JoinableTaskContext joinableTaskContext
        ) {
            // Don't use registry service from the constructor, since that imports
            // all the factory providers, which may import IPythonWorkspaceContextProvider
            // (ie circular dependency).
            _workspaceService = workspaceService;
            _optionsService = optionsService;
            _registryService = registryService;
            _joinableTaskContext = joinableTaskContext;
            _workspaceService.OnActiveWorkspaceChanged += OnActiveWorkspaceChanged;
        }

        public event EventHandler<PythonWorkspaceContextEventArgs> WorkspaceClosing;

        public event EventHandler<PythonWorkspaceContextEventArgs> WorkspaceClosed;

        public event EventHandler<PythonWorkspaceContextEventArgs> WorkspaceOpening;

        public event EventHandler<PythonWorkspaceContextEventArgs> WorkspaceInitialized;

        public IPythonWorkspaceContext Workspace {
            get {
                EnsureInitialized();

                lock (_currentContextLock) {
                    return _currentContext;
                }
            }
        }

        public void Dispose() {
            _workspaceService.OnActiveWorkspaceChanged -= OnActiveWorkspaceChanged;
        }

        private void EnsureInitialized() {
            lock (_currentContextLock) {
                if (_initialized) {
                    return;
                }

                _initialized = true;
                InitializeCurrentContext();
            }
        }

        private async Task OnActiveWorkspaceChanged(object sender, EventArgs e) {
            await _joinableTaskContext.Factory.SwitchToMainThreadAsync();

            CloseCurrentContext();
            InitializeCurrentContext();
        }

        private void CloseCurrentContext() {
            lock (_currentContextLock) {
                var current = _currentContext;
                if (current != null) {
                    WorkspaceClosing?.Invoke(this, new PythonWorkspaceContextEventArgs(current));
                    current.Dispose();
                    WorkspaceClosed?.Invoke(this, new PythonWorkspaceContextEventArgs(current));
                }
                _currentContext = null;
            }
        }

        private void InitializeCurrentContext() {
            var workspace = _workspaceService.CurrentWorkspace;
            if (workspace != null) {
                var context = new PythonWorkspaceContext(workspace, _optionsService.Value, _registryService.Value);

                // Workspace interpreter factory provider will rescan the
                // workspace folder for factories.
                WorkspaceOpening?.Invoke(this, new PythonWorkspaceContextEventArgs(context));

                // Workspace sets its interpreter factory instance,
                // now that they've been discovered by the factory provider.
                context.Initialize();

                lock (_currentContextLock) {
                    _currentContext = context;
                }

                // Let users know this workspace context is all initialized
                WorkspaceInitialized?.Invoke(this, new PythonWorkspaceContextEventArgs(context));
            }
        }
    }
}

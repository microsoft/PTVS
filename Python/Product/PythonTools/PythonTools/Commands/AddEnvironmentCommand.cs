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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.PythonTools.Infrastructure.Commands;
using Microsoft.PythonTools.Environments;

namespace Microsoft.PythonTools.Commands {
    class AddEnvironmentCommand : IAsyncCommand {
        private readonly IServiceProvider _serviceProvider;
        private readonly AddEnvironmentDialog.PageKind _page;

        public AddEnvironmentCommand(IServiceProvider serviceProvider)
            : this(serviceProvider, AddEnvironmentDialog.PageKind.VirtualEnvironment) {
        }

        public AddEnvironmentCommand(IServiceProvider serviceProvider, AddEnvironmentDialog.PageKind page) {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _page = page;
        }

        public CommandStatus Status => CommandStatus.SupportedAndEnabled;

        public Task InvokeAsync() {
            return AddEnvironmentAsync(_serviceProvider, _page);
        }

        public static Task AddEnvironmentAsync(IServiceProvider serviceProvider, AddEnvironmentDialog.PageKind page) {
            var envSwitchMgr = serviceProvider.GetPythonToolsService().EnvironmentSwitcherManager;
            var workspace = (envSwitchMgr.Context as EnvironmentSwitcherWorkspaceContext)?.Workspace;
            var project = (envSwitchMgr.Context as EnvironmentSwitcherProjectContext)?.Project;
            if (workspace == null && project == null) {
                var sln = (IVsSolution)serviceProvider.GetService(typeof(SVsSolution));
                project = sln?.EnumerateLoadedPythonProjects().FirstOrDefault();
            }

            return AddEnvironmentDialog.ShowDialogAsync(
                page,
                serviceProvider,
                project,
                workspace,
                null,
                null,
                null
            );
        }
    }
}

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

namespace Microsoft.PythonTools.Intellisense
{
    [Export(typeof(WorkspaceAnalysis))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class WorkspaceAnalysis : IDisposable
    {
        private readonly IPythonWorkspaceContextProvider _pythonWorkspaceService;
        private readonly IInterpreterOptionsService _optionsService;
        private readonly IServiceProvider _site;
        private readonly Dictionary<IPythonWorkspaceContext, WorkspaceAnalyzer> _analyzers;

        [ImportingConstructor]
        public WorkspaceAnalysis(
            [Import] IPythonWorkspaceContextProvider pythonWorkspaceService,
            [Import] IInterpreterOptionsService optionsService,
            [Import(typeof(SVsServiceProvider), AllowDefault = true)] IServiceProvider site = null
        )
        {
            _pythonWorkspaceService = pythonWorkspaceService;
            _optionsService = optionsService;
            _site = site;
            _analyzers = new Dictionary<IPythonWorkspaceContext, WorkspaceAnalyzer>();

            _pythonWorkspaceService.WorkspaceClosing += OnWorkspaceClosing;
        }

        public string WorkspaceName => _pythonWorkspaceService.Workspace?.WorkspaceName;

        public void Dispose()
        {
            _pythonWorkspaceService.WorkspaceClosing -= OnWorkspaceClosing;
        }

        public VsProjectAnalyzer TryGetWorkspaceAnalyzer()
        {
            _site.MustBeCalledFromUIThread();

            var workspace = _pythonWorkspaceService.Workspace;
            if (workspace == null)
            {
                return null;
            }

            if (_analyzers.TryGetValue(workspace, out WorkspaceAnalyzer analyzer))
            {
                return analyzer.Analyzer;
            }

            return null;
        }

        public async Task<VsProjectAnalyzer> GetAnalyzerAsync()
        {
            _site.MustBeCalledFromUIThread();

            var workspace = _pythonWorkspaceService.Workspace;
            if (workspace == null)
            {
                return null;
            }

            if (_analyzers.TryGetValue(workspace, out WorkspaceAnalyzer analyzer))
            {
                return analyzer.Analyzer;
            }

            if (workspace.CurrentFactory != null)
            {
                analyzer = new WorkspaceAnalyzer(workspace, _optionsService, _site);
                await analyzer.InitializeAsync();
                _analyzers[workspace] = analyzer;
                return analyzer.Analyzer;
            }

            return null;
        }

        private void OnWorkspaceClosing(object sender, PythonWorkspaceContextEventArgs e)
        {
            _site.GetUIThread().Invoke(() =>
            {
                CloseAnalyzer(e.Workspace);
            });
        }

        private void CloseAnalyzer(IPythonWorkspaceContext workspace)
        {
            _site.MustBeCalledFromUIThread();

            if (_analyzers.TryGetValue(workspace, out WorkspaceAnalyzer analyzer))
            {
                _analyzers.Remove(workspace);
                analyzer.Dispose();
            }
        }
    }
}

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
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Repl {
    [Export(typeof(IInteractiveEvaluatorProvider))]
    sealed class PythonReplEvaluatorProvider : IInteractiveEvaluatorProvider, IDisposable {
        private readonly IInterpreterRegistryService _interpreterService;
        private readonly IPythonWorkspaceContextProvider _workspaceContextProvider;
        private readonly IServiceProvider _serviceProvider;
        private readonly IVsSolution _solution;
        private readonly SolutionEventsListener _solutionEvents;

        private const string _prefix = "E915ECDA-2F45-4398-9E07-15A877137F44";
        private const string _projectFullPathVariable = "$(MSBuildProjectFullPath)";

        [ImportingConstructor]
        public PythonReplEvaluatorProvider(
            [Import] IInterpreterRegistryService interpreterService,
            [Import] IPythonWorkspaceContextProvider workspaceContextProvider,
            [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider
        ) {
            Debug.Assert(interpreterService != null);
            _interpreterService = interpreterService;
            _workspaceContextProvider = workspaceContextProvider;
            _serviceProvider = serviceProvider;
            _solution = (IVsSolution)_serviceProvider.GetService(typeof(SVsSolution));
            _solutionEvents = new SolutionEventsListener(_solution);
            _solutionEvents.ProjectLoaded += ProjectChanged;
            _solutionEvents.ProjectClosing += ProjectChanged;
            _solutionEvents.ProjectRenamed += ProjectChanged;
            _solutionEvents.SolutionOpened += SolutionChanged;
            _solutionEvents.SolutionClosed += SolutionChanged;
            _solutionEvents.StartListeningForChanges();
            _workspaceContextProvider.WorkspaceClosed += OnWorkspaceChanged;
            _workspaceContextProvider.WorkspaceInitialized += OnWorkspaceChanged;
        }

        private void OnWorkspaceChanged(object sender, PythonWorkspaceContextEventArgs e) {
            EvaluatorsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SolutionChanged(object sender, EventArgs e) {
            EvaluatorsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ProjectChanged(object sender, ProjectEventArgs e) {
            EvaluatorsChanged?.Invoke(this, EventArgs.Empty);
        }
        public void Dispose() {
            _solutionEvents.Dispose();
            _solutionEvents.Dispose();
            _workspaceContextProvider.WorkspaceClosed -= OnWorkspaceChanged;
            _workspaceContextProvider.WorkspaceInitialized -= OnWorkspaceChanged;
        }

        public event EventHandler EvaluatorsChanged;

        public IEnumerable<KeyValuePair<string, string>> GetEvaluators() {
            _serviceProvider.MustBeCalledFromUIThread();

            foreach (var interpreter in _interpreterService.Configurations.Where(PythonInterpreterFactoryExtensions.IsRunnable)) {
                yield return new KeyValuePair<string, string>(
                    interpreter.Description,
                    GetEvaluatorId(interpreter)
                );
            }

            var solution = _serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution;
            if (solution != null) {
                foreach (var project in solution.EnumerateLoadedPythonProjects()) {
                    if (project.IsClosed || project.IsClosing) {
                        continue;
                    }
                    yield return new KeyValuePair<string, string>(
                        Strings.ReplProjectProjectCaption.FormatUI(project.Caption),
                        GetEvaluatorId(project)
                    );
                }
            }

            var workspace = _workspaceContextProvider.Workspace;
            if (workspace != null) {
                yield return new KeyValuePair<string, string>(
                    Strings.ReplWorkspaceCaption.FormatUI(workspace.WorkspaceName),
                    GetEvaluatorId(workspace)
                );
            }
        }

        internal static string GetEvaluatorId(InterpreterConfiguration config) {
            return "{0};env;{1};{2}".FormatInvariant(
                _prefix,
                config.Description,
                config.Id
            );
        }

        internal static string GetEvaluatorId(PythonProjectNode project) {
            return "{0};project;{1};{2}".FormatInvariant(
                _prefix,
                project.Caption,
                project.GetMkDocument()
            );
        }

        internal static string GetEvaluatorId(PythonProjectNode project, InterpreterConfiguration config) {
            if (project == null) {
                throw new ArgumentNullException(nameof(project));
            }
            if (config == null) {
                throw new ArgumentNullException(nameof(config));
            }

            return GetProjectEnvironmentEvaluatorId(
                project.ProjectIDGuid,
                GetStableProjectEnvironmentInterpreterId(project.GetMkDocument(), config.Id)
            );
        }

        internal static string GetStableProjectEnvironmentInterpreterId(
            string projectMoniker,
            string interpreterId
        ) {
            var relativeId = MSBuildProjectInterpreterFactoryProvider.GetProjectRelativeId(
                projectMoniker,
                interpreterId
            );
            return relativeId == null ?
                interpreterId :
                MSBuildProjectInterpreterFactoryProvider.GetInterpreterId(
                    _projectFullPathVariable,
                    relativeId
                );
        }

        internal static string ResolveProjectEnvironmentInterpreterId(
            string projectMoniker,
            string interpreterId
        ) {
            var relativeId = MSBuildProjectInterpreterFactoryProvider.GetProjectRelativeId(
                _projectFullPathVariable,
                interpreterId
            );
            return relativeId == null || string.IsNullOrEmpty(projectMoniker) ?
                interpreterId :
                MSBuildProjectInterpreterFactoryProvider.GetInterpreterId(
                    projectMoniker,
                    relativeId
                );
        }

        internal static string GetProjectEnvironmentEvaluatorId(Guid projectGuid, string interpreterId) {
            if (projectGuid == Guid.Empty) {
                throw new ArgumentException("A project GUID is required.", nameof(projectGuid));
            }
            if (string.IsNullOrEmpty(interpreterId)) {
                throw new ArgumentException("An interpreter ID is required.", nameof(interpreterId));
            }

            return "{0};project-env;{1};{2}".FormatInvariant(
                _prefix,
                projectGuid.ToString("D"),
                Uri.EscapeDataString(interpreterId)
            );
        }

        internal static bool TryParseProjectEnvironmentEvaluatorId(
            string evaluatorId,
            out Guid projectGuid,
            out string interpreterId
        ) {
            projectGuid = Guid.Empty;
            interpreterId = null;

            var bits = evaluatorId?.Split(new[] { ';' }, 10);
            if (bits == null ||
                bits.Length != 4 ||
                !bits[0].Equals(_prefix, StringComparison.OrdinalIgnoreCase) ||
                !bits[1].Equals("project-env", StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            try {
                interpreterId = Uri.UnescapeDataString(bits[3]);
            } catch (UriFormatException) {
                return false;
            }

            return Guid.TryParse(bits[2], out projectGuid) && !string.IsNullOrEmpty(interpreterId);
        }

        internal static string GetEvaluatorId(IPythonWorkspaceContext workspace) {
            return "{0};workspace;{1};{2}".FormatInvariant(
                _prefix,
                workspace.WorkspaceName,
                workspace.Location
            );
        }

        internal static string GetTemporaryId(string key, InterpreterConfiguration config) {
            return GetEvaluatorId(config) + ";" + key;
        }


        public IInteractiveEvaluator GetEvaluator(string evaluatorId) {
            if (string.IsNullOrEmpty(evaluatorId)) {
                return null;
            }

            // Max out at 10 splits to protect against malicious IDs
            var bits = evaluatorId.Split(new[] { ';' }, 10);

            if (bits.Length < 2 || !bits[0].Equals(_prefix, StringComparison.OrdinalIgnoreCase)) {
                return null;
            }

            if (bits[1].Equals("env", StringComparison.OrdinalIgnoreCase)) {
                return GetEnvironmentEvaluator(bits.Skip(2).ToArray());
            }

            if (bits[1].Equals("project", StringComparison.OrdinalIgnoreCase)) {
                return GetProjectEvaluator(bits.Skip(2).ToArray());
            }

            if (bits[1].Equals("project-env", StringComparison.OrdinalIgnoreCase)) {
                return GetProjectEnvironmentEvaluator(evaluatorId);
            }

            if (bits[1].Equals("workspace", StringComparison.OrdinalIgnoreCase)) {
                return GetWorkspaceEvaluator(bits.Skip(2).ToArray());
            }
            return null;
        }

        private IInteractiveEvaluator GetEnvironmentEvaluator(IReadOnlyList<string> args) {
            _serviceProvider.MustBeCalledFromUIThread();

            var config = _interpreterService.FindConfiguration(args.ElementAtOrDefault(1));

            var eval = new PythonInteractiveEvaluator(_serviceProvider) {
                DisplayName = args.ElementAtOrDefault(0),
                Configuration = new LaunchConfiguration(config)
            };

            eval.Configuration.SearchPaths = _serviceProvider.GetPythonToolsService().GetGlobalPythonSearchPaths(config).ToList();

            return eval;
        }

        private IInteractiveEvaluator GetProjectEvaluator(IReadOnlyList<string> args) {
            _serviceProvider.MustBeCalledFromUIThread();

            var project = args.ElementAtOrDefault(1);

            var eval = new PythonInteractiveEvaluator(_serviceProvider) {
                DisplayName = args.ElementAtOrDefault(0),
                ProjectMoniker = project
            };

            eval.UpdatePropertiesFromProjectMoniker();

            return eval;
        }

        private IInteractiveEvaluator GetProjectEnvironmentEvaluator(string evaluatorId) {
            _serviceProvider.MustBeCalledFromUIThread();

            if (!TryParseProjectEnvironmentEvaluatorId(evaluatorId, out var projectGuid, out var interpreterId)) {
                return null;
            }

            var project = VsShellUtilities
                .GetHierarchy(_serviceProvider, projectGuid)
                ?.GetProject()
                ?.GetPythonProject();
            var config = _interpreterService.FindConfiguration(
                ResolveProjectEnvironmentInterpreterId(project?.GetMkDocument(), interpreterId)
            );
            var eval = new PythonInteractiveEvaluator(_serviceProvider) {
                DisplayName = Strings.ReplProjectEnvironmentCaption.FormatUI(
                    project?.Caption ?? projectGuid.ToString("D"),
                    config?.Description ?? interpreterId
                ),
                ProjectGuid = projectGuid,
                ProjectMoniker = project?.GetMkDocument() ?? projectGuid.ToString("D"),
                ProjectInterpreterId = interpreterId
            };

            eval.UpdatePropertiesFromProjectMoniker();
            return eval;
        }

        private IInteractiveEvaluator GetWorkspaceEvaluator(IReadOnlyList<string> args) {
            _serviceProvider.MustBeCalledFromUIThread();

            var workspace = args.ElementAtOrDefault(1);

            var eval = new PythonInteractiveEvaluator(_serviceProvider) {
                DisplayName = args.ElementAtOrDefault(0),
                WorkspaceMoniker = workspace
            };

            eval.UpdatePropertiesFromWorkspaceMoniker();

            return eval;
        }
    }

}

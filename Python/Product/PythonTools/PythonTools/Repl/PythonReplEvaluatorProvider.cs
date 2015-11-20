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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Repl {
    [Export(typeof(IInteractiveEvaluatorProvider))]
    sealed class PythonReplEvaluatorProvider : IInteractiveEvaluatorProvider, IDisposable {
        private readonly IInterpreterOptionsService _interpreterService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IVsSolution _solution;
        private readonly SolutionEventsListener _solutionEvents;

        private const string _prefix = "E915ECDA-2F45-4398-9E07-15A877137F44";

        private const string _replGuid = "FAEC7F47-85D8-4899-8D7B-0B855B732CC8";
        private const string _configurableGuid = "3C4CB167-E213-4377-8909-437139C3C553";
        private const string _configurable2Guid = "EA3C9BAE-087A-44FA-A897-18A626EC3B5D";

        [ImportingConstructor]
        public PythonReplEvaluatorProvider(
            [Import] IInterpreterOptionsService interpreterService,
            [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider
        ) {
            Debug.Assert(interpreterService != null);
            _interpreterService = interpreterService;
            _serviceProvider = serviceProvider;
            _solution = (IVsSolution)_serviceProvider.GetService(typeof(SVsSolution));
            _solutionEvents = new SolutionEventsListener(_solution);
            _solutionEvents.ProjectLoaded += ProjectChanged;
            _solutionEvents.ProjectClosing += ProjectChanged;
            _solutionEvents.ProjectRenamed += ProjectChanged;
            _solutionEvents.SolutionOpened += SolutionChanged;
            _solutionEvents.SolutionClosed += SolutionChanged;
            _solutionEvents.StartListeningForChanges();
        }

        private void SolutionChanged(object sender, EventArgs e) {
            EvaluatorsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ProjectChanged(object sender, ProjectEventArgs e) {
            EvaluatorsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() {
            _solutionEvents.Dispose();
        }

        public event EventHandler EvaluatorsChanged;

        public IEnumerable<KeyValuePair<string, string>> GetEvaluators() {
            foreach (var interpreter in _interpreterService.Interpreters) {
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
                        "Project: " + project.Caption,
                        GetEvaluatorId(project)
                    );
                }
            }
        }

        internal static string GetEvaluatorId(IPythonInterpreterFactory factory) {
            return string.Format("{0};env;{1};{2};{3}",
                _prefix,
                factory.Description,
                factory.Configuration.InterpreterPath,
                factory.Configuration.Version
            );
        }

        internal static string GetEvaluatorId(PythonProjectNode project) {
            return string.Format("{0};project;{1};{2}",
                _prefix,
                project.Caption,
                project.GetMkDocument()
            );
        }


        public IInteractiveEvaluator GetEvaluator(string evaluatorId) {
            if (string.IsNullOrEmpty(evaluatorId)) {
                return null;
            }

            // Handle legacy IDs
            if (evaluatorId.StartsWith(_replGuid, StringComparison.OrdinalIgnoreCase) ||
                evaluatorId.StartsWith(_configurableGuid, StringComparison.OrdinalIgnoreCase) ||
                evaluatorId.StartsWith(_configurable2Guid, StringComparison.OrdinalIgnoreCase)) {
                return GetLegacyEvaluator(evaluatorId);
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

            return null;
        }

        private static PythonLanguageVersion GetVersion(string versionStr) {
            Version version;
            if (string.IsNullOrEmpty(versionStr)) {
                return PythonLanguageVersion.None;
            }

            return (Version.TryParse(versionStr, out version) ? version : new Version()).ToLanguageVersion();
        }

        private static PythonLanguageVersion GetVersion(PythonProjectNode project) {
            return (project.GetInterpreterFactory()?.Configuration.Version ?? new Version()).ToLanguageVersion();
        }

        private IInteractiveEvaluator GetEnvironmentEvaluator(IReadOnlyList<string> args) {
            var eval = new PythonInteractiveEvaluator(_serviceProvider) {
                DisplayName = args.ElementAtOrDefault(0),
                InterpreterPath = args.ElementAtOrDefault(1),
                LanguageVersion = GetVersion(args.ElementAtOrDefault(2)),
                WorkingDirectory = args.ElementAtOrDefault(3)
            };

            eval.ScriptsPath = GetScriptsPath(null, eval.DisplayName)
                ?? GetScriptsPath(null, eval.LanguageVersion.ToVersion().ToString());

            return eval;
        }

        private IInteractiveEvaluator GetProjectEvaluator(IReadOnlyList<string> args) {
            var project = args.ElementAtOrDefault(1);

            IVsHierarchy hier;
            if (string.IsNullOrEmpty(project) ||
                ErrorHandler.Failed(_solution.GetProjectOfUniqueName(project, out hier))) {
                return null;
            }
            var pyProj = hier?.GetProject()?.GetPythonProject();
            if (pyProj == null) {
                return null;
            }

            var props = PythonProjectLaunchProperties.Create(pyProj);
            if (props == null) {
                return null;
            }

            var eval = new PythonInteractiveEvaluator(_serviceProvider) {
                DisplayName = args.ElementAtOrDefault(0),
                InterpreterPath = props.GetInterpreterPath(),
                InterpreterArguments = props.GetInterpreterArguments(),
                LanguageVersion = GetVersion(pyProj),
                WorkingDirectory = props.GetWorkingDirectory(),
                EnvironmentVariables = props.GetEnvironment(true)
            };

            eval.ScriptsPath = GetScriptsPath(pyProj.ProjectHome, "Scripts");

            return eval;
        }

        private string GetScriptsPath(string root, params string[] parts) {
            if (string.IsNullOrEmpty(root)) {
                // TODO: Allow customizing the scripts path
                //root = _serviceProvider.GetPythonToolsService().InteractiveOptions.ScriptsPath;
                if (string.IsNullOrEmpty(root)) {
                    root = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    parts = new[] { "Visual Studio " + AssemblyVersionInfo.VSName, "Python Scripts" }
                        .Concat(parts).ToArray();
                }
            }
            if (parts.Length > 0) {
                try {
                    root = CommonUtils.GetAbsoluteDirectoryPath(root, Path.Combine(parts));
                } catch (ArgumentException) {
                    return null;
                }
            }

            if (!Directory.Exists(root)) {
                return null;
            }

            return root;
        }

        private IInteractiveEvaluator GetLegacyEvaluator(string replId) {
                if (replId.StartsWith(_replGuid, StringComparison.OrdinalIgnoreCase)) {
                string[] components = replId.Split(new[] { ' ' }, 3);
                if (components.Length == 3) {
                    return PythonReplEvaluator.Create(
                        _serviceProvider,
                        components[1],
                        components[2],
                        _interpreterService
                    );
                }
            } else if (replId.StartsWith(_configurableGuid, StringComparison.OrdinalIgnoreCase)) {
                return CreateConfigurableEvaluator(replId);
            } else if (replId.StartsWith(_configurable2Guid, StringComparison.OrdinalIgnoreCase)) {
                return new PythonReplEvaluatorDontPersist(
                    null,
                    _serviceProvider,
                    new ConfigurablePythonReplOptions(),
                    _interpreterService
                );
            }
            return null;
        }

        /// <summary>
        /// Creates an interactive evaluator programmatically for some plugin
        /// </summary>
        private IInteractiveEvaluator CreateConfigurableEvaluator(string replId) {
            string[] components = replId.Split(new[] { '|' }, 5);
            if (components.Length == 5) {
                string interpreter = components[1];
                string interpreterVersion = components[2];
                // string userId = components[3];
                // We don't care about the user identifier - it is there to
                // ensure that evaluators can be distinguished within a project
                // and/or with the same interpreter.
                string projectName = components[4];

                var factory = _interpreterService.FindInterpreter(interpreter, interpreterVersion);

                if (factory != null) {
                    var replOptions = new ConfigurablePythonReplOptions();
                    replOptions.InterpreterFactory = factory;

                    var solution = _serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution;
                    if (solution != null) {
                        foreach (var pyProj in solution.EnumerateLoadedPythonProjects()) {
                            var name = ((IVsHierarchy)pyProj).GetRootCanonicalName();
                            if (string.Equals(name, projectName, StringComparison.OrdinalIgnoreCase)) {
                                replOptions.Project = pyProj;
                                break;
                            }
                        }
                    }

                    return new PythonReplEvaluatorDontPersist(
                        replOptions.InterpreterFactory,
                        _serviceProvider,
                        replOptions,
                        _interpreterService
                    );
                }
            }
            return null;
        }

        internal static string GetReplId(IPythonInterpreterFactory interpreter, PythonProjectNode project = null) {
            return GetReplId(interpreter, project, false);
        }

        internal static string GetReplId(IPythonInterpreterFactory interpreter, PythonProjectNode project, bool alwaysPerProject) {
            if (alwaysPerProject || project != null && project.Interpreters.IsProjectSpecific(interpreter)) {
                return GetConfigurableReplId(interpreter, (IVsHierarchy)project, "");
            } else {
                return String.Format("{0} {1} {2}",
                    _replGuid,
                    interpreter.Id,
                    interpreter.Configuration.Version
                );
            }
        }

        internal static string GetConfigurableReplId(string userId) {
            return String.Format("{0}|{1}",
                _configurable2Guid,
                userId
            );
        }

        internal static string GetConfigurableReplId(IPythonInterpreterFactory interpreter, IVsHierarchy project, string userId) {
            return String.Format("{0}|{1}|{2}|{3}|{4}",
                _configurableGuid,
                interpreter.Id,
                interpreter.Configuration.Version,
                userId,
                project != null ? project.GetRootCanonicalName() : ""
            );
        }
    }

}

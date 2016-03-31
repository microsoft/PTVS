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
using System.ComponentModel.Composition;
using System.Diagnostics;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Repl {
    [Export(typeof(IInteractiveEvaluatorProvider))]
    class PythonReplEvaluatorProvider : IInteractiveEvaluatorProvider {
        readonly IInterpreterRegistryService _interpreterService;
        readonly IServiceProvider _serviceProvider;

        private const string _replGuid = "FAEC7F47-85D8-4899-8D7B-0B855B732CC8";
        private const string _configurableGuid = "3C4CB167-E213-4377-8909-437139C3C553";
        private const string _configurable2Guid = "EA3C9BAE-087A-44FA-A897-18A626EC3B5D";

        [ImportingConstructor]
        public PythonReplEvaluatorProvider(
            [Import] IInterpreterRegistryService interpreterService,
            [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider
        ) {
            Debug.Assert(interpreterService != null);
            _interpreterService = interpreterService;
            _serviceProvider = serviceProvider;
        }

        public IInteractiveEvaluator GetEvaluator(string replId) {
            if (replId.StartsWith(_replGuid, StringComparison.OrdinalIgnoreCase)) {
                string[] components = replId.Split(new[] { ' ' }, 2);
                if (components.Length == 2) {
                    return PythonReplEvaluator.Create(
                        _serviceProvider,
                        components[1],
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
            string[] components = replId.Split(new[] { '|' }, 2);
            if (components.Length == 2) {
                string interpreter = components[1];

                var factory = _interpreterService.FindInterpreter(interpreter);

                if (factory != null) {
                    var replOptions = new ConfigurablePythonReplOptions();
                    replOptions.InterpreterFactory = factory;

                    if (interpreter.StartsWith(MSBuildProjectInterpreterFactoryProvider.MSBuildProviderName + "|")) {
                        string[] projectComponents = interpreter.Split(new[] { '|' }, 3);
                        string projectName = projectComponents[2];

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

        internal static string GetReplId(string id, PythonProjectNode project = null) {
            return GetReplId(id, project, false);
        }

        internal static string GetReplId(string id, PythonProjectNode project, bool alwaysPerProject) {
            if (alwaysPerProject || IsProjectSpecific(id, project)) {
                return GetConfigurableReplId(id, (IVsHierarchy)project);
            } else {
                return String.Format("{0} {1}",
                    _replGuid,
                    id
                );
            }
        }

        private static bool IsProjectSpecific(string id, PythonProjectNode project) {
            if (project != null) {
                var vsProjectContext = project.Site.GetComponentModel().GetService<VsProjectContextProvider>();
                return vsProjectContext.IsProjectSpecific(id);
            }
            return false;
        }

        internal static string GetConfigurableReplId(string userId) {
            return String.Format("{0}|{1}",
                _configurable2Guid,
                userId
            );
        }

        internal static string GetConfigurableReplId(string id, IVsHierarchy project) {
            return String.Format("{0}|{1}",
                _configurableGuid,
                id
            );
        }
    }

}

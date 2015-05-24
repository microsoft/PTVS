/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
#if DEV14_OR_LATER
using Microsoft.VisualStudio.InteractiveWindow;
#else
using Microsoft.VisualStudio.Repl;
#endif

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Repl {
#if DEV14_OR_LATER
using IReplEvaluator = IInteractiveEvaluator;
using IReplEvaluatorProvider = Microsoft.PythonTools.Repl.IInteractiveEvaluatorProvider;
#endif

    [Export(typeof(IReplEvaluatorProvider))]
    class PythonReplEvaluatorProvider : IReplEvaluatorProvider {
        readonly IInterpreterOptionsService _interpreterService;
        readonly IServiceProvider _serviceProvider;

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
        }

        #region IReplEvaluatorProvider Members

        public IReplEvaluator GetEvaluator(string replId) {
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
                return CreateConfigurableInterpreter(replId);
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
        /// Creates an interpreter which was created programmatically by some plugin
        /// </summary>
        private IReplEvaluator CreateConfigurableInterpreter(string replId) {
            string[] components = replId.Split(new[] { '|' }, 5);
            if (components.Length == 5) {
                string interpreter = components[1];
                string interpreterVersion = components[2];
                // string userId = components[3];
                // We don't care about the user identifier - it is there to
                // ensure that evaluators can be distinguished within a project
                // and/or with the same interpreter.
                string projectName = components[4];

                var replOptions = new ConfigurablePythonReplOptions();
                replOptions.InterpreterFactory = _interpreterService.FindInterpreter(interpreter, interpreterVersion);

                if (replOptions.InterpreterFactory == null) {
                    var solution = _serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution;
                    if (solution != null) {
                        foreach (var proj in solution.EnumerateLoadedProjects()) {
                            if (!proj.GetRootCanonicalName().Equals(projectName, StringComparison.Ordinal)) {
                                continue;
                            }
                            var pyProj = proj.GetPythonProject();
                            if (pyProj == null) {
                                continue;
                            }
                            replOptions.InterpreterFactory = pyProj.Interpreters.FindInterpreter(interpreter, interpreterVersion);
                            replOptions.Project = pyProj;
                            break;
                        }
                    }
                }

                if (replOptions.InterpreterFactory != null) {
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

        #endregion

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

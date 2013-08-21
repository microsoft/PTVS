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
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Repl {
#if INTERACTIVE_WINDOW
    using IReplEvaluator = IInteractiveEngine;
    using IReplEvaluatorProvider = IInteractiveEngineProvider;
#endif
    
    [Export(typeof(IReplEvaluatorProvider))]
    class PythonReplEvaluatorProvider : IReplEvaluatorProvider {
        readonly IInterpreterOptionsService _interpreterService;
        readonly IErrorProviderFactory _errorProviderFactory;
        readonly IServiceProvider _serviceProvider;

        private const string _replGuid = "FAEC7F47-85D8-4899-8D7B-0B855B732CC8";
        internal const string _configurableGuid = "3C4CB167-E213-4377-8909-437139C3C553";

        [ImportingConstructor]
        public PythonReplEvaluatorProvider(
            [Import] IInterpreterOptionsService interpreterService,
            [Import] IErrorProviderFactory errorProviderFactory,
            [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider) {
            Debug.Assert(interpreterService != null);
            Debug.Assert(errorProviderFactory != null);
            _interpreterService = interpreterService;
            _errorProviderFactory = errorProviderFactory;
            _serviceProvider = serviceProvider;
        }

        #region IReplEvaluatorProvider Members

        public IReplEvaluator GetEvaluator(string replId) {
            if (replId.StartsWith(_replGuid)) {
                string[] components = replId.Split(new[] { ' ' }, 3);
                if (components.Length == 3) {
                    var interpreter = _interpreterService.FindInterpreter(components[1], components[2]);
                    if (interpreter != null) {
                        return new PythonReplEvaluator(interpreter, _errorProviderFactory, _interpreterService);
                    }
                }
            } else if (replId.StartsWith(_configurableGuid)) {
                return CreateConfigurableInterpreter(replId);
            }
            return null;
        }

        /// <summary>
        /// Creates an interpreter which was created programmatically by some plugin
        /// </summary>
        private IReplEvaluator CreateConfigurableInterpreter(string replId) {
            string[] components = replId.Split(new[] { '|' }, 7);
            if (components.Length == 6 || components.Length == 7) {
                string workingDir = components[1];
                string interpreter = components[2];
                string interpreterVersion = components[3];
                string envVars = components[5];
                // we don't care about the user identifier

                var pyInterpreter = _interpreterService.FindInterpreter(interpreter, interpreterVersion);

                if (pyInterpreter == null && components.Length == 7) {
                    string projectName = components[6];
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
                            pyInterpreter = pyProj.Interpreters.FindInterpreter(interpreter, interpreterVersion);
                            break;
                        }
                    }
                }

                if (pyInterpreter != null) {
                    var replOptions = new ConfigurablePythonReplOptions(
                        workingDir
                    );

                    return new PythonReplEvaluatorDontPersist(pyInterpreter, _errorProviderFactory, replOptions, envVars, _interpreterService);
                }
            }
            return null;
        }

        #endregion

        internal static string GetReplId(IPythonInterpreterFactory interpreter, PythonProjectNode project = null) {
            if (project != null && project.Interpreters.IsProjectSpecific(interpreter)) {
                return String.Format("{0}|{1}|{2}|{3}|{4};{5}||{6}",
                    PythonReplEvaluatorProvider._configurableGuid,
                    project.GetWorkingDirectory(),
                    interpreter.Id,
                    interpreter.Configuration.Version,
                    project.ProjectIDGuid,
                    interpreter.Id,
                    ((IVsProject)project).GetRootCanonicalName()
                );
            } else {
                return _replGuid + " " +
                    interpreter.Id + " " +
                    interpreter.Configuration.Version;
            }
        }
    }

}

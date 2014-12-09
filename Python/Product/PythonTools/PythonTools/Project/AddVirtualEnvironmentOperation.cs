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
using System.IO;
using System.Threading.Tasks;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    sealed class AddVirtualEnvironmentOperation {
        private readonly PythonProjectNode _project;
        private readonly string _virtualEnvPath;
        private readonly IPythonInterpreterFactory _baseInterpreter;
        private readonly bool _create;
        private readonly bool _useVEnv;
        private readonly bool _installRequirements;
        private readonly Redirector _output;
        
        public AddVirtualEnvironmentOperation(
            PythonProjectNode project,
            string virtualEnvPath,
            IPythonInterpreterFactory baseInterpreter,
            bool create,
            bool useVEnv,
            bool installRequirements,
            Redirector output = null
        ) {
            _project = project;
            _virtualEnvPath = virtualEnvPath;
            _baseInterpreter = baseInterpreter;
            _create = create;
            _useVEnv = useVEnv;
            _installRequirements = installRequirements;
            _output = output;
        }

        private void WriteOutput(string resourceKey, params object[] args) {
            if (_output != null) {
                _output.WriteLine(SR.GetString(resourceKey, args));
            }
        }

        private void WriteError(string resourceKey, params object[] args) {
            if (_output != null) {
                _output.WriteErrorLine(SR.GetString(resourceKey, args));
            }
        }
        
        public async Task Run() {
            var service = _project.Site.GetComponentModel().GetService<IInterpreterOptionsService>();

            var factory = await _project.CreateOrAddVirtualEnvironment(
                service,
                _create,
                _virtualEnvPath,
                _baseInterpreter,
                _useVEnv
            );

            if (factory == null) {
                return;
            }

            var txt = CommonUtils.GetAbsoluteFilePath(_project.ProjectHome, "requirements.txt");
            if (!_installRequirements || !File.Exists(txt)) {
                return;
            }

            WriteOutput(SR.RequirementsTxtInstalling, txt);
            if (await Pip.Install(
                _project.Site,
                factory,
                "-r " + ProcessOutput.QuoteSingleArgument(txt),
                false,  // never elevate for a virtual environment
                _output
            )) {
                WriteOutput(SR.PackageInstallSucceeded, Path.GetFileName(txt));
            } else {
                WriteOutput(SR.PackageInstallFailed, Path.GetFileName(txt));
            }
        }

    }
}

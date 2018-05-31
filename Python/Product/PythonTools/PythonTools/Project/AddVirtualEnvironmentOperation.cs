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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Project {
    sealed class AddVirtualEnvironmentOperation {
        private readonly PythonProjectNode _project;
        private readonly string _virtualEnvPath;
        private readonly string _baseInterpreter;
        private readonly bool _create;
        private readonly bool _useVEnv;
        private readonly bool _installRequirements;
        private readonly string _requirementsPath;
        private readonly Redirector _output;
        
        public AddVirtualEnvironmentOperation(
            PythonProjectNode project,
            string virtualEnvPath,
            string baseInterpreterId,
            bool create,
            bool useVEnv,
            bool installRequirements,
            string requirementsPath,
            Redirector output = null
        ) {
            _project = project;
            _virtualEnvPath = virtualEnvPath;
            _baseInterpreter = baseInterpreterId;
            _create = create;
            _useVEnv = useVEnv;
            _installRequirements = installRequirements;
            _requirementsPath = requirementsPath;
            _output = output;
        }

        private void WriteOutput(string message) {
            if (_output != null) {
                _output.WriteLine(message);
            }
        }

        private void WriteError(string message) {
            if (_output != null) {
                _output.WriteErrorLine(message);
            }
        }
        
        public async Task Run() {
            var service = _project.Site.GetComponentModel().GetService<IInterpreterRegistryService>();

            IPythonInterpreterFactory factory;
            try {
                var baseInterp = service.FindInterpreter(_baseInterpreter);

                factory = await _project.CreateOrAddVirtualEnvironment(
                    service,
                    _create,
                    _virtualEnvPath,
                    baseInterp,
                    _useVEnv
                );
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                WriteError(ex.Message);
                factory = null;
            }

            if (factory == null) {
                return;
            }

            var txt = _requirementsPath;
            if (!_installRequirements || !File.Exists(txt)) {
                return;
            }

            var interpreterOpts = _project.Site.GetComponentModel().GetService<IInterpreterOptionsService>();
            var pm = interpreterOpts?.GetPackageManagers(factory).FirstOrDefault(p => p.UniqueKey == "pip");
            if (pm == null) {
                WriteError(
                    Strings.PackageManagementNotSupported_Package.FormatUI(PathUtils.GetFileOrDirectoryName(txt))
                );
                return;
            }

            WriteOutput(Strings.RequirementsTxtInstalling.FormatUI(txt));
            bool success = false;
            try {
                var ui = new VsPackageManagerUI(_project.Site);
                if (!pm.IsReady) {
                    await pm.PrepareAsync(ui, CancellationToken.None);
                }
                success = await pm.InstallAsync(
                    PackageSpec.FromArguments("-r " + ProcessOutput.QuoteSingleArgument(txt)),
                    ui,
                    CancellationToken.None
                );
            } catch (InvalidOperationException ex) {
                WriteOutput(ex.Message);
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                WriteOutput(ex.Message);
                Debug.Fail(ex.ToUnhandledExceptionMessage(GetType()));
            }

            if (success) {
                WriteOutput(Strings.PackageInstallSucceeded.FormatUI(Path.GetFileName(txt)));
            } else {
                WriteOutput(Strings.PackageInstallFailed.FormatUI(Path.GetFileName(txt)));
            }
        }
    }
}

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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.InterpreterList;
using Microsoft.PythonTools.Logging;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.TaskStatusCenter;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Environments {
    sealed class AddVirtualEnvironmentOperation {
        private readonly IServiceProvider _site;
        private readonly PythonProjectNode _project;
        private readonly IPythonWorkspaceContext _workspace;
        private readonly string _virtualEnvPath;
        private readonly string _baseInterpreter;
        private readonly bool _useVEnv;
        private readonly bool _installReqs;
        private readonly string _reqsPath;
        private readonly bool _registerAsCustomEnv;
        private readonly string _customEnvName;
        private readonly bool _setAsCurrent;
        private readonly bool _setAsDefault;
        private readonly bool _viewInEnvWindow;
        private readonly Redirector _output;
        private readonly IVsTaskStatusCenterService _statusCenter;
        private readonly IInterpreterRegistryService _registry;
        private readonly IInterpreterOptionsService _options;
        private readonly IPythonToolsLogger _logger;

        public AddVirtualEnvironmentOperation(
            IServiceProvider site,
            PythonProjectNode project,
            IPythonWorkspaceContext workspace,
            string virtualEnvPath,
            string baseInterpreterId,
            bool useVEnv,
            bool installRequirements,
            string requirementsPath,
            bool registerAsCustomEnv,
            string customEnvName,
            bool setAsCurrent,
            bool setAsDefault,
            bool viewInEnvWindow,
            Redirector output = null
        ) {
            _site = site ?? throw new ArgumentNullException(nameof(site));
            _project = project;
            _workspace = workspace;
            _virtualEnvPath = virtualEnvPath ?? throw new ArgumentNullException(nameof(virtualEnvPath));
            _baseInterpreter = baseInterpreterId ?? throw new ArgumentNullException(nameof(baseInterpreterId));
            _useVEnv = useVEnv;
            _installReqs = installRequirements;
            _reqsPath = requirementsPath;
            _registerAsCustomEnv = registerAsCustomEnv;
            _customEnvName = customEnvName;
            _setAsCurrent = setAsCurrent;
            _setAsDefault = setAsDefault;
            _viewInEnvWindow = viewInEnvWindow;
            _output = output;
            _statusCenter = _site.GetService(typeof(SVsTaskStatusCenterService)) as IVsTaskStatusCenterService;
            _registry = _site.GetComponentModel().GetService<IInterpreterRegistryService>();
            _options = _site.GetComponentModel().GetService<IInterpreterOptionsService>();
            _logger = _site.GetService(typeof(IPythonToolsLogger)) as IPythonToolsLogger;
        }

        public async Task RunAsync() {
            var outputWindow = OutputWindowRedirector.GetGeneral(_site);
            var taskHandler = _statusCenter.PreRegister(
                new TaskHandlerOptions() {
                    ActionsAfterCompletion = CompletionActions.RetainAndNotifyOnFaulted | CompletionActions.RetainAndNotifyOnRanToCompletion,
                    Title = Strings.VirtualEnvStatusCenterCreateTitle.FormatUI(PathUtils.GetFileOrDirectoryName(_virtualEnvPath)),
                    DisplayTaskDetails = (t) => { outputWindow.ShowAndActivate(); }
                },
                new TaskProgressData() {
                    CanBeCanceled = false,
                    ProgressText = Strings.VirtualEnvStatusCenterCreateProgressPreparing,
                    PercentComplete = null,
                }
            );

            var task = CreateVirtualEnvironmentAsync(taskHandler);
            taskHandler?.RegisterTask(task);
            _site.ShowTaskStatusCenter();
        }

        private async Task CreateVirtualEnvironmentAsync(ITaskHandler taskHandler) {
            IPythonInterpreterFactory factory = null;

            bool failed = true;
            var baseInterp = _registry.FindInterpreter(_baseInterpreter);

            try {
                factory = await VirtualEnv.CreateAndAddFactory(
                    _site,
                    _registry,
                    _options,
                    _project,
                    _workspace,
                    _virtualEnvPath,
                    baseInterp,
                    _registerAsCustomEnv,
                    _customEnvName,
                    _useVEnv
                );

                if (factory != null) {
                    if (_installReqs && File.Exists(_reqsPath)) {
                        await InstallPackagesAsync(taskHandler, factory);
                    }

                    await _site.GetUIThread().InvokeTask(async () => {
                        // Note that for a workspace, VirtualEnv.CreateAndAddFactory
                        // takes care of updating PythonSettings.json, as that is
                        // required in order to obtain the factory. So no need to do
                        // anything here for workspace.
                        if (_project != null) {
                            _project.AddInterpreter(factory.Configuration.Id);
                            if (_setAsCurrent) {
                                _project.SetInterpreterFactory(factory);
                            }
                        }

                        if (_setAsDefault && _options != null) {
                            _options.DefaultInterpreter = factory;
                        }

                        if (_viewInEnvWindow) {
                            await InterpreterListToolWindow.OpenAtAsync(_site, factory);
                        }
                    });
                }

                failed = false;
            } finally {
                _logger?.LogEvent(PythonLogEvent.CreateVirtualEnv, new CreateVirtualEnvInfo() {
                    Failed = failed,
                    InstallRequirements = _installReqs,
                    UseVEnv = _useVEnv,
                    Global = _registerAsCustomEnv,
                    LanguageVersion = baseInterp?.Configuration?.Version.ToString() ?? "",
                    Architecture = baseInterp?.Configuration?.ArchitectureString ?? "",
                    SetAsDefault = _setAsDefault,
                    SetAsCurrent = _setAsCurrent,
                    OpenEnvironmentsWindow = _viewInEnvWindow,
                });
            }

            taskHandler?.Progress.Report(new TaskProgressData() {
                CanBeCanceled = false,
                ProgressText = Strings.VirtualEnvStatusCenterCreateProgressCompleted,
                PercentComplete = 100,
            });
        }

        private async Task InstallPackagesAsync(ITaskHandler taskHandler, IPythonInterpreterFactory factory) {
            taskHandler?.Progress.Report(new TaskProgressData() {
                CanBeCanceled = false,
                ProgressText = Strings.VirtualEnvStatusCenterCreateProgressInstallingPackages,
                PercentComplete = null,
            });

            var pm = _options?.GetPackageManagers(factory).FirstOrDefault(p => p.UniqueKey == "pip");
            if (pm != null) {
                var operation = new InstallPackagesOperation(_site, pm, _reqsPath, _output);
                await operation.InstallPackagesAsync();
            } else {
                throw new ApplicationException(Strings.PackageManagementNotSupported_Package.FormatUI(PathUtils.GetFileOrDirectoryName(_reqsPath)));
            }
        }
    }
}

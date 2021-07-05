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

using Microsoft.PythonTools.InterpreterList;
using Microsoft.PythonTools.Project;

namespace Microsoft.PythonTools.Environments {
    sealed class AddCondaEnvironmentOperation {
        private readonly IServiceProvider _site;
        private readonly ICondaEnvironmentManager _condaMgr;
        private readonly PythonProjectNode _project;
        private readonly IPythonWorkspaceContext _workspace;
        private readonly string _envNameOrPath;
        private readonly string _actualName;
        private readonly string _envFilePath;
        private readonly List<PackageSpec> _packages;
        private readonly bool _setAsCurrent;
        private readonly bool _setAsDefault;
        private readonly bool _viewInEnvWindow;
        private readonly OutputWindowRedirector _outputWindow;
        private readonly IVsStatusbar _statusBar;
        private readonly bool _showAndActiveOutputWindow;
        private readonly IVsTaskStatusCenterService _statusCenter;
        private readonly IInterpreterRegistryService _registry;
        private readonly IInterpreterOptionsService _options;
        private readonly IPythonToolsLogger _logger;
        private readonly CondaEnvironmentFactoryProvider _factoryProvider;

        public AddCondaEnvironmentOperation(
            IServiceProvider site,
            ICondaEnvironmentManager condaMgr,
            PythonProjectNode project,
            IPythonWorkspaceContext workspace,
            string envNameOrPath,
            string envFilePath,
            List<PackageSpec> packages,
            bool setAsCurrent,
            bool setAsDefault,
            bool viewInEnvWindow
        ) {
            _site = site ?? throw new ArgumentNullException(nameof(site));
            _condaMgr = condaMgr ?? throw new ArgumentNullException(nameof(condaMgr));
            _project = project;
            _workspace = workspace;
            _envNameOrPath = envNameOrPath ?? throw new ArgumentNullException(nameof(envNameOrPath));
            _envFilePath = envFilePath;
            _packages = packages ?? throw new ArgumentNullException(nameof(packages));
            _setAsCurrent = setAsCurrent;
            _setAsDefault = setAsDefault;
            _viewInEnvWindow = viewInEnvWindow;

            // If passed a path, the actual name reported by conda will the last part
            _actualName = PathUtils.GetFileOrDirectoryName(_envNameOrPath);
            if (_actualName.Length == 0) {
                _actualName = _envNameOrPath;
            }

            _outputWindow = OutputWindowRedirector.GetGeneral(_site);
            _statusBar = _site.GetService(typeof(SVsStatusbar)) as IVsStatusbar;
            _showAndActiveOutputWindow = _site.GetPythonToolsService().GeneralOptions.ShowOutputWindowForVirtualEnvCreate;
            _statusCenter = _site.GetService(typeof(SVsTaskStatusCenterService)) as IVsTaskStatusCenterService;
            _registry = _site.GetComponentModel().GetService<IInterpreterRegistryService>();
            _options = _site.GetComponentModel().GetService<IInterpreterOptionsService>();
            _logger = _site.GetService(typeof(IPythonToolsLogger)) as IPythonToolsLogger;
            _factoryProvider = _site.GetComponentModel().GetService<CondaEnvironmentFactoryProvider>();
        }

        public async Task RunAsync() {
            var taskHandler = _statusCenter?.PreRegister(
                new TaskHandlerOptions() {
                    ActionsAfterCompletion = CompletionActions.RetainAndNotifyOnFaulted | CompletionActions.RetainAndNotifyOnRanToCompletion,
                    Title = Strings.CondaStatusCenterCreateTitle.FormatUI(_actualName),
                    DisplayTaskDetails = (t) => { _outputWindow.ShowAndActivate(); }
                },
                new TaskProgressData() {
                    CanBeCanceled = false,
                    ProgressText = Strings.CondaStatusCenterCreateProgressPreparing,
                    PercentComplete = null,
                }
            );

            var ui = new CondaUI(taskHandler, _outputWindow, _showAndActiveOutputWindow);
            _statusBar?.SetText(Strings.CondaStatusBarCreateStarted.FormatUI(_actualName));

            var task = Task.Run(() => CreateCondaEnvironmentAsync(ui, taskHandler, taskHandler?.UserCancellation ?? CancellationToken.None));
            taskHandler.RegisterTask(task);
            _site.ShowTaskStatusCenter();
        }

        private async Task CreateCondaEnvironmentAsync(CondaUI ui, ITaskHandler taskHandler, CancellationToken ct) {
            bool createdCondaEnvNoPython = false;

            try {
                var factory = await CreateFactoryAsync(ui, taskHandler, ct);
                if (factory == null) {
                    createdCondaEnvNoPython = true;
                } else {
                    await _site.GetUIThread().InvokeTask(async () => {
                        if (_project != null) {
                            _project.AddInterpreter(factory.Configuration.Id);
                            if (_setAsCurrent) {
                                _project.SetInterpreterFactory(factory);
                            }
                        } else if (_workspace != null) {
                            await _workspace.SetInterpreterFactoryAsync(factory);
                        }

                        if (_setAsDefault && _options != null) {
                            _options.DefaultInterpreter = factory;
                        }

                        if (_viewInEnvWindow) {
                            await InterpreterListToolWindow.OpenAtAsync(_site, factory);
                        }
                    });

                    taskHandler?.Progress.Report(new TaskProgressData() {
                        CanBeCanceled = false,
                        ProgressText = Strings.CondaStatusCenterCreateProgressCompleted,
                        PercentComplete = 100,
                    });

                    _statusBar?.SetText(Strings.CondaStatusBarCreateSucceeded.FormatUI(_actualName));
                }
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                _statusBar?.SetText(Strings.CondaStatusBarCreateFailed.FormatUI(_actualName));
                ui.OnErrorTextReceived(_condaMgr, ex.Message);
                throw;
            }

            if (createdCondaEnvNoPython) {
                ui.OnErrorTextReceived(_condaMgr, Strings.CondaEnvCreatedWithoutPython.FormatUI(_actualName));
                _statusBar?.SetText(Strings.CondaEnvNotDetected);
                throw new ApplicationException(Strings.CondaEnvNotDetected);
            }
        }

        private async Task<IPythonInterpreterFactory> CreateFactoryAsync(CondaUI ui, ITaskHandler taskHandler, CancellationToken ct) {
            // Force discovery, don't respect the ignore nofitication ref count,
            // which won't go back to 0 if multiple conda environments are being
            // created around the same time. We need the discovery in order to find
            // the new factory.
            using (_factoryProvider?.SuppressDiscoverFactories(forceDiscoveryOnDispose: true)) {
                taskHandler?.Progress.Report(new TaskProgressData() {
                    CanBeCanceled = false,
                    ProgressText = Strings.CondaStatusCenterCreateProgressCreating,
                    PercentComplete = null,
                });

                bool failed = true;
                bool useEnvFile = !string.IsNullOrEmpty(_envFilePath);

                try {
                    if (useEnvFile) {
                        if (!await _condaMgr.CreateFromEnvironmentFileAsync(
                            _envNameOrPath,
                            _envFilePath,
                            ui,
                            ct
                        )) {
                            throw new ApplicationException(Strings.CondaStatusCenterCreateFailure);
                        }
                    } else {
                        if (!await _condaMgr.CreateAsync(
                            _envNameOrPath,
                            _packages.ToArray(),
                            ui,
                            ct
                        )) {
                            throw new ApplicationException(Strings.CondaStatusCenterCreateFailure);
                        }
                    }

                    failed = false;
                } finally {
                    _logger?.LogEvent(PythonLogEvent.CreateCondaEnv, new CreateCondaEnvInfo() {
                        Failed = failed,
                        FromEnvironmentFile = useEnvFile,
                        SetAsDefault = _setAsDefault,
                        SetAsCurrent = _setAsCurrent,
                        OpenEnvironmentsWindow = _viewInEnvWindow,
                    });
                }
            }

            var expectedId = CondaEnvironmentFactoryConstants.GetInterpreterId(
                CondaEnvironmentFactoryProvider.EnvironmentCompanyName,
                _actualName
            );

            // This will return null if the environment that was created does
            // not contain a python interpreter. Common case for this is
            // when the package list the user has entered is empty string.
            var factory = _factoryProvider?.GetInterpreterFactory(expectedId);

            return factory;
        }

        class CondaUI : ICondaEnvironmentManagerUI, IPackageManagerUI {
            private readonly ITaskHandler _taskHandler;
            private readonly Redirector _outputWindow;
            private readonly bool _showAndActiveOutputWindow;

            public CondaUI(ITaskHandler taskHandler, Redirector outputWindow, bool showAndActiveOutputWindow) {
                _taskHandler = taskHandler;
                _outputWindow = outputWindow ?? throw new ArgumentNullException(nameof(outputWindow));
                _showAndActiveOutputWindow = showAndActiveOutputWindow;
            }

            public void OnOperationStarted(ICondaEnvironmentManager sender, string operation) {
                if (_showAndActiveOutputWindow) {
                    _outputWindow.ShowAndActivate();
                }
            }

            public void OnOperationFinished(ICondaEnvironmentManager sender, string operation, bool success) {
                if (_showAndActiveOutputWindow) {
                    _outputWindow.ShowAndActivate();
                }
            }

            public void OnOutputTextReceived(ICondaEnvironmentManager sender, string text) {
                _outputWindow.WriteLine(text.TrimEndNewline());
                _taskHandler?.Progress.Report(new TaskProgressData() {
                    CanBeCanceled = false,
                    ProgressText = text,
                    PercentComplete = null
                });
            }

            public void OnErrorTextReceived(ICondaEnvironmentManager sender, string text) {
                _outputWindow.WriteErrorLine(text.TrimEndNewline());
            }

            public void OnOutputTextReceived(IPackageManager sender, string text) {
                _outputWindow.WriteLine(text.TrimEndNewline());
                _taskHandler?.Progress.Report(new TaskProgressData() {
                    CanBeCanceled = false,
                    ProgressText = text,
                    PercentComplete = null
                });
            }

            public void OnErrorTextReceived(IPackageManager sender, string text) {
                _outputWindow.WriteErrorLine(text.TrimEndNewline());
            }

            public void OnOperationStarted(IPackageManager sender, string operation) {
                if (_showAndActiveOutputWindow) {
                    _outputWindow.ShowAndActivate();
                }
            }

            public void OnOperationFinished(IPackageManager sender, string operation, bool success) {
                if (_showAndActiveOutputWindow) {
                    _outputWindow.ShowAndActivate();
                }
            }

            public Task<bool> ShouldElevateAsync(IPackageManager sender, string operation) {
                return Task.FromResult(false);
            }
        }
    }
}

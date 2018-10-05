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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.InterpreterList;
using Microsoft.PythonTools.Logging;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TaskStatusCenter;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Environments {
    sealed class AddCondaEnvironmentOperation {
        private readonly IServiceProvider _site;
        private readonly ICondaEnvironmentManager _condaMgr;
        private readonly PythonProjectNode _project;
        private readonly string _envName;
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

        public AddCondaEnvironmentOperation(
            IServiceProvider site,
            ICondaEnvironmentManager condaMgr,
            PythonProjectNode project,
            string envName,
            string envFilePath,
            List<PackageSpec> packages,
            bool setAsCurrent,
            bool setAsDefault,
            bool viewInEnvWindow
        ) {
            _site = site ?? throw new ArgumentNullException(nameof(site));
            _condaMgr = condaMgr;
            _project = project;
            _envName = envName;
            _envFilePath = envFilePath;
            _packages = packages;
            _setAsCurrent = setAsCurrent;
            _setAsDefault = setAsDefault;
            _viewInEnvWindow = viewInEnvWindow;

            _outputWindow = OutputWindowRedirector.GetGeneral(_site);
            _statusBar = _site.GetService(typeof(SVsStatusbar)) as IVsStatusbar;
            _showAndActiveOutputWindow = _site.GetPythonToolsService().GeneralOptions.ShowOutputWindowForVirtualEnvCreate;
            _statusCenter = _site.GetService(typeof(SVsTaskStatusCenterService)) as IVsTaskStatusCenterService;
            _registry = _site.GetComponentModel().GetService<IInterpreterRegistryService>();
            _options = _site.GetComponentModel().GetService<IInterpreterOptionsService>();
            _logger = _site.GetService(typeof(IPythonToolsLogger)) as IPythonToolsLogger;
        }

        public async Task RunAsync(CancellationToken ct = default(CancellationToken)) {
            var taskHandler = _statusCenter.PreRegister(
                new TaskHandlerOptions() {
                    ActionsAfterCompletion = CompletionActions.RetainAndNotifyOnFaulted | CompletionActions.RetainAndNotifyOnRanToCompletion,
                    Title = Strings.CondaStatusCenterCreateTitle.FormatUI(_envName),
                    DisplayTaskDetails = (t) => { _outputWindow.ShowAndActivate(); }
                },
                new TaskProgressData() {
                    CanBeCanceled = false,
                    ProgressText = Strings.CondaStatusCenterCreateProgressPreparing,
                    PercentComplete = null,
                }
            );

            var ui = new CondaUI(taskHandler, _outputWindow, _showAndActiveOutputWindow);
            _statusBar.SetText(Strings.CondaStatusBarCreateStarted.FormatUI(_envName));

            var task = Task.Run(() => CreateCondaEnvironmentAsync(ui, taskHandler, ct));
            taskHandler.RegisterTask(task);
            _site.ShowTaskStatusCenter();
        }

        private async Task CreateCondaEnvironmentAsync(CondaUI ui, ITaskHandler taskHandler, CancellationToken ct) {
            try {
                var factory = await CreateFactoryAsync(ui, taskHandler, ct);
                if (factory != null) {
                    await _site.GetUIThread().InvokeTask(async () => {
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

                taskHandler.Progress.Report(new TaskProgressData() {
                    CanBeCanceled = false,
                    ProgressText = Strings.CondaStatusCenterCreateProgressCompleted,
                    PercentComplete = 100,
                });

                _statusBar.SetText(Strings.CondaStatusBarCreateSucceeded.FormatUI(_envName));
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                _statusBar.SetText(Strings.CondaStatusBarCreateFailed.FormatUI(_envName));
                ui.OnErrorTextReceived(_condaMgr, ex.Message);
                throw;
            }
        }

        private async Task<IPythonInterpreterFactory> CreateFactoryAsync(CondaUI ui, ITaskHandler taskHandler, CancellationToken ct) {
            var tcs = new TaskCompletionSource<IPythonInterpreterFactory>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler interpretersChanged = (object sender, EventArgs e) => {
                var first = _registry.Interpreters
                    .Where(f => CondaEnvironmentFactoryProvider.IsCondaEnv(f, _envName))
                    .FirstOrDefault();
                if (first != null) {
                    tcs.TrySetResult(first);
                }
            };

            IPythonInterpreterFactory factory = null;

            _registry.InterpretersChanged += interpretersChanged;
            try {
                taskHandler.Progress.Report(new TaskProgressData() {
                    CanBeCanceled = false,
                    ProgressText = Strings.CondaStatusCenterCreateProgressCreating,
                    PercentComplete = null,
                });

                bool failed = false;
                bool useEnvFile = !string.IsNullOrEmpty(_envFilePath);

                try {
                    if (useEnvFile) {
                        if (!await _condaMgr.CreateFromEnvironmentFileAsync(
                            _envName,
                            _envFilePath,
                            ui,
                            ct
                        )) {
                            throw new ApplicationException(Strings.CondaStatusCenterCreateFailure);
                        }
                    } else {
                        if (!await _condaMgr.CreateAsync(
                            _envName,
                            _packages.ToArray(),
                            ui,
                            ct
                        )) {
                            throw new ApplicationException(Strings.CondaStatusCenterCreateFailure);
                        }
                    }
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    failed = true;
                    throw;
                } finally {
                    _logger?.LogEvent(PythonLogEvent.CreateCondaEnv, new CreateCondaEnvInfo() {
                        Failed = failed,
                        FromEnvironmentFile = useEnvFile,
                        SetAsDefault = _setAsDefault,
                        SetAsCurrent = _setAsCurrent,
                        OpenEnvironmentsWindow = _viewInEnvWindow,
                    });
                }

                // Wait for the factory to refresh
                taskHandler.Progress.Report(new TaskProgressData() {
                    CanBeCanceled = false,
                    ProgressText = Strings.CondaStatusCenterCreateProgressDetecting,
                    PercentComplete = 90,
                });

                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
                if (await Task.WhenAny(tcs.Task, timeoutTask) == timeoutTask) {
                    throw new ApplicationException(Strings.WaitForEnvTimeout.FormatUI(_envName));
                }
                factory = await tcs.Task;
            } finally {
                _registry.InterpretersChanged -= interpretersChanged;
            }

            return factory;
        }

        class CondaUI : ICondaEnvironmentManagerUI, IPackageManagerUI {
            private readonly ITaskHandler _taskHandler;
            private readonly Redirector _outputWindow;
            private readonly bool _showAndActiveOutputWindow;

            public CondaUI(ITaskHandler taskHandler, Redirector outputWindow, bool showAndActiveOutputWindow) {
                _taskHandler = taskHandler ?? throw new ArgumentNullException(nameof(taskHandler));
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
                _taskHandler.Progress.Report(new TaskProgressData() {
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
                _taskHandler.Progress.Report(new TaskProgressData() {
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

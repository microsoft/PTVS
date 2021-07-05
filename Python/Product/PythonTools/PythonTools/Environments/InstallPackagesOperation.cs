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

using Microsoft.PythonTools.Project;

namespace Microsoft.PythonTools.Environments {
    sealed class InstallPackagesOperation {
        private readonly IServiceProvider _site;
        private readonly IPackageManager _pm;
        private readonly string _reqsPath;
        private readonly Redirector _output;
        private readonly IVsTaskStatusCenterService _statusCenter;

        public InstallPackagesOperation(
            IServiceProvider site,
            IPackageManager pm,
            string requirementsPath,
            Redirector output = null
        ) {
            _site = site ?? throw new ArgumentNullException(nameof(site));
            _pm = pm ?? throw new ArgumentNullException(nameof(pm));
            _reqsPath = requirementsPath ?? throw new ArgumentNullException(nameof(requirementsPath));
            _output = output;
            _statusCenter = _site.GetService(typeof(SVsTaskStatusCenterService)) as IVsTaskStatusCenterService;
        }

        private void WriteOutput(string message) {
            _output?.WriteLine(message);
        }

        private void WriteError(string message) {
            _output?.WriteErrorLine(message);
        }

        public async Task RunAsync() {
            var outputWindow = OutputWindowRedirector.GetGeneral(_site);
            var taskHandler = _statusCenter?.PreRegister(
                new TaskHandlerOptions() {
                    ActionsAfterCompletion = CompletionActions.RetainAndNotifyOnFaulted | CompletionActions.RetainAndNotifyOnRanToCompletion,
                    Title = Strings.InstallPackagesStatusCenterTitle.FormatUI(PathUtils.GetFileOrDirectoryName(_pm.Factory.Configuration.Description)),
                    DisplayTaskDetails = (t) => { outputWindow.ShowAndActivate(); }
                },
                new TaskProgressData() {
                    CanBeCanceled = false,
                    ProgressText = Strings.InstallPackagesStatusCenterProgressPreparing,
                    PercentComplete = null,
                }
            );

            var task = InstallPackagesAsync(taskHandler);
            taskHandler?.RegisterTask(task);
            _site.ShowTaskStatusCenter();
        }

        private async Task InstallPackagesAsync(ITaskHandler taskHandler) {
            await InstallPackagesAsync();

            taskHandler?.Progress.Report(new TaskProgressData() {
                CanBeCanceled = false,
                ProgressText = Strings.InstallPackagesStatusCenterProgressCompleted,
                PercentComplete = 100,
            });
        }

        public async Task InstallPackagesAsync() {
            WriteOutput(Strings.RequirementsTxtInstalling.FormatUI(_reqsPath));
            bool success = false;
            try {
                var ui = new VsPackageManagerUI(_site);
                if (!_pm.IsReady) {
                    await _pm.PrepareAsync(ui, CancellationToken.None);
                }
                success = await _pm.InstallAsync(
                    PackageSpec.FromArguments("-r " + ProcessOutput.QuoteSingleArgument(_reqsPath)),
                    ui,
                    CancellationToken.None
                );
            } catch (InvalidOperationException ex) {
                WriteOutput(ex.Message);
                throw;
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                WriteOutput(ex.Message);
                Debug.Fail(ex.ToUnhandledExceptionMessage(GetType()));
                throw;
            } finally {
                if (success) {
                    WriteOutput(Strings.PackageInstallSucceeded.FormatUI(Path.GetFileName(_reqsPath)));
                } else {
                    var msg = Strings.PackageInstallFailed.FormatUI(Path.GetFileName(_reqsPath));
                    WriteOutput(msg);
                    throw new ApplicationException(msg);
                }
            }
        }
    }
}

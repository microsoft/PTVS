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
using System.Threading;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Navigation;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudio.Imaging;
using Microsoft.PythonTools.InteractiveWindow.Shell;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.InteractiveWindow;

namespace Microsoft.PythonTools.Commands {
    /// <summary>
    /// Provides the command for starting a file or the start item of a project in the REPL window.
    /// </summary>
    internal sealed class ExecuteInReplCommand : Command {
        private readonly IServiceProvider _serviceProvider;

        public ExecuteInReplCommand(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
        }

        internal static IVsInteractiveWindow/*!*/ EnsureReplWindow(IServiceProvider serviceProvider, VsProjectAnalyzer analyzer, PythonProjectNode project) {
            return EnsureReplWindow(serviceProvider, analyzer.InterpreterFactory.Configuration, project);
        }

        internal static IVsInteractiveWindow/*!*/ EnsureReplWindow(IServiceProvider serviceProvider, InterpreterConfiguration config, PythonProjectNode project) {
            var compModel = serviceProvider.GetComponentModel();
            var provider = compModel.GetService<InteractiveWindowProvider>();
            var vsProjectContext = compModel.GetService<VsProjectContextProvider>();

            var projectId = project != null ? PythonReplEvaluatorProvider.GetEvaluatorId(project) : null;
            var configId = config != null ? PythonReplEvaluatorProvider.GetEvaluatorId(config) : null;

            IVsInteractiveWindow window;

            // If we find an open window for the project, prefer that to a per-config one
            if (!string.IsNullOrEmpty(projectId)) {
                window = provider.Open(projectId);
                if (window != null) {
                    if (window.InteractiveWindow.GetPythonEvaluator()?.AssociatedProjectHasChanged == true) {
                        // We have an existing window, but it needs to be reset.
                        // Let's create a new one
                        window = provider.Create(projectId);
                        project.AddActionOnClose(window, InteractiveWindowProvider.Close);
                    }

                    return window;
                }
            }

            // If we find an open window for the configuration, return that
            if (!string.IsNullOrEmpty(configId)) {
                window = provider.Open(configId);
                if (window != null) {
                    return window;
                }
            }

            // No window found, so let's create one
            if (!string.IsNullOrEmpty(projectId)) {
                window = provider.Create(projectId);
                project.AddActionOnClose(window, InteractiveWindowProvider.Close);
            } else if (!string.IsNullOrEmpty(configId)) {
                window = provider.Create(configId);
            } else {
                var interpService = compModel.GetService<IInterpreterOptionsService>();
                window = provider.Create(PythonReplEvaluatorProvider.GetEvaluatorId(interpService.DefaultInterpreter.Configuration));
            }

            return window;
        }

        public override EventHandler BeforeQueryStatus {
            get {
                return QueryStatusMethod;
            }
        }

        private void QueryStatusMethod(object sender, EventArgs args) {
            var oleMenu = sender as OleMenuCommand;
            if (oleMenu == null) {
                Debug.Fail("Unexpected command type " + sender == null ? "(null)" : sender.GetType().FullName);
                return;
            }

            var pyProj = CommonPackage.GetStartupProject(_serviceProvider) as PythonProjectNode;
            var textView = CommonPackage.GetActiveTextView(_serviceProvider);

            oleMenu.Supported = true;

            if (pyProj != null) {
                // startup project, so visible in Project mode
                oleMenu.Visible = true;
                oleMenu.Text = "Execute Project in P&ython Interactive";

                // Only enable if runnable
                oleMenu.Enabled = pyProj.GetInterpreterFactory().IsRunnable();

            } else if (textView != null && textView.TextBuffer.ContentType.IsOfType(PythonCoreConstants.ContentType)) {
                // active file, so visible in File mode
                oleMenu.Visible = true;
                oleMenu.Text = "Execute File in P&ython Interactive";

                // Only enable if runnable
                var interpreterService = _serviceProvider.GetComponentModel().GetService<IInterpreterOptionsService>();
                oleMenu.Enabled = interpreterService != null && interpreterService.DefaultInterpreter.IsRunnable();

            } else {
                // Python is not active, so hide the command
                oleMenu.Visible = false;
                oleMenu.Enabled = false;
            }
        }

        public override async void DoCommand(object sender, EventArgs e) {
            var pyProj = CommonPackage.GetStartupProject(_serviceProvider) as PythonProjectNode;
            var textView = CommonPackage.GetActiveTextView(_serviceProvider);

            var config = pyProj?.GetLaunchConfigurationOrThrow();
            if (config == null && textView != null) {
                var pyService = _serviceProvider.GetPythonToolsService();
                config = new LaunchConfiguration(pyService.DefaultInterpreterConfiguration) {
                    ScriptName = textView.GetFilePath(),
                    WorkingDirectory = PathUtils.GetParent(textView.GetFilePath())
                };
            }
            if (config == null) {
                Debug.Fail("Should not be executing command when it is invisible");
                return;
            }

            var window = EnsureReplWindow(_serviceProvider, config.Interpreter, pyProj);
            window.Show(true);

            var eval = (IPythonInteractiveEvaluator)window.InteractiveWindow.Evaluator;

            // The interpreter may take some time to startup, do this off the UI thread.
            await ThreadHelper.JoinableTaskFactory.RunAsync(async () => {
                await ((IInteractiveEvaluator)eval).ResetAsync();

                window.InteractiveWindow.WriteLine(string.Format("Running {0}", config.ScriptName));

                await eval.ExecuteFileAsync(config.ScriptName, config.ScriptArguments);
            });
        }

        public override int CommandId {
            get { return (int)PkgCmdIDList.cmdidExecuteFileInRepl; }
        }
    }
}

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

using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Commands
{
    /// <summary>
    /// Provides the command for starting a file or the start item of a project in the REPL window.
    /// </summary>
    internal sealed class ExecuteInReplCommand : Command
    {
        private readonly IServiceProvider _serviceProvider;

        public ExecuteInReplCommand(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        internal static IVsInteractiveWindow/*!*/ EnsureReplWindow(IServiceProvider serviceProvider, VsProjectAnalyzer analyzer, PythonProjectNode project, IPythonWorkspaceContext workspace)
        {
            return EnsureReplWindow(serviceProvider, analyzer.InterpreterFactory.Configuration, project, workspace);
        }

        internal static IVsInteractiveWindow/*!*/ EnsureReplWindow(IServiceProvider serviceProvider, InterpreterConfiguration config, PythonProjectNode project, IPythonWorkspaceContext workspace)
        {
            var compModel = serviceProvider.GetComponentModel();
            var provider = compModel.GetService<InteractiveWindowProvider>();
            var vsProjectContext = compModel.GetService<VsProjectContextProvider>();

            var projectId = project != null ? PythonReplEvaluatorProvider.GetEvaluatorId(project) : null;
            var workspaceId = workspace != null ? PythonReplEvaluatorProvider.GetEvaluatorId(workspace) : null;
            var configId = config != null ? PythonReplEvaluatorProvider.GetEvaluatorId(config) : null;

            if (config?.IsRunnable() == false)
            {
                throw new MissingInterpreterException(
                    Strings.MissingEnvironment.FormatUI(config.Description, config.Version)
                );
            }

            IVsInteractiveWindow window;

            // If we find an open window for the project, prefer that to a per-config one
            if (!string.IsNullOrEmpty(projectId))
            {
                window = provider.Open(
                    projectId,
                    e => ((e as SelectableReplEvaluator)?.Evaluator as PythonCommonInteractiveEvaluator)?.AssociatedProjectHasChanged != true
                );
                if (window != null)
                {
                    return window;
                }
            }

            // If we find an open window for the workspace, prefer that to a per config one
            if (!string.IsNullOrEmpty(workspaceId))
            {
                window = provider.Open(
                    workspaceId,
                    e => ((e as SelectableReplEvaluator)?.Evaluator as PythonCommonInteractiveEvaluator)?.AssociatedWorkspaceHasChanged != true
                );
                if (window != null)
                {
                    return window;
                }
            }

            // If we find an open window for the configuration, return that
            if (!string.IsNullOrEmpty(configId))
            {
                window = provider.Open(configId);
                if (window != null)
                {
                    return window;
                }
            }

            // No window found, so let's create one
            if (!string.IsNullOrEmpty(projectId))
            {
                window = provider.Create(projectId);
                project.AddActionOnClose(window, w => InteractiveWindowProvider.CloseIfEvaluatorMatches(w, projectId));
            }
            else if (!string.IsNullOrEmpty(workspaceId))
            {
                window = provider.Create(workspaceId);
                workspace.AddActionOnClose(window, w => InteractiveWindowProvider.CloseIfEvaluatorMatches(w, workspaceId));
            }
            else if (!string.IsNullOrEmpty(configId))
            {
                window = provider.Create(configId);
            }
            else
            {
                var interpService = compModel.GetService<IInterpreterOptionsService>();
                window = provider.Create(PythonReplEvaluatorProvider.GetEvaluatorId(interpService.DefaultInterpreter.Configuration));
            }

            return window;
        }

        public override EventHandler BeforeQueryStatus
        {
            get
            {
                return QueryStatusMethod;
            }
        }

        private void QueryStatusMethod(object sender, EventArgs args)
        {
            var oleMenu = sender as OleMenuCommand;
            if (oleMenu == null)
            {
                Debug.Fail("Unexpected command type " + sender == null ? "(null)" : sender.GetType().FullName);
                return;
            }

            var workspace = _serviceProvider.GetWorkspace();
            var pyProj = CommonPackage.GetStartupProject(_serviceProvider) as PythonProjectNode;
            var textView = CommonPackage.GetActiveTextView(_serviceProvider);

            oleMenu.Supported = true;

            if (pyProj != null)
            {
                // startup project, so visible in Project mode
                oleMenu.Visible = true;
                oleMenu.Text = Strings.ExecuteInReplCommand_ExecuteProject;

                // Only enable if runnable
                oleMenu.Enabled = pyProj.GetInterpreterFactory().IsRunnable();

            }
            else if (textView != null && textView.TextBuffer.ContentType.IsOfType(PythonCoreConstants.ContentType))
            {
                // active file, so visible in File mode
                oleMenu.Visible = true;
                oleMenu.Text = Strings.ExecuteInReplCommand_ExecuteFile;

                // Only enable if runnable
                if (workspace != null)
                {
                    oleMenu.Enabled = workspace.CurrentFactory.IsRunnable();
                }
                else
                {
                    var interpreterService = _serviceProvider.GetComponentModel().GetService<IInterpreterOptionsService>();
                    oleMenu.Enabled = interpreterService != null && interpreterService.DefaultInterpreter.IsRunnable();
                }
            }
            else
            {
                // Python is not active, so hide the command
                oleMenu.Visible = false;
                oleMenu.Enabled = false;
            }
        }

        public override void DoCommand(object sender, EventArgs e)
        {
            DoCommand().HandleAllExceptions(_serviceProvider, GetType()).DoNotWait();
        }

        private async System.Threading.Tasks.Task DoCommand()
        {
            var workspace = _serviceProvider.GetWorkspace();
            var pyProj = CommonPackage.GetStartupProject(_serviceProvider) as PythonProjectNode;
            var textView = CommonPackage.GetActiveTextView(_serviceProvider);

            var scriptName = textView?.GetFilePath();

            if (!string.IsNullOrEmpty(scriptName) && pyProj != null)
            {
                if (pyProj.FindNodeByFullPath(scriptName) == null)
                {
                    // Starting a script that isn't in the project.
                    // Try and find the project. If we fail, we will
                    // use the default environment.
                    pyProj = _serviceProvider.GetProjectFromFile(scriptName);
                }
            }

            LaunchConfiguration config = null;
            try
            {
                if (workspace != null)
                {
                    config = PythonCommonInteractiveEvaluator.GetWorkspaceLaunchConfigurationOrThrow(workspace);
                }
                else
                {
                    config = pyProj?.GetLaunchConfigurationOrThrow();
                }
            }
            catch (MissingInterpreterException ex)
            {
                MessageBox.Show(ex.Message, Strings.ProductTitle);
                return;
            }
            if (config == null)
            {
                var interpreters = _serviceProvider.GetComponentModel().GetService<IInterpreterOptionsService>();
                config = new LaunchConfiguration(interpreters.DefaultInterpreter.Configuration);
            }
            else
            {
                config = config.Clone();
            }

            if (!string.IsNullOrEmpty(scriptName))
            {
                config.ScriptName = scriptName;
                // Only overwrite the working dir for a loose file, don't do it for workspaces
                if (workspace == null)
                {
                    config.WorkingDirectory = PathUtils.GetParent(scriptName);
                }
            }

            if (config == null)
            {
                Debug.Fail("Should not be executing command when it is invisible");
                return;
            }

            IVsInteractiveWindow window;
            try
            {
                window = EnsureReplWindow(_serviceProvider, config.Interpreter, pyProj, workspace);
            }
            catch (MissingInterpreterException ex)
            {
                MessageBox.Show(ex.Message, Strings.ProductTitle);
                return;
            }

            window.Show(true);
            var eval = (IPythonInteractiveEvaluator)window.InteractiveWindow.Evaluator;

            // The interpreter may take some time to startup, do this off the UI thread.
            await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ((IInteractiveEvaluator)eval).ResetAsync();

                window.InteractiveWindow.WriteLine(Strings.ExecuteInReplCommand_RunningMessage.FormatUI(config.ScriptName));

                await eval.ExecuteFileAsync(config.ScriptName, config.ScriptArguments);
            });
        }

        public override int CommandId
        {
            get { return (int)PkgCmdIDList.cmdidExecuteFileInRepl; }
        }
    }
}

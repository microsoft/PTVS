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
using Microsoft.VisualStudio.InteractiveWindow.Shell;
using Microsoft.PythonTools.Infrastructure;

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

            string replId = project != null ?
                PythonReplEvaluatorProvider.GetEvaluatorId(project) :
                PythonReplEvaluatorProvider.GetEvaluatorId(config);
            var window = provider.OpenOrCreate(replId);
            project?.AddActionOnClose(window, InteractiveWindowProvider.Close);
            //if (project != null && project.Interpreters.IsProjectSpecific(factory)) {
            //    project.AddActionOnClose(window, BasePythonReplEvaluator.CloseReplWindow);
            //}

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

            VsProjectAnalyzer analyzer;
            string filename, dir = null, args = null;

            if (pyProj != null) {
                analyzer = pyProj.GetAnalyzer();
                filename = pyProj.GetStartupFile();
                dir = pyProj.GetWorkingDirectory();
                args = ((IPythonProjectLaunchProperties)pyProj).GetArguments();
            } else if (textView != null) {
                var pyService = _serviceProvider.GetPythonToolsService();
                analyzer = pyService.DefaultAnalyzer;
                filename = textView.GetFilePath();
            } else {
                Debug.Fail("Should not be executing command when it is invisible");
                return;
            }
            if (string.IsNullOrEmpty(filename)) {
                // TODO: Error reporting
                return;
            }
            if (string.IsNullOrEmpty(dir)) {
                dir = PathUtils.GetParent(filename);
            }

            var window = EnsureReplWindow(_serviceProvider, analyzer, pyProj);

            window.Show(true);

            var eval = (IPythonInteractiveEvaluator)window.InteractiveWindow.Evaluator;

            // The interpreter may take some time to startup, do this off the UI thread.
            await ThreadHelper.JoinableTaskFactory.RunAsync(async () => {
                await eval.ResetAsync();

                window.InteractiveWindow.WriteLine(string.Format("Running {0}", filename));

                await eval.ExecuteFileAsync(filename, args);
            });
        }

        public override int CommandId {
            get { return (int)PkgCmdIDList.cmdidExecuteFileInRepl; }
        }
    }
}

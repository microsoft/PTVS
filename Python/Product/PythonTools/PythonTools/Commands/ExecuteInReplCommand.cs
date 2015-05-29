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
using System.Threading;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Navigation;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudioTools;
#if DEV14_OR_LATER
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.InteractiveWindow.Shell;
#else
using Microsoft.VisualStudio.Repl;
#endif

namespace Microsoft.PythonTools.Commands {
#if DEV14_OR_LATER
    using IReplWindow = IInteractiveWindow;
    using IReplWindowProvider = InteractiveWindowProvider;
    using IReplWindowToolWindow = IVsInteractiveWindow;
#else
    using IReplWindowToolWindow = IReplWindow;
#endif

    /// <summary>
    /// Provides the command for starting a file or the start item of a project in the REPL window.
    /// </summary>
    internal sealed class ExecuteInReplCommand : Command {
        private readonly IServiceProvider _serviceProvider;

        public ExecuteInReplCommand(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
        }

        internal static IReplWindowToolWindow/*!*/ EnsureReplWindow(IServiceProvider serviceProvider, VsProjectAnalyzer analyzer, PythonProjectNode project) {
            return EnsureReplWindow(serviceProvider, analyzer.InterpreterFactory, project);
        }

        internal static IReplWindowToolWindow/*!*/ EnsureReplWindow(IServiceProvider serviceProvider, IPythonInterpreterFactory factory, PythonProjectNode project) {
            var compModel = serviceProvider.GetComponentModel();
            var provider = compModel.GetService<IReplWindowProvider>();

            string replId = PythonReplEvaluatorProvider.GetReplId(factory, project);
            var window = provider.FindReplWindow(replId);
            if (window == null) {
                window = provider.CreateReplWindow(
                    serviceProvider.GetPythonContentType(),
                    factory.Description + " Interactive",
                    typeof(PythonLanguageInfo).GUID,
                    replId
                );

                var toolWindow = window as ToolWindowPane;
                if (toolWindow != null) {
#if DEV14_OR_LATER
                    toolWindow.BitmapImageMoniker = KnownMonikers.PYInteractiveWindow;
#else
                    // TODO: Add image here for VS 2013
#endif
                }

                var pyService = serviceProvider.GetPythonToolsService();
                window.SetSmartUpDown(pyService.GetInteractiveOptions(factory).ReplSmartHistory);
            }

            if (project != null && project.Interpreters.IsProjectSpecific(factory)) {
                project.AddActionOnClose(window, BasePythonReplEvaluator.CloseReplWindow);
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
            VsProjectAnalyzer analyzer;
            var interpreterService = _serviceProvider.GetComponentModel().GetService<IInterpreterOptionsService>();
            string filename, dir;
            if (!PythonToolsPackage.TryGetStartupFileAndDirectory(_serviceProvider, out filename, out dir, out analyzer) ||
                interpreterService == null ||
                interpreterService.NoInterpretersValue == analyzer.InterpreterFactory) {
                // no interpreters installed, disable the command.
                oleMenu.Visible = true;
                oleMenu.Enabled = false;
                oleMenu.Supported = true;
            } else {

                IWpfTextView textView;
                var pyProj = CommonPackage.GetStartupProject(_serviceProvider) as PythonProjectNode;
                var window = (IReplWindow)EnsureReplWindow(_serviceProvider, analyzer, pyProj);
                if (pyProj != null) {
                    // startup project, enabled in Start in REPL mode.
                    oleMenu.Visible = true;
                    oleMenu.Enabled = true;
                    oleMenu.Supported = true;
                    oleMenu.Text = "Execute Project in P&ython Interactive";
                } else if ((textView = CommonPackage.GetActiveTextView(_serviceProvider)) != null &&
                    textView.TextBuffer.ContentType == _serviceProvider.GetPythonContentType()) {
                    // enabled in Execute File mode...
                    oleMenu.Visible = true;
                    oleMenu.Enabled = true;
                    oleMenu.Supported = true;
                    oleMenu.Text = "Execute File in P&ython Interactive";
                } else {
                    oleMenu.Visible = false;
                    oleMenu.Enabled = false;
                    oleMenu.Supported = false;
                }
            }
        }

        public override void DoCommand(object sender, EventArgs args) {
            VsProjectAnalyzer analyzer;
            string filename, dir;
            var pyProj = CommonPackage.GetStartupProject(_serviceProvider) as PythonProjectNode;
            if (!PythonToolsPackage.TryGetStartupFileAndDirectory(_serviceProvider, out filename, out dir, out analyzer)) {
                // TODO: Error reporting
                return;
            }

            var window = EnsureReplWindow(_serviceProvider, analyzer, pyProj);
            IVsWindowFrame windowFrame = (IVsWindowFrame)((ToolWindowPane)window).Frame;

#if DEV14_OR_LATER
            window.Show(true);
#else
            ErrorHandler.ThrowOnFailure(windowFrame.Show());
            window.Focus();
#endif

            // The interpreter may take some time to startup, do this off the UI thread.
            ThreadPool.QueueUserWorkItem(x => {
#if DEV14_OR_LATER
                window.InteractiveWindow.Evaluator.ResetAsync();

                window.InteractiveWindow.WriteLine(String.Format("Running {0}", filename));
                string scopeName = Path.GetFileNameWithoutExtension(filename);

                ((PythonReplEvaluator)window.InteractiveWindow.Evaluator).ExecuteFile(filename);
#else
                window.Reset();
                
                window.WriteLine(String.Format("Running {0}", filename));
                string scopeName = Path.GetFileNameWithoutExtension(filename);

                window.Evaluator.ExecuteFile(filename);
#endif
            });
        }

        public override int CommandId {
            get { return (int)PkgCmdIDList.cmdidExecuteFileInRepl; }
        }
    }
}

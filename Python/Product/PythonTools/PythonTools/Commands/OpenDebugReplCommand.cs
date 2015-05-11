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
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Navigation;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio;
#if DEV14_OR_LATER
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.InteractiveWindow.Shell;
#else
using Microsoft.VisualStudio.Repl;
#endif
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Commands {
#if DEV14_OR_LATER
    using IReplWindow = IInteractiveWindow;
    using IVsReplWindow = IVsInteractiveWindow;
    using IReplEvaluator = IInteractiveEvaluator;
    using IReplWindowProvider = InteractiveWindowProvider;
#else
    using IVsReplWindow = IReplWindow;
#endif

    /// <summary>
    /// Provides the command for starting the Python Debug REPL window.
    /// </summary>
    class OpenDebugReplCommand : Command {
        private readonly IServiceProvider _serviceProvider;

        public OpenDebugReplCommand(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
        }

        internal static IVsReplWindow/*!*/ EnsureReplWindow(IServiceProvider serviceProvider) {
            var compModel = serviceProvider.GetComponentModel();
            var provider = compModel.GetService<IReplWindowProvider>();

            string replId = PythonDebugReplEvaluatorProvider.GetDebugReplId();
            var window = provider.FindReplWindow(replId);
            if (window == null) {
                window = provider.CreateReplWindow(serviceProvider.GetPythonContentType(), "Python Debug Interactive", typeof(PythonLanguageInfo).GUID, replId);

                var pyService = serviceProvider.GetPythonToolsService();
                window.SetSmartUpDown(pyService.DebugInteractiveOptions.ReplSmartHistory);
            }
            return window;
        }

        public override void DoCommand(object sender, EventArgs args) {
            var window = EnsureReplWindow(_serviceProvider);
            IVsWindowFrame windowFrame = (IVsWindowFrame)((ToolWindowPane)window).Frame;

            ErrorHandler.ThrowOnFailure(windowFrame.Show());
            window.Focus();
        }

        public override EventHandler BeforeQueryStatus {
            get {
                return QueryStatusMethod;
            }
        }

        private void QueryStatusMethod(object sender, EventArgs args) {
            var oleMenu = sender as OleMenuCommand;

            oleMenu.Visible = true;
            oleMenu.Enabled = true;
            oleMenu.Supported = true;
        }

        public string Description {
            get {
                return "Python Interactive Debug";
            }
        }

        public override int CommandId {
            get { return (int)PkgCmdIDList.cmdidDebugReplWindow; }
        }
    }
}

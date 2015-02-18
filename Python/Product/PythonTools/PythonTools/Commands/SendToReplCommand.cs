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
using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Commands {
    using IServiceProvider = System.IServiceProvider;

#if INTERACTIVE_WINDOW
    using IReplWindow = IInteractiveWindow;
    using IReplCommand = IInteractiveWindowCommand;
#endif

    /// <summary>
    /// Provides the command to send selected text from a buffer to the remote REPL window.
    /// </summary>
    class SendToReplCommand : Command {
        protected readonly IServiceProvider _serviceProvider;

        public SendToReplCommand(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
        }

        public override void DoCommand(object sender, EventArgs args) {
            var activeView = CommonPackage.GetActiveTextView(_serviceProvider);
            var project = activeView.TextBuffer.GetProject(_serviceProvider);
            var analyzer = activeView.GetAnalyzer(_serviceProvider);

            ToolWindowPane window = (ToolWindowPane)ExecuteInReplCommand.EnsureReplWindow(_serviceProvider, analyzer, project);

            IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
            ErrorHandler.ThrowOnFailure(windowFrame.Show());
            IReplWindow repl = (IReplWindow)window;
            
            PythonReplEvaluator eval = repl.Evaluator as PythonReplEvaluator;
            
            eval.EnsureConnected();
            repl.Submit(GetActiveInputs(activeView, eval));

            repl.Focus();
        }

        private static IEnumerable<string> GetActiveInputs(IWpfTextView activeView, PythonReplEvaluator eval) {
            return eval.JoinCode(activeView.Selection.SelectedSpans.SelectMany(s => eval.SplitCode(s.GetText())));
        }

        private bool IsRealInterpreter(IPythonInterpreterFactory factory) {
            if (factory == null) {
                return false;
            }
            var interpreterService = _serviceProvider.GetComponentModel().GetService<IInterpreterOptionsService>();
            return interpreterService != null && interpreterService.NoInterpretersValue != factory;
        }

        public override int? EditFilterQueryStatus(ref VisualStudio.OLE.Interop.OLECMD cmd, IntPtr pCmdText) {
            var activeView = CommonPackage.GetActiveTextView(_serviceProvider);
            if (activeView != null && activeView.TextBuffer.ContentType.IsOfType(PythonCoreConstants.ContentType)) {
                var analyzer = activeView.GetAnalyzer(_serviceProvider);

                if (activeView.Selection.IsEmpty || 
                    activeView.Selection.Mode == TextSelectionMode.Box ||
                    analyzer == null || 
                    !IsRealInterpreter(analyzer.InterpreterFactory)) {
                    cmd.cmdf = (uint)(OLECMDF.OLECMDF_SUPPORTED);
                } else {
                    cmd.cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                }
            } else {
                cmd.cmdf = (uint)(OLECMDF.OLECMDF_INVISIBLE);
            }

            return VSConstants.S_OK;
        }

        public override EventHandler BeforeQueryStatus {
            get {
                return (sender, args) => {
                    ((OleMenuCommand)sender).Visible = false;
                    ((OleMenuCommand)sender).Supported = false;
                };
            }
        }

        public override int CommandId {
            get { return (int)PkgCmdIDList.cmdidSendToRepl; }
        }
    }
}

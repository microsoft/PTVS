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
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.InteractiveWindow.Shell;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudioTools;
using IServiceProvider = System.IServiceProvider;

namespace Microsoft.PythonTools.Commands {
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
            var repl = (IVsInteractiveWindow)window;

            PythonReplEvaluator eval = repl.InteractiveWindow.Evaluator as PythonReplEvaluator;

            eval.EnsureConnected();
            repl.InteractiveWindow.Submit(GetActiveInputs(activeView, eval));

            repl.Show(true);
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

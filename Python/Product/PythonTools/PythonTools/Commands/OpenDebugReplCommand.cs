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
using Microsoft.PythonTools.Navigation;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.InteractiveWindow.Shell;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Commands {
    /// <summary>
    /// Provides the command for starting the Python Debug REPL window.
    /// </summary>
    class OpenDebugReplCommand : Command {
        private readonly IServiceProvider _serviceProvider;

        public OpenDebugReplCommand(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
        }

        internal static IVsInteractiveWindow/*!*/ EnsureReplWindow(IServiceProvider serviceProvider) {
            var compModel = serviceProvider.GetComponentModel();
            var provider = compModel.GetService<InteractiveWindowProvider>();

            string replId = PythonDebugReplEvaluatorProvider.GetDebugReplId();
            var window = provider.FindReplWindow(replId);
            if (window == null) {
                window = provider.CreateInteractiveWindow(serviceProvider.GetPythonContentType(), "Python Debug Interactive", typeof(PythonLanguageInfo).GUID, replId);

                var pyService = serviceProvider.GetPythonToolsService();
                window.InteractiveWindow.SetSmartUpDown(pyService.DebugInteractiveOptions.ReplSmartHistory);
            }
            return window;
        }

        public override void DoCommand(object sender, EventArgs args) {
            EnsureReplWindow(_serviceProvider).Show(true);
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

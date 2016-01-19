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
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Commands {
    class SendToDefiningModuleCommand : SendToReplCommand {
        public SendToDefiningModuleCommand(IServiceProvider serviceProvider)
            : base(serviceProvider) {
        }

        public override void DoCommand(object sender, EventArgs args) {
            var activeView = PythonToolsPackage.GetActiveTextView(_serviceProvider);
            var pyProj = activeView.TextBuffer.GetProject(_serviceProvider);
            var analyzer = activeView.GetAnalyzer(_serviceProvider);
            var window = ExecuteInReplCommand.EnsureReplWindow(_serviceProvider, analyzer, pyProj);
            var eval = window.InteractiveWindow.Evaluator as PythonReplEvaluator;

            string path = activeView.GetFilePath();
            string scope;
            if (path != null && (scope = eval.GetScopeByFilename(path)) != null) {
                // we're now in the correct module, execute the code
                window.InteractiveWindow.Operations.Cancel();
                // TODO: get correct prompt
                window.InteractiveWindow.WriteLine(">>>" + " $module " + scope);
                eval.SetScope(scope);

                base.DoCommand(sender, args);
            } else {
                window.InteractiveWindow.WriteLine("Could not find defining module.");
            }
        }

        public override int CommandId {
            get { return (int)PkgCmdIDList.cmdidSendToDefiningModule; }
        }
    }
}

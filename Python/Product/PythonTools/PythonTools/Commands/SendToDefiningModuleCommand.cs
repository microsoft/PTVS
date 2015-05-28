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
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudioTools;
#if !DEV14_OR_LATER
using Microsoft.VisualStudio.Repl;
#endif

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
#if DEV14_OR_LATER
            var eval = window.InteractiveWindow.Evaluator as PythonReplEvaluator;
#else
            var eval = window.Evaluator as PythonReplEvaluator;
#endif

            string path = activeView.GetFilePath();
            string scope;
            if (path != null && (scope = eval.GetScopeByFilename(path)) != null) {
                // we're now in the correct module, execute the code
#if DEV14_OR_LATER
                window.InteractiveWindow.Operations.Cancel();
                // TODO: get correct prompt
                window.WriteLine(">>>" + " $module " + scope);
#else
                window.Cancel();
                window.WriteLine(window.GetOptionValue(ReplOptions.PrimaryPrompt) + " $module " + scope);
#endif
                eval.SetScope(scope);

                base.DoCommand(sender, args);
            } else {
                window.WriteLine("Could not find defining module.");

            }
        }

        public override int CommandId {
            get { return (int)PkgCmdIDList.cmdidSendToDefiningModule; }
        }
    }
}

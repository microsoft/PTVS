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
using Microsoft.VisualStudio.Repl;

namespace Microsoft.PythonTools.Commands {
    class SendToDefiningModuleCommand : SendToReplCommand {
        public override void DoCommand(object sender, EventArgs args) {
            var activeView = PythonToolsPackage.GetActiveTextView();
            var analyzer = activeView.GetAnalyzer();
            var window = ExecuteInReplCommand.EnsureReplWindow(analyzer);
            var eval = window.Evaluator as PythonReplEvaluator;

            string path = activeView.GetFilePath();
            string scope;
            if (path != null && (scope = eval.GetScopeByFilename(path)) != null) {
                // we're now in the correct module, execute the code
                window.Cancel();
                window.WriteLine(window.GetOptionValue(ReplOptions.PrimaryPrompt) + " $module " + scope);
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

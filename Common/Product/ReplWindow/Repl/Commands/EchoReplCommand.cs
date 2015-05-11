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

using System.ComponentModel.Composition;
using System.Threading.Tasks;

#if NTVS_FEATURE_INTERACTIVEWINDOW
namespace Microsoft.NodejsTools.Repl {
#else
namespace Microsoft.VisualStudio.Repl {
#endif
    [Export(typeof(IReplCommand))]
    class EchoReplCommand : IReplCommand {
        #region IReplCommand Members

        public Task<ExecutionResult> Execute(IReplWindow window, string arguments) {
            arguments = arguments.ToLowerInvariant();
            if (arguments == "on") {
                window.SetOptionValue(ReplOptions.ShowOutput, true);
            } else {
                window.SetOptionValue(ReplOptions.ShowOutput, false);
            }
            return ExecutionResult.Succeeded;
        }

        public string Description {
            get { return "Suppress or unsuppress output to the buffer"; }
        }

        public string Command {
            get { return "echo"; }
        }

        public object ButtonContent {
            get {
                return null;
            }
        }

        #endregion
    }
}

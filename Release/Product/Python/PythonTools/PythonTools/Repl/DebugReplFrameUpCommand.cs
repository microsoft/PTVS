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
using Microsoft.VisualStudio.Repl;
using Microsoft.PythonTools.Debugger.DebugEngine;
using System;

namespace Microsoft.PythonTools.Repl {
#if INTERACTIVE_WINDOW
    using IReplWindow = IInteractiveWindow;
    using IReplCommand = IInteractiveWindowCommand;
#endif

    [Export(typeof(IReplCommand))]
    [ReplRole("Debug")]
    class DebugReplFrameUpCommand : IReplCommand2 {
        #region IReplCommand Members

        public Task<ExecutionResult> Execute(IReplWindow window, string arguments) {
            var eval = window.Evaluator as PythonDebugReplEvaluator;
            if (eval != null) {
                eval.FrameUp();
            }
            return ExecutionResult.Succeeded;
        }

        public string Description {
            get { return "Move the current frame one level up in the stack trace."; }
        }

        public string Command {
            get { return "up"; }
        }

        public object ButtonContent {
            get {
                return null;
            }
        }

        public System.Collections.Generic.IEnumerable<string> Aliases {
            get { return new string[] { "u" }; }
        }

        #endregion
    }
}

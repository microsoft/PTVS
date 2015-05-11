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

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PythonTools.Debugger.DebugEngine;
using System;
using Microsoft.PythonTools.Debugger;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text;
#if DEV14_OR_LATER
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.InteractiveWindow.Commands;
#else
using Microsoft.VisualStudio.Repl;
#endif

namespace Microsoft.PythonTools.Repl {
#if DEV14_OR_LATER
    using IReplCommand = IInteractiveWindowCommand;
    using IReplCommand2 = IInteractiveWindowCommand;
    using IReplWindow = IInteractiveWindow;
    using ReplRoleAttribute = Microsoft.PythonTools.Repl.InteractiveWindowRoleAttribute;
#endif

    [Export(typeof(IReplCommand))]
    [ReplRole("Debug")]
    class DebugReplThreadCommand : IReplCommand {
        #region IReplCommand Members

        public Task<ExecutionResult> Execute(IReplWindow window, string arguments) {
            var eval = window.Evaluator as PythonDebugReplEvaluator;
            if (eval != null) {
                if (string.IsNullOrEmpty(arguments)) {
                    eval.DisplayActiveThread();
                } else {
                    long id;
                    if (long.TryParse(arguments, out id)) {
                        eval.ChangeActiveThread(id, true);
                    } else {
                        window.WriteError(String.Format("Invalid arguments '{0}'. Expected thread id.", arguments));
                    }
                }
            }
            return ExecutionResult.Succeeded;
        }

        public string Description {
            get { return "Switches the current thread to the specified thread id."; }
        }

        public string Command {
            get { return "thread"; }
        }

        public object ButtonContent {
            get {
                return null;
            }
        }

#if DEV14_OR_LATER
        public IEnumerable<ClassificationSpan> ClassifyArguments(ITextSnapshot snapshot, Span argumentsSpan, Span spanToClassify) {
            yield break;
        }

        public string CommandLine {
            get {
                return "";
            }
        }

        public IEnumerable<string> DetailedDescription {
            get {
                yield return Description;
            }
        }

        public IEnumerable<KeyValuePair<string, string>> ParametersDescription {
            get {
                yield break;
            }
        }
        public IEnumerable<string> Names {
            get {
                yield return Command;
            }
        }
#endif
        #endregion
    }
}

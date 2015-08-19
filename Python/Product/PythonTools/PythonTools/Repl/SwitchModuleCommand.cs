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
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
#if DEV14_OR_LATER
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.InteractiveWindow.Commands;
#else
using Microsoft.VisualStudio.Repl;
using IInteractiveWindow = Microsoft.VisualStudio.Repl.IReplWindow;
using IInteractiveWindowCommand = Microsoft.VisualStudio.Repl.IReplCommand;
#endif

namespace Microsoft.PythonTools.Repl {
    [Export(typeof(IInteractiveWindowCommand))]
    [ContentType(PythonCoreConstants.ContentType)]
    class SwitchModuleCommand : IInteractiveWindowCommand {
        public Task<ExecutionResult> Execute(IInteractiveWindow window, string arguments) {
            var remoteEval = window.Evaluator as IMultipleScopeEvaluator;
            Debug.Assert(remoteEval != null, "Evaluator does not support switching scope");
            if (remoteEval != null) {
                remoteEval.SetScope(arguments);
            }
            return ExecutionResult.Succeeded;
        }

        public string Description {
            get { return "Switches the current scope to the specified module name."; }
        }

        public string Command {
            get { return "mod"; }
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
                return new[] { "Switches the current scope to the specified module name." };
            }
        }

        public IEnumerable<KeyValuePair<string, string>> ParametersDescription {
            get {
                yield break;
            }
        }

        public IEnumerable<string> Names {
            get {
                yield return "mod";
            }
        }
#endif
    }
}

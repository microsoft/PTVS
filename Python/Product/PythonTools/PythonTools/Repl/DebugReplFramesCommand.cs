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
using Microsoft.PythonTools.Debugger.DebugEngine;
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.InteractiveWindow.Commands;

namespace Microsoft.PythonTools.Repl {
    [Export(typeof(IInteractiveWindowCommand))]
    [InteractiveWindowRole("Debug")]
    [ContentType(PythonCoreConstants.ContentType)]
    class DebugReplFramesCommand : IInteractiveWindowCommand {
        public Task<ExecutionResult> Execute(IInteractiveWindow window, string arguments) {
            var eval = window.Evaluator as PythonDebugReplEvaluator;
            if (eval != null) {
                eval.DisplayFrames();
            }
            return ExecutionResult.Succeeded;
        }

        public string Description {
            get { return "Lists the frames for the current thread."; }
        }

        public string Command {
            get { return "where"; }
        }

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
                yield return "bt";
                yield return "w";
            }
        }
    }
}

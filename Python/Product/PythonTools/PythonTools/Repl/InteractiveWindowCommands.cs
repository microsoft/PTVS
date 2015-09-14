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

#if DEV14_OR_LATER
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.InteractiveWindow.Commands;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Repl {
    [Export(typeof(IInteractiveWindowCommand))]
    [ContentType(PythonCoreConstants.ContentType)]
    class InteractiveCommentCommand : IInteractiveWindowCommand {
        private readonly IStandardClassificationService _registry;

        [ImportingConstructor]
        public InteractiveCommentCommand(IStandardClassificationService registry) {
            _registry = registry;
        }

        public string CommandLine {
            get { return "comment text"; }
        }

        public string Description {
            get {
                return "A comment marker";
            }
        }

        public IEnumerable<string> DetailedDescription {
            get { yield break; }
        }

        public IEnumerable<string> Names {
            get {
                yield return "$";
            }
        }

        public IEnumerable<KeyValuePair<string, string>> ParametersDescription {
            get {
                yield break;
            }
        }

        public IEnumerable<ClassificationSpan> ClassifyArguments(
            ITextSnapshot snapshot,
            Span argumentsSpan,
            Span spanToClassify
        ) {
            if (spanToClassify.Length > 0) {
                yield return new ClassificationSpan(
                    new SnapshotSpan(snapshot, spanToClassify),
                    _registry.Comment
                );
            }
        }

        public Task<ExecutionResult> Execute(IInteractiveWindow window, string arguments) {
            return ExecutionResult.Succeeded;
        }
    }


    [Export(typeof(IInteractiveWindowCommand))]
    [ContentType(PythonCoreConstants.ContentType)]
    class InteractiveWaitCommand : IInteractiveWindowCommand {
        private readonly IStandardClassificationService _registry;

        [ImportingConstructor]
        public InteractiveWaitCommand(IStandardClassificationService registry) {
            _registry = registry;
        }

        public string CommandLine {
            get {
                return "timeout";
            }
        }

        public string Description {
            get {
                return "Wait for at least the specified number of milliseconds";
            }
        }

        public IEnumerable<string> DetailedDescription {
            get {
                yield break;
            }
        }

        public IEnumerable<string> Names {
            get {
                yield return "wait";
            }
        }

        public IEnumerable<KeyValuePair<string, string>> ParametersDescription {
            get {
                yield return new KeyValuePair<string, string>("timeout", "Milliseconds to wait before continuing");
            }
        }

        public IEnumerable<ClassificationSpan> ClassifyArguments(
            ITextSnapshot snapshot,
            Span argumentsSpan,
            Span spanToClassify
        ) {
            var arguments = snapshot.GetText(argumentsSpan);
            int timeout;
            if (int.TryParse(arguments, out timeout)) {
                yield return new ClassificationSpan(
                    new SnapshotSpan(snapshot, argumentsSpan),
                    _registry.NumberLiteral
                );
            }
        }

        public async Task<ExecutionResult> Execute(IInteractiveWindow window, string arguments) {
            await Task.Delay(int.Parse(arguments));
            return ExecutionResult.Success;
        }
    }
}

#endif
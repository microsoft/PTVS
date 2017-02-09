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

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.PythonTools.InteractiveWindow;
using Microsoft.PythonTools.InteractiveWindow.Commands;
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
                return Strings.ReplCommentCommandDescription;
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
                return Strings.ReplWaitCommandDescription;
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
                yield return new KeyValuePair<string, string>("timeout", Strings.ReplWaitCommandTimeoutParameterDescription);
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

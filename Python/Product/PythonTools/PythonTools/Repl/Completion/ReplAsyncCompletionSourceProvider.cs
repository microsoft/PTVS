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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Repl.Completion {
    [Export(typeof(IAsyncCompletionSourceProvider))]
    [ContentType(PythonCoreConstants.ReplContentType)]
    [Name("PythonReplCompletion")]
    internal class ReplAsyncCompletionSourceProvider : IAsyncCompletionSourceProvider {
        private readonly ReplRemoteCompletionBroker _broker = new ReplRemoteCompletionBroker();
        private readonly Dictionary<ITextView, ReplAsyncCompletionSource> _sourceMap = new Dictionary<ITextView, ReplAsyncCompletionSource>();

        [Import]
        public ITextStructureNavigatorSelectorService TextStructureNavigatorSelectorService { get; set; }

        public IAsyncCompletionSource GetOrCreate(ITextView textView) {
            if (!this._sourceMap.TryGetValue(textView, out var source)) {
                source = new ReplAsyncCompletionSource(_broker, textView, this.TextStructureNavigatorSelectorService);
                _sourceMap.Add(textView, source);

                // We want to make sure we don't block on commit as language servers can take a while to return results.
                textView.Options.SetOptionValue(DefaultOptions.NonBlockingCompletionOptionId, true);
                textView.Closed += OnTextViewClosed;
            }

            return source;
        }

        private void OnTextViewClosed(object sender, System.EventArgs e) {
            try {
                if (sender is ITextView textView) {
                    textView.Options.SetOptionValue(DefaultOptions.NonBlockingCompletionOptionId, false);
                    this._sourceMap.Remove(textView);
                }
            } catch {
                // Any exceptions caught here should just be swallowed. We may be in a partially disposed state with the editor, and ObjectDisposedException may be thrown.
            }
        }
    }
}

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

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Repl.Completion {

    [Export(typeof(IAsyncCompletionSourceProvider))]
    [ContentType(CodeRemoteContentDefinition.CodeRemoteBaseTypeName)]
    [Name("PythonReplCompletions")] 
    internal class AsyncCompletionSourceProvider : IAsyncCompletionSourceProvider {
        private readonly Dictionary<ITextView, AsyncCompletionSource> sourceMap = new Dictionary<ITextView, AsyncCompletionSource>();

        [Import(typeof(SVsServiceProvider))]
        public IServiceProvider ServiceProvider = null;

        [Import]
        public ITextStructureNavigatorSelectorService TextStructureNavigatorSelectorService { get; set; } = null;

        [Import]
        public IAsyncCompletionBroker EditorCompletionBroker { get; set; } = null;

        public IAsyncCompletionSource GetOrCreate(ITextView textView) {
            AsyncCompletionSource source = null;

            // First make sure this is for a REPL text view. One of the buffers has to be a repl buffer
            var matches = textView.BufferGraph.GetTextBuffers((b) => b.IsReplBuffer());
            if (matches.Count > 0) {
                // Then make sure we have a language client to use.
                var service = ServiceProvider.GetService(typeof(PythonToolsService)) as PythonToolsService;
                var languageClient = service.LanguageClient;
                if (languageClient != null && !this.sourceMap.TryGetValue(textView, out source)) {
                    source = new AsyncCompletionSource(
                        textView,
                        this.TextStructureNavigatorSelectorService,
                        this.EditorCompletionBroker,
                        languageClient);
                    this.sourceMap.Add(textView, source);

                    // We want to make sure we don't block on commit as language servers can take a while to return results.
                    textView.Options.SetOptionValue(DefaultOptions.NonBlockingCompletionOptionId, true);
                    textView.Closed += this.OnTextViewClosed;
                }

            }

            return source;
        }

        private void OnTextViewClosed(object sender, System.EventArgs e) {
            try {
                if (sender is ITextView textView) {
                    textView.Options.SetOptionValue(DefaultOptions.NonBlockingCompletionOptionId, false);
                    this.sourceMap.Remove(textView);
                }
            } catch {
                // Any exceptions caught here should just be swallowed. We may be in a partially disposed state with the editor, and ObjectDisposedException may be thrown.
            }
        }
    }
}

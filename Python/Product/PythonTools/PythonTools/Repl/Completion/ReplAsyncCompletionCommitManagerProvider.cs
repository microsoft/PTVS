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
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Repl.Completion {
    [Export(typeof(IAsyncCompletionCommitManagerProvider))]
    [ContentType(PythonCoreConstants.ReplContentType)]
    [Name("PythonReplCommitManager")]
    internal class ReplAsyncCompletionCommitManagerProvider : IAsyncCompletionCommitManagerProvider {
        private readonly ReplRemoteCompletionBroker _broker = new ReplRemoteCompletionBroker();
        private readonly Dictionary<ITextView, ReplAsyncCompletionCommitManager> _managerMap = new Dictionary<ITextView, ReplAsyncCompletionCommitManager>();

        public IAsyncCompletionCommitManager GetOrCreate(ITextView textView) {
            if (!_managerMap.TryGetValue(textView, out var manager)) {
                manager = new ReplAsyncCompletionCommitManager(_broker, textView);
                _managerMap.Add(textView, manager);
                textView.Closed += OnTextViewClosed;
            }

            return manager;
        }

        private void OnTextViewClosed(object sender, System.EventArgs e) {
            if (sender is ITextView textView) {
                _managerMap.Remove(textView);
            }
        }
    }
}

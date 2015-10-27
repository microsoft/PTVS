// Visual Studio Shared Project
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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.VisualStudioTools.MockVsTests {
    [Export(typeof(ICompletionBroker))]
    class MockCompletionBroker : ICompletionBroker {
        private readonly IEnumerable<Lazy<ICompletionSourceProvider, IContentTypeMetadata>> _completionProviders;
        private readonly IIntellisenseSessionStackMapService _stackMap;

        [ImportingConstructor]
        public MockCompletionBroker(IIntellisenseSessionStackMapService stackMap, [ImportMany]IEnumerable<Lazy<ICompletionSourceProvider, IContentTypeMetadata>> completionProviders) {
            _stackMap = stackMap;
            _completionProviders = completionProviders;
        }

        public ICompletionSession CreateCompletionSession(ITextView textView, ITrackingPoint triggerPoint, bool trackCaret) {
            throw new NotImplementedException();
        }

        public void DismissAllSessions(ITextView textView) {
            foreach (var session in _stackMap.GetStackForTextView(textView).Sessions) {
                if (session is ICompletionSession) {
                    session.Dismiss();
                }
            }
        }

        public ReadOnlyCollection<ICompletionSession> GetSessions(ITextView textView) {
            List<ICompletionSession> res = new List<ICompletionSession>();
            foreach (var session in _stackMap.GetStackForTextView(textView).Sessions) {
                if (session is ICompletionSession) {
                    res.Add(session as ICompletionSession);
                }
            }
            return new ReadOnlyCollection<ICompletionSession>(res);
        }

        public bool IsCompletionActive(ITextView textView) {
            foreach (var session in _stackMap.GetStackForTextView(textView).Sessions) {
                if (session is ICompletionSession) {
                    return true;
                }
            }
            return false;
        }

        public ICompletionSession TriggerCompletion(ITextView textView, ITrackingPoint triggerPoint, bool trackCaret) {
            throw new NotImplementedException();
        }

        public ICompletionSession TriggerCompletion(ITextView textView) {
            ObservableCollection<CompletionSet> sets = new ObservableCollection<CompletionSet>();
            var session = new MockCompletionSession(
                textView,
                sets,
                textView.TextBuffer.CurrentSnapshot.CreateTrackingPoint(
                    textView.Caret.Position.BufferPosition.Position,
                    PointTrackingMode.Negative
                )
            );

            foreach (var provider in _completionProviders) {
                foreach (var targetContentType in provider.Metadata.ContentTypes) {
                    if (textView.TextBuffer.ContentType.IsOfType(targetContentType)) {
                        var source = provider.Value.TryCreateCompletionSource(textView.TextBuffer);
                        if (source != null) {
                            source.AugmentCompletionSession(session, sets);
                        }
                    }
                }
            }

            if (session.CompletionSets.Count > 0 && !session.IsDismissed) {
                _stackMap.GetStackForTextView(textView).PushSession(session);
            }

            return session;
        }
    }
}

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
using System.Linq;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.VisualStudioTools.MockVsTests {
#if DEV14_OR_LATER
#pragma warning disable 0618
#endif

    [Export(typeof(ISmartTagBroker))]
    public class MockSmartTagBroker : ISmartTagBroker {
        private readonly List<KeyValuePair<ITextView, ISmartTagSession>> _sessions = new List<KeyValuePair<ITextView, ISmartTagSession>>();

        public readonly List<ISmartTagSourceProvider> SourceProviders = new List<ISmartTagSourceProvider>();

        public ISmartTagSession CreateSmartTagSession(ITextView textView, SmartTagType type, ITrackingPoint triggerPoint, SmartTagState state) {
            var session = new MockSmartTagSession(this) {
                TextView = textView,
                Type = type,
                TriggerPoint = triggerPoint,
                State = state
            };
            lock (_sessions) {
                _sessions.Add(new KeyValuePair<ITextView, ISmartTagSession>(textView, session));
            }
            session.Dismissed += Session_Dismissed;
            return session;
        }

        private void Session_Dismissed(object sender, EventArgs e) {
            var session = sender as ISmartTagSession;
            if (session != null) {
                lock (_sessions) {
                    _sessions.RemoveAll(kv => kv.Value == session);
                }
            }
        }

        public ReadOnlyCollection<ISmartTagSession> GetSessions(ITextView textView) {
            lock (_sessions) {
                return new ReadOnlyCollection<ISmartTagSession>(
                    _sessions.Where(kv => kv.Key == textView).Select(kv => kv.Value).ToList()
                );
            }
        }

        public bool IsSmartTagActive(ITextView textView) {
            lock (_sessions) {
                return _sessions.Any(kv => kv.Key == textView);
            }
        }


    }
}

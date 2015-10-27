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
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;

namespace Microsoft.VisualStudioTools.MockVsTests {
    [Export(typeof(IQuickInfoBroker))]
    class MockQuickInfoBroker : IQuickInfoBroker {
        public IQuickInfoSession CreateQuickInfoSession(VisualStudio.Text.Editor.ITextView textView, VisualStudio.Text.ITrackingPoint triggerPoint, bool trackMouse) {
            throw new NotImplementedException();
        }

        public System.Collections.ObjectModel.ReadOnlyCollection<IQuickInfoSession> GetSessions(VisualStudio.Text.Editor.ITextView textView) {
            throw new NotImplementedException();
        }

        public bool IsQuickInfoActive(VisualStudio.Text.Editor.ITextView textView) {
            throw new NotImplementedException();
        }

        public IQuickInfoSession TriggerQuickInfo(VisualStudio.Text.Editor.ITextView textView, VisualStudio.Text.ITrackingPoint triggerPoint, bool trackMouse) {
            throw new NotImplementedException();
        }

        public IQuickInfoSession TriggerQuickInfo(VisualStudio.Text.Editor.ITextView textView) {
            throw new NotImplementedException();
        }
    }
}

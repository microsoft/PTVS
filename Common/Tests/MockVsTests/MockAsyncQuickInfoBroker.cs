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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudioTools.MockVsTests
{
    [Export(typeof(IAsyncQuickInfoBroker))]
    class MockAsyncQuickInfoBroker : IAsyncQuickInfoBroker
    {
        public bool IsQuickInfoActive(ITextView textView)
        {
            throw new NotImplementedException();
        }

        public Task<IAsyncQuickInfoSession> TriggerQuickInfoAsync(ITextView textView, ITrackingPoint triggerPoint = null, QuickInfoSessionOptions options = QuickInfoSessionOptions.None, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public IAsyncQuickInfoSession GetSession(ITextView textView)
        {
            throw new NotImplementedException();
        }

        public Task<QuickInfoItemsCollection> GetQuickInfoItemsAsync(ITextView textView, ITrackingPoint triggerPoint, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}

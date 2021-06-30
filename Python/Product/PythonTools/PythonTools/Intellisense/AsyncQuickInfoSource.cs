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

using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Intellisense {
    internal class AsyncQuickInfoSource : IAsyncQuickInfoSource {
        private readonly ITextBuffer _textBuffer;
        private volatile IAsyncQuickInfoSession _curSession;

        public AsyncQuickInfoSource(ITextBuffer textBuffer) {
            _textBuffer = textBuffer;
        }

        #region IAsyncQuickInfoSource Members
        public async Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
            if (_curSession != null && _curSession.State != QuickInfoSessionState.Dismissed) {
                await _curSession.DismissAsync();
                _curSession = null;
            }

            _curSession = session;
            _curSession.StateChanged += CurSessionStateChanged;

            var quickInfo = GetQuickInfo(session.TextView);
            return quickInfo != null ? new QuickInfoItem(quickInfo.Span, quickInfo.Text) : null;
        }

        public static void AddQuickInfo(ITextView view, QuickInfo info) => view.Properties[typeof(QuickInfo)] = info;

        private static QuickInfo GetQuickInfo(ITextView view)
            => view.Properties.TryGetProperty(typeof(QuickInfo), out QuickInfo quickInfo) ? quickInfo : null;

        private void CurSessionStateChanged(object sender, QuickInfoSessionStateChangedEventArgs e)
            => _curSession = null;

        #endregion

        public void Dispose() { }
    }
}

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

using System;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Intellisense {
#pragma warning disable 618 // TODO: switch to quick info async interfaces introduced in 15.6
    internal class QuickInfoSource : IQuickInfoSource {
        private readonly ITextBuffer _textBuffer;
        private IQuickInfoSession _curSession;

        public QuickInfoSource(ITextBuffer textBuffer) {
            _textBuffer = textBuffer;
        }

        #region IQuickInfoSource Members

        public void AugmentQuickInfoSession(IQuickInfoSession session, System.Collections.Generic.IList<object> quickInfoContent, out ITrackingSpan applicableToSpan) {
            if (_curSession != null && !_curSession.IsDismissed) {
                _curSession.Dismiss();
                _curSession = null;
            }

            _curSession = session;
            _curSession.Dismissed += CurSessionDismissed;

            var quickInfo = GetQuickInfo(session.TextView);
            AugmentQuickInfoWorker(quickInfoContent, quickInfo, out applicableToSpan);
        }

        internal static void AugmentQuickInfoWorker(System.Collections.Generic.IList<object> quickInfoContent, QuickInfo quickInfo, out ITrackingSpan applicableToSpan) {
            if (quickInfo != null) {
                quickInfoContent.Add(quickInfo.Text);
                applicableToSpan = quickInfo.Span;
            } else {
                applicableToSpan = null;
            }
        }

        public static void AddQuickInfo(ITextView view, QuickInfo info) {
            view.Properties[typeof(QuickInfo)] = info;
        }

        private static QuickInfo GetQuickInfo(ITextView view) {
            QuickInfo quickInfo;
            if (view.Properties.TryGetProperty(typeof(QuickInfo), out quickInfo)) {
                return quickInfo;
            }
            return null;
        }

        private void CurSessionDismissed(object sender, EventArgs e) {
            _curSession = null;
        }        

        #endregion

        public void Dispose() {
        }
    }
#pragma warning disable 618
}

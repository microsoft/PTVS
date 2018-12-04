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

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace TestUtilities.Python {
    internal static class WpfTextViewExtensions {
        public static void Select(this IWpfTextView textView, string text, bool isReversed = false) {
            var snapshot = textView.TextBuffer.CurrentSnapshot;
            var start = snapshot.GetText().IndexOf(text);
            var span = new SnapshotSpan(snapshot, new Span(start, text.Length));
            textView.Selection.Select(span, isReversed);
            textView.Selection.IsActive = true;
        }

        public static void Select(this IWpfTextView textView, int start, int length, bool isReversed = false) {
            var snapshot = textView.TextBuffer.CurrentSnapshot;
            var span = new SnapshotSpan(snapshot, new Span(start, length));
            textView.Selection.Select(span, isReversed);
            textView.Selection.IsActive = true;
        }

        public static void SelectAll(this IWpfTextView textView, bool isReversed = false) {
            var snapshot = textView.TextBuffer.CurrentSnapshot;
            var span = new SnapshotSpan(snapshot, new Span(0, snapshot.Length));
            textView.Selection.Select(span, isReversed);
            textView.Selection.IsActive = true;
        }

        public static string GetText(this IWpfTextView textView) => textView.TextBuffer.CurrentSnapshot.GetText();
    }
}
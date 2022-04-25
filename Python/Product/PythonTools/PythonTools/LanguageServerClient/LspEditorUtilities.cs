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
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.LanguageServerClient {
    internal static class LspEditorUtilities {
        internal static SnapshotPoint GetSnapshotPositionFromProtocolPosition(this ITextSnapshot textSnapshot, Position position) {
            var line = textSnapshot.GetLineNumberFromPosition(position.Line);
            var snapshotPosition = textSnapshot.GetLineFromLineNumber(position.Line).Start + position.Character;

            return new SnapshotPoint(textSnapshot, snapshotPosition);
        }

        internal static SnapshotPoint? GetPointAtSubjectBuffer(this ITextView textView, SnapshotPoint point, ITextBuffer textBuffer) {
            if (textBuffer != null) {
                SnapshotSpan caretSpan = new SnapshotSpan(point, point);

                foreach (SnapshotSpan span in textView.BufferGraph.MapDownToBuffer(caretSpan, SpanTrackingMode.EdgeInclusive, textBuffer)) {
                    return span.Start;
                }
            }

            return null;
        }

        internal static Position GetPosition(this SnapshotPoint snapshotPoint) {
            var position = new Position();
            position.Line = snapshotPoint.GetContainingLine().LineNumber;
            position.Character = snapshotPoint.Position - snapshotPoint.GetContainingLine().Start.Position;

            return position;
        }

        internal static SnapshotPoint? GetCaretPointAtSubjectBuffer(this ITextView textView, ITextBuffer textBuffer) {
            return textView.GetPointAtSubjectBuffer(textView.Caret.Position.BufferPosition, textBuffer);
        }

        internal static void ApplyTextEdits(IEnumerable<TextEdit> textEdits, ITextSnapshot snapshot, ITextBuffer textBuffer) {
            var vsTextEdit = textBuffer.CreateEdit();
            foreach (var textEdit in textEdits) {
                if (string.IsNullOrEmpty(textEdit.NewText)) {
                    var startPosition = snapshot.GetSnapshotPositionFromProtocolPosition(textEdit.Range.Start);
                    var endPosition = snapshot.GetSnapshotPositionFromProtocolPosition(textEdit.Range.End);
                    var difference = endPosition - startPosition;
                    if (startPosition > -1 && endPosition > -1 && difference > 0) {
                        var span = GetTranslatedSpan(startPosition, difference, snapshot, vsTextEdit.Snapshot);
                        vsTextEdit.Delete(span);
                    }
                } else if (textEdit.Range.Start == textEdit.Range.End) {
                    var position = snapshot.GetSnapshotPositionFromProtocolPosition(textEdit.Range.Start);
                    if (position > -1) {
                        var span = GetTranslatedSpan(position, 0, snapshot, vsTextEdit.Snapshot);
                        vsTextEdit.Insert(span.Start, textEdit.NewText);
                    }
                } else {
                    var startPosition = snapshot.GetSnapshotPositionFromProtocolPosition(textEdit.Range.Start);
                    var endPosition = snapshot.GetSnapshotPositionFromProtocolPosition(textEdit.Range.End);
                    var difference = endPosition - startPosition;

                    if (startPosition > -1 && endPosition > -1 && difference > 0) {
                        var span = GetTranslatedSpan(startPosition, difference, snapshot, vsTextEdit.Snapshot);
                        vsTextEdit.Replace(span, textEdit.NewText);
                    }
                }
            }

            vsTextEdit.Apply();
        }

        internal static void ApplyTextEdit(TextEdit textEdit, ITextSnapshot snapshot, ITextBuffer textBuffer) {
            ApplyTextEdits(new[] { textEdit }, snapshot, textBuffer);
        }

        private static Span GetTranslatedSpan(int startPosition, int length, ITextSnapshot oldSnapshot, ITextSnapshot newSnapshot) {
            var span = new Span(startPosition, length);

            if (oldSnapshot.Version != newSnapshot.Version) {
                var snapshotSpan = new SnapshotSpan(oldSnapshot, span);
                var translatedSnapshotSpan = snapshotSpan.TranslateTo(newSnapshot, SpanTrackingMode.EdgeExclusive);
                span = translatedSnapshotSpan.Span;
            }

            return span;
        }
    }
}

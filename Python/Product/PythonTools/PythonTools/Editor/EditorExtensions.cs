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
using System.Diagnostics;
using Microsoft.Python.Core.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Editor.Core {
    internal static class EditorExtensions {
        internal static bool IsPythonContent(this ITextBuffer buffer) {
            return buffer.ContentType.IsOfType(PythonCoreConstants.ContentType);
        }

        internal static bool IsPythonContent(this ITextSnapshot buffer) {
            return buffer.ContentType.IsOfType(PythonCoreConstants.ContentType);
        }

        private static SnapshotPoint? MapPoint(ITextView view, SnapshotPoint point) {
            return view.BufferGraph.MapDownToFirstMatch(
               point,
               PointTrackingMode.Positive,
               IsPythonContent,
               PositionAffinity.Successor
            );
        }

        public static SnapshotPoint? MapDownToPythonBuffer(this ITextView view, SnapshotPoint point) => MapPoint(view, point);

        /// <summary>
        /// Maps down to the buffer using positive point tracking and successor position affinity
        /// </summary>
        public static SnapshotPoint? MapDownToBuffer(this ITextView textView, int position, ITextBuffer buffer)
        {
            if (textView.BufferGraph == null) {
                // Unit test case
                if (position <= buffer.CurrentSnapshot.Length) {
                    return new SnapshotPoint(buffer.CurrentSnapshot, position);
                }
                return null;
            }
            if (position <= textView.TextBuffer.CurrentSnapshot.Length) {
                return textView.BufferGraph.MapDownToBuffer(
                    new SnapshotPoint(textView.TextBuffer.CurrentSnapshot, position),
                    PointTrackingMode.Positive,
                    buffer,
                    PositionAffinity.Successor
                );
            }
            return null;
        }

        // TODO: currently unused, could be deleted
        //public static SourceLocation ToSourceLocation(this SnapshotPoint point) {
        //    return new SourceLocation(
        //        point.Position,
        //        point.GetContainingLine().LineNumber + 1,
        //        point.Position - point.GetContainingLine().Start.Position + 1
        //    );
        //}

        //public static SourceSpan ToSourceSpan(this SnapshotSpan span) {
        //    return new SourceSpan(
        //        ToSourceLocation(span.Start),
        //        ToSourceLocation(span.End)
        //    );
        //}

        //public static SnapshotPoint ToSnapshotPoint(this SourceLocation location, ITextSnapshot snapshot) {
        //    ITextSnapshotLine line;

        //    if (location.Line < 1) {
        //        return new SnapshotPoint(snapshot, 0);
        //    }

        //    try {
        //        line = snapshot.GetLineFromLineNumber(location.Line - 1);
        //    } catch (ArgumentOutOfRangeException) {
        //        Debug.Assert(location.Line == snapshot.LineCount + 1 && location.Column == 1,
        //            $"Out of range should only occur at end of snapshot ({snapshot.LineCount + 1}, 1), not at {location}");
        //        return new SnapshotPoint(snapshot, snapshot.Length);
        //    }

        //    if (location.Column > line.LengthIncludingLineBreak) {
        //        return line.EndIncludingLineBreak;
        //    }
        //    return line.Start + (location.Column - 1);
        //}

        //public static SnapshotSpan ToSnapshotSpan(this SourceSpan span, ITextSnapshot snapshot) {
        //    return new SnapshotSpan(
        //        ToSnapshotPoint(span.Start, snapshot),
        //        ToSnapshotPoint(span.End, snapshot)
        //    );
        //}
    }
}

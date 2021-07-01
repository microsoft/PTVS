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

using Microsoft.PythonTools.Editor;

namespace Microsoft.PythonTools.Intellisense
{
    /// <summary>
    /// Creates a preview window for showing the difference that will occur when changes are 
    /// applied.
    /// </summary>
    [Export(typeof(PreviewChangesService))]
    class PreviewChangesService
    {
        private readonly IWpfDifferenceViewerFactoryService _diffFactory;
        private readonly IDifferenceBufferFactoryService _diffBufferFactory;
        private readonly ITextBufferFactoryService _bufferFactory;
        private readonly ITextViewRoleSet _previewRoleSet;

        [ImportingConstructor]
        public PreviewChangesService(IWpfDifferenceViewerFactoryService diffFactory, IDifferenceBufferFactoryService diffBufferFactory, ITextBufferFactoryService bufferFactory, ITextEditorFactoryService textEditorFactoryService)
        {
            _diffFactory = diffFactory;
            _diffBufferFactory = diffBufferFactory;
            _bufferFactory = bufferFactory;
            _previewRoleSet = textEditorFactoryService.CreateTextViewRoleSet(PredefinedTextViewRoles.Analyzable);
        }

        public object CreateDiffView(AnalysisProtocol.ChangeInfo[] changes, PythonTextBufferInfo buffer, int atVersion)
        {
            if (changes == null || buffer == null || !buffer.LocationTracker.CanTranslateFrom(atVersion))
            {
                return null;
            }

            var snapshot = buffer.CurrentSnapshot;

            // Create a copy of the left hand buffer (we're going to remove all of the
            // content we don't care about from it).
            var leftBuffer = _bufferFactory.CreateTextBuffer(buffer.Buffer.ContentType);
            using (var edit = leftBuffer.CreateEdit())
            {
                edit.Insert(0, snapshot.GetText());
                edit.Apply();
            }

            // create a buffer for the right hand side, copy the original buffer
            // into it, and then apply the changes.
            var rightBuffer = _bufferFactory.CreateTextBuffer(buffer.Buffer.ContentType);
            using (var edit = rightBuffer.CreateEdit())
            {
                edit.Insert(0, snapshot.GetText());
                edit.Apply();
            }

            var startingVersion = rightBuffer.CurrentSnapshot;

            VsProjectAnalyzer.ApplyChanges(changes, rightBuffer, new LocationTracker(startingVersion), startingVersion.Version.VersionNumber);

            var textChanges = startingVersion.Version.Changes;
            int minPos = startingVersion.Length, maxPos = 0;
            foreach (var change in textChanges)
            {
                minPos = Math.Min(change.OldPosition, minPos);
                maxPos = Math.Max(change.OldPosition, maxPos);
            }

            if (minPos == startingVersion.Length && maxPos == 0)
            {
                // no changes?  that's weird...
                return null;
            }

            MinimizeBuffers(leftBuffer, rightBuffer, startingVersion, minPos, maxPos);

            // create the difference buffer and view...
            var diffBuffer = _diffBufferFactory.CreateDifferenceBuffer(leftBuffer, rightBuffer);
            var diffView = _diffFactory.CreateDifferenceView(diffBuffer, _previewRoleSet);

            diffView.ViewMode = DifferenceViewMode.Inline;
            diffView.InlineView.ZoomLevel *= .75;
            diffView.InlineView.VisualElement.Focusable = false;
            diffView.InlineHost.GetTextViewMargin("deltadifferenceViewerOverview").VisualElement.Visibility = System.Windows.Visibility.Collapsed;

            // Reduce the size of the buffer once it's ready
            diffView.DifferenceBuffer.SnapshotDifferenceChanged += (sender, args) =>
            {
                diffView.InlineView.DisplayTextLineContainingBufferPosition(
                    new SnapshotPoint(diffView.DifferenceBuffer.CurrentInlineBufferSnapshot, 0),
                    0.0, ViewRelativePosition.Top, double.MaxValue, double.MaxValue
                );

                var width = Math.Max(diffView.InlineView.MaxTextRightCoordinate * (diffView.InlineView.ZoomLevel / 100), 400); // Width of the widest line.
                var height = diffView.InlineView.LineHeight * (diffView.InlineView.ZoomLevel / 100) * // Height of each line.
                    diffView.DifferenceBuffer.CurrentInlineBufferSnapshot.LineCount;

                diffView.VisualElement.Width = width;
                diffView.VisualElement.Height = height;
            };

            return diffView.VisualElement;
        }

        private static void MinimizeBuffers(ITextBuffer leftBuffer, ITextBuffer rightBuffer, ITextSnapshot startingVersion, int minPos, int maxPos)
        {
            // Remove the unchanged content from both buffers
            using (var edit = leftBuffer.CreateEdit())
            {
                edit.Delete(0, minPos);
                edit.Delete(Span.FromBounds(maxPos, startingVersion.Length));
                edit.Apply();
            }

            using (var edit = rightBuffer.CreateEdit())
            {
                edit.Delete(
                    0,
                    Tracking.TrackPositionForwardInTime(
                        PointTrackingMode.Negative,
                        minPos,
                        startingVersion.Version,
                        rightBuffer.CurrentSnapshot.Version
                    )
                );

                edit.Delete(
                    Span.FromBounds(
                        Tracking.TrackPositionForwardInTime(
                            PointTrackingMode.Positive,
                            maxPos,
                            startingVersion.Version,
                            rightBuffer.CurrentSnapshot.Version
                        ),
                        rightBuffer.CurrentSnapshot.Length
                    )
                );
                edit.Apply();
            }
        }
    }
}

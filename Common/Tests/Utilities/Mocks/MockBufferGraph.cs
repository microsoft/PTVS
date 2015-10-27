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
using System.Windows.Documents;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Projection;

namespace TestUtilities.Mocks {
    public class MockBufferGraph : IBufferGraph {
        private readonly MockTextView _view;
        private readonly List<ITextBuffer> _buffers = new List<ITextBuffer>();

        public MockBufferGraph(MockTextView view) {
            _view = view;
            _buffers.Add(view.TextBuffer);
        }

        public IMappingPoint CreateMappingPoint(SnapshotPoint point, PointTrackingMode trackingMode) {
            throw new NotImplementedException();
        }

        public IMappingSpan CreateMappingSpan(SnapshotSpan span, SpanTrackingMode trackingMode) {
            throw new NotImplementedException();
        }

        public Collection<ITextBuffer> GetTextBuffers(Predicate<ITextBuffer> match) {
            var res = new Collection<ITextBuffer>();
            foreach (var buffer in _buffers) {
                if (match(buffer)) {
                    res.Add(buffer);
                }
            }
            return res;
        }

        public void AddBuffer(ITextBuffer buffer) {
            _buffers.Add(buffer);
        }

        public event EventHandler<GraphBufferContentTypeChangedEventArgs> GraphBufferContentTypeChanged {
            add { }
            remove { }
        }

        public event EventHandler<GraphBuffersChangedEventArgs> GraphBuffersChanged {
            add {
            }
            remove {
            }
        }

        public NormalizedSnapshotSpanCollection MapDownToBuffer(SnapshotSpan span, SpanTrackingMode trackingMode, ITextBuffer targetBuffer) {
            throw new NotImplementedException();
        }

        public SnapshotPoint? MapDownToBuffer(SnapshotPoint position, PointTrackingMode trackingMode, ITextBuffer targetBuffer, PositionAffinity affinity) {
            throw new NotImplementedException();
        }

        public NormalizedSnapshotSpanCollection MapDownToFirstMatch(SnapshotSpan span, SpanTrackingMode trackingMode, Predicate<ITextSnapshot> match) {
            throw new NotImplementedException();
        }

        public SnapshotPoint? MapDownToFirstMatch(SnapshotPoint position, PointTrackingMode trackingMode, Predicate<ITextSnapshot> match, PositionAffinity affinity) {
            return position;
        }

        public SnapshotPoint? MapDownToInsertionPoint(SnapshotPoint position, PointTrackingMode trackingMode, Predicate<ITextSnapshot> match) {
            var snapshot = position.Snapshot;
            var buffer = snapshot.TextBuffer;
            int pos = position.TranslateTo(snapshot, trackingMode);
            while (!match(snapshot)) {
                var projBuffer = buffer as IProjectionBufferBase;
                if (projBuffer == null) {
                    return null;
                }
                var projSnapshot = projBuffer.CurrentSnapshot;
                if (projSnapshot.SourceSnapshots.Count == 0) {
                    return null;
                }
                var pt = projSnapshot.MapToSourceSnapshot(pos);
                pos = pt.Position;
                snapshot = pt.Snapshot;
                buffer = snapshot.TextBuffer;
            }
            return new SnapshotPoint(snapshot, pos);
        }

        public NormalizedSnapshotSpanCollection MapDownToSnapshot(SnapshotSpan span, SpanTrackingMode trackingMode, ITextSnapshot targetSnapshot) {
            throw new NotImplementedException();
        }

        public SnapshotPoint? MapDownToSnapshot(SnapshotPoint position, PointTrackingMode trackingMode, ITextSnapshot targetSnapshot, PositionAffinity affinity) {
            throw new NotImplementedException();
        }

        public NormalizedSnapshotSpanCollection MapUpToBuffer(SnapshotSpan span, SpanTrackingMode trackingMode, ITextBuffer targetBuffer) {
            throw new NotImplementedException();
        }

        public SnapshotPoint? MapUpToBuffer(SnapshotPoint point, PointTrackingMode trackingMode, PositionAffinity affinity, ITextBuffer targetBuffer) {
            int position = 0;
            for (int i = 0; i < _buffers.Count; i++) {
                if (_buffers[i] == targetBuffer) {
                    return new SnapshotPoint(point.Snapshot, position + point.Position);
                }
                position += _buffers[i].CurrentSnapshot.Length;
            }
            return null;
        }

        public NormalizedSnapshotSpanCollection MapUpToFirstMatch(SnapshotSpan span, SpanTrackingMode trackingMode, Predicate<ITextSnapshot> match) {
            throw new NotImplementedException();
        }

        public SnapshotPoint? MapUpToFirstMatch(SnapshotPoint point, PointTrackingMode trackingMode, Predicate<ITextSnapshot> match, PositionAffinity affinity) {
            throw new NotImplementedException();
        }

        public NormalizedSnapshotSpanCollection MapUpToSnapshot(SnapshotSpan span, SpanTrackingMode trackingMode, ITextSnapshot targetSnapshot) {
            throw new NotImplementedException();
        }

        public SnapshotPoint? MapUpToSnapshot(SnapshotPoint point, PointTrackingMode trackingMode, PositionAffinity affinity, ITextSnapshot targetSnapshot) {
            throw new NotImplementedException();
        }

        public ITextBuffer TopBuffer {
            get { throw new NotImplementedException(); }
        }
    }
}

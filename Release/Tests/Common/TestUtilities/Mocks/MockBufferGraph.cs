/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.Text;

namespace TestUtilities.Mocks {
    public class MockBufferGraph : IBufferGraph {
        public IMappingPoint CreateMappingPoint(SnapshotPoint point, PointTrackingMode trackingMode) {
            throw new NotImplementedException();
        }

        public IMappingSpan CreateMappingSpan(SnapshotSpan span, SpanTrackingMode trackingMode) {
            throw new NotImplementedException();
        }

        public System.Collections.ObjectModel.Collection<ITextBuffer> GetTextBuffers(Predicate<ITextBuffer> match) {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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

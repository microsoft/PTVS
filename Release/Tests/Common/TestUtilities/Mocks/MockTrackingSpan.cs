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
using Microsoft.VisualStudio.Text;

namespace TestUtilities.Mocks {
    public class MockTrackingSpan : ITrackingSpan {
        private readonly int _start, _length;
        private readonly MockTextSnapshot _snapshot;

        public MockTrackingSpan(MockTextSnapshot snapshot, int start, int length) {
            _start = start;
            _length = length;
            _snapshot = snapshot;
        }

        public SnapshotPoint GetEndPoint(ITextSnapshot snapshot) {
            return new SnapshotPoint(_snapshot, _start + _length);
        }

        public Span GetSpan(ITextVersion version) {
            return new Span(_start, _length);
        }

        public SnapshotSpan GetSpan(ITextSnapshot snapshot) {
            return new SnapshotSpan(snapshot, new Span(_start, _length));
        }

        public SnapshotPoint GetStartPoint(ITextSnapshot snapshot) {
            throw new NotImplementedException();
        }

        public string GetText(ITextSnapshot snapshot) {
            throw new NotImplementedException();
        }

        public ITextBuffer TextBuffer {
            get { return _snapshot.TextBuffer; }
        }

        public TrackingFidelityMode TrackingFidelity {
            get { throw new NotImplementedException(); }
        }

        public SpanTrackingMode TrackingMode {
            get { throw new NotImplementedException(); }
        }
    }
}

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

namespace TestUtilities.Mocks
{
    public class MockTrackingSpan : ITrackingSpan
    {
        private readonly int _start, _length;
        private readonly MockTextSnapshot _snapshot;
        private readonly SpanTrackingMode _trackingMode;
        private readonly ITrackingPoint _startPoint, _endPoint;

        public MockTrackingSpan(MockTextSnapshot snapshot, int start, int length, SpanTrackingMode trackingMode = SpanTrackingMode.EdgeExclusive)
        {
            _start = start;
            _length = length;
            _snapshot = snapshot;
            _trackingMode = trackingMode;
            switch (_trackingMode)
            {
                case SpanTrackingMode.EdgeExclusive:
                    _startPoint = new MockTrackingPoint(snapshot, start, PointTrackingMode.Positive);
                    _endPoint = new MockTrackingPoint(snapshot, start + length, PointTrackingMode.Negative);
                    break;
                case SpanTrackingMode.EdgeInclusive:
                    _startPoint = new MockTrackingPoint(snapshot, start, PointTrackingMode.Negative);
                    _endPoint = new MockTrackingPoint(snapshot, start + length, PointTrackingMode.Positive);
                    break;
                case SpanTrackingMode.EdgeNegative:
                    _startPoint = new MockTrackingPoint(snapshot, start, PointTrackingMode.Negative);
                    _endPoint = new MockTrackingPoint(snapshot, start + length, PointTrackingMode.Negative);
                    break;
                case SpanTrackingMode.EdgePositive:
                    _startPoint = new MockTrackingPoint(snapshot, start, PointTrackingMode.Positive);
                    _endPoint = new MockTrackingPoint(snapshot, start + length, PointTrackingMode.Positive);
                    break;
            }
        }

        public SnapshotPoint GetEndPoint(ITextSnapshot snapshot)
        {
            return new SnapshotPoint(_snapshot, _start + _length);
        }

        public Span GetSpan(ITextVersion version)
        {
            return Span.FromBounds(
                _startPoint.GetPosition(version),
                _endPoint.GetPosition(version)
            );
        }

        public SnapshotSpan GetSpan(ITextSnapshot snapshot)
        {
            return new SnapshotSpan(snapshot, GetSpan(snapshot.Version));
        }

        public SnapshotPoint GetStartPoint(ITextSnapshot snapshot)
        {
            var span = GetSpan(snapshot.Version);
            return new SnapshotPoint(snapshot, span.Start);
        }

        public string GetText(ITextSnapshot snapshot)
        {
            var span = GetSpan(snapshot.Version);
            return snapshot.GetText(span);
        }

        public ITextBuffer TextBuffer
        {
            get { return _snapshot.TextBuffer; }
        }

        public TrackingFidelityMode TrackingFidelity
        {
            get { throw new NotImplementedException(); }
        }

        public SpanTrackingMode TrackingMode
        {
            get { return _trackingMode; }
        }
    }
}

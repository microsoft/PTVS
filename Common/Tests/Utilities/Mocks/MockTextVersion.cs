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
    public class MockTextVersion : ITextVersion2
    {
        private readonly int _version;
        private readonly MockTextImageVersion _imageVersion;
        internal readonly MockTextSnapshot _snapshot;
        private MockTextVersion _nextVersion;
        private INormalizedTextChangeCollection _changes;

        public MockTextVersion(int version, MockTextSnapshot snapshot)
        {
            _version = version;
            _snapshot = snapshot;
            _imageVersion = new MockTextImageVersion(this);
        }

        /// <summary>
        /// changes to get to the next version
        /// </summary>
        public INormalizedTextChangeCollection Changes => _changes;

        public ITrackingSpan CreateCustomTrackingSpan(Span span, TrackingFidelityMode trackingFidelity, object customState, CustomTrackToVersion behavior)
        {
            throw new NotImplementedException();
        }

        public ITrackingPoint CreateTrackingPoint(int position, PointTrackingMode trackingMode, TrackingFidelityMode trackingFidelity)
        {
            throw new NotImplementedException();
        }

        public ITrackingPoint CreateTrackingPoint(int position, PointTrackingMode trackingMode)
        {
            throw new NotImplementedException();
        }

        public ITrackingSpan CreateTrackingSpan(int start, int length, SpanTrackingMode trackingMode, TrackingFidelityMode trackingFidelity)
        {
            throw new NotImplementedException();
        }

        public ITrackingSpan CreateTrackingSpan(int start, int length, SpanTrackingMode trackingMode)
        {
            throw new NotImplementedException();
        }

        public ITrackingSpan CreateTrackingSpan(Span span, SpanTrackingMode trackingMode, TrackingFidelityMode trackingFidelity)
        {
            throw new NotImplementedException();
        }

        public ITrackingSpan CreateTrackingSpan(Span span, SpanTrackingMode trackingMode)
        {
            throw new NotImplementedException();
        }

        public int Length => _snapshot.Length;

        public ITextVersion Next => _nextVersion;

        public int ReiteratedVersionNumber => throw new NotImplementedException();

        public ITextBuffer TextBuffer => _snapshot.TextBuffer;

        public int VersionNumber => _version;

        public ITextImageVersion ImageVersion => _imageVersion;

        internal void SetNext(MockTextVersion nextVersion, params ITextChange[] changes)
        {
            _nextVersion = nextVersion;
            _changes = new MockNormalizedTextChangeCollection(changes);
        }
    }
}

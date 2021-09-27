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
    public class MockMappingPoint : IMappingPoint
    {
        private readonly ITrackingPoint _trackingPoint;

        public MockMappingPoint(ITrackingPoint trackingPoint)
        {
            _trackingPoint = trackingPoint;
        }

        public ITextBuffer AnchorBuffer
        {
            get { throw new NotImplementedException(); }
        }

        public IBufferGraph BufferGraph
        {
            get { throw new NotImplementedException(); }
        }

        public SnapshotPoint? GetInsertionPoint(Predicate<ITextBuffer> match)
        {
            throw new NotImplementedException();
        }

        public SnapshotPoint? GetPoint(Predicate<ITextBuffer> match, PositionAffinity affinity)
        {
            throw new NotImplementedException();
        }

        public SnapshotPoint? GetPoint(ITextSnapshot targetSnapshot, PositionAffinity affinity)
        {
            try
            {
                return _trackingPoint.GetPoint(targetSnapshot);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        public SnapshotPoint? GetPoint(ITextBuffer targetBuffer, PositionAffinity affinity)
        {
            return GetPoint(targetBuffer.CurrentSnapshot, affinity);
        }
    }
}

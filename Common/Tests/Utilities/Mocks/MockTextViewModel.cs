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
	public class MockTextViewModel : ITextViewModel
	{
		public ITextBuffer DataBuffer { get; set; }

		public ITextDataModel DataModel => throw new NotImplementedException();

		public ITextBuffer EditBuffer { get; set; }

		public SnapshotPoint GetNearestPointInVisualBuffer(SnapshotPoint editBufferPoint)
		{
			throw new NotImplementedException();
		}

		public SnapshotPoint GetNearestPointInVisualSnapshot(SnapshotPoint editBufferPoint, ITextSnapshot targetVisualSnapshot, PointTrackingMode trackingMode)
		{
			throw new NotImplementedException();
		}

		public bool IsPointInVisualBuffer(SnapshotPoint editBufferPoint, PositionAffinity affinity)
		{
			throw new NotImplementedException();
		}

		public ITextBuffer VisualBuffer => throw new NotImplementedException();

		public PropertyCollection Properties => throw new NotImplementedException();

		public void Dispose()
		{
			throw new NotImplementedException();
		}
	}
}

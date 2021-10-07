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
	public class MockTextCaret : ITextCaret
	{
		private MockTrackingPoint _position;
		private readonly MockTextView _view;

		public MockTextCaret(MockTextView view)
		{
			_view = view;
			_position = new MockTrackingPoint((MockTextSnapshot)_view.TextBuffer.CurrentSnapshot, 0);
		}

		public double Bottom => throw new System.NotImplementedException();

		public Microsoft.VisualStudio.Text.Formatting.ITextViewLine ContainingTextViewLine => throw new System.NotImplementedException();

		public void EnsureVisible()
		{
		}

		public double Height => throw new System.NotImplementedException();

		public bool InVirtualSpace => throw new System.NotImplementedException();

		public bool IsHidden
		{
			get => throw new System.NotImplementedException();
			set => throw new System.NotImplementedException();
		}

		public double Left => throw new System.NotImplementedException();

		public CaretPosition MoveTo(Microsoft.VisualStudio.Text.VirtualSnapshotPoint bufferPosition, Microsoft.VisualStudio.Text.PositionAffinity caretAffinity, bool captureHorizontalPosition)
		{
			throw new System.NotImplementedException();
		}

		public CaretPosition MoveTo(Microsoft.VisualStudio.Text.VirtualSnapshotPoint bufferPosition, Microsoft.VisualStudio.Text.PositionAffinity caretAffinity)
		{
			throw new System.NotImplementedException();
		}

		public CaretPosition MoveTo(Microsoft.VisualStudio.Text.VirtualSnapshotPoint bufferPosition)
		{
			throw new System.NotImplementedException();
		}

		public CaretPosition MoveTo(Microsoft.VisualStudio.Text.SnapshotPoint bufferPosition, Microsoft.VisualStudio.Text.PositionAffinity caretAffinity, bool captureHorizontalPosition)
		{
			throw new System.NotImplementedException();
		}

		public CaretPosition MoveTo(Microsoft.VisualStudio.Text.SnapshotPoint bufferPosition, Microsoft.VisualStudio.Text.PositionAffinity caretAffinity)
		{
			throw new System.NotImplementedException();
		}

		public CaretPosition MoveTo(Microsoft.VisualStudio.Text.SnapshotPoint bufferPosition)
		{
			_view.Selection.Clear();
			_position = new MockTrackingPoint((MockTextSnapshot)bufferPosition.Snapshot, bufferPosition.Position);
			return Position;
		}

		public CaretPosition MoveTo(Microsoft.VisualStudio.Text.Formatting.ITextViewLine textLine)
		{
			throw new System.NotImplementedException();
		}

		public CaretPosition MoveTo(Microsoft.VisualStudio.Text.Formatting.ITextViewLine textLine, double xCoordinate, bool captureHorizontalPosition)
		{
			throw new System.NotImplementedException();
		}

		public CaretPosition MoveTo(Microsoft.VisualStudio.Text.Formatting.ITextViewLine textLine, double xCoordinate)
		{
			throw new System.NotImplementedException();
		}

		public CaretPosition MoveToNextCaretPosition()
		{
			throw new System.NotImplementedException();
		}

		public CaretPosition MoveToPreferredCoordinates()
		{
			throw new System.NotImplementedException();
		}

		public CaretPosition MoveToPreviousCaretPosition()
		{
			throw new System.NotImplementedException();
		}

		public bool OverwriteMode => throw new System.NotImplementedException();

		public CaretPosition Position => new CaretPosition(
			  new VirtualSnapshotPoint(_position.GetPoint(_view.TextBuffer.CurrentSnapshot)),
			  new MockMappingPoint(_position),
			  PositionAffinity.Predecessor);

		internal void SetPosition(SnapshotPoint position)
		{
			_position = new MockTrackingPoint((MockTextSnapshot)position.Snapshot, position.Position);
		}

		public event System.EventHandler<CaretPositionChangedEventArgs> PositionChanged
		{
			add
			{
			}
			remove
			{
			}
		}

		public double Right => throw new System.NotImplementedException();

		public double Top => throw new System.NotImplementedException();

		public double Width => throw new System.NotImplementedException();
	}
}

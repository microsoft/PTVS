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
	public class MockTextView : IWpfTextView, ITextView
	{
		private readonly ITextBuffer _buffer;
		private readonly PropertyCollection _props = new PropertyCollection();
		private readonly MockTextSelection _selection;
		private readonly MockTextCaret _caret;
		private readonly MockBufferGraph _bufferGraph;
		private bool _hasFocus;
		private ITextViewModel _textViewModel;

		private static readonly ITextViewModel _notImplementedTextViewModel = new MockTextViewModel();

		public MockTextView(ITextBuffer buffer)
		{
			_buffer = buffer;
			_selection = new MockTextSelection(this);
			_bufferGraph = new MockBufferGraph(this);
			_caret = new MockTextCaret(this);
		}

		public MockBufferGraph BufferGraph => _bufferGraph;

		IBufferGraph ITextView.BufferGraph => _bufferGraph;

		public ITextCaret Caret => _caret;

		public void Close()
		{
			IsClosed = true;
			var evt = Closed;
			if (evt != null)
			{
				evt(this, EventArgs.Empty);
			}
		}

		public event EventHandler Closed;

		public void DisplayTextLineContainingBufferPosition(Microsoft.VisualStudio.Text.SnapshotPoint bufferPosition, double verticalDistance, ViewRelativePosition relativeTo, double? viewportWidthOverride, double? viewportHeightOverride)
		{
			throw new NotImplementedException();
		}

		public void DisplayTextLineContainingBufferPosition(Microsoft.VisualStudio.Text.SnapshotPoint bufferPosition, double verticalDistance, ViewRelativePosition relativeTo)
		{
			throw new NotImplementedException();
		}

		public Microsoft.VisualStudio.Text.SnapshotSpan GetTextElementSpan(Microsoft.VisualStudio.Text.SnapshotPoint point)
		{
			throw new NotImplementedException();
		}

		public Microsoft.VisualStudio.Text.Formatting.ITextViewLine GetTextViewLineContainingBufferPosition(Microsoft.VisualStudio.Text.SnapshotPoint bufferPosition)
		{
			throw new NotImplementedException();
		}

		public event EventHandler GotAggregateFocus;

		public void OnGotAggregateFocus()
		{
			var gotFocus = GotAggregateFocus;
			if (gotFocus != null)
			{
				gotFocus(this, EventArgs.Empty);
			}
			_hasFocus = true;
		}

		public bool HasAggregateFocus => _hasFocus;

		public bool InLayout => throw new NotImplementedException();

		public bool IsClosed { get; set; }

		public bool IsMouseOverViewOrAdornments => throw new NotImplementedException();

		public event EventHandler<TextViewLayoutChangedEventArgs> LayoutChanged
		{
			add { throw new NotImplementedException(); }
			remove { throw new NotImplementedException(); }
		}

		public double LineHeight => throw new NotImplementedException();

		public event EventHandler LostAggregateFocus;

		public void OnLostAggregateFocus()
		{
			var lostFocus = LostAggregateFocus;
			if (lostFocus != null)
			{
				lostFocus(this, EventArgs.Empty);
			}
			_hasFocus = false;
		}

		public double MaxTextRightCoordinate => throw new NotImplementedException();

		public event EventHandler<MouseHoverEventArgs> MouseHover;

		public void HoverMouse(MouseHoverEventArgs args)
		{
			var mouseHover = MouseHover;
			if (mouseHover != null)
			{
				mouseHover(this, args);
			}
		}

		public IEditorOptions Options => new MockTextOptions();

		public Microsoft.VisualStudio.Text.ITrackingSpan ProvisionalTextHighlight
		{
			get => throw new NotImplementedException();
			set => throw new NotImplementedException();
		}

		public void QueueSpaceReservationStackRefresh()
		{
			throw new NotImplementedException();
		}

		public ITextViewRoleSet Roles { get; } = new MockTextViewRoleSet();

		public ITextSelection Selection => _selection;

		public ITextBuffer TextBuffer => _buffer;

		public Microsoft.VisualStudio.Text.ITextDataModel TextDataModel => throw new NotImplementedException();

		public Microsoft.VisualStudio.Text.ITextSnapshot TextSnapshot => _buffer.CurrentSnapshot;

		public ITextViewLineCollection TextViewLines => throw new NotImplementedException();

		public ITextViewModel TextViewModel
		{
			get
			{
				if (_textViewModel == _notImplementedTextViewModel)
				{
					// To avoid the NotImplementedException, you should set
					// TextViewModel as part of initializing the test
					throw new NotImplementedException();
				}
				return _textViewModel;
			}
			set => _textViewModel = value;
		}

		public IViewScroller ViewScroller => throw new NotImplementedException();

		public double ViewportBottom => throw new NotImplementedException();

		public double ViewportHeight => throw new NotImplementedException();

		public event EventHandler ViewportHeightChanged
		{
			add { throw new NotImplementedException(); }
			remove { throw new NotImplementedException(); }
		}

		public double ViewportLeft
		{
			get => throw new NotImplementedException();
			set => throw new NotImplementedException();
		}

		public event EventHandler ViewportLeftChanged
		{
			add { throw new NotImplementedException(); }
			remove { throw new NotImplementedException(); }
		}

		public double ViewportRight => throw new NotImplementedException();

		public double ViewportTop => throw new NotImplementedException();

		public double ViewportWidth => throw new NotImplementedException();

		public event EventHandler ViewportWidthChanged
		{
			add { throw new NotImplementedException(); }
			remove { throw new NotImplementedException(); }
		}

		public Microsoft.VisualStudio.Text.ITextSnapshot VisualSnapshot => throw new NotImplementedException();

		public Microsoft.VisualStudio.Utilities.PropertyCollection Properties => _props;

		#region IWpfTextView Members

		public System.Windows.Media.Brush Background
		{
			get => throw new NotImplementedException();
			set => throw new NotImplementedException();
		}

		public event EventHandler<BackgroundBrushChangedEventArgs> BackgroundBrushChanged
		{
			add { }
			remove { }
		}

		public Microsoft.VisualStudio.Text.Formatting.IFormattedLineSource FormattedLineSource => throw new NotImplementedException();

		public IAdornmentLayer GetAdornmentLayer(string name)
		{
			throw new NotImplementedException();
		}

		public ISpaceReservationManager GetSpaceReservationManager(string name)
		{
			throw new NotImplementedException();
		}

		Microsoft.VisualStudio.Text.Formatting.IWpfTextViewLine IWpfTextView.GetTextViewLineContainingBufferPosition(SnapshotPoint bufferPosition)
		{
			throw new NotImplementedException();
		}

		public Microsoft.VisualStudio.Text.Formatting.ILineTransformSource LineTransformSource => throw new NotImplementedException();

		IWpfTextViewLineCollection IWpfTextView.TextViewLines => throw new NotImplementedException();

		public System.Windows.FrameworkElement VisualElement => throw new NotImplementedException();

		public double ZoomLevel
		{
			get => throw new NotImplementedException();
			set => throw new NotImplementedException();
		}

		public event EventHandler<ZoomLevelChangedEventArgs> ZoomLevelChanged
		{
			add { }
			remove { }
		}

		#endregion
	}
}

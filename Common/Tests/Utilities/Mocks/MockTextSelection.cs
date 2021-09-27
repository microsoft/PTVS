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
    public class MockTextSelection : ITextSelection
    {
        private VirtualSnapshotPoint _start, _end;
        private bool _isReversed, _isActive = true;
        private readonly ITextView _view;

        public MockTextSelection(ITextView view)
        {
            _view = view;
        }

        public bool ActivationTracksFocus
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public Microsoft.VisualStudio.Text.VirtualSnapshotPoint ActivePoint
        {
            get { throw new NotImplementedException(); }
        }

        public Microsoft.VisualStudio.Text.VirtualSnapshotPoint AnchorPoint
        {
            get { throw new NotImplementedException(); }
        }

        public void Clear()
        {
            _start = new VirtualSnapshotPoint();
            _end = new VirtualSnapshotPoint();
        }

        public Microsoft.VisualStudio.Text.VirtualSnapshotPoint End
        {
            get { return _end; }
        }

        public Microsoft.VisualStudio.Text.VirtualSnapshotSpan? GetSelectionOnTextViewLine(Microsoft.VisualStudio.Text.Formatting.ITextViewLine line)
        {
            throw new NotImplementedException();
        }

        public bool IsActive
        {
            get
            {
                return _isActive;
            }
            set
            {
                _isActive = true;
            }
        }

        public bool IsEmpty
        {
            get { return _start == _end; }
        }

        public bool IsReversed
        {
            get { return _isReversed; }
        }

        public TextSelectionMode Mode
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public void Select(VirtualSnapshotPoint anchorPoint, VirtualSnapshotPoint activePoint)
        {
            throw new NotImplementedException();
        }

        public void Select(SnapshotSpan selectionSpan, bool isReversed)
        {
            _start = new VirtualSnapshotPoint(selectionSpan.Start);
            _end = new VirtualSnapshotPoint(selectionSpan.End);
            _isReversed = isReversed;
            _isActive = true;

            if (_isReversed)
            {
                ((MockTextCaret)_view.Caret).SetPosition(_end.Position);
            }
            else
            {
                ((MockTextCaret)_view.Caret).SetPosition(_start.Position);
            }
        }

        public NormalizedSnapshotSpanCollection SelectedSpans
        {
            get { throw new NotImplementedException(); }
        }

        public event EventHandler SelectionChanged
        {
            add
            {
            }
            remove
            {
            }
        }

        public VirtualSnapshotPoint Start
        {
            get { return _start; }
        }

        public Microsoft.VisualStudio.Text.VirtualSnapshotSpan StreamSelectionSpan
        {
            get
            {
                return new VirtualSnapshotSpan(_start, _end);
            }
        }

        public ITextView TextView
        {
            get { return _view; }
        }

        public System.Collections.ObjectModel.ReadOnlyCollection<Microsoft.VisualStudio.Text.VirtualSnapshotSpan> VirtualSelectedSpans
        {
            get { throw new NotImplementedException(); }
        }
    }
}

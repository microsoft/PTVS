// Python Tools for Visual Studio
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

namespace Microsoft.PythonTools.Repl
{
    internal class ResizingAdorner : Adorner
    {
        private readonly VisualCollection _visualChildren;
        private readonly FrameworkElement _bottomRight;

        private Point _mouseDownPoint;
        private readonly Size _originalSize;
        private Size _initialSize;
        private Size _desiredSize;

        public ResizingAdorner(UIElement adornedElement, Size originalSize)
            : base(adornedElement)
        {
            _visualChildren = new VisualCollection(this);
            _originalSize = _desiredSize = originalSize;
            Width = Height = double.NaN;
            _bottomRight = BuildAdornerCorner(Cursors.SizeNWSE);
        }

        private FrameworkElement BuildAdornerCorner(Cursor cursor)
        {
            var thumb = new Border();
            // TODO: this thumb should be styled to look like a dotted triangle, 
            // similar to the one you can see on the bottom right corner of 
            // Internet Explorer window
            thumb.Cursor = cursor;
            thumb.Height = thumb.Width = 10;
            thumb.Opacity = 0.40;
            thumb.Background = new SolidColorBrush(Colors.MediumBlue);
            thumb.IsHitTestVisible = true;
            thumb.MouseDown += Thumb_MouseDown;
            thumb.MouseMove += Thumb_MouseMove;
            thumb.MouseUp += Thumb_MouseUp;
            thumb.HorizontalAlignment = HorizontalAlignment.Right;
            thumb.VerticalAlignment = VerticalAlignment.Bottom;
            _visualChildren.Add(thumb);
            return thumb;
        }

        private void Thumb_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            _initialSize = _desiredSize;
            _mouseDownPoint = e.GetPosition(AdornedElement);
            _bottomRight.CaptureMouse();

            var evt = ResizeStarted;
            if (evt != null)
            {
                evt(this, e);
            }

            e.Handled = true;
        }

        private void Thumb_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_bottomRight.IsMouseCaptured)
            {
                return;
            }

            var pt = e.GetPosition(AdornedElement);
            var newWidth = _initialSize.Width + pt.X - _mouseDownPoint.X;
            //var newHeight = _initialSize.Height + pt.Y - _initialPoint.Y;
            var newHeight = _initialSize.Height / _initialSize.Width * newWidth;

            var desiredSize = new Size(
                Math.Max(newWidth, 0),
                Math.Max(newHeight, 0)
            );

            if (_desiredSize != desiredSize)
            {
                _desiredSize = desiredSize;
                var evt = DesiredSizeChanged;
                if (evt != null)
                {
                    evt(this, new DesiredSizeChangedEventArgs(desiredSize));
                }
            }

            e.Handled = true;
        }

        private void Thumb_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            _initialSize = _desiredSize;
            _bottomRight.ReleaseMouseCapture();

            var evt = ResizeCompleted;
            if (evt != null)
            {
                evt(this, e);
            }

            e.Handled = true;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            if (_desiredSize.Width < 0)
            {
                _desiredSize.Width = 0;
            }
            if (_desiredSize.Height < 0)
            {
                _desiredSize.Height = 0;
            }

            var size = new Size(
                Math.Min(_desiredSize.Width, constraint.Width),
                Math.Min(_desiredSize.Height, constraint.Height)
            );
            _bottomRight.Measure(size);
            return size;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _bottomRight.Arrange(new Rect(new Point(), finalSize));
            return finalSize;
        }

        protected override int VisualChildrenCount
        {
            get { return _visualChildren.Count; }
        }

        protected override Visual GetVisualChild(int index)
        {
            return _visualChildren[index];
        }

        public event RoutedEventHandler ResizeStarted;
        public event RoutedEventHandler ResizeCompleted;
        public event EventHandler<DesiredSizeChangedEventArgs> DesiredSizeChanged;
    }

    class DesiredSizeChangedEventArgs : EventArgs
    {
        private readonly Size _size;

        public DesiredSizeChangedEventArgs(Size size)
        {
            _size = size;
        }

        public Size Size
        {
            get
            {
                return _size;
            }
        }
    }
}

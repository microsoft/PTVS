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
    internal class ZoomableInlineAdornment : Grid
    {
        private readonly ITextView _parent;
        private ResizingAdorner _adorner;
        private readonly Size _originalSize;
        private Size _desiredSize;

        public ZoomableInlineAdornment(UIElement content, ITextView parent, Size desiredSize)
        {
            _parent = parent;
            Debug.Assert(parent is IInputElement);
            _originalSize = _desiredSize = new Size(
                Math.Max(double.IsNaN(desiredSize.Width) ? 100 : desiredSize.Width, 10),
                Math.Max(double.IsNaN(desiredSize.Height) ? 100 : desiredSize.Height, 10)
            );

            // First time through, we want to reduce the image to fit within the
            // viewport.
            if (_desiredSize.Width > parent.ViewportWidth)
            {
                _desiredSize.Width = parent.ViewportWidth;
                _desiredSize.Height = _originalSize.Height / _originalSize.Width * _desiredSize.Width;
            }
            if (_desiredSize.Height > parent.ViewportHeight)
            {
                _desiredSize.Height = parent.ViewportHeight;
                _desiredSize.Width = _originalSize.Width / _originalSize.Height * _desiredSize.Height;
            }

            ContextMenu = MakeContextMenu();

            Focusable = true;
            MinWidth = MinHeight = 50;

            Children.Add(content);

            GotFocus += OnGotFocus;
            LostFocus += OnLostFocus;
        }

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonDown(e);

            Focus();
            e.Handled = true;
        }

        protected override void OnPreviewMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseRightButtonUp(e);

            ContextMenu.IsOpen = true;
            e.Handled = true;
        }

        private ContextMenu MakeContextMenu()
        {
            var result = new ContextMenu();
            AddMenuItem(result, "Copy", "Ctrl+C", (s, e) => OnCopy());
            result.Items.Add(new Separator());
            AddMenuItem(result, "Zoom In", "Ctrl+OemPlus", (s, e) => OnZoomIn());
            AddMenuItem(result, "Zoom Out", "Ctrl+OemMinus", (s, e) => OnZoomOut());
            result.Items.Add(new Separator());
            AddMenuItem(result, "150%", null, (s, e) => Zoom(1.5));
            AddMenuItem(result, "100%", null, (s, e) => Zoom(1.0));
            AddMenuItem(result, "75%", null, (s, e) => Zoom(0.75));
            AddMenuItem(result, "50%", null, (s, e) => Zoom(0.50));
            AddMenuItem(result, "25%", null, (s, e) => Zoom(0.25));
            return result;
        }

        private static void AddMenuItem(ContextMenu menu, string text, string shortcut, RoutedEventHandler handler)
        {
            var item = new MenuItem();
            item.Header = text;
            item.Click += handler;
            menu.Items.Add(item);
        }

        private void OnGotFocus(object sender, RoutedEventArgs args)
        {
            _adorner = new ResizingAdorner(this, _desiredSize);
            _adorner.DesiredSizeChanged += OnDesiredSizeChanged;

            var adornerLayer = AdornerLayer.GetAdornerLayer(this);
            if (adornerLayer != null)
            {
                adornerLayer.Add(_adorner);
            }
        }

        private void OnLostFocus(object sender, RoutedEventArgs args)
        {
            if (_adorner == null)
            {
                Debug.Fail("Lost focus without creating an adorner");
                return;
            }

            _adorner.DesiredSizeChanged -= OnDesiredSizeChanged;

            var adornerLayer = AdornerLayer.GetAdornerLayer(this);
            if (adornerLayer != null)
            {
                adornerLayer.Remove(_adorner);
                _adorner = null;
            }
        }

        protected override Size MeasureOverride(Size constraint)
        {
            if (_desiredSize.Width < MinWidth)
            {
                _desiredSize.Width = MinWidth;
            }
            if (_desiredSize.Height < MinHeight)
            {
                _desiredSize.Height = MinHeight;
            }

            var size = new Size(
                Math.Min(_desiredSize.Width, constraint.Width),
                Math.Min(_desiredSize.Height, constraint.Height)
            );
            foreach (UIElement c in Children)
            {
                c.Measure(size);
            }
            return size;
        }

        private void OnDesiredSizeChanged(object sender, DesiredSizeChangedEventArgs e)
        {
            _desiredSize = e.Size;
            InvalidateMeasure();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs args)
        {
            var modifiers = args.KeyboardDevice.Modifiers & ModifierKeys.Control;
            if (modifiers == ModifierKeys.Control && (args.Key == Key.OemPlus || args.Key == Key.Add))
            {
                OnZoomIn();
                args.Handled = true;
            }
            else if (modifiers == ModifierKeys.Control && (args.Key == Key.OemMinus || args.Key == Key.Subtract))
            {
                OnZoomOut();
                args.Handled = true;
            }

            base.OnPreviewKeyDown(args);
        }

        private void OnCopy()
        {
            double width = ActualWidth;
            double height = ActualHeight;
            RenderTargetBitmap bmpCopied = new RenderTargetBitmap(
                (int)Math.Round(width),
                (int)Math.Round(height),
                96,
                96,
                PixelFormats.Default
            );
            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(new Point(), new Size(width, height)));
                VisualBrush vb = new VisualBrush(this);
                dc.DrawRectangle(vb, null, new Rect(new Point(), new Size(width, height)));
            }
            bmpCopied.Render(dv);
            Clipboard.SetImage(bmpCopied);
        }

        internal void Zoom(double zoomFactor)
        {
            _desiredSize = new Size(
                Math.Max(_originalSize.Width * zoomFactor, 50),
                Math.Max(_originalSize.Height * zoomFactor, _originalSize.Height / _originalSize.Width * 50)
            );
            InvalidateMeasure();
        }

        private void OnZoomIn()
        {
            _desiredSize = new Size(_desiredSize.Width * 1.1, _desiredSize.Height * 1.1);
            InvalidateMeasure();
        }

        private void OnZoomOut()
        {
            _desiredSize = new Size(_desiredSize.Width * 0.9, _desiredSize.Height * 0.9);
            InvalidateMeasure();
        }
    }
}

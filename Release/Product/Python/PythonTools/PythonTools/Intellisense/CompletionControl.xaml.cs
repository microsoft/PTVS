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

using System.Windows;
using System.Windows.Controls;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// A wrapper around the default completion control.  This control simply adds a warning which informs
    /// the user if the intellisense database is not currently up-to-date.  We embed the normal completion
    /// control (wherever it comes from, we import it via MEF) into our control.
    /// 
    /// This control forwards calls form IIntellisenseCommandTarget and IMouseProcessor onto the inner
    /// control.
    /// </summary>
    public partial class CompletionControl : UserControl, IIntellisenseCommandTarget, IMouseProcessor {
        private readonly UIElement _view;
        
        public CompletionControl(UIElement view, ICompletionSession session) {
            InitializeComponent();

            _view = view;
            _warning.Foreground = SystemColors.HotTrackBrush;
            _warning.Text = Microsoft.PythonTools.Resources.WarningAnalysisNotCurrent;
            
            if (session.TextView.GetAnalyzer().InterpreterFactory.IsAnalysisCurrent()) {
                _warning.Visibility = System.Windows.Visibility.Collapsed;
            }

            view.SetValue(Grid.RowProperty, 0);
            view.SetValue(Grid.ColumnProperty, 0);
            _grid.Children.Add(view);

            var content = view as ContentControl;
            if (content != null) {
                content.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
                content.Width = Width;
                content.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch;
            }

            session.Dismissed += SessionDismissed;
        }

        void SessionDismissed(object sender, System.EventArgs e) {
            _grid.Children.Remove(_view);
        }

        #region IIntellisenseCommandTarget Members

        public bool ExecuteKeyboardCommand(IntellisenseKeyboardCommand command) {
            IIntellisenseCommandTarget target = _view as IIntellisenseCommandTarget;
            if (target != null) {
                return target.ExecuteKeyboardCommand(command);
            }
            return false;
        }

        #endregion

        #region IMouseProcessor Members

        public void PostprocessDragEnter(DragEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessDragEnter(e);
            }
        }

        public void PostprocessDragLeave(DragEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessDragLeave(e);
            }
        }

        public void PostprocessDragOver(DragEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessDragOver(e);
            }
        }

        public void PostprocessDrop(DragEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessDrop(e);
            }
        }

        public void PostprocessGiveFeedback(GiveFeedbackEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessGiveFeedback(e);
            }
        }

        public void PostprocessMouseDown(System.Windows.Input.MouseButtonEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessMouseDown(e);
            }
        }

        public void PostprocessMouseEnter(System.Windows.Input.MouseEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessMouseEnter(e);
            }
        }

        public void PostprocessMouseLeave(System.Windows.Input.MouseEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessMouseLeave(e);
            }
        }

        public void PostprocessMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessMouseLeftButtonDown(e);
            }
        }

        public void PostprocessMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessMouseLeftButtonUp(e);
            }
        }

        public void PostprocessMouseMove(System.Windows.Input.MouseEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessMouseMove(e);
            }
        }

        public void PostprocessMouseRightButtonDown(System.Windows.Input.MouseButtonEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessMouseRightButtonDown(e);
            }
        }

        public void PostprocessMouseRightButtonUp(System.Windows.Input.MouseButtonEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessMouseRightButtonUp(e);
            }
        }

        public void PostprocessMouseUp(System.Windows.Input.MouseButtonEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessMouseUp(e);
            }
        }

        public void PostprocessMouseWheel(System.Windows.Input.MouseWheelEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessMouseWheel(e);
            }
        }

        public void PostprocessQueryContinueDrag(QueryContinueDragEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessQueryContinueDrag(e);
            }
        }

        public void PreprocessDragEnter(DragEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessDragEnter(e);
            }
        }

        public void PreprocessDragLeave(DragEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessDragLeave(e);
            }
        }

        public void PreprocessDragOver(DragEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessDragOver(e);
            }
        }

        public void PreprocessDrop(DragEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessDrop(e);
            }
        }

        public void PreprocessGiveFeedback(GiveFeedbackEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessGiveFeedback(e);
            }
        }

        public void PreprocessMouseDown(System.Windows.Input.MouseButtonEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessMouseDown(e);
            }
        }

        public void PreprocessMouseEnter(System.Windows.Input.MouseEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessMouseEnter(e);
            }
        }

        public void PreprocessMouseLeave(System.Windows.Input.MouseEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessMouseLeave(e);
            }
        }

        public void PreprocessMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessMouseLeftButtonDown(e);
            }
        }

        public void PreprocessMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessMouseLeftButtonUp(e);
            }
        }

        public void PreprocessMouseMove(System.Windows.Input.MouseEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessMouseMove(e);
            }
        }

        public void PreprocessMouseRightButtonDown(System.Windows.Input.MouseButtonEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessMouseRightButtonDown(e);
            }
        }

        public void PreprocessMouseRightButtonUp(System.Windows.Input.MouseButtonEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessMouseRightButtonUp(e);
            }
        }

        public void PreprocessMouseUp(System.Windows.Input.MouseButtonEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessMouseUp(e);
            }
        }

        public void PreprocessMouseWheel(System.Windows.Input.MouseWheelEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessMouseWheel(e);
            }
        }

        public void PreprocessQueryContinueDrag(QueryContinueDragEventArgs e) {
            IMouseProcessor processor = _view as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessQueryContinueDrag(e);
            }
        }

        #endregion
    }
}

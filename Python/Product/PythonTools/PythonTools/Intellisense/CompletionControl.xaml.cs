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

using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudioTools;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// A wrapper around the default completion control.  This control simply adds a warning which informs
    /// the user if the intellisense database is not currently up-to-date.  We embed the normal completion
    /// control (wherever it comes from, we import it via MEF) into our control.
    /// 
    /// This control forwards calls from IIntellisenseCommandTarget and IMouseProcessor onto the inner
    /// control.
    /// </summary>
    public partial class CompletionControl : ContentControl, IIntellisenseCommandTarget, IMouseProcessor, IDisposable {
        public static readonly object HotTrackBrushKey = VsBrushes.ToolWindowTabMouseOverTextKey;
        private static readonly DependencyPropertyKey WarningVisibilityPropertyKey = DependencyProperty.RegisterReadOnly("WarningVisibility", typeof(Visibility), typeof(CompletionControl), new PropertyMetadata(Visibility.Visible));
        public static readonly DependencyProperty WarningVisibilityProperty = WarningVisibilityPropertyKey.DependencyProperty;
        private readonly IServiceProvider _serviceProvider;

        [Obsolete("Use version which takes IServiceProvider")]
        public CompletionControl(UIElement view, ICompletionSession session)
            : this(PythonToolsPackage.Instance, view, session) {
        }

        public CompletionControl(IServiceProvider serviceProvider, UIElement view, ICompletionSession session) {
            _serviceProvider = serviceProvider;

            InitializeComponent();

            Content = view;

            var fact = session.TextView.GetAnalyzer(serviceProvider).InterpreterFactory as IPythonInterpreterFactoryWithDatabase;
            if (fact == null) {
                SetValue(WarningVisibilityPropertyKey, Visibility.Collapsed);
            } else {
                SetValue(WarningVisibilityPropertyKey, fact.IsCurrent ? Visibility.Collapsed : Visibility.Visible);
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e) {
            // force the inner presenter to not re-size to a smaller size.  The inner presenter
            // will then force the tab controls to not re-size keeping the completion list 
            // to a fixed size.
            // http://pytools.codeplex.com/workitem/554
            var content = Content as ContentControl;
            if (content != null && (content.Width < ActualWidth || double.IsNaN(content.Width))) {
                content.Width = content.MinWidth = ActualWidth;
            }
        }

        private async void Button_GotFocus(object sender, RoutedEventArgs e) {
            // HACK: Handling GotFocus because this is the only event we get
            // when clicking on the button - the VS text editor somehow absorbs
            // all the rest...

            // Short delay to ensure the completion control has lost focus,
            // otherwise the environment list will be hidden immediately.
            await Task.Delay(50);
            // Should already be on the UI thread, but we'll invoke to be safe
            _serviceProvider.GetUIThread().Invoke(() => {
                _serviceProvider.ShowInterpreterList();
            });
        }

        #region IIntellisenseCommandTarget Members

        public bool ExecuteKeyboardCommand(IntellisenseKeyboardCommand command) {
            IIntellisenseCommandTarget target = Content as IIntellisenseCommandTarget;
            if (target != null) {
                return target.ExecuteKeyboardCommand(command);
            }
            return false;
        }

        #endregion

        #region IMouseProcessor Members

        public void PostprocessDragEnter(DragEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessDragEnter(e);
            }
        }

        public void PostprocessDragLeave(DragEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessDragLeave(e);
            }
        }

        public void PostprocessDragOver(DragEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessDragOver(e);
            }
        }

        public void PostprocessDrop(DragEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessDrop(e);
            }
        }

        public void PostprocessGiveFeedback(GiveFeedbackEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessGiveFeedback(e);
            }
        }

        public void PostprocessMouseDown(System.Windows.Input.MouseButtonEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessMouseDown(e);
            }
        }

        public void PostprocessMouseEnter(System.Windows.Input.MouseEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessMouseEnter(e);
            }
        }

        public void PostprocessMouseLeave(System.Windows.Input.MouseEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessMouseLeave(e);
            }
        }

        public void PostprocessMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessMouseLeftButtonDown(e);
            }
        }

        public void PostprocessMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessMouseLeftButtonUp(e);
            }
        }

        public void PostprocessMouseMove(System.Windows.Input.MouseEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessMouseMove(e);
            }
        }

        public void PostprocessMouseRightButtonDown(System.Windows.Input.MouseButtonEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessMouseRightButtonDown(e);
            }
        }

        public void PostprocessMouseRightButtonUp(System.Windows.Input.MouseButtonEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessMouseRightButtonUp(e);
            }
        }

        public void PostprocessMouseUp(System.Windows.Input.MouseButtonEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessMouseUp(e);
            }
        }

        public void PostprocessMouseWheel(System.Windows.Input.MouseWheelEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessMouseWheel(e);
            }
        }

        public void PostprocessQueryContinueDrag(QueryContinueDragEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PostprocessQueryContinueDrag(e);
            }
        }

        public void PreprocessDragEnter(DragEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessDragEnter(e);
            }
        }

        public void PreprocessDragLeave(DragEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessDragLeave(e);
            }
        }

        public void PreprocessDragOver(DragEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessDragOver(e);
            }
        }

        public void PreprocessDrop(DragEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessDrop(e);
            }
        }

        public void PreprocessGiveFeedback(GiveFeedbackEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessGiveFeedback(e);
            }
        }

        public void PreprocessMouseDown(System.Windows.Input.MouseButtonEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessMouseDown(e);
            }
        }

        public void PreprocessMouseEnter(System.Windows.Input.MouseEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessMouseEnter(e);
            }
        }

        public void PreprocessMouseLeave(System.Windows.Input.MouseEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessMouseLeave(e);
            }
        }

        public void PreprocessMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessMouseLeftButtonDown(e);
            }
        }

        public void PreprocessMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessMouseLeftButtonUp(e);
            }
        }

        public void PreprocessMouseMove(System.Windows.Input.MouseEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessMouseMove(e);
            }
        }

        public void PreprocessMouseRightButtonDown(System.Windows.Input.MouseButtonEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessMouseRightButtonDown(e);
            }
        }

        public void PreprocessMouseRightButtonUp(System.Windows.Input.MouseButtonEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessMouseRightButtonUp(e);
            }
        }

        public void PreprocessMouseUp(System.Windows.Input.MouseButtonEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessMouseUp(e);
            }
        }

        public void PreprocessMouseWheel(System.Windows.Input.MouseWheelEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessMouseWheel(e);
            }
        }

        public void PreprocessQueryContinueDrag(QueryContinueDragEventArgs e) {
            IMouseProcessor processor = Content as IMouseProcessor;
            if (processor != null) {
                processor.PreprocessQueryContinueDrag(e);
            }
        }

        #endregion

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                Content = null;
            }
        }
    }
}

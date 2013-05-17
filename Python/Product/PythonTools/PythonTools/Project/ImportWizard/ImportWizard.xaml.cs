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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Project.ImportWizard {
    /// <summary>
    /// Interaction logic for ImportWizard.xaml
    /// </summary>
    internal partial class ImportWizard : DialogWindowVersioningWorkaround {
        public static readonly RoutedCommand BrowseFolderCommand = new RoutedCommand();
        public static readonly RoutedCommand BrowseOpenFileCommand = new RoutedCommand();
        public static readonly RoutedCommand BrowseSaveFileCommand = new RoutedCommand();



        public ImportSettings ImportSettings {
            get { return (ImportSettings)GetValue(ImportSettingsProperty); }
            private set { SetValue(ImportSettingsPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey ImportSettingsPropertyKey = DependencyProperty.RegisterReadOnly("ImportSettings", typeof(ImportSettings), typeof(ImportWizard), new PropertyMetadata());
        public static readonly DependencyProperty ImportSettingsProperty = ImportSettingsPropertyKey.DependencyProperty;

        public int PageCount {
            get { return (int)GetValue(PageCountProperty); }
            private set { SetValue(PageCountPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey PageCountPropertyKey = DependencyProperty.RegisterReadOnly("PageCount", typeof(int), typeof(ImportWizard), new PropertyMetadata(0));
        public static readonly DependencyProperty PageCountProperty = PageCountPropertyKey.DependencyProperty;

        private CollectionViewSource _pageSequence;

        public ICollectionView PageSequence {
            get { return (ICollectionView)GetValue(PageSequenceProperty); }
            private set { SetValue(PageSequencePropertyKey, value); }
        }

        private static readonly DependencyPropertyKey PageSequencePropertyKey = DependencyProperty.RegisterReadOnly("PageSequence", typeof(ICollectionView), typeof(ImportWizard), new PropertyMetadata());
        public static readonly DependencyProperty PageSequenceProperty = PageSequencePropertyKey.DependencyProperty;

        public ImportWizard() {
            ImportSettings = new ImportSettings();

            _pageSequence = new CollectionViewSource {
                Source = new ObservableCollection<Page>(new Page[] {
                    new FileSourcePage { DataContext = ImportSettings },
                    new InterpreterPage { DataContext = ImportSettings },
                    new SaveProjectPage { DataContext = ImportSettings }
                })
            };
            PageCount = _pageSequence.View.OfType<object>().Count();

            PageSequence = _pageSequence.View;
            PageSequence.MoveCurrentToFirst();

            DataContext = this;

            InitializeComponent();
        }

        private void Browse_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = e.OriginalSource is TextBox;
        }

        private void BrowseFolder_Executed(object sender, ExecutedRoutedEventArgs e) {
            var tb = (TextBox)e.OriginalSource;

            if (!tb.AcceptsReturn) {
                var path = tb.GetValue(TextBox.TextProperty) as string;
                path = PythonToolsPackage.Instance.BrowseForDirectory(new System.Windows.Interop.WindowInteropHelper(this).Handle, path);
                if (path != null) {
                    tb.SetValue(TextBox.TextProperty, path);
                }
            } else {
                var existing = tb.GetValue(TextBox.TextProperty) as string;
                var path = e.Parameter as string;
                path = PythonToolsPackage.Instance.BrowseForDirectory(new System.Windows.Interop.WindowInteropHelper(this).Handle, path);
                if (path != null) {
                    if (string.IsNullOrEmpty(existing)) {
                        tb.SetValue(TextBox.TextProperty, path);
                    } else {
                        tb.SetValue(TextBox.TextProperty, existing.TrimEnd(new[] { '\r', '\n' }) + Environment.NewLine + path);
                    }
                }
            }
        }

        private void BrowseOpenFile_Executed(object sender, ExecutedRoutedEventArgs e) {
            var tb = (TextBox)e.OriginalSource;
            var filter = (e.Parameter as string) ?? "All Files (*.*)|*.*";

            var path = tb.GetValue(TextBox.TextProperty) as string;
            path = PythonToolsPackage.Instance.BrowseForFileOpen(new System.Windows.Interop.WindowInteropHelper(this).Handle, filter, path);
            if (path != null) {
                tb.SetValue(TextBox.TextProperty, path);
            }
        }

        private void BrowseSaveFile_Executed(object sender, ExecutedRoutedEventArgs e) {
            var tb = (TextBox)e.OriginalSource;
            var filter = (e.Parameter as string) ?? "All Files (*.*)|*.*";

            var path = tb.GetValue(TextBox.TextProperty) as string;
            path = PythonToolsPackage.Instance.BrowseForFileSave(new System.Windows.Interop.WindowInteropHelper(this).Handle, filter, path);
            if (path != null) {
                tb.SetValue(TextBox.TextProperty, path);
            }
        }

        private void Finish_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = ImportSettings != null && ImportSettings.IsValid;
        }

        private void Finish_Executed(object sender, ExecutedRoutedEventArgs e) {
            DialogResult = true;
            Close();
        }

        private void Cancel_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
        }

        private void Cancel_Executed(object sender, ExecutedRoutedEventArgs e) {
            DialogResult = false;
            Close();
        }

        private void Back_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = PageSequence != null && PageSequence.CurrentPosition > 0;
        }

        private void Back_Executed(object sender, ExecutedRoutedEventArgs e) {
            PageSequence.MoveCurrentToPrevious();
        }

        private void Next_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = PageSequence != null && PageSequence.CurrentPosition < PageCount - 1;
        }

        private void Next_Executed(object sender, ExecutedRoutedEventArgs e) {
            PageSequence.MoveCurrentToNext();
        }
    }
}

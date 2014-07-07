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
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudioTools;
using WpfCommands = Microsoft.VisualStudioTools.Wpf.Commands;

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

        private static readonly DependencyPropertyKey IsNextDefaultPropertyKey = DependencyProperty.RegisterReadOnly("IsNextDefault", typeof(bool), typeof(ImportWizard), new PropertyMetadata(true));
        public static readonly DependencyProperty IsNextDefaultProperty = IsNextDefaultPropertyKey.DependencyProperty;
        private static readonly DependencyPropertyKey IsFinishDefaultPropertyKey = DependencyProperty.RegisterReadOnly("IsFinishDefault", typeof(bool), typeof(ImportWizard), new PropertyMetadata(false));
        public static readonly DependencyProperty IsFinishDefaultProperty = IsFinishDefaultPropertyKey.DependencyProperty;

        private CollectionViewSource _pageSequence;

        public ICollectionView PageSequence {
            get { return (ICollectionView)GetValue(PageSequenceProperty); }
            private set { SetValue(PageSequencePropertyKey, value); }
        }

        private static readonly DependencyPropertyKey PageSequencePropertyKey = DependencyProperty.RegisterReadOnly("PageSequence", typeof(ICollectionView), typeof(ImportWizard), new PropertyMetadata());
        public static readonly DependencyProperty PageSequenceProperty = PageSequencePropertyKey.DependencyProperty;

        public ImportWizard() {
            InitializeComponent();
        }

        public ImportWizard(string sourcePath, string projectPath) {
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
            PageSequence.CurrentChanged += PageSequence_CurrentChanged;
            PageSequence.MoveCurrentToFirst();

            if (!string.IsNullOrEmpty(sourcePath)) {
                ImportSettings.SourcePath = sourcePath;
            }
            if (!string.IsNullOrEmpty(projectPath)) {
                ImportSettings.ProjectPath = projectPath;
            }
            ImportSettings.UpdateIsValid();

            DataContext = this;

            InitializeComponent();
        }

        private void PageSequence_CurrentChanged(object sender, EventArgs e) {
            SetValue(IsNextDefaultPropertyKey, PageSequence.CurrentPosition < PageCount - 1);
            SetValue(IsFinishDefaultPropertyKey, PageSequence.CurrentPosition >= PageCount - 1);
        }

        private void Finish_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = ImportSettings != null && ImportSettings.IsValid;
        }

        private void Finish_Executed(object sender, ExecutedRoutedEventArgs e) {
            if (ImportSettings.ProjectFileExists) {
                if (MessageBoxResult.Cancel == MessageBox.Show(
                    SR.GetString(SR.ImportWizardProjectExists),
                    SR.ProductName,
                    MessageBoxButton.OKCancel
                )) {
                    return;
                }
            }
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

        private void Browse_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            WpfCommands.CanExecute(this, sender, e);
        }

        private void Browse_Executed(object sender, ExecutedRoutedEventArgs e) {
            WpfCommands.Executed(this, sender, e);
        }
    }
}

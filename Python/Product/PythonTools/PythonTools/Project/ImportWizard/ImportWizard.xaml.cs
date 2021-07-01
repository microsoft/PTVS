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

using Microsoft.VisualStudioTools;
using WpfCommands = Microsoft.VisualStudioTools.Wpf.Commands;

namespace Microsoft.PythonTools.Project.ImportWizard
{
    /// <summary>
    /// Interaction logic for ImportWizard.xaml
    /// </summary>
    internal partial class ImportWizard : DialogWindowVersioningWorkaround
    {
        public static readonly RoutedCommand BrowseFolderCommand = new RoutedCommand();
        public static readonly RoutedCommand BrowseOpenFileCommand = new RoutedCommand();
        public static readonly RoutedCommand BrowseSaveFileCommand = new RoutedCommand();

        private readonly IServiceProvider _site;


        public ImportSettings ImportSettings
        {
            get { return (ImportSettings)GetValue(ImportSettingsProperty); }
            private set { SetValue(ImportSettingsPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey ImportSettingsPropertyKey = DependencyProperty.RegisterReadOnly("ImportSettings", typeof(ImportSettings), typeof(ImportWizard), new PropertyMetadata());
        public static readonly DependencyProperty ImportSettingsProperty = ImportSettingsPropertyKey.DependencyProperty;

        public int PageCount
        {
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

        public ICollectionView PageSequence
        {
            get { return (ICollectionView)GetValue(PageSequenceProperty); }
            private set { SetValue(PageSequencePropertyKey, value); }
        }

        private static readonly DependencyPropertyKey PageSequencePropertyKey = DependencyProperty.RegisterReadOnly("PageSequence", typeof(ICollectionView), typeof(ImportWizard), new PropertyMetadata());
        public static readonly DependencyProperty PageSequenceProperty = PageSequencePropertyKey.DependencyProperty;

        public ImportWizard()
        {
            InitializeComponent();
        }

        public ImportWizard(IServiceProvider serviceProvider, string sourcePath, string projectPath)
        {
            var interpreterService = serviceProvider.GetComponentModel().GetService<IInterpreterRegistryService>();
            _site = serviceProvider;
            ImportSettings = new ImportSettings(serviceProvider, interpreterService);

            _pageSequence = new CollectionViewSource
            {
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

            if (!string.IsNullOrEmpty(sourcePath))
            {
                ImportSettings.SetInitialSourcePath(sourcePath);
                Loaded += ImportWizard_Loaded;
            }
            if (!string.IsNullOrEmpty(projectPath))
            {
                ImportSettings.SetInitialProjectPath(projectPath);
            }
            ImportSettings.UpdateIsValid();

            DataContext = this;

            InitializeComponent();
        }

        async void ImportWizard_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ImportWizard_Loaded;
            await ImportSettings.UpdateSourcePathAsync().HandleAllExceptions(_site);
        }

        private void PageSequence_CurrentChanged(object sender, EventArgs e)
        {
            SetValue(IsNextDefaultPropertyKey, PageSequence.CurrentPosition < PageCount - 1);
            SetValue(IsFinishDefaultPropertyKey, PageSequence.CurrentPosition >= PageCount - 1);
        }

        private void Finish_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = ImportSettings != null && ImportSettings.IsValid;
        }

        private void Finish_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (ImportSettings.ProjectFileExists)
            {
                if (MessageBoxResult.Cancel == MessageBox.Show(
                    Strings.ImportWizardProjectExists,
                    Strings.ProductTitle,
                    MessageBoxButton.OKCancel
                ))
                {
                    return;
                }
            }
            DialogResult = true;
            Close();
        }

        private void Cancel_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void Cancel_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Back_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = PageSequence != null && PageSequence.CurrentPosition > 0;
        }

        private void Back_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            PageSequence.MoveCurrentToPrevious();
        }

        private void Next_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = PageSequence != null && PageSequence.CurrentPosition < PageCount - 1;
        }

        private void Next_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            PageSequence.MoveCurrentToNext();
        }

        private void Browse_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            WpfCommands.CanExecute(this, sender, e);
        }

        private void Browse_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            WpfCommands.Executed(this, sender, e);
        }
    }
}

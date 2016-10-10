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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.CookiecutterTools.Infrastructure;
using Microsoft.CookiecutterTools.ViewModel;

namespace Microsoft.CookiecutterTools.View {
    /// <summary>
    /// Interaction logic for CookiecutterSearchPage.xaml
    /// </summary>
    internal partial class CookiecutterSearchPage : Page {
        public CookiecutterSearchPage() {
            InitializeComponent();
        }

        public event EventHandler<EventArgs> SelectedTemplateChanged;

        private CookiecutterViewModel ViewModel => (CookiecutterViewModel)DataContext;

        private void TextBox_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                ViewModel.SearchAsync().DoNotWait();
            } else if (e.Key == Key.Escape) {
                ViewModel.SearchTerm = string.Empty;
                ViewModel.SearchAsync().DoNotWait();
            }
        }

        private void Search_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void Search_Executed(object sender, ExecutedRoutedEventArgs e) {
            ViewModel.SearchAsync().DoNotWait();
        }

        private void OpenInBrowser_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var url = (string)e.Parameter;
            e.CanExecute = !string.IsNullOrEmpty(url);
            e.Handled = true;
        }

        private void OpenInBrowser_Executed(object sender, ExecutedRoutedEventArgs e) {
            var url = (string)e.Parameter;
            Process.Start(url)?.Dispose();
        }

        private void OpenInExplorer_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var folderPath = (string)e.Parameter;
            e.CanExecute = Directory.Exists(folderPath);
            e.Handled = true;
        }

        private void OpenInExplorer_Executed(object sender, ExecutedRoutedEventArgs e) {
            var folderPath = (string)e.Parameter;
            ViewModel.OpenFolderInExplorer(folderPath);
            Process.Start(folderPath)?.Dispose();
        }

        private void LoadMore_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void LoadMore_Executed(object sender, ExecutedRoutedEventArgs e) {
            var token = (string)e.Parameter;
            ViewModel.LoadMoreTemplates(token).DoNotWait();
        }

        private void RunSelection_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = ViewModel?.SelectedTemplate != null && !ViewModel.IsCloning && !ViewModel.IsLoading;
            e.Handled = true;
        }

        private void RunSelection_Executed(object sender, ExecutedRoutedEventArgs e) {
            LoadTemplate();
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
            var val = e.NewValue as TemplateViewModel;
            ViewModel.SelectTemplate(val).DoNotWait();

            SelectedTemplateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnItemMouseDoubleClick(object sender, MouseButtonEventArgs args) {
            var item = sender as TreeViewItem;
            if (item != null) {
                if (item.IsSelected) {
                    var template = item.DataContext as TemplateViewModel;
                    if (template != null) {
                        if (template != ViewModel.SelectedTemplate) {
                            ViewModel.SelectedTemplate = template;
                        }

                        LoadTemplate();
                    }
                }
            }
        }

        private void OnItemPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) {
            var item = sender as TreeViewItem;
            if (item != null) {
                var template = item.DataContext as TemplateViewModel;
                if (template != null) {
                    item.Focus();
                    e.Handled = true;
                }
            }
        }

        public void LoadTemplate() {
            TemplateViewModel collidingTemplate;
            if (ViewModel.IsCloneNeeded(ViewModel.SelectedTemplate) && ViewModel.IsCloneCollision(ViewModel.SelectedTemplate, out collidingTemplate)) {
                MessageBox.Show(string.Format(CultureInfo.CurrentUICulture, Strings.CloneCollisionMessage, ViewModel.SelectedTemplate.RepositoryName, collidingTemplate.ClonedPath), Strings.ProductTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ViewModel.LoadTemplateAsync().DoNotWait();
        }
    }
}

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

using Microsoft.CookiecutterTools.Infrastructure;
using Microsoft.CookiecutterTools.ViewModel;

namespace Microsoft.CookiecutterTools.View
{
    /// <summary>
    /// Interaction logic for CookiecutterSearchPage.xaml
    /// </summary>
    internal partial class CookiecutterSearchPage : Page
    {
        public CookiecutterSearchPage()
        {
            InitializeComponent();
        }

        public event EventHandler<EventArgs> SelectedTemplateChanged;

        private CookiecutterViewModel ViewModel => (CookiecutterViewModel)DataContext;

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ViewModel.SearchAsync().HandleAllExceptions(null, GetType()).DoNotWait();
            }
            else if (e.Key == Key.Escape)
            {
                ViewModel.SearchTerm = string.Empty;
                ViewModel.SearchAsync().HandleAllExceptions(null, GetType()).DoNotWait();
            }
        }

        private void Search_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void Search_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ViewModel.SearchAsync().HandleAllExceptions(null, GetType()).DoNotWait();
        }

        private void OpenInBrowser_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            var url = (string)e.Parameter;
            Uri uri;
            e.CanExecute = Uri.TryCreate(url, UriKind.Absolute, out uri) &&
                           (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            e.Handled = true;
        }

        private void OpenInBrowser_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var url = (string)e.Parameter;
            Process.Start(url)?.Dispose();
        }

        private void LoadMore_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void LoadMore_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var token = (string)e.Parameter;
            ViewModel.LoadMoreTemplatesAsync(token).HandleAllExceptions(null, GetType()).DoNotWait();
        }

        private void RunSelection_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = ViewModel?.CanLoadSelectedTemplate == true;
            e.Handled = true;
        }

        private void RunSelection_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            LoadTemplate();
        }

        private async void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var val = e.NewValue as TemplateViewModel;
            await ViewModel.SelectTemplateAsync(val);

            SelectedTemplateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnItemMouseDoubleClick(object sender, MouseButtonEventArgs args)
        {
            var item = sender as TreeViewItem;
            if (item != null && item.IsSelected)
            {
                var template = item.DataContext as TemplateViewModel;
                if (template != null)
                {
                    LoadTemplate(template);
                }
            }
        }

        private void OnItemPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = sender as TreeViewItem;
            if (item != null)
            {
                var template = item.DataContext as TemplateViewModel;
                if (template != null)
                {
                    item.Focus();
                    e.Handled = true;
                }
            }
        }

        private void OnItemPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var item = sender as TreeViewItem;
            if (item != null && item.IsSelected)
            {
                if (e.Key == Key.Enter)
                {
                    if (DoInvoke(item.DataContext))
                    {
                        e.Handled = true;
                    }
                }
            }
        }

        private void OnInvokeTemplate(object sender, InvokeEventArgs e)
        {
            DoInvoke(e.Item);
        }

        private bool DoInvoke(object item)
        {
            var continuation = item as ContinuationViewModel;
            if (continuation != null)
            {
                ViewModel.LoadMoreTemplatesAsync(continuation.ContinuationToken).HandleAllExceptions(null, GetType()).DoNotWait();
                return true;
            }
            else
            {
                var template = item as TemplateViewModel;
                if (template != null)
                {
                    LoadTemplate(template);
                    return true;
                }
            }
            return false;
        }

        private void LoadTemplate(TemplateViewModel template)
        {
            if (template != ViewModel.SelectedTemplate)
            {
                ViewModel.SelectedTemplate = template;
            }

            if (ViewModel.CanLoadSelectedTemplate)
            {
                LoadTemplate();
            }
        }

        public void LoadTemplate()
        {
            TemplateViewModel collidingTemplate;
            if (ViewModel.IsCloneNeeded(ViewModel.SelectedTemplate) && ViewModel.IsCloneCollision(ViewModel.SelectedTemplate, out collidingTemplate))
            {
                MessageBox.Show(Strings.CloneCollisionMessage.FormatUI(ViewModel.SelectedTemplate.RepositoryName, collidingTemplate.ClonedPath), Strings.ProductTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ViewModel.LoadTemplateAsync().HandleAllExceptions(null, GetType()).DoNotWait();
        }

        public void UpdateTemplate()
        {
            ViewModel.UpdateTemplateAsync().HandleAllExceptions(null, GetType()).DoNotWait();
        }

        internal void CheckForUpdates()
        {
            ViewModel.CheckForUpdatesAsync().HandleAllExceptions(null, GetType()).DoNotWait();
        }

        private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel.CanNavigateToOwner)
            {
                ViewModel.NavigateToOwner();
            }
        }
    }
}

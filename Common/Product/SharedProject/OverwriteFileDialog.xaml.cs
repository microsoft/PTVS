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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Windows;

namespace Microsoft.VisualStudioTools {
    /// <summary>
    /// Interaction logic for OverwriteFileDialog.xaml
    /// </summary>
    internal partial class OverwriteFileDialog : DialogWindowVersioningWorkaround {
        public bool ShouldOverwrite;

        public OverwriteFileDialog() {
            InitializeComponent();
        }

        public OverwriteFileDialog(string message, bool doForAllItems) {
            InitializeComponent();

            if (!doForAllItems) {
                _allItems.Visibility = Visibility.Hidden;
            }

            _message.Text = message;
        }

        
        private void YesClick(object sender, RoutedEventArgs e) {
            ShouldOverwrite = true;
            DialogResult = true;
            Close();
        }

        private void NoClick(object sender, RoutedEventArgs e) {
            ShouldOverwrite = false;
            DialogResult = true;
            Close();
        }

        private void CancelClick(object sender, RoutedEventArgs e) {
            Close();
        }

        public bool AllItems {
            get {
                return _allItems.IsChecked.Value;
            }
        }
    }
}

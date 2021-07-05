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

namespace Microsoft.PythonTools.Environments {
    public partial class AddCondaEnvironmentControl : UserControl {
        public static readonly ICommand AddPackageNames = new RoutedCommand();
        public static readonly ICommand BrowsePackages = new RoutedCommand();

        public AddCondaEnvironmentControl() {
            InitializeComponent();
        }


        private void AddPackageNames_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void AddPackageNames_Executed(object sender, ExecutedRoutedEventArgs e) {
            var packagesToAppend = (string)e.Parameter;
            var condaView = (AddCondaEnvironmentView)DataContext;
            var packages = condaView.Packages ?? string.Empty;
            if (packages.Length > 0 && !packages.EndsWithOrdinal(" ")) {
                packages += " ";
            }
            packages += packagesToAppend;
            condaView.Packages = packages;
        }

        private void BrowsePackages_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void BrowsePackages_Executed(object sender, ExecutedRoutedEventArgs e) {
            ContextMenu cm = this.FindResource("SuggestedPackagesMenu") as ContextMenu;
            cm.PlacementTarget = sender as Button;
            cm.IsOpen = true;
        }

        private void SuggestedPackages_Click(object sender, RoutedEventArgs e) {
            ContextMenu cm = this.FindResource("SuggestedPackagesMenu") as ContextMenu;
            cm.PlacementTarget = sender as Button;
            cm.IsOpen = true;
        }

        private void Browse_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            Microsoft.VisualStudioTools.Wpf.Commands.CanExecute(null, sender, e);
        }

        private void Browse_Executed(object sender, ExecutedRoutedEventArgs e) {
            Microsoft.VisualStudioTools.Wpf.Commands.Executed(null, sender, e);
        }
    }
}

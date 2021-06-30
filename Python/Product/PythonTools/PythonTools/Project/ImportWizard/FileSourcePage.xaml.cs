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

using System.Diagnostics;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools.Project.ImportWizard {
    /// <summary>
    /// Interaction logic for FileSourcePage.xaml
    /// </summary>
    internal partial class FileSourcePage : Page {
        public FileSourcePage() {
            InitializeComponent();
        }

        private async void SourcePathTextBox_SourceUpdated(object sender, DataTransferEventArgs e) {
            Debug.Assert(DataContext is ImportSettings);
            var settings = (ImportSettings)DataContext;
            SourcePathDoesNotExist.Visibility =
                (string.IsNullOrEmpty(settings.SourcePath) || Directory.Exists(settings.SourcePath)) ?
                System.Windows.Visibility.Collapsed :
                System.Windows.Visibility.Visible;
            await settings.UpdateSourcePathAsync().HandleAllExceptions(null);
        }
    }
}

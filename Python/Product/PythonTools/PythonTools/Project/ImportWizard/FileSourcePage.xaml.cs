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

using System.Diagnostics;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.VisualStudioTools;

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
            await settings.UpdateSourcePathAsync().HandleAllExceptions(SR.ProductName);
        }
    }
}

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

using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Microsoft.PythonTools.EnvironmentsList {
    sealed class EnvironmentPathsExtensionProvider : IEnvironmentViewExtension {
        private EnvironmentPathsExtension _wpfObject;

        public int SortPriority {
            get { return -10; }
        }

        public string LocalizedDisplayName {
            get { return Resources.EnvironmentPathsExtensionDisplayName; }
        }

        public object HelpContent {
            get { return Resources.EnvironmentPathsExtensionHelpContent; }
        }

        public string HelpText {
            get { return Resources.EnvironmentPathsExtensionHelpContent; }
        }

        public FrameworkElement WpfObject {
            get {
                if (_wpfObject == null) {
                    _wpfObject = new EnvironmentPathsExtension();
                }
                return _wpfObject;
            }
        }

        public override string ToString() => this.LocalizedDisplayName;
    }

    public partial class EnvironmentPathsExtension : UserControl {
        public static readonly ICommand OpenInBrowser = new RoutedCommand();
        public static readonly ICommand OpenInFileExplorer = new RoutedCommand();
        public static readonly ICommand StartInterpreter = new RoutedCommand();
        public static readonly ICommand StartWindowsInterpreter = new RoutedCommand();
        public static readonly ICommand ConfigureEnvironment = new RoutedCommand();

        public EnvironmentPathsExtension() {
            InitializeComponent();
        }

        void OpenInFileExplorer_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var path = e.Parameter as string;
            e.CanExecute = File.Exists(path) || Directory.Exists(path);
        }

        private void OpenInFileExplorer_Executed(object sender, ExecutedRoutedEventArgs e) {
            var path = (string)e.Parameter;
            var psi = new ProcessStartInfo();
            psi.UseShellExecute = false;
            psi.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

            if (File.Exists(path)) {
                psi.Arguments = "/select,\"" + path + "\"";
            } else if (Directory.Exists(path)) {
                psi.Arguments = "\"" + path + "\"";
            }

            Process.Start(psi);
        }

        private void CopyToClipboard_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = e.Parameter is string || e.Parameter is IDataObject;
            e.Handled = true;
        }

        private void CopyToClipboard_Executed(object sender, ExecutedRoutedEventArgs e) {
            var str = e.Parameter as string;
            if (str != null) {
                Clipboard.SetText(str);
            } else {
                Clipboard.SetDataObject((IDataObject)e.Parameter);
            }
        }

        private void OpenInBrowser_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
        }

        private void OpenInBrowser_Executed(object sender, ExecutedRoutedEventArgs e) {
            var url = (string)e.Parameter;
            Process.Start(url)?.Dispose();
        }

        private void IsIPythonModeEnabled_Loaded(object sender, RoutedEventArgs e) {
            var ev = (e.Source as FrameworkElement)?.DataContext as EnvironmentView;
            if (ev == null) {
                return;
            }

            e.Handled = true;
            if (ev.IsIPythonModeEnabled.HasValue) {
                return;
            }

            ev.IsIPythonModeEnabled = ev.IPythonModeEnabledGetter?.Invoke(ev) ?? false;
        }
    }
}

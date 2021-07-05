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

namespace Microsoft.PythonTools.Profiling {
    /// <summary>
    /// Interaction logic for LaunchProfiling.xaml
    /// </summary>
    public partial class LaunchProfiling : DialogWindowVersioningWorkaround {
        readonly ProfilingTargetView _viewModel;
        private readonly IServiceProvider _serviceProvider;

        internal LaunchProfiling(IServiceProvider serviceProvider, ProfilingTargetView viewModel) {
            _serviceProvider = serviceProvider;
            _viewModel = viewModel;
            DataContext = _viewModel;
            InitializeComponent();
        }

        private void FindInterpreterClick(object sender, RoutedEventArgs e) {
            var standalone = _viewModel.Standalone;
            if (standalone != null) {
                var path = _serviceProvider.BrowseForFileOpen(
                    new System.Windows.Interop.WindowInteropHelper(this).Handle,
                    Strings.ExecutableFilesFilter,
                    standalone.InterpreterPath
                );
                if (File.Exists(path)) {
                    standalone.InterpreterPath = path;
                }
            }
        }

        private void FindScriptClick(object sender, RoutedEventArgs e) {
            var standalone = _viewModel.Standalone;
            if (standalone != null) {
                var path = _serviceProvider.BrowseForFileOpen(
                    new System.Windows.Interop.WindowInteropHelper(this).Handle,
                    Strings.PythonFilesFilter,
                    standalone.ScriptPath
                );
                if (File.Exists(path)) {
                    standalone.ScriptPath = path;
                    if (!Directory.Exists(standalone.WorkingDirectory)) {
                        standalone.WorkingDirectory = Path.GetDirectoryName(path);
                    }
                }
            }
        }

        private void FindWorkingDirectoryClick(object sender, RoutedEventArgs e) {
            var standalone = _viewModel.Standalone;
            if (standalone != null) {
                var path = _serviceProvider.BrowseForDirectory(
                    new System.Windows.Interop.WindowInteropHelper(this).Handle,
                    standalone.WorkingDirectory
                );
                if (!string.IsNullOrEmpty(path)) {
                    standalone.WorkingDirectory = path;
                }
            }
        }

        private void OkClick(object sender, RoutedEventArgs e) {
            this.DialogResult = true;
            Close();
        }

        private void CancelClick(object sender, RoutedEventArgs e) {
            this.DialogResult = false;
            Close();
        }
    }
}

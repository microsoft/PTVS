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

using System.IO;
using System.Windows;

namespace Microsoft.PythonTools.Profiling {
    /// <summary>
    /// Interaction logic for LaunchProfiling.xaml
    /// </summary>
    public partial class LaunchProfiling : DialogWindowVersioningWorkaround {
        readonly ProfilingTargetView _viewModel;

        public LaunchProfiling(ProfilingTargetView viewModel) {
            _viewModel = viewModel;
            DataContext = _viewModel;
            InitializeComponent();
        }

        private void FindInterpreterClick(object sender, RoutedEventArgs e) {
            var standalone = _viewModel.Standalone;
            if (standalone != null) {
                var path = PythonToolsPackage.Instance.BrowseForFileOpen(
                    new System.Windows.Interop.WindowInteropHelper(this).Handle,
                    "Executable files (*.exe;*.bat;*.cmd)|*.exe;*.bat;*.cmd|All Files (*.*)|*.*",
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
                var path = PythonToolsPackage.Instance.BrowseForFileOpen(
                    new System.Windows.Interop.WindowInteropHelper(this).Handle,
                    "Python files (*.py;*.pyw)|*.py;*.pyw|All Files (*.*)|*.*",
                    standalone.ScriptPath
                );
                if (File.Exists(path)) {
                    standalone.ScriptPath = path;
                }
            }
        }

        private void FindWorkingDirectoryClick(object sender, RoutedEventArgs e) {
            var standalone = _viewModel.Standalone;
            if (standalone != null) {
                var path = PythonToolsPackage.Instance.BrowseForDirectory(
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

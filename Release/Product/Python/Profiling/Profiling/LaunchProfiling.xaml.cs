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

using System;
using System.Windows;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Profiling {
    /// <summary>
    /// Interaction logic for LaunchProfiling.xaml
    /// </summary>
    public partial class LaunchProfiling : DialogWindowVersioningWorkaround {
        readonly ProfilingTargetView _viewModel;

        public LaunchProfiling(ProfilingTargetView viewModel) {
            _viewModel = viewModel;

            InitializeComponent();

            DataContext = _viewModel;
        }

        private void FindInterpreterClick(object sender, RoutedEventArgs e) {
            var standalone = _viewModel.Standalone;
            if (standalone != null) {
                var dlg = new OpenFileDialog();
                // TODO: Specify an OpenFileDialog filter for finding an interpreter to profile
                dlg.CheckFileExists = true;
                bool res = dlg.ShowDialog() ?? false;
                if (res) {
                    standalone.InterpreterPath = dlg.FileName;
                }
            }
        }

        private void FindScriptClick(object sender, RoutedEventArgs e) {
            var standalone = _viewModel.Standalone;
            if (standalone != null) {
                var dlg = new OpenFileDialog();
                // TODO: Specify an OpenFileDialog filter for finding a script to profile
                dlg.CheckFileExists = true;
                bool res = dlg.ShowDialog() ?? false;
                if (res) {
                    standalone.ScriptPath = dlg.FileName;
                }
            }
        }

        private void FindWorkingDirectoryClick(object sender, RoutedEventArgs e) {
            var standalone = _viewModel.Standalone;
            if (standalone != null) {
                var dlg = new System.Windows.Forms.FolderBrowserDialog();
                dlg.SelectedPath = standalone.WorkingDirectory;
                var res = dlg.ShowDialog();
                if (res == System.Windows.Forms.DialogResult.OK) {
                    standalone.WorkingDirectory = dlg.SelectedPath;
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

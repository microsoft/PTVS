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
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.EnvironmentsList.Host {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private readonly CompositionContainer _container;

        public MainWindow() {
            InitializeComponent();

            EnvironmentsToolWindow.ViewCreated += EnvironmentsToolWindow_ViewCreated;

            _container = new CompositionContainer(new AggregateCatalog(
                new AssemblyCatalog(typeof(IInterpreterRegistryService).Assembly),
                new AssemblyCatalog(typeof(IInterpreterOptionsService).Assembly)
            ));

            EnvironmentsToolWindow.Interpreters = _container.GetExport<IInterpreterRegistryService>().Value;
            EnvironmentsToolWindow.Service = _container.GetExport<IInterpreterOptionsService>().Value;
        }

        void EnvironmentsToolWindow_ViewCreated(object sender, EnvironmentViewEventArgs e) {
            e.View.Extensions.Add(new PipExtensionProvider(e.View.Factory));
            var withDb = e.View.Factory as PythonInterpreterFactoryWithDatabase;
            if (withDb != null) {
                e.View.Extensions.Add(new DBExtensionProvider(withDb));
            }
        }


        private void OpenInteractiveWindow_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = e.Parameter is EnvironmentView;
        }

        private void OpenInteractiveWindow_Executed(object sender, ExecutedRoutedEventArgs e) {
            MessageBox.Show("Opening interactive window for " + ((EnvironmentView)e.Parameter).Description);
        }

        private void UnhandledException_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
        }

        private void UnhandledException_Executed(object sender, ExecutedRoutedEventArgs e) {
            System.Diagnostics.Debug.WriteLine((Exception)e.Parameter);
        }

        private void OpenInteractiveOptions_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = e.Parameter is EnvironmentView;
        }

        private void OpenInteractiveOptions_Executed(object sender, ExecutedRoutedEventArgs e) {
            MessageBox.Show("Opening interactive options for " + ((EnvironmentView)e.Parameter).Description);
        }

        private void OpenConsole_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = e.Parameter is EnvironmentView;
        }

        private void OpenConsole_Executed(object sender, ExecutedRoutedEventArgs e) {
            MessageBox.Show("Opening console for " + ((EnvironmentView)e.Parameter).InterpreterPath);
        }


        internal static string GetScriptsPath(EnvironmentView view) {
            if (view == null) {
                return null;
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Visual Studio " + AssemblyVersionInfo.VSName,
                "Python Scripts",
                view.Description
            );
        }

        private void OpenInteractiveScripts_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var path = GetScriptsPath(e.Parameter as EnvironmentView);
            e.CanExecute = !string.IsNullOrEmpty(path);
            e.Handled = true;
        }

        private void OpenInteractiveScripts_Executed(object sender, ExecutedRoutedEventArgs e) {
            var path = GetScriptsPath(e.Parameter as EnvironmentView);
            if (string.IsNullOrEmpty(path)) {
                return;
            }

            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }

            var psi = new ProcessStartInfo();
            psi.UseShellExecute = false;
            psi.FileName = "explorer.exe";
            psi.Arguments = "\"" + path + "\"";

            Process.Start(psi).Dispose();
            e.Handled = true;
        }

        private void EnableIPythonInteractive_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var path = GetScriptsPath(e.Parameter as EnvironmentView);
            e.CanExecute = !string.IsNullOrEmpty(path) && !File.Exists(Path.Combine(path, "__test_mode.txt"));
            e.Handled = true;
        }

        private void EnableIPythonInteractive_Executed(object sender, ExecutedRoutedEventArgs e) {
            var path = GetScriptsPath(e.Parameter as EnvironmentView);
            if (string.IsNullOrEmpty(path)) {
                return;
            }

            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }

            File.WriteAllText(Path.Combine(path, "__test_mode.txt"), "# Contents of the file");
        }

        private void DisableIPythonInteractive_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var path = GetScriptsPath(e.Parameter as EnvironmentView);
            e.CanExecute = !string.IsNullOrEmpty(path) && File.Exists(Path.Combine(path, "__test_mode.txt"));
            e.Handled = true;
        }

        private void DisableIPythonInteractive_Executed(object sender, ExecutedRoutedEventArgs e) {
            var path = GetScriptsPath(e.Parameter as EnvironmentView);
            if (string.IsNullOrEmpty(path)) {
                return;
            }

            path = Path.Combine(path, "__test_mode.txt");
            if (File.Exists(path)) {
                File.Delete(path);
            }
        }
    }
}

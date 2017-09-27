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

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.EnvironmentsList {
    internal partial class NoDBExtension : UserControl {
        public static readonly RoutedCommand DisableExperiment = new RoutedCommand();

        private bool _hasBeenDisabled;

        public NoDBExtension() {
            InitializeComponent();
        }

        private void DisableExperiment_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = !_hasBeenDisabled;
            e.Handled = true;
        }

        private void DisableExperiment_Executed(object sender, ExecutedRoutedEventArgs e) {
            ExperimentalOptions.NoDatabaseFactory = false;
            _hasBeenDisabled = true;
            e.Handled = true;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public sealed class NoDBExtensionProvider : IEnvironmentViewExtension {
        private FrameworkElement _wpfObject;

        public NoDBExtensionProvider() {
        }

        public int SortPriority {
            get { return -7; }
        }

        public string LocalizedDisplayName {
            get { return Resources.NoDBExtensionDisplayName; }
        }

        public object HelpContent {
            get { return Resources.NoDBExtensionHelpContent; }
        }

        public string HelpText {
            get { return Resources.NoDBExtensionHelpContent; }
        }

        public FrameworkElement WpfObject {
            get {
                if (_wpfObject == null) {
                    _wpfObject = new NoDBExtension();
                }
                return _wpfObject;
            }
        }
    }

}

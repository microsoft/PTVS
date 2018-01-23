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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.EnvironmentsList {
    internal partial class CondaExtension : UserControl, ICanFocus {
        public static readonly ICommand Create = new RoutedCommand();

        private readonly CondaExtensionProvider _provider;
        
        public CondaExtension(CondaExtensionProvider provider) {
            _provider = provider;
            DataContextChanged += ConfigurationExtension_DataContextChanged;
            InitializeComponent();
        }

        void ICanFocus.Focus() {
            Dispatcher.BeginInvoke((Action)(() => {
                try {
                    Focus();
                    if (EnvironmentNameText.IsVisible) {
                        Keyboard.Focus(EnvironmentNameText);
                    } else {
                        EnvironmentNameText.IsVisibleChanged += EnvironmentNameText_IsVisibleChanged;
                    }
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                }
            }), DispatcherPriority.Loaded);
        }

        private void EnvironmentNameText_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            EnvironmentNameText.IsVisibleChanged -= EnvironmentNameText_IsVisibleChanged;
            Keyboard.Focus(EnvironmentNameText);
        }

        void ConfigurationExtension_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            var view = e.NewValue as EnvironmentView;
            if (view != null) {
                var current = Subcontext.DataContext as CondaEnvironmentView;
                if (current == null) {
                    var cev = new CondaEnvironmentView();
                    Subcontext.DataContext = cev;
                }
            }
        }

        private void Create_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var view = e.Parameter as CondaEnvironmentView;
            e.CanExecute = view != null && _provider.CanCreateEnvironment(view);
            e.Handled = true;
        }

        private async void Create_Executed(object sender, ExecutedRoutedEventArgs e) {
            var cev = (CondaEnvironmentView)e.Parameter;
            try {
                e.Handled = true;
                await _provider.CreateEnvironmentAsync(cev);
            } catch (OperationCanceledException) {
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                ToolWindow.SendUnhandledException(this, ExceptionDispatchInfo.Capture(ex));
            }

            CommandManager.InvalidateRequerySuggested();
        }
    }

    sealed class CondaExtensionProvider : IEnvironmentViewExtension, ICondaEnvironmentManagerUI {
        private FrameworkElement _wpfObject;
        private readonly IInterpreterOptionsService _options;
        private readonly IInterpreterRegistryService _registry;
        private bool _isWorking;

        private static readonly KeyValuePair<string, string>[] UnbufferedEnv = new[] {
            new KeyValuePair<string, string>("PYTHONUNBUFFERED", "1")
        };

        internal CondaExtensionProvider(IInterpreterOptionsService options, IInterpreterRegistryService registry) {
            _options = options;
            _registry = registry;
        }

        internal async Task CreateEnvironmentAsync(CondaEnvironmentView view) {
            var mgr = CondaEnvironmentManager.Create(_registry);
            if (mgr == null) {
                // TODO: Instead of this message box, hide the input/create
                // controls and show a message there instead.
                MessageBox.Show(Resources.CondaExtensionNotAvailable, Resources.ProductTitle);
                return;
            }

            _isWorking = true;
            try {
                // Use single equal sign to install the selected version or any of its revisions
                var packages = new[] { PackageSpec.FromArguments($"python={view.VersionName}") };
                await mgr.CreateAsync(view.EnvironmentName, packages, this, CancellationToken.None);

            } finally {
                _isWorking = false;
            }
        }

        internal bool CanCreateEnvironment(CondaEnvironmentView view) {
            if (string.IsNullOrEmpty(view.EnvironmentName) || string.IsNullOrEmpty(view.VersionName) || _isWorking) {
                return false;
            }
            return true;
        }

        internal void ResetConfiguration(CondaEnvironmentView view) {
            view.EnvironmentName = string.Empty;
            view.VersionName = string.Empty;
        }

        public event EventHandler<OutputEventArgs> OutputTextReceived;
        public event EventHandler<OutputEventArgs> ErrorTextReceived;
        public event EventHandler<OutputEventArgs> OperationStarted;
        public event EventHandler<OperationFinishedEventArgs> OperationFinished;

        void ICondaEnvironmentManagerUI.OnOutputTextReceived(ICondaEnvironmentManager mgr, string text) {
            OutputTextReceived?.Invoke(this, new OutputEventArgs(text));
        }

        void ICondaEnvironmentManagerUI.OnErrorTextReceived(ICondaEnvironmentManager mgr, string text) {
            ErrorTextReceived?.Invoke(this, new OutputEventArgs(text));
        }

        void ICondaEnvironmentManagerUI.OnOperationStarted(ICondaEnvironmentManager mgr, string operation) {
            OperationStarted?.Invoke(this, new OutputEventArgs(operation));
        }

        void ICondaEnvironmentManagerUI.OnOperationFinished(ICondaEnvironmentManager mgr, string operation, bool success) {
            OperationFinished?.Invoke(this, new OperationFinishedEventArgs(operation, success));
        }

        public int SortPriority {
            get { return -9; }
        }

        public string LocalizedDisplayName {
            get { return Resources.CondaExtensionDisplayName; }
        }

        public FrameworkElement WpfObject {
            get {
                if (_wpfObject == null) {
                    _wpfObject = new CondaExtension(this);
                }
                return _wpfObject;
            }
        }

        public object HelpContent {
            get { return Resources.CondaExtensionHelpContent; }
        }

        public string HelpText {
            get { return Resources.CondaExtensionHelpContent; }
        }
    }

    struct CondaValues {
        public string EnvironmentName;
        public string VersionName;
    }

    sealed class CondaEnvironmentView : INotifyPropertyChanged {
        private static readonly string[] _versionNames = new[] {
            "2.6",
            "2.7",
            "3.3",
            "3.4",
            "3.5",
            "3.6"
        };

        private CondaValues _values;

        public CondaEnvironmentView() {
        }

        public static IList<string> VersionNames => _versionNames;

        public string EnvironmentName {
            get { return _values.EnvironmentName; }
            set {
                if (_values.EnvironmentName != value) {
                    _values.EnvironmentName = value;
                    OnPropertyChanged();
                }
            }
        }

        public string VersionName {
            get { return _values.VersionName; }
            set {
                if (_values.VersionName != value) {
                    _values.VersionName = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

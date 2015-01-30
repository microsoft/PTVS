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
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.PythonTools.EnvironmentsList.Properties;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;
using Microsoft.VisualStudioTools.Wpf;

namespace Microsoft.PythonTools.EnvironmentsList {
    internal partial class ConfigurationExtension : UserControl {
        public static readonly ICommand Apply = new RoutedCommand();
        public static readonly ICommand Reset = new RoutedCommand();
        public static readonly ICommand AutoDetect = new RoutedCommand();

        private readonly ConfigurationExtensionProvider _provider;
        
        public ConfigurationExtension(ConfigurationExtensionProvider provider) {
            _provider = provider;
            DataContextChanged += ConfigurationExtension_DataContextChanged;
            InitializeComponent();
        }

        void ConfigurationExtension_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            var view = e.NewValue as EnvironmentView;
            if (view != null) {
                var current = Subcontext.DataContext as ConfigurationEnvironmentView;
                if (current == null || current.EnvironmentView != view) {
                    var cev = new ConfigurationEnvironmentView(view);
                    _provider.ResetConfiguration(cev);
                    Subcontext.DataContext = cev;
                }
            }
        }

        private void Apply_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var view = e.Parameter as ConfigurationEnvironmentView;
            e.CanExecute = view != null && _provider.IsConfigurationChanged(view);
        }

        private void Apply_Executed(object sender, ExecutedRoutedEventArgs e) {
            _provider.ApplyConfiguration((ConfigurationEnvironmentView)e.Parameter);
        }

        private void Reset_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var view = e.Parameter as ConfigurationEnvironmentView;
            e.CanExecute = view != null && _provider.IsConfigurationChanged(view);
        }

        private void Reset_Executed(object sender, ExecutedRoutedEventArgs e) {
            _provider.ResetConfiguration((ConfigurationEnvironmentView)e.Parameter);
        }

        private void Browse_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            Commands.CanExecute(null, sender, e);
        }

        private void Browse_Executed(object sender, ExecutedRoutedEventArgs e) {
            Commands.Executed(null, sender, e);
        }

        private void SelectAllText(object sender, RoutedEventArgs e) {
            var tb = sender as TextBox;
            if (tb != null) {
                tb.SelectAll();
            }
        }

        private void AutoDetect_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var view = e.Parameter as ConfigurationEnvironmentView;
            e.CanExecute = view != null && !view.IsAutoDetectRunning && (
                Directory.Exists(view.PrefixPath) ||
                File.Exists(view.InterpreterPath) ||
                File.Exists(view.WindowsInterpreterPath)
            );
        }


        private async void AutoDetect_Executed(object sender, ExecutedRoutedEventArgs e) {
            var view = (ConfigurationEnvironmentView)e.Parameter;
            try {
                view.IsAutoDetectRunning = true;
                CommandManager.InvalidateRequerySuggested();

                var newView = await AutoDetectAsync(view.Values);

                view.Values = newView;
                CommandManager.InvalidateRequerySuggested();
            } finally {
                view.IsAutoDetectRunning = false;
            }
        }

        private async Task<ConfigurationValues> AutoDetectAsync(ConfigurationValues view) {
            if (!Directory.Exists(view.PrefixPath)) {
                if (File.Exists(view.InterpreterPath)) {
                    view.PrefixPath = Path.GetDirectoryName(view.InterpreterPath);
                } else if (File.Exists(view.WindowsInterpreterPath)) {
                    view.PrefixPath = Path.GetDirectoryName(view.WindowsInterpreterPath);
                } else if (Directory.Exists(view.LibraryPath)) {
                    view.PrefixPath = Path.GetDirectoryName(view.LibraryPath);
                } else {
                    // Don't have enough information, so abort without changing
                    // any settings.
                    return view;
                }
                while (Directory.Exists(view.PrefixPath) && !File.Exists(CommonUtils.FindFile(view.PrefixPath, "site.py"))) {
                    view.PrefixPath = Path.GetDirectoryName(view.PrefixPath);
                }
            }

            if (!Directory.Exists(view.PrefixPath)) {
                // If view.PrefixPath is not valid by this point, we can't find anything
                // else, so abort withou changing any settings.
                return view;
            }

            if (!File.Exists(view.InterpreterPath)) {
                view.InterpreterPath = CommonUtils.FindFile(
                    view.PrefixPath,
                    CPythonInterpreterFactoryConstants.ConsoleExecutable,
                    firstCheck: new[] { "scripts" }
                );
            }
            if (!File.Exists(view.WindowsInterpreterPath)) {
                view.WindowsInterpreterPath = CommonUtils.FindFile(
                    view.PrefixPath,
                    CPythonInterpreterFactoryConstants.WindowsExecutable,
                    firstCheck: new[] { "scripts" }
                );
            }
            if (!Directory.Exists(view.LibraryPath)) {
                var sitePy = CommonUtils.FindFile(
                    view.PrefixPath,
                    "site.py",
                    firstCheck: new[] { "lib" }
                );
                if (File.Exists(sitePy)) {
                    view.LibraryPath = Path.GetDirectoryName(sitePy);
                }
            }

            if (File.Exists(view.InterpreterPath)) {
                using (var output = ProcessOutput.RunHiddenAndCapture(
                    view.InterpreterPath, "-c", "import sys; print('%s.%s' % (sys.version_info[0], sys.version_info[1]))"
                )) {
                    var exitCode = await output;
                    if (exitCode == 0) {
                        view.VersionName = output.StandardOutputLines.FirstOrDefault() ?? view.VersionName;
                    }
                }

                var binaryType = Microsoft.PythonTools.Interpreter.NativeMethods.GetBinaryType(view.InterpreterPath);
                if (binaryType == ProcessorArchitecture.Amd64) {
                    view.ArchitectureName = "64-bit";
                } else if (binaryType == ProcessorArchitecture.X86) {
                    view.ArchitectureName = "32-bit";
                }
            }

            return view;
        }
    }

    sealed class ConfigurationExtensionProvider : IEnvironmentViewExtension {
        private FrameworkElement _wpfObject;
        private readonly ConfigurablePythonInterpreterFactoryProvider _factoryProvider;

        internal ConfigurationExtensionProvider(ConfigurablePythonInterpreterFactoryProvider factoryProvider) {
            _factoryProvider = factoryProvider;
        }

        public void ApplyConfiguration(ConfigurationEnvironmentView view) {
            _factoryProvider.SetOptions(new InterpreterFactoryCreationOptions {
                Id = view.EnvironmentView.Factory.Id,
                Description = view.Description,
                PrefixPath = view.PrefixPath,
                InterpreterPath = view.InterpreterPath,
                WindowInterpreterPath = view.WindowsInterpreterPath,
                LibraryPath = view.LibraryPath,
                PathEnvironmentVariableName = view.PathEnvironmentVariable,
                ArchitectureString = view.ArchitectureName,
                LanguageVersionString = view.VersionName
            });
        }

        public bool IsConfigurationChanged(ConfigurationEnvironmentView view) {
            var factory = view.EnvironmentView.Factory;
            var arch = factory.Configuration.Architecture == ProcessorArchitecture.Amd64 ? "64-bit" : "32-bit";
            return view.Description != factory.Description ||
                view.PrefixPath != factory.Configuration.PrefixPath ||
                view.InterpreterPath != factory.Configuration.InterpreterPath ||
                view.WindowsInterpreterPath != factory.Configuration.WindowsInterpreterPath ||
                view.LibraryPath != factory.Configuration.LibraryPath ||
                view.PathEnvironmentVariable != factory.Configuration.PathEnvironmentVariable ||
                view.ArchitectureName != arch ||
                view.VersionName != factory.Configuration.Version.ToString();
        }

        public void ResetConfiguration(ConfigurationEnvironmentView view) {
            var factory = view.EnvironmentView.Factory;
            view.Description = factory.Description;
            view.PrefixPath = factory.Configuration.PrefixPath;
            view.InterpreterPath = factory.Configuration.InterpreterPath;
            view.WindowsInterpreterPath = factory.Configuration.WindowsInterpreterPath;
            view.LibraryPath = factory.Configuration.LibraryPath;
            view.PathEnvironmentVariable = factory.Configuration.PathEnvironmentVariable;
            view.ArchitectureName = factory.Configuration.Architecture == ProcessorArchitecture.Amd64 ? "64-bit" : "32-bit";
            view.VersionName = factory.Configuration.Version.ToString();
        }

        public int SortPriority {
            get { return -9; }
        }

        public string LocalizedDisplayName {
            get { return Resources.ConfigurationExtensionDisplayName; }
        }

        public FrameworkElement WpfObject {
            get {
                if (_wpfObject == null) {
                    _wpfObject = new ConfigurationExtension(this);
                }
                return _wpfObject;
            }
        }

        public object HelpContent {
            get { return Resources.ConfigurationExtensionHelpContent; }
        }
    }

    struct ConfigurationValues {
        public string Description;
        public string PrefixPath;
        public string InterpreterPath;
        public string WindowsInterpreterPath;
        public string LibraryPath;
        public string PathEnvironmentVariable;
        public string VersionName;
        public string ArchitectureName;
    }

    sealed class ConfigurationEnvironmentView : INotifyPropertyChanged {
        private static readonly string[] _architectureNames = new[] {
            "32-bit",
            "64-bit"
        };

        private static readonly string[] _versionNames = new[] {
            "2.5",
            "2.6",
            "2.7",
            "3.0",
            "3.1",
            "3.2",
            "3.3",
            "3.4",
            "3.5"
        };

        private readonly EnvironmentView _view;
        private bool _isAutoDetectRunning;
        private ConfigurationValues _values;

        public ConfigurationEnvironmentView(EnvironmentView view) {
            _view = view;
        }

        public EnvironmentView EnvironmentView {
            get { return _view; }
        }

        public static IList<string> ArchitectureNames {
            get {
                return _architectureNames;
            }
        }

        public static IList<string> VersionNames {
            get {
                return _versionNames;
            }
        }

        public bool IsAutoDetectRunning {
            get { return _isAutoDetectRunning; }
            set {
                if (_isAutoDetectRunning != value) {
                    _isAutoDetectRunning = value;
                    OnPropertyChanged();
                }
            }
        }

        public ConfigurationValues Values {
            get { return _values; }
            set {
                Description = value.Description;
                PrefixPath = value.PrefixPath;
                InterpreterPath = value.InterpreterPath;
                WindowsInterpreterPath = value.WindowsInterpreterPath;
                LibraryPath = value.LibraryPath;
                PathEnvironmentVariable = value.PathEnvironmentVariable;
                VersionName = value.VersionName;
                ArchitectureName = value.ArchitectureName;
            }
        }


        public string Description {
            get { return _values.Description; }
            set {
                if (_values.Description != value) {
                    _values.Description = value;
                    OnPropertyChanged();
                }
            }
        }

        public string PrefixPath {
            get { return _values.PrefixPath; }
            set {
                if (_values.PrefixPath != value) {
                    _values.PrefixPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public string InterpreterPath {
            get { return _values.InterpreterPath; }
            set {
                if (_values.InterpreterPath != value) {
                    _values.InterpreterPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public string WindowsInterpreterPath {
            get { return _values.WindowsInterpreterPath; }
            set {
                if (_values.WindowsInterpreterPath != value) {
                    _values.WindowsInterpreterPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public string LibraryPath {
            get { return _values.LibraryPath; }
            set {
                if (_values.LibraryPath != value) {
                    _values.LibraryPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public string PathEnvironmentVariable {
            get { return _values.PathEnvironmentVariable; }
            set {
                if (_values.PathEnvironmentVariable != value) {
                    _values.PathEnvironmentVariable = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ArchitectureName {
            get { return _values.ArchitectureName; }
            set {
                if (_values.ArchitectureName != value) {
                    _values.ArchitectureName = value;
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
            var evt = PropertyChanged;
            if (evt != null) {
                evt(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}

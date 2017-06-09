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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.EnvironmentsList {
    public sealed class EnvironmentView : DependencyObject {
        public static readonly RoutedCommand OpenInteractiveWindow = new RoutedCommand();
        public static readonly RoutedCommand OpenInteractiveScripts = new RoutedCommand();
        public static readonly RoutedCommand OpenInPowerShell = new RoutedCommand();
        public static readonly RoutedCommand OpenInCommandPrompt = new RoutedCommand();
        public static readonly RoutedCommand MakeGlobalDefault = new RoutedCommand();
        public static readonly RoutedCommand MakeActiveInCurrentProject = new RoutedCommand();

        private const string AddNewEnvironmentViewId = "__AddNewEnvironmentView";
        private const string OnlineHelpViewId = "__OnlineHelpView";

        public static readonly IEnumerable<InterpreterConfiguration> ExtraItems = new[] {
            new InterpreterConfiguration(OnlineHelpViewId, OnlineHelpViewId),
            new InterpreterConfiguration(AddNewEnvironmentViewId, AddNewEnvironmentViewId)
        };

        // Names of properties that will be requested from interpreter configurations
        internal const string CompanyKey = "Company";
        internal const string SupportUrlKey = "SupportUrl";

        /// <summary>
        /// Used with <see cref="CommonUtils.FindFile"/> to more efficiently
        /// find interpreter executables.
        /// </summary>
        private static readonly string[] _likelyInterpreterPaths = new[] { "Scripts" };

        private readonly IInterpreterOptionsService _service;
        private readonly IInterpreterRegistryService _registry;
        private readonly IPythonInterpreterFactoryWithDatabase _withDb;

        public IPythonInterpreterFactory Factory { get; }
        public InterpreterConfiguration Configuration { get; }
        public string LocalizedDisplayName { get; }
        public string LocalizedHelpText { get; }

        private EnvironmentView(string id, string localizedName, string localizedHelpText) {
            Configuration = new InterpreterConfiguration(id, id);
            Description = LocalizedDisplayName = localizedName;
            LocalizedHelpText = localizedHelpText ?? "";
            Extensions = new ObservableCollection<object>();
        }

        internal EnvironmentView(
            IInterpreterOptionsService service,
            IInterpreterRegistryService registry,
            IPythonInterpreterFactory factory,
            Redirector redirector
        ) {
            if (service == null) {
                throw new ArgumentNullException(nameof(service));
            }
            if (registry == null) {
                throw new ArgumentNullException(nameof(registry));
            }
            if (factory == null) {
                throw new ArgumentNullException(nameof(factory));
            }
            if (factory.Configuration == null) {
                throw new ArgumentException("factory must include a configuration");
            }

            _service = service;
            _registry = registry;
            Factory = factory;
            Configuration = Factory.Configuration;
            LocalizedDisplayName = Configuration.Description;

            _withDb = factory as IPythonInterpreterFactoryWithDatabase;
            if (_withDb != null) {
                _withDb.IsCurrentChanged += Factory_IsCurrentChanged;
                IsCheckingDatabase = _withDb.IsCheckingDatabase;
                IsCurrent = _withDb.IsCurrent;
            }
            

            if (_service.IsConfigurable(Factory.Configuration.Id)) {
                IsConfigurable = true;
            }

            Description = Factory.Configuration.Description;
            IsDefault = (_service != null && _service.DefaultInterpreterId == Configuration.Id);

            PrefixPath = Factory.Configuration.PrefixPath;
            InterpreterPath = Factory.Configuration.InterpreterPath;
            WindowsInterpreterPath = Factory.Configuration.WindowsInterpreterPath;

            Extensions = new ObservableCollection<object>();
            Extensions.Add(new EnvironmentPathsExtensionProvider());
            if (IsConfigurable) {
                Extensions.Add(new ConfigurationExtensionProvider(_service, alwaysCreateNew: false));
            }

            CanBeDefault = Factory.CanBeDefault();

            Company = _registry.GetProperty(Factory.Configuration.Id, CompanyKey) as string ?? "";
            SupportUrl = _registry.GetProperty(Factory.Configuration.Id, SupportUrlKey) as string ?? "";

            LocalizedHelpText = Company;
        }

        public static EnvironmentView CreateAddNewEnvironmentView(IInterpreterOptionsService service) {
            var ev = new EnvironmentView(AddNewEnvironmentViewId, Resources.EnvironmentViewCustomAutomationName, null);
            ev.Extensions = new ObservableCollection<object>();
            ev.Extensions.Add(new ConfigurationExtensionProvider(service, alwaysCreateNew: true));
            return ev;
        }

        public static EnvironmentView CreateOnlineHelpEnvironmentView() {
            return new EnvironmentView(OnlineHelpViewId, Resources.EnvironmentViewOnlineHelpLabel, null);
        }

        public static EnvironmentView CreateMissingEnvironmentView(string id, string description) {
            return new EnvironmentView(id, description + Strings.MissingSuffix, null);
        }

        public static bool IsAddNewEnvironmentView(string id) => AddNewEnvironmentViewId.Equals(id);
        public static bool IsOnlineHelpView(string id) => OnlineHelpViewId.Equals(id);

        public static bool IsAddNewEnvironmentView(EnvironmentView view) => AddNewEnvironmentViewId.Equals(view?.Configuration.Id);
        public static bool IsOnlineHelpView(EnvironmentView view) => OnlineHelpViewId.Equals(view?.Configuration.Id);

        public ObservableCollection<object> Extensions { get; private set; }

        private void Factory_IsCurrentChanged(object sender, EventArgs e) {
            Debug.Assert(_withDb != null);
            if (_withDb == null) {
                return;
            }

            Dispatcher.BeginInvoke((Action)(() => {
                IsCheckingDatabase = _withDb.IsCheckingDatabase;
                IsCurrent = _withDb.IsCurrent;
            }));
        }

        #region Read-only State Dependency Properties

        private static readonly DependencyPropertyKey IsConfigurablePropertyKey = DependencyProperty.RegisterReadOnly("IsConfigurable", typeof(bool), typeof(EnvironmentView), new PropertyMetadata(false));
        private static readonly DependencyPropertyKey CanBeDefaultPropertyKey = DependencyProperty.RegisterReadOnly("CanBeDefault", typeof(bool), typeof(EnvironmentView), new PropertyMetadata(true));
        private static readonly DependencyPropertyKey IsDefaultPropertyKey = DependencyProperty.RegisterReadOnly("IsDefault", typeof(bool), typeof(EnvironmentView), new PropertyMetadata(false));
        private static readonly DependencyPropertyKey IsCurrentPropertyKey = DependencyProperty.RegisterReadOnly("IsCurrent", typeof(bool), typeof(EnvironmentView), new PropertyMetadata(true));
        private static readonly DependencyPropertyKey IsCheckingDatabasePropertyKey = DependencyProperty.RegisterReadOnly("IsCheckingDatabase", typeof(bool), typeof(EnvironmentView), new PropertyMetadata(false));
        private static readonly DependencyPropertyKey RefreshDBProgressPropertyKey = DependencyProperty.RegisterReadOnly("RefreshDBProgress", typeof(int), typeof(EnvironmentView), new PropertyMetadata(0));
        private static readonly DependencyPropertyKey RefreshDBMessagePropertyKey = DependencyProperty.RegisterReadOnly("RefreshDBMessage", typeof(string), typeof(EnvironmentView), new PropertyMetadata());
        private static readonly DependencyPropertyKey IsRefreshingDBPropertyKey = DependencyProperty.RegisterReadOnly("IsRefreshingDB", typeof(bool), typeof(EnvironmentView), new PropertyMetadata(false));
        private static readonly DependencyPropertyKey IsRefreshDBProgressIndeterminatePropertyKey = DependencyProperty.RegisterReadOnly("IsRefreshDBProgressIndeterminate", typeof(bool), typeof(EnvironmentView), new PropertyMetadata(false));

        public static readonly DependencyProperty IsConfigurableProperty = IsConfigurablePropertyKey.DependencyProperty;
        public static readonly DependencyProperty CanBeDefaultProperty = CanBeDefaultPropertyKey.DependencyProperty;
        public static readonly DependencyProperty IsDefaultProperty = IsDefaultPropertyKey.DependencyProperty;
        public static readonly DependencyProperty IsCurrentProperty = IsCurrentPropertyKey.DependencyProperty;
        public static readonly DependencyProperty IsCheckingDatabaseProperty = IsCheckingDatabasePropertyKey.DependencyProperty;
        public static readonly DependencyProperty RefreshDBMessageProperty = RefreshDBMessagePropertyKey.DependencyProperty;
        public static readonly DependencyProperty RefreshDBProgressProperty = RefreshDBProgressPropertyKey.DependencyProperty;
        public static readonly DependencyProperty IsRefreshingDBProperty = IsRefreshingDBPropertyKey.DependencyProperty;
        public static readonly DependencyProperty IsRefreshDBProgressIndeterminateProperty = IsRefreshDBProgressIndeterminatePropertyKey.DependencyProperty;

        public bool IsConfigurable {
            get { return Factory == null ? false : (bool)GetValue(IsConfigurableProperty); }
            set { if (Factory != null) { SetValue(IsConfigurablePropertyKey, value); } }
        }

        public bool CanBeDefault {
            get { return Factory == null ? false : (bool)GetValue(CanBeDefaultProperty); }
            set { if (Factory != null) { SetValue(CanBeDefaultPropertyKey, value); } }
        }

        public bool IsDefault {
            get { return Factory == null ? false : (bool)GetValue(IsDefaultProperty); }
            internal set { if (Factory != null) { SetValue(IsDefaultPropertyKey, value); } }
        }

        public bool IsCurrent {
            get { return Factory == null ? true : (bool)GetValue(IsCurrentProperty); }
            internal set { if (Factory != null) { SetValue(IsCurrentPropertyKey, value); } }
        }

        public bool IsCheckingDatabase {
            get { return Factory == null ? false : (bool)GetValue(IsCheckingDatabaseProperty); }
            internal set { if (Factory != null) { SetValue(IsCheckingDatabasePropertyKey, value); } }
        }

        public int RefreshDBProgress {
            get { return Factory == null ? 0 : (int)GetValue(RefreshDBProgressProperty); }
            internal set { if (Factory != null) { SetValue(RefreshDBProgressPropertyKey, value); } }
        }

        public string RefreshDBMessage {
            get { return Factory == null ? string.Empty : (string)GetValue(RefreshDBMessageProperty); }
            internal set { if (Factory != null) { SetValue(RefreshDBMessagePropertyKey, value); } }
        }

        public bool IsRefreshingDB {
            get { return Factory == null ? false : (bool)GetValue(IsRefreshingDBProperty); }
            internal set { if (Factory != null) { SetValue(IsRefreshingDBPropertyKey, value); } }
        }

        public bool IsRefreshDBProgressIndeterminate {
            get { return Factory == null ? false : (bool)GetValue(IsRefreshDBProgressIndeterminateProperty); }
            internal set { if (Factory != null) { SetValue(IsRefreshDBProgressIndeterminatePropertyKey, value); } }
        }

        #endregion

        #region Configuration Dependency Properties

        private static readonly DependencyPropertyKey DescriptionPropertyKey = DependencyProperty.RegisterReadOnly("Description", typeof(string), typeof(EnvironmentView), new PropertyMetadata(""));
        private static readonly DependencyPropertyKey PrefixPathPropertyKey = DependencyProperty.RegisterReadOnly("PrefixPath", typeof(string), typeof(EnvironmentView), new PropertyMetadata());
        private static readonly DependencyPropertyKey InterpreterPathPropertyKey = DependencyProperty.RegisterReadOnly("InterpreterPath", typeof(string), typeof(EnvironmentView), new PropertyMetadata());
        private static readonly DependencyPropertyKey WindowsInterpreterPathPropertyKey = DependencyProperty.RegisterReadOnly("WindowsInterpreterPath", typeof(string), typeof(EnvironmentView), new PropertyMetadata());
        private static readonly DependencyPropertyKey PathEnvironmentVariablePropertyKey = DependencyProperty.RegisterReadOnly("PathEnvironmentVariable", typeof(string), typeof(EnvironmentView), new PropertyMetadata());

        public static readonly DependencyProperty DescriptionProperty = DescriptionPropertyKey.DependencyProperty;
        public static readonly DependencyProperty PrefixPathProperty = PrefixPathPropertyKey.DependencyProperty;
        public static readonly DependencyProperty InterpreterPathProperty = InterpreterPathPropertyKey.DependencyProperty;
        public static readonly DependencyProperty WindowsInterpreterPathProperty = WindowsInterpreterPathPropertyKey.DependencyProperty;
        public static readonly DependencyProperty PathEnvironmentVariableProperty = PathEnvironmentVariablePropertyKey.DependencyProperty;

        public string Description {
            get { return (string)GetValue(DescriptionProperty) ?? ""; }
            set { SetValue(DescriptionPropertyKey, value ?? ""); }
        }

        public string PrefixPath {
            get { return Factory == null ? string.Empty : (string)GetValue(PrefixPathProperty); }
            set { if (Factory != null) { SetValue(PrefixPathPropertyKey, value); } }
        }

        public string InterpreterPath {
            get { return Factory == null ? string.Empty : (string)GetValue(InterpreterPathProperty); }
            set { if (Factory != null) { SetValue(InterpreterPathPropertyKey, value); } }
        }

        public string WindowsInterpreterPath {
            get { return Factory == null ? string.Empty : (string)GetValue(WindowsInterpreterPathProperty); }
            set { if (Factory != null) { SetValue(WindowsInterpreterPathPropertyKey, value); } }
        }

        public string PathEnvironmentVariable {
            get { return Factory == null ? string.Empty : (string)GetValue(PathEnvironmentVariableProperty); }
            set { if (Factory != null) { SetValue(PathEnvironmentVariablePropertyKey, value); } }
        }

        #endregion

        #region Extra Information Dependency Properties

        private static readonly DependencyPropertyKey CompanyPropertyKey = DependencyProperty.RegisterReadOnly("Company", typeof(string), typeof(EnvironmentView), new PropertyMetadata(""));
        private static readonly DependencyPropertyKey SupportUrlPropertyKey = DependencyProperty.RegisterReadOnly("SupportUrl", typeof(string), typeof(EnvironmentView), new PropertyMetadata(""));

        public static readonly DependencyProperty CompanyProperty = CompanyPropertyKey.DependencyProperty;
        public static readonly DependencyProperty SupportUrlProperty = SupportUrlPropertyKey.DependencyProperty;

        public string Company {
            get { return (string)GetValue(CompanyProperty) ?? ""; }
            set { SetValue(CompanyPropertyKey, value ?? ""); }
        }

        public string SupportUrl {
            get { return (string)GetValue(SupportUrlProperty) ?? ""; }
            set { SetValue(SupportUrlPropertyKey, value ?? ""); }
        }

        #endregion

        public static readonly DependencyProperty IsIPythonModeEnabledProperty = DependencyProperty.Register("IsIPythonModeEnabled", typeof(bool), typeof(EnvironmentView), new FrameworkPropertyMetadata(OnIsIPythonModeEnabledChanged));

        public bool IsIPythonModeEnabled {
            get { return (bool)GetValue(IsIPythonModeEnabledProperty); }
            set { SetValue(IsIPythonModeEnabledProperty, value); }
        }

        public Action<EnvironmentView, bool> IPythonModeEnabledSetter { get; set; }

        private static void OnIsIPythonModeEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var view = (EnvironmentView)d;
            view.IPythonModeEnabledSetter?.Invoke(view, (bool)e.NewValue);
        }
    }

    public sealed class EnvironmentViewTemplateSelector : DataTemplateSelector {
        public DataTemplate Environment { get; set; }

        public DataTemplate AddNewEnvironment { get; set; }

        public DataTemplate OnlineHelp { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container) {
            var ev = item as EnvironmentView;
            if (ev == null) {
                return base.SelectTemplate(item, container);
            }

            if (EnvironmentView.IsAddNewEnvironmentView(ev) && AddNewEnvironment != null) {
                return AddNewEnvironment;
            }

            if (EnvironmentView.IsOnlineHelpView(ev) && OnlineHelp != null) {
                return OnlineHelp;
            }

            if (Environment != null) {
                return Environment;
            }

            return base.SelectTemplate(item, container);
        }
    }

    public sealed class EnvironmentViewItemContainerSelector : StyleSelector {
        public Style Environment { get; set; }
        public Style OnlineHelp { get; set; }

        public override Style SelectStyle(object item, DependencyObject container) {
            return SelectStyle(item as EnvironmentView)
                ?? container.GetValue(ItemsControl.ItemContainerStyleProperty) as Style
                ?? base.SelectStyle(item, container);
        }

        private Style SelectStyle(EnvironmentView ev) {
            if (ev == null) {
                return null;
            }

            if (EnvironmentView.IsOnlineHelpView(ev) && OnlineHelp != null) {
                return OnlineHelp;
            }

            if (Environment != null) {
                return Environment;
            }

            return null;
        }

    }
}

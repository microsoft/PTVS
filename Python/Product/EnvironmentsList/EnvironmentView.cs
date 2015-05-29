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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.EnvironmentsList {
    public sealed class EnvironmentView : DependencyObject {
        public static readonly RoutedCommand OpenInteractiveWindow = new RoutedCommand();
        public static readonly RoutedCommand OpenInteractiveOptions = new RoutedCommand();
        public static readonly RoutedCommand MakeGlobalDefault = new RoutedCommand();
        public static readonly RoutedCommand MakeActiveInCurrentProject = new RoutedCommand();

        public static readonly Lazy<EnvironmentView> AddNewEnvironmentView =
            new Lazy<EnvironmentView>(() => new EnvironmentView());
        public static readonly Lazy<IEnumerable<EnvironmentView>> AddNewEnvironmentViewOnce =
            new Lazy<IEnumerable<EnvironmentView>>(() => new[] { AddNewEnvironmentView.Value });

        public static readonly Lazy<EnvironmentView> OnlineHelpView =
            new Lazy<EnvironmentView>(() => new EnvironmentView());
        public static readonly Lazy<IEnumerable<EnvironmentView>> OnlineHelpViewOnce =
            new Lazy<IEnumerable<EnvironmentView>>(() => new[] { OnlineHelpView.Value });

        /// <summary>
        /// Used with <see cref="CommonUtils.FindFile"/> to more efficiently
        /// find interpreter executables.
        /// </summary>
        private static readonly string[] _likelyInterpreterPaths = new[] { "Scripts" };

        /// <summary>
        /// Used with <see cref="CommonUtils.FindFile"/> to more efficiently
        /// find interpreter libraries.
        /// </summary>
        private static readonly string[] _likelyLibraryPaths = new[] { "Lib" };

        private readonly IInterpreterOptionsService _service;
        private readonly IPythonInterpreterFactoryWithDatabase _withDb;

        public IPythonInterpreterFactory Factory { get; private set; }

        private EnvironmentView() { }

        internal EnvironmentView(
            IInterpreterOptionsService service,
            IPythonInterpreterFactory factory,
            Redirector redirector
        ) {
            if (service == null) {
                throw new ArgumentNullException("service");
            }
            if (factory == null) {
                throw new ArgumentNullException("factory");
            }

            _service = service;
            Factory = factory;

            _withDb = factory as IPythonInterpreterFactoryWithDatabase;
            if (_withDb != null) {
                _withDb.IsCurrentChanged += Factory_IsCurrentChanged;
                IsCheckingDatabase = _withDb.IsCheckingDatabase;
                IsCurrent = _withDb.IsCurrent;
            }

            var configurableProvider = _service != null ?
                _service.KnownProviders
                    .OfType<ConfigurablePythonInterpreterFactoryProvider>()
                    .FirstOrDefault() :
                null;

            if (configurableProvider != null && configurableProvider.IsConfigurable(factory)) {
                IsConfigurable = true;
            }

            Description = Factory.Description;
            IsDefault = (_service != null && _service.DefaultInterpreter == Factory);

            PrefixPath = Factory.Configuration.PrefixPath;
            InterpreterPath = Factory.Configuration.InterpreterPath;
            WindowsInterpreterPath = Factory.Configuration.WindowsInterpreterPath;
            LibraryPath = Factory.Configuration.LibraryPath;

            Extensions = new ObservableCollection<object>();
            Extensions.Add(new EnvironmentPathsExtensionProvider());
            if (IsConfigurable) {
                Extensions.Add(new ConfigurationExtensionProvider(configurableProvider));
            }

            CanBeDefault = Factory.CanBeDefault();
        }

        public override string ToString() {
            return string.Format(
                "{{{0}:{1}}}", GetType().FullName,
                _withDb == null ? "(null)" : _withDb.Description
            );
        }

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

        private static readonly DependencyPropertyKey DescriptionPropertyKey = DependencyProperty.RegisterReadOnly("Description", typeof(string), typeof(EnvironmentView), new PropertyMetadata());
        private static readonly DependencyPropertyKey PrefixPathPropertyKey = DependencyProperty.RegisterReadOnly("PrefixPath", typeof(string), typeof(EnvironmentView), new PropertyMetadata());
        private static readonly DependencyPropertyKey InterpreterPathPropertyKey = DependencyProperty.RegisterReadOnly("InterpreterPath", typeof(string), typeof(EnvironmentView), new PropertyMetadata());
        private static readonly DependencyPropertyKey WindowsInterpreterPathPropertyKey = DependencyProperty.RegisterReadOnly("WindowsInterpreterPath", typeof(string), typeof(EnvironmentView), new PropertyMetadata());
        private static readonly DependencyPropertyKey LibraryPathPropertyKey = DependencyProperty.RegisterReadOnly("LibraryPath", typeof(string), typeof(EnvironmentView), new PropertyMetadata());
        private static readonly DependencyPropertyKey PathEnvironmentVariablePropertyKey = DependencyProperty.RegisterReadOnly("PathEnvironmentVariable", typeof(string), typeof(EnvironmentView), new PropertyMetadata());

        public static readonly DependencyProperty DescriptionProperty = DescriptionPropertyKey.DependencyProperty;
        public static readonly DependencyProperty PrefixPathProperty = PrefixPathPropertyKey.DependencyProperty;
        public static readonly DependencyProperty InterpreterPathProperty = InterpreterPathPropertyKey.DependencyProperty;
        public static readonly DependencyProperty WindowsInterpreterPathProperty = WindowsInterpreterPathPropertyKey.DependencyProperty;
        public static readonly DependencyProperty LibraryPathProperty = LibraryPathPropertyKey.DependencyProperty;
        public static readonly DependencyProperty PathEnvironmentVariableProperty = PathEnvironmentVariablePropertyKey.DependencyProperty;

        public string Description {
            get { return Factory == null ? string.Empty : (string)GetValue(DescriptionProperty); }
            set { if (Factory != null) { SetValue(DescriptionPropertyKey, value); } }
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

        public string LibraryPath {
            get { return Factory == null ? string.Empty : (string)GetValue(LibraryPathProperty); }
            set { if (Factory != null) { SetValue(LibraryPathPropertyKey, value); } }
        }

        public string PathEnvironmentVariable {
            get { return Factory == null ? string.Empty : (string)GetValue(PathEnvironmentVariableProperty); }
            set { if (Factory != null) { SetValue(PathEnvironmentVariablePropertyKey, value); } }
        }

        #endregion
    }

    public sealed class EnvironmentViewTemplateSelector : DataTemplateSelector {
        public DataTemplate Environment { get; set; }

        public DataTemplate AddNewEnvironment { get; set; }

        public DataTemplate OnlineHelp { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container) {
            if (EnvironmentView.AddNewEnvironmentView.IsValueCreated) {
                if (object.ReferenceEquals(item, EnvironmentView.AddNewEnvironmentView.Value) &&
                    AddNewEnvironment != null) {
                    return AddNewEnvironment;
                }
            }
            if (EnvironmentView.OnlineHelpView.IsValueCreated) {
                if (object.ReferenceEquals(item, EnvironmentView.OnlineHelpView.Value) &&
                    OnlineHelp != null) {
                    return OnlineHelp;
                }
            }
            if (item is EnvironmentView && Environment != null) {
                return Environment;
            }
            return base.SelectTemplate(item, container);
        }
    }
}

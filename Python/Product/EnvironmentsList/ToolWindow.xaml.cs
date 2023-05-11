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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Logging;
using Microsoft.VisualStudio.ComponentModelHost;

namespace Microsoft.PythonTools.EnvironmentsList {
    public partial class ToolWindow : UserControl {
        internal readonly ObservableCollection<EnvironmentView> _environments;
        internal readonly ObservableCollection<object> _extensions;

        private readonly CollectionViewSource _environmentsView, _extensionsView;
        private IInterpreterRegistryService _interpreters;
        private IInterpreterOptionsService _options;
        private IServiceProvider _site;

        public static readonly RoutedCommand UnhandledException = new RoutedCommand();
        private static readonly object[] EmptyObjectArray = new object[0];

        public ToolWindow() {
            _environments = new ObservableCollection<EnvironmentView>();
            _extensions = new ObservableCollection<object>();
            _environmentsView = new CollectionViewSource { Source = _environments };
            _extensionsView = new CollectionViewSource { Source = _extensions };
            _extensionsView.SortDescriptions.Add(new SortDescription("SortPriority", ListSortDirection.Ascending));
            _extensionsView.SortDescriptions.Add(new SortDescription("LocalizedDisplayName", ListSortDirection.Ascending));
            _environmentsView.View.CurrentChanged += EnvironmentsView_CurrentChanged;
            if (_interpreters != null) {
                _interpreters.CondaInterpreterDiscoveryCompleted += OnCondaInterpreterDiscoveryCompleted;
            }
            DataContext = this;
            InitializeComponent();
            SizeChanged += ToolWindow_SizeChanged;
        }

            private void OnCondaInterpreterDiscoveryCompleted(object sender, EventArgs e) {
                UpdateEnvironments();
            }

            private void EnvironmentsView_CurrentChanged(object sender, EventArgs e) {
            var item = _environmentsView.View.CurrentItem as EnvironmentView;
            if (item == null) {
                lock (_extensions) {
                    _extensions.Clear();
                }
                return;
            }
            OnViewSelected(item);
            var oldSelect = _extensionsView.View.CurrentItem?.GetType();
            var newSelect = oldSelect == null ? null :
                item.Extensions?.FirstOrDefault(ext => ext != null && ext.GetType().IsEquivalentTo(oldSelect));
            lock (_extensions) {
                _extensions.Clear();
                foreach (var ext in item.Extensions.MaybeEnumerate()) {
                    _extensions.Add(ext);
                }
                if (newSelect != null) {
                    _extensionsView.View.MoveCurrentTo(newSelect);
                } else {
                    _extensionsView.View.MoveCurrentToFirst();
                }
            }
        }

        public IServiceProvider Site {
            get {
                return _site;
            }
            set {
                _site = value;
                if (value != null) {
                    var compModel = _site.GetService(typeof(SComponentModel)) as IComponentModel;
                    InitializeEnvironments(compModel.GetService<IInterpreterRegistryService>(), compModel.GetService<IInterpreterOptionsService>());
                } else {
                    InitializeEnvironments(null, null);
                }
            }
        }

        internal IPythonToolsLogger TelemetryLogger { get; set; }

        internal static async void SendUnhandledException(UIElement element, ExceptionDispatchInfo edi) {
            try {
                await element.Dispatcher.InvokeAsync(() => {
                    UnhandledException.Execute(edi, element);
                });
            } catch (InvalidOperationException) {
                UnhandledException.Execute(edi, element);
            }
        }

        void ToolWindow_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (!e.WidthChanged) {
                return;
            }

            UpdateLayout(e.NewSize.Width, e.NewSize.Height);
        }

        void UpdateLayout(double width, double height) {
            if (double.IsNaN(width) || double.IsNaN(height)) {
                return;
            }

            if (width < 500) {
                SwitchToVerticalLayout();
            } else if (width >= 600) {
                SwitchToHorizontalLayout();
            } else if (VerticalLayout.Visibility != Visibility.Visible) {
                SwitchToVerticalLayout();
            }
        }

        void SwitchToHorizontalLayout() {
            if (HorizontalLayout.Visibility == Visibility.Visible) {
                return;
            }
            VerticalLayout.Visibility = Visibility.Collapsed;
            BindingOperations.ClearBinding(ContentView_Vertical, ContentControl.ContentProperty);
            BindingOperations.SetBinding(ContentView_Horizontal, ContentControl.ContentProperty, new Binding {
                Path = new PropertyPath("CurrentItem.WpfObject"),
                Source = Extensions
            });
            HorizontalLayout.Visibility = Visibility.Visible;
            UpdateLayout();
        }

        void SwitchToVerticalLayout() {
            if (VerticalLayout.Visibility == Visibility.Visible) {
                return;
            }
            HorizontalLayout.Visibility = Visibility.Collapsed;
            BindingOperations.ClearBinding(ContentView_Horizontal, ContentControl.ContentProperty);
            BindingOperations.SetBinding(ContentView_Vertical, ContentControl.ContentProperty, new Binding {
                Path = new PropertyPath("CurrentItem.WpfObject"),
                Source = Extensions
            });
            VerticalLayout.Visibility = Visibility.Visible;
            UpdateLayout();
        }

        public ICollectionView Environments => _environmentsView.View;
        public ICollectionView Extensions => _extensionsView.View;

        private void MakeGlobalDefault_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var view = e.Parameter as EnvironmentView;
            e.CanExecute = view != null && !view.IsDefault && view.Factory.CanBeDefault();
        }

        private void MakeGlobalDefault_Executed(object sender, ExecutedRoutedEventArgs e) {
            _options.DefaultInterpreter = ((EnvironmentView)e.Parameter).Factory;
        }

        private void UpdateEnvironments(string select = null) {
            if (_options == null) {
                lock (_environments) {
                    _environments.Clear();
                }
                return;
            }

            lock (_environments) {
                if (select == null) {
                    var selectView = _environmentsView.View.CurrentItem as EnvironmentView;
                    select = selectView?.Configuration?.Id;
                }

                // We allow up to three retries at this process to handle race
                // conditions where an interpreter disappears between the
                // point where we enumerate known configuration IDs and convert
                // them into a view. The first time that happens, we insert a
                // stub entry, and on the subsequent pass it will be removed.
                bool anyMissing = true;
                for (int retries = 3; retries > 0 && anyMissing; --retries) {
                    var configs = _interpreters.Configurations.Where(f => f.IsUIVisible()).ToList();

                    anyMissing = false;
                    _environments.Merge(
                        configs,
                        ev => ev.Configuration,
                        c => c,
                        c => {
                            var fact = _interpreters.FindInterpreter(c.Id);
                            EnvironmentView view = null;
                            try {
                                if (fact != null) {
                                    view = new EnvironmentView(_options, _interpreters, fact, null);
                                }
                            } catch (ArgumentException) {
                            }
                            if (view == null) {
                                view = EnvironmentView.CreateMissingEnvironmentView(c.Id + "+Missing", c.Description);
                                anyMissing = true;
                            }
                            OnViewCreated(view);
                            return view;
                        },
                        InterpreterConfigurationComparer.Instance,
                        InterpreterConfigurationComparer.Instance
                    );
                }

                if (select != null) {
                    var selectView = _environments.FirstOrDefault(v => v.Factory != null &&
                        v.Factory.Configuration.Id == select);
                    if (selectView == null) {
                        select = null;
                    } else {
                        _environmentsView.View.MoveCurrentTo(selectView);
                    }
                }
                if (select == null) {
                    var defaultView = _environments.FirstOrDefault(v => v.IsDefault);
                    if (defaultView != null && _environmentsView.View.CurrentItem == null) {
                        _environmentsView.View.MoveCurrentTo(defaultView);
                    }
                }
            }
        }

        private void FirstUpdateEnvironments() {
            UpdateEnvironments();

            UpdateLayout();
            UpdateLayout(ActualWidth, ActualHeight);
        }

        private void OnViewCreated(EnvironmentView view) {
            ViewCreated?.Invoke(this, new EnvironmentViewEventArgs(view));
        }

        internal void OnViewSelected(EnvironmentView view) {
            ViewSelected?.Invoke(this, new EnvironmentViewEventArgs(view));
        }

        public event EventHandler<EnvironmentViewEventArgs> ViewCreated;
        public event EventHandler<EnvironmentViewEventArgs> ViewSelected;

        internal IInterpreterOptionsService OptionsService => _options;

        public void InitializeEnvironments(IInterpreterRegistryService interpreters, IInterpreterOptionsService options, bool synchronous = false) {
            if (_interpreters != null) {
                _interpreters.InterpretersChanged -= Service_InterpretersChanged;
            }
            _interpreters = interpreters;
            if (_interpreters != null) {
                _interpreters.InterpretersChanged += Service_InterpretersChanged;
            }

            if (_options != null) {
                _options.DefaultInterpreterChanged -= Service_DefaultInterpreterChanged;
            }
            _options = options;
            if (_options != null) {
                _options.DefaultInterpreterChanged += Service_DefaultInterpreterChanged;
            }

            if (_interpreters != null && _options != null) {
                if (synchronous) {
                    Dispatcher.Invoke(FirstUpdateEnvironments);
                } else {
                    Dispatcher.InvokeAsync(FirstUpdateEnvironments).Task.DoNotWait();
                }
            }
        }

        private async void Service_InterpretersChanged(object sender, EventArgs e) {
            try {
                await Dispatcher.InvokeAsync(() => UpdateEnvironments());
            } catch (OperationCanceledException) {
            }
        }

        private async void Service_DefaultInterpreterChanged(object sender, EventArgs e) {
            var newDefault = _options.DefaultInterpreter;
            try {
                await Dispatcher.InvokeAsync(() => {
                    lock (_environments) {
                        foreach (var view in _environments) {
                            if (view.Factory == newDefault) {
                                view.IsDefault = true;
                            } else if (view.IsDefault == true) {
                                view.IsDefault = false;
                            }
                        }
                    }
                });
            } catch (OperationCanceledException) {
            }
        }

        private void ConfigurableViewAdded_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = e.Parameter is string;
            e.Handled = true;
        }

        private async void ConfigurableViewAdded_Executed(object sender, ExecutedRoutedEventArgs e) {
            var id = (string)e.Parameter;
            e.Handled = true;

            for (int retries = 10; retries > 0; --retries) {
                var env = _environments.FirstOrDefault(ev => ev.Factory?.Configuration?.Id == id);
                if (env != null) {
                    Environments.MoveCurrentTo(env);
                    return;
                }
                await Task.Delay(50).ConfigureAwait(continueOnCapturedContext: true);
            }
            Debug.Fail("Failed to switch to added environment");
        }

        private void ConfigureEnvironment_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = (e.Parameter as EnvironmentView)?.IsConfigurable ?? false;
            e.Handled = true;
        }

        private void ConfigureEnvironment_Executed(object sender, ExecutedRoutedEventArgs e) {
            e.Handled = true;

            var env = (EnvironmentView)e.Parameter;
            if (Environments.CurrentItem != env) {
                Environments.MoveCurrentTo(env);
                if (Environments.CurrentItem != env) {
                    return;
                }
            }
            var ext = env.Extensions.OfType<ConfigurationExtensionProvider>().FirstOrDefault();
            if (ext != null) {
                Extensions.MoveCurrentTo(ext);
            }
        }

        private void EnvironmentsList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            var list = sender as ListBox;
            if (list != null && e.AddedItems.Count > 0) {
                list.ScrollIntoView(e.AddedItems[0]);
                e.Handled = true;
            }
        }

        private void OnlineHelpListItem_GotFocus(object sender, RoutedEventArgs e) {
            for (var child = sender as DependencyObject;
                child != null;
                child = VisualTreeHelper.GetParent(child)
            ) {
                var lbi = child as ListBoxItem;
                if (lbi != null) {
                    lbi.IsSelected = true;
                    return;
                }
            }
        }

        private class InterpreterConfigurationComparer : IEqualityComparer<InterpreterConfiguration>, IComparer<InterpreterConfiguration> {
            public static readonly InterpreterConfigurationComparer Instance = new InterpreterConfigurationComparer();

            public bool Equals(InterpreterConfiguration x, InterpreterConfiguration y) => x == y;
            public int GetHashCode(InterpreterConfiguration obj) => obj.GetHashCode();

            public int Compare(InterpreterConfiguration x, InterpreterConfiguration y) {
                if (object.ReferenceEquals(x, y)) {
                    return 0;
                }
                if (x == null) {
                    return y == null ? 0 : 1;
                } else if (y == null) {
                    return -1;
                }

                int result = StringComparer.CurrentCultureIgnoreCase.Compare(
                    x.Description,
                    y.Description
                );

                if (result == 0) {
                    result = StringComparer.Ordinal.Compare(
                        x.Id,
                        y.Id
                    );
                }

                return result;
            }
        }
    }

    /// <summary>
    /// Contains the newly created view.
    /// </summary>
    public class EnvironmentViewEventArgs : EventArgs {
        public EnvironmentViewEventArgs(EnvironmentView view) {
            View = view;
        }
        
        public EnvironmentView View { get; private set; }
    }
}

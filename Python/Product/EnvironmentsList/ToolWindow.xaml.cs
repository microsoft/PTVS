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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Logging;
using Microsoft.VisualStudio.ComponentModelHost;

namespace Microsoft.PythonTools.EnvironmentsList {
    public partial class ToolWindow : UserControl, IDisposable {
        internal readonly ObservableCollection<EnvironmentView> _environments;
        internal readonly ObservableCollection<object> _extensions;

        private readonly CollectionViewSource _environmentsView, _extensionsView;
        private IInterpreterRegistryService _interpreters;
        private IInterpreterOptionsService _service;
        private IServiceProvider _site;

        private EnvironmentView _addNewEnvironmentView;
        private IEnumerable<EnvironmentView> _addNewEnvironmentViewOnce;

        private AnalyzerStatusListener _listener;
        private readonly object _listenerLock = new object();
        private int _listenerTimeToLive;
        const int _listenerDefaultTimeToLive = 120;

        // lock(_environments) when accessing _currentlyRefreshing
        private readonly Dictionary<IPythonInterpreterFactory, AnalysisProgress> _currentlyRefreshing;

        private bool _isDisposed;

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
            _currentlyRefreshing = new Dictionary<IPythonInterpreterFactory, AnalysisProgress>();
            DataContext = this;
            InitializeComponent();
            CreateListener();
            SizeChanged += ToolWindow_SizeChanged;
        }

        private void EnvironmentsView_CurrentChanged(object sender, EventArgs e) {
            var item = _environmentsView.View.CurrentItem as EnvironmentView;
            if (item == null) {
                lock (_extensions) {
                    _extensions.Clear();
                }
                return;
            }
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
                    Service = compModel.GetService<IInterpreterOptionsService>();
                    Interpreters = compModel.GetService<IInterpreterRegistryService>();
                    _addNewEnvironmentView = EnvironmentView.CreateAddNewEnvironmentView(Service);
                    _addNewEnvironmentViewOnce = new[] { _addNewEnvironmentView };
                } else {
                    Service = null;
                    Interpreters = null;
                    _addNewEnvironmentView = null;
                    _addNewEnvironmentViewOnce = null;
                }
            }
        }

        public IPythonToolsLogger TelemetryLogger { get; set; }

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


        private void CreateListener() {
            lock (_listenerLock) {
                var oldListener = _listener;
                if (oldListener != null) {
                    oldListener.ThrowPendingExceptions();
                    oldListener.Dispose();
                }
                var newListener = new AnalyzerStatusListener(Listener_ProgressUpdate, TimeSpan.FromMilliseconds(250));
                newListener.ThrowPendingExceptions();
                _listener = newListener;
                _listenerTimeToLive = _listenerDefaultTimeToLive;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ToolWindow() {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                _isDisposed = true;

                if (disposing) {
                    lock (_listenerLock) {
                        if (_listener != null) {
                            _listener.Dispose();
                            _listener = null;
                        }
                    }
                }
            }
        }

        public ICollectionView Environments => _environmentsView.View;
        public ICollectionView Extensions => _extensionsView.View;

        private void MakeGlobalDefault_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var view = e.Parameter as EnvironmentView;
            e.CanExecute = view != null && !view.IsDefault && view.Factory.CanBeDefault();
        }

        private void MakeGlobalDefault_Executed(object sender, ExecutedRoutedEventArgs e) {
            _service.DefaultInterpreter = ((EnvironmentView)e.Parameter).Factory;
        }

        private async void Listener_ProgressUpdate(Dictionary<string, AnalysisProgress> status) {
            bool anyUpdates = status.Any();
            if (!anyUpdates) {
                lock (_environments) {
                    anyUpdates = _currentlyRefreshing.Count != 0;
                }
            }

            if (anyUpdates) {
                var updates = new List<DispatcherOperation>();

                lock (_environments) {
                    foreach (var env in _environments) {
                        if (env.Factory == null) {
                            continue;
                        }

                        AnalysisProgress progress;
                        if (status.TryGetValue(AnalyzerStatusUpdater.GetIdentifier(env.Factory), out progress)) {
                            _currentlyRefreshing[env.Factory] = progress;

                            updates.Add(env.Dispatcher.InvokeAsync(() => {
                                if (progress.Maximum > 0) {
                                    var percent = progress.Progress * 100 / progress.Maximum;
                                    var current = env.RefreshDBProgress;
                                    // Filter out small instances of 'reverse'
                                    // progress, but allow big jumps backwards.
                                    if (percent > current || percent < current - 25) {
                                        env.RefreshDBProgress = percent;
                                    }
                                    env.IsRefreshDBProgressIndeterminate = false;
                                } else {
                                    env.IsRefreshDBProgressIndeterminate = true;
                                }
                                env.RefreshDBMessage = progress.Message;
                                env.IsRefreshingDB = true;
                            }));
                        } else if (_currentlyRefreshing.TryGetValue(env.Factory, out progress)) {
                            _currentlyRefreshing.Remove(env.Factory);
                            try {
                                TelemetryLogger?.LogEvent(PythonLogEvent.AnalysisCompleted, new AnalysisInfo {
                                    InterpreterId = env.Factory.Configuration.Id,
                                    AnalysisSeconds = progress.Seconds
                                });
                            } catch (Exception ex) {
                                Debug.Fail(ex.ToUnhandledExceptionMessage(GetType()));
                            }
                            updates.Add(env.Dispatcher.InvokeAsync(() => {
                                env.IsRefreshingDB = false;
                                env.IsRefreshDBProgressIndeterminate = false;
                                env.RefreshDBMessage = string.Empty;
                                CommandManager.InvalidateRequerySuggested();
                            }));
                        }
                    }
                }

                try {
                    await Task.WhenAll(updates.Select(d => d.Task).ToArray());
                } catch (OperationCanceledException) {
                    // Tasks were cancelled, which probably means we are closing.
                    // In this case, _timer will be disposed before the next update.
                }
            }

            if (Interlocked.Decrement(ref _listenerTimeToLive) == 0) {
                // It's time to reset the listener. We do this periodically in
                // case the global mutex has become abandoned. By releasing our
                // handle, it should go away and any errors (which may be caused
                // by users killing the analyzer) become transient rather than
                // permanent.

                // Because we are currently on the listener's thread, we need to
                // recreate on a separate thread so that this one can terminate.
                Task.Run((Action)CreateListener)
                    .HandleAllExceptions(Site, GetType())
                    .DoNotWait();
            }
        }

        private void StartRefreshDB_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var view = e.Parameter as EnvironmentView;
            var factory = view == null ? null : view.Factory as IPythonInterpreterFactoryWithDatabase;
            e.CanExecute = factory != null &&
                !view.IsRefreshingDB &&
                File.Exists(factory.Configuration.InterpreterPath);
        }

        private async void StartRefreshDB_Executed(object sender, ExecutedRoutedEventArgs e) {
            var view = (EnvironmentView)e.Parameter;
            await StartRefreshDBAsync(view)
                .SilenceException<OperationCanceledException>()
                .HandleAllExceptions(Site, GetType());
        }

        private async Task StartRefreshDBAsync(EnvironmentView view) {
            view.IsRefreshingDB = true;
            view.IsRefreshDBProgressIndeterminate = true;

            var tcs = new TaskCompletionSource<int>();
            ((IPythonInterpreterFactoryWithDatabase)view.Factory).GenerateDatabase(
                Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ?
                    GenerateDatabaseOptions.None :
                    GenerateDatabaseOptions.SkipUnchanged,
                tcs.SetResult
            );
            await tcs.Task;

            // Ensure that the factory is added to the list of those currently
            // being refreshed. This ensures that if the task completes before
            // an asynchronous update arrives, we will still reset the state.
            // If an update has arrived, this causes a benign refresh of the
            // command state.
            lock (_environments) {
                if (!_currentlyRefreshing.ContainsKey(view.Factory)) {
                    _currentlyRefreshing[view.Factory] = default(AnalysisProgress);
                }
            }
        }

        private void UpdateEnvironments(string select = null) {
            if (_service == null) {
                lock (_environments) {
                    _environments.Clear();
                }
                return;
            }

            lock (_environments) {
                if (select == null) {
                    var selectView = _environmentsView.View.CurrentItem as EnvironmentView;
                    select = selectView?.Factory?.Configuration.Id;
                }

                _environments.Merge(
                    _interpreters.Interpreters
                    .Where(f => f.IsUIVisible())
                    .Select(f => {
                        var view = new EnvironmentView(_service, _interpreters, f, null);
                        OnViewCreated(view);
                        return view;
                    })
                    .Concat(_addNewEnvironmentViewOnce ?? Enumerable.Empty<EnvironmentView>())
                    .Concat(EnvironmentView.OnlineHelpViewOnce.Value),
                    EnvironmentComparer.Instance,
                    EnvironmentComparer.Instance
                );

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

        public event EventHandler<EnvironmentViewEventArgs> ViewCreated;

        public IInterpreterRegistryService Interpreters {
            get {
                return _interpreters;
            }
            set {
                if (_interpreters != null) {
                    _interpreters.InterpretersChanged -= Service_InterpretersChanged;
                }
                _interpreters = value;
                if (_service != null) {
                    _interpreters.InterpretersChanged += Service_InterpretersChanged;
                }
                if (_service != null) {
                    Dispatcher.InvokeAsync(FirstUpdateEnvironments).Task.DoNotWait();
                }
            }
        }

        public IInterpreterOptionsService Service {
            get {
                return _service;
            }
            set {
                if (_service != null) {
                    _service.DefaultInterpreterChanged -= Service_DefaultInterpreterChanged;
                }
                _service = value;
                if (_service != null) {
                    _service.DefaultInterpreterChanged += Service_DefaultInterpreterChanged;
                }
                if (_interpreters != null) {
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
            var newDefault = _service.DefaultInterpreter;
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

        class EnvironmentComparer : IEqualityComparer<EnvironmentView>, IComparer<EnvironmentView> {
            public static readonly EnvironmentComparer Instance = new EnvironmentComparer();

            public bool Equals(EnvironmentView x, EnvironmentView y) {
                return object.ReferenceEquals(x, y) || (
                    x.Factory != null && x.Factory.Configuration != null &&
                    y.Factory != null && y.Factory.Configuration != null &&
                    x.Factory.Configuration.Id == y.Factory.Configuration.Id
                );
            }

            public int GetHashCode(EnvironmentView obj) {
                return obj.Factory != null ? obj.Factory.GetHashCode() : 0;
            }

            public int Compare(EnvironmentView x, EnvironmentView y) {
                if (object.ReferenceEquals(x, y)) {
                    return 0;
                }

                if (x != null && x._addNewEnvironmentView) {
                    return 1;
                } else if (y != null && y._addNewEnvironmentView) {
                    return -1;
                }
                if (EnvironmentView.OnlineHelpView.IsValueCreated) {
                    if (object.ReferenceEquals(x, EnvironmentView.OnlineHelpView.Value)) {
                        return 1;
                    } else if (object.ReferenceEquals(y, EnvironmentView.OnlineHelpView.Value)) {
                        return -1;
                    }
                }

                int result = StringComparer.CurrentCultureIgnoreCase.Compare(
                    x.Description,
                    y.Description
                );

                if (result == 0) {
                    // Any missing information means not equal, so we need to
                    // pick a winner. We arbitrarily sort the non-null entry
                    // first, or x if they both have nulls.
                    if (y.Factory?.Configuration?.Id == null) {
                        result = -1;
                    } else if (x.Factory?.Configuration?.Id == null) {
                        result = 1;
                    } else {
                        result = StringComparer.Ordinal.Compare(
                            x.Factory.Configuration.Id,
                            y.Factory.Configuration.Id
                        );
                    }
                }

                return result;
            }
        }
    }

    public class EnvironmentViewEventArgs : EventArgs {
        public EnvironmentViewEventArgs(EnvironmentView view) {
            View = view;
        }
        
        public EnvironmentView View { get; private set; }
    }
}

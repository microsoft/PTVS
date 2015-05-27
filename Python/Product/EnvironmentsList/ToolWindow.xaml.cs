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
using System.ComponentModel;
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
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.EnvironmentsList {
    public partial class ToolWindow : UserControl, IDisposable {
        internal readonly ObservableCollection<EnvironmentView> _environments;
        private readonly object _environmentsLock = new object();

        private readonly CollectionViewSource _environmentsView;
        private readonly HashSet<IPythonInterpreterFactory> _currentlyRefreshing;
        private IInterpreterOptionsService _service;
        
        private AnalyzerStatusListener _listener;
        private readonly object _listenerLock = new object();
        private int _listenerTimeToLive;
        const int _listenerDefaultTimeToLive = 120;
        
        private bool _isDisposed;

        public static readonly RoutedCommand UnhandledException = new RoutedCommand();

        public ToolWindow() {
            _environments = new ObservableCollection<EnvironmentView>();
            _environmentsView = new CollectionViewSource { Source = _environments };
            _currentlyRefreshing = new HashSet<IPythonInterpreterFactory>();
            DataContext = this;
            InitializeComponent();
            CreateListener();
            SizeChanged += ToolWindow_SizeChanged;
        }

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
            
            if (width <= height * 0.9 || width < 400) {
                SwitchToVerticalLayout();
            } else if (width >= height * 1.1) {
                SwitchToHorizontalLayout();
            }
        }

        void SwitchToHorizontalLayout() {
            if (HorizontalLayout.Visibility == Visibility.Visible) {
                return;
            }
            VerticalLayout.Visibility = Visibility.Collapsed;
            BindingOperations.ClearBinding(ContentView_Vertical, ContentControl.ContentProperty);
            BindingOperations.SetBinding(ContentView_Horizontal, ContentControl.ContentProperty, new Binding {
                Path=new PropertyPath("CurrentItem.WpfObject"),
                Source=FindResource("SortedExtensions")
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
                Source = FindResource("SortedExtensions")
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

        public ICollectionView Environments {
            get { return _environmentsView.View; }
        }

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
                lock (_environmentsLock) {
                    anyUpdates = _currentlyRefreshing.Count != 0;
                }
            }

            if (anyUpdates) {
                var updates = new List<DispatcherOperation>();

                lock (_environmentsLock) {
                    foreach (var env in _environments) {
                        if (env.Factory == null) {
                            continue;
                        }

                        AnalysisProgress progress;
                        if (status.TryGetValue(AnalyzerStatusUpdater.GetIdentifier(env.Factory), out progress)) {
                            _currentlyRefreshing.Add(env.Factory);

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
                        } else if (_currentlyRefreshing.Remove(env.Factory)) {
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
                    .HandleAllExceptions(Properties.Resources.PythonToolsForVisualStudio)
                    .DoNotWait();
            }
        }

        private void StartRefreshDB_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var view = e.Parameter as EnvironmentView;
            var factory = view == null ? null : view.Factory as IPythonInterpreterFactoryWithDatabase;
            e.CanExecute = factory != null &&
                !view.IsRefreshingDB &&
                File.Exists(factory.Configuration.InterpreterPath) &&
                Directory.Exists(factory.Configuration.LibraryPath);
        }

        private async void StartRefreshDB_Executed(object sender, ExecutedRoutedEventArgs e) {
            var view = (EnvironmentView)e.Parameter;
            await StartRefreshDBAsync(view)
                .SilenceException<OperationCanceledException>()
                .HandleAllExceptions(Properties.Resources.PythonToolsForVisualStudio);
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
            lock (_environmentsLock) {
                _currentlyRefreshing.Add(view.Factory);
            }
        }

        private void UpdateEnvironments(IPythonInterpreterFactory select = null) {
            if (_service == null) {
                lock (_environmentsLock) {
                    _environments.Clear();
                }
                return;
            }

            lock (_environmentsLock) {
                if (select == null) {
                    var selectView = _environmentsView.View.CurrentItem as EnvironmentView;
                    if (selectView != null) {
                        select = selectView.Factory;
                    }
                }

                _environments.Merge(
                    _service.Interpreters
                    .Distinct()
                    .Where(f => f.IsUIVisible())
                    .Select(f => {
                        var view = new EnvironmentView(_service, f, null);
                        OnViewCreated(view);
                        return view;
                    })
                    .Concat(EnvironmentView.AddNewEnvironmentViewOnce.Value)
                    .Concat(EnvironmentView.OnlineHelpViewOnce.Value),
                    EnvironmentComparer.Instance,
                    EnvironmentComparer.Instance
                );

                if (select != null) {
                    var selectView = _environments.FirstOrDefault(v => v.Factory != null &&
                        v.Factory.Id == select.Id && v.Factory.Configuration.Version == select.Configuration.Version);
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
            var evt = ViewCreated;
            if (evt != null) {
                evt(this, new EnvironmentViewEventArgs(view));
            }
        }

        public event EventHandler<EnvironmentViewEventArgs> ViewCreated;

        public IInterpreterOptionsService Service {
            get {
                return _service;
            }
            set {
                if (_service != null) {
                    _service.DefaultInterpreterChanged -= Service_DefaultInterpreterChanged;
                    _service.InterpretersChanged -= Service_InterpretersChanged;
                }
                _service = value;
                if (_service != null) {
                    _service.DefaultInterpreterChanged += Service_DefaultInterpreterChanged;
                    _service.InterpretersChanged += Service_InterpretersChanged;
                }
                Dispatcher.InvokeAsync(FirstUpdateEnvironments).Task.DoNotWait();
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
                    lock (_environmentsLock) {
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

        private void AddCustomEnvironment_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            if (_service == null) {
                e.CanExecute = false;
                e.Handled = true;
                return;
            }

            var configurable = _service.KnownProviders
                .OfType<ConfigurablePythonInterpreterFactoryProvider>()
                .FirstOrDefault();

            if (configurable == null) {
                e.CanExecute = false;
                e.Handled = true;
                return;
            }

            e.CanExecute = true;
            // Not handled, in case another handler wants to suppress
            return;
        }

        private async void AddCustomEnvironment_Executed(object sender, ExecutedRoutedEventArgs e) {
            var configurable = _service == null ? null : _service.KnownProviders
                .OfType<ConfigurablePythonInterpreterFactoryProvider>()
                .FirstOrDefault();
            
            if (configurable == null) {
                return;
            }

            const string fmt = "New Environment {0}";
            HashSet<string> names;
            lock (_environmentsLock) {
                names = new HashSet<string>(_environments
                    .Where(view => view.Factory != null)
                    .Select(view => view.Factory.Description)
                );
            }
            var name = string.Format(fmt, 1);
            for (int i = 2; names.Contains(name) && i < int.MaxValue; ++i) {
                name = string.Format(fmt, i);
            }

            var factory = configurable.SetOptions(new InterpreterFactoryCreationOptions {
                Id = Guid.NewGuid(),
                Description = name
            });

            UpdateEnvironments(factory);

            await Dispatcher.InvokeAsync(() => {
                var coll = TryFindResource("SortedExtensions") as CollectionViewSource;
                if (coll != null) {
                    var select = coll.View.OfType<ConfigurationExtensionProvider>().FirstOrDefault();
                    if (select != null) {
                        coll.View.MoveCurrentTo(select);
                    }
                }
            }, DispatcherPriority.Normal);
        }

        class EnvironmentComparer : IEqualityComparer<EnvironmentView>, IComparer<EnvironmentView> {
            public static readonly EnvironmentComparer Instance = new EnvironmentComparer();

            public bool Equals(EnvironmentView x, EnvironmentView y) {
                return object.ReferenceEquals(x, y) || (
                    x.Factory != null && x.Factory.Configuration != null &&
                    y.Factory != null && y.Factory.Configuration != null &&
                    x.Factory.Id == y.Factory.Id &&
                    x.Factory.Configuration.Version == y.Factory.Configuration.Version
                );
            }

            public int GetHashCode(EnvironmentView obj) {
                return obj.Factory != null ? obj.Factory.GetHashCode() : 0;
            }

            public int Compare(EnvironmentView x, EnvironmentView y) {
                if (object.ReferenceEquals(x, y)) {
                    return 0;
                }

                if (EnvironmentView.AddNewEnvironmentView.IsValueCreated) {
                    if (object.ReferenceEquals(x, EnvironmentView.AddNewEnvironmentView.Value)) {
                        return 1;
                    } else if (object.ReferenceEquals(y, EnvironmentView.AddNewEnvironmentView.Value)) {
                        return -1;
                    }
                }
                if (EnvironmentView.OnlineHelpView.IsValueCreated) {
                    if (object.ReferenceEquals(x, EnvironmentView.OnlineHelpView.Value)) {
                        return 1;
                    } else if (object.ReferenceEquals(y, EnvironmentView.OnlineHelpView.Value)) {
                        return -1;
                    }
                }

                return StringComparer.CurrentCultureIgnoreCase.Compare(
                    x.Description,
                    y.Description
                );
            }
        }

        private void EnvironmentsList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            var list = sender as ListBox;
            if (list != null && e.AddedItems.Count > 0) {
                list.ScrollIntoView(e.AddedItems[0]);
                e.Handled = true;
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

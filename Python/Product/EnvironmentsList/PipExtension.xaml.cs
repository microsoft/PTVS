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
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.PythonTools.EnvironmentsList.Properties;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.EnvironmentsList {
    internal sealed partial class PipExtension : UserControl {
        public static readonly ICommand InstallPackage = new RoutedCommand();
        public static readonly ICommand UpgradePackage = new RoutedCommand();
        public static readonly ICommand UninstallPackage = new RoutedCommand();
        public static readonly ICommand InstallPip = new RoutedCommand();

        private readonly PipExtensionProvider _provider;

        public PipExtension(PipExtensionProvider provider) {
            _provider = provider;
            DataContextChanged += PackageExtension_DataContextChanged;
            InitializeComponent();
        }

        private void PackageExtension_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            var view = e.NewValue as EnvironmentView;
            if (view != null) {
                var current = Subcontext.DataContext as PipEnvironmentView;
                if (current == null || current.EnvironmentView != view) {
                    if (current != null) {
                        current.Dispose();
                    }
                    Subcontext.DataContext = new PipEnvironmentView(view, _provider);
                }
            }
        }

        private void UninstallPackage_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = e.Parameter is PipPackageView;
        }

        private async void UninstallPackage_Executed(object sender, ExecutedRoutedEventArgs e) {
            try {
                var view = (PipPackageView)e.Parameter;
                await _provider.UninstallPackage(view.PackageSpec);
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                if (ErrorHandler.IsCriticalException(ex)) {
                    throw;
                }
                ToolWindow.SendUnhandledException(this, ExceptionDispatchInfo.Capture(ex));
            }
        }

        private void UpgradePackage_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            if (!_provider.CanExecute) {
                e.CanExecute = false;
                e.Handled = true;
                return;
            }
            
            var view = e.Parameter as PipPackageView;
            if (view == null) {
                e.CanExecute = false;
                e.Handled = true;
                return;
            }

            e.CanExecute = !view.UpgradeVersion.IsEmpty && view.UpgradeVersion.CompareTo(view.Version) > 0;
            e.Handled = true;
        }

        private async void UpgradePackage_Executed(object sender, ExecutedRoutedEventArgs e) {
            try {
                var view = (PipPackageView)e.Parameter;
                // Provide Name, not PackageSpec, or we'll upgrade to our
                // current version.
                await _provider.InstallPackage(view.Name, true);
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                if (ErrorHandler.IsCriticalException(ex)) {
                    throw;
                }
                ToolWindow.SendUnhandledException(this, ExceptionDispatchInfo.Capture(ex));
            }
        }

        private void InstallPackage_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = _provider.CanExecute && !string.IsNullOrEmpty(e.Parameter as string);
            e.Handled = true;
        }

        private async void InstallPackage_Executed(object sender, ExecutedRoutedEventArgs e) {
            try {
                await _provider.InstallPackage((string)e.Parameter, true);
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                if (ErrorHandler.IsCriticalException(ex)) {
                    throw;
                }
                ToolWindow.SendUnhandledException(this, ExceptionDispatchInfo.Capture(ex));
            }
        }

        private void InstallPip_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = _provider.CanExecute;
            e.Handled = true;
        }

        private async void InstallPip_Executed(object sender, ExecutedRoutedEventArgs e) {
            try {
                await _provider.InstallPip();
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                if (ErrorHandler.IsCriticalException(ex)) {
                    throw;
                }
                ToolWindow.SendUnhandledException(this, ExceptionDispatchInfo.Capture(ex));
            }
        }

        private void ForwardMouseWheel(object sender, MouseWheelEventArgs e) {
            PackagesList.RaiseEvent(new MouseWheelEventArgs(
                e.MouseDevice,
                e.Timestamp,
                e.Delta
            ) { RoutedEvent = UIElement.MouseWheelEvent });
            e.Handled = true;
        }

        private void Delete_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var tb = e.OriginalSource as TextBox;
            if (tb != null) {
                e.Handled = true;
                e.CanExecute = !string.IsNullOrEmpty(tb.Text);
                return;
            }
        }

        private void Delete_Executed(object sender, ExecutedRoutedEventArgs e) {
            var tb = e.OriginalSource as TextBox;
            if (tb != null) {
                tb.Clear();
                e.Handled = true;
                return;
            }
        }
    }

    sealed class PipEnvironmentView : DependencyObject, IDisposable {
        private readonly EnvironmentView _view;
        private ObservableCollection<PipPackageView> _installed;
        private List<PackageResultView> _installable;
        private List<PackageResultView> _installableFiltered;
        private CollectionViewSource _installedView;
        private CollectionViewSource _installableView;
        private readonly Timer _installableViewRefreshTimer;
        internal readonly PipExtensionProvider _provider;
        private readonly InstallPackageView _installCommandView;
        private readonly FuzzyStringMatcher _matcher;

        internal PipEnvironmentView(
            EnvironmentView view,
            PipExtensionProvider provider
        ) {
            _view = view;
            _provider = provider;
            _provider.UpdateStarted += PipExtensionProvider_UpdateStarted;
            _provider.UpdateComplete += PipExtensionProvider_UpdateComplete;
            _provider.IsPipInstalledChanged += PipExtensionProvider_IsPipInstalledChanged;
            _installCommandView = new InstallPackageView(this);

            _matcher = new FuzzyStringMatcher(FuzzyMatchMode.FuzzyIgnoreCase);

            _installed = new ObservableCollection<PipPackageView>();
            _installedView = new CollectionViewSource { Source = _installed };
            _installedView.Filter += InstalledView_Filter;
            _installedView.View.CurrentChanged += InstalledView_CurrentChanged;
            _installable = new List<PackageResultView>();
            _installableFiltered = new List<PackageResultView>();
            _installableView = new CollectionViewSource { Source = _installableFiltered };
            _installableView.View.CurrentChanged += InstallableView_CurrentChanged;
            _installableViewRefreshTimer = new Timer(InstallablePackages_Refresh);

            FinishInitialization();
        }

        private async void PipExtensionProvider_IsPipInstalledChanged(object sender, EventArgs e) {
            await Dispatcher.InvokeAsync(() => { IsPipInstalled = _provider.IsPipInstalled ?? true; });
        }

        private void InstalledView_CurrentChanged(object sender, EventArgs e) {
            if (_installedView.View.CurrentItem != null) {
                _installableView.View.MoveCurrentTo(null);
            }
        }

        private void InstallableView_CurrentChanged(object sender, EventArgs e) {
            if (_installableView.View.CurrentItem != null) {
                _installedView.View.MoveCurrentTo(null);
            }
        }

        private async void FinishInitialization() {
            try {
                await RefreshPackages();
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }
                ToolWindow.SendUnhandledException(_provider.WpfObject, ExceptionDispatchInfo.Capture(ex));
            }
        }

        public void Dispose() {
            _provider.UpdateStarted -= PipExtensionProvider_UpdateStarted;
            _provider.UpdateComplete -= PipExtensionProvider_UpdateComplete;
            _provider.IsPipInstalledChanged -= PipExtensionProvider_IsPipInstalledChanged;
            _installableViewRefreshTimer.Dispose();
        }

        public EnvironmentView EnvironmentView {
            get { return _view; }
        }

        public InstallPackageView InstallCommand {
            get { return _installCommandView; }
        }

        private async void PipExtensionProvider_UpdateStarted(object sender, EventArgs e) {
            try {
                await Dispatcher.InvokeAsync(() => { IsListRefreshing = true; });
            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }
                ToolWindow.SendUnhandledException(_provider.WpfObject, ExceptionDispatchInfo.Capture(ex));
            }
        }

        private async void PipExtensionProvider_UpdateComplete(object sender, EventArgs e) {
            try {
                await RefreshPackages();
            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }
                ToolWindow.SendUnhandledException(_provider.WpfObject, ExceptionDispatchInfo.Capture(ex));
            }
        }


        public bool IsPipInstalled {
            get { return (bool)GetValue(IsPipInstalledProperty); }
            private set { SetValue(IsPipInstalledPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey IsPipInstalledPropertyKey = DependencyProperty.RegisterReadOnly(
            "IsPipInstalled",
            typeof(bool),
            typeof(PipEnvironmentView),
            new PropertyMetadata(true)
        );

        public static readonly DependencyProperty IsPipInstalledProperty = IsPipInstalledPropertyKey.DependencyProperty;


        public string SearchQuery {
            get { return (string)GetValue(SearchQueryProperty); }
            set { SetValue(SearchQueryProperty, value); }
        }

        public static readonly DependencyProperty SearchQueryProperty = DependencyProperty.Register(
            "SearchQuery",
            typeof(string),
            typeof(PipEnvironmentView),
            new PropertyMetadata(Filter_Changed)
        );

        private static void Filter_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var view = d as PipEnvironmentView;
            if (view != null) {
                try {
                    view._installedView.View.Refresh();
                    view._installableViewRefreshTimer.Change(500, Timeout.Infinite);
                } catch (ObjectDisposedException) {
                }
            }
        }

        private async void InstallablePackages_Refresh(object state) {
            string query = null;
            try {
                query = await Dispatcher.InvokeAsync(() => SearchQuery);
            } catch (OperationCanceledException) {
                return;
            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }
                ToolWindow.SendUnhandledException(_provider.WpfObject, ExceptionDispatchInfo.Capture(ex));
            }

            lock (_installable) {
                _installableFiltered.Clear();
                if (_installable.Any() && !string.IsNullOrEmpty(query)) {
                    _installableFiltered.AddRange(
                        _installable
                            .Select(p => Tuple.Create(_matcher.GetSortKey(p.Package.PackageSpec, query), p))
                            .Where(t => _matcher.IsCandidateMatch(t.Item2.Package.PackageSpec, query, t.Item1))
                            .OrderByDescending(t => t.Item1)
                            .Select(t => t.Item2)
                            .Take(20)
                    );
                }
            }

            try {
                await Dispatcher.InvokeAsync(() => {
                    _installableView.View.Refresh();
                });
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }
                ToolWindow.SendUnhandledException(_provider.WpfObject, ExceptionDispatchInfo.Capture(ex));
            }
        }

        public ICollectionView InstalledPackages {
            get {
                if (EnvironmentView == null || EnvironmentView.Factory == null) {
                    return null;
                }
                return _installedView.View;
            }
        }

        public ICollectionView InstallablePackages {
            get {
                if (EnvironmentView == null || EnvironmentView.Factory == null) {
                    return null;
                }
                return _installableView.View;
            }
        }

        private void InstalledView_Filter(object sender, FilterEventArgs e) {
            PipPackageView package;
            PackageResultView result;
            var query = SearchQuery;
            var matcher = string.IsNullOrEmpty(query) ? null : _matcher;

            if ((package = e.Item as PipPackageView) != null) {
                e.Accepted = matcher == null || matcher.IsCandidateMatch(package.PackageSpec, query);
            } else if (e.Item is InstallPackageView) {
                e.Accepted = matcher != null;
            } else if ((result = e.Item as PackageResultView) != null) {
                e.Accepted = matcher != null && matcher.IsCandidateMatch(result.Package.PackageSpec, query);
            }
        }

        private async Task RefreshPackages() {
            await Dispatcher.InvokeAsync(() => {
                IsListRefreshing = true;
                CommandManager.InvalidateRequerySuggested();
            });
            try {
                if (IsPipInstalled) {
                    await Task.WhenAll(
                        RefreshInstalledPackages(),
                        RefreshInstallablePackages()
                    );
                }
            } finally {
                Dispatcher.Invoke(() => {
                    IsListRefreshing = false;
                    CommandManager.InvalidateRequerySuggested();
                });
            }
        }

        private async Task RefreshInstalledPackages() {
            var installed = await _provider.GetInstalledPackagesAsync();

            await Dispatcher.InvokeAsync(() => {
                lock (_installed) {
                    _installed.Merge(installed, PackageViewComparer.Instance, PackageViewComparer.Instance);
                }
            });
        }

        private async Task RefreshInstallablePackages() {
            var installable = await _provider.GetAvailablePackagesAsync();

            lock (_installable) {
                _installable.Clear();
                _installable.AddRange(installable.Select(pv => new PackageResultView(this, pv)));
            }
            try {
                _installableViewRefreshTimer.Change(100, Timeout.Infinite);
            } catch (ObjectDisposedException) {
            }
        }

        public bool IsListRefreshing {
            get { return (bool)GetValue(IsListRefreshingProperty); }
            private set { SetValue(IsListRefreshingPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey IsListRefreshingPropertyKey = DependencyProperty.RegisterReadOnly(
            "IsListRefreshing",
            typeof(bool),
            typeof(PipEnvironmentView),
            new PropertyMetadata(true, Filter_Changed)
        );
        public static readonly DependencyProperty IsListRefreshingProperty =
            IsListRefreshingPropertyKey.DependencyProperty;
    }

    class PackageViewComparer :
        IEqualityComparer<PipPackageView>,
        IComparer<PipPackageView>,
        IEqualityComparer<PackageResultView>,
        IComparer<PackageResultView> {
        public static readonly PackageViewComparer Instance = new PackageViewComparer();

        public bool Equals(PipPackageView x, PipPackageView y) {
            return StringComparer.OrdinalIgnoreCase.Equals(x.PackageSpec, y.PackageSpec);
        }

        public int GetHashCode(PipPackageView obj) {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.PackageSpec);
        }

        public int Compare(PipPackageView x, PipPackageView y) {
            return StringComparer.OrdinalIgnoreCase.Compare(x.PackageSpec, y.PackageSpec);
        }

        public bool Equals(PackageResultView x, PackageResultView y) {
            return Equals(x.Package, y.Package);
        }

        public int GetHashCode(PackageResultView obj) {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(
                obj.IndexName + ":" + obj.Package.PackageSpec
            );
        }

        public int Compare(PackageResultView x, PackageResultView y) {
            return Compare(x.Package, y.Package);
        }
    }

    class InstallPackageView {
        private readonly PipEnvironmentView _view;

        public InstallPackageView(PipEnvironmentView view) {
            _view = view;
        }

        public PipEnvironmentView View {
            get { return _view; }
        }

        public string IndexName {
            get { return _view._provider.IndexName; }
        }
    }

    class PackageResultView {
        private readonly PipEnvironmentView _view;
        private readonly PipPackageView _package;

        public PackageResultView(PipEnvironmentView view, PipPackageView package) {
            _view = view;
            _package = package;
        }

        public PipEnvironmentView View {
            get { return _view; }
        }

        public string PackageSpec {
            get { return _package.PackageSpec; }
        }

        public string IndexName {
            get { return _view._provider.IndexName; }
        }

        public PipPackageView Package {
            get { return _package; }
        }
    }
}

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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudioTools;
using Resources = Microsoft.PythonTools.EnvironmentsList.Properties.Resources;


namespace Microsoft.PythonTools.EnvironmentsList {
    internal partial class DBExtension : UserControl {
        public static readonly RoutedCommand StartRefreshDB = new RoutedCommand();

        private readonly DBExtensionProvider _provider;
        private readonly DBEnvironmentView _view;
        private readonly CollectionViewSource _sortedPackages;

        public DBExtension(DBExtensionProvider provider) {
            _provider = provider;
            _view = new DBEnvironmentView(null, _provider);
            DataContextChanged += DBExtension_DataContextChanged;
            InitializeComponent();

            // Set the default sort order
            _sortedPackages = (CollectionViewSource)_packageList.FindResource("SortedPackages");
            _sortedPackages.SortDescriptions.Add(new SortDescription("IsUpToDate", ListSortDirection.Ascending));
            _sortedPackages.SortDescriptions.Add(new SortDescription("FullName", ListSortDirection.Ascending));
        }

        private void DBExtension_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            var view = e.NewValue as EnvironmentView;
            if (view != null) {
                var current = Subcontext.DataContext as DBEnvironmentView;
                if (current == null || current.EnvironmentView != view) {
                    if (current != null) {
                        current.Dispose();
                    }
                    Subcontext.DataContext = new DBEnvironmentView(view, _provider);
                }
            }
        }
    }

    sealed class DBEnvironmentView : DependencyObject, IDisposable {
        private readonly EnvironmentView _view;
        private readonly DBExtensionProvider _provider;
        private ObservableCollection<DBPackageView> _packages;
        private readonly object _packagesLock = new object();

        internal DBEnvironmentView(
            EnvironmentView view,
            DBExtensionProvider provider
        ) {
            _view = view;
            _provider = provider;
            _provider.ModulesChanged += Provider_ModulesChanged;
        }

        public void Dispose() {
            _provider.ModulesChanged -= Provider_ModulesChanged;
        }

        public EnvironmentView EnvironmentView {
            get { return _view; }
        }

        public ObservableCollection<DBPackageView> Packages {
            get {
                if (_packages == null) {
                    lock (_packagesLock) {
                        _packages = _packages ?? new ObservableCollection<DBPackageView>();
                        RefreshPackages()
                            .HandleAllExceptions(Resources.PythonToolsForVisualStudio, GetType())
                            .ContinueWith(t => { t.Wait(); });
                    }
                }
                return _packages;
            }
        }

        private async Task RefreshPackages() {
            var views = DBPackageView.FromModuleList(
                await _provider.EnumerateAllModules(),
                await _provider.EnumerateStdLibModules(),
                _provider.Factory
            ).ToArray();

            if (_packages == null) {
                lock (_packagesLock) {
                    _packages = _packages ?? new ObservableCollection<DBPackageView>();
                }
            }

            await Dispatcher.InvokeAsync(() => {
                lock (_packagesLock) {
                    _packages.Merge(
                        views,
                        DBPackageViewComparer.Instance,
                        DBPackageViewComparer.Instance
                    );
                }
            });
        }

        private async void Provider_ModulesChanged(object sender, EventArgs e) {
            try {
                await RefreshPackages();
            } catch (OperationCanceledException) {
            }
        }


    }

    class DBPackageViewComparer : IEqualityComparer<DBPackageView>, IComparer<DBPackageView> {
        public static readonly DBPackageViewComparer Instance = new DBPackageViewComparer();

        public bool Equals(DBPackageView x, DBPackageView y) {
            if (x != null && y != null) {
                return StringComparer.OrdinalIgnoreCase.Equals(x.FullName, y.FullName);
            }
            return x == y;
        }

        public int GetHashCode(DBPackageView obj) {
            if (obj != null) {
                return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.FullName);
            }
            return 0;
        }

        public int Compare(DBPackageView x, DBPackageView y) {
            if (x != null && y != null) {
                return StringComparer.CurrentCultureIgnoreCase.Compare(x.FullName, y.FullName);
            } else if (x == null && y == null) {
                return 0;
            } else {
                return x == null ? 1 : -1;
            }
        }
    }

    internal sealed class DBPackageView {
        private readonly string _fullname;
        private readonly string _name;
        private bool _isUpToDate;
        private int _moduleCount;

        public DBPackageView(string fullname) {
            _fullname = fullname;
            _name = _fullname.Substring(_fullname.LastIndexOf('.') + 1);
            _isUpToDate = true;
        }

        public static IEnumerable<DBPackageView> FromModuleList(
            IList<string> modules,
            IList<string> stdLibModules,
            IPythonInterpreterFactoryWithDatabase factory
        ) {
            var stdLib = new HashSet<string>(stdLibModules, StringComparer.Ordinal);
            var stdLibPackage = new DBPackageView("(Standard Library)");
            yield return stdLibPackage;
#if DEBUG
            var seenPackages = new HashSet<string>(StringComparer.Ordinal);
#endif

            HashSet<string> knownModules = null;
            bool areKnownModulesUpToDate = false;
            if (!factory.IsCurrent) {
                var factory2 = factory as IPythonInterpreterFactoryWithDatabase2;
                if (factory2 == null) {
                    knownModules = new HashSet<string>(Regex.Matches(
                        factory.GetIsCurrentReason(CultureInfo.InvariantCulture),
                        @"\b[\w\d\.]+\b"
                    ).Cast<Match>().Select(m => m.Value),
                        StringComparer.Ordinal
                    );
                    areKnownModulesUpToDate = false;
                } else {
                    knownModules = new HashSet<string>(factory2.GetUpToDateModules(), StringComparer.Ordinal);
                    areKnownModulesUpToDate = true;
                }
            }
            for (int i = 0; i < modules.Count; ) {
                if (stdLib.Contains(modules[i])) {
                    stdLibPackage._isUpToDate = knownModules == null ||
                        knownModules.Contains(modules[i]) == areKnownModulesUpToDate; ;
                    stdLibPackage._moduleCount += 1;
                    i += 1;
                    continue;
                }

#if DEBUG
                Debug.Assert(seenPackages.Add(modules[i]));
#endif

                var package = new DBPackageView(modules[i]);
                package._isUpToDate = knownModules == null ||
                    knownModules.Contains(modules[i]) == areKnownModulesUpToDate;
                package._moduleCount = 1;

                var dotName = package._fullname + ".";
                for (++i; i < modules.Count && modules[i].StartsWith(dotName, StringComparison.Ordinal); ++i) {
                    package._isUpToDate &= knownModules == null ||
                        knownModules.Contains(modules[i]) == areKnownModulesUpToDate;
                    package._moduleCount += 1;
                }

                yield return package;
            }
        }

        public string FullName { get { return _fullname; } }
        public string Name { get { return _name; } }

        public int TotalModules { get { return _moduleCount; } }
        /// <summary>
        /// '1' if the package is up to date; otherwise '0'. Numbers are used
        /// instead of bool to make it easier to sort by status.
        /// </summary>
        public int IsUpToDate { get { return _isUpToDate ? 1 : 0; } }
    }

    public sealed class DBExtensionProvider : IEnvironmentViewExtension {
        private readonly IPythonInterpreterFactoryWithDatabase _factory;
        private FrameworkElement _wpfObject;
        private List<string> _modules;
        private List<string> _stdLibModules;

        public DBExtensionProvider(IPythonInterpreterFactoryWithDatabase factory) {
            _factory = factory;
            _factory.IsCurrentChanged += Factory_IsCurrentChanged;
        }

        private void Factory_IsCurrentChanged(object sender, EventArgs e) {
            _modules = null;
            var evt = ModulesChanged;
            if (evt != null) {
                evt(this, EventArgs.Empty);
            }
        }

        public int SortPriority {
            get { return -7; }
        }

        public string LocalizedDisplayName {
            get { return Resources.DBExtensionDisplayName; }
        }

        public object HelpContent {
            get { return Resources.DBExtensionHelpContent; }
        }

        public FrameworkElement WpfObject {
            get {
                if (_wpfObject == null) {
                    _wpfObject = new DBExtension(this);
                }
                return _wpfObject;
            }
        }

        public event EventHandler ModulesChanged;

        public IPythonInterpreterFactoryWithDatabase Factory {
            get {
                return _factory;
            }
        }

        private static IEnumerable<string> GetParentModuleNames(string fullname) {
            var sb = new StringBuilder();
            foreach (var bit in fullname.Split('.')) {
                sb.Append(bit);
                yield return sb.ToString();
                sb.Append('.');
            }
        }

        public async Task<List<string>> EnumerateStdLibModules(bool refresh = false) {
            if (_stdLibModules == null || refresh) {
                await EnumerateAllModules(true);
                Debug.Assert(_stdLibModules != null);
            }
            return _stdLibModules;
        }

        public async Task<List<string>> EnumerateAllModules(bool refresh = false) {
            if (_modules == null || refresh) {
                var stdLibPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                stdLibPaths.Add(_factory.Configuration.LibraryPath);
                stdLibPaths.Add(Path.Combine(_factory.Configuration.PrefixPath, "DLLs"));
                stdLibPaths.Add(Path.GetDirectoryName(_factory.Configuration.InterpreterPath));

                var results = await Task.Run(() => {
                    var seenModules = new HashSet<string>(StringComparer.Ordinal);
                    var stdLibModules = new List<string>();

                    var modules = ModulePath.GetModulesInLib(_factory)
                        .Select(mp => {
                            if (stdLibPaths.Contains(mp.LibraryPath)) {
                                stdLibModules.Add(mp.ModuleName);
                            }
                            return mp.ModuleName;
                        })
                        .Where(name => seenModules.Add(name))
                        .OrderBy(name => name)
                        .ToList();

                    return Tuple.Create(modules, stdLibModules);
                });

                _modules = results.Item1;
                _stdLibModules = results.Item2;
            }
            return _modules;
        }
    }

}

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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.OS;
using Microsoft.PythonTools.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PathUtils = Microsoft.PythonTools.Infrastructure.PathUtils;

namespace Microsoft.PythonTools.Interpreter {
    sealed class PipPackageManager : IPackageManager, IDisposable {
        private IPythonInterpreterFactory _factory;
        private PipPackageCache _cache;
        private readonly List<PackageSpec> _packages;
        private CancellationTokenSource _currentRefresh;
        private bool _isReady, _everCached;

        private List<FileSystemWatcher> _libWatchers;
        private Timer _refreshIsCurrentTrigger;

        private readonly PipPackageManagerCommands _commands;
        private readonly ICondaLocatorProvider _condaLocatorProvider;
        internal readonly SemaphoreSlim _working = new SemaphoreSlim(1);

        private bool _pipListHasFormatOption;
        private int _suppressCount;
        private bool _isDisposed;

        // Prevent too many concurrent executions to avoid exhausting disk IO
        private static readonly SemaphoreSlim _concurrencyLock = new SemaphoreSlim(4);

        private static readonly KeyValuePair<string, string>[] UnbufferedEnv = new[] {
            new KeyValuePair<string, string>("PYTHONUNBUFFERED", "1")
        };

        private static readonly Regex PackageNameRegex = new Regex(
            "^(?!__pycache__)(?<name>[a-z0-9_]+)(-.+)?",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public PipPackageManager(
            IPythonInterpreterFactory factory,
            PipPackageManagerCommands commands,
            int priority,
            ICondaLocatorProvider condaLocatorProvider
        ) {
            _packages = new List<PackageSpec>();
            _pipListHasFormatOption = true;

            if (factory == null) {
                throw new ArgumentNullException(nameof(factory));
            }
            if (!File.Exists(factory.Configuration?.InterpreterPath)) {
                throw new NotSupportedException();
            }

            _factory = factory;
            _commands = commands ?? new PipPackageManagerCommands();
            Priority = priority;
            _condaLocatorProvider = condaLocatorProvider;
            _cache = PipPackageCache.GetCache();
        }

        public string UniqueKey => "pip";
        public int Priority { get; }

        public IPythonInterpreterFactory Factory => _factory;

        public void Dispose() {
            if (_isDisposed) {
                return;
            }
            _isDisposed = true;

            DisableNotifications();
            _working.Dispose();
        }

        private void AbortOnInvalidConfiguration() {
            if (_factory == null || _factory.Configuration == null ||
                string.IsNullOrEmpty(_factory.Configuration.InterpreterPath)) {
                throw new InvalidOperationException(Strings.MisconfiguredEnvironment);
            }
        }

        private async Task AbortIfNotReady(CancellationToken cancellationToken) {
            if (!IsReady) {
                await UpdateIsReadyAsync(false, cancellationToken);
                if (!IsReady) {
                    throw new InvalidOperationException(Strings.MisconfiguredPip.FormatUI((_factory?.Configuration as VisualStudioInterpreterConfiguration)?.PrefixPath ?? "<null>"));
                }
            }
        }

        private async Task<KeyValuePair<string, string>[]> GetEnvironmentVariables() {
            var prefixPath = _factory.Configuration.GetPrefixPath();
            if (_condaLocatorProvider != null && Directory.Exists(prefixPath) && CondaUtils.IsCondaEnvironment(prefixPath)) {
                var rootConda = _condaLocatorProvider.FindLocator()?.CondaExecutablePath;
                if (File.Exists(rootConda)) {
                    var env = await CondaUtils.GetActivationEnvironmentVariablesForPrefixAsync(rootConda, prefixPath);
                    return env.Union(UnbufferedEnv).ToArray();
                } else {
                    // Normally, the root is required for this environment to have been discovered,
                    // but it could be that the user added this as a custom environment and then
                    // uninstalled the root. When that's the case, there is no way to activate,
                    // so proceed without activation env variables.
                    return UnbufferedEnv;
                }
            } else {
                // Not a conda environment, no activation necessary.
                return UnbufferedEnv;
            }
        }

        private Task<bool> ShouldElevate(IPackageManagerUI ui, string operation) {
            return ui == null ? Task.FromResult(false) : ui.ShouldElevateAsync(this, operation);
        }

        public bool IsReady {
            get {
                return _isReady;
            }
            private set {
                if (_isReady != value) {
                    _isReady = value;
                    IsReadyChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        public event EventHandler IsReadyChanged;

        private async Task UpdateIsReadyAsync(bool alreadyHasLock, CancellationToken cancellationToken) {
            IDisposable workingLock = null;
            if (!alreadyHasLock) {
                try {
                    workingLock = await _working.LockAsync(cancellationToken);
                } catch (ObjectDisposedException ex) {
                    throw new OperationCanceledException("Package manager has already closed", ex);
                }
            }
            try {
                var envVars = await GetEnvironmentVariables();

                using (var proc = ProcessOutput.Run(
                    _factory.Configuration.InterpreterPath,
                    _commands.CheckIsReady(),
                    _factory.Configuration.GetPrefixPath(),
                    envVars,
                    false,
                    null
                )) {
                    try {
                        IsReady = (await proc == 0);
                    } catch (OperationCanceledException) {
                        IsReady = false;
                        return;
                    }
                }
            } finally {
                workingLock?.Dispose();
            }
        }

        public async Task PrepareAsync(IPackageManagerUI ui, CancellationToken cancellationToken) {
            if (IsReady) {
                return;
            }

            AbortOnInvalidConfiguration();

            await UpdateIsReadyAsync(false, cancellationToken);
            if (IsReady) {
                return;
            }

            var operation = "pip_downloader.py";
            using (await _working.LockAsync(cancellationToken)) {
                ui?.OnOperationStarted(this, operation);
                ui?.OnOutputTextReceived(this, Strings.InstallingPipStarted);

                var envVars = await GetEnvironmentVariables();

                using (var proc = ProcessOutput.Run(
                    _factory.Configuration.InterpreterPath,
                    _commands.Prepare(),
                    _factory.Configuration.GetPrefixPath(),
                    envVars,
                    false,
                    PackageManagerUIRedirector.Get(this, ui),
                    elevate: await ShouldElevate(ui, operation)
                )) {
                    try {
                        IsReady = (await proc == 0);
                    } catch (OperationCanceledException) {
                        IsReady = false;
                    }
                }

                ui?.OnOutputTextReceived(this, IsReady ? Strings.InstallingPipSuccess : Strings.InstallingPackageFailed);
                ui?.OnOperationFinished(this, operation, IsReady);
            }
        }

        public async Task<bool> ExecuteAsync(string arguments, IPackageManagerUI ui, CancellationToken cancellationToken) {
            AbortOnInvalidConfiguration();
            await AbortIfNotReady(cancellationToken);

            using (await _working.LockAsync(cancellationToken)) {
                bool success = false;

                var args = string.Join(" ", _commands.Base().Select(ProcessOutput.QuoteSingleArgument)) + " " + arguments;
                var operation = args.Trim();
                ui?.OnOutputTextReceived(this, operation);
                ui?.OnOperationStarted(this, Strings.ExecutingCommandStarted.FormatUI(arguments));

                var envVars = await GetEnvironmentVariables();

                try {
                    using (var output = ProcessOutput.Run(
                        _factory.Configuration.InterpreterPath,
                        new[] { args.Trim() },
                        _factory.Configuration.GetPrefixPath(),
                        envVars,
                        false,
                        PackageManagerUIRedirector.Get(this, ui),
                        quoteArgs: false,
                        elevate: await ShouldElevate(ui, operation)
                    )) {
                        if (!output.IsStarted) {
                            return false;
                        }
                        var exitCode = await output;
                        success = exitCode == 0;
                    }
                    return success;
                } catch (IOException) {
                    return false;
                } finally {
                    if (!success) {
                        // Check whether we failed because pip is missing
                        UpdateIsReadyAsync(true, CancellationToken.None).DoNotWait();
                    }

                    var msg = success ? Strings.ExecutingCommandSucceeded : Strings.ExecutingCommandFailed;
                    ui?.OnOutputTextReceived(this, msg.FormatUI(arguments));
                    ui?.OnOperationFinished(this, operation, success);
                    await CacheInstalledPackagesAsync(true, false, cancellationToken);
                }
            }
        }

        public async Task<bool> InstallAsync(PackageSpec package, IPackageManagerUI ui, CancellationToken cancellationToken) {
            AbortOnInvalidConfiguration();
            await AbortIfNotReady(cancellationToken);

            bool success = false;

            var args = _commands.Install(package.FullSpec).ToArray();
            var operation = string.Join(" ", args);
            var name = string.IsNullOrEmpty(package.Name) ? package.FullSpec : package.Name;

            using (await _working.LockAsync(cancellationToken)) {
                ui?.OnOperationStarted(this, operation);
                ui?.OnOutputTextReceived(this, Strings.InstallingPackageStarted.FormatUI(name));

                var envVars = await GetEnvironmentVariables();

                try {
                    using (var output = ProcessOutput.Run(
                        _factory.Configuration.InterpreterPath,
                        args,
                        _factory.Configuration.GetPrefixPath(),
                        envVars,
                        false,
                        PackageManagerUIRedirector.Get(this, ui),
                        quoteArgs: false,
                        elevate: await ShouldElevate(ui, operation)
                    )) {
                        if (!output.IsStarted) {
                            return false;
                        }
                        var exitCode = await output;
                        success = exitCode == 0;
                    }
                    return success;
                } catch (IOException) {
                    return false;
                } finally {
                    if (!success) {
                        // Check whether we failed because pip is missing
                        UpdateIsReadyAsync(true, CancellationToken.None).DoNotWait();
                    }

                    var msg = success ? Strings.InstallingPackageSuccess : Strings.InstallingPackageFailed;
                    ui?.OnOutputTextReceived(this, msg.FormatUI(name));
                    ui?.OnOperationFinished(this, operation, success);
                    await CacheInstalledPackagesAsync(true, false, cancellationToken);
                }
            }
        }

        public async Task<bool> UninstallAsync(PackageSpec package, IPackageManagerUI ui, CancellationToken cancellationToken) {
            AbortOnInvalidConfiguration();
            await AbortIfNotReady(cancellationToken);

            bool success = false;
            var args = _commands.Uninstall(package.FullSpec).ToArray();
            var operation = string.Join(" ", args);
            var name = string.IsNullOrEmpty(package.Name) ? package.FullSpec : package.Name;

            try {
                using (await _working.LockAsync(cancellationToken)) {
                    ui?.OnOperationStarted(this, operation);
                    ui?.OnOutputTextReceived(this, Strings.UninstallingPackageStarted.FormatUI(name));

                    var envVars = await GetEnvironmentVariables();

                    using (var output = ProcessOutput.Run(
                        _factory.Configuration.InterpreterPath,
                        args,
                        _factory.Configuration.GetPrefixPath(),
                        envVars,
                        false,
                        PackageManagerUIRedirector.Get(this, ui),
                        elevate: await ShouldElevate(ui, operation)
                    )) {
                        if (!output.IsStarted) {
                            // The finally block handles output
                            return false;
                        }
                        var exitCode = await output;
                        success = exitCode == 0;
                    }
                    return success;
                }
            } catch (IOException) {
                return false;
            } finally {
                if (!success) {
                    // Check whether we failed because pip is missing
                    UpdateIsReadyAsync(false, CancellationToken.None).DoNotWait();
                }

                if (IsReady) {
                    await CacheInstalledPackagesAsync(false, false, cancellationToken);
                    if (!success) {
                        // Double check whether the package has actually
                        // been uninstalled, to avoid reporting errors 
                        // where, for all practical purposes, there is no
                        // error.
                        if (!(await GetInstalledPackageAsync(package, cancellationToken)).IsValid) {
                            success = true;
                        }
                    }
                }

                var msg = success ? Strings.UninstallingPackageSuccess : Strings.UninstallingPackageFailed;
                ui?.OnOutputTextReceived(this, msg.FormatUI(name));
                ui?.OnOperationFinished(this, operation, success);
            }
        }

        public event EventHandler InstalledPackagesChanged;
        public event EventHandler InstalledFilesChanged;

        private bool SupportsDashMPip => _factory.Configuration.Version > new Version(2, 7);

        public string ExtensionDisplayName => Strings.PipExtensionDisplayName;

        public string IndexDisplayName => Strings.PipDefaultIndexName;

        public string SearchHelpText => Strings.PipExtensionSearchPyPILabel;

        public string GetInstallCommandDisplayName(string searchQuery) {
            if (string.IsNullOrEmpty(searchQuery)) {
                return string.Empty;
            }

            return Strings.PipExtensionPipInstallFrom.FormatUI(searchQuery);
        }

        public bool CanUninstall(PackageSpec package) {
            return true;
        }

        private async Task CacheInstalledPackagesAsync(
            bool alreadyHasLock,
            bool alreadyHasConcurrencyLock,
            CancellationToken cancellationToken
        ) {
            if (!IsReady) {
                await UpdateIsReadyAsync(alreadyHasLock, cancellationToken);
                if (!IsReady) {
                    return;
                }
            }

            List<PackageSpec> packages = null;

            var workingLock = alreadyHasLock ? null : await _working.LockAsync(cancellationToken);
            try {
                var args = _pipListHasFormatOption ? _commands.ListJson() : _commands.List();

                var concurrencyLock = alreadyHasConcurrencyLock ? null : await _concurrencyLock.LockAsync(cancellationToken);
                try {
                    var envVars = await GetEnvironmentVariables();

                    using (var proc = ProcessOutput.Run(
                        _factory.Configuration.InterpreterPath,
                        args,
                        _factory.Configuration.GetPrefixPath(),
                        envVars,
                        false,
                        null
                    )) {
                        try {
                            if ((await proc) == 0) {
                                if (_pipListHasFormatOption) {
                                    try {
                                        var data = JToken.ReadFrom(new JsonTextReader(new StringListReader(proc.StandardOutputLines)));
                                        packages = data
                                            .Select(j => new PackageSpec(j.Value<string>("name"), j.Value<string>("version")))
                                            .Where(p => p.IsValid)
                                            .OrderBy(p => p.Name)
                                            .ToList();
                                    } catch (JsonException ex) {
                                        Debug.WriteLine("Failed to parse: {0}".FormatInvariant(ex.Message));
                                        foreach (var l in proc.StandardOutputLines) {
                                            Debug.WriteLine(l);
                                        }
                                    }
                                } else {
                                    packages = proc.StandardOutputLines
                                        .Select(i => PackageSpec.FromPipList(i))
                                        .Where(p => p.IsValid)
                                        .OrderBy(p => p.Name)
                                        .ToList();
                                }
                            } else if (_pipListHasFormatOption) {
                                // Actually, pip probably doesn't have the --format option
                                Debug.WriteLine("{0} does not support --format".FormatInvariant(_factory.Configuration.InterpreterPath));
                                _pipListHasFormatOption = false;
                                await CacheInstalledPackagesAsync(true, true, cancellationToken);
                                return;
                            } else {
                            }
                        } catch (OperationCanceledException) {
                            // Process failed to run
                            Debug.WriteLine("Failed to run pip to collect packages");
                            foreach (var line in proc.StandardOutputLines) {
                                Debug.WriteLine(line);
                            }
                        }
                    }

                    if (packages == null) {
                        // Pip failed, so return a directory listing
                        var paths = await PythonLibraryPath.GetSearchPathsAsync(
                            _factory.Configuration,
                            new FileSystem(new OSPlatform()),
                            new ProcessServices()
                        );

                        packages = await Task.Run(() => paths.Where(p => p.Type != PythonLibraryPathType.StdLib && Directory.Exists(p.Path))
                            .SelectMany(p => PathUtils.EnumerateDirectories(p.Path, recurse: false))
                            .Select(path => Path.GetFileName(path))
                            .Select(name => PackageNameRegex.Match(name))
                            .Where(match => match.Success)
                            .Select(match => new PackageSpec(match.Groups["name"].Value))
                            .Where(p => p.IsValid)
                            .OrderBy(p => p.Name)
                            .ToList());
                    }
                } finally {
                    concurrencyLock?.Dispose();
                }

                // Outside of concurrency lock, still in working lock

                _packages.Clear();
                _packages.AddRange(packages);
                _everCached = true;
            } finally {
                workingLock?.Dispose();
            }

            InstalledPackagesChanged?.Invoke(this, EventArgs.Empty);
            _factory.NotifyImportNamesChanged();
        }

        public async Task<IList<PackageSpec>> GetInstalledPackagesAsync(CancellationToken cancellationToken) {
            using (await _working.LockAsync(cancellationToken)) {
                if (!_everCached) {
                    await CacheInstalledPackagesAsync(true, false, cancellationToken);
                }
                return _packages.ToArray();
            }
        }

        public async Task<PackageSpec> GetInstalledPackageAsync(PackageSpec package, CancellationToken cancellationToken) {
            if (!package.IsValid) {
                return package;
            }
            using (await _working.LockAsync(cancellationToken)) {
                if (!_everCached) {
                    await CacheInstalledPackagesAsync(true, false, cancellationToken);
                }
                return _packages.FirstOrDefault(p => p.Name == package.Name) ?? new PackageSpec(null);
            }
        }

        public Task<IList<PackageSpec>> GetInstallablePackagesAsync(CancellationToken cancellationToken) {
            if (_cache == null) {
                return Task.FromResult<IList<PackageSpec>>(Array.Empty<PackageSpec>());
            }
            return _cache.GetAllPackagesAsync(cancellationToken);
        }

        public async Task<PackageSpec> GetInstallablePackageAsync(PackageSpec package, CancellationToken cancellationToken) {
            if (!package.IsValid) {
                return package;
            }
            return await _cache.GetPackageInfoAsync(package, cancellationToken);
        }

        private sealed class Suppressed : IDisposable {
            private readonly PipPackageManager _manager;

            public Suppressed(PipPackageManager manager) {
                _manager = manager;
            }

            public void Dispose() {
                if (Interlocked.Decrement(ref _manager._suppressCount) == 0) {
                    _manager.WatchingLibrary = true;
                }
            }
        }

        public void EnableNotifications() {
            if (_libWatchers == null) {
                _libWatchers = new List<FileSystemWatcher>();
                _refreshIsCurrentTrigger = new Timer(RefreshIsCurrentTimer_Elapsed);
                CreateLibraryWatchers().DoNotWait();

                UpdateIsReadyAsync(false, CancellationToken.None)
                    .SilenceException<OperationCanceledException>()
                    .DoNotWait();
            }
        }

        public void DisableNotifications() {
            if (_libWatchers != null) {
                lock (_libWatchers) {
                    foreach (var w in _libWatchers) {
                        w.EnableRaisingEvents = false;
                        w.Dispose();
                    }
                }
                _refreshIsCurrentTrigger.Dispose();
            }
        }

        public IDisposable SuppressNotifications() {
            WatchingLibrary = false;
            Interlocked.Increment(ref _suppressCount);
            return new Suppressed(this);
        }

        private bool WatchingLibrary {
            get {
                if (_libWatchers == null) {
                    return false;
                }
                lock (_libWatchers) {
                    return _libWatchers.Any(w => w.EnableRaisingEvents);
                }
            }
            set {
                if (_libWatchers == null) {
                    return;
                }

                lock (_libWatchers) {
                    bool clearAll = false;

                    try {
                        foreach (var w in _libWatchers) {
                            if (w.EnableRaisingEvents == value) {
                                continue;
                            }
                            w.EnableRaisingEvents = value;
                        }
                    } catch (IOException) {
                        // May occur if the library has been deleted while the
                        // watcher was disabled.
                        clearAll = true;
                    } catch (ObjectDisposedException) {
                        clearAll = true;
                    }

                    if (clearAll) {
                        foreach (var w in _libWatchers) {
                            w.EnableRaisingEvents = false;
                            w.Dispose();
                        }
                        _libWatchers.Clear();
                    }
                }
            }
        }

        private async Task CreateLibraryWatchers() {
            Debug.Assert(_libWatchers != null, "Should not create watchers when suppressed");

            IReadOnlyList<string> paths = null;

            if (_factory.Configuration.SearchPaths.Any()) {
                paths = _factory.Configuration.SearchPaths;
            }

            if (paths == null) {
                try {
                    paths = (await PythonLibraryPath.GetSearchPathsAsync(_factory.Configuration, new FileSystem(new OSPlatform()), new ProcessServices()))
                        .Select(p => p.Path)
                        .ToArray();
                } catch (InvalidOperationException) {
                    return;
                }
            }

            paths = paths
                .Where(Directory.Exists)
                .OrderBy(p => p.Length)
                .ToList();

            var watching = new List<string>();
            var watchers = new List<FileSystemWatcher>();

            foreach (var path in paths) {
                if (watching.Any(p => PathUtils.IsSubpathOf(p, path))) {
                    continue;
                }

                FileSystemWatcher watcher = null;
                try {
                    watcher = new FileSystemWatcher {
                        IncludeSubdirectories = true,
                        Path = path,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
                    };
                    watcher.Created += OnChanged;
                    watcher.Deleted += OnChanged;
                    watcher.Changed += OnChanged;
                    watcher.Renamed += OnRenamed;
                    watcher.EnableRaisingEvents = true;

                    watching.Add(path);
                    watchers.Add(watcher);
                } catch (IOException) {
                    // Raced with directory deletion. We normally handle the
                    // library being deleted by disposing the watcher, but this
                    // occurs in response to an event from the watcher. Because
                    // we never got to start watching, we will just dispose
                    // immediately.
                    watcher?.Dispose();
                } catch (ArgumentException ex) {
                    watcher?.Dispose();
                    Debug.WriteLine("Error starting FileSystemWatcher:\r\n{0}", ex);
                }
            }

            List<FileSystemWatcher> oldWatchers;
            lock (_libWatchers) {
                oldWatchers = _libWatchers.ToList();
                _libWatchers.Clear();
                _libWatchers.AddRange(watchers);
            }

            foreach (var oldWatcher in oldWatchers) {
                oldWatcher.EnableRaisingEvents = false;
                oldWatcher.Dispose();
            }
        }

        private async void RefreshIsCurrentTimer_Elapsed(object state) {
            if (_isDisposed) {
                return;
            }

            try {
                _refreshIsCurrentTrigger.Change(Timeout.Infinite, Timeout.Infinite);
            } catch (ObjectDisposedException) {
            }

            InstalledFilesChanged?.Invoke(this, EventArgs.Empty);
            _factory.NotifyImportNamesChanged();

            var cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;
            var oldCts = Interlocked.Exchange(ref _currentRefresh, cts);
            try {
                oldCts?.Cancel();
                oldCts?.Dispose();
            } catch (ObjectDisposedException) {
            }

            try {
                await CacheInstalledPackagesAsync(false, false, cancellationToken);
            } catch (OperationCanceledException) {
            } catch (FileNotFoundException) {
                // Happens if we attempt to refresh an environment that was just deleted
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                Debug.Fail(ex.ToUnhandledExceptionMessage(GetType()));
            }
        }

        public void NotifyPackagesChanged() {
            try {
                _refreshIsCurrentTrigger.Change(100, Timeout.Infinite);
            } catch (ObjectDisposedException) {
            }
        }

        private void OnRenamed(object sender, RenamedEventArgs e) {
            if (Directory.Exists(e.FullPath) ||
                ModulePath.IsPythonFile(e.FullPath, false, true, false) ||
                ModulePath.IsPythonFile(e.OldFullPath, false, true, false)) {
                try {
                    _refreshIsCurrentTrigger.Change(1000, Timeout.Infinite);
                } catch (ObjectDisposedException) {
                }
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e) {
            if ((Directory.Exists(e.FullPath) && 
                !PathUtils.GetFileOrDirectoryName(e.FullPath).Equals("__pycache__", StringComparison.OrdinalIgnoreCase)) ||
                ModulePath.IsPythonFile(e.FullPath, false, true, false)
            ) {
                try {
                    _refreshIsCurrentTrigger.Change(1000, Timeout.Infinite);
                } catch (ObjectDisposedException) {
                }
            }
        }
    }
}

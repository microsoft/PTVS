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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools.Interpreter {
    class PipPackageManager : IPackageManager, IDisposable {
        private IPythonInterpreterFactory _factory;
        private PipPackageCache _cache;
        private readonly Timer _refreshIsCurrentTrigger;
        private readonly List<FileSystemWatcher> _libWatchers;
        private readonly List<PackageSpec> _packages;
        private CancellationTokenSource _currentRefresh;
        private bool _isReady, _everCached;

        internal readonly SemaphoreSlim _working = new SemaphoreSlim(1);

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


        public PipPackageManager(bool allowFileSystemWatchers = true) {
            _packages = new List<PackageSpec>();

            if (allowFileSystemWatchers) {
                _libWatchers = new List<FileSystemWatcher>();
                _refreshIsCurrentTrigger = new Timer(RefreshIsCurrentTimer_Elapsed);
            }
        }

        public void SetInterpreterFactory(IPythonInterpreterFactory factory) {
            if (factory == null) {
                throw new ArgumentNullException(nameof(factory));
            }
            if (!File.Exists(factory.Configuration?.InterpreterPath)) {
                throw new NotSupportedException();
            }

            _factory = factory;

            _cache = PipPackageCache.GetCache(new Uri("https://pypi.python.org/pypi/"));

            if (_libWatchers != null) {
                CreateLibraryWatchers().DoNotWait();
            }
            _refreshIsCurrentTrigger?.Change(1000, Timeout.Infinite);
        }

        public IPythonInterpreterFactory Factory => _factory;

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~PipPackageManager() {
            Dispose(false);
        }

        protected void Dispose(bool disposing) {
            if (_isDisposed) {
                return;
            }
            _isDisposed = true;

            if (disposing) {
                if (_libWatchers != null) {
                    lock (_libWatchers) {
                        foreach (var w in _libWatchers) {
                            w.EnableRaisingEvents = false;
                            w.Dispose();
                        }
                    }
                }
                _refreshIsCurrentTrigger?.Dispose();
            }
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
                    throw new InvalidOperationException(Strings.MisconfiguredEnvironment);
                }
            }
        }

        private Task<bool> ShouldElevate(IPackageManagerUI ui, string operation) {
            return ui == null ? Task.FromResult(false) : ui.ShouldElevateAsync(this, operation);
        }

        public bool IsReady {
            get { return _isReady; }
            private set {
                if (_isReady != value) {
                    _isReady = value;
                    IsReadyChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        public event EventHandler IsReadyChanged;

        private async Task UpdateIsReadyAsync(bool alreadyHasLock, CancellationToken cancellationToken) {
            var workingLock = alreadyHasLock ? null : await _working.LockAsync(cancellationToken);
            try {
                using (var proc = ProcessOutput.Run(
                    _factory.Configuration.InterpreterPath,
                    new[] { "-E", "-c", "import pip" },
                    _factory.Configuration.PrefixPath,
                    UnbufferedEnv,
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

                using (var proc = ProcessOutput.Run(
                    _factory.Configuration.InterpreterPath,
                    new[] { "-E", PythonToolsInstallPath.GetFile("pip_downloader.py", GetType().Assembly) },
                    _factory.Configuration.PrefixPath,
                    UnbufferedEnv,
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
                string args;

                if (!SupportsDashMPip) {
                    args = "-c \"import pip; pip.main()\" ";
                } else {
                    args = "-m pip ";
                }

                args += arguments;

                var operation = args;
                ui?.OnOutputTextReceived(this, operation);
                ui?.OnOperationStarted(this, Strings.ExecutingCommandStarted.FormatUI(arguments));

                try {
                    using (var output = ProcessOutput.Run(
                        _factory.Configuration.InterpreterPath,
                        new[] { args },
                        _factory.Configuration.PrefixPath,
                        UnbufferedEnv,
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
                    await CacheInstalledPackagesAsync(true, cancellationToken);
                }
            }
        }

        public async Task<bool> InstallAsync(PackageSpec package, IPackageManagerUI ui, CancellationToken cancellationToken) {
            AbortOnInvalidConfiguration();
            await AbortIfNotReady(cancellationToken);

            bool success = false;
            List<string> args;

            if (!SupportsDashMPip) {
                args = new List<string> { "-c", "\"import pip; pip.main()\"", "install" };
            } else {
                args = new List<string> { "-m", "pip", "install" };
            }

            args.Add(package.FullSpec);
            var name = string.IsNullOrEmpty(package.Name) ? package.FullSpec : package.Name;
            var operation = string.Join(" ", args);

            using (await _working.LockAsync(cancellationToken)) {
                ui?.OnOperationStarted(this, operation);
                ui?.OnOutputTextReceived(this, Strings.InstallingPackageStarted.FormatUI(name));

                try {
                    using (var output = ProcessOutput.Run(
                        _factory.Configuration.InterpreterPath,
                        args,
                        _factory.Configuration.PrefixPath,
                        UnbufferedEnv,
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
                    await CacheInstalledPackagesAsync(true, cancellationToken);
                }
            }
        }

        public async Task<bool> UninstallAsync(PackageSpec package, IPackageManagerUI ui, CancellationToken cancellationToken) {
            AbortOnInvalidConfiguration();
            await AbortIfNotReady(cancellationToken);

            bool success = false;
            List<string> args;

            if (!SupportsDashMPip) {
                args = new List<string> { "-c", "import pip; pip.main()", "uninstall", "-y" };
            } else {
                args = new List<string> { "-m", "pip", "uninstall", "-y" };
            }

            args.Add(package.FullSpec);
            var name = string.IsNullOrEmpty(package.Name) ? package.FullSpec : package.Name;
            var operation = string.Join(" ", args);

            try {
                using (await _working.LockAsync(cancellationToken)) {
                    ui?.OnOperationStarted(this, operation);
                    ui?.OnOutputTextReceived(this, Strings.InstallingPackageStarted.FormatUI(name));

                    using (var output = ProcessOutput.Run(
                        _factory.Configuration.InterpreterPath,
                        args,
                        _factory.Configuration.PrefixPath,
                        UnbufferedEnv,
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
                    await CacheInstalledPackagesAsync(false, cancellationToken);
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

        private async Task CacheInstalledPackagesAsync(bool alreadyHasLock, CancellationToken cancellationToken) {
            if (!IsReady) {
                await UpdateIsReadyAsync(alreadyHasLock, cancellationToken);
                if (!IsReady) {
                    return;
                }
            }

            List<PackageSpec> packages = null;

            var workingLock = alreadyHasLock ? null : await _working.LockAsync(cancellationToken);
            try {
                string[] args;
                if (!SupportsDashMPip) {
                    args = new[] { "-E", "-c", "import pip; pip.main()", "list" };
                } else {
                    args = new[] { "-E", "-m", "pip", "list" };
                }

                using (await _concurrencyLock.LockAsync(cancellationToken)) {
                    using (var proc = ProcessOutput.Run(
                        _factory.Configuration.InterpreterPath,
                        args,
                        _factory.Configuration.PrefixPath,
                        UnbufferedEnv,
                        false,
                        null
                    )) {
                        try {
                            if (await proc == 0) {
                                packages = proc.StandardOutputLines
                                    .Select(i => PackageSpec.FromPipList(i))
                                    .Where(p => p.IsValid)
                                    .OrderBy(p => p.Name)
                                    .ToList();
                            }
                        } catch (OperationCanceledException) {
                            // Process failed to run
                            Debug.WriteLine("Failed to run pip to collect packages");
                            Debug.WriteLine(string.Join(Environment.NewLine, proc.StandardOutputLines));
                        }
                    }

                    if (packages == null) {
                        // Pip failed, so return a directory listing
                        var paths = await PythonTypeDatabase.GetDatabaseSearchPathsAsync(_factory);

                        packages = await Task.Run(() => paths.Where(p => !p.IsStandardLibrary)
                            .SelectMany(p => PathUtils.EnumerateDirectories(p.Path, recurse: false))
                            .Select(path => Path.GetFileName(path))
                            .Select(name => PackageNameRegex.Match(name))
                            .Where(match => match.Success)
                            .Select(match => new PackageSpec(match.Groups["name"].Value))
                            .Where(p => p.IsValid)
                            .OrderBy(p => p.Name)
                            .ToList());
                    }
                }

                // Outside of concurrency lock, still in working lock

                _packages.Clear();
                _packages.AddRange(packages);
                _everCached = true;
            } finally {
                workingLock?.Dispose();
            }

            InstalledPackagesChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task<IList<PackageSpec>> GetInstalledPackagesAsync(CancellationToken cancellationToken) {
            using (await _working.LockAsync(cancellationToken)) {
                if (!_everCached) {
                    await CacheInstalledPackagesAsync(true, cancellationToken);
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
                    await CacheInstalledPackagesAsync(true, cancellationToken);
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

            IList<PythonLibraryPath> paths;
            try {
                paths = await PythonTypeDatabase.GetDatabaseSearchPathsAsync(_factory);
            } catch (InvalidOperationException) {
                return;
            }

            paths = paths.OrderBy(p => p.Path.Length).ToList();

            var watching = new List<string>();
            var watchers = new List<FileSystemWatcher>();

            foreach (var path in paths) {
                if (watching.Any(p => PathUtils.IsSubpathOf(p, path.Path))) {
                    continue;
                }

                FileSystemWatcher watcher = null;
                try {
                    watcher = new FileSystemWatcher {
                        IncludeSubdirectories = true,
                        Path = path.Path,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
                    };
                    watcher.Created += OnChanged;
                    watcher.Deleted += OnChanged;
                    watcher.Changed += OnChanged;
                    watcher.Renamed += OnRenamed;
                    watcher.EnableRaisingEvents = true;

                    watching.Add(path.Path);
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

        private void RefreshIsCurrentTimer_Elapsed(object state) {
            if (_isDisposed) {
                return;
            }

            InstalledFilesChanged?.Invoke(this, EventArgs.Empty);

            var cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;
            var oldCts = Interlocked.Exchange(ref _currentRefresh, cts);
            try {
                oldCts?.Cancel();
            } catch (ObjectDisposedException) {
            }
            oldCts?.Dispose();

            CacheInstalledPackagesAsync(false, cancellationToken)
                .SilenceException<OperationCanceledException>()
                .DoNotWait();
        }

        public void NotifyPackagesChanged() {
            _refreshIsCurrentTrigger.Change(100, Timeout.Infinite);
        }

        private void OnRenamed(object sender, RenamedEventArgs e) {
            _refreshIsCurrentTrigger.Change(1000, Timeout.Infinite);
        }

        private void OnChanged(object sender, FileSystemEventArgs e) {
            _refreshIsCurrentTrigger.Change(1000, Timeout.Infinite);
        }
    }
}

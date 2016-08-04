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
        private readonly Timer _refreshIsCurrentTrigger;
        private readonly List<FileSystemWatcher> _libWatchers;
        private readonly List<PackageSpec> _packages;
        private CancellationTokenSource _currentRefresh;

        private readonly SemaphoreSlim _working = new SemaphoreSlim(1);

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
                throw new ArgumentNullException("factory");
            }
            if (!File.Exists(factory.Configuration?.InterpreterPath)) {
                throw new NotSupportedException();
            }

            _factory = factory;
            if (_libWatchers != null) {
                CreateLibraryWatchers().DoNotWait();
            }
            _refreshIsCurrentTrigger?.Change(1000, Timeout.Infinite);
        }

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

        public bool IsReady { get; private set; }

        private async Task UpdateIsReady(CancellationToken cancellationToken) {
            using (await _working.LockAsync(cancellationToken)) {
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
            }
        }

        public async Task PrepareAsync(IPackageManagerUI ui, CancellationToken cancellationToken) {
            if (IsReady) {
                return;
            }

            AbortOnInvalidConfiguration();

            using (await _working.LockAsync(cancellationToken)) {
                ui?.WriteOutput(Strings.InstallingPipStarted);

                using (var proc = ProcessOutput.Run(
                    _factory.Configuration.InterpreterPath,
                    new[] { "-E", PythonToolsInstallPath.GetFile("pip_downloader.py", GetType().Assembly) },
                    _factory.Configuration.PrefixPath,
                    UnbufferedEnv,
                    false,
                    PackageManagerUIRedirector.Get(ui),
                    elevate: ui?.ShouldElevate() ?? false
                )) {
                    try {
                        IsReady = (await proc == 0);
                    } catch (OperationCanceledException) {
                        IsReady = false;
                    }
                }

                if (IsReady) {
                    ui?.WriteOutput(Strings.InstallingPipSuccess);
                } else {
                    ui?.WriteError(Strings.InstallingPipFailed);
                }
            }
        }

        public event EventHandler InstalledPackagesChanged;
        public event EventHandler InstalledFilesChanged;

        private bool SupportsDashMPip => _factory.Configuration.Version > new Version(2, 7);

        private async Task CacheInstalledPackagesAsync(CancellationToken cancellationToken) {
            if (!IsReady) {
                await UpdateIsReady(cancellationToken);
                if (!IsReady) {
                    return;
                }
            }

            List<PackageSpec> packages = null;

            using (await _working.LockAsync(cancellationToken)) {
                using (await _concurrencyLock.LockAsync(cancellationToken)) {
                    using (var proc = ProcessOutput.Run(
                        _factory.Configuration.InterpreterPath,
                        new[] {
                            "-E",
                            SupportsDashMPip ? "-m" : "-c",
                            SupportsDashMPip ? "pip" : "import pip; pip.main()",
                            "list"
                        },
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

                lock (_packages) {
                    _packages.Clear();
                    _packages.AddRange(packages);
                }
            }

            InstalledPackagesChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task<IList<PackageSpec>> GetInstalledPackagesAsync(CancellationToken cancellationToken) {
            lock (_packages) {
                return _packages.ToArray();
            }
        }

        public async Task<PackageSpec> GetInstalledPackageAsync(string name, CancellationToken cancellationToken) {
            lock (_packages) {
                return _packages.FirstOrDefault(p => p.Name == name);
            }
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

            List<PythonLibraryPath> paths;
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
            var oldCts = Interlocked.Exchange(ref _currentRefresh, cts);
            oldCts?.Cancel();
            oldCts?.Dispose();

            CacheInstalledPackagesAsync(cts.Token).DoNotWait();
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

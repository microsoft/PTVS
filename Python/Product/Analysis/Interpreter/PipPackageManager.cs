using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools.Interpreter {
    class PipPackageManager : IPackageManager {
        private readonly IPythonInterpreterFactory _factory;
        private readonly List<FileSystemWatcher> _libWatchers;
        private readonly List<PackageSpec> _packages;
        private int _suppressCount;

        private static readonly KeyValuePair<string, string>[] UnbufferedEnv = new[] {
            new KeyValuePair<string, string>("PYTHONUNBUFFERED", "1")
        };

        private static readonly Regex PackageNameRegex = new Regex(
            "^(?!__pycache__)(?<name>[a-z0-9_]+)(-.+)?",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);


        public PipPackageManager(IPythonInterpreterFactory factory) {
            _factory = factory;
            _libWatchers = new List<FileSystemWatcher>();
            _packages = new List<PackageSpec>();
        }

        public event EventHandler InstalledPackagesChanged;

        private void OnInstalledPackagesChanged() {
            InstalledPackagesChanged?.Invoke(this, EventArgs.Empty);
        }

        private bool SupportsDashMPip => _factory.Configuration.Version > new Version(2, 7);

        private async Task CacheInstalledPackagesAsync(CancellationToken cancellationToken) {
            List<PackageSpec> packages = null;

            using (var proc = ProcessOutput.Run(
                _factory.Configuration.InterpreterPath,
                new[] {
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
                            .Select(i => new PackageSpec(i))
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

            lock (_packages) {
                _packages.Clear();
                _packages.AddRange(packages);
            }

            OnInstalledPackagesChanged();
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
                lock (_libWatchers) {
                    return _libWatchers.Any(w => w.EnableRaisingEvents);
                }
            }
            set {
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

    }
}

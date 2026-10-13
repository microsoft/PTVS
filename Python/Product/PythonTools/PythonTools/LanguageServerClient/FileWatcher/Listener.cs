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
using System.Threading.Tasks;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.VSIntegration.Contracts;
using StreamJsonRpc;

namespace Microsoft.PythonTools.LanguageServerClient.FileWatcher {
    class Listener : IDisposable {
        private JsonRpc _rpc;
        private readonly IFileWatcherService _WorkspaceFileWatcher;
        private System.IO.FileSystemWatcher _solutionWatcher;
        private readonly object _watchersLock = new object();
        private readonly Dictionary<string, Matcher> _matchers = new Dictionary<string, Matcher>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, System.IO.FileSystemWatcher> _externalWatchers =
            new Dictionary<string, System.IO.FileSystemWatcher>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, FileSystemWatcher[]> _registrations =
            new Dictionary<string, FileSystemWatcher[]>(StringComparer.Ordinal);
        private readonly List<FileSystemWatcher> _excludes = new List<FileSystemWatcher>();
        private bool disposedValue;
        private string _root;

        public Listener(StreamJsonRpc.JsonRpc rpc, IVsFolderWorkspaceService workspaceService, IServiceProvider site) {
            this._rpc = rpc;
            this._rpc.Disconnected += _rpc_Disconnected;

            _WorkspaceFileWatcher = null;
            // Depending upon if this is a workspace or a solution, listen to different change events.
            if (workspaceService?.CurrentWorkspace != null) {
                _WorkspaceFileWatcher = workspaceService.CurrentWorkspace.GetService<IFileWatcherService>();
                _root = PathUtils.NormalizeDirectoryPath(workspaceService.CurrentWorkspace.Location);
                if (!string.IsNullOrEmpty(_root)) {
                    _matchers[_root] = CreateMatcher();
                }
                if (_WorkspaceFileWatcher != null) {
                    _WorkspaceFileWatcher.OnFileSystemChanged += OnFileChanged;
                }
            } else {
                var path = GetSolutionDirectory(site);
                if (path != null) {
                    _root = PathUtils.NormalizeDirectoryPath(path);
                    if (!string.IsNullOrEmpty(_root)) {
                        _matchers[_root] = CreateMatcher();
                        _solutionWatcher = CreateFileSystemWatcher(_root);
                    }
                }
            }
        }

        private void _rpc_Disconnected(object sender, JsonRpcDisconnectedEventArgs e) {
            lock (_watchersLock) {
                if (_rpc != null) {
                    _rpc.Disconnected -= _rpc_Disconnected;
                    _rpc = null;
                }
            }
        }

        private string GetSolutionDirectory(IServiceProvider site) {
            try {
                var dte = (EnvDTE80.DTE2)site.GetService(typeof(EnvDTE.DTE));
                var path = dte?.Solution?.FileName;
                return Path.GetDirectoryName(path);

            } catch {
                return null;
            }

        }

        public void RegisterPatterns(string registrationId, FileSystemWatcher[] patterns) {
            System.IO.FileSystemWatcher[] obsoleteWatchers;
            lock (_watchersLock) {
                if (disposedValue) {
                    return;
                }

                _registrations[registrationId ?? string.Empty] = patterns ?? Array.Empty<FileSystemWatcher>();
                obsoleteWatchers = RebuildMatchers();
            }
            DisposeWatchers(obsoleteWatchers);
        }

        public void UnregisterPatterns(string registrationId) {
            System.IO.FileSystemWatcher[] obsoleteWatchers;
            lock (_watchersLock) {
                if (disposedValue || !_registrations.Remove(registrationId ?? string.Empty)) {
                    return;
                }

                obsoleteWatchers = RebuildMatchers();
            }
            DisposeWatchers(obsoleteWatchers);
        }

        public void AddExclude(FileSystemWatcher pattern) {
            System.IO.FileSystemWatcher[] obsoleteWatchers;
            lock (_watchersLock) {
                if (disposedValue || pattern == null) {
                    return;
                }

                _excludes.Add(pattern);
                obsoleteWatchers = RebuildMatchers();
            }
            DisposeWatchers(obsoleteWatchers);
        }

        private System.IO.FileSystemWatcher[] RebuildMatchers() {
            _matchers.Clear();
            if (!string.IsNullOrEmpty(_root)) {
                _matchers[_root] = CreateMatcher();
            }

            var externalRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pattern in _excludes) {
                AddPattern(pattern, include: false, externalRoots);
            }
            foreach (var patterns in _registrations.Values) {
                foreach (var pattern in patterns) {
                    AddPattern(pattern, include: true, externalRoots);
                }
            }

            var obsoleteWatchers = _externalWatchers
                .Where(entry => !externalRoots.Contains(entry.Key))
                .ToArray();
            foreach (var entry in obsoleteWatchers) {
                _externalWatchers.Remove(entry.Key);
            }
            foreach (var root in externalRoots.Where(root => !_externalWatchers.ContainsKey(root))) {
                TryCreateExternalWatcher(root);
            }

            return obsoleteWatchers.Select(entry => entry.Value).ToArray();
        }

        private void AddPattern(FileSystemWatcher pattern, bool include, HashSet<string> externalRoots) {
            var root = GetPatternRoot(pattern?.GlobPattern);
            if (root != null) {
                var matcher = GetOrCreateMatcher(root);
                if (include) {
                    matcher.AddInclude(pattern.GlobPattern.Pattern);
                } else {
                    matcher.AddExclude(pattern.GlobPattern.Pattern);
                }
                if (!IsDirectoryUnderRoot(root, _root) && Directory.Exists(root)) {
                    externalRoots.Add(root);
                }
            }
        }

        private string GetPatternRoot(GlobPattern globPattern) {
            if (string.IsNullOrEmpty(globPattern?.Pattern)) {
                return null;
            }
            if (globPattern.BaseUri == null) {
                return _root;
            }
            if (!globPattern.BaseUri.IsFile) {
                return null;
            }

            var path = globPattern.BaseUri.LocalPath;
            if (path.Length >= 3 &&
                (path[0] == Path.DirectorySeparatorChar || path[0] == Path.AltDirectorySeparatorChar) &&
                char.IsLetter(path[1]) &&
                path[2] == ':') {
                path = path.Substring(1);
            }
            return PathUtils.NormalizeDirectoryPath(path);
        }

        private Matcher GetOrCreateMatcher(string root) {
            if (!_matchers.TryGetValue(root, out var matcher)) {
                matcher = CreateMatcher();
                _matchers[root] = matcher;
            }
            return matcher;
        }

        private static Matcher CreateMatcher() {
            var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            matcher.AddExclude("**/.vs/**/*.*");
            matcher.AddExclude("**/*~");
            matcher.AddExclude("**/~*");
            matcher.AddExclude("**/*TMP");
            matcher.AddExclude("**/__pycache__");
            return matcher;
        }

        private void TryCreateExternalWatcher(string root) {
            try {
                _externalWatchers[root] = CreateFileSystemWatcher(root);
            } catch (ArgumentException ex) {
                Trace.TraceWarning("Unable to watch Pylance path '{0}': {1}", root, ex);
            } catch (IOException ex) {
                Trace.TraceWarning("Unable to watch Pylance path '{0}': {1}", root, ex);
            } catch (UnauthorizedAccessException ex) {
                Trace.TraceWarning("Unable to watch Pylance path '{0}': {1}", root, ex);
            }
        }

        private static void DisposeWatchers(System.IO.FileSystemWatcher[] watchers) {
            Array.ForEach(watchers, watcher => watcher.Dispose());
        }

        private System.IO.FileSystemWatcher CreateFileSystemWatcher(string root) {
            var watcher = new System.IO.FileSystemWatcher {
                Path = root,
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime |
                    NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.Size
            };
            watcher.Changed += OnFileChanged_Sync;
            watcher.Created += OnFileChanged_Sync;
            watcher.Deleted += OnFileChanged_Sync;
            watcher.Renamed += OnFileChanged_Sync;
            watcher.EnableRaisingEvents = true;
            return watcher;
        }

        private static bool IsDirectoryUnderRoot(string path, string root) {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(root)) {
                return false;
            }

            var relativePath = PathUtils.GetRelativeDirectoryPath(root, path);
            return IsRelativePathUnderRoot(relativePath);
        }

        private static bool IsFileUnderRoot(string path, string root) {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(root)) {
                return false;
            }

            return IsRelativePathUnderRoot(PathUtils.GetRelativeFilePath(root, path));
        }

        private static bool IsRelativePathUnderRoot(string relativePath) {
            return string.IsNullOrEmpty(relativePath) ||
                (!Path.IsPathRooted(relativePath) &&
                 !relativePath.Equals("..", StringComparison.Ordinal) &&
                 !relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal));
        }

        private bool MatchesPattern(string fullPath) {
            lock (_watchersLock) {
                foreach (var entry in _matchers) {
                    if (!IsFileUnderRoot(fullPath, entry.Key)) {
                        continue;
                    }

                    var item = new InMemoryDirectoryInfo(entry.Key, new[] { fullPath });
                    if (entry.Value.Execute(item).HasMatches) {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool Equals(System.IO.FileSystemWatcher l, System.IO.FileSystemWatcher r) {
            if (l == r) {
                return true;
            }
            if (l.Path == r.Path && l.IncludeSubdirectories == r.IncludeSubdirectories && r.Filter == l.Filter) {
                return true;
            }
            return false;
        }

        private async void OnFileChanged_Sync(object sender, System.IO.FileSystemEventArgs e) {
            await OnFileChanged(sender, e);
        }
        private async Task OnFileChanged(object sender, System.IO.FileSystemEventArgs e) {

            // Snapshot _rpc into a local. The field is nulled by _rpc_Disconnected
            // (or by Dispose) and can race with this thread-pool handler.
            JsonRpc rpc;
            lock (_watchersLock) {
                if (disposedValue) {
                    return;
                }
                rpc = _rpc;
            }

            // Skip directory change events
            if (e.IsDirectoryChanged() || rpc == null) {
                return;
            }

            // See if this matches one of our patterns.
            if (MatchesPattern(e.FullPath)) {
                // Send out the event to the language server
                var renamedArgs = e as System.IO.RenamedEventArgs;
                var didChangeParams = new DidChangeWatchedFilesParams();

                // Visual Studio actually does a rename when saving. The rename is from a file ending with '~'
                if (renamedArgs == null || renamedArgs.OldFullPath.EndsWith("~")) {
                    renamedArgs = null;
                    didChangeParams.Changes = new FileEvent[] { new FileEvent() };
                    didChangeParams.Changes[0].Uri = new Uri(e.FullPath);

                    switch (e.ChangeType) {
                        case WatcherChangeTypes.Created:
                            didChangeParams.Changes[0].FileChangeType = FileChangeType.Created;
                            break;
                        case WatcherChangeTypes.Deleted:
                            didChangeParams.Changes[0].FileChangeType = FileChangeType.Deleted;
                            break;
                        case WatcherChangeTypes.Changed:
                            didChangeParams.Changes[0].FileChangeType = FileChangeType.Changed;
                            break;
                        case WatcherChangeTypes.Renamed:
                            didChangeParams.Changes[0].FileChangeType = FileChangeType.Changed;
                            break;

                        default:
                            didChangeParams.Changes = Array.Empty<FileEvent>();
                            break;
                    }
                } else {
                    // file renamed
                    var deleteEvent = new FileEvent();
                    deleteEvent.FileChangeType = FileChangeType.Deleted;
                    deleteEvent.Uri = new Uri(renamedArgs.OldFullPath);

                    var createEvent = new FileEvent();
                    createEvent.FileChangeType = FileChangeType.Created;
                    createEvent.Uri = new Uri(renamedArgs.FullPath);

                    didChangeParams.Changes = new FileEvent[] { deleteEvent, createEvent };
                }

                if (didChangeParams.Changes.Any()) {
                    try {
                        await rpc.NotifyWithParameterObjectAsync(Methods.WorkspaceDidChangeWatchedFiles.Name, didChangeParams);
                    } catch (ObjectDisposedException) {
                        // The JsonRpc was disposed by its owner before Disconnected reached us.
                    } catch (ConnectionLostException) {
                        // Pylance crashed or its pipe closed mid-notify.
                    }
                }
            }
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposing) {
                lock (_watchersLock) {
                    disposedValue = true;
                }
                return;
            }

            System.IO.FileSystemWatcher solutionWatcher;
            System.IO.FileSystemWatcher[] externalWatchers;
            JsonRpc rpc;
            lock (_watchersLock) {
                if (disposedValue) {
                    return;
                }

                disposedValue = true;
                solutionWatcher = _solutionWatcher;
                _solutionWatcher = null;
                externalWatchers = _externalWatchers.Values.ToArray();
                _externalWatchers.Clear();
                _registrations.Clear();
                _excludes.Clear();
                _matchers.Clear();
                rpc = _rpc;
                _rpc = null;
            }

            solutionWatcher?.Dispose();
            DisposeWatchers(externalWatchers);

            if (_WorkspaceFileWatcher != null) {
                _WorkspaceFileWatcher.OnFileSystemChanged -= OnFileChanged;
            }

            if (rpc != null) {
                rpc.Disconnected -= _rpc_Disconnected;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~Listener()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose() {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}

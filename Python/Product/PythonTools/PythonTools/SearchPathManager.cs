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
using System.Linq;
using System.Threading;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio;

namespace Microsoft.PythonTools {
    sealed class SearchPathManager : IVsFileChangeEvents, IDisposable {
        private readonly IVsFileChangeEx _changeService;
        private readonly Timer _notifyChangeTimer;
        private readonly List<SearchPath> _paths = new List<SearchPath>();

        public SearchPathManager(IServiceProvider site) {
            _changeService = site.GetService(typeof(SVsFileChangeEx)) as IVsFileChangeEx;
            _notifyChangeTimer = new Timer(RaiseChanged, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Dispose() {
            uint[] cookies;
            lock (_paths) {
                cookies = _paths.Select(p => p.Cookie).ToArray();
                _paths.Clear();
            }

            Unwatch(cookies);

            var timer = _notifyChangeTimer;
            if (timer != null) {
                timer.Dispose();
            }
        }

        public event EventHandler Changed;

        public IList<string> GetAbsoluteSearchPaths() {
            lock (_paths) {
                return _paths.Select(p => p.Path).ToArray();
            }
        }

        public IList<string> GetAbsolutePersistedSearchPaths() {
            lock (_paths) {
                return _paths.Where(p => p.Persisted).Select(p => p.Path).ToArray();
            }
        }

        public void Add(string absolutePath, bool persisted, object moniker = null) {
            absolutePath = PathUtils.TrimEndSeparator(absolutePath);
            if (string.IsNullOrEmpty(absolutePath)) {
                throw new ArgumentException("cannot be null or empty", nameof(absolutePath));
            }

            var cookie = Watch(absolutePath);
            lock (_paths) {
                _paths.Add(new SearchPath(absolutePath, persisted, moniker, cookie));
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Remove(string absolutePath) {
            absolutePath = PathUtils.TrimEndSeparator(absolutePath);
            if (string.IsNullOrEmpty(absolutePath)) {
                throw new ArgumentException("cannot be null or empty", nameof(absolutePath));
            }

            uint? removed = null;
            lock (_paths) {
                var toRemove = _paths.FirstOrDefault(p => p.Path.Equals(absolutePath, StringComparison.OrdinalIgnoreCase));
                if (toRemove.Path != null && _paths.Remove(toRemove)) {
                    removed = toRemove.Cookie;
                }
            }
            if (removed != null) {
                Unwatch(removed.Value);
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool AddOrReplace(object moniker, string absolutePath, bool isPersisted) {
            absolutePath = PathUtils.TrimEndSeparator(absolutePath);
            if (string.IsNullOrEmpty(absolutePath)) {
                throw new ArgumentException("cannot be null or empty", nameof(absolutePath));
            }
            if (moniker == null) {
                throw new ArgumentNullException("cannot be null", nameof(moniker));
            }

            var cookie = Watch(absolutePath);
            bool any = false, changed = false;
            lock (_paths) {
                for (int i = 0; i < _paths.Count; ++i) {
                    var p = _paths[i];
                    if (p.Moniker == moniker) {
                        if (any) {
                            throw new InvalidOperationException("multiple entries for the one moniker");
                        }
                        any = true;
                        if (!p.Path.Equals(absolutePath, StringComparison.OrdinalIgnoreCase) ||
                            p.Persisted != isPersisted) {
                            _paths[i] = new SearchPath(absolutePath, isPersisted, moniker, cookie);
                            changed = true;
                        }
                    }
                }
                if (!any) {
                    _paths.Add(new SearchPath(absolutePath, isPersisted, moniker, cookie));
                    changed = true;
                }
            }
            if (changed) {
                Changed?.Invoke(this, EventArgs.Empty);
            }
            return changed;
        }

        public void RemoveByMoniker(object moniker) {
            bool any;
            uint[] removed;
            lock (_paths) {
                removed = _paths.Where(p => p.Moniker == moniker).Select(p => p.Cookie).ToArray();
                any = _paths.RemoveAll(p => p.Moniker == moniker) > 0;
            }

            if (any) {
                Unwatch(removed);
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public void LoadPathsFromString(string projectHome, string setting) {
            var newPaths = new List<SearchPath>();
            if (!string.IsNullOrEmpty(setting)) {
                foreach (var path in setting.Split(';')) {
                    if (string.IsNullOrEmpty(path)) {
                        continue;
                    }

                    if (string.IsNullOrEmpty(projectHome)) {
                        newPaths.Add(new SearchPath(path, true, null, Watch(path)));
                    } else {
                        var absolutePath = PathUtils.GetAbsoluteFilePath(projectHome, path);
                        newPaths.Add(new SearchPath(absolutePath, true, null, Watch(absolutePath)));
                    }
                }
            }

            uint[] removed;
            lock (_paths) {
                removed = _paths.Where(p => p.Moniker == null).Select(p => p.Cookie).ToArray();
                _paths.RemoveAll(p => p.Moniker == null);
                _paths.InsertRange(0, newPaths);
            }
            Unwatch(removed);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public string SavePathsToString(string projectHome) {
            List<string> paths;

            lock (_paths) {
                paths = _paths.Where(p => p.Persisted).Select(p => p.Path).ToList();
            }

            if (!string.IsNullOrEmpty(projectHome)) {
                for (int i = 0; i < paths.Count; ++i) {
                    paths[i] = PathUtils.GetRelativeFilePath(projectHome, paths[i]);
                    if (string.IsNullOrEmpty(paths[i])) {
                        paths[i] = ".";
                    }
                }
            }

            return string.Join(";", paths);
        }


        internal struct SearchPath {
            public string Path;
            public bool Persisted;
            public object Moniker;
            public uint Cookie;

            public SearchPath(string path, bool persisted, object moniker, uint cookie) {
                Path = path;
                Persisted = persisted;
                Moniker = moniker;
                Cookie = cookie;
            }
        }

        private uint Watch(string path) {
            if (_changeService == null) {
                return 0;
            }

            uint cookie;
            if (ErrorHandler.Succeeded(_changeService.AdviseDirChange(path, 1, this, out cookie))) {
                return cookie;
            }
            return 0;
        }

        private void Unwatch(uint[] cookies) {
            foreach (var cookie in cookies) {
                Unwatch(cookie);
            }
        }

        private void Unwatch(uint cookie) {
            if (_changeService == null || cookie == 0) {
                return;
            }

            ErrorHandler.ThrowOnFailure(_changeService.UnadviseDirChange(cookie));
        }

        private void RaiseChanged(object state) {
            try {
                _notifyChangeTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            } catch (ObjectDisposedException) {
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public int FilesChanged(uint cChanges, string[] rgpszFile, uint[] rggrfChange) {
            return VSConstants.S_OK;
        }

        int IVsFileChangeEvents.DirectoryChanged(string pszDirectory) {
            try {
                _notifyChangeTimer?.Change(500, Timeout.Infinite);
            } catch (ObjectDisposedException) {
            }
            return VSConstants.S_OK;
        }
    }
}

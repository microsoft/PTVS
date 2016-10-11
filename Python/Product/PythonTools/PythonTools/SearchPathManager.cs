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
using System.Linq;
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools {
    class SearchPathManager {
        private readonly List<SearchPath> _paths = new List<SearchPath>();

        public SearchPathManager() { }

        public event EventHandler Changed;

        public IList<string> GetRelativeSearchPaths(string root) {
            lock (_paths) {
                return _paths.Select(p => PathUtils.GetRelativeFilePath(root, p.Path)).ToArray();
            }
        }

        public IList<string> GetAbsoluteSearchPaths() {
            lock (_paths) {
                return _paths.Select(p => p.Path).ToArray();
            }
        }

        public IList<string> GetRelativePersistedSearchPaths(string root) {
            lock (_paths) {
                return _paths.Where(p => p.Persisted).Select(p => PathUtils.GetRelativeFilePath(root, p.Path)).ToArray();
            }
        }

        public IList<string> GetAbsolutePersistedSearchPaths() {
            lock (_paths) {
                return _paths.Where(p => p.Persisted).Select(p => p.Path).ToArray();
            }
        }

        public void Add(string absolutePath, bool persisted) {
            absolutePath = PathUtils.TrimEndSeparator(absolutePath);
            if (string.IsNullOrEmpty(absolutePath)) {
                throw new ArgumentException("cannot be null or empty", nameof(absolutePath));
            }

            lock (_paths) {
                _paths.Add(new SearchPath(absolutePath, persisted));
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Insert(int index, string absolutePath, bool persisted) {
            absolutePath = PathUtils.TrimEndSeparator(absolutePath);
            if (string.IsNullOrEmpty(absolutePath)) {
                throw new ArgumentException("cannot be null or empty", nameof(absolutePath));
            }

            lock (_paths) {
                _paths.Insert(index, new SearchPath(absolutePath, persisted));
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public bool Contains(string absolutePath) {
            absolutePath = PathUtils.TrimEndSeparator(absolutePath);
            if (string.IsNullOrEmpty(absolutePath)) {
                throw new ArgumentException("cannot be null or empty", nameof(absolutePath));
            }

            lock (_paths) {
                return _paths.Any(p => p.Path.Equals(absolutePath, StringComparison.OrdinalIgnoreCase));
            }
        }

        public bool Contains(string absolutePath, bool isPersisted) {
            absolutePath = PathUtils.TrimEndSeparator(absolutePath);
            if (string.IsNullOrEmpty(absolutePath)) {
                throw new ArgumentException("cannot be null or empty", nameof(absolutePath));
            }

            lock (_paths) {
                return _paths.Any(p => p.Persisted == isPersisted && p.Path.Equals(absolutePath, StringComparison.OrdinalIgnoreCase));
            }
        }

        public void Clear() {
            bool any;
            lock (_paths) {
                any = _paths.Any();
                _paths.Clear();
            }

            if (any) {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Remove(string absolutePath) {
            absolutePath = PathUtils.TrimEndSeparator(absolutePath);
            if (string.IsNullOrEmpty(absolutePath)) {
                throw new ArgumentException("cannot be null or empty", nameof(absolutePath));
            }

            bool removed = false;
            lock (_paths) {
                var toRemove = _paths.FirstOrDefault(p => p.Path.Equals(absolutePath, StringComparison.OrdinalIgnoreCase));
                if (toRemove.Path != null && _paths.Remove(toRemove)) {
                    removed = true;
                }
            }
            if (removed) {
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
                        newPaths.Add(new SearchPath(path, true));
                    } else {
                        newPaths.Add(new SearchPath(PathUtils.GetAbsoluteFilePath(projectHome, path), true));
                    }
                }
            }

            lock (_paths) {
                _paths.Clear();
                _paths.AddRange(newPaths);
            }
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

            public SearchPath(string path, bool persisted) {
                Path = path;
                Persisted = persisted;
            }
        }
    }
}

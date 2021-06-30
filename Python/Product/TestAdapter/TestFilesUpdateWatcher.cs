// Visual Studio Shared Project
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
using System.Collections.Concurrent;
using System.IO;
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.VisualStudioTools.TestAdapter {
    class TestFilesUpdateWatcher : IDisposable {
        private readonly ConcurrentDictionary<string, FileSystemWatcher> _fileWatchers;
        public event EventHandler<TestFileChangedEventArgs> FileChangedEvent;

        public TestFilesUpdateWatcher() {
            _fileWatchers = new ConcurrentDictionary<string, FileSystemWatcher>(StringComparer.OrdinalIgnoreCase);
        }

        public bool AddWatch(string path) {
            if (!String.IsNullOrEmpty(path)) {
                var directoryName = Path.GetDirectoryName(path);
                var filter = Path.GetFileName(path);// String.Format("*{0}",Path.GetFileName(path));

                if (!_fileWatchers.ContainsKey(path) && Directory.Exists(directoryName)) {
                    try {
                        var watcher = new FileSystemWatcher(directoryName, filter);
                        _fileWatchers[path] = watcher;

                        watcher.NotifyFilter = NotifyFilters.LastWrite
                                            | NotifyFilters.FileName
                                            | NotifyFilters.DirectoryName
                                            | NotifyFilters.CreationTime;
                        watcher.Changed += OnChanged;  //only handle on change  in project mode
                        watcher.Renamed += OnRenamed;
                        watcher.EnableRaisingEvents = true;
                        return true;
                    } catch (Exception ex) when (!ex.IsCriticalException()) {
                        ex.ReportUnhandledException(null, GetType());
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// No project event listeners in open folder mode so directory listener will handle more events
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public bool AddDirectoryWatch(string path) {
            if (!String.IsNullOrEmpty(path)) {
                if (!_fileWatchers.ContainsKey(path) && Directory.Exists(path)) {
                    var watcher = new FileSystemWatcher(path);
                    _fileWatchers[path] = watcher;

                    watcher.NotifyFilter = NotifyFilters.LastWrite
                                     | NotifyFilters.FileName
                                     | NotifyFilters.DirectoryName
                                     | NotifyFilters.CreationTime;

                    watcher.IncludeSubdirectories = true;
                    watcher.Changed += OnChanged;
                    watcher.Renamed += OnRenamed;
                    watcher.Created += OnCreated;
                    watcher.Deleted += OnDeleted;
                    watcher.EnableRaisingEvents = true;
                    return true;
                }
            }
            return false;
        }

        public void RemoveWatch(string path) {
            if (!String.IsNullOrEmpty(path)
                && _fileWatchers.TryRemove(path, out FileSystemWatcher watcher)) {
                watcher.EnableRaisingEvents = false;
                watcher.Changed -= OnChanged;
                watcher.Renamed -= OnRenamed;
                watcher.Created -= OnCreated;
                watcher.Deleted -= OnDeleted;
                watcher.Dispose();
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e) {
            var evt = FileChangedEvent;
            if (evt != null) {
                evt(sender, new TestFileChangedEventArgs(null, e.FullPath, TestFileChangedReason.Changed));
            }
        }

        private void OnRenamed(object sender, RenamedEventArgs e) {
            var evt = FileChangedEvent;
            if (evt != null) {
                evt(sender, new TestFileChangedEventArgs(null, e.FullPath, TestFileChangedReason.Renamed, e.OldFullPath));
            }
        }

        private void OnCreated(object sender, FileSystemEventArgs e) {
            var evt = FileChangedEvent;
            if (evt != null) {
                evt(sender, new TestFileChangedEventArgs(null, e.FullPath, TestFileChangedReason.Added));
            }
        }

        private void OnDeleted(object sender, FileSystemEventArgs e) {
            var evt = FileChangedEvent;
            if (evt != null) {
                evt(sender, new TestFileChangedEventArgs(null, e.FullPath, TestFileChangedReason.Removed));
            }
        }

        public void Dispose() {
            Dispose(true);
            // Use SupressFinalize in case a subclass
            // of this type implements a finalizer.
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (disposing && _fileWatchers != null) {
                foreach (var watcher in _fileWatchers.Values) {
                    if (watcher != null) {
                        watcher.Changed -= OnChanged;
                        watcher.Renamed -= OnRenamed;
                        watcher.Created -= OnCreated;
                        watcher.Deleted -= OnDeleted;
                        watcher.Dispose();
                    }
                }

                _fileWatchers.Clear();
            }
        }
    }
}

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
using System.IO;
using System.Linq;
using Microsoft.DsTools.Core.Disposables;

namespace Microsoft.Python.LanguageServer {
    sealed class PathsWatcher: IDisposable {
        private readonly DisposableBag _disposableBag = new DisposableBag(nameof(PathsWatcher));
        private readonly Action<FileSystemEventArgs> _onChanged;

        public PathsWatcher(string[] paths, Action<FileSystemEventArgs> onChanged) {
            if (paths?.Length == 0) {
                return;
            }

            _onChanged = onChanged;

            var list = new List<FileSystemWatcher>();
            var reduced = ReduceToCommonRoots(paths);
            foreach (var p in reduced) {
                var fsw = new FileSystemWatcher(p);
                fsw.IncludeSubdirectories = true;
                fsw.EnableRaisingEvents = true;

                _disposableBag
                    .Add(() => fsw.Changed -= OnChanged)
                    .Add(() => fsw.Created -= OnChanged)
                    .Add(() => fsw.Deleted -= OnChanged)
                    .Add(fsw);
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e) => _onChanged(e);

        public void Dispose() {
            _disposableBag.ThrowIfDisposed();
            _disposableBag.TryDispose();
        }

        private IEnumerable<string> ReduceToCommonRoots(string[] paths) {
            if (paths.Length == 0) {
                return paths;
            }

            var original = paths.OrderBy(s => s.Length).ToList();
            List<string> reduced = null;

            while (reduced == null || original.Count > reduced.Count) {
                var shortest = original[0];
                reduced = new List<string>();
                reduced.Add(shortest);
                for (var i = 1; i < original.Count; i++) {
                    // take all that do not start with the shortest
                    if (!original[i].StartsWith(shortest)) {
                        reduced.Add(original[i]);
                    }
                }
                original = reduced;
            }
            return reduced;
        }
    }
}

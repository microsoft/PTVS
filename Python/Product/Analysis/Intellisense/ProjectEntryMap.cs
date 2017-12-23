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
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Intellisense {
    sealed class ProjectEntryMap : IEnumerable<KeyValuePair<string, IProjectEntry>> {
        private readonly List<IProjectEntry> _ids = new List<IProjectEntry>();
        private readonly Stack<int> _freedIds = new Stack<int>();
        private readonly ConcurrentDictionary<Uri, IProjectEntry> _projectFiles = new ConcurrentDictionary<Uri, IProjectEntry>();
        private static object _idKey = new object();

        /// <summary>
        /// Must be called from the owning thread
        /// </summary>
        public int Add(string filename, IProjectEntry node) {
#if DEBUG
            foreach (var item in _ids) {
                Debug.Assert(node != item);
            }
#endif
            int id;
            if (_freedIds.Count > 0) {
                var i = _freedIds.Pop();
                _ids[i] = node;
                id = i + 1;
            } else {
                _ids.Add(node);
                // ids are 1 based
                id = _ids.Count;
            }
            _projectFiles[new Uri(filename)] = node;
            node.Properties[_idKey] = id;
            return id;
        }

        /// <summary>
        /// Must be called from the owning thread
        /// </summary>
        public void Remove(IProjectEntry node) {
            int i = GetId(node) - 1;
            if (i < 0 ||
                i >= _ids.Count ||
                !object.ReferenceEquals(node, _ids[i])) {
                throw new InvalidOperationException("Removing node with invalid ID or map is corrupted");
            }

            _ids[i] = null;
            _freedIds.Push(i);
            IProjectEntry removed;
            _projectFiles.TryRemove(new Uri(node.FilePath), out removed);
        }

        /// <summary>
        /// Must be called from the owning thread
        /// </summary>
        public void Clear() {
            _ids.Clear();
            _projectFiles.Clear();
            _freedIds.Clear();
        }

        public static int GetId(IProjectEntry node) {
            if (node == null) {
                return -1;
            }

            return (int)node.Properties[_idKey];
        }

        public IEnumerator<KeyValuePair<string, IProjectEntry>> GetEnumerator() {
            return _projectFiles.Select(kv => new KeyValuePair<string, IProjectEntry>(kv.Key.AbsolutePath.Replace('/', '\\'), kv.Value)).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        /// <summary>
        /// Must be called from the UI thread
        /// </summary>
        public IProjectEntry this[int itemId] {
            get {
                int i = (int)itemId - 1;
                if (0 <= i && i < _ids.Count) {
                    return _ids[i];
                }
                return null;
            }
        }

        public IProjectEntry Get(int itemId) {
            var r = this[itemId];
            if (r == null) {
                throw new ProjectEntryNotFoundException(itemId);
            }
            return r;
        }

        public T Get<T>(int itemId) where T : class, IProjectEntry {
            return Get(itemId) as T;
        }

        public bool TryGetValue(string path, out IProjectEntry item) {
            return _projectFiles.TryGetValue(new Uri(path), out item);
        }

        public bool TryGetValue(Uri path, out IProjectEntry item) {
            return _projectFiles.TryGetValue(path, out item);
        }
    }

    [Serializable]
    sealed class ProjectEntryNotFoundException : KeyNotFoundException {
        public ProjectEntryNotFoundException(int itemId) : base($"Entry Id {itemId}") { }
    }
}

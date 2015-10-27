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
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.VisualStudioTools.Project {
    sealed class HierarchyIdMap {
        private readonly List<HierarchyNode> _ids = new List<HierarchyNode>();
        private readonly Stack<int> _freedIds = new Stack<int>();

        /// <summary>
        /// Must be called from the UI thread
        /// </summary>
        public uint Add(HierarchyNode node) {
#if DEBUG
            foreach (var item in _ids) {
                Debug.Assert(node != item);
            }
#endif
            if (_freedIds.Count > 0) {
                var i = _freedIds.Pop();
                _ids[i] = node;
                return (uint)i + 1;
            } else {
                _ids.Add(node);
                // ids are 1 based
                return (uint)_ids.Count;
            }
        }

        /// <summary>
        /// Must be called from the UI thread
        /// </summary>
        public void Remove(HierarchyNode node) {
            int i = (int)node.ID - 1;
            if(i < 0 ||
                i >= _ids.Count ||
                !object.ReferenceEquals(node, _ids[i])) {
                throw new InvalidOperationException("Removing node with invalid ID or map is corrupted");
            }

            _ids[i] = null;
            _freedIds.Push(i);
        }

        /// <summary>
        /// Must be called from the UI thread
        /// </summary>
        public HierarchyNode this[uint itemId] {
            get {
                int i = (int)itemId - 1;
                if (0 <= i && i < _ids.Count) {
                    return _ids[i];
                }
                return null;
            }
        }
    }
}

/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System.Collections.Generic;

namespace Microsoft.VisualStudioTools.Project {
    /// <summary>
    /// Alternative to EventSinkCollection.  EventSinkCollection typically has O(n)
    /// performance for additions.  This trades off a little extra memory usage for
    /// removed nodes in favor of O(1) addition time.  Both implementations have take
    /// O(n) for removal.
    /// </summary>
    sealed class HierarchyIdMap {
        private readonly Dictionary<uint, HierarchyNode> _ids = new Dictionary<uint,HierarchyNode>();
        private List<uint> _freedIds = new List<uint>();

        public uint Add(HierarchyNode node) {
            UIThread.MustBeCalledFromUIThread();

            uint res;

            if (_freedIds.Count > 0) {
                res = _freedIds[_freedIds.Count - 1];
                _freedIds.RemoveAt(_freedIds.Count - 1);
            } else {
                // ids are 1 based
                res = (uint)_ids.Count + 1;
            }

            _ids[res] = node;
            return res;
        }

        public void Remove(HierarchyNode node) {
            UIThread.MustBeCalledFromUIThread();

            foreach (var keyValue in _ids) {
                if (keyValue.Value == node) {
                    _ids.Remove(keyValue.Key);
                    _freedIds.Add(keyValue.Key);
                    break;
                }
            }
        }

        public HierarchyNode this[uint itemId] {
            get {
                UIThread.MustBeCalledFromUIThread();

                HierarchyNode res;
                _ids.TryGetValue(itemId, out res);
                return res;
            }
        }

        public uint Count {
            get {
                return (uint)_ids.Count;
            }
        }
    }
}

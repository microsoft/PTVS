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
    sealed class HierarchyIdMap {
        private readonly List<HierarchyNode> _ids = new List<HierarchyNode>();
        private readonly Stack<int> _freedIds = new Stack<int>();

        public uint Add(HierarchyNode node) {
            UIThread.MustBeCalledFromUIThread();

            if (_freedIds.Count > 0) {
                var i = _freedIds.Pop();
                _ids[i] = node;
                return (uint)i;
            } else {
                _ids.Add(node);
                // ids are 1 based
                return (uint)_ids.Count;
            }
        }

        public void Remove(HierarchyNode node) {
            UIThread.MustBeCalledFromUIThread();

            int i = (int)node.ID - 1;
            if (0 <= i && i < _ids.Count && object.ReferenceEquals(node, _ids[i])) {
                _ids[i] = null;
            } else {
                for (i = 0; i < _ids.Count; ++i) {
                    if (object.ReferenceEquals(node, _ids[i])) {
                        _ids[i] = null;
                        _freedIds.Push(i);
                        break;
                    }
                }
            }
        }

        public HierarchyNode this[uint itemId] {
            get {
                UIThread.MustBeCalledFromUIThread();

                int i = (int)itemId - 1;
                if (0 <= i && i < _ids.Count) {
                    return _ids[i];
                }
                return null;
            }
        }

        public uint Count {
            get {
                return (uint)_ids.Count;
            }
        }
    }
}

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

using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudioTools.Navigation {
    /// <summary>
    /// Class used to identify a module. The module is identified using the hierarchy that
    /// contains it and its item id inside the hierarchy.
    /// </summary>
    public sealed class ModuleId {
        private IVsHierarchy _ownerHierarchy;
        private uint _itemId;

        public ModuleId(IVsHierarchy owner, uint id) {
            _ownerHierarchy = owner;
            _itemId = id;
        }

        public IVsHierarchy Hierarchy {
            get { return _ownerHierarchy; }
        }

        public uint ItemID {
            get { return _itemId; }
        }

        public override int GetHashCode() {
            int hash = 0;
            if (null != _ownerHierarchy) {
                hash = _ownerHierarchy.GetHashCode();
            }
            hash = hash ^ (int)_itemId;
            return hash;
        }

        public override bool Equals(object obj) {
            ModuleId other = obj as ModuleId;
            if (null == obj) {
                return false;
            }
            if (!_ownerHierarchy.Equals(other._ownerHierarchy)) {
                return false;
            }
            return (_itemId == other._itemId);
        }
    }
}
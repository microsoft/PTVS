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

using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project
{
    /// <summary>
    /// Represents search path entry as a node in the Solution Explorer.
    /// </summary>
    [ComVisible(true)]
    internal class CommonSearchPathNode : BaseSearchPathNode
    {
        private int _index;
        public CommonSearchPathNode(PythonProjectNode project, string path, int index)
            : base(project, path, new VirtualProjectElement(project))
        {
            _index = index;
        }

        public override int SortPriority
        {
            get
            {
                return PythonConstants.SearchPathNodeMaxSortPriority + _index;
            }
        }

        public override int MenuCommandId
        {
            get { return PythonConstants.SearchPathMenuId; }
        }

        public override Guid MenuGroupId
        {
            get { return GuidList.guidPythonToolsCmdSet; }
        }

        public int Index
        {
            get { return _index; }
            set { _index = value; }
        }

        /// <summary>
        /// Generic Search Path Node can only be removed from project.
        /// </summary>        
        internal override bool CanDeleteItem(__VSDELETEITEMOPERATION deleteOperation)
        {
            return deleteOperation == __VSDELETEITEMOPERATION.DELITEMOP_RemoveFromProject;
        }

        public override bool Remove(bool removeFromStorage)
        {
            //Save this search path, because the node can be deleted after call to base Remove()
            string path = this.Url;
            if (base.Remove(removeFromStorage))
            {
                //Remove entry from project's Search Path
                _project.RemoveSearchPath(path);
                return true;
            }
            return false;
        }

        public void Remove()
        {
            base.Remove(false);
        }
    }
}

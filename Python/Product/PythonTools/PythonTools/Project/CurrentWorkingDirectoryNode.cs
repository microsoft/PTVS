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

using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project
{
    /// <summary>
    /// Represents Current Working Directory node.
    /// </summary>
    internal class CurrentWorkingDirectoryNode : BaseSearchPathNode
    {

        public CurrentWorkingDirectoryNode(PythonProjectNode project, string path)
            : base(project, path, new VirtualProjectElement(project)) { }

        public override string Caption
        {
            get
            {
                return Strings.CurrentWorkingDirectoryCaption.FormatUI(base.Caption);
            }
        }

        //Working Directory node cannot be deleted
        internal override bool CanDeleteItem(Microsoft.VisualStudio.Shell.Interop.__VSDELETEITEMOPERATION deleteOperation)
        {
            return false;
        }

        public override int SortPriority
        {
            get
            {
                return CommonConstants.WorkingDirectorySortPriority;
            }
        }
    }
}

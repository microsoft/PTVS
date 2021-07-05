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

namespace Microsoft.PythonTools.Project {
    [ComVisible(true)]
    [Guid(CommonConstants.SearchPathsPropertiesGuid)]
    public class CommonSearchPathNodeProperties : NodeProperties {
        #region properties
        [SRCategoryAttribute(SR.Misc)]
        [SRDisplayName(SR.FolderName)]
        [SRDescriptionAttribute(SR.FolderNameDescription)]
        [AutomationBrowsable(false)]
        public string FolderName {
            get {
                return PathUtils.GetFileOrDirectoryName(this.HierarchyNode.Url);
            }
        }

        [SRCategoryAttribute(SR.Misc)]
        [SRDisplayName(SR.FullPath)]
        [SRDescriptionAttribute(SR.FullPathDescription)]
        [AutomationBrowsable(true)]
        public string FullPath {
            get {
                return this.HierarchyNode.Url;
            }
        }

        #region properties - used for automation only
        [Browsable(false)]
        [AutomationBrowsable(true)]
        public string FileName {
            get {
                return this.HierarchyNode.Url;
            }
        }

        #endregion

        #endregion

        #region ctors
        internal CommonSearchPathNodeProperties(HierarchyNode node)
            : base(node) { }
        #endregion

        public override string GetClassName() {
            return Strings.SearchPathProperties;
        }
    }
}

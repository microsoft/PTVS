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

namespace Microsoft.PythonTools.Project {
    [ComVisible(true)]
    [Guid(PythonConstants.InterpretersPropertiesGuid)]
    public class InterpretersNodeProperties : NodeProperties {
        [Browsable(false)]
        [AutomationBrowsable(false)]
        protected IPythonInterpreterFactory Factory {
            get {
                var node = HierarchyNode as InterpretersNode;
                if (node != null) {
                    return node._factory;
                }
                return null;
            }
        }

        // TODO: Expose interpreter configuration through properties

        [SRCategory(SR.Misc)]
        [SRDisplayName(SR.FolderName)]
        [SRDescription(SR.FolderNameDescription)]
        [AutomationBrowsable(false)]
        public string FolderName {
            get {
                return PathUtils.GetFileOrDirectoryName(this.HierarchyNode.Url);
            }
        }

        [SRCategory(SR.Misc)]
        [SRDisplayName(SR.FullPath)]
        [SRDescription(SR.FullPathDescription)]
        [AutomationBrowsable(true)]
        public string FullPath {
            get {
                return this.HierarchyNode.Url;
            }
        }

#if DEBUG
        [SRCategory(SR.Misc)]
        [SRDisplayName("EnvironmentIdDisplayName")]
        [SRDescription("EnvironmentIdDescription")]
        [AutomationBrowsable(true)]
        public string Id => Factory?.Configuration?.Id ?? "";
#endif

        [SRCategory(SR.Misc)]
        [SRDisplayName("EnvironmentVersionDisplayName")]
        [SRDescription("EnvironmentVersionDescription")]
        [AutomationBrowsable(true)]
        public string Version => Factory?.Configuration?.Version.ToString() ?? "";

        [Browsable(false)]
        [AutomationBrowsable(true)]
        public string FileName => HierarchyNode.Url;

        internal InterpretersNodeProperties(HierarchyNode node)
            : base(node) { }

        public override string GetClassName() {
            return "Environment Properties";
        }

    }
}

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
    sealed class DeprecatedReferenceNode : ReferenceNode
    {
        private readonly string _caption, _message;

        public DeprecatedReferenceNode(ProjectNode root, string name, string message) : base(root)
        {
            _caption = name;
            _message = message;
        }

        public DeprecatedReferenceNode(ProjectNode root, ProjectElement element, string name, string message) : base(root, element)
        {
            _caption = name;
            _message = message;
        }

        public override string Caption => _caption;
        public string Message => _message;
        protected override ImageMoniker GetIconMoniker(bool open) => KnownMonikers.ReferenceWarning;
        protected override NodeProperties CreatePropertiesObject() => new DeprecatedReferenceNodeProperties(this);

        protected override void BindReferenceData() { }
    }

    [ComVisible(true)]
    public sealed class DeprecatedReferenceNodeProperties : NodeProperties
    {
        internal DeprecatedReferenceNodeProperties(DeprecatedReferenceNode node) : base(node)
        {
        }

        public override string GetClassName() => SR.GetString(SR.ReferenceProperties);

        private new DeprecatedReferenceNode Node => (DeprecatedReferenceNode)HierarchyNode;

        [Browsable(true)]
        [SRCategory(SR.Misc)]
        [SRDisplayName(SR.RefName)]
        [SRDescription(SR.RefNameDescription)]
        [AutomationBrowsable(true)]
        public override string Name => Node.Caption;


        [Browsable(true)]
        [SRCategory(SR.Misc)]
        [SRDisplayName("RefDeprecatedMessage")]
        [SRDescription("RefDeprecatedMessageDescription")]
        [AutomationBrowsable(true)]
        public string Message => Node.Message;
    }
}

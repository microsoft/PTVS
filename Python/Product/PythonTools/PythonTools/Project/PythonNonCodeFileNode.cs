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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.IO;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    class PythonNonCodeFileNode : CommonNonCodeFileNode {
        private object _designerContext;

        public PythonNonCodeFileNode(CommonProjectNode root, ProjectElement e)
            : base(root, e) {
        }

        public override int QueryService(ref Guid guidService, out object result) {
            var model = ProjectMgr.GetService(typeof(SComponentModel)) as IComponentModel;
            var designerSupport = model?.GetService<IXamlDesignerSupport>();
            if (designerSupport != null &&
                guidService == designerSupport.DesignerContextTypeGuid &&
                Path.GetExtension(Url).Equals(".xaml", StringComparison.OrdinalIgnoreCase)) {
                // Create a DesignerContext for the XAML designer for this file
                if (_designerContext == null) {
                    _designerContext = designerSupport.CreateDesignerContext();
                    var child = (
                        // look for spam.py
                        ProjectMgr.FindNodeByFullPath(Path.ChangeExtension(Url, PythonConstants.FileExtension)) ??
                        // then look for spam.pyw
                        ProjectMgr.FindNodeByFullPath(Path.ChangeExtension(Url, PythonConstants.WindowsFileExtension))
                    ) as CommonFileNode;

                    if (child != null) {
                        designerSupport.InitializeEventBindingProvider(
                            _designerContext,
                            (child as PythonFileNode)?.GetProjectEntry(),
                            () => child.GetTextView(),
                            () => child.GetTextBuffer()
                        );
                    }
                }
                result = _designerContext;
                return VSConstants.S_OK;
            }

            return base.QueryService(ref guidService, out result);
        }
    }
}

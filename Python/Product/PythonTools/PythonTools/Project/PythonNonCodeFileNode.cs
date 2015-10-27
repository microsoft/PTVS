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
using Microsoft.VisualStudio;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    class PythonNonCodeFileNode : CommonNonCodeFileNode {
        private object _designerContext;

        public PythonNonCodeFileNode(CommonProjectNode root, ProjectElement e)
            : base(root, e) {
        }

        protected internal object DesignerContext {
            get {
                if (_designerContext == null) {
                    _designerContext = XamlDesignerSupport.CreateDesignerContext();
                    //Set the EventBindingProvider for this XAML file so the designer will call it
                    //when event handlers need to be generated
                    var dirName = Path.GetDirectoryName(Url);
                    var fileName = Path.GetFileNameWithoutExtension(Url);
                    var filenameWithoutExt = Path.Combine(dirName, fileName);

                    // look for fob.py
                    var child = ProjectMgr.FindNodeByFullPath(filenameWithoutExt + PythonConstants.FileExtension);
                    if (child == null) {
                        // then look for fob.pyw
                        child = ProjectMgr.FindNodeByFullPath(filenameWithoutExt + PythonConstants.WindowsFileExtension);
                    }

                    if (child != null) {
                        XamlDesignerSupport.InitializeEventBindingProvider(_designerContext, child as PythonFileNode);
                    }
                }
                return _designerContext;
            }
        }

        public override int QueryService(ref Guid guidService, out object result) {
            if (XamlDesignerSupport.DesignerContextType != null &&
                guidService == XamlDesignerSupport.DesignerContextType.GUID &&
                Path.GetExtension(Url).Equals(".xaml", StringComparison.OrdinalIgnoreCase)) {
                // Create a DesignerContext for the XAML designer for this file
                result = DesignerContext;
                return VSConstants.S_OK;
            }

            return base.QueryService(ref guidService, out result);
        }
    }
}

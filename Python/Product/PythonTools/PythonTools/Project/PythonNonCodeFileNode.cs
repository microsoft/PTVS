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

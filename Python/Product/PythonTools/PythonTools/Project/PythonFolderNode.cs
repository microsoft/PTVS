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
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Node used for a Python package (a directory with __init__ in it).
    /// 
    /// Currently we just provide a specialized icon for the different folder.
    /// </summary>
    class PythonFolderNode : CommonFolderNode {
        private ImageList _imageList;

        public PythonFolderNode(CommonProjectNode root, ProjectElement element)
            : base(root, element) {
        }

        public override object GetIconHandle(bool open) {
            if (ItemNode.IsExcluded) {
                return base.GetIconHandle(open);
            }

            for (HierarchyNode child = this.FirstChild; child != null; child = child.NextSibling) {
                if (child.Url.EndsWith("\\__init__.py", StringComparison.Ordinal)) {
                    if (_imageList == null) {
                        _imageList = Utilities.GetImageList(Assembly.GetExecutingAssembly().GetManifestResourceStream("Microsoft.Resources.PythonPackageIcons.bmp"));
                    }

                    return open ?
                        ((Bitmap)_imageList.Images[0]).GetHicon() :
                        ((Bitmap)_imageList.Images[1]).GetHicon();
                }
            }

            return base.GetIconHandle(open);
        }
    }
}

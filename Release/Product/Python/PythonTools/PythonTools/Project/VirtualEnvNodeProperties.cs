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
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.PythonTools.Project {
    [ComVisible(true)]
    [Guid(PythonConstants.VirtualEnvPropertiesGuid)]
    public class VirtualEnvNodeProperties : NodeProperties {
        #region properties
        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.FolderName)]
        [SRDescriptionAttribute(SR.FolderNameDescription)]
        [AutomationBrowsable(false)]
        public string FolderName {
            get {
                return Path.GetFileName(CommonUtils.TrimEndSeparator(this.Node.Url));
            }
        }

        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.FullPath)]
        [SRDescriptionAttribute(SR.FullPathDescription)]
        [AutomationBrowsable(true)]
        public string FullPath {
            get {
                return this.Node.VirtualNodeName;
            }
        }

        #region properties - used for automation only
        [Browsable(false)]
        [AutomationBrowsable(true)]
        public string FileName {
            get {
                return this.Node.VirtualNodeName;
            }
        }

        #endregion

        #endregion

        #region ctors
        public VirtualEnvNodeProperties(HierarchyNode node)
            : base(node) { }
        #endregion

        public override string GetClassName() {
            return "Virtual Env Properties";
        }
    }
}

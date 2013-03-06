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

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using VSLangProj;

namespace Microsoft.PythonTools.Project.Automation {
    [SuppressMessage("Microsoft.Interoperability", "CA1405:ComVisibleTypeBaseTypesShouldBeComVisible")]
    [ComVisible(true)]
    public class OAWebPiReference : OAReferenceBase<WebPiReferenceNode> {
        public OAWebPiReference(WebPiReferenceNode webPiReferenceNode) :
            base(webPiReferenceNode) {
        }
        
        [SRCategoryAttribute(SR.Advanced)]
        [LocDisplayName(SR.BuildAction)]
        [SRDescriptionAttribute(SR.BuildActionDescription)]
        [TypeConverter(typeof(BuildActionTypeConverter))]
        public prjBuildAction BuildAction {
            get {
                return prjBuildAction.prjBuildActionNone;
            }
        }

        #region Reference override
       
        public override string Name {
            get {
                return System.IO.Path.GetFileNameWithoutExtension(BaseReferenceNode.Url);
            }
        }

        public override prjReferenceType Type {
            get {
                return prjReferenceType.prjReferenceTypeAssembly;
            }
        }
        #endregion
    }
}

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

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.VisualStudioTools.Project.Automation;
using VSLangProj;
using VSLangProj80;

namespace Microsoft.PythonTools.Project.Automation {
    [ComVisible(true)]
    public class OAPythonExtensionReference : OAReferenceBase {
        internal OAPythonExtensionReference(PythonExtensionReferenceNode pythonExtensionReferenceNode) :
            base(pythonExtensionReferenceNode) {
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

        public override uint RefType {
            get {
                return (uint)__PROJECTREFERENCETYPE.PROJREFTYPE_ASSEMBLY;
            }
        }

        public override bool CopyLocal {
            get {
                return false;
            }
            set { }
        }

        #endregion
    }
}

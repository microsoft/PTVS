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

using EnvDTE;
using Microsoft.VsWizards.ImportProjectFolderWizard;
using Microsoft.VsWizards.ImportProjectFolderWizard.Managed;

namespace Microsoft.PythonTools.Project.Automation {
    /// <summary>
    /// Provides our own version of the PageManager for the New Project from Existing Code wizard.
    /// 
    /// Our version just lets us track when the wizard is adding files so we can tweak the behavior
    /// a little bit.
    /// 
    /// New in 1.1.
    /// </summary>
    public class PythonPageManager : PageManager, IPageManager {
        public PythonPageManager(ImportFolderDialog parent)
            : base(parent) {
        }

        public PythonPageManager(ImportFolderDialog parent, bool closeSolution, bool isSubproject, bool silent, ProjectItems parentProjectItems)
            : base(parent, closeSolution, isSubproject, silent, parentProjectItems) {
        }

        Microsoft.VSWizards.WizardPage IPageManager.CreateProject() {
            OAProjectItems.CreatingFromExistingCode = true;
            try {
                return base.CreateProject();
            } finally {
                OAProjectItems.CreatingFromExistingCode = false;
            }
        }   
    }
}

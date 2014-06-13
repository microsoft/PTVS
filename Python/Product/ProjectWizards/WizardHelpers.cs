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
using System.Windows.Forms;
using EnvDTE;
using Microsoft.PythonTools.ProjectWizards.Properties;
using Microsoft.VisualStudio.Shell;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace Microsoft.PythonTools.ProjectWizards {
    static class WizardHelpers {
        public static IServiceProvider GetProvider(object automationObject) {
            var oleProvider = automationObject as IOleServiceProvider;
            if (oleProvider != null) {
                return new ServiceProvider(oleProvider);
            }
            MessageBox.Show(Resources.ErrorNoDte, Resources.PythonToolsForVisualStudio);
            return null;
        }

        public static DTE GetDTE(object automationObject) {
            var dte = automationObject as DTE;
            if (dte == null) {
                var provider = GetProvider(automationObject);
                if (provider != null) {
                    dte = provider.GetService(typeof(DTE)) as DTE;
                }
            }
            if (dte == null) {
                MessageBox.Show(Resources.ErrorNoDte, Resources.PythonToolsForVisualStudio);
            }
            return dte;
        }
    }
}

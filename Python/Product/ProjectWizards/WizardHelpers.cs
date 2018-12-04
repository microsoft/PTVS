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

using System;
using System.Windows.Forms;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace Microsoft.PythonTools.ProjectWizards {
    static class WizardHelpers {
        public static IServiceProvider GetProvider(object automationObject) {
            var oleProvider = automationObject as IOleServiceProvider;
            if (oleProvider != null) {
                return new ServiceProvider(oleProvider);
            }
            MessageBox.Show(Strings.ErrorNoDte, Strings.ProductTitle);
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
                MessageBox.Show(Strings.ErrorNoDte, Strings.ProductTitle);
            }
            return dte;
        }
    }
}

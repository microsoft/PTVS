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
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Utility {
    public static class MessageBox {
        public static void ShowErrorMessage(IServiceProvider sp, string message) {
            var shell = (IVsUIShell)sp.GetService(typeof(SVsUIShell));
            shell.ShowMessageBox(0, Guid.Empty, null, message, null, 0, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST, OLEMSGICON.OLEMSGICON_CRITICAL, 0, out _);
        }

        public static bool ShowYesNoPrompt(IServiceProvider sp, string message) {
            var shell = (IVsUIShell)sp.GetService(typeof(SVsUIShell));
            shell.ShowMessageBox(0, Guid.Empty, null, message, null, 0, OLEMSGBUTTON.OLEMSGBUTTON_YESNO, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST, OLEMSGICON.OLEMSGICON_QUERY, 0, out var result);
            return result == NativeMethods.IDYES;
        }
    }
}

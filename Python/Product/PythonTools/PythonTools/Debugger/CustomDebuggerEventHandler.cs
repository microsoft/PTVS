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
using System.ComponentModel.Design;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.PythonTools.DkmDebugger;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools.Project;
using NativeMethods = Microsoft.VisualStudioTools.Project.NativeMethods;

namespace Microsoft.PythonTools.Debugger {
    [Guid("996D22BD-D117-4611-88F2-2832CB7D9517")]
    public class CustomDebuggerEventHandler : IVsCustomDebuggerEventHandler110 {
        public int OnCustomDebugEvent(ref Guid ProcessId, VsComponentMessage message) {
            switch ((VsPackageMessage)message.MessageCode) {
                case VsPackageMessage.WarnAboutPythonSymbols:
                    WarnAboutPythonSymbols((string)message.Parameter1);
                    return 0;
                default:
                    return 0;
            }
        }

        private void WarnAboutPythonSymbols(string moduleName) {
            const string content =
                "Python/native mixed-mode debugging requires symbol files for the Python interpreter that is being debugged. Please add the folder " +
                "containing those symbol files to your symbol search path, and force a reload of symbols for {0}.";

            var buttons = new[] {
                new TASKDIALOG_BUTTON { nButtonID = 1, pszButtonText = "Open symbol settings dialog" },
                new TASKDIALOG_BUTTON { nButtonID = 2, pszButtonText = "Download symbols for my interpreter" }
            };

            var pButtons = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TASKDIALOG_BUTTON)) * 2);
            for (int i = 0; i < buttons.Length; ++i) {
                Marshal.StructureToPtr(buttons[i], pButtons + Marshal.SizeOf(typeof(TASKDIALOG_BUTTON)) * i, false);
            }

            var taskDialogConfig = new TASKDIALOGCONFIG {
                cbSize = (uint)Marshal.SizeOf(typeof(TASKDIALOGCONFIG)),
                dwFlags = TASKDIALOG_FLAGS.TDF_USE_COMMAND_LINKS,
                dwCommonButtons = TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_CLOSE_BUTTON,
                pszWindowTitle = "Python Symbols Required",
                pszContent = string.Format(content, moduleName),
                cButtons = (uint)buttons.Length,
                pButtons = pButtons
            };

            var uiShell = (IVsUIShell)PythonToolsPackage.GetGlobalService(typeof(SVsUIShell));
            uiShell.GetDialogOwnerHwnd(out taskDialogConfig.hwndParent);
            uiShell.EnableModeless(0);

            int nButton, nRadioButton;
            bool fVerificationFlagChecked;
            NativeMethods.TaskDialogIndirect(ref taskDialogConfig, out nButton, out nRadioButton, out fVerificationFlagChecked);

            uiShell.EnableModeless(1);
            Marshal.FreeHGlobal(pButtons);

            if (nButton == 1) {
                var cmdId = new CommandID(VSConstants.GUID_VSStandardCommandSet97, VSConstants.cmdidToolsOptions);
                PythonToolsPackage.Instance.GlobalInvoke(cmdId,  "1F5E080F-CBD2-459C-8267-39fd83032166");
            } else if (nButton == 2) {
                PythonToolsPackage.OpenWebBrowser(
                    string.Format("http://go.microsoft.com/fwlink/?LinkId=308954&clcid=0x{0:X}", CultureInfo.CurrentCulture.LCID));
            }
        }
    }
}

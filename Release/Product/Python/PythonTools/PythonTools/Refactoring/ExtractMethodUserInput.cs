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
using System.Windows;
using System.Windows.Interop;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Refactoring {
    class ExtractMethodUserInput : IExtractMethodInput {
        internal static ExtractMethodUserInput Instance = new ExtractMethodUserInput();

        public bool ShouldExpandSelection() {
            var res = MessageBox.Show(@"The selected text does not cover an entire expression.

Would you like the selection to be extended to a valid expression?",
                        "Expand extract method selection?",
                        MessageBoxButton.YesNo
                    );

            return res == MessageBoxResult.Yes;
        }


        public ExtractMethodRequest GetExtractionInfo(ExtractedMethodCreator previewer) {
            var dialog = new ExtractMethodDialog(previewer);
            var shell = (IVsUIShell)PythonToolsPackage.GetGlobalService(typeof(SVsUIShell));
            IntPtr owner;
            if (ErrorHandler.Succeeded(shell.GetDialogOwnerHwnd(out owner))) {
                WindowInteropHelper helper = new WindowInteropHelper(dialog);
                helper.Owner = owner;
            }

            var res = dialog.ShowDialog();
            if (res != null && res.Value) {
                return dialog.GetExtractInfo();
            }

            return null;
        }

        public void CannotExtract(string reason) {
            MessageBox.Show(reason, "Cannot extract method", MessageBoxButton.OK);
        }
    }
}

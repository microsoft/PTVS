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
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Microsoft.PythonTools.Refactoring {
    /// <summary>
    /// Handles input when running the rename refactoring within Visual Studio.
    /// </summary>
    class RenameVariableUserInput : IRenameVariableInput {
        internal static RenameVariableUserInput Instance = new RenameVariableUserInput();

        public const string RefactorGuidStr = "{5A822660-832B-4AF0-9A86-1048D33A05E7}";
        private static readonly Guid RefactorGuid = new Guid(RefactorGuidStr);

        public RenameVariableRequest GetRenameInfo(string originalName) {
            var requestView = new RenameVariableRequestView(originalName);
            var dialog = new RenameVariableDialog(requestView);
            bool res = dialog.ShowModal() ?? false;
            if (res) {
                return requestView.GetRequest();
            }

            return null;
        }

        public void CannotRename(string message) {
            MessageBox.Show(message, "Cannot rename", MessageBoxButton.OK);
        }

        public void OutputLog(string message) {
            IVsOutputWindowPane pane = GetPane();
            if (pane != null) {
                pane.Activate();

                pane.OutputString(message);
                pane.OutputString(Environment.NewLine);
            }
        }

        public void ClearRefactorPane() {
            IVsOutputWindowPane pane = GetPane();
            if (pane != null) {
                pane.Clear();
            }
        }

        private static IVsOutputWindowPane GetPane() {
            IVsOutputWindowPane pane;
            var outWin = (IVsOutputWindow)CommonPackage.GetGlobalService(typeof(IVsOutputWindow));

            char[] buffer = new char[1024];
            Guid tmp = RefactorGuid;

            if (!ErrorHandler.Succeeded(outWin.GetPane(ref tmp, out pane))) {
                ErrorHandler.ThrowOnFailure(outWin.CreatePane(ref tmp, "Refactor", 1, 1));

                if (!ErrorHandler.Succeeded(outWin.GetPane(ref tmp, out pane))) {
                    return null;
                }
            }
            return pane;
        }

        public ITextBuffer GetBufferForDocument(string filename) {
            return PythonToolsPackage.GetBufferForDocument(filename);
        }


        public IVsLinkedUndoTransactionManager BeginGlobalUndo() {
            var linkedUndo = (IVsLinkedUndoTransactionManager)PythonToolsPackage.GetGlobalService(typeof(SVsLinkedUndoTransactionManager));
            ErrorHandler.ThrowOnFailure(linkedUndo.OpenLinkedUndo(
                (uint)LinkedTransactionFlags2.mdtGlobal,
                "Rename Variable"
            ));
            return linkedUndo;
        }


        public void EndGlobalUndo(IVsLinkedUndoTransactionManager linkedUndo) {
            ErrorHandler.ThrowOnFailure(linkedUndo.CloseLinkedUndo());
        }
    }
}

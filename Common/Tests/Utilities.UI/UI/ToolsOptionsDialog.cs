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
using System.Windows.Automation;

namespace TestUtilities.UI {
    public class ToolsOptionsDialog : AutomationWrapper, IDisposable {
        private ToolsOptionsDialog(IntPtr handle)
            : base(AutomationElement.FromHandle(handle)) {
        }

        public static ToolsOptionsDialog Open(VisualStudioApp app) {
            return new ToolsOptionsDialog(app.OpenDialogWithDteExecuteCommand("Tools.Options"));
        }

        public void Dispose() {
            try {
                Element.GetWindowPattern().Close();
            } catch (InvalidOperationException) {
            } catch (ElementNotAvailableException) {
            }
        }

        public void OK(TimeSpan timeout) {
            WaitForInputIdle();
            var button = FindByAutomationId("1").AsWrapper();   // "OK"
            button.SetFocus();
            WaitForClosed(timeout, button.Invoke);
        }

        public void Cancel(TimeSpan timeout) {
            WaitForInputIdle();
            var button = FindByAutomationId("2").AsWrapper();   // "Cancel"
            button.SetFocus();
            WaitForClosed(timeout, button.Invoke);
        }

        public string SelectedView {
            get {
                // TODO: Implement getting the selected view (when we need it)
                throw new NotSupportedException("Cannot get the selected view (yet)");
            }
            set {
                var treeView = new TreeView(Element.FindFirst(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ClassNameProperty, "SysTreeView32")
                ));

                treeView.FindItem(value.Split('\\', '/')).SetFocus();
            }
        }
    }
}

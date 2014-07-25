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
using System.Windows.Input;

namespace TestUtilities.UI {
    public class SelectFolderDialog : AutomationDialog {
        public SelectFolderDialog(VisualStudioApp app, AutomationElement element)
            : base(app, element) {
        }

        public static SelectFolderDialog AddExistingFolder(VisualStudioApp app) {
            return new SelectFolderDialog(
                app,
                AutomationElement.FromHandle(app.OpenDialogWithDteExecuteCommand("Project.AddExistingFolder"))
            );
        }

        public static SelectFolderDialog AddFolderToSearchPath(VisualStudioApp app) {
            return new SelectFolderDialog(
                app,
                AutomationElement.FromHandle(app.OpenDialogWithDteExecuteCommand("Project.AddSearchPathFolder"))
            );
        }

        public void SelectFolder() {
            ClickButtonByName("Select Folder");
        }

        public string FolderName { 
            get {
                return GetFilenameEditBox().GetValuePattern().Current.Value;
            }
            set {
                GetFilenameEditBox().GetValuePattern().SetValue(value);
            }
        }

        public string Address {
            get {
                return GetAddressBox().Current.Name.Substring("Address: ".Length);
            }
        }

        private AutomationElement GetFilenameEditBox() {
            return Element.FindFirst(TreeScope.Descendants,
                new AndCondition(
                    new PropertyCondition(AutomationElement.ClassNameProperty, "Edit"),
                    new PropertyCondition(AutomationElement.NameProperty, "Folder:")
                )
            );
        }

        private AutomationElement GetAddressBox() {
            return Element.FindFirst(TreeScope.Descendants,
                new AndCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Pane),
                    new PropertyCondition(AutomationElement.ClassNameProperty, "Breadcrumb Parent")
                )
            ).FindFirst(TreeScope.Children, Condition.TrueCondition);
        }
    }
}

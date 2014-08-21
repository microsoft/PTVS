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

namespace TestUtilities.UI.Python.Django {
    class NewAppDialog : AutomationDialog {
        public NewAppDialog(VisualStudioApp app, AutomationElement element)
            : base(app, element) {
        }

        public static NewAppDialog FromDte(VisualStudioApp app) {
            return new NewAppDialog(
                app,
                AutomationElement.FromHandle(
                    app.OpenDialogWithDteExecuteCommand("ProjectandSolutionContextMenus.Project.Add.Djangoapp")
                )
            );
        }

        public override void OK() {
            ClickButtonAndClose("_ok", nameIsAutomationId: true);
        }

        public override void Cancel() {
            ClickButtonAndClose("_cancel", nameIsAutomationId: true);
        }
        
        public string AppName {
            get {
                return GetAppNameEditBox().GetValuePattern().Current.Value;
            }
            set {
                GetAppNameEditBox().GetValuePattern().SetValue(value);
            }
        }

        private AutomationElement GetAppNameEditBox() {
            return Element.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, "_newAppName")
            );
        }
    }
}

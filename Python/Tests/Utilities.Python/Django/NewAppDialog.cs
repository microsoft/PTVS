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

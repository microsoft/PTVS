// Visual Studio Shared Project
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

namespace TestUtilities.UI
{
    class CredentialsDialog : AutomationDialog
    {
        public CredentialsDialog(VisualStudioApp app, AutomationElement element)
            : base(app, element)
        {
        }

        public static CredentialsDialog PublishSelection(VisualStudioApp app)
        {
            return new CredentialsDialog(
                app,
                AutomationElement.FromHandle(app.OpenDialogWithDteExecuteCommand("Build.PublishSelection"))
            );
        }

        public string UserName
        {
            get
            {
                return GetUsernameEditBox().GetValuePattern().Current.Value;
            }
            set
            {
                GetUsernameEditBox().GetValuePattern().SetValue(value);
            }
        }

        public string Password
        {
            get
            {
                return GetPasswordEditBox().GetValuePattern().Current.Value;
            }
            set
            {
                GetPasswordEditBox().GetValuePattern().SetValue(value);
            }
        }

        private AutomationElement GetUsernameEditBox()
        {
            return Element.FindFirst(TreeScope.Descendants,
                new AndCondition(
                    new PropertyCondition(AutomationElement.NameProperty, "User name:"),
                    new PropertyCondition(AutomationElement.ClassNameProperty, "Edit")
                )
            );
        }

        private AutomationElement GetPasswordEditBox()
        {
            return Element.FindFirst(TreeScope.Descendants,
                new AndCondition(
                    new PropertyCondition(AutomationElement.NameProperty, "Password:"),
                    new PropertyCondition(AutomationElement.ClassNameProperty, "Edit")
                )
            );
        }
    }
}

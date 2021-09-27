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

namespace TestUtilities.UI
{
    public class SelectFolderDialog : AutomationDialog
    {
        public SelectFolderDialog(VisualStudioApp app, AutomationElement element)
            : base(app, element)
        {
        }

        public static SelectFolderDialog AddExistingFolder(VisualStudioApp app)
        {
            return new SelectFolderDialog(
                app,
                AutomationElement.FromHandle(app.OpenDialogWithDteExecuteCommand("Project.AddExistingFolder"))
            );
        }

        public static SelectFolderDialog AddFolderToSearchPath(VisualStudioApp app)
        {
            return new SelectFolderDialog(
                app,
                AutomationElement.FromHandle(app.OpenDialogWithDteExecuteCommand("Project.AddSearchPathFolder"))
            );
        }

        public void SelectFolder()
        {
            ClickButtonByName("Select Folder");
        }

        public string FolderName
        {
            get
            {
                return GetFilenameEditBox().GetValuePattern().Current.Value;
            }
            set
            {
                GetFilenameEditBox().GetValuePattern().SetValue(value);
            }
        }

        public string Address
        {
            get
            {
                foreach (AutomationElement e in Element.FindAll(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ClassNameProperty, "ToolbarWindow32"))
                )
                {
                    var name = e.Current.Name;
                    if (name.StartsWith("Address: ", StringComparison.CurrentCulture))
                    {
                        return name.Substring("Address: ".Length);
                    }
                }

                Assert.Fail("Unable to find address");
                return null;
            }
        }

        private AutomationElement GetFilenameEditBox()
        {
            return Element.FindFirst(TreeScope.Descendants,
                new AndCondition(
                    new PropertyCondition(AutomationElement.ClassNameProperty, "Edit"),
                    new PropertyCondition(AutomationElement.NameProperty, "Folder:")
                )
            );
        }
    }
}

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
    /// <summary>
    /// Wrapps VS's Add Reference Dialog
    /// </summary>
    class AddReferenceDialog : AutomationWrapper
    {
        public AddReferenceDialog(AutomationElement element)
            : base(element)
        {
        }

        /// <summary>
        /// Clicks the OK button on the dialog.
        /// </summary>
        public void ClickOK()
        {
            ClickButtonByName("OK");
        }

        public void ActivateBrowseTab()
        {
            for (int i = 0; i < 20; i++)
            {
                var tabItem = Element.FindFirst(
                    TreeScope.Descendants,
                    new AndCondition(
                        new PropertyCondition(
                            AutomationElement.ControlTypeProperty,
                            ControlType.TabItem
                        ),
                        new PropertyCondition(
                            AutomationElement.NameProperty,
                            "Browse"
                        )
                    )
                );
                if (tabItem == null)
                {
                    System.Threading.Thread.Sleep(500);
                    continue;
                }

                var pattern = (SelectionItemPattern)tabItem.GetCurrentPattern(SelectionItemPattern.Pattern);
                pattern.Select();
            }
        }

        public string BrowseFilename
        {
            get
            {
                return GetFilenameValuePattern().Current.Value;
            }
            set
            {
                GetFilenameValuePattern().SetValue(value);
            }
        }

        private ValuePattern GetFilenameValuePattern()
        {
            var filename = Element.FindFirst(
                TreeScope.Descendants,
                new AndCondition(
                    new PropertyCondition(
                        AutomationElement.ControlTypeProperty,
                        ControlType.Edit
                    ),
                    new PropertyCondition(
                        AutomationElement.NameProperty,
                        "File name:"
                    )
                )
            );

            return (ValuePattern)filename.GetCurrentPattern(ValuePattern.Pattern);
        }
    }
}

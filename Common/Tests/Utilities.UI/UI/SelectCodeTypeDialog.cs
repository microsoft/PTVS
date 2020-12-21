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

using System;
using System.Windows.Automation;

namespace TestUtilities.UI
{
    public class SelectCodeTypeDialog : AutomationWrapper
    {
        private CheckListView _availableCodeTypes;

        public SelectCodeTypeDialog(AutomationElement element) : base(element)
        {
            var actElement = Element.FindFirst(
                           TreeScope.Descendants,
                           new PropertyCondition(
                               AutomationElement.AutomationIdProperty,
                               "1005")); // AutomationId 1005 discovered with UISpy
            _availableCodeTypes = new CheckListView(actElement);
        }

        public SelectCodeTypeDialog(IntPtr hwnd) : this(AutomationElement.FromHandle(hwnd)) { }

        public CheckListView AvailableCodeTypes
        {
            get
            {
                return _availableCodeTypes;
            }
        }

        public void SetDebugSpecificCodeTypes()
        {
            Select(FindByAutomationId("1199")); // utomationId 1199 discovered with UISpy
        }

        public void SetAutomaticallyDetermineCodeTypes()
        {
            Select(FindByAutomationId("1196")); // AutomationId 1196
        }

        public CheckBox GetCodeTypeCheckBox(string codeType)
        {
            var selectedItem = _availableCodeTypes.GetFirstByName(codeType);
            return selectedItem;
        }

        public void ClickOk()
        {
            ClickButtonByName("OK");
        }

        public void ClickCancel()
        {
            ClickButtonByName("Cancel");
        }

    }
}

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
    class Menu : AutomationWrapper
    {
        public Menu(AutomationElement element)
            : base(element)
        {
        }

        public List<MenuItem> Items
        {
            get
            {
                Condition con = new PropertyCondition(
                                    AutomationElement.LocalizedControlTypeProperty,
                                    "menu item"
                                );
                AutomationElementCollection ell = Element.FindAll(TreeScope.Children, con);
                List<MenuItem> items = new List<MenuItem>();
                for (int i = 0; i < ell.Count; i++)
                {
                    items.Add(new MenuItem(ell[i]));
                }
                return items;
            }
        }
    }
}

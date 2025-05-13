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
using System.Windows.Input;

namespace TestUtilities.UI {
    public class ComboBox : AutomationWrapper {
        public ComboBox(AutomationElement element)
            : base(element) {
        }


        //public void SelectItem(string itemName)
        //{
        //    //var comboBox = FindComboBox(comboBoxAutomationId);

        //    var expandCollapse = Element.GetCurrentPattern(ExpandCollapsePattern.Pattern) as ExpandCollapsePattern;
        //    expandCollapse.Expand();


        //    var listItems = Element.FindAll(TreeScope.Descendants,
        //                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));

        //    Console.WriteLine($"ComboBox contains {listItems.Count} items:");
        //    foreach (AutomationElement i in listItems)
        //    {
        //        Console.WriteLine($"- '{i.Current.Name}'");
        //    }


        //    var item = Element.FindFirst(TreeScope.Descendants,
        //        new AndCondition(
        //            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem),
        //            new PropertyCondition(AutomationElement.NameProperty, itemName)));

        //    if (item == null)
        //        throw new Exception($"Item '{itemName}' not found in ComboBox.");

        //    var selectionItem = item.GetCurrentPattern(SelectionItemPattern.Pattern) as SelectionItemPattern;
        //    selectionItem.Select();

        //    expandCollapse.Collapse();
        //}

        //public string GetSelectedItemName()
        //{
        //    //var comboBox = FindComboBox(comboBoxAutomationId);

        //    var selectionPattern = Element.GetCurrentPattern(SelectionPattern.Pattern) as SelectionPattern;
        //    var selectedItems = selectionPattern.Current.GetSelection();

        //    return selectedItems.Length > 0 ? selectedItems[0].Current.Name : null;
        //}

        //private AutomationElement FindComboBox(string automationId)
        //{
        //    var comboBox = Element.FindFirst(TreeScope.Descendants,
        //        new AndCondition(
        //            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ComboBox),
        //            new PropertyCondition(AutomationElement.AutomationIdProperty, automationId)));

        //    if (comboBox == null)
        //        throw new Exception($"ComboBox with AutomationId '{automationId}' not found.");

        //    return comboBox;
        //}
        //public void ClickItem(string itemName)
        //{            
        //    ExpandComboBox();

        //    var item = FindComboBoxItem(itemName);

        //    if (item.TryGetCurrentPattern(InvokePattern.Pattern, out object pattern))
        //    {
        //        ((InvokePattern)pattern).Invoke();
        //    }
        //    else if (item.TryGetCurrentPattern(SelectionItemPattern.Pattern, out pattern))
        //    {
        //        ((SelectionItemPattern)pattern).Select();
        //    }
        //    else
        //    {
        //        throw new InvalidOperationException($"Item '{itemName}' does not support click or selection.");
        //    }

        //    CollapseComboBox();
        //}




        //private AutomationElement FindComboBoxItem(string itemName)
        //{
        //    var listItems = Element.FindAll(TreeScope.Descendants,
        //                 new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));

        //    Console.WriteLine($"ComboBox contains {listItems.Count} items:");
        //    foreach (AutomationElement item in listItems)
        //    {
        //        Console.WriteLine($"- '{item.Current.Name}'");
        //    }

        //    foreach (AutomationElement item in listItems)
        //    {
        //        if (string.Equals(item.Current.Name.Split('\n')[0], itemName, StringComparison.OrdinalIgnoreCase))
        //        {
        //            return item;
        //        }
        //    }

        //    throw new Exception($"Item '{itemName}' not found in ComboBox.");
        //    //var item = Element.FindFirst(TreeScope.Descendants,
        //    //    new AndCondition(
        //    //        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem),
        //    //        new PropertyCondition(AutomationElement.NameProperty, itemName)));

        //    //if (item == null)
        //    //    throw new Exception($"Item '{itemName}' not found in ComboBox.");

        //    //return item;
        //}

        //private void ExpandComboBox()
        //{
        //    var expandCollapse = Element.GetCurrentPattern(ExpandCollapsePattern.Pattern) as ExpandCollapsePattern;
        //    expandCollapse.Expand();
        //}

        //private void CollapseComboBox()
        //{
        //    var expandCollapse = Element.GetCurrentPattern(ExpandCollapsePattern.Pattern) as ExpandCollapsePattern;
        //    expandCollapse.Collapse();
        //}

        public void LogAvailableItems()
        {
            ExpandCollapsePattern pat = (ExpandCollapsePattern)Element.GetCurrentPattern(ExpandCollapsePattern.Pattern);
            pat.Expand();
            try
            {
                var items = Element.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));
                Console.WriteLine("Available items in the ComboBox:");
                foreach (AutomationElement item in items)
                {
                    Console.WriteLine($"- {item.Current.Name}");

                }
            }
            finally
            {
                pat.Collapse();
            }
        }
        public void SelectItem(string name)
        {
            ExpandCollapsePattern pat = (ExpandCollapsePattern)Element.GetCurrentPattern(ExpandCollapsePattern.Pattern);
            pat.Expand();
            try
            {
                //var itemFirst = Element.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name));
                AutomationElement itemFirst = null;
                var items = Element.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));
                Console.WriteLine("Available items in the ComboBox:");
                foreach (AutomationElement item in items)
                {
                    Console.WriteLine("controls");
                    Console.WriteLine($"{item.Current.Name}");
                    if (item.Current.Name.Contains(name))
                    {
                        Console.WriteLine($"Supported patterns for item '{name}':");
                        itemFirst = item;
                        break;

                    }

                }

                if (itemFirst == null)
                {
                    LogAvailableItems();
                    throw new ElementNotAvailableException(name + " is not in the combobox");
                }

                // Log supported patterns for the item
                Console.WriteLine($"Supported patterns for item '{name}':");
                foreach (var pattern in itemFirst.GetSupportedPatterns())
                {
                    Console.WriteLine($"- {Automation.PatternName(pattern)}");
                }

                try
                {
                    var selectionItemPattern = itemFirst.GetCurrentPattern(SelectionItemPattern.Pattern) as SelectionItemPattern;
                    if (selectionItemPattern != null)
                    {
                        selectionItemPattern.Select();
                    }
                }
                catch (Exception)
                {
                    LogAvailableItems();
                    throw;
                }

            }
            finally
            {
                pat.Collapse();
            }
        }

        /// <summary>
        /// Selects an item in the combo box by clicking on it.
        /// Only use this if SelectItem doesn't work!
        /// </summary
        /// <param name="name"></param>
        public void ClickItem(string name)
        {
            ExpandCollapsePattern pat = (ExpandCollapsePattern)Element.GetCurrentPattern(ExpandCollapsePattern.Pattern);
            pat.Expand();

            AutomationElement itemFirst = null;
            var items = Element.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));
            Console.WriteLine("Available items in the ComboBox:");
            foreach (AutomationElement item in items)
            {
                Console.WriteLine("controls");
                Console.WriteLine($"{item.Current.Name}");
                if (item.Current.Name.Contains(name))
                {
                    Console.WriteLine($"Supported patterns for item '{name}':");
                    itemFirst = item;
                    break;

                }

            }

            // On Win8, we need to move mouse onto the text, otherwise we cannot select the item 
            //AutomationElement innerText = item.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));


            var boundingRect = itemFirst.Current.BoundingRectangle;
            if (!boundingRect.IsEmpty)
            {
                var fallbackPoint = new System.Windows.Point(
                    boundingRect.X + boundingRect.Width / 2,
                    boundingRect.Y + boundingRect.Height / 2
                );
                Mouse.MoveTo(fallbackPoint);
                Mouse.Click(MouseButton.Left);
            }


            Keyboard.Press(Key.Enter); // Simulate pressing Enter

        }

        public string GetSelectedItemName()
        {
            var selection = Element.GetSelectionPattern().Current.GetSelection();
            if (selection == null || selection.Length == 0)
            {
                return "";
            }
            return selection[0].Current.Name;
        }

        public string GetEnteredText() {
            return GetValue();
        }
    }
}

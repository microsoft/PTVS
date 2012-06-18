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

using System.Windows.Automation;

namespace AnalysisTest.UI {
    class ComboBox : AutomationWrapper {
        public ComboBox(AutomationElement element)
            : base(element) {            
        }

        public void SelectItem(string name) {
            ExpandCollapsePattern pat = (ExpandCollapsePattern)Element.GetCurrentPattern(ExpandCollapsePattern.Pattern);
            pat.Expand();

            var item = Element.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name)); 
 
            ((SelectionItemPattern)item.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();

            pat.Collapse();
        }

        public string GetSelectedItemName() {
            ExpandCollapsePattern pat = (ExpandCollapsePattern)Element.GetCurrentPattern(ExpandCollapsePattern.Pattern);
            pat.Expand();
            try {
                var items = Element.FindAll(TreeScope.Descendants, 
                    new OrCondition(
                        new PropertyCondition(AutomationElement.ClassNameProperty, "ListBoxItem"),
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)
                    )
                );

                foreach (AutomationElement item in items) {
                    if (((SelectionItemPattern)item.GetCurrentPattern(SelectionItemPattern.Pattern)).Current.IsSelected) {
                        return item.Current.Name;
                    }
                }
                return null;
            } finally {
                pat.Collapse();
            }
        }

        public string GetEnteredText() {
            return GetValue();
        }
    }
}

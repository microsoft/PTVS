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
using System.Threading;
using System.Windows.Automation;

namespace TestUtilities.UI {
    public class NavigateToDialog : AutomationWrapper {
        public NavigateToDialog(IntPtr hwnd)
            : base(AutomationElement.FromHandle(hwnd)) {
        }

        public void GoToSelection() {
            ClickButtonByAutomationId("okButton");
        }

        public void Close() {
            ClickButtonByAutomationId("cancelButton");
        }

        public string SearchTerm {
            get {
                var term = (ValuePattern)GetSearchBox().GetCurrentPattern(ValuePattern.Pattern);
                return term.Current.Value;
            }
            set {
                var term = (ValuePattern)GetSearchBox().GetCurrentPattern(ValuePattern.Pattern);
                term.SetValue(string.Empty);
                GetSearchBox().SetFocus();
                Keyboard.Type(value);
            }
        }

        private AutomationElement GetSearchBox() {
            return Element.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, "searchTerms")
            ).FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ClassNameProperty, "Edit")
            );
        }

        public GridPattern GetResultsList() {
            return (GridPattern)Element.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, "results")
            ).GetCurrentPattern(GridPattern.Pattern);
        }

        internal int WaitForNumberOfResults(int results) {
            var list = GetResultsList();

            for (int count = 10; count > 0 && list.Current.RowCount < results; --count) {
                Thread.Sleep(1000);
            }
            return list.Current.RowCount;
        }
    }
}

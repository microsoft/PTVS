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

namespace TestUtilities.UI {
    public class OverwriteFileDialog : AutomationWrapper {
        public OverwriteFileDialog(IntPtr hwnd)
            : base(AutomationElement.FromHandle(hwnd)) {
        }

        public void Yes() {
            Invoke(FindButton("_yes"));
        }

        public void No() {
            Invoke(FindButton("_no"));
        }

        public void Cancel() {
            Invoke(FindButton("_cancel"));
        }


        public bool AllItems {
            get {
                return FindByAutomationId("_allItems").GetTogglePattern().Current.ToggleState == ToggleState.On;
            }
            set {
                if (AllItems) {
                    if (!value) {
                        FindByAutomationId("_allItems").GetTogglePattern().Toggle();
                    }
                } else {
                    if (value) {
                        FindByAutomationId("_allItems").GetTogglePattern().Toggle();
                    }
                }
            }
        }


        public string Text {
            get {
                var message = (ValuePattern)GetMessageTextBlock().GetCurrentPattern(ValuePattern.Pattern);
                return message.Current.Value;
            }
        }

        private AutomationElement GetMessageTextBlock() {
            return Element.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, "_message")
            );
        }
    }
}

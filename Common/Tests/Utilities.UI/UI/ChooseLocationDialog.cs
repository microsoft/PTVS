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
    class ChooseLocationDialog : AutomationWrapper {
        public ChooseLocationDialog(IntPtr hwnd)
            : base(AutomationElement.FromHandle(hwnd)) {
        }

        public void ClickOK() {
            ClickButtonByName("OK");
        }

        public AutomationElement FindProject(string name) {
            var list = Element.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "ListBox"));
            return list.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.NameProperty, name));
        }

    }
}

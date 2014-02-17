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
using System;
using System.Threading;

namespace TestUtilities.UI
{
    class ProjectPropertiesWindow : AutomationWrapper
    {
        public ProjectPropertiesWindow(IntPtr element)
            : base(AutomationElement.FromHandle(element)) { 
        }

        public AutomationElement this[string tabName] {
            get {
                var tabItem = FindFirstByControlType(tabName, ControlType.Pane);
                if (tabItem == null) {
                    AutomationWrapper.DumpElement(Element);
                    return null;
                } else {
                    AutomationWrapper.DumpElement(tabItem);
                }

                Mouse.MoveTo(tabItem.GetClickablePoint());
                Thread.Sleep(100);
                Mouse.Click(System.Windows.Input.MouseButton.Left);
                Thread.Sleep(100);

                return FindByAutomationId("PageHostingPanel");
            }
        }
    }
}

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

namespace TestUtilities.UI
{
    class ProjectPropertiesWindow : AutomationWrapper
    {
        public ProjectPropertiesWindow(AutomationElement element)
            : base(element) { 
        }

        public AutomationElement this[string tabName] {
            get {
                var tabItem = FindFirstByControlType(tabName, ControlType.Pane);
                var point = tabItem.GetClickablePoint();
                Mouse.MoveTo(point);
                System.Threading.Thread.Sleep(100);
                Mouse.Click(System.Windows.Input.MouseButton.Left);
                System.Threading.Thread.Sleep(100);                
                return FindFirstByControlType(tabName, ControlType.Pane);
            }
        }
    }
}

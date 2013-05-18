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

namespace TestUtilities.UI
{
    public class TreeNode : AutomationWrapper
    {
        public TreeNode(AutomationElement element)
            : base(element) {
        }
        
        public void Select()
        {
            Mouse.MoveTo(new System.Drawing.Point(0, 0));
            System.Threading.Thread.Sleep(100);
            Mouse.MoveTo(this.Element.GetClickablePoint());
            System.Threading.Thread.Sleep(100);
            Mouse.Click(System.Windows.Input.MouseButton.Left);
            System.Threading.Thread.Sleep(100);
        }

        public string Value
        {
            get
            {
                return this.Element.Current.Name.ToString();
            }
        }

        public void ExpandCollapse()
        {            
            var pattern = (InvokePattern)Element.GetCurrentPattern(InvokePattern.Pattern);
            pattern.Invoke();
        }

        public void DoubleClick()
        {
            Mouse.MoveTo(new System.Drawing.Point(0, 0));
            System.Threading.Thread.Sleep(100);
            Mouse.MoveTo(this.Element.GetClickablePoint());
            System.Threading.Thread.Sleep(100);
            Mouse.DoubleClick(System.Windows.Input.MouseButton.Left);
            System.Threading.Thread.Sleep(100);
        }

        public void ShowContextMenu()
        {
            Select();
            System.Threading.Thread.Sleep(100);
            Mouse.Click(System.Windows.Input.MouseButton.Right);
            System.Threading.Thread.Sleep(100);
            //System.Windows.Automation.AutomationElement.RootElement
        }
    }
}

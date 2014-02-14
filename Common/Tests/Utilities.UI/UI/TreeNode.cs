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
using System.Collections.Generic;
using System.Linq;
using System.Windows.Automation;

namespace TestUtilities.UI
{
    public class TreeNode : AutomationWrapper
    {
        public TreeNode(AutomationElement element)
            : base(element) {
        }

        public new void Select()
        {
            try
            {
                Element.GetSelectionItemPattern().AddToSelection();
            }
            catch (InvalidOperationException)
            {
                // Control does not support this pattern, so let's just click
                // on it.
                var point = Element.GetClickablePoint();
                point.Offset(0.0, 50.0);
                Mouse.MoveTo(point);
                System.Threading.Thread.Sleep(100);
                point.Offset(0.0, -50.0);
                Mouse.MoveTo(point);
                System.Threading.Thread.Sleep(100);
                Mouse.Click(System.Windows.Input.MouseButton.Left);
                System.Threading.Thread.Sleep(100);
            }
        }

        public void Deselect()
        {
            Element.GetSelectionItemPattern().RemoveFromSelection();
        }

        public string Value
        {
            get
            {
                return this.Element.Current.Name.ToString();
            }
        }

        public bool IsExpanded
        {
            get
            {
                switch (Element.GetExpandCollapsePattern().Current.ExpandCollapseState)
                {
                    case ExpandCollapseState.Collapsed:
                        return false;
                    case ExpandCollapseState.Expanded:
                        return true;
                    case ExpandCollapseState.LeafNode:
                        return true;
                    case ExpandCollapseState.PartiallyExpanded:
                        return false;
                    default:
                        return false;
                }
            }
            set
            {
                if (value)
                {
                    Element.GetExpandCollapsePattern().Expand();
                }
                else
                {
                    Element.GetExpandCollapsePattern().Collapse();
                }
            }
        }

        public List<TreeNode> Nodes
        {
            get
            {
                return Element.FindAll(
                    TreeScope.Children,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem)
                )
                    .OfType<AutomationElement>()
                    .Select(e => new TreeNode(e))
                    .ToList();
            }
        }

        public void ExpandCollapse()
        {
            try {
                var pattern = Element.GetExpandCollapsePattern();
                switch (pattern.Current.ExpandCollapseState)
                {
                    case ExpandCollapseState.Collapsed:
                        pattern.Expand();
                        break;
                    case ExpandCollapseState.Expanded:
                        pattern.Collapse();
                        break;
                    case ExpandCollapseState.LeafNode:
                        break;
                    case ExpandCollapseState.PartiallyExpanded:
                        pattern.Expand();
                        break;
                    default:
                        break;
                }
            } catch (InvalidOperationException) {
                Element.GetInvokePattern().Invoke();
            }
        }

        public void DoubleClick()
        {
            Element.GetInvokePattern().Invoke();
        }

        public void ShowContextMenu()
        {
            Select();
            System.Threading.Thread.Sleep(100);
            Mouse.Click(System.Windows.Input.MouseButton.Right);
            System.Threading.Thread.Sleep(100);
        }
    }
}

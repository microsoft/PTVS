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
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;

namespace TestUtilities.UI
{
    public class TreeNode : AutomationWrapper, ITreeNode
    {
        public TreeNode(AutomationElement element)
            : base(element)
        {
        }

        public new void Select()
        {
            try
            {
                var parent = Element.GetSelectionItemPattern().Current.SelectionContainer;
                foreach (var item in parent.GetSelectionPattern().Current.GetSelection())
                {
                    item.GetSelectionItemPattern().RemoveFromSelection();
                }
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

        void ITreeNode.Select()
        {
            base.Select();
        }

        void ITreeNode.AddToSelection()
        {
            AutomationWrapper.AddToSelection(Element);
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
            try
            {
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
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine("Can't expand/collapse");
                foreach (var pattern in Element.GetSupportedPatterns())
                {
                    Console.WriteLine("{0} {1}", pattern.Id, pattern.ProgrammaticName);
                }
                try
                {
                    Element.GetInvokePattern().Invoke();
                }
                catch (InvalidOperationException)
                {
                    Console.WriteLine("Can't even invoke, let's try double clicking...");
                    // What an annoying control...
                    var point = Element.GetClickablePoint();
                    point.Offset(0.0, 50.0);
                    Mouse.MoveTo(point);
                    System.Threading.Thread.Sleep(100);
                    point.Offset(0.0, -50.0);
                    Mouse.MoveTo(point);
                    System.Threading.Thread.Sleep(100);
                    Mouse.DoubleClick(System.Windows.Input.MouseButton.Left);
                    System.Threading.Thread.Sleep(100);
                }
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

        /// <summary>
        /// Selects the provided items with the mouse preparing for a drag and drop
        /// </summary>
        /// <param name="source"></param>
        private static void SelectItemsForDragAndDrop(ITreeNode[] source)
        {
            AutomationWrapper.Select(((TreeNode)source.First()).Element);
            for (int i = 1; i < source.Length; i++)
            {
                AutomationWrapper.AddToSelection(((TreeNode)source[i]).Element);
            }

            Mouse.MoveTo(((TreeNode)source.Last()).Element.GetClickablePoint());
            Mouse.Down(MouseButton.Left);
        }


        public void DragOntoThis(params ITreeNode[] source)
        {
            DragOntoThis(Key.None, source);
        }

        public void DragOntoThis(Key modifier, params ITreeNode[] source)
        {
            SelectItemsForDragAndDrop(source);

            try
            {
                try
                {
                    if (modifier != Key.None)
                    {
                        Keyboard.Press(modifier);
                    }
                    var dest = Element;
                    if (source.Length == 1 && source[0] == this)
                    {
                        // dragging onto ourself, the mouse needs to move
                        var point = dest.GetClickablePoint();
                        Mouse.MoveTo(new Point(point.X + 1, point.Y + 1));
                    }
                    else
                    {
                        Mouse.MoveTo(dest.GetClickablePoint());
                    }
                }
                finally
                {
                    Mouse.Up(MouseButton.Left);
                }
            }
            finally
            {
                if (modifier != Key.None)
                {
                    Keyboard.Release(modifier);
                }
            }
        }

    }
}

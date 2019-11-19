// Python Tools for Visual Studio
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
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Input;

namespace TestUtilities.UI {
    public class PythonTestExplorerGridView : ListView {
        public PythonTestExplorerGridView(AutomationElement element) : base(element) { }

        /// <summary>
        /// Waits for the item in the tree to be available for up to a specified timeout.
        /// </summary>
        protected AutomationElement WaitForItemHelper(Func<string[], AutomationElement> getItem, string[] path, TimeSpan timeout) {
            AutomationElement item = null;
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            while (stopWatch.Elapsed < timeout) {
                item = getItem(path);
                if (item != null) {
                    break;
                }
                Thread.Sleep(250);
            }

            if (item == null) {
                Console.WriteLine("Failed to find {0} within {1} ms", String.Join("\\", path), timeout.TotalMilliseconds);
                DumpElement(Element);
            }
            return item;
        }

        /// <summary>
        /// Waits for the item in the tree to be available for up to 10 seconds.
        /// </summary>
        public AutomationElement WaitForItem(params string[] path) {
            return WaitForItemHelper(FindItem, path);
        }

        protected AutomationElement WaitForItemHelper(Func<string[], AutomationElement> getItem, string[] path) {
            return WaitForItemHelper(getItem, path, TimeSpan.FromSeconds(10));
        }

        public void CollapseAll() {
            foreach (AutomationElement child in Element.FindAll(TreeScope.Children, Condition.TrueCondition)) {
                if (child.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var pattern)) {
                    ((ExpandCollapsePattern)pattern).Collapse();
                }
            }
        }

        public void ExpandAll() {
            try {
                foreach (AutomationElement child in Element.FindAll(TreeScope.Descendants, Condition.TrueCondition)) {
                    if (child.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var pattern)) {
                        ((ExpandCollapsePattern)pattern).Expand();
                    }
                }
            } catch (InvalidOperationException) {
            }
        }

        /// <summary>
        /// Finds the specified item in the tree and returns it.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public AutomationElement FindItem(params string[] path) {
            try {
                return FindNode(Element.FindAll(TreeScope.Children, Condition.TrueCondition), path, 0);
            } catch (InvalidOperationException) {
            }

            return null;
        }

        protected static AutomationElement FindNode(AutomationElementCollection nodes, string[] splitPath, int depth) {
            for (int i = 0; i < nodes.Count; i++) {
                var node = nodes[i];
                var name = node.GetCurrentPropertyValue(AutomationElement.NameProperty) as string;

                //NameProperty is now getting appended with strings like "Not Run". we can no longer use Equals
                if (name.Contains(splitPath[depth])) {
                    if (depth == splitPath.Length - 1) {
                        return node;
                    }

                    // ensure the node is expanded...
                    try {
                        EnsureExpanded(node);
                    } catch (InvalidOperationException) {
                        // handle race w/ items being removed...
                        Console.WriteLine("Failed to expand {0}", splitPath[depth]);
                    }
                    return FindNode(node.FindAll(TreeScope.Children, Condition.TrueCondition), splitPath, depth + 1);
               }
            }
            return null;
        }
    }
}

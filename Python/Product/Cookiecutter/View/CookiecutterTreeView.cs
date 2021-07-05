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

using Microsoft.CookiecutterTools.ViewModel;

namespace Microsoft.CookiecutterTools.View {
    /// <summary>
    /// TreeView that provides a clean hierarchy of automation peers, with a
    /// 1 to 1 mapping for each tree item - automation peer.
    /// Additional controls, such as the expander, are discarded.
    /// This results in a clear parent-children relationship, and fixes an issue
    /// with narrator where it would report an inaccurate item count.
    /// </summary>
    class CookiecutterTreeView : TreeView {
        public event EventHandler<InvokeEventArgs> InvokeItem;

        protected override DependencyObject GetContainerForItemOverride() {
            return new CookiecutterTreeViewItem(this);
        }

        protected override AutomationPeer OnCreateAutomationPeer() {
            return new CookiecutterTreeViewAutomationPeer(this);
        }

        public void DoInvoke(TreeItemViewModel item) {
            InvokeItem?.Invoke(this, new InvokeEventArgs(item));
        }

        class CookiecutterTreeViewItem : TreeViewItem {
            private CookiecutterTreeView _treeView;

            public CookiecutterTreeViewItem(CookiecutterTreeView treeView) {
                _treeView = treeView;
            }

            protected override DependencyObject GetContainerForItemOverride() {
                return new CookiecutterTreeViewItem(_treeView);
            }

            protected override AutomationPeer OnCreateAutomationPeer() {
                return new CookiecutterItemAutomationPeer(_treeView, this);
            }
        }

        class CookiecutterTreeViewAutomationPeer : TreeViewAutomationPeer {
            public CookiecutterTreeViewAutomationPeer(CookiecutterTreeView owner)
                : base(owner) {
            }

            protected override ItemAutomationPeer CreateItemAutomationPeer(object item) {
                return new CookiecutterDataItemAutomationPeer((CookiecutterTreeView)Owner, item, this, null);
            }
        }

        class CookiecutterItemAutomationPeer : TreeViewItemAutomationPeer {
            private CookiecutterTreeView _treeView;

            public CookiecutterItemAutomationPeer(CookiecutterTreeView treeView, TreeViewItem owner)
                : base(owner) {
                _treeView = treeView;
            }

            protected override List<AutomationPeer> GetChildrenCore() {
                var originalChildren = base.GetChildrenCore();
                if (originalChildren == null) {
                    return null;
                }
                return originalChildren.Where(peer => peer is CookiecutterDataItemAutomationPeer).ToList();
            }

            protected override ItemAutomationPeer CreateItemAutomationPeer(object item) {
                if (item is ContinuationViewModel || item is TemplateViewModel) {
                    return new CookiecutterInvokableDataItemAutomationPeer(_treeView, item, this, null);
                } else {
                    return new CookiecutterDataItemAutomationPeer(_treeView, item, this, null);
                }
            }
        }

        class CookiecutterDataItemAutomationPeer : TreeViewDataItemAutomationPeer {
            protected CookiecutterTreeView _treeView;

            public CookiecutterDataItemAutomationPeer(CookiecutterTreeView treeView, object item, ItemsControlAutomationPeer itemsControlAutomationPeer, TreeViewDataItemAutomationPeer parentDataItemAutomationPeer)
                : base(item, itemsControlAutomationPeer, parentDataItemAutomationPeer) {
                _treeView = treeView;
            }

            protected override List<AutomationPeer> GetChildrenCore() {
                var originalChildren = base.GetChildrenCore();
                if (originalChildren == null) {
                    return null;
                }
                return originalChildren.Where(peer => peer is CookiecutterDataItemAutomationPeer).ToList();
            }
        }

        /// <summary>
        /// Automation peer for a tree item which supports the invoke pattern.
        /// </summary>
        class CookiecutterInvokableDataItemAutomationPeer : CookiecutterDataItemAutomationPeer, IInvokeProvider {
            public CookiecutterInvokableDataItemAutomationPeer(CookiecutterTreeView treeView, object item, ItemsControlAutomationPeer itemsControlAutomationPeer, TreeViewDataItemAutomationPeer parentDataItemAutomationPeer)
                : base(treeView, item, itemsControlAutomationPeer, parentDataItemAutomationPeer) {
            }

            public override object GetPattern(PatternInterface patternInterface) {
                switch (patternInterface) {
                    case PatternInterface.Invoke:
                        return this;
                    default:
                        return base.GetPattern(patternInterface);
                }
            }

            public void Invoke() {
                _treeView.DoInvoke((TreeItemViewModel)Item);
            }
        }
    }

    class InvokeEventArgs : EventArgs {
        public InvokeEventArgs(TreeItemViewModel item) {
            Item = item;
        }

        public TreeItemViewModel Item { get; }
    }
}

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
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Options {
    class OptionsTreeView : TreeView {
        private static ImageList _imageList;
        internal const int TransparentIndex = 0;
        internal const int OpenFolderIndex = 1;
        internal const int ClosedFolderIndex = 2;

        public OptionsTreeView() {
            InitializeImageList();

            DrawMode = TreeViewDrawMode.OwnerDrawText;
            ShowLines = ShowRootLines = ShowPlusMinus = true;
            Scrollable = true;

            VsShellUtilities.ApplyTreeViewThemeStyles(this);
        }

        protected override void OnGotFocus(EventArgs e) {
        }

        private void InitializeImageList() {
            if (_imageList == null) {
                using (var imgHandler = new ImageHandler(typeof(OptionsTreeView).Assembly.GetManifestResourceStream("Microsoft.PythonTools.Project.Resources.imagelis.bmp"))) {
#pragma warning disable 0618
                    var openFolder = imgHandler.ImageList.Images[(int)ProjectNode.ImageName.OpenFolder];
                    var closedFolder = imgHandler.ImageList.Images[(int)ProjectNode.ImageName.Folder];
#pragma warning restore 0618

                    _imageList = new ImageList();

                    Bitmap bmp = new Bitmap(16, 16);

                    // transparent image is the image we use for owner drawn icons
                    _imageList.TransparentColor = Color.Magenta;
                    using (var g = Graphics.FromImage(bmp)) {
                        g.FillRectangle(
                            Brushes.Magenta,
                            new Rectangle(0, 0, 16, 16)
                        );
                    }
                    _imageList.Images.Add(bmp);
                    _imageList.Images.Add(openFolder);
                    _imageList.Images.Add(closedFolder);
                }
            }

            StateImageList = _imageList;
        }

        protected override void OnMouseUp(MouseEventArgs e) {
            var node = GetNodeAt(e.Location);
            if (node == null) {
                return;
            }

            OptionSettingNode settingsNode = node as OptionSettingNode;
            if (settingsNode != null) {
                if (settingsNode == SelectedNode) {
                    ToggleSetting(settingsNode);
                } else {
                    SelectedNode = settingsNode;
                }
            }
        }

        private void ToggleSetting(OptionSettingNode settingsNode) {
            settingsNode.ToggleSetting();
            InvalidateNodeIcon(settingsNode);
            OnAfterCheck(new TreeViewEventArgs(settingsNode));
        }

        private void InvalidateNodeIcon(TreeNode node) {
            this.Invalidate(
                ((OptionNode)node).IconBounds
            );
        }

        protected override void OnKeyPress(KeyPressEventArgs e) {
            if (e.KeyChar == ' ') {
                // toggling an option
                var node = SelectedNode as OptionSettingNode;
                if (node != null) {
                    ToggleSetting(node);
                    e.Handled = true;
                } else {
                    var folder = SelectedNode as OptionFolderNode;
                    if (folder != null) {
                        if (folder.IsExpanded) {
                            folder.Collapse();
                        } else {
                            folder.Expand();
                        }
                        e.Handled = true;
                    }
                }
            }
        }

        protected override void OnDrawNode(DrawTreeNodeEventArgs e) {
            OptionSettingNode setting = e.Node as OptionSettingNode;
            if (setting != null) {
                setting.DrawNode(e);
            }
            e.DrawDefault = true;
            base.OnDrawNode(e);
        }

        protected override void OnAfterExpand(TreeViewEventArgs e) {
            OptionFolderNode node = e.Node as OptionFolderNode;
            if (node != null) {
                node.SelectedImageIndex = node.ImageIndex = OpenFolderIndex;
                InvalidateNodeIcon(node);
                node.WasExpanded = true;
                return;
            }
            base.OnAfterExpand(e);
        }

        protected override void OnAfterCollapse(TreeViewEventArgs e) {
            OptionFolderNode node = e.Node as OptionFolderNode;
            if (node != null) {
                node.SelectedImageIndex = node.ImageIndex = ClosedFolderIndex;
                InvalidateNodeIcon(node);
                node.WasExpanded = false;
                return;
            }
            base.OnAfterCollapse(e);
        }

        protected override AccessibleObject CreateAccessibilityInstance() {
            return new TreeAccessibleObject(this);
        }

        // Taken from C#
        internal class TreeAccessibleObject : Control.ControlAccessibleObject {
            private readonly OptionsTreeView _tree;

            public TreeAccessibleObject(OptionsTreeView tree)
                : base(tree) {
                _tree = tree;
            }

            public override AccessibleObject GetChild(int index) {
                return ((OptionNode)FindTreeNode(index)).AccessibleObject;
            }

            public override int GetChildCount() {
                return _tree.GetNodeCount(true);
            }

            public override AccessibleObject HitTest(int x, int y) {
                return ((OptionNode)_tree.GetNodeAt(x, y)).AccessibleObject;
            }

            public override AccessibleObject GetFocused() {
                return ((OptionNode)_tree.SelectedNode).AccessibleObject;
            }

            public override AccessibleObject GetSelected() {
                return ((OptionNode)_tree.SelectedNode).AccessibleObject;
            }

            public override void Select(AccessibleSelection flags) {
                _tree.Select();
            }

            public override System.Drawing.Rectangle Bounds {
                get { return _tree.RectangleToScreen(_tree.Bounds); }
            }

            public override AccessibleObject Navigate(AccessibleNavigation navdir) {
                switch (navdir) {
                    case AccessibleNavigation.FirstChild:
                    case AccessibleNavigation.Down:
                        return GetChild(0);

                    case AccessibleNavigation.LastChild:
                        return GetChild(GetChildCount() - 1);

                    case AccessibleNavigation.Left:
                    case AccessibleNavigation.Previous:
                        return null;

                    case AccessibleNavigation.Next:
                    case AccessibleNavigation.Right:
                        return null;

                    case AccessibleNavigation.Up:
                        return Parent;

                    default:
                        System.Diagnostics.Debug.Assert(false, "What direction is this?");
                        return null;
                }
            }

            private TreeNode FindTreeNode(int index) {
                // Note this only handles 2 levels in the tree,
                // but that's because until we have more, we won't
                // know what index order the index will come in.
                foreach (TreeNode outerNode in _tree.Nodes) {
                    if (index == 0) {
                        return outerNode;
                    }
                    --index;
                    for (int i = outerNode.Nodes.Count - 1; i >= 0; --i) {
                        TreeNode innerNode = outerNode.Nodes[i];
                        System.Diagnostics.Debug.Assert(innerNode.Nodes.Count == 0, "We don't handle tree's nested this deep");
                        if (index == 0) {
                            return innerNode;
                        }
                        --index;
                    }
                }
                System.Diagnostics.Debug.Assert(false, "Didn't find a node for index: " + index.ToString());
                return null;
            }
        }
    }

    abstract class OptionNode : TreeNode {
        public OptionNode(string text)
            : base(text) {
        }

        public virtual AccessibleObject AccessibleObject {
            get {
                return new TreeNodeAccessibleObject(this);
            }
        }

        public Rectangle IconBounds {
            get {
                return new Rectangle(
                    Bounds.Left - 21,
                    Bounds.Top,
                    20,
                    Bounds.Height
                );
            }
        }

        // Taken from C#
        internal class TreeNodeAccessibleObject : Control.ControlAccessibleObject {
            private readonly OptionNode _node;

            public TreeNodeAccessibleObject(OptionNode node)
                : base(node.TreeView) {
                _node = node;
            }

            public override string Value {
                get { return null; }
            }

            public override string DefaultAction {
                get {
                    if (_node.IsExpanded) {
                        return "Collapse";
                    } else {
                        return "Expand";
                    }
                }
            }

            public override void DoDefaultAction() {
                _node.Toggle();
            }

            public override System.Drawing.Rectangle Bounds {
                get { return _node.TreeView.RectangleToScreen(_node.Bounds); }
            }

            public override string Name {
                get { return _node.Text; }
                set { _node.Text = value; }
            }

            public override int GetChildCount() {
                return 0;
            }

            public override AccessibleObject GetChild(int index) {
                System.Diagnostics.Debug.Assert(false, "Nodes have no children, only the treeview does");
                return null;
            }

            public override AccessibleObject Parent {
                get { return _node.TreeView.AccessibilityObject; }
            }

            public override AccessibleObject Navigate(AccessibleNavigation navdir) {
                switch (navdir) {
                    case AccessibleNavigation.Down:
                    case AccessibleNavigation.FirstChild:
                    case AccessibleNavigation.LastChild:
                        // TreeNodes don't have children.
                        return null;

                    case AccessibleNavigation.Left:
                    case AccessibleNavigation.Previous:
                        if (Index == 0) {
                            return null;
                        }
                        return _node.TreeView.AccessibilityObject.GetChild(Index - 1);

                    case AccessibleNavigation.Next:
                    case AccessibleNavigation.Right:
                        int count = _node.TreeView.AccessibilityObject.GetChildCount();
                        if (Index == count - 1) {
                            return null;
                        }
                        return _node.TreeView.AccessibilityObject.GetChild(Index + 1);

                    case AccessibleNavigation.Up:
                        return Parent;

                    default:
                        System.Diagnostics.Debug.Assert(false, "What direction is this?");
                        return null;
                }
            }

            public override AccessibleStates State {
                get {
                    AccessibleStates ret = AccessibleStates.Selectable | AccessibleStates.Focusable;
                    if (_node.IsSelected) {
                        ret |= AccessibleStates.Focused;
                        ret |= AccessibleStates.Selected;
                    }
                    if (_node.Nodes.Count != 0) {
                        if (_node.IsExpanded) {
                            ret |= AccessibleStates.Expanded;
                        } else {
                            ret |= AccessibleStates.Collapsed;
                        }
                    }
                    return ret;
                }
            }

            public override void Select(AccessibleSelection flags) {
                _node.TreeView.SelectedNode = _node;
            }

            public override AccessibleObject GetFocused() {
                return ((OptionNode)_node.TreeView.SelectedNode).AccessibleObject;
            }

            public override AccessibleObject GetSelected() {
                return ((OptionNode)_node.TreeView.SelectedNode).AccessibleObject;
            }

            public override AccessibleObject HitTest(int x, int y) {
                return ((OptionNode)_node.TreeView.GetNodeAt(x, y)).AccessibleObject;
            }

            public int Index {
                get {
                    int index = 0;
                    foreach (TreeNode outerNode in _node.TreeView.Nodes) {
                        if (_node == outerNode) {
                            return index;
                        }
                        index++;
                        for (int i = outerNode.Nodes.Count - 1; i >= 0; --i) {
                            TreeNode innerNode = outerNode.Nodes[i];
                            if (_node == innerNode) {
                                return index;
                            }
                            index++;
                        }
                    }
                    System.Diagnostics.Debug.Assert(false, "Couldn't find index for node");
                    return 0;
                }
            }
        }
    }

    class OptionFolderNode : OptionNode {
        public bool WasExpanded = true;

        public OptionFolderNode(string name)
            : base(name) {
            StateImageIndex = SelectedImageIndex = ImageIndex = OptionsTreeView.OpenFolderIndex;
        }
    }

    abstract class OptionSettingNode : OptionNode {
        public OptionSettingNode(string text)
            : base(text) {
            StateImageIndex = SelectedImageIndex = ImageIndex = OptionsTreeView.TransparentIndex;
        }

        internal abstract object SettingValue {
            get;
            set;
        }

        internal virtual void DrawNode(DrawTreeNodeEventArgs e) {
            e.Graphics.FillRectangle(SystemBrushes.ControlLightLight, IconBounds);
        }

        internal virtual void ToggleSetting() {
        }

        internal virtual void Connected() {
        }
    }

    class BooleanCheckBoxNode : OptionSettingNode {
        private bool _state;

        public BooleanCheckBoxNode(string text)
            : base(text) {
        }

        internal override object SettingValue {
            get {
                return _state;
            }
            set {
                if (value is bool) {
                    _state = (bool)value;
                } else {
                    throw new InvalidOperationException();
                }
            }
        }

        public override AccessibleObject AccessibleObject {
            get { return new BooleanAccessibleObject(this); }
        }

        public class BooleanAccessibleObject : TreeNodeAccessibleObject {
            private readonly BooleanCheckBoxNode _option;

            public BooleanAccessibleObject(BooleanCheckBoxNode option)
                : base(option) {
                _option = option;
            }

            public override string DefaultAction {
                get {
                    return _option._state ? "Uncheck" : "Check";
                }
            }

            public override void DoDefaultAction() {
                _option.ToggleSetting();
            }

            public override AccessibleStates State {
                get {
                    return _option._state ? AccessibleStates.Checked : base.State;
                }
            }

            public override AccessibleRole Role {
                get { return AccessibleRole.CheckButton; }
            }
        }

        internal override void ToggleSetting() {
            switch (_state) {
                case false: _state = true; break;
                case true: _state = false; break;
            }
        }

        internal override void DrawNode(DrawTreeNodeEventArgs e) {
            var optNode = (OptionNode)e.Node;            
            CheckBoxRenderer.DrawCheckBox(
                e.Graphics,
                optNode.IconBounds.Location,
                _state ?
                    CheckBoxState.CheckedNormal :
                    CheckBoxState.UncheckedNormal
            );
        }
    }

    class TriStateCheckBoxNode : OptionSettingNode {
        private bool? _state;

        public TriStateCheckBoxNode(string text)
            : base(text) {
        }

        internal override object SettingValue {
            get {
                return _state;
            }
            set {
                if (value == null || value is bool) {
                    _state = (bool?)value;
                } else {
                    throw new InvalidOperationException();
                }
            }
        }

        public override AccessibleObject AccessibleObject {
            get { return new TriStateAccessibleObject(this); }
        }

        public class TriStateAccessibleObject : TreeNodeAccessibleObject {
            private readonly TriStateCheckBoxNode _option;

            public TriStateAccessibleObject(TriStateCheckBoxNode option)
                : base(option) {
                _option = option;
            }

            public override string DefaultAction {
                get {
                    switch (_option._state) {
                        case null: return "Check";
                        case true: return "Uncheck";
                        default: return "Mixed";
                    }
                }
            }

            public override void DoDefaultAction() {
                _option.ToggleSetting();
            }

            public override AccessibleStates State {
                get {
                    switch (_option._state) {
                        case null: return AccessibleStates.Mixed;
                        case true: return AccessibleStates.Checked;
                        default: return base.State;
                    }
                }
            }

            public override AccessibleRole Role {
                get { return AccessibleRole.CheckButton; }
            }
        }

        internal override void ToggleSetting() {
            switch (_state) {
                case false: _state = null; break;
                case true: _state = false; break;
                case null: _state = true; break;
            }
        }

        internal override void DrawNode(DrawTreeNodeEventArgs e) {
            var optNode = (OptionNode)e.Node;
            CheckBoxRenderer.DrawCheckBox(
                e.Graphics,
                optNode.IconBounds.Location,
                _state != null ?
                    _state.Value ?
                        CheckBoxState.CheckedNormal :
                        CheckBoxState.UncheckedNormal :
                        CheckBoxState.MixedNormal
            );
        }
    }

    sealed class IntegerNode : OptionSettingNode, IDisposable {
        private int _value;
        private readonly TextBox _textBox;

        public IntegerNode(string text)
            : base(text) {
            _textBox = new TextBox();
            _textBox.TextChanged += TextChanged;
            _textBox.GotFocus += TextBoxGotFocus;
        }

        public void Dispose() {
            _textBox.Dispose();
        }

        private void TextBoxGotFocus(object sender, EventArgs e) {
            TreeView.SelectedNode = this;
        }

        private void TextChanged(object sender, EventArgs e) {
            uint value;
            if (UInt32.TryParse(_textBox.Text, out value)) {
                _value = (int)value;
            }
        }

        internal override void Connected() {
            _textBox.Font = TreeView.Font;
            TreeView.Controls.Add(_textBox);
            TreeView.Invalidated += TreeViewInvalidated;
        }

        private void TreeViewInvalidated(object sender, InvalidateEventArgs e) {
            if (IsVisible) {
                _textBox.Show();
                _textBox.Top = Bounds.Top;
                _textBox.Left = Bounds.Right;
                _textBox.Height = Bounds.Height - 4;
                _textBox.Width = 40;
            } else {
                _textBox.Hide();
            }
        }

        internal override object SettingValue {
            get {
                return _value;
            }
            set {
                _value = (int)value;
                _textBox.Text = _value.ToString();
            }
        }

        internal string Value {
            get {
                return _textBox.Text;
            }

        }

        // taken from C#
        class IntegerAccessibleObject : TreeNodeAccessibleObject {
            private readonly IntegerNode _option;

            public IntegerAccessibleObject(IntegerNode option)
                : base(option) {
                _option = option;
            }

            public override AccessibleRole Role {
                get { return AccessibleRole.Text; }
            }

            public override string Value {
                get { return _option.Value; }
                set { _option._value = (int)uint.Parse(value); }
            }
        }

        public override System.Windows.Forms.AccessibleObject AccessibleObject {
            get {
                return new IntegerAccessibleObject(this);
            }
        }
    }
}

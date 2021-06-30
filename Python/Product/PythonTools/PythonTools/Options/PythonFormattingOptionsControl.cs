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
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Options {
    internal partial class PythonFormattingOptionsControl : UserControl {
        private readonly Dictionary<string, OptionSettingNode> _nodes = new Dictionary<string, OptionSettingNode>();
        private TreeNode _deactivatedNode;
        private readonly IServiceProvider _serviceProvider;
        private readonly ITextBuffer _buffer;
        private static readonly string DefaultText = Strings.FormattingOptionsDefaultText;

        public PythonFormattingOptionsControl(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
            InitializeComponent();

            _optionsTree.AfterSelect += AfterSelectOrCheckNode;
            _optionsTree.AfterCheck += AfterSelectOrCheckNode;

            var compModel = _serviceProvider.GetComponentModel();
            var editorFactory = compModel.GetService<ITextEditorFactoryService>();
            var bufferFactory = compModel.GetService<ITextBufferFactoryService>();
            var contentTypeRegistry = compModel.GetService<IContentTypeRegistryService>();
            var textContentType = contentTypeRegistry.GetContentType("Python");

            _buffer = bufferFactory.CreateTextBuffer(textContentType);
            var editor = editorFactory.CreateTextView(_buffer, CreateRoleSet());

            _editorHost.Child = (UIElement)editor;
            _buffer.Replace(new Span(0, 0), DefaultText);
        }

        private ITextViewRoleSet/*!*/ CreateRoleSet() {
            var textEditorFactoryService = _serviceProvider.GetComponentModel().GetService<ITextEditorFactoryService>();
            return textEditorFactoryService.CreateTextViewRoleSet(
                PredefinedTextViewRoles.Interactive,
                PredefinedTextViewRoles.PrimaryDocument,
                PredefinedTextViewRoles.Zoomable,
                PredefinedTextViewRoles.Document
            );
        }

        private string PreviewText {
            get {
                return _buffer.CurrentSnapshot.GetText();
            }
            set {
                _buffer.Replace(
                    new Span(0, _buffer.CurrentSnapshot.Length),
                    value
                );
            }
        }

        private void AfterSelectOrCheckNode(object sender, TreeViewEventArgs e) {
            if (e.Node is OptionSettingNode) {
                var optionsNode = (OptionSettingNode)e.Node;
                var optionInfo = (OptionInfo)e.Node.Tag;

                PreviewText = optionInfo.GetPreviewText(optionsNode.SettingValue);
            } else {
                PreviewText = DefaultText;
            }
        }

        internal PythonFormattingOptionsControl(IServiceProvider serviceProvider, params OptionCategory[] options)
            : this(serviceProvider) {
            _optionsTree.BeginUpdate();
            foreach (var cat in options) {
                var curCat = new OptionFolderNode(cat.Description);
                _optionsTree.Nodes.Add(curCat);

                foreach (var option in cat.Options) {
                    var optNode = option.CreateNode();
                    optNode.Tag = option;
                    curCat.Nodes.Add(optNode);
                    optNode.Connected();

                    _nodes[option.Key] = optNode;
                }
            }
            _optionsTree.EndUpdate();
        }

        internal void OnActivated() {
            // when the user switches between pages we lose focus and when
            // we come back our selected node changes.  So we track the node
            // and fix it back up when we get activated again.  If this is the
            // 1st time loading we make sure we're scrolled to the top by ensuring
            // the first node is visible.
            if (_deactivatedNode != null) {
                _optionsTree.SelectedNode = _deactivatedNode;
                _optionsTree.Select();
                _optionsTree.Focus();
                _deactivatedNode.EnsureVisible();
            } else if (_optionsTree.Nodes.Count > 0) {
                _optionsTree.Nodes[0].EnsureVisible();
            }
        }

        internal void OnDeactivated() {
            _deactivatedNode = _optionsTree.SelectedNode;
        }

        protected override void OnLoad(EventArgs e) {
            foreach (var node in _optionsTree.Nodes) {
                OptionFolderNode folder = node as OptionFolderNode;
                if (folder != null && folder.WasExpanded) {
                    // this control gets closed and re-opened when the user closs
                    // the options dialog and re-opens it.  We remember which nodes
                    // were open and re-expand them so that they are unchanged.
                    folder.Expand();
                }
            }

            VsShellUtilities.ApplyTreeViewThemeStyles(_optionsTree);

            base.OnLoad(e);
        }

        public object GetSetting(string key) {
            return _nodes[key].SettingValue;
        }

        public void SetSetting(string key, object value) {
            _nodes[key].SettingValue = value;
        }
    }
}

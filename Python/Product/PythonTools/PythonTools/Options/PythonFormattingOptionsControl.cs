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

using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using Microsoft.PythonTools.Editor.Formatting;

namespace Microsoft.PythonTools.Options {
    public partial class PythonFormattingOptionsControl : UserControl {
        public PythonFormattingOptionsControl() {
            InitializeComponent();
        }

        internal void SyncControlWithPageSettings(PythonToolsService pyService) {
            object[] items;
            string[] itemNames;

            if (_formatterCombo.Items.Count == 0) {
                var formattingProviders = pyService.ComponentModel.DefaultExportProvider.GetExports<IPythonFormatter>();
                Debug.Assert(formattingProviders != null);

                itemNames = formattingProviders.Select(p => p.Value.DisplayName).ToArray();
                Debug.Assert(itemNames.Length > 0);

                items = itemNames.Cast<object>().ToArray();
                _formatterCombo.Items.AddRange(items);
            } else {
                items = _formatterCombo.Items.Cast<object>().ToArray();
                itemNames = items.Cast<string>().ToArray();
            }

            var formatter = string.IsNullOrEmpty(pyService.FormattingOptions.Formatter) && itemNames.Contains("black")
                ? "black"
                : !string.IsNullOrEmpty(pyService.FormattingOptions.Formatter)
                    ? pyService.FormattingOptions.Formatter
                    : (string)items[0];
            
            pyService.FormattingOptions.Formatter = formatter;

            _formatterCombo.SelectedItem = formatter;
            _pasteRemovesReplPrompts.Checked = pyService.FormattingOptions.PasteRemovesReplPrompts;
        }

        internal void SyncPageWithControlSettings(PythonToolsService pyService) {
            pyService.FormattingOptions.PasteRemovesReplPrompts = _pasteRemovesReplPrompts.Checked;
            pyService.FormattingOptions.Formatter = (string)_formatterCombo.SelectedItem;
        }
    }
}

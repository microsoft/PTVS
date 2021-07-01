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

namespace Microsoft.PythonTools.Options
{
    public partial class PythonDiagnosticsOptionsControl : UserControl
    {
        public PythonDiagnosticsOptionsControl()
        {
            InitializeComponent();
        }

        public Action<bool> CopyToClipboard;

        public Action<bool> SaveToFile;

        private void _copyToClipboard_Click(object sender, EventArgs e)
        {
            // We pass in the live value of the include analysis logs option
            // because the value hasn't been applied to DiagnosticsOptions
            // instance (it's done when OK is clicked).
            System.Diagnostics.Debug.Assert(CopyToClipboard != null, "No listener for CopyToClipboard event");
            CopyToClipboard?.Invoke(_includeAnalysisLogs.Checked);
        }

        private void _saveToFile_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.Assert(SaveToFile != null, "No listener for SaveToFile event");
            SaveToFile?.Invoke(_includeAnalysisLogs.Checked);
        }

        internal void SyncControlWithPageSettings(PythonToolsService pyService)
        {
            _includeAnalysisLogs.Checked = pyService.DiagnosticsOptions.IncludeAnalysisLogs;
        }

        internal void SyncPageWithControlSettings(PythonToolsService pyService)
        {
            pyService.DiagnosticsOptions.IncludeAnalysisLogs = _includeAnalysisLogs.Checked;
        }
    }
}

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
    public partial class PythonCondaOptionsControl : UserControl
    {
        private readonly IServiceProvider _serviceProvider;

        private PythonCondaOptionsControl() : this(null)
        {
        }

        public PythonCondaOptionsControl(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            InitializeComponent();
        }

        internal void SyncControlWithPageSettings(PythonToolsService pyService)
        {
            _condaPathTextBox.Text = pyService.CondaOptions.CustomCondaExecutablePath ?? string.Empty;
        }

        internal void SyncPageWithControlSettings(PythonToolsService pyService)
        {
            pyService.CondaOptions.CustomCondaExecutablePath = _condaPathTextBox.Text;
        }

        private void condaPathButton_Click(object sender, System.EventArgs e)
        {
            var newPath = _serviceProvider.BrowseForFileOpen(Handle, Strings.CondaExecutableFilter, _condaPathTextBox.Text);
            if (!string.IsNullOrEmpty(newPath))
            {
                _condaPathTextBox.Text = newPath;
            }
        }
    }
}

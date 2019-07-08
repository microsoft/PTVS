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
using System.Linq;
using System.Windows.Forms;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Options;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Project {
    public partial class PythonTestPropertyPageControl : UserControl {
        static readonly IPythonInterpreterFactory Separator =
            new InterpreterPlaceholder("", Strings.PythonGeneralPropertyPageControl_OtherInterpretersSeparator);
        static readonly IPythonInterpreterFactory GlobalDefault =
            new InterpreterPlaceholder("", Strings.PythonGeneralPropertyPageControl_UseGlobalDefaultInterpreter);

        private IInterpreterRegistryService _service;
        private readonly PythonTestPropertyPage _propPage;

        internal PythonTestPropertyPageControl(PythonTestPropertyPage newPage) {
            InitializeComponent();

            _propPage = newPage;
        }

        
        internal void SaveSettings() {
            _service = _propPage.Project.Site.GetComponentModel().GetService<IInterpreterRegistryService>();
           
            _propPage.Project.SetProjectProperty(PythonConstants.PyTestArgsSetting, PytestArgs);
            _propPage.Project.SetProjectProperty(PythonConstants.PyTestPathSetting, PytestPath);
            _propPage.Project.SetProjectProperty(PythonConstants.PyTestEnabledSetting, PytestEnabled.ToString());
        }

        internal void LoadSettings() {
            _service = _propPage.Project.Site.GetComponentModel().GetService<IInterpreterRegistryService>();
           
            PytestArgs = _propPage.Project.GetProjectProperty(PythonConstants.PyTestArgsSetting, false);
            PytestPath = _propPage.Project.GetProjectProperty(PythonConstants.PyTestPathSetting, false);
            if (string.IsNullOrEmpty(PytestPath)) {
                PytestPath = "pytest";
            }
            PytestEnabled = Convert.ToBoolean(_propPage.Project.GetProjectProperty(PythonConstants.PyTestEnabledSetting, false));
        }

        public string PytestPath {
            get { return _pytestPath.Text; }
            set { _pytestPath.Text = value; }
        }

        public string PytestArgs {
            get { return _arguments.Text; }
            set { _arguments.Text = value; }
        }

        public bool PytestEnabled {
            get { return _pytestEnabled.Checked; }
            set { _pytestEnabled.Checked = value; }
        }

        private void Changed(object sender, EventArgs e) {
            _propPage.IsDirty = true;
        }

        private void TableLayoutPanel2_Paint(object sender, PaintEventArgs e) {

        }

        private void _applicationGroup_Enter(object sender, EventArgs e) {

        }
    }
}

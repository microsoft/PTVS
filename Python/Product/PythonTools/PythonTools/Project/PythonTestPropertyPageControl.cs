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

        internal void LoadSettings() {
            _service = _propPage.Project.Site.GetComponentModel().GetService<IInterpreterRegistryService>();

            UnitTestArgs = _propPage.Project.GetProjectProperty(PythonConstants.UnitTestArgsSetting, false);
            string testFrameworkStr = _propPage.Project.GetProjectProperty(PythonConstants.TestFrameworkSetting, false);
            TestFramework = TestFrameworkType.None; 
            if(Enum.TryParse<TestFrameworkType>(testFrameworkStr, ignoreCase:true, out TestFrameworkType parsedFramworked)) {
                TestFramework = parsedFramworked;
            }
        }

        internal void SaveSettings() {
            _service = _propPage.Project.Site.GetComponentModel().GetService<IInterpreterRegistryService>();

            _propPage.Project.SetProjectProperty(PythonConstants.UnitTestArgsSetting, UnitTestArgs);
            _propPage.Project.SetProjectProperty(PythonConstants.TestFrameworkSetting, TestFramework.ToString());
        }


        internal TestFrameworkType TestFramework {
            get {
                return (TestFrameworkType)_testFramework.SelectedIndex;
            }
            set {
                _testFramework.SelectedIndex = (int)value;
            }
        }

        public string UnitTestArgs {
            get { return _unitTestArgs.Text; }
            set { _unitTestArgs.Text = value; }
        }

        private void Changed(object sender, EventArgs e) {
            _propPage.IsDirty = true;
        }

        private void TestFrameworkSelectedIndexChanged(object sender, EventArgs e) {
            _propPage.IsDirty = true;
            UpdateLayout(TestFramework);
        }

        private void UpdateLayout(TestFrameworkType framework) {
            if (framework == TestFrameworkType.UnitTest) {
                _unitTestArgs.Visible = true;
                _unitTestArgsLabel.Visible = true;
            } else {
                _unitTestArgs.Visible = false;
                _unitTestArgsLabel.Visible = false;
            }
        }

        private void TableLayoutPanel_Paint(object sender, PaintEventArgs e) {

        }
    }
}

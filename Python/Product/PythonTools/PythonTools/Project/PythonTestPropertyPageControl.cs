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
using System.Windows.Forms;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Options;

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

            string rootDir = _propPage.Project.GetProjectProperty(PythonConstants.UnitTestRootDirectorySetting, false);
            UnitTestRootDir = string.IsNullOrEmpty(rootDir) ? PythonConstants.DefaultUnitTestRootDirectory : rootDir;

            string pattern = _propPage.Project.GetProjectProperty(PythonConstants.UnitTestPatternSetting, false);
            UnitTestPattern = string.IsNullOrEmpty(pattern) ? PythonConstants.DefaultUnitTestPattern : pattern;

            string testFrameworkStr = _propPage.Project.GetProjectProperty(PythonConstants.TestFrameworkSetting, false);
            TestFramework = TestFrameworkType.None;
            if (Enum.TryParse<TestFrameworkType>(testFrameworkStr, ignoreCase: true, out TestFrameworkType parsedFramworked)) {
                TestFramework = parsedFramworked;
            }
        }

        internal void SaveSettings() {
            _service = _propPage.Project.Site.GetComponentModel().GetService<IInterpreterRegistryService>();

            _propPage.Project.SetProjectProperty(PythonConstants.TestFrameworkSetting, TestFramework.ToString());
            _propPage.Project.SetProjectProperty(PythonConstants.UnitTestPatternSetting, UnitTestPattern);
            _propPage.Project.SetProjectProperty(PythonConstants.UnitTestRootDirectorySetting, UnitTestRootDir);
        }


        internal TestFrameworkType TestFramework {
            get {
                return (TestFrameworkType)_testFramework.SelectedIndex;
            }
            set {
                _testFramework.SelectedIndex = (int)value;
            }
        }

        public string UnitTestRootDir {
            get { return _unitTestRootDir.Text; }
            set { _unitTestRootDir.Text = value; }
        }

        public string UnitTestPattern {
            get { return _unitTestPattern.Text; }
            set { _unitTestPattern.Text = value; }
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
                _unitTestRootDir.Visible = true;
                _unittestRootDirLabel.Visible = true;
                _unitTestPattern.Visible = true;
                _unittestPatternLabel.Visible = true;
            } else {
                _unitTestRootDir.Visible = false;
                _unittestRootDirLabel.Visible = false;
                _unitTestPattern.Visible = false;
                _unittestPatternLabel.Visible = false;
            }
        }
    }
}

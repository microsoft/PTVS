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

using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project
{
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
        Justification = "object is owned by VS")]
    [Guid(PythonConstants.TestPropertyPageGuid)]
    public sealed class PythonTestPropertyPage : CommonPropertyPage
    {
        private readonly PythonTestPropertyPageViewModel _viewModel;
        private readonly PythonTestPropertyPageHostControl _control;

        public PythonTestPropertyPage()
        {
            _viewModel = new PythonTestPropertyPageViewModel();
            _viewModel.PropertyChanged += OnPropertyChanged;
            _control = new PythonTestPropertyPageHostControl();
            _control.HostedControl.DataContext = _viewModel;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _viewModel.PropertyChanged -= OnPropertyChanged;
            }
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!Loading)
            {
                IsDirty = true;
            }
        }

        public override Control Control
        {
            get { return _control; }
        }

        public override void Apply()
        {
            Project.SetProjectProperty(PythonConstants.TestFrameworkSetting, _viewModel.SelectedFramework);
            Project.SetProjectProperty(PythonConstants.UnitTestPatternSetting, _viewModel.UnitTestPattern);
            Project.SetProjectProperty(PythonConstants.UnitTestRootDirectorySetting, _viewModel.UnitTestRootDirectory);
            IsDirty = false;
        }

        public override void LoadSettings()
        {
            Loading = true;
            try
            {
                string framework = Project.GetProjectProperty(PythonConstants.TestFrameworkSetting, false);
                if (!Enum.TryParse(framework, ignoreCase: true, out TestFrameworkType parsedFramework))
                {
                    parsedFramework = TestFrameworkType.None;
                }
                _viewModel.SelectedFramework = parsedFramework.ToString().ToLowerInvariant();

                string rootDir = Project.GetProjectProperty(PythonConstants.UnitTestRootDirectorySetting, false);
                _viewModel.UnitTestRootDirectory = string.IsNullOrEmpty(rootDir)
                    ? PythonConstants.DefaultUnitTestRootDirectory
                    : rootDir;

                string pattern = Project.GetProjectProperty(PythonConstants.UnitTestPatternSetting, false);
                _viewModel.UnitTestPattern = string.IsNullOrEmpty(pattern)
                    ? PythonConstants.DefaultUnitTestPattern
                    : pattern;
            }
            finally
            {
                Loading = false;
            }
        }

        public override string Name
        {
            get { return Strings.PythonTestPropertyPageLabel; }
        }
    }
}

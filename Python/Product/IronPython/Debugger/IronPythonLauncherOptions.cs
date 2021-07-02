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
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.IronPythonTools.Debugger {
    public partial class IronPythonLauncherOptions : UserControl, IPythonLauncherOptions {
        private readonly IPythonProject _properties;
        private bool _loadingSettings;
        public const string DebugStandardLibrarySetting = "DebugStdLib";
        public IronPythonLauncherOptions() {
            InitializeComponent();

            _toolTip.SetToolTip(_searchPathLabel, Strings.Launcher_SearchPathHelp);
            _toolTip.SetToolTip(_searchPaths, Strings.Launcher_SearchPathHelp);

            _toolTip.SetToolTip(_arguments, Strings.Launcher_ArgumentsHelp);
            _toolTip.SetToolTip(_argumentsLabel, Strings.Launcher_ArgumentsHelp);

            _toolTip.SetToolTip(_interpArgsLabel, Strings.Launcher_InterpreterArgsHelp);
            _toolTip.SetToolTip(_interpArgs, Strings.Launcher_InterpreterArgsHelp);

            _toolTip.SetToolTip(_interpreterPath, Strings.Launcher_InterpreterPathHelp);
            _toolTip.SetToolTip(_interpreterPathLabel, Strings.Launcher_InterpreterPathHelp);
        }

        public IronPythonLauncherOptions(IPythonProject properties) : this() {
            _properties = properties;
        }

        #region ILauncherOptions Members

        public void SaveSettings() {
            _properties.SetProperty(PythonConstants.SearchPathSetting, SearchPaths);
            _properties.SetProperty(PythonConstants.CommandLineArgumentsSetting, Arguments);
            _properties.SetProperty(PythonConstants.InterpreterPathSetting, InterpreterPath);
            _properties.SetProperty(DebugStandardLibrarySetting, DebugStandardLibrary.ToString());
            _properties.SetProperty(PythonConstants.InterpreterArgumentsSetting, _interpArgs.Text);
            RaiseIsSaved();
        }

        public void LoadSettings() {
            _loadingSettings = true;
            SearchPaths = _properties.GetUnevaluatedProperty(PythonConstants.SearchPathSetting);
            InterpreterPath = _properties.GetUnevaluatedProperty(PythonConstants.InterpreterPathSetting);
            Arguments = _properties.GetUnevaluatedProperty(PythonConstants.CommandLineArgumentsSetting);
            DebugStandardLibrary = Convert.ToBoolean(_properties.GetProperty(DebugStandardLibrarySetting));
            _interpArgs.Text = _properties.GetUnevaluatedProperty(PythonConstants.InterpreterArgumentsSetting);
            _loadingSettings = false;
        }

        public void ReloadSetting(string settingName) {
            switch (settingName) {
                case PythonConstants.SearchPathSetting:
                    SearchPaths = _properties.GetUnevaluatedProperty(PythonConstants.SearchPathSetting);
                    break;
            }
        }

        public event EventHandler<DirtyChangedEventArgs> DirtyChanged;

        Control IPythonLauncherOptions.Control {
            get { return this; }
        }

        #endregion

        public string SearchPaths {
            get { return _searchPaths.Text; }
            set { _searchPaths.Text = value; }
        }

        public string Arguments {
            get { return _arguments.Text; }
            set { _arguments.Text = value; }
        }

        public string InterpreterPath {
            get { return _interpreterPath.Text; }
            set { _interpreterPath.Text = value; }
        }

        public bool DebugStandardLibrary {
            get { return _debugStdLib.Checked; }
            set { _debugStdLib.Checked = value; }
        }

        private void RaiseIsDirty() {
            if (!_loadingSettings) {
                var isDirty = DirtyChanged;
                if (isDirty != null) {
                    DirtyChanged(this, DirtyChangedEventArgs.DirtyValue);
                }
            }
        }

        private void RaiseIsSaved() {
            var isDirty = DirtyChanged;
            if (isDirty != null) {
                DirtyChanged(this, DirtyChangedEventArgs.SavedValue);
            }
        }

        private void SearchPathsTextChanged(object sender, EventArgs e) {
            RaiseIsDirty();
        }

        private void ArgumentsTextChanged(object sender, EventArgs e) {
            RaiseIsDirty();
        }

        private void InterpreterPathTextChanged(object sender, EventArgs e) {
            RaiseIsDirty();
        }

        private void DebugStdLibCheckedChanged(object sender, EventArgs e) {
            RaiseIsDirty();
        }

        private void InterpreterArgsTextChanged(object sender, EventArgs e) {
            RaiseIsDirty();
        }
    }
}

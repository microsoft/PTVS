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
using System.Windows.Forms;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    public partial class DefaultPythonLauncherOptions : UserControl, IPythonLauncherOptions {
        private readonly IPythonProject _properties;
        private bool _loadingSettings;

        public DefaultPythonLauncherOptions(IPythonProject properties) {
            _properties = properties;
            InitializeComponent();
            const string searchPathHelp = "Specifies additional directories which are added to sys.path for making libraries available for importing.";
            const string argumentsHelp = "Specifies arguments which are passed to the script and available via sys.argv.";
            const string interpArgsHelp = "Specifies arguments which alter how the interpreter is started (for example, -O to generate optimized byte code).";
            const string interpPathHelp = "Overrides the interpreter executable which is used for launching the project.";

            _toolTip.SetToolTip(_searchPathLabel, searchPathHelp);
            _toolTip.SetToolTip(_searchPaths, searchPathHelp);

            _toolTip.SetToolTip(_arguments, argumentsHelp);
            _toolTip.SetToolTip(_argumentsLabel, argumentsHelp);

            _toolTip.SetToolTip(_interpArgsLabel, interpArgsHelp);
            _toolTip.SetToolTip(_interpArgs, interpArgsHelp);

            _toolTip.SetToolTip(_interpreterPath, interpPathHelp);
            _toolTip.SetToolTip(_interpreterPathLabel, interpPathHelp);

#if DEV11_OR_LATER
            _mixedMode.Visible = true;
#endif
        }

        #region ILauncherOptionsControl Members

        public void SaveSettings() {
            _properties.SetProperty(CommonConstants.SearchPath, SearchPaths);
            _properties.SetProperty(CommonConstants.CommandLineArguments, Arguments);
            _properties.SetProperty(CommonConstants.InterpreterPath, InterpreterPath);
            _properties.SetProperty(CommonConstants.InterpreterArguments, InterpreterArguments);
            _properties.SetProperty(PythonConstants.EnableNativeCodeDebugging, EnableNativeCodeDebugging.ToString());
            RaiseIsSaved();
        }

        public void LoadSettings() {
            _loadingSettings = true;
            SearchPaths = _properties.GetUnevaluatedProperty(CommonConstants.SearchPath);
            InterpreterPath = _properties.GetUnevaluatedProperty(CommonConstants.InterpreterPath);
            Arguments = _properties.GetUnevaluatedProperty(CommonConstants.CommandLineArguments);
            InterpreterArguments = _properties.GetUnevaluatedProperty(CommonConstants.InterpreterArguments);

            bool enableNativeCodeDebugging;
            bool.TryParse(_properties.GetUnevaluatedProperty(PythonConstants.EnableNativeCodeDebugging), out enableNativeCodeDebugging);
            EnableNativeCodeDebugging = enableNativeCodeDebugging;

            _loadingSettings = false;
        }

        public void ReloadSetting(string settingName) {
            switch (settingName) {
                case CommonConstants.SearchPath:
                    SearchPaths = _properties.GetUnevaluatedProperty(CommonConstants.SearchPath);
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

        public string InterpreterArguments {
            get { return _interpArgs.Text; }
            set { _interpArgs.Text = value; }
        }

        public bool EnableNativeCodeDebugging {
            get { return _mixedMode.Checked; }
            set { _mixedMode.Checked = value; }
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

        private void InterpreterArgumentsTextChanged(object sender, EventArgs e) {
            RaiseIsDirty();
        }

        private void _mixedMode_CheckedChanged(object sender, EventArgs e) {
            RaiseIsDirty();
        }
    }
}

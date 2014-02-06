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
using Microsoft.PythonTools;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project.Web {
    public partial class PythonWebLauncherOptions : UserControl, IPythonLauncherOptions {
        private readonly IPythonProject _properties;
        private bool _loadingSettings;

        public PythonWebLauncherOptions() {
            InitializeComponent();

            _toolTip.SetToolTip(_searchPathLabel, SR.GetString(SR.WebLauncherSearchPathHelp));
            _toolTip.SetToolTip(_searchPaths, SR.GetString(SR.WebLauncherSearchPathHelp));

            _toolTip.SetToolTip(_arguments, SR.GetString(SR.WebLauncherArgumentsHelp));
            _toolTip.SetToolTip(_argumentsLabel, SR.GetString(SR.WebLauncherArgumentsHelp));

            _toolTip.SetToolTip(_interpArgsLabel, SR.GetString(SR.WebLauncherInterpreterArgumentsHelp));
            _toolTip.SetToolTip(_interpArgs, SR.GetString(SR.WebLauncherInterpreterArgumentsHelp));

            _toolTip.SetToolTip(_interpreterPath, SR.GetString(SR.WebLauncherInterpreterPathHelp));
            _toolTip.SetToolTip(_interpreterPathLabel, SR.GetString(SR.WebLauncherInterpreterPathHelp));

            _toolTip.SetToolTip(_launchUrl, SR.GetString(SR.WebLauncherLaunchUrlHelp));
            _toolTip.SetToolTip(_launchUrlLabel, SR.GetString(SR.WebLauncherLaunchUrlHelp));

            _toolTip.SetToolTip(_portNumber, SR.GetString(SR.WebLauncherPortNumberHelp));
            _toolTip.SetToolTip(_portNumberLabel, SR.GetString(SR.WebLauncherPortNumberHelp));
        }

        public PythonWebLauncherOptions(IPythonProject properties)
            : this() {
            _properties = properties;
        }

        #region ILauncherOptions Members

        public void SaveSettings() {
            _properties.SetProperty(PythonConstants.SearchPathSetting, SearchPaths);
            _properties.SetProperty(PythonConstants.CommandLineArgumentsSetting, Arguments);
            _properties.SetProperty(PythonConstants.InterpreterPathSetting, InterpreterPath);
            _properties.SetProperty(PythonConstants.InterpreterArgumentsSetting, _interpArgs.Text);
            _properties.SetProperty(PythonConstants.WebBrowserUrlSetting, _launchUrl.Text);
            _properties.SetProperty(PythonConstants.WebBrowserPortSetting, _portNumber.Text);
            RaiseIsSaved();
        }

        public void LoadSettings() {
            _loadingSettings = true;
            SearchPaths = _properties.GetUnevaluatedProperty(PythonConstants.SearchPathSetting);
            InterpreterPath = _properties.GetUnevaluatedProperty(PythonConstants.InterpreterPathSetting);
            Arguments = _properties.GetUnevaluatedProperty(PythonConstants.CommandLineArgumentsSetting);
            _interpArgs.Text = _properties.GetUnevaluatedProperty(PythonConstants.InterpreterArgumentsSetting);
            _launchUrl.Text = _properties.GetUnevaluatedProperty(PythonConstants.WebBrowserUrlSetting);
            _portNumber.Text = _properties.GetUnevaluatedProperty(PythonConstants.WebBrowserPortSetting);
            _loadingSettings = false;
        }

        public void ReloadSetting(string settingName) {
            switch (settingName) {
                case PythonConstants.SearchPathSetting:
                    SearchPaths = _properties.GetUnevaluatedProperty(PythonConstants.SearchPathSetting);
                    break;
                case PythonConstants.InterpreterPathSetting:
                    InterpreterPath = _properties.GetUnevaluatedProperty(PythonConstants.InterpreterPathSetting);
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

        private void RaiseIsSaved() {
            var isDirty = DirtyChanged;
            if (isDirty != null) {
                DirtyChanged(this, DirtyChangedEventArgs.SavedValue);
            }
        }

        private void Setting_TextChanged(object sender, EventArgs e) {
            if (!_loadingSettings) {
                var isDirty = DirtyChanged;
                if (isDirty != null) {
                    DirtyChanged(this, DirtyChangedEventArgs.DirtyValue);
                }
            }
        }
    }
}

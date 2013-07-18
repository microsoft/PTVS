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

namespace Microsoft.PythonTools.Django.Debugger {
    public partial class DjangoLauncherOptions : UserControl, IPythonLauncherOptions {
        private readonly IPythonProject _properties;
        private bool _loadingSettings;
        public const string SettingModulesSetting = "DjangoSettingsModule";
        public DjangoLauncherOptions() {
            InitializeComponent();

            const string searchPathHelp = "Specifies additional directories which are added to sys.path for making libraries available for importing.";
            const string argumentsHelp = "Specifies arguments which are passed to the script and available via sys.argv.";
            const string interpArgsHelp = "Specifies arguments which alter how the interpreter is started (for example, -O to generate optimized byte code).";
            const string interpPathHelp = "Overrides the interpreter executable which is used for launching the project.";
            const string settingsModuleHelp = "The Python path to a settings module, e.g. \"myproject.settings.main\". If this isn't provided, the DJANGO_SETTINGS_MODULE environment variable will be used.";
            const string portNumberHelp = "The port to launch when using the Django development server.  When not specified a random free port will be used.";
            const string launchUrlHelp = "The URL to launch when using the Django development server.  When not specified http://localhost will be launched.";

            _toolTip.SetToolTip(_searchPathLabel, searchPathHelp);
            _toolTip.SetToolTip(_searchPaths, searchPathHelp);

            _toolTip.SetToolTip(_arguments, argumentsHelp);
            _toolTip.SetToolTip(_argumentsLabel, argumentsHelp);

            _toolTip.SetToolTip(_interpArgsLabel, interpArgsHelp);
            _toolTip.SetToolTip(_interpArgs, interpArgsHelp);

            _toolTip.SetToolTip(_interpreterPath, interpPathHelp);
            _toolTip.SetToolTip(_interpreterPathLabel, interpPathHelp);

            _toolTip.SetToolTip(_settingsModule, settingsModuleHelp);
            _toolTip.SetToolTip(_settingsModuleLabel, settingsModuleHelp);

            _toolTip.SetToolTip(_launchUrl, launchUrlHelp);
            _toolTip.SetToolTip(_launchUrlLabel, launchUrlHelp);

            _toolTip.SetToolTip(_portNumber, portNumberHelp);
            _toolTip.SetToolTip(_portNumberLabel, portNumberHelp);
        }

        public DjangoLauncherOptions(IPythonProject properties)
            : this() {
            _properties = properties;
        }

        #region ILauncherOptions Members

        public void SaveSettings() {
            _properties.SetProperty(PythonConstants.SearchPathSetting, SearchPaths);
            _properties.SetProperty(PythonConstants.CommandLineArgumentsSetting, Arguments);
            _properties.SetProperty(PythonConstants.InterpreterPathSetting, InterpreterPath);
            _properties.SetProperty(PythonConstants.InterpreterArgumentsSetting, _interpArgs.Text);
            _properties.SetProperty(SettingModulesSetting, _settingsModule.Text);
            _properties.SetProperty(PythonConstants.WebBrowserUrlSetting, _launchUrl.Text);
            _properties.SetProperty(PythonConstants.WebBrowserPortSetting, _portNumber.Text);
            RaiseIsSaved();
        }

        public void LoadSettings() {
            _loadingSettings = true;
            SearchPaths = _properties.GetUnevaluatedProperty(PythonConstants.SearchPathSetting);
            InterpreterPath = _properties.GetUnevaluatedProperty(PythonConstants.InterpreterPathSetting);
            Arguments = _properties.GetUnevaluatedProperty(PythonConstants.CommandLineArgumentsSetting);
            SettingsModule = _properties.GetUnevaluatedProperty(SettingModulesSetting);
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

        public string SettingsModule {
            get { return _settingsModule.Text; }
            set { _settingsModule.Text = value; }
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

        private void SettingsModuleTextChanged(object sender, EventArgs e) {
            RaiseIsDirty();
        }

        private void PortNumberTextChanged(object sender, EventArgs e) {
            RaiseIsDirty();
        }

        private void LaunchUrlTextChanged(object sender, EventArgs e) {
            RaiseIsDirty();
        }
    }
}

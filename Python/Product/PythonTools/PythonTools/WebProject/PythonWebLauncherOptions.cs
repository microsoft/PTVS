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
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project.Web {
    public partial class PythonWebLauncherOptions : UserControl, IPythonLauncherOptions {
        private readonly Dictionary<string, TextBox> _textBoxMap;
        private readonly Dictionary<string, ComboBox> _comboBoxMap;
        private readonly IPythonProject _properties;
        private bool _loadingSettings;

        public PythonWebLauncherOptions() {
            InitializeComponent();

            _textBoxMap = new Dictionary<string, TextBox> {
                { PythonConstants.SearchPathSetting, _searchPaths },
                { PythonConstants.CommandLineArgumentsSetting, _arguments },
                { PythonConstants.InterpreterPathSetting, _interpreterPath },
                { PythonConstants.InterpreterArgumentsSetting, _interpArgs },
                { PythonConstants.WebBrowserUrlSetting, _launchUrl },
                { PythonConstants.WebBrowserPortSetting, _portNumber },
                { PythonWebLauncher.RunWebServerTargetProperty, _runServerTarget },
                { PythonWebLauncher.RunWebServerArgumentsProperty, _runServerArguments },
                { PythonWebLauncher.RunWebServerEnvironmentProperty, _runServerEnvironment },
                { PythonWebLauncher.DebugWebServerTargetProperty, _debugServerTarget },
                { PythonWebLauncher.DebugWebServerArgumentsProperty, _debugServerArguments },
                { PythonWebLauncher.DebugWebServerEnvironmentProperty, _debugServerEnvironment }
            };

            _comboBoxMap = new Dictionary<string, ComboBox> {
                { PythonWebLauncher.RunWebServerTargetTypeProperty, _runServerTargetType },
                { PythonWebLauncher.DebugWebServerTargetTypeProperty, _debugServerTargetType }
            };

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

            _toolTip.SetToolTip(_runServerTarget, SR.GetString(SR.WebLauncherRunServerTargetHelp));
            _toolTip.SetToolTip(_runServerTargetLabel, SR.GetString(SR.WebLauncherRunServerTargetHelp));

            _toolTip.SetToolTip(_runServerTargetType, SR.GetString(SR.WebLauncherRunServerTargetTypeHelp));

            _toolTip.SetToolTip(_runServerArguments, SR.GetString(SR.WebLauncherRunServerArgumentsHelp));
            _toolTip.SetToolTip(_runServerArgumentsLabel, SR.GetString(SR.WebLauncherRunServerArgumentsHelp));

            _toolTip.SetToolTip(_runServerEnvironment, SR.GetString(SR.WebLauncherRunServerEnvironmentHelp));
            _toolTip.SetToolTip(_runServerEnvironmentLabel, SR.GetString(SR.WebLauncherRunServerEnvironmentHelp));

            _toolTip.SetToolTip(_debugServerTarget, SR.GetString(SR.WebLauncherDebugServerTargetHelp));
            _toolTip.SetToolTip(_debugServerTargetLabel, SR.GetString(SR.WebLauncherDebugServerTargetHelp));

            _toolTip.SetToolTip(_debugServerTargetType, SR.GetString(SR.WebLauncherDebugServerTargetTypeHelp));

            _toolTip.SetToolTip(_debugServerArguments, SR.GetString(SR.WebLauncherDebugServerArgumentsHelp));
            _toolTip.SetToolTip(_debugServerArgumentsLabel, SR.GetString(SR.WebLauncherDebugServerArgumentsHelp));

            _toolTip.SetToolTip(_debugServerEnvironment, SR.GetString(SR.WebLauncherDebugServerEnvironmentHelp));
            _toolTip.SetToolTip(_debugServerEnvironmentLabel, SR.GetString(SR.WebLauncherDebugServerEnvironmentHelp));
        }

        public PythonWebLauncherOptions(IPythonProject properties)
            : this() {
            _properties = properties;
        }

        #region ILauncherOptions Members

        public void SaveSettings() {
            foreach (var propTextBox in _textBoxMap) {
                _properties.SetProperty(propTextBox.Key, propTextBox.Value.Text);
            }
            foreach (var propComboBox in _comboBoxMap) {
                var value = propComboBox.Value.SelectedItem as string;
                if (value != null) {
                    _properties.SetProperty(propComboBox.Key, value);
                }
            }
            RaiseIsSaved();
        }

        public void LoadSettings() {
            _loadingSettings = true;
            foreach (var propTextBox in _textBoxMap) {
                propTextBox.Value.Text = _properties.GetUnevaluatedProperty(propTextBox.Key);
            }
            foreach (var propComboBox in _comboBoxMap) {
                int index = propComboBox.Value.FindString(_properties.GetUnevaluatedProperty(propComboBox.Key));
                propComboBox.Value.SelectedIndex = index >= 0 ? index : 0;
            }
            _loadingSettings = false;
        }

        public void ReloadSetting(string settingName) {
            TextBox textBox;
            ComboBox comboBox;
            if (_textBoxMap.TryGetValue(settingName, out textBox)) {
                textBox.Text = _properties.GetUnevaluatedProperty(settingName);
            } else if (_comboBoxMap.TryGetValue(settingName, out comboBox)) {
                int index = comboBox.FindString(_properties.GetUnevaluatedProperty(settingName));
                comboBox.SelectedIndex = index >= 0 ? index : 0;
            }
        }

        public event EventHandler<DirtyChangedEventArgs> DirtyChanged;

        Control IPythonLauncherOptions.Control {
            get { return this; }
        }

        #endregion

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

        private void Setting_SelectedValueChanged(object sender, EventArgs e) {
            if (!_loadingSettings) {
                var isDirty = DirtyChanged;
                if (isDirty != null) {
                    DirtyChanged(this, DirtyChangedEventArgs.DirtyValue);
                }
            }
        }
    }
}

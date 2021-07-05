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

namespace Microsoft.PythonTools.Project.Web {
    public partial class PythonWebLauncherOptions : UserControl, IPythonLauncherOptions {
        private readonly Dictionary<string, TextBox> _textBoxMap;
        private readonly Dictionary<string, TextBox> _textBoxWebMap;
        private readonly Dictionary<string, ComboBox> _comboBoxMap;
        private readonly HashSet<string> _multilineProps;
        private readonly IPythonProject _properties;
        private bool _loadingSettings;
        public const string DjangoSettingsModuleProperty = "DjangoSettingsModule";

        public PythonWebLauncherOptions() {
            InitializeComponent();

            _textBoxMap = new Dictionary<string, TextBox> {
                { PythonConstants.SearchPathSetting, _searchPaths },
                { PythonConstants.CommandLineArgumentsSetting, _arguments },
                { PythonConstants.InterpreterPathSetting, _interpreterPath },
                { PythonConstants.InterpreterArgumentsSetting, _interpArgs },
                { PythonConstants.WebBrowserUrlSetting, _launchUrl },
                { PythonConstants.WebBrowserPortSetting, _portNumber },
                { PythonConstants.EnvironmentSetting, _environment },
            };

            _textBoxWebMap = new Dictionary<string, TextBox> {
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

            _multilineProps = new HashSet<string> {
                PythonConstants.EnvironmentSetting,
                PythonWebLauncher.RunWebServerEnvironmentProperty,
                PythonWebLauncher.DebugWebServerEnvironmentProperty
            };
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
            foreach (var propTextBox in _textBoxWebMap) {
                _properties.SetOrAddPropertyAfter(propTextBox.Key, propTextBox.Value.Text, DjangoSettingsModuleProperty);
            }
            foreach (var propComboBox in _comboBoxMap) {
                var value = propComboBox.Value.SelectedItem as string;
                if (value != null) {
                    _properties.SetOrAddPropertyAfter(propComboBox.Key, value, DjangoSettingsModuleProperty);
                }
            }
            RaiseIsSaved();
        }

        public void LoadSettings() {
            _loadingSettings = true;
            var textBoxMaps = _textBoxMap.Union(_textBoxWebMap);
            foreach (var propTextBox in textBoxMaps) {
                propTextBox.Value.Text = GetUnevaluatedProperty(propTextBox.Key);
            }
            foreach (var propComboBox in _comboBoxMap) {
                int index = propComboBox.Value.FindString(_properties.GetUnevaluatedProperty(propComboBox.Key));
                propComboBox.Value.SelectedIndex = index >= 0 ? index : 0;
            }
            _loadingSettings = false;
        }

        public void ReloadSetting(string settingName) {
            var textBoxMaps = _textBoxMap.Union(_textBoxWebMap);
            if (_textBoxMap.TryGetValue(settingName, out TextBox textBox)) {
                textBox.Text = GetUnevaluatedProperty(settingName);
            } else if (_textBoxWebMap.TryGetValue(settingName, out textBox)) {
                textBox.Text = GetUnevaluatedProperty(settingName);
            } else if (_comboBoxMap.TryGetValue(settingName, out ComboBox comboBox)) {
                int index = comboBox.FindString(_properties.GetUnevaluatedProperty(settingName));
                comboBox.SelectedIndex = index >= 0 ? index : 0;
            }
        }

        public event EventHandler<DirtyChangedEventArgs> DirtyChanged;

        Control IPythonLauncherOptions.Control {
            get { return this; }
        }

        #endregion

        private string GetUnevaluatedProperty(string settingName) {
            string value = _properties.GetUnevaluatedProperty(settingName);
            if (_multilineProps.Contains(settingName)) {
                value = FixLineEndings(value);
            }
            return value;
        }

        private static Regex lfToCrLfRegex = new Regex(@"(?<!\r)\n");

        private static string FixLineEndings(string value) {
            // TextBox requires \r\n for line separators, but XML can have either \n or \r\n, and we should treat those equally.
            // (It will always have \r\n when we write it out, but users can edit it by other means.)
            return lfToCrLfRegex.Replace(value ?? String.Empty, "\r\n");
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

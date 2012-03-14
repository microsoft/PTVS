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
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Options {
    public partial class PythonInterpreterOptionsControl : UserControl {
        private bool _loadingOptions;
        private ToolTip _invalidVersionToolTip = new ToolTip();
        private ToolTip _invalidPathToolTip = new ToolTip();
        private ToolTip _invalidWindowsPathToolTip = new ToolTip();

        public PythonInterpreterOptionsControl() {
            InitializeComponent();
            InitInterpreters();
        }

        internal void InitInterpreters() {
            _showSettingsFor.Items.Clear();
            _defaultInterpreter.Items.Clear();

            foreach (var interpreter in OptionsPage._options) {
                var display = interpreter.Display;
                int index = _defaultInterpreter.Items.Add(display);
                _showSettingsFor.Items.Add(display);

                if (IsDefaultInterpreter(interpreter)) {
                    _defaultInterpreter.SelectedIndex = index;
                    _showSettingsFor.SelectedIndex = index;
                }
            }

            if (_showSettingsFor.Items.Count > 0 && _showSettingsFor.SelectedIndex == -1) {
                _showSettingsFor.SelectedIndex = 0;
            }

            LoadNewOptions();
        }

        internal static bool IsDefaultInterpreter(InterpreterOptions interpreter) {
            Version vers;
            return interpreter.Id == PythonToolsPackage.Instance.InterpreterOptionsPage.DefaultInterpreterValue &&
                    Version.TryParse(interpreter.Version, out vers) &&
                    vers == PythonToolsPackage.Instance.InterpreterOptionsPage.DefaultInterpreterVersionValue;
        }

        private void LoadNewOptions() {
            var curOptions = CurrentOptions;

            if (curOptions != null) {
                _defaultInterpreter.Enabled = true;
                _showSettingsFor.Enabled = true;

                if (curOptions.IsConfigurable) {
                    _removeInterpreter.Enabled = _path.Enabled = _version.Enabled = _arch.Enabled = _windowsPath.Enabled = _pathEnvVar.Enabled = true;
                } else {
                    _removeInterpreter.Enabled = _path.Enabled = _version.Enabled = _arch.Enabled = _windowsPath.Enabled = _pathEnvVar.Enabled = false;
                }

                _loadingOptions = true;
                try {
                    _path.Text = curOptions.InterpreterPath;
                    _windowsPath.Text = curOptions.WindowsInterpreterPath;
                    _arch.SelectedIndex = _arch.Items.Count - 1;
                    for (int i = 0; i < _arch.Items.Count; i++) {
                        if (String.Equals((string)_arch.Items[i], curOptions.Architecture, StringComparison.OrdinalIgnoreCase)) {
                            _arch.SelectedIndex = i;
                            break;
                        }
                    }

                    _version.Text = curOptions.Version;
                    _pathEnvVar.Text = curOptions.PathEnvironmentVariable;

                    _generateCompletionDb.Enabled = curOptions.SupportsCompletionDb;
                } finally {
                    _loadingOptions = false;
                }
            } else {
                InitializeWithNoInterpreters();
            }
        }

        private void InitializeWithNoInterpreters() {
            _loadingOptions = true;
            _removeInterpreter.Enabled = _path.Enabled = _version.Enabled = _arch.Enabled = _windowsPath.Enabled = _pathEnvVar.Enabled = false;
            _generateCompletionDb.Enabled = false;
            _showSettingsFor.Items.Add("No Python Interpreters Installed");
            _defaultInterpreter.Items.Add("No Python Interpreters Installed");
            _showSettingsFor.SelectedIndex = _defaultInterpreter.SelectedIndex = 0;
            _defaultInterpreter.Enabled = false;
            _showSettingsFor.Enabled = false;
            _browsePath.Enabled = false;
            _browseWindowsPath.Enabled = false;

            _path.Text = "";
            _windowsPath.Text = "";
            _version.Text = "";
            _pathEnvVar.Text = "";
            _arch.SelectedIndex = 0;
            _loadingOptions = false;
        }

        private void ShowSettingsForSelectedIndexChanged(object sender, EventArgs e) {
            LoadNewOptions();
        }

        private PythonInterpreterOptionsPage OptionsPage {
            get {
                return PythonToolsPackage.Instance.InterpreterOptionsPage;
            }
        }

        public int DefaultInterpreter {
            get {
                return _defaultInterpreter.SelectedIndex;
            }
        }

        private void PathTextChanged(object sender, EventArgs e) {
            if (!_loadingOptions) {
                if (_path.Text.IndexOfAny(Path.GetInvalidPathChars()) != -1) {
                    ShowErrorBalloon(_invalidPathToolTip, _pathLabel, _path, "The path contains invalid characters.");
                } else {
                    CurrentOptions.InterpreterPath = _path.Text;
                    HideErrorBalloon(_invalidPathToolTip, _pathLabel);
                }
            }
        }

        private void HideErrorBalloon(ToolTip toolTip, Label inputLabel) {
            toolTip.RemoveAll();
            toolTip.Hide(this);
            inputLabel.ForeColor = SystemColors.ControlText;
        }

        private void ShowErrorBalloon(ToolTip toolTip, Label inputLabel, Control inputControl, string message) {
            toolTip.ShowAlways = true;
            toolTip.IsBalloon = true;
            toolTip.ToolTipIcon = ToolTipIcon.None;
            toolTip.Show(message,
                this,
                new System.Drawing.Point(inputControl.Location.X + 10, inputControl.Location.Y + _interpreterSettingsGroup.Location.Y + inputControl.Height - 5),
                5000);
            inputLabel.ForeColor = Color.Red;
        }

        private void WindowsPathTextChanged(object sender, EventArgs e) {
            if (!_loadingOptions) {
                if (_windowsPath.Text.IndexOfAny(Path.GetInvalidPathChars()) != -1) {
                    ShowErrorBalloon(_invalidWindowsPathToolTip, _windowsPathLabel, _windowsPath, "The path contains invalid characters.");
                } else {
                    CurrentOptions.WindowsInterpreterPath = _windowsPath.Text;
                    HideErrorBalloon(_invalidWindowsPathToolTip, _windowsPathLabel);
                }
            }
        }

        private void ArchSelectedIndexChanged(object sender, EventArgs e) {
            if (!_loadingOptions) {
                CurrentOptions.Architecture = _arch.Text;
            }
        }

        private void PathEnvVarTextChanged(object sender, EventArgs e) {
            if (!_loadingOptions) {
                CurrentOptions.PathEnvironmentVariable = _pathEnvVar.Text;
            }
        }

        private void VersionTextChanged(object sender, EventArgs e) {
            if (!_loadingOptions) {
                Version vers;
                if (Version.TryParse(_version.Text, out vers)) {
                    CurrentOptions.Version = _version.Text;
                    HideErrorBalloon(_invalidVersionToolTip, _versionLabel);
                } else {
                    ShowErrorBalloon(_invalidVersionToolTip, _versionLabel, _version, "Version is an invalid format and will not be saved.\r\n\r\nValid formats are in the form of Major.Minor[.Build[.Revision]].");
                }
            }
        }

        private InterpreterOptions CurrentOptions {
            get {
                return GetOption(_showSettingsFor.SelectedIndex);
            }
        }

        internal InterpreterOptions GetOption(int index) {
            int curOption = 0;
            foreach (var option in OptionsPage._options) {
                if (!option.Removed) {
                    if (curOption == index) {
                        return option;
                    }

                    curOption++;
                }
            }
            return null;
        }

        private void AddInterpreterClick(object sender, EventArgs e) {
            var newInterp = new NewInterpreter();
            if (newInterp.ShowDialog() == DialogResult.OK) {
                if (!_showSettingsFor.Enabled) {
                    // previously we had no interpreters, re-enable the control
                    _showSettingsFor.Items.Clear();
                    _defaultInterpreter.Items.Clear();
                    _defaultInterpreter.Enabled = true;
                    _showSettingsFor.Enabled = true;
                }
                var newOptions = new InterpreterOptions() { Display = newInterp.InterpreterDescription, Added = true, IsConfigurable = true };

                // two indicies to track: the indicies in our drop down (the realIndex) and the indicies in our
                // options list which may still have uncommitted changes.
                int realIndex = 0, optionsIndex = OptionsPage._options.Count;
                for (int i = 0; i < OptionsPage._options.Count; i++) {
                    var curOption = OptionsPage._options[i];
                    if (String.Compare(newOptions.Display, curOption.Display) < 0) {
                        optionsIndex = i;
                        break;
                    }
                    if (!curOption.Removed) {
                        realIndex++;
                    }
                }                

                OptionsPage._options.Insert(
                    optionsIndex,
                    new InterpreterOptions() { Display = newInterp.InterpreterDescription, Added = true, IsConfigurable = true }
                );
                _showSettingsFor.Items.Insert(realIndex, newInterp.InterpreterDescription);
                _showSettingsFor.SelectedIndex = realIndex;
                _defaultInterpreter.Items.Insert(realIndex, newInterp.InterpreterDescription);
                if (_defaultInterpreter.SelectedIndex == -1) {
                    _defaultInterpreter.SelectedIndex = 0;
                }
            }
        }

        private void RemoveInterpreterClick(object sender, EventArgs e) {
            if (_showSettingsFor.SelectedIndex != -1) {
                var res = MessageBox.Show(String.Format("Do you want to remove the interpreter {0}?", _showSettingsFor.Text), "Remove Interpreter", MessageBoxButtons.YesNo);
                if (res == DialogResult.Yes) {
                    var curOption = CurrentOptions;
                    curOption.Removed = true;
                    int index = _showSettingsFor.SelectedIndex;
                    _showSettingsFor.Items.RemoveAt(index);
                    _defaultInterpreter.Items.RemoveAt(index);

                    if (_showSettingsFor.Items.Count == 0) {
                        // last interpreter removed...
                        InitializeWithNoInterpreters();
                    }

                    if (_defaultInterpreter.SelectedIndex == -1) {
                        _defaultInterpreter.SelectedIndex = 0;
                    }
                    _showSettingsFor.SelectedIndex = 0;
                    
                }
            }
        }

        private void GenerateCompletionDbClick(object sender, EventArgs e) {
            if (CurrentOptions.IsConfigurable && CurrentOptions.Factory == null) {
                // we need to create a dummy factory for kicking off the configuration.
                if (CurrentOptions.Id == Guid.Empty) {
                    CurrentOptions.Id = Guid.NewGuid();
                }
                Version vers;
                Version.TryParse(_version.Text, out vers);

                var factCreator = PythonToolsPackage.ComponentModel.GetService<IPythonConfigurableInterpreterFactoryProvider>();
                CurrentOptions.Factory = factCreator.CreateConfigurableInterpreterFactory(
                    CurrentOptions.Id,
                    _path.Text,
                    _windowsPath.Text,
                    _pathEnvVar.Text,
                    _showSettingsFor.Text,
                    ProcessorArchitecture.X86,
                    vers
                );
            }

            var curFactory = CurrentOptions.Factory;
            switch(new GenerateIntellisenseDbDialog(CurrentOptions, () => DatabaseGenerated(curFactory)).ShowDialog()) {
                case DialogResult.OK:
                    MessageBox.Show("Analysis is complete and now available.", "Python Tools for Visual Studio");
                    break;
                case DialogResult.Ignore:
                    MessageBox.Show("Analysis is proceeding in the background, it will become available when completed.", "Python Tools for Visual Studio");
                    break;
            }
        }

        private void DatabaseGenerated(IPythonInterpreterFactory curFactory) {
            // default analyzer
            if (PythonToolsPackage.Instance.DefaultAnalyzer.InterpreterFactory == curFactory) {
                PythonToolsPackage.Instance.RecreateAnalyzer();
            }

            // all open projects
            foreach (EnvDTE.Project project in PythonToolsPackage.Instance.DTE.Solution.Projects) {
                var pyProj = project.GetPythonProject();
                if (pyProj != null) {
                    var analyzer = pyProj.GetAnalyzer();
                    if (analyzer != null && analyzer.InterpreterFactory == curFactory) {
                        pyProj.ClearInterpreter();
                    }
                }
            }
        }

        private void BrowsePathClick(object sender, EventArgs e) {
            var dialog = new OpenFileDialog();
            dialog.CheckFileExists = true;
            if (dialog.ShowDialog() == DialogResult.OK) {
                _path.Text = dialog.FileName;
            }
        }

        private void BrowseWindowsPathClick(object sender, EventArgs e) {
            var dialog = new OpenFileDialog();
            dialog.CheckFileExists = true;
            if (dialog.ShowDialog() == DialogResult.OK) {
                _windowsPath.Text = dialog.FileName;
            }
        }
    }

}

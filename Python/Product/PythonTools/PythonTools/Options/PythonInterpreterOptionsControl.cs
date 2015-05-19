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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Options {
    public partial class PythonInterpreterOptionsControl : UserControl {
        private IInterpreterOptionsService _service;
        private bool _loadingOptions;
        private ToolTip _invalidPathToolTip = new ToolTip();
        private ToolTip _invalidWindowsPathToolTip = new ToolTip();
        private ToolTip _invalidLibraryPathToolTip = new ToolTip();
        private readonly IServiceProvider _serviceProvider;

        [Obsolete("An IServiceProvider should be provided")]
        public PythonInterpreterOptionsControl()
            : this(PythonToolsPackage.Instance) {
        }

        public PythonInterpreterOptionsControl(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
            InitializeComponent();

            _service = serviceProvider.GetComponentModel().GetService<IInterpreterOptionsService>();
            UpdateInterpreters();
        }

        internal void UpdateInterpreters() {
            if (InvokeRequired) {
                Invoke((Action)(() => UpdateInterpreters()));
                return;
            }

            _addInterpreter.Enabled = _service.KnownProviders.OfType<ConfigurablePythonInterpreterFactoryProvider>().Any();

            _showSettingsFor.BeginUpdate();
            _defaultInterpreter.BeginUpdate();
            try {
                _showSettingsFor.Items.Clear();
                _defaultInterpreter.Items.Clear();

                var interpreters = _serviceProvider.GetPythonToolsService()
                    .InterpreterOptions
                    .Select(x => x.Key)
                    .Where(f => f.IsUIVisible() && f.CanBeConfigured())
                    .OrderBy(f => f.Description);

                foreach (var interpreter in interpreters) {
                    InterpreterOptions opts;
                    if (_serviceProvider.GetPythonToolsService().TryGetInterpreterOptions(interpreter, out opts) && !opts.Removed) {
                        _showSettingsFor.Items.Add(interpreter);
                        _defaultInterpreter.Items.Add(interpreter);
                    }
                }

                if (_showSettingsFor.Items.Count > 0 && _service.DefaultInterpreter != _service.NoInterpretersValue) {
                    _showSettingsFor.SelectedItem = _defaultInterpreter.SelectedItem = _service.DefaultInterpreter;
                }

                if (_defaultInterpreter.SelectedItem == null && _defaultInterpreter.Items.Count > 0) {
                    _defaultInterpreter.SelectedIndex = _defaultInterpreter.Items.Count - 1;
                }
                if (_showSettingsFor.SelectedItem == null && _showSettingsFor.Items.Count > 0) {
                    _showSettingsFor.SelectedItem = _defaultInterpreter.SelectedItem;
                    if (_showSettingsFor.SelectedItem == null) {
                        _showSettingsFor.SelectedIndex = 0;
                    }
                }
            } finally {
                _showSettingsFor.EndUpdate();
                _defaultInterpreter.EndUpdate();
            }

            LoadNewOptions();
        }

        protected override void OnVisibleChanged(EventArgs e) {
            base.OnVisibleChanged(e);

            if (Visible) {

                var selection = PythonInterpreterOptionsPage.NextOptionsSelection ?? _service.DefaultInterpreter;
                PythonInterpreterOptionsPage.NextOptionsSelection = null;
                _showSettingsFor.SelectedItem = selection;
                _defaultInterpreter.SelectedItem = _service.DefaultInterpreter;

                if (_showSettingsFor.SelectedItem == null && _showSettingsFor.Items.Count > 0) {
                    _showSettingsFor.SelectedIndex = 0;
                }
                if (_defaultInterpreter.SelectedItem == null && _defaultInterpreter.Items.Count > 0) {
                    _defaultInterpreter.SelectedIndex = 0;
                }
            }
        }

        private void LoadNewOptions() {
            var curOptions = CurrentOptions;

            if (curOptions != null) {
                _defaultInterpreter.Enabled = true;
                _showSettingsFor.Enabled = true;
                _interpreterSettingsGroup.Enabled = curOptions.IsConfigurable;

                _loadingOptions = true;
                try {
                    _path.Text = curOptions.InterpreterPath;
                    _windowsPath.Text = curOptions.WindowsInterpreterPath;
                    _libraryPath.Text = curOptions.LibraryPath;
                    _arch.SelectedIndex = _arch.Items.Count - 1;
                    for (int i = 0; i < _arch.Items.Count; i++) {
                        if (String.Equals((string)_arch.Items[i], curOptions.Architecture, StringComparison.OrdinalIgnoreCase)) {
                            _arch.SelectedIndex = i;
                            break;
                        }
                    }

                    _version.SelectedIndex = _version.FindStringExact(curOptions.Version);
                    _pathEnvVar.Text = curOptions.PathEnvironmentVariable;
                } finally {
                    _loadingOptions = false;
                }
            } else {
                InitializeWithNoInterpreters();
            }
        }

        private void InitializeWithNoInterpreters() {
            _loadingOptions = true;
            _showSettingsFor.Items.Add("No Python Environments Installed");
            _defaultInterpreter.Items.Add("No Python Environments Installed");
            _showSettingsFor.SelectedIndex = _defaultInterpreter.SelectedIndex = 0;
            _defaultInterpreter.Enabled = false;
            _showSettingsFor.Enabled = false;
            _interpreterSettingsGroup.Enabled = false;

            _path.Text = "";
            _windowsPath.Text = "";
            _libraryPath.Text = "";
            _version.SelectedIndex = _version.FindStringExact("2.7");
            _pathEnvVar.Text = "";
            _arch.SelectedIndex = 0;
            _loadingOptions = false;
        }

        private void ShowSettingsForSelectedIndexChanged(object sender, EventArgs e) {
            LoadNewOptions();
        }

        public IPythonInterpreterFactory DefaultInterpreter {
            get {
                return _defaultInterpreter.SelectedItem as IPythonInterpreterFactory;
            }
        }

        private void PathTextChanged(object sender, EventArgs e) {
            if (!_loadingOptions) {
                if (string.IsNullOrEmpty(_path.Text)) {
                    CurrentOptions.InterpreterPath = string.Empty;
                    HideErrorBalloon(_invalidPathToolTip, _pathLabel);
                } else if (!CommonUtils.IsValidPath(_path.Text)) {
                    ShowErrorBalloon(_invalidPathToolTip, _pathLabel, _path, "The path contains invalid characters.");
                } else {
                    CurrentOptions.InterpreterPath = _path.Text;
                    if (_arch.SelectedIndex == -1 || (string)_arch.SelectedItem == "Unknown") {
                        try {
                            switch (Microsoft.PythonTools.Interpreter.NativeMethods.GetBinaryType(_path.Text)) {
                                case ProcessorArchitecture.X86:
                                    _arch.SelectedIndex = _arch.FindStringExact("x86");
                                    break;
                                case ProcessorArchitecture.Amd64:
                                    _arch.SelectedIndex = _arch.FindStringExact("x64");
                                    break;
                                default:
                                    _arch.SelectedIndex = _arch.FindStringExact("Unknown");
                                    break;
                            }
                        } catch (ArgumentOutOfRangeException) {
                            // Just a best attempt - if things fail for whatever
                            // reason, it's not even worth informing the user.
                        }
                    }
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
                if (!string.IsNullOrEmpty(_windowsPath.Text) && !CommonUtils.IsValidPath(_windowsPath.Text)) {
                    ShowErrorBalloon(_invalidWindowsPathToolTip, _windowsPathLabel, _windowsPath, "The path contains invalid characters.");
                } else {
                    CurrentOptions.WindowsInterpreterPath = _windowsPath.Text;
                    HideErrorBalloon(_invalidWindowsPathToolTip, _windowsPathLabel);
                }
            }
        }

        private void LibraryPathTextChanged(object sender, EventArgs e) {
            if (!_loadingOptions) {
                if (!string.IsNullOrEmpty(_libraryPath.Text) && !CommonUtils.IsValidPath(_libraryPath.Text)) {
                    ShowErrorBalloon(_invalidLibraryPathToolTip, _libraryPathLabel, _libraryPath, "The path contains invalid characters.");
                } else {
                    CurrentOptions.LibraryPath = _libraryPath.Text;
                    HideErrorBalloon(_invalidLibraryPathToolTip, _libraryPathLabel);
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

        private void Version_SelectedIndexChanged(object sender, EventArgs e) {
            if (!_loadingOptions) {
                CurrentOptions.Version = _version.SelectedItem as string;
            }
        }

        private InterpreterOptions CurrentOptions {
            get {
                var fact = _showSettingsFor.SelectedItem as IPythonInterpreterFactory;
                return fact != null ? 
                    _serviceProvider.GetPythonToolsService().GetInterpreterOptions(fact) : null;
            }
        }

        private void AddInterpreterClick(object sender, EventArgs e) {
            var newInterp = new NewInterpreter();
            newInterp.Font = Font;
            if (newInterp.ShowDialog() == DialogResult.OK) {
                if (!_showSettingsFor.Enabled) {
                    // previously we had no interpreters, re-enable the control
                    _showSettingsFor.Items.Clear();
                    _defaultInterpreter.Items.Clear();
                    _defaultInterpreter.Enabled = true;
                    _showSettingsFor.Enabled = true;
                }
                var id = Guid.NewGuid();
                var factory = new InterpreterPlaceholder(id, newInterp.InterpreterDescription);
                var newOptions = new InterpreterOptions(_serviceProvider.GetPythonToolsService(), factory) {
                    Display = newInterp.InterpreterDescription,
                    Added = true,
                    IsConfigurable = true,
                    SupportsCompletionDb = true,
                    Id = id,
                    Factory = factory,
                    InteractiveOptions = new PythonInteractiveOptions(
                        _serviceProvider,
                        _serviceProvider.GetPythonToolsService(), 
                        "Interactive Windows", 
                        ""
                    )
                };

                _serviceProvider.GetPythonToolsService().AddInterpreterOptions(newOptions.Factory, newOptions, true);
                _showSettingsFor.BeginUpdate();
                UpdateInterpreters();
                _showSettingsFor.SelectedItem = newOptions.Factory;
                _showSettingsFor.EndUpdate();
            }
        }

        private void RemoveInterpreterClick(object sender, EventArgs e) {
            if (_showSettingsFor.SelectedIndex != -1) {
                var res = MessageBox.Show(String.Format("Do you want to remove the interpreter {0}?", _showSettingsFor.Text), "Remove Interpreter", MessageBoxButtons.YesNo);
                if (res == DialogResult.Yes) {
                    var curOption = CurrentOptions;
                    if (curOption != null) {
                        curOption.Removed = true;
                        UpdateInterpreters();
                    }
                }
            }
        }

        private void BrowsePathClick(object sender, EventArgs e) {
            var dialog = new OpenFileDialog();
            dialog.CheckFileExists = true;
            dialog.Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*";
            if (dialog.ShowDialog() == DialogResult.OK) {
                _path.Text = dialog.FileName;
            }
        }

        private void BrowseWindowsPathClick(object sender, EventArgs e) {
            var dialog = new OpenFileDialog();
            dialog.CheckFileExists = true;
            dialog.Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*";
            if (dialog.ShowDialog() == DialogResult.OK) {
                _windowsPath.Text = dialog.FileName;
            }
        }

        private void BrowseLibraryPathClick(object sender, EventArgs e) {
            string dir = _libraryPath.Text;
            if (string.IsNullOrEmpty(dir)) {
                if (File.Exists(_path.Text)) {
                    dir = Path.GetDirectoryName(_path.Text);
                }
            }

            var libPath = _serviceProvider.BrowseForDirectory(Handle, dir);
            if (!string.IsNullOrEmpty(libPath)) {
                _libraryPath.Text = libPath;
            }
        }

        private void Interpreter_Format(object sender, ListControlConvertEventArgs e) {
            var factory = e.ListItem as IPythonInterpreterFactory;
            if (factory != null) {
                e.Value = factory.Description;
            } else {
                e.Value = e.ListItem.ToString();
            }
        }
    }

}

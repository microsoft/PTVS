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
using System.Linq;
using System.Windows.Forms;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Project {
    public partial class PythonGeneralPropertyPageControl : ThemeAwareUserControl {
        static readonly IPythonInterpreterFactory Separator =
            new InterpreterPlaceholder("", Strings.PythonGeneralPropertyPageControl_OtherInterpretersSeparator);
        static readonly IPythonInterpreterFactory GlobalDefault =
            new InterpreterPlaceholder("", Strings.PythonGeneralPropertyPageControl_UseGlobalDefaultInterpreter);

        private IInterpreterRegistryService _service;
        private readonly PythonGeneralPropertyPage _propPage;

        internal PythonGeneralPropertyPageControl(PythonGeneralPropertyPage newPythonGeneralPropertyPage) {
            InitializeComponent();

            _propPage = newPythonGeneralPropertyPage;
            
            ApplyThemeColors();
        }

        internal void LoadSettings() {
            _service = _propPage.Project.Site.GetComponentModel().GetService<IInterpreterRegistryService>();

            StartupFile = _propPage.Project.GetProjectProperty(CommonConstants.StartupFile, false);
            WorkingDirectory = _propPage.Project.GetProjectProperty(CommonConstants.WorkingDirectory, false);
            if (string.IsNullOrEmpty(WorkingDirectory)) {
                WorkingDirectory = ".";
            }
            IsWindowsApplication = Convert.ToBoolean(_propPage.Project.GetProjectProperty(CommonConstants.IsWindowsApplication, false));
            OnInterpretersChanged();

            if (_propPage.PythonProject.IsActiveInterpreterGlobalDefault) {
                // ActiveInterpreter will never be null, so we need to check
                // the property to find out if it's following the global
                // default.
                SetDefaultInterpreter(null);
            } else {
                SetDefaultInterpreter(_propPage.PythonProject.ActiveInterpreter);
            }

        }

        private void InitializeInterpreters() {
            _defaultInterpreter.BeginUpdate();
            
            try {
                var selection = _defaultInterpreter.SelectedItem;
                _defaultInterpreter.Items.Clear();
                var available = _propPage.PythonProject.InterpreterFactories.ToArray();
                var globalInterpreters = _service.Interpreters.Where(f => f.IsUIVisible() && f.CanBeDefault()).ToList();
                if (available != null && available.Length > 0) {
                    foreach (var interpreter in available) {
                        _defaultInterpreter.Items.Add(interpreter);
                        globalInterpreters.Remove(interpreter);
                    }

                    if (globalInterpreters.Any()) {
                        _defaultInterpreter.Items.Add(Separator);
                        foreach (var interpreter in globalInterpreters) {
                            _defaultInterpreter.Items.Add(interpreter);
                        }
                    }
                } else {
                    _defaultInterpreter.Items.Add(GlobalDefault);

                    foreach (var interpreter in globalInterpreters) {
                        _defaultInterpreter.Items.Add(interpreter);
                    }
                }

                if (_propPage.PythonProject.IsActiveInterpreterGlobalDefault) {
                    // ActiveInterpreter will never be null, so we need to check
                    // the property to find out if it's following the global
                    // default.
                    SetDefaultInterpreter(null);
                } else if (selection != null) {
                    SetDefaultInterpreter((IPythonInterpreterFactory)selection);
                } else { 
                    SetDefaultInterpreter(_propPage.PythonProject.ActiveInterpreter);
                }
            } finally {
                _defaultInterpreter.EndUpdate();
            }
        }

        internal void OnInterpretersChanged() {
            _defaultInterpreter.SelectedIndexChanged -= Changed;
            
            InitializeInterpreters();

            _defaultInterpreter.SelectedIndexChanged += Changed;
        }

        public string StartupFile {
            get { return _startupFile.Text; }
            set { _startupFile.Text = value; }
        }

        public string WorkingDirectory {
            get { return _workingDirectory.Text; }
            set { _workingDirectory.Text = value; }
        }

        public bool IsWindowsApplication {
            get { return _windowsApplication.Checked; }
            set { _windowsApplication.Checked = value; }
        }

        public IPythonInterpreterFactory DefaultInterpreter {
            get {
                if (_defaultInterpreter.SelectedItem == GlobalDefault) {
                    return null;
                }
                return _defaultInterpreter.SelectedItem as IPythonInterpreterFactory;
            }
        }

        public void SetDefaultInterpreter(IPythonInterpreterFactory interpreter) {
            if (interpreter == null) {
                _defaultInterpreter.SelectedIndex = 0;
            } else {
                try {
                    _defaultInterpreter.SelectedItem = interpreter;
                } catch (IndexOutOfRangeException) {
                    _defaultInterpreter.SelectedIndex = 0;
                }
            }
        }

        private void Changed(object sender, EventArgs e) {
            if (_defaultInterpreter.SelectedItem == Separator) {
                _defaultInterpreter.SelectedItem = _propPage.PythonProject.ActiveInterpreter;
            }
            _propPage.IsDirty = true;
        }

        private void Interpreter_Format(object sender, ListControlConvertEventArgs e) {
            var factory = e.ListItem as IPythonInterpreterFactory;

            e.Value = factory?.Configuration?.Description ?? e.ListItem?.ToString() ?? "-";
        }
    }
}

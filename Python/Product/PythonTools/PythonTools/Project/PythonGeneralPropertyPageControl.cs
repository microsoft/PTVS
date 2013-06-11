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
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Options;
using Microsoft.VisualStudio.ComponentModelHost;

namespace Microsoft.PythonTools.Project {
    public partial class PythonGeneralPropertyPageControl : UserControl {
        private IInterpreterOptionsService _service;
        private readonly PythonGeneralPropertyPage _propPage;

        public PythonGeneralPropertyPageControl() {
            InitializeComponent();

            _service = PythonToolsPackage.ComponentModel.GetService<IInterpreterOptionsService>();
            InitializeInterpreters();
            _service.InterpretersChanged += InterpreterOptionsPage_InterpretersChanged;
        }

        private void InitializeInterpreters() {
            _defaultInterpreter.Items.Add(new InterpreterPlaceholder(Guid.Empty, "Use global default"));
            foreach (var interpreter in _service.Interpreters) {
                _defaultInterpreter.Items.Add(interpreter);
            }
        }

        private void InterpreterOptionsPage_InterpretersChanged(object sender, EventArgs e) {
            _defaultInterpreter.SelectedIndexChanged -= Changed;

            _defaultInterpreter.Items.Clear();
            InitializeInterpreters();

            SetDefaultInterpreter(_propPage.PythonProject.Interpreters.ActiveInterpreter);

            _defaultInterpreter.SelectedIndexChanged += Changed;
        }

        protected override void OnHandleDestroyed(EventArgs e) {
            base.OnHandleDestroyed(e);
            _service.InterpretersChanged -= InterpreterOptionsPage_InterpretersChanged;
        }

        internal PythonGeneralPropertyPageControl(PythonGeneralPropertyPage newPythonGeneralPropertyPage)
            : this() {
            _propPage = newPythonGeneralPropertyPage;
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
                if (_defaultInterpreter.SelectedIndex == 0) {
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
            _propPage.IsDirty = true;
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

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
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    [Guid(PythonConstants.GeneralPropertyPageGuid)]
    public class PythonGeneralPropertyPage : CommonPropertyPage {
        private readonly PythonGeneralPropertyPageControl _control;

        public PythonGeneralPropertyPage() {
            _control = new PythonGeneralPropertyPageControl(this);
        }

        public override Control Control {
            get {
                return _control;
            }
        }

        internal override CommonProjectNode Project {
            get {
                return base.Project;
            }
            set {
                if (base.Project != null) {
                    PythonProject.Interpreters.InterpreterFactoriesChanged -= OnInterpretersChanged;
                    PythonProject.Interpreters.ActiveInterpreterChanged -= OnInterpretersChanged;
                    base.Project.PropertyPage = null;
                }
                base.Project = value;
                if (value != null) {
                    PythonProject.Interpreters.InterpreterFactoriesChanged += OnInterpretersChanged;
                    PythonProject.Interpreters.ActiveInterpreterChanged += OnInterpretersChanged;
                    value.PropertyPage = this;
                }
            }
        }

        private void OnInterpretersChanged(object sender, EventArgs e) {
            if (_control.InvokeRequired) {
                _control.BeginInvoke((Action)_control.OnInterpretersChanged);
            } else {
                _control.OnInterpretersChanged();
            }
        }

        internal PythonProjectNode PythonProject {
            get {
                return (PythonProjectNode)Project;
            }
        }

        public override string Name {
            get { return "General"; }
        }

        public override void Apply() {
            Project.SetProjectProperty(CommonConstants.StartupFile, _control.StartupFile);
            Project.SetProjectProperty(CommonConstants.WorkingDirectory, _control.WorkingDirectory);
            Project.SetProjectProperty(CommonConstants.IsWindowsApplication, _control.IsWindowsApplication.ToString());
            
            var interp = _control.DefaultInterpreter;
            if (interp != null && !PythonProject.Interpreters.GetInterpreterFactories().Contains(interp)) {
                PythonProject.Interpreters.AddInterpreter(interp);
            }
            PythonProject.Interpreters.ActiveInterpreter = _control.DefaultInterpreter;
            IsDirty = false;
            LoadSettings();
        }

        public override void LoadSettings() {
            Loading = true;
            try {
                _control.StartupFile = this.Project.GetProjectProperty(CommonConstants.StartupFile, false);
                _control.WorkingDirectory = this.Project.GetProjectProperty(CommonConstants.WorkingDirectory, false);
                if (string.IsNullOrEmpty(_control.WorkingDirectory)) {
                    _control.WorkingDirectory = ".";
                }
                _control.IsWindowsApplication = Convert.ToBoolean(this.Project.GetProjectProperty(CommonConstants.IsWindowsApplication, false));
                _control.OnInterpretersChanged();

                if (PythonProject.Interpreters.IsActiveInterpreterGlobalDefault) {
                    // ActiveInterpreter will never be null, so we need to check
                    // the property to find out if it's following the global
                    // default.
                    _control.SetDefaultInterpreter(null);
                } else {
                    _control.SetDefaultInterpreter(PythonProject.Interpreters.ActiveInterpreter);
                }
            } finally {
                Loading = false;
            }
        }
    }
}

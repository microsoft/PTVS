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
                base.Project = value;
                if (value != null) {
                    value.PropertyPage = this;
                } else {
                    value.PropertyPage = null;
                }
            }
        }

        public override string Name {
            get { return "General"; }
        }

        public override void Apply() {
            Project.SetProjectProperty(CommonConstants.StartupFile, _control.StartupFile);
            Project.SetProjectProperty(CommonConstants.WorkingDirectory, _control.WorkingDirectory);
            Project.SetProjectProperty(CommonConstants.IsWindowsApplication, _control.IsWindowsApplication.ToString());
            if (_control.DefaultInterpreter != null) {
                if (_control.DefaultInterpreter.Id.ToString() != Project.GetProjectProperty(PythonConstants.InterpreterId, false) ||
                    _control.DefaultInterpreter.Configuration.Version.ToString() != Project.GetProjectProperty(PythonConstants.InterpreterVersion, false)) {
                    Project.SetProjectProperty(PythonConstants.InterpreterId, _control.DefaultInterpreter.Id.ToString());
                    Project.SetProjectProperty(PythonConstants.InterpreterVersion, _control.DefaultInterpreter.Configuration.Version.ToString());
                    ((PythonProjectNode)Project).ClearInterpreter();
                }
            }
            IsDirty = false;
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
                Guid guid;
                Version version;
                if (Guid.TryParse(this.Project.GetProjectProperty(PythonConstants.InterpreterId, false), out guid) &&
                    Version.TryParse(this.Project.GetProjectProperty(PythonConstants.InterpreterVersion, false), out version)) {
                    _control.SetDefaultInterpreter(guid, version);
                }
            } finally {
                Loading = false;
            }
        }
    }
}

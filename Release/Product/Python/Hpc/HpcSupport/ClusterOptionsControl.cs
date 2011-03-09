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
using Microsoft.PythonTools.Project;

namespace Microsoft.PythonTools.Hpc {
    public partial class ClusterOptionsControl : UserControl, IPythonLauncherOptions {
        private readonly ClusterOptions _options;
        private readonly IPythonProject _project;

        public ClusterOptionsControl() {
            InitializeComponent();

            _propGrid.PropertyValueChanged += PropGridPropertyValueChanged;
        }

        private void PropGridPropertyValueChanged(object s, PropertyValueChangedEventArgs e) {
            var dirtyChanged = DirtyChanged;
            if (dirtyChanged != null) {
                dirtyChanged(this, DirtyChangedEventArgs.DirtyValue);
            }
        }

        public ClusterOptionsControl(IPythonProject project) : this() {
            _project = project;
            _options = new ClusterOptions(project);
        }

        #region IPythonLauncherOptions Members

        public void SaveSettings() {
            _project.SetProperty(ClusterOptions.AppArgumentsSetting, _options.InterpreterArguments);
            _project.SetProperty(ClusterOptions.AppCommandSetting, _options.PythonInterpreter);
            _project.SetProperty(ClusterOptions.PublishBeforeRunSetting, _options.PublishBeforeRun.ToString());
            _project.SetProperty(ClusterOptions.RunEnvironmentSetting, _options.RunEnvironment.ToString());
            _project.SetProperty(ClusterOptions.WorkingDirSetting, _options.WorkingDir);
            _project.SetProperty(ClusterOptions.MpiExecPathSetting, _options.MpiExecPath);
            _project.SetProperty(ClusterOptions.DeploymentDirSetting, _options.DeploymentDirectory);
            _project.SetProperty(ClusterOptions.TargetPlatformSetting, _options.TargetPlatform.ToString());
        }

        public void LoadSettings() {
            _options.InterpreterArguments = _project.GetProperty(ClusterOptions.AppArgumentsSetting);
            _options.PythonInterpreter = _project.GetProperty(ClusterOptions.AppCommandSetting);
            _options.PublishBeforeRun = _project.PublishBeforeRun();
            _options.RunEnvironment = new ClusterEnvironment(_project.GetProperty(ClusterOptions.RunEnvironmentSetting));
            _options.WorkingDir = _project.GetProperty(ClusterOptions.WorkingDirSetting);
            _options.MpiExecPath = _project.GetProperty(ClusterOptions.MpiExecPathSetting);
            _options.DeploymentDirectory = _project.GetProperty(ClusterOptions.DeploymentDirSetting);
            _options.TargetPlatform = _project.TargetPlatform();

            _propGrid.SelectedObject = _options;
        }

        public event EventHandler<DirtyChangedEventArgs> DirtyChanged;

        public Control Control {
            get { return this; }
        }

        #endregion
    }
}

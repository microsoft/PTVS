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
using System.Windows.Forms;

namespace Microsoft.PythonTools.Project {
    partial class PythonDebugPropertyPageControl : UserControl {
        private readonly PythonDebugPropertyPage _propPage;
        private readonly List<LauncherInfo> _launchers = new List<LauncherInfo>();
        private readonly Dictionary<object, bool> _dirtyPages = new Dictionary<object, bool>();
        private readonly ToolTip _debuggerToolTip = new ToolTip();
        private bool _launcherSelectionDirty;
        private int _dirtyCount;
        private Control _curLauncher;

        public PythonDebugPropertyPageControl() {
            InitializeComponent();
        }

        internal PythonDebugPropertyPageControl(PythonDebugPropertyPage newPythonGeneralPropertyPage)
            : this() {
            _propPage = newPythonGeneralPropertyPage;
        }

        internal void LoadSettings() {
            var compModel = PythonToolsPackage.ComponentModel;
            var launchers = compModel.GetExtensions<IPythonLauncherProvider>();
            var launchProvider = _propPage.Project.GetProjectProperty(PythonConstants.LaunchProvider, false);
            if (String.IsNullOrWhiteSpace(launchProvider)) {
                launchProvider = DefaultLauncherProvider.DefaultLauncherDescription;
            }

            foreach (var launcher in launchers) {
                var launchInfo = new LauncherInfo((PythonProjectNode)_propPage.Project, launcher);
                launchInfo.LauncherOptions.DirtyChanged += LauncherOptionsDirtyChanged;
                _launchers.Add(launchInfo);
                this._launchModeCombo.Items.Add(launcher.Name);

                if (launcher.Name == launchProvider) {
                    _launchModeCombo.SelectedIndexChanged -= LaunchModeComboSelectedIndexChanged;
                    _launchModeCombo.SelectedIndex = _launchModeCombo.Items.Count - 1;
                    _launchModeCombo.SelectedIndexChanged += LaunchModeComboSelectedIndexChanged;
                    _debuggerToolTip.SetToolTip(_launchModeCombo, launcher.Description);

                    SwitchLauncher(launchInfo);
                }
            }
        }

        public string CurrentLauncher {
            get {
                return _launchModeCombo.Items[_launchModeCombo.SelectedIndex].ToString();
            }
        }

        private void LauncherOptionsDirtyChanged(object sender, DirtyChangedEventArgs e) {
            bool wasDirty;
            if (!_dirtyPages.TryGetValue(sender, out wasDirty)) {
                _dirtyPages[sender] = e.IsDirty;

                if (e.IsDirty) {
                    _dirtyCount++;
                }
            } else if (wasDirty != e.IsDirty) {
                if (e.IsDirty) {
                    _dirtyCount++;
                } else {
                    _dirtyCount--;
                }
            }

            _propPage.IsDirty = _dirtyCount != 0 || _launcherSelectionDirty;
        }

        public void SaveSettings() {
            _launchers[_launchModeCombo.SelectedIndex].LauncherOptions.SaveSettings();
        }

        class LauncherInfo {
            public readonly Control OptionsControl;
            public readonly IPythonLauncherProvider Launcher;
            public readonly IPythonLauncherOptions LauncherOptions;

            public LauncherInfo(PythonProjectNode project, IPythonLauncherProvider launcher) {
                Launcher = launcher;
                LauncherOptions = launcher.GetLauncherOptions(project);
                OptionsControl = LauncherOptions.Control;
                LauncherOptions.LoadSettings();
            }
        }

        private void SwitchLauncher(LauncherInfo info) {
            var newLauncher = info.OptionsControl;
            info.LauncherOptions.LoadSettings();

            if (_curLauncher != null) {
                this.Controls.Remove(_curLauncher);
            }

            Controls.Add(newLauncher);
            newLauncher.Location = new Point(11, 48);
            _curLauncher = newLauncher;
            _debuggerToolTip.SetToolTip(_launchModeCombo, info.Launcher.Description);
        }

        private void LaunchModeComboSelectedIndexChanged(object sender, EventArgs e) {
            _launcherSelectionDirty = true;
            _propPage.IsDirty = true;
            SwitchLauncher(_launchers[_launchModeCombo.SelectedIndex]);
        }

    }
}

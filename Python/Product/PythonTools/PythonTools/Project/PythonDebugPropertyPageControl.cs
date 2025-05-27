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
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    partial class PythonDebugPropertyPageControl : ThemeAwareUserControl {
        private readonly PythonDebugPropertyPage _propPage;
        private readonly Dictionary<object, bool> _dirtyPages = new Dictionary<object, bool>();
        private readonly ToolTip _debuggerToolTip = new ToolTip();
        private bool _launcherSelectionDirty;
        private int _dirtyCount;
        private Control _curLauncher;

        // Add a field to track dropdown state
        private bool _isDropDownOpen;
        private LauncherInfo _pendingSelection;

        internal PythonDebugPropertyPageControl(PythonDebugPropertyPage newPythonGeneralPropertyPage)
        {
            InitializeComponent();

            _propPage = newPythonGeneralPropertyPage;

            // Add these new event handlers
            _launchModeCombo.DropDown += LaunchModeCombo_DropDown;
            _launchModeCombo.DropDownClosed += LaunchModeCombo_DropDownClosed;
            
            // Apply the VS theme to this control
            ApplyThemeColors();
        }

        internal void LoadSettings() {
            var compModel = _propPage.Project.Site.GetComponentModel();
            var launchProvider = _propPage.Project.GetProjectProperty(PythonConstants.LaunchProvider, false);
            if (String.IsNullOrWhiteSpace(launchProvider)) {
                launchProvider = DefaultLauncherProvider.DefaultLauncherName;
            }

            _launchModeCombo.SelectedIndexChanged -= LaunchModeComboSelectedIndexChanged;

            LauncherInfo currentInfo = null;
            var projectNode = (PythonProjectNode)_propPage.Project;
            foreach (var info in compModel.GetExtensions<IPythonLauncherProvider>()
                .Select(i => new LauncherInfo(projectNode, i))
                .OrderBy(i => i.SortKey)) {

                info.LauncherOptions.DirtyChanged += LauncherOptionsDirtyChanged;
                _launchModeCombo.Items.Add(info);
                if (info.Launcher.Name == launchProvider) {
                    currentInfo = info;
                }
            }

            if (currentInfo != null) {
                _launchModeCombo.SelectedItem = currentInfo;
                SwitchLauncher(currentInfo);
            } else {
                _launchModeCombo.SelectedIndex = -1;
                SwitchLauncher(null);
            }

            _launchModeCombo.SelectedIndexChanged += LaunchModeComboSelectedIndexChanged;
            
            // Apply theme after all controls are loaded
            ApplyThemeColors();
        }

        public string CurrentLauncher {
            get {
                return ((LauncherInfo)_launchModeCombo.SelectedItem).Launcher.Name;
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
                    _dirtyPages.Remove(sender);
                }
            }

            _propPage.IsDirty = _dirtyCount != 0 || _launcherSelectionDirty;
        }

        public void SaveSettings() {
            var launcher = (LauncherInfo)_launchModeCombo.SelectedItem;
            launcher.LauncherOptions.SaveSettings();
        }

        public void ReloadSetting(string settingName) {
            var launcher = (LauncherInfo)_launchModeCombo.SelectedItem;
            launcher.LauncherOptions.ReloadSetting(settingName);
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

            public string DisplayName => Launcher.LocalizedName;

            public string SortKey => $"{Launcher.SortPriority:D011};{Launcher.LocalizedName}";
        }

        private void SwitchLauncher(LauncherInfo info) {
            if (_curLauncher != null) {
                tableLayout.Controls.Remove(_curLauncher);
            }

            if (info == null) {
                _curLauncher = null;
                _debuggerToolTip.SetToolTip(_launchModeCombo, null);
                return;
            }

            var newLauncher = info.OptionsControl;
            info.LauncherOptions.LoadSettings();
            tableLayout.Controls.Add(newLauncher);
            tableLayout.SetCellPosition(newLauncher, new TableLayoutPanelCellPosition(0, 1));
            tableLayout.SetColumnSpan(newLauncher, 2);
            newLauncher.Dock = DockStyle.Fill;
            _curLauncher = newLauncher;
            _debuggerToolTip.SetToolTip(_launchModeCombo, info.Launcher.Description);
            
            // Apply theme to the newly added launcher control
            ApplyThemeColors();
        }

        private void LaunchModeComboSelectedIndexChanged(object sender, EventArgs e) {
            _launcherSelectionDirty = true;
            _propPage.IsDirty = true;

            // If dropdown is open, store selection but don't switch yet
            if (_isDropDownOpen) {
                _pendingSelection = (LauncherInfo)_launchModeCombo.SelectedItem;
            } else {
                // For mouse clicks or direct selection, switch immediately
                SwitchLauncher((LauncherInfo)_launchModeCombo.SelectedItem);
            }
        }

        private void _launchModeCombo_Format(object sender, ListControlConvertEventArgs e) {
            var launcher = (LauncherInfo)e.ListItem;
            e.Value = launcher.DisplayName;
        }

        // Add these new methods
        private void LaunchModeCombo_DropDown(object sender, EventArgs e) {
            _isDropDownOpen = true;
            _pendingSelection = null;
        }

        private void LaunchModeCombo_DropDownClosed(object sender, EventArgs e) {
            _isDropDownOpen = false;

            // Apply the pending selection if there is one
            if (_pendingSelection != null) {
                SwitchLauncher(_pendingSelection);
                _pendingSelection = null;
            }
        }
    }
}

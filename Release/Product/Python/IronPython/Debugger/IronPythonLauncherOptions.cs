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
using Microsoft.PythonTools;
using Microsoft.PythonTools.Project;

namespace Microsoft.IronPythonTools.Debugger {
    public partial class IronPythonLauncherOptions : UserControl, IPythonLauncherOptions {
        private readonly IPythonProject _properties;
        private bool _loadingSettings;
        public const string DebugStandardLibrarySetting = "DebugStdLib";
        public IronPythonLauncherOptions() {
            InitializeComponent();
        }

        public IronPythonLauncherOptions(IPythonProject properties) : this() {
            _properties = properties;
        }

        #region ILauncherOptions Members

        public void SaveSettings() {
            _properties.SetProperty(CommonConstants.SearchPath, SearchPaths);
            _properties.SetProperty(CommonConstants.CommandLineArguments, Arguments);
            _properties.SetProperty(CommonConstants.InterpreterPath, InterpreterPath);
            _properties.SetProperty(DebugStandardLibrarySetting, DebugStandardLibrary.ToString());
            RaiseIsSaved();
        }

        public void LoadSettings() {
            _loadingSettings = true;
            SearchPaths = _properties.GetProperty(CommonConstants.SearchPath);
            InterpreterPath = _properties.GetProperty(CommonConstants.InterpreterPath);
            Arguments = _properties.GetProperty(CommonConstants.CommandLineArguments);
            DebugStandardLibrary = Convert.ToBoolean(_properties.GetProperty(DebugStandardLibrarySetting));
            _loadingSettings = false;
        }

        public event EventHandler<DirtyChangedEventArgs> DirtyChanged;

        Control IPythonLauncherOptions.Control {
            get { return this; }
        }

        #endregion

        public string SearchPaths {
            get { return _searchPaths.Text; }
            set { _searchPaths.Text = value; }
        }

        public string Arguments {
            get { return _arguments.Text; }
            set { _arguments.Text = value; }
        }

        public string InterpreterPath {
            get { return _interpreterPath.Text; }
            set { _interpreterPath.Text = value; }
        }

        public bool DebugStandardLibrary {
            get { return _debugStdLib.Checked; }
            set { _debugStdLib.Checked = value; }
        }

        private void RaiseIsDirty() {
            if (!_loadingSettings) {
                var isDirty = DirtyChanged;
                if (isDirty != null) {
                    DirtyChanged(this, DirtyChangedEventArgs.DirtyValue);
                }
            }
        }

        private void RaiseIsSaved() {
            var isDirty = DirtyChanged;
            if (isDirty != null) {
                DirtyChanged(this, DirtyChangedEventArgs.SavedValue);
            }
        }

        private void SearchPathsTextChanged(object sender, EventArgs e) {
            RaiseIsDirty();
        }

        private void ArgumentsTextChanged(object sender, EventArgs e) {
            RaiseIsDirty();
        }

        private void InterpreterPathTextChanged(object sender, EventArgs e) {
            RaiseIsDirty();
        }

        private void DebugStdLibCheckedChanged(object sender, EventArgs e) {
            RaiseIsDirty();
        }
    }
}

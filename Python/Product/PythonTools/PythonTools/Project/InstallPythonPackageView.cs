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
using System.ComponentModel;
using System.Diagnostics;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Provides a view model for the InstallPythonPackage class.
    /// </summary>
    sealed class InstallPythonPackageView : INotifyPropertyChanged {
        private string _name;
        private string _installUsing;
        private bool _installUsingPip, _installUsingEasyInstall;
        private bool _installElevated;
        private bool _isValid;
        private readonly bool _isInsecure;

        private static readonly string[] _installUsingOptions = new[] { "pip", "easy_install" };


        /// <summary>
        /// Create a InstallPythonPackageView with default values.
        /// </summary>
        public InstallPythonPackageView(bool isInsecure) {
            InstallUsing = _installUsingOptions[0];
            _isInsecure = isInsecure;
            PropertyChanged += new PropertyChangedEventHandler(OnPropertyChanged);
        }

        /// <summary>
        /// Gets or sets the name of the Python package to be installed.
        /// </summary>
        public string Name {
            get {
                return _name;
            }
            set {
                if (_name != value) {
                    _name = value;
                    OnPropertyChanged("Name");
                }
            }
        }

        /// <summary>
        /// Gets whether the download may be insecure.
        /// </summary>
        public bool IsInsecure {
            get {
                return _isInsecure;
            }
        }

        /// <summary>
        /// Gets the possible values for InstallUsing.
        /// </summary>
        public IEnumerable<string> InstallUsingOptions {
            get {
                return _installUsingOptions;
            }
        }

        /// <summary>
        /// Gets the current selected tool to install the package with.
        /// </summary>
        public string InstallUsing {
            get {
                return _installUsing;
            }
            set {
                if (_installUsing != value) {
                    _installUsing = value;
                    OnPropertyChanged("InstallUsing");
                    InstallUsingPip = _installUsing == "pip";
                    InstallUsingEasyInstall = _installUsing == "easy_install";
                    if (PythonToolsPackage.Instance != null) {
                        if (InstallUsingPip) {
                            InstallElevated = PythonToolsPackage.Instance.GeneralOptionsPage.ElevatePip;
                        } else if (InstallUsingEasyInstall) {
                            InstallElevated = PythonToolsPackage.Instance.GeneralOptionsPage.ElevateEasyInstall;
                        } else {
                            InstallElevated = false;
                            IsValid = false;
                        }
                    } else {
                        InstallElevated = false;
                    }
                }
            }
        }

        /// <summary>
        /// True if InstallUsing is set to pip.
        /// </summary>
        public bool InstallUsingPip {
            get {
                return _installUsingPip;
            }
            private set {
                if (_installUsingPip != value) {
                    _installUsingPip = value;
                    OnPropertyChanged("InstallUsingPip");
                }
            }
        }

        /// <summary>
        /// True if InstallUsing is set to easy_install.
        /// </summary>
        public bool InstallUsingEasyInstall {
            get {
                return _installUsingEasyInstall;
            }
            private set {
                if (_installUsingEasyInstall != value) {
                    _installUsingEasyInstall = value;
                    OnPropertyChanged("InstallUsingEasyInstall");
                }
            }
        }

        /// <summary>
        /// True if the user wants to elevate to install this package.
        /// </summary>
        public bool InstallElevated {
            get {
                return _installElevated;
            }
            set {
                if (_installElevated != value) {
                    _installElevated = value;
                    OnPropertyChanged("InstallElevated");
                }
            }
        }

        /// <summary>
        /// Receives our own property change events to update IsValid.
        /// </summary>
        void OnPropertyChanged(object sender, PropertyChangedEventArgs e) {
            Debug.Assert(sender == this);

            if (e.PropertyName != "IsValid") {
                IsValid = !String.IsNullOrWhiteSpace(_name);
            }
        }

        /// <summary>
        /// True if the settings are valid and all paths exist; otherwise, false.
        /// </summary>
        public bool IsValid {
            get {
                return _isValid;
            }
            private set {
                if (_isValid != value) {
                    _isValid = value;
                    OnPropertyChanged("IsValid");
                }
            }
        }

        private void OnPropertyChanged(string propertyName) {
            var evt = PropertyChanged;
            if (evt != null) {
                evt(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        /// <summary>
        /// Raised when the value of a property changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
    }
}


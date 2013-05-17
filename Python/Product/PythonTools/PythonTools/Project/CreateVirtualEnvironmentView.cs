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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.ComponentModelHost;

namespace Microsoft.PythonTools {
    /// <summary>
    /// Provides a view model for the StandaloneTarget class.
    /// </summary>
    sealed class CreateVirtualEnvironmentView : INotifyPropertyChanged {
        private ReadOnlyCollection<PythonInterpreterView> _availableInterpreters;
        private string _location, _name;
        private PythonInterpreterView _interpreter;
        private bool _isValid;
        private readonly bool _isCreate;

        /// <summary>
        /// Create a CreateVirtualEnvironmentView with default values.
        /// </summary>
        public CreateVirtualEnvironmentView(bool isCreate) {
            _isCreate = isCreate;
            var componentService = (IComponentModel)(PythonToolsPackage.GetGlobalService(typeof(SComponentModel)));
            var factoryProviders = componentService.GetExtensions<IPythonInterpreterFactoryProvider>();

            var availableInterpreters = new List<PythonInterpreterView>();
            // TODO: Can we filter based upon interpreters w/ virtual env installed?
            foreach (var factoryProvider in factoryProviders) {
                foreach (var factory in factoryProvider.GetInterpreterFactories()) {
                    availableInterpreters.Add(new PythonInterpreterView(factory));
                }
            }
            _availableInterpreters = new ReadOnlyCollection<PythonInterpreterView>(availableInterpreters);

            PropertyChanged += new PropertyChangedEventHandler(OnPropertyChanged);

            if (IsAnyAvailableInterpreters) {
                Interpreter = AvailableInterpreters[0];
            }
        }

        public string Title {
            get {
                return _isCreate ? "Create Virtual Environment" : "Add Virtual Environment";
            }
        }

        public bool IsCreate {
            get {
                return _isCreate;
            }
        }

        public Visibility NameVisibility {
            get {
                return IsCreate ? Visibility.Visible : Visibility.Hidden;
            }
        }

        /// <summary>
        /// The interpreters that may be selected.
        /// </summary>
        public ReadOnlyCollection<PythonInterpreterView> AvailableInterpreters {
            get {
                return _availableInterpreters;
            }
        }

        /// <summary>
        /// True if AvailableInterpreters has at least one item.
        /// </summary>
        public bool IsAnyAvailableInterpreters {
            get {
                return _availableInterpreters.Count > 0;
            }
        }

        /// <summary>
        /// Gets or sets the location of the virtual environment.
        /// 
        /// In Create mode this is the directory where the virtual environment will be created,
        /// in add mode this is the full path to the virtual environment.
        /// </summary>
        public string Location {
            get {
                return _location;
            }
            set {
                if (_location != value) {
                    _location = value;
                    OnPropertyChanged("Location");
                }
            }
        }

        /// <summary>
        /// Gets the name of the newly virtual environment to be created.
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
        /// The currently selected Python interpreter. Setting this to null will select a
        /// custom interpreter.
        /// </summary>
        public PythonInterpreterView Interpreter {
            get {
                return _interpreter;
            }
            set {
                if (_interpreter != value) {
                    _interpreter = value;
                    OnPropertyChanged("Interpreter");
                }
            }
        }

        /// <summary>
        /// Receives our own property change events to update IsValid.
        /// </summary>
        void OnPropertyChanged(object sender, PropertyChangedEventArgs e) {
            Debug.Assert(sender == this);

            if (e.PropertyName != "IsValid") {
                if (IsCreate) {
                    IsValid =
                        IsAnyAvailableInterpreters &&
                        Location != null && Location.IndexOfAny(Path.GetInvalidPathChars()) == -1 &&
                        Name != null && Name.IndexOfAny(Path.GetInvalidFileNameChars()) == -1 &&                        
                        !Directory.Exists(Path.Combine(Location, Name));
                } else {
                    IsValid =
                        IsAnyAvailableInterpreters &&
                        Location != null && Location.IndexOfAny(Path.GetInvalidPathChars()) == -1 &&
                        Directory.Exists(Location) &&
                        (File.Exists(Path.Combine(Location, "Scripts", "python.exe")) ||
                        File.Exists(Path.Combine(Location, "python.exe")));
                }
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
 

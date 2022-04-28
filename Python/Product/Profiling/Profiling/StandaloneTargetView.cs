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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Profiling {
    /// <summary>
    /// Provides a view model for the StandaloneTarget class.
    /// </summary>
    sealed class StandaloneTargetView : INotifyPropertyChanged {
        private readonly ReadOnlyCollection<PythonInterpreterView> _availableInterpreters;
        private readonly PythonInterpreterView _customInterpreter;

        private PythonInterpreterView _interpreter;
        private string _interpreterPath;
        private bool _canSpecifyInterpreterPath;
        private string _workingDirectory;
        private string _scriptPath;
        private string _arguments;

        private bool _isValid;

        public StandaloneTargetView(IServiceProvider serviceProvider) {
            var componentService = serviceProvider.GetComponentModel();
            
            var interpreterProviders = componentService.DefaultExportProvider.GetExports<IPythonInterpreterFactoryProvider, Dictionary<string, object>>();
            var interpreterOptions = componentService.GetService<IInterpreterOptionsService>();
            var registry = componentService.GetService<IInterpreterRegistryService>();
            var pythonService = serviceProvider.GetPythonToolsService();

            var availableInterpreters = registry.Configurations.Select(
                config => new PythonInterpreterView(
                    config.Description, 
                    config.Id, 
                    config.InterpreterPath
                )
            ).ToList();

            _customInterpreter = new CustomPythonInterpreterView();
            availableInterpreters.Add(_customInterpreter);
            _availableInterpreters = new ReadOnlyCollection<PythonInterpreterView>(availableInterpreters);

            _interpreterPath = null;
            _canSpecifyInterpreterPath = false;
            _scriptPath = null;
            _workingDirectory = null;
            _arguments = null;

            _isValid = false;

            PropertyChanged += new PropertyChangedEventHandler(StandaloneTargetView_PropertyChanged);

            if (IsAnyAvailableInterpreters) {
                var defaultId = interpreterOptions.DefaultInterpreterId;
                Interpreter = AvailableInterpreters.FirstOrDefault(v => v.Id == defaultId);
            }
        }

        /// <summary>
        /// Create a StandaloneTargetView with values taken from a template.
        /// </summary>
        public StandaloneTargetView(StandaloneTarget template)
            : this(PythonProfilingPackage.Instance, template) {
        }

        public StandaloneTargetView(IServiceProvider serviceProvider, StandaloneTarget template)
            : this(serviceProvider) {
            if (template.PythonInterpreter != null) {
                if (IsAnyAvailableInterpreters) {
                    Interpreter = AvailableInterpreters
                        .FirstOrDefault(v => v.Id == template.PythonInterpreter.Id);
                } else {
                    Interpreter = _customInterpreter;
                }
            } else {
                InterpreterPath = template.InterpreterPath;
            }
            ScriptPath = template.Script;
            WorkingDirectory = template.WorkingDirectory;
            Arguments = template.Arguments;
        }

        /// <summary>
        /// Returns a StandaloneTarget with values taken from the view model.
        /// </summary>
        /// <returns></returns>
        public StandaloneTarget GetTarget() {
            if (IsValid) {
                return new StandaloneTarget {
                    PythonInterpreter = CanSpecifyInterpreterPath ? null : Interpreter.GetInterpreter(),
                    InterpreterPath = CanSpecifyInterpreterPath ? InterpreterPath : null,
                    Script = ScriptPath ?? string.Empty,
                    WorkingDirectory = WorkingDirectory ?? string.Empty,
                    Arguments = Arguments ?? string.Empty
                };
            } else {
                return null;
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
        /// The currently selected Python interpreter. Setting this to null will select a
        /// custom interpreter.
        /// </summary>
        public PythonInterpreterView Interpreter {
            get {
                return _interpreter;
            }
            set {
                if (_interpreter != value) {
                    _interpreter = value ?? _customInterpreter;
                    OnPropertyChanged("Interpreter");
                    CanSpecifyInterpreterPath = (_interpreter is CustomPythonInterpreterView);
                }
            }
        }

        /// <summary>
        /// The current interpreter path. This can be set regardless of the value of
        /// CanSpecifyInterpreterPath.
        /// </summary>
        public string InterpreterPath {
            get {
                return _interpreterPath;
            }
            set {
                if (_interpreterPath != value) {
                    _interpreterPath = value;
                    if (Interpreter is CustomPythonInterpreterView) {
                        Interpreter = new CustomPythonInterpreterView(_interpreterPath);
                    }
                    OnPropertyChanged("InterpreterPath");
                }
            }
        }

        /// <summary>
        /// True if InterpreterPath is valid; false if it will be ignored.
        /// </summary>
        public bool CanSpecifyInterpreterPath {
            get {
                return _canSpecifyInterpreterPath;
            }
            private set {
                if (_canSpecifyInterpreterPath != value) {
                    _canSpecifyInterpreterPath = value;
                    OnPropertyChanged("CanSpecifyInterpreterPath");
                }
            }
        }

        /// <summary>
        /// The current script path.
        /// </summary>
        public string ScriptPath {
            get {
                return _scriptPath;
            }
            set {
                if (_scriptPath != value) {
                    _scriptPath = value;
                    OnPropertyChanged("ScriptPath");
                    //if (string.IsNullOrEmpty(WorkingDirectory)) {
                    //    WorkingDirectory = Path.GetDirectoryName(_scriptPath);
                    //}
                }
            }
        }

        /// <summary>
        /// The current working directory.
        /// </summary>
        public string WorkingDirectory {
            get {
                return _workingDirectory;
            }
            set {
                if (_workingDirectory != value) {
                    _workingDirectory = value;
                    OnPropertyChanged("WorkingDirectory");
                }
            }
        }

        /// <summary>
        /// The current set of arguments to pass to the script.
        /// </summary>
        public string Arguments {
            get {
                return _arguments;
            }
            set {
                if (_arguments != value) {
                    _arguments = value;
                    OnPropertyChanged("Arguments");
                }
            }
        }

        /// <summary>
        /// Receives our own property change events to update IsValid.
        /// </summary>
        void StandaloneTargetView_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            Debug.Assert(sender == this);

            if (e.PropertyName != "IsValid") {
                IsValid = File.Exists(ScriptPath) &&
                    Directory.Exists(WorkingDirectory) &&
                    (CanSpecifyInterpreterPath == false || File.Exists(InterpreterPath));
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
 

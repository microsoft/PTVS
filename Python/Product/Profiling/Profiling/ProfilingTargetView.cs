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
using System.Linq;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Profiling {
    /// <summary>
    /// Provides a view model for the ProfilingTarget class.
    /// </summary>
    sealed class ProfilingTargetView : INotifyPropertyChanged {
        private readonly ReadOnlyCollection<ProjectTargetView> _availableProjects;
        
        private ProjectTargetView _project;
        private bool _isProjectSelected, _isStandaloneSelected;
        private StandaloneTargetView _standalone;
        private readonly string _startText;

        private bool _isValid;
        
        /// <summary>
        /// Create a ProfilingTargetView with default values.
        /// </summary>
        public ProfilingTargetView(IServiceProvider serviceProvider) {
            var solution = serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution;
            
            var availableProjects = new List<ProjectTargetView>();
            foreach (var project in solution.EnumerateLoadedProjects()) {
                availableProjects.Add(new ProjectTargetView((IVsHierarchy)project));
            }
            _availableProjects = new ReadOnlyCollection<ProjectTargetView>(availableProjects);

            _project = null;
            _standalone = new StandaloneTargetView(serviceProvider);
            _isProjectSelected = true;

            _isValid = false;

            PropertyChanged += new PropertyChangedEventHandler(ProfilingTargetView_PropertyChanged);
            _standalone.PropertyChanged += new PropertyChangedEventHandler(Standalone_PropertyChanged);

            var startupProject = PythonProfilingPackage.GetStartupProjectGuid(serviceProvider);
            Project = AvailableProjects.FirstOrDefault(p => p.Guid == startupProject) ??
                AvailableProjects.FirstOrDefault();
            if (Project != null) {
                IsStandaloneSelected = false;
                IsProjectSelected = true;
            } else {
                IsProjectSelected = false;
                IsStandaloneSelected = true;
            }
            _startText = Strings.LaunchProfiling_OK;
        }

        /// <summary>
        /// Create a ProfilingTargetView with values taken from a template.
        /// </summary>
        public ProfilingTargetView(IServiceProvider serviceProvider, ProfilingTarget template)
            : this(serviceProvider) {
            if (template.ProjectTarget != null) {
                Project = new ProjectTargetView(template.ProjectTarget);
                IsStandaloneSelected = false;
                IsProjectSelected = true;
            } else if (template.StandaloneTarget != null) {
                Standalone = new StandaloneTargetView(serviceProvider, template.StandaloneTarget);
                IsProjectSelected = false;
                IsStandaloneSelected = true;
            }
            _startText = Strings.LaunchProfiling_OK;
        }

        /// <summary>
        /// Returns a ProfilingTarget with the values set from the view model.
        /// </summary>
        public ProfilingTarget GetTarget() {
            if (IsValid) {
                return new ProfilingTarget {
                    ProjectTarget = IsProjectSelected ? Project.GetTarget() : null,
                    StandaloneTarget = IsStandaloneSelected ? Standalone.GetTarget() : null,
                };
            } else {
                return null;
            }
        }


        public ReadOnlyCollection<ProjectTargetView> AvailableProjects {
            get {
                return _availableProjects;
            }
        }

        /// <summary>
        /// True if AvailableProjects has at least one item.
        /// </summary>
        public bool IsAnyAvailableProjects {
            get {
                return _availableProjects.Count > 0;
            }
        }

        /// <summary>
        /// A view of the details of the current project.
        /// </summary>
        public ProjectTargetView Project {
            get {
                return _project;
            }
            set {
                if (_project != value) {
                    _project = value;
                    OnPropertyChanged("Project");
                }
            }
        }

        /// <summary>
        /// True if a project is the currently selected target; otherwise, false.
        /// </summary>
        public bool IsProjectSelected {
            get {
                return _isProjectSelected;
            }
            set {
                if (_isProjectSelected != value) {
                    _isProjectSelected = value;
                    OnPropertyChanged("IsProjectSelected");
                }
            }
        }

        /// <summary>
        /// A view of the details of the current standalone script.
        /// </summary>
        public StandaloneTargetView Standalone {
            get {
                return _standalone;
            }
            set {
                if (_standalone != value) {
                    if (_standalone != null) {
                        _standalone.PropertyChanged -= Standalone_PropertyChanged;
                    }
                    _standalone = value;
                    if (_standalone != null) {
                        _standalone.PropertyChanged += Standalone_PropertyChanged;
                    }

                    OnPropertyChanged("Standalone");
                }
            }
        }

        /// <summary>
        /// True if a standalone script is the currently selected target; otherwise, false.
        /// </summary>
        public bool IsStandaloneSelected {
            get {
                return _isStandaloneSelected;
            }
            set {
                if (_isStandaloneSelected != value) {
                    _isStandaloneSelected = value;
                    OnPropertyChanged("IsStandaloneSelected");
                }
            }
        }

        /// <summary>
        /// Receives our own property change events to update IsValid.
        /// </summary>
        void ProfilingTargetView_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            Debug.Assert(sender == this);

            if (e.PropertyName != "IsValid") {
                IsValid = (IsProjectSelected != IsStandaloneSelected) &&
                    (IsProjectSelected ?
                        Project != null :
                        (Standalone != null && Standalone.IsValid));
            }
        }

        /// <summary>
        /// Propagate property change events from Standalone.
        /// </summary>
        void Standalone_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            Debug.Assert(Standalone == sender);
            OnPropertyChanged("Standalone");
        }


        /// <summary>
        /// True if all settings are valid; otherwise, false.
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

        public string StartText {
            get {
                return _startText;
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
 

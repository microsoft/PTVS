/* ****************************************************************************
 *
 * Copyright (c) Steve Dower (Zooba)
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

namespace Microsoft.PythonTools.Profiling {
    /// <summary>
    /// Provides a view model for the ProfilingTarget class.
    /// </summary>
    public sealed class ProfilingTargetView : INotifyPropertyChanged {
        private ReadOnlyCollection<ProjectTargetView> _availableProjects;
        
        private ProjectTargetView _project;
        private bool _isProjectSelected;
        private StandaloneTargetView _standalone;

        private bool _isValid;
        
        /// <summary>
        /// Create a ProfilingTargetView with default values.
        /// </summary>
        public ProfilingTargetView() {
            var dteService = (EnvDTE.DTE)(PythonProfilingPackage.GetGlobalService(typeof(EnvDTE.DTE)));

            var availableProjects = new List<ProjectTargetView>();
            foreach (EnvDTE.Project project in dteService.Solution.Projects) {
                var kind = project.Kind;
                if (String.Equals(kind, PythonProfilingPackage.PythonProjectGuid, StringComparison.OrdinalIgnoreCase)) {
                    availableProjects.Add(new ProjectTargetView(project));
                }
            }
            _availableProjects = new ReadOnlyCollection<ProjectTargetView>(availableProjects);

            _project = null;
            _standalone = new StandaloneTargetView();
            _isProjectSelected = true;

            _isValid = false;

            PropertyChanged += new PropertyChangedEventHandler(ProfilingTargetView_PropertyChanged);
            _standalone.PropertyChanged += new PropertyChangedEventHandler(Standalone_PropertyChanged);

            if (IsAnyAvailableProjects) {
                Project = AvailableProjects[0];
            } else {
                IsStandaloneSelected = true;
            }
        }

        /// <summary>
        /// Create a ProfilingTargetView with values taken from a template.
        /// </summary>
        /// <param name="template"></param>
        public ProfilingTargetView(ProfilingTarget template)
            : this() {
            if (template.ProjectTarget != null) {
                Project = new ProjectTargetView(template.ProjectTarget);
                IsProjectSelected = true;
            } else if (template.StandaloneTarget != null) {
                Standalone = new StandaloneTargetView(template.StandaloneTarget);
                IsStandaloneSelected = true;
            }
        }

        /// <summary>
        /// Returns a ProfilingTarget with the values set from the view model.
        /// </summary>
        public ProfilingTarget GetTarget() {
            if (IsValid) {
                return new ProfilingTarget {
                    ProjectTarget = IsProjectSelected ? Project.GetTarget() : null,
                    StandaloneTarget = IsStandaloneSelected ? Standalone.GetTarget() : null
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
                    OnPropertyChanged("IsStandaloneSelected");
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
                return !IsProjectSelected;
            }
            set {
                IsProjectSelected = !value;
            }
        }


        /// <summary>
        /// Receives our own property change events to update IsValid.
        /// </summary>
        void ProfilingTargetView_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            Debug.Assert(sender == this);

            if (e.PropertyName != "IsValid") {
                IsValid = (IsProjectSelected && Project != null) ||
                    (IsStandaloneSelected && Standalone != null && Standalone.IsValid);
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
 

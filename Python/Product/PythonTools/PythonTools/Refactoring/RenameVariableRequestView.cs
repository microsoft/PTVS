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

using System.ComponentModel;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Refactoring {
    /// <summary>
    /// Provides a view model for the RenameVariableRequest class.
    /// </summary>
    sealed class RenameVariableRequestView : INotifyPropertyChanged {
        private readonly string _originalName;
        private readonly PythonLanguageVersion _languageVersion;

        private string _name;
        private bool _isValid;

        private bool _previewChanges;
        private bool _searchInComments;
        private bool _searchInStrings;

        /// <summary>
        /// Create a RenameVariableRequestView with default values.
        /// </summary>
        public RenameVariableRequestView(string originalName, PythonLanguageVersion languageVersion) {
            _originalName = originalName;
            _languageVersion = languageVersion;
            //_name = null;

            // Access properties rather than underlying variables to ensure dependent properties
            // are also updated.
            Name = _originalName;
            _previewChanges = true;
        }

        /// <summary>
        /// Create a RenameVariableRequestView with values taken from a template.
        /// </summary>
        public RenameVariableRequestView(
            string originalName,
            PythonLanguageVersion languageVersion,
            RenameVariableRequest template
        )
            : this(originalName, languageVersion) {
            // Access properties rather than underlying variables to ensure dependent properties
            // are also updated.
            Name = template.Name;
        }

        /// <summary>
        /// Returns a RenameVariableRequest with the values set from the view model.
        /// </summary>
        public RenameVariableRequest GetRequest() {
            if (IsValid) {
                return new RenameVariableRequest(Name, PreviewChanges, SearchInComments, SearchInStrings);
            } else {
                return null;
            }
        }

        /// <summary>
        /// The new name for the variable.
        /// </summary>
        public string Name {
            get {
                return _name;
            }
            set {
                if (_name != value) {
                    _name = value;
                    OnPropertyChanged("Name");
                    IsValid = !_originalName.Equals(_name) && ExtractMethodRequestView.IsValidPythonIdentifier(_name, _languageVersion);
                }
            }
        }

        /// <summary>
        /// True if the name is a valid Python name; otherwise, false.
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

        /// <summary>
        /// True to show a list of changes before they are made; false to make changes without confirmation.
        /// </summary>
        public bool PreviewChanges {
            get {
                return _previewChanges;
            }
            set {
                if (_previewChanges != value) {
                    _previewChanges = value;
                    OnPropertyChanged("PreviewChanges");
                }
            }
        }

        /// <summary>
        /// True to change references in comments; false to ignore comments.
        /// </summary>
        public bool SearchInComments {
            get {
                return _searchInComments;
            }
            set {
                if (_searchInComments != value) {
                    _searchInComments = value;
                    OnPropertyChanged("SearchInComments");
                }
            }
        }

        /// <summary>
        /// True to change references in strings; false to ignore strings.
        /// </summary>
        public bool SearchInStrings {
            get {
                return _searchInStrings;
            }
            set {
                if (_searchInStrings != value) {
                    _searchInStrings = value;
                    OnPropertyChanged("SearchInStrings");
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


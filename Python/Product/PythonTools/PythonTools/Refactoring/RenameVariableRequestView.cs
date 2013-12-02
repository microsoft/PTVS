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
using System.ComponentModel;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Refactoring {
    /// <summary>
    /// Provides a view model for the RenameVariableRequest class.
    /// </summary>
    sealed class RenameVariableRequestView : INotifyPropertyChanged {
        private readonly string _originalName;
        private readonly PythonLanguageVersion _languageVersion;
        internal static readonly Regex _validNameRegex = ExtractMethodRequestView._validNameRegex;

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
                    IsValid =
                        !_originalName.Equals(_name) &&
                        _validNameRegex.IsMatch(_name) &&
                        !PythonKeywords.IsKeyword(_name, _languageVersion);
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
 

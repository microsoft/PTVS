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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio;

namespace Microsoft.PythonTools.Refactoring {
    using AP = AnalysisProtocol;

    /// <summary>
    /// Provides a view model for the ExtractMethodRequest class.
    /// </summary>
    sealed class ExtractMethodRequestView : INotifyPropertyChanged {
        private readonly ExtractedMethodCreator _previewer;
        private static readonly Regex Python2IdentifierRegex = new Regex("^[a-zA-Z_][a-zA-Z0-9_]*$");

        private const string _defaultName = "method_name";
        private string _name;
        private readonly FontFamily _previewFontFamily;
        private bool _isValid;

        private readonly ReadOnlyCollection<ScopeWrapper> _targetScopes;
        private readonly ScopeWrapper _defaultScope;
        private readonly IServiceProvider _serviceProvider;
        private ScopeWrapper _targetScope;
        private ReadOnlyCollection<ClosureVariable> _closureVariables;

        private string _previewText;

        /// <summary>
        /// Create an ExtractMethodRequestView with default values.
        /// </summary>
        public ExtractMethodRequestView(IServiceProvider serviceProvider, ExtractedMethodCreator previewer) {
            _previewer = previewer;
            _serviceProvider = serviceProvider;

            var extraction = _previewer.LastExtraction;

            AP.ScopeInfo lastClass = null;
            for (int i = extraction.scopes.Length - 1; i >= 0; i--) {
                if (extraction.scopes[i].type == "class") {
                    lastClass = extraction.scopes[i];
                    break;
                }
            }

            var targetScopes = new List<ScopeWrapper>();
            foreach (var scope in extraction.scopes) {
                if (!(scope.type == "class") || scope == lastClass) {
                    var wrapper = new ScopeWrapper(scope);
                    if (scope == lastClass) {
                        _defaultScope = wrapper;
                    }
                    targetScopes.Add(wrapper);
                }
            }

            _targetScopes = new ReadOnlyCollection<ScopeWrapper>(targetScopes);
            if (_defaultScope == null && _targetScopes.Any()) {
                _defaultScope = _targetScopes[0];
            }

            _previewFontFamily = new FontFamily(GetTextEditorFont());

            PropertyChanged += ExtractMethodRequestView_PropertyChanged;

            // Access properties rather than underlying variables to ensure dependent properties
            // are also updated.
            Name = _defaultName;
            TargetScope = _defaultScope;
        }

        /// <summary>
        /// Create an ExtractMethodRequestView with values taken from template.
        /// </summary>
        public ExtractMethodRequestView(IServiceProvider serviceProvider, ExtractedMethodCreator previewer, ExtractMethodRequest template)
            : this(serviceProvider, previewer) {
            // Access properties rather than underlying variables to ensure dependent properties
            // are also updated.
            Name = template.Name;
            TargetScope = template.TargetScope;
            foreach (var cv in ClosureVariables) {
                cv.IsClosure = !template.Parameters.Contains(cv.Name);
            }
        }

        /// <summary>
        /// Returns an ExtractMethodRequestView with the values set from the view model.
        /// </summary>
        public ExtractMethodRequest GetRequest() {
            if (IsValid) {
                string[] parameters;
                if (ClosureVariables != null) {
                    parameters = ClosureVariables.Where(cv => !cv.IsClosure).Select(cv => cv.Name).ToArray();
                } else {
                    parameters = new string[0];
                }
                return new ExtractMethodRequest(TargetScope ?? _defaultScope,
                    Name ?? _defaultName,
                    parameters);
            } else {
                return null;
            }
        }

        /// <summary>
        /// The name of the new method which should be created
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
        /// The font family to display preview text using.
        /// </summary>
        public FontFamily PreviewFontFamily {
            get {
                return _previewFontFamily;
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
        /// The target scope to extract the method to.
        /// </summary>
        public ScopeWrapper TargetScope {
            get {
                return _targetScope;
            }
            set {
                if (_targetScope != value) {
                    _targetScope = value;
                    OnPropertyChanged("TargetScope");

                    List<ClosureVariable> closureVariables = new List<ClosureVariable>();
                    if (_targetScope != null) {
                        foreach (var variable in _previewer.LastExtraction.variables) {
                            if (_targetScope.Scope.variables.Contains(variable)) {
                                // we can either close over or pass these in as parameters, add them to the list
                                closureVariables.Add(new ClosureVariable(variable));
                            }
                        }

                        closureVariables.Sort();
                    }
                    ClosureVariables = new ReadOnlyCollection<ClosureVariable>(closureVariables);
                }
            }
        }

        /// <summary>
        /// The set of potential scopes to extract the method to.
        /// </summary>
        public ReadOnlyCollection<ScopeWrapper> TargetScopes {
            get {
                return _targetScopes;
            }
        }

        /// <summary>
        /// The list of closure/parameter settings for the current TargetScope.
        /// </summary>
        public ReadOnlyCollection<ClosureVariable> ClosureVariables {
            get {
                return _closureVariables;
            }
            private set {
                if (_closureVariables != value) {
                    if (_closureVariables != null) {
                        foreach (var cv in _closureVariables) {
                            cv.PropertyChanged -= ClosureVariable_PropertyChanged;
                        }
                    }

                    _closureVariables = value;

                    if (_closureVariables != null) {
                        foreach (var cv in ClosureVariables) {
                            cv.PropertyChanged += ClosureVariable_PropertyChanged;
                        }
                    }

                    OnPropertyChanged("ClosureVariables");
                    UpdatePreview();
                }
            }
        }

        /// <summary>
        /// Receives our own property change events to update IsValid.
        /// </summary>
        void ExtractMethodRequestView_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName != "IsValid") {
                IsValid = (TargetScope != null) && IsValidPythonIdentifier(Name, _previewer.PythonVersion);
            }
            if (e.PropertyName != "PreviewText") {
                UpdatePreview();
            }
        }

        internal static bool IsValidPythonIdentifier(string identifier, PythonLanguageVersion pythonVersion) {
            if (String.IsNullOrEmpty(identifier) || PythonKeywords.IsKeyword(identifier, pythonVersion)) {
                return false;
            }

            //Python2 identifiers are only certain ASCII characters
            if (pythonVersion < PythonLanguageVersion.V30) {
                return Python2IdentifierRegex.IsMatch(identifier);
            }

            //Python3 identifiers can include unicode characters
            if (!Tokenizer.IsIdentifierStartChar(identifier[0])) {
                return false;
            }

            return identifier.Skip(1).All(Tokenizer.IsIdentifierChar);
        }

        /// <summary>
        /// Propagate property change events from ClosureVariable.
        /// </summary>
        void ClosureVariable_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            OnPropertyChanged("ClosureVariables");
        }

        /// <summary>
        /// Updates PreviewText based on the current settings.
        /// </summary>
        private void UpdatePreview() {
            var info = GetRequest();
            if (info != null) {
                UpdatePreviewAsync(info).DoNotWait();
            } else {
                PreviewText = Strings.ExtractMethod_InvalidMethodName;
            }
        }

        private async Task UpdatePreviewAsync(ExtractMethodRequest info) {
            try {
                var response = await _previewer.GetExtractionResult(info);
                PreviewText = response.methodBody;
            } catch (Exception ex) {
                Debug.Fail(ex.ToUnhandledExceptionMessage(GetType()));
                PreviewText = Strings.ExtractMethod_FailedToGetPreview;
            }
        }

        /// <summary>
        /// An example of how the extracted method will appear.
        /// </summary>
        public string PreviewText {
            get {
                return _previewText;
            }
            private set {
                if (_previewText != value) {
                    _previewText = value;
                    OnPropertyChanged("PreviewText");
                }
            }
        }

        /// <summary>
        /// Returns the name of the font set by the user for editor windows.
        /// </summary>
        private string GetTextEditorFont() {
            try {
                var store = (IVsFontAndColorStorage)_serviceProvider.GetService(typeof(SVsFontAndColorStorage));
                Guid textEditorCategory = new Guid(FontsAndColorsCategory.TextEditor);
                if (store != null && store.OpenCategory(ref textEditorCategory,
                    (uint)(__FCSTORAGEFLAGS.FCSF_LOADDEFAULTS | __FCSTORAGEFLAGS.FCSF_READONLY)) == VSConstants.S_OK) {
                    try {
                        FontInfo[] info = new FontInfo[1];
                        store.GetFont(null, info);
                        if (info[0].bstrFaceName != null) {
                            return info[0].bstrFaceName;
                        }
                    } finally {
                        store.CloseCategory();
                    }
                }
            } catch { }

            return "Consolas";
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

        /// <summary>
        /// Provides a view model for the closure/parameter state of a variable.
        /// </summary>
        public sealed class ClosureVariable : INotifyPropertyChanged, IComparable<ClosureVariable> {
            private readonly string _name;
            private readonly string _displayName;
            private bool _isClosure;

            public ClosureVariable(string name) {
                _name = name;
                _displayName = name.Replace("_", "__");
                _isClosure = true;
            }

            /// <summary>
            /// The name of the variable.
            /// </summary>
            public string Name {
                get { return _name; }
            }

            /// <summary>
            /// The name of the variable with
            /// </summary>
            public string DisplayName {
                get { return _displayName; }
            }

            /// <summary>
            /// True to close over the variable; otherwise, false to pass it as a parameter.
            /// </summary>
            public bool IsClosure {
                get { return _isClosure; }
                set {
                    if (_isClosure != value) {
                        _isClosure = value;
                        OnPropertyChanged("IsClosure");
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

            /// <summary>
            /// Compares two ClosureVariable instances by name.
            /// </summary>
            public int CompareTo(ClosureVariable other) {
                return string.CompareOrdinal(Name, other?.Name ?? "");
            }
        }
    }

    class ScopeWrapper {
        public readonly AP.ScopeInfo Scope;

        public ScopeWrapper(AP.ScopeInfo scope) {
            Scope = scope;
        }

        public string Name {
            get {
                return Scope.name;
            }
        }
    }
}


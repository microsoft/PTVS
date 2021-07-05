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

namespace Microsoft.PythonTools.Django.Project {
    class NewAppDialogViewModel : INotifyPropertyChanged {
        private string _name;
        private bool _isValid;
        private static readonly Regex _validNameRegex = new Regex("^[a-zA-Z_][a-zA-Z0-9_]*$");

        public string Name {
            get {
                return _name;
            }
            set {
                if (_name != value) {
                    _name = value;
                    OnPropertyChanged("Name");
                    IsValid = _validNameRegex.IsMatch(_name);
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

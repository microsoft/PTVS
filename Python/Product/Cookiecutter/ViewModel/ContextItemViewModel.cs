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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.CookiecutterTools.Model;

namespace Microsoft.CookiecutterTools.ViewModel {
    class ContextItemViewModel : INotifyPropertyChanged {
        private string _name;
        private string _selector;
        private string _label;
        private string _description;
        private string _url;
        private string _val;
        private string _default;
        private List<string> _items;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Constructor for design view.
        /// </summary>
        public ContextItemViewModel() :
            this(null, Selectors.String, null, null, null, null, null) {
        }

        public ContextItemViewModel(string name, string selector, string label, string description, string url, string defaultValue, string[] items = null) {
            _name = name;
            _selector = selector;
            _label = !string.IsNullOrEmpty(label) ? label : name;
            _description = !string.IsNullOrEmpty(description) ? description : defaultValue;
            _url = url;
            _val = string.Empty;
            _default = defaultValue;
            _items = new List<string>();
            if (items != null) {
                _items.AddRange(items);
            }
        }

        public string Name {
            get {
                return _name;
            }

            set {
                _name = value;
            }
        }

        public string Selector {
            get {
                return _selector;
            }

            set {
                if (value != _selector) {
                    _selector = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selector)));
                }
            }
        }

        public string Label {
            get {
                return _label;
            }

            set {
                if (value != _label) {
                    _label = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
                }
            }
        }

        public string Description {
            get {
                return _description;
            }

            set {
                if (value != _description) {
                    _description = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
                }
            }
        }

        public string Url {
            get {
                return _url;
            }

            set {
                if (value != _url) {
                    _url = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Url)));
                }
            }
        }

        public string Val {
            get {
                return _val;
            }

            set {
                if (value != _val) {
                    _val = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Val)));
                }
            }
        }

        public string Default {
            get {
                return _default;
            }

            set {
                if (value != _default) {
                    _default = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Default)));
                }
            }
        }

        public List<string> Items {
            get {
                return _items;
            }
        }
    }
}

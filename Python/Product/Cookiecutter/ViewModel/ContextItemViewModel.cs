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
        private bool _visible;
        private readonly List<string> _items;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Constructor for design view.
        /// </summary>
        public ContextItemViewModel() :
            this(string.Empty, Selectors.String, string.Empty, string.Empty, string.Empty, string.Empty, true, null) {
        }

        public ContextItemViewModel(string name, string selector, string label, string description, string url, string defaultValue, bool visible = true, string[] items = null) {
            _name = name;
            _selector = selector;
            _label = !string.IsNullOrEmpty(label) ? label : name;
            _description = !string.IsNullOrEmpty(description) ? description : defaultValue;
            _url = url;
            _val = string.Empty;
            _default = defaultValue;
            _visible = visible;
            _items = new List<string>();
            if (items != null && items.Length > 0) {
                _items.AddRange(items);
            }

            // These selectors don't have a way of showing the default value (watermark)
            // when no value is set (and there's no way to unset the value once it is set).
            // So we'll always start with the value set to default.
            if (selector == Selectors.YesNo || selector == Selectors.List) {
                _val = _default;
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
                return _url ?? string.Empty;
            }

            set {
                if (value != _url) {
                    _url = value ?? string.Empty;
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

        public bool Visible {
            get {
                return _visible;
            }

            set {
                if (value != _visible) {
                    _visible = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Visible)));
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

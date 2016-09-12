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

namespace Microsoft.CookiecutterTools.ViewModel {
    class ContextItemViewModel : INotifyPropertyChanged {
        private string _name;
        private string _val;
        private string _default;
        private List<string> _items;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Constructor for design view.
        /// </summary>
        public ContextItemViewModel() :
            this(null, null, null) {
        }

        public ContextItemViewModel(string name, string defaultValue, string[] items = null) {
            _name = name;
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

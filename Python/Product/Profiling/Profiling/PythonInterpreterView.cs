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

namespace Microsoft.PythonTools.Profiling {
    /// <summary>
    /// Provides a view model for the PythonInterpreter class.
    /// </summary>
    class PythonInterpreterView {
        readonly string _name;
        readonly string _id;
        readonly string _path;

        /// <summary>
        /// Create a PythonInterpreterView with values from parameters.
        /// </summary>
        public PythonInterpreterView(string name, string id, string path) {
            _name = name;
            _id = id;
            _path = path;
        }

        /// <summary>
        /// Returns a PythonInterpreter with the values from the model view.
        /// </summary>
        /// <returns></returns>
        public PythonInterpreter GetInterpreter() {
            return new PythonInterpreter {
                Id = Id
            };
        }

        /// <summary>
        /// The display name of the interpreter, if available.
        /// </summary>
        public string Name {
            get {
                return _name;
            }
        }

        /// <summary>
        /// The Guid identifying the interpreter.
        /// </summary>
        public string Id {
            get {
                return _id;
            }
        }

        /// <summary>
        /// The path to the interpreter, if available.
        /// </summary>
        public string Path {
            get {
                return _path;
            }
        }

        public override string ToString() {
            return Name;
        }

        public override bool Equals(object obj) {
            var other = obj as PythonInterpreterView;
            if (other == null) {
                return false;
            } else {
                return Id.Equals(other.Id);
            }
        }

        public override int GetHashCode() {
            return Id.GetHashCode();
        }
    }
}


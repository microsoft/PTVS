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

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Provides information about a value exported from a module.
    /// </summary>
    public struct ExportedMemberInfo {
        private readonly string _fromName, _name;
        
        public ExportedMemberInfo(string fromName, string name) {
            _fromName = fromName;
            _name = name;
        }

        /// <summary>
        /// The name of the value being exported, fully qualified with the
        /// module/package name.
        /// </summary>
        public string Name {
            get {
                if (string.IsNullOrEmpty(_fromName)) {
                    return _name;
                } else {
                    return _fromName + "." + _name;
                }
            }
        }

        /// <summary>
        /// The name of the member or module that can be imported.
        /// </summary>
        public string ImportName {
            get {
                return _name;
            }
        }

        /// <summary>
        /// The name of the module it is imported from, if applicable.
        /// </summary>
        public string FromName {
            get {
                return _fromName;
            }
        }
    }
}

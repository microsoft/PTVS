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

using System.IO;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Debugger {
    class PythonModule {
        private readonly int _moduleId;
        private readonly string _filename;

        public PythonModule(int moduleId, string filename) {
            _moduleId = moduleId;
            _filename = filename;
        }

        public int ModuleId {
            get {
                return _moduleId;
            }
        }

        public string Name {
            get {
                
                if (CommonUtils.IsValidPath(_filename)) {
                    return Path.GetFileNameWithoutExtension(_filename);
                }
                return _filename;
            }
        }

        public string Filename {
            get {
                return _filename;
            }
        }
    }
}

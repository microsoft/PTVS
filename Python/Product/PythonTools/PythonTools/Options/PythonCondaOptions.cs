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

namespace Microsoft.PythonTools.Options {
    public sealed class PythonCondaOptions {
        private readonly PythonToolsService _service;

        private const string Category = "Conda";

        private const string CustomCondaExecutablePathSetting = "CustomCondaExecutablePath";

        internal PythonCondaOptions(PythonToolsService service) {
            _service = service;
            Load();
        }

        public void Load() {
            CustomCondaExecutablePath = _service.LoadString(CustomCondaExecutablePathSetting, Category) ?? "";
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Save() {
            _service.SaveString(CustomCondaExecutablePathSetting, Category, CustomCondaExecutablePath);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Reset() {
            CustomCondaExecutablePath = string.Empty;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler Changed;

        /// <summary>
        /// Path to the conda executable to use to manage conda environments.
        /// </summary>
        public string CustomCondaExecutablePath {
            get;
            set;
        }
    }
}

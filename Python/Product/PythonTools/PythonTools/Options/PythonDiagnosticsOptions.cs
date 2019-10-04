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
    public sealed class PythonDiagnosticsOptions {
        private readonly PythonToolsService _service;

        private const string Category = "Diagnostics";

        private const string IncludeAnalysisLogsSetting = "IncludeAnalysisLogs";

        internal PythonDiagnosticsOptions(PythonToolsService service) {
            _service = service;
            Load();
        }

        public void Load() {
            IncludeAnalysisLogs = _service.LoadBool(IncludeAnalysisLogsSetting, Category) ?? true;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Save() {
            _service.SaveBool(IncludeAnalysisLogsSetting, Category, IncludeAnalysisLogs);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Reset() {
            IncludeAnalysisLogs = true;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler Changed;

        /// <summary>
        /// True to pause at the end of execution when an error occurs. Default
        /// is true.
        /// </summary>
        public bool IncludeAnalysisLogs {
            get;
            set;
        }
    }
}

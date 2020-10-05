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
using Microsoft.PythonTools.LanguageServerClient;

namespace Microsoft.PythonTools.Options {
    public sealed class PythonAnalysisOptions {
        private const string Category = "Analysis";
        private readonly PythonToolsService _service;

        public bool AutoSearchPaths { get; set; }
        public string DiagnosticMode { get; set; }
        public string LogLevel { get; set; }
        public string StubPath { get; set; }
        public string TypeCheckingMode { get; set; }
        public string[] TypeshedPaths { get; set; }
        public bool UseLibraryCodeForTypes { get; set; }

        internal PythonAnalysisOptions(PythonToolsService service) {
            _service = service;
            Load();
        }

        public void Load() {
            AutoSearchPaths = _service.LoadBool(nameof(AutoSearchPaths), Category) ?? true;
            DiagnosticMode = _service.LoadString(nameof(DiagnosticMode), Category) ?? PythonLanguageClient.DiagnosticMode.OpenFilesOnly;
            LogLevel = _service.LoadString(nameof(LogLevel), Category) ?? PythonLanguageClient.LogLevel.Information;
            StubPath = _service.LoadString(nameof(StubPath), Category);
            TypeCheckingMode = _service.LoadString(nameof(TypeCheckingMode), Category) ?? PythonLanguageClient.TypeCheckingMode.Basic;
            TypeshedPaths = _service.LoadString(nameof(TypeshedPaths), Category)?.Split(';');
            UseLibraryCodeForTypes = _service.LoadBool(nameof(UseLibraryCodeForTypes), Category) ?? true;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Save() {
            _service.SaveBool(nameof(AutoSearchPaths), Category, AutoSearchPaths);
            _service.SaveString(nameof(DiagnosticMode), Category, DiagnosticMode);
            _service.SaveString(nameof(LogLevel), Category, LogLevel);
            _service.SaveString(nameof(StubPath), Category, StubPath);
            _service.SaveString(nameof(TypeCheckingMode), Category, TypeCheckingMode);
            _service.SaveString(nameof(TypeshedPaths), Category, TypeshedPaths != null ? string.Join(";", TypeshedPaths) : null);
            _service.SaveBool(nameof(UseLibraryCodeForTypes), Category, UseLibraryCodeForTypes);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Reset() {
            AutoSearchPaths = true;
            DiagnosticMode = PythonLanguageClient.DiagnosticMode.OpenFilesOnly;
            LogLevel = PythonLanguageClient.LogLevel.Information;
            StubPath = null;
            TypeCheckingMode = PythonLanguageClient.TypeCheckingMode.Basic;
            TypeshedPaths = null;
            UseLibraryCodeForTypes = true;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler Changed;
    }
}
